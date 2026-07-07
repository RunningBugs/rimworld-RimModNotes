using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace BetterOutfitStand;

[StaticConstructorOnStartup]
public static class BetterOutfitStandHarmony
{
    static BetterOutfitStandHarmony()
    {
        new Harmony("RunningBugs.BetterOutfitStand").PatchAll();
    }
}

[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetGizmos))]
public static class ThingWithComps_GetGizmos_Patch
{
    public static void Postfix(ThingWithComps __instance, ref IEnumerable<Gizmo> __result)
    {
        if (!OutfitStandHaulGizmoUtility.ShouldOfferFor(__instance))
        {
            return;
        }
        __result = AppendOutfitStandGizmo(__result, __instance);
    }

    private static IEnumerable<Gizmo> AppendOutfitStandGizmo(IEnumerable<Gizmo> original, Thing thing)
    {
        foreach (Gizmo gizmo in original)
        {
            yield return gizmo;
        }
        yield return OutfitStandHaulGizmoUtility.MakeCommand(thing);
    }
}

[HarmonyPatch(typeof(RimWorld.Building_OutfitStand), nameof(RimWorld.Building_OutfitStand.Notify_ItemAdded))]
public static class BuildingOutfitStand_NotifyItemAdded_Patch
{
    public static void Postfix(RimWorld.Building_OutfitStand __instance, Thing thing)
    {
        OutfitStandHaulGizmoUtility.AllowDefOnStand(__instance, thing?.def);
    }
}

[HarmonyPatch(typeof(RimWorld.Building_OutfitStand), nameof(RimWorld.Building_OutfitStand.Notify_ItemRemoved))]
public static class BuildingOutfitStand_NotifyItemRemoved_Patch
{
    public static void Postfix(RimWorld.Building_OutfitStand __instance, Thing thing)
    {
        OutfitStandHaulGizmoUtility.DisableDefIfNoLongerHeld(__instance, thing?.def);
    }
}
