using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace AAT;

[HarmonyPatch(typeof(ReverseDesignatorDatabase), "InitDesignators")]
public static class ReverseDesignatorDatabase_InitDesignators_Patch
{
    public static void Postfix(ReverseDesignatorDatabase __instance)
    {
        FieldInfo field = typeof(ReverseDesignatorDatabase).GetField("desList", BindingFlags.Instance | BindingFlags.NonPublic);
        List<Designator> desList = field?.GetValue(__instance) as List<Designator>;
        desList?.Add(new Designator_HaulUrgent());
    }
}
