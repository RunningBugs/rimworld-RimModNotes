using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BetterOutfitStand;

public static class OutfitStandHaulGizmoUtility
{
    private static readonly Texture2D HaulToOutfitStandIcon = ContentFinder<Texture2D>.Get("UI/Commands/Haul", false) ?? TexCommand.ForbidOff;

    public static bool ShouldOfferFor(Thing thing)
    {
        if (thing == null || !thing.Spawned || thing.MapHeld == null)
        {
            return false;
        }
        if (thing is Apparel)
        {
            return true;
        }
        return thing.def.IsWeapon;
    }

    public static Gizmo MakeCommand(Thing thing)
    {
        Map map = thing.MapHeld ?? Find.CurrentMap;
        bool hasAnyStand = map != null && OutfitStandsOnMap(map).Any();
        return new Command_Target
        {
            defaultLabel = "BOS.HaulToTargetOutfitStand".Translate(),
            defaultDesc = "BOS.HaulToTargetOutfitStand.Desc".Translate(thing.LabelShort.Named("THING")),
            icon = (Texture)HaulToOutfitStandIcon,
            Disabled = !hasAnyStand,
            disabledReason = !hasAnyStand ? "BOS.NoOutfitStand".Translate() : null,
            targetingParams = TargetingParametersForOutfitStand(thing),
            action = target => TryOrderHaulToOutfitStand(thing, target)
        };
    }

    private static TargetingParameters TargetingParametersForOutfitStand(Thing thing)
    {
        return new TargetingParameters
        {
            canTargetPawns = false,
            canTargetItems = false,
            canTargetBuildings = true,
            mapObjectTargetsMustBeAutoAttackable = false,
            validator = target => target.HasThing && target.Thing is Building_OutfitStand stand && stand.Faction == Faction.OfPlayer && stand.CanEverStoreThing(thing)
        };
    }

    private static IEnumerable<Building_OutfitStand> OutfitStandsOnMap(Map map)
    {
        if (map?.listerBuildings == null)
        {
            yield break;
        }
        foreach (Building building in map.listerBuildings.allBuildingsColonist)
        {
            if (building is Building_OutfitStand stand)
            {
                yield return stand;
            }
        }
    }

    private static void TryOrderHaulToOutfitStand(Thing thing, LocalTargetInfo target)
    {
        if (thing == null || !thing.Spawned || thing.Destroyed)
        {
            Messages.Message("BOS.CannotHaulToOutfitStand.Destroyed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        if (target.Thing is not Building_OutfitStand stand || stand.Faction != Faction.OfPlayer)
        {
            Messages.Message("BOS.MustTargetOutfitStand".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        if (!stand.CanEverStoreThing(thing))
        {
            Messages.Message("CannotStoreThingOnTarget".Translate(thing.Named("THING"), stand.Named("TARGET")), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        AllowDefOnStand(stand, thing.def);

        Pawn pawn = FindBestHauler(thing, stand);
        if (pawn == null)
        {
            Messages.Message("BOS.NoPawnCanHaulToOutfitStand".Translate(thing.LabelShort.Named("THING"), stand.LabelShort.Named("TARGET")), thing, MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        Job job = MakeHaulJob(thing, stand);
        if (job == null)
        {
            Messages.Message("BOS.CannotHaulToOutfitStand".Translate(thing.LabelShort.Named("THING"), stand.LabelShort.Named("TARGET")), thing, MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        job.playerForced = true;
        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        Messages.Message("BOS.HaulToOutfitStandOrdered".Translate(pawn.LabelShort.Named("PAWN"), thing.LabelShort.Named("THING"), stand.LabelShort.Named("TARGET")), thing, MessageTypeDefOf.TaskCompletion, historical: false);
    }

    private static Pawn FindBestHauler(Thing thing, Building_OutfitStand stand)
    {
        Map map = thing.MapHeld ?? stand.MapHeld;
        if (map == null)
        {
            return null;
        }

        if (Find.Selector.SingleSelectedThing is Pawn selectedPawn && selectedPawn.MapHeld == map && PawnCanHaulToStand(selectedPawn, thing, stand))
        {
            return selectedPawn;
        }

        Pawn bestPawn = null;
        float bestDistance = float.MaxValue;
        foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
        {
            if (!PawnCanHaulToStand(pawn, thing, stand))
            {
                continue;
            }
            float distance = pawn.Position.DistanceToSquared(thing.PositionHeld) + thing.PositionHeld.DistanceToSquared(stand.PositionHeld);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPawn = pawn;
            }
        }
        return bestPawn;
    }

    private static bool PawnCanHaulToStand(Pawn pawn, Thing thing, Building_OutfitStand stand)
    {
        if (pawn == null || pawn.Downed || pawn.InMentalState || pawn.WorkTagIsDisabled(WorkTags.ManualDumb | WorkTags.Hauling | WorkTags.AllWork))
        {
            return false;
        }
        if (ModsConfig.BiotechActive && pawn.IsWorkTypeDisabledByAge(WorkTypeDefOf.Hauling, out var _))
        {
            return false;
        }
        if (!pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, pawn.NormalMaxDanger(), 1, -1, null, ignoreOtherReservations: false))
        {
            return false;
        }
        return pawn.CanReserveAndReach(stand, PathEndMode.InteractionCell, pawn.NormalMaxDanger(), 1, -1, null, ignoreOtherReservations: false);
    }

    private static Job MakeHaulJob(Thing thing, Building_OutfitStand stand)
    {
        if (thing is Apparel apparel)
        {
            return JobMaker.MakeJob(JobDefOf.PutApparelOnOutfitStand, apparel, stand);
        }

        ThingOwner owner = stand.TryGetInnerInteractableThingOwner();
        if (owner == null || !owner.CanAcceptAnyOf(thing) || !((IHaulDestination)stand).Accepts(thing))
        {
            return null;
        }
        Job job = JobMaker.MakeJob(JobDefOf.HaulToContainer, thing, stand);
        job.count = Math.Min(1, thing.stackCount);
        job.haulMode = HaulMode.ToContainer;
        return job;
    }

    public static void AllowDefOnStand(Building_OutfitStand stand, ThingDef thingDef)
    {
        if (stand == null || thingDef == null)
        {
            return;
        }
        stand.GetStoreSettings().filter.SetAllow(thingDef, true);
        if (stand is Building_BetterOutfitStand betterStand)
        {
            betterStand.EnsureHeldItemsAllowed();
        }
    }

    public static void DisableDefIfNoLongerHeld(Building_OutfitStand stand, ThingDef thingDef)
    {
        if (stand == null || thingDef == null)
        {
            return;
        }
        foreach (Thing heldItem in stand.HeldItems)
        {
            if (heldItem.def == thingDef)
            {
                return;
            }
        }
        stand.GetStoreSettings().filter.SetAllow(thingDef, false);
    }
}
