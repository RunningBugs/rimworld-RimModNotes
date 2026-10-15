using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

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


/*
*   Patch when RemoveDesignation is called
*   This is useful to mark the HaulUrgentlyCache as dirty
*/
[HarmonyPatch(typeof(DesignationManager), "RemoveDesignation")]
public static class DesignationManager_RemoveDesignation_Patch
{
    public static void Postfix(DesignationManager __instance, Designation des)
    {
        if (des == null || des.def != HaulUrgentlyDefOf.HaulUrgentlyDesignation)
        {
            return;
        }

        HaulUrgentlyCache cache = __instance.map.GetComponent<HaulUrgentlyCache>();
        if (cache != null)
        {
            cache.dirty = true;
        }
    }
}

[HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.AddDesignation))]
public static class DesignationManager_AddDesignation_Patch
{
    public static void Postfix(DesignationManager __instance, Designation newDes)
    {
        if (newDes == null || newDes.def != HaulUrgentlyDefOf.HaulUrgentlyDesignation)
        {
            return;
        }

        __instance.map.GetComponent<HaulUrgentlyCache>()?.ClearCache();
    }
}


internal static class HaulUrgentlyDesignationUtility
{
    public static void RemoveUrgentDesignationOn(Thing thing)
    {
        if (thing == null)
        {
            return;
        }

        Map map = thing.MapHeld ?? thing.Map;
        if (map?.designationManager == null)
        {
            return;
        }

        if (map.designationManager.DesignationOn(thing, HaulUrgentlyDefOf.HaulUrgentlyDesignation) != null)
        {
            map.designationManager.TryRemoveDesignationOn(thing, HaulUrgentlyDefOf.HaulUrgentlyDesignation);
            map.GetComponent<HaulUrgentlyCache>()?.ClearCache();
        }
    }
}

[HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.PlaceHauledThingInCell), new Type[] { typeof(TargetIndex), typeof(Toil), typeof(bool), typeof(bool) })]
public static class ToilsHaul_PlaceHauledThingInCell_Patch
{
    public static void Postfix(Toil __result)
    {
        Action originalInitAction = __result.initAction;
        __result.initAction = () =>
        {
            Thing carriedThing = __result.actor?.carryTracker?.CarriedThing;
            HaulUrgentlyDesignationUtility.RemoveUrgentDesignationOn(carriedThing);
            originalInitAction?.Invoke();
        };
    }
}

[HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.DepositHauledThingInContainer), new Type[] { typeof(TargetIndex), typeof(TargetIndex), typeof(Action) })]
public static class ToilsHaul_DepositHauledThingInContainer_Patch
{
    public static void Postfix(Toil __result)
    {
        Action originalInitAction = __result.initAction;
        __result.initAction = () =>
        {
            Thing carriedThing = __result.actor?.carryTracker?.CarriedThing;
            HaulUrgentlyDesignationUtility.RemoveUrgentDesignationOn(carriedThing);
            originalInitAction?.Invoke();
        };
    }
}

[DefOf]
public static class HaulUrgentlyDefOf
{
    public static DesignationDef HaulUrgentlyDesignation;
}

[StaticConstructorOnStartup]
public static class PickUpAndHaulCompatHandler
{

    static PickUpAndHaulCompatHandler()
    {
        Apply();
    }

    public static void Apply()
    {
        try
        {
            Type typeInAnyAssembly = GenTypes.GetTypeInAnyAssembly("PickUpAndHaul.WorkGiver_HaulToInventory");
            if (!(typeInAnyAssembly == null))
            {
                if (!typeof(WorkGiver_HaulGeneral).IsAssignableFrom(typeInAnyAssembly))
                {
                    throw new Exception("Expected work giver to extend WorkGiver_HaulGeneral");
                }
                if (typeInAnyAssembly.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new Exception("Expected work giver to have parameterless constructor");
                }
                WorkGiver_HaulGeneral haulWorkGiver = (WorkGiver_HaulGeneral)Activator.CreateInstance(typeInAnyAssembly);
                WorkGiver_HaulUrgently.JobOnThingDelegate = (pawn, thing, forced) => haulWorkGiver.ShouldSkip(pawn, forced) ? null : haulWorkGiver.JobOnThing(pawn, thing, forced);
                Log.Message("Applied compatibility patch for \"Pick Up And Haul\"");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to apply Pick Up And Haul compatibility layer: {ex}");
        }
    }
}



public class WorkGiver_HaulUrgently : WorkGiver_HaulGeneral
{
    public delegate Job TryGetJobOnThing(Pawn pawn, Thing t, bool forced);

