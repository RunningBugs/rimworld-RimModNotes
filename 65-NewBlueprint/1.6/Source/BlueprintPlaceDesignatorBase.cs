using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// Base class for blueprint placement  
public abstract class BlueprintPlaceDesignatorBase : Designator
{
    protected readonly PrefabDef blueprint;
    protected Rot4 currentRotation = Rot4.North;

    public BlueprintPlaceDesignatorBase(PrefabDef prefab)
    {
        blueprint = prefab;
        icon = ContentFinder<Texture2D>.Get("Blueprint2/blueprint");
        soundSucceeded = SoundDefOf.Designate_Cancel;
        isOrder = true;
    }

    public override void SelectedUpdate()
    {
        GenUI.RenderMouseoverBracket();
        
        // Handle rotation input
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Q)
        {
            currentRotation = currentRotation.Rotated(RotationDirection.Counterclockwise);
            Event.current.Use();
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }
        else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.E)
        {
            currentRotation = currentRotation.Rotated(RotationDirection.Clockwise);
            Event.current.Use();
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }
        
        
        if (Find.Selector.dragBox.IsValidAndActive)
            return;
        
        var mousePos = UI.MouseMapPosition();
        if (mousePos.InBounds(Find.CurrentMap))
        {
            DrawGhost(mousePos.ToIntVec3());
        }
    }

    public override void DrawMouseAttachments()
    {
        base.DrawMouseAttachments();
        
        if (blueprint != null)
        {
            // var text = $"{blueprint.label}\nPress Q/E to rotate\nRotation: {currentRotation}";
            var text = "Blueprint2.PressQERotate".Translate(blueprint.label, currentRotation.ToString());
            GenUI.DrawMouseAttachment(ContentFinder<Texture2D>.Get("Blueprint2/blueprint"), text);
        }
    }

    protected abstract void DrawGhost(IntVec3 center);
    protected abstract AcceptanceReport CanPlaceAt(IntVec3 loc);
    protected abstract void PlaceBlueprint(IntVec3 c);

    public override AcceptanceReport CanDesignateCell(IntVec3 loc)
    {
        if (!loc.IsValid || !loc.InBounds(Find.CurrentMap))
            return false;

        return CanPlaceAt(loc);
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        if (!CanDesignateCell(c).Accepted)
            return;

        PlaceBlueprint(c);
        Finalize(true);
    }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
    }
}