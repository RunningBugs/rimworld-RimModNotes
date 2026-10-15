using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod69Checks
{
    public const string PackageId = "RunningBugs.RitualOutcomeSelection";

    public static void Run(Map map)
    {
        var get = typeof(RitualOutcomeEffectWorker_FromQuality).GetMethod("GetOutcome");
        var registered = Harmony.GetPatchInfo(get)?.Postfixes.SingleOrDefault(p => p.PatchMethod.DeclaringType?.FullName == "RitualOutcomeSelection.RitualOutcomeEffectWorker_FromQuality_GetOutcome_Patch");
        Require(registered != null, "Actual GetOutcome postfix not registered");
        Require(Harmony.GetPatchInfo(typeof(LordJob_Ritual).GetMethod("ApplyOutcome"))?.Prefixes.Any(p => p.PatchMethod.DeclaringType?.FullName == "RitualOutcomeSelection.LordJob_Ritual_ApplyOutcome_Patch") == true, "Actual ApplyOutcome prefix not registered");
        Require(Harmony.GetPatchInfo(typeof(Window).GetMethod("Close"))?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.FullName == "RitualOutcomeSelection.Dialog_BeginRitual_Close_Patch") == true, "Actual Close postfix not registered");
        var field = registered.PatchMethod.DeclaringType.GetField("result");
        var previous = field.GetValue(null);
        var native = new RitualOutcomePossibility { label = "native", chance = 1f, positivityIndex = -1 };
        var selected = new RitualOutcomePossibility { label = "selected", chance = 0f, positivityIndex = 1 };
        var def = new RitualOutcomeEffectDef { outcomeChances = new List<RitualOutcomePossibility> { native, selected } };
        var worker = new RitualOutcomeEffectWorker_FromQuality(def);
        var originalWindows = Find.WindowStack.Windows.ToArray();
        try
        {
            field.SetValue(null, null);
            Require(ReferenceEquals(worker.GetOutcome(1f, null), native), "Unselected real GetOutcome must retain vanilla result");
            field.SetValue(null, selected);
            Require(ReferenceEquals(worker.GetOutcome(1f, null), selected) && field.GetValue(null) == null, "Actual selected result must override once then clear");
            Require(ReferenceEquals(worker.GetOutcome(1f, null), native), "Selection leaked into the next outcome call");
            var job = new LordJob_Ritual();
            typeof(LordJob_Ritual).GetField("ended", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(job, true);
            field.SetValue(null, selected);
            job.ApplyOutcome(0f, false, false, true);
            Require(field.GetValue(null) == null, "Actual cancelled ApplyOutcome prefix did not clear selection");
            field.SetValue(null, selected);
            job.ApplyOutcome(1f, false, false, false);
            Require(ReferenceEquals(field.GetValue(null), selected), "Finished ApplyOutcome prefix unexpectedly cleared selection");
            var begin = new Dialog_BeginRitual("RW17 Mod69", null, new TargetInfo(map.Center, map), map, _ => false, null, null,
                filter: (_, _, _) => false, outcome: def);
            Find.WindowStack.Add(begin);
            begin.Close(false);
            var chooser = Find.WindowStack.Windows.Single(w => w.GetType().FullName == "RitualOutcomeSelection.Dialog_RitualOutcomeSelection" && !originalWindows.Contains(w));
            Require(chooser.forcePause && chooser.draggable && chooser.InitialSize.x == 400f && chooser.InitialSize.y == 300f, "Actual dialog-close chain did not create the expected selection window");
            Require(ReferenceEquals(chooser.GetType().GetField("outcomeDef", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chooser), def), "Protected outcome access did not pass the actual definition to the chooser");
        }
        finally
        {
            field.SetValue(null, previous);
            foreach (var window in Find.WindowStack.Windows.Where(w => !originalWindows.Contains(w)).ToArray())
                Find.WindowStack.TryRemove(window, false);
        }
        Log.Message("RW17 Mod69: actual GetOutcome one-shot/fallback, ApplyOutcome cancelled/finished prefix and real begin-dialog Close/window state passed; radio/confirm rendering and completed ritual pending");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Mod69: " + message);
    }
}
