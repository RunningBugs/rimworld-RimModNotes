using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DubsMintMenusRightClickAction;

[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        new Harmony("com.runningbugs.dubsmintmenusrightclickaction").PatchAll(typeof(Start).Assembly);
        Logger.Log.Message("Loaded");
    }
}

[HarmonyPatch]
public static class RightClickActionPatch
{
    public static MethodBase TargetMethod() => AccessTools.Method(
        AccessTools.TypeByName("DubsMintMenus.Patch_BillStack_DoListing"), "DoRow",
        new[] { typeof(RecipeDef), typeof(HashSet<Building>), typeof(Precept_Building) });

    public static bool RecipeButton(Rect rect, bool mouseover, RecipeDef recipe)
    {
        if (!Widgets.ButtonInvisible(rect, mouseover)) return false;
        var e = Event.current;
        if (e == null || e.type != EventType.Used || e.button != 1) return true;
        if (recipe != null)
            foreach (var product in recipe.products)
                Find.WindowStack.Add(new Dialog_InfoCard(product.thingDef));
        return false;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var button = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonInvisible),
            new[] { typeof(Rect), typeof(bool) });
        var replacement = AccessTools.Method(typeof(RightClickActionPatch), nameof(RecipeButton));
        var result = new List<CodeInstruction>();
        int matches = 0;
        foreach (var original in instructions)
        {
            if (original.Calls(button))
            {
                var recipeArg = new CodeInstruction(OpCodes.Ldarg_0);
                recipeArg.labels.AddRange(original.labels);
                recipeArg.blocks.AddRange(original.blocks);
                result.Add(recipeArg);
                result.Add(new CodeInstruction(OpCodes.Call, replacement));
                matches++;
            }
            else result.Add(new CodeInstruction(original));
        }
        if (matches != 1)
            throw new InvalidOperationException("Expected one Dubs DoRow recipe button, found " + matches);
        return result;
    }
}
