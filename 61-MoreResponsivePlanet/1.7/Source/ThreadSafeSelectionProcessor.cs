using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld.Planet;
using RimWorld;

namespace MoreResponsivePlanet
{
    public class SingleClickResult
    {
        public WorldObject SelectedObject { get; set; }
        public bool ClickedOnObject { get; set; }
        public PlanetTile SelectedTile { get; set; }
    }

    public class SelectionCommand
    {
        public List<WorldObject> ObjectsToSelect { get; set; } = new List<WorldObject>();
        public List<WorldObject> ObjectsToDeselect { get; set; } = new List<WorldObject>();
        public PlanetTile? TileToSelect { get; set; }
        public bool ClearAllFirst { get; set; }
    }

    public class SelectionTask
    {
        public int DragId { get; set; }
        public bool IsDragSelection { get; set; }
        public WorldSelector WorldSelector { get; set; }
        public Rect DragRect { get; set; }
        public PlanetTile MouseTile { get; set; }
        public volatile bool IsCancelled = false;
    }

    public class ThreadSafeSelectionProcessor
    {
        private static ThreadSafeSelectionProcessor _instance;
        public static ThreadSafeSelectionProcessor Instance => _instance ??= new ThreadSafeSelectionProcessor();

        private volatile int _lastAppliedDragId = -1;
        
        // Simple task management - only need lock for task handoff between threads
        private Thread _backgroundThread;
        private volatile bool _backgroundThreadRunning = false;
        private readonly object _taskLock = new object();
        private volatile SelectionTask _currentTask = null;

        public void ProcessDragSelectionAsync(WorldSelector worldSelector, Rect dragRect, int dragId)
        {
            var task = new SelectionTask
            {
                DragId = dragId,
                IsDragSelection = true,
                WorldSelector = worldSelector,
                DragRect = dragRect
            };
            
            SubmitTask(task);
        }

        public void ProcessSingleClickAsync(WorldSelector worldSelector, int dragId)
        {
            // Capture mouse tile on main thread since GenWorld.MouseTile() needs main thread
            PlanetTile mouseTile = GenWorld.MouseTile();
            
            var task = new SelectionTask
            {
                DragId = dragId,
                IsDragSelection = false,
                WorldSelector = worldSelector,
                MouseTile = mouseTile
            };
            
            SubmitTask(task);
        }

        private void SubmitTask(SelectionTask newTask)
        {
            lock (_taskLock)
            {
                // Cancel current task immediately
                if (_currentTask != null)
                {
                    _currentTask.IsCancelled = true;

                }
                
                // Set new task
                _currentTask = newTask;
                
                // Start background thread if not running
                if (!_backgroundThreadRunning)
                {
                    StartBackgroundThread();
                }
            }
        }

        private void StartBackgroundThread()
        {
            if (_backgroundThread != null && _backgroundThread.IsAlive)
            {
                return; // Already running
            }
            
            _backgroundThreadRunning = true;
            _backgroundThread = new Thread(BackgroundThreadWorker)
            {
                Name = "SelectionProcessor",
                IsBackground = true
            };
            _backgroundThread.Start();
            

        }

