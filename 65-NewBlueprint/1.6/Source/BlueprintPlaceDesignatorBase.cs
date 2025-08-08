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
    protected bool mirrorX = false;
    protected bool mirrorZ = false;

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

        // Handle rotation using RimWorld keybindings (with legacy fallback)
        if (KeyBindingDefOf.Designator_RotateLeft.KeyDownEvent ||
            (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Q))
        {
            currentRotation = currentRotation.Rotated(RotationDirection.Counterclockwise);
            Event.current.Use();
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }
        else if (KeyBindingDefOf.Designator_RotateRight.KeyDownEvent ||
                 (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.E))
        {
            currentRotation = currentRotation.Rotated(RotationDirection.Clockwise);
            Event.current.Use();
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }
        // Handle mirroring using custom keybindings (with legacy fallback)
        else if (BlueprintKeyBindingDefOf.Blueprint_MirrorX.KeyDownEvent ||
                 (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.X))
        {
            mirrorX = !mirrorX;
            Event.current.Use();
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        }
        else if (BlueprintKeyBindingDefOf.Blueprint_MirrorZ.KeyDownEvent ||
                 (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Z))
        {
            mirrorZ = !mirrorZ;
            Event.current.Use();
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
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
            var mirrorStatus = "";
            if (mirrorX && mirrorZ)
                mirrorStatus = "Blueprint2.MirrorBoth".Translate();
            else if (mirrorX)
                mirrorStatus = "Blueprint2.MirrorX".Translate();
            else if (mirrorZ)
                mirrorStatus = "Blueprint2.MirrorZ".Translate();
            else
                mirrorStatus = "Blueprint2.MirrorNone".Translate();

            // Keep existing localized message for backward compatibility
            var baseText = "Blueprint2.PressQERotateXZMirror".Translate(blueprint.label, currentRotation.ToString(), mirrorStatus);

            // Append dynamic keybinding labels so users see their actual bindings
            var rotateLeft = KeyBindingDefOf.Designator_RotateLeft?.MainKeyLabel ?? "Q";
            var rotateRight = KeyBindingDefOf.Designator_RotateRight?.MainKeyLabel ?? "E";
            var mirrorXLabel = BlueprintKeyBindingDefOf.Blueprint_MirrorX?.MainKeyLabel ?? "X";
            var mirrorZLabel = BlueprintKeyBindingDefOf.Blueprint_MirrorZ?.MainKeyLabel ?? "Z";
            var switchModeLabel = BlueprintKeyBindingDefOf.Blueprint_SwitchMode?.MainKeyLabel ?? "R";

            var text = baseText
                       + "\n"
                       + $"Rotate: {rotateLeft}/{rotateRight}  "
                       + $"Mirror: {mirrorXLabel}/{mirrorZLabel}  "
                       + $"Mode: {switchModeLabel}";

            GenUI.DrawMouseAttachment(ContentFinder<Texture2D>.Get("Blueprint2/blueprint"), text);
        }
    }

    protected IntVec3 GetMirroredPosition(IntVec3 position)
    {
        var result = position;
        if (mirrorX)
            result.x = -result.x;
        if (mirrorZ)
            result.z = -result.z;
        return result;
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

}