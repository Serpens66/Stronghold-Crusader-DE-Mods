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
        internal void OnTribeIssueOrderMoveHere(TribeIssueOrderMoveHereEventArgs args)
        {
            if (!Enabled || args == null)
                return;

            if (args.Phase == EventHookPhase.Pre)
            {
                PendingFormationCommand pending;
                lock (stateSync)
                {
                    pending = pendingCommand;
                }
                DetectUnexpectedSecondOrder(args, pending);
                if (pending == null || !pending.Matches(args))
                    return;

                try
                {
                    FormationOrderPacket packet = pending.Packet;
                    FormationKind kind = FormationModel.NormalizeKind(packet.Formation);
                    int density = FormationModel.NormalizeDensity(packet.Density);
                    FormationUnit[] units = CaptureOrderedGroupUnits(packet.TribeId);
                    if (units.Length == 0)
                        throw new InvalidOperationException(
                            "The commanded tribe has no active units at dispatch.");
                    if (units.Length != packet.UnitCount)
                    {
                        throw new InvalidOperationException(
                            $"Formation unit count changed: packet={packet.UnitCount}, " +
                            $"current={units.Length}.");
                    }

                    NativeDestination[] destinations = BuildManagedDestinations(
                        packet.TargetX,
                        packet.TargetY,
                        kind,
                        density,
                        packet.DirectionSector,
                        packet.Rows,
                        FormationModel.NormalizePlacementMode(packet.PlacementMode),
                        units,
                        explicitDirection: false,
                        out _);
                    ulong actualPlanHash = ComputePlanHash(
                        units, destinations,
                        FormationModel.NormalizePlacementMode(packet.PlacementMode), packet.Rows);
                    if (actualPlanHash != packet.PlanHash)
                    {
                        throw new InvalidOperationException(
                            $"Formation plan hash mismatch: packet=0x{packet.PlanHash:X16}, " +
                            $"current=0x{actualPlanHash:X16}.");
                    }

                    ActiveFormationCommand active = new ActiveFormationCommand(
                        pending, kind, density, units, destinations);
                    lock (stateSync)
                    {
                        if (!ReferenceEquals(pendingCommand, pending) || activeCommand != null)
                            return;
                        pendingCommand = null;
                        activeCommand = active;
                    }
                    LogDebugNoThrow(
                        $"FORMATION_ORDER_SCOPE_PRE: source={pending.Source}, " +
                        $"operation={packet.OperationId}, tribe={packet.TribeId}, " +
                        $"expected={units.Length}, kind={kind}, " +
                        $"thread={Environment.CurrentManagedThreadId}.");
                }
                catch (Exception exception)
                {
                    lock (stateSync)
                    {
                        if (ReferenceEquals(pendingCommand, pending))
                            pendingCommand = null;
                        ClearUnitAssignmentFrames(null);
                        activeCommand = null;
                        commonGroupCommand = null;
                    }
                    LogWarningNoThrow(
                        $"FORMATION_ORDER_FELL_BACK_TO_VANILLA: source={pending.Source}, " +
                        $"operation={pending.Packet.OperationId}, reason=preparation-failed, " +
                        $"error={exception.Message}.");
                }
                return;
            }

            if (args.Phase != EventHookPhase.Post)
                return;

            ActiveFormationCommand completed = null;
            try
            {
                lock (stateSync)
                {
                    if (activeCommand != null && activeCommand.Matches(args))
                        completed = activeCommand;
                }
            }
            finally
            {
                lock (stateSync)
                {
                    if (ReferenceEquals(activeCommand, completed))
                    {
                        ClearUnitAssignmentFrames(completed);
                        activeCommand = null;
                    }
                    if (ReferenceEquals(commonGroupCommand, completed))
                        commonGroupCommand = null;
                }
            }

            if (completed == null)
                return;
            int successfulFormationTargets = completed.SuccessfulTerminalCount;
            UnitFallbackSummary fallbackSummary = RunUnitFallbacks(completed);
            string common =
                $"source={completed.Pending.Source}, " +
                $"operation={completed.Pending.Packet.OperationId}, " +
                $"tribe={completed.TribeId}, path={completed.AssignmentPath}, " +
                $"selectorAssigned={completed.AssignedCount}, " +
                $"terminalAttempts={completed.TerminalAttemptCount}, " +
                $"formationSucceeded={successfulFormationTargets}, " +
                $"fallbackSucceeded={fallbackSummary.Succeeded}, " +
                $"fallbackFailed={fallbackSummary.Failed}, " +
                $"expected={completed.ExpectedCount}";
            if (successfulFormationTargets == completed.ExpectedCount)
                LogDebugNoThrow($"FORMATION_ORDER_APPLIED: {common}.");
            else if (successfulFormationTargets > 0)
                LogWarningNoThrow($"FORMATION_ORDER_PARTIAL_FALLBACK: {common}.");
            else
                LogWarningNoThrow($"FORMATION_ORDER_FELL_BACK_TO_VANILLA: {common}.");
        }

        private UnitFallbackSummary RunUnitFallbacks(ActiveFormationCommand command)
        {
            UnitFallbackRequest[] requests = command.GetFallbackRequests();
            int succeeded = 0;
            int failedCount = 0;
            for (int index = 0; index < requests.Length; index++)
            {
                UnitFallbackRequest request = requests[index];
                if (!TryGetMatchingUnit(
                        request.UnitId, request.GlobalId, out _))
                {
                    failedCount++;
                    LogWarningNoThrow(
                        $"FORMATION_UNIT_FALLBACK_ERROR: " +
                        $"operation={command.Pending.Packet.OperationId}, " +
                        $"unit={request.UnitId}, error=unit-identity-changed.");
                    continue;
                }
                var attempt = new UnitFallbackAttempt(
                    request.UnitId,
                    command.TargetX,
                    command.TargetY,
                    request.Unknown);
                lock (stateSync)
                {
                    if (unitFallbackAttempt != null)
                        throw new InvalidOperationException("Nested unit fallback was rejected.");
                    unitFallbackAttempt = attempt;
                }
                try
                {
                    if (APIShared.UnitAccess.TryGetById(request.UnitId, out _, out _)) GameUnitManagerAPI.Instance.MoveToTile(
                        request.UnitId,
                        command.TargetX,
                        command.TargetY,
                        request.Unknown);
                }
                catch (Exception exception)
                {
                    LogWarningNoThrow(
                        $"FORMATION_UNIT_FALLBACK_ERROR: " +
                        $"operation={command.Pending.Packet.OperationId}, " +
                        $"unit={request.UnitId}, error={exception.Message}.");
                }
                finally
                {
                    lock (stateSync)
                    {
                        if (ReferenceEquals(unitFallbackAttempt, attempt))
                            unitFallbackAttempt = null;
                    }
                }

                bool accepted = attempt.ReturnValue > 0 &&
                    TryGetMatchingUnit(
                        request.UnitId, request.GlobalId, out GameUnit* verifiedUnit) &&
                    verifiedUnit->r_TargetTilePositionX == command.TargetX &&
                    verifiedUnit->r_TargetTilePositionY == command.TargetY;
                if (accepted)
                    succeeded++;
                else
                    failedCount++;
                LogWarningNoThrow(
                    $"FORMATION_UNIT_FELL_BACK_TO_VANILLA: " +
                    $"operation={command.Pending.Packet.OperationId}, unit={request.UnitId}, " +
                    $"return={attempt.ReturnValue}, accepted={accepted}.");
            }
            return new UnitFallbackSummary(succeeded, failedCount);
        }

        private sealed class PendingFormationCommand
        {
            internal PendingFormationCommand(FormationOrderPacket packet, string source)
            {
                Packet = packet ?? throw new ArgumentNullException(nameof(packet));
                Source = source ?? string.Empty;
            }

            internal FormationOrderPacket Packet { get; }
            internal string Source { get; }

            internal bool Matches(TribeIssueOrderMoveHereEventArgs args) =>
                args != null && FormationOrderMatchModel.Matches(
                    Packet.TribeId,
                    Packet.TargetX,
                    Packet.TargetY,
                    Packet.IsNewOrder,
                    Packet.MoveType,
                    args.TribeId,
                    args.TileX,
                    args.TileY,
                    args.IsPatrolPath,
                    args.IsNewOrder,
                    (int)args.MoveType);
        }

        private sealed class ActiveFormationCommand
        {
            private readonly Dictionary<int, NativeDestination> destinationsByUnitId;
            private readonly Dictionary<int, uint> globalIdsByUnitId;
            private readonly Dictionary<int, int> terminalUnknownByUnitId =
                new Dictionary<int, int>();
            private readonly HashSet<int> successfulTerminalUnitIds = new HashSet<int>();
            private int standardAssignments;
            private int assassinAssignments;

            internal ActiveFormationCommand(
                PendingFormationCommand pending,
                FormationKind kind,
                int density,
                FormationUnit[] units,
                NativeDestination[] destinations)
            {
                Pending = pending ?? throw new ArgumentNullException(nameof(pending));
                Kind = kind;
                Density = density;
                ExpectedCount = units?.Length ?? 0;
                Destinations = destinations ?? Array.Empty<NativeDestination>();
                destinationsByUnitId = new Dictionary<int, NativeDestination>(ExpectedCount);
                globalIdsByUnitId = new Dictionary<int, uint>(ExpectedCount);
                if (Destinations.Length != ExpectedCount)
                {
                    throw new InvalidOperationException(
                        "The native unit and destination counts do not match.");
                }
                for (int index = 0; index < ExpectedCount; index++)
                {
                    int unitId = units[index].UnitId;
                    uint globalId = units[index].GlobalId;
                    if (unitId <= 0 || globalId == 0 ||
                        destinationsByUnitId.ContainsKey(unitId))
                        throw new InvalidOperationException(
                            $"Invalid or duplicate native unit identity {unitId}/{globalId}.");
                    destinationsByUnitId.Add(unitId, Destinations[index]);
                    globalIdsByUnitId.Add(unitId, globalId);
                }
            }

            internal PendingFormationCommand Pending { get; }
            internal FormationKind Kind { get; }
            internal int TribeId => Pending.Packet.TribeId;
            internal int TargetX => Pending.Packet.TargetX;
            internal int TargetY => Pending.Packet.TargetY;
            internal int IsNewOrder => Pending.Packet.IsNewOrder;
            internal int Density { get; }
            internal int ExpectedCount { get; }
            internal NativeDestination[] Destinations { get; }
            internal int Cursor { get; set; }
            internal bool CommonPathEntered { get; set; }
            internal int TerminalAttemptCount => terminalUnknownByUnitId.Count;
            internal int SuccessfulTerminalCount => successfulTerminalUnitIds.Count;
            internal int AssignedCount =>
                Math.Min(
                    ExpectedCount,
                    standardAssignments + assassinAssignments);
            internal string AssignmentPath
            {
                get
                {
                    int kinds = (standardAssignments > 0 ? 1 : 0) +
                        (assassinAssignments > 0 ? 1 : 0) +
                        (CommonPathEntered ? 1 : 0);
                    if (kinds > 1)
                        return "mixed";
                    if (CommonPathEntered)
                        return "common";
                    if (assassinAssignments > 0)
                        return "assassin";
                    if (standardAssignments > 0)
                        return "standard";
                    return CommonPathEntered ? "common-unassigned" : "none";
                }
            }

            internal bool Matches(TribeIssueOrderMoveHereEventArgs args) =>
                Pending.Matches(args);

            internal bool TryGetUnitDestination(
                int unitId,
                out NativeDestination destination,
                out uint globalId)
            {
                if (destinationsByUnitId.TryGetValue(unitId, out destination) &&
                    globalIdsByUnitId.TryGetValue(unitId, out globalId))
                {
                    return true;
                }
                destination = default;
                globalId = 0;
                return false;
            }

            internal void RecordSelectorAssignment(bool assassin)
            {
                if (assassin)
                    assassinAssignments++;
                else
                    standardAssignments++;
            }

            internal void RecordTerminalResult(int unitId, bool accepted, int unknown)
            {
                terminalUnknownByUnitId[unitId] = unknown;
                if (accepted)
                    successfulTerminalUnitIds.Add(unitId);
            }

            internal UnitFallbackRequest[] GetFallbackRequests()
            {
                var result = new List<UnitFallbackRequest>();
                foreach (KeyValuePair<int, NativeDestination> pair in destinationsByUnitId)
                {
                    if (successfulTerminalUnitIds.Contains(pair.Key) ||
                        !globalIdsByUnitId.TryGetValue(pair.Key, out uint globalId))
                        continue;
                    terminalUnknownByUnitId.TryGetValue(pair.Key, out int unknown);
                    result.Add(new UnitFallbackRequest(pair.Key, globalId, unknown));
                }
                result.Sort((left, right) => left.UnitId.CompareTo(right.UnitId));
                return result.ToArray();
            }
        }

        private sealed class UnitFallbackAttempt
        {
            internal UnitFallbackAttempt(int unitId, int x, int y, int unknown)
            {
                UnitId = unitId;
                X = x;
                Y = y;
                Unknown = unknown;
            }

            internal int UnitId { get; }
            internal int X { get; }
            internal int Y { get; }
            internal int Unknown { get; }
            internal long ReturnValue { get; private set; }

            internal bool Matches(UnitMoveHereEventArgs args) =>
                args != null && args.Phase == EventHookPhase.Post &&
                args.UnitId == UnitId && args.TileX == X && args.TileY == Y &&
                args.Unknown == Unknown;

            internal void Observe(long returnValue)
            {
                ReturnValue = returnValue;
            }
        }

        private readonly struct UnitFallbackRequest
        {
            internal UnitFallbackRequest(int unitId, uint globalId, int unknown)
            {
                UnitId = unitId;
                GlobalId = globalId;
                Unknown = unknown;
            }

            internal int UnitId { get; }
            internal uint GlobalId { get; }
            internal int Unknown { get; }
        }

        private readonly struct UnitFallbackSummary
        {
            internal UnitFallbackSummary(int succeeded, int failed)
            {
                Succeeded = succeeded;
                Failed = failed;
            }

            internal int Succeeded { get; }
            internal int Failed { get; }
        }
    }
}
