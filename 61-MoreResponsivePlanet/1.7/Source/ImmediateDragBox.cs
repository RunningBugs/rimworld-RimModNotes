using UnityEngine;
using Verse;

namespace MoreResponsivePlanet
{
    public static class ImmediateDragBox
    {
        private static bool _isActive = false;
        private static Vector2 _startPos;
        private static int _currentDragId = 0;
        
        public static void StartDrag(Vector2 startPosition)
        {
            // Increment drag ID FIRST to invalidate any previous operations
            _currentDragId++;
            
            _isActive = true;
            _startPos = startPosition;
            
            // Capture screenshot for background during drag
            ScreenshotCache.CaptureScreenshot();
            

        }
        
        public static void EndDrag()
        {
            _isActive = false;
            
            // Clear the screenshot cache
            ScreenshotCache.ClearCache();
        }
        
        public static void RenderImmediate()
        {
            if (!_isActive) return;
            
            // Get current mouse position
            Vector2 currentPos = UI.MousePositionOnUIInverted;
            
            // Calculate drag rectangle
            float leftX = Mathf.Min(_startPos.x, currentPos.x);
            float rightX = Mathf.Max(_startPos.x, currentPos.x);
            float botY = Mathf.Min(_startPos.y, currentPos.y);
            float topY = Mathf.Max(_startPos.y, currentPos.y);
            
            Rect dragRect = new Rect(leftX, botY, rightX - leftX, topY - botY);
            
            // Only draw if it's a meaningful size
            if (dragRect.width > 1f || dragRect.height > 1f)
            {
                Widgets.DrawBox(dragRect, 2);
            }
        }
        
        public static bool IsActive => _isActive;
        
        public static int CurrentDragId => _currentDragId;
        
        public static Rect GetCurrentRect()
        {
            if (!_isActive) return Rect.zero;
            
            Vector2 currentPos = UI.MousePositionOnUIInverted;
            float leftX = Mathf.Min(_startPos.x, currentPos.x);
            float rightX = Mathf.Max(_startPos.x, currentPos.x);
            float botY = Mathf.Min(_startPos.y, currentPos.y);
            float topY = Mathf.Max(_startPos.y, currentPos.y);
            
            return new Rect(leftX, botY, rightX - leftX, topY - botY);
        }
        
        public static bool IsValidDrag()
        {
            if (!_isActive) return false;
            Rect rect = GetCurrentRect();
            return rect.width > 7f || rect.height > 7f;
        }
    }
}
