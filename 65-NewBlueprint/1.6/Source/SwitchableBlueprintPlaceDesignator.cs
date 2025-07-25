using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// R-key switchable blueprint placement designator
public class SwitchableBlueprintPlaceDesignator : BlueprintPlaceDesignatorBase
{
    private PlaceMode currentMode = PlaceMode.BuildingsOnly;

    public SwitchableBlueprintPlaceDesignator(PrefabDef prefab) : base(prefab)
    {
        UpdateLabels();
    }

    public override void SelectedUpdate()
    {
        // Handle R key for mode switching
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.R)
        {
            // Switch modes
            currentMode = currentMode switch
            {
                PlaceMode.TerrainOnly => PlaceMode.BuildingsOnly,
                PlaceMode.BuildingsOnly => PlaceMode.TerrainOnly,
                _ => PlaceMode.TerrainOnly
            };
            UpdateLabels();
            Event.current.Use();
        }
        
        // Call base implementation for other functionality
        base.SelectedUpdate();
    }
    
    private void UpdateLabels()
    {
        var modeText = currentMode switch
        {
            PlaceMode.TerrainOnly => "Blueprint2.Terrain".Translate(),
            PlaceMode.BuildingsOnly => "Blueprint2.Buildings".Translate(),
            _ => "Blueprint2.Unknown".Translate()
        };
        defaultLabel = "Blueprint2.PlaceBlueprintWithSwitchMode".Translate(blueprint.label, modeText, "Blueprint2.SwitchMode".Translate());
        defaultDesc = "Blueprint2.PlaceBlueprintDescription".Translate(blueprint.label, modeText, "Blueprint2.SwitchMode".Translate());
    }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
    }

    protected override AcceptanceReport CanPlaceAt(IntVec3 loc)
    {
        return true; // Always allow - we'll skip individual items that can't be placed
    }

    protected override void PlaceBlueprint(IntVec3 c)
    {
        // Use the UnifiedBlueprintPlaceDesignator logic directly
        var map = Find.CurrentMap;
        var placedCount = 0;
        var skippedCount = 0;

        // Place terrain if mode allows
        if (currentMode == PlaceMode.TerrainOnly)
        {
            foreach (var (terrainData, cell) in blueprint.GetTerrain())
            {
                if (terrainData.def == null || !terrainData.def.BuildableByPlayer)
                {
                    skippedCount++;
                    continue;
                }

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var finalWorldPos = adjustedPosition + c;

                if (finalWorldPos.InBounds(map))
                {
                    if (GenConstruct.CanPlaceBlueprintAt(terrainData.def, finalWorldPos, currentRotation, map).Accepted)
                    {
                        try
                        {
                            if (DebugSettings.godMode)
                            {
                                if (terrainData.def is TerrainDef terrainDef)
                                {
                                    try
                                    {
                                        if (terrainDef.isFoundation)
                                        {
                                            if (map.terrainGrid.FoundationAt(finalWorldPos) == null && 
                                                map.terrainGrid.UnderTerrainAt(finalWorldPos) == null)
                                            {
                                                map.terrainGrid.SetFoundation(finalWorldPos, terrainDef);
                                                placedCount++;
                                            }
                                            else
                                            {
                                                skippedCount++;
                                            }
                                        }
                                        else if (terrainDef.temporary)
                                        {
                                            map.terrainGrid.SetTempTerrain(finalWorldPos, terrainDef);
                                            placedCount++;
                                        }
                                        else
                                        {
                                            map.terrainGrid.SetTerrain(finalWorldPos, terrainDef);
                                            placedCount++;
                                        }
                                    }
                                    catch (System.Exception terrainEx)
                                    {
                                        Log.Warning($"Failed to place terrain {terrainDef.defName} at {finalWorldPos}: {terrainEx.Message}");
                                        skippedCount++;
                                    }
                                }
                                else
                                {
                                    skippedCount++;
                                }
                            }
                            else
                            {
                                GenConstruct.PlaceBlueprintForBuild(terrainData.def, finalWorldPos, map, currentRotation, Faction.OfPlayer, null);
                                placedCount++;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Warning($"Failed to place terrain for {terrainData.def?.defName}: {ex.Message}");
                            skippedCount++;
                        }
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                else
                {
                    skippedCount++;
                }
            }
        }

        // Place buildings if mode allows
        if (currentMode == PlaceMode.BuildingsOnly)
        {
            foreach (var (thingData, cell) in blueprint.GetThings())
            {
                if (thingData.def == null || !thingData.def.BuildableByPlayer)
                {
                    skippedCount++;
                    continue;
                }

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var finalWorldPos = adjustedPosition + c;

                if (finalWorldPos.InBounds(map))
                {
                    var thingRotInt = (int)thingData.relativeRotation;
                    var finalRotInt = (thingRotInt + currentRotation.AsInt) % 4;
                    var finalRot = new Rot4(finalRotInt);

                    if (GenConstruct.CanPlaceBlueprintAt(thingData.def, finalWorldPos, finalRot, map).Accepted)
                    {
                        try
                        {
                            if (DebugSettings.godMode)
                            {
                                if (thingData.def is ThingDef thingDef)
                                {
                                    var thing = ThingMaker.MakeThing(thingDef, thingData.stuff);
                                    thing.SetFactionDirect(Faction.OfPlayer);

                                    if (thingData.quality.HasValue && thing.TryGetComp<CompQuality>() != null)
                                    {
                                        thing.TryGetComp<CompQuality>().SetQuality(thingData.quality.Value, ArtGenerationContext.Colony);
                                    }

                                    if (thingData.hp > 0 && thingData.hp < thing.MaxHitPoints)
                                    {
                                        thing.HitPoints = thingData.hp;
                                    }

                                    GenSpawn.Spawn(thing, finalWorldPos, map, finalRot);
                                    placedCount++;
                                }
                                else
                                {
                                    skippedCount++;
                                }
                            }
                            else
                            {
                                GenConstruct.PlaceBlueprintForBuild(thingData.def, finalWorldPos, map, finalRot, Faction.OfPlayer, thingData.stuff);
                                placedCount++;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Warning($"Failed to place building for {thingData.def?.defName}: {ex.Message}");
                            skippedCount++;
                        }
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                else
                {
                    skippedCount++;
                }
            }
        }

        var action = DebugSettings.godMode ? "spawned" : "blueprint placed";
        var modeText = currentMode switch
        {
            PlaceMode.TerrainOnly => "Terrain",
            PlaceMode.BuildingsOnly => "Building",
            _ => "Unknown"
        };
        var message = $"{modeText} {action}: {placedCount} items";
        if (skippedCount > 0)
        {
            message += $" ({skippedCount} skipped)";
        }
        // Messages.Message(message, MessageTypeDefOf.PositiveEvent);
    }

    protected override void DrawGhost(IntVec3 center)
    {
        if (blueprint == null)
            return;

        var map = Find.CurrentMap;
        var canPlace = CanDesignateCell(center).Accepted;
        
        // Draw terrain if mode allows
        if (currentMode == PlaceMode.TerrainOnly)
        {
            foreach (var (terrainData, cell) in blueprint.GetTerrain())
            {
                if (terrainData.def == null || !terrainData.def.BuildableByPlayer)
                    continue;

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var finalWorldPos = adjustedPosition + center;

                if (finalWorldPos.InBounds(map))
                {
                    // Draw terrain preview with better coloring and thickness
                    var terrainColor = canPlace ? new Color(0.0f, 0.7f, 1.0f, 0.6f) : new Color(1.0f, 0.0f, 0.0f, 0.6f);
                    GenDraw.DrawFieldEdges([finalWorldPos], terrainColor);
                    
                    // Add a subtle glow effect
                    if (canPlace)
                    {
                        var glowColor = new Color(0.3f, 0.8f, 1.0f, 0.2f);
                        GenDraw.DrawFieldEdges([finalWorldPos], glowColor, AltitudeLayer.Blueprint.AltitudeFor() + 0.1f);
                    }
                }
            }
        }
        
        // Draw buildings if mode allows
        if (currentMode == PlaceMode.BuildingsOnly)
        {
            foreach (var (thingData, cell) in blueprint.GetThings())
            {
                if (thingData.def == null || !thingData.def.BuildableByPlayer)
                    continue;

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var finalWorldPos = adjustedPosition + center;

                if (finalWorldPos.InBounds(map))
                {
                    var thingRotInt = (int)thingData.relativeRotation;
                    var finalRotInt = (thingRotInt + currentRotation.AsInt) % 4;
                    var finalRot = new Rot4(finalRotInt);

                    var ghostColor = canPlace ? new Color(0.8f, 0.9f, 1f, 0.7f) : new Color(1f, 0.5f, 0.5f, 0.7f);

                    // Use GenThing.TrueCenter for proper graphics positioning
                    var graphicsPos = GenThing.TrueCenter(finalWorldPos, finalRot, thingData.def.size, AltitudeLayer.Blueprint.AltitudeFor());

                    // Calculate occupied cells for outline fallback
                    var occupiedCells = GenAdj.CellsOccupiedBy(finalWorldPos, finalRot, thingData.def.size).ToList();

                    if (thingData.def?.graphic != null && occupiedCells.Count > 0)
                    {
                        var graphicType = thingData.def.graphic.GetType();
                        if (graphicType == typeof(Graphic_Cluster) || graphicType.Name.Contains("Cluster"))
                        {
                            GenDraw.DrawFieldEdges(occupiedCells, ghostColor);
                            
                            // Add glow effect for clusters
                            if (canPlace)
                            {
                                var glowColor = new Color(0.9f, 0.95f, 1f, 0.3f);
                                GenDraw.DrawFieldEdges(occupiedCells, glowColor, AltitudeLayer.Blueprint.AltitudeFor() + 0.1f);
                            }
                        }
                        else
                        {
                            try
                            {
                                var graphic = thingData.def.graphic.GetColoredVersion(
                                    thingData.def.graphic.Shader,
                                    ghostColor,
                                    Color.white);
                                
                                graphic.DrawFromDef(graphicsPos, finalRot, thingData.def, 0f);
                                
                                // Add glow effect for regular buildings
                                if (canPlace)
                                {
                                    var glowGraphic = thingData.def.graphic.GetColoredVersion(
                                        ShaderDatabase.Transparent,
                                        new Color(0.9f, 0.95f, 1f, 0.3f),
                                        Color.white);
                                    glowGraphic.DrawFromDef(graphicsPos, finalRot, thingData.def, 0f);
                                }
                            }
                            catch
                            {
                                GenDraw.DrawFieldEdges(occupiedCells, ghostColor);
                                
                                // Add glow effect for fallback
                                if (canPlace)
                                {
                                    var glowColor = new Color(0.9f, 0.95f, 1f, 0.3f);
                                    GenDraw.DrawFieldEdges(occupiedCells, glowColor, AltitudeLayer.Blueprint.AltitudeFor() + 0.1f);
                                }
                            }
                        }
                    }
                    else
                    {
                        GenDraw.DrawFieldEdges(occupiedCells, ghostColor);
                        
                        // Add glow effect for fallback
                        if (canPlace)
                        {
                            var glowColor = new Color(0.9f, 0.95f, 1f, 0.3f);
                            GenDraw.DrawFieldEdges(occupiedCells, glowColor, AltitudeLayer.Blueprint.AltitudeFor() + 0.1f);
                        }
                    }
                }
            }
        }
    }
}