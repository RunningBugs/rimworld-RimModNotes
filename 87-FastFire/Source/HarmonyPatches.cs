using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace FastFire;

internal static class FireReservationTracker
{
    public static readonly HashSet<int> GlobalReservedFireIds = new HashSet<int>();
    public static readonly HashSet<int> GlobalPhysicalReservedFireIds = new HashSet<int>();
}

// =========================================================================
// 1. Fire Spawn / DeSpawn / Pathing Debounce
// =========================================================================

[HarmonyPatch(typeof(Fire), nameof(Fire.SpawnSetup))]
internal static class Patch_Fire_SpawnSetup
{
    public static void Postfix(Fire __instance, Map map, bool respawningAfterLoad)
    {
        if (__instance.Spawned && map != null)
        {
            FireMapComponent comp = map.GetComponent<FireMapComponent>();
            if (comp == null)
            {
                return;
            }
            if (__instance.parent == null)
            {
                comp.RegisterStaticThingFire(__instance, respawningAfterLoad);
            }
            else
            {
                comp.RegisterPawnAttachedFire(__instance);
            }
        }
    }
}

[HarmonyPatch(typeof(Fire), nameof(Fire.DeSpawn))]
internal static class Patch_Fire_DeSpawn
{
    public struct State
    {
        public Map map;
        public IntVec3 pos;
        public bool wasStaticSpawned;
        public bool wasAttachedSpawned;
    }

    public static void Prefix(Fire __instance, out State __state)
    {
        __state = new State
        {
            map = __instance.Map,
            pos = __instance.Position,
            wasStaticSpawned = __instance.Spawned && __instance.parent == null,
            wasAttachedSpawned = __instance.Spawned && __instance.parent != null
        };
    }

    public static void Postfix(Fire __instance, State __state)
    {
        if (__state.map != null)
        {
            FireMapComponent comp = __state.map.GetComponent<FireMapComponent>();
            if (comp == null)
            {
                return;
            }
            if (__state.wasStaticSpawned)
            {
                comp.DeregisterStaticThingFire(__instance, __state.pos);
            }
            else if (__state.wasAttachedSpawned)
            {
                comp.DeregisterPawnAttachedFire(__instance);
            }
        }
    }
}

[HarmonyPatch(typeof(Fire), "RecalcPathsOnAndAroundMe")]
internal static class Patch_Fire_RecalcPathsOnAndAroundMe
{
    public static bool Prefix(Fire __instance)
    {
        if (__instance.parent == null && FastFireMod.Settings.UsePathingDebounce)
        {
            return false;
        }
        return true;
    }
}

// =========================================================================
// 2. TickManager Bypass (Compatible & ExtremeGrid)
// =========================================================================

