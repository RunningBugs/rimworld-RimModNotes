using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace TestMod;

[StaticConstructorOnStartup]
public class ApplyPatches
{
    static ApplyPatches()
    {
        // Apply the Harmony patches
        var harmony = new Harmony("com.testmod.hidegenebankgenes");
        harmony.PatchAll();
        Log.Message("Patches applied".Colorize(Color.green));
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
                    Log.Message("Switching to orbital view!".Colorize(Color.green));
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
                    Log.Message("Switching to surface view!".Colorize(Color.green));
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
            Log.Message("First selected object found".Colorize(Color.green));
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
                Log.Message($"Set map generator to: {mapGeneratorDefName}".Colorize(Color.yellow));
            }
        }

        Log.Message($"Selected asteroid: {asteroidObject.def.defName} with generator: {mapGeneratorDefName}".Colorize(Color.green));
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
                Log.Message($"Allowing asteroid settlement on tile {tile}".Colorize(Color.green));
                __result = true;
                reason?.Clear(); // Clear any error message
            }
        }
    }
}


public class AsteroidStartingModSettings : ModSettings
{
    public string defaultMapGenerator = "Asteroid";

    public override void ExposeData()
    {
        Scribe_Values.Look(ref defaultMapGenerator, "defaultMapGenerator", "Asteroid");
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

        listingStandard.Label("Default Asteroid Map Generation:");

        // Radio buttons for generation type
        if (listingStandard.RadioButton("Standard Asteroid (Gold/Plasteel/Uranium)", settings.defaultMapGenerator == "Asteroid"))
        {
            settings.defaultMapGenerator = "Asteroid";
        }

        if (listingStandard.RadioButton("Basic Asteroid (Mixed Resources)", settings.defaultMapGenerator == "AsteroidBasic"))
        {
            settings.defaultMapGenerator = "AsteroidBasic";
        }

        if (listingStandard.RadioButton("Item Stash Asteroid (Loot Containers)", settings.defaultMapGenerator == "OrbitalItemStash"))
        {
            settings.defaultMapGenerator = "OrbitalItemStash";
        }

        if (listingStandard.RadioButton("Terrestrial (Normal Biome)", settings.defaultMapGenerator == "Base_Player"))
        {
            settings.defaultMapGenerator = "Base_Player";
        }

        listingStandard.Gap();
        listingStandard.Label("Note: This setting determines how asteroid starting locations generate their maps.");

        listingStandard.End();
        base.DoSettingsWindowContents(inRect);
    }

    public override string SettingsCategory()
    {
        return "Asteroid Starting Locations";
    }
}


 // Instead of patching GenStep_Asteroid, patch the Space GenStep which runs earlier
  [HarmonyPatch(typeof(GenStep_Space), nameof(GenStep_Space.Generate))]
  public static class GenStep_Space_Generate_Patch
  {
      static void Postfix(Map map, GenStepParams parms)
      {
          // Set player start spot after space generation for player colonies
          if (map.Parent.Faction == Faction.OfPlayer)
          {
              IntVec3 playerStartSpot = FindPlayerStartSpot(map);
              if (playerStartSpot.IsValid)
              {
                  MapGenerator.PlayerStartSpot = playerStartSpot;
                  Log.Message($"Set player start spot to {playerStartSpot} after Space generation".Colorize(Color.green));
              }
          }
      }

      private static IntVec3 FindPlayerStartSpot(Map map)
      {
          // First try to find a valid walkable spot
          var validCells = map.AllCells.Where(c =>
              c.Walkable(map) &&
              c.InBounds(map) &&
              c != IntVec3.Invalid &&
              !c.Fogged(map) &&
              map.terrainGrid.TerrainAt(c) != TerrainDefOf.Space).ToList();

          if (validCells.Count > 0)
          {
              return validCells.RandomElement();
          }

          // If no walkable spots, try any non-space terrain
          var nonSpaceCells = map.AllCells.Where(c =>
              c.InBounds(map) &&
              c != IntVec3.Invalid &&
              map.terrainGrid.TerrainAt(c) != TerrainDefOf.Space).ToList();

          if (nonSpaceCells.Count > 0)
          {
              return nonSpaceCells.RandomElement();
          }

          // Last resort fallback
          return map.Center;
      }
  }