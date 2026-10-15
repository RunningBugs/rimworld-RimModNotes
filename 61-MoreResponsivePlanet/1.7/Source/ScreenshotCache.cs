using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace MoreResponsivePlanet
{
    public static class ScreenshotCache
    {
        private static Texture2D _cachedScreenshot;
        private static bool _hasValidCache = false;

        public static void CaptureScreenshot()
        {
            try
            {
                // Clean up previous screenshot
                if (_cachedScreenshot != null)
                {
                    Object.DestroyImmediate(_cachedScreenshot);
                }

                // Get the actual screen dimensions
                int screenWidth = Screen.width;
                int screenHeight = Screen.height;
                float uiScale = Prefs.UIScale;



                // Create texture with actual screen dimensions
                _cachedScreenshot = new Texture2D(screenWidth, screenHeight, TextureFormat.RGB24, false);
                
                // Read pixels from the entire screen
                _cachedScreenshot.ReadPixels(new Rect(0, 0, screenWidth, screenHeight), 0, 0);
                _cachedScreenshot.Apply();
                
                _hasValidCache = true;

            }
            catch (System.Exception ex)
            {
                Log.Error($"Failed to capture screenshot: {ex}");
                _hasValidCache = false;
            }
        }

        public static void RenderCachedScreenshot()
        {
            if (_hasValidCache && _cachedScreenshot != null)
            {
                // Draw the cached screenshot using actual screen coordinates
                // Convert from screen coordinates to GUI coordinates
                float uiScale = Prefs.UIScale;
                Rect guiRect = new Rect(0, 0, Screen.width / uiScale, Screen.height / uiScale);
                
                GUI.DrawTexture(guiRect, _cachedScreenshot);
                

            }
        }

        public static void ClearCache()
        {
            if (_cachedScreenshot != null)
            {
                Object.DestroyImmediate(_cachedScreenshot);
                _cachedScreenshot = null;
            }
            _hasValidCache = false;
        }

        public static bool HasValidCache => _hasValidCache;
    }
}
