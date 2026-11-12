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

namespace Pathfinding.UnitCommands
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
            bool vanilla = state.UseVanillaFallback;
            packet = new FormationOrderPacket
            {
                ProtocolVersion = ProtocolVersion,
                OperationId = unchecked(++nextOperationId),
                TribeId = state.TribeId,
                TargetX = state.Target.NativeX,
                TargetY = state.Target.NativeY,
                IsNewOrder = 1,
                MoveType = (int)TribeMoveType.DefaultInSync,
                Formation = (byte)(vanilla ? FormationKind.Vanilla : state.Kind),
                Density = (byte)(vanilla ? 1 : FormationModel.NormalizeDensity(state.Density)),
                PlacementMode = (byte)(vanilla ? RangedPlacementMode.Off : state.PlacementMode),
                DirectionSector = (byte)state.Geometry.Direction,
                AspectWidth = vanilla ? (ushort)0 : checked((ushort)state.Geometry.Aspect.Width),
                AspectDepth = vanilla ? (ushort)0 : checked((ushort)state.Geometry.Aspect.Depth),
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

            if (packetChannel == null)
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
                // Release dispatch runs in EngineInterface.run, which may be on the simulation thread.
                // Keep the existing synchronous sender (channel.Send is Unity-only); receive still
                // requires APIShared's body-bound native issuer and the live tribe owner below.
                if (APIShared.Networking.ChoreTransport.TrySend(
                    packet, packetChannel.PacketId, packetChannel != null,
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

        private static bool IsAuthorizedFormationMessage(
            APIShared.Networking.AuthenticatedChoreMessage<FormationOrderPacket> message)
        {
            if (message == null || !ValidatePacket(message.Packet, out _)) return false;
            // This matches local gesture admission: a human can order only their own live tribe.
            return GameTribeManagerAPI.Instance.TryGetTribeById(message.Packet.TribeId, out GameTribe* tribe) &&
                tribe != null && tribe->r_AliveState == AliveState.IsAlive &&
                tribe->r_PlayerIdOwner == message.SenderPlayerId;
        }

        private void OnPacketReceived(APIShared.Networking.AuthenticatedChoreMessage<FormationOrderPacket> message)
        {
            if (!IsAuthorizedFormationMessage(message)) return;
            try
            {
                if (!ApplyPacket(message.Packet, "multiplayer-chore"))
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
            lock (speechEngineLock)
            {
                if (packet == null) return ApplyPacketLocked(packet, source);
                var evidence = new FormationMoveSupport(packet.TribeId, packet.TargetX, packet.TargetY);
                var previous = FormationMoveSupport.Current;
                FormationMoveSupport.Current = evidence;
                using (new FormationMoveSupport.SourceScope(source))
                {
                    try { return ApplyPacketLocked(packet, source); }
                    finally
                    {
                        FormationMoveSupport.Current = previous;
                        try { CompleteFormationSupport(packet, source, evidence); }
                        catch { } // Support logging must never change a synchronized command result.
                    }
                }
            }
        }

        private bool ApplyPacketLocked(FormationOrderPacket packet, string source)
        {
            if (!ValidatePacket(packet, out string rejection))
            {
                var evidence = FormationMoveSupport.Current;
                if (evidence != null) evidence.Veto = rejection;
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

        private void CompleteFormationSupport(FormationOrderPacket packet, string source, FormationMoveSupport evidence)
        {
            var identity = ReadFormationTribe(packet.TribeId);
            int? tick = ReadFormationTick();
            lock (stateSync)
            {
                lastSupportOperation = packet.OperationId;
                lastSupportEpoch = commandRuntime.mapEpoch;
                lastSupportTick = tick;
                lastSupportSource = source;
                lastSupportTribe = packet.TribeId;
                lastSupportPlayer = identity.Player;
                lastSupportX = packet.TargetX;
                lastSupportY = packet.TargetY;
                lastSupportResult = evidence;
            }
            if (!evidence.ShouldWarn) return;
            LogFormationSupport("move-" + (evidence.Veto ?? (evidence.PostReturn.HasValue ? "native-result" : "no-post")),
                $"FORMATION_MOVE_RESULT {FormationMission(Shared.MissionEvents.CurrentContext)} player={identity.Player} " +
                $"operation={packet.OperationId} tribe={packet.TribeId}/{identity.GlobalId} " +
                $"target={packet.TargetX},{packet.TargetY} release={(source == "singleplayer" ? "claimed" : "remote-or-local-chore")} " +
                $"tick={tick?.ToString() ?? "unknown"} source={source} {evidence.Describe()}");
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
            else if (packet.UnitCount < 1 ||
                     packet.UnitCount > MaximumUnitCount)
                rejection = "invalid unit count";
            else if (!FormationAspectRatio.IsValidPair(packet.AspectWidth, packet.AspectDepth) ||
                     (((FormationKind)packet.Formation == FormationKind.Circle ||
                       (FormationKind)packet.Formation == FormationKind.Vanilla) &&
                      (packet.AspectWidth != 0 || packet.AspectDepth != 0)))
                rejection = "invalid aspect ratio";
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
