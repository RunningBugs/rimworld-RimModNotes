using HarmonyLib;
using UnityEngine;
using Verse;

namespace CultistKidnapping;

public sealed class CultistKidnappingMod : Mod
{
    public static KidnappingSettings Settings;

    public CultistKidnappingMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<KidnappingSettings>();
        new Harmony("RunningBugs.CultistKidnapping").PatchAll(typeof(CultistKidnappingMod).Assembly);
    }

    public override string SettingsCategory() => "CK_Name".Translate();

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);
        listing.Label("CK_Description".Translate());
        listing.Gap();
        listing.CheckboxLabeled("CK_EnableRepeat".Translate(), ref Settings.repeatRaids,
            "CK_EnableRepeatTip".Translate());
        listing.Label("CK_Interval".Translate(Settings.intervalDays.min, Settings.intervalDays.max));
        listing.IntRange(ref Settings.intervalDays, 1, 30);
        listing.Label("CK_IntervalTip".Translate());
        if (listing.ButtonText("CK_Reset".Translate()))
        {
            Settings.repeatRaids = true;
            Settings.intervalDays = new IntRange(3, 5);
        }
        listing.End();
    }
}

public sealed class KidnappingSettings : ModSettings
{
    public bool repeatRaids = true;
    public IntRange intervalDays = new(3, 5);

    public void Normalize()
    {
        intervalDays.min = Mathf.Clamp(intervalDays.min, 1, 30);
        intervalDays.max = Mathf.Clamp(intervalDays.max, intervalDays.min, 30);
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref repeatRaids, "repeatRaids", true);
        Scribe_Values.Look(ref intervalDays, "intervalDays", new IntRange(3, 5));
        Normalize();
    }
}
