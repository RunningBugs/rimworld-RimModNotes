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

    // Allocation reduction: reuse buffers for ghost drawing
    private static readonly List<IntVec3> tmpCells = new List<IntVec3>(32);
    private static readonly List<IntVec3> tmpOne = new List<IntVec3>(1);

    public UnifiedBlueprintPlaceDesignator(PrefabDef prefab, PlaceMode mode) : base(prefab)
    {
        placeMode = mode;
        var modeText = mode.GetLabel();
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

        // Place floors if mode allows
        if (placeMode == PlaceMode.FloorOnly)
        {
            foreach (var (terrainData, cell) in blueprint.GetTerrain())
            {
                if (terrainData.def == null || !terrainData.def.BuildableByPlayer)
                {
                    skippedCount++;
                    continue;
                }

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var mirroredPosition = GetMirroredPosition(adjustedPosition);
                var finalWorldPos = mirroredPosition + c;

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
        if (placeMode == PlaceMode.Buildings)
        {
            foreach (var (thingData, cell) in blueprint.GetThings())
            {
                if (thingData.def == null || !thingData.def.BuildableByPlayer)
                {
                    skippedCount++;
                    continue;
                }

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var mirroredPosition = GetMirroredPosition(adjustedPosition);
                var finalWorldPos = mirroredPosition + c;

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
        var message = $"{placeMode.GetLabel()} {action}: {placedCount} items";
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

        // Precompute colors and altitude to avoid per-iteration allocations
        var blueprintAlt = AltitudeLayer.Blueprint.AltitudeFor();
        var canPlaceTerr = true; // computed per-cell below but we need storage
        var canPlaceBld = true;

        // Draw floors if mode allows
        if (placeMode == PlaceMode.FloorOnly)
        {
            foreach (var (terrainData, cell) in blueprint.GetTerrain())
            {
                if (terrainData.def == null || !terrainData.def.BuildableByPlayer)
                    continue;

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var mirroredPosition = GetMirroredPosition(adjustedPosition);
                var finalWorldPos = mirroredPosition + center;

                if (!finalWorldPos.InBounds(map))
                    continue;

                canPlaceTerr = GenConstruct.CanPlaceBlueprintAt(terrainData.def, finalWorldPos, currentRotation, map).Accepted;
                var ghostColor = canPlaceTerr ? new Color(1f, 1f, 1f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);

                tmpOne.Clear();
                tmpOne.Add(finalWorldPos);
                GenDraw.DrawFieldEdges(tmpOne, ghostColor);
            }
        }

        // Draw buildings if mode allows
        if (placeMode == PlaceMode.Buildings)
        {
            foreach (var (thingData, cell) in blueprint.GetThings())
            {
                if (thingData.def == null || !thingData.def.BuildableByPlayer)
                    continue;

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var mirroredPosition = GetMirroredPosition(adjustedPosition);
                var finalWorldPos = mirroredPosition + center;

                if (!finalWorldPos.InBounds(map))
                    continue;

                var thingRotInt = (int)thingData.relativeRotation;
                var finalRotInt = (thingRotInt + currentRotation.AsInt) & 3;
                var finalRot = new Rot4(finalRotInt);

                canPlaceBld = GenConstruct.CanPlaceBlueprintAt(thingData.def, finalWorldPos, finalRot, map).Accepted;
                var ghostColor = canPlaceBld ? new Color(1f, 1f, 1f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);

                // Reuse buffer for occupied cells
                tmpCells.Clear();
                foreach (var iv in GenAdj.CellsOccupiedBy(finalWorldPos, finalRot, thingData.def.Size))
                    tmpCells.Add(iv);

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
                        GenDraw.DrawFieldEdges(tmpCells, ghostColor);
                    }
                }
                else
                {
                    GenDraw.DrawFieldEdges(tmpCells, ghostColor);
                }
            }
        }
    }
}