    public static TryGetJobOnThing JobOnThingDelegate = DefaultJobOnThing;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        IReadOnlyList<Thing> things = GetHaulablesForPawn(pawn);
        for (int i = 0; i < things.Count; i++)
        {
            Thing thing = things[i];
            if (IsValidUrgentHaulThingForPawn(pawn, thing))
            {
                yield return thing;
            }
        }
    }

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return GetHaulablesForPawn(pawn).Count == 0;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!IsValidUrgentHaulThingForPawn(pawn, t))
        {
            return null;
        }
        return JobOnThingDelegate(pawn, t, forced);
    }

    private static Job DefaultJobOnThing(Pawn pawn, Thing t, bool forced)
    {
        if (!HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, forced))
        {
            return null;
        }
        return HaulAIUtility.HaulToStorageJob(pawn, t, forced);
    }

    private static IReadOnlyList<Thing> GetHaulablesForPawn(Pawn pawn)
    {
        Map map = pawn?.Map;
        if (map == null)
        {
            return Array.Empty<Thing>();
        }
        return map.GetComponent<HaulUrgentlyCache>()?.GetDesignatedAndHaulableThingsForMap(GenTicks.TicksGame) ?? Array.Empty<Thing>();
    }

    private static bool IsValidUrgentHaulThingForPawn(Pawn pawn, Thing thing)
    {
        return pawn?.Map != null
            && thing != null
            && !thing.Destroyed
            && thing.Spawned
            && thing.Map == pawn.Map
            && thing.def != null;
    }
}


public class Alert_NoUrgentHaulStorage : Alert
{
    private const int MaxListedCulpritsInExplanation = 5;
    public override AlertPriority Priority => AlertPriority.High;
    protected override Color BGColor => new(1f, 0.9215686f, 0.01568628f, 0.35f);


    public Alert_NoUrgentHaulStorage()
    {
        defaultLabel = "Alert_noStorage_label".Translate();
        Recalculate();
    }


    public override TaggedString GetExplanation()
    {
        var things = Find.CurrentMap?.GetComponent<HaulUrgentlyCache>()?.GetNoStorageThings().Take(MaxListedCulpritsInExplanation + 1).ToList() ?? new List<Thing>();
        var list = things.Select(t => t?.LabelShort).Take(MaxListedCulpritsInExplanation).ToList();
        if (things.Count > MaxListedCulpritsInExplanation)
        {
            list.Add("...");
        }
        return "Alert_noStorage_desc".Translate(string.Join(", ", list));
    }

    public override AlertReport GetReport()
    {
        var things = Find.CurrentMap?.GetComponent<HaulUrgentlyCache>()?.GetNoStorageThings();

        if (things != null && things.Any())
        {
            return AlertReport.CulpritsAre(things.ToList());
        }
        return AlertReport.Inactive;
    }
}


public class HaulUrgentlyCache : MapComponent
{
    private const int CacheExpireTickInterval = 60;
    private const int CleanupTickInterval = 60;
    private const int NoStorageCacheExpireTickInterval = 60;

    private int lastUpdateTick = -999999;
    private int lastNoStorageUpdateTick = -999999;

    private readonly List<Thing> designatedThings = new();
    private readonly List<Thing> designatedHaulableThings = new();
    private readonly List<Thing> noStorageThings = new();
    private readonly HashSet<Thing> designatedSet = new();
    private readonly Queue<Designation> cleanupQueue = new();

    public bool dirty = true;

    public HaulUrgentlyCache(Map map) : base(map)
    {
    }

    public IReadOnlyList<Thing> GetDesignatedAndHaulableThingsForMap(int tick)
    {
        RecacheIfNeeded(tick);
        return designatedHaulableThings;
    }

    public IEnumerable<Thing> GetNoStorageThings()
    {
        int tick = GenTicks.TicksGame;
        RecacheIfNeeded(tick);
        if (dirty || tick >= lastNoStorageUpdateTick + NoStorageCacheExpireTickInterval)
        {
            RebuildNoStorageCache();
            lastNoStorageUpdateTick = tick;
        }
        return noStorageThings;
    }

    public override void MapComponentTick()
    {
        int tick = GenTicks.TicksGame;
        if (dirty || tick >= lastUpdateTick + CacheExpireTickInterval)
        {
            BuildCache(tick);
        }

        if (tick % CleanupTickInterval == map.uniqueID % CleanupTickInterval)
        {
            CleanupInvalidDesignations();
        }
    }

    public void ClearCache()
    {
        designatedThings.Clear();
        designatedHaulableThings.Clear();
        noStorageThings.Clear();
        designatedSet.Clear();
        lastUpdateTick = -999999;
        lastNoStorageUpdateTick = -999999;
        dirty = true;
    }

    private void RecacheIfNeeded(int tick)
    {
        if (dirty || tick >= lastUpdateTick + CacheExpireTickInterval)
        {
            BuildCache(tick);
        }
    }