[HarmonyPatch(typeof(TickManager), nameof(TickManager.RegisterAllTickabilityFor))]
internal static class Patch_TickManager_Register
{
    public static bool Prefix(Thing t)
    {
        if (t is Fire { parent: null } && FastFireMod.Settings.IsActive)
        {
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(TickManager), nameof(TickManager.DeRegisterAllTickabilityFor))]
internal static class Patch_TickManager_DeRegister
{
    public static bool Prefix(Thing t)
    {
        if (t is Fire { parent: null } && FastFireMod.Settings.IsActive)
        {
            return false;
        }
        return true;
    }
}

// =========================================================================
// 3. DynamicDrawManager Bypass & Batched Fire Draw
// =========================================================================

[HarmonyPatch(typeof(DynamicDrawManager), nameof(DynamicDrawManager.RegisterDrawable))]
internal static class Patch_DynamicDrawManager_Register
{
    public static bool Prefix(Thing t)
    {
        if (t is Fire { parent: null } && FastFireMod.Settings.UseBatchedRendering)
        {
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(DynamicDrawManager), nameof(DynamicDrawManager.DeRegisterDrawable))]
internal static class Patch_DynamicDrawManager_DeRegister
{
    public static bool Prefix(Thing t)
    {
        if (t is Fire { parent: null } && FastFireMod.Settings.UseBatchedRendering)
        {
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(DynamicDrawManager), nameof(DynamicDrawManager.DrawDynamicThings))]
internal static class Patch_DynamicDrawManager_DrawDynamicThings
{
    public static void Postfix(DynamicDrawManager __instance)
    {
        if (!DebugViewSettings.drawThingsDynamic || __instance.map == null || __instance.map.Disposed)
        {
            return;
        }
        __instance.map.GetComponent<FireMapComponent>()?.DrawBatchedFires();
    }
}

// =========================================================================
// 4. CompGlower / GlowGrid Cluster Lighting Bypass
// =========================================================================

[HarmonyPatch(typeof(CompGlower), nameof(CompGlower.UpdateLit))]
internal static class Patch_CompGlower_UpdateLit
{
    public static bool Prefix(CompGlower __instance)
    {
        if (__instance.parent is Fire { parent: null } && FastFireMod.Settings.UseClusterLighting)
        {
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(GlowGrid), nameof(GlowGrid.RegisterGlower))]
internal static class Patch_GlowGrid_RegisterGlower
{
    public static bool Prefix(CompGlower newGlow)
    {
        if (newGlow?.parent is Fire { parent: null } && FastFireMod.Settings.UseClusterLighting)
        {
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(GlowGrid), nameof(GlowGrid.DeRegisterGlower))]
internal static class Patch_GlowGrid_DeRegisterGlower
{
    public static bool Prefix(GlowGrid __instance, CompGlower oldGlow)
    {
        if (oldGlow?.parent is Fire { parent: null } && !__instance.litGlowers.Contains(oldGlow))
        {
            return false;
        }
        return true;
    }
}

// =========================================================================
// 5. Batch Despawn (O(N^2) -> O(N)) & Unused Destroy/Reservation Overhead
// =========================================================================

[HarmonyPatch(typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.Remove))]
internal static class Patch_ThingOwner_Remove
{
    public static bool Prefix(ThingOwner<Thing> __instance, Thing item, ref bool __result)
    {
        if (__instance.Owner is Map map && item is Fire { parent: null })
        {
            FireMapComponent comp = map.GetComponent<FireMapComponent>();
            if (comp != null && comp.TryDeferBatchDespawnRemoval(item))
            {
                if (item.holdingOwner == __instance)
                {
                    item.holdingOwner = null;
                }
                __result = true;
                return false;
            }
        }
        return true;
    }
}

[HarmonyPatch(typeof(ListerThings), nameof(ListerThings.Remove))]
internal static class Patch_ListerThings_Remove
{
    public static bool Prefix(ListerThings __instance, Thing t)
    {
        if (__instance.use == ListerThingsUse.Global && t is Fire { parent: null })
        {
            Map map = t.Map;
            if (map != null)
            {
                FireMapComponent comp = map.GetComponent<FireMapComponent>();
                if (comp != null && comp.TryDeferBatchDespawnRemoval(t))
                {
                    return false;
                }
            }
        }
        return true;
    }
}

[HarmonyPatch(typeof(RegionListersUpdater), nameof(RegionListersUpdater.DeregisterInRegions))]
internal static class Patch_RegionListersUpdater_Deregister
{
    [ThreadStatic]
    private static List<Region> tmpBatchRegions;

