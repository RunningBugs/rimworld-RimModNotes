using RunningBugs.RimLocksmith.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RunningBugs.RimLocksmith;

public sealed class RimLocksmithSettings : ModSettings
{
    public bool ApplyDefaultToNewColonyDoors = true;
    // 「管理殖民地单位」:启用(默认)后门配置显示玩家派系四类开关(殖民者/奴隶/
    // 殖民地动物/殖民地机械体);禁用后玩家派系单位免管,一律跟随原版。
    public bool ManageColonyPawns = true;
    // ANOMALY-CONTROL(disabled): 暂不考虑控制异象,设置项注释保留。
    // public bool ControlAnomalies = false;
    public LockConfigData DefaultConfig = LockConfigData.CreateDefault();

    public override void ExposeData()
    {
        Scribe_Values.Look(ref ApplyDefaultToNewColonyDoors, "applyDefaultToNewColonyDoors", true);
        Scribe_Values.Look(ref ManageColonyPawns, "manageColonyPawns", true);
        // ANOMALY-CONTROL(disabled): 旧设置值留在存档中忽略。
        // Scribe_Values.Look(ref ControlAnomalies, "controlAnomalies", false);
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
        listing.CheckboxLabeled("RimLocksmith.ManageColonyPawns".Translate(), ref ManageColonyPawns, "RimLocksmith.ManageColonyPawnsDesc".Translate());
        // ANOMALY-CONTROL(disabled): 设置页勾选框注释保留。
        // listing.CheckboxLabeled("RimLocksmith.ControlAnomalies".Translate(), ref ControlAnomalies, "RimLocksmith.ControlAnomaliesDesc".Translate());
        listing.GapLine();
        listing.Label("RimLocksmith.DefaultPreset".Translate());
        UIUtil.DrawConfigControls(listing, DefaultConfig, markConfigured: false);
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
