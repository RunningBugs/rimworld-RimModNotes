using RunningBugs.RimLocksmith.Core;
using Verse;

namespace RunningBugs.RimLocksmith;

public static class LockConfigScribe
{
    public static void Look(ref LockConfigData config, string labelPrefix, bool createIfMissing)
    {
        if (createIfMissing && config == null)
        {
            config = LockConfigData.CreateDefault();
        }

        bool hasConfig = config != null;
        Scribe_Values.Look(ref hasConfig, labelPrefix + "_hasConfig", createIfMissing);

        if (!hasConfig)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                config = null;
            }
            return;
        }

        if (config == null)
        {
            config = LockConfigData.CreateDefault();
        }

        // version 缺省视为 1,使旧存档(无 _version 或 version==1)触发 v2 迁移
        Scribe_Values.Look(ref config.Version, labelPrefix + "_version", 1);
        Scribe_Values.Look(ref config.UserConfigured, labelPrefix + "_userConfigured", false);
        Scribe_Values.Look(ref config.LinkedPresetId, labelPrefix + "_linkedPresetId", string.Empty);
        Scribe_Values.Look(ref config.AllowColonists, labelPrefix + "_allowColonists", true);
        Scribe_Values.Look(ref config.AllowSlaves, labelPrefix + "_allowSlaves", true);
        Scribe_Values.Look(ref config.AllowGuests, labelPrefix + "_allowGuests", true);
        Scribe_Values.Look(ref config.AllowTraders, labelPrefix + "_allowTraders", true);
        Scribe_Values.Look(ref config.AnimalAccess, labelPrefix + "_animalAccess", AnimalAccess.All);
        Scribe_Values.Look(ref config.MechAccess, labelPrefix + "_mechAccess", MechAccess.All);

        if (Scribe.mode == LoadSaveMode.LoadingVars && config.Version < 2)
        {
            // v1 → v2 迁移:盟友并入访客;动物/机械体的旧布尔折叠进三态档位;
            // 囚犯/敌人/野生动物/其他的旧开关直接丢弃(这些类别现在跟随原版)。
            bool legacyAllies = true;
            Scribe_Values.Look(ref legacyAllies, labelPrefix + "_allowAllies", true);
            config.AllowGuests = config.AllowGuests && legacyAllies;

            bool legacyColonyAnimals = true;
            Scribe_Values.Look(ref legacyColonyAnimals, labelPrefix + "_allowColonyAnimals", true);
            if (!legacyColonyAnimals)
            {
                config.AnimalAccess = AnimalAccess.None;
            }

            bool legacyColonyMechanoids = true;
            Scribe_Values.Look(ref legacyColonyMechanoids, labelPrefix + "_allowColonyMechanoids", true);
            if (!legacyColonyMechanoids)
            {
                config.MechAccess = MechAccess.None;
            }

            config.Version = LockConfigData.CurrentVersion;
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars)
        {
            config.Normalize();
        }
    }
}
