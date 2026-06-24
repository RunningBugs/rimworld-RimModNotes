using RunningBugs.RimLocksmith.Core;
using Verse;

namespace RunningBugs.RimLocksmith;

public sealed class CompRimLocksmithDoor : ThingComp
{
    private LockConfigData config;

    public LockConfigData Config => EnsureConfig();

    public bool HasConfig => config != null;

    public LockConfigData EnsureConfig()
    {
        if (config == null)
        {
            config = RimLocksmithMod.Settings?.DefaultConfig.Clone() ?? LockConfigData.CreateDefault();
            config.UserConfigured = false;
        }
        config.Normalize();
        return config;
    }

    public void SetConfig(LockConfigData newConfig, bool userConfigured)
    {
        config = (newConfig ?? LockConfigData.CreateDefault()).Clone();
        config.UserConfigured = userConfigured;
        NotifyChanged();
    }

    public void NotifyChanged()
    {
        if (parent?.Map != null)
        {
            parent.Map.reachability.ClearCache();
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        LockConfigScribe.Look(ref config, "rimLocksmithConfig", createIfMissing: false);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && config != null)
        {
            config.Normalize();
        }
    }
}
