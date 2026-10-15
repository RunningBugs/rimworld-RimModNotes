using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RW17.Validation;

public static class Mod02Checks
{
    public const string PackageId = "RunningBugs.ItemPolicy";
    public static void Run(Map map)
    {
        var type = AccessTools.TypeByName("_ItemPolicy.ItemPolicyUtility");
        Require(Current.Game.GetComponent(type) != null, "GameComponent missing");
        var target = AccessTools.Method(typeof(JobGiver_DropUnusedInventory), "ShouldKeepDrugInInventory");
        Require(Harmony.GetPatchInfo(target)?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.FullName
            == "_ItemPolicy.ShouldKeepDrugInInventoryPatch" && p.PatchMethod.Name == "Postfix") == true,
            "Concrete drug-retention postfix missing");
        var postfix = Harmony.GetPatchInfo(target).Postfixes.Single(p => p.PatchMethod.DeclaringType?.FullName
            == "_ItemPolicy.ShouldKeepDrugInInventoryPatch" && p.PatchMethod.Name == "Postfix");
        var control = new Harmony(postfix.owner);
        var pawn = map.mapPawns.FreeColonists.FirstOrDefault(p => !p.inventory.UnloadEverything
            && MassUtility.Capacity(p) - MassUtility.GearAndInventoryMass(p) > 1f);
        Require(pawn != null, "A colonist with spare carrying capacity is required");
        var policies = (IDictionary)type.GetField("policies").GetValue(null);
        var hadPolicy = policies.Contains(pawn);
        var oldPolicy = policies[pawn];
        var state = (IDictionary)AccessTools.Field(AccessTools.TypeByName("_ItemPolicy.PawnStateTracker"),
            "nextInventoryStockTick").GetValue(null);
        var hadTick = state.Contains(pawn);
        var oldTick = state[pawn];
        Thing stock = null, drug = null;
        Job issued = null;
        Rand.PushState(17);
        try
        {
            policies.Remove(pawn);
            state[pawn] = -9999;
            var stockDef = ThingDefOf.Silver;
            var needed = pawn.inventory.Count(stockDef) + 3;
            Set(stockDef, needed);
            Set(ThingDefOf.MedicineHerbal, -2);
            Require(Get(stockDef) == needed && Get(ThingDefOf.MedicineHerbal) == 0,
                "Policy positive count or nonpositive clamp failed");
            type.GetMethod("RemoveItemPolicyEntry").Invoke(null, new object[] { pawn, ThingDefOf.MedicineHerbal });
            var policy = type.GetMethod("GetPawnPolicy").Invoke(null, new object[] { pawn });
            var giver = (ThinkNode_JobGiver)Activator.CreateInstance(
                AccessTools.TypeByName("_ItemPolicy.JobGiver_TakeItemForInventoryStock"));
            Require((bool)giver.GetType().GetMethod("AnyThingsRequiredNow").Invoke(giver, new[] { pawn, policy }),
                "Missing stock was not detected");
            var cell = GenRadial.RadialCellsAround(pawn.Position, 15f, false)
                .First(c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetThingList(map).Count == 0);
            stock = ThingMaker.MakeThing(stockDef);
            stock.stackCount = 10;
            GenSpawn.Spawn(stock, cell, map);
            stock.SetForbidden(false);
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            issued = giver.TryIssueJobPackage(pawn, new JobIssueParams()).Job;
            Require(issued != null && issued.def == JobDefOf.TakeInventory
                && issued.targetA.Thing.def == stockDef && issued.count > 0 && issued.count <= 3,
                "Actual ThinkNode job-package call did not issue a bounded TakeInventory job");

            control.Unpatch(target, postfix.PatchMethod);
            try
            {
                foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsDrug))
                {
                    var candidate = ThingMaker.MakeThing(def);
                    if (!JobGiver_DropUnusedInventory.ShouldKeepDrugInInventory(pawn, candidate))
                    { drug = candidate; break; }
                    candidate.Destroy(DestroyMode.Vanish);
                }
            }
            finally
            {
                control.Patch(target, postfix: new HarmonyMethod(postfix.PatchMethod)
                    { priority = postfix.priority, before = postfix.before, after = postfix.after });
            }
            Require(drug != null, "No vanilla-discarded drug available for the control");
            Set(drug.def, 1);
            Require(JobGiver_DropUnusedInventory.ShouldKeepDrugInInventory(pawn, drug),
                "Policy did not retain the drug through the real patched method");
            Set(drug.def, 0);
            Require(!JobGiver_DropUnusedInventory.ShouldKeepDrugInInventory(pawn, drug),
                "Zero policy incorrectly retained the control drug");

            var policyType = policy.GetType();
            var other = Activator.CreateInstance(policyType);
            ((IDictionary)policyType.GetField("data").GetValue(other))[stockDef] = 2;
            Require(ReferenceEquals(policyType.GetMethod("MergePolicy").Invoke(policy, new[] { other }), policy)
                && ((IDictionary)policyType.GetField("data").GetValue(policy)).Count == 1,
                "Policy replacement did not return the target or clear old entries");
            ((IDictionary)policyType.GetField("data").GetValue(other))[stockDef] = 99;
            Require(Get(stockDef) == 2, "Merged policy aliases the source dictionary");
            var found = (IEnumerable)type.GetMethod("Search").Invoke(null, new object[] { stockDef.label });
            Require(found.Cast<ThingDef>().Any(d => d.label.Contains(stockDef.label)),
                "GameComponent did not initialize the search index");
            type.GetMethod("RemoveItemPolicyEntry").Invoke(null, new object[] { pawn, stockDef });
            Require(!policies.Contains(pawn), "Removing last entry did not remove pawn policy");
        }
        finally
        {
            if (issued != null) JobMaker.ReturnToPool(issued);
            if (stock != null && !stock.Destroyed) stock.Destroy(DestroyMode.Vanish);
            if (drug != null && !drug.Destroyed) drug.Destroy(DestroyMode.Vanish);
            if (hadPolicy) policies[pawn] = oldPolicy; else policies.Remove(pawn);
            if (hadTick) state[pawn] = oldTick; else state.Remove(pawn);
            Rand.PopState();
        }
        void Set(ThingDef def, int count) => type.GetMethod("SetItemPolicyEntry")
            .Invoke(null, new object[] { pawn, def, count });
        int Get(ThingDef def) => (int)type.GetMethod("GetItemPolicyEntry")
            .Invoke(null, new object[] { pawn, def });
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Mod02: " + message);
    }
}
