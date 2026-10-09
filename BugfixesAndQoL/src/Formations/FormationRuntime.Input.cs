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
            if (!Enabled || args == null)
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
            if (!Enabled || args == null)
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
            if (!Enabled || args == null)
                return;
            try
            {
                RequireMainThread("input-up");
                if (args.Phase != EventHookPhase.Post)
                    return;
                ActiveDrag state;
                lock (stateSync)
                    state = drag;
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
                APIShared.Internal.DebugLogHelper.LogDebug(
                    log,
                    $"FORMATION_RELEASE_EVENT: button={state.CommandButton}, " +
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
                        int oldRows = state.Rows;
                        state.Geometry.ApplyWheel(wheel, frame);
                        if (state.Rows != oldRows) changedRows = state.Rows;
                    }

                    if (TryCaptureTarget(out GroundTarget endpoint))
                    {
                        state.Geometry.UpdateDirection(
                            endpoint.NativeX - state.Target.NativeX,
                            endpoint.NativeY - state.Target.NativeY, MinimumDragTileDistance);
                    }
                    PublishPreview(state, force: false);
                }
            }
            if (abort)
                AbortDrag("state-changed");
            else if (changedRows != 0)
            {
                menuViewModel.RememberSelectedRows(state.Kind, changedRows);
                LogDebugNoThrow(
                    $"FORMATION_ROWS_CHANGED: rows={changedRows}, " +
                    $"thread={Environment.CurrentManagedThreadId}.");
            }
        }

        private void TryStartDrag(int commandButton)
        {
            APIShared.Internal.GroundMovePreviewRejection modeRejection =
                EvaluateCommandMode();
            if (modeRejection != APIShared.Internal.GroundMovePreviewRejection.None)
            {
                LogTargetRejection(modeRejection, default);
                return;
            }
            if (!HasValidMap() || markerRenderer == null ||
                !markerRenderer.ReplacementAvailable || FatControler.instance == null ||
                FatControler.instance.overNoesisGUI() || IsShiftHeld())
                return;

            menuViewModel.CloseMenu();
            if (!TryCaptureSelection(out SelectionIdentity[] selection, out int tribeId))
                return;
            if (!TryCaptureCommandTarget(
                    out GroundTarget target,
                    out APIShared.Internal.GroundMovePreviewRejection targetRejection))
            {
                if (targetRejection != APIShared.Internal.GroundMovePreviewRejection.None)
                    LogTargetRejection(targetRejection, target);
                return;
            }

            var state = new ActiveDrag(
                commandButton,
                tribeId,
                target,
                selection,
                FormationModel.NormalizeKind((int)formationConfig.Value),
                FormationModel.NormalizeDensity(densityConfig.Value),
                FormationModel.NormalizePlacementMode((int)placementModeConfig.Value),
                menuViewModel.GetRememberedRows(FormationModel.NormalizeKind((int)formationConfig.Value)));
            state.MapEpoch = commandRuntime.mapEpoch;
            state.PlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            state.Authorization = new FormationMoveAuthorization(
                state.PlayerId, tribeId, selection.Length,
                target.NativeX, target.NativeY, nativeFeedbackGeneration);
            menuViewModel.SetPreviewAuthorization(false);
            state.PreviewAuthorization = () => AuthorizePreview(state);
            ResolveDirectionAndWidth(state, out int direction, out int width);
            state.DirectionSector = direction;
            state.Width = width;
            lock (stateSync)
            {
                releaseConsumptionWatch = null;
                drag = state;
                PublishPreview(state, force: true);
            }
            APIShared.Internal.DebugLogHelper.LogDebug(
                log,
                $"FORMATION_DRAG_START: tribe={tribeId}, units={selection.Length}, " +
                $"target={target.NativeX},{target.NativeY}, kind={state.Kind}, " +
                $"density={state.Density}, placement={state.PlacementMode}.");
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
