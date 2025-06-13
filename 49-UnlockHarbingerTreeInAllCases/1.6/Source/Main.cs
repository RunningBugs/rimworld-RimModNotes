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
            if (!Find.EntityCodex.Discovered(MyEntityCodexEntryDefOf.HarbingerTree))
            {
                Find.EntityCodex.SetDiscovered(MyEntityCodexEntryDefOf.HarbingerTree);
                Messages.Message("HarbingerTreeForcedDiscovered".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }

            // Continue with the original method
            return true;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_SpecialTreeSpawn), "CanFireNowSub")]
    public static class Patch_IncidentWorker_SpecialTreeSpawn_CanFireNowSub
    {
        public static void Postfix(IncidentParms parms, IncidentWorker_SpecialTreeSpawn __instance, ref bool __result)
        {
            // Making this event always triggerable if it's a Harbinger Tree event on a extreme biome map when result is false
            if (!__result && __instance is IncidentWorker_HarbingerTreeSpawn && parms.target is Map m && m.Biome.isExtremeBiome)
            {
                __result = true;
            }
        }
    }
}
