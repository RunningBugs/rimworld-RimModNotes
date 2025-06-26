using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AAT;

[HarmonyPatch(typeof(ReverseDesignatorDatabase), "InitDesignators")]
public static class ReverseDesignatorDatabase_InitDesignators_HarvestPatch
{
    public static void Postfix(ReverseDesignatorDatabase __instance)
    {
        FieldInfo field = typeof(ReverseDesignatorDatabase).GetField("desList", BindingFlags.Instance | BindingFlags.NonPublic);
        List<Designator> desList = field?.GetValue(__instance) as List<Designator>;
        desList?.Add(new Designator_HarvestFullyGrown());
    }
}

[DefOf]
public static class HarvestFullyGrownDefOf
{
    public static DesignationDef HarvestFullyGrownDesignation;
}

public class Designator_HarvestFullyGrown : Designator
{
    private static readonly IntVec3 DragStartCell = IntVec3.Invalid;
    
    public override DesignationDef Designation => DesignationDefOf.HarvestPlant;
    public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

    public Designator_HarvestFullyGrown()
    {
        defaultLabel = "DesignatorHarvestFullyGrown".Translate();
        defaultDesc = "DesignatorHarvestFullyGrownDesc".Translate();
        icon = ContentFinder<Texture2D>.Get("harvestGrown", true);
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_DragStandard_Changed;
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        if (!c.InBounds(Map) || c.Fogged(Map))
        {
            return false;
        }

        List<Thing> thingList = c.GetThingList(Map);
        for (int i = 0; i < thingList.Count; i++)
        {
            if (CanDesignateThing(thingList[i]).Accepted)
            {
                return true;
            }
        }
        return false;
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        List<Thing> thingList = c.GetThingList(Map);
        for (int i = 0; i < thingList.Count; i++)
        {
            Thing thing = thingList[i];
            if (CanDesignateThing(thing).Accepted)
            {
                DesignateThing(thing);
            }
        }
    }

    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        if (t?.def?.plant == null)
        {
            return false;
        }

        Plant plant = t as Plant;
        if (plant == null)
        {
            return false;
        }

        if (plant.Map?.designationManager?.DesignationOn(plant, DesignationDefOf.HarvestPlant) != null)
        {
            return false;
        }

        if (!plant.HarvestableNow)
        {
            return false;
        }

        if (plant.LifeStage != PlantLifeStage.Mature)
        {
            return false;
        }

        if (!PlantMatchesModifierKeyFilter(plant))
        {
            return false;
        }

        return true;
    }

    public override void DesignateThing(Thing t)
    {
        if (CanDesignateThing(t).Accepted)
        {
            Plant plant = t as Plant;
            plant.Map?.designationManager?.AddDesignation(new Designation(plant, DesignationDefOf.HarvestPlant));
            plant.SetForbidden(false, false);
        }
    }

    private bool PlantMatchesModifierKeyFilter(Plant plant)
    {
        bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool controlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || 
                             Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

        if (!shiftPressed && !controlPressed)
        {
            return true;
        }

        string harvestTag = plant.def.plant.harvestTag;
        
        if (shiftPressed && !controlPressed)
        {
            return harvestTag == "Standard";
        }
        
        if (controlPressed && !shiftPressed)
        {
            return harvestTag == "Wood";
        }

        return false;
    }

    public override void SelectedUpdate()
    {
        GenUI.RenderMouseoverBracket();
        DrawDesignationsOnMap();
    }

    private void DrawDesignationsOnMap()
    {
        // Standard UI highlight rendering - simplified approach
    }

    public override void ProcessInput(Event ev)
    {
        if (ev.button == 1)
        {
            ShowContextMenu();
        }
        else
        {
            base.ProcessInput(ev);
        }
    }

    private void ShowContextMenu()
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>();

        options.Add(new FloatMenuOption(
            "HarvestFullyGrownAll".Translate(),
            () => DesignateAllFullyGrownOnMap(false),
            MenuOptionPriority.Default,
            null,
            null,
            0f,
            null,
            null));

        options.Add(new FloatMenuOption(
            "HarvestFullyGrownHome".Translate(),
            () => DesignateAllFullyGrownOnMap(true),
            MenuOptionPriority.Default,
            null,
            null,
            0f,
            null,
            null));

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void DesignateAllFullyGrownOnMap(bool homeAreaOnly)
    {
        if (Map == null) return;

        int designatedCount = 0;
        List<Thing> allPlants = Map.listerThings.ThingsInGroup(ThingRequestGroup.Plant);

        foreach (Thing thing in allPlants)
        {
            if (homeAreaOnly && (Map.areaManager.Home[thing.Position] == false))
            {
                continue;
            }

            if (CanDesignateThing(thing).Accepted)
            {
                DesignateThing(thing);
                designatedCount++;
            }
        }

        if (designatedCount > 0)
        {
            Messages.Message(
                "HarvestFullyGrownSuccess".Translate(designatedCount),
                MessageTypeDefOf.TaskCompletion,
                false);
        }
        else
        {
            Messages.Message(
                "HarvestFullyGrownFailure".Translate(),
                MessageTypeDefOf.RejectInput,
                false);
        }
    }
}