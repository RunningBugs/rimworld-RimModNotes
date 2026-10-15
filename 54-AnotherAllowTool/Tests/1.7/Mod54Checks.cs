using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RW17.Validation;

public static class Mod54Checks
{
    public const string PackageId = "RunningBugs.AnotherAllowTool";

    public static void Run(Map map)
    {
        Require(map == Find.CurrentMap, "Run on the fully initialized current disposable map.");
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "AAT");
        Designator Tool(string name) => (Designator)Activator.CreateInstance(assembly.GetType("AAT." + name));
        foreach (var target in new[]
        {
            AccessTools.Method(typeof(ReverseDesignatorDatabase), "InitDesignators"),
            AccessTools.Method(typeof(DesignationManager), "AddDesignation"),
            AccessTools.Method(typeof(DesignationManager), "RemoveDesignation"),
            AccessTools.Method(typeof(Thing), "GetGizmos"),
            AccessTools.Method(typeof(Toils_Haul), "PlaceHauledThingInCell", new[] { typeof(TargetIndex), typeof(Toil), typeof(bool), typeof(bool) }),
            AccessTools.Method(typeof(Toils_Haul), "DepositHauledThingInContainer", new[] { typeof(TargetIndex), typeof(TargetIndex), typeof(Action) })
        }) Require(Harmony.GetPatchInfo(target)?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.Assembly == assembly) == true,
            "All actual designator/cache/deposit targets have this MOD's postfix.");

        var reverse = new ReverseDesignatorDatabase();
        Require(reverse.AllDesignators.Any(d => d.GetType().FullName == "AAT.Designator_HaulUrgent") &&
            reverse.AllDesignators.Any(d => d.GetType().FullName == "AAT.Designator_HarvestFullyGrown"),
            "The real private reverse-designator initialization includes both tools.");
        IntVec3 cell = CellRect.CenteredOn(map.Center, 30).FirstOrDefault(c =>
            Enumerable.Range(0, 6).All(i => (c + new IntVec3(i, 0, 0)).InBounds(map) &&
                (c + new IntVec3(i, 0, 0)).Standable(map) && !(c + new IntVec3(i, 0, 0)).Fogged(map) &&
                (c + new IntVec3(i, 0, 0)).GetThingList(map).Count == 0), IntVec3.Invalid);
        Require(cell.IsValid, "A visible clear row for independent tool fixtures exists.");
        Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
        Thing otherSteel = ThingMaker.MakeThing(ThingDefOf.Steel);
        Thing wood = ThingMaker.MakeThing(ThingDefOf.WoodLog);
        Plant plant = (Plant)ThingMaker.MakeThing(ThingDefOf.Plant_Potato);
        Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
            Faction.OfPlayer, forceGenerateNewPawn: true, forceNoBackstory: true));
        Zone_Stockpile stockpile = null;
        var oldSelection = Find.Selector.SelectedObjects.ToList();
        var selectionPatch = assembly.GetType("AAT.Thing_GetGizmos_Patch");
        var selectedDef = selectionPatch.GetField("defToSelect"); var selectedStuff = selectionPatch.GetField("stuffToSelect");
        object oldDef = selectedDef.GetValue(null), oldStuff = selectedStuff.GetValue(null);
        try
        {
            GenSpawn.Spawn(steel, cell, map); GenSpawn.Spawn(otherSteel, cell + IntVec3.East, map);
            GenSpawn.Spawn(wood, cell + new IntVec3(2, 0, 0), map);
            GenSpawn.Spawn(plant, cell + new IntVec3(3, 0, 0), map);
            GenSpawn.Spawn(pawn, cell + new IntVec3(5, 0, 0), map);
            steel.SetForbidden(true, false);
            var allow = Tool("Designator_Allow"); var forbid = Tool("Designator_Forbid");
            Require(allow.CanDesignateThing(steel).Accepted, "The actual allow tool accepts a forbidden live item.");
            allow.DesignateThing(steel); Require(!steel.IsForbidden(Faction.OfPlayer), "Allow changes the real forbidden state.");
            Require(forbid.CanDesignateThing(steel).Accepted, "The actual forbid tool accepts the allowed item.");
            forbid.DesignateThing(steel); Require(steel.IsForbidden(Faction.OfPlayer), "Forbid changes it back.");
            allow.DesignateThing(steel);

            var urgent = Tool("Designator_HaulUrgent");
            var urgentDef = DefDatabase<DesignationDef>.GetNamed("HaulUrgentlyDesignation");
            Require(urgent.CanDesignateThing(steel).Accepted, "The real urgent tool accepts an unstored live item.");
            urgent.DesignateThing(steel);
            Require(map.designationManager.DesignationOn(steel, urgentDef) != null, "Urgent designation is really installed.");
            var cache = map.components.Single(c => c.GetType().FullName == "AAT.HaulUrgentlyCache");
            var cacheType = cache.GetType();
            var list = (IReadOnlyList<Thing>)cacheType.GetMethod("GetDesignatedAndHaulableThingsForMap")
                .Invoke(cache, new object[] { GenTicks.TicksGame });
            Require(list.Count(t => t == steel) == 1, "The actual cache intersects the real haulables without duplicates.");
            map.designationManager.TryRemoveDesignationOn(steel, urgentDef);
            Require((bool)cacheType.GetField("dirty").GetValue(cache), "Real removal dirties the map cache.");
            urgent.DesignateThing(steel);

            stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(stockpile); stockpile.AddCell(cell + new IntVec3(4, 0, 0));
            stockpile.GetStoreSettings().filter.SetDisallowAll(); stockpile.GetStoreSettings().filter.SetAllow(ThingDefOf.Steel, true);
            cacheType.GetMethod("ClearCache").Invoke(cache, null);
            var giver = (WorkGiver_HaulGeneral)DefDatabase<WorkGiverDef>.GetNamed("HaulUrgently").Worker;
            Require(!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling), "The actual worker can intrinsically haul.");
            Require(giver.PotentialWorkThingsGlobal(pawn).Contains(steel), "The real work giver sees the urgent item.");
            Job job = giver.JobOnThing(pawn, steel, true);
            Require(job?.def == JobDefOf.HaulToCell && job.targetA.Thing == steel, "The actual delegate produces a real storage haul job.");
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            Require(pawn.carryTracker.TryStartCarry(steel, steel.stackCount) > 0, "The real carrier picked up the urgent item.");
            Toil place = Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, null, true);
            place.actor = pawn; place.initAction();
            Require(steel.Spawned && stockpile.ContainsCell(steel.Position) && pawn.carryTracker.CarriedThing == null,
                "The actual patched native deposit places the item in the actual stockpile.");
            Require(map.designationManager.DesignationOn(steel, urgentDef) == null, "Actual delivery clears the urgent designation.");

            plant.Growth = 1f;
            var harvest = Tool("Designator_HarvestFullyGrown");
            Require(harvest.CanDesignateThing(plant).Accepted, "The real mature harvestable plant is accepted.");
            harvest.DesignateThing(plant);
            Require(map.designationManager.DesignationOn(plant, DesignationDefOf.HarvestPlant) != null,
                "The actual harvest tool installs native HarvestPlant designation.");
            Find.Selector.ClearSelection(); Find.Selector.Select(steel, false, false);
            Require(steel.GetGizmos().Any(g => g.GetType().FullName == "AAT.Designator_SelectSimilar"),
                "Actual Thing.GetGizmos exposes select-similar for the selected item.");
            var similar = Tool("Designator_SelectSimilar");
            Require(similar.CanDesignateThing(otherSteel).Accepted && !similar.CanDesignateThing(wood).Accepted,
                "The real similarity predicate accepts matching def/stuff and rejects different def.");
            similar.DesignateThing(otherSteel);
            Require(Find.Selector.IsSelected(otherSteel), "Actual designator modifies the real selection.");
            Log.Message("[RW17.Validation] Mod54 checks passed: six_patches, reverse_designators, allow_forbid, " +
                "urgent_designation_cache, real_haul_job, actual_cell_delivery, urgent_cleanup, mature_harvest, select_similar");
        }
        finally
        {
            Find.Selector.ClearSelection(); selectedDef.SetValue(null, oldDef); selectedStuff.SetValue(null, oldStuff);
            foreach (var obj in oldSelection) Find.Selector.Select(obj, false, false);
            if (stockpile != null) map.zoneManager.DeregisterZone(stockpile);
            foreach (Thing thing in new Thing[] { steel, otherSteel, wood, plant, pawn })
                if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
