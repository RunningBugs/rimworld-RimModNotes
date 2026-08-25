using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CommonModCompatibilityPatches;

[StaticConstructorOnStartup]
public static class CommonCompatibilityBootstrap
{
    private static readonly Harmony Harmony = new("com.RunningBugs.CommonModCompatibilityPatches");

    static CommonCompatibilityBootstrap()
    {
        int applied = 0;
        applied += Apply(AllowToolHaulUrgentlyCompatibility.TryApply);
        applied += Apply(ReservationEventCompatibility.TryApply);
        applied += Apply(ReplaceStuffBridgeCompatibility.TryApply);
        applied += Apply(ReplaceStuffOverMineableCompatibility.TryApply);
        applied += Apply(BuildFromInventoryReservationCountCompatibility.TryApply);
        applied += Apply(TinyTweaksAutoRebuildCompatibility.TryApply);
        applied += Apply(RimStoryADeadCompatibility.TryApply);
        applied += Apply(RimStoryMassFuneralCompatibility.TryApply);
        applied += Apply(GoodwillSituationManagerThreadSafetyCompatibility.TryApply);
        applied += Apply(ZeroWeightSongSelectionCompatibility.TryApply);
        applied += Apply(NalsDynamicPortraitsWorkItemsCompatibility.TryApply);
        applied += Apply(NewRatkinWanderingCaravanCompatibility.TryApply);
        applied += Apply(InvokeHoraxOfferingCompatibility.TryApply);
        applied += Apply(SleepingSlotFallbackCompatibility.TryApply);
        applied += Apply(PawnDuplicatorGeneCopyCompatibility.TryApply);
        applied += Apply(PawnHealthBarBleedLabelCompatibility.TryApply);
        applied += Apply(KiiroStealthMapTickCompatibility.TryApply);
        applied += Apply(AlienRaceNullPawnRulesCompatibility.TryApply);
        applied += Apply(MinifyEverythingReinstallReservationCompatibility.TryApply);
        applied += Apply(LifeStageMinAgeFallbackCompatibility.TryApply);
        applied += Apply(QualityBuilderNullMapCompatibility.TryApply);
        applied += Apply(VbeVideoPathEscapingCompatibility.TryApply);
        applied += Apply(ShipJobWaitDespawnedThingsCompatibility.TryApply);
        applied += Apply(InventoryTabStableSortCompatibility.TryApply);
        applied += Apply(GD3BlackShieldMaplessRemovalCompatibility.TryApply);
        applied += Apply(PawnBodySilhouetteDrawCompatibility.TryApply);
        applied += Apply(TacticalGroupsColonyGroupIdCollisionCompatibility.TryApply);
        applied += Apply(TacticalGroupsBillPawnRestrictionCompatibility.TryApply);
        applied += Apply(AllowToolPartyHuntWorldSettingsCompatibility.TryApply);
        applied += Apply(ListerHaulablesSharedScanCursorCompatibility.TryApply);

        Log.Message($"[CommonModCompatibilityPatches] Applied {applied} compatibility patch group(s).".Colorize(Color.green));
    }

    // A TryApply that throws (e.g. a Harmony argument-injection mismatch)
    // must never take down the whole static constructor: without this guard a
    // single broken group aborts the .cctor with a TypeInitializationException
    // and silently disables every other patch group too.
    private static int Apply(Func<Harmony, bool> tryApply)
    {
        try
        {
            return tryApply(Harmony) ? 1 : 0;
        }
        catch (Exception exception)
        {
            Log.Warning($"[CommonModCompatibilityPatches] Skipped a compatibility patch group because it failed to apply: {exception.GetType().Name}: {exception.Message}");
            return 0;
        }
    }
}

internal static class InvokeHoraxOfferingCompatibility
{
    private const int OfferingTransferWarningId = 82038122;
    private const int OfferingConsumeWarningId = 82038123;
    private const int OfferingMissingWarningId = 82038124;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive("Ludeon.RimWorld.Anomaly"))
        {
            return false;
        }

        MethodInfo holdTarget = AccessTools.Method(typeof(PsychicRitualToil_InvokeHorax), nameof(PsychicRitualToil_InvokeHorax.HoldRequiredOfferings), new[] { typeof(PsychicRitual), typeof(PsychicRitualGraph) });
        MethodInfo consumeTarget = AccessTools.Method(typeof(PsychicRitualToil_InvokeHorax), nameof(PsychicRitualToil_InvokeHorax.ConsumeRequiredOffering), new[] { typeof(PsychicRitual) });
        MethodInfo holdPrefix = AccessTools.Method(typeof(InvokeHoraxOfferingCompatibility), nameof(HoldRequiredOfferingsPrefix));
        MethodInfo consumePrefix = AccessTools.Method(typeof(InvokeHoraxOfferingCompatibility), nameof(ConsumeRequiredOfferingPrefix));
        if (holdTarget == null || consumeTarget == null || holdPrefix == null || consumePrefix == null)
        {
            return false;
        }

        harmony.Patch(holdTarget, prefix: new HarmonyMethod(holdPrefix));
        harmony.Patch(consumeTarget, prefix: new HarmonyMethod(consumePrefix));
        return true;
    }

    public static bool HoldRequiredOfferingsPrefix(PsychicRitualToil_InvokeHorax __instance, PsychicRitual psychicRitual)
    {
        if (!TryGetContext(__instance, psychicRitual, out IngredientCount requiredOffering, out PsychicRitualRoleDef invokerRole, out List<Pawn> invokers))
        {
            return false;
        }

        int remaining = Mathf.CeilToInt(requiredOffering.GetBaseCount()) - CountHeldOfferings(invokers, requiredOffering);
        if (remaining <= 0)
        {
            return false;
        }

        foreach (Pawn pawn in invokers)
        {
            if (remaining <= 0)
            {
                break;
            }
            if (pawn?.inventory?.innerContainer == null || pawn.carryTracker?.innerContainer == null)
            {
                continue;
            }

            List<Thing> offerings = pawn.inventory.GetDirectlyHeldThings()
                .Where(thing => IsValidOffering(thing, requiredOffering))
                .ToList();
            foreach (Thing offering in offerings)
            {
                if (remaining <= 0)
                {
                    break;
                }
                if (!pawn.inventory.innerContainer.Contains(offering))
                {
                    continue;
                }

                int transferCount = Mathf.Min(remaining, offering.stackCount);
                int transferred = pawn.inventory.innerContainer.TryTransferToContainer(offering, pawn.carryTracker.innerContainer, transferCount);
                remaining -= transferred;
            }
        }

        Log.WarningOnce("[CommonModCompatibilityPatches] Used collection-safe Invoke Horax required-offering transfer to avoid modifying a pawn inventory while enumerating it.", OfferingTransferWarningId);
        return false;
    }

    public static bool ConsumeRequiredOfferingPrefix(PsychicRitualToil_InvokeHorax __instance, PsychicRitual psychicRitual)
    {
        if (!TryGetContext(__instance, psychicRitual, out IngredientCount requiredOffering, out PsychicRitualRoleDef invokerRole, out List<Pawn> invokers))
        {
            return false;
        }

        int remaining = Mathf.CeilToInt(requiredOffering.GetBaseCount());
        ConsumeFromCarryTrackers(invokers, requiredOffering, ref remaining);
        if (remaining > 0)
        {
            ConsumeFromInventories(invokers, requiredOffering, ref remaining);
            Log.WarningOnce("[CommonModCompatibilityPatches] Consumed Invoke Horax required offerings from invoker inventories as a fallback because they were not all held in carry trackers at ritual end.", OfferingConsumeWarningId);
        }

        if (remaining > 0)
        {
            Log.WarningOnce($"[CommonModCompatibilityPatches] Invoke Horax ritual ended with {remaining} required offering(s) still unaccounted for; suppressed vanilla ConsumeRequiredOffering exception after consuming every matching carried/inventory offering found.", OfferingMissingWarningId);
        }

        return false;
    }

    private static bool TryGetContext(PsychicRitualToil_InvokeHorax toil, PsychicRitual psychicRitual, out IngredientCount requiredOffering, out PsychicRitualRoleDef invokerRole, out List<Pawn> invokers)
    {
        requiredOffering = toil?.requiredOffering;
        invokerRole = toil?.invokerRole;
        invokers = null;
        if (requiredOffering == null || psychicRitual?.assignments == null || invokerRole == null)
        {
            return false;
        }

        invokers = psychicRitual.assignments.AssignedPawns(invokerRole).ToList();
        return true;
    }

    private static int CountHeldOfferings(List<Pawn> invokers, IngredientCount requiredOffering)
    {
        int count = 0;
        for (int i = 0; i < invokers.Count; i++)
        {
            Thing carriedThing = invokers[i]?.carryTracker?.CarriedThing;
            if (IsValidOffering(carriedThing, requiredOffering))
            {
                count += carriedThing.stackCount;
            }
        }
        return count;
    }

    private static void ConsumeFromCarryTrackers(List<Pawn> invokers, IngredientCount requiredOffering, ref int remaining)
    {
        for (int i = 0; i < invokers.Count && remaining > 0; i++)
        {
            Thing carriedThing = invokers[i]?.carryTracker?.CarriedThing;
            ConsumeThing(carriedThing, requiredOffering, ref remaining);
        }
    }

    private static void ConsumeFromInventories(List<Pawn> invokers, IngredientCount requiredOffering, ref int remaining)
    {
        for (int i = 0; i < invokers.Count && remaining > 0; i++)
        {
            Pawn pawn = invokers[i];
            if (pawn?.inventory?.innerContainer == null)
            {
                continue;
            }

            List<Thing> offerings = pawn.inventory.GetDirectlyHeldThings()
                .Where(thing => IsValidOffering(thing, requiredOffering))
                .ToList();
            foreach (Thing offering in offerings)
            {
                if (remaining <= 0)
                {
                    break;
                }
                if (!pawn.inventory.innerContainer.Contains(offering))
                {
                    continue;
                }

                ConsumeThing(offering, requiredOffering, ref remaining);
            }
        }
    }

    private static bool IsValidOffering(Thing thing, IngredientCount requiredOffering)
    {
        return thing != null && !thing.Destroyed && thing.stackCount > 0 && requiredOffering?.filter?.Allows(thing) == true;
    }

    private static void ConsumeThing(Thing thing, IngredientCount requiredOffering, ref int remaining)
    {
        if (remaining <= 0 || !IsValidOffering(thing, requiredOffering))
        {
            return;
        }

        int consumeCount = Mathf.Min(remaining, thing.stackCount);
        if (consumeCount < thing.stackCount)
        {
            thing.stackCount -= consumeCount;
        }
        else
        {
            thing.Destroy();
        }
        remaining -= consumeCount;
    }
}

