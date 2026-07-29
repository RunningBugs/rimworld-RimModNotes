using System;
using System.Collections.Generic;
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
            CandidateWindowRenderer.EnsureExists();
            Log.Message("[LinuxImeFix] Linux IME patches active.".Colorize(Color.green));
        }
        else
            Log.Message("[LinuxImeFix] Not LinuxPlayer, idle.".Colorize(Color.gray));
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

public sealed class ImePatchedFieldState
{
    public Rect Rect;
    public TextFieldState TextState;
}

public static class NativeBridge
{
    private const int RTLD_NOW = 2;
    private static bool loadAttempted, available;
    private static IntPtr handle;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int InitDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ProcessKeyDelegate(int keyval, int keycode, int state);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PollUtf8Delegate(byte[] buf, int len);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetPreeditDelegate(byte[] buf, int len);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetPreeditCursorDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IsPreeditVisibleDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetCandidateCountDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetCandidateDelegate(int index, byte[] buf, int len);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetLookupCursorDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IsLookupVisibleDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FocusInDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FocusOutDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetCursorDelegate(int x, int y);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ResetDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IsReadyDelegate();

    private static InitDelegate pInit;
    private static ProcessKeyDelegate pProcessKey;
    private static PollUtf8Delegate pPollUtf8;
    private static GetPreeditDelegate pGetPreedit;
    private static GetPreeditCursorDelegate pGetPreeditCursor;
    private static IsPreeditVisibleDelegate pIsPreeditVisible;
    private static GetCandidateCountDelegate pGetCandidateCount;
    private static GetCandidateDelegate pGetCandidate;
    private static GetLookupCursorDelegate pGetLookupCursor;
    private static IsLookupVisibleDelegate pIsLookupVisible;
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
            string path = FindNativeLibrary();
            if (path == null) { Log.Warning("[LinuxImeFix] libLinuxImeFixNative.so not found"); return; }
            handle = dlopen(path, RTLD_NOW);
            if (handle == IntPtr.Zero) { Log.Warning($"[LinuxImeFix] dlopen failed: {PtrString(dlerror())}"); return; }
            pInit = Get<InitDelegate>("rimworld_ime_init");
            pProcessKey = Get<ProcessKeyDelegate>("rimworld_ime_process_key");
            pPollUtf8 = Get<PollUtf8Delegate>("rimworld_ime_poll_utf8");
            pGetPreedit = Get<GetPreeditDelegate>("rimworld_ime_get_preedit");
            pGetPreeditCursor = Get<GetPreeditCursorDelegate>("rimworld_ime_get_preedit_cursor");
            pIsPreeditVisible = Get<IsPreeditVisibleDelegate>("rimworld_ime_is_preedit_visible");
            pGetCandidateCount = Get<GetCandidateCountDelegate>("rimworld_ime_get_candidate_count");
            pGetCandidate = Get<GetCandidateDelegate>("rimworld_ime_get_candidate");
            pGetLookupCursor = Get<GetLookupCursorDelegate>("rimworld_ime_get_lookup_cursor");
            pIsLookupVisible = Get<IsLookupVisibleDelegate>("rimworld_ime_is_lookup_visible");
            pFocusIn = Get<FocusInDelegate>("rimworld_ime_focus_in");
            pFocusOut = Get<FocusOutDelegate>("rimworld_ime_focus_out");
            pSetCursor = Get<SetCursorDelegate>("rimworld_ime_set_cursor");
            pReset = Get<ResetDelegate>("rimworld_ime_reset");
            pIsReady = Get<IsReadyDelegate>("rimworld_ime_is_ready");
            available = pInit != null && pProcessKey != null && pPollUtf8 != null &&
                pGetPreedit != null && pGetPreeditCursor != null && pIsPreeditVisible != null &&
                pGetCandidateCount != null && pGetCandidate != null && pGetLookupCursor != null &&
                pIsLookupVisible != null && pFocusIn != null && pFocusOut != null &&
                pSetCursor != null && pReset != null && pIsReady != null;
            if (!available) { Log.Warning("[LinuxImeFix] Missing required native exports"); return; }
            int ok = pInit();
            Log.Message(ok != 0 && IsReady ? "[LinuxImeFix] IBus connected.".Colorize(Color.green) : "[LinuxImeFix] IBus connection failed.");
        }
        catch (Exception ex) { Log.Warning($"[LinuxImeFix] Load error: {ex}"); }
    }

    /// <summary>
    /// Resolves libLinuxImeFixNative.so on disk. Assembly.Location is empty when the
    /// assembly was loaded from raw bytes (e.g. PurePatcher reloads all mod assemblies
    /// from memory), so fall back to locating the owning ModContentPack and searching
    /// its folder tree.
    /// </summary>
    private static string FindNativeLibrary()
    {
        const string fileName = "libLinuxImeFixNative.so";
        Assembly asm = Assembly.GetExecutingAssembly();
        try
        {
            string loc = asm.Location;
            if (!string.IsNullOrEmpty(loc))
            {
                string p = Path.Combine(Path.GetDirectoryName(loc) ?? "", fileName);
                if (File.Exists(p)) return p;
            }
        }
        catch { /* Location may be empty/invalid for in-memory assemblies */ }

        foreach (ModContentPack mod in LoadedModManager.RunningMods)
        {
            try
            {
                if (mod.assemblies?.loadedAssemblies == null || !mod.assemblies.loadedAssemblies.Contains(asm))
                    continue;
                string[] hits = Directory.GetFiles(mod.RootDir, fileName, SearchOption.AllDirectories);
                if (hits.Length > 0) return hits[0];
            }
            catch { }
        }
        return null;
    }

    public static bool IsReady => available && pIsReady != null && pIsReady() != 0;
    public static bool ProcessKey(int k, int c, int s) => available && pProcessKey != null && pProcessKey(k, c, s) != 0;
    public static string PollCommit()
    {
        if (!available || pPollUtf8 == null) return null;
        int n = pPollUtf8(buffer, buffer.Length);
        return n <= 0 ? null : System.Text.Encoding.UTF8.GetString(buffer, 0, n);
    }
    public static string GetPreedit()
    {
        if (!available || pGetPreedit == null) return null;
        int n = pGetPreedit(buffer, buffer.Length);
        return n <= 0 ? null : System.Text.Encoding.UTF8.GetString(buffer, 0, n);
    }
    public static int GetPreeditCursor() => available && pGetPreeditCursor != null ? pGetPreeditCursor() : 0;
    public static bool IsPreeditVisible() => available && pIsPreeditVisible != null && pIsPreeditVisible() != 0;
    public static int GetCandidateCount() => available && pGetCandidateCount != null ? pGetCandidateCount() : 0;
    public static string GetCandidate(int idx)
    {
        if (!available || pGetCandidate == null) return null;
        int n = pGetCandidate(idx, buffer, buffer.Length);
        return n <= 0 ? null : System.Text.Encoding.UTF8.GetString(buffer, 0, n);
    }
    public static int GetLookupCursor() => available && pGetLookupCursor != null ? pGetLookupCursor() : 0;
    public static bool IsLookupVisible() => available && pIsLookupVisible != null && pIsLookupVisible() != 0;
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
    private static int focusedControl;
    private static int lastCursorX = int.MinValue;
    private static int lastCursorY = int.MinValue;
    private static bool sawFocusedTextFieldThisFrame;
    private static bool composing;
    private static bool shiftDown;
    private static bool controlDown;
    private static bool altDown;
    private const int IBusShiftMask = 0x1;
    private const int IBusControlMask = 0x4;
    private const int IBusAltMask = 0x8;
    private const int IBusReleaseMask = 0x40000000;
    private static int compControl;
    private static string compBaseText = "";
    private static int compBaseCursor, compBaseSelect;

    // Stored candidate window anchor positions (in UI-screen coordinates via UI.GUIToScreenPoint).
    // Calculated during FinishTextField when GUI group stack is correct.
    // Used by DrawCandidateWindow in UIRootOnGUI postfix (drawn on top of everything).
    private static Vector2 storedAnchorBelow; // position below text field (cursor X, rect.yMax)
    private static Vector2 storedAnchorAbove; // position above text field (cursor X, rect.yMin)
    private static bool hasStoredAnchor;

    public static TextFieldState PrepareTextField(Rect rect, string text)
    {
        var state = new TextFieldState
        {
            Text = text ?? "",
            Cursor = (text ?? "").Length,
            Select = (text ?? "").Length,
        };
        if (!IsLinux) return state;

        if (TryGetActiveTextEditor(out var editor) && RectLooksLike(editor.position, rect))
        {
            sawFocusedTextFieldThisFrame = true;
            state.Text = editor.text ?? state.Text;
            state.Cursor = Mathf.Clamp(editor.cursorIndex, 0, state.Text.Length);
            state.Select = Mathf.Clamp(editor.selectIndex, 0, state.Text.Length);

            if (NativeBridge.IsReady)
            {
                if (focusedControl != editor.controlID)
                {
                    if (focusedControl != 0)
                    {
                        NativeBridge.Reset();
                        NativeBridge.FocusOut();
                        composing = false;
                        ResetModifierState();
                    }
                    NativeBridge.FocusIn();
                    focusedControl = editor.controlID;
                    lastCursorX = int.MinValue;
                    lastCursorY = int.MinValue;
                }
                var cursorPoint = CursorScreenPoint(rect, editor);
                int cursorX = (int)cursorPoint.x;
                int cursorY = (int)cursorPoint.y;
                if (cursorX != lastCursorX || cursorY != lastCursorY)
                {
                    NativeBridge.SetCursor(cursorX, cursorY);
                    lastCursorX = cursorX;
                    lastCursorY = cursorY;
                }
            }
            Input.imeCompositionMode = IMECompositionMode.On;
            Input.compositionCursorPos = new Vector2(rect.xMin + CaretXOffset(editor), rect.yMax + 2f);

            if (lastLoggedControl != editor.controlID)
            {
                lastLoggedControl = editor.controlID;
                Log.Message($"[LinuxImeFix] TextField control={editor.controlID}".Colorize(Color.cyan));
            }

            ProcessKeyEvent(state);
        }
        return state;
    }

    private static void ProcessKeyEvent(TextFieldState state)
    {
        state.KeyConsumed = false;
        state.Commit = null;
        if (!NativeBridge.IsReady) return;
        if (Event.current == null) return;
        bool isKeyDown = Event.current.type == EventType.KeyDown;
        bool isKeyUp = Event.current.type == EventType.KeyUp;
        if (!isKeyDown && !isKeyUp) return;

        var kc = Event.current.keyCode;
        bool isModifier = IsModifierKey(kc);
        if (isKeyUp && !isModifier) return;
        SyncModifierStateFromEventBeforeKey(kc, isKeyDown, isKeyUp);

        // Convert to IBus keyval.
        // IMPORTANT: check keyCode FIRST for special keys, because Unity sets
        // Event.current.character to '\b' (0x08) for BackSpace, not '\0'.
        // Sending 0x08 to IBus does nothing; we need X11 keysym 0xFF08.
        if (!TryGetIBusKeyval(kc, Event.current.character, out int keyval)) return;

        int ibusState = CurrentModifierState();
        if (isKeyUp) ibusState |= IBusReleaseMask;

        bool consumed = NativeBridge.ProcessKey(keyval, 0, ibusState);
        string commit = NativeBridge.PollCommit();
        UpdateModifierStateAfterKey(kc, isKeyDown, isKeyUp);

        if (!string.IsNullOrEmpty(commit))
        {
            state.Commit = commit;
            composing = false;
        }

        if (consumed)
        {
            Event.current.Use();
            state.KeyConsumed = true;
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
                composing = false;
            }
        }
        else
        {
            if (composing) composing = false;
        }
    }

    private static bool TryGetIBusKeyval(KeyCode kc, char ch, out int keyval)
    {
        if (kc == KeyCode.Backspace) keyval = 0xFF08;
        else if (kc == KeyCode.Delete) keyval = 0xFFFF;
        else if (kc == KeyCode.LeftArrow) keyval = 0xFF51;
        else if (kc == KeyCode.RightArrow) keyval = 0xFF53;
        else if (kc == KeyCode.UpArrow) keyval = 0xFF52;
        else if (kc == KeyCode.DownArrow) keyval = 0xFF54;
        else if (kc == KeyCode.Home) keyval = 0xFF50;
        else if (kc == KeyCode.End) keyval = 0xFF57;
        else if (kc == KeyCode.Escape) keyval = 0xFF1B;
        else if (kc == KeyCode.Return || kc == KeyCode.KeypadEnter) keyval = 0xFF0D;
        else if (kc == KeyCode.Tab) keyval = 0xFF09;
        else if (kc == KeyCode.PageUp) keyval = 0xFF55;
        else if (kc == KeyCode.PageDown) keyval = 0xFF56;
        else if (kc == KeyCode.LeftShift) keyval = 0xFFE1;
        else if (kc == KeyCode.RightShift) keyval = 0xFFE2;
        else if (kc == KeyCode.LeftControl) keyval = 0xFFE3;
        else if (kc == KeyCode.RightControl) keyval = 0xFFE4;
        else if (kc == KeyCode.LeftAlt) keyval = 0xFFE9;
        else if (kc == KeyCode.RightAlt) keyval = 0xFFEA;
        else if (kc == KeyCode.LeftCommand) keyval = 0xFFEB;
        else if (kc == KeyCode.RightCommand) keyval = 0xFFEC;
        else if (ch != '\0') keyval = ch;
        else
        {
            keyval = (int)kc;
            return keyval != 0;
        }
        return true;
    }

    private static int CurrentModifierState()
    {
        int state = 0;
        if (shiftDown) state |= IBusShiftMask;
        if (controlDown) state |= IBusControlMask;
        if (altDown) state |= IBusAltMask;
        return state;
    }

    private static void SyncModifierStateFromEventBeforeKey(KeyCode kc, bool isKeyDown, bool isKeyUp)
    {
        if (!isKeyUp)
        {
            shiftDown = Event.current.shift;
            controlDown = Event.current.control;
            altDown = Event.current.alt;
        }
        if (isKeyDown)
        {
            SetModifierState(kc, true);
        }
    }

    private static void UpdateModifierStateAfterKey(KeyCode kc, bool isKeyDown, bool isKeyUp)
    {
        if (isKeyUp)
        {
            SetModifierState(kc, false);
        }
        else if (isKeyDown)
        {
            SetModifierState(kc, true);
        }
    }

    private static void SetModifierState(KeyCode kc, bool down)
    {
        if (kc == KeyCode.LeftShift || kc == KeyCode.RightShift) shiftDown = down;
        else if (kc == KeyCode.LeftControl || kc == KeyCode.RightControl) controlDown = down;
        else if (kc == KeyCode.LeftAlt || kc == KeyCode.RightAlt) altDown = down;
    }

    private static void ResetModifierState()
    {
        shiftDown = false;
        controlDown = false;
        altDown = false;
    }

    public static void FinishTextField(Rect rect, TextFieldState state, ref string result)
    {
        if (!IsLinux) return;

        if (state.Commit == null && NativeBridge.IsReady)
        {
            string late = NativeBridge.PollCommit();
            if (!string.IsNullOrEmpty(late))
                state.Commit = late;
        }

        if (!string.IsNullOrEmpty(state.Commit))
        {
            string baseText = state.KeyConsumed ? compBaseText : (result ?? "");
            int cursor = state.KeyConsumed ? compBaseCursor : state.Cursor;
            int select = state.KeyConsumed ? compBaseSelect : state.Select;
            int a = Mathf.Clamp(Mathf.Min(cursor, select), 0, baseText.Length);
            int b = Mathf.Clamp(Mathf.Max(cursor, select), 0, baseText.Length);
            string next = baseText.Remove(a, b - a).Insert(a, state.Commit);
            int caret = a + state.Commit.Length;
            result = next;
            if (TryGetActiveTextEditor(out var editor))
            {
                editor.text = next;
                editor.cursorIndex = caret;
                editor.selectIndex = caret;
            }
            compBaseText = next;
            compBaseCursor = caret;
            compBaseSelect = caret;
            Log.Message($"[LinuxImeFix] commit '{state.Commit}' -> '{next}'".Colorize(Color.cyan));
        }

        // Only store anchor if THIS text field has keyboard focus.
        // Otherwise we'd overwrite the anchor of the focused field with
        // coordinates from an unfocused field (e.g. background text fields).
        if (TryGetActiveTextEditor(out var ed) && RectLooksLike(ed.position, rect))
        {
            StoreCandidateAnchor(rect);
        }
    }

    private static void StoreCandidateAnchor(Rect rect)
    {
        if (!NativeBridge.IsReady) { hasStoredAnchor = false; return; }

        if (!TryGetActiveTextEditor(out var editor)) { hasStoredAnchor = false; return; }

        // Convert local GUI coordinates to UI-screen coordinates.
        // UI.GUIToScreenPoint handles UIScale and GUI group nesting.
        float cursorXOffset = CaretXOffset(editor);
        Vector2 below = UI.GUIToScreenPoint(new Vector2(rect.xMin + cursorXOffset, rect.yMax + 2f));
        Vector2 above = UI.GUIToScreenPoint(new Vector2(rect.xMin + cursorXOffset, rect.yMin - 2f));
        storedAnchorBelow = below;
        storedAnchorAbove = above;
        hasStoredAnchor = true;
    }

    private static Vector2 CursorScreenPoint(Rect rect, TextEditor editor)
        => UI.GUIToScreenPoint(new Vector2(rect.xMin + CaretXOffset(editor), rect.yMax + 2f));

    private static float CaretXOffset(TextEditor editor)
    {
        if (editor == null) return 8f;
        string textBeforeCursor = editor.text ?? "";
        int ci = Mathf.Clamp(editor.cursorIndex, 0, textBeforeCursor.Length);
        textBeforeCursor = textBeforeCursor.Substring(0, ci);
        return Text.CalcSize(textBeforeCursor).x;
    }

    /// <summary>
    /// Called from UIRootOnGUI postfix — draws on top of all other UI.
    /// Uses anchor positions stored during FinishTextField.
    /// </summary>
    public static void DrawCandidateWindow()
    {
        if (!IsLinux || !NativeBridge.IsReady || !hasStoredAnchor) return;

        GameFont oldFont = Text.Font;
        Color oldColor = GUI.color;
        try
        {
            DrawCandidateWindowCore();
        }
        finally
        {
            Text.Font = oldFont;
            GUI.color = oldColor;
        }
    }

    private static void DrawCandidateWindowCore()
    {
        bool lookupVisible = NativeBridge.IsLookupVisible();
        bool preeditVisible = NativeBridge.IsPreeditVisible();
        if (!lookupVisible && !preeditVisible) return;

        string preedit = NativeBridge.GetPreedit() ?? "";
        int candCount = NativeBridge.GetCandidateCount();
        int lookupCursor = NativeBridge.GetLookupCursor();
        if (string.IsNullOrEmpty(preedit) && candCount == 0) return;

        // Calculate window dimensions
        float lineHeight = 20f;
        float padding = 5f;
        float width = 280f;
        float height = padding * 2;
        if (!string.IsNullOrEmpty(preedit)) height += lineHeight;

        List<string> candidates = new();
        for (int i = 0; i < candCount; i++)
        {
            string cand = NativeBridge.GetCandidate(i);
            if (!string.IsNullOrEmpty(cand))
            {
                candidates.Add(cand);
                float w = Text.CalcSize($"{i + 1}. {cand}").x + padding * 4;
                if (w > width) width = w;
            }
        }
        if (candidates.Count > 0) height += candidates.Count * lineHeight;

        // Position: default below text field at cursor X
        float baseX = storedAnchorBelow.x;
        float baseY = storedAnchorBelow.y;

        // Boundary detection using UI.screenWidth/Height
        float sw = UI.screenWidth;
        float sh = UI.screenHeight;

        // Horizontal: keep window within screen
        if (baseX + width > sw - 4f)
            baseX = sw - width - 4f;
        if (baseX < 4f)
            baseX = 4f;

        // Vertical: if below doesn't fit, try above
        if (baseY + height > sh - 4f)
        {
            // Try above
            float aboveY = storedAnchorAbove.y - height;
            if (aboveY >= 4f)
            {
                baseY = aboveY;
            }
            else
            {
                // Neither fits perfectly — pick whichever has more room
                float belowOverflow = (baseY + height) - (sh - 4f);
                float aboveOverflow = 4f - aboveY;
                if (aboveOverflow < belowOverflow)
                    baseY = aboveY;
                else
                    baseY = Mathf.Clamp(baseY, 4f, sh - height - 4f);
            }
        }
        if (baseY < 4f) baseY = 4f;

        Rect windowRect = new Rect(baseX, baseY, width, height);

        // Draw background
        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        GUI.DrawTexture(windowRect, BaseContent.WhiteTex);
        GUI.color = Color.white;
        Widgets.DrawBox(windowRect, 1);

        float y = windowRect.yMin + padding;

        // Preedit
        if (!string.IsNullOrEmpty(preedit))
        {
            GUI.color = Color.yellow;
            Text.Font = GameFont.Tiny;
            string preeditDisplay = preedit;
            int preeditCursor = NativeBridge.GetPreeditCursor();
            if (preeditCursor >= 0 && preeditCursor < preedit.Length)
                preeditDisplay = preedit.Substring(0, preeditCursor) + "|" + preedit.Substring(preeditCursor);
            GUI.Label(new Rect(windowRect.xMin + padding, y, width - padding * 2, lineHeight), preeditDisplay);
            y += lineHeight;
            GUI.color = Color.white;
        }

        // Candidates
        Text.Font = GameFont.Tiny;
        for (int i = 0; i < candidates.Count; i++)
        {
            bool selected = i == lookupCursor;
            Rect candRect = new Rect(windowRect.xMin + padding, y, width - padding * 2, lineHeight);
            if (selected)
            {
                GUI.color = new Color(0.3f, 0.5f, 0.8f, 0.8f);
                GUI.DrawTexture(candRect, BaseContent.WhiteTex);
                GUI.color = Color.white;
            }
            string label = $"{i + 1}. {candidates[i]}";
            GUI.color = selected ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(candRect, label);
            GUI.color = Color.white;
            y += lineHeight;
        }
    }

    public static void FrameEndRefresh()
    {
        if (!IsLinux || !NativeBridge.IsReady)
        {
            sawFocusedTextFieldThisFrame = false;
            return;
        }
        if (!sawFocusedTextFieldThisFrame)
        {
            // Unity/RimWorld invokes OnGUI multiple times per frame/event. Some passes
            // can reach UIRootOnGUI without re-rendering the focused TextField, even
            // though GUIUtility.keyboardControl still points at its TextEditor. Do not
            // Reset/FocusOut in that case: it clears IBus/Rime preedit immediately,
            // causing the candidate window to flash for one pass and leaving Space
            // with nothing to commit.
            if (focusedControl != 0 && GUIUtility.keyboardControl == focusedControl && TryGetActiveTextEditor(out _))
            {
                Input.imeCompositionMode = IMECompositionMode.On;
                sawFocusedTextFieldThisFrame = false;
                return;
            }

            if (focusedControl != 0)
            {
                NativeBridge.Reset();
                NativeBridge.FocusOut();
                focusedControl = 0;
                lastCursorX = int.MinValue;
                lastCursorY = int.MinValue;
                composing = false;
                ResetModifierState();
            }
            Input.imeCompositionMode = IMECompositionMode.Auto;
            hasStoredAnchor = false;
        }
        sawFocusedTextFieldThisFrame = false;
    }

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

