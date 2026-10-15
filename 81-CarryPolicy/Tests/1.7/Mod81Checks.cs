using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RW17.Validation;
public static class Mod81Checks
{
    public const string PackageId = "RunningBugs.CarryPolicy";
    public static void Run(Map map)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "CarryPolicy");
        const string owner = "com.RunningBugs.CarryPolicy";
        MethodInfo unload = AccessTools.PropertyGetter(typeof(Pawn_InventoryTracker), "FirstUnloadableThing");
        MethodInfo drop = AccessTools.Method(typeof(JobGiver_DropUnusedInventory), "Drop");
        foreach (var target in new[] { unload, drop })
            Require(Harmony.GetPatchInfo(target)?.Prefixes.Any(p => p.owner == owner && p.PatchMethod.DeclaringType.Assembly == assembly) == true,
                "Actual inventory prefix registered on " + target.Name);
        var jobGiver = DefDatabase<ThinkTreeDef>.GetNamed("Humanlike").thinkRoot.ThisAndChildrenRecursive.OfType<ThinkNode_JobGiver>()
            .Single(n => n.GetType().FullName == "_CarryPolicy.JobGiver_CarryPolicy");
        Require(DefDatabase<PawnTableDef>.GetNamed("Assign").columns.Any(c => c.workerClass.FullName == "_CarryPolicy.PawnColumnWorker_CarryPolicy"),
            "Actual Assign table XML contains the carry policy column.");
        Type utility = assembly.GetType("_CarryPolicy.CarryPolicyUtility", true), dbType = assembly.GetType("_CarryPolicy.CarryPolicyDatabase", true);
        object db = Current.Game.GetComponent(dbType); Require(db != null, "Actual per-Game policy database is registered.");
        var policies = (IList)dbType.GetField("policies").GetValue(db); var originals = policies.Cast<object>().ToList();
        IDictionary assignments = (IDictionary)dbType.GetField("assignments").GetValue(db);
        IDictionary next = (IDictionary)dbType.GetField("nextCheckTick").GetValue(db);
        Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
            forceGenerateNewPawn: true, forceNoBackstory: true, forceNoGear: true, canGeneratePawnRelations: false));
        var owned = new List<Thing> { pawn }; var originalThings = map.listerThings.AllThings.ToHashSet();
        object policy = null;
        IntVec3 cell = CellRect.CenteredOn(map.Center, 25).First(c => c.InBounds(map) && !c.Fogged(map) && c.Standable(map) && c.GetThingList(map).Count == 0 &&
            (c + IntVec3.East).InBounds(map) && (c + IntVec3.East).Standable(map) && (c + IntVec3.East).GetThingList(map).Count == 0);
        Type entryType = assembly.GetType("_CarryPolicy.CarryPolicyEntry", true);
        object Entry(ThingDef def, float count) { object e = Activator.CreateInstance(entryType); entryType.GetField("thing").SetValue(e, def); entryType.GetField("count").SetValue(e, count); return e; }
        int Target(object entry) => (int)utility.GetMethod("TargetCount").Invoke(null, new object[] { entry, pawn });
        int Baseline(object entry) => (int)utility.GetMethod("BaselineTargetCount").Invoke(null, new object[] { entry });
        void Fresh() { next.Remove(pawn); pawn.inventory.UnloadEverything = false; }
        Job Give() => jobGiver.TryIssueJobPackage(pawn, default).Job;
        Thing Inventory(ThingDef def, int count)
        { Thing thing = ThingMaker.MakeThing(def); thing.stackCount = count; owned.Add(thing); Require(pawn.inventory.innerContainer.TryAdd(thing), "Real native inventory accepts fixture stack."); return thing; }
        try
        {
            GenSpawn.Spawn(pawn, cell, map); pawn.inventory.innerContainer.ClearAndDestroyContents(); pawn.inventoryStock.stockEntries.Clear();
            policy = dbType.GetMethod("MakeNewPolicy").Invoke(db, null); utility.GetMethod("SetPolicyFor").Invoke(null, new[] { pawn, policy });
            Require(ReferenceEquals(utility.GetMethod("CurrentPolicyFor").Invoke(null, new object[] { pawn }), policy), "Actual Pawn reference assignment resolves the selected policy.");
            Require(!((AcceptanceReport)dbType.GetMethod("TryDelete").Invoke(db, new[] { policy })).Accepted, "An actually assigned live Pawn prevents policy deletion.");
            IList entries = (IList)policy.GetType().GetField("entries").GetValue(policy);
            InventoryStockGroupDef medicineGroup = DefDatabase<InventoryStockGroupDef>.AllDefs.First(g => g.thingDefs.Contains(ThingDefOf.MedicineIndustrial));
            ThingDef medicine = medicineGroup.thingDefs.First(d => d.stackLimit >= 10 && map.listerThings.ThingsOfDef(d).Count == 0);
            int nativeQuota = Math.Min(3, medicineGroup.max);
            Require(nativeQuota > 1, $"Native medicine stock supplies two distinct valid quotas (min={medicineGroup.min}, max={medicineGroup.max}).");
            object medEntry = Entry(medicine, 1), steelEntry = Entry(ThingDefOf.Steel, 5); entries.Add(medEntry); entries.Add(steelEntry);
            pawn.inventoryStock.SetThingForGroup(medicineGroup, medicine); pawn.inventoryStock.SetCountForGroup(medicineGroup, nativeQuota);
            Thing med = Inventory(medicine, nativeQuota), steel = Inventory(ThingDefOf.Steel, 9);
            var actual = pawn.inventory.FirstUnloadableThing;
            Require(actual.Thing == steel && actual.Count == 4, "Actual getter scans past native-stock-protected medicine and keeps five policy steel.");
            var patch = Harmony.GetPatchInfo(unload).Prefixes.Single(p => p.owner == owner); var control = new Harmony(owner);
            ThingCount native;
            control.Unpatch(unload, patch.PatchMethod);
            try { native = pawn.inventory.FirstUnloadableThing; }
            finally { control.Patch(unload, prefix: new HarmonyMethod(patch.PatchMethod) { priority = patch.priority, before = patch.before, after = patch.after }); }
            Require(native.Thing == steel && native.Count == 9, "Same actual native inventory without this exact prefix unloads all steel.");
            pawn.inventory.UnloadEverything = true; Require(pawn.inventory.UnloadEverything, "Real unload setter remains active when later non-kept cargo exists."); pawn.inventory.UnloadEverything = false;
            object milkEntry = Entry(DefDatabase<ThingDef>.GetNamed("Milk"), 1); entries.Clear(); entries.Add(milkEntry);
            int milkTarget = Target(milkEntry);
            Require(Baseline(milkEntry) == 18 && Baseline(Entry(ThingDefOf.MealSimple, 1)) == 1 && milkTarget > 0 && milkTarget < 40,
                "Actual meal/milk nutrition conversion and Pawn-specific target have valid physical quantities.");
            pawn.inventory.innerContainer.ClearAndDestroyContents(); Thing milk = Inventory(DefDatabase<ThingDef>.GetNamed("Milk"), 40);
            object dropper = new JobGiver_DropUnusedInventory(); drop.Invoke(dropper, new object[] { pawn, milk });
            Require(pawn.inventory.Count(DefDatabase<ThingDef>.GetNamed("Milk")) == milkTarget && map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("Milk")).Where(t => !originalThings.Contains(t)).Sum(t => t.stackCount) == 40 - milkTarget,
                "Actual private Drop path puts only excess milk on the real map and keeps the exact policy quota.");
            drop.Invoke(dropper, new object[] { pawn, pawn.inventory.innerContainer.First(t => t.def == DefDatabase<ThingDef>.GetNamed("Milk")) });
            Require(pawn.inventory.Count(DefDatabase<ThingDef>.GetNamed("Milk")) == milkTarget, "Actual repeat Drop protects milk at quota.");
            entries.Clear(); pawn.inventory.innerContainer.ClearAndDestroyContents(); pawn.inventoryStock.stockEntries.Clear(); Fresh(); Require(Give() == null, "Actual injected job giver has no work for an empty policy.");
            int delay = (int)next[pawn] - Find.TickManager.TicksGame; Require(delay >= 300 && delay <= 600, "No-deficit actual scheduling uses the short cooldown.");
            entryType.GetField("count").SetValue(medEntry, 3f);
            entries.Add(medEntry); Fresh(); Require(Give() == null, "A real missing stock creates no pickup job.");
            delay = (int)next[pawn] - Find.TickManager.TicksGame; Require(delay >= 2500 && delay <= 5000, "Missing reachable stock schedules long backoff.");
            Thing supply = ThingMaker.MakeThing(medicine); supply.stackCount = 10; owned.Add(supply); GenSpawn.Spawn(supply, cell + IntVec3.East, map);
            Require(Give() == null, "Actual per-Pawn throttle prevents immediate rescan."); Fresh(); Job job = Give();
            Require(job?.def == JobDefOf.TakeInventory && job.targetA.Thing == supply && job.count == 3, "Actual injected giver selects real reachable stock and only the deficit count.");
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            if (pawn.inventory.Count(medicine) != 3)
            {
                Require(pawn.jobs.curDriver is JobDriver_TakeInventory, "Native inventory JobDriver was initialized.");
                var toils = (List<Toil>)AccessTools.Field(typeof(JobDriver), "toils").GetValue(pawn.jobs.curDriver); toils.Last().initAction();
            }
            Require(pawn.inventory.Count(medicine) == 3 && supply.stackCount == 7, "Real native TakeToInventory moves only the three requested items.");
            if (pawn.jobs.curJob != null) pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            supply.Destroy(DestroyMode.Vanish); pawn.inventory.innerContainer.ClearAndDestroyContents(); entries.Clear(); entries.Add(Entry(ThingDefOf.MealNutrientPaste, 1)); Fresh();
            ThingDef dispenserDef = ThingDefOf.NutrientPasteDispenser;
            IntVec3 center = CellRect.CenteredOn(map.Center, 60).First(c => GenAdj.OccupiedRect(c, Rot4.North, dispenserDef.size).Cells.All(n =>
                n.InBounds(map) && !n.Fogged(map) && n.Standable(map) && n.GetThingList(map).Count == 0));
            var dispenser = (Building_NutrientPasteDispenser)ThingMaker.MakeThing(dispenserDef); owned.Add(dispenser); dispenser.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(dispenser, center, map);
            IntVec3 hopperCell = dispenser.AdjCellsCardinalInBounds.First(c => c.Standable(map) && c.GetThingList(map).Count == 0);
            Thing hopper = ThingMaker.MakeThing(ThingDefOf.Hopper); owned.Add(hopper); hopper.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(hopper, hopperCell, map);
            Thing corn = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RawCorn")); corn.stackCount = 30; owned.Add(corn); GenSpawn.Spawn(corn, hopperCell, map);
            dispenser.powerComp.PowerOn = true;
            Require(dispenser.CanDispenseNow && pawn.CanReach(dispenser, PathEndMode.Touch, Danger.Some) && map.listerThings.ThingsOfDef(ThingDefOf.MealNutrientPaste).Count == 0,
                "A genuine powered dispenser/hopper/feedstock is reachable with no existing floor paste.");
            Job pasteJob = Give(); Require(pasteJob?.def == DefDatabase<JobDef>.GetNamed("CarryPolicyTakeFromDispenser") && pasteJob.targetA.Thing == dispenser,
                "Actual injected job giver selects the real working paste dispenser.");
            pawn.jobs.StartJob(pasteJob, JobCondition.InterruptForced);
            Require(pawn.jobs.curDriver?.GetType().Assembly == assembly, "Actual paste Job resolves to this MOD's driver.");
            int feedBefore = corn.stackCount;
            var pasteToils = (List<Toil>)AccessTools.Field(typeof(JobDriver), "toils").GetValue(pawn.jobs.curDriver); pasteToils.Last().initAction();
            Require(pawn.inventory.Count(ThingDefOf.MealNutrientPaste) == 1 && corn.stackCount < feedBefore,
                "The actual driver's native TryDispenseFood produces one inventory meal and consumes real hopper feedstock.");
            dispenser.powerComp.PowerOn = false;
            if (pawn.jobs.curJob != null) pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            pawn.inventory.innerContainer.ClearAndDestroyContents(); Fresh(); Require(Give() == null, "Actual powered-off dispenser is not selected.");
            Log.Message("[RW17.Validation] Mod81 checks passed: actual_xml_node_column, per_game_database_assignment_delete_guard, native_vs_mod_unload_scan, " +
                "unload_setter, physical_nutrition, actual_drop_excess_quota, short_long_throttle, real_pickup_deficit_and_transfer, powered_dispenser_hopper_feed, actual_driver_one_meal, no_power_guard");
        }
        finally
        {
            if (pawn.Spawned && pawn.jobs.curJob != null) pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            foreach (Thing thing in map.listerThings.AllThings.Where(t => !originalThings.Contains(t)).ToList()) if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
            foreach (Thing thing in owned) if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
            assignments.Remove(pawn); next.Remove(pawn);
            foreach (object value in policies.Cast<object>().Where(p => !originals.Contains(p)).ToList()) policies.Remove(value);
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
