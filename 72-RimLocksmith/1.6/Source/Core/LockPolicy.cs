namespace RunningBugs.RimLocksmith.Core;

public static class LockPolicy
{
    public static bool AllowsOpeningClosedDoor(PawnAccessFacts pawn, DoorAccessFacts door, LockConfigData config)
    {
        if (!door.IsColonyDoor)
        {
            return false;
        }

        if (door.IsOpenOrFreePassage)
        {
            return true;
        }

        if (!pawn.CanOpenDoors)
        {
            return false;
        }

        if (pawn.IsFenceBlockedRoamer && !door.RoamerCanOpen && (!pawn.IsRopedByPawn || !pawn.RoperCanOpen))
        {
            return false;
        }

        return pawn.Category switch
        {
            AccessCategory.Colonist => config.AllowColonists,
            AccessCategory.Slave => config.AllowSlaves,
            AccessCategory.Prisoner => config.AllowPrisoners,
            AccessCategory.ColonyAnimal => config.AllowColonyAnimals,
            AccessCategory.ColonyMechanoid => config.AllowColonyMechanoids,
            AccessCategory.Guest => config.AllowGuests,
            AccessCategory.Ally => config.AllowAllies,
            AccessCategory.Trader => config.AllowTraders,
            AccessCategory.Hostile => config.AllowHostiles,
            AccessCategory.WildAnimal => config.AllowWildAnimals,
            _ => config.AllowOthers
        };
    }
}