        private void BackgroundThreadWorker()
        {
            try
            {
                while (_backgroundThreadRunning)
                {
                    SelectionTask taskToProcess = null;
                    
                    lock (_taskLock)
                    {
                        if (_currentTask != null && !_currentTask.IsCancelled)
                        {
                            taskToProcess = _currentTask;
                            _currentTask = null; // Clear current task
                        }
                    }
                    
                    if (taskToProcess != null)
                    {
                        ProcessTask(taskToProcess);
                    }
                    else
                    {
                        // No work to do, sleep briefly
                        Thread.Sleep(1);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Background thread error: {ex}");
            }
            finally
            {
                _backgroundThreadRunning = false;

            }
        }

        private void ProcessTask(SelectionTask task)
        {
            if (task.IsCancelled)
            {

                return;
            }
            
            if (task.IsDragSelection)
            {
                ProcessDragSelectionBackground(task.WorldSelector, task.DragRect, task.DragId);
            }
            else
            {
                ProcessSingleClickBackground(task.WorldSelector, task.DragId, task.MouseTile);
            }
        }

        private void ProcessDragSelectionBackground(WorldSelector worldSelector, Rect dragRect, int dragId)
        {
            try
            {


                // Create local copies of world data to avoid main thread interference
                var worldObjectsCopy = CopyWorldObjects();
                var selectedObjectsCopy = CopySelectedObjects(worldSelector);

                // Check if this drag is still valid
                if (dragId != ImmediateDragBox.CurrentDragId)
                {

                    return;
                }

                // Process selection using copied data (single background thread - no lock needed)
                var newSelection = CalculateDragSelection(dragRect, worldObjectsCopy, selectedObjectsCopy);

                // Check again before preparing final results
                if (dragId != ImmediateDragBox.CurrentDragId)
                {
                    return;
                }

                // Prepare optimized selection command on background thread
                var selectionCommand = PrepareSelectionCommand(selectedObjectsCopy, newSelection);

                // Apply minimal command on main thread (NO LOCK - never block main thread)
                UnityMainThreadDispatcher.Instance.Enqueue(() => ApplySelectionCommandNonBlocking(worldSelector, selectionCommand, dragId));
            }
            catch (Exception ex)
            {
                Log.Error($"Error in drag selection processing: {ex}");
            }
        }

        private void ProcessSingleClickBackground(WorldSelector worldSelector, int dragId, PlanetTile mouseTile)
        {
            try
            {


                // Create local copies of world data
                var worldObjectsCopy = CopyWorldObjects();

                // Check if this drag is still valid
                if (dragId != ImmediateDragBox.CurrentDragId)
                {

                    return;
                }

                // Create local copies of world data
                var selectedObjectsCopy = CopySelectedObjects(worldSelector);

                // Process single click using copied data (single background thread - no lock needed)
                var clickResult = CalculateSingleClickSelection(worldObjectsCopy);
                clickResult.SelectedTile = mouseTile; // Use the captured tile

                // Check again before preparing final results
                if (dragId != ImmediateDragBox.CurrentDragId)
                {
                    return;
                }

                // Prepare optimized selection command on background thread
                var selectionCommand = PrepareSingleClickCommand(selectedObjectsCopy, clickResult);

                // Apply minimal command on main thread (NO LOCK - never block main thread)
                UnityMainThreadDispatcher.Instance.Enqueue(() => ApplySelectionCommandNonBlocking(worldSelector, selectionCommand, dragId));
            }
            catch (Exception ex)
            {
                Log.Error($"Error in single click processing: {ex}");
            }
        }

        private List<WorldObject> CopyWorldObjects()
        {
            // Create a snapshot of world objects (this should be called from main thread initially)
            return Find.WorldObjects.AllWorldObjects.ToList();
        }

        private List<WorldObject> CopySelectedObjects(WorldSelector worldSelector)
        {
            return worldSelector.SelectedObjects.ToList();
        }

        private List<WorldObject> CalculateDragSelection(Rect dragRect, List<WorldObject> worldObjects, List<WorldObject> currentSelection)
        {
            // This mirrors the original SelectInsideDragBox logic but works on copied data
            var newSelection = new List<WorldObject>();

            bool shiftIsHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (!shiftIsHeld)
            {
                // Clear selection if shift not held
                newSelection.Clear();
            }
            else
            {
                // Keep current selection if shift held
                newSelection.AddRange(currentSelection);
            }

            // Find objects in drag rectangle
            var objectsInRect = worldObjects.Where(obj => 
                obj.SelectableNow && 
                IsObjectInRect(obj, dragRect) &&
                !obj.HiddenBehindTerrainNow()).ToList();

            // Prioritize caravans
            if (objectsInRect.Any(x => x is Caravan))
            {
                objectsInRect = objectsInRect.Where(x => x is Caravan).ToList();
                if (objectsInRect.Any(x => x.Faction == Faction.OfPlayer))
                {
                    objectsInRect = objectsInRect.Where(x => x.Faction == Faction.OfPlayer).ToList();
                }
            }

            // Add objects to selection
            foreach (var obj in objectsInRect)
            {
                if (!newSelection.Contains(obj) && newSelection.Count < 80)
                {
                    newSelection.Add(obj);
                }
            }

            return newSelection;
        }

        private SingleClickResult CalculateSingleClickSelection(List<WorldObject> worldObjects)
        {
            // This mirrors the original SelectUnderMouse logic
            Vector2 mousePos = UI.MousePositionOnUIInverted;
            
            var objectsUnderMouse = worldObjects.Where(obj => 
                obj.SelectableNow && 
                IsObjectUnderMouse(obj, mousePos) &&
                !obj.HiddenBehindTerrainNow()).ToList();

            var result = new SingleClickResult();
            
            if (objectsUnderMouse.Count > 0)
            {
                result.SelectedObject = objectsUnderMouse.FirstOrDefault();
                result.ClickedOnObject = true;
            }
            else
            {
                // Clicked on empty space - select tile
                result.SelectedObject = null;
                result.ClickedOnObject = false;
                // SelectedTile will be set from the captured value
            }

            return result;
        }

        private bool IsObjectInRect(WorldObject obj, Rect rect)
        {
            Vector2 screenPos = obj.ScreenPos();
            return rect.Contains(screenPos);
        }

        private bool IsObjectUnderMouse(WorldObject obj, Vector2 mousePos)
        {
            Vector2 screenPos = obj.ScreenPos();
            return Vector2.Distance(screenPos, mousePos) < 20f; // Reasonable click tolerance
        }

        private SelectionCommand PrepareSelectionCommand(List<WorldObject> currentSelection, List<WorldObject> newSelection)
        {
            // ALL COMPUTATION HAPPENS ON BACKGROUND THREAD
            var command = new SelectionCommand();

            // Calculate what needs to be added/removed
            var toAdd = newSelection.Except(currentSelection).ToList();
            var toRemove = currentSelection.Except(newSelection).ToList();

            if (toRemove.Count == currentSelection.Count && toAdd.Count == newSelection.Count)
            {
                // Complete replacement - more efficient to clear all first
                command.ClearAllFirst = true;
                command.ObjectsToSelect = newSelection.ToList();
            }
            else
            {
                // Incremental changes
                command.ClearAllFirst = false;
                command.ObjectsToSelect = toAdd;
                command.ObjectsToDeselect = toRemove;
            }

            return command;
        }

        private SelectionCommand PrepareSingleClickCommand(List<WorldObject> currentSelection, SingleClickResult clickResult)
        {
            // ALL COMPUTATION HAPPENS ON BACKGROUND THREAD
            var command = new SelectionCommand();

            // Cache input state on background thread (safer)
            bool shiftIsHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (clickResult.ClickedOnObject && clickResult.SelectedObject != null)
            {
                // Clicked on a world object
                if (!shiftIsHeld)
                {
                    command.ClearAllFirst = true;
                    command.ObjectsToSelect = new List<WorldObject> { clickResult.SelectedObject };
                }
                else
                {
                    // Shift-click: toggle selection
                    if (currentSelection.Contains(clickResult.SelectedObject))
                    {
                        command.ObjectsToDeselect = new List<WorldObject> { clickResult.SelectedObject };
                    }
                    else
                    {
                        command.ObjectsToSelect = new List<WorldObject> { clickResult.SelectedObject };
                    }
                }
            }
            else
            {
                // Clicked on empty space - select tile
                if (!shiftIsHeld)
                {
                    command.ClearAllFirst = true;
                    command.TileToSelect = clickResult.SelectedTile;
                }
            }

            return command;
        }

        private void ApplySelectionCommandNonBlocking(WorldSelector worldSelector, SelectionCommand command, int dragId)
        {
            // Skip if this is an older result than what we've already applied
            if (dragId <= _lastAppliedDragId)
            {

                return;
            }

            // Final check - only apply if this drag is still current
            if (dragId != ImmediateDragBox.CurrentDragId)
            {

                return;
            }

            // NO LOCK HERE - never block main thread!
            // MINIMAL OPERATIONS - all computation was done on background thread
            try
            {
                // Apply the pre-computed command with minimal main thread work
                if (command.ClearAllFirst)
                {
                    worldSelector.ClearSelection();
                }

                // Remove specific objects (minimal calls)
                foreach (var obj in command.ObjectsToDeselect)
                {
                    worldSelector.Deselect(obj);
                }

                // Add specific objects (minimal calls)
                foreach (var obj in command.ObjectsToSelect)
                {
                    worldSelector.Select(obj, playSound: false);
                }

                // Set tile if specified
                if (command.TileToSelect.HasValue)
                {
                    worldSelector.SelectedTile = command.TileToSelect.Value;
                }

                _lastAppliedDragId = dragId;

            }
            catch (Exception ex)
            {
                Log.Error($"Error applying selection command: {ex}");
            }
        }
    }
}
