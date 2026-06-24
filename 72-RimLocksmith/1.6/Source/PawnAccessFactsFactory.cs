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
        bool isFenceBlockedRoamer = pawn.RaceProps?.FenceBlocked ?? false;
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
}
