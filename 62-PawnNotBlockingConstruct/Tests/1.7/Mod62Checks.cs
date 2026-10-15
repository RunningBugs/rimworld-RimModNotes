using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

// Compile into the isolated test driver; never ship this assembly with the MOD.
public static class Mod62Checks
{
    public const string PackageId = "RunningBugs.PawnNotBlockingConstruction";

    public static void Run(Map map)
    {
        if (map == null || Faction.OfPlayer == null)
            throw new InvalidOperationException("Run after a disposable player map is ready.");

        var target = AccessTools.Method(typeof(GenConstruct), nameof(GenConstruct.BlocksConstruction),
            new[] { typeof(Thing), typeof(Thing) });
        Require(Harmony.GetPatchInfo(target)?.Postfixes.Any(p =>
            p.PatchMethod.DeclaringType?.Assembly.GetName().Name == "PawnNotBlockingConstruction" &&
            p.PatchMethod.DeclaringType.FullName == "Template.GenConstruct_BlocksConstruction_Prefix") == true,
            "The actual construction method has this MOD's postfix.");

        IntVec3 cell = CellRect.CenteredOn(map.Center, 20).FirstOrDefault(c =>
            c.InBounds(map) && c.Standable(map) && c.GetThingList(map).Count == 0, IntVec3.Invalid);
        Require(cell.IsValid, "An empty standable test cell exists.");

        var frame = (Frame)ThingMaker.MakeThing(ThingDefOf.Wall.frameDef, ThingDefOf.WoodLog);
        frame.SetFaction(Faction.OfPlayer);
        Pawn pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
        try
        {
            GenSpawn.Spawn(frame, cell, map);
            GenSpawn.Spawn(pawn, cell, map);
            Require(frame.Spawned && pawn.Spawned && frame.Position == pawn.Position,
                "The test pawn and frame occupy the same map cell.");
            Require(!pawn.IsHiddenFromPlayer(), "The test pawn is visible, so vanilla would block.");
            Require(!GenConstruct.BlocksConstruction(frame, frame), "A frame does not block itself.");
            Require(!GenConstruct.BlocksConstruction(frame, pawn), "A pawn does not block construction.");
            Require(GenConstruct.FirstBlockingThing(frame, null) == null,
                "The real map traversal ignores the occupying pawn.");

            GenSpawn.Spawn(steel, cell, map);
            Require(GenConstruct.BlocksConstruction(frame, steel),
                "A steel item still blocks an impassable wooden wall.");
            Require(GenConstruct.FirstBlockingThing(frame, null) == steel,
                "Map traversal returns the real non-pawn blocker.");
            Log.Message("[RW17.Validation] Mod62 checks passed: postfix_registered, " +
                "self_is_not_blocker, pawn_is_not_blocker, map_traversal_ignores_pawn, " +
                "item_still_blocks, map_traversal_keeps_item_blocker");
        }
        finally
        {
            foreach (Thing thing in new Thing[] { steel, pawn, frame })
                if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
