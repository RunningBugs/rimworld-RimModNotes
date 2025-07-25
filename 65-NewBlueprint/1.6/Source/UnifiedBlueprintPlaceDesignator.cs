using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// Unified blueprint placement designator with flexible placement modes
public class UnifiedBlueprintPlaceDesignator : BlueprintPlaceDesignatorBase
{
    private PlaceMode placeMode;

    public UnifiedBlueprintPlaceDesignator(PrefabDef prefab, PlaceMode mode) : base(prefab)
    {
        placeMode = mode;
        var modeText = mode switch
        {
            PlaceMode.TerrainOnly => "Blueprint2.Terrain".Translate(),
            PlaceMode.BuildingsOnly => "Blueprint2.Buildings".Translate(),
            _ => "Blueprint2.Unknown".Translate()
        };
        defaultLabel = "Blueprint2.PlaceBlueprintWithSwitchMode".Translate(prefab.label, modeText);
        defaultDesc = "Blueprint2.PlaceBlueprintDescription".Translate(prefab.label, modeText);
    }

    protected override AcceptanceReport CanPlaceAt(IntVec3 loc)
    {
        return true; // Always allow - we'll skip individual items that can't be placed
    }

    protected override void PlaceBlueprint(IntVec3 c)
    {
        var map = Find.CurrentMap;
        var placedCount = 0;
        var skippedCount = 0;

        // Place terrain if mode allows
        if (placeMode == PlaceMode.TerrainOnly)
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
                                // In god mode, place terrain directly
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
                                // Normal mode - place terrain blueprint
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
        if (placeMode == PlaceMode.BuildingsOnly)
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
                                // In god mode, spawn building directly
                                if (thingData.def is ThingDef thingDef)
                                {
                                    var thing = ThingMaker.MakeThing(thingDef, thingData.stuff);
                                    thing.SetFactionDirect(Faction.OfPlayer);

                                    // Set quality if specified
                                    if (thingData.quality.HasValue && thing.TryGetComp<CompQuality>() != null)
                                    {
                                        thing.TryGetComp<CompQuality>().SetQuality(thingData.quality.Value, ArtGenerationContext.Colony);
                                    }

                                    // Set HP if specified
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
                                // Normal mode - place building blueprint
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
        var modeText = placeMode switch
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
        
        // Draw terrain if mode allows
        if (placeMode == PlaceMode.TerrainOnly)
        {
            foreach (var (terrainData, cell) in blueprint.GetTerrain())
            {
                if (terrainData.def == null || !terrainData.def.BuildableByPlayer)
                    continue;

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var finalWorldPos = adjustedPosition + center;

                if (finalWorldPos.InBounds(map))
                {
                    var canPlace = GenConstruct.CanPlaceBlueprintAt(terrainData.def, finalWorldPos, currentRotation, map).Accepted;
                    var ghostColor = canPlace ? Color.white : Color.red;
                    ghostColor.a = 0.3f;

                    var occupiedCells = new List<IntVec3> { finalWorldPos };
                    GenDraw.DrawFieldEdges(occupiedCells, ghostColor);
                }
            }
        }
        
        // Draw buildings if mode allows
        if (placeMode == PlaceMode.BuildingsOnly)
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

                    var canPlace = GenConstruct.CanPlaceBlueprintAt(thingData.def, finalWorldPos, finalRot, map).Accepted;
                    var ghostColor = canPlace ? Color.white : Color.red;
                    ghostColor.a = 0.3f;

                    var occupiedCells = GenAdj.CellsOccupiedBy(finalWorldPos, finalRot, thingData.def.Size).ToList();

                    // Try to draw the actual graphic if available
                    if (thingData.def.graphic?.MatSingle != null)
                    {
                        try
                        {
                            var matrix4x = default(Matrix4x4);
                            matrix4x.SetTRS(GenThing.TrueCenter(finalWorldPos, finalRot, thingData.def.Size, thingData.def.Altitude), finalRot.AsQuat, Vector3.one);
                            Graphics.DrawMesh(MeshPool.plane10, matrix4x, thingData.def.graphic.MatSingle, 0);
                        }
                        catch
                        {
                            GenDraw.DrawFieldEdges(occupiedCells, ghostColor);
                        }
                    }
                    else
                    {
                        GenDraw.DrawFieldEdges(occupiedCells, ghostColor);
                    }
                }
            }
        }
    }
}