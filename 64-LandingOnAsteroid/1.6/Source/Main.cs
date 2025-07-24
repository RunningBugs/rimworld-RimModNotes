using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LandingOnAsteroid;

[StaticConstructorOnStartup]
public class ApplyPatches
{
    static ApplyPatches()
    {
        // Apply the Harmony patches
        var harmony = new Harmony("com.RunningBugs.LandingOnAsteroid");
        harmony.PatchAll();
        Log.Message("[LandingOnAsteroid] Patches applied successfully".Colorize(Color.green));
    }
}

[HarmonyPatch(typeof(Page_SelectStartingSite), nameof(Page_SelectStartingSite.ExtraOnGUI))]
public static class Page_SelectStartingSite_ExtraOnGUI_Patch
{
    static void Postfix(Page_SelectStartingSite __instance)
    {
        if (!ModsConfig.OdysseyActive) return;

        float gizmoSize = 75f;
        float margin = 10f;
        Vector2 position = new Vector2(margin, margin);

        // Get both layers
        var orbitalLayer = Find.WorldGrid.FirstLayerOfDef(PlanetLayerDefOf.Orbit);
        var surfaceLayer = Find.WorldGrid.FirstLayerOfDef(PlanetLayerDefOf.Surface);

        Command_Action layerGizmo = null;

        // Show orbital gizmo if on surface
        if (surfaceLayer != null && surfaceLayer.IsSelected && orbitalLayer != null)
        {
            layerGizmo = new Command_Action
            {
                defaultLabel = "WorldSelectLayer".Translate(orbitalLayer.Def.Named("LAYER")),
                defaultDesc = orbitalLayer.Def.viewGizmoTooltip,
                icon = orbitalLayer.Def.ViewGizmoTexture,
                action = () =>
                {
                    PlanetLayer.Selected = orbitalLayer;
                }
            };
        }
        // Show surface gizmo if on orbital
        else if (orbitalLayer != null && orbitalLayer.IsSelected && surfaceLayer != null)
        {
            layerGizmo = new Command_Action
            {
                defaultLabel = "WorldSelectLayer".Translate(surfaceLayer.Def.Named("LAYER")),
                defaultDesc = surfaceLayer.Def.viewGizmoTooltip,
                icon = surfaceLayer.Def.ViewGizmoTexture,
                action = () =>
                {
                    PlanetLayer.Selected = surfaceLayer;
                }
            };
        }

        if (layerGizmo != null)
        {
            Rect clickRect = new Rect(position.x, position.y, gizmoSize, gizmoSize);

            if (Event.current.type == EventType.Repaint)
            {
                var renderParms = new GizmoRenderParms();
                layerGizmo.GizmoOnGUI(position, gizmoSize, renderParms);
            }

            if (Mouse.IsOver(clickRect))
            {
                if (Event.current.type == EventType.Repaint)
                {
                    TooltipHandler.TipRegion(clickRect, layerGizmo.Desc);
                }

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    layerGizmo.action?.Invoke();
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Event.current.Use();
                }
            }
        }
    }
}

[HarmonyPatch(typeof(Page_SelectStartingSite), nameof(Page_SelectStartingSite.DoWindowContents))]
public static class Page_SelectStartingSite_DoWindowContents_Patch
{
    static void Postfix()
    {
        // Handle asteroid selection in orbital view
        if (Find.WorldSelector.FirstSelectedObject != null)
        {
            var selectedObject = Find.WorldSelector.FirstSelectedObject;

            // Check if it's an asteroid or other orbital object
            if (selectedObject is WorldObject worldObj && worldObj.def.worldObjectClass.IsSubclassOf(typeof(SpaceMapParent)))
            {
                SetAsteroidStartingLocation(selectedObject);
            }
        }
    }

    private static void SetAsteroidStartingLocation(WorldObject asteroidObject)
    {
        // Set the selected tile
        Find.GameInitData.startingTile = asteroidObject.Tile;
        Find.WorldInterface.SelectedTile = asteroidObject.Tile;

        // Use the default map generator from settings
        string mapGeneratorDefName = AsteroidStartingMod.settings.defaultMapGenerator;

        if (!string.IsNullOrEmpty(mapGeneratorDefName) && mapGeneratorDefName != "Base_Player")
        {
            var mapGenDef = DefDatabase<MapGeneratorDef>.GetNamed(mapGeneratorDefName);
            if (mapGenDef != null)
            {
                Find.GameInitData.mapGeneratorDef = mapGenDef;
            }
        }

        // Also try to configure the precious resource here as a backup
        if (asteroidObject is SpaceMapParent spaceParent && spaceParent.preciousResource != null)
        {
            AsteroidMineralConfig.SetPreciousResource(spaceParent.preciousResource);
        }
    }
}

