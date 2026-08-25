namespace RunningBugs.RimLocksmith.Core;

public sealed class LockConfigData
{
    // v3:删除商队开关(并入访客)与动物宠物档。
    // 玩家派系开关(殖民者/奴隶/殖民地动物/殖民地机械体)受设置项
    // 「管理殖民地单位」(ManageColonyPawns,默认启用)门控;异象开关仍注释禁用。
    public const int CurrentVersion = 3;

    public int Version = CurrentVersion;
    public bool UserConfigured;
    public string LinkedPresetId = string.Empty;

    public bool AllowColonists = true;
    public bool AllowSlaves = true;
    public bool AllowGuests = true;
    // ANOMALY-CONTROL(disabled)
    // public bool AllowAnomalies = true;
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
            // ANOMALY-CONTROL(disabled)
            // AllowAnomalies = AllowAnomalies,
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
