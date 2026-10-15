using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RW17.Validation;

public static class Mod68Checks
{
    public const string PackageId = "RunningBugs.BetterOutfitStand";
    public static void Run(Map map)
    {
        Require(ModsConfig.OdysseyActive, "Outfit stands require actual Odyssey defs.");
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "BetterOutfitStand");
        Type standType = assembly.GetType("BetterOutfitStand.Building_BetterOutfitStand", true);
        Type util = assembly.GetType("BetterOutfitStand.OutfitStandHaulGizmoUtility", true);
        Type driverType = assembly.GetType("BetterOutfitStand.JobDriver_UseOutfitStandBetter", true);
        foreach (var target in new[] { AccessTools.Method(typeof(ThingWithComps), "GetGizmos"),
            AccessTools.Method(typeof(Building_OutfitStand), "Notify_ItemAdded"),
            AccessTools.Method(typeof(Building_OutfitStand), "Notify_ItemRemoved") })
            Require(Harmony.GetPatchInfo(target)?.Postfixes.Any(p => p.owner == PackageId && p.PatchMethod.DeclaringType.Assembly == assembly) == true,
                "The actual MOD postfix is registered on " + target.Name);
        ThingDef standDef = DefDatabase<ThingDef>.GetNamed("Building_OutfitStand");
        Require(standDef.thingClass == standType, "XML replaces the real adult stand class.");
        Require(DefDatabase<ThingDef>.GetNamed("Building_KidOutfitStand").thingClass.FullName == "BetterOutfitStand.Building_BetterKidOutfitStand",
            "XML replaces the real child stand class.");
        JobDef jobDef = DefDatabase<JobDef>.GetNamed("UseOutfitStand_Better");
        Require(jobDef.driverClass == driverType, "The job def resolves the real MOD driver.");
        IntVec3 cell = CellRect.CenteredOn(map.Center, 25).FirstOrDefault(c => c.InBounds(map) && !c.Fogged(map) &&
            GenAdj.CellsAdjacent8Way(c, Rot4.North, IntVec2.One).All(n => n.InBounds(map) && n.Standable(map) && n.GetThingList(map).Count == 0) &&
            c.Standable(map) && c.GetThingList(map).Count == 0 && (c + IntVec3.East * 3).InBounds(map) &&
            (c + IntVec3.East * 3).Standable(map) && (c + IntVec3.East * 3).GetThingList(map).Count == 0, IntVec3.Invalid);
        Require(cell.IsValid, "Disposable stand and pawn fixture cells exist.");
        var stand = (Building_OutfitStand)ThingMaker.MakeThing(standDef, ThingDefOf.WoodLog);
        Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
            forceGenerateNewPawn: true, mustBeCapableOfViolence: true, fixedBiologicalAge: 30f,
            forceNoBackstory: true, forceNoGear: true));
        Apparel worn = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_BasicShirt"), ThingDefOf.Cloth);
        Apparel held = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_BasicShirt"), ThingDefOf.Cloth);
        ThingWithComps oldWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Autopistol"));
        ThingWithComps newWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Revolver"));
        ThingWithComps floorWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Autopistol"));
        try
        {
            stand.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(stand, cell, map); GenSpawn.Spawn(pawn, cell + IntVec3.East * 3, map);
            foreach (Apparel initial in pawn.apparel.WornApparel.ToList()) { pawn.apparel.Remove(initial); initial.Destroy(DestroyMode.Vanish); }
            if (pawn.equipment.Primary is ThingWithComps initialWeapon) { pawn.equipment.Remove(initialWeapon); initialWeapon.Destroy(DestroyMode.Vanish); }
            Require((bool)driverType.GetMethod("PawnCanWearApparel").Invoke(null, new object[] { held, pawn }), "A real human can wear the fixture shirt.");
            pawn.apparel.Wear(worn); pawn.equipment.AddEquipment(oldWeapon);
            Require((bool)driverType.GetMethod("PawnCanWieldWeapon", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { newWeapon, pawn }), "Actual equipment eligibility accepts the fixture weapon.");
            stand.GetStoreSettings().filter.SetAllow(held.def, false);
            Require(stand.AddApparel(held), "Native stand accepts fixture apparel.");
            Require(stand.GetStoreSettings().filter.Allows(held.def), "Real Notify_ItemAdded postfix reallows the held def.");
            Require(stand.TryAddHeldWeapon(newWeapon), "Native stand accepts the replacement weapon.");
            GenSpawn.Spawn(floorWeapon, cell + IntVec3.North, map);
            Require((bool)util.GetMethod("ShouldOfferFor").Invoke(null, new object[] { floorWeapon }), "Spawned real weapon qualifies for the haul command.");
            Command_Target command = floorWeapon.GetGizmos().OfType<Command_Target>().Single(g =>
                g.defaultLabel == "BOS.HaulToTargetOutfitStand".Translate());
            Require(!command.Disabled && command.targetingParams.validator(new TargetInfo(stand)), "Actual gizmo accepts a real player stand.");
            Require(!(bool)util.GetMethod("ShouldOfferFor").Invoke(null, new object[] { held }), "Held apparel does not qualify as a floor haul target.");
            var toStand = (HashSet<Apparel>)standType.GetField("WornApparelToTransferToStand").GetValue(stand);
            var toPawn = (HashSet<Apparel>)standType.GetField("StandApparelToTransferToPawn").GetValue(stand);
            toStand.Add(worn); toPawn.Add(held);
            Job job = JobMaker.MakeJob(jobDef, stand);
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            JobDriver driver = pawn.jobs.curDriver;
            Require(driver?.GetType() == driverType && toStand.Count == 0 && toPawn.Count == 0,
                "Actual job startup consumes both pending transfer selections.");
            // Exercise the real registered transfer callback with real native containers/equipment;
            // path travel and duration are intentionally not simulated here.
            var toils = (List<Toil>)AccessTools.Field(typeof(JobDriver), "toils").GetValue(driver);
            Action transfer = toils.Last().finishActions.Single(a => a.Method.DeclaringType == driverType);
            transfer();
            Require(pawn.apparel.WornApparel.Contains(held) && !pawn.apparel.WornApparel.Contains(worn) &&
                stand.HeldItems.Contains(worn) && !stand.HeldItems.Contains(held), "Both directions transfer through actual native apparel owners.");
            Require(pawn.outfits.forcedHandler.IsForced(held), "Transferred apparel is actually forced for the pawn.");
            Require(pawn.equipment.Primary == newWeapon && stand.HeldWeapon == oldWeapon && !oldWeapon.Spawned,
                "Actual equipment replacement drops then stores the old weapon on the stand.");
            Require(stand.GetStoreSettings().filter.Allows(held.def) && stand.GetStoreSettings().filter.Allows(oldWeapon.def),
                "Transfer enables destination filters while paused automatic removal leaves the source apparel def allowed.");
            Require(stand.GetFloatMenuOptions(pawn).Any(o => o.Label == "BetterOutfitStand".Translate()), "The custom stand float menu is exposed.");
            Log.Message("[RW17.Validation] Mod68 checks passed: actual_patch_targets, xml_classes, native_job_start, " +
                "held_filter, spawned_weapon_gizmo, target_validator, two_way_apparel_transfer, forced_outfit, weapon_swap, filter_disable_paused, float_menu");
        }
        finally
        {
            if (pawn.Spawned && pawn.jobs.curJob != null) pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            foreach (Thing thing in new Thing[] { floorWeapon, worn, held, oldWeapon, newWeapon, stand, pawn })
                if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
