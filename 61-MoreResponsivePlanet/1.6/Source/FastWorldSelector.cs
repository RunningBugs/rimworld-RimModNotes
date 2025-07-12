using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld.Planet;
using RimWorld;

namespace MoreResponsivePlanet
{
    public class FastWorldSelector
    {
        private static FastWorldSelector _instance;
        public static FastWorldSelector Instance => _instance ??= new FastWorldSelector();

        private FastWorldDragBox _fastDragBox;
        private SelectionProcessor _selectionProcessor;

        public FastWorldSelector()
        {
            _fastDragBox = FastWorldDragBox.Instance;
            _selectionProcessor = SelectionProcessor.Instance;
        }

        public void FastWorldSelectorOnGUI(WorldSelector originalSelector)
        {
            HandleFastWorldClicks(originalSelector);
            
            // Handle cancel key
            if (KeyBindingDefOf.Cancel.KeyDownEvent && originalSelector.SelectedObjects.Count > 0)
            {
                originalSelector.ClearSelection();
                Event.current.Use();
            }

            // Always update our drag box for immediate rendering
            if (_fastDragBox.IsDragging)
            {
                _fastDragBox.UpdateDrag();
            }

            // Render our drag box here in the proper GUI context
            _fastDragBox.FastDragBoxOnGUI();
        }

        private void HandleFastWorldClicks(WorldSelector originalSelector)
        {
            if (Event.current.type == EventType.MouseDown)
            {
                if (Event.current.button == 0) // Left mouse button
                {
                    if (Event.current.clickCount == 1)
                    {
                        // Start our fast drag box
                        _fastDragBox.StartDrag(UI.MousePositionOnUIInverted);
                        
                        // MonoBehaviour disabled for now
                        // if (FastDragBoxRenderer.Instance != null)
                        // {
                        //     FastDragBoxRenderer.Instance.StartDrag();
                        // }
                        
                        // Explicitly disable the original drag box to prevent interference
                        originalSelector.dragBox.active = false;
                    }
                    else if (Event.current.clickCount == 2)
                    {
                        // Handle double-click - select all matching objects
                        SelectAllMatchingObjectUnderMouseOnScreen(originalSelector);
                    }
                    Event.current.Use();
                }
                // Don't handle right-clicks here - let the original system handle them
                // This prevents interference with drag performance
            }
            else if (Event.current.rawType == EventType.MouseUp)
            {
                if (Event.current.button == 0 && _fastDragBox.IsDragging)
                {
                    // End drag and process selection
                    bool wasValidDrag = _fastDragBox.IsValidDrag();
                    Rect dragRect = _fastDragBox.CurrentDragRect;

                    _fastDragBox.EndDrag();
                    
                    // Ensure original drag box stays disabled
                    originalSelector.dragBox.active = false;

                    // Process selection based on drag validity
                    if (!wasValidDrag)
                    {
                        // Single click
                        _selectionProcessor.ProcessSingleClick(originalSelector);
                    }
                    else
                    {
                        // Drag selection - use our cached drag rect
                        _selectionProcessor.ProcessDragSelection(originalSelector, dragRect);
                    }
                }
                Event.current.Use();
            }
        }



        private void SelectAllMatchingObjectUnderMouseOnScreen(WorldSelector originalSelector)
        {
            // This mirrors the SelectAllMatchingObjectUnderMouseOnScreen method from WorldSelector
            var objectsUnderMouse = originalSelector.SelectableObjectsUnderMouse();
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
                    originalSelector.Select(allWorldObjects[i]);
                }
            }
        }


    }
}
