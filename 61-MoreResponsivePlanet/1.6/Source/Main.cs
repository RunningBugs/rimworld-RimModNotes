using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
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
            
            Log.Message("[More Responsive Planet] Mod loaded successfully!");
        }
    }

    // Harmony patch to replace WorldSelector.WorldSelectorOnGUI
    [HarmonyPatch(typeof(WorldSelector), "WorldSelectorOnGUI")]
    public static class WorldSelector_WorldSelectorOnGUI_Patch
    {
        public static bool Prefix(WorldSelector __instance)
        {
            // Handle world input
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
            else if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && selector.SelectedObjects.Count > 0)
            {
                // Handle right-click - add this without touching drag logic
                HandleRightClick(selector);
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
        
        private static void HandleRightClick(WorldSelector selector)
        {
            // Handle right-click exactly like RimWorld does
            if (selector.SelectedObjects.Count == 1 && selector.SelectedObjects[0] is Caravan caravan)
            {
                if (caravan.IsPlayerControlled && !FloatMenuMakerWorld.TryMakeFloatMenu(caravan))
                {
                    AutoOrderToTile(caravan, GenWorld.MouseTile());
                }
            }
            else
            {
                for (int i = 0; i < selector.SelectedObjects.Count; i++)
                {
                    if (selector.SelectedObjects[i] is Caravan c && c.IsPlayerControlled)
                    {
                        AutoOrderToTile(c, GenWorld.MouseTile());
                    }
                }
            }
        }
        
        private static void AutoOrderToTile(Caravan c, PlanetTile tile)
        {
            if (!tile.Valid) return;
            
            if (c.autoJoinable && CaravanExitMapUtility.AnyoneTryingToJoinCaravan(c))
            {
                CaravanExitMapUtility.OpenSomeoneTryingToJoinCaravanDialog(c, delegate
                {
                    AutoOrderToTileNow(c, tile);
                });
            }
            else
            {
                AutoOrderToTileNow(c, tile);
            }
        }
        
        private static void AutoOrderToTileNow(Caravan c, PlanetTile tile)
        {
            if (tile.Valid && (tile != c.Tile || c.pather.Moving))
            {
                PlanetTile planetTile = CaravanUtility.BestGotoDestNear(tile, c);
                if (planetTile.Valid)
                {
                    c.pather.StartPath(planetTile, null, repathImmediately: true);
                    c.gotoMote.OrderedToTile(planetTile);
                    SoundDefOf.ColonistOrdered.PlayOneShotOnCamera();
                }
            }
        }
        
        private static void ProcessSingleClickImmediate(WorldSelector selector)
        {
            // Implement full RimWorld single click logic with object cycling
            try
            {
                // First check colonist bar (like RimWorld does)
                if (Current.ProgramState == ProgramState.Playing)
                {
                    Thing thing = Find.ColonistBar.ColonistOrCorpseAt(UI.MousePositionOnUIInverted);
                    Pawn pawn = thing as Pawn;
                    if (thing != null && (pawn == null || !pawn.IsCaravanMember()))
                    {
                        if (thing.Spawned)
                        {
                            CameraJumper.TryJumpAndSelect(thing);
                        }
                        else
                        {
                            CameraJumper.TryJump(thing);
                        }
                        return;
                    }
                }
                
                bool shiftIsHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                
                // Get selectable objects under mouse with RimWorld's full logic
                var objectsList = selector.SelectableObjectsUnderMouse(out bool clickedDirectlyOnCaravan, out bool usedColonistBar).ToList();
                
                bool canSelectTile = true;
                if (usedColonistBar || (clickedDirectlyOnCaravan && objectsList.Count >= 2))
                {
                    canSelectTile = false;
                }
                
                if (objectsList.Count == 0)
                {
                    // No objects - select tile if allowed
                    if (shiftIsHeld) return;
                    
                    PlanetTile previousTile = selector.SelectedTile;
                    selector.ClearSelection();
                    
                    if (canSelectTile)
                    {
                        PlanetTile mouseTile = GenWorld.MouseTile();
                        if (mouseTile.Valid && previousTile != mouseTile)
                        {
                            selector.SelectedTile = mouseTile;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                        }
                    }
                }
                else if (objectsList.FirstOrDefault(selector.SelectedObjects.Contains) != null)
                {
                    // At least one object is already selected - cycle through them
                    if (shiftIsHeld)
                    {
                        // Shift held - deselect all selected objects under mouse
                        foreach (var obj in objectsList)
                        {
                            if (selector.SelectedObjects.Contains(obj))
                            {
                                selector.Deselect(obj);
                            }
                        }
                        return;
                    }
                    
                    PlanetTile tile = canSelectTile ? GenWorld.MouseTile() : PlanetTile.Invalid;
                    SelectFirstOrNextFrom(selector, objectsList, tile);
                }
                else
                {
                    // No objects are selected - select the first one
                    if (!shiftIsHeld)
                    {
                        selector.ClearSelection();
                    }
                    selector.Select(objectsList[0]);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in immediate single click: {ex}");
            }
        }
        
        private static void SelectFirstOrNextFrom(WorldSelector selector, List<WorldObject> objects, PlanetTile tile)
        {
            // This mirrors RimWorld's SelectFirstOrNextFrom method exactly
            int selectedIndex = objects.FindIndex(selector.SelectedObjects.Contains);
            PlanetTile tileToSelect = PlanetTile.Invalid;
            int objectIndexToSelect = -1;
            
            if (selectedIndex != -1)
            {
                // Found a selected object
                if (selectedIndex == objects.Count - 1 || selector.SelectedObjects.Count >= 2)
                {
                    // If it's the last object OR multiple objects are selected
                    if (selector.SelectedObjects.Count >= 2)
                    {
                        objectIndexToSelect = 0; // Go back to first object
                    }
                    else if (tile.Valid)
                    {
                        tileToSelect = tile; // Select the tile instead
                    }
                    else
                    {
                        objectIndexToSelect = 0; // Go back to first object
                    }
                }
                else
                {
                    // Select the next object in the list
                    objectIndexToSelect = selectedIndex + 1;
                }
            }
            else if (objects.Count == 0)
            {
                // No objects available - select tile
                tileToSelect = tile;
            }
            else
            {
                // No objects are currently selected - select first
                objectIndexToSelect = 0;
            }
            
            // Apply the selection
            selector.ClearSelection();
            if (objectIndexToSelect >= 0)
            {
                selector.Select(objects[objectIndexToSelect]);
            }
            if (tileToSelect.Valid)
            {
                selector.SelectedTile = tileToSelect;
            }
        }

        private static void SelectAllMatchingObjectUnderMouseOnScreen(WorldSelector selector)
        {
            var objectsUnderMouse = selector.SelectableObjectsUnderMouse();
            var objectsList = objectsUnderMouse.ToList();
            
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
            // Skip original method entirely - our WorldSelectorOnGUI patch handles everything
            return false;
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