    public static bool Prefix(Thing thing, Map map)
    {
        if (thing is Fire { parent: null } && map != null)
        {
            FireMapComponent comp = map.GetComponent<FireMapComponent>();
            if (comp != null && comp.IsInBatchDespawn)
            {
                List<Region> regs = tmpBatchRegions ??= new List<Region>(16);
                RegionListersUpdater.GetTouchableRegions(thing, map, regs, allowAdjacentEvenIfCantTouch: true);
                for (int i = 0; i < regs.Count; i++)
                {
                    comp.RecordDirtyRegionForBatchDespawn(regs[i]);
                }
                regs.Clear();
                comp.TryDeferBatchDespawnRemoval(thing);
                return false;
            }
        }
        return true;
    }
}

[HarmonyPatch(typeof(Thing), "RemoveAllReservationsAndDesignationsOnThis")]
internal static class Patch_Thing_RemoveAllReservationsAndDesignationsOnThis
{
    public static bool Prefix(Thing __instance)
    {
        if (__instance is Fire && FastFireMod.Settings.IsActive
            && !FireReservationTracker.GlobalReservedFireIds.Contains(__instance.thingIDNumber)
            && !FireReservationTracker.GlobalPhysicalReservedFireIds.Contains(__instance.thingIDNumber))
        {
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(PhysicalInteractionReservationManager), nameof(PhysicalInteractionReservationManager.ReleaseAllForTarget))]
internal static class Patch_PhysicalReservation_ReleaseAllForTarget
{
    public static bool Prefix(PhysicalInteractionReservationManager __instance, LocalTargetInfo target)
    {
        if (__instance.reservations.Count == 0)
        {
            return false;
        }
        if (target.Thing is Fire f && FastFireMod.Settings.IsActive && !FireReservationTracker.GlobalPhysicalReservedFireIds.Contains(f.thingIDNumber))
        {
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(PhysicalInteractionReservationManager), nameof(PhysicalInteractionReservationManager.Reserve))]
internal static class Patch_PhysicalReservation_Reserve
{
    public static void Postfix(LocalTargetInfo target)
    {
        if (target.Thing is Fire f)
        {
            FireReservationTracker.GlobalPhysicalReservedFireIds.Add(f.thingIDNumber);
        }
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.Reserve))]
internal static class Patch_ReservationManager_Reserve
{
    public static void Postfix(bool __result, LocalTargetInfo target)
    {
        if (__result && target.Thing is Fire f)
        {
            FireReservationTracker.GlobalReservedFireIds.Add(f.thingIDNumber);
            f.MapHeld?.GetComponent<FireMapComponent>()?.reservedFireThingIds.Add(f.thingIDNumber);
        }
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.ReleaseAllForTarget))]
internal static class Patch_ReservationManager_ReleaseAllForTarget
{
    public static void Postfix(ReservationManager __instance, Thing t)
    {
        if (t is Fire f)
        {
            FireReservationTracker.GlobalReservedFireIds.Remove(f.thingIDNumber);
            f.MapHeld?.GetComponent<FireMapComponent>()?.reservedFireThingIds.Remove(f.thingIDNumber);
        }
    }
}

[HarmonyPatch(typeof(GenLeaving), nameof(GenLeaving.DoLeavingsFor),
    new[] { typeof(Thing), typeof(Map), typeof(DestroyMode), typeof(CellRect), typeof(Predicate<IntVec3>), typeof(List<Thing>) })]
internal static class Patch_GenLeaving_DoLeavingsFor
{
    public static bool Prefix(Thing diedThing)
    {
        if (diedThing is Fire && FastFireMod.Settings.IsActive)
        {
            return false;
        }
        return true;
    }
}

// =========================================================================
// 6. FireUtility Core APIs (Grid + Cached Flammability)
// =========================================================================

[HarmonyPatch(typeof(FireUtility), nameof(FireUtility.ContainsStaticFire), new[] { typeof(IntVec3), typeof(Map) })]
internal static class Patch_FireUtility_ContainsStaticFire
{
    public static bool Prefix(IntVec3 c, Map map, ref bool __result)
    {
        if (!FastFireMod.Settings.UseGridEngine)
        {
            return true;
        }
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        if (comp == null)
        {
            return true;
        }
        __result = comp.HasAnyStaticFireAt(c);
        return false;
    }
}

[HarmonyPatch(typeof(FireUtility), nameof(FireUtility.ChanceToStartFireIn))]
internal static class Patch_FireUtility_ChanceToStartFireIn
{
    public static bool Prefix(IntVec3 c, Map map, SimpleCurve flammabilityChanceCurve, ref float __result)
    {
        if (!FastFireMod.Settings.IsActive)
        {
            return true;
        }
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        __result = FireMath.ChanceToStartFireInFast(c, map, comp, flammabilityChanceCurve);
        return false;
    }
}

[HarmonyPatch(typeof(FireUtility), nameof(FireUtility.TryStartFireIn))]
internal static class Patch_FireUtility_TryStartFireIn
{
    public static bool Prefix(IntVec3 c, Map map, float fireSize, Thing instigator, SimpleCurve flammabilityChanceCurve, ref bool __result)
    {
        if (!FastFireMod.Settings.UseGridEngine)
        {
            return true;
        }
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        if (comp == null)
        {
            return true;
        }
        if (FireMath.ChanceToStartFireInFast(c, map, comp, flammabilityChanceCurve) <= 0f)
        {
            __result = false;
            return false;
        }
        __result = comp.TryAddGridFire(c, fireSize, instigator);
        return false;
    }
}

[HarmonyPatch(typeof(FireUtility), nameof(FireUtility.NumFiresAt))]
internal static class Patch_FireUtility_NumFiresAt
{
    public static void Postfix(IntVec3 c, Map map, ref int __result)
    {
        if (FastFireMod.Settings.UseGridEngine)
        {
            FireMapComponent comp = map.GetComponent<FireMapComponent>();
            if (comp != null && comp.HasGridFireAt(c))
            {
                __result++;
            }
        }
    }
}

[HarmonyPatch(typeof(FireUtility), nameof(FireUtility.GetFiresNearCell))]
internal static class Patch_FireUtility_GetFiresNearCell
{
    public static bool Prefix(IntVec3 cell, Map map, ref List<Fire> __result)
    {
        if (!FastFireMod.Settings.UseGridEngine)
        {
            return true;
        }
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        if (comp == null || comp.GridFireCount == 0)
        {
            return true;
        }
        __result = comp.CollectNearbyFiresForFloatMenu(cell, 16);
        return false;
    }
}

// =========================================================================
// 7. PathGrid, PersistentDangerSource & PathFinder Flush
// =========================================================================

[HarmonyPatch(typeof(PathGrid), nameof(PathGrid.CalculatedCostAt))]
internal static class Patch_PathGrid_CalculatedCostAt
{
    public static void Postfix(PathGrid __instance, IntVec3 c, bool perceivedStatic, ref int __result)
    {
        if (!perceivedStatic || __result >= PathGrid.ImpassableCost || !FastFireMod.Settings.UseGridEngine)
        {
            return;
        }
        FireMapComponent comp = __instance.map.GetComponent<FireMapComponent>();
        if (comp == null || comp.GridFireCount == 0)
        {
            return;
        }
        int idx = __instance.map.cellIndices.CellToIndex(c);
        if (comp.staticFireAtCell[idx] == 1)
        {
            __result += 1000;
        }
        __result += comp.gridNeighborFireCount[idx] * 150;
    }
}

[HarmonyPatch(typeof(PersistentDangerSource), nameof(PersistentDangerSource.ComputeAll))]
internal static class Patch_PersistentDangerSource_ComputeAll
{
    public static void Postfix(PersistentDangerSource __instance)
    {
        if (!FastFireMod.Settings.UseGridEngine)
        {
            return;
        }
        FireMapComponent comp = __instance.map.GetComponent<FireMapComponent>();
        if (comp == null || comp.GridFireCount == 0)
        {
            return;
        }
        byte[] grid = comp.staticFireAtCell;
        for (int i = 0; i < grid.Length; i++)
        {
            if (grid[i] == 1)
            {
                __instance.data.Set(i, true);
            }
        }
    }
}

[HarmonyPatch(typeof(PersistentDangerSource), nameof(PersistentDangerSource.UpdateIncrementally))]
internal static class Patch_PersistentDangerSource_UpdateIncrementally
{
    public static void Postfix(PersistentDangerSource __instance, List<IntVec3> cellDeltas)
    {
        if (!FastFireMod.Settings.UseGridEngine)
        {
            return;
        }
        FireMapComponent comp = __instance.map.GetComponent<FireMapComponent>();
        if (comp == null || comp.GridFireCount == 0)
        {
            return;
        }
        byte[] grid = comp.staticFireAtCell;
        CellIndices indices = __instance.map.cellIndices;
        for (int i = 0; i < cellDeltas.Count; i++)
        {
            int pos = indices.CellToIndex(cellDeltas[i]);
            if (grid[pos] == 1)
            {
                __instance.data.Set(pos, true);
            }
        }
    }
}

[HarmonyPatch(typeof(PathFinderMapData), "Notify_ThingSpawnedDespawned")]
internal static class Patch_PathFinderMapData_NotifyThing
{
    public static bool Prefix(Thing thing)
    {
        if (thing is Fire { parent: null } && FastFireMod.Settings.UsePathingDebounce)
        {
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(PathFinder), nameof(PathFinder.PathFinderTick))]
internal static class Patch_PathFinder_Tick
{
    public static void Prefix(PathFinder __instance)
    {
        if (FastFireMod.Settings.UsePathingDebounce && __instance.workQueue.Count > 0)
        {
            __instance.map.GetComponent<FireMapComponent>()?.FlushPendingPathUpdates(force: false);
        }
    }
}

[HarmonyPatch(typeof(PathFinder), nameof(PathFinder.FindPathNow),
    new[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathFinderCostTuning?), typeof(PathEndMode), typeof(PathRequest.IPathGridCustomizer) })]
internal static class Patch_PathFinder_FindPathNow
{
    public static void Prefix(PathFinder __instance)
    {
        if (FastFireMod.Settings.UsePathingDebounce)
        {
            __instance.map.GetComponent<FireMapComponent>()?.FlushPendingPathUpdates(force: true);
        }
    }
}

// =========================================================================
// 8. Watchers, Alerts, FleeUtility, DropCellFinder & Area_Home
// =========================================================================

[HarmonyPatch(typeof(FireWatcher), "UpdateObservations")]
internal static class Patch_FireWatcher_UpdateObservations
{
    public static bool Prefix(FireWatcher __instance)
    {
        if (!FastFireMod.Settings.UseAiSpatialAccel && !FastFireMod.Settings.UseGridEngine)
        {
            return true;
        }
        FireMapComponent comp = __instance.map.GetComponent<FireMapComponent>();
        if (comp == null)
        {
            return true;
        }
        float danger = (float)(comp.totalStaticFireCount * 0.5 + comp.totalStaticFireSizeSum);

        // Add any pawn-attached fires if present
        List<Thing> realFires = __instance.map.listerThings.ThingsOfDef(ThingDefOf.Fire);
        if (comp.GridFireCount == 0 && realFires.Count == comp.totalStaticFireCount)
        {
            __instance.fireDanger = danger;
            return false;
        }
        for (int i = 0; i < realFires.Count; i++)
        {
            if (realFires[i] is Fire { parent: not null } f)
            {
                danger += 0.5f + f.fireSize;
            }
        }
        __instance.fireDanger = danger;
        return false;
    }
}

[HarmonyPatch(typeof(Alert_FireInHomeArea), nameof(Alert_FireInHomeArea.GetReport))]
internal static class Patch_Alert_FireInHomeArea_GetReport
{
    public static bool Prefix(ref AlertReport __result)
    {
        if (!FastFireMod.Settings.UseAiSpatialAccel && !FastFireMod.Settings.UseGridEngine)
        {
            return true;
        }
        List<Map> maps = Find.Maps;
        for (int i = 0; i < maps.Count; i++)
        {
            Map map = maps[i];
            FireMapComponent comp = map.GetComponent<FireMapComponent>();
            if (comp != null)
            {
                if (comp.TryGetFirstHomeAreaUnfoggedFireCell(out IntVec3 cell))
                {
                    __result = AlertReport.CulpritIs(new RimWorld.Planet.GlobalTargetInfo(cell, map));
                    return false;
                }
                // Still check pawn-attached fires in home area
                List<Thing> realFires = map.listerThings.ThingsOfDef(ThingDefOf.Fire);
                if (realFires.Count > 0 && (comp.GridFireCount > 0 || realFires.Count != comp.totalStaticFireCount))
                {
                    for (int j = 0; j < realFires.Count; j++)
                    {
                        if (realFires[j] is Fire { parent: not null } f && map.areaManager.Home[f.Position] && !f.Position.Fogged(map))
                        {
                            __result = f;
                            return false;
                        }
                    }
                }
            }
        }
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Area_Home), "Set")]
internal static class Patch_Area_Home_Set
{
    public static void Prefix(Area_Home __instance, IntVec3 c, out bool __state)
    {
        __state = __instance[c];
    }

