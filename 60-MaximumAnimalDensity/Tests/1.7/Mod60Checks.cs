using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RW17.Validation;

public static class Mod60Checks
{
    public const string PackageId = "RunningBugs.MaximumAnimalDensity";

    public static void Run(Map map)
    {
        var target = AccessTools.PropertyGetter(typeof(Tile), nameof(Tile.AnimalDensity));
        Require(Harmony.GetPatchInfo(target)?.Postfixes.Any(p =>
            p.PatchMethod.DeclaringType?.Assembly.GetName().Name == "MaximumAnimalDensity") == true,
            "The actual density getter has this MOD's postfix.");
        var modAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(a =>
            a.GetName().Name == "MaximumAnimalDensity");
        object settings = modAssembly.GetType("MaximumAnimalDensity.SettingsUI")
            .GetField("settings").GetValue(null);
        Require(settings != null, "Real MOD settings are initialized.");
        var enabled = settings.GetType().GetField("isEnabled");
        var limit = settings.GetType().GetField("maxAllowedAnimalsDensity");
        object savedEnabled = enabled.GetValue(settings);
        object savedLimit = limit.GetValue(settings);
        var tile = new Tile
        {
            PrimaryBiome = new BiomeDef { animalDensity = 1.5f },
            mutatorsNullable = new List<TileMutatorDef> { new() { animalDensityFactor = 2f } }
        };
        try
        {
            enabled.SetValue(settings, false);
            limit.SetValue(settings, 0.4f);
            Require(tile.AnimalDensity == 3f, "Disabled: native biome/mutator product is preserved.");
            enabled.SetValue(settings, true);
            Require(tile.AnimalDensity == 0.4f, "Enabled: product exceeding the limit is capped.");
            tile.PrimaryBiome.animalDensity = 0.5f;
            limit.SetValue(settings, 1.8f);
            Require(tile.AnimalDensity == 1f, "A density below the limit is not increased.");
            limit.SetValue(settings, 0f);
            Require(tile.AnimalDensity == 0f, "Zero limit produces zero density.");
            Require(map?.wildAnimalSpawner != null, "A disposable map and spawner are ready.");
            Require(map.TileInfo.AnimalDensity == 0f, "The real world tile also observes zero limit.");
            float downstream = (float)AccessTools.PropertyGetter(typeof(WildAnimalSpawner),
                "DesiredAnimalDensity").Invoke(map.wildAnimalSpawner, null);
            Require(downstream == 0f, "The actual spawner's desired density sees the cap.");
            Log.Message("[RW17.Validation] Mod60 checks passed: postfix_registered, disabled_native, " +
                "cap_above_limit, below_limit_unchanged, zero_limit, map_tile, spawner_downstream");
        }
        finally
        {
            enabled.SetValue(settings, savedEnabled);
            limit.SetValue(settings, savedLimit);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
