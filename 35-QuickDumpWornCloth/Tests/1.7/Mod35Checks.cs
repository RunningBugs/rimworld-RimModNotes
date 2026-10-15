using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RW17.Validation;

public static class Mod35Checks
{
    public const string PackageId = "RunningBugs.QuickDumpWornCloth";
    private static Thing watchedOverlap;
    private static int abandonCount;

    public static void Run(Map map)
    {
        var caravan = (Caravan)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Caravan);
        caravan.Tile = map.Tile;
        caravan.SetFaction(Faction.OfPlayer);
        var component = caravan.AllComps.Single(c => c.GetType().FullName == "QuickDumpWornCloth.WorldObjectComp_DumpWornCloth");
        Require(!component.GetGizmos().Any(), "Empty caravan has a dump gizmo");
        component.parent = null;
        Require(!component.GetGizmos().Any(), "Non-caravan parent has a dump gizmo");
        component.parent = caravan;
        Pawn owner = null;
        Pawn enemy = null;
        var spy = new Harmony("rw17.validation.mod35.abandon");
        var abandon = typeof(Thing).GetMethod("Notify_AbandonedAtTile", new[] { typeof(PlanetTile) });
        try
        {
            owner = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            enemy = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfAncients);
            caravan.AddPawn(owner, false);
            var clean = MakeApparel();
            var tainted = MakeApparel();
            tainted.WornByCorpse = true;
            var enemyGun = MakeGun(enemy);
            var ownGun = MakeGun(owner);
            var orphanGun = MakeGun(enemy);
            typeof(CompBiocodable).GetField("codedPawn", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(orphanGun.TryGetComp<CompBiocodable>(), null);
            var overlap = MakeApparel();
            overlap.WornByCorpse = true;
            var code = new CompBiocodable { parent = overlap, props = new CompProperties_Biocodable() };
            overlap.AllComps.Add(code);
            code.CodeFor(enemy);
            watchedOverlap = overlap;
            abandonCount = 0;
            spy.Patch(abandon, prefix: new HarmonyMethod(typeof(Mod35Checks), nameof(CountAbandon)));
            foreach (var thing in new Thing[] { clean, tainted, enemyGun, ownGun, orphanGun, overlap })
                Require(owner.inventory.innerContainer.TryAdd(thing, false), "Could not build actual inventory fixture");
            caravan.RecacheInventory();
            var command = caravan.GetGizmos().OfType<Command_Action>().Single(c => c.defaultLabel == "Dump worn cloth");
            command.action();
            Require(abandonCount == 1, "Overlapping tainted/biocoded apparel must be abandoned exactly once");
            Require(tainted.Destroyed && enemyGun.Destroyed && orphanGun.Destroyed && overlap.Destroyed,
                "Actual caravan command did not destroy every eligible item");
            Require(!clean.Destroyed && !ownGun.Destroyed && owner.inventory.innerContainer.Contains(clean) && owner.inventory.innerContainer.Contains(ownGun),
                "Actual caravan command removed clean or player-biocoded inventory");
            Require(!caravan.GetGizmos().OfType<Command_Action>().Any(c => c.defaultLabel == "Dump worn cloth"), "Dump command remained after all eligible items were removed");
            Log.Message("RW17 Mod35: actual caravan gizmo/action, tainted/enemy/orphan/overlap removals and clean/player-coded preservation passed");
        }
        finally
        {
            spy.Unpatch(abandon, HarmonyPatchType.Prefix, spy.Id);
            watchedOverlap = null;
            if (owner != null)
            {
                caravan.RemovePawn(owner);
                owner.inventory.innerContainer.ClearAndDestroyContents();
                owner.Destroy(DestroyMode.Vanish);
            }
            if (enemy != null && !enemy.Destroyed)
                enemy.Destroy(DestroyMode.Vanish);
            caravan.Destroy();
        }
    }

    private static Apparel MakeApparel() => (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_Pants"), ThingDefOf.Cloth);

    private static void CountAbandon(Thing __instance)
    {
        if (ReferenceEquals(__instance, watchedOverlap))
            abandonCount++;
    }

    private static ThingWithComps MakeGun(Pawn codedFor)
    {
        var gun = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle"));
        var comp = gun.TryGetComp<CompBiocodable>();
        Require(comp != null, "Vanilla weapon lacks biocodable component");
        comp.CodeFor(codedFor);
        return gun;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Mod35: " + message);
    }
}
