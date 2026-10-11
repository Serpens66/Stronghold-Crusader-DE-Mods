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
        internal bool TryChooseStandardFormationSlot(IntPtr manager, int spacing, int x, int y)
        {
            ActiveFormationCommand command;
            lock (stateSync) command = activeCommand;
            if (!Enabled || !Matches(command, manager, x, y) ||
                !TryTakeDestination(command, out NativeDestination destination)) return false;
            WriteFormationOutput(destination);
            RecordSelectorAssignment(command, false);
            return true;
        }

        internal bool TryChooseAssassinFormationSlot(IntPtr manager, int spacing, int x, int y, out int tileId)
        {
            tileId = 0;
            ActiveFormationCommand command;
            lock (stateSync) command = activeCommand;
            if (!Enabled || !Matches(command, manager, x, y) ||
                !TryTakeDestination(command, out NativeDestination destination)) return false;
            WriteFormationOutput(destination);
            RecordSelectorAssignment(command, true);
            tileId = destination.TileId;
            return true;
        }

        internal long CommonGroupMoveHook(
            IntPtr manager,
            int tribeId,
            short x,
            short y,
            short patrol,
            int newOrder, Func<long> original)
        {
            ActiveFormationCommand previous;
            UnitAssignmentFrame previousUnitFrame;
            ActiveFormationCommand command = null;
            lock (stateSync)
            {
                previous = commonGroupCommand;
                previousUnitFrame = unitAssignmentFrame;
                ActiveFormationCommand candidate = activeCommand;
                commonGroupCommand = null;
                unitAssignmentFrame = null;
                if (candidate != null &&
                    manager == nativeTribeManager && candidate.TribeId == tribeId &&
                    candidate.TargetX == x && candidate.TargetY == y &&
                    patrol == 0 && candidate.IsNewOrder == newOrder)
                {
                    command = candidate;
                    command.CommonPathEntered = true;
                    commonGroupCommand = command;
                }
            }
            try
            {
                return original();
            }
            finally
            {
                lock (stateSync)
                {
                    ClearUnitAssignmentFrames(command);
                    if (ReferenceEquals(commonGroupCommand, command))
                        commonGroupCommand = previous;
                    unitAssignmentFrame = previousUnitFrame;
                }
            }
        }

        internal void OnUnitMoveHere(UnitMoveHereEventArgs args)
        {
            if (!Enabled || args == null)
                return;
            if (args.Phase == EventHookPhase.Pre)
            {
                if (args.SkipOriginalFunction)
                    return;
                try
                {
                    lock (stateSync)
                    {
                        PruneUnitAssignmentFrames();
                        ActiveFormationCommand command = activeCommand;
                        if (command == null ||
                             !command.TryGetUnitDestination(
                                 args.UnitId,
                                 out NativeDestination destination,
                                 out uint globalId) ||
                             !TryGetMatchingUnit(args.UnitId, globalId, out GameUnit* unit))
                        {
                            return;
                        }
                        UnitAssignmentFrame parent = unitAssignmentFrame;
                        int originalX = args.TileX;
                        int originalY = args.TileY;
                        args.TileX = destination.X;
                        args.TileY = destination.Y;
                        unit->r_AttackMoveToTargetTileX = (ushort)destination.X;
                        unit->r_AttackMoveToTargetTileY = (ushort)destination.Y;
                        command.RecordTerminalAttempt(args.UnitId, args.Unknown);
                        unitAssignmentFrame = new UnitAssignmentFrame(
                            args,
                            parent,
                            command,
                            args.UnitId,
                            globalId,
                            destination,
                            originalX,
                            originalY,
                            args.Unknown);
                    }
                }
                catch (Exception exception)
                {
                    DisableAfterNativeFailure("unit-target-pre", exception);
                }
                return;
            }

            if (args.Phase != EventHookPhase.Post)
                return;
            try
            {
                lock (stateSync)
                {
                    UnitFallbackAttempt fallback = unitFallbackAttempt;
                    if (fallback != null && fallback.Matches(args))
                    {
                        fallback.Observe(args.ReturnValue);
                        return;
                    }
                    PruneUnitAssignmentFrames();
                    UnitAssignmentFrame frame = unitAssignmentFrame;
                    if (frame == null)
                        return;
                    try
                    {
                        bool originalObserved = false;
                        if (args.UnitId == frame.UnitId &&
                            args.TileX == frame.OriginalX &&
                            args.TileY == frame.OriginalY &&
                            args.Unknown == frame.OriginalUnknown &&
                            !frame.PreArgs.SkipOriginalFunction &&
                            frame.PreArgs.UnitId == frame.UnitId &&
                            frame.PreArgs.TileX == frame.Destination.X &&
                            frame.PreArgs.TileY == frame.Destination.Y &&
                            frame.PreArgs.Unknown == frame.OriginalUnknown &&
                            ReferenceEquals(activeCommand, frame.Command) &&
                            TryGetMatchingUnit(
                                frame.UnitId, frame.GlobalId, out GameUnit* unit) &&
                            unit->r_TribeId == frame.Command.TribeId)
                        {
                            originalObserved = true;
                        }
                        bool accepted = originalObserved && args.ReturnValue > 0 &&
                            TryGetMatchingUnit(frame.UnitId, frame.GlobalId, out GameUnit* verifiedUnit) &&
                            verifiedUnit->r_TargetTilePositionX == frame.Destination.X &&
                            verifiedUnit->r_TargetTilePositionY == frame.Destination.Y &&
                            verifiedUnit->r_AttackMoveToTargetTileX == frame.Destination.X &&
                            verifiedUnit->r_AttackMoveToTargetTileY == frame.Destination.Y;
                        FinishUnitAssignmentFrame(frame, accepted, originalObserved, args.ReturnValue);
                    }
                    finally
                    {
                        unitAssignmentFrame = frame.Parent;
                    }
                }
            }
            catch (Exception exception)
            {
                DisableAfterNativeFailure("unit-target-post", exception);
            }
        }

        private void PruneUnitAssignmentFrames()
        {
            while (unitAssignmentFrame != null &&
                (unitAssignmentFrame.PreArgs.SkipOriginalFunction ||
                 !ReferenceEquals(unitAssignmentFrame.Command, activeCommand)))
            {
                UnitAssignmentFrame frame = unitAssignmentFrame;
                unitAssignmentFrame = frame.Parent;
                FinishUnitAssignmentFrame(frame, false);
            }
        }

        private void ClearUnitAssignmentFrames(ActiveFormationCommand command)
        {
            while (unitAssignmentFrame != null &&
                (command == null || ReferenceEquals(unitAssignmentFrame.Command, command)))
            {
                UnitAssignmentFrame frame = unitAssignmentFrame;
                unitAssignmentFrame = frame.Parent;
                FinishUnitAssignmentFrame(frame, false);
            }
        }

        private static bool TryGetMatchingUnit(
            int unitId,
            uint globalId,
            out GameUnit* unit)
        {
            unit = null;
            return globalId != 0 &&
                APIShared.UnitAccess.TryGetById(unitId, out unit, out _) &&
                unit != null && APIShared.UnitAccess.IsReallyAlive(unit) &&
                unit->r_GlobalId == globalId;
        }

        private static void FinishUnitAssignmentFrame(
            UnitAssignmentFrame frame,
            bool accepted,
            bool originalObserved = false,
            long returnValue = 0)
        {
            if (frame == null)
                return;
            frame.Command.RecordTerminalResult(
                frame.UnitId,
                accepted,
                frame.OriginalUnknown,
                originalObserved,
                returnValue);
            if (accepted)
                return;
            if (frame.PreArgs.UnitId == frame.UnitId &&
                frame.PreArgs.TileX == frame.Destination.X &&
                frame.PreArgs.TileY == frame.Destination.Y)
            {
                frame.PreArgs.TileX = frame.OriginalX;
                frame.PreArgs.TileY = frame.OriginalY;
            }
            if (TryGetMatchingUnit(frame.UnitId, frame.GlobalId, out GameUnit* unit) &&
                unit->r_AttackMoveToTargetTileX == frame.Destination.X &&
                unit->r_AttackMoveToTargetTileY == frame.Destination.Y)
            {
                unit->r_AttackMoveToTargetTileX = (ushort)frame.OriginalX;
                unit->r_AttackMoveToTargetTileY = (ushort)frame.OriginalY;
            }
        }

        private void RecordSelectorAssignment(
            ActiveFormationCommand command,
            bool assassin)
        {
            lock (stateSync)
            {
                if (ReferenceEquals(activeCommand, command))
                    command.RecordSelectorAssignment(assassin);
            }
        }

        private void WriteFormationOutput(NativeDestination destination)
        {
            int* output = (int*)((byte*)nativeTribeManager.ToPointer() + 0x0C);
            output[0] = destination.X;
            output[1] = destination.Y;
            output[2] = 0;
        }

        private sealed class UnitAssignmentFrame
        {
            internal UnitAssignmentFrame(
                UnitMoveHereEventArgs preArgs,
                UnitAssignmentFrame parent,
                ActiveFormationCommand command,
                int unitId,
                uint globalId,
                NativeDestination destination,
                int originalX,
                int originalY,
                int originalUnknown)
            {
                PreArgs = preArgs;
                Parent = parent;
                Command = command;
                UnitId = unitId;
                GlobalId = globalId;
                Destination = destination;
                OriginalX = originalX;
                OriginalY = originalY;
                OriginalUnknown = originalUnknown;
            }

            internal UnitMoveHereEventArgs PreArgs { get; }
            internal UnitAssignmentFrame Parent { get; }
            internal ActiveFormationCommand Command { get; }
            internal int UnitId { get; }
            internal uint GlobalId { get; }
            internal NativeDestination Destination { get; }
            internal int OriginalX { get; }
            internal int OriginalY { get; }
            internal int OriginalUnknown { get; }
        }
    }
}
