using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace MoreResponsivePlanet
{
    [StaticConstructorOnStartup]
    public static class MoreResponsivePlanetMod
    {
        static MoreResponsivePlanetMod()
        {
            var harmony = new Harmony("RunningBugs.MoreResponsivePlanet");
            harmony.PatchAll();
            
            // Use simple GUI-based approach
            // ImmediateGLRenderer.Initialize();
            
            Log.Message("[More Responsive Planet] Mod loaded successfully!");
        }
    }

    // Harmony patch to replace WorldSelector.WorldSelectorOnGUI
    [HarmonyPatch(typeof(WorldSelector), "WorldSelectorOnGUI")]
    public static class WorldSelector_WorldSelectorOnGUI_Patch
    {
        public static bool Prefix(WorldSelector __instance)
        {
            // Handle input events
            HandleWorldInput(__instance);
            
            // Handle cancel key
            if (KeyBindingDefOf.Cancel.KeyDownEvent && __instance.SelectedObjects.Count > 0)
            {
                __instance.ClearSelection();
                Event.current.Use();
            }
            
            // Render our immediate drag box here
            ImmediateDragBox.RenderImmediate();
            
            return false; // Skip original method
        }
        
        private static void HandleWorldInput(WorldSelector selector)
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (Event.current.clickCount == 1)
                {
                    // Start our drag box
                    ImmediateDragBox.StartDrag(UI.MousePositionOnUIInverted);
                    selector.dragBox.active = false; // Disable original
                }
                else if (Event.current.clickCount == 2)
                {
                    // Handle double-click
                    SelectAllMatchingObjectUnderMouseOnScreen(selector);
                }
                Event.current.Use();
            }
            else if (Event.current.rawType == EventType.MouseUp && Event.current.button == 0)
            {
                if (ImmediateDragBox.IsActive)
                {
                    bool wasValidDrag = ImmediateDragBox.IsValidDrag();
                    Rect dragRect = ImmediateDragBox.GetCurrentRect();
                    
                    ImmediateDragBox.EndDrag();
                    selector.dragBox.active = false;
                    
                    if (!wasValidDrag)
                    {
                        SelectionProcessor.Instance.ProcessSingleClick(selector);
                    }
                    else
                    {
                        SelectionProcessor.Instance.ProcessDragSelection(selector, dragRect);
                    }
                }
                Event.current.Use();
            }
        }
        
        private static void SelectAllMatchingObjectUnderMouseOnScreen(WorldSelector selector)
        {
            var objectsUnderMouse = selector.SelectableObjectsUnderMouse();
            var objectsList = System.Linq.Enumerable.ToList(objectsUnderMouse);
            
            if (objectsList.Count == 0) return;

            System.Type targetType = objectsList[0].GetType();
            var allWorldObjects = Find.WorldObjects.AllWorldObjects;
            
            for (int i = 0; i < allWorldObjects.Count; i++)
            {
                if (targetType == allWorldObjects[i].GetType() && 
                    (allWorldObjects[i] == objectsList[0] || 
                     allWorldObjects[i].AllMatchingObjectsOnScreenMatchesWith(objectsList[0])) &&
                    allWorldObjects[i].VisibleToCameraNow())
                {
                    selector.Select(allWorldObjects[i]);
                }
            }
        }
    }

    // Harmony patch to completely skip WorldDragBox.DragBoxOnGUI
    [HarmonyPatch(typeof(WorldDragBox), "DragBoxOnGUI")]
    public static class WorldDragBox_DragBoxOnGUI_Patch
    {
        public static bool Prefix()
        {
            // Skip original method entirely - we render elsewhere
            return false;
        }
    }

    // Additional patch to prevent the original selector from interfering with our drag logic
    [HarmonyPatch(typeof(WorldSelector), "HandleWorldClicks")]
    public static class WorldSelector_HandleWorldClicks_Patch
    {
        public static bool Prefix()
        {
            // Let our FastWorldSelector handle all click logic instead
            return false; // Skip original method entirely
        }
    }

    // Patch to ensure immediate mouse event handling
    [HarmonyPatch(typeof(WorldSelector), "Notify_DialogOpened")]
    public static class WorldSelector_Notify_DialogOpened_Patch
    {
        public static void Postfix()
        {
            // Stop our drag box when dialogs open
            ImmediateDragBox.EndDrag();
        }
    }

    // Patch to skip expensive operations during dragging
    [HarmonyPatch(typeof(WorldInterface), "WorldInterfaceOnGUI")]
    public static class WorldInterface_WorldInterfaceOnGUI_Patch
    {
        public static bool Prefix(WorldInterface __instance)
        {
            // If we're dragging, use a minimal GUI version
            if (ImmediateDragBox.IsActive)
            {
                // Only render essential elements during drag
                __instance.selector.dragBox.DragBoxOnGUI(); // This will call our fast version
                return false; // Skip the expensive original method
            }
            return true; // Normal execution when not dragging
        }
    }






}
