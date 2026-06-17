using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace LinuxImeFix;

[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        Harmony harmony = new("com.RunningBugs.LinuxImeFix");
        harmony.PatchAll();

        if (LinuxImeUtility.IsLinux)
        {
            NativeBridge.Load();
            Log.Message("[LinuxImeFix] Linux IME patches active.".Colorize(Color.green));
        }
        else
        {
            Log.Message("[LinuxImeFix] Not LinuxPlayer, idle.".Colorize(Color.gray));
        }
    }
}

public sealed class TextFieldState
{
    public string Text = string.Empty;
    public int Cursor;
    public int Select;
    public bool KeyConsumed;
    public string Commit;
}

public static class NativeBridge
{
    private const int RTLD_NOW = 2;
    private static bool loadAttempted;
    private static bool available;
    private static IntPtr handle;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int InitDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ProcessKeyDelegate(int keyval, int keycode, int state);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PollUtf8Delegate(byte[] buf, int len);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FocusInDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FocusOutDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetCursorDelegate(int x, int y);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ResetDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IsReadyDelegate();

    private static InitDelegate pInit;
    private static ProcessKeyDelegate pProcessKey;
    private static PollUtf8Delegate pPollUtf8;
    private static FocusInDelegate pFocusIn;
    private static FocusOutDelegate pFocusOut;
    private static SetCursorDelegate pSetCursor;
    private static ResetDelegate pReset;
    private static IsReadyDelegate pIsReady;
    private static byte[] buffer = new byte[4096];

    [DllImport("libdl.so.2")] private static extern IntPtr dlopen(string f, int flags);
    [DllImport("libdl.so.2")] private static extern IntPtr dlsym(IntPtr h, string s);
    [DllImport("libdl.so.2")] private static extern IntPtr dlerror();

    public static void Load()
    {
        if (loadAttempted) return;
        loadAttempted = true;
        try
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            string path = Path.Combine(dir, "libLinuxImeFixNative.so");
            if (!File.Exists(path)) { Log.Warning($"[LinuxImeFix] {path} not found"); return; }
            handle = dlopen(path, RTLD_NOW);
            if (handle == IntPtr.Zero) { Log.Warning($"[LinuxImeFix] dlopen failed: {PtrString(dlerror())}"); return; }
            pInit = Get<InitDelegate>("rimworld_ime_init");
            pProcessKey = Get<ProcessKeyDelegate>("rimworld_ime_process_key");
            pPollUtf8 = Get<PollUtf8Delegate>("rimworld_ime_poll_utf8");
            pFocusIn = Get<FocusInDelegate>("rimworld_ime_focus_in");
            pFocusOut = Get<FocusOutDelegate>("rimworld_ime_focus_out");
            pSetCursor = Get<SetCursorDelegate>("rimworld_ime_set_cursor");
            pReset = Get<ResetDelegate>("rimworld_ime_reset");
            pIsReady = Get<IsReadyDelegate>("rimworld_ime_is_ready");
            available = pInit != null && pProcessKey != null && pPollUtf8 != null;
            if (!available) { Log.Warning("[LinuxImeFix] Missing exports"); return; }
            int ok = pInit();
            Log.Message(ok != 0 ? "[LinuxImeFix] IBus connected.".Colorize(Color.green) : "[LinuxImeFix] IBus connection failed.");
        }
        catch (Exception ex) { Log.Warning($"[LinuxImeFix] Load error: {ex}"); }
    }

    public static bool IsReady => available && pIsReady != null && pIsReady() != 0;
    public static bool ProcessKey(int keyval, int keycode, int state) => available && pProcessKey != null && pProcessKey(keyval, keycode, state) != 0;
    public static string PollCommit()
    {
        if (!available || pPollUtf8 == null) return null;
        int n = pPollUtf8(buffer, buffer.Length);
        if (n <= 0) return null;
        return System.Text.Encoding.UTF8.GetString(buffer, 0, n);
    }
    public static void FocusIn() { if (available && pFocusIn != null) pFocusIn(); }
    public static void FocusOut() { if (available && pFocusOut != null) pFocusOut(); }
    public static void SetCursor(int x, int y) { if (available && pSetCursor != null) pSetCursor(x, y); }
    public static void Reset() { if (available && pReset != null) pReset(); }

    private static T Get<T>(string sym) where T : class
    {
        IntPtr p = dlsym(handle, sym);
        if (p == IntPtr.Zero) { Log.Warning($"[LinuxImeFix] dlsym {sym} failed"); return null; }
        return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
    }
    private static string PtrString(IntPtr p) => p == IntPtr.Zero ? "(null)" : Marshal.PtrToStringAnsi(p);
}

