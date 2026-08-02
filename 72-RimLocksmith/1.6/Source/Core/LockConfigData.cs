namespace RunningBugs.RimLocksmith.Core;

public sealed class LockConfigData
{
    public const int CurrentVersion = 2;

    public int Version = CurrentVersion;
    public bool UserConfigured;
    public string LinkedPresetId = string.Empty;

    public bool AllowColonists = true;
    public bool AllowSlaves = true;
    public bool AllowGuests = true;
    public bool AllowTraders = true;
    public AnimalAccess AnimalAccess = AnimalAccess.All;
    public MechAccess MechAccess = MechAccess.All;

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
            AllowGuests = AllowGuests,
            AllowTraders = AllowTraders,
            AnimalAccess = AnimalAccess,
            MechAccess = MechAccess
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
