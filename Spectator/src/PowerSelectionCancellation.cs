using System;
using System.Threading;

namespace Spectator
{
    // Non-null selections are published by the main thread under the engine monitor;
    // cancellation is serialized with that publication on the same main thread.
    // Native callbacks may only consume/clear selections (exception cleanup is already
    // under the engine monitor). This is not a general concurrent-publisher service.
    // Keep the whole cancellation protocol here so
    // the no-selection path is tested with the real monitor held by another thread.
    internal static class PowerSelectionCancellation
    {
        internal static void Cancel<T>(ref T pending, ref long generation, object engineLock,
            Action<T> cancelNative) where T : class
        {
            // A native confirmation may already have consumed the selection and queued
            // an action. Invalidate that action even when there is nothing left to cancel.
            Interlocked.Increment(ref generation);
            if (Volatile.Read(ref pending) == null) return;
            if (engineLock == null) { Interlocked.Exchange(ref pending, null); return; }
            lock (engineLock)
            {
                // Native confirmation can win while we wait for the monitor.
                T previous = Interlocked.Exchange(ref pending, null);
                if (previous != null) cancelNative(previous);
            }
        }
    }
}
