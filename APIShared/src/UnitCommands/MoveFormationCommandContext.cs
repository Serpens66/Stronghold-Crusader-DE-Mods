using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Shared;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace APIShared.UnitCommands
{
    internal static class MoveFormationCommandContext
    {
        // R3 input and the native MoveHere event can cross a managed/simulation
        // boundary. The one in-flight command must therefore be process-global,
        // synchronized, and explicitly cleared rather than ThreadStatic.
        private static readonly object syncRoot = new object();
        private static PendingCommand pending;
        private static ActiveCommand active;
        private static int moveChoreExecutionDepth;
        private static TribeIssueOrderMoveHereEventArgs observedPreEvent;

        internal static Action CaptureForNestedCommand()
        {
            lock (syncRoot)
            {
                PendingCommand savedPending = pending;
                ActiveCommand savedActive = active;
                TribeIssueOrderMoveHereEventArgs savedEvent = observedPreEvent;
                int savedDepth = moveChoreExecutionDepth;
                MoveFormationCommandSnapshot savedSnapshot = MoveFormationCommandSnapshotStore.current;
                return () => {
                    lock (syncRoot)
                    {
                        pending = savedPending; active = savedActive; observedPreEvent = savedEvent;
                        moveChoreExecutionDepth = savedDepth;
                        MoveFormationCommandSnapshotStore.current = savedSnapshot;
                    }
                };
            }
        }

        internal static void Arm(int tribeId, int tileX, int tileY, int spacing)
        {
            lock (syncRoot)
            {
                pending = new PendingCommand(
                    tribeId, tileX, tileY, MoveFormationSpacingPolicy.Normalize(spacing));
                active = null;
            }
        }

        internal static bool TryMarkOutgoing(
            int tribeId, int tileX, int tileY, int moveType,
            out int markedMoveType, out bool pendingMatched)
        {
            lock (syncRoot)
            {
                PendingCommand command = pending;
                markedMoveType = moveType;
                pendingMatched = command != null && command.Matches(tribeId, tileX, tileY);
                return pendingMatched && QueueNativeContract.TryEncodeFormationSpacing(
                    moveType, command.Spacing, out markedMoveType);
            }
        }

        internal static void ObserveMoveOrder(TribeIssueOrderMoveHereEventArgs args, bool enabled)
        {
            if (args == null || args.Phase != EventHookPhase.Pre)
                return;

            lock (syncRoot)
            {
                if (ReferenceEquals(observedPreEvent, args))
                    return;
                observedPreEvent = args;
                // A missing Post event (for example when Extended Shift consumes
                // its marked Vanilla command) must not leak spacing into a later Move.
                active = null;
            }

            int encoded = (int)args.MoveType;
            bool hasPrivateBits =
                (encoded & QueueNativeContract.MoveFormationSpacingMask) != 0;
            bool executingMoveChore;
            lock (syncRoot)
                executingMoveChore = moveChoreExecutionDepth > 0;
            bool hasTransportSpacing =
                QueueNativeContract.TryResolveExecutedFormationSpacing(
                    encoded,
                    out int decoded,
                    out int encodedSpacing);
            if (!hasTransportSpacing && !hasPrivateBits)
            {
                lock (syncRoot)
                {
                    if (pending == null ||
                        !pending.Matches(args.TribeId, args.TileX, args.TileY))
                        return;
                }
            }
            if (hasPrivateBits)
                args.MoveType = (TribeMoveType)decoded;

            lock (syncRoot)
            {
                PendingCommand command = pending;
                bool matchesLocalRelease = command != null &&
                    command.Matches(args.TribeId, args.TileX, args.TileY);
                if (enabled && args.IsPatrolPath == 0 &&
                    (hasTransportSpacing || matchesLocalRelease))
                {
                    active = new ActiveCommand(
                        args.TribeId,
                        args.TileX,
                        args.TileY,
                        hasTransportSpacing ? encodedSpacing : command.Spacing,
                        encoded,
                        hasTransportSpacing ? decoded : encoded,
                        executingMoveChore);
                    if (matchesLocalRelease)
                        pending = null;
                }
            }
        }

        internal static void EnterMoveChoreExecution()
        {
            lock (syncRoot)
                moveChoreExecutionDepth++;
        }

        internal static void ExitMoveChoreExecution()
        {
            lock (syncRoot)
            {
                if (moveChoreExecutionDepth > 0)
                    moveChoreExecutionDepth--;
            }
        }

        internal static bool TryGetActive(
            int tribeId, int tileX, int tileY, out int spacing)
        {
            lock (syncRoot)
            {
                ActiveCommand command = active;
                if (command != null && command.Matches(tribeId, tileX, tileY))
                {
                    spacing = command.Spacing;
                    return true;
                }
                spacing = MoveFormationSpacingPolicy.Default;
                return false;
            }
        }

        internal static bool TryGetActiveDecodeDiagnostic(
            int tribeId,
            int tileX,
            int tileY,
            out int rawMoveType,
            out int decodedMoveType,
            out int spacing,
            out bool executingMoveChore)
        {
            lock (syncRoot)
            {
                ActiveCommand command = active;
                if (command != null && command.Matches(tribeId, tileX, tileY))
                {
                    rawMoveType = command.RawMoveType;
                    decodedMoveType = command.DecodedMoveType;
                    spacing = command.Spacing;
                    executingMoveChore = command.ExecutingMoveChore;
                    return true;
                }

                rawMoveType = 0;
                decodedMoveType = 0;
                spacing = MoveFormationSpacingPolicy.Default;
                executingMoveChore = false;
                return false;
            }
        }

        internal static void CompleteMoveOrder()
        {
            lock (syncRoot)
            {
                active = null;
                observedPreEvent = null;
            }
        }

        internal static void Clear()
        {
            lock (syncRoot)
            {
                pending = null;
                active = null;
                moveChoreExecutionDepth = 0;
                observedPreEvent = null;
            }
        }

        private sealed class PendingCommand
        {
            internal PendingCommand(int tribeId, int tileX, int tileY, int spacing)
            {
                TribeId = tribeId;
                TileX = tileX;
                TileY = tileY;
                Spacing = spacing;
            }

            internal int TribeId { get; }
            internal int TileX { get; }
            internal int TileY { get; }
            internal int Spacing { get; }
            internal bool Matches(int tribeId, int tileX, int tileY) =>
                TribeId == tribeId && TileX == tileX && TileY == tileY;
        }

        private sealed class ActiveCommand
        {
            internal ActiveCommand(
                int tribeId,
                int tileX,
                int tileY,
                int spacing,
                int rawMoveType,
                int decodedMoveType,
                bool executingMoveChore)
            {
                TribeId = tribeId;
                TileX = tileX;
                TileY = tileY;
                Spacing = spacing;
                RawMoveType = rawMoveType;
                DecodedMoveType = decodedMoveType;
                ExecutingMoveChore = executingMoveChore;
            }

            internal int TribeId { get; }
            internal int TileX { get; }
            internal int TileY { get; }
            internal int Spacing { get; }
            internal int RawMoveType { get; }
            internal int DecodedMoveType { get; }
            internal bool ExecutingMoveChore { get; }
            internal bool Matches(int tribeId, int tileX, int tileY) =>
                TribeId == tribeId && TileX == tileX && TileY == tileY;
        }
    }

}
