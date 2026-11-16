using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FastFire;

public enum FireOptimizationMode
{
    ExtremeGrid = 0,
    Compatible = 1,
    Vanilla = 2
}

public sealed class FastFireSettings : ModSettings
{
    public FireOptimizationMode mode = FireOptimizationMode.ExtremeGrid;
    public bool enableClusterLighting = true;
    public bool enableBatchedRendering = true;
    public bool enablePathingDebounce = true;
    public bool enableAudioAggregation = true;
    public bool enableAiSpatialAccel = true;
    public int debugSpawnTargetCount = 12000;

    public bool IsActive => mode != FireOptimizationMode.Vanilla;
    public bool UseGridEngine => mode == FireOptimizationMode.ExtremeGrid;
    public bool UseClusterLighting => IsActive && enableClusterLighting;
    public bool UseBatchedRendering => IsActive && enableBatchedRendering;
    public bool UsePathingDebounce => IsActive && enablePathingDebounce;
    public bool UseAudioAggregation => IsActive && enableAudioAggregation;
    public bool UseAiSpatialAccel => IsActive && enableAiSpatialAccel;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref mode, "mode", FireOptimizationMode.ExtremeGrid);
        Scribe_Values.Look(ref enableClusterLighting, "enableClusterLighting", true);
        Scribe_Values.Look(ref enableBatchedRendering, "enableBatchedRendering", true);
        Scribe_Values.Look(ref enablePathingDebounce, "enablePathingDebounce", true);
        Scribe_Values.Look(ref enableAudioAggregation, "enableAudioAggregation", true);
        Scribe_Values.Look(ref enableAiSpatialAccel, "enableAiSpatialAccel", true);
        Scribe_Values.Look(ref debugSpawnTargetCount, "debugSpawnTargetCount", 12000);
        debugSpawnTargetCount = Mathf.Clamp(debugSpawnTargetCount, 10000, 30000);
        base.ExposeData();
    }
}

public sealed class FastFireMod : Mod
{
    public static FastFireSettings Settings { get; private set; } = new FastFireSettings();

    private Vector2 scrollPos;

    public FastFireMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<FastFireSettings>();
        var harmony = new Harmony("RunningBugs.FastFire");
        harmony.PatchAll();
    }

    public override string SettingsCategory() => "FastFire_SettingsCategory".Translate();

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var viewRect = new Rect(0f, 0f, inRect.width - 20f, 520f);
        Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);

        var listing = new Listing_Standard();
        listing.Begin(viewRect);

        Text.Font = GameFont.Medium;
        listing.Label("FastFire_ModeSection".Translate());
        Text.Font = GameFont.Small;
        listing.Gap(6f);

        DrawModeRadio(listing, FireOptimizationMode.ExtremeGrid, "FastFire_Mode_ExtremeGrid", "FastFire_Mode_ExtremeGridDesc");
        DrawModeRadio(listing, FireOptimizationMode.Compatible, "FastFire_Mode_Compatible", "FastFire_Mode_CompatibleDesc");
        DrawModeRadio(listing, FireOptimizationMode.Vanilla, "FastFire_Mode_Vanilla", "FastFire_Mode_VanillaDesc");

        listing.GapLine(12f);

        Text.Font = GameFont.Medium;
        listing.Label("FastFire_ModulesSection".Translate());
        Text.Font = GameFont.Small;
        listing.Gap(6f);

        listing.CheckboxLabeled("FastFire_EnableClusterLighting".Translate(), ref Settings.enableClusterLighting, "FastFire_EnableClusterLightingDesc".Translate());
        listing.CheckboxLabeled("FastFire_EnableBatchedRendering".Translate(), ref Settings.enableBatchedRendering, "FastFire_EnableBatchedRenderingDesc".Translate());
        listing.CheckboxLabeled("FastFire_EnablePathingDebounce".Translate(), ref Settings.enablePathingDebounce, "FastFire_EnablePathingDebounceDesc".Translate());
        listing.CheckboxLabeled("FastFire_EnableAudioAggregation".Translate(), ref Settings.enableAudioAggregation, "FastFire_EnableAudioAggregationDesc".Translate());
        listing.CheckboxLabeled("FastFire_EnableAiSpatialAccel".Translate(), ref Settings.enableAiSpatialAccel, "FastFire_EnableAiSpatialAccelDesc".Translate());

        listing.GapLine(12f);
        listing.Label("FastFire_DebugCountLabel".Translate(Settings.debugSpawnTargetCount));
        float sliderVal = listing.Slider(Settings.debugSpawnTargetCount, 10000f, 30000f);
        Settings.debugSpawnTargetCount = Mathf.RoundToInt(sliderVal / 1000f) * 1000;
        if (Settings.debugSpawnTargetCount < 10000)
        {
            Settings.debugSpawnTargetCount = 10000;
        }

        listing.End();
        Widgets.EndScrollView();
    }

    private static void DrawModeRadio(Listing_Standard listing, FireOptimizationMode targetMode, string labelKey, string descKey)
    {
        if (listing.RadioButton(labelKey.Translate(), Settings.mode == targetMode, 0f, descKey.Translate()))
        {
            if (Settings.mode != targetMode)
            {
                Settings.mode = targetMode;
                FireMapComponent.SyncAllMapsToCurrentSettings();
            }
        }
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        FireMapComponent.SyncAllMapsToCurrentSettings();
    }
}