    private void BuildCache(int tick)
    {
        designatedThings.Clear();
        designatedHaulableThings.Clear();
        designatedSet.Clear();

        List<Designation> urgentDesignations = map.designationManager.designationsByDef[HaulUrgentlyDefOf.HaulUrgentlyDesignation];
        for (int i = 0; i < urgentDesignations.Count; i++)
        {
            Designation designation = urgentDesignations[i];
            Thing thing = designation.target.Thing;
            if (IsValidDesignatedThing(thing))
            {
                designatedThings.Add(thing);
                designatedSet.Add(thing);
            }
            else
            {
                cleanupQueue.Enqueue(designation);
            }
        }

        ICollection<Thing> haulables = map.listerHaulables.ThingsPotentiallyNeedingHauling();
        foreach (Thing thing in haulables)
        {
            if (designatedSet.Contains(thing) && IsValidDesignatedThing(thing))
            {
                designatedHaulableThings.Add(thing);
            }
        }

        RemoveQueuedDesignations();
        lastUpdateTick = tick;
        dirty = false;
    }

    private void CleanupInvalidDesignations()
    {
        RecacheIfNeeded(GenTicks.TicksGame);
        List<Designation> urgentDesignations = map.designationManager.designationsByDef[HaulUrgentlyDefOf.HaulUrgentlyDesignation];
        for (int i = 0; i < urgentDesignations.Count; i++)
        {
            Designation designation = urgentDesignations[i];
            Thing thing = designation.target.Thing;
            if (!IsValidDesignatedThing(thing) || !designatedHaulableThings.Contains(thing))
            {
                cleanupQueue.Enqueue(designation);
            }
        }

        if (cleanupQueue.Count > 0)
        {
            RemoveQueuedDesignations();
            ClearCache();
        }
    }

    private void RemoveQueuedDesignations()
    {
        while (cleanupQueue.Count > 0)
        {
            Designation designation = cleanupQueue.Dequeue();
            if (designation?.designationManager != null)
            {
                designation.designationManager.RemoveDesignation(designation);
            }
        }
    }

    private void RebuildNoStorageCache()
    {
        noStorageThings.Clear();
        for (int i = 0; i < designatedHaulableThings.Count; i++)
        {
            Thing thing = designatedHaulableThings[i];
            if (!IsValidDesignatedThing(thing) || map.reservationManager.IsReserved(thing))
            {
                continue;
            }

            if (!StoreUtility.TryFindBestBetterStorageFor(thing, null, map, StoreUtility.CurrentStoragePriorityOf(thing, false), Faction.OfPlayer, out IntVec3 _, out IHaulDestination _))
            {
                noStorageThings.Add(thing);
            }
        }
    }

    private bool IsValidDesignatedThing(Thing thing)
    {
        return thing != null
            && !thing.Destroyed
            && thing.Spawned
            && thing.Map == map
            && thing.def != null;
    }
}

public class Designator_HaulUrgent : Designator
{
    private static readonly List<Thing> tmpDesignateThings = new();

    protected override DesignationDef Designation => HaulUrgentlyDefOf.HaulUrgentlyDesignation;
    public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

    public Designator_HaulUrgent()
    {
        defaultLabel = "DesignatorHaulUrgently".Translate();
        defaultDesc = "DesignatorHaulUrgentlyDesc".Translate();
        icon = ContentFinder<Texture2D>.Get("haulUrgently", true);
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_Haul;
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        return true;
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        tmpDesignateThings.Clear();
        List<Thing> things = Map.thingGrid.ThingsListAtFast(c);
        for (int i = 0; i < things.Count; i++)
        {
            tmpDesignateThings.Add(things[i]);
        }

        for (int i = 0; i < tmpDesignateThings.Count; i++)
        {
            DesignateThing(tmpDesignateThings[i]);
        }
        tmpDesignateThings.Clear();
    }

    private bool ThingIsRelevant(Thing thing)
    {
        if (thing.def == null || thing.Map == null || GridsUtility.Fogged(thing.Position, thing.Map))
        {
            return false;
        }
        return (thing.def.alwaysHaulable || thing.def.EverHaulable) && !StoreUtility.IsInValidBestStorage(thing);
    }

    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        return ThingIsRelevant(t) && t.MapHeld != null && t.MapHeld.designationManager.DesignationOn(t, HaulUrgentlyDefOf.HaulUrgentlyDesignation) == null;
    }

    public override void DesignateThing(Thing t)
    {
        if (!CanDesignateThing(t).Accepted)
        {
            return;
        }

        Map map = t.MapHeld ?? t.Map;
        if (map == null)
        {
            return;
        }

        if (t.def.designateHaulable && map.designationManager.DesignationOn(t, DesignationDefOf.Haul) == null)
        {
            map.designationManager.AddDesignation(new Designation(t, DesignationDefOf.Haul));
        }

        if (map.designationManager.DesignationOn(t, Designation) == null)
        {
            map.designationManager.AddDesignation(new Designation(t, Designation));
        }

        t.SetForbidden(false, false);
        map.GetComponent<HaulUrgentlyCache>()?.ClearCache();
    }

    public override void SelectedUpdate()
    {
        GenUI.RenderMouseoverBracket();
    }
}

