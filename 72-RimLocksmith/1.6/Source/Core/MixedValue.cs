namespace RunningBugs.RimLocksmith.Core;

public enum MixedValue
{
    False,
    True,
    Mixed
}

public static class MixedValueUtility
{
    public static MixedValue Combine(bool first, bool next, bool hasMixedAlready)
    {
        if (hasMixedAlready) return MixedValue.Mixed;
        return first == next ? (first ? MixedValue.True : MixedValue.False) : MixedValue.Mixed;
    }
}
