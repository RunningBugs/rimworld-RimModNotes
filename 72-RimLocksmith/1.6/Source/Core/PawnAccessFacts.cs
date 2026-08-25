namespace RunningBugs.RimLocksmith.Core;

public readonly struct PawnAccessFacts
{
    public PawnAccessFacts(AccessCategory category, bool hasOverseer = false)
    {
        Category = category;
        HasOverseer = hasOverseer;
    }

    public AccessCategory Category { get; }
    public bool HasOverseer { get; }
}
