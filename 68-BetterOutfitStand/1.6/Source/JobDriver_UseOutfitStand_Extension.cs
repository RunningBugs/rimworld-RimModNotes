using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BetterOutfitStand;

public class JobDriver_UseOutfitStandBetter : JobDriver
{
    private int duration;

    private HashSet<Apparel> wornApparelToTransferToStand;

    private HashSet<Apparel> standApparelToTransferToPawn;

    private HashSet<Apparel> standApparelToDrop;

    private Building_BetterOutfitStand OutfitStand => (Building_BetterOutfitStand)job.targetA.Thing;
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref duration, "duration", 0);
        Scribe_Collections.Look(ref wornApparelToTransferToStand, "wornApparelToTransferToStand", LookMode.Reference);
        Scribe_Collections.Look(ref standApparelToTransferToPawn, "standApparelToTransferToPawn", LookMode.Reference);
        Scribe_Collections.Look(ref standApparelToDrop, "standApparelToDrop", LookMode.Reference);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
    }

    public static bool PawnCanWearApparel(Thing apparel, Pawn pawn)
    {
        return apparel is Apparel a && a.PawnCanWear(pawn) && ApparelUtility.HasPartsToWear(pawn, a.def) && (!CompBiocodable.IsBiocoded(a) || CompBiocodable.IsBiocodedFor(a, pawn));
    }

    public override void Notify_Starting()
    {
        base.Notify_Starting();
        standApparelToTransferToPawn = OutfitStand.StandApparelToTransferToPawn;
        wornApparelToTransferToStand = OutfitStand.WornApparelToTransferToStand;
        standApparelToDrop = [];
        List<Apparel> wornApparel = pawn.apparel.WornApparel;
        IReadOnlyList<Thing> heldItems = OutfitStand.HeldItems;
        duration = 0;

        if (standApparelToTransferToPawn.NullOrEmpty())
        {
            PawnDropMode(wornApparel, heldItems);
        }
        else
        {
            PawnTakeMode(wornApparel, heldItems);
        }
    }

    /// <summary>
    /// In this mode, the wornApparelToTransferToStand will be forced to execute.
    /// If the apparel to be transferred is locked, then it will not be transferred.
    /// First find all apparels that can not wear together with the current apparel to transfer.
    /// then we check if the pawn can wear the apparels.
    /// If the pawn can wear the apparel, then we add it to standApparelToTransferToPawn.
    /// </summary>
    /// <param name="wornApparel"></param>
    /// <param name="heldItems"></param>
    private void PawnDropMode(List<Apparel> wornApparel, IReadOnlyList<Thing> heldItems)
    {
        foreach (Apparel apparel in wornApparelToTransferToStand)
        {
            if (pawn.apparel.IsLocked(apparel))
            {
                continue;
            }

            duration += (int)(apparel.GetStatValue(StatDefOf.EquipDelay) * 60f);

            foreach (Thing item in heldItems)
            {
                if (item is not Apparel a2)
                {
                    continue;
                }

                if (!ApparelUtility.CanWearTogether(apparel.def, a2.def, pawn.RaceProps.body))
                {
                    if (PawnCanWearApparel(a2, pawn))
                    {
                        standApparelToTransferToPawn.Add(a2);
                        duration += (int)(a2.GetStatValue(StatDefOf.EquipDelay) * 60f);
                    }
                    else
                    {
                        standApparelToDrop.Add(a2);
                    }
                }
            }
        }
    }

    private void PawnTakeMode(List<Apparel> wornApparel, IReadOnlyList<Thing> heldItems)
    {
        foreach (Thing item in standApparelToTransferToPawn)
        {
            if (!(item is Apparel apparel) || !apparel.PawnCanWear(pawn) || !ApparelUtility.HasPartsToWear(pawn, apparel.def) || (CompBiocodable.IsBiocoded(apparel) && !CompBiocodable.IsBiocodedFor(apparel, pawn)))
            {
                continue;
            }
            bool flag = true;
            foreach (Apparel item2 in wornApparel)
            {
                if (!ApparelUtility.CanWearTogether(apparel.def, item2.def, pawn.RaceProps.body))
                {
                    if (pawn.apparel.IsLocked(item2))
                    {
                        flag = false;
                        break;
                    }
                    duration += (int)(item2.GetStatValue(StatDefOf.EquipDelay) * 60f);
                    wornApparelToTransferToStand.Add(item2);
                }
            }
            if (flag)
            {
                standApparelToTransferToPawn.Add(apparel);
                duration += (int)(apparel.GetStatValue(StatDefOf.EquipDelay) * 60f);
            }
        }
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnBurningImmobile(TargetIndex.A);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDespawnedNullOrForbidden(TargetIndex.A);
        Toil toil = ToilMaker.MakeToil("MakeNewToils");
        toil.WithProgressBarToilDelay(TargetIndex.A);
        toil.defaultCompleteMode = ToilCompleteMode.Delay;
        toil.defaultDuration = duration;
        yield return toil;
        Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
        toil2.AddFinishAction(DoTransfer);
        toil2.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return toil2;
    }

    private void DoTransfer()
    {
        foreach (Apparel item in wornApparelToTransferToStand)
        {
            pawn.apparel.Remove(item);
        }
        foreach (Apparel item2 in standApparelToTransferToPawn)
        {
            OutfitStand.RemoveApparel(item2);
            pawn.apparel.Wear(item2);
            pawn.outfits.forcedHandler.SetForced(item2, forced: true);
        }
        foreach (Apparel toDrop in standApparelToDrop)
        {
            OutfitStand.TryDrop(toDrop, OutfitStand.Position, ThingPlaceMode.Near, 1, out Thing dropped);
            dropped.SetForbidden(true);
        }
        foreach (Apparel item3 in wornApparelToTransferToStand)
        {
            OutfitStand.AddApparel(item3);
        }
        ThingWithComps heldWeapon = OutfitStand.HeldWeapon;
        if (heldWeapon != null && PawnCanWieldWeapon(heldWeapon, pawn) && OutfitStand.RemoveHeldWeapon(heldWeapon))
        {
            pawn.equipment.MakeRoomFor(heldWeapon, out var dropped);
            pawn.equipment.AddEquipment(heldWeapon);
            if (dropped != null)
            {
                dropped.DeSpawn();
                OutfitStand.TryAddHeldWeapon(dropped);
            }
        }
    }

    private static bool PawnCanWieldWeapon(Thing weapon, Pawn pawn)
    {
        if (weapon.def.IsWeapon && pawn.WorkTagIsDisabled(WorkTags.Violent))
        {
            return false;
        }
        if (weapon.def.IsRangedWeapon && pawn.WorkTagIsDisabled(WorkTags.Shooting))
        {
            return false;
        }
        if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
        {
            return false;
        }
        if (pawn.IsQuestLodger() && !EquipmentUtility.QuestLodgerCanEquip(weapon, pawn))
        {
            return false;
        }
        if (!EquipmentUtility.CanEquip(weapon, pawn, out var _))
        {
            return false;
        }
        return true;
    }
}