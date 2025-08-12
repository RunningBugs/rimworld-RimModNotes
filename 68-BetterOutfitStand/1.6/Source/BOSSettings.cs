using UnityEngine;
using Verse;

namespace BetterOutfitStand;

public class BOSSettings : ModSettings
{
    public bool SelectAllByDefault = false;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref SelectAllByDefault, "BOS.SelectAllByDefault", false);
    }
}

public class BOSMod : Mod
{
    public static BOSSettings Settings;

    public BOSMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<BOSSettings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Widgets.CheckboxLabeled(new Rect(inRect.x, inRect.y, inRect.width, 30f), "BOS.SelectAllByDefault".Translate(), ref Settings.SelectAllByDefault);
    }

    public override string SettingsCategory()
    {
        return "BetterOutfitStand".Translate();
    }
}