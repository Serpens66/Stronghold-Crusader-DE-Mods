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
                state.Selection.Length,
                HasExplicitDirection(state), state.Rows);
            if (!force && state.HasLastPreviewKey &&
                state.LastPreviewKey.Equals(previewKey))
                return;
            state.LastPreviewKey = previewKey;
            state.HasLastPreviewKey = true;
            state.HasPreviewPlan = false;
            state.PreviewPlanHash = 0UL;

            FormationUnit[] units = new FormationUnit[state.Selection.Length];
            for (int index = 0; index < units.Length; index++)
            {
                SelectionIdentity selected = state.Selection[index];
                units[index] = new FormationUnit(
                    selected.UnitId,
                    selected.GlobalId,
                    selected.UnitType,
                    Classify((eChimps)selected.UnitType));
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
                state.PreviewPlanHash = ComputePlanHash(
                    units, destinations, state.PlacementMode, state.Rows);
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
                APIShared.Internal.DebugLogHelper.LogDebug(
                    log,
                    $"FORMATION_PREVIEW_UPDATED: kind={previewKey.Kind}, " +
                    $"density={previewKey.Density}, placement={previewKey.PlacementMode}, " +
                    $"direction={previewKey.DirectionSector}, explicitDirection={previewKey.ExplicitDirection}, " +
                    $"width={previewKey.Width}, " +
                    $"rows={previewKey.Rows}, " +
                    $"markers={new HashSet<int>(markerTiles).Count}, " +
                    $"plan=0x{state.PreviewPlanHash:X16}, " +
                    $"thread={Environment.CurrentManagedThreadId}.");
            }
            catch (Exception exception)
            {
                ClearPreview();
                APIShared.Internal.DebugLogHelper.LogDebug(
                    log,
                    $"Formation preview unavailable for this target: {exception.Message}");
            }
        }

        private static void ResolveDirectionAndWidth(
            ActiveDrag state,
            out int direction,
            out int width)
        {
            direction = state.Geometry.Direction;
            width = FormationModel.ResolveWidthForRows(state.Kind, state.Selection.Length, state.Rows);
        }

        private static bool HasExplicitDirection(ActiveDrag state) => state.Geometry.ExplicitDirection;

        private bool AuthorizePreview(ActiveDrag state)
        {
            bool allowed;
            lock (stateSync)
            {
                allowed = false;
                if (Enabled && ReferenceEquals(drag, state) && ValidateActiveDrag(state))
                {
                    GroundMoveFeedback feedback = groundFeedbackReader.Read();
                    GameCursorManager* cursor = GamePlayerManagerAPI.Instance.GetCursorManager().Pointer;
                    bool coherent = cursor != null && cursor->r_IsCursorInGame == 1 &&
                        cursor->r_MouseTileX == (uint)feedback.X &&
                        cursor->r_MouseTileY == (uint)feedback.Y;
                    bool wasConfirmed = state.Authorization.IsConfirmed;
                    long generation = nativeFeedbackRunActive
                        ? nativeFeedbackRunGeneration : nativeFeedbackGeneration;
                    allowed = state.Authorization.Observe(feedback, generation, coherent);
                    if (allowed && !wasConfirmed)
                        LogDebugNoThrow($"FORMATION_MOVE_CONFIRMED: tribe={state.TribeId}, " +
                            $"target={state.Target.NativeX},{state.Target.NativeY}, " +
                            $"generation={generation}, hoveredUnit={feedback.HoveredUnit}.");
                }
            }
            menuViewModel.SetPreviewAuthorization(allowed);
            return allowed;
        }

        private sealed class ActiveDrag
        {
            internal FormationMoveAuthorization Authorization;
            internal int MapEpoch;
            internal int PlayerId;
            internal Func<bool> PreviewAuthorization;
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
            internal FormationGestureState Geometry { get; }
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
