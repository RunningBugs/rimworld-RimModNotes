namespace RunningBugs.RimLocksmith.Core;

public readonly struct PawnAccessFacts
{
    public PawnAccessFacts(AccessCategory category, bool canOpenDoors = true, bool isFenceBlockedRoamer = false, bool isRopedByPawn = false, bool roperCanOpen = false)
    {
        Category = category;
        CanOpenDoors = canOpenDoors;
        IsFenceBlockedRoamer = isFenceBlockedRoamer;
        IsRopedByPawn = isRopedByPawn;
        RoperCanOpen = roperCanOpen;
    }

    public AccessCategory Category { get; }
    public bool CanOpenDoors { get; }
    public bool IsFenceBlockedRoamer { get; }
    public bool IsRopedByPawn { get; }
    public bool RoperCanOpen { get; }
}
