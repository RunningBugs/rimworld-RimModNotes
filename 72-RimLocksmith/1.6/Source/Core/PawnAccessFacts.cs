namespace RunningBugs.RimLocksmith.Core;

public readonly struct PawnAccessFacts
{
    public PawnAccessFacts(AccessCategory category, float bodySize = 0f, bool hasOverseer = false)
    {
        Category = category;
        BodySize = bodySize;
        HasOverseer = hasOverseer;
    }

    public AccessCategory Category { get; }
    public float BodySize { get; }
    public bool HasOverseer { get; }
}
