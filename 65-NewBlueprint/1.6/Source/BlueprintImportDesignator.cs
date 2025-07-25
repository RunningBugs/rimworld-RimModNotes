using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// Import blueprint designator
public class BlueprintImportDesignator : Designator
{
    public BlueprintImportDesignator()
    {
        defaultLabel = "Blueprint2.ImportBlueprint".Translate();
        defaultDesc = "Blueprint2.ImportBlueprintDescription".Translate();
        icon = ContentFinder<Texture2D>.Get("Blueprint2/blueprint");
        isOrder = true;
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 loc) => false;
    public override void DesignateSingleCell(IntVec3 c) { }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        
        // Try to import a single blueprint first
        var importedPrefab = BlueprintClipboard.ImportFromClipboard();
        if (importedPrefab != null)
        {
            HandleImportedBlueprint(importedPrefab);
            return;
        }
        
        // Try to import multiple blueprints
        var importedPrefabs = BlueprintClipboard.ImportAllBlueprintsFromClipboard();
        if (importedPrefabs != null && importedPrefabs.Count > 0)
        {
            HandleImportedBlueprints(importedPrefabs);
            return;
        }
        
        // If both failed, show a single consolidated message
        var clipboardContent = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(clipboardContent))
        {
            Messages.Message("Blueprint2.ClipboardIsEmpty".Translate(), MessageTypeDefOf.RejectInput);
        }
        else
        {
            Messages.Message("Blueprint2.NoValidBlueprintsFound".Translate(), MessageTypeDefOf.RejectInput);
        }
    }
    
    private void HandleImportedBlueprint(PrefabDef importedPrefab)
    {
        var hasThings = importedPrefab.GetThings().Any();
        var hasTerrain = importedPrefab.GetTerrain().Any();
        
        if (!hasThings && !hasTerrain)
        {
            Messages.Message("Blueprint2.EmptyBlueprintImported".Translate(), MessageTypeDefOf.RejectInput);
            return;
        }
        
        // Check for naming conflicts
        if (BlueprintCreateDesignatorBase.savedUnifiedBlueprints.ContainsKey(importedPrefab.defName))
        {
            // Show conflict dialog
            var existingBlueprints = new Dictionary<string, PrefabDef>
            {
                { importedPrefab.defName, BlueprintCreateDesignatorBase.savedUnifiedBlueprints[importedPrefab.defName] }
            };
            var importingBlueprints = new List<PrefabDef> { importedPrefab };
            Find.WindowStack.Add(new Dialog_BlueprintImportConflicts(importingBlueprints, existingBlueprints));
        }
        else
        {
            // No conflict, store directly
            BlueprintCreateDesignatorBase.savedUnifiedBlueprints[importedPrefab.defName] = importedPrefab;
            Messages.Message("Blueprint2.BlueprintImportedSuccessfully".Translate(importedPrefab.label), MessageTypeDefOf.PositiveEvent);
            OfferPlacementOptions(importedPrefab);
        }
    }
    
    private void HandleImportedBlueprints(List<PrefabDef> importedPrefabs)
    {
        // Check for naming conflicts
        var existingBlueprints = new Dictionary<string, PrefabDef>();
        var conflictingBlueprints = new List<PrefabDef>();
        
        foreach (var prefab in importedPrefabs)
        {
            if (BlueprintCreateDesignatorBase.savedUnifiedBlueprints.ContainsKey(prefab.defName))
            {
                existingBlueprints[prefab.defName] = BlueprintCreateDesignatorBase.savedUnifiedBlueprints[prefab.defName];
                conflictingBlueprints.Add(prefab);
            }
            else
            {
                // No conflict, store directly
                BlueprintCreateDesignatorBase.savedUnifiedBlueprints[prefab.defName] = prefab;
            }
        }
        
        if (conflictingBlueprints.Count > 0)
        {
            // Show conflict dialog for conflicting blueprints
            Find.WindowStack.Add(new Dialog_BlueprintImportConflicts(conflictingBlueprints, existingBlueprints));
        }
        
        Messages.Message("Blueprint2.BlueprintsImportedWithConflicts".Translate(importedPrefabs.Count, conflictingBlueprints.Count), MessageTypeDefOf.PositiveEvent);
        
        // Offer placement options for the first blueprint if it has content
        if (importedPrefabs.Count > 0)
        {
            var firstPrefab = importedPrefabs[0];
            var hasThings = firstPrefab.GetThings().Any();
            var hasTerrain = firstPrefab.GetTerrain().Any();
            if (hasThings || hasTerrain)
            {
                OfferPlacementOptions(firstPrefab);
            }
        }
    }
    
    private void OfferPlacementOptions(PrefabDef prefab)
    {
        var hasThings = prefab.GetThings().Any();
        var hasTerrain = prefab.GetTerrain().Any();
        
        // Offer placement options based on what's available
        var options = new List<FloatMenuOption>();
        
        if (hasThings && hasTerrain)
        {
            options.Add(new FloatMenuOption("Place Terrain Only", () => {
                Find.DesignatorManager.Select(new UnifiedBlueprintPlaceDesignator(prefab, PlaceMode.TerrainOnly));
            }));
            options.Add(new FloatMenuOption("Place Buildings Only", () => {
                Find.DesignatorManager.Select(new UnifiedBlueprintPlaceDesignator(prefab, PlaceMode.BuildingsOnly));
            }));
        }
        else if (hasTerrain)
        {
            options.Add(new FloatMenuOption("Place Terrain", () => {
                Find.DesignatorManager.Select(new UnifiedBlueprintPlaceDesignator(prefab, PlaceMode.TerrainOnly));
            }));
        }
        else if (hasThings)
        {
            options.Add(new FloatMenuOption("Place Buildings", () => {
                Find.DesignatorManager.Select(new UnifiedBlueprintPlaceDesignator(prefab, PlaceMode.BuildingsOnly));
            }));
        }
        
        if (options.Count == 1)
        {
            // Auto-select if only one option
            options[0].action();
        }
        else if (options.Count > 1)
        {
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}