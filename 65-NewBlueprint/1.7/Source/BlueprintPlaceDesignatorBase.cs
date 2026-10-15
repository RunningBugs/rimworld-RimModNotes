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

        // Only use formal keybindings. No raw Event.current fallbacks.
        // Rotation uses vanilla bindings (Designator_RotateLeft/Right).
        if (KeyBindingDefOf.Designator_RotateLeft.KeyDownEvent)
        {
            currentRotation = currentRotation.Rotated(RotationDirection.Counterclockwise);
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }
        else if (KeyBindingDefOf.Designator_RotateRight.KeyDownEvent)
        {
            currentRotation = currentRotation.Rotated(RotationDirection.Clockwise);
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }
        // Mirroring uses our mod keybindings only.
        else if (BlueprintKeyBindingDefOf.Blueprint_MirrorX.KeyDownEvent)
        {
            mirrorX = !mirrorX;
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        }
        else if (BlueprintKeyBindingDefOf.Blueprint_MirrorZ.KeyDownEvent)
        {
            mirrorZ = !mirrorZ;
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

        if (blueprint == null)
            return;

        var mirrorStatus = "";
        if (mirrorX && mirrorZ)
            mirrorStatus = "Blueprint2.MirrorBoth".Translate();
        else if (mirrorX)
            mirrorStatus = "Blueprint2.MirrorX".Translate();
        else if (mirrorZ)
            mirrorStatus = "Blueprint2.MirrorZ".Translate();
        else
            mirrorStatus = "Blueprint2.MirrorNone".Translate();

        var rotateLeft = KeyBindingDefOf.Designator_RotateLeft?.MainKeyLabel ?? "?";
        var rotateRight = KeyBindingDefOf.Designator_RotateRight?.MainKeyLabel ?? "?";
        var mirrorXLabel = BlueprintKeyBindingDefOf.Blueprint_MirrorX?.MainKeyLabel ?? "?";
        var mirrorZLabel = BlueprintKeyBindingDefOf.Blueprint_MirrorZ?.MainKeyLabel ?? "?";

        var header = blueprint.label ?? "Blueprint2.Blueprint".Translate();
        var text = header
                   + "\n" + "Blueprint2.Rotation".Translate() + $": {rotateLeft}/{rotateRight}"
                   + "\n" + "Blueprint2.MirrorLabel".Translate() + $": {mirrorXLabel}/{mirrorZLabel}"
                   + "\n" + "Blueprint2.MirrorLabel".Translate() + $": {mirrorStatus}";

        GenUI.DrawMouseAttachment(ContentFinder<Texture2D>.Get("Blueprint2/blueprint"), text);
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