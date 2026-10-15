using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using Verse;

namespace MoreResponsivePlanet
{
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        public static UnityMainThreadDispatcher Instance => _instance;

        private readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();

        public static void Initialize()
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("UnityMainThreadDispatcher");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<UnityMainThreadDispatcher>();

            }
        }

        void Update()
        {
            // Execute all queued actions on the main thread
            while (_executionQueue.TryDequeue(out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Error($"Error executing queued action: {ex}");
                }
            }
        }

        public void Enqueue(Action action)
        {
            if (action != null)
            {
                _executionQueue.Enqueue(action);
            }
        }

        void OnDestroy()
        {
            _instance = null;
        }
    }
}
