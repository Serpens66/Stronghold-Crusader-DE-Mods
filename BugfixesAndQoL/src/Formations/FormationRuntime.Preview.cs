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
        private void PublishPreview(ActiveDrag state, bool force)
        {
            if (!state.PreviewAdmissionCaptured || state.PreviewAdmissionRevision != commandRuntime.placementRevision)
            {
                state.HasPreviewPlan = false; state.PreviewPlanHash = 0;
                ClearPreview();
                return;
            }
            APIShared.Internal.GroundMovePreviewRejection modeRejection =
                EvaluateCommandMode();
            if (modeRejection != APIShared.Internal.GroundMovePreviewRejection.None)
            {
                state.HasPreviewPlan = false;
                ClearPreview();
                return;
            }
            APIShared.Internal.GroundMovePreviewRejection targetRejection =
                EvaluateFormationTargetBounds(state.Target);
            if (targetRejection != APIShared.Internal.GroundMovePreviewRejection.None)
            {
                state.HasPreviewPlan = false;
                state.PreviewPlanHash = 0UL;
                ClearPreview();
                return;
            }
            ResolveDirectionAndWidth(state, out int direction, out int width);
            state.DirectionSector = direction;
            state.Width = width;
            FormationPreviewKey previewKey = FormationPreviewKey.Create(
                state.Kind,
                state.Density,
                state.PlacementMode,
                direction,
                width,
                state.Target.NativeX,
                state.Target.NativeY,
                state.PreviewUnits?.Length ?? state.Selection.Length,
                HasExplicitDirection(state), state.Rows);
            if (!force && state.HasLastPreviewKey &&
                state.LastPreviewKey.Equals(previewKey))
                return;
            state.LastPreviewKey = previewKey;
            state.HasLastPreviewKey = true;
            state.HasPreviewPlan = false;
            state.PreviewPlanHash = 0UL;

            FormationUnit[] units = state.PreviewUnits;
            if (units == null || units.Length == 0 || state.PreviewAdmissionRevision != commandRuntime.placementRevision)
            {
                ClearPreview();
                return;
            }

            try
            {
                NativeDestination[] destinations = BuildManagedDestinations(
                    state.Target.NativeX,
                    state.Target.NativeY,
                    state.Kind,
                    state.Density,
                    direction,
                    state.Rows,
                    state.PlacementMode,
                    units,
                    HasExplicitDirection(state),
                    out FormationDirectionIndicator directionIndicator);

                var points = new FormationPreviewPoint[destinations.Length];
                for (int index = 0; index < destinations.Length; index++)
                {
                    points[index] = new FormationPreviewPoint(
                        destinations[index].X,
                        destinations[index].Y,
                        destinations[index].Role);
                }
                // A filtered UI snapshot cannot constrain later synchronized admission.
                state.PreviewPlanHash = units.Length == state.Selection.Length ? ComputePlanHash(
                    units, destinations, state.PlacementMode, state.Rows) : 0UL;
                state.HasPreviewPlan = destinations.Length == units.Length;
                var markerTiles = new int[destinations.Length];
                for (int index = 0; index < destinations.Length; index++)
                    markerTiles[index] = destinations[index].TileId;
                markerRenderer.SetPreviewMarkerTiles(markerTiles, state.PreviewAuthorization);
                if (!markerRenderer.ReplacementAvailable)
                {
                    menuViewModel.ClearPreview();
                    return;
                }
                menuViewModel.PublishPreview(points, directionIndicator);
            }
            catch (Exception exception)
            {
                Shared.DevDiag.Count("manual.preview.slotsFailure");
                ClearPreview();
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/src/Formations/FormationRuntime.Preview.cs:129", $"Formation preview unavailable for this target: {exception.Message}");
            }
        }

        private static void ResolveDirectionAndWidth(
            ActiveDrag state,
            out int direction,
            out int width)
        {
            direction = state.Geometry.Direction;
            width = FormationModel.ResolveWidthForRows(state.Kind, Math.Max(1,
                state.PreviewUnits?.Length ?? state.Selection.Length), state.Rows);
        }

        private static bool HasExplicitDirection(ActiveDrag state) => state.Geometry.ExplicitDirection;

        private bool AuthorizePreview(ActiveDrag state)
        {
            bool allowed;
            lock (stateSync)
            {
                allowed = false;
                if (nativeFeedbackRunActive && speechEngineLock != null && System.Threading.Monitor.IsEntered(speechEngineLock) &&
                    LocalInputEnabled && ReferenceEquals(drag, state) && ValidateActiveDrag(state))
                {
                    GroundMoveFeedback feedback = groundFeedbackReader.Read();
                    GameCursorManager* cursor = GamePlayerManagerAPI.Instance.GetCursorManager().Pointer;
                    bool coherent = cursor != null && cursor->r_IsCursorInGame == 1 &&
                        cursor->r_MouseTileX == (uint)feedback.X &&
                        cursor->r_MouseTileY == (uint)feedback.Y;
                    bool wasConfirmed = state.Authorization.IsConfirmed;
                    allowed = state.Authorization.Observe(feedback, nativeFeedbackRunGeneration, coherent);
                    if (!wasConfirmed && allowed) Shared.DevDiag.Count("manual.gesture.confirmed");
                    if (!allowed) Shared.DevDiag.Count("manual.gesture.unconfirmed");
                }
            }
            if (state.DisplayedAuthorization != allowed)
            {
                state.DisplayedAuthorization = allowed;
                Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => {
                    lock (stateSync) if (ReferenceEquals(drag, state) && state.DisplayedAuthorization == allowed)
                        menuViewModel.SetPreviewAuthorization(allowed);
                });
            }
            return allowed;
        }

        private void ObserveNativeGroundFeedback()
        {
            ActiveDrag state;
            lock (stateSync) state = nativeFeedbackRunActive ? drag : null;
            if (state == null) return;
            AuthorizePreview(state);
            bool captured;
            lock (stateSync) captured = ReferenceEquals(drag, state) && CapturePreviewAdmission(state);
            if (captured) Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => {
                lock (stateSync) if (ReferenceEquals(drag, state) && ValidateActiveDrag(state)) PublishPreview(state, true);
            });
        }

        private bool CapturePreviewAdmission(ActiveDrag state)
        {
            if (state.PreviewAdmissionCaptured || speechEngineLock == null) return false;
            bool taken = false;
            try
            {
                // UI never waits for simulation. If busy, the next native render entry
                // captures once under the same validated engine lock, before presentation.
                System.Threading.Monitor.TryEnter(speechEngineLock, 0, ref taken);
                if (!taken) return false;
                state.PreviewAdmissionCaptured = true;
                state.PreviewUnits = commandRuntime.CapturePreviewFormationAdmission(state.PreviewUnits,
                    state.TribeId, state.Target.NativeX, state.Target.NativeY, out state.PreviewAdmissionRevision);
                state.Geometry = state.Geometry.WithCount(state.PreviewUnits.Length);
                return true;
            }
            catch (Exception)
            {
                state.PreviewUnits = null; // Gesture proof and authoritative dispatch are independent.
                Shared.DevDiag.Count("manual.preview.admissionFailure");
                return true;
            }
            finally { if (taken) System.Threading.Monitor.Exit(speechEngineLock); }
        }

        private sealed class ActiveDrag
        {
            internal FormationMoveAuthorization Authorization;
            internal int MapEpoch;
            internal int PlayerId;
            internal Func<bool> PreviewAuthorization;
            internal bool DisplayedAuthorization;
            internal FormationUnit[] PreviewUnits;
            internal long PreviewAdmissionRevision;
            internal bool PreviewAdmissionCaptured;
            internal ActiveDrag(
                int commandButton,
                int tribeId,
                GroundTarget target,
                SelectionIdentity[] selection,
                FormationKind kind,
                int density,
                RangedPlacementMode placementMode,
                int rememberedRows)
            {
                CommandButton = commandButton;
                TribeId = tribeId;
                Target = target;
                Selection = selection;
                Kind = kind;
                Density = density;
                PlacementMode = placementMode;
                ReleaseGate = new FormationReleaseGate(commandButton);
                int sumX = 0;
                int sumY = 0;
                for (int index = 0; index < selection.Length; index++)
                {
                    sumX += selection[index].X;
                    sumY += selection[index].Y;
                }
                int centerX = sumX / selection.Length;
                int centerY = sumY / selection.Length;
                DefaultDirectionSector = FormationModel.QuantizeDirection(
                    target.NativeX - centerX,
                    target.NativeY - centerY,
                    0);
                Geometry = new FormationGestureState(kind, selection.Length, DefaultDirectionSector, rememberedRows);
            }

            internal int CommandButton { get; }
            internal int TribeId { get; }
            internal GroundTarget Target { get; }
            internal SelectionIdentity[] Selection { get; }
            internal FormationKind Kind { get; }
            internal int Density { get; }
            internal RangedPlacementMode PlacementMode { get; }
            internal FormationReleaseGate ReleaseGate { get; }
            internal FormationGestureState Geometry { get; set; }
            internal int Rows => Geometry.Rows;
            internal int DefaultDirectionSector { get; }
            internal int DirectionSector { get; set; }
            internal int Width { get; set; }
            internal bool HasLastPreviewKey { get; set; }
            internal FormationPreviewKey LastPreviewKey { get; set; }
            internal bool HasPreviewPlan { get; set; }
            internal ulong PreviewPlanHash { get; set; }
        }

        private enum DispatchDisposition
        {
            Rejected = 0,
            Accepted = 1
        }
    }
}
