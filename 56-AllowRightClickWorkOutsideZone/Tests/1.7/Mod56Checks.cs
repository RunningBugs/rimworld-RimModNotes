using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace RW17.Validation;

public static class Mod56Checks
{
    public const string PackageId = "RunningBugs.AllowRightClickWorkOutsideZone";

    public static void Run(Map map)
    {
        Require(map == Find.CurrentMap, "Run on the current disposable map.");
        var providers = (IEnumerable<FloatMenuOptionProvider>)typeof(FloatMenuMakerMap)
            .GetField("providers", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var provider = providers.SingleOrDefault(p => p.GetType().FullName ==
            "ARCWOZ.FloatMenuOptionProvider_BypassRestrictions" && p.GetType().Assembly.GetName().Name == "ARCWOZ");
        Require(provider != null, "The real menu pipeline discovered this MOD provider.");
        IntVec3 cell = CellRect.CenteredOn(map.Center, 20).FirstOrDefault(c =>
            c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetThingList(map).Count == 0 &&
            (c + IntVec3.East).InBounds(map) && (c + IntVec3.East).Standable(map) &&
            (c + IntVec3.East).GetThingList(map).Count == 0 &&
            GenAdj.AdjacentCells.All(d => (c + d).InBounds(map) && (c + d).GetItemCount(map) <= 1), IntVec3.Invalid);
        Require(cell.IsValid, "An empty fixture cell exists.");
        Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
            Faction.OfPlayer, forceGenerateNewPawn: true, forceNoBackstory: true));
        Thing filth = ThingMaker.MakeThing(ThingDefOf.Filth_Dirt);
        Area_Allowed area = null;
        bool oldHome = map.areaManager.Home[cell];
        var oldProvider = FloatMenuMakerMap.currentProvider;
        try
        {
            GenSpawn.Spawn(pawn, cell + IntVec3.East, map);
            GenSpawn.Spawn(filth, cell, map);
            Require(!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Cleaning), "The fixture pawn can intrinsically clean.");
            Require(map.areaManager.TryMakeNewAllowed(out area), "A disposable allowed area was created.");
            area[pawn.Position] = true;
            pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
            pawn.workSettings.SetPriority(WorkTypeDefOf.Cleaning, 0);
            map.areaManager.Home[cell] = true;
            // Native cleaning deliberately waits 600 ticks after filth grows.
            typeof(Filth).GetField("growTick", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(filth, Find.TickManager.TicksGame - 601);
            var cleaner = (WorkGiver_CleanFilth)DefDatabase<WorkGiverDef>.GetNamed("CleanFilth").Worker;
            Require(((Filth)filth).TicksSinceThickened >= 600 && !filth.Fogged() &&
                cleaner.HasJobOnThing(pawn, filth, true) && pawn.CanReach(filth, PathEndMode.Touch, Danger.Deadly),
                "The actual native scanner accepts this aged, visible, reachable cleaning fixture.");
            var originalPriorities = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .ToDictionary(w => w, w => pawn.workSettings.GetPriority(w));
            var context = new FloatMenuContext(new List<Pawn> { pawn }, cell.ToVector3Shifted(), map);
            var vanilla = new FloatMenuOptionProvider_WorkGivers();
            FloatMenuMakerMap.currentProvider = vanilla;
            vanilla.GetOptions(context).ToList();
            var ordinary = vanilla.GetOptionsFor(filth, context).ToList();
            Require(!ordinary.Any(o => !o.Disabled), "Vanilla cannot order this disabled/outside-area cleaning.");

            FloatMenuMakerMap.currentProvider = provider;
            provider.GetOptions(context).ToList();
            var bypass = provider.GetOptionsFor(filth, context).ToList();
            Log.Message("[RW17.Validation] Mod56 fixture: age=" + ((Filth)filth).TicksSinceThickened +
                ", ordinary=" + string.Join(" | ", ordinary.Select(o => o.Label + " disabled=" + o.Disabled)) +
                ", bypass=" + string.Join(" | ", bypass.Select(o => o.Label + " disabled=" + o.Disabled)));
            Require(bypass.Any(o => !o.Disabled), "The actual MOD provider offers enabled cleaning.");
            string suffix = " [" + "BypassRestrictions".Translate() + "]";
            Require(bypass.All(o => o.Label.EndsWith(suffix, StringComparison.Ordinal)),
                "Real menu options are tagged with the localized bypass suffix.");
            Require(pawn.playerSettings.AreaRestrictionInPawnCurrentMap == area,
                "Generating options restores the real pawn area restriction.");
            Require(originalPriorities.All(p => pawn.workSettings.GetPriority(p.Key) == p.Value),
                "Generating both option paths restores every real work priority.");
            Log.Message("[RW17.Validation] Mod56 checks passed: provider_discovered, vanilla_restricted, " +
                "bypass_option_enabled, localized_suffix, area_restored, priorities_restored");
        }
        finally
        {
            FloatMenuMakerMap.currentProvider = oldProvider;
            map.areaManager.Home[cell] = oldHome;
            if (!filth.Destroyed) filth.Destroy(DestroyMode.Vanish);
            if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
            area?.Delete();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
