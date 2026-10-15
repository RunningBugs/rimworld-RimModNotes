using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod44Checks
{
    public const string PackageId = "RunningBugs.NotSoManyPolicies";

    public static void Run(Map map)
    {
        VerifyPatch(typeof(OutfitDatabase), "GenerateStartingOutfits", "Template.OutfitDatabase_GenerateStartingOutfits_Patch");
        VerifyPatch(typeof(FoodRestrictionDatabase), "GenerateStartingFoodRestrictions", "Template.FoodRestrictionDatabase_GenerateStartingFoodRestrictions_Patch");
        var outfits = new OutfitDatabase();
        if (outfits.AllOutfits.Count != 1 || outfits.AllOutfits[0].label != "OutfitAnything".Translate().ToString())
            throw new InvalidOperationException("Mod44 actual outfit constructor must retain only Anything");
        if (outfits.DefaultOutfit() != outfits.AllOutfits[0])
            throw new InvalidOperationException("Mod44 remaining default outfit is invalid");
        var food = new FoodRestrictionDatabase();
        var expected = new[] { "FoodRestrictionLavish".Translate().ToString(), "FoodRestrictionNothing".Translate().ToString() };
        if (food.AllFoodRestrictions.Count != 2 || !food.AllFoodRestrictions.Select(p => p.label).SequenceEqual(expected))
            throw new InvalidOperationException("Mod44 actual food constructor must retain only Lavish/Nothing");
        if (!food.AllFoodRestrictions[0].filter.Allows(ThingDefOf.MealSimple) || food.AllFoodRestrictions[1].filter.Allows(ThingDefOf.MealSimple))
            throw new InvalidOperationException("Mod44 retained food policy filters have wrong semantics");
        outfits.MakeNewOutfit();
        food.MakeNewFoodRestriction();
        if (outfits.AllOutfits.Count != 2 || food.AllFoodRestrictions.Count != 3)
            throw new InvalidOperationException("Mod44 must allow policies created after initialization");
        Log.Message("RW17 Mod44: actual constructor postfixes, retained policy/default/filter semantics and post-start additions passed");
    }

    private static void VerifyPatch(Type type, string method, string patchType)
    {
        var original = type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        if (original == null || Harmony.GetPatchInfo(original)?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.FullName == patchType) != true)
            throw new InvalidOperationException("Mod44 missing specific registered patch: " + patchType);
    }
}
