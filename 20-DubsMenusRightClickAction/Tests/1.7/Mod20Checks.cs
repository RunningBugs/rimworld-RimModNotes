using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod20Checks
{
    public const string PackageId = "RunningBugs.DubsMenusRightClickAction";
    public static void Run(Map map)
    {
        var upstream = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "DubsMintMenus");
        var row = upstream.GetType("DubsMintMenus.Patch_BillStack_DoListing").GetMethod("DoRow",
            BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(RecipeDef), typeof(HashSet<Building>), typeof(Precept_Building) }, null);
        var plant = upstream.GetType("DubsMintMenus.Dialog_FancyDanPlantSetterBob")
            .GetMethod("Clicked", new[] { typeof(ThingDef) });
        var patchType = AccessTools.TypeByName("DubsMintMenusRightClickAction.RightClickActionPatch");
        var transpiler = AccessTools.Method(patchType, "Transpiler");
        Require(row != null && plant != null, "Actual installed 1.7 upstream entry points missing");
        Require(Harmony.GetPatchInfo(row)?.Transpilers.Any(p => p.PatchMethod == transpiler) == true,
            "Concrete recipe-row transpiler missing");
        Require(Harmony.GetPatchInfo(plant)?.Prefixes.Any(p => p.PatchMethod.DeclaringType?.FullName
            == "DubsMintMenusRightClickAction.PlantsViewPatch" && p.PatchMethod.Name == "Prefix") == true,
            "Concrete plant-selection prefix missing");
        var original = PatchProcessor.GetOriginalInstructions(row);
        var transformed = (IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { original });
        var wrapper = AccessTools.Method(patchType, "RecipeButton");
        Require(transformed.Count(i => i.Calls(wrapper)) == 1, "Actual original DoRow IL did not yield exactly one wrapper");
        Console.WriteLine("RW17_MOD20_STATIC_CONTRACTS_OK; actual right-click/rendering remains pending");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Mod20: " + message);
    }
}
