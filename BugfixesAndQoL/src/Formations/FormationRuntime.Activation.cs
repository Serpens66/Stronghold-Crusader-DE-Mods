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

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class FormationRuntime
    {
        internal void ResetTransientState()
        {
            groundFrameObserver?.SetActive(false);
            ActiveDrag abandoned;
            lock (stateSync)
            {
                speechQueue.Clear();
                pendingStart = null;
                if (drag == null && pendingCommand == null && activeCommand == null) return;
                abandoned = drag;
                drag = null;
                pendingCommand = null;
                activeCommand = commonGroupCommand = null;
                ClearUnitAssignmentFrames(null);
                unitFallbackAttempt = null;
            }
            DiscardDeferredRelease(abandoned);
            ClearPreview();
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(menuViewModel.CloseMenu);
        }

        private bool ValidateActiveDrag(ActiveDrag state) =>
            LocalInputEnabled && state != null && markerRenderer != null &&
            markerRenderer.ReplacementAvailable && HasValidMap() && !IsShiftHeld() &&
            state.MapEpoch == commandRuntime.mapEpoch &&
            state.PlayerId == GamePlayerManagerAPI.Instance.GetLocalPlayerId() &&
            (FatControler.instance == null || !FatControler.instance.overNoesisGUI()) &&
            GetCommandMouseButton() == state.CommandButton;

        private void AbortDrag(string reason, bool preserveRelease = false)
        {
            ActiveDrag state;
            lock (stateSync)
            {
                state = drag;
                if (state != null && preserveRelease) state.ReleasePassThrough = true;
                drag = null;
                pendingStart = null;
                RefreshNativeObserverDemand();
            }
            if (state == null)
                return;
            if (preserveRelease)
            {
                // If the original run is currently hiding the edge, its finally hands it
                // back. Otherwise the already restored input remains for Vanilla's next run.
                LogCommandHandoff(state, reason);
            }
            else DiscardDeferredRelease(state);
            ClearPreview();
        }

        private void AbortCommandMode(string phase, APIShared.Internal.GroundMovePreviewRejection rejection) =>
            AbortDrag(phase,
                preserveRelease: rejection == APIShared.Internal.GroundMovePreviewRejection.PatrolCommandMode);

        private void LogCommandHandoff(ActiveDrag state, string reason)
        {
            if (!formationSupportLogGate.TryReserve("patrol-handoff", 30, out long suppressed)) return;
            EmitCommandHandoff(state, reason, suppressed);
        }

        private readonly Shared.LogRateGate formationSupportLogGate = new Shared.LogRateGate();

        private void EmitCommandHandoff(ActiveDrag state, string reason, long suppressed)
        {
            // Rare transition only; no capture, formatting or lookup on ordinary input.
            string message =
                $"FORMATION_INPUT_HANDOFF: reason={reason}, attempt={inputAttempt}, mapEpoch={state.MapEpoch}, " +
                $"player={state.PlayerId}, tribe={state.TribeId}, selection={state.Selection.Length}, " +
                $"anchor=({state.Target.NativeX},{state.Target.NativeY}), button={state.CommandButton}, " +
                $"patrol=active, deferred={state.ReleaseDeferred}, releaseClaimed={state.ReleaseGate.VanillaReleaseClaimed}, suppressed={suppressed}; " +
                "action=pass-original-input-to-vanilla.";
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => Shared.DebugLogHelper.LogInfo(log, message));
        }

        // Simulation-side contract failure (native selector): synchronized execution stops.
        internal void DisableForProcess(string contract, Exception exception)
        {
            failed = true;
            lock (stateSync)
            {
                pendingCommand = null;
                activeCommand = null;
                commonGroupCommand = null;
                unitAssignmentFrame = null;
                unitFallbackAttempt = null;
            }
            FailOpen(contract, exception);
        }

        // Local input/presentation failure: only this player's gestures fall back to
        // Vanilla. Received packets keep the synchronized formation path on every peer.
        private void FailOpen(string contract, Exception exception)
        {
            groundFrameObserver?.SetActive(false);
            ActiveDrag abandoned;
            bool permanent;
            int failures;
            lock (stateSync)
            {
                bool camera = contract == "camera-presentation";
                FormationFailurePolicy policy = camera ? cameraFailures : inputFailures;
                permanent = policy.RecordFailure(contract, exception,
                    camera ? cameraAttempt : inputAttempt, critical: failed);
                failures = policy.ConsecutiveFailures;
                localInputFailed |= permanent;
                speechQueue.Clear();
                abandoned = drag;
                drag = null;
                pendingStart = null;
                releaseConsumptionWatch = null;
            }
            try { DiscardDeferredRelease(abandoned); } catch { }
            // Fail-open must reach Vanilla even if a presentation or logger also fails.
            try { ClearPreview(); } catch { }
            try { Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(menuViewModel.CloseMenu); } catch { }
            try { Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(menuViewModel.RefreshHostState); } catch { }
            try
            {
                LogErrorNoThrow($"FORMATION_{(permanent ? "DISABLED" : "GESTURE_ABORTED")}: " +
                    $"contract={contract}; failures={failures}; Vanilla remains active; {exception}");
            }
            catch { }
        }

        private void ClearPreview()
        {
            long version;
            lock (stateSync)
            {
                if (drag != null) drag.DisplayedAuthorization = false;
                version = ++previewPublicationVersion;
                markerRenderer?.ClearPreviewMarkerTiles();
            }
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => {
                lock (stateSync) if (version == previewPublicationVersion) menuViewModel.ClearPreview();
            });
        }
    }
}
