using System;
using System.Collections;
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
        applied += ReplaceStuffOverMineableCompatibility.TryApply(Harmony) ? 1 : 0;
        applied += BuildFromInventoryReservationCountCompatibility.TryApply(Harmony) ? 1 : 0;
        applied += TinyTweaksAutoRebuildCompatibility.TryApply(Harmony) ? 1 : 0;
        applied += RimStoryADeadCompatibility.TryApply(Harmony) ? 1 : 0;

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

internal static class ReplaceStuffOverMineableCompatibility
{
    public const string ReplaceStuffPackageId = "Memegoddess.ReplaceStuff";

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(ReplaceStuffPackageId))
        {
            return false;
        }

        Type overMineableType = AccessTools.TypeByName("Replace_Stuff.OverMineable.InterceptBlueprintOverMinable");
        MethodInfo target = AccessTools.Method(overMineableType, "Prefix", new[] { typeof(BuildableDef), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(Faction) });
        MethodInfo prefix = AccessTools.Method(typeof(ReplaceStuffOverMineableCompatibility), nameof(Prefix));
        MethodInfo finalizer = AccessTools.Method(typeof(ReplaceStuffOverMineableCompatibility), nameof(Finalizer));
        if (target == null || prefix == null || finalizer == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix), finalizer: new HarmonyMethod(finalizer));
        return true;
    }

    public static bool Prefix(BuildableDef sourceDef, IntVec3 center, Map map, Rot4 rotation, Faction faction)
    {
        if (sourceDef == null || map == null || map.thingGrid == null || map.designationManager == null || !center.IsValid)
        {
            return false;
        }

        if (faction != Faction.OfPlayer)
        {
            return true;
        }

        try
        {
            CellRect occupied = GenAdj.OccupiedRect(center, rotation, sourceDef.Size).ClipInsideMap(map);
            if (occupied.Area <= 0)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        return true;
    }

    public static Exception Finalizer(Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        Log.Warning("[CommonModCompatibilityPatches] Suppressed Replace Stuff over-mineable blueprint helper exception: " + __exception.GetType().Name + ": " + __exception.Message);
        return null;
    }
}

