using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace Shared
{
    public enum ActivePlayerKeepWaitStatus
    {
        Succeeded,
        TimedOut,
        CallbackFailed,
        Cancelled
    }

    public sealed class ActivePlayerKeepSnapshot
    {
        internal ActivePlayerKeepSnapshot(int[] playerIds, int[] keepBuildingIds)
        {
            PlayerIds = playerIds;
            KeepBuildingIds = keepBuildingIds;
        }

        public int[] PlayerIds { get; }
        public int[] KeepBuildingIds { get; }
    }

    public sealed class ActivePlayerKeepWaitResult
    {
        internal ActivePlayerKeepWaitResult(
            ActivePlayerKeepWaitStatus status,
            ActivePlayerKeepSnapshot snapshot,
            string details)
        {
            Status = status;
            Snapshot = snapshot;
            Details = details ?? string.Empty;
        }

        public ActivePlayerKeepWaitStatus Status { get; }
        public ActivePlayerKeepSnapshot Snapshot { get; }
        public string Details { get; }
        public bool Succeeded => Status == ActivePlayerKeepWaitStatus.Succeeded;
    }

    public sealed class ActivePlayerKeepWaitHandle : IDisposable
    {
        private readonly Action<ActivePlayerKeepSnapshot> readyCallback;
        private readonly Action<string> errorLogger;
        private readonly Action<ActivePlayerKeepWaitResult> completionCallback;
        private readonly string timeoutErrorText;
        private bool listening;
        private readonly int simulationTimeoutTicks;
        private readonly int startedMapTick;

        internal ActivePlayerKeepWaitHandle(
            Action<ActivePlayerKeepSnapshot> readyCallback,
            int timeoutTicks, int startedTick,
            Action<string> errorLogger,
            string timeoutErrorText,
            Action<ActivePlayerKeepWaitResult> completionCallback)
        {
            this.readyCallback = readyCallback ?? throw new ArgumentNullException(nameof(readyCallback));
            this.errorLogger = errorLogger;
            this.timeoutErrorText = string.IsNullOrWhiteSpace(timeoutErrorText)
                ? "Active-player Keep readiness timed out."
                : timeoutErrorText.Trim();
            this.completionCallback = completionCallback;
            simulationTimeoutTicks = timeoutTicks;
            startedMapTick = startedTick;
            listening = true;
        }

        public bool IsCompleted { get; private set; }
        public ActivePlayerKeepWaitResult Result { get; private set; }

        public void Dispose()
        {
            if (IsCompleted)
                return;

            StopListening();
            Complete(new ActivePlayerKeepWaitResult(
                ActivePlayerKeepWaitStatus.Cancelled,
                null,
                "The Keep-readiness wait was cancelled."));
        }

        internal void OnGameTick(int tick)
        {
            if (IsCompleted)
                return;

            if (ActivePlayerKeepReadiness.TryCapture(out ActivePlayerKeepSnapshot snapshot, out string failure))
            {
                StopListening();
                try
                {
                    readyCallback(snapshot);
                    Complete(new ActivePlayerKeepWaitResult(
                        ActivePlayerKeepWaitStatus.Succeeded,
                        snapshot,
                        string.Empty));
                }
                catch (Exception ex)
                {
                    string details = $"The Keep-readiness callback failed: {ex}";
                    errorLogger?.Invoke(details);
                    Complete(new ActivePlayerKeepWaitResult(
                        ActivePlayerKeepWaitStatus.CallbackFailed,
                        snapshot,
                        details));
                }
                return;
            }

            if (!APIShared.Simulation.SimulationClock.TryGetElapsedTicks(startedMapTick, tick, out int elapsed) ||
                elapsed < simulationTimeoutTicks) return;

            StopListening();
            string timeoutDetails = $"{timeoutErrorText} Last readiness failure: {failure}";
            errorLogger?.Invoke(timeoutDetails);
            Complete(new ActivePlayerKeepWaitResult(
                ActivePlayerKeepWaitStatus.TimedOut,
                null,
                timeoutDetails));
        }

        private void StopListening()
        {
            if (!listening)
                return;

            ActivePlayerKeepReadiness.RemoveSimulationWait(this);
            listening = false;
        }

        private void Complete(ActivePlayerKeepWaitResult result)
        {
            Result = result;
            IsCompleted = true;
            try
            {
                completionCallback?.Invoke(result);
            }
            catch (Exception ex)
            {
                errorLogger?.Invoke($"The Keep-readiness completion callback failed: {ex}");
            }
        }
    }

    public static unsafe class ActivePlayerKeepReadiness
    {
        private static readonly List<ActivePlayerKeepWaitHandle> simulationWaits = new List<ActivePlayerKeepWaitHandle>();
        private static bool simulationTicksRegistered;
        internal static void InitializeSimulationTicks(string ownerGuid)
        {
            if (simulationTicksRegistered) return;
            if (!APIShared.Simulation.SimulationTickEvents.TryRegister(ownerGuid, "active-player-keep-readiness",
                OnSimulationTick, out string reason, 100)) throw new InvalidOperationException(reason);
            simulationTicksRegistered = true;
        }
        private static void OnSimulationTick(int tick)
        {
            if (simulationWaits.Count == 0) return;
            // Waits may complete/create other waits during delivery; new waits start next tick.
            foreach (var wait in simulationWaits.ToArray()) wait.OnGameTick(tick);
        }
        internal static void RemoveSimulationWait(ActivePlayerKeepWaitHandle wait) => simulationWaits.Remove(wait);
        internal static ActivePlayerKeepWaitHandle WaitForSimulationTicks(
            Action<ActivePlayerKeepSnapshot> readyCallback, int timeoutTicks, Action<string> errorLogger,
            string timeoutErrorText, Action<ActivePlayerKeepWaitResult> completionCallback)
        {
            if (!simulationTicksRegistered) throw new InvalidOperationException("Simulation readiness source is not registered.");
            if (timeoutTicks <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutTicks));
            if (!MissionEvents.TryGetCurrentMapTick(out int startedTick))
                throw new InvalidOperationException("A simulation readiness wait needs native map time at creation.");
            var wait = new ActivePlayerKeepWaitHandle(readyCallback, timeoutTicks, startedTick, errorLogger,
                timeoutErrorText, completionCallback);
            simulationWaits.Add(wait);
            return wait;
        }
        public static bool TryCapture(
            out ActivePlayerKeepSnapshot snapshot,
            out string failure)
        {
            snapshot = null;
            int[] playerIds = ActivePlayerHelper.GetActivePlayerIds();
            if (playerIds.Length == 0)
            {
                failure = "the synchronized gameMembers roster is unavailable or contains no active players.";
                return false;
            }

            int[] keepBuildingIds = new int[playerIds.Length];
            List<string> missing = new List<string>();
            for (int index = 0; index < playerIds.Length; index++)
            {
                int playerId = playerIds[index];
                bool ready = TryGetReadyKeep(playerId, out int keepId);
                keepBuildingIds[index] = keepId;
                if (!ready)
                    missing.Add($"P{playerId}:keepId={keepId}");
            }

            if (missing.Count > 0)
            {
                failure = $"no ready Keep was found for [{string.Join(",", missing)}]; activePlayers=[{string.Join(",", playerIds)}].";
                return false;
            }

            snapshot = new ActivePlayerKeepSnapshot(playerIds, keepBuildingIds);
            failure = string.Empty;
            return true;
        }

        public static bool TryGetReadyKeep(int playerId, out int keepBuildingId)
        {
            keepBuildingId = -1;
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            if (!players.IsPlayerIdValid(playerId))
                return false;

            keepBuildingId = players.GetPlayerKeepId(playerId);
            return keepBuildingId > 0 &&
                   GameBuildingManagerAPI.Instance.TryGetBuildingById(keepBuildingId, out GameBuilding* keep) &&
                   keep != null &&
                   keep->r_PlayerIdOwner == playerId &&
                   IsKeepType(keep->r_BuildingType) &&
                   (keep->r_AliveState == AliveState.NeedsInit ||
                    keep->r_AliveState == AliveState.IsAlive);
        }

        private static bool IsKeepType(eStructs buildingType)
        {
            return buildingType == eStructs.STRUCT_KEEP_ONE ||
                   buildingType == eStructs.STRUCT_KEEP_TWO ||
                   buildingType == eStructs.STRUCT_KEEP_THREE ||
                   buildingType == eStructs.STRUCT_KEEP_FOUR ||
                   buildingType == eStructs.STRUCT_KEEP_FIVE;
        }
    }
}
