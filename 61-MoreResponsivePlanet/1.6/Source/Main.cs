using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.Sound;
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
            
            // Initialize thread-safe selection system
            UnityMainThreadDispatcher.Initialize();
            
            // Initialize input poller for reliable input detection
            InputPoller.Initialize();
            
            Log.Message("[More Responsive Planet] Mod loaded successfully!");
        }
    }

    // Harmony patch to replace WorldSelector.WorldSelectorOnGUI
    [HarmonyPatch(typeof(WorldSelector), "WorldSelectorOnGUI")]
    public static class WorldSelector_WorldSelectorOnGUI_Patch
    {
        public static bool Prefix(WorldSelector __instance)
        {
            // Set up input poller callbacks if not already done
            SetupInputCallbacks(__instance);
            
            // Handle GUI-based input as fallback
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
        
        private static bool _inputCallbacksSetup = false;
        private static void SetupInputCallbacks(WorldSelector selector)
        {
            if (_inputCallbacksSetup || InputPoller.Instance == null) return;
            
            InputPoller.Instance.OnMouseDown = () => {
                // Start drag immediately when input poller detects mouse down
                ImmediateDragBox.StartDrag(UI.MousePositionOnUIInverted);
                selector.dragBox.active = false;
            };
            
            InputPoller.Instance.OnMouseUp = () => {
                // End drag immediately when input poller detects mouse up
                if (ImmediateDragBox.IsActive)
                {
                    bool wasValidDrag = ImmediateDragBox.IsValidDrag();
                    Rect dragRect = ImmediateDragBox.GetCurrentRect();
                    int dragId = ImmediateDragBox.CurrentDragId;
                    
                    ImmediateDragBox.EndDrag();
                    selector.dragBox.active = false;
                    
                    if (!wasValidDrag)
                    {
                        ProcessSingleClickImmediate(selector);
                    }
                    else
                    {
                        ThreadSafeSelectionProcessor.Instance.ProcessDragSelectionAsync(selector, dragRect, dragId);
                    }
                }
            };
            
            InputPoller.Instance.OnDoubleClick = () => {
                // Handle double-click immediately
                SelectAllMatchingObjectUnderMouseOnScreen(selector);
            };
            
            _inputCallbacksSetup = true;
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
                    
                    // Get current drag ID BEFORE ending drag
                    int dragId = ImmediateDragBox.CurrentDragId;
                    
                    // ALWAYS end drag immediately for responsiveness
                    ImmediateDragBox.EndDrag();
                    selector.dragBox.active = false;
                    
                    // Use different processing for single clicks vs drags
                    if (!wasValidDrag)
                    {
                        // Fast synchronous processing for single clicks
                        ProcessSingleClickImmediate(selector);
                    }
                    else
                    {
                        // Async processing for drag selections (heavy)
                        ThreadSafeSelectionProcessor.Instance.ProcessDragSelectionAsync(selector, dragRect, dragId);
                    }
                }
                Event.current.Use();
            }
        }
        
        private static void ProcessSingleClickImmediate(WorldSelector selector)
        {
            // Fast synchronous single click processing - no background thread needed
            try
            {
                // Get objects under mouse (fast operation)
                var objectsUnderMouse = selector.SelectableObjectsUnderMouse();
                var objectsList = System.Linq.Enumerable.ToList(objectsUnderMouse);
                
                bool shiftIsHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                
                if (objectsList.Count > 0)
                {
                    // Clicked on a world object - immediate selection
                    var selectedObject = objectsList[0]; // Get first object
                    
                    if (!shiftIsHeld)
                    {
                        selector.ClearSelection();
                    }
                    
                    if (!selector.IsSelected(selectedObject))
                    {
                        selector.Select(selectedObject);
                    }
                }
                else
                {
                    // Clicked on empty space - select tile immediately
                    if (!shiftIsHeld)
                    {
                        PlanetTile previousTile = selector.SelectedTile;
                        selector.ClearSelection();
                        
                        PlanetTile mouseTile = GenWorld.MouseTile();
                        if (mouseTile.Valid && previousTile != mouseTile)
                        {
                            selector.SelectedTile = mouseTile;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                        }
                    }
                }
                

            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in immediate single click: {ex}");
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



    // Patch to optimize GUI during dragging while keeping world objects visible
    [HarmonyPatch(typeof(WorldInterface), "WorldInterfaceOnGUI")]
    public static class WorldInterface_WorldInterfaceOnGUI_Patch
    {
        public static bool Prefix(WorldInterface __instance)
        {
            // If we're dragging, use an optimized GUI version
            if (ImmediateDragBox.IsActive)
            {
                // Render essential elements only during drag
                RenderOptimizedWorldGUI(__instance);
                return false; // Skip the expensive original method
            }
            return true; // Normal execution when not dragging
        }
        
        private static void RenderOptimizedWorldGUI(WorldInterface worldInterface)
        {
            // During dragging, show cached screenshot + drag box for maximum responsiveness
            
            // 1. Render cached screenshot as background (shows world objects frozen in time)
            ScreenshotCache.RenderCachedScreenshot();
            
            // 2. Render our fast drag box on top
            worldInterface.selector.dragBox.DragBoxOnGUI(); // This calls our fast version
            
            // Skip ALL expensive live updates during drag:
            // - Live world objects (ExpandableWorldObjectsUtility.ExpandableWorldObjectsOnGUI)
            // - Live selection overlays (WorldSelectionDrawer.SelectionOverlaysOnGUI)
            // - Route planner (worldInterface.routePlanner.WorldRoutePlannerOnGUI)
            // - Global controls (worldInterface.globalControls.WorldGlobalControlsOnGUI)
            // - Colonist bar (Find.ColonistBar.ColonistBarOnGUI)
            // - Debug drawing (Find.WorldDebugDrawer.WorldDebugDrawerOnGUI)
            // - World gizmos (WorldGizmoUtility.WorldUIOnGUI)
            // - Landmarks (ExpandableLandmarksUtility.ExpandableLandmarksOnGUI)
        }
    }






}
