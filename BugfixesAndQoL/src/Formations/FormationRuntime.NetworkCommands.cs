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
            ResolveDirectionAndWidth(state, out int direction, out int width);
            if (state.TribeId <= 0 || state.TribeId >= MaximumTribeCount ||
                width <= 0 || width > ushort.MaxValue)
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
                DirectionSector = (byte)direction,
                Width = (ushort)width,
                Rows = checked((ushort)state.Rows),
                UnitCount = checked((ushort)state.Selection.Length),
                PlanHash = state.PreviewPlanHash
            };
            return state.HasPreviewPlan;
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
                byte[] body = GameNetworkAPI.Serialize(packet);
                if (body == null || body.Length + sizeof(short) > 1200)
                {
                    rejection = "serialized payload exceeds the Chore limit";
                    return DispatchDisposition.Rejected;
                }
                byte[] blob = new byte[body.Length + sizeof(short)];
                BitConverter.GetBytes(packetHook.GetPacketId()).CopyTo(blob, 0);
                Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
                object result = sendChorePayloadMethod.Invoke(null, new object[] { blob });
                if (result is bool sent && sent)
                    return DispatchDisposition.Accepted;
                rejection = "Chore transport refused the packet";
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
            if (args?.Phase != EventHookPhase.Post)
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
            if (!Enabled)
                return TryIssuePacketVanillaFallback(packet, source, "feature-disabled");
            if (!ValidatePacket(packet, out string rejection))
            {
                LogErrorNoThrow($"Rejected Formation Chore: {rejection}.");
                return TryIssuePacketVanillaFallback(packet, source, rejection);
            }

            var command = new PendingFormationCommand(packet, source);

            lock (stateSync)
            {
                if (pendingCommand != null || activeCommand != null)
                    throw new InvalidOperationException("A nested formation command was rejected.");
                pendingCommand = command;
                dispatchDepth++;
            }
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
                    LogWarningNoThrow(
                        $"FORMATION_ORDER_FELL_BACK_TO_VANILLA: source={source}, " +
                        $"operation={packet.OperationId}, reason=move-command-rejected.");
                    return false;
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
                        LogWarningNoThrow(
                            $"FORMATION_ORDER_FELL_BACK_TO_VANILLA: source={source}, " +
                            $"operation={packet.OperationId}, reason=missing-post-event.");
                    }
                }
            }
        }

        private bool TryIssuePacketVanillaFallback(
            FormationOrderPacket packet,
            string source,
            string reason)
        {
            if (packet == null || packet.TribeId <= 0 ||
                packet.TribeId >= MaximumTribeCount ||
                (uint)packet.TargetX >= MapWidth || (uint)packet.TargetY >= MapWidth ||
                (packet.IsNewOrder != 0 && packet.IsNewOrder != 1) ||
                (packet.MoveType != (int)TribeMoveType.DefaultInSync &&
                 packet.MoveType != (int)TribeMoveType.Fast))
                return false;
            bool issued;
            lock (stateSync) dispatchDepth++;
            try
            {
                issued = GameTribeManagerAPI.Instance.IssueMoveHereCommand(
                    packet.TribeId, packet.TargetX, packet.TargetY,
                    isPatrolPath: false, bIsNewOrder: packet.IsNewOrder,
                    tribeMoveType: (TribeMoveType)packet.MoveType);
            }
            finally { lock (stateSync) dispatchDepth--; }
            LogWarningNoThrow(
                $"FORMATION_ORDER_FELL_BACK_TO_VANILLA: source={source}, " +
                $"operation={packet.OperationId}, reason={reason}, issued={issued}.");
            return issued;
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
            else if (packet.Width == 0 || packet.Width > MaximumUnitCount)
                rejection = "invalid width";
            else if (packet.UnitCount < 2 ||
                     packet.UnitCount > FormationPreviewMarkerModel.MaximumMarkers)
                rejection = "invalid unit count";
            else if (packet.Rows == 0 || packet.Rows > packet.UnitCount ||
                     (((FormationKind)packet.Formation == FormationKind.Circle ||
                       (FormationKind)packet.Formation == FormationKind.Vanilla) &&
                      packet.Rows != FormationModel.ResolveAutomaticRows((FormationKind)packet.Formation, packet.UnitCount)))
                rejection = "invalid rows";
            else if (packet.Width != FormationModel.ResolveWidthForRows(
                         (FormationKind)packet.Formation, packet.UnitCount, packet.Rows))
                rejection = "width does not match rows";
            else if (packet.PlanHash == 0UL)
                rejection = "managed formation packet has no plan hash";
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