[HarmonyPatch]
public static class DubsMintMenus_InputField_Patch
{
    public static bool Prepare() => AccessTools.TypeByName("DubsMintMenus.DubGUI") != null;

    public static MethodBase TargetMethod()
    {
        Type type = AccessTools.TypeByName("DubsMintMenus.DubGUI");
        return AccessTools.Method(type, "InputField", new[]
        {
            typeof(string), typeof(Rect), typeof(string).MakeByRefType(), typeof(Texture2D),
            typeof(int), typeof(bool), typeof(bool), typeof(bool)
        });
    }

    public static void Prefix(Rect rect, ref string buff, Texture2D icon, bool ShowName, out ImePatchedFieldState __state)
    {
        Rect textRect = DubsTextRect(rect, icon, ShowName);
        __state = new ImePatchedFieldState
        {
            Rect = textRect,
            TextState = LinuxImeUtility.PrepareTextField(textRect, buff)
        };
    }

    public static void Postfix(ref string buff, ImePatchedFieldState __state)
    {
        if (__state?.TextState == null) return;
        LinuxImeUtility.FinishTextField(__state.Rect, __state.TextState, ref buff);
    }

    private static Rect DubsTextRect(Rect rect, Texture2D icon, bool showName)
    {
        Rect textRect = rect;
        if (icon != null)
        {
            float iconWidth = rect.height;
            textRect.width -= iconWidth;
            textRect.x += iconWidth;
        }
        if (showName)
        {
            textRect = GenUI.RightPart(rect, 0.8f);
        }
        return textRect;
    }
}

[HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
public static class UIRoot_Play_Patch
{
    public static void Postfix() => LinuxImeUtility.FrameEndRefresh();
}

[HarmonyPatch(typeof(UIRoot_Entry), nameof(UIRoot_Entry.UIRootOnGUI))]
public static class UIRoot_Entry_Patch
{
    public static void Postfix() => LinuxImeUtility.FrameEndRefresh();
}

/// <summary>
/// MonoBehaviour that draws the candidate window in its own OnGUI call.
/// GUI.depth controls draw order: lower depth = drawn later = on top.
/// This ensures the candidate window is always above other UI.
/// </summary>
public class CandidateWindowRenderer : MonoBehaviour
{
    private static CandidateWindowRenderer instance;

    public static void EnsureExists()
    {
        if (instance != null) return;
        var go = new GameObject("LinuxImeFix_CandidateWindowRenderer");
        instance = go.AddComponent<CandidateWindowRenderer>();
        DontDestroyOnLoad(go);
    }

    void OnGUI()
    {
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        try
        {
            // Lower depth draws later/on top in Unity IMGUI. Do not restore this
            // after drawing: RimWorld/Unity appear to use the final GUI.depth when
            // ordering controls across OnGUI callers, and restoring it caused the
            // candidate window to fall behind later UI.
            GUI.depth = -10000;
            // Apply the same UIScale matrix that RimWorld uses (UI.ApplyUIScale).
            // Without this, coordinates stored during UIRootOnGUI (in UI space)
            // won't match the drawing space in this separate OnGUI call.
            if (Prefs.UIScale != 1f)
            {
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                    new Vector3(Prefs.UIScale, Prefs.UIScale, 1f));
            }
            LinuxImeUtility.DrawCandidateWindow();
        }
        finally
        {
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }
    }
}
