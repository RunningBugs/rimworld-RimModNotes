using Verse;
using RimWorld;
using HarmonyLib;
using UnityEngine;
using RimWorld.Planet;

namespace MaximumAnimalDensity;


[StaticConstructorOnStartup]
public class ApplyPatches
{
    static ApplyPatches()
    {
        Harmony harmony = new("com.RunningBugs.MaximumAnimalDensity");
        harmony.PatchAll();
        Log.Message("MaximumAnimalDensity: Patches applied successfully.".Colorize(Color.green));
    }
}

public class Settings : ModSettings
{
    public float maxAllowedAnimalsDensity = 1f;
    public bool isEnabled = true;
    public override void ExposeData()
    {
        Scribe_Values.Look(ref maxAllowedAnimalsDensity, "maxAllowedAnimalsDensity", 1f);
        Scribe_Values.Look(ref isEnabled, "isEnabled", true);
    }
}

public class SettingsUI : Mod
{
    public static Settings settings;
    public SettingsUI(ModContentPack content) : base(content)
    {
        settings = GetSettings<Settings>();
    }

    public override string SettingsCategory() => "MaximumAnimalDensity".Translate();

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);
        listing.CheckboxLabeled("MaximumAnimalDensity.IsEnabled".Translate(), ref settings.isEnabled);
        if (settings.isEnabled)
        {
            var num = settings.maxAllowedAnimalsDensity;
            settings.maxAllowedAnimalsDensity = listing.SliderLabeled("MaximumAnimalDensity.MaxAllowedAnimalsDensity".Translate(num.ToString("F2")), num, 0f, 2f);
        }
        listing.End();
    }
}

[HarmonyPatch(typeof(Tile), nameof(Tile.AnimalDensity), MethodType.Getter)]
public static class Tile_AnimalDensity_Patch
{
    public static void Postfix(ref float __result)
    {
        if (SettingsUI.settings.isEnabled)
        {
            __result = Mathf.Min(__result, SettingsUI.settings.maxAllowedAnimalsDensity);
        }
    }
}


