using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// Temporary blueprint creator (doesn't save the blueprint)
public class TemporaryBlueprintCreateDesignator : UnifiedBlueprintCreateDesignator
{
    public TemporaryBlueprintCreateDesignator()
    {
        defaultLabel = "Blueprint2.CreateTemporaryBlueprint".Translate();
        defaultDesc = "Blueprint2.CreateTemporaryBlueprintDescription".Translate();
    }

    public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
    {
        // Override to skip naming dialog and go directly to placement
        var cellsList = cells.ToList();
        if (cellsList.Count == 0) return;

        var rect = CellRect.FromLimits(cellsList.Min(c => c.x), cellsList.Min(c => c.z), cellsList.Max(c => c.x), cellsList.Max(c => c.z));
        var blueprint = CreateBlueprint(rect);
        
        if (blueprint != null)
        {
            blueprint.defName = "TempBlueprint_" + System.Guid.NewGuid().ToString("N")[..8];
            blueprint.label = "Blueprint2.TemporaryBlueprintLabel".Translate();
            
            Messages.Message("Blueprint2.TemporaryBlueprintCreated".Translate(), MessageTypeDefOf.PositiveEvent);
            
            // Start placement directly with tab-switching capability
            Find.DesignatorManager.Select(new SwitchableBlueprintPlaceDesignator(blueprint));
        }
    }
}