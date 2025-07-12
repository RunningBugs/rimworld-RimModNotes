using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace MoreResponsivePlanet
{
    public class ImmediateGLRenderer : MonoBehaviour
    {
        private static ImmediateGLRenderer _instance;
        public static ImmediateGLRenderer Instance => _instance;

        private bool _isDragging = false;
        private Vector2 _dragStart;
        private Material _lineMaterial;

        public static void Initialize()
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ImmediateGLRenderer");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ImmediateGLRenderer>();
                Log.Message("[ImmediateGLRenderer] Initialized for immediate mode rendering");
            }
        }

        void Start()
        {
            // Create a simple unlit material for immediate mode rendering
            _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
        }

        void Update()
        {
            // Only render if we're in world view and dragging
            if (ShouldRender() && _isDragging)
            {
                RenderDragBoxImmediate();
                // Debug logging
                if (Time.frameCount % 30 == 0) // Log every 30 frames to avoid spam
                {
                    Log.Message($"[ImmediateGLRenderer] Rendering drag box: {GetCurrentDragRect()}");
                }
            }
        }

        private bool ShouldRender()
        {
            return Find.World != null && 
                   WorldRendererUtility.WorldSelected && 
                   Find.WindowStack.Count == 0; // No dialogs blocking
        }

        public void StartDrag(Vector2 startPos)
        {
            _isDragging = true;
            _dragStart = startPos;
            Log.Message($"[ImmediateGLRenderer] Started drag at {startPos}");
        }

        public void EndDrag()
        {
            _isDragging = false;
            Log.Message("[ImmediateGLRenderer] Ended drag");
        }

        public bool IsDragging => _isDragging;

        public Rect GetCurrentDragRect()
        {
            if (!_isDragging) return Rect.zero;

            Vector2 currentMouse = UI.MousePositionOnUIInverted;
            float leftX = Mathf.Min(_dragStart.x, currentMouse.x);
            float rightX = Mathf.Max(_dragStart.x, currentMouse.x);
            float botY = Mathf.Min(_dragStart.y, currentMouse.y);
            float topY = Mathf.Max(_dragStart.y, currentMouse.y);

            return new Rect(leftX, botY, rightX - leftX, topY - botY);
        }

        public bool IsValidDrag()
        {
            if (!_isDragging) return false;
            Rect rect = GetCurrentDragRect();
            return rect.width > 7f || rect.height > 7f;
        }

        private void RenderDragBoxImmediate()
        {
            Rect dragRect = GetCurrentDragRect();
            if (dragRect.width < 1f && dragRect.height < 1f) return;

            // Convert UI coordinates to screen coordinates for GL rendering
            float screenHeight = Screen.height;
            Vector2 screenMin = new Vector2(dragRect.xMin, screenHeight - dragRect.yMax);
            Vector2 screenMax = new Vector2(dragRect.xMax, screenHeight - dragRect.yMin);

            // Use immediate mode GL rendering
            _lineMaterial.SetPass(0);
            
            GL.PushMatrix();
            GL.LoadPixelMatrix(); // Use pixel-perfect coordinates
            
            GL.Begin(GL.LINES);
            GL.Color(Color.white);
            
            // Draw rectangle outline (4 lines)
            // Top line
            GL.Vertex3(screenMin.x, screenMax.y, 0);
            GL.Vertex3(screenMax.x, screenMax.y, 0);
            
            // Right line
            GL.Vertex3(screenMax.x, screenMax.y, 0);
            GL.Vertex3(screenMax.x, screenMin.y, 0);
            
            // Bottom line
            GL.Vertex3(screenMax.x, screenMin.y, 0);
            GL.Vertex3(screenMin.x, screenMin.y, 0);
            
            // Left line
            GL.Vertex3(screenMin.x, screenMin.y, 0);
            GL.Vertex3(screenMin.x, screenMax.y, 0);
            
            GL.End();
            GL.PopMatrix();
        }
    }
}
