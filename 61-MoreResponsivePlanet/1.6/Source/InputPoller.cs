using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace MoreResponsivePlanet
{
    public class InputPoller : MonoBehaviour
    {
        private static InputPoller _instance;
        public static InputPoller Instance => _instance;

        // Input state tracking
        private bool _wasMouseDown = false;
        private bool _wasMouseUp = false;
        private float _lastClickTime = 0f;

        // Callbacks for input events
        public System.Action OnMouseDown;
        public System.Action OnMouseUp;
        public System.Action OnDoubleClick;

        public static void Initialize()
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("InputPoller");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<InputPoller>();
            }
        }

        void Update()
        {
            // Only poll input when in world view
            if (!ShouldPollInput()) return;

            PollMouseInput();
        }

        private bool ShouldPollInput()
        {
            return Find.World != null && 
                   WorldRendererUtility.WorldSelected &&
                   Find.WindowStack.Count == 0; // No dialogs blocking input
        }

        private void PollMouseInput()
        {
            bool mouseDown = Input.GetMouseButtonDown(0);
            bool mouseUp = Input.GetMouseButtonUp(0);

            // Detect mouse down
            if (mouseDown && !_wasMouseDown)
            {
                _wasMouseDown = true;
                
                // Check for double-click
                float currentTime = Time.unscaledTime;
                bool isDoubleClick = (currentTime - _lastClickTime) < 0.3f;
                _lastClickTime = currentTime;

                if (isDoubleClick)
                {
                    OnDoubleClick?.Invoke();
                }
                else
                {
                    OnMouseDown?.Invoke();
                }
            }

            // Detect mouse up
            if (mouseUp && _wasMouseDown)
            {
                _wasMouseDown = false;
                _wasMouseUp = true;
                OnMouseUp?.Invoke();
            }

            // Reset mouse up flag after one frame
            if (_wasMouseUp && !mouseUp)
            {
                _wasMouseUp = false;
            }
        }

        void OnDestroy()
        {
            _instance = null;
        }
    }
}
