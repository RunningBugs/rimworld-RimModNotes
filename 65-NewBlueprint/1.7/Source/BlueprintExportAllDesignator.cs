using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// Export blueprints designator
public class BlueprintExportDesignator : Designator
{
    public BlueprintExportDesignator()
    {
        defaultLabel = "Blueprint2.ExportBlueprints".Translate();
        defaultDesc = "Blueprint2.ExportBlueprintsDescription".Translate();
        icon = ContentFinder<Texture2D>.Get("Blueprint2/blueprint");
        isOrder = true;
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 loc) => false;
    public override void DesignateSingleCell(IntVec3 c) { }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        ShowExportMenu();
    }
    
    private void ShowExportMenu()
    {
        var options = new List<FloatMenuOption>();
        
        // Combine all saved blueprints
        var allBlueprints = new Dictionary<string, PrefabDef>();
        
        // Add unified blueprints
        foreach (var kvp in BlueprintCreateDesignatorBase.savedUnifiedBlueprints)
        {
            allBlueprints[kvp.Key] = kvp.Value;
        }
        
        // Add building blueprints
        foreach (var kvp in BlueprintCreateDesignatorBase.savedBuildingBlueprints)
        {
            if (!allBlueprints.ContainsKey(kvp.Key))
                allBlueprints[kvp.Key] = kvp.Value;
        }
        
        // Add terrain blueprints
        foreach (var kvp in BlueprintCreateDesignatorBase.savedTerrainBlueprints)
        {
            if (!allBlueprints.ContainsKey(kvp.Key))
                allBlueprints[kvp.Key] = kvp.Value;
        }
        
        if (allBlueprints.Count == 0)
        {
            options.Add(new FloatMenuOption("Blueprint2.NoBlueprintsToExportMenu".Translate(), null));
        }
        else
        {
            // Add option to export all blueprints
            options.Add(new FloatMenuOption("Blueprint2.ExportAllBlueprints".Translate(), () => {
                BlueprintClipboard.ExportAllBlueprintsToClipboard();
            }));
            
            // Add options to export individual blueprints
            foreach (var kvp in allBlueprints)
            {
                var blueprint = kvp.Value;
                var option = new FloatMenuOption(
                    $"{"Blueprint2.Export".Translate()}: {blueprint.label ?? blueprint.defName}",
                    () => {
                        BlueprintClipboard.ExportToClipboard(blueprint);
                    }
                );
                options.Add(option);
            }
        }
        
        Find.WindowStack.Add(new FloatMenu(options));
    }
}