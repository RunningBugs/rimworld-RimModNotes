using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace RunningBugs.RimLocksmith;

[StaticConstructorOnStartup]
public static class RimLocksmithStartup
{
    static RimLocksmithStartup() { }
}

/// <summary>
/// postfix 只收窄:原版 PawnCanOpen 已包含全部特殊判定(敌对/囚犯/围栏/
/// CanOpenAnyDoor/Released 等),Mod 仅在原版允许时按门配置进一步拦下
/// 可配置类别,永远不把原版的 false 改成 true。
/// </summary>
[HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
public static class Patch_BuildingDoor_PawnCanOpen
{
    public static void Postfix(Building_Door __instance, Pawn p, ref bool __result)
    {
        if (__result && RimLocksmithUtility.ShouldDeny(__instance, p))
        {
            __result = false;
        }
    }
}

[HarmonyPatch]
public static class Patch_KnownCompatDoor_PawnCanOpen
{
    public static bool Prepare()
    {
        return AccessTools.Method("DoorsExpanded.Building_DoorExpanded:PawnCanOpen") != null
            || AccessTools.Method("Building_DoorExpanded:PawnCanOpen") != null;
    }

    public static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo doorsExpanded = AccessTools.Method("DoorsExpanded.Building_DoorExpanded:PawnCanOpen");
        if (doorsExpanded != null) yield return doorsExpanded;

        MethodInfo legacyDoorsExpanded = AccessTools.Method("Building_DoorExpanded:PawnCanOpen");
        if (legacyDoorsExpanded != null) yield return legacyDoorsExpanded;
    }

    public static void Postfix(object __instance, Pawn p, ref bool __result)
    {
        if (__result && __instance is Building_Door door && RimLocksmithUtility.ShouldDeny(door, p))
        {
            __result = false;
        }
    }
}
