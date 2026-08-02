using System;
using System.Collections.Generic;
using RunningBugs.RimLocksmith.Core;
using Verse;

namespace RunningBugs.RimLocksmith;

public static class UIUtil
{
    public static string ModeLabel(AnimalAccess mode) => ("RimLocksmith.AnimalAccess." + mode).Translate();

    public static string ModeLabel(MechAccess mode) => ("RimLocksmith.MechAccess." + mode).Translate();

    /// <summary>绘制一份配置的完整编辑控件(设置页默认预设用)。改动即生效。</summary>
    public static void DrawConfigControls(Listing_Standard listing, LockConfigData config, bool markConfigured)
    {
        DrawToggle(listing, "RimLocksmith.AllowColonists", ref config.AllowColonists);
        DrawToggle(listing, "RimLocksmith.AllowSlaves", ref config.AllowSlaves);
        DrawToggle(listing, "RimLocksmith.AllowGuests", ref config.AllowGuests);
        DrawToggle(listing, "RimLocksmith.AllowTraders", ref config.AllowTraders);
        if (listing.ButtonText("RimLocksmith.AnimalAccess".Translate() + ": " + ModeLabel(config.AnimalAccess)))
        {
            OpenModeMenu<AnimalAccess>(mode => config.AnimalAccess = mode, ModeLabel);
        }
        if (listing.ButtonText("RimLocksmith.MechAccess".Translate() + ": " + ModeLabel(config.MechAccess)))
        {
            OpenModeMenu<MechAccess>(mode => config.MechAccess = mode, ModeLabel);
        }
        if (markConfigured)
        {
            config.UserConfigured = true;
        }
    }

    public static void OpenModeMenu<T>(Action<T> onSelect, Func<T, string> label) where T : Enum
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        foreach (T mode in Enum.GetValues(typeof(T)))
        {
            T current = mode;
            options.Add(new FloatMenuOption(label(current), () => onSelect(current)));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void DrawToggle(Listing_Standard listing, string key, ref bool value)
    {
        listing.CheckboxLabeled(key.Translate(), ref value, "RimLocksmith.AllowOpenTooltip".Translate());
    }
}
