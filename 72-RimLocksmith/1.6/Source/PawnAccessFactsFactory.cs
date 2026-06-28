using System.Reflection;
using RimWorld;
using RunningBugs.RimLocksmith.Core;
using Verse;

namespace RunningBugs.RimLocksmith;

public static class PawnAccessFactsFactory
{
    public static PawnAccessFacts FromPawn(Pawn pawn)
    {
        if (pawn == null)
        {
            return new PawnAccessFacts(AccessCategory.Other, canOpenDoors: false);
        }

        bool canOpenDoors = pawn.CanOpenDoors;
        bool isFenceBlockedRoamer = IsFenceBlockedRoamer(pawn);
        bool isRopedByPawn = pawn.roping?.IsRopedByPawn ?? false;
        bool roperCanOpen = isRopedByPawn && pawn.roping.RopedByPawn != null && pawn.roping.RopedByPawn.CanOpenDoors;

        if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer)) return Make(AccessCategory.Hostile);
        if (pawn.IsPrisonerOfColony) return Make(AccessCategory.Prisoner);
        if (pawn.IsSlaveOfColony) return Make(AccessCategory.Slave);
        if (ModsConfig.BiotechActive && pawn.IsColonyMech) return Make(AccessCategory.ColonyMechanoid);
        if (pawn.Faction == Faction.OfPlayer && pawn.RaceProps.Animal) return Make(AccessCategory.ColonyAnimal);
        if (pawn.IsColonist || pawn.Faction == Faction.OfPlayer) return Make(AccessCategory.Colonist);
        if (pawn.RaceProps.Animal) return Make(AccessCategory.WildAnimal);
        if (pawn.TraderKind != null) return Make(AccessCategory.Trader);
        if (pawn.Faction != null && !pawn.Faction.HostileTo(Faction.OfPlayer))
        {
            return pawn.Faction.PlayerGoodwill >= 75
                ? Make(AccessCategory.Ally)
                : Make(AccessCategory.Guest);
        }
        return Make(AccessCategory.Other);

        PawnAccessFacts Make(AccessCategory category)
        {
            return new PawnAccessFacts(category, canOpenDoors, isFenceBlockedRoamer, isRopedByPawn, roperCanOpen);
        }
    }

    public static bool IsFenceBlockedRoamer(Pawn pawn)
    {
        if (pawn?.RaceProps?.FenceBlocked != true)
        {
            return false;
        }

        return !HasObjectSpecificRoamSuppression(pawn);
    }

    private static bool HasObjectSpecificRoamSuppression(Pawn pawn)
    {
        if (!ModsConfig.OdysseyActive || pawn?.health?.hediffSet?.hediffs == null)
        {
            return false;
        }

        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff == null)
            {
                continue;
            }

            if (hediff.def?.defName == "SentienceCatalyst")
            {
                return true;
            }

            object curStage = hediff.CurStage;
            FieldInfo removeRoamMtbField = curStage?.GetType().GetField("removeRoamMtb", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (removeRoamMtbField != null && removeRoamMtbField.GetValue(curStage) is bool removeRoamMtb && removeRoamMtb)
            {
                return true;
            }
        }

        return false;
    }
}
