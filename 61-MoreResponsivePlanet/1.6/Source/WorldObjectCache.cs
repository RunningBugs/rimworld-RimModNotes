using UnityEngine;
using Verse;
using RimWorld.Planet;
using RimWorld;

namespace MoreResponsivePlanet
{
    public static class WorldObjectCache
    {
        private static RenderTexture _cachedWorldObjects;
        private static bool _cacheDirty = true;
        private static Camera _cacheCamera;
        private static int _lastWorldObjectCount = -1;
        private static Vector3 _lastCameraPosition;
        private static float _lastCameraZoom;

        public static void Initialize()
        {
            // Create a render texture for caching world objects
            _cachedWorldObjects = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            _cachedWorldObjects.name = "WorldObjectCache";
            
            // Create a camera for rendering to the cache
            GameObject cameraGO = new GameObject("WorldObjectCacheCamera");
            Object.DontDestroyOnLoad(cameraGO);
            _cacheCamera = cameraGO.AddComponent<Camera>();
            _cacheCamera.enabled = false; // We'll render manually
            _cacheCamera.clearFlags = CameraClearFlags.Color;
            _cacheCamera.backgroundColor = Color.clear;
            
            Log.Message("[WorldObjectCache] Initialized caching system");
        }

        public static void MarkDirty()
        {
            _cacheDirty = true;
        }

        public static bool ShouldUpdateCache()
        {
            if (_cacheDirty) return true;
            
            // Check if world objects count changed
            int currentCount = Find.WorldObjects.AllWorldObjects.Count;
            if (currentCount != _lastWorldObjectCount)
            {
                _lastWorldObjectCount = currentCount;
                return true;
            }
            
            // Check if camera moved significantly
            Vector3 currentPos = Find.WorldCamera.transform.position;
            float currentZoom = Find.WorldCameraDriver.AltitudePercent;
            
            if (Vector3.Distance(currentPos, _lastCameraPosition) > 0.1f || 
                Mathf.Abs(currentZoom - _lastCameraZoom) > 0.01f)
            {
                _lastCameraPosition = currentPos;
                _lastCameraZoom = currentZoom;
                return true;
            }
            
            return false;
        }

        public static void UpdateCache()
        {
            if (!ShouldUpdateCache()) return;
            
            try
            {
                // Set up the cache camera to match the world camera
                _cacheCamera.CopyFrom(Find.WorldCamera);
                _cacheCamera.targetTexture = _cachedWorldObjects;
                
                // Clear the render texture
                RenderTexture.active = _cachedWorldObjects;
                GL.Clear(true, true, Color.clear);
                
                // Render world objects to the cache
                _cacheCamera.Render();
                
                // Manually render world objects using the same methods RimWorld uses
                RenderWorldObjectsToCache();
                
                RenderTexture.active = null;
                _cacheDirty = false;
                
                Log.Message("[WorldObjectCache] Cache updated");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[WorldObjectCache] Error updating cache: {ex}");
                _cacheDirty = true; // Try again next frame
            }
        }

        private static void RenderWorldObjectsToCache()
        {
            // Use RimWorld's existing rendering methods but render to our cache
            if (Find.World?.renderer == null) return;
            
            // Set the render target
            RenderTexture.active = _cachedWorldObjects;
            
            // Set up matrices for rendering
            GL.PushMatrix();
            GL.LoadProjectionMatrix(_cacheCamera.projectionMatrix);
            GL.modelview = _cacheCamera.worldToCameraMatrix;
            
            try
            {
                // Render world objects using RimWorld's layer system
                foreach (var layer in Find.World.renderer.AllVisibleDrawLayers)
                {
                    if (layer is WorldDrawLayer_WorldObjects)
                    {
                        layer.Render();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[WorldObjectCache] Error rendering layer: {ex}");
            }
            
            GL.PopMatrix();
            RenderTexture.active = null;
        }

        public static void RenderCachedWorldObjects()
        {
            if (_cachedWorldObjects == null) return;
            
            // Update cache if needed
            UpdateCache();
            
            // Draw the cached texture to screen
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _cachedWorldObjects);
        }

        public static void Cleanup()
        {
            if (_cachedWorldObjects != null)
            {
                _cachedWorldObjects.Release();
                Object.DestroyImmediate(_cachedWorldObjects);
                _cachedWorldObjects = null;
            }
            
            if (_cacheCamera != null)
            {
                Object.DestroyImmediate(_cacheCamera.gameObject);
                _cacheCamera = null;
            }
        }

        // Notify cache when world objects change
        public static void NotifyWorldObjectAdded() => MarkDirty();
        public static void NotifyWorldObjectRemoved() => MarkDirty();
        public static void NotifyWorldObjectMoved() => MarkDirty();
        public static void NotifyCameraMoved() => MarkDirty();
    }
}