public static class LinuxImeUtility
{
    public static bool IsLinux => Application.platform == RuntimePlatform.LinuxPlayer;
    private static int lastLoggedControl;

    // Composition tracking: save text field state when IME starts consuming keys.
    // Restore + insert when commit arrives.
    private static bool composing;
    private static int compControl;
    private static string compBaseText = "";
    private static int compBaseCursor, compBaseSelect;

    public static TextFieldState PrepareTextField(Rect rect, string text)
    {
        var state = new TextFieldState { Text = text ?? "", Cursor = (text ?? "").Length, Select = (text ?? "").Length };
        if (!IsLinux) return state;

        if (TryGetActiveTextEditor(out var editor) && RectLooksLike(editor.position, rect))
        {
            state.Text = editor.text ?? state.Text;
            state.Cursor = Mathf.Clamp(editor.cursorIndex, 0, state.Text.Length);
            state.Select = Mathf.Clamp(editor.selectIndex, 0, state.Text.Length);

            if (NativeBridge.IsReady)
            {
                NativeBridge.FocusIn();
                var sp = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMin + 8f, rect.yMax + 2f));
                NativeBridge.SetCursor((int)sp.x, (int)sp.y);
            }
            Input.imeCompositionMode = IMECompositionMode.On;
            Input.compositionCursorPos = new Vector2(rect.xMin + 8f, rect.yMax + 2f);

            if (lastLoggedControl != editor.controlID)
            {
                lastLoggedControl = editor.controlID;
                Log.Message($"[LinuxImeFix] TextField control={editor.controlID}".Colorize(Color.cyan));
            }

            // Process key event in Prefix BEFORE GUI.TextField runs.
            // GUI.TextField internally does textEditor.text = text, overwriting any changes.
            // So we must Use() the event here, and apply commit in Postfix.
            ProcessKeyEvent(state);
        }
        return state;
    }

    private static void ProcessKeyEvent(TextFieldState state)
    {
        state.KeyConsumed = false;
        state.Commit = null;

        if (!NativeBridge.IsReady) return;
        if (Event.current == null || Event.current.type != EventType.KeyDown) return;

        var kc = Event.current.keyCode;
        if (IsEditingKey(kc)) return;
        if (Event.current.character == '\0' && IsModifierKey(kc)) return;

        int keyval = Event.current.character != '\0' ? (int)Event.current.character : (int)kc;
        int ibusState = 0;
        if (Event.current.shift) ibusState |= 0x1;
        if (Event.current.control) ibusState |= 0x4;
        if (Event.current.alt) ibusState |= 0x8;

        bool consumed = NativeBridge.ProcessKey(keyval, 0, ibusState);
        string commit = NativeBridge.PollCommit();

        if (consumed)
        {
            // Eat the event so GUI.TextField doesn't process this key
            Event.current.Use();
            state.KeyConsumed = true;

            // Track composition
            if (!composing || compControl != GUIUtility.keyboardControl)
            {
                composing = true;
                compControl = GUIUtility.keyboardControl;
                compBaseText = state.Text;
                compBaseCursor = state.Cursor;
                compBaseSelect = state.Select;
            }

            if (!string.IsNullOrEmpty(commit))
            {
                state.Commit = commit;
                composing = false;
            }
        }
        else
        {
            if (composing) composing = false;
        }
    }

    public static void FinishTextField(Rect rect, TextFieldState state, ref string result)
    {
        if (!IsLinux) return;

        // Check for late commits (arrived after ProcessKey but before Postfix)
        if (state.Commit == null && NativeBridge.IsReady)
        {
            string late = NativeBridge.PollCommit();
            if (!string.IsNullOrEmpty(late))
                state.Commit = late;
        }

        if (!string.IsNullOrEmpty(state.Commit))
        {
            // GUI.TextField returned the original text (event was Use'd).
            // Now we insert the commit into the result.
            // RimWorld will use __result to update its own variable (e.g. curName).
            string baseText = result ?? "";
            int cursor = state.Cursor;
            int select = state.Select;

            // If we were composing, use the pre-composition state as base
            if (state.KeyConsumed)
            {
                baseText = compBaseText;
                cursor = compBaseCursor;
                select = compBaseSelect;
            }

            int a = Mathf.Clamp(Mathf.Min(cursor, select), 0, baseText.Length);
            int b = Mathf.Clamp(Mathf.Max(cursor, select), 0, baseText.Length);
            string next = baseText.Remove(a, b - a).Insert(a, state.Commit);
            int caret = a + state.Commit.Length;

            result = next;

            // Also update the TextEditor so it reflects the new text
            if (TryGetActiveTextEditor(out var editor))
            {
                editor.text = next;
                editor.cursorIndex = caret;
                editor.selectIndex = caret;
            }

            // Update composition base for next commit
            compBaseText = next;
            compBaseCursor = caret;
            compBaseSelect = caret;

            Log.Message($"[LinuxImeFix] commit '{state.Commit}' -> '{next}'".Colorize(Color.cyan));
        }
    }

    public static void FrameEndRefresh()
    {
        if (!IsLinux || !NativeBridge.IsReady) return;
        if (!TryGetActiveTextEditor(out _))
        {
            NativeBridge.FocusOut();
            Input.imeCompositionMode = IMECompositionMode.Auto;
        }
    }

    private static bool IsEditingKey(KeyCode kc) => kc switch
    {
        KeyCode.Backspace or KeyCode.Delete or KeyCode.LeftArrow or KeyCode.RightArrow
        or KeyCode.UpArrow or KeyCode.DownArrow or KeyCode.Home or KeyCode.End
        or KeyCode.PageUp or KeyCode.PageDown or KeyCode.Escape or KeyCode.Tab => true,
        _ => false
    };

    private static bool IsModifierKey(KeyCode kc) => kc switch
    {
        KeyCode.LeftShift or KeyCode.RightShift or KeyCode.LeftControl or KeyCode.RightControl
        or KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.LeftCommand or KeyCode.RightCommand => true,
        _ => false
    };

    private static bool TryGetActiveTextEditor(out TextEditor editor)
    {
        editor = null;
        if (!IsLinux || Event.current == null) return false;
        int kc = GUIUtility.keyboardControl;
        if (kc == 0) return false;
        editor = GUIUtility.GetStateObject(typeof(TextEditor), kc) as TextEditor;
        return editor != null && editor.controlID == kc && !IsDefault(editor.position) && editor.position.width > 0f;
    }

    private static bool RectLooksLike(Rect a, Rect b) =>
        !IsDefault(a) && !IsDefault(b) &&
        Mathf.Abs(a.x - b.x) <= 4f && Mathf.Abs(a.y - b.y) <= 4f &&
        Mathf.Abs(a.width - b.width) <= 10f && Mathf.Abs(a.height - b.height) <= 10f;

    private static bool IsDefault(Rect r) => r.x == 0f && r.y == 0f && r.width == 0f && r.height == 0f;
}

