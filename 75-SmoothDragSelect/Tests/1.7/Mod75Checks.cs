using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RW17.Validation;
public static class Mod75Checks
{
    public const string PackageId = "RunningBugs.MouseEventThrottle";
    private static bool? originalRan;
    private static void Observe(bool __runOriginal) { originalRan = __runOriginal; }
    public static void Run(Map map)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MouseEventThrottle");
        const string owner = "com.runningbugs.mouseeventthrottle";
        var targets = new[] { AccessTools.Method(typeof(UIRoot_Play), "UIRootOnGUI"), AccessTools.Method(typeof(Root), "Update"),
            AccessTools.Method(typeof(Root), "OnGUI"), AccessTools.Method(typeof(TickManager), "TickManagerUpdate"),
            AccessTools.Method(typeof(Map), "MapUpdate"), AccessTools.Method(typeof(MapDrawer), "DrawMapMesh"),
            AccessTools.Method(typeof(DynamicDrawManager), "DrawDynamicThings") };
        foreach (var target in targets)
            Require(Harmony.GetPatchInfo(target)?.Prefixes.Any(p => p.owner == owner && p.PatchMethod.DeclaringType.Assembly == assembly) == true,
                "Actual MOD prefix registered on " + target.Name);
        object settings = assembly.GetType("MouseEventThrottle.MouseEventThrottleMod").GetField("settings").GetValue(null);
        var enabled = settings.GetType().GetField("throttleEnabled"); object oldEnabled = enabled.GetValue(settings);
        var gate = assembly.GetType("MouseEventThrottle.UIRootOnGUI_Gate", true);
        var frame = AccessTools.Field(gate, "smoothedFrameMs"); var last = AccessTools.Field(gate, "lastProcessed");
        var count = gate.GetField("throttledPasses"); object oldFrame = frame.GetValue(null), oldLast = last.GetValue(null), oldCount = count.GetValue(null);
        var prefix = gate.GetMethod("Prefix");
        Event oldEvent = Event.current; GameFont oldFont = Text.Font; TextAnchor oldAnchor = Text.Anchor; bool oldWrap = Text.WordWrap;
        var observer = new Harmony("RW17.Validation.Mod75Observer"); var observe = AccessTools.Method(typeof(Mod75Checks), "Observe");
        bool installed = false;
        bool Gate() => (bool)prefix.Invoke(null, null);
        void EventOf(EventType type) { Event.current = new Event { type = type, rawType = type }; }
        try
        {
            enabled.SetValue(settings, true);
            foreach (EventType type in new[] { EventType.MouseDown, EventType.MouseUp, EventType.KeyDown, EventType.KeyUp,
                EventType.ScrollWheel, EventType.Layout, EventType.Repaint })
            { EventOf(type); Require(Gate(), "Actual gate preserves mandatory event " + type); }
            Event.current = null; Require(Gate(), "A null native Event is never blocked.");
            foreach (EventType type in new[] { EventType.MouseDrag, EventType.MouseMove })
            {
                EventOf(type); frame.SetValue(null, 1000d); last.SetValue(null, Time.realtimeSinceStartupAsDouble - 1d);
                Require(Gate(), "An elapsed native mouse event interval is allowed.");
                int before = (int)count.GetValue(null);
                Text.Font = GameFont.Medium; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                Require(!Gate() && (int)count.GetValue(null) == before + 1, "The duplicate native mouse event is throttled and counted.");
                Require(Text.Font == GameFont.Small && Text.Anchor == TextAnchor.UpperLeft && Text.WordWrap,
                    "The blocked pass calls actual Verse.Text.StartOfOnGUI to reset GUI text state.");
                enabled.SetValue(settings, false); Require(Gate(), "The actual setting disables mouse throttling."); enabled.SetValue(settings, true);
            }
            EventOf(EventType.MouseMove); frame.SetValue(null, 0d); last.SetValue(null, Time.realtimeSinceStartupAsDouble - 1d);
            Require(Gate(), "Healthy-frame clamp also allows an elapsed event.");
            Require(!Gate(), "Healthy-frame lower interval also blocks an immediate duplicate.");
            // Observe Harmony's genuine runOriginal flag on a safe blocked call: no headless GUI tree is drawn.
            observer.Patch(targets[0], postfix: new HarmonyMethod(observe) { priority = Priority.Last }); installed = true;
            EventOf(EventType.MouseDrag); frame.SetValue(null, 1000d); last.SetValue(null, Time.realtimeSinceStartupAsDouble);
            originalRan = null; Find.UIRoot.UIRootOnGUI();
            Require(originalRan == false, "The real patched UIRoot_Play call skips its original for a duplicate drag event.");
            Log.Message("[RW17.Validation] Mod75 checks passed: seven_actual_targets, mandatory_events, null_event, " +
                "actual_time_gate_two_mouse_types, counters, native_text_reset, disabled_setting, healthy_clamp, genuine_harmony_skip");
        }
        finally
        {
            if (installed) observer.Unpatch(targets[0], observe);
            enabled.SetValue(settings, oldEnabled); frame.SetValue(null, oldFrame); last.SetValue(null, oldLast); count.SetValue(null, oldCount);
            Event.current = oldEvent; Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap;
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
