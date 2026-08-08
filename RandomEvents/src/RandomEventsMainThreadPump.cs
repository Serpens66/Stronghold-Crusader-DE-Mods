using System;
using System.Collections.Generic;
using UnityEngine;

namespace RandomEvents
{
    internal sealed class RandomEventsMainThreadPump : MonoBehaviour
    {
        private readonly Queue<Action> pending = new Queue<Action>();

        public static RandomEventsMainThreadPump Create()
        {
            GameObject host = new GameObject("RandomEvents.MainThreadPump");
            DontDestroyOnLoad(host);
            return host.AddComponent<RandomEventsMainThreadPump>();
        }

        public void Enqueue(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            lock (pending)
                pending.Enqueue(action);
        }

        private void LateUpdate()
        {
            // Timeline arrays must not be mutated from the Script Extender's inline native pre-tick hook.
            while (true)
            {
                Action action;
                lock (pending)
                {
                    if (pending.Count == 0)
                        return;
                    action = pending.Dequeue();
                }
                action();
            }
        }
    }
}
