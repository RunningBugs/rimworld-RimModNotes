using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod63Checks
{
    public const string PackageId = "RunningBugs.HideGenebankGenesFromTraders";
    private const string Owner = "com.RunningBugs.HideGenebankGenesFromTraders";

    // Run only in the disposable map, after game/Def initialization and all startup patches.
    public static void Run(Map map)
    {
        Stage("entered; managed thread " + Environment.CurrentManagedThreadId);
        Require(map != null && ModsConfig.BiotechActive, "Map and Biotech are required");
        var negotiator = map.mapPawns.FreeColonists.FirstOrDefault();
        Require(negotiator != null, "A spawned colonist is required");
        var ground = AccessTools.Method(typeof(Pawn_TraderTracker), "ColonyThingsWillingToBuy",
            new[] { typeof(Pawn) });
        var orbital = AccessTools.Method(typeof(TradeUtility), "AllLaunchableThingsForTrade",
            new[] { typeof(Map), typeof(ITrader) });
        Require(Harmony.GetPatchInfo(ground)?.Postfixes.Any(p => p.owner == Owner
            && p.PatchMethod.DeclaringType?.FullName == "HideGenebankGenesFromTraders.HideGenebankGenes_TraderGroups_Patch"
            && p.PatchMethod.Name == "Postfix") == true,
            "Ground postfix was not registered");
        Require(Harmony.GetPatchInfo(orbital)?.Postfixes.Any(p => p.owner == Owner
            && p.PatchMethod.DeclaringType?.FullName == "HideGenebankGenesFromTraders.HideGenebankGenes_OrbitalTraders_Patch"
            && p.PatchMethod.Name == "Postfix") == true,
            "Orbital postfix was not registered");

        var groundPostfix = Harmony.GetPatchInfo(ground).Postfixes.Single(p => p.owner == Owner);
        var orbitalPostfix = Harmony.GetPatchInfo(orbital).Postfixes.Single(p => p.owner == Owner);
        var checks = new Harmony(Owner);

        var anchor = GenRadial.RadialCellsAround(negotiator.Position, 30f, false).FirstOrDefault(c =>
            FixtureCells(c).All(p => p.InBounds(map) && p.Standable(map) && !p.Fogged(map)
                && p.GetThingList(map).Count == 0), IntVec3.Invalid);
        Require(anchor.IsValid, "No empty reachable fixture area near the colonist");
        var looseCell = anchor + new IntVec3(0, 0, 3);
        var silverCell = anchor + new IntVec3(1, 0, 3);
        var ejectedCell = anchor + new IntVec3(2, 0, 3);
        var homeCells = new[] { looseCell, silverCell, ejectedCell };
        var previousHome = homeCells.Select(c => map.areaManager.Home[c]).ToArray();
        var created = new List<Thing>();
        try
        {
            foreach (var cell in homeCells) map.areaManager.Home[cell] = true;
            var bank = Make(ThingDefOf.GeneBank);
            bank.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(bank, anchor, map);
            var container = bank.TryGetComp<CompGenepackContainer>();
            var beacon = Make(ThingDefOf.OrbitalTradeBeacon);
            beacon.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(beacon, anchor + new IntVec3(3, 0, 0), map);
            beacon.TryGetComp<CompPowerTrader>().PowerOn = true;
            var stored = (Genepack)Make(ThingDefOf.Genepack);
            stored.Initialize(new List<GeneDef> { GeneDefOf.Inbred });
            Require(container.innerContainer.TryAdd(stored), "Failed to store the genepack");
            var loose = (Genepack)Make(ThingDefOf.Genepack);
            loose.Initialize(new List<GeneDef> { GeneDefOf.Inbred });
            GenSpawn.Spawn(loose, looseCell, map);
            var silver = Make(ThingDefOf.Silver);
            GenSpawn.Spawn(silver, silverCell, map);
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            Require(stored.ParentHolder == container && stored.holdingOwner == container.innerContainer
                && !stored.Spawned && loose.ParentHolder == map
                && loose.holdingOwner == map.spawnedThings && loose.Spawned,
                "Genepack container/Map owner chain was not applied");
            var tracker = new Pawn_TraderTracker(negotiator);
            Stage("fixture owner chains verified");
            // Only remove these two exact production postfixes; never UnpatchAll or other owners.
            bool groundRemoved = false, orbitalRemoved = false;
            try
            {
                checks.Unpatch(ground, groundPostfix.PatchMethod);
                groundRemoved = true;
                checks.Unpatch(orbital, orbitalPostfix.PatchMethod);
                orbitalRemoved = true;
                Stage("exact postfixes temporarily removed");
                Check(tracker.ColonyThingsWillingToBuy(negotiator), stored, loose, silver, true,
                    "ground control");
                Check(TradeUtility.AllLaunchableThingsForTrade(map), stored, loose, silver, true,
                    "orbital control");
                Stage("both real unfiltered controls passed");
            }
            finally
            {
                try
                {
                    if (groundRemoved) checks.Patch(ground, postfix: Restore(groundPostfix));
                }
                finally
                {
                    if (orbitalRemoved) checks.Patch(orbital, postfix: Restore(orbitalPostfix));
                }
            }
            Stage("exact postfixes restored");
            Check(tracker.ColonyThingsWillingToBuy(negotiator), stored, loose, silver, false,
                "ground patched");
            Check(TradeUtility.AllLaunchableThingsForTrade(map), stored, loose, silver, false,
                "orbital patched");
            Stage("stored/loose/silver filters passed");

            container.innerContainer.Remove(stored);
            GenSpawn.Spawn(stored, ejectedCell, map);
            Require(stored.ParentHolder == map && stored.holdingOwner == map.spawnedThings
                && stored.Spawned, "Ejected genepack did not enter the Map owner chain");
            Check(tracker.ColonyThingsWillingToBuy(negotiator), stored, loose, silver, true,
                "ground after ejection");
            Check(TradeUtility.AllLaunchableThingsForTrade(map), stored, loose, silver, true,
                "orbital after ejection");
            Stage("ejection visibility passed");
        }
        finally
        {
            for (var i = created.Count - 1; i >= 0; i--)
                if (!created[i].Destroyed) created[i].Destroy(DestroyMode.Vanish);
            for (var i = 0; i < homeCells.Length; i++)
                map.areaManager.Home[homeCells[i]] = previousHome[i];
        }

        Thing Make(ThingDef def)
        {
            var thing = ThingMaker.MakeThing(def);
            created.Add(thing);
            return thing;
        }
    }

    private static IEnumerable<IntVec3> FixtureCells(IntVec3 anchor) =>
        GenAdj.OccupiedRect(anchor, Rot4.North, ThingDefOf.GeneBank.size).Cells.Concat(new[] {
            anchor + new IntVec3(3, 0, 0), anchor + new IntVec3(0, 0, 3),
            anchor + new IntVec3(1, 0, 3), anchor + new IntVec3(2, 0, 3) });

    private static void Check(IEnumerable<Thing> query, Thing stored, Thing loose, Thing silver,
        bool expectStored, string stage)
    {
        var things = query.ToList();
        Require(things.Contains(stored) == expectStored, stage + ": wrong stored genepack visibility");
        Require(things.Contains(loose), stage + ": loose genepack was lost");
        Require(things.Contains(silver), stage + ": ordinary sale item was lost");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mod63: " + message);
    }

    private static HarmonyMethod Restore(Patch patch) => new HarmonyMethod(patch.PatchMethod)
        { priority = patch.priority, before = patch.before, after = patch.after };
    private static void Stage(string stage)
    {
        Console.WriteLine("RW17_MOD63_STAGE " + stage);
        Console.Out.Flush();
    }
}
