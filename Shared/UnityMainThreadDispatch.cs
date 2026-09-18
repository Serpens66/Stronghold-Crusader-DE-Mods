using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Shared
{
    /// <summary>
    /// Process-lifetime, per-mod-assembly dispatcher for work that must leave an
    /// arbitrary packet or simulation callback before it touches Unity or Noesis.
    /// </summary>
    internal static class UnityMainThreadDispatch
    {
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();
        private static readonly object InitializationGate = new object();
        private static SynchronizationContext unityContext;
        private static int unityThreadId;
        private static int dispatchScheduled;

        public static bool IsInitialized =>
            Volatile.Read(ref unityContext) != null && Volatile.Read(ref unityThreadId) != 0;

        public static bool IsMainThread =>
            IsInitialized && Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref unityThreadId);

        public static void InitializeForCurrentThread()
        {
            SynchronizationContext context = SynchronizationContext.Current;
            int threadId = Thread.CurrentThread.ManagedThreadId;
            if (context == null ||
                !string.Equals(
                    context.GetType().FullName,
                    "UnityEngine.UnitySynchronizationContext",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Main-thread dispatch must initialize on UnityEngine.UnitySynchronizationContext.");
            }

            lock (InitializationGate)
            {
                if (unityContext == null)
                {
                    unityThreadId = threadId;
                    Volatile.Write(ref unityContext, context);
                    return;
                }

                if (!ReferenceEquals(unityContext, context) || unityThreadId != threadId)
                {
                    throw new InvalidOperationException(
                        "Main-thread dispatch was already initialized for a different context or thread.");
                }
            }
        }

        public static bool TryEnqueue(Action action)
        {
            if (action == null || !IsInitialized)
                return false;

            Queue.Enqueue(action);
            return ScheduleDrain();
        }

        public static bool TryRunInlineOrEnqueue(Action action)
        {
            if (action == null || !IsInitialized)
                return false;
            if (IsMainThread)
            {
                action();
                return true;
            }
            return TryEnqueue(action);
        }

        private static bool ScheduleDrain()
        {
            SynchronizationContext context = Volatile.Read(ref unityContext);
            if (context == null)
                return false;
            if (Interlocked.CompareExchange(ref dispatchScheduled, 1, 0) != 0)
                return true;
            try
            {
                context.Post(_ => Drain(), null);
                return true;
            }
            catch
            {
                Interlocked.Exchange(ref dispatchScheduled, 0);
                while (Queue.TryDequeue(out _))
                {
                }
                return false;
            }
        }

        private static void Drain()
        {
            if (!IsMainThread)
            {
                Interlocked.Exchange(ref dispatchScheduled, 0);
                while (Queue.TryDequeue(out _))
                {
                }
                return;
            }

            try
            {
                while (Queue.TryDequeue(out Action action))
                {
                    try
                    {
                        action();
                    }
                    catch
                    {
                        // A dispatched callback must not escape through Unity's
                        // SynchronizationContext and prevent later queued work.
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref dispatchScheduled, 0);
                if (!Queue.IsEmpty)
                    ScheduleDrain();
            }
        }
    }
}
