using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AAT;


[HarmonyPatch(typeof(Thing), "GetGizmos")]
public static class Thing_GetGizmos_Patch
{
    public static ThingDef defToSelect = null;
    public static ThingDef stuffToSelect = null;

    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Thing __instance)
    {
        List<Gizmo> gizmos = new(__result);

        if (__instance.Spawned && __instance.MapHeld != null)
        {
            if (Find.Selector.NumSelected == 1)
            {
                defToSelect = __instance.def;
                stuffToSelect = __instance.Stuff;
                gizmos.Add(new Designator_SelectSimilar());
            }
            else if (Find.Selector.NumSelected > 1)
            {
                if (Find.Selector.SelectedObjects.OfType<Thing>().All(t => t.def == __instance.def && t.Stuff == __instance.Stuff))
                {
                    defToSelect = __instance.def;
                    stuffToSelect = __instance.Stuff;
                    gizmos.Add(new Designator_SelectSimilar());
                }
            }
        }

        return gizmos;
    }
}



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

        var thingValid = t.def != null &&
                   t.def.selectable &&
                   t.def.label != null &&
                   t.Spawned &&
                   !t.Fogged();

        return thingValid && t.def == Thing_GetGizmos_Patch.defToSelect && t.Stuff == Thing_GetGizmos_Patch.stuffToSelect;
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