    public static void Postfix(Area_Home __instance, IntVec3 c, bool val, bool __state)
    {
        if (__state != val && __instance.Map != null)
        {
            __instance.Map.GetComponent<FireMapComponent>()?.OnHomeAreaCellChanged(c, val);
        }
    }
}

[HarmonyPatch(typeof(FleeUtility), nameof(FleeUtility.FleeLargeFireJob))]
internal static class Patch_FleeUtility_FleeLargeFireJob
{
    public static bool Prefix(Pawn pawn, int minFiresNearbyToFlee, int distToFireToFlee, int fleeDistance, ref Job __result)
    {
        if (!FastFireMod.Settings.UseAiSpatialAccel && !FastFireMod.Settings.UseGridEngine)
        {
            return true;
        }
        FireMapComponent comp = pawn.Map.GetComponent<FireMapComponent>();
        if (comp == null)
        {
            return true;
        }

        if (comp.TotalStaticFireCount + pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count < minFiresNearbyToFlee)
        {
            __result = null;
            return false;
        }

        if (comp.TryCheckNearbyFiresForFlee(pawn.Position, minFiresNearbyToFlee, distToFireToFlee, 20, out Fire closestFire))
        {
            __result = FleeUtility.FleeJob(pawn, closestFire, fleeDistance);
            return false;
        }

        // If Grid engine isn't active and TryCheckNearbyFiresForFlee saw nothing (note: pawn fires don't have parent==null),
        // only fall through if there are enough pawn-attached fires near pawn
        if (pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count > comp.TotalStaticFireCount)
        {
            return true;
        }
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(DropCellFinder), "IsSafeDropSpot")]
internal static class Patch_DropCellFinder_IsSafeDropSpot
{
    public static void Postfix(IntVec3 cell, Map map, int distToFires, ref bool __result)
    {
        if (__result && FastFireMod.Settings.UseGridEngine)
        {
            FireMapComponent comp = map.GetComponent<FireMapComponent>();
            if (comp != null && comp.GridFireCount > 0 && comp.HasAnyFireNearDropCell(cell, distToFires, 5))
            {
                __result = false;
            }
        }
    }
}

// =========================================================================
// 9. Firefighting Jobs, Native Verbs, Weapons, Abilities & Interactions
// =========================================================================

[HarmonyPatch(typeof(WorkGiver), nameof(WorkGiver.ShouldSkip))]
internal static class Patch_WorkGiver_FightFires_ShouldSkip
{
    public static bool Prefix(WorkGiver __instance, Pawn pawn, bool forced, ref bool __result)
    {
        if (!(__instance is WorkGiver_FightFires) || !FastFireMod.Settings.UseAiSpatialAccel)
        {
            return true;
        }
        FireMapComponent comp = pawn.Map?.GetComponent<FireMapComponent>();
        if (comp == null)
        {
            return true;
        }
        if (comp.TotalStaticFireCount == 0 && comp.PawnAttachedFireCount == 0)
        {
            __result = true;
            return false;
        }
        if (!forced && comp.homeAreaUnfoggedFireCount == 0 && comp.PawnAttachedFireCount == 0)
        {
            __result = true;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(WorkGiver), nameof(WorkGiver.NonScanJob))]
internal static class Patch_WorkGiver_FightFires_NonScanJob
{
    public static bool Prefix(WorkGiver __instance, Pawn pawn, ref Job __result)
    {
        if (!(__instance is WorkGiver_FightFires))
        {
            return true;
        }
        if (!FastFireMod.Settings.UseGridEngine && !FastFireMod.Settings.UseAiSpatialAccel)
        {
            return true;
        }
        FireMapComponent comp = pawn.Map?.GetComponent<FireMapComponent>();
        if (comp == null)
        {
            return true;
        }

        if (comp.TryFindBestHomeAreaFireTarget(pawn, out Thing realFire, out IntVec3 gridCell))
        {
            Thing target = realFire ?? comp.MaterializeProxyFireAt(gridCell);
            if (target != null)
            {
                Job job = JobMaker.MakeJob(JobDefOf.BeatFire, target);
                job.workGiverDef = __instance.def;
                __result = job;
                return false;
            }
        }

        // We already checked all fires (real + grid); return null and temporarily skip scanner scanThings?
        // Wait: returning false with __result = null makes JobGiver_Work proceed to scanner!
        // To prevent JobGiver_Work from re-scanning 10,000 fires in ClosestThingReachable when we already checked all fires,
        // we mark a quick thread-static skip token for the scanner or let Patch_GenClosest handle it cleanly.
        Patch_JobGiver_Work_FightFiresNoTarget.SkipNextFightFiresScan = true;
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
internal static class Patch_JobGiver_Work_FightFiresNoTarget
{
    [ThreadStatic]
    public static bool SkipNextFightFiresScan;

    public static void Finalizer()
    {
        SkipNextFightFiresScan = false;
    }
}

[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable))]
internal static class Patch_GenClosest_ClosestThingReachable
{
    public static bool Prefix(
        IntVec3 root,
        Map map,
        ThingRequest thingReq,
        PathEndMode peMode,
        TraverseParms traverseParams,
        float maxDistance,
        Predicate<Thing> validator,
        IEnumerable<Thing> customGlobalSearchSet,
        ref Thing __result)
    {
        if (thingReq.singleDef != ThingDefOf.Fire && thingReq.group != ThingRequestGroup.Fire)
        {
            return true;
        }
        if (Patch_JobGiver_Work_FightFiresNoTarget.SkipNextFightFiresScan)
        {
            Patch_JobGiver_Work_FightFiresNoTarget.SkipNextFightFiresScan = false;
            __result = null;
            return false;
        }
        if (!FastFireMod.Settings.UseGridEngine || customGlobalSearchSet != null)
        {
            return true;
        }
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        if (comp == null || comp.GridFireCount == 0)
        {
            return true;
        }
        __result = comp.FindClosestReachableFireForGenClosest(root, peMode, traverseParams, maxDistance, validator);
        return false;
    }
}

[HarmonyPatch(typeof(JobDriver_BeatFire), "StartBeatingFireIfAnyAt")]
internal static class Patch_JobDriver_BeatFire_StartBeating
{
    public static void Postfix(JobDriver_BeatFire __instance, IntVec3 cell, Toil nextToil, ref bool __result)
    {
        if (__result || !FastFireMod.Settings.UseGridEngine)
        {
            return;
        }
        FireMapComponent comp = __instance.pawn?.Map?.GetComponent<FireMapComponent>();
        if (comp != null && comp.HasGridFireAt(cell))
        {
            Fire proxy = comp.MaterializeProxyFireAt(cell);
            if (proxy != null)
            {
                __instance.job.targetA = proxy;
                __instance.pawn.pather.StopDead();
                __instance.JumpToToil(nextToil);
                __result = true;
            }
        }
    }
}

[HarmonyPatch(typeof(JobDriver_ExtinguishFiresNearby), "StartBeatingFireIfAnyAt")]
internal static class Patch_JobDriver_ExtinguishFiresNearby_StartBeating
{
    public static void Postfix(JobDriver_ExtinguishFiresNearby __instance, IntVec3 cell, Toil nextToil, ref bool __result)
    {
        if (__result || !FastFireMod.Settings.UseGridEngine)
        {
            return;
        }
        FireMapComponent comp = __instance.pawn?.Map?.GetComponent<FireMapComponent>();
        if (comp != null && comp.HasGridFireAt(cell))
        {
            Fire proxy = comp.MaterializeProxyFireAt(cell);
            if (proxy != null)
            {
                __instance.job.targetA = proxy;
                __instance.pawn.pather.StopDead();
                __instance.JumpToToil(nextToil);
                __result = true;
            }
        }
    }
}

[HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
internal static class Patch_JobDriver_Wait_CheckForAutoAttack
{
    public static void Postfix(JobDriver_Wait __instance)
    {
        if (!FastFireMod.Settings.UseGridEngine)
        {
            return;
        }
        Pawn pawn = __instance.pawn;
        if (pawn == null || pawn.Destroyed || !pawn.Spawned || !pawn.kindDef.canMeleeAttack || pawn.Downed || pawn.stances.FullBodyBusy || pawn.IsCarryingPawn())
        {
            return;
        }
        if (!(pawn.RaceProps.ToolUser && pawn.Faction == Faction.OfPlayer && !pawn.WorkTagIsDisabled(WorkTags.Firefighting)))
        {
            return;
        }
        if (pawn.InMentalState && !pawn.MentalState.def.allowBeatfire)
        {
            return;
        }
        FireMapComponent comp = pawn.Map.GetComponent<FireMapComponent>();
        if (comp == null || comp.GridFireCount == 0)
        {
            return;
        }

        IntVec3 bestCell = IntVec3.Invalid;
        float bestSize = float.MaxValue;
        for (int i = 0; i < 9; i++)
        {
            IntVec3 c = pawn.Position + GenAdj.AdjacentCellsAndInside[i];
            if (!c.InBounds(pawn.Map) || !comp.HasGridFireAt(c))
            {
                continue;
            }
            float size = comp.GetStaticFireSizeAt(pawn.Map.cellIndices.CellToIndex(c));
            if (!bestCell.IsValid || size < bestSize || i == 8)
            {
                bestCell = c;
                bestSize = size;
            }
        }

        if (bestCell.IsValid)
        {
            Fire proxy = comp.MaterializeProxyFireAt(bestCell);
            if (proxy != null)
            {
                pawn.natives.TryBeatFire(proxy);
            }
        }
    }
}

[HarmonyPatch(typeof(CompAbilityEffect_Waterskip), nameof(CompAbilityEffect_Waterskip.Apply))]
internal static class Patch_Waterskip_Apply
{
    private static readonly MethodInfo AffectedCellsMethod = AccessTools.Method(typeof(CompAbilityEffect_Waterskip), "AffectedCells");

    public static void Postfix(CompAbilityEffect_Waterskip __instance, LocalTargetInfo target)
    {
        if (!FastFireMod.Settings.UseGridEngine)
        {
            return;
        }
        Map map = __instance.parent.pawn.Map;
        FireMapComponent comp = map?.GetComponent<FireMapComponent>();
        if (comp == null || comp.GridFireCount == 0 || AffectedCellsMethod == null)
        {
            return;
        }
        var cells = (IEnumerable<IntVec3>)AffectedCellsMethod.Invoke(__instance, new object[] { target, map });
        foreach (IntVec3 c in cells)
        {
            if (c.InBounds(map))
            {
                comp.ExtinguishGridFireAt(c);
            }
        }
    }
}

[HarmonyPatch(typeof(WaterSplash), "Impact")]
internal static class Patch_WaterSplash_Impact
{
    public static void Prefix(WaterSplash __instance, out Map __state)
    {
        __state = __instance.Map;
    }

    public static void Postfix(WaterSplash __instance, Map __state)
    {
        if (!FastFireMod.Settings.UseGridEngine || __state == null)
        {
            return;
        }
        FireMapComponent comp = __state.GetComponent<FireMapComponent>();
        if (comp != null && comp.GridFireCount > 0)
        {
            comp.ExtinguishGridFireAt(__instance.Position);
        }
    }
}

[HarmonyPatch(typeof(Projectile_Liquid), "DoImpact")]
internal static class Patch_Projectile_Liquid_DoImpact
{
    public static void Postfix(Projectile_Liquid __instance, IntVec3 cell)
    {
        if (!FastFireMod.Settings.UseGridEngine || __instance.DamageDef != DamageDefOf.Extinguish)
        {
            return;
        }
        Map map = __instance.Map;
        if (map == null || !cell.InBounds(map))
        {
            return;
        }
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        if (comp != null && comp.GridFireCount > 0 && comp.HasGridFireAt(cell))
        {
            comp.ApplyExtinguishDamageGridAt(cell, __instance.def.projectile.GetDamageAmount(null));
        }
    }
}

[HarmonyPatch(typeof(Building_TurretFoam), nameof(Building_TurretFoam.TryFindNewTarget))]
internal static class Patch_Building_TurretFoam_TryFindNewTarget
{
    public static void Postfix(Building_TurretFoam __instance, ref LocalTargetInfo __result)
    {
        if (__result.IsValid || !FastFireMod.Settings.UseGridEngine)
        {
            return;
        }
        FireMapComponent comp = __instance.Map?.GetComponent<FireMapComponent>();
        if (comp == null || comp.GridFireCount == 0)
        {
            return;
        }
        int num = GenRadial.NumCellsInRadius(__instance.AttackVerb.EffectiveRange);
        for (int i = 0; i < num; i++)
        {
            IntVec3 c = __instance.Position + GenRadial.RadialPattern[i];
            if (c.InBounds(__instance.Map) && comp.HasGridFireAt(c) && GenSight.LineOfSight(__instance.Position, c, __instance.Map, skipFirstCell: true))
            {
                __result = c;
                return;
            }
        }
    }
}

[HarmonyPatch(typeof(CompFirefoamPack), "ChanceToUse")]
internal static class Patch_CompFirefoamPack_ChanceToUse
{
    public static void Postfix(Pawn wearer, ref float __result)
    {
        if (__result >= 1f || !FastFireMod.Settings.UseGridEngine || wearer?.Map == null)
        {
            return;
        }
        FireMapComponent comp = wearer.Map.GetComponent<FireMapComponent>();
        if (comp == null || comp.GridFireCount == 0)
        {
            return;
        }
        int num = GenRadial.NumCellsInRadius(1.9f);
        for (int i = 0; i < num; i++)
        {
            IntVec3 c = wearer.Position + GenRadial.RadialPattern[i];
            if (c.InBounds(wearer.Map) && comp.HasGridFireAt(c))
            {
                __result = i == 0 ? 1f : Math.Max(__result, 0.2f);
                if (__result >= 1f)
                {
                    return;
                }
            }
        }
    }
}

[HarmonyPatch(typeof(DamageWorker), nameof(DamageWorker.ExplosionAffectCell))]
internal static class Patch_DamageWorker_ExplosionAffectCell
{
    public static void Postfix(DamageWorker __instance, Explosion explosion, IntVec3 c)
    {
        if (!FastFireMod.Settings.UseGridEngine || explosion?.Map == null || !c.InBounds(explosion.Map))
        {
            return;
        }
        FireMapComponent comp = explosion.Map.GetComponent<FireMapComponent>();
        if (comp == null || comp.GridFireCount == 0 || !comp.HasGridFireAt(c))
        {
            return;
        }
        if (__instance.def == DamageDefOf.Extinguish)
        {
            comp.ApplyExtinguishDamageGridAt(c, explosion.GetDamageAmountAt(c));
        }
        else if (__instance.def == DamageDefOf.Bomb)
        {
            comp.ExtinguishGridFireAt(c);
        }
    }
}

[HarmonyPatch(typeof(CastPositionFinder), "CastPositionPreference")]
internal static class Patch_CastPositionFinder_CastPositionPreference
{
    public static void Postfix(IntVec3 c, ref float __result)
    {
        if (__result < 0f || !FastFireMod.Settings.UseGridEngine)
        {
            return;
        }
        Map map = CastPositionFinder.req.caster?.Map;
        if (map != null)
        {
            FireMapComponent comp = map.GetComponent<FireMapComponent>();
            if (comp != null && comp.HasGridFireAt(c))
            {
                __result = -1f;
            }
        }
    }
}

// =========================================================================
// 10. Particles Throttle, Mouseover Readout, and Debug DestroyAllFire
// =========================================================================

[HarmonyPatch(typeof(FleckMaker), nameof(FleckMaker.ThrowFireGlow))]
internal static class Patch_FleckMaker_ThrowFireGlow
{
    public static bool Prefix(Map map)
    {
        if (!FastFireMod.Settings.UseBatchedRendering || map == null)
        {
            return true;
        }
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        if (comp != null && comp.TotalStaticFireCount >= 40)
        {
            return Rand.Value < 0.1f;
        }
        return true;
    }
}

[HarmonyPatch(typeof(MouseoverReadout), nameof(MouseoverReadout.MouseoverReadoutOnGUI))]
internal static class Patch_MouseoverReadout_OnGUI
{
    public static void Postfix()
    {
        if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint || Find.MainTabsRoot.OpenTab != null || !FastFireMod.Settings.UseGridEngine)
        {
            return;
        }
        Map map = Find.CurrentMap;
        if (map == null)
        {
            return;
        }
        IntVec3 c = UI.MouseCell();
        if (!c.InBounds(map) || c.Fogged(map))
        {
            return;
        }
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        if (comp != null && comp.HasGridFireAt(c))
        {
            float size = comp.GetStaticFireSizeAt(map.cellIndices.CellToIndex(c));
            Text.Font = GameFont.Small;
            UnityEngine.GUI.color = new UnityEngine.Color(1f, 0.75f, 0.35f, 0.9f);
            string label = $"{ThingDefOf.Fire.LabelCap} ({(size * 100f).ToString("F0")}%)";
            Widgets.Label(new UnityEngine.Rect(15f, (float)UI.screenHeight - 46f, 300f, 22f), label);
            UnityEngine.GUI.color = UnityEngine.Color.white;
        }
    }
}

[HarmonyPatch(typeof(DebugToolsGeneral), "DestroyAllFire")]
internal static class Patch_DebugToolsGeneral_DestroyAllFire
{
    public static bool Prefix()
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            return true;
        }
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        if (comp == null || !FastFireMod.Settings.IsActive)
        {
            return true;
        }
        comp.ClearAllFiresForDebug();
        return false;
    }
}

[HarmonyPatch(typeof(DebugThingPlaceHelper), nameof(DebugThingPlaceHelper.DebugSpawn))]
internal static class Patch_DebugThingPlaceHelper_DebugSpawn
{
    public static bool Prefix(ThingDef def, IntVec3 c)
    {
        if (def != ThingDefOf.Fire || !FastFireMod.Settings.UseGridEngine)
        {
            return true;
        }
        Map map = Find.CurrentMap;
        if (map == null || !c.InBounds(map))
        {
            return true;
        }
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        if (comp == null)
        {
            return true;
        }
        if (comp.HasGridFireAt(c))
        {
            comp.GrowGridFireSizeAt(c, 0.1f);
            return false;
        }
        if (!c.ContainsStaticFire(map))
        {
            comp.TryAddGridFire(c, FireMath.MinFireSize, null);
            return false;
        }
        return true;
    }
}
