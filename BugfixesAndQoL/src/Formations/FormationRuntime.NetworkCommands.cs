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
        private bool TryCreatePacket(ActiveDrag state, out FormationOrderPacket packet)
        {
            packet = null;
            if (EvaluateCommandMode() != APIShared.Internal.GroundMovePreviewRejection.None)
                return false;
            if (state.TribeId <= 0 || state.TribeId >= MaximumTribeCount)
                return false;
            packet = new FormationOrderPacket
            {
                ProtocolVersion = ProtocolVersion,
                OperationId = unchecked(++nextOperationId),
                TribeId = state.TribeId,
                TargetX = state.Target.NativeX,
                TargetY = state.Target.NativeY,
                IsNewOrder = 1,
                MoveType = (int)TribeMoveType.DefaultInSync,
                Formation = (byte)state.Kind,
                Density = (byte)FormationModel.NormalizeDensity(state.Density),
                PlacementMode = (byte)state.PlacementMode,
                DirectionSector = (byte)state.Geometry.Direction,
                RequestedRows = checked((ushort)Math.Min(state.Selection.Length, state.Geometry.RequestedRows)),
                UnitCount = checked((ushort)state.Selection.Length),
                SelectionHash = ComputeSelectionHash(state.Selection)
            };
            // A preview slot shortage must not cancel a move for its reachable subset.
            return packet.SelectionHash != 0;
        }

        private DispatchDisposition TryDispatch(
            FormationOrderPacket packet,
            out string rejection)
        {
            rejection = null;
            if (!GameNetworkAPI.IsMultiplayerGame())
            {
                if (ApplyPacket(packet, "singleplayer"))
                    return DispatchDisposition.Accepted;
                rejection = "Vanilla rejected the synchronized move command";
                return DispatchDisposition.Rejected;
            }

            if (packetHook == null)
            {
                rejection = "packet hook is unavailable";
                return DispatchDisposition.Rejected;
            }
            if (SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA == 0)
            {
                rejection = "Chore manager is unavailable";
                return DispatchDisposition.Rejected;
            }
            try
            {
                if (APIShared.Networking.ChoreTransport.TrySend(
                    packet, packetHook.GetPacketId(), packetHook != null,
                    out _, out rejection))
                    return DispatchDisposition.Accepted;
                return DispatchDisposition.Rejected;
            }
            catch (Exception exception)
            {
                rejection = exception.Message;
                return DispatchDisposition.Rejected;
            }
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<FormationOrderPacket> args)
        {
            if (args?.Phase != EventHookPhase.Post || args.SenderSteamId.HasValue)
                return;
            try
            {
                if (!ApplyPacket(args.Packet, "multiplayer-chore"))
                {
                    LogErrorNoThrow(
                        "Formation Chore was rejected before Vanilla could accept it.");
                }
            }
            catch (Exception exception)
            {
                LogErrorNoThrow($"Formation Chore execution failed: {exception}");
            }
        }

        private bool ApplyPacket(FormationOrderPacket packet, string source)
        {
            // Local release dispatch precedes EngineInterface.run's own lock. Received
            // Chores already hold it; both paths must plan from one synchronous state.
            lock (speechEngineLock) return ApplyPacketLocked(packet, source);
        }

        private bool ApplyPacketLocked(FormationOrderPacket packet, string source)
        {
            if (!ValidatePacket(packet, out string rejection))
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "formation-packet-" + rejection,
                    $"Rejected Formation Chore: {rejection}.");
                // Consumed rejection: never restore the release or move a different selection.
                return true;
            }
            if (!Enabled)
                return TryIssuePacketVanillaFallback(packet, source, "feature-disabled");

            var command = new PendingFormationCommand(packet, source);

            bool nested;
            lock (stateSync)
            {
                nested = pendingCommand != null || activeCommand != null;
                if (!nested)
                {
                    pendingCommand = command;
                    dispatchDepth++;
                }
            }
            // A nested command keeps the outer command intact and still reaches Vanilla
            // instead of being dropped (multiplayer) or disabling the feature (singleplayer).
            if (nested)
                return TryIssuePacketVanillaFallback(packet, source, "nested-formation-command");
            try
            {
                bool issued = GameTribeManagerAPI.Instance.IssueMoveHereCommand(
                    packet.TribeId,
                    packet.TargetX,
                    packet.TargetY,
                    isPatrolPath: false,
                    bIsNewOrder: packet.IsNewOrder,
                    tribeMoveType: (TribeMoveType)packet.MoveType);
                if (!issued)
                {
                    if (command.NativeDispatchObserved) return true;
                    Shared.DebugLogHelper.LogWarningLimited(log, "formation-dispatch-unobserved",
                        "Formation move command rejected before native dispatch.");
                    return command.NativeDispatchObserved;
                }
                return true;
            }
            finally
            {
                lock (stateSync)
                {
                    dispatchDepth--;
                    if (ReferenceEquals(pendingCommand, command))
                        pendingCommand = null;
                    if (activeCommand != null && activeCommand.Pending == command)
                    {
                        ClearUnitAssignmentFrames(activeCommand);
                        activeCommand = null;
                        commonGroupCommand = null;
                        Shared.DebugLogHelper.LogWarningLimited(log, "formation-missing-post",
                            "Formation command completed without its native Post event.");
                    }
                }
            }
        }

        private bool TryIssuePacketVanillaFallback(
            FormationOrderPacket packet,
            string source,
            string reason)
        {
            if (!ValidatePacket(packet, out _)) return true;
            FormationUnit[] selection = CaptureOrderedGroupUnits(packet.TribeId);
            if (selection.Length != packet.UnitCount || ComputeSelectionHash(selection) != packet.SelectionHash)
                return true;
            bool issued;
            PendingFormationCommand outerPending;
            ActiveFormationCommand outerActive, outerCommon;
            UnitAssignmentFrame outerFrame;
            UnitFallbackAttempt outerAttempt;
            lock (stateSync)
            {
                outerPending = pendingCommand; outerActive = activeCommand; outerCommon = commonGroupCommand;
                outerFrame = unitAssignmentFrame; outerAttempt = unitFallbackAttempt;
                // A nested identical tribe/anchor must not borrow the outer plan or
                // complete its unit frame. The native admission runtime owns its own stack.
                pendingCommand = null; activeCommand = commonGroupCommand = null;
                unitAssignmentFrame = null; unitFallbackAttempt = null;
                dispatchDepth++;
            }
            try
            {
                issued = GameTribeManagerAPI.Instance.IssueMoveHereCommand(
                    packet.TribeId, packet.TargetX, packet.TargetY,
                    isPatrolPath: false, bIsNewOrder: packet.IsNewOrder,
                    tribeMoveType: (TribeMoveType)packet.MoveType);
            }
            finally
            {
                lock (stateSync)
                {
                    ClearUnitAssignmentFrames(null);
                    pendingCommand = outerPending; activeCommand = outerActive; commonGroupCommand = outerCommon;
                    unitAssignmentFrame = outerFrame; unitFallbackAttempt = outerAttempt;
                    dispatchDepth--;
                }
            }
            Shared.DebugLogHelper.LogWarningLimited(log, "formation-fallback-" + reason,
                $"FORMATION_ORDER_FELL_BACK_TO_VANILLA: reason={reason}, issued={issued}.");
            // The native command was already submitted. A negative movement result
            // must not replay the original release as a second whole-group order.
            return true;
        }

        private static bool ValidatePacket(FormationOrderPacket packet, out string rejection)
        {
            if (packet == null)
                rejection = "packet is null";
            else if (packet.ProtocolVersion != ProtocolVersion)
                rejection = "protocol mismatch";
            else if (packet.OperationId <= 0)
                rejection = "invalid operation ID";
            else if (packet.TribeId <= 0 || packet.TribeId >= MaximumTribeCount)
                rejection = "invalid tribe ID";
            else if ((uint)packet.TargetX >= MapWidth || (uint)packet.TargetY >= MapWidth)
                rejection = "invalid target";
            else if (packet.Formation > (byte)FormationKind.Circle)
                rejection = "invalid formation";
            else if (packet.Density < 1 || packet.Density > 4)
                rejection = "invalid density";
            else if (packet.PlacementMode > (byte)RangedPlacementMode.Center)
                rejection = "invalid ranged placement mode";
            else if (packet.DirectionSector > 7)
                rejection = "invalid direction";
            else if (packet.UnitCount < 2 ||
                     packet.UnitCount > FormationPreviewMarkerModel.MaximumMarkers)
                rejection = "invalid unit count";
            else if (packet.RequestedRows > packet.UnitCount ||
                     (((FormationKind)packet.Formation == FormationKind.Circle ||
                       (FormationKind)packet.Formation == FormationKind.Vanilla) &&
                      packet.RequestedRows != 0))
                rejection = "invalid rows";
            else if (packet.SelectionHash == 0UL)
                rejection = "managed formation packet has no selection identity hash";
            else if (packet.IsNewOrder != 0 && packet.IsNewOrder != 1)
                rejection = "invalid new-order flag";
            else if (packet.MoveType != (int)TribeMoveType.DefaultInSync &&
                     packet.MoveType != (int)TribeMoveType.Fast)
                rejection = "invalid move type";
            else
            {
                rejection = null;
                return true;
            }
            return false;
        }
    }
}
