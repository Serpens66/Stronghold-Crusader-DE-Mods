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
        private int EngineRunHook(bool mpFrameSkip)
        {
            if (!startupConfirmed && initialized && HasValidMap())
            {
                startupConfirmed = true;
                LogDebugNoThrow("FORMATION_RUNTIME_AFTER_STARTUP_CLEANUP: persistent EngineInterface.run callback reached.");
            }
            if (!Enabled)
            {
                try { ResetTransientState(); }
                catch (Exception ex) { FailOpen("disabled-reset", ex); }
                return engineRunOriginal(mpFrameSkip);
            }

            bool originalEntered = false;
            FormationMouseState inputState = default;
            try
            {
                AgeReleaseConsumptionWatch();
                EditorDirector director = EditorDirector.instance;
                if (director == null)
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);

                inputState = CaptureMouseState(director);

                ActiveDrag state;
                lock (stateSync)
                    state = drag;
                if (state == null)
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);

                APIShared.Internal.GroundMovePreviewRejection modeRejection =
                    EvaluateCommandMode();
                if (modeRejection != APIShared.Internal.GroundMovePreviewRejection.None)
                {
                    AbortDrag("release-command-" +
                        ToRejectionReason(modeRejection));
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                }
                APIShared.Internal.GroundMovePreviewRejection targetRejection =
                    EvaluateFormationTargetBounds(state.Target);
                if (targetRejection != APIShared.Internal.GroundMovePreviewRejection.None)
                {
                    AbortDrag("release-target-" +
                        ToRejectionReason(targetRejection));
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                }

                if (!ValidateActiveDrag(state))
                {
                    AbortDrag("release-state-changed");
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                }
                if (!state.Authorization.IsConfirmed)
                {
                    if (FormationReleaseStateModel.HasCommandRelease(inputState, state.CommandButton))
                        AbortDrag("release-unconfirmed-move");
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                }

                bool releaseClaimed;
                lock (stateSync)
                {
                    releaseClaimed = ReferenceEquals(drag, state) &&
                        state.ReleaseGate.TryClaimVanillaRelease(inputState);
                    if (releaseClaimed)
                        drag = null;
                }
                if (!releaseClaimed)
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                ClearPreview();
                LogDebugNoThrow(
                    $"FORMATION_RELEASE_CLAIMED: tribe={state.TribeId}, " +
                    $"button={state.CommandButton}, " +
                    $"releaseEventSeen={state.ReleaseGate.ReleaseEventSeen}, " +
                    $"left={inputState.LeftState}, rightDown={inputState.RightDown}, " +
                    $"rightUp={inputState.RightUp}, stateRead={inputState.StateRead}, " +
                    $"upPending={inputState.UpPending}.");

                // The full release state was captured before dispatch. Once dispatch
                // is accepted, no fallible reflection read may stand between
                // acceptance and permanent consumption of the input edge.
                if (!TryCreatePacket(state, out FormationOrderPacket packet))
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);

                DispatchDisposition disposition = TryDispatch(packet, out string rejection);
                if (disposition != DispatchDisposition.Accepted)
                {
                    APIShared.Internal.DebugLogHelper.LogWarning(
                        log,
                        $"Formation order fell back to Vanilla: {rejection}.");
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                }

                return RunOriginalAfterReleaseConsumed(
                    state,
                    director,
                    mpFrameSkip,
                    packet,
                    inputState,
                    ref originalEntered);
            }
            catch (Exception exception)
            {
                FailOpen("engine-run", exception);
                if (originalEntered)
                    throw;
                return RunOriginalOnce(mpFrameSkip, ref originalEntered);
            }
        }

        private int RunOriginalAfterReleaseConsumed(
            ActiveDrag state,
            EditorDirector director,
            bool mpFrameSkip,
            FormationOrderPacket packet,
            FormationMouseState originalState,
            ref bool originalEntered)
        {
            if (director == null)
            {
                throw new InvalidOperationException(
                    "EditorDirector disappeared after the formation order was accepted.");
            }

            try
            {
                director.clearMouseStateForEngine();
            }
            finally
            {
                // clearMouseStateForEngine does not clear rightDown/rightUp. Applying
                // the audited terminal state also makes consumption robust if that
                // method changes part of the state before reporting a failure.
                ApplyMouseState(director, FormationReleaseStateModel.Consume());
            }
            lock (stateSync)
            {
                releaseConsumptionWatch = new ReleaseConsumptionWatch(
                    packet.OperationId, state.TribeId, state.CommandButton, 2);
            }
            LogDebugNoThrow(
                $"FORMATION_RELEASE_CONSUMED: operation={packet.OperationId}, " +
                $"tribe={state.TribeId}, button={state.CommandButton}, " +
                $"left={originalState.LeftState}, rightDown={originalState.RightDown}, " +
                $"rightUp={originalState.RightUp}, " +
                $"stateRead={originalState.StateRead}, " +
                $"upPending={originalState.UpPending}.");

            try
            {
                formationConfig.Value = state.Kind;
                densityConfig.Value = FormationModel.NormalizeDensity(state.Density);
                placementModeConfig.Value = state.PlacementMode;
                LogDebugNoThrow(
                    $"FORMATION_ORDER_QUEUED: operation={packet.OperationId}, " +
                    $"tribe={packet.TribeId}, target={packet.TargetX},{packet.TargetY}, " +
                    $"kind={(FormationKind)packet.Formation}, density={packet.Density}, " +
                    $"placement={(RangedPlacementMode)packet.PlacementMode}, direction={packet.DirectionSector}, " +
                    $"width={packet.Width}, rows={packet.Rows}, " +
                    $"units={packet.UnitCount}, " +
                    $"plan=0x{packet.PlanHash:X16}.");
            }
            catch (Exception exception)
            {
                LogWarningNoThrow(
                    $"FORMATION_SELECTION_PERSIST_FAILED: operation={packet.OperationId}, " +
                    $"error={exception.Message}.");
            }

            // The accepted packet is now the sole authority for this gesture. Do not
            // restore any release state even if the original run reports an error:
            // restoring it would enqueue a second Vanilla move on the next tick.
            return RunOriginalOnce(mpFrameSkip, ref originalEntered);
        }

        private int RunOriginalOnce(bool mpFrameSkip, ref bool originalEntered)
        {
            if (originalEntered)
                throw new InvalidOperationException(
                    "EngineInterface.run original was entered more than once for one hook invocation.");
            originalEntered = true;
            lock (stateSync)
            {
                nativeFeedbackRunGeneration = nativeFeedbackGeneration + 1;
                nativeFeedbackRunActive = true;
            }
            try
            {
                int result = engineRunOriginal(mpFrameSkip);
                // The terminal marker callback can run inside DLL_RunTick. It
                // uses this run's generation immediately, before the render
                // tail clears cursor kind. No-buffer runs never reach it and
                // return zero, so they do not publish a completed generation.
                if (result > 0)
                {
                    lock (stateSync) nativeFeedbackGeneration = nativeFeedbackRunGeneration;
                }
                return result;
            }
            finally
            {
                lock (stateSync) nativeFeedbackRunActive = false;
            }
        }

        private void CameraUpdateHook(CameraControls2D self)
        {
            ActiveDrag state;
            lock (stateSync)
                state = drag;
            if (Enabled && state != null && state.Authorization.IsConfirmed)
                self.AllowZoom = false;
            cameraUpdateOriginal(self);
            try
            {
                menuViewModel.RefreshHostState();
                menuViewModel.RefreshPreview();
            }
            catch (Exception ex) { FailOpen("camera-presentation", ex); }
        }

        private void AgeReleaseConsumptionWatch()
        {
            lock (stateSync)
            {
                if (releaseConsumptionWatch == null)
                    return;
                if (!releaseConsumptionWatch.AdvanceEngineRun())
                    releaseConsumptionWatch = null;
            }
        }

        private void DetectUnexpectedSecondOrder(
            TribeIssueOrderMoveHereEventArgs args,
            PendingFormationCommand pending)
        {
            ReleaseConsumptionWatch watch;
            lock (stateSync)
            {
                watch = releaseConsumptionWatch;
                if (watch == null || watch.Reported || args.TribeId != watch.TribeId)
                    return;
                if (pending != null && pending.Matches(args))
                    return;
                watch.Reported = true;
            }

            LogWarningNoThrow(
                $"FORMATION_UNEXPECTED_SECOND_ORDER: operation={watch.OperationId}, " +
                $"tribe={watch.TribeId}, button={watch.CommandButton}, " +
                $"target={args.TileX},{args.TileY}.");
        }

        private sealed class ReleaseConsumptionWatch
        {
            private int remainingEngineRuns;

            internal ReleaseConsumptionWatch(
                int operationId,
                int tribeId,
                int commandButton,
                int remainingEngineRuns)
            {
                OperationId = operationId;
                TribeId = tribeId;
                CommandButton = commandButton;
                this.remainingEngineRuns = remainingEngineRuns;
            }

            internal int OperationId { get; }
            internal int TribeId { get; }
            internal int CommandButton { get; }
            internal bool Reported { get; set; }

            internal bool AdvanceEngineRun()
            {
                remainingEngineRuns--;
                return remainingEngineRuns > 0;
            }
        }
    }
}
