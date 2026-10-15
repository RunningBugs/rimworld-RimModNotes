using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RunningBugs.RimLocksmith;

[HarmonyPatch(typeof(MainTabWindow_Inspect), "get_ShouldShowPaneContents")]
public static class Patch_MainTabWindowInspect_ShouldShowPaneContents
{
    public static void Postfix(ref bool __result)
    {
        if (!__result && RimLocksmithUtility.SelectionIsOnlyDoorsWithAtLeastOneColonyDoor())
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(MainTabWindow_Inspect), "get_CurTabs")]
public static class Patch_MainTabWindowInspect_CurTabs
{
    public static void Postfix(ref IEnumerable<InspectTabBase> __result)
    {
        if (__result != null)
        {
            return;
        }
        if (!RimLocksmithUtility.SelectionIsOnlyDoorsWithAtLeastOneColonyDoor())
        {
            return;
        }

        List<Building_Door> configurableDoors = RimLocksmithUtility.SelectedConfigurableColonyDoors();
        if (configurableDoors.Count > 0)
        {
            __result = configurableDoors[0].GetInspectTabs();
        }
    }
}
