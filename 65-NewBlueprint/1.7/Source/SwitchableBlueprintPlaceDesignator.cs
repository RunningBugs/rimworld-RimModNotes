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
    private PlaceMode currentMode = PlaceMode.Buildings;

    // Allocation reduction: reuse buffers for ghost drawing
    private static readonly List<IntVec3> tmpCells = new List<IntVec3>(32);
    private static readonly List<IntVec3> tmpOne = new List<IntVec3>(1);

    public SwitchableBlueprintPlaceDesignator(PrefabDef prefab) : base(prefab)
    {
        UpdateLabels();
    }

    public override void SelectedUpdate()
    {
        // Handle mode switching via binding only (no legacy raw keys)
        if (BlueprintKeyBindingDefOf.Blueprint_SwitchMode.KeyDownEvent)
        {
            currentMode = currentMode switch
            {
                PlaceMode.Buildings => PlaceMode.Bridges,
                PlaceMode.Bridges => PlaceMode.FloorOnly,
                PlaceMode.FloorOnly => PlaceMode.Buildings,
                _ => PlaceMode.Buildings
            };
            UpdateLabels();
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        base.SelectedUpdate();
    }
    
    private void UpdateLabels()
    {
        var modeText = currentMode.GetLabel();
        defaultLabel = "Blueprint2.PlaceBlueprintWithSwitchMode".Translate(blueprint.label, modeText, "Blueprint2.SwitchMode".Translate());
        defaultDesc = "Blueprint2.PlaceBlueprintDescription".Translate(blueprint.label, modeText, "Blueprint2.SwitchMode".Translate());
    }

    public override void DrawMouseAttachments()
    {
        var mirrorStatus = "";
        if (mirrorX && mirrorZ)
            mirrorStatus = "Blueprint2.MirrorBoth".Translate();
        else if (mirrorX)
            mirrorStatus = "Blueprint2.MirrorX".Translate();
        else if (mirrorZ)
            mirrorStatus = "Blueprint2.MirrorZ".Translate();
        else
            mirrorStatus = "Blueprint2.MirrorNone".Translate();

        var rotateLeft = KeyBindingDefOf.Designator_RotateLeft?.MainKeyLabel ?? "?";
        var rotateRight = KeyBindingDefOf.Designator_RotateRight?.MainKeyLabel ?? "?";
        var mirrorXLabel = BlueprintKeyBindingDefOf.Blueprint_MirrorX?.MainKeyLabel ?? "?";
        var mirrorZLabel = BlueprintKeyBindingDefOf.Blueprint_MirrorZ?.MainKeyLabel ?? "?";
        var switchModeLabel = BlueprintKeyBindingDefOf.Blueprint_SwitchMode?.MainKeyLabel ?? "?";

        var header = blueprint.label ?? "Blueprint2.Blueprint".Translate();
        var text = header
                   + "\n" + "Blueprint2.Rotation".Translate() + $": {rotateLeft}/{rotateRight}"
                   + "\n" + "Blueprint2.MirrorLabel".Translate() + $": {mirrorXLabel}/{mirrorZLabel}"
                   + "\n" + "Blueprint2.MirrorLabel".Translate() + $": {mirrorStatus}"
                   + "\n" + "Blueprint2.Mode".Translate() + $": {switchModeLabel} (" + "Blueprint2.Current".Translate() + $": {currentMode.GetLabel()})";

        GenUI.DrawMouseAttachment(ContentFinder<Texture2D>.Get("Blueprint2/blueprint"), text);
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
        if (currentMode == PlaceMode.FloorOnly)
        {
            foreach (var (terrainData, cell) in blueprint.GetTerrain())
            {
                if (terrainData.def is not TerrainDef tDef || !tDef.BuildableByPlayer)
                {
                    skippedCount++;
                    continue;
                }
                // Floors layer = only layerable non-foundation
                if (!tDef.layerable || tDef.isFoundation)
                {
                    skippedCount++;
                    continue;
                }

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var mirroredPosition = GetMirroredPosition(adjustedPosition);
                var finalWorldPos = mirroredPosition + c;

                if (finalWorldPos.InBounds(map))
                {
                    if (GenConstruct.CanPlaceBlueprintAt(tDef, finalWorldPos, currentRotation, map).Accepted)
                    {
                        try
                        {
                            if (DebugSettings.godMode)
                            {
                                try
                                {
                                    if (tDef.temporary)
                                    {
                                        map.terrainGrid.SetTempTerrain(finalWorldPos, tDef);
                                        placedCount++;
                                    }
                                    else
                                    {
                                        map.terrainGrid.SetTerrain(finalWorldPos, tDef);
                                        placedCount++;
                                    }
                                }
                                catch (System.Exception terrainEx)
                                {
                                    Log.Warning($"Failed to place terrain {tDef.defName} at {finalWorldPos}: {terrainEx.Message}");
                                    skippedCount++;
                                }
                            }
                            else
                            {
                                GenConstruct.PlaceBlueprintForBuild(tDef, finalWorldPos, map, currentRotation, Faction.OfPlayer, null);
                                placedCount++;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Warning($"Failed to place terrain for {tDef?.defName}: {ex.Message}");
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
        if (currentMode == PlaceMode.Buildings)
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

        // Place bridges (foundations) by layer:
        // 1) Place any foundation terrains captured in the prefab.
        // 2) For floor cells that have no captured foundation, synthesize using a default substructure def.
        if (currentMode == PlaceMode.Bridges)
        {
            // Collect foundation cells explicitly captured in prefab
            var capturedFoundationCells = new HashSet<IntVec3>();
            foreach (var (terrainData, cell) in blueprint.GetTerrain())
            {
                if (terrainData.def is TerrainDef tDef && tDef.BuildableByPlayer && tDef.isFoundation)
                {
                    var adj = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                    var mir = GetMirroredPosition(adj);
                    var pos = mir + c;
                    if (!pos.InBounds(map)) { skippedCount++; continue; }
                    if (!GenConstruct.CanPlaceBlueprintAt(tDef, pos, currentRotation, map).Accepted) { skippedCount++; continue; }

                    try
                    {
                        if (DebugSettings.godMode)
                        {
                            if (map.terrainGrid.FoundationAt(pos) == null && map.terrainGrid.UnderTerrainAt(pos) == null)
                            {
                                map.terrainGrid.SetFoundation(pos, tDef);
                                placedCount++;
                            }
                            else
                            {
                                skippedCount++;
                            }
                        }
                        else
                        {
                            GenConstruct.PlaceBlueprintForBuild(tDef, pos, map, currentRotation, Faction.OfPlayer, null);
                            placedCount++;
                        }
                        capturedFoundationCells.Add(pos);
                    }
                    catch
                    {
                        skippedCount++;
                    }
                }
            }

            // Synthesize foundations under captured floors that don't already have a captured foundation
            var defaultFoundation = ResolveDefaultFoundationDef();
            if (defaultFoundation != null)
            {
                foreach (var (terrainData, cell) in blueprint.GetTerrain())
                {
                    if (terrainData.def is not TerrainDef floorDef || !floorDef.BuildableByPlayer)
                        continue;

                    // Floors layer: layerable and not foundation
                    if (!floorDef.layerable || floorDef.isFoundation)
                        continue;

                    var adj = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                    var mir = GetMirroredPosition(adj);
                    var pos = mir + c;

                    if (!pos.InBounds(map)) { skippedCount++; continue; }
                    if (capturedFoundationCells.Contains(pos)) continue; // already handled

                    if (!GenConstruct.CanPlaceBlueprintAt(defaultFoundation, pos, currentRotation, map).Accepted)
                    {
                        skippedCount++;
                        continue;
                    }

                    try
                    {
                        if (DebugSettings.godMode)
                        {
                            if (map.terrainGrid.FoundationAt(pos) == null && map.terrainGrid.UnderTerrainAt(pos) == null)
                            {
                                map.terrainGrid.SetFoundation(pos, defaultFoundation);
                                placedCount++;
                            }
                            else
                            {
                                skippedCount++;
                            }
                        }
                        else
                        {
                            GenConstruct.PlaceBlueprintForBuild(defaultFoundation, pos, map, currentRotation, Faction.OfPlayer, null);
                            placedCount++;
                        }
                    }
                    catch
                    {
                        skippedCount++;
                    }
                }
            }
        }

        var action = DebugSettings.godMode ? "spawned" : "blueprint placed";
        var modeText = currentMode.GetLabel();
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

        // Precompute colors to avoid per-iteration allocations
        var terrColor = canPlace ? new Color(0.0f, 0.7f, 1.0f, 0.6f) : new Color(1.0f, 0.0f, 0.0f, 0.6f);
        var terrGlow = new Color(0.3f, 0.8f, 1.0f, 0.2f);
        var ghostColor = canPlace ? new Color(0.8f, 0.9f, 1f, 0.7f) : new Color(1f, 0.5f, 0.5f, 0.7f);
        var bldGlow = new Color(0.9f, 0.95f, 1f, 0.3f);
        var blueprintAlt = AltitudeLayer.Blueprint.AltitudeFor();

        // Draw terrain if mode allows
        if (currentMode == PlaceMode.FloorOnly)
        {
            foreach (var (terrainData, cell) in blueprint.GetTerrain())
            {
                if (terrainData.def is not TerrainDef td || !td.BuildableByPlayer)
                    continue;

                // Floors ghost = only layerable non-foundation
                if (!td.layerable || td.isFoundation)
                    continue;

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var mirroredPosition = GetMirroredPosition(adjustedPosition);
                var finalWorldPos = mirroredPosition + center;

                if (!finalWorldPos.InBounds(map))
                    continue;

                // Reuse buffer for single cell
                tmpOne.Clear();
                tmpOne.Add(finalWorldPos);
                GenDraw.DrawFieldEdges(tmpOne, terrColor);

                if (canPlace)
                {
                    GenDraw.DrawFieldEdges(tmpOne, terrGlow, blueprintAlt + 0.1f);
                }
            }
        }

        // Draw buildings if mode allows
        if (currentMode == PlaceMode.Buildings)
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

                // Reuse buffer for occupied cells
                tmpCells.Clear();
                foreach (var iv in GenAdj.CellsOccupiedBy(finalWorldPos, finalRot, thingData.def.Size))
                    tmpCells.Add(iv);

                if (thingData.def?.graphic != null && tmpCells.Count > 0)
                {
                    var graphicType = thingData.def.graphic.GetType();
                    if (graphicType == typeof(Graphic_Cluster) || graphicType.Name.Contains("Cluster"))
                    {
                        GenDraw.DrawFieldEdges(tmpCells, ghostColor);
                        if (canPlace)
                        {
                            GenDraw.DrawFieldEdges(tmpCells, bldGlow, blueprintAlt + 0.1f);
                        }
                    }
                    else
                    {
                        // Use GenThing.TrueCenter for proper graphics positioning
                        var graphicsPos = GenThing.TrueCenter(finalWorldPos, finalRot, thingData.def.Size, blueprintAlt);
                        try
                        {
                            var graphic = thingData.def.graphic.GetColoredVersion(
                                thingData.def.graphic.Shader,
                                ghostColor,
                                Color.white);

                            graphic.DrawFromDef(graphicsPos, finalRot, thingData.def, 0f);

                            if (canPlace)
                            {
                                var glowGraphic = thingData.def.graphic.GetColoredVersion(
                                    ShaderDatabase.Transparent,
                                    bldGlow,
                                    Color.white);
                                glowGraphic.DrawFromDef(graphicsPos, finalRot, thingData.def, 0f);
                            }
                        }
                        catch
                        {
                            GenDraw.DrawFieldEdges(tmpCells, ghostColor);
                            if (canPlace)
                            {
                                GenDraw.DrawFieldEdges(tmpCells, bldGlow, blueprintAlt + 0.1f);
                            }
                        }
                    }
                }
                else
                {
                    GenDraw.DrawFieldEdges(tmpCells, ghostColor);
                    if (canPlace)
                    {
                        GenDraw.DrawFieldEdges(tmpCells, bldGlow, blueprintAlt + 0.1f);
                    }
                }
            }
        }

        // Draw bridges (foundations) captured in the prefab only
        if (currentMode == PlaceMode.Bridges)
        {
            foreach (var (terrainData, cell) in blueprint.GetTerrain())
            {
                if (terrainData.def is not TerrainDef tDef || !tDef.isFoundation || !tDef.BuildableByPlayer)
                    continue;

                var adjustedPosition = PrefabUtility.GetAdjustedLocalPosition(cell, currentRotation);
                var mirroredPosition = GetMirroredPosition(adjustedPosition);
                var finalWorldPos = mirroredPosition + center;

                if (!finalWorldPos.InBounds(map))
                    continue;

                tmpOne.Clear();
                tmpOne.Add(finalWorldPos);

                var canPlaceBr = GenConstruct.CanPlaceBlueprintAt(tDef, finalWorldPos, currentRotation, map).Accepted;
                var color = canPlaceBr ? new Color(0.0f, 0.7f, 1.0f, 0.6f) : new Color(1.0f, 0.0f, 0.0f, 0.6f);
                GenDraw.DrawFieldEdges(tmpOne, color);
            }
        }
    }

    // Prefer explicit substructure if present, then heavy bridge, then bridge, then any isFoundation
    private TerrainDef ResolveDefaultFoundationDef()
    {
        var sub = DefDatabase<TerrainDef>.GetNamedSilentFail("Substructure");
        if (sub != null && sub.BuildableByPlayer) return sub;

        var heavy = DefDatabase<TerrainDef>.GetNamedSilentFail("HeavyBridge");
        if (heavy != null && heavy.BuildableByPlayer) return heavy;

        var bridge = DefDatabase<TerrainDef>.GetNamedSilentFail("Bridge");
        if (bridge != null && bridge.BuildableByPlayer) return bridge;

        foreach (var d in DefDatabase<TerrainDef>.AllDefs)
        {
            if (d != null && d.isFoundation && d.BuildableByPlayer)
                return d;
        }
        return null;
    }

}