internal static class NewRatkinWanderingCaravanCompatibility
{
    public const string PackageId = "Solaris.RatkinRaceMod";
    private const int CleanupWarningId = 82038121;
    private static FieldInfo rosterLeaderField;
    private static FieldInfo rosterGuardsField;
    private static FieldInfo settlerPoolField;
    private static FieldInfo settlerRequirementsField;
    private static FieldInfo settlerAppearanceCountField;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type componentType = AccessTools.TypeByName("NewRatkin.GameComponent_WanderingCaravan");
        MethodInfo target = AccessTools.Method(componentType, "CleanupDeadPawns");
        MethodInfo prefix = AccessTools.Method(typeof(NewRatkinWanderingCaravanCompatibility), nameof(Prefix));
        if (componentType == null || target == null || prefix == null)
        {
            return false;
        }

        rosterLeaderField = AccessTools.Field(componentType, "rosterLeader");
        rosterGuardsField = AccessTools.Field(componentType, "rosterGuards");
        settlerPoolField = AccessTools.Field(componentType, "settlerPool");
        settlerRequirementsField = AccessTools.Field(componentType, "settlerRequirements");
        settlerAppearanceCountField = AccessTools.Field(componentType, "settlerAppearanceCount");
        if (rosterLeaderField == null || rosterGuardsField == null || settlerPoolField == null || settlerRequirementsField == null || settlerAppearanceCountField == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    public static bool Prefix(object __instance)
    {
        RemoveDeadOrNullPawns(GetPawnList(rosterLeaderField, __instance));
        RemoveDeadOrNullPawns(GetPawnList(rosterGuardsField, __instance));
        CleanupSettlerPool(__instance);
        return false;
    }

    private static List<Pawn> GetPawnList(FieldInfo field, object instance)
    {
        return field?.GetValue(instance) as List<Pawn>;
    }

    private static void RemoveDeadOrNullPawns(List<Pawn> pawns)
    {
        pawns?.RemoveAll(IsDeadOrNull);
    }

    private static void CleanupSettlerPool(object instance)
    {
        List<Pawn> settlerPool = GetPawnList(settlerPoolField, instance);
        if (settlerPool == null)
        {
            return;
        }

        object settlerRequirements = settlerRequirementsField.GetValue(instance);
        object settlerAppearanceCount = settlerAppearanceCountField.GetValue(instance);
        bool removedNullPawn = false;

        for (int i = settlerPool.Count - 1; i >= 0; i--)
        {
            Pawn pawn = settlerPool[i];
            if (!IsDeadOrNull(pawn))
            {
                continue;
            }

            settlerPool.RemoveAt(i);
            if (pawn == null)
            {
                removedNullPawn = true;
                continue;
            }

            RemoveDictionaryKey(settlerRequirements, pawn);
            RemoveDictionaryKey(settlerAppearanceCount, pawn);
        }

        if (removedNullPawn)
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] Removed null pawn reference(s) from NewRatkinPlus wandering caravan settler pool before the original cleanup could call Dictionary.Remove(null).", CleanupWarningId);
        }
    }

    private static bool IsDeadOrNull(Pawn pawn)
    {
        return pawn == null || pawn.DestroyedOrNull() || pawn.Dead;
    }

    private static void RemoveDictionaryKey(object dictionary, Pawn pawn)
    {
        if (dictionary == null || pawn == null)
        {
            return;
        }

        if (dictionary is IDictionary nonGenericDictionary)
        {
            nonGenericDictionary.Remove(pawn);
        }
    }
}

internal static class NalsDynamicPortraitsWorkItemsCompatibility
{
    public const string PackageId = "Nals.DynamicPortraits";
    private const int UnsafeWorkItemWarningId = 82038119;
    private const int SuppressedWorkItemExceptionWarningId = 82038120;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type renderColonistType = AccessTools.TypeByName("DynamicPortrait.RenderColonist");
        MethodInfo target = AccessTools.Method(renderColonistType, "DrawWorkItems");
        MethodInfo prefix = AccessTools.Method(typeof(NalsDynamicPortraitsWorkItemsCompatibility), nameof(Prefix));
        MethodInfo finalizer = AccessTools.Method(typeof(NalsDynamicPortraitsWorkItemsCompatibility), nameof(Finalizer));

        if (target == null || prefix == null || finalizer == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix), finalizer: new HarmonyMethod(finalizer));
        return true;
    }

    public static bool Prefix(Pawn pawn)
    {
        if (pawn?.CurJob == null || pawn.CurJobDef == null)
        {
            return false;
        }

        if (!IsSafeWorkItemTarget(pawn.CurJob.targetA.Thing)
            || !IsSafeWorkItemTarget(pawn.CurJob.targetB.Thing)
            || !IsSafeWorkItemTarget(pawn.CurJob.targetC.Thing))
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] Skipped [NL] Dynamic Portraits work-item overlay because a current job target has an incomplete ThingDef/graphic definition that would crash DrawWorkItems.", UnsafeWorkItemWarningId);
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

        if (__exception is NullReferenceException || __exception is ArgumentException)
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] Suppressed [NL] Dynamic Portraits DrawWorkItems exception: " + __exception.GetType().Name + ": " + __exception.Message, SuppressedWorkItemExceptionWarningId);
            return null;
        }

        return __exception;
    }

    private static bool IsSafeWorkItemTarget(Thing thing)
    {
        if (thing == null)
        {
            return true;
        }

        ThingDef def = thing.def;
        if (def == null)
        {
            return false;
        }

        Texture2D uiIcon;
        try
        {
            uiIcon = def.uiIcon;
        }
        catch
        {
            return false;
        }

        if ((UnityEngine.Object)(object)uiIcon == (UnityEngine.Object)(object)BaseContent.BadTex)
        {
            return true;
        }

        Type thingClass = def.thingClass;
        if (thingClass == null)
        {
            return false;
        }

        if (thingClass.IsSubclassOf(typeof(Building)) && def.graphicData == null)
        {
            return false;
        }

        return true;
    }
}

internal static class ZeroWeightSongSelectionCompatibility
{
    private const int ZeroWeightSongWarningId = 82038117;
    private const int EmergencySongWarningId = 82038118;
    private static FieldInfo recentSongsField;
    private static MethodInfo appropriateNowMethod;

    public static bool TryApply(Harmony harmony)
    {
        MethodInfo target = AccessTools.Method(typeof(MusicManagerPlay), "ChooseNextSong");
        MethodInfo prefix = AccessTools.Method(typeof(ZeroWeightSongSelectionCompatibility), nameof(Prefix));
        recentSongsField = AccessTools.Field(typeof(MusicManagerPlay), "recentSongs");
        appropriateNowMethod = AccessTools.Method(typeof(MusicManagerPlay), "AppropriateNow");

        if (target == null || prefix == null || recentSongsField == null || appropriateNowMethod == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    public static bool Prefix(MusicManagerPlay __instance, ref SongDef __result)
    {
        Queue<SongDef> recentSongs = recentSongsField.GetValue(__instance) as Queue<SongDef>;
        if (recentSongs == null)
        {
            return true;
        }

        TrimRecentSongs(recentSongs);

        List<SongDef> candidates = GetAppropriateSongs(__instance);
        if (candidates.Empty())
        {
            recentSongs.Clear();
            return true;
        }

        if (TotalCommonality(candidates) > 0f)
        {
            return true;
        }

        recentSongs.Clear();
        List<SongDef> candidatesAfterRecentReset = GetAppropriateSongs(__instance);
        if (TotalCommonality(candidatesAfterRecentReset) > 0f)
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] MusicManagerPlay.ChooseNextSong found only zero-weight songs after recent-song filtering; cleared recent songs so vanilla can choose a normal positive-weight song instead of playing zero-weight special-use music.", ZeroWeightSongWarningId);
            return true;
        }

        SongDef emergencySong = DefDatabase<SongDef>.AllDefs
            .Where(song => song != null && song.playOnMap && song.commonality > 0f && song.clip != null)
            .RandomElementWithFallback();
        if (emergencySong == null)
        {
            return true;
        }

        __result = emergencySong;
        Log.WarningOnce("[CommonModCompatibilityPatches] MusicManagerPlay.ChooseNextSong found no positive-weight appropriate songs even after clearing recent songs; selected a positive-weight map song as an emergency fallback and did not select zero-weight special-use music. Selected song: " + __result.defName, EmergencySongWarningId);
        return false;
    }

