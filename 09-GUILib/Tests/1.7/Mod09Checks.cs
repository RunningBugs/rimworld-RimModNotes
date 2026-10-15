using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RW17.Validation;

public static class Mod09Checks
{
    public const string PackageId = "RunningBugs.GUI";
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Mod09: " + message); }

    public static void Run(Map map)
    {
        var mod = LoadedModManager.RunningMods.Single(m => m.PackageId.Equals(PackageId, StringComparison.OrdinalIgnoreCase));
        var assembly = mod.assemblies.loadedAssemblies.Single(a => a.GetName().Name == "SimpleGUI");
        var data = assembly.GetType("SimpleGUI.ToggleIconData", true);
        var patcher = assembly.GetType("SimpleGUI.ToggleIconPatcher", true);
        var sampleType = assembly.GetType("SimpleGUI.SampleWindow", true);
        var postfix = patcher.GetMethod("AddIcon", All);
        var target = typeof(PlaySettings).GetMethod("DoPlaySettingsGlobalControls");
        var registration = Harmony.GetPatchInfo(target)?.Postfixes.SingleOrDefault(p => p.owner == "com.RunningBugs.SimpleGUI" && p.PatchMethod == postfix);
        Require(registration != null, "actual toolbar postfix is missing");
        Require((Type)data.GetProperty("WindowType", All).GetValue(null) == sampleType && data.GetProperty("Tex", All).GetValue(null) is Texture2D && data.GetProperty("Action", All).GetValue(null) is Action, "real startup library registration");
        Require((string)assembly.GetType("SimpleGUI.Log", true).GetField("prefix", All).GetValue(null) == "SimpleGUI", "real startup logger prefix");
        var flag = patcher.GetField("flag", All); var oldFlag = flag.GetValue(null);
        var oldEvent = Event.current; var oldWindows = Find.WindowStack.Windows.ToArray();
        var fields = data.GetFields(BindingFlags.Static | BindingFlags.NonPublic); var oldData = fields.Select(f => f.GetValue(null)).ToArray();
        try
        {
            Event.current = new Event { type = EventType.Layout };
            foreach (var window in oldWindows.Where(w => sampleType.IsInstanceOfType(w))) Find.WindowStack.TryRemove(window, false);
            flag.SetValue(null, false);
            postfix.Invoke(null, new object[] { new WidgetRow(), true });
            Require(!Find.WindowStack.IsOpen(sampleType), "world-view negative control");
            flag.SetValue(null, true);
            Find.PlaySettings.DoPlaySettingsGlobalControls(new WidgetRow(100f, 100f), false);
            var sample = Find.WindowStack.Windows.Single(w => w.GetType() == sampleType);
            Require(sample.draggable && sample.resizeable, "real gallery native Window properties");
            Find.PlaySettings.DoPlaySettingsGlobalControls(new WidgetRow(100f, 100f), false);
            Require(Find.WindowStack.Windows.Count(w => w.GetType() == sampleType) == 1, "repeated real toolbar call must not duplicate window");
            VerifyGallery(sample);
            flag.SetValue(null, false);
            Find.PlaySettings.DoPlaySettingsGlobalControls(new WidgetRow(100f, 100f), false);
            Require(!Find.WindowStack.IsOpen(sampleType), "false toolbar state closes actual native Window");
            var expectedType = typeof(ProbeWindow); var tex = (Texture2D)data.GetProperty("Tex", All).GetValue(null); int opened = 0;
            Action action = () => { opened++; Find.WindowStack.Add(new ProbeWindow()); };
            data.GetMethod("setupToggleIcon", All).Invoke(null, new object[] { expectedType, tex, "RW17 configurable library fixture", SoundDefOf.Mouseover_ButtonToggle, action, "RW17GuiFixture" });
            Require((Type)data.GetProperty("WindowType", All).GetValue(null) == expectedType && (string)data.GetProperty("Tooltip", All).GetValue(null) == "RW17 configurable library fixture" && (string)data.GetProperty("TutorTag", All).GetValue(null) == "RW17GuiFixture", "actual public library configuration roundtrip");
            flag.SetValue(null, true); Find.PlaySettings.DoPlaySettingsGlobalControls(new WidgetRow(100f, 100f), false);
            Require(opened == 1 && Find.WindowStack.IsOpen(expectedType), "registered consumer callback/native window positive control");
            Find.PlaySettings.DoPlaySettingsGlobalControls(new WidgetRow(100f, 100f), false);
            Require(opened == 1, "library consumer callback cannot run twice for open window");
            flag.SetValue(null, false); Find.PlaySettings.DoPlaySettingsGlobalControls(new WidgetRow(100f, 100f), false);
            Require(!Find.WindowStack.IsOpen(expectedType), "registered consumer native close");
        }
        finally
        {
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, oldData[i]);
            flag.SetValue(null, oldFlag); Event.current = oldEvent;
            foreach (var window in Find.WindowStack.Windows.Where(w => !oldWindows.Contains(w)).ToArray()) Find.WindowStack.TryRemove(window, false);
            foreach (var window in oldWindows.Where(w => !Find.WindowStack.Windows.Contains(w))) Find.WindowStack.Add(window);
        }
        if (Environment.GetEnvironmentVariable("RW17_GUI09_RENDER") == "1")
        {
            ((Action)data.GetProperty("Action", All).GetValue(null))();
            var gallery = Find.WindowStack.Windows.Single(w => w.GetType() == sampleType);
            ((QuickSearchWidget)sampleType.GetField("searchWidget", All).GetValue(gallery)).filter.Text = "steel";
            flag.SetValue(null, true);
            Log.Message("RW17_GUI09_RENDER_READY: actual gallery window left open with real steel search for root GPU capture");
        }
        Log.Message("RW17 Mod09: actual toolbar Harmony registration/native toggle-open-close/dedup/world guard/library consumer callback and configuration/gallery real Def rows and layout helpers passed; real pixels/physical click-drag-resize-scroll/search input pending");
    }

    private static void VerifyGallery(Window gallery)
    {
        var type = gallery.GetType(); var getRow = type.GetMethod("GetRow", All); var gap = type.GetMethod("GapVertical", All); var y = type.GetField("y", All);
        object[] args = { default(Rect), default(Rect), new Rect(2f, 3f, 400f, 120f), 30f }; getRow.Invoke(gallery, args);
        Require((Rect)args[0] == new Rect(2f, 3f, 400f, 30f) && (Rect)args[1] == new Rect(2f, 33f, 400f, 90f) && (float)y.GetValue(gallery) == 30f, "actual independent row splitter geometry");
        gap.Invoke(gallery, new object[] { 7f }); Require((float)y.GetValue(gallery) == 37f, "actual gap helper");
        var search = (QuickSearchWidget)type.GetField("searchWidget", All).GetValue(gallery);
        search.filter.Text = "steel";
        Require(DefDatabase<ThingDef>.AllDefsListForReading.Any(d => search.filter.Matches(d.label)), "real loaded steel Def search input");
        var selected = (IDictionary)type.GetField("selectedDefs", All).GetValue(gallery);
        var method = typeof(Widgets).GetMethods(BindingFlags.Public | BindingFlags.Static).Single(m => m.Name == "DefLabelWithIcon" && m.GetParameters().Length == 4);
        var observer = new Harmony("rw17.validation.mod09.defrows");
        drawn.Clear();
        observer.Patch(method, prefix: new HarmonyMethod(typeof(Mod09Checks).GetMethod(nameof(ObserveDef), BindingFlags.Static | BindingFlags.NonPublic)));
        try
        {
            gallery.DoWindowContents(new Rect(0, 0, 680, 500));
            var expected = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => search.filter.Matches(d.label)).Take(51).ToArray();
            Require(drawn.SequenceEqual(expected) && drawn.Count > 0 && selected.Count == 0, "actual gallery drawn rows match real Def/filter/order/cap; no click means no selection");
            search.filter.Text = "RW17_NO_SUCH_ITEM"; drawn.Clear(); gallery.DoWindowContents(new Rect(0, 0, 680, 500)); Require(drawn.Count == 0, "real search no-match negative control");
            search.filter.Text = ""; drawn.Clear(); gallery.DoWindowContents(new Rect(0, 0, 680, 500)); Require(drawn.Count == 0, "original empty search gallery behavior");
        }
        finally { observer.Unpatch(method, typeof(Mod09Checks).GetMethod(nameof(ObserveDef), BindingFlags.Static | BindingFlags.NonPublic)); }
    }

    private static readonly System.Collections.Generic.List<ThingDef> drawn = new();
    // Observe the real drawing entry, returning true so the original rendering always executes.
    private static void ObserveDef(Def def) { if (def is ThingDef thing) drawn.Add(thing); }
    private sealed class ProbeWindow : Window { public override void DoWindowContents(Rect rect) { } }
}
