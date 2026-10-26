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
            lock (stateSync)
            {
                speechQueue.Clear();
                if (drag == null && pendingCommand == null && activeCommand == null) return;
                drag = null;
                pendingCommand = null;
                activeCommand = commonGroupCommand = null;
                ClearUnitAssignmentFrames(null);
                unitFallbackAttempt = null;
            }
            ClearPreview();
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(menuViewModel.CloseMenu);
        }

        private bool ValidateActiveDrag(ActiveDrag state) =>
            LocalInputEnabled && state != null && markerRenderer != null &&
            markerRenderer.ReplacementAvailable && HasValidMap() && !IsShiftHeld() &&
            state.MapEpoch == commandRuntime.mapEpoch &&
            state.PlayerId == GamePlayerManagerAPI.Instance.GetLocalPlayerId() &&
            (FatControler.instance == null || !FatControler.instance.overNoesisGUI()) &&
            GetCommandMouseButton() == state.CommandButton &&
            SelectionMatches(state.Selection, state.TribeId);

        private void AbortDrag(string reason)
        {
            ActiveDrag state;
            lock (stateSync)
            {
                state = drag;
                drag = null;
                RefreshNativeObserverDemand();
            }
            if (state == null)
                return;
            ClearPreview();
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
            localInputFailed = true;
            lock (stateSync)
            {
                speechQueue.Clear();
                drag = null;
                releaseConsumptionWatch = null;
            }
            // Fail-open must reach Vanilla even if a presentation or logger also fails.
            try { ClearPreview(); } catch { }
            try { Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(menuViewModel.CloseMenu); } catch { }
            try { Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(menuViewModel.RefreshHostState); } catch { }
            try
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/Formations/FormationRuntime.Activation.cs:88", $"FORMATION_DISABLED: contract={contract}; Vanilla remains active; {exception}");
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