    private static void TrimRecentSongs(Queue<SongDef> recentSongs)
    {
        while (recentSongs.Count > 7)
        {
            recentSongs.Dequeue();
        }
    }

    private static List<SongDef> GetAppropriateSongs(MusicManagerPlay manager)
    {
        return DefDatabase<SongDef>.AllDefs.Where(song => AppropriateNow(manager, song)).ToList();
    }

    private static float TotalCommonality(List<SongDef> songs)
    {
        float totalCommonality = 0f;
        for (int i = 0; i < songs.Count; i++)
        {
            totalCommonality += songs[i].commonality;
        }
        return totalCommonality;
    }

    private static bool AppropriateNow(MusicManagerPlay manager, SongDef song)
    {
        try
        {
            return song != null && (bool)appropriateNowMethod.Invoke(manager, new object[] { song });
        }
        catch
        {
            return false;
        }
    }
}

internal static class SleepingSlotFallbackCompatibility
{
    private const int OccupiedSlotFallbackWarningId = 82038126;
    private const int MissingSlotFallbackWarningId = 82038127;

    public static bool TryApply(Harmony harmony)
    {
        MethodInfo target = AccessTools.Method(typeof(RestUtility), nameof(RestUtility.GetBedSleepingSlotPosFor));
        MethodInfo prefix = AccessTools.Method(typeof(SleepingSlotFallbackCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    public static bool Prefix(Pawn pawn, Building_Bed bed, ref IntVec3 __result)
    {
        if (pawn == null || bed == null)
        {
            return true;
        }

        if (bed.IsOwner(pawn, out int? assignedSleepingSlot))
        {
            __result = bed.GetSleepingSlotPos(assignedSleepingSlot.Value);
            return false;
        }

        for (int i = 0; i < bed.SleepingSlotsCount; i++)
        {
            if ((i >= bed.OwnersForReading.Count || bed.OwnersForReading[i] == null) && bed.GetCurOccupant(i) == pawn)
            {
                __result = bed.GetSleepingSlotPos(i);
                return false;
            }
        }

        for (int j = 0; j < bed.SleepingSlotsCount; j++)
        {
            if ((j >= bed.OwnersForReading.Count || bed.OwnersForReading[j] == null) && bed.GetCurOccupant(j) == null)
            {
                __result = bed.GetSleepingSlotPos(j);
                return false;
            }
        }

        // Vanilla emits the red error "Could not find good sleeping slot
        // position" here (typically when a bed is reassigned or fully occupied
        // while a pawn is still lying in it). Fall back gracefully instead: a
        // pawn physically occupying a slot still gets their actual slot, so
        // checks like JobInBedUtility.InBedOrRestSpotNow keep working.
        for (int k = 0; k < bed.SleepingSlotsCount; k++)
        {
            if (bed.GetCurOccupant(k) == pawn)
            {
                __result = bed.GetSleepingSlotPos(k);
                Log.WarningOnce($"[CommonModCompatibilityPatches] {pawn} had no unassigned sleeping slot in {bed}; used their occupied slot as fallback instead of logging the vanilla 'Could not find good sleeping slot position' error.", OccupiedSlotFallbackWarningId);
                return false;
            }
        }

        __result = bed.GetSleepingSlotPos(0);
        Log.WarningOnce($"[CommonModCompatibilityPatches] {pawn} had no unassigned sleeping slot in {bed}; used slot 0 as fallback instead of logging the vanilla 'Could not find good sleeping slot position' error.", MissingSlotFallbackWarningId);
        return false;
    }
}

internal static class PawnDuplicatorGeneCopyCompatibility
{
    private const int MissingOverrideGeneWarningId = 82038128;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive("Ludeon.RimWorld.Anomaly"))
        {
            return false;
        }

        MethodInfo target = AccessTools.Method(typeof(GameComponent_PawnDuplicator), "CopyGenes", new[] { typeof(Pawn), typeof(Pawn) });
        MethodInfo prefix = AccessTools.Method(typeof(PawnDuplicatorGeneCopyCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    // Reimplements the private vanilla CopyGenes with safe override linking.
    // Vanilla resolves xenogene override links before endogenes are copied, so
    // a xenogene overridden by an endogene (possible with modded xenotypes)
    // crashes Enumerable.First with "Sequence contains no matching element"
    // during the Unnatural Corpse incident. Here both lists are fully
    // populated before any link is resolved, and an overriding gene that
    // failed to be copied leaves the link null instead of throwing.
    public static bool Prefix(Pawn pawn, Pawn newPawn)
    {
        if (pawn?.genes == null || newPawn?.genes == null)
        {
            return true;
        }

        List<Gene> sourceXenogenes = null;
        if (ModsConfig.BiotechActive)
        {
            newPawn.genes.Xenogenes.Clear();
            sourceXenogenes = pawn.genes.Xenogenes;
            foreach (Gene item in sourceXenogenes)
            {
                newPawn.genes.AddGene(item.def, xenogene: true);
            }
        }

        newPawn.genes.Endogenes.Clear();
        List<Gene> sourceEndogenes = pawn.genes.Endogenes;
        foreach (Gene item2 in sourceEndogenes)
        {
            newPawn.genes.AddGene(item2.def, xenogene: false);
        }

        if (ModsConfig.BiotechActive)
        {
            ResolveOverrides(pawn, newPawn, sourceXenogenes, newPawn.genes.Xenogenes);
        }
        ResolveOverrides(pawn, newPawn, sourceEndogenes, newPawn.genes.Endogenes);
        return false;
    }

    private static void ResolveOverrides(Pawn pawn, Pawn newPawn, List<Gene> sourceGenes, List<Gene> copiedGenes)
    {
        int count = Math.Min(sourceGenes.Count, copiedGenes.Count);
        for (int i = 0; i < count; i++)
        {
            Gene sourceGene = sourceGenes[i];
            Gene copiedGene = copiedGenes[i];
            if (!sourceGene.Overridden)
            {
                copiedGene.overriddenByGene = null;
                continue;
            }

            Gene overridingGene = null;
            List<Gene> allCopiedGenes = newPawn.genes.GenesListForReading;
            for (int j = 0; j < allCopiedGenes.Count; j++)
            {
                if (allCopiedGenes[j].def == sourceGene.overriddenByGene.def)
                {
                    overridingGene = allCopiedGenes[j];
                    break;
                }
            }

            copiedGene.overriddenByGene = overridingGene;
            if (overridingGene == null)
            {
                Log.WarningOnce($"[CommonModCompatibilityPatches] Pawn duplicator could not find overriding gene '{sourceGene.overriddenByGene?.def?.defName}' for '{sourceGene.def?.defName}' on a duplicate of {pawn}; left the override link empty instead of throwing 'Sequence contains no matching element' during the Unnatural Corpse incident.", MissingOverrideGeneWarningId);
            }
        }
    }
}

internal static class PawnHealthBarBleedLabelCompatibility
{
    public const string PackageId = "Paluto22.PawnHealthBar";
    private const int BleedRateBoundaryWarningId = 82038129;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type extrasType = AccessTools.TypeByName("Paluto22_PawnHelathBar.DrawGUIOverlayExtras");
        MethodInfo target = AccessTools.Method(extrasType, "DrawBleedLabel");
        MethodInfo prefix = AccessTools.Method(typeof(PawnHealthBarBleedLabelCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    // The original guard lets BleedRateTotal >= 0.01f through, but its internal
    // GetPawnLabel only builds the label text when BleedRateTotal > 0.01f. A pawn
    // bleeding at exactly 0.01f therefore passes a null string to
    // GenUI.GetWidthCached, which throws ArgumentNullException every frame.
    // Tighten the boundary: skip the original method unless a label can
    // actually be produced (nothing drawable is lost otherwise).
    public static bool Prefix(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null)
        {
            return false;
        }

        float bleedRateTotal = pawn.health.hediffSet.BleedRateTotal;
        if (bleedRateTotal >= 0.01f && !(bleedRateTotal > 0.01f))
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] Skipped Pawn HealthBar bleed label for a pawn bleeding at exactly 0.01 rate; the original method would pass a null label to GenUI.GetWidthCached and throw ArgumentNullException every frame.", BleedRateBoundaryWarningId);
            return false;
        }

        return bleedRateTotal > 0.01f;
    }
}

internal static class KiiroStealthMapTickCompatibility
{
    public const string PackageId = "Ancot.KiiroRace";
    private const int OffMapStealthWarningId = 82038130;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type compType = AccessTools.TypeByName("Kiiro.HediffComp_Stealth");
        MethodInfo target = AccessTools.Method(compType, "CompPostTick");
        MethodInfo prefix = AccessTools.Method(typeof(KiiroStealthMapTickCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    // Kiiro.HediffComp_Stealth.CompPostTick dereferences Pawn.Map.glowGrid every
    // 90-tick hash interval without checking whether the pawn is spawned on a
    // map. For pawns in a caravan, shuttle, or other mapless state the Map is
    // null, so every tick throws a NullReferenceException and vanilla removes
    // the hediff entirely (stealth permanently lost). Skip the tick while the
    // pawn has no map; ground glow is meaningless off-map anyway.
    public static bool Prefix(HediffComp __instance)
    {
        Pawn pawn = __instance?.parent?.pawn;
        if (pawn == null || pawn.Map == null)
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] Skipped Kiiro stealth hediff tick for a pawn without a map; the original method would dereference a null Map and make vanilla remove the hediff.", OffMapStealthWarningId);
            return false;
        }

        return true;
    }
}

internal static class AlienRaceNullPawnRulesCompatibility
{
    public const string PackageId = "erdelf.HumanoidAlienRaces";
    private const int NullPawnRulesWarningId = 82038131;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type patchesType = AccessTools.TypeByName("AlienRace.HarmonyPatches");
        MethodInfo target = AccessTools.Method(patchesType, "RulesForPawnPostfix");
        MethodInfo prefix = AccessTools.Method(typeof(AlienRaceNullPawnRulesCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    // AlienRace.HarmonyPatches.RulesForPawnPostfix unconditionally reads
    // pawn.def.LabelCap, but vanilla GrammarUtility.RulesForPawn is also called
    // with a null pawn (e.g. Kiiro Story's wanderer-join quest letter when its
    // generated pawn is missing). The postfix then throws a
    // NullReferenceException that aborts the whole quest generation. Vanilla
    // handles null pawns fine, so just skip the postfix in that case.
    public static bool Prefix(Pawn pawn)
    {
        if (pawn == null)
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] Skipped AlienRace RulesForPawn postfix for a null pawn; the original postfix would throw a NullReferenceException and abort the calling quest/text generation.", NullPawnRulesWarningId);
            return false;
        }

        return true;
    }
}

internal static class MinifyEverythingReinstallReservationCompatibility
{
    public const string PackageId = "erdelf.MinifyEverything";
    private const int InUseReinstallWarningId = 82038132;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        MethodInfo target = AccessTools.Method(typeof(WorkGiver_ConstructDeliverResources), "InstallJob");
        MethodInfo prefix = AccessTools.Method(typeof(MinifyEverythingReinstallReservationCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    // MinifyEverything's InstallJobTranspiler rewrites the maxPawns literal in
    // WorkGiver_ConstructDeliverResources.InstallJob's reservation check from
    // 1 to 2. Vanilla reservation rules only allow stacking reservations whose
    // maxPawns match, so the relaxed check also passes for buildings that are
    // still in use (e.g. a bed reserved for LayDown with maxPawns 2). The
    // issued HaulToContainer job then fails TryMakePreToilReservations with
    // maxPawns 1, spamming a red "Could not reserve" error every retry.
    // Restore the vanilla maxPawns-1 validation before the job is issued;
    // the job simply waits until the building is no longer occupied.
    public static bool Prefix(Pawn pawn, Blueprint_Install install, ref Job __result)
    {
        Thing thingToInstall = install?.MiniToInstallOrBuildingToReinstall;
        if (pawn?.Map == null || thingToInstall == null)
        {
            return true;
        }

        if (pawn.CanReserve(thingToInstall, 1, -1, null, false))
        {
            return true;
        }

        Pawn reserver = pawn.Map.reservationManager.FirstRespectedReserver(thingToInstall, pawn);
        if (reserver != null)
        {
            JobFailReason.Is("ReservedBy".Translate(reserver.LabelShort, reserver));
        }

        Log.WarningOnce("[CommonModCompatibilityPatches] Blocked a MinifyEverything reinstall haul job for a building that is still reserved with a different maxPawns (e.g. an occupied bed); the original relaxed check would issue a job that fails every StartJob with a red reservation error.", InUseReinstallWarningId);
        __result = null;
        return false;
    }
}

internal static class LifeStageMinAgeFallbackCompatibility
{
    private const int MissingLifeStageWarningId = 82038133;

    public static bool TryApply(Harmony harmony)
    {
        MethodInfo target = AccessTools.Method(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.LifeStageMinAge));
        MethodInfo prefix = AccessTools.Method(typeof(LifeStageMinAgeFallbackCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    // Vanilla LifeStageMinAge logs a red error and returns 0 when the queried
    // life stage def is absent from the pawn's race. Alien races (e.g. Kiiro)
    // define their own life stage defs, so vanilla callers querying
    // HumanlikeChild/HumanlikeAdult (the ideo certainty age curve) and mods
    // doing the same (1trickPwnyta's Defaults birthday policy check) spam a
    // red error for every alien pawn. Return the exact same 0f fallback
    // vanilla produces after logging, just without the error, so every caller
    // behaves bit-for-bit as it did unpatched.
    public static bool Prefix(Pawn ___pawn, LifeStageDef lifeStage, ref float __result)
    {
        List<LifeStageAge> lifeStageAges = ___pawn?.RaceProps?.lifeStageAges;
        if (lifeStageAges == null)
        {
            return true;
        }

        for (int i = 0; i < lifeStageAges.Count; i++)
        {
            if (lifeStageAges[i].def == lifeStage)
            {
                return true;
            }
        }

        Log.WarningOnce("[CommonModCompatibilityPatches] Silenced vanilla LifeStageMinAge error for a life stage def missing from a pawn's race (e.g. HumanlikeChild on an alien race); returning the same 0 fallback vanilla would produce after logging.", MissingLifeStageWarningId);
        __result = 0f;
        return false;
    }
}

internal static class QualityBuilderNullMapCompatibility
{
    private const int NullMapWarningId = 82038135;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.AnyActive("hatti.qualitybuilder.fork", "hatti.qualitybuilder"))
        {
            return false;
        }

        Type qualityBuilderType = AccessTools.TypeByName("QualityBuilder.QualityBuilder");
        MethodInfo target = AccessTools.Method(qualityBuilderType, "GetFirstBuildingBuildingOrFrame", new[] { typeof(Map), typeof(IntVec3) });
        MethodInfo prefix = AccessTools.Method(typeof(QualityBuilderNullMapCompatibility), nameof(Prefix));
        if (target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    // QualityBuilder captures the target frame's Map when the finish-frame
    // toils are created and later dereferences it in
    // GetFirstBuildingBuildingOrFrame without a null check. When the frame is
    // despawned, carried, or otherwise mapless at that point (observed with
    // chess table frames, likely via Replace Stuff interactions), the finish
    // action throws a NullReferenceException and breaks the job. Return no
    // building instead; the caller already handles that gracefully.
    public static bool Prefix(Map map, ref Thing __result)
    {
        if (map == null)
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] Skipped QualityBuilder building/frame lookup with a null map; the original method would throw a NullReferenceException inside the finish-frame job cleanup.", NullMapWarningId);
            __result = null;
            return false;
        }

        return true;
    }
}

internal static class VbeVideoPathEscapingCompatibility
{
    public const string PackageId = "vanillaexpanded.backgrounds";

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type defType = AccessTools.TypeByName("VBE.BackgroundImageDef");
        MethodInfo target = AccessTools.PropertyGetter(defType, "Video");
        MethodInfo postfix = AccessTools.Method(typeof(VbeVideoPathEscapingCompatibility), nameof(Postfix));
        if (defType == null || target == null || postfix == null)
        {
            return false;
        }

        harmony.Patch(target, postfix: new HarmonyMethod(postfix));
        return true;
    }

    // VBE assigns BackgroundImageDef.Video directly to UnityEngine.Video.VideoPlayer.url.
    // User-provided animated backgrounds live under the RimWorld config folder, whose
    // path contains spaces (".../RimWorld by Ludeon Studios/Config/..."). VideoPlayer
    // parses url as a URI, and an unescaped absolute path with spaces never finishes
    // Prepare() on Linux: no errorReceived, no frames, no preview, only a blank
    // background slot. Convert rooted filesystem paths to proper file:// URIs so
    // spaces and other reserved characters are percent-encoded before VBE uses them.
    public static void Postfix(ref string __result)
    {
        if (string.IsNullOrEmpty(__result) || __result.Contains("://"))
        {
            return;
        }

        if (!Path.IsPathRooted(__result))
        {
            return;
        }

        if (Uri.TryCreate(__result, UriKind.Absolute, out Uri uri) && uri.IsFile)
        {
            __result = uri.AbsoluteUri;
        }
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

internal static class AllowToolPartyHuntWorldSettingsCompatibility
{
    public const string PackageId = "UnlimitedHugs.AllowTool";
    private const int WorldSettingsNotReadyWarningId = 82038140;
    private static PropertyInfo controllerInstanceProperty;
    private static PropertyInfo worldSettingsProperty;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type patchType = AccessTools.TypeByName("AllowTool.Patches.JobDriverWait_CheckForAutoAttack_Patch");
        Type controllerType = AccessTools.TypeByName("AllowTool.AllowToolController");
        MethodInfo target = AccessTools.Method(patchType, "DoPartyHunting");
        MethodInfo prefix = AccessTools.Method(typeof(AllowToolPartyHuntWorldSettingsCompatibility), nameof(Prefix));
        if (patchType == null || controllerType == null || target == null || prefix == null)
        {
            return false;
        }

        controllerInstanceProperty = AccessTools.Property(controllerType, "Instance");
        worldSettingsProperty = AccessTools.Property(controllerType, "WorldSettings");
        if (controllerInstanceProperty == null || worldSettingsProperty == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    // AllowToolController.WorldSettings is only assigned in HugsLib's
    // WorldLoaded() callback. While a save is still loading, Map.FinalizeInit
    // resumes pawn jobs; a drafted pawn whose pathing failed gets a fresh
    // JobDriver_Wait (Wait_Combat), whose CheckForAutoAttack runs AllowTool's
    // DoPartyHunting postfix. PartyHuntHandler.WorldSettings then dereferences
    // the still-null WorldSettings and throws a NullReferenceException; the
    // vanilla error-recover job hits the same postfix again, so the game
    // gives up and leaves the pawn jobless. Skip the postfix until the world
    // settings exist; party hunting is meaningless mid-load anyway.
    public static bool Prefix()
    {
        object controller = controllerInstanceProperty.GetValue(null, null);
        if (controller != null && worldSettingsProperty.GetValue(controller, null) != null)
        {
            return true;
        }

        Log.WarningOnce("[CommonModCompatibilityPatches] Skipped AllowTool party-hunt behavior because AllowToolController.WorldSettings is not initialized yet (HugsLib WorldLoaded has not run during save loading); the original postfix would throw a NullReferenceException inside JobDriver_Wait and leave the pawn jobless.", WorldSettingsNotReadyWarningId);
        return false;
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

internal static class GoodwillSituationManagerThreadSafetyCompatibility
{
    public const string VanillaExpandedFrameworkPackageId = "OskarPotocki.VanillaFactionsExpanded.Core";
    private static FieldInfo cachedDataField;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(VanillaExpandedFrameworkPackageId))
        {
            return false;
        }

        MethodInfo getSituationsTarget = AccessTools.Method(typeof(GoodwillSituationManager), nameof(GoodwillSituationManager.GetSituations), new[] { typeof(Faction) });
        MethodInfo recalculateAllTarget = AccessTools.Method(typeof(GoodwillSituationManager), nameof(GoodwillSituationManager.RecalculateAll), new[] { typeof(bool) });
        MethodInfo getSituationsPrefix = AccessTools.Method(typeof(GoodwillSituationManagerThreadSafetyCompatibility), nameof(GetSituationsPrefix));
        MethodInfo recalculateAllPrefix = AccessTools.Method(typeof(GoodwillSituationManagerThreadSafetyCompatibility), nameof(RecalculateAllPrefix));
        cachedDataField = AccessTools.Field(typeof(GoodwillSituationManager), "cachedData");

        if (getSituationsTarget == null || recalculateAllTarget == null || getSituationsPrefix == null || recalculateAllPrefix == null || cachedDataField == null)
        {
            return false;
        }

        harmony.Patch(getSituationsTarget, prefix: new HarmonyMethod(getSituationsPrefix));
        harmony.Patch(recalculateAllTarget, prefix: new HarmonyMethod(recalculateAllPrefix));
        return true;
    }

    public static bool GetSituationsPrefix(GoodwillSituationManager __instance, Faction other, ref List<GoodwillSituationManager.CachedSituation> __result)
    {
        if (other == null || other.IsPlayer)
        {
            Log.Error("Called GetSituations() for faction " + other);
            __result = null;
            return false;
        }

        lock (__instance)
        {
            Dictionary<Faction, List<GoodwillSituationManager.CachedSituation>> cache = GetOrResetCache(__instance);
            try
            {
                if (cache.TryGetValue(other, out List<GoodwillSituationManager.CachedSituation> cachedSituations))
                {
                    __result = cachedSituations;
                    return false;
                }
            }
            catch (InvalidOperationException)
            {
                cache = ResetCache(__instance);
                Log.Warning("[CommonModCompatibilityPatches] Reset corrupted GoodwillSituationManager cache after concurrent access was detected.");
            }

            List<GoodwillSituationManager.CachedSituation> situations = BuildSituations(other);
            cache[other] = situations;
            NotifyHostilityChanged(other, canSendHostilityChangedLetter: true);
            __result = situations;
            return false;
        }
    }

    public static bool RecalculateAllPrefix(GoodwillSituationManager __instance, bool canSendHostilityChangedLetter)
    {
        lock (__instance)
        {
            Dictionary<Faction, List<GoodwillSituationManager.CachedSituation>> cache = ResetCache(__instance);
            List<Faction> allFactions = Find.FactionManager?.AllFactionsListForReading;
            if (allFactions == null)
            {
                return false;
            }

            for (int i = 0; i < allFactions.Count; i++)
            {
                Faction faction = allFactions[i];
                if (faction != null && faction != Faction.OfPlayer && faction.HasGoodwill)
                {
                    cache[faction] = BuildSituations(faction);
                    NotifyHostilityChanged(faction, canSendHostilityChangedLetter);
                }
            }
        }

        return false;
    }

    private static Dictionary<Faction, List<GoodwillSituationManager.CachedSituation>> GetOrResetCache(GoodwillSituationManager manager)
    {
        Dictionary<Faction, List<GoodwillSituationManager.CachedSituation>> cache = cachedDataField.GetValue(manager) as Dictionary<Faction, List<GoodwillSituationManager.CachedSituation>>;
        return cache ?? ResetCache(manager);
    }

    private static Dictionary<Faction, List<GoodwillSituationManager.CachedSituation>> ResetCache(GoodwillSituationManager manager)
    {
        Dictionary<Faction, List<GoodwillSituationManager.CachedSituation>> cache = new Dictionary<Faction, List<GoodwillSituationManager.CachedSituation>>();
        cachedDataField.SetValue(manager, cache);
        return cache;
    }

    private static List<GoodwillSituationManager.CachedSituation> BuildSituations(Faction other)
    {
        List<GoodwillSituationManager.CachedSituation> situations = new List<GoodwillSituationManager.CachedSituation>();
        if (other == null || !other.HasGoodwill)
        {
            return situations;
        }

        List<GoodwillSituationDef> defs = DefDatabase<GoodwillSituationDef>.AllDefsListForReading;
        for (int i = 0; i < defs.Count; i++)
        {
            GoodwillSituationDef def = defs[i];
            int maxGoodwill = def.Worker.GetMaxGoodwill(other);
            int naturalGoodwillOffset = def.Worker.GetNaturalGoodwillOffset(other);
            if (maxGoodwill < 100 || naturalGoodwillOffset != 0)
            {
                situations.Add(new GoodwillSituationManager.CachedSituation
                {
                    def = def,
                    maxGoodwill = maxGoodwill,
                    naturalGoodwillOffset = naturalGoodwillOffset
                });
            }
        }

        return situations;
    }

    private static void NotifyHostilityChanged(Faction other, bool canSendHostilityChangedLetter)
    {
        if (Current.ProgramState != ProgramState.Entry && other?.HasGoodwill == true && Faction.OfPlayer != null)
        {
            Faction.OfPlayer.Notify_GoodwillSituationsChanged(other, canSendHostilityChangedLetter, null, null);
        }
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

internal static class RimStoryMassFuneralCompatibility
{
    public const string RimStoryPackageId = "Mlie.RimStory";
    private const int InvalidMassFuneralWarningId = 82038125;
    private static FieldInfo lastGraveField;
    private static FieldInfo buriedPawnsField;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(RimStoryPackageId))
        {
            return false;
        }

        Type massFuneralType = AccessTools.TypeByName("RimStory.MassFuneral");
        Type resourcesType = AccessTools.TypeByName("RimStory.Resources");
        if (massFuneralType == null || resourcesType == null)
        {
            return false;
        }

        MethodInfo target = AccessTools.Method(massFuneralType, "TryStartMassFuneral", new[] { typeof(Map) });
        MethodInfo prefix = AccessTools.Method(typeof(RimStoryMassFuneralCompatibility), nameof(Prefix));
        lastGraveField = AccessTools.Field(resourcesType, "lastGrave");
        buriedPawnsField = AccessTools.Field(resourcesType, "deadPawnsForMassFuneralBuried");
        if (target == null || prefix == null || lastGraveField == null || buriedPawnsField == null || !lastGraveField.IsStatic || !buriedPawnsField.IsStatic)
        {
            return false;
        }

        EnsureBuriedPawnsList();
        harmony.Patch(target, prefix: new HarmonyMethod(prefix));

        Type savesType = AccessTools.TypeByName("RimStory.Saves");
        MethodInfo exposeDataTarget = AccessTools.Method(savesType, "ExposeData", Type.EmptyTypes);
        MethodInfo exposeDataPostfix = AccessTools.Method(typeof(RimStoryMassFuneralCompatibility), nameof(ExposeDataPostfix));
        if (exposeDataTarget != null && exposeDataPostfix != null)
        {
            harmony.Patch(exposeDataTarget, postfix: new HarmonyMethod(exposeDataPostfix));
        }

        return true;
    }

    public static bool Prefix(Map map, ref bool __result)
    {
        MassFuneralContext context = GetContext(map);
        if (context == MassFuneralContext.Valid)
        {
            return true;
        }

        __result = false;
        if (context == MassFuneralContext.ForeignMap)
        {
            return false;
        }

        ClearBuriedPawns();
        Log.WarningOnce("[CommonModCompatibilityPatches] Skipped RimStory mass funeral because its saved last grave or buried-pawn list was invalid; discarded the stale mass-funeral queue to prevent repeated errors.", InvalidMassFuneralWarningId);
        return false;
    }

    public static void ExposeDataPostfix()
    {
        EnsureBuriedPawnsList();
    }

    private enum MassFuneralContext
    {
        Invalid,
        ForeignMap,
        Valid
    }

    private static void EnsureBuriedPawnsList()
    {
        if (buriedPawnsField == null)
        {
            return;
        }

        try
        {
            if (buriedPawnsField.GetValue(null) == null)
            {
                buriedPawnsField.SetValue(null, Activator.CreateInstance(buriedPawnsField.FieldType));
            }
        }
        catch
        {
            // Keep the optional compatibility patch quiet if a future RimStory version changes this field type.
        }
    }

    private static MassFuneralContext GetContext(Map map)
    {
        if (map == null || lastGraveField == null || buriedPawnsField == null)
        {
            return MassFuneralContext.Invalid;
        }

        Building_Grave lastGrave;
        IList buriedPawns;
        try
        {
            lastGrave = lastGraveField.GetValue(null) as Building_Grave;
            buriedPawns = buriedPawnsField.GetValue(null) as IList;
        }
        catch
        {
            return MassFuneralContext.Invalid;
        }

        if (lastGrave == null || lastGrave.Destroyed || !lastGrave.Spawned || !lastGrave.Position.IsValid || buriedPawns == null)
        {
            return MassFuneralContext.Invalid;
        }

        Map graveMap = lastGrave.Map;
        if (graveMap == null)
        {
            return MassFuneralContext.Invalid;
        }

        if (graveMap != map)
        {
            return MassFuneralContext.ForeignMap;
        }

        return HasValidBuriedPawn(buriedPawns) ? MassFuneralContext.Valid : MassFuneralContext.Invalid;
    }

    private static bool HasValidBuriedPawn(IList buriedPawns)
    {
        for (int i = 0; i < buriedPawns.Count; i++)
        {
            if (buriedPawns[i] is Pawn pawn && !pawn.DestroyedOrNull())
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearBuriedPawns()
    {
        if (buriedPawnsField?.GetValue(null) is IList buriedPawns)
        {
            buriedPawns.Clear();
        }
    }
}

internal static class ShipJobWaitDespawnedThingsCompatibility
{
    public static bool TryApply(Harmony harmony)
    {
        MethodInfo target = AccessTools.Method(typeof(ShipJob_Wait), nameof(ShipJob_Wait.TickInterval));
        MethodInfo transpiler = AccessTools.Method(typeof(ShipJobWaitDespawnedThingsCompatibility), nameof(TickIntervalTranspiler));
        if (target == null || transpiler == null)
        {
            return false;
        }

        harmony.Patch(target, transpiler: new HarmonyMethod(transpiler));
        return true;
    }

    // Vanilla ShipJob_Wait.TickInterval evaluates
    // "item.MapHeld.PocketMapParent.sourceMap == transportShip.shipThing.Map"
    // for every sendAwayIfAllDespawned entry without null checks. A pawn who
    // left the map (e.g. a healed ShuttleCrash_Rescue guard who walked off
    // instead of boarding) has MapHeld == null, and a pawn on a regular map
    // has PocketMapParent == null, so the check throws a NullReferenceException
    // every 60 ticks, aborting the rest of the game tick while the shuttle
    // stays grounded. Route both dereferences through null-safe helpers so such
    // an entry simply counts as "not on this map", matching vanilla intent.
    public static IEnumerable<CodeInstruction> TickIntervalTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo pocketMapParentGetter = AccessTools.PropertyGetter(typeof(Map), nameof(Map.PocketMapParent));
        FieldInfo sourceMapField = AccessTools.Field(typeof(PocketMapParent), nameof(PocketMapParent.sourceMap));
        MethodInfo safePocketMapParent = AccessTools.Method(typeof(ShipJobWaitDespawnedThingsCompatibility), nameof(SafePocketMapParent));
        MethodInfo safeSourceMap = AccessTools.Method(typeof(ShipJobWaitDespawnedThingsCompatibility), nameof(SafeSourceMap));

        int replacements = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (pocketMapParentGetter != null && safePocketMapParent != null && instruction.Calls(pocketMapParentGetter))
            {
                replacements++;
                yield return new CodeInstruction(OpCodes.Call, safePocketMapParent);
            }
            else if (sourceMapField != null && safeSourceMap != null && instruction.LoadsField(sourceMapField))
            {
                replacements++;
                yield return new CodeInstruction(OpCodes.Call, safeSourceMap);
            }
            else
            {
                yield return instruction;
            }
        }

        if (replacements != 2)
        {
            Log.Warning($"[CommonModCompatibilityPatches] ShipJob_Wait.TickInterval transpiler made {replacements} null-safety replacement(s), expected 2; vanilla code may have changed.");
        }
    }

    private static PocketMapParent SafePocketMapParent(Map map)
    {
        return map?.PocketMapParent;
    }

    private static Map SafeSourceMap(PocketMapParent pocketMapParent)
    {
        return pocketMapParent?.sourceMap;
    }
}

internal static class InventoryTabStableSortCompatibility
{
    public const string PackageId = "Kota.InventoryTab";
    private static PropertyInfo thingInSlotProperty;
    private static FieldInfo thingIdField;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type slotType = AccessTools.TypeByName("InventoryTab.Slot");
        MethodInfo target = slotType == null ? null : AccessTools.Method(slotType, "CompareTo", new[] { slotType });
        MethodInfo prefix = AccessTools.Method(typeof(InventoryTabStableSortCompatibility), nameof(Prefix));
        if (slotType == null || target == null || prefix == null)
        {
            return false;
        }

        thingInSlotProperty = AccessTools.Property(slotType, "thingInSlot");
        thingIdField = AccessTools.Field(slotType, "thingId");
        if (thingInSlotProperty == null || thingIdField == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    // InventoryTab sorts its item rows with the unstable List<Slot>.Sort() and a
    // Slot.CompareTo that only compares MarketValue: its name tie-break is
    // commented out, so equal-value slots compare as 0. The sorted list is
    // rebuilt every auto-update interval from map.listerThings.AllThings (whose
    // order changes constantly as haulers pick up/merge stacks) via a
    // Dictionary dump, so the input order to the unstable sort keeps changing
    // and equal-value rows visibly swap places on every refresh ("flickering").
    // Replace the comparator with a fully deterministic one: market value
    // descending (same as the original), then the item label, then the unique
    // grouping key, so the output order no longer depends on the input order.
    public static bool Prefix(object __instance, object other, ref int __result)
    {
        __result = CompareSlots(__instance, other);
        return false;
    }

    private static int CompareSlots(object slotA, object slotB)
    {
        Thing thingA = thingInSlotProperty?.GetValue(slotA, null) as Thing;
        Thing thingB = thingInSlotProperty?.GetValue(slotB, null) as Thing;

        float valueA = thingA?.MarketValue ?? 0f;
        float valueB = thingB?.MarketValue ?? 0f;
        int valueComparison = valueB.CompareTo(valueA);
        if (valueComparison != 0)
        {
            return valueComparison;
        }

        int labelComparison = string.Compare(thingA?.LabelNoCount, thingB?.LabelNoCount, StringComparison.CurrentCulture);
        if (labelComparison != 0)
        {
            return labelComparison;
        }

        string idA = thingIdField?.GetValue(slotA) as string;
        string idB = thingIdField?.GetValue(slotB) as string;
        return string.Compare(idA, idB, StringComparison.Ordinal);
    }
}

internal static class GD3BlackShieldMaplessRemovalCompatibility
{
    public const string PackageId = "fxz.glitterworlddestroyer.mk5";
    private const int MaplessShieldWarningId = 82038134;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type compType = AccessTools.TypeByName("GD3.HediffComp_BlackShield");
        MethodInfo target = AccessTools.Method(compType, "CompPostPostRemoved", Type.EmptyTypes);
        MethodInfo prefix = AccessTools.Method(typeof(GD3BlackShieldMaplessRemovalCompatibility), nameof(Prefix));
        if (compType == null || target == null || prefix == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    // GD3.HediffComp_BlackShield.CompPostPostRemoved unconditionally plays
    // shield-break effects and queries p.Map.mapPawns, but the hediff can be
    // removed while the pawn has no map (observed on a PawnFlyer mid-flight
    // during its HealthTick). p.Map is then null and FleckMaker.Static throws
    // a NullReferenceException every tick. The shield-break visuals and AoE
    // damage are meaningless off-map, so skip the whole effect while mapless.
    public static bool Prefix(HediffComp __instance)
    {
        Pawn pawn = __instance?.parent?.pawn;
        if (pawn == null || pawn.Map == null)
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] Skipped GD3 BlackShield break effect for a mapless pawn (e.g. a PawnFlyer mid-flight); the original method would dereference a null Map and throw a NullReferenceException every tick.", MaplessShieldWarningId);
            return false;
        }

        return true;
    }
}

internal static class PawnBodySilhouetteDrawCompatibility
{
    private const int UnsafeSilhouetteWarningId = 82038136;
    private const int SuppressedSilhouetteExceptionWarningId = 82038137;
    private static FieldInfo lastPawnField;

    public static bool TryApply(Harmony harmony)
    {
        MethodInfo target = AccessTools.Method(typeof(Graphic_PawnBodySilhouette), "DrawWorker");
        MethodInfo prefix = AccessTools.Method(typeof(PawnBodySilhouetteDrawCompatibility), nameof(Prefix));
        MethodInfo finalizer = AccessTools.Method(typeof(PawnBodySilhouetteDrawCompatibility), nameof(Finalizer));
        if (target == null || prefix == null || finalizer == null)
        {
            return false;
        }

        lastPawnField = AccessTools.Field(typeof(Graphic_PawnBodySilhouette), "lastPawn");
        if (lastPawnField == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix), finalizer: new HarmonyMethod(finalizer));
        return true;
    }

    // Graphic_PawnBodySilhouette.DrawWorker (used by motes such as
    // Mote_MechResurrectWarmupOnTarget) keeps drawing for its linked pawn even
    // after that pawn was destroyed and discarded (e.g. the resurrection
    // finishes and the corpse's inner pawn is discarded while the mote lives
    // on). A discarded pawn has ParentHolder == null, so PawnRenderer.BodyAngle
    // dereferences pawn.ParentHolder.ParentHolder and throws a
    // NullReferenceException every frame the mote is on screen. Skip drawing
    // for destroyed/discarded pawns; a finalizer suppresses any residual
    // null-shape exception as a safety net.
    public static bool Prefix(Graphic_PawnBodySilhouette __instance, Thing thing)
    {
        Pawn pawn = ResolveLinkedPawn(__instance, thing as Mote);
        if (pawn != null && (pawn.Destroyed || pawn.Discarded))
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] Skipped pawn-body silhouette mote drawing for a destroyed/discarded linked pawn; the original DrawWorker would throw a NullReferenceException in PawnRenderer.BodyAngle every frame.", UnsafeSilhouetteWarningId);
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

        if (__exception is NullReferenceException || __exception is ArgumentException)
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] Suppressed pawn-body silhouette DrawWorker exception: " + __exception.GetType().Name + ": " + __exception.Message, SuppressedSilhouetteExceptionWarningId);
            return null;
        }

        return __exception;
    }

    private static Pawn ResolveLinkedPawn(Graphic_PawnBodySilhouette instance, Mote mote)
    {
        if (mote == null)
        {
            return null;
        }

        Thing linked = mote.link1.Target.Thing;
        Pawn pawn = (linked as Corpse)?.InnerPawn ?? linked as Pawn;
        if (pawn == null)
        {
            pawn = lastPawnField?.GetValue(instance) as Pawn;
        }

        return pawn;
    }
}

internal static class TacticalGroupsColonyGroupIdCollisionCompatibility
{
    public const string PackageId = "DerekBickley.LTOColonyGroupsFinal";
    private const int ReassignedGroupIdWarningId = 82038138;
    private static FieldInfo groupIDField;
    private static FieldInfo defaultGroupNameField;
    private static FieldInfo curGroupNameField;
    private static PropertyInfo tacticalGroupsProperty;
    private static FieldInfo colonyGroupsField;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type colonyGroupType = AccessTools.TypeByName("TacticalGroups.ColonyGroup");
        Type colonistGroupType = AccessTools.TypeByName("TacticalGroups.ColonistGroup");
        Type tacticUtilsType = AccessTools.TypeByName("TacticalGroups.TacticUtils");
        Type tacticalGroupsType = AccessTools.TypeByName("TacticalGroups.TacticalGroups");
        MethodInfo postfix = AccessTools.Method(typeof(TacticalGroupsColonyGroupIdCollisionCompatibility), nameof(Postfix));
        if (colonyGroupType == null || colonistGroupType == null || tacticUtilsType == null || tacticalGroupsType == null || postfix == null)
        {
            return false;
        }

        groupIDField = AccessTools.Field(colonistGroupType, "groupID");
        defaultGroupNameField = AccessTools.Field(colonistGroupType, "defaultGroupName");
        curGroupNameField = AccessTools.Field(colonistGroupType, "curGroupName");
        tacticalGroupsProperty = AccessTools.Property(tacticUtilsType, "TacticalGroups");
        colonyGroupsField = AccessTools.Field(tacticalGroupsType, "colonyGroups");
        if (groupIDField == null || tacticalGroupsProperty == null || colonyGroupsField == null)
        {
            return false;
        }

        ConstructorInfo listCtor = AccessTools.Constructor(colonyGroupType, new[] { typeof(List<Pawn>) });
        ConstructorInfo pawnCtor = AccessTools.Constructor(colonyGroupType, new[] { typeof(Pawn) });
        MethodInfo convertToTaskForce = AccessTools.Method(colonyGroupType, "ConvertToTaskForce");
        MethodInfo convertToColonyGroup = AccessTools.Method(colonyGroupType, "ConvertToColonyGroup");
        MethodInfo exposeData = AccessTools.Method(colonyGroupType, "ExposeData");
        if (listCtor == null || pawnCtor == null || convertToTaskForce == null || convertToColonyGroup == null || exposeData == null)
        {
            return false;
        }

        HarmonyMethod harmonyPostfix = new HarmonyMethod(postfix);
        harmony.Patch(listCtor, postfix: harmonyPostfix);
        harmony.Patch(pawnCtor, postfix: harmonyPostfix);
        harmony.Patch(convertToTaskForce, postfix: harmonyPostfix);
        harmony.Patch(convertToColonyGroup, postfix: harmonyPostfix);
        harmony.Patch(exposeData, postfix: harmonyPostfix);
        return true;
    }

    // TacticalGroups assigns ColonyGroup.groupID from per-category counts:
    // constructors use colonyGroups.Count + 1 or the isColonyGroup count + 1,
    // while ConvertToTaskForce/ConvertToColonyGroup use the isTaskForce /
    // isColonyGroup count + 1. The categories do not avoid each other, but
    // GetUniqueLoadID is just type name + groupID, so a colony group and a
    // task force can both end up as "TacticalGroups.ColonyGroup1"; the next
    // save/load then throws "An item with the same key has already been added"
    // while registering the second group in the loaded object directory.
    // After every ID (re)assignment, bump the ID until it is unique among
    // loaded groups of the same type; the ExposeData postfix additionally
    // repairs already-colliding saves at PostLoadInit time.
    public static void Postfix(object __instance)
    {
        if (__instance == null || Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.LoadingVars)
        {
            return;
        }

        try
        {
            EnsureUniqueGroupId(__instance);
        }
        catch
        {
            // Keep the compatibility patch quiet if a future TacticalGroups version changes these members.
        }
    }

    private static void EnsureUniqueGroupId(object group)
    {
        object manager = tacticalGroupsProperty.GetValue(null, null);
        if (manager == null || colonyGroupsField.GetValue(manager) is not IDictionary colonyGroups)
        {
            return;
        }

        Type type = group.GetType();
        int id = (int)groupIDField.GetValue(group);
        if (!HasCollision(colonyGroups, group, type, id))
        {
            return;
        }

        int newId = id;
        do
        {
            newId++;
        }
        while (HasCollision(colonyGroups, group, type, newId));

        groupIDField.SetValue(group, newId);
        string defaultName = defaultGroupNameField?.GetValue(group) as string;
        if (defaultName != null && curGroupNameField?.GetValue(group) as string == defaultName + " " + id)
        {
            curGroupNameField.SetValue(group, defaultName + " " + newId);
        }

        Log.WarningOnce($"[CommonModCompatibilityPatches] Reassigned a colliding TacticalGroups ColonyGroup id {id} to {newId}; the mod's per-category id counting lets a colony group and a task force share a load ID, which otherwise throws a duplicate-key exception on every save load.", ReassignedGroupIdWarningId);
    }

    private static bool HasCollision(IDictionary colonyGroups, object group, Type type, int id)
    {
        foreach (object value in colonyGroups.Values)
        {
            if (value == null || ReferenceEquals(value, group) || value.GetType() != type)
            {
                continue;
            }

            if ((int)groupIDField.GetValue(value) == id)
            {
                return true;
            }
        }

        return false;
    }
}

internal static class TacticalGroupsBillPawnRestrictionCompatibility
{
    public const string PackageId = "DerekBickley.LTOColonyGroupsFinal";
    private const int UnsafeGroupPawnWarningId = 82038139;
    private static PropertyInfo allPawnGroupsProperty;
    private static PropertyInfo billsSelectedGroupProperty;
    private static FieldInfo pawnsField;
    private static FieldInfo curGroupNameField;

    public static bool TryApply(Harmony harmony)
    {
        if (!ModDetection.IsActive(PackageId))
        {
            return false;
        }

        Type patchesType = AccessTools.TypeByName("TacticalGroups.HarmonyPatches_GroupBills");
        Type tacticUtilsType = AccessTools.TypeByName("TacticalGroups.TacticUtils");
        Type pawnGroupType = AccessTools.TypeByName("TacticalGroups.PawnGroup");
        MethodInfo target = AccessTools.Method(patchesType, "GeneratePawnRestrictionOptions");
        MethodInfo prefix = AccessTools.Method(typeof(TacticalGroupsBillPawnRestrictionCompatibility), nameof(Prefix));
        if (patchesType == null || tacticUtilsType == null || pawnGroupType == null || target == null || prefix == null)
        {
            return false;
        }

        allPawnGroupsProperty = AccessTools.Property(tacticUtilsType, "AllPawnGroups");
        billsSelectedGroupProperty = AccessTools.Property(patchesType, "BillsSelectedGroup");
        pawnsField = AccessTools.Field(pawnGroupType, "pawns");
        curGroupNameField = AccessTools.Field(pawnGroupType, "curGroupName");
        if (allPawnGroupsProperty == null || billsSelectedGroupProperty == null || pawnsField == null || curGroupNameField == null)
        {
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        return true;
    }

    // HarmonyPatches_GroupBills.GeneratePawnRestrictionOptions calls
    // pawn.skills.GetSkill(...) for every pawn of every pawn group without
    // null checks. Vanilla only creates the skills tracker for ToolUser
    // pawns, so a group containing an animal or mechanoid (or a stale null
    // pawn entry) throws a NullReferenceException every time
    // Dialog_BillConfig renders its worker dropdown. Reimplement the postfix
    // with identical behavior but skip the unsafe entries, so valid groups
    // keep their dropdown options.
    //
    // The target's own parameters are named ___bill/__result (it is itself a
    // postfix on Dialog_BillConfig). We cannot declare a ___bill parameter
    // here: for Harmony, a triple-underscore prefix means "inject this field
    // of the patched type", and HarmonyPatches_GroupBills has no bill field,
    // so patch application throws ArgumentException. Use __args instead; its
    // writes are copied back into the target's ref parameter.
    public static bool Prefix(object[] __args)
    {
        Bill_Production bill = __args[0] as Bill_Production;
        IEnumerable<Widgets.DropdownMenuElement<Pawn>> result = __args[1] as IEnumerable<Widgets.DropdownMenuElement<Pawn>>;
        if (bill == null || result == null)
        {
            return true;
        }

        List<Widgets.DropdownMenuElement<Pawn>> options = result.ToList();
        bool skippedUnsafePawn = false;
        if (allPawnGroupsProperty.GetValue(null, null) is IEnumerable pawnGroups)
        {
            foreach (object pawnGroup in pawnGroups)
            {
                if (pawnGroup == null)
                {
                    skippedUnsafePawn = true;
                    continue;
                }
                if (pawnsField.GetValue(pawnGroup) is not List<Pawn> pawns || pawns.Count == 0)
                {
                    continue;
                }

                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    bool allowed = true;
                    if (bill.recipe?.workSkill != null)
                    {
                        if (pawn?.skills == null)
                        {
                            skippedUnsafePawn = true;
                            continue;
                        }

                        int level = pawn.skills.GetSkill(bill.recipe.workSkill).Level;
                        allowed = level >= bill.allowedSkillRange.min && level <= bill.allowedSkillRange.max;
                    }

                    if (!allowed)
                    {
                        continue;
                    }

                    object capturedGroup = pawnGroup;
                    string groupName = curGroupNameField.GetValue(pawnGroup) as string;
                    Widgets.DropdownMenuElement<Pawn> item = new Widgets.DropdownMenuElement<Pawn>
                    {
                        option = new FloatMenuOption("TG.AnyPawnOfGroup".Translate(groupName ?? string.Empty), delegate
                        {
                            bill.SetAnyPawnRestriction();
                            SetBillSelectedGroup(bill, capturedGroup);
                        }, (MenuOptionPriority)4, null, null, 0f, null, null, true, 0),
                        payload = null
                    };
                    int insertIndex = ModsConfig.IdeologyActive ? 2 : 1;
                    options.Insert(Math.Min(insertIndex, options.Count), item);
                    break;
                }
            }
        }

        if (skippedUnsafePawn)
        {
            Log.WarningOnce("[CommonModCompatibilityPatches] Skipped null/unskilled pawn entries (e.g. animals or mechanoids) in [LTO] Colony Groups pawn groups while building the bill worker dropdown; the original postfix would throw a NullReferenceException every time Dialog_BillConfig renders.", UnsafeGroupPawnWarningId);
        }

        __args[1] = options;
        return false;
    }

    private static void SetBillSelectedGroup(Bill_Production bill, object pawnGroup)
    {
        if (billsSelectedGroupProperty.GetValue(null, null) is IDictionary billsSelectedGroup)
        {
            billsSelectedGroup[bill] = pawnGroup;
        }
    }
}

internal static class ListerHaulablesSharedScanCursorCompatibility
{
    // ListerHaulables keeps its periodic rescan cursors in static fields shared
    // by every map (groupCycleIndex / cellCycleIndices). ListerHaulablesTick runs
    // once per map per game tick, so with two or more maps ticking (second
    // settlement, Odyssey pocket map, quest site) the shared cursor advances
    // several steps per tick and each map only ever rescans the slot groups at
    // one residue class of groupCycleIndex % groups.Count. Slot groups on the
    // other residue classes are never rescanned at all: event-driven updates only
    // recalculate the storage whose own settings changed, so an item sitting in
    // a starved group that newly gained a better storage never re-enters the
    // haulables set and is never re-hauled (and AllowTool's Haul Urgently marks
    // on it get cleaned up because the item is not in the set). Swap a per-map
    // cursor state in before each tick and save it back afterwards, so every map
    // gets the vanilla single-map cursor sequence while the original method body
    // (and any other mod's patches on it) keeps running unchanged.
    private sealed class ScanCursorState
    {
        public int GroupCycleIndex;
        public readonly List<int> CellCycleIndices = new List<int>();
    }

    private static readonly ConditionalWeakTable<ListerHaulables, ScanCursorState> States = new ConditionalWeakTable<ListerHaulables, ScanCursorState>();

    public static bool TryApply(Harmony harmony)
    {
        FieldInfo groupCycleIndex = AccessTools.Field(typeof(ListerHaulables), "groupCycleIndex");
        FieldInfo cellCycleIndices = AccessTools.Field(typeof(ListerHaulables), "cellCycleIndices");
        MethodInfo target = AccessTools.Method(typeof(ListerHaulables), nameof(ListerHaulables.ListerHaulablesTick));
        MethodInfo prefix = AccessTools.Method(typeof(ListerHaulablesSharedScanCursorCompatibility), nameof(Prefix));
        MethodInfo postfix = AccessTools.Method(typeof(ListerHaulablesSharedScanCursorCompatibility), nameof(Postfix));
        if (groupCycleIndex == null || !groupCycleIndex.IsStatic || cellCycleIndices == null || !cellCycleIndices.IsStatic || target == null || prefix == null || postfix == null)
        {
            // Upstream made the cursors per-instance (bug fixed) or renamed them.
            return false;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
        return true;
    }

    public static void Prefix(ListerHaulables __instance)
    {
        ScanCursorState state = States.GetOrCreateValue(__instance);
        ListerHaulables.groupCycleIndex = state.GroupCycleIndex;
        CopyContents(ListerHaulables.cellCycleIndices, state.CellCycleIndices);
    }

    public static void Postfix(ListerHaulables __instance)
    {
        ScanCursorState state = States.GetOrCreateValue(__instance);
        state.GroupCycleIndex = ListerHaulables.groupCycleIndex;
        CopyContents(state.CellCycleIndices, ListerHaulables.cellCycleIndices);
    }

    // cellCycleIndices is static readonly, so swap the contents rather than the
    // reference; the lists are as long as the map's slot-group count, i.e. a
    // handful of ints per call.
    private static void CopyContents(List<int> target, List<int> source)
    {
        target.Clear();
        target.AddRange(source);
    }
}