[HarmonyPatch(typeof(Widgets), nameof(Widgets.TextField), new[] { typeof(Rect), typeof(string) })]
public static class Widgets_TextField_Patch
{
    public static void Prefix(Rect rect, string text, out TextFieldState __state)
        => __state = LinuxImeUtility.PrepareTextField(rect, text);
    public static void Postfix(Rect rect, TextFieldState __state, ref string __result)
        => LinuxImeUtility.FinishTextField(rect, __state, ref __result);
}

[HarmonyPatch(typeof(Widgets), nameof(Widgets.TextArea), new[] { typeof(Rect), typeof(string), typeof(bool) })]
public static class Widgets_TextArea_Patch
{
    public static void Prefix(Rect rect, string text, out TextFieldState __state)
        => __state = LinuxImeUtility.PrepareTextField(rect, text);
    public static void Postfix(Rect rect, TextFieldState __state, ref string __result)
        => LinuxImeUtility.FinishTextField(rect, __state, ref __result);
}

[HarmonyPatch(typeof(DevGUI), nameof(DevGUI.TextField), new[] { typeof(Rect), typeof(string) })]
public static class DevGUI_TextField_Patch
{
    public static void Prefix(Rect rect, string text, out TextFieldState __state)
        => __state = LinuxImeUtility.PrepareTextField(rect, text);
    public static void Postfix(Rect rect, TextFieldState __state, ref string __result)
        => LinuxImeUtility.FinishTextField(rect, __state, ref __result);
}

[HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
public static class UIRoot_Play_Patch { public static void Postfix() => LinuxImeUtility.FrameEndRefresh(); }

[HarmonyPatch(typeof(UIRoot_Entry), nameof(UIRoot_Entry.UIRootOnGUI))]
public static class UIRoot_Entry_Patch { public static void Postfix() => LinuxImeUtility.FrameEndRefresh(); }
