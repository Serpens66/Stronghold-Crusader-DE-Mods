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


        private void OnKeyDown(UnityInputEventArgs args)
        {
            if (!LocalInputEnabled || args == null)
                return;
            try
            {
                RequireMainThread("input-down");
                if (args.Phase != EventHookPhase.Post)
                    return;
                ActiveDrag state;
                lock (stateSync)
                    state = drag;

                if (state != null)
                {
                    if (TryGetMouseButton(args.Key, out int pressedButton) &&
                        state.ReleaseGate.ReleaseEventSeen)
                    {
                        AbortDrag("stale-release-before-new-down");
                        if (pressedButton == state.CommandButton)
                            TryStartDrag(state.CommandButton);
                        return;
                    }
                    if (!state.ReleaseGate.CanModify)
                        return;
                    return;
                }

                int commandButton = GetCommandMouseButton();
                if (args.Key == ToKeyCode(commandButton))
                    TryStartDrag(commandButton);
            }
            catch (Exception exception)
            {
                FailOpen("input-down", exception);
            }
        }

        private void OnKeyHeld(UnityInputEventArgs args)
        {
            if (!LocalInputEnabled || args == null)
                return;
            try
            {
                RequireMainThread("input-held");
                if (args.Phase != EventHookPhase.Post)
                    return;
                ActiveDrag state;
                lock (stateSync)
                    state = drag;
                if (state == null || !state.ReleaseGate.CanModify ||
                    args.Key != ToKeyCode(state.CommandButton))
                    return;
                APIShared.Internal.GroundMovePreviewRejection modeRejection =
                    EvaluateCommandMode();
                if (modeRejection != APIShared.Internal.GroundMovePreviewRejection.None)
                {
                    AbortDrag("command-" + ToRejectionReason(modeRejection));
                    return;
                }
                APIShared.Internal.GroundMovePreviewRejection targetRejection =
                    EvaluateFormationTargetBounds(state.Target);
                if (targetRejection != APIShared.Internal.GroundMovePreviewRejection.None)
                {
                    AbortDrag("target-" + ToRejectionReason(targetRejection));
                    return;
                }
                UpdateGesture(state);
            }
            catch (Exception exception)
            {
                FailOpen("input-held", exception);
            }
        }

        private void OnKeyUp(UnityInputEventArgs args)
        {
            if (!LocalInputEnabled || args == null)
                return;
            try
            {
                RequireMainThread("input-up");
                if (args.Phase != EventHookPhase.Post)
                    return;
                ActiveDrag state;
                lock (stateSync)
                {
                    state = drag;
                    // A click released before its deferred start completed stays Vanilla.
                    if (state == null && pendingStart != null && args.Key == ToKeyCode(pendingStart.CommandButton))
                        pendingStart = null;
                }
                if (state == null || args.Key != ToKeyCode(state.CommandButton))
                    return;
                if (FatControler.instance != null &&
                    FatControler.instance.overNoesisGUI())
                {
                    AbortDrag("release-over-ui");
                    return;
                }
                APIShared.Internal.GroundMovePreviewRejection modeRejection =
                    EvaluateCommandMode();
                if (modeRejection != APIShared.Internal.GroundMovePreviewRejection.None)
                {
                    AbortDrag("release-command-" +
                        ToRejectionReason(modeRejection));
                    return;
                }

                UpdateGesture(state);
                bool releaseObserved = false;
                lock (stateSync)
                {
                    if (ReferenceEquals(drag, state))
                        releaseObserved = state.ReleaseGate.ObserveInputRelease();
                }
                if (!releaseObserved)
                    return;
                ClearPreview();
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/src/Formations/FormationRuntime.Input.cs:145", $"FORMATION_RELEASE_EVENT: button={state.CommandButton}, " +
                        $"thread={Environment.CurrentManagedThreadId}; awaiting native release.");
            }
            catch (Exception exception)
            {
                FailOpen("input-up", exception);
            }
        }

        private void UpdateGesture(ActiveDrag state)
        {
            bool abort;
            int changedRows = 0;
            lock (stateSync)
            {
                if (!ReferenceEquals(drag, state) || !state.ReleaseGate.CanModify)
                    return;
                abort = !ValidateActiveDrag(state);
                if (!abort)
                {
                    int frame = Time.frameCount;
                    float wheel = state.Authorization.IsConfirmed ? Input.mouseScrollDelta.y : 0f;
                    if (wheel != 0f && lastWheelFrame != frame)
                    {
                        lastWheelFrame = frame;
                        int oldRows = state.Geometry.RequestedRows;
                        state.Geometry.ApplyWheel(wheel, frame);
                        if (state.Geometry.RequestedRows != oldRows) changedRows = state.Geometry.RequestedRows;
                    }

                    if (TryCaptureTarget(out GroundTarget endpoint))
                    {
                        state.Geometry.UpdateDirection(
                            endpoint.NativeX - state.Target.NativeX,
                            endpoint.NativeY - state.Target.NativeY, MinimumDragTileDistance);
                    }
                }
            }
            if (abort)
                AbortDrag("state-changed");
            else
            {
                PublishPreview(state, force: false);
                if (changedRows != 0) menuViewModel.RememberSelectedRows(state.Kind, changedRows);
            }
        }

        private void TryStartDrag(int commandButton)
        {
            APIShared.Internal.GroundMovePreviewRejection modeRejection =
                EvaluateCommandMode();
            if (modeRejection != APIShared.Internal.GroundMovePreviewRejection.None)
            {
                return;
            }
            if (!HasValidMap() || markerRenderer == null ||
                !markerRenderer.ReplacementAvailable || FatControler.instance == null ||
                FatControler.instance.overNoesisGUI() || IsShiftHeld())
                return;

            menuViewModel.CloseMenu();
            // Unity mouse/map state is main-thread data; only the native selection needs the engine lock.
            if (speechEngineLock == null || !TryCaptureCommandTarget(out GroundTarget target, out _))
                return;
            FormationKind kind = FormationModel.NormalizeKind((int)formationConfig.Value);
            var start = new PendingDragStart
            {
                CommandButton = commandButton,
                MapEpoch = commandRuntime.mapEpoch,
                PlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId(),
                Target = target,
                Kind = kind,
                Density = FormationModel.NormalizeDensity(densityConfig.Value),
                PlacementMode = FormationModel.NormalizePlacementMode((int)placementModeConfig.Value),
                RememberedRows = menuViewModel.GetRememberedRows(kind)
            };
            SelectionIdentity[] selection;
            int tribeId;
            bool taken = false;
            try
            {
                System.Threading.Monitor.TryEnter(speechEngineLock, 0, ref taken);
                if (!taken)
                {
                    // Never stall the frame for a running tick: the next EngineInterface.run
                    // hook completes this start under the lock it already holds.
                    // Rejected (2026-10-26): blocking Monitor.Enter stalls every command click
                    // until the tick ends (long AI ticks: hundreds of ms); dropping to Vanilla
                    // loses the formation under load.
                    lock (stateSync) pendingStart = start;
                    Shared.DevDiag.Count("manual.gesture.deferredStart");
                    return;
                }
                if (!TryCaptureSelection(out selection, out tribeId)) return;
            }
            finally { if (taken) System.Threading.Monitor.Exit(speechEngineLock); }
            CompleteDragStart(start, selection, tribeId);
        }

        // Runs on the main thread (free lock) or inside the engine-run hook (deferred start).
        private void CompleteDragStart(PendingDragStart start, SelectionIdentity[] selection, int tribeId)
        {
            var state = new ActiveDrag(
                start.CommandButton,
                tribeId,
                start.Target,
                selection,
                start.Kind,
                start.Density,
                start.PlacementMode,
                start.RememberedRows);
            state.MapEpoch = start.MapEpoch;
            state.PlayerId = start.PlayerId;
            var previewUnits = new FormationUnit[selection.Length];
            for (int i = 0; i < previewUnits.Length; i++)
                previewUnits[i] = new FormationUnit(selection[i].UnitId, selection[i].GlobalId,
                    selection[i].UnitType, Classify((eChimps)selection[i].UnitType));
            state.PreviewUnits = previewUnits;
            state.Authorization = new FormationMoveAuthorization(
                state.PlayerId, tribeId, selection.Length,
                start.Target.NativeX, start.Target.NativeY, System.Threading.Interlocked.Read(ref nativeFeedbackGeneration));
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => menuViewModel.SetPreviewAuthorization(false));
            state.PreviewAuthorization = () => state.Authorization.IsConfirmed;
            ResolveDirectionAndWidth(state, out int direction, out int width);
            state.DirectionSector = direction;
            state.Width = width;
            lock (stateSync)
            {
                releaseConsumptionWatch = null;
                drag = state;
                groundFrameObserver.SetActive(true);
            }
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => PublishPreview(state, force: true));
        }

        // Engine thread, lock not yet held: complete a start deferred by a busy engine lock.
        private ActiveDrag TryCompletePendingStart()
        {
            lock (speechEngineLock)
            {
                PendingDragStart start;
                lock (stateSync)
                {
                    start = pendingStart;
                    pendingStart = null;
                    if (start == null || drag != null) return drag;
                }
                if (start.MapEpoch != commandRuntime.mapEpoch ||
                    start.PlayerId != GamePlayerManagerAPI.Instance.GetLocalPlayerId() ||
                    EvaluateCommandMode() != APIShared.Internal.GroundMovePreviewRejection.None ||
                    !TryCaptureSelection(out SelectionIdentity[] selection, out int tribeId))
                    return null;
                CompleteDragStart(start, selection, tribeId);
                lock (stateSync) return drag;
            }
        }

        private sealed class PendingDragStart
        {
            internal int CommandButton, MapEpoch, PlayerId, Density, RememberedRows;
            internal GroundTarget Target;
            internal FormationKind Kind;
            internal RangedPlacementMode PlacementMode;
        }

        private static bool IsShiftHeld()
        {
            KeyManager manager = KeyManager.instance;
            return manager != null &&
                (manager.IsKeyHeldDown(KeyCode.LeftShift, ignoreModifiers: true) ||
                 manager.IsKeyHeldDown(KeyCode.RightShift, ignoreModifiers: true));
        }

        private static int GetCommandMouseButton() =>
            ConfigSettings.Settings_SH1RTSControls ? 0 : 1;

        private static KeyCode ToKeyCode(int mouseButton) =>
            mouseButton == 0 ? KeyCode.Mouse0 : KeyCode.Mouse1;

        private static bool TryGetMouseButton(KeyCode key, out int mouseButton)
        {
            if (key == KeyCode.Mouse0)
            {
                mouseButton = 0;
                return true;
            }
            if (key == KeyCode.Mouse1)
            {
                mouseButton = 1;
                return true;
            }
            mouseButton = -1;
            return false;
        }

        private FormationMouseState CaptureMouseState(EditorDirector director) =>
            new FormationMouseState(
                (int)leftMouseStateField.GetValue(director),
                (bool)rightMouseDownField.GetValue(director),
                (bool)rightMouseUpField.GetValue(director),
                (bool)mouseStateReadField.GetValue(director),
                (bool)mouseUpPendingField.GetValue(director));

        private void ApplyMouseState(
            EditorDirector director,
            FormationMouseState state)
        {
            leftMouseStateField.SetValue(director, state.LeftState);
            rightMouseDownField.SetValue(director, state.RightDown);
            rightMouseUpField.SetValue(director, state.RightUp);
            mouseStateReadField.SetValue(director, state.StateRead);
            mouseUpPendingField.SetValue(director, state.UpPending);
        }
    }
}
