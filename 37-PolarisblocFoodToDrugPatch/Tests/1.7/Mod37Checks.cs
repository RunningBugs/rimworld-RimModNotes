using System;
using RimWorld;
using Verse;
namespace RW17.Validation;
public static class Mod37Checks
{
    public const string PackageId = "RunningBugs.PolarisblocFoodToDrugPatch";
    public static void Run(Map map)
    {
        var food = DefDatabase<ThingDef>.GetNamed("Polarisbloc_EmergencyFood");
        var props = food.GetCompProperties<CompProperties_Drug>();
        if (!food.IsDrug || food.ingestible.drugCategory != DrugCategory.Medical ||
            food.orderedTakeGroup != DefDatabase<OrderedTakeGroupDef>.GetNamed("Drug") || props == null || props.listOrder != 1200 || !props.teetotalerCanConsume)
            throw new Exception("Mod37: real SF food does not have the intended drug-policy properties");
        var policy = new DrugPolicy(987654, "RW17 disposable food policy");
        if (policy[food].drug != food || ThingDefOf.MealSurvivalPack.IsDrug)
            throw new Exception("Mod37: actual policy inclusion or ordinary food negative control failed");
        Console.WriteLine("RW17_MOD37_ACTUAL_SF_FOOD_AND_DRUG_POLICY_OK; Mechenemy branch and ingestion/culture remain untested");
    }
}