[HarmonyPatch(typeof(TileFinder), nameof(TileFinder.IsValidTileForNewSettlement))]
public static class TileFinder_IsValidTileForNewSettlement_Patch
{
    static void Postfix(PlanetTile tile, StringBuilder reason, ref bool __result)
    {
        // If validation already passed, don't override
        if (__result) return;

        // Check if this is an asteroid in orbital layer
        if (tile.Layer?.Def?.defName == "Orbit")
        {
            // Find if there's an asteroid world object on this tile
            var worldObjects = Find.WorldObjects.AllWorldObjects
                .Where(wo => wo.Tile == tile && wo.def.worldObjectClass.IsSubclassOf(typeof(SpaceMapParent)))
                .ToList();

            if (worldObjects.Any())
            {
                Log.Message($"[LandingOnAsteroid] Allowing asteroid settlement on tile {tile}".Colorize(Color.green));
                __result = true;
                reason?.Clear(); // Clear any error message
            }
        }
    }
}


public class AsteroidStartingModSettings : ModSettings
{
    public string defaultMapGenerator = "AsteroidBasic";

    public override void ExposeData()
    {
        Scribe_Values.Look(ref defaultMapGenerator, "LandingOnAsteroid.defaultMapGenerator", "AsteroidBasic");
        if (defaultMapGenerator != "AsteroidBasic" && defaultMapGenerator != "Base_Player")
        {
            Log.Warning($"[LandingOnAsteroid] Invalid defaultMapGenerator '{defaultMapGenerator}', resetting to 'AsteroidBasic'");
            defaultMapGenerator = "AsteroidBasic";
        }
        base.ExposeData();
    }
}

public class AsteroidStartingMod : Mod
{
    public static AsteroidStartingModSettings settings;

    public AsteroidStartingMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<AsteroidStartingModSettings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);

        listingStandard.Label("LandingOnAsteroid.DefaultAsteroidMapGenerationLabel".Translate());

        if (listingStandard.RadioButton("LandingOnAsteroid.BasicAsteroid".Translate(), settings.defaultMapGenerator == "AsteroidBasic"))
        {
            settings.defaultMapGenerator = "AsteroidBasic";
        }

        if (listingStandard.RadioButton("LandingOnAsteroid.BasePlayer".Translate(), settings.defaultMapGenerator == "Base_Player"))
        {
            settings.defaultMapGenerator = "Base_Player";
        }

        listingStandard.Gap();
        listingStandard.Label("LandingOnAsteroid.Note".Translate());

        listingStandard.End();
        base.DoSettingsWindowContents(inRect);
    }

    public override string SettingsCategory()
    {
        return "LandingOnAsteroid".Translate();
    }
}


