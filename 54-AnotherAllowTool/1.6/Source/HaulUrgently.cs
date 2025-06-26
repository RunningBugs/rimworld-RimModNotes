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


public class Toils_UrgentHaul
{
    public static Toil RemoveUrgentHaulDesignation(HaulUrgentlyCache cache)
    {
        Toil toil = new()
        {
            initAction = () =>
            {
                cache.dirty = true;
                Log.Warning($"Urgent Haul cache make dirty in toil");
            },
            defaultCompleteMode = ToilCompleteMode.Instant
        };
        // toil.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        return toil;
    }
}


/**
*   Patches on JobDriver_HaulToCell will update the HaulUrgentlyCache
*   This is useful to remove the designation when the job is done
*/
[HarmonyPatch(typeof(JobDriver_HaulToCell), "MakeNewToils")]
public static class JobDriver_HaulToCell_MakeNewToils_Patch
{
    public static void Postfix(JobDriver_HaulToCell __instance, ref IEnumerable<Toil> __result)
    {
        var pawn = __instance.pawn;
        if (pawn != null && pawn.Map != null)
        {
            HaulUrgentlyCache cache = pawn.Map.GetComponent<HaulUrgentlyCache>();
            if (cache != null)
            {
                __result.AddItem(Toils_UrgentHaul.RemoveUrgentHaulDesignation(cache));
            }
            else
            {
                Log.Error($"HaulUrgentlyCache not found for map {pawn.Map} in JobDriver_HaulToCell_MakeNewToils_Patch");
            }
        }
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



public class WorkGiver_HaulUrgently : WorkGiver_Scanner
{
    public delegate Job TryGetJobOnThing(Pawn pawn, Thing t, bool forced);

    public static TryGetJobOnThing JobOnThingDelegate = (pawn, t, forced) => HaulAIUtility.HaulToStorageJob(pawn, t, false);
    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return JobOnThingDelegate(pawn, t, forced);
    }

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        IReadOnlyList<Thing> things = GetHaulablesForPawn(pawn);
        for (int i = 0; i < things.Count; i++)
        {
            if (HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, things[i], false))
            {
                yield return things[i];
            }
        }
    }

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return GetHaulablesForPawn(pawn).Count == 0;
    }

    private static IReadOnlyList<Thing> GetHaulablesForPawn(Pawn pawn)
    {
        Map map = pawn.Map;
        IReadOnlyList<Thing> res = null;
        if (map != null)
        {
            res = map.GetComponent<HaulUrgentlyCache>()?.GetDesignatedAndHaulableThingsForMap(map, GenTicks.TicksGame) ?? new List<Thing>();
            return res;
        }
        res = new List<Thing>();
        return res;
    }
}


public class Alert_NoUrgentHaulStorage : Alert
{
    private const int MaxListedCulpritsInExplanation = 5;
    public override AlertPriority Priority => AlertPriority.High;
    public override Color BGColor => new(1f, 0.9215686f, 0.01568628f, 0.35f);


    public Alert_NoUrgentHaulStorage()
    {
        defaultLabel = "Alert_noStorage_label".Translate();
        Recalculate();
    }


    public override TaggedString GetExplanation()
    {
        var things = Find.CurrentMap.GetComponent<HaulUrgentlyCache>()?.GetNoStorageThings().Take(MaxListedCulpritsInExplanation + 1).ToList();
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
    private int lastUpdateTick = -1;
    private const int expireTickInterval = 1;
    private List<Thing> designatedThings = new();
    private List<Thing> designatedHaulableThings = new();
    public bool dirty = true;

    private bool IsInValid(int tick)
    {
        return lastUpdateTick < 0 || tick >= lastUpdateTick + expireTickInterval;
    }

    private void BuildCache()
    {
        // Currently the dirty check is disabled
        // Reason being the Job will not correctly change the dirty flag 
        // TODO: Need to implement this in the Job system in the future
        if (dirty)
        {
            designatedThings = map.designationManager.AllDesignations.Where(d => d.def == HaulUrgentlyDefOf.HaulUrgentlyDesignation).Select(d => d.target.Thing).ToList();

            designatedHaulableThings = designatedThings.Where(t => t.def.EverHaulable || t.def.alwaysHaulable).ToList();
            lastUpdateTick = GenTicks.TicksGame;
            dirty = false;
        }
    }

    public IEnumerable<Thing> GetNoStorageThings()
    {
        return designatedThings.Where(t => !StoreUtility.TryFindBestBetterStorageFor(t, null, t.Map, StoreUtility.CurrentStoragePriorityOf(t), Faction.OfPlayer, out IntVec3 v1, out IHaulDestination v2));
    }

    public void UpdateInStorageDesignations()
    {
        designatedThings.ForEach(t =>
        {
            if (StoreUtility.IsInValidBestStorage(t))
            {
                map.designationManager.TryRemoveDesignationOn(t, HaulUrgentlyDefOf.HaulUrgentlyDesignation);
            }
        });
    }

    public override void MapComponentTick()
    {
        if (!IsInValid(GenTicks.TicksGame))
        {
            return;
        }
        UpdateInStorageDesignations();
        BuildCache();
    }

    public void ClearCache()
    {
        designatedThings.Clear();
        designatedHaulableThings.Clear();
        lastUpdateTick = -1;
        dirty = true;
    }

    public HaulUrgentlyCache(Map map) : base(map)
    {
    }

    public IReadOnlyList<Thing> GetDesignatedAndHaulableThingsForMap(Map map, int tick)
    {
        if (IsInValid(tick))
        {
            BuildCache();
        }
        return designatedHaulableThings;
    }
}

public class Designator_HaulUrgent : Designator
{
    public override DesignationDef Designation => HaulUrgentlyDefOf.HaulUrgentlyDesignation;
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
        var things = Map.thingGrid.ThingsListAtFast(c);
        DesignateMultiThing(things);
    }

    private void DesignateMultiThing(IEnumerable<Thing> things)
    {
        foreach (Thing thing in things)
        {
            DesignateThing(thing);
        }
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
        if (CanDesignateThing(t).Accepted)
        {
            t.MapHeld.designationManager.AddDesignation(new Designation(t, Designation));
            t.SetForbidden(false, false);
            t.MapHeld.GetComponent<HaulUrgentlyCache>().dirty = true;
        }
        // t.MapHeld.GetComponent<HaulUrgentlyCache>()?.ClearCache();
    }

    public override void SelectedUpdate()
    {
        GenUI.RenderMouseoverBracket();
    }
}

