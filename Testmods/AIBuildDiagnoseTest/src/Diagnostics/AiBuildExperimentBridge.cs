using System;
using System.Threading;
using BepInEx.Logging;
namespace AIBuildDiagnoseTest
{
    // Assembly-private integration with the optional companion, not an APIShared service.
    // Register after the general observer is ready. Callbacks run synchronously on the native
    // caller thread and remain rooted for process lifetime; ordinary sessions must stay inert.
    internal static class AiBuildExperimentBridge
    {
        private static readonly object sync = new object();
        private static Action<AiBuildDiagnosticRecord> observer;
        internal static bool TryRegister(string ownerGuid, Action<AiBuildDiagnosticRecord> callback, out string error)
        {
            error = null;
            if (ownerGuid != "CanarariWoodTest_Serp" || callback == null)
            { error = "Invalid companion observer owner or callback."; return false; }
            lock (sync)
            {
                if (observer != null)
                {
                    if (ReferenceEquals(observer, callback)) return true;
                    error = "Another companion observer is already registered."; return false;
                }
                if (!AiBuildDiagnostic.HasObserver)
                { error = "General observer is unavailable."; return false; }
                Volatile.Write(ref observer, callback);
                return true;
            }
        }
        internal static void Deliver(AiBuildDiagnosticRecord record, ManualLogSource log)
        {
            Action<AiBuildDiagnosticRecord> callback = Volatile.Read(ref observer);
            if (callback == null) return;
            try { callback(record); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_COMPANION_CALLBACK_FAILED: " + ex); }
        }
    }
}
