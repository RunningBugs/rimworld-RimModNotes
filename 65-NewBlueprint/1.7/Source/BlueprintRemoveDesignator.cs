using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

public class BlueprintRemoveDesignator : Designator
{
    public BlueprintRemoveDesignator()
    {
        defaultLabel = "Blueprint2.RemoveBlueprint".Translate();
        defaultDesc = "Blueprint2.RemoveBlueprintDescription".Translate();
        icon = ContentFinder<Texture2D>.Get("Blueprint2/blueprint");
        isOrder = true;
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 loc) => false;
    public override void DesignateSingleCell(IntVec3 c) { }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        ShowRemoveBlueprintDialog();
    }

    private void ShowRemoveBlueprintDialog()
    {
        var options = new List<FloatMenuOption>();
        
        var allBlueprints = new Dictionary<string, PrefabDef>();
        
        foreach (var kvp in BlueprintCreateDesignatorBase.savedUnifiedBlueprints)
        {
            allBlueprints[kvp.Key] = kvp.Value;
        }
        
        foreach (var kvp in BlueprintCreateDesignatorBase.savedBuildingBlueprints)
        {
            if (!allBlueprints.ContainsKey(kvp.Key))
                allBlueprints[kvp.Key] = kvp.Value;
        }
        
        foreach (var kvp in BlueprintCreateDesignatorBase.savedTerrainBlueprints)
        {
            if (!allBlueprints.ContainsKey(kvp.Key))
                allBlueprints[kvp.Key] = kvp.Value;
        }
        
        if (allBlueprints.Count == 0)
        {
            options.Add(new FloatMenuOption("Blueprint2.NoBlueprintsToRemove".Translate(), null));
        }
        else
        {
            foreach (var kvp in allBlueprints)
            {
                var blueprint = kvp.Value;
                var blueprintKey = kvp.Key;
                var option = new FloatMenuOption(
                    blueprint.label ?? blueprint.defName,
                    () => {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            "Blueprint2.ConfirmRemoveBlueprint".Translate(blueprint.label ?? blueprint.defName),
                            () => RemoveBlueprint(blueprintKey, blueprint)
                        ));
                    }
                );
                options.Add(option);
            }
        }
        
        Find.WindowStack.Add(new FloatMenu(options));
    }
    
    private void RemoveBlueprint(string key, PrefabDef blueprint)
    {
        var removed = false;
        
        if (BlueprintCreateDesignatorBase.savedUnifiedBlueprints.ContainsKey(key))
        {
            BlueprintCreateDesignatorBase.savedUnifiedBlueprints.Remove(key);
            removed = true;
        }
        
        if (BlueprintCreateDesignatorBase.savedBuildingBlueprints.ContainsKey(key))
        {
            BlueprintCreateDesignatorBase.savedBuildingBlueprints.Remove(key);
            removed = true;
        }
        
        if (BlueprintCreateDesignatorBase.savedTerrainBlueprints.ContainsKey(key))
        {
            BlueprintCreateDesignatorBase.savedTerrainBlueprints.Remove(key);
            removed = true;
        }
        
        if (removed)
        {
            Messages.Message("Blueprint2.BlueprintRemoved".Translate(blueprint.label ?? blueprint.defName), MessageTypeDefOf.PositiveEvent);
        }
        else
        {
            Messages.Message("Blueprint2.FailedToRemoveBlueprint".Translate(), MessageTypeDefOf.RejectInput);
        }
    }
}