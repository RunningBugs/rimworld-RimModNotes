using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AAT;

public class SelectSimilarDefOf
{
    public static DesignationDef SelectSimilarDesignation;
}

public class Designator_SelectSimilar : Designator
{
    public override DesignationDef Designation => SelectSimilarDefOf.SelectSimilarDesignation;
    public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

    public Designator_SelectSimilar()
    {
        defaultLabel = "DesignatorSelectSimilar".Translate();
        defaultDesc = "DesignatorSelectSimilarDesc".Translate();
        icon = ContentFinder<Texture2D>.Get("selectSimilar", true);
        useMouseIcon = true;
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundSucceeded = SoundDefOf.Designate_DragStandard_Changed;
        hasDesignateAllFloatMenuOption = true;
        designateAllLabel = "DesignatorSelectSimilarAll".Translate();
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 loc)
    {
        return loc.InBounds(Map);
    }

    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        var selector = Find.Selector;

        if (selector.SelectedObjects.Count == 0)
        {
            return false; // No things selected, cannot select similar
        }

        ThingDef def = null;
        foreach (var thing in selector.SelectedObjects.OfType<Thing>())
        {
            if (def != null && thing.def != def)
            {
                return false; // Different types of things selected
            }
            def = thing.def;
        }

        var thingValid = t.def != null &&
                   t.def.selectable &&
                   t.def.label != null &&
                   t.Spawned &&
                   !t.Fogged();

        return thingValid && t.def == def;
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        var things = Map.thingGrid.ThingsListAtFast(c);
        DesignateMultiThing(things);
    }

    private void DesignateMultiThing(IEnumerable<Thing> things)
    {
        foreach (var thing in things)
        {
            DesignateThing(thing);
        }
    }

    public override void DesignateThing(Thing thing)
    {
        if (CanDesignateThing(thing))
        {
            TrySelectThing(thing);
        }
    }

    private bool TrySelectThing(Thing thing)
    {
        var selector = Find.Selector;
        if (selector.IsSelected(thing) || !CanDesignateThing(thing))
        {
            return false;
        }

        selector.SelectedObjects.Add(thing);
        SelectionDrawer.Notify_Selected(thing);
        return true;
    }

    public override void SelectedUpdate()
    {
        GenUI.RenderMouseoverBracket();
    }
}
