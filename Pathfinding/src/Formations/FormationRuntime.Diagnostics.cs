using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.Network;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Pathfinding.UnitCommands
{
    internal sealed unsafe partial class FormationRuntime
    {
        private int lastSupportOperation, lastSupportTribe, lastSupportPlayer, lastSupportX, lastSupportY;
        private int lastSupportEpoch;
        private int? lastSupportTick;
        private string lastSupportSource;
        private FormationMoveSupport lastSupportResult;

        private static int? ReadFormationTick() =>
            APIShared.Simulation.SimulationTickEvents.TryGetCurrentTick(out int tick) ? (int?)tick : null;

        private static string FormationMission(APIShared.MissionContext mission) =>
            mission == null ? "mission=unknown mode=unknown" :
                $"session={mission.SessionId} map={System.IO.Path.GetFileName(mission.FilePath)} " +
                $"mode={mission.Mode.Kind} multiplayer={mission.Mode.IsRealMultiplayer} host={mission.IsHost} " +
                $"trail={mission.Mode.CustomizedTrailId} mission={mission.Mode.CustomizedMissionId}";

        private static (int Player, uint GlobalId) ReadFormationTribe(int tribeId)
        {
            if (GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null)
                return (tribe->r_PlayerIdOwner, tribe->r_GlobalId);
            return (0, 0);
        }

        private void BeginReleaseSupport(ActiveDrag state, int operation)
        {
            try
            {
                uint globalId;
                lock (speechEngineLock) globalId = ReadFormationTribe(state.TribeId).GlobalId;
                ReleaseConsumptionWatch old;
                lock (stateSync)
                {
                    old = releaseConsumptionWatch;
                    releaseConsumptionWatch = new ReleaseConsumptionWatch(operation, state.TribeId, state.CommandButton, 2)
                    {
                        Attempt = inputAttempt,
                        MapEpoch = state.MapEpoch,
                        Player = state.PlayerId,
                        X = state.Target.NativeX,
                        Y = state.Target.NativeY,
                        Mission = Shared.MissionEvents.CurrentContext,
                        ReleaseTick = ReadFormationTick(),
                        GlobalId = globalId,
                        Handoff = operation == 0 ? "cancelled" : GameNetworkAPI.IsMultiplayerGame() ? "chore-queued" : "local"
                    };
                }
                ReportAdditionalOrder(old);
            }
            catch { } // The release was already consumed before collecting support evidence.
        }

        private void ReportFormationReleaseFailure(ActiveDrag state, int operation, string reason)
        {
            try
            {
                var identity = ReadFormationTribe(state.TribeId);
                LogFormationSupport("release-" + reason,
                    $"FORMATION_DISPATCH_REJECTED {FormationMission(Shared.MissionEvents.CurrentContext)} tick={ReadFormationTick()?.ToString() ?? "unknown"} " +
                    $"player={state.PlayerId} operation={operation} tribe={state.TribeId}/{identity.GlobalId} " +
                    $"input={inputAttempt} button={state.CommandButton} release=claimed " +
                    $"target={state.Target.NativeX},{state.Target.NativeY} reason={reason}");
            }
            catch { }
        }

        private void ReportAdditionalOrder(ReleaseConsumptionWatch watch)
        {
            if (watch?.Additional == null || (watch.NewPress && watch.Additional.PostReturn == 1)) return;
            string previous;
            bool previousExecuted;
            lock (stateSync)
            {
                bool matches = lastSupportEpoch == watch.MapEpoch && lastSupportOperation == watch.OperationId &&
                    lastSupportTribe == watch.TribeId && lastSupportPlayer == watch.Player &&
                    lastSupportX == watch.X && lastSupportY == watch.Y;
                previous = matches && lastSupportResult != null
                    ? $"tick={lastSupportTick?.ToString() ?? "unknown"} source={lastSupportSource} {lastSupportResult.Describe()}"
                    : null;
                previousExecuted = matches && lastSupportResult?.PostReturn.HasValue == true;
            }
            LogFormationSupport("additional-" + watch.AdditionalSource,
                $"FORMATION_ADDITIONAL_ORDER {FormationMission(watch.Mission)} releaseTick={watch.ReleaseTick?.ToString() ?? "unknown"} " +
                $"input={watch.Attempt} player={watch.Player} operation={watch.OperationId} tribe={watch.TribeId}/{watch.GlobalId} " +
                $"button={watch.CommandButton} release=consumed target={watch.X},{watch.Y} handoff={watch.Handoff} " +
                $"previous=[{previous ?? "execution-unobserved"}] " +
                $"secondExecutionObserved={previousExecuted && watch.Additional.PostReturn.HasValue} " +
                $"newPress={watch.NewPress} ambiguousPost={watch.AdditionalAmbiguous} " +
                $"requestTick={watch.AdditionalTick?.ToString() ?? "unknown"} source={watch.AdditionalSource} " +
                $"requestTarget={watch.Additional.X},{watch.Additional.Y} {watch.Additional.Describe()}");
        }

        private void RecordFormationVeto(TribeIssueOrderMoveHereEventArgs args, string reason)
        {
            var evidence = FormationMoveSupport.Current;
            if (evidence?.Matches(args) == true) evidence.Veto = reason;
        }

        private void RecordFormationTargets(TribeIssueOrderMoveHereEventArgs args, int count)
        {
            var evidence = FormationMoveSupport.Current;
            if (evidence?.Matches(args) == true) evidence.Assigned = count;
        }

        private void LogFormationSupport(string category, string message)
        {
            try
            {
                string site = $"{Shared.MissionEvents.CurrentContext?.SessionId}/{category}";
                if (formationSupportLogGate.TryReserve(site, 30, out long suppressed))
                    LogWarningNoThrow($"{message} suppressed={suppressed}");
            }
            catch { }
        }

        private void DisableAfterNativeFailure(string stage, Exception exception)
        {
            lock (stateSync)
            {
                ClearUnitAssignmentFrames(null);
                pendingCommand = null;
                activeCommand = null;
                commonGroupCommand = null;
                unitFallbackAttempt = null;
                failed = true;
            }
            try { Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(menuViewModel.CloseMenu); } catch { }
            try { Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(menuViewModel.RefreshHostState); } catch { }
            try
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "Pathfinding/FormationRuntime.Diagnostics/DisableAfterNativeFailure", $"Formation native assignment failed open at {stage}; " +
                    $"future formation commands are disabled: {exception}");
            }
            catch
            {
            }
        }

        private void LogDebugNoThrow(string message)
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    Shared.DebugLogHelper.LogDebugLimited(log, "Pathfinding/FormationRuntime.Diagnostics/LogDebugNoThrow", message);
                }
                catch
                {
                }
            });
        }

        private void LogWarningNoThrow(string message)
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "Pathfinding/FormationRuntime.Diagnostics/LogWarningNoThrow", message);
                }
                catch
                {
                }
            });
        }

        private void LogErrorNoThrow(string message)
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "Pathfinding/FormationRuntime.Diagnostics/LogErrorNoThrow", message);
                }
                catch
                {
                }
            });
        }
    }
}
