using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// Terrain blueprint placement
public class TerrainBlueprintPlaceDesignator : BlueprintPlaceDesignatorBase
{
    public TerrainBlueprintPlaceDesignator(PrefabDef prefab) : base(prefab)
    {
        defaultLabel = "Blueprint2.PlaceBlueprintWithSwitchMode".Translate(prefab.label, PlaceMode.TerrainOnly.GetLabel());
        defaultDesc = "Blueprint2.PlaceBlueprintDescription".Translate(prefab.label, PlaceMode.TerrainOnly.GetLabel());
    }

    protected override AcceptanceReport CanPlaceAt(IntVec3 loc)
    {
        // Always allow placement - we'll skip individual items that can't be placed
        return true;
    }

    protected override void PlaceBlueprint(IntVec3 c)
    {
        var map = Find.CurrentMap;
        var placedCount = 0;
        var skippedCount = 0;
        
        // Place terrain blueprints - skip ones that can't be placed
        foreach (var (terrainData, cell) in blueprint.GetTerrain())
        {
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
                            // In god mode, place terrain directly
                            var terrainDef = terrainData.def as TerrainDef;
                            if (terrainDef != null)
                            {
                                try
                                {
                                    if (terrainDef.isFoundation)
                                    {
                                        // Check if foundation can be placed (no existing foundation/under terrain)
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
                                    // Terrain placement failed, skip this one
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
                            // Normal mode - place blueprint
                            GenConstruct.PlaceBlueprintForBuild(terrainData.def, finalWorldPos, map, currentRotation, Faction.OfPlayer, null);
                            placedCount++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        // Log error but continue placing other blueprints
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

        var action = DebugSettings.godMode ? "spawned" : "blueprint placed";
        var message = $"Terrain {action}: {placedCount} items";
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
        
        // Draw terrain preview
        foreach (var (_, cell) in blueprint.GetTerrain())
        {
            var rotatedCell = cell.RotatedBy(currentRotation) + center;
            if (rotatedCell.InBounds(map))
            {
                var terrainColor = canPlace ? Color.cyan : Color.red;
                terrainColor.a = 0.5f;
                GenDraw.DrawFieldEdges([rotatedCell], terrainColor);
            }
        }
    }
}