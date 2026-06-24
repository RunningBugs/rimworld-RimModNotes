using RunningBugs.RimLocksmith.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RunningBugs.RimLocksmith;

public sealed class RimLocksmithSettings : ModSettings
{
    public bool ApplyDefaultToNewColonyDoors = true;
    public LockConfigData DefaultConfig = LockConfigData.CreateDefault();

    public override void ExposeData()
    {
        Scribe_Values.Look(ref ApplyDefaultToNewColonyDoors, "applyDefaultToNewColonyDoors", true);
        LockConfigScribe.Look(ref DefaultConfig, "defaultConfig", createIfMissing: true);
        if (Scribe.mode == LoadSaveMode.PostLoadInit || DefaultConfig == null)
        {
            DefaultConfig ??= LockConfigData.CreateDefault();
            DefaultConfig.Normalize();
            DefaultConfig.UserConfigured = false;
        }
    }

    public void DoWindowContents(Rect inRect)
    {
        Listing_Standard listing = new Listing_Standard();
        listing.Begin(inRect);
        listing.CheckboxLabeled("RimLocksmith.ApplyDefaultToNewColonyDoors".Translate(), ref ApplyDefaultToNewColonyDoors, "RimLocksmith.ApplyDefaultToNewColonyDoorsDesc".Translate());
        listing.GapLine();
        listing.Label("RimLocksmith.DefaultPreset".Translate());
        UIUtil.DrawConfigToggles(listing, DefaultConfig, markConfigured: false);
        listing.GapLine();
        if (listing.ButtonText("RimLocksmith.ApplyDefaultToUnconfiguredColonyDoors".Translate()))
        {
            int changed = RimLocksmithUtility.ApplyDefaultToColonyDoors(overwriteConfigured: false);
            Messages.Message("RimLocksmith.AppliedDefaultCount".Translate(changed), MessageTypeDefOf.TaskCompletion, false);
        }
        if (listing.ButtonText("RimLocksmith.ApplyDefaultToAllColonyDoors".Translate()))
        {
            int changed = RimLocksmithUtility.ApplyDefaultToColonyDoors(overwriteConfigured: true);
            Messages.Message("RimLocksmith.AppliedDefaultCount".Translate(changed), MessageTypeDefOf.TaskCompletion, false);
        }
        if (listing.ButtonText("RimLocksmith.ResetDefaultPreset".Translate()))
        {
            DefaultConfig = LockConfigData.CreateDefault();
        }
        listing.End();
        Write();
    }
}
