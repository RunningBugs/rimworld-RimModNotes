using Verse;
using UnityEngine;
using System;

namespace PermanentDarkness;


public class ModSettingsUI : Mod
{
    public static SettingsData settings = null;

    public ModSettingsUI(ModContentPack content) : base(content)
    {
        settings = GetSettings<SettingsData>();
        sliderPos = settings.darknessLevel;
    }

    private float sliderPos = 0f;

    public static bool IsMultipleOfPointFive(float number)
    {
        float doubled = number * 2;
        return doubled == Math.Truncate(doubled);
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);
        listing.CheckboxLabeled("PD.applyDarknessControlSettings".Translate(), ref settings.darknessControl);
        if (settings.darknessControl)
        {
            float newSliderPos = listing.SliderLabeled("PD.settings.darknessLevel".Translate() + $"{settings.darknessLevel}", sliderPos, 0f, 2f);
            sliderPos = 0.1f * (int)(newSliderPos * 10);
            settings.darknessLevel = sliderPos;

            bool before = settings.shadowControl;
            listing.CheckboxLabeled("PD.settings.shadowControl".Translate(), ref settings.shadowControl, "PD.settings.shadowControl.tooltip".Translate());
            if (before != settings.shadowControl)
            {
                GameCondition_PermanentDarkness.shadowControlDirty = true;
            }
        }
        listing.End();
    }

    public override string SettingsCategory()
    {
        return "PD.settingsCategory".Translate();
    }
}

public class SettingsData : ModSettings
{
    public bool darknessControl = false;
    public float darknessLevel = 0.35f;
    public bool shadowControl = false;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref darknessControl, "PD.settings.darknessControl", false);
        Scribe_Values.Look(ref darknessLevel, "PD.settings.darknessLevel", 0.35f);
        Scribe_Values.Look(ref shadowControl, "PD.settings.shadowControl", false);
    }

}
