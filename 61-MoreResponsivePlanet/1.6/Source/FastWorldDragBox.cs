using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace MoreResponsivePlanet
{
    public class FastWorldDragBox
    {
        private static FastWorldDragBox _instance;
        public static FastWorldDragBox Instance => _instance ??= new FastWorldDragBox();

        // Cache the drag box state for immediate rendering
        private bool _isDragging = false;
        private Vector2 _dragStart;
        private Rect _currentDragRect;

        public void FastDragBoxOnGUI()
        {
            // Update drag rect immediately if we're dragging
            if (_isDragging)
            {
                UpdateDragRect(); // Ensure we have the latest mouse position
                
                // Draw immediately with no additional checks
                if (_currentDragRect.width > 0 && _currentDragRect.height > 0)
                {
                    Widgets.DrawBox(_currentDragRect, 2);
                }
            }
        }

        public void StartDrag(Vector2 startPos)
        {
            _isDragging = true;
            _dragStart = startPos;
            UpdateDragRect();
            Log.Message($"[FastWorldDragBox] Started drag at {startPos}");
        }

        public void UpdateDrag()
        {
            if (_isDragging)
            {
                UpdateDragRect();
            }
        }

        public void EndDrag()
        {
            _isDragging = false;
        }

        public bool IsDragging => _isDragging;

        public Rect CurrentDragRect => _currentDragRect;

        public Vector2 DragStart => _dragStart;

        private void UpdateDragRect()
        {
            if (!_isDragging) return;

            Vector2 currentMouse = UI.MousePositionOnUIInverted;
            float leftX = Mathf.Min(_dragStart.x, currentMouse.x);
            float rightX = Mathf.Max(_dragStart.x, currentMouse.x);
            float botZ = Mathf.Min(_dragStart.y, currentMouse.y);
            float topZ = Mathf.Max(_dragStart.y, currentMouse.y);

            _currentDragRect = new Rect(leftX, botZ, rightX - leftX, topZ - botZ);
        }

        public bool IsValidDrag()
        {
            if (!_isDragging) return false;
            
            Vector2 currentMouse = UI.MousePositionOnUIInverted;
            Vector2 delta = _dragStart - currentMouse;
            return delta.magnitude > 7f; // Same threshold as original WorldDragBox
        }
    }
}
