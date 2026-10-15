using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod49Checks
{
    public const string PackageId = "RunningBugs.UnlockHarbingerTreeInAllCases";

    public static void Run(Map map)
    {
        if (!ModsConfig.AnomalyActive)
            throw new InvalidOperationException("Mod49 requires Anomaly for its actual codex/incident assertions");
        var execute = typeof(IncidentWorker_HarbingerTreeSpawn).GetMethod("TryExecuteWorker", BindingFlags.Instance | BindingFlags.NonPublic);
        var canFire = typeof(IncidentWorker_SpecialTreeSpawn).GetMethod("CanFireNowSub", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(Harmony.GetPatchInfo(execute)?.Prefixes.Any(p => p.PatchMethod.DeclaringType?.FullName == "UnlockHarbingerTreeInAllCases.Patch_IncidentWorker_HarbingerTreeSpawn_TryExecuteWorker") == true, "Prefix not registered");
        Require(Harmony.GetPatchInfo(canFire)?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.FullName == "UnlockHarbingerTreeInAllCases.Patch_IncidentWorker_SpecialTreeSpawn_CanFireNowSub") == true, "Postfix not registered");
        var worker = DefDatabase<IncidentDef>.GetNamed("HarbingerTreeSpawn").Worker;
        var codexEntry = DefDatabase<EntityCodexEntryDef>.GetNamed("HarbingerTree");
        var oldCodex = Current.Game.entityCodex;
        var oldExtreme = map.Biome.isExtremeBiome;
        var level = Find.Anomaly.LevelDef;
        var oldDesiredCount = level.desiredHarbingerTreeCount;
        try
        {
            Current.Game.entityCodex = new EntityCodex();
            Current.Game.entityCodex.Reset();
            level.desiredHarbingerTreeCount = 0;
            var parms = new IncidentParms { target = map, bypassStorytellerSettings = false };
            map.Biome.isExtremeBiome = true;
            Require((bool)canFire.Invoke(worker, new object[] { parms }), "Extreme-biome Harbinger event must be enabled even at zero desired trees");
            var other = DefDatabase<IncidentDef>.AllDefs.Select(d => d.Worker)
                .First(w => w is IncidentWorker_SpecialTreeSpawn && w is not IncidentWorker_HarbingerTreeSpawn);
            Require(!(bool)canFire.Invoke(other, new object[] { parms }), "Non-Harbinger tree must retain original extreme-biome rejection");
            map.Biome.isExtremeBiome = false;
            Require(!(bool)canFire.Invoke(worker, new object[] { parms }), "Non-extreme biome must retain zero-count rejection");
            Require(!Find.EntityCodex.Discovered(codexEntry), "Fresh codex fixture starts discovered");
            var treeCount = map.listerThings.ThingsOfDef(ThingDefOf.Plant_TreeHarbinger).Count;
            Require(!(bool)execute.Invoke(worker, new object[] { parms }), "Zero desired trees must make actual tree spawning fail");
            Require(Find.EntityCodex.Discovered(codexEntry), "Actual failed TryExecuteWorker must still discover the codex entry");
            Require(!(bool)execute.Invoke(worker, new object[] { parms }) && Find.EntityCodex.Discovered(codexEntry), "Already discovered branch must remain idempotent");
            Require(map.listerThings.ThingsOfDef(ThingDefOf.Plant_TreeHarbinger).Count == treeCount, "Failure fixture unexpectedly generated a tree");
        }
        finally
        {
            Current.Game.entityCodex = oldCodex;
            map.Biome.isExtremeBiome = oldExtreme;
            level.desiredHarbingerTreeCount = oldDesiredCount;
        }
        Log.Message("RW17 Mod49: actual protected incident hooks, extreme/non-extreme/non-Harbinger boundaries, failed spawn codex discovery and idempotence passed");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Mod49: " + message);
    }
}
