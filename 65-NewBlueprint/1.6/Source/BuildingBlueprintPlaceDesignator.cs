using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// Building blueprint placement
public class BuildingBlueprintPlaceDesignator : BlueprintPlaceDesignatorBase
{
    public BuildingBlueprintPlaceDesignator(PrefabDef prefab) : base(prefab)
    {
        defaultLabel = "Blueprint2.PlaceBlueprintWithSwitchMode".Translate(prefab.label, PlaceMode.BuildingsOnly.GetLabel());
        defaultDesc = "Blueprint2.PlaceBlueprintDescription".Translate(prefab.label, PlaceMode.BuildingsOnly.GetLabel());
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
        
        // Place building blueprints - skip ones that can't be placed
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
                            var thingDef = thingData.def as ThingDef;
                            if (thingDef != null)
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
                            // Normal mode - place blueprint
                            GenConstruct.PlaceBlueprintForBuild(thingData.def, finalWorldPos, map, finalRot, Faction.OfPlayer, thingData.stuff);
                            placedCount++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        // Log error but continue placing other blueprints
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

        var action = DebugSettings.godMode ? "spawned" : "blueprint placed";
        var message = $"Building {action}: {placedCount} items";
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
        
        // Draw buildings from the prefab - only buildable ones
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
                
                var ghostColor = canPlace ? Color.white : Color.red;
                ghostColor.a = 0.6f;
                
                // Use GenThing.TrueCenter for proper graphics positioning
                var graphicsPos = GenThing.TrueCenter(finalWorldPos, finalRot, thingData.def.size, AltitudeLayer.Blueprint.AltitudeFor());
                
                // Calculate occupied cells for outline
                var occupiedCells = GenAdj.CellsOccupiedBy(finalWorldPos, finalRot, thingData.def.size).ToList();
                
                if (thingData.def?.graphic != null && occupiedCells.Count > 0)
                {
                    var graphicType = thingData.def.graphic.GetType();
                    if (graphicType == typeof(Graphic_Cluster) || graphicType.Name.Contains("Cluster"))
                    {
                        GenDraw.DrawFieldEdges(occupiedCells, ghostColor);
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
                        }
                        catch
                        {
                            GenDraw.DrawFieldEdges(occupiedCells, ghostColor);
                        }
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