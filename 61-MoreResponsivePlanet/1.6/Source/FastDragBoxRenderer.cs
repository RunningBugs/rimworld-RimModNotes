using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace MoreResponsivePlanet
{
    public class FastDragBoxRenderer : MonoBehaviour
    {
        private static FastDragBoxRenderer _instance;
        public static FastDragBoxRenderer Instance => _instance;

        private bool _isDragging = false;
        private Vector2 _dragStart;
        private Rect _currentDragRect;
        public static void Initialize()
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("FastDragBoxRenderer");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<FastDragBoxRenderer>();
                Log.Message("[FastDragBoxRenderer] MonoBehaviour initialized");
            }
        }

        void Update()
        {
            // Just update state, don't handle input
            // Input is handled by FastWorldSelector through RimWorld's event system
        }

        void OnGUI()
        {
            // Render drag box with highest priority
            if (_isDragging && ShouldRender())
            {
                UpdateDragRect();
                DrawDragBox();
            }
        }

        private bool ShouldHandleInput()
        {
            return Find.World != null && 
                   WorldRendererUtility.WorldSelected && 
                   Find.WindowStack.Count == 0; // No dialogs open
        }

        private bool ShouldRender()
        {
            return Find.World != null && WorldRendererUtility.WorldSelected;
        }

        private void HandleInput()
        {
            // Let RimWorld handle input, we just provide fast rendering
            // Input is handled by FastWorldSelector
        }

        public void StartDrag()
        {
            _isDragging = true;
            // Use RimWorld's UI coordinate system directly
            _dragStart = UI.MousePositionOnUIInverted;
            UpdateDragRect();
            Log.Message($"[FastDragBoxRenderer] Started drag at {_dragStart}");
        }

        public void EndDrag()
        {
            if (_isDragging)
            {
                _isDragging = false;
                Log.Message("[FastDragBoxRenderer] Ended drag");
                
                // Notify our selection processor
                if (IsValidDrag())
                {
                    SelectionProcessor.Instance.ProcessDragSelection(Find.WorldSelector, _currentDragRect);
                }
                else
                {
                    SelectionProcessor.Instance.ProcessSingleClick(Find.WorldSelector);
                }
            }
        }

        public void ForceEndDrag()
        {
            _isDragging = false;
        }

        private void UpdateDragRect()
        {
            if (!_isDragging) return;

            // Use RimWorld's UI coordinate system directly
            Vector2 currentMouse = UI.MousePositionOnUIInverted;

            float leftX = Mathf.Min(_dragStart.x, currentMouse.x);
            float rightX = Mathf.Max(_dragStart.x, currentMouse.x);
            float botY = Mathf.Min(_dragStart.y, currentMouse.y);
            float topY = Mathf.Max(_dragStart.y, currentMouse.y);

            _currentDragRect = new Rect(leftX, botY, rightX - leftX, topY - botY);
        }

        private void DrawDragBox()
        {
            // Use RimWorld's widget system for consistent rendering
            Widgets.DrawBox(_currentDragRect, 2);
        }



        public bool IsValidDrag()
        {
            if (!_isDragging) return false;
            return _currentDragRect.width > 7f || _currentDragRect.height > 7f;
        }

        public bool IsDragging => _isDragging;
        public Rect CurrentDragRect => _currentDragRect;
    }
}
