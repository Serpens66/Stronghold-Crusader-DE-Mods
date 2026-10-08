using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;

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
        private readonly long startedTimestamp;
        private readonly long timeoutStopwatchTicks;
        private bool listening;

        internal ActivePlayerKeepWaitHandle(
            Action<ActivePlayerKeepSnapshot> readyCallback,
            TimeSpan timeout,
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
            startedTimestamp = Stopwatch.GetTimestamp();
            timeoutStopwatchTicks = Math.Max(
                1L,
                checked((long)Math.Ceiling(timeout.TotalSeconds * Stopwatch.Frequency)));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
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

        private void OnGameTick(int tick)
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

            if (Stopwatch.GetTimestamp() - startedTimestamp < timeoutStopwatchTicks)
                return;

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

            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
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
        public static ActivePlayerKeepWaitHandle Wait(
            Action<ActivePlayerKeepSnapshot> readyCallback,
            TimeSpan timeout,
            Action<string> errorLogger = null,
            string timeoutErrorText = null,
            Action<ActivePlayerKeepWaitResult> completionCallback = null)
        {
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), "The timeout must be greater than zero.");

            return new ActivePlayerKeepWaitHandle(
                readyCallback,
                timeout,
                errorLogger,
                timeoutErrorText,
                completionCallback);
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
