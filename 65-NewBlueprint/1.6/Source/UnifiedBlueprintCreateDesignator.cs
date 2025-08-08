using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// Unified blueprint creator that captures both terrain and buildings
public class UnifiedBlueprintCreateDesignator : BlueprintCreateDesignatorBase
{
    public UnifiedBlueprintCreateDesignator()
    {
        defaultLabel = "Blueprint2.CreateBlueprint".Translate();
        defaultDesc = "Blueprint2.CreateBlueprintDescription".Translate();
        icon = ContentFinder<Texture2D>.Get("Blueprint2/blueprint");
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        soundSucceeded = SoundDefOf.Designate_Cancel;
        useMouseIcon = true;
        isOrder = true;
    }

    protected override PrefabDef CreateBlueprint(CellRect rect)
    {
        // Create prefab with both buildings and terrain
        var blueprint = PrefabUtility.CreatePrefab(rect, copyAllThings: true, copyTerrain: true);
        
        var hasValidTerrain = false;
        var hasValidBuildings = false;
        
        // Filter out non-buildable terrain
        if (blueprint?.terrain != null)
        {
            var filteredTerrain = blueprint.terrain.Where(t =>
                t.def != null && t.def.BuildableByPlayer).ToList();
            
            blueprint.terrain.Clear();
            blueprint.terrain.AddRange(filteredTerrain);
            hasValidTerrain = filteredTerrain.Count > 0;

            // Ensure substructure (foundation-like) terrains captured by the prefab system are preserved alongside floors.
            // For any captured floor (layerable && !isFoundation), if the map had a foundation at capture time,
            // add that foundation as a terrain entry in the prefab at the same local cell.
            try
            {
                var map = Find.CurrentMap;
                if (map != null)
                {
                    // Build a cache of existing foundation groups in the prefab by def
                    var foundationGroups = new Dictionary<TerrainDef, PrefabTerrainData>();
                    foreach (var group in blueprint.terrain)
                    {
                        if (group.def is TerrainDef gDef && gDef.isFoundation)
                        {
                            if (!foundationGroups.ContainsKey(gDef))
                                foundationGroups[gDef] = group;
                            if (group.rects == null)
                                group.rects = new List<CellRect>();
                        }
                    }

                    // Collect foundation additions for floor cells
                    foreach (var (terrainData, localCell) in blueprint.GetTerrain())
                    {
                        if (terrainData.def is not TerrainDef floorDef ||
                            !floorDef.BuildableByPlayer ||
                            !floorDef.layerable ||
                            floorDef.isFoundation)
                        {
                            continue; // not a floor entry
                        }

                        var world = new IntVec3(rect.minX + localCell.x, 0, rect.minZ + localCell.z);
                        var found = map.terrainGrid?.FoundationAt(world);
                        if (found == null) continue;

                        // Get or create group for this foundation def
                        if (!foundationGroups.TryGetValue(found, out var group))
                        {
                            group = new PrefabTerrainData
                            {
                                def = found,
                                chance = 1f,
                                rects = new List<CellRect>()
                            };
                            blueprint.terrain.Add(group);
                            foundationGroups[found] = group;
                        }

                        group.rects.Add(new CellRect(localCell.x, localCell.z, 1, 1));
                        hasValidTerrain = true;
                    }
                }
            }
            catch { /* best-effort, keep prefab intact on any error */ }
        }
        
        // Filter out non-buildable buildings  
        if (blueprint?.things != null)
        {
            var filteredThings = blueprint.things.Where(t => 
                t.def != null && t.def.BuildableByPlayer).ToList();
            
            blueprint.things.Clear();
            blueprint.things.AddRange(filteredThings);
            hasValidBuildings = filteredThings.Count > 0;
        }
        
        // Return null if nothing buildable found
        if (!hasValidTerrain && !hasValidBuildings)
        {
            Messages.Message("Blueprint2.NoBuildableTerrainOrBuildings".Translate(), MessageTypeDefOf.RejectInput);
            return null;
        }
        
        return blueprint;
    }

    protected override string GetBlueprintType() => "Blueprint";
    protected override Dictionary<string, PrefabDef> GetBlueprintStorage() => savedUnifiedBlueprints;

    public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
    {
        // Override to show naming dialog instead of immediate placement
        var cellsList = cells.ToList();
        if (cellsList.Count == 0) return;

        var rect = CellRect.FromLimits(cellsList.Min(c => c.x), cellsList.Min(c => c.z), cellsList.Max(c => c.x), cellsList.Max(c => c.z));
        var blueprint = CreateBlueprint(rect);
        
        if (blueprint != null)
        {
            // Generate default name
            var defaultName = $"{"Blueprint2.Blueprint".Translate()}_{System.DateTime.Now:yyyyMMdd_HHmmss}";
            
            // Show naming dialog
            Find.WindowStack.Add(new Dialog_NameBlueprint(defaultName, (finalName) => {
                blueprint.defName = finalName;
                blueprint.label = finalName;
                
                // Save the blueprint
                GetBlueprintStorage()[blueprint.defName] = blueprint;
                Messages.Message("Blueprint2.BlueprintCreatedWithName".Translate(finalName), MessageTypeDefOf.PositiveEvent);
                
                // Start placement with tab-switching capability
                Find.DesignatorManager.Select(new SwitchableBlueprintPlaceDesignator(blueprint));
            }));
        }
    }
}