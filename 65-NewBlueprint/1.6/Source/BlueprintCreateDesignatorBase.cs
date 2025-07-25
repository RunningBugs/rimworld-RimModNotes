using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// Base class for blueprint creation
public abstract class BlueprintCreateDesignatorBase : Designator_Cells
{
    public static readonly Dictionary<string, PrefabDef> savedTerrainBlueprints = new();
    public static readonly Dictionary<string, PrefabDef> savedBuildingBlueprints = new();
    public static readonly Dictionary<string, PrefabDef> savedUnifiedBlueprints = new();
    
    public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

    public override AcceptanceReport CanDesignateCell(IntVec3 loc)
    {
        return loc.IsValid && loc.InBounds(Find.CurrentMap);
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        DesignateMultiCell([c]);
    }

    protected abstract PrefabDef CreateBlueprint(CellRect rect);
    protected abstract string GetBlueprintType();
    protected abstract Dictionary<string, PrefabDef> GetBlueprintStorage();

    public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
    {
        var cellList = cells.ToList();
        if (cellList.Count == 0)
            return;

        var rect = CellRect.FromLimits(cellList.Min(c => c.x), cellList.Min(c => c.z), cellList.Max(c => c.x), cellList.Max(c => c.z));
        var map = Find.CurrentMap;
        
        if (rect.Area == 0)
            return;

        var blueprint = CreateBlueprint(rect);
        if (blueprint != null)
        {
            string blueprintName = $"{GetBlueprintType().Replace(" ", "")}_{rect.Width}x{rect.Height}_{Find.TickManager.TicksGame}";
            blueprint.defName = blueprintName;
            blueprint.label = $"{GetBlueprintType()} ({rect.Width}x{rect.Height})";
            
            GetBlueprintStorage()[blueprintName] = blueprint;
            
            Messages.Message("BlueprintCreated".Translate(GetBlueprintType(), blueprint.label), MessageTypeDefOf.PositiveEvent);
            
            if (GetBlueprintType() == "Terrain Blueprint")
                Find.DesignatorManager.Select(new TerrainBlueprintPlaceDesignator(blueprint));
            else
                Find.DesignatorManager.Select(new BuildingBlueprintPlaceDesignator(blueprint));
        }
    }
}