using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MouseEventThrottle
{
    public class MouseEventThrottleMod : Mod
    {
        public static Settings settings;

        public MouseEventThrottleMod(ModContentPack content)
            : base(content)
        {
            new Harmony("com.runningbugs.mouseeventthrottle").PatchAll();
            settings = GetSettings<Settings>();
            Log.Message("[MouseEventThrottle] loaded successfully!");
        }

        public override string SettingsCategory()
        {
            return "SmoothDragSelect_ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            settings.Draw(inRect);
        }
    }

    public class Settings : ModSettings
    {
        public bool throttleEnabled = true;

        public bool profilingEnabled;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref throttleEnabled, "throttleEnabled", true);
            Scribe_Values.Look(ref profilingEnabled, "profilingEnabled", false);
        }

        public void Draw(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("SmoothDragSelect_ThrottleEnabled".Translate(), ref throttleEnabled,
                "SmoothDragSelect_ThrottleEnabledTip".Translate());
            ls.CheckboxLabeled("SmoothDragSelect_ProfilingEnabled".Translate(), ref profilingEnabled,
                "SmoothDragSelect_ProfilingEnabledTip".Translate());
            ls.End();
        }
    }

    /// <summary>
    /// High polling-rate mice flood IMGUI with hundreds of MouseDrag/MouseMove
    /// events per second, and Unity runs the entire OnGUI tree
    /// (UIRoot_Play.UIRootOnGUI: colonist bar, main buttons, window stack, mod
    /// windows, ...) for every one of them. When the real frame rate drops,
    /// more events queue up per frame, more full UI passes run per frame, and
    /// the frame rate spirals down further (measured: ~100 passes/s at ~1 fps).
    ///
    /// This gate rate-limits full UI passes for MouseDrag/MouseMove events:
    /// at most one pass per MinIntervalSeconds. Nothing in the UI needs a
    /// higher granularity — hover visuals and the drag box refresh on the
    /// Repaint pass every rendered frame, and input that matters arrives as
    /// MouseDown/MouseUp/KeyDown/ScrollWheel events, which are never
    /// throttled. Text state is re-initialized before skipping so later
    /// ScreenFader/CameraDriver passes in the same OnGUI call stay consistent.
    /// </summary>
    [HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
    public static class UIRootOnGUI_Gate
    {
        // Adaptive rate limit for full UI passes triggered by mouse events.
        // The interval scales with the smoothed frame time: at 30+ fps mouse
        // passes run at up to 60Hz, in a frame-rate crisis the gate closes
        // down to 10Hz so the (measured ~20ms) full passes can't consume the
        // entire frame budget and starve rendering further.
        private const double MinInterval = 1.0 / 60.0;
        private const double MaxInterval = 1.0 / 10.0;

        private static double smoothedFrameMs = 33.0;
        private static double lastProcessed;

        public static int throttledPasses;

        public static void NotifyFrameMs(double frameMs)
        {
            smoothedFrameMs = smoothedFrameMs * 0.7 + frameMs * 0.3;
        }

        public static bool Prefix()
        {
            if (MouseEventThrottleMod.settings != null && !MouseEventThrottleMod.settings.throttleEnabled)
            {
                return true;
            }
            Event ev = Event.current;
            if (ev == null || (ev.type != EventType.MouseDrag && ev.type != EventType.MouseMove))
            {
                return true;
            }
            double minIntervalSec = smoothedFrameMs / 1000.0 / 2.0;
            if (minIntervalSec < MinInterval)
            {
                minIntervalSec = MinInterval;
            }
            else if (minIntervalSec > MaxInterval)
            {
                minIntervalSec = MaxInterval;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - lastProcessed < minIntervalSec)
            {
                throttledPasses++;
                Text.StartOfOnGUI();
                return false;
            }
            lastProcessed = now;
            return true;
        }
    }

    /// <summary>
    /// Per-frame phase profiler. Sections are timed with prefix/postfix pairs
    /// (start timestamp passed via Harmony __state, so nested sections don't
    /// interfere) and accumulated per frame; a frame boundary is detected in
    /// Root.Update. When a frame exceeds the threshold its breakdown is
    /// logged, so slow frames can be attributed to ticking, map update,
    /// map-mesh drawing, dynamic (pawn/thing) drawing, or the GUI phase.
    /// </summary>
    internal static class FrameProfiler
    {
        private const double LogThresholdMs = 200.0;

        private static readonly Stopwatch wall = Stopwatch.StartNew();
        private static readonly Dictionary<string, double> frameMs = new Dictionary<string, double>();
        private static double frameStartMs = -1.0;
        private static double rootUpdateMs;

        public static long Now()
        {
            return wall.ElapsedMilliseconds;
        }

        public static void BeginFrame()
        {
            double now = wall.Elapsed.TotalMilliseconds;
            double frameTotal = now - frameStartMs;
            if (frameStartMs > 0.0)
            {
                UIRootOnGUI_Gate.NotifyFrameMs(frameTotal);
                if (frameTotal > LogThresholdMs)
                {
                    Report(frameTotal);
                }
            }
            frameMs.Clear();
            rootUpdateMs = 0.0;
            frameStartMs = now;
        }

        public static void EndRootUpdate(long startMs)
        {
            rootUpdateMs = wall.ElapsedMilliseconds - startMs;
        }

        public static void EndSection(string name, long startMs)
        {
            double elapsed = wall.ElapsedMilliseconds - startMs;
            frameMs.TryGetValue(name, out double acc);
            frameMs[name] = acc + elapsed;
        }

        private static void Report(double frameTotal)
        {
            if (MouseEventThrottleMod.settings != null && !MouseEventThrottleMod.settings.profilingEnabled)
            {
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append($"[MouseEventThrottle] slow frame {frameTotal:0}ms: update={rootUpdateMs:0}ms");
            foreach (KeyValuePair<string, double> kv in frameMs)
            {
                sb.Append($", {kv.Key}={kv.Value:0}ms");
            }
            frameMs.TryGetValue("guiPhase", out double gui);
            sb.Append($", unaccounted={(frameTotal - rootUpdateMs - gui):0}ms");
            Log.Message(sb.ToString());
        }
    }

    [HarmonyPatch(typeof(Root), nameof(Root.Update))]
    public static class RootUpdate_Profiler
    {
        public static void Prefix(out long __state)
        {
            FrameProfiler.BeginFrame();
            DragStats.OnFrame();
            __state = FrameProfiler.Now();
        }

        public static void Postfix(long __state)
        {
            FrameProfiler.EndRootUpdate(__state);
        }
    }

    [HarmonyPatch(typeof(Root), nameof(Root.OnGUI))]
    public static class RootOnGUI_Profiler
    {
        public static void Prefix(out long __state)
        {
            __state = FrameProfiler.Now();
        }

        public static void Postfix(long __state)
        {
            // Root.OnGUI runs once per event; accumulate so the whole GUI
            // phase of this frame is visible next to the Update phase.
            FrameProfiler.EndSection("guiPhase", __state);
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TickManagerUpdate))]
    public static class TickManager_Profiler
    {
        public static void Prefix(out long __state)
        {
            __state = FrameProfiler.Now();
        }

        public static void Postfix(long __state)
        {
            FrameProfiler.EndSection("ticks", __state);
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.MapUpdate))]
    public static class MapUpdate_Profiler
    {
        public static void Prefix(out long __state)
        {
            __state = FrameProfiler.Now();
        }

        public static void Postfix(long __state)
        {
            FrameProfiler.EndSection("mapUpdate", __state);
        }
    }

    [HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.DrawMapMesh))]
    public static class DrawMapMesh_Profiler
    {
        public static void Prefix(out long __state)
        {
            __state = FrameProfiler.Now();
        }

        public static void Postfix(long __state)
        {
            FrameProfiler.EndSection("drawMapMesh", __state);
        }
    }

    [HarmonyPatch(typeof(DynamicDrawManager), nameof(DynamicDrawManager.DrawDynamicThings))]
    public static class DrawDynamicThings_Profiler
    {
        public static void Prefix(out long __state)
        {
            __state = FrameProfiler.Now();
        }

        public static void Postfix(long __state)
        {
            FrameProfiler.EndSection("drawDynamicThings", __state);
        }
    }

    /// <summary>
    /// Diagnostics: while a map drag box is active, count full UI passes,
    /// throttled passes and real rendered frames, then log a one-line summary
    /// when the drag ends.
    /// </summary>
    internal static class DragStats
    {
        private static readonly Stopwatch clock = new Stopwatch();
        private static int guiPasses;
        private static int frames;
        private static int throttledAtStart;
        private static bool measuring;

        private static bool DragActive
        {
            get
            {
                Selector selector = Find.Selector;
                return selector != null && selector.dragBox != null && selector.dragBox.active;
            }
        }

        public static void OnGuiPass()
        {
            bool dragging = DragActive;
            if (dragging && !measuring)
            {
                measuring = true;
                clock.Restart();
                guiPasses = 0;
                frames = 0;
                throttledAtStart = UIRootOnGUI_Gate.throttledPasses;
            }
            if (!measuring)
            {
                return;
            }
            guiPasses++;
            if (!dragging)
            {
                Finish();
            }
        }

        public static void OnFrame()
        {
            if (!measuring)
            {
                return;
            }
            frames++;
            if (!Input.GetMouseButton(0))
            {
                Finish();
            }
        }

        private static void Finish()
        {
            measuring = false;
            clock.Stop();
            double seconds = clock.Elapsed.TotalSeconds;
            if (seconds < 1.0 || (MouseEventThrottleMod.settings != null && !MouseEventThrottleMod.settings.profilingEnabled))
            {
                return;
            }
            int throttled = UIRootOnGUI_Gate.throttledPasses - throttledAtStart;
            Log.Message($"[MouseEventThrottle] drag {seconds:0.0}s: {guiPasses / seconds:0} full UI passes/s, {throttled / seconds:0} throttled/s, {frames / seconds:0} real frames/s");
        }
    }

    [HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
    public static class UIRootOnGUI_Stats
    {
        public static void Prefix()
        {
            DragStats.OnGuiPass();
        }
    }
}
