using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Verse.Sound;
using System.Reflection;

/// <summary>
/// A controller to handle the drag-and-move functionality for a single pawn.
/// It mirrors the logic of the vanilla MultiPawnGotoController but is simplified for one pawn.
/// </summary>
public class SinglePawnGotoController
{
    private bool active;
    private Pawn pawn;
    private IntVec3 start;
    private IntVec3 end;

    private static readonly Material GotoCircleMaterial = MaterialPool.MatFrom("UI/Overlays/Circle75Solid", ShaderDatabase.Transparent, GenColor.FromBytes(153, 207, 135) * new Color(1f, 1f, 1f, 0.4f));
    private static readonly Material GotoBetweenLineMaterial = MaterialPool.MatFrom("UI/Overlays/ThickLine", ShaderDatabase.Transparent, GenColor.FromBytes(153, 207, 135) * new Color(1f, 1f, 1f, 0.18f));

    public bool Active => active;

    public void StartInteraction(Pawn p, IntVec3 mouseCell)
    {
        Deactivate();
        active = true;
        pawn = p;
        start = end = mouseCell;
        SoundDefOf.DragGoto.PlayOneShotOnCamera();
    }

    public void Deactivate()
    {
        active = false;
        pawn = null;
    }

    public void ProcessInputEvents()
    {
        if (!Active || pawn == null || !pawn.Spawned) return;

        IntVec3 mouseCell = UI.MouseCell();
        if (mouseCell.InBounds(pawn.Map) && mouseCell != end)
        {
            end = mouseCell;
            SoundDefOf.DragGoto.PlayOneShotOnCamera();
        }
    }

    public void FinalizeInteraction()
    {
        if (Active)
        {
            IntVec3 finalDest = RCellFinder.BestOrderedGotoDestNear(end, pawn);
            if (finalDest.IsValid)
            {
                FloatMenuOptionProvider_DraftedMove.PawnGotoAction(end, pawn, finalDest);
            }
            SoundDefOf.ColonistOrdered.PlayOneShotOnCamera();
        }
        Deactivate();
    }

    public void Draw()
    {
        if (!Active || pawn == null || !pawn.Spawned || end.Fogged(pawn.Map)) return;

        // Draw ghost pawn
        Vector3 drawLoc = end.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays.AltitudeFor());
        pawn.Drawer.renderer.RenderPawnAt(drawLoc, Rot4.South);

        // Draw circle
        Vector3 s = new Vector3(1.7f, 1f, 1.7f);
        Vector3 pos = end.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays.AltitudeFor() + 0.03658537f);
        Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, s), GotoCircleMaterial, 0);
        
        // Draw line
        // Vector3 lineStart = start.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays.AltitudeFor() - 0.03658537f);
        // Vector3 lineEnd = end.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays.AltitudeFor() - 0.03658537f);
        // GenDraw.DrawLineBetween(lineStart, lineEnd, GotoBetweenLineMaterial, 0.9f);
    }

    public void OnGUI()
    {
        if (!Active || pawn == null || !pawn.Spawned || end.Fogged(pawn.Map)) return;

        Rect rect = end.ToUIRect();
        Vector2 pos = new Vector2(rect.center.x, rect.yMax + 5f);
        GenMapUI.DrawPawnLabel(pawn, pos, 0.5f);
    }
}


[StaticConstructorOnStartup]
public static class PatchAll
{
    public static readonly SinglePawnGotoController singlePawnGotoController = new SinglePawnGotoController();

    static PatchAll()
    {
        var harmony = new Harmony("com.RunningBugs.FreeGoTo");
        harmony.PatchAll();
        Log.Message($"FreeGoTo: Patches applied successfully.".Colorize(Color.green));
    }
}

[HarmonyPatch(typeof(Selector), "HandleMapClicks")]
public static class Selector_HandleMapClicks_Patch
{
    // Cache MethodInfo for performance
    private static readonly MethodInfo m_SelectAllMatchingObjectUnderMouseOnScreen = AccessTools.Method(typeof(Selector), "SelectAllMatchingObjectUnderMouseOnScreen");
    private static readonly MethodInfo m_HandleMultiselectGoto = AccessTools.Method(typeof(Selector), "HandleMultiselectGoto");
    private static readonly MethodInfo m_SelectUnderMouse = AccessTools.Method(typeof(Selector), "SelectUnderMouse");
    private static readonly MethodInfo m_SelectInsideDragBox = AccessTools.Method(typeof(Selector), "SelectInsideDragBox");

