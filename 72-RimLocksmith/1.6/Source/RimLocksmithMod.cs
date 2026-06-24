using HarmonyLib;
using UnityEngine;
using Verse;

namespace RunningBugs.RimLocksmith;

public sealed class RimLocksmithMod : Mod
{
    public static RimLocksmithSettings Settings;

    public RimLocksmithMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<RimLocksmithSettings>();
        new Harmony("runningbugs.rimlocksmith").PatchAll();
    }

    public override string SettingsCategory() => "RimLocksmith.SettingsCategory".Translate();

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoWindowContents(inRect);
    }
}
