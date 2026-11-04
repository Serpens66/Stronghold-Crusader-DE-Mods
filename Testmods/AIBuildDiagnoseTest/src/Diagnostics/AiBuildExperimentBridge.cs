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
        private static IAiBuildDetailCapture capture;
        private static Action<AIBuildDiagnoseRuntime.WallObservation> wallObserver;
        internal static bool HasDetails => Volatile.Read(ref capture) != null;
        internal static bool TryRegisterDetails(string ownerGuid, IAiBuildDetailCapture provider,
            Action<AIBuildDiagnoseRuntime.WallObservation> walls, out string error)
        {
            error = null;
            if (ownerGuid != "CanarariWoodTest_Serp" || provider == null || walls == null)
            { error = "Invalid detail capture owner or provider."; return false; }
            lock (sync)
            {
                if (observer == null) { error = "Companion observer is unavailable."; return false; }
                if (capture != null)
                {
                    if (ReferenceEquals(capture, provider) && ReferenceEquals(wallObserver, walls)) return true;
                    error = "Another detail capture is already registered."; return false;
                }
                Volatile.Write(ref wallObserver, walls);
                Volatile.Write(ref capture, provider);
                return true;
            }
        }
        internal static AiEconomyGridEvidence CaptureGrid(ulong state, int mode, int x = -1, int y = -1)
        {
            var provider = Volatile.Read(ref capture);
            if (provider != null)
            {
                if (x >= 0 && y >= 0)
                    return state == 0 ? provider.CaptureEconomyGridEvidence(x, y) :
                        provider.CaptureEconomyGridEvidence(state, mode, x, y);
                return provider.CaptureEconomyGridEvidence(state, mode);
            }
            return new AiEconomyGridEvidence("unavailable", state, mode, x, y, -1, -1, -1, null);
        }
        internal static AiNearbyPathEvidence CaptureNearby(ulong state, int inputX, int inputY,
            int resultX, int resultY, bool before) => Volatile.Read(ref capture)?.CaptureNearbyPathEvidence(
                state, inputX, inputY, resultX, resultY, before);
        internal static AiRouteEvidence CaptureRoute(int player, int mapper, int x, int y) =>
            Volatile.Read(ref capture)?.CaptureRouteEvidence(player, mapper, x, y);
        internal static System.Collections.Generic.IReadOnlyList<AiPathTileSample> CaptureTiles(
            int x, int y, int width, int height) => Volatile.Read(ref capture)?.CaptureTiles(x, y, width, height)
                ?? Array.AsReadOnly(new AiPathTileSample[0]);
        internal static void DeliverWall(AIBuildDiagnoseRuntime.WallObservation wall, ManualLogSource log)
        {
            try { Volatile.Read(ref wallObserver)?.Invoke(wall); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_COMPANION_WALL_FAILED: " + ex); }
        }
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
        internal static void Dispatch(AiBuildDiagnosticRecord record,
            Action<AiBuildDiagnosticRecord> general, ManualLogSource log)
        {
            try { general?.Invoke(record); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_CALLBACK_FAILED: " + ex); }
            Deliver(record, log);
        }
    }
}