    static bool Prefix(Selector __instance)
    {
        if (Event.current.type == EventType.MouseDown)
        {
            if (Event.current.button == 0)
            {
                if (Event.current.clickCount == 1)
                {
                    __instance.dragBox.active = true;
                    __instance.dragBox.start = UI.MouseMapPosition();
                }
                if (Event.current.clickCount == 2)
                {
                    m_SelectAllMatchingObjectUnderMouseOnScreen.Invoke(__instance, null);
                }
                Event.current.Use();
            }
            if (Event.current.button == 1 && __instance.SelectedPawns.Any())
            {
                List<FloatMenuOption> list = null;
                FloatMenuContext context = null;
                try
                {
                    list = FloatMenuMakerMap.GetOptions(__instance.SelectedPawns, UI.MouseMapPosition(), out context);
                }
                catch (System.Exception ex)
                {
                    Log.Error("Error trying to make float menu: " + ex);
                }
                if (!list.NullOrEmpty())
                {
                    FloatMenuOption autoTakeOption = FloatMenuMakerMap.GetAutoTakeOption(list);
                    
                    if (list.Count == 1 && list[0].isGoto && context.ValidSelectedPawns.Any(p => p.Drafted))
                    {
                        m_HandleMultiselectGoto.Invoke(__instance, new object[] { context });
                    }
                    else if (autoTakeOption != null)
                    {
                        autoTakeOption.Chosen(colonistOrdering: true, null);
                    }
                    else
                    {
                        string title = ((!context.IsMultiselect) ? context.FirstSelectedPawn.LabelCap : null);
                        FloatMenuMap floatMenuMap = new FloatMenuMap(list, title, UI.MouseMapPosition());
                        floatMenuMap.givesColonistOrders = true;
                        Find.WindowStack.Add(floatMenuMap);
                    }
                }
                else if (context != null && context.ValidSelectedPawns.Any(p => p.Drafted))
                {
                    m_HandleMultiselectGoto.Invoke(__instance, new object[] { context });
                }
                Event.current.Use();
            }
        }
        if (Event.current.rawType == EventType.MouseUp)
        {
            if (Event.current.button == 0)
            {
                if (__instance.dragBox.active)
                {
                    __instance.dragBox.active = false;
                    if (!__instance.dragBox.IsValid)
                    {
                        m_SelectUnderMouse.Invoke(__instance, null);
                    }
                    else
                    {
                        m_SelectInsideDragBox.Invoke(__instance, null);
                    }
                }
            }
            else if (Event.current.button == 1)
            {
                if (__instance.gotoController.Active)
                {
                    __instance.gotoController.FinalizeInteraction();
                }
                if (PatchAll.singlePawnGotoController.Active)
                {
                    PatchAll.singlePawnGotoController.FinalizeInteraction();
                }
            }
            Event.current.Use();
        }

        if ((__instance.gotoController.Active || PatchAll.singlePawnGotoController.Active) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2))
        {
            __instance.gotoController.FinalizeInteraction();
            PatchAll.singlePawnGotoController.FinalizeInteraction();
        }
        
        if (__instance.gotoController.Active)
        {
            __instance.gotoController.ProcessInputEvents();
        }
        if (PatchAll.singlePawnGotoController.Active)
        {
            PatchAll.singlePawnGotoController.ProcessInputEvents();
        }
        
        return false; // Skip original method
    }
}

[HarmonyPatch(typeof(Selector), "HandleMultiselectGoto")]
public static class Selector_HandleMultiselectGoto_Patch
{
    static bool Prefix(Selector __instance, FloatMenuContext context, List<Pawn> ___tmpDraftedGotoPawns)
    {
        ___tmpDraftedGotoPawns.Clear();
        foreach (Pawn allSelectedPawn in context.allSelectedPawns)
        {
            if (FloatMenuMakerMap.ShouldGenerateFloatMenuForPawn(allSelectedPawn).Accepted && allSelectedPawn.Drafted)
            {
                ___tmpDraftedGotoPawns.Add(allSelectedPawn);
            }
        }
        
        IntVec3 mouseCell = CellFinder.StandableCellNear(context.ClickedCell, context.map, 2.9f);
        if (!mouseCell.IsValid)
        {
            ___tmpDraftedGotoPawns.Clear();
            return false;
        }

        if (___tmpDraftedGotoPawns.Count == 1)
        {
            PatchAll.singlePawnGotoController.StartInteraction(___tmpDraftedGotoPawns[0], mouseCell);
        }
        else if (___tmpDraftedGotoPawns.Count > 1)
        {
            __instance.gotoController.StartInteraction(mouseCell);
            foreach (Pawn tmpDraftedGotoPawn in ___tmpDraftedGotoPawns)
            {
                __instance.gotoController.AddPawn(tmpDraftedGotoPawn);
            }
        }
        
        ___tmpDraftedGotoPawns.Clear();
        return false;
    }
}

[HarmonyPatch(typeof(Selector), "SelectorOnGUI_BeforeMainTabs")]
public static class Selector_OnGUI_BeforeMainTabs_Patch
{
    static void Postfix()
    {
        if (PatchAll.singlePawnGotoController.Active)
        {
            PatchAll.singlePawnGotoController.OnGUI();
        }
    }
}

[HarmonyPatch(typeof(MapInterface), "MapInterfaceUpdate")]
public static class MapInterface_Update_Patch
{
    static void Postfix()
    {
        if (PatchAll.singlePawnGotoController.Active)
        {
            PatchAll.singlePawnGotoController.Draw();
        }
    }
}

[HarmonyPatch(typeof(Selector), "ClearSelection")]
public static class Selector_ClearSelection_Patch
{
    static void Postfix()
    {
        if (PatchAll.singlePawnGotoController.Active)
        {
            PatchAll.singlePawnGotoController.Deactivate();
        }
    }
}
