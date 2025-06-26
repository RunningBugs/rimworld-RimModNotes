using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace AAT;

/**
*   Patches on ReverseDesignatorDatabase will skip the Dragger by default
*   It's called through MapGizmoUtility.MapUIOnGUI, which is later than the DesignationManager
*   Patches on Thing GetGizmos will call the dragger through the DesignationManager update
*/
[HarmonyPatch(typeof(ReverseDesignatorDatabase), "InitDesignators")]
public static class ReverseDesignatorDatabase_InitDesignators_Patch
{
    public static void Postfix(ReverseDesignatorDatabase __instance)
    {
        FieldInfo field = typeof(ReverseDesignatorDatabase).GetField("desList", BindingFlags.Instance | BindingFlags.NonPublic);
        List<Designator> desList = field?.GetValue(__instance) as List<Designator>;
        desList?.Add(new Designator_HaulUrgent());
        // desList?.Add(new Designator_SelectSimilar());
    }
}

[HarmonyPatch(typeof(Thing), "GetGizmos")]
public static class Thing_GetGizmos_Patch
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Thing __instance)
    {
        List<Gizmo> gizmos = new(__result);

        if (__instance.Spawned && __instance.MapHeld != null && Find.Selector.NumSelected == 1)
        {
            gizmos.Add(new Designator_SelectSimilar());
        }

        return gizmos;
    }
}
