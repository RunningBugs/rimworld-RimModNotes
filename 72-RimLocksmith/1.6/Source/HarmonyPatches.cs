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

[HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
public static class Patch_BuildingDoor_PawnCanOpen
{
    public static bool Prefix(Building_Door __instance, Pawn p, ref bool __result)
    {
        if (RimLocksmithUtility.TryAllowsOpen(__instance, p, out bool allowed))
        {
            __result = allowed;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(PathUtility), nameof(PathUtility.GetDoorCost))]
public static class Patch_PathUtility_GetDoorCost
{
    public static bool Prefix(Building_Door door, TraverseParms traverseParms, Pawn pawn, ref ushort __result)
    {
        if (traverseParms.mode == TraverseMode.NoPassClosedDoors || traverseParms.mode == TraverseMode.NoPassClosedDoorsOrWater)
        {
            return true;
        }
        if (traverseParms.mode == TraverseMode.ByPawn && !traverseParms.canBashDoors && pawn != null && door.IsForbiddenToPass(pawn))
        {
            return true;
        }
        if (!RimLocksmithUtility.TryAllowsOpen(door, pawn, out bool allowed))
        {
            return true;
        }
        if (allowed)
        {
            __result = (ushort)door.TicksToOpenNow;
            return false;
        }
        if (traverseParms.canBashDoors)
        {
            __result = 300;
            return false;
        }
        __result = ushort.MaxValue;
        return false;
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

    public static bool Prefix(object __instance, Pawn p, ref bool __result)
    {
        if (__instance is Building_Door door && RimLocksmithUtility.TryAllowsOpen(door, p, out bool allowed))
        {
            __result = allowed;
            return false;
        }
        return true;
    }
}
