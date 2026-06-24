namespace RunningBugs.RimLocksmith.Core;

public sealed class LockConfigData
{
    public const int CurrentVersion = 1;

    public int Version = CurrentVersion;
    public bool UserConfigured;
    public string LinkedPresetId = string.Empty;

    public bool AllowColonists = true;
    public bool AllowSlaves = true;
    public bool AllowPrisoners = false;
    public bool AllowColonyAnimals = true;
    public bool AllowColonyMechanoids = true;
    public bool AllowGuests = true;
    public bool AllowAllies = true;
    public bool AllowTraders = true;
    public bool AllowHostiles = false;
    public bool AllowWildAnimals = false;
    public bool AllowOthers = false;

    public static LockConfigData CreateDefault(bool userConfigured = false)
    {
        return new LockConfigData { UserConfigured = userConfigured };
    }

    public LockConfigData Clone()
    {
        return new LockConfigData
        {
            Version = Version,
            UserConfigured = UserConfigured,
            LinkedPresetId = LinkedPresetId,
            AllowColonists = AllowColonists,
            AllowSlaves = AllowSlaves,
            AllowPrisoners = AllowPrisoners,
            AllowColonyAnimals = AllowColonyAnimals,
            AllowColonyMechanoids = AllowColonyMechanoids,
            AllowGuests = AllowGuests,
            AllowAllies = AllowAllies,
            AllowTraders = AllowTraders,
            AllowHostiles = AllowHostiles,
            AllowWildAnimals = AllowWildAnimals,
            AllowOthers = AllowOthers
        };
    }

    public void Normalize()
    {
        if (Version <= 0 || Version > CurrentVersion)
        {
            Version = CurrentVersion;
        }
    }
}
