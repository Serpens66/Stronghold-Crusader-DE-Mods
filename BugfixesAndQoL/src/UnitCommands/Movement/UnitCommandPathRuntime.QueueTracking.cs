using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {
        internal void ObserveNativeWaypointQueueAtCommand(
            MoveCommandScope command, string phase, int requestedX, int requestedY)
        {
            try
            {
                if (command == null || !TryReadNativeWaypointQueue(
                        command.TribeId, out NativeWaypointQueueSnapshot snapshot))
                {
                    return;
                }

                if (string.Equals(phase, "pre", StringComparison.Ordinal))
                {
                    command.QueuePreSnapshot = snapshot;
                    command.HasQueuePreSnapshot = true;
                }
                else if (string.Equals(phase, "post", StringComparison.Ordinal))
                {
                    command.QueuePostSnapshot = snapshot;
                    command.HasQueuePostSnapshot = true;
                }

                bool alreadyTracked = trackedNativeWaypointQueues.TryGetValue(
                    command.TribeId, out NativeWaypointQueueTracker tracker);
                if (snapshot.Count == 0 && !alreadyTracked)
                    return;
                if (!alreadyTracked)
                {
                    tracker = new NativeWaypointQueueTracker(command.TribeId);
                    trackedNativeWaypointQueues.Add(command.TribeId, tracker);
                }
                tracker.LastCommandSequence = command.Sequence;
                tracker.LastRequestedX = requestedX;
                tracker.LastRequestedY = requestedY;
                LogNativeWaypointQueueChange(tracker, snapshot, $"command-{phase}", -1);
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("native-waypoint-queue-command", ex);
            }
        }

        internal void ObserveNativeWaypointQueues(int tick)
        {
            if (trackedNativeWaypointQueues.Count == 0)
                return;

            try
            {
                var tribeIds = new List<int>(trackedNativeWaypointQueues.Keys);
                foreach (int tribeId in tribeIds)
                {
                    if (!trackedNativeWaypointQueues.TryGetValue(
                            tribeId, out NativeWaypointQueueTracker tracker) ||
                        !TryReadNativeWaypointQueue(tribeId, out NativeWaypointQueueSnapshot snapshot))
                    {
                        trackedNativeWaypointQueues.Remove(tribeId);
                        continue;
                    }

                    bool changed = LogNativeWaypointQueueChange(
                        tracker, snapshot, "simulation-tick", tick);
                    if (snapshot.Count == 0)
                    {
                        tracker.EmptyTicks = changed ? 0 : tracker.EmptyTicks + 1;
                        if (tracker.EmptyTicks >= 2)
                            trackedNativeWaypointQueues.Remove(tribeId);
                    }
                    else
                    {
                        tracker.EmptyTicks = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("native-waypoint-queue-tick", ex);
            }
        }

        internal bool TryReadNativeWaypointQueue(
            int tribeId, out NativeWaypointQueueSnapshot snapshot)
        {
            snapshot = default;
            if (nativeTribeManager == IntPtr.Zero || tribeId < 0 || tribeId >= MaximumTribeCount)
                return false;

            byte* tribe = (byte*)nativeTribeManager.ToPointer() + tribeId * TribeRecordSize;
            int index = *(ushort*)(tribe + TribeMovementWaypointIndexOffset);
            int count = *(ushort*)(tribe + TribeMovementWaypointCountOffset);
            int mode = *(short*)(tribe + TribeMovementModeOffset);
            if (index < 0 || index > MaximumNativeMovementWaypoints ||
                count < 0 || count > MaximumNativeMovementWaypoints)
            {
                return false;
            }

            int currentX = -1;
            int currentY = -1;
            if (count > 0 && index < count && index < MaximumNativeMovementWaypoints)
            {
                currentX = *(ushort*)(tribe + TribeMovementWaypointBaseOffset + index * 4);
                currentY = *(ushort*)(tribe + TribeMovementWaypointBaseOffset + index * 4 + 2);
            }
            int lastX = -1;
            int lastY = -1;
            if (count > 0)
            {
                int last = count - 1;
                lastX = *(ushort*)(tribe + TribeMovementWaypointBaseOffset + last * 4);
                lastY = *(ushort*)(tribe + TribeMovementWaypointBaseOffset + last * 4 + 2);
            }
            snapshot = new NativeWaypointQueueSnapshot(
                index, count, mode, currentX, currentY, lastX, lastY);
            return true;
        }

        internal bool LogNativeWaypointQueueChange(
            NativeWaypointQueueTracker tracker,
            NativeWaypointQueueSnapshot snapshot,
            string source,
            int tick)
        {
            string signature = snapshot.ToString();
            if (string.Equals(tracker.LastSignature, signature, StringComparison.Ordinal))
                return false;
            tracker.LastSignature = signature;
            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-native-waypoint-queue source={source} tick={tick} " +
                $"commandSeq={tracker.LastCommandSequence} tribe={tracker.TribeId} " +
                $"requested=({tracker.LastRequestedX},{tracker.LastRequestedY}) {signature}.");
            return true;
        }

        internal sealed class NativeWaypointQueueTracker
        {
            public NativeWaypointQueueTracker(int tribeId)
            {
                TribeId = tribeId;
            }

            public int TribeId { get; }
            public long LastCommandSequence { get; set; }
            public int LastRequestedX { get; set; }
            public int LastRequestedY { get; set; }
            public int EmptyTicks { get; set; }
            public string LastSignature { get; set; }
        }

        internal readonly struct NativeWaypointQueueSnapshot
        {
            public NativeWaypointQueueSnapshot(
                int index, int count, int mode,
                int currentX, int currentY, int lastX, int lastY)
            {
                Index = index;
                Count = count;
                Mode = mode;
                CurrentX = currentX;
                CurrentY = currentY;
                LastX = lastX;
                LastY = lastY;
            }

            public int Index { get; }
            public int Count { get; }
            public int Mode { get; }
            public int CurrentX { get; }
            public int CurrentY { get; }
            public int LastX { get; }
            public int LastY { get; }

            public override string ToString() =>
                $"index={Index} count={Count} mode={Mode} " +
                $"current=({CurrentX},{CurrentY}) last=({LastX},{LastY})";
        }

    }
}