// Patch the Next button to configure map generator for asteroids
[HarmonyPatch(typeof(Page_SelectStartingSite), "DoNext")]
public static class Page_SelectStartingSite_DoNext_Patch
{
    static void Prefix()
    {
        try
        {
            // Check if we have an asteroid selected
            if (Find.WorldSelector?.FirstSelectedObject != null)
            {
                var selectedObject = Find.WorldSelector.FirstSelectedObject;

                // Check if it's an asteroid or other orbital object
                if (selectedObject is WorldObject worldObj &&
                    worldObj.def?.worldObjectClass != null &&
                    worldObj.def.worldObjectClass.IsSubclassOf(typeof(SpaceMapParent)))
                {
                    ConfigureMapGeneratorForAsteroid(worldObj);
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Error($"[LandingOnAsteroid] Error in Page_SelectStartingSite_DoNext_Patch.Prefix: {ex}");
        }
    }

    private static void ConfigureMapGeneratorForAsteroid(WorldObject asteroidObject)
    {
        try
        {
            // Get the SpaceMapParent to access precious resource
            if (asteroidObject is SpaceMapParent spaceParent && spaceParent.preciousResource != null)
            {
                // Store the precious resource using our custom configuration class
                AsteroidMineralConfig.SetPreciousResource(spaceParent.preciousResource);
            }
            else
            {
                Log.Warning($"[LandingOnAsteroid] Could not get precious resource from asteroid: {asteroidObject.def.defName}");
                AsteroidMineralConfig.ClearPreciousResource();
            }
        }
        catch (System.Exception ex)
        {
            Log.Error($"[LandingOnAsteroid] Error configuring map generator for asteroid: {ex}");
            AsteroidMineralConfig.ClearPreciousResource();
        }
    }
}

// Static class to store asteroid mineral configuration
public static class AsteroidMineralConfig
{
    private static ThingDef storedPreciousResource = null;

    public static void SetPreciousResource(ThingDef resource)
    {
        storedPreciousResource = resource;
        Log.Message($"[LandingOnAsteroid] Stored precious resource: {resource?.defName ?? "null"}".Colorize(Color.yellow));
    }

    public static ThingDef GetPreciousResource()
    {
        return storedPreciousResource;
    }

    public static void ClearPreciousResource()
    {
        storedPreciousResource = null;
        Log.Message("[LandingOnAsteroid] Cleared stored precious resource".Colorize(Color.yellow));
    }

    public static bool HasPreciousResource()
    {
        return storedPreciousResource != null;
    }
}

// Safe cast helper for getting precious resource from map parent
public static class SafeMapParentCast
{
    public static ThingDef GetPreciousResource(Map map)
    {
        // First priority: Our stored precious resource
        if (AsteroidMineralConfig.HasPreciousResource())
        {
            var stored = AsteroidMineralConfig.GetPreciousResource();
            Log.Message($"[LandingOnAsteroid] Using stored precious resource: {stored.defName}".Colorize(Color.green));
            return stored;
        }

        // Second priority: Try to get from SpaceMapParent (safe cast)
        if (map.ParentHolder is SpaceMapParent spaceParent && spaceParent.preciousResource != null)
        {
            Log.Message($"[LandingOnAsteroid] Using SpaceMapParent precious resource: {spaceParent.preciousResource.defName}".Colorize(Color.cyan));
            return spaceParent.preciousResource;
        }

        // Third priority: For Settlement (new colonies), we don't have a precious resource
        if (map.ParentHolder is Settlement settlement)
        {
            Log.Message($"[LandingOnAsteroid] Map parent is Settlement, no precious resource available".Colorize(Color.yellow));
            return null;
        }

        Log.Warning($"[LandingOnAsteroid] Unknown map parent type: {map.ParentHolder?.GetType()?.Name ?? "null"}");
        return null;
    }
}

// Fix the InvalidCastException by using safe casting
[HarmonyPatch(typeof(GenStep_Asteroid), "SpawnOres")]
public static class GenStep_Asteroid_SpawnOres_Patch
{
    static System.Exception Finalizer(System.Exception __exception, GenStep_Asteroid __instance, Map map, GenStepParams parms)
    {
        if (__exception is System.InvalidCastException ex)
        {
            Log.Warning($"[LandingOnAsteroid] InvalidCastException triggered in GenStep_Asteroid.SpawnOres: This means landing on asteroid. If not the case, please report this error: {ex}");

            ThingDef thingDef = SafeMapParentCast.GetPreciousResource(map);
            thingDef ??= __instance.mineableCounts.RandomElement().mineable;
            int num = 0;
            for (int i = 0; i < __instance.mineableCounts.Count; i++)
            {
                if (__instance.mineableCounts[i].mineable == thingDef)
                {
                    num = __instance.mineableCounts[i].countRange.RandomInRange;
                    break;
                }
            }
            if (num == 0)
            {
                Debug.LogError("No count found for resource " + thingDef);
                return null; // Suppress exception
            }
            int randomInRange = __instance.numChunks.RandomInRange;
            int forcedLumpSize = num / randomInRange;
            GenStep_ScatterLumpsMineable genStep_ScatterLumpsMineable = new GenStep_ScatterLumpsMineable();
            genStep_ScatterLumpsMineable.count = randomInRange;
            genStep_ScatterLumpsMineable.forcedDefToScatter = thingDef;
            genStep_ScatterLumpsMineable.forcedLumpSize = forcedLumpSize;
            genStep_ScatterLumpsMineable.Generate(map, parms);
            return null; // Suppress the original exception
        }
        return __exception; // Let other exceptions pass through
    }
}