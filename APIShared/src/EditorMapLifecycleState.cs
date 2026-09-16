using System;
using System.Collections.Generic;

namespace APIShared
{
    // No Unity dependencies: transition and replay semantics are exercised without starting the game.
    internal sealed class EditorMapLifecycleState
    {
        private readonly Dictionary<string, Action<EditorMapLifecycleNotification>> observers =
            new Dictionary<string, Action<EditorMapLifecycleNotification>>(StringComparer.Ordinal);
        private readonly Queue<Action> deliveries = new Queue<Action>();
        private readonly Action<Exception> onError;
        private bool delivering;
        private long sequence;
        private long pending;
        private int operationDepth;
        internal EditorMapLifecycleNotification Current { get; private set; }

        internal EditorMapLifecycleState(Action<Exception> onError) { this.onError = onError; }

        internal bool Run(EditorMapOrigin origin, string filePath, Func<bool> vanilla)
        {
            long token = Begin();
            operationDepth++;
            bool success = false;
            try { success = vanilla(); return success; }
            finally
            {
                operationDepth--;
                Complete(token, origin, filePath, success);
            }
        }

        internal void ObserveNativeUnload()
        {
            if (operationDepth == 0) End(EditorMapEndReason.NativeUnload);
        }

        internal void ObserveScreen(bool actualMainGame, bool editorMode)
        {
            if (!actualMainGame || !editorMode) End(EditorMapEndReason.SceneChanged);
        }

        internal bool Register(string owner, string id, Action<EditorMapLifecycleNotification> callback)
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(id) || callback == null)
                return false;
            string key = owner.Length + ":" + owner + id;
            if (observers.ContainsKey(key)) return false;
            observers.Add(key, callback);
            var current = Current;
            if (current != null)
                Enqueue(() => callback(new EditorMapLifecycleNotification(current.SessionId, current.Origin,
                    current.FilePath, EditorMapLifecycleKind.Ready, EditorMapEndReason.None, true)));
            return true;
        }

        internal long Begin()
        {
            long token = ++sequence;
            pending = token;
            EndCurrent(EditorMapEndReason.MapReplacement);
            return token;
        }

        internal void Complete(long token, EditorMapOrigin origin, string filePath, bool success)
        {
            if (pending != token) return;
            pending = 0;
            if (!success) return;
            Current = new EditorMapLifecycleNotification(token, origin, filePath,
                EditorMapLifecycleKind.Ready, EditorMapEndReason.None, false);
            Publish(Current);
        }

        internal void End(EditorMapEndReason reason)
        {
            pending = 0;
            EndCurrent(reason);
        }

        private void EndCurrent(EditorMapEndReason reason)
        {
            var old = Current;
            Current = null;
            if (old != null)
                Publish(new EditorMapLifecycleNotification(old.SessionId, old.Origin, old.FilePath,
                    EditorMapLifecycleKind.Ended, reason, false));
        }

        private void Publish(EditorMapLifecycleNotification notification)
        {
            var targets = new List<Action<EditorMapLifecycleNotification>>(observers.Values).ToArray();
            // Queue the entire publication first; reentrant transitions follow it for every observer.
            foreach (var target in targets)
                deliveries.Enqueue(() => target(notification));
            Drain();
        }

        private void Enqueue(Action action) { deliveries.Enqueue(action); Drain(); }
        private void Drain()
        {
            if (delivering) return;
            delivering = true;
            try
            {
                while (deliveries.Count != 0)
                {
                    try { deliveries.Dequeue()(); }
                    catch (Exception ex) { try { onError?.Invoke(ex); } catch { } }
                }
            }
            finally { delivering = false; }
        }
    }
}
