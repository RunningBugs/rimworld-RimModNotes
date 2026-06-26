using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CommonModCompatibilityPatches;

[StaticConstructorOnStartup]
public static class CommonCompatibilityBootstrap
{
    private static readonly Harmony Harmony = new("com.RunningBugs.CommonModCompatibilityPatches");

    static CommonCompatibilityBootstrap()
    {
        int applied = 0;
        applied += AllowToolHaulUrgentlyCompatibility.TryApply(Harmony) ? 1 : 0;
        applied += ReservationEventCompatibility.TryApply(Harmony) ? 1 : 0;
        applied += ReplaceStuffBridgeCompatibility.TryApply(Harmony) ? 1 : 0;

        Log.Message($"[CommonModCompatibilityPatches] Applied {applied} compatibility patch group(s).".Colorize(Color.green));
    }
}

internal static class ModDetection
{
    public static bool IsActive(string packageId)
    {
        return ModsConfig.ActiveModsInLoadOrder.Any(mod => string.Equals(mod.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
    }

    public static bool AnyActive(params string[] packageIds)
    {
        for (int i = 0; i < packageIds.Length; i++)
        {
            if (IsActive(packageIds[i]))
            {
                return true;
            }
        }
        return false;
    }
}

internal static class AllowToolHaulUrgentlyCompatibility
{
    public const string PackageId = "UnlimitedHugs.AllowTool";

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type workGiverType = AccessTools.TypeByName("AllowTool.WorkGiver_HaulUrgently");
        MethodInfo target = AccessTools.Method(workGiverType, "PotentialWorkThingsGlobal");
        MethodInfo prefix = AccessTools.Method(typeof(AllowToolHaulUrgentlyCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        PrepareReflection();
        return true;
    }

    private static readonly Type AllowToolControllerType = AccessTools.TypeByName("AllowTool.AllowToolController");
    private static readonly PropertyInfo InstanceProperty = AccessTools.Property(AllowToolControllerType, "Instance");
    private static readonly PropertyInfo HaulUrgentlyCacheProperty = AccessTools.Property(AllowToolControllerType, "HaulUrgentlyCache");
    private static MethodInfo getDesignatedAndHaulableThingsForMapMethod;

    public static void PrepareReflection()
    {
        object cache = GetHaulUrgentlyCache();
        if (cache != null)
        {
            getDesignatedAndHaulableThingsForMapMethod = AccessTools.Method(cache.GetType(), "GetDesignatedAndHaulableThingsForMap", new[] { typeof(Map), typeof(float) });
        }
    }

    public static bool Prefix(Pawn pawn, ref IEnumerable<Thing> __result)
    {
        __result = SafePotentialWorkThingsGlobal(pawn);
        return false;
    }

    private static IEnumerable<Thing> SafePotentialWorkThingsGlobal(Pawn pawn)
    {
        Map map = pawn?.Map;
        if (map == null)
        {
            yield break;
        }

        IReadOnlyList<Thing> things = GetAllowToolUrgentHaulables(map);
        if (things == null)
        {
            yield break;
        }

        for (int i = 0; i < things.Count; i++)
        {
            Thing thing = things[i];
            if (IsSafeLiveThingForPawn(pawn, thing) && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, false))
            {
                yield return thing;
            }
        }
    }

    private static IReadOnlyList<Thing> GetAllowToolUrgentHaulables(Map map)
    {
        try
        {
            object cache = GetHaulUrgentlyCache();
            if (cache == null)
            {
                return Array.Empty<Thing>();
            }

            getDesignatedAndHaulableThingsForMapMethod ??= AccessTools.Method(cache.GetType(), "GetDesignatedAndHaulableThingsForMap", new[] { typeof(Map), typeof(float) });
            if (getDesignatedAndHaulableThingsForMapMethod == null)
            {
                return Array.Empty<Thing>();
            }

            return getDesignatedAndHaulableThingsForMapMethod.Invoke(cache, new object[] { map, Time.unscaledTime }) as IReadOnlyList<Thing> ?? Array.Empty<Thing>();
        }
        catch
        {
            return Array.Empty<Thing>();
        }
    }

    private static object GetHaulUrgentlyCache()
    {
        object controller = InstanceProperty?.GetValue(null, null);
        return controller == null ? null : HaulUrgentlyCacheProperty?.GetValue(controller, null);
    }

    private static bool IsSafeLiveThingForPawn(Pawn pawn, Thing thing)
    {
        return pawn?.Map != null
            && thing != null
            && !thing.Destroyed
            && thing.Spawned
            && thing.MapHeld != null
            && thing.Map == pawn.Map
            && thing.def != null;
    }
}

internal static class ReservationEventCompatibility
{
    public const string BuildFromInventoryPackageId = "Memegoddess.BuildFromInventory";
    public const string ReplaceStuffPackageId = "Memegoddess.ReplaceStuff";

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.AnyActive(BuildFromInventoryPackageId, ReplaceStuffPackageId))
        {
            return false;
        }

        MethodInfo target = AccessTools.Method(typeof(PathFinderMapData), "Notify_Reservation");
        MethodInfo prefix = AccessTools.Method(typeof(ReservationEventCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    public static bool Prefix(ReservationManager.Reservation reservation)
    {
        if (reservation == null)
        {
            return false;
        }

        LocalTargetInfo target = reservation.Target;
        if (!target.HasThing)
        {
            return true;
        }

        Thing thing = target.Thing;
        if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map == null || thing.def == null)
        {
            return false;
        }

        return true;
    }
}

internal static class ReplaceStuffBridgeCompatibility
{
    public const string ReplaceStuffPackageId = "Memegoddess.ReplaceStuff";

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(ReplaceStuffPackageId))
        {
            return false;
        }

        Type placeBridgesType = AccessTools.TypeByName("Replace_Stuff.PlaceBridges.PlaceBridges");
        MethodInfo target = AccessTools.Method(placeBridgesType, "GetNeededBridge", new[] { typeof(BuildableDef), typeof(IntVec3), typeof(Map), typeof(ThingDef) });
        MethodInfo prefix = AccessTools.Method(typeof(ReplaceStuffBridgeCompatibility), nameof(Prefix));
        MethodInfo finalizer = AccessTools.Method(typeof(ReplaceStuffBridgeCompatibility), nameof(Finalizer));
        if (target == null || prefix == null || finalizer == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix), finalizer: new HarmonyMethod(finalizer));
        return true;
    }

    public static bool Prefix(BuildableDef def, IntVec3 pos, Map map, ThingDef stuff, ref TerrainDef __result)
    {
        if (def == null || map == null || map.terrainGrid == null || !pos.IsValid || !pos.InBounds(map))
        {
            __result = null;
            return false;
        }

        TerrainAffordanceDef needed;
        try
        {
            needed = def.GetTerrainAffordanceNeed(stuff);
        }
        catch
        {
            __result = null;
            return false;
        }

        if (needed == null)
        {
            __result = null;
            return false;
        }

        return true;
    }

    public static Exception Finalizer(Exception __exception, ref TerrainDef __result)
    {
        if (__exception == null)
        {
            return null;
        }

        __result = null;
        Log.Warning("[CommonModCompatibilityPatches] Suppressed Replace Stuff bridge helper exception and returned no bridge: " + __exception.GetType().Name + ": " + __exception.Message);
        return null;
    }
}
