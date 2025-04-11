using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UnlockHarbingerTreeInAllCases
{
    [StaticConstructorOnStartup]
    public static class UnlockHarbingerTreeInAllCases
    {
        static UnlockHarbingerTreeInAllCases()
        {
            var harmony = new Harmony("RunningBugs.UnlockHarbingerTreeInAllCases");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [DefOf]
    public class MyEntityCodexEntryDefOf
    {
        public static EntityCodexEntryDef HarbingerTree;
    }

    [HarmonyPatch(typeof(IncidentWorker_HarbingerTreeSpawn), "TryExecuteWorker")]
    public static class Patch_IncidentWorker_HarbingerTreeSpawn_TryExecuteWorker
    {
        public static bool Prefix(IncidentWorker_HarbingerTreeSpawn __instance)
        {
            // Whenever this incident is triggered, unlock the Harbinger Tree entry in the Entity Codex
            Find.EntityCodex.SetDiscovered(MyEntityCodexEntryDefOf.HarbingerTree);

            // Continue with the original method
            return true;
        }
    }
}