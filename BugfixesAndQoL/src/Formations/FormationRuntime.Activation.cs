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
            lock (stateSync)
            {
                if (drag == null && pendingCommand == null && activeCommand == null) return;
                drag = null;
                pendingCommand = null;
                activeCommand = commonGroupCommand = null;
                ClearUnitAssignmentFrames(null);
                unitFallbackAttempt = null;
            }
            ClearPreview();
            menuViewModel.CloseMenu();
        }

        private bool ValidateActiveDrag(ActiveDrag state) =>
            Enabled && state != null && markerRenderer != null &&
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
            }
            if (state == null)
                return;
            ClearPreview();
            APIShared.Internal.DebugLogHelper.LogDebug(log, $"FORMATION_DRAG_ABORTED: {reason}.");
        }

        internal void DisableForProcess(string contract, Exception exception) => FailOpen(contract, exception);

        private void FailOpen(string contract, Exception exception)
        {
            failed = true;
            lock (stateSync)
            {
                drag = null;
                pendingCommand = null;
                activeCommand = null;
                commonGroupCommand = null;
                unitAssignmentFrame = null;
                unitFallbackAttempt = null;
                releaseConsumptionWatch = null;
            }
            // Fail-open must reach Vanilla even if a presentation or logger also fails.
            try { ClearPreview(); } catch { }
            try { menuViewModel.CloseMenu(); } catch { }
            try { APIShared.Internal.UnityMainThreadDispatch.TryRunInlineOrEnqueue(menuViewModel.RefreshHostState); } catch { }
            try
            {
                APIShared.Internal.DebugLogHelper.LogError(log,
                    $"FORMATION_DISABLED: contract={contract}; Vanilla remains active; {exception}");
            }
            catch { }
        }

        private void ClearPreview()
        {
            markerRenderer?.ClearPreviewMarkerTiles();
            menuViewModel.ClearPreview();
        }
    }
}
