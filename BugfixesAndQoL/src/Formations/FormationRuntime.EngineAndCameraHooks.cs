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
                {
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        LogDebugNoThrow("FORMATION_RUNTIME_AFTER_STARTUP_CLEANUP: persistent EngineInterface.run callback reached.");
                }
            }
            if (!LocalInputEnabled)
            {
                try
                {
                    if (!Enabled) ResetTransientState();
                    else AbortDrag("local-input-disabled");
                }
                catch (Exception ex) { FailOpen("disabled-reset", ex); }
                return engineRunOriginal(mpFrameSkip);
            }

            bool originalEntered = false;
            FormationMouseState inputState = default;
            ActiveDrag state = null;
            try
            {
                AgeReleaseConsumptionWatch();
                EditorDirector director = EditorDirector.instance;
                if (director == null)
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);

                lock (stateSync)
                    state = drag;
                if (state == null)
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);

                inputState = CaptureMouseState(director);
                FormationReleaseDisposition disposition;
                FormationOrderPacket packet;
                FormationSpeechRequest speechRequest;
                lock (speechEngineLock)
                    disposition = PrepareFormationRelease(state, inputState, out packet, out speechRequest);
                if (disposition == FormationReleaseDisposition.Deferred)
                    return RunOriginalWithDeferredRelease(state, director, inputState, mpFrameSkip, ref originalEntered);
                if (disposition == FormationReleaseDisposition.Cancelled)
                {
                    ConsumeGestureRelease(state, director, 0);
                    QueueSpeech(null);
                    ClearPreview();
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                }
                if (disposition != FormationReleaseDisposition.Accepted)
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                return RunOriginalAfterReleaseConsumed(state, director, mpFrameSkip,
                    packet, inputState, speechRequest, ref originalEntered);
            }
            catch (Exception exception)
            {
                // Once claimed, even a presentation/dispatch exception must not hand
                // this release back to Vanilla at the current mouse position.
                if (!originalEntered && state?.ReleaseGate.VanillaReleaseClaimed == true)
                {
                    try { ConsumeGestureRelease(state, EditorDirector.instance, 0); } catch { }
                    try { QueueSpeech(null); } catch { }
                }
                FailOpen("engine-run", exception);
                if (originalEntered)
                    throw;
                return RunOriginalOnce(mpFrameSkip, ref originalEntered);
            }
        }

        private enum FormationReleaseDisposition { Vanilla, Deferred, Cancelled, Accepted }

        // Caller holds the engine lock; do not run the native frame while preparing.
        private FormationReleaseDisposition PrepareFormationRelease(ActiveDrag state,
            FormationMouseState input, out FormationOrderPacket packet, out FormationSpeechRequest speech)
        {
            packet = null; speech = null;
            if (!ValidateActiveDrag(state) ||
                EvaluateCommandMode() != APIShared.Internal.GroundMovePreviewRejection.None ||
                EvaluateFormationTargetBounds(state.Target) != APIShared.Internal.GroundMovePreviewRejection.None)
            {
                AbortDrag("release-context-changed");
                return FormationReleaseDisposition.Vanilla;
            }
            GestureSelectionState selection = RefreshGestureSelection(state);
            if (selection == GestureSelectionState.Changed)
            {
                AbortDrag("release-selection-changed");
                return FormationReleaseDisposition.Vanilla;
            }
            if (!FormationReleaseStateModel.HasCommandRelease(input, state.CommandButton))
                return FormationReleaseDisposition.Vanilla;
            if (selection == GestureSelectionState.Unavailable)
            {
                if (state.Authorization.IsConfirmed || (state.Geometry.ExplicitDirection && state.Authorization.OwnsGroundGesture))
                {
                    lock (stateSync)
                    {
                        state.ReleaseGate.ObserveInputRelease(); // Freeze controls even without a managed KeyUp.
                        state.ReleaseDeferred = true;
                    }
                    return FormationReleaseDisposition.Deferred;
                }
                AbortDrag("release-unavailable-unclassified");
                return FormationReleaseDisposition.Vanilla;
            }
            bool cancelled = selection == GestureSelectionState.Empty || !state.Authorization.IsConfirmed;
            if (cancelled && selection != GestureSelectionState.Empty &&
                (!state.Geometry.ExplicitDirection || !state.Authorization.OwnsGroundGesture))
            {
                AbortDrag("release-unclassified");
                return FormationReleaseDisposition.Vanilla;
            }
            lock (stateSync)
            {
                if (!ReferenceEquals(drag, state) || !state.ReleaseGate.TryClaimVanillaRelease(input))
                    return FormationReleaseDisposition.Vanilla;
                drag = null;
                state.ReleaseDeferred = false;
                RefreshNativeObserverDemand();
            }
            if (cancelled) return FormationReleaseDisposition.Cancelled;
            // The identity snapshot and packet are captured under the same engine lock.
            if (!TryCreatePacket(state, out packet)) return FormationReleaseDisposition.Cancelled;
            speech = CaptureSpeech(state, (TribeMoveType)packet.MoveType);
            if (TryDispatch(packet, out string rejection) == DispatchDisposition.Accepted)
                return FormationReleaseDisposition.Accepted;
            Shared.DebugLogHelper.LogWarningLimited(log, "formation-anchored-dispatch",
                $"Anchored formation dispatch rejected: {rejection}.");
            // Ownership is already established; a technical rejection cannot retarget the release.
            return FormationReleaseDisposition.Cancelled;
        }

        private void ConsumeGestureRelease(ActiveDrag state, EditorDirector director, int operationId)
        {
            if (director == null) throw new InvalidOperationException("EditorDirector disappeared during release.");
            try { director.clearMouseStateForEngine(); }
            finally { ApplyMouseState(director, FormationReleaseStateModel.Consume()); }
            lock (stateSync)
                releaseConsumptionWatch = new ReleaseConsumptionWatch(operationId, state.TribeId, state.CommandButton, 2);
        }

        private int RunOriginalWithDeferredRelease(ActiveDrag state, EditorDirector director, FormationMouseState input,
            bool mpFrameSkip, ref bool originalEntered)
        {
            ApplyMouseState(director, FormationReleaseStateModel.Consume());
            try { return RunOriginalOnce(mpFrameSkip, ref originalEntered); }
            finally
            {
                // No command accepted: retry this edge only while the same gesture
                // still owns it. An abort/new press during the native run must not revive it.
                lock (stateSync)
                    if (ReferenceEquals(drag, state) && state.ReleaseDeferred) ApplyMouseState(director, input);
            }
        }

        private void DiscardDeferredRelease(ActiveDrag state)
        {
            if (state == null || !state.ReleaseDeferred || EditorDirector.instance == null) return;
            FormationMouseState current = CaptureMouseState(EditorDirector.instance);
            ApplyMouseState(EditorDirector.instance, FormationReleaseStateModel.DiscardDeferred(current, state.CommandButton));
            state.ReleaseDeferred = false;
        }

        private int RunOriginalAfterReleaseConsumed(
            ActiveDrag state,
            EditorDirector director,
            bool mpFrameSkip,
            FormationOrderPacket packet,
            FormationMouseState originalState,
            FormationSpeechRequest speechRequest,
            ref bool originalEntered)
        {
            ConsumeGestureRelease(state, director, packet.OperationId);
            ClearPreview();
            QueueSpeech(speechRequest);

            try
            {
                Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => {
                    formationConfig.Value = state.Kind;
                    densityConfig.Value = FormationModel.NormalizeDensity(state.Density);
                    placementModeConfig.Value = state.PlacementMode;
                });
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
            var previous = FormationNativeRunContext.Enter(this,
                System.Threading.Interlocked.Increment(ref nativeFeedbackGeneration));
            try
            {
                // Only a real native render entry can confirm input or emit speech.
                // A no-buffer managed run cannot manufacture that proof.
                return engineRunOriginal(mpFrameSkip);
            }
            finally
            {
                FormationNativeRunContext.Restore(previous);
            }
        }

        private void CameraUpdateHook(CameraControls2D self)
        {
            ActiveDrag state;
            lock (stateSync)
                state = drag;
            if (LocalInputEnabled && state != null && state.Authorization.IsConfirmed)
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