internal static class BuildFromInventoryReservationCountCompatibility
{
    public const string BuildFromInventoryPackageId = "Memegoddess.BuildFromInventory";

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(BuildFromInventoryPackageId))
        {
            return false;
        }

        MethodInfo target = AccessTools.Method(typeof(ReservationManager), nameof(ReservationManager.Reserve), new[]
        {
            typeof(Pawn),
            typeof(Job),
            typeof(LocalTargetInfo),
            typeof(int),
            typeof(int),
            typeof(ReservationLayerDef),
            typeof(bool),
            typeof(bool),
            typeof(bool)
        });
        MethodInfo prefix = AccessTools.Method(typeof(BuildFromInventoryReservationCountCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    public static void Prefix(Job job, LocalTargetInfo target, int maxPawns, ref int stackCount)
    {
        if (stackCount <= 0 || maxPawns <= 1 || job?.def != JobDefOf.HaulToContainer || !target.HasThing)
        {
            return;
        }

        Thing thing = target.Thing;
        if (thing == null || thing.Destroyed)
        {
            return;
        }

        int availableStack = thing.stackCount;
        if (availableStack > 0 && stackCount > availableStack)
        {
            stackCount = availableStack;
        }
    }
}

internal static class TinyTweaksAutoRebuildCompatibility
{
    public const string TinyTweaksPackageId = "XeoNovaDan.TinyTweaks";
    private const string AutoRebuildSignal = "TT_ParentLaunched";
    private static FieldInfo previousMapField;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(TinyTweaksPackageId))
        {
            return false;
        }

        Type compType = AccessTools.TypeByName("TinyTweaks.CompLaunchableAutoRebuild");
        MethodInfo target = AccessTools.Method(compType, "ReceiveCompSignal", new[] { typeof(string) });
        MethodInfo prefix = AccessTools.Method(typeof(TinyTweaksAutoRebuildCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        previousMapField = AccessTools.Field(compType, "previousMap");
        if (previousMapField == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    public static bool Prefix(ThingComp __instance, string signal)
    {
        if (signal != AutoRebuildSignal)
        {
            return true;
        }

        Map previousMap = previousMapField?.GetValue(__instance) as Map;
        if (previousMap == null)
        {
            Log.Warning("[CommonModCompatibilityPatches] Skipped TinyTweaks auto-rebuild blueprint placement because previousMap was null.");
            return false;
        }

        Thing parent = __instance?.parent;
        if (parent == null || parent.def == null || !parent.Position.IsValid)
        {
            Log.Warning("[CommonModCompatibilityPatches] Skipped TinyTweaks auto-rebuild blueprint placement because launchable parent context was invalid.");
            return false;
        }

        return true;
    }
}

internal static class RimStoryADeadCompatibility
{
    public const string RimStoryPackageId = "Mlie.RimStory";
    private static FieldInfo deadPawnField;
    private static FieldInfo dateField;
    private static FieldInfo eventsToDeleteField;
    private static readonly HashSet<int> reportedInvalidEvents = new();

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(RimStoryPackageId))
        {
            return false;
        }

        Type aDeadType = AccessTools.TypeByName("RimStory.ADead");
        MethodInfo target = AccessTools.Method(aDeadType, "TryStartEvent", new[] { typeof(Map) });
        MethodInfo prefix = AccessTools.Method(typeof(RimStoryADeadCompatibility), nameof(Prefix));
        MethodInfo finalizer = AccessTools.Method(typeof(RimStoryADeadCompatibility), nameof(Finalizer));
        if (aDeadType == null || target == null || prefix == null || finalizer == null)
        {
            return false;
        }

        deadPawnField = AccessTools.Field(aDeadType, "deadPawn");
        dateField = AccessTools.Field(aDeadType, "date");
        Type resourcesType = AccessTools.TypeByName("RimStory.Resources");
        eventsToDeleteField = AccessTools.Field(resourcesType, "eventsToDelete");
        if (deadPawnField == null || dateField == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix), finalizer: new HarmonyMethod(finalizer));
        return true;
    }

    public static bool Prefix(object __instance, Map map, ref bool __result)
    {
        if (!HasValidContext(__instance, map))
        {
            __result = false;
            QueueEventForDeletion(__instance);
            ReportInvalidEventOnce(__instance, "invalid saved ADead context");
            return false;
        }

        return true;
    }

    public static Exception Finalizer(object __instance, Exception __exception, ref bool __result)
    {
        if (__exception == null)
        {
            return null;
        }

        __result = false;
        QueueEventForDeletion(__instance);
        ReportInvalidEventOnce(__instance, "exception in RimStory.ADead.TryStartEvent: " + __exception.GetType().Name + ": " + __exception.Message);
        return null;
    }

    private static bool HasValidContext(object instance, Map map)
    {
        if (instance == null || map == null || deadPawnField == null || dateField == null)
        {
            return false;
        }

        Pawn deadPawn = deadPawnField.GetValue(instance) as Pawn;
        object date = dateField.GetValue(instance);
        if (deadPawn == null || date == null || deadPawn.Destroyed || deadPawn.relations == null)
        {
            return false;
        }

        return deadPawn.Dead;
    }

    private static void QueueEventForDeletion(object instance)
    {
        if (instance == null || eventsToDeleteField == null)
        {
            return;
        }

        if (eventsToDeleteField.GetValue(null) is not IList eventsToDelete || eventsToDelete.Contains(instance))
        {
            return;
        }

        eventsToDelete.Add(instance);
    }

    private static void ReportInvalidEventOnce(object instance, string reason)
    {
        int key = instance == null ? 0 : instance.GetHashCode();
        if (!reportedInvalidEvents.Add(key))
        {
            return;
        }

        Log.Warning("[CommonModCompatibilityPatches] Suppressed RimStory ADead anniversary event because of " + reason + "; queued event for deletion.");
    }
}

