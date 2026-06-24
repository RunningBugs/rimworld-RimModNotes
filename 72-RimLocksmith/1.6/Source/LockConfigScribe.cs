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

        Scribe_Values.Look(ref config.Version, labelPrefix + "_version", LockConfigData.CurrentVersion);
        Scribe_Values.Look(ref config.UserConfigured, labelPrefix + "_userConfigured", false);
        Scribe_Values.Look(ref config.LinkedPresetId, labelPrefix + "_linkedPresetId", string.Empty);
        Scribe_Values.Look(ref config.AllowColonists, labelPrefix + "_allowColonists", true);
        Scribe_Values.Look(ref config.AllowSlaves, labelPrefix + "_allowSlaves", true);
        Scribe_Values.Look(ref config.AllowPrisoners, labelPrefix + "_allowPrisoners", false);
        Scribe_Values.Look(ref config.AllowColonyAnimals, labelPrefix + "_allowColonyAnimals", true);
        Scribe_Values.Look(ref config.AllowColonyMechanoids, labelPrefix + "_allowColonyMechanoids", true);
        Scribe_Values.Look(ref config.AllowGuests, labelPrefix + "_allowGuests", true);
        Scribe_Values.Look(ref config.AllowAllies, labelPrefix + "_allowAllies", true);
        Scribe_Values.Look(ref config.AllowTraders, labelPrefix + "_allowTraders", true);
        Scribe_Values.Look(ref config.AllowHostiles, labelPrefix + "_allowHostiles", false);
        Scribe_Values.Look(ref config.AllowWildAnimals, labelPrefix + "_allowWildAnimals", false);
        Scribe_Values.Look(ref config.AllowOthers, labelPrefix + "_allowOthers", false);

        if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars)
        {
            config.Normalize();
        }
    }
}
