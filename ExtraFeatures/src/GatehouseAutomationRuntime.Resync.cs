using APIShared.Multiplayer;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ExtraFeatures
{
    internal sealed unsafe partial class GatehouseAutomationRuntime
    {
        private const string GateResyncRecord = "manual-gates";
        // The audited building pool has 3,999 usable slots. Each row is three Int32s.
        private const int MaximumGateRecords = 3999;
        private const int GateRecordBytes = 3 * sizeof(int);
        private bool gateResyncReady;
        private volatile bool gateRepairing;

        private bool IsManualStateSupported => automationReady &&
            (gateResyncReady || !Shared.GameplayModActivationGate.Snapshot.IsRealMultiplayer);
        private bool ManualControlAvailable => IsManualStateSupported && !gateRepairing;

        private void RegisterGateResync()
        {
            gateResyncReady = MultiplayerResyncEvents.TryRegister(ExtraFeaturesPlugin.PluginGuid,
                GateResyncRecord, BeginGateRepair, CompleteGateRepair, out string reason) &&
                MultiplayerResyncData.TryRegister<GateRepairState>(ExtraFeaturesPlugin.PluginGuid, GateResyncRecord,
                    1, MaximumGateRecords * GateRecordBytes, CaptureGateResync, PrepareGateResync,
                    CommitGateResync, out reason);
            if (!gateResyncReady)
                LogError("Manual gate multiplayer repair is unavailable: " + reason);
        }

        private void BeginGateRepair()
        {
            gateRepairing = gateResyncReady && mapActive && !editorSessionActive &&
                Shared.GameplayModActivationGate.Snapshot.IsRealMultiplayer;
            if (gateRepairing)
                InvalidateGateQueryCache();
        }

        private void CompleteGateRepair() => gateRepairing = false;

        private byte[] CaptureGateResync()
        {
            HashSet<int> manual = Volatile.Read(ref manualGateIdsSnapshot);
            if (!gateRepairing || manual.Count == 0)
                return Array.Empty<byte>();

            var rows = new SortedDictionary<int, (int Owner, eStructs Type)>();
            foreach (ref GameBuilding building in GameBuildingManagerAPI.Instance.GetBuildingsAsSpan())
                if (building.r_AliveState == AliveState.IsAlive && building.r_GlobalId > 0 &&
                    building.r_GlobalId <= int.MaxValue && manual.Contains((int)building.r_GlobalId) &&
                    (IsGatehouseType(building.r_BuildingType) || building.r_BuildingType == eStructs.STRUCT_DRAWBRIDGE))
                    rows.Add((int)building.r_GlobalId, (building.r_PlayerIdOwner, building.r_BuildingType));

            var bytes = new byte[rows.Count * GateRecordBytes];
            int offset = 0;
            foreach (var row in rows)
            {
                Span<byte> target = bytes.AsSpan(offset, GateRecordBytes);
                BinaryPrimitives.WriteInt32LittleEndian(target, row.Key);
                BinaryPrimitives.WriteInt32LittleEndian(target.Slice(sizeof(int)), row.Value.Owner);
                BinaryPrimitives.WriteInt32LittleEndian(target.Slice(2 * sizeof(int)), (int)row.Value.Type);
                offset += GateRecordBytes;
            }
            return bytes;
        }

        private GateRepairState PrepareGateResync(ReadOnlyMemory<byte> data)
        {
            if (!gateRepairing)
            {
                if (!data.IsEmpty)
                    throw new InvalidDataException("Manual gates have no active repair mission.");
                return null;
            }
            if (data.Length % GateRecordBytes != 0 || data.Length > MaximumGateRecords * GateRecordBytes)
                throw new InvalidDataException("Invalid manual gate repair record size.");

            var rows = new Dictionary<int, (int Owner, eStructs Type)>();
            int previous = 0;
            for (int offset = 0; offset < data.Length; offset += GateRecordBytes)
            {
                ReadOnlySpan<byte> row = data.Span.Slice(offset, GateRecordBytes);
                int globalId = BinaryPrimitives.ReadInt32LittleEndian(row);
                int owner = BinaryPrimitives.ReadInt32LittleEndian(row.Slice(sizeof(int)));
                var type = (eStructs)BinaryPrimitives.ReadInt32LittleEndian(row.Slice(2 * sizeof(int)));
                if (globalId <= previous || owner < 0 || owner > GamePlayerManagerAPI.MAX_PLAYERS ||
                    (!IsGatehouseType(type) && type != eStructs.STRUCT_DRAWBRIDGE))
                    throw new InvalidDataException("Invalid manual gate repair identity.");
                rows.Add(globalId, (owner, type));
                previous = globalId;
            }

            var state = new GateRepairState();
            foreach (ref GameBuilding building in GameBuildingManagerAPI.Instance.GetBuildingsAsSpan())
                if (building.r_AliveState == AliveState.IsAlive && building.r_GlobalId > 0 &&
                    building.r_GlobalId <= int.MaxValue && rows.TryGetValue((int)building.r_GlobalId, out var row) &&
                    building.r_PlayerIdOwner == row.Owner && building.r_BuildingType == row.Type)
                    state.Manual.Add((int)building.r_GlobalId);
            state.Published = state.Manual.Count == 0 ? NoManualGateIds : new HashSet<int>(state.Manual);
            return state;
        }

        private void CommitGateResync(GateRepairState state)
        {
            if (state == null)
                return;
            // Chore 57 covers the native timers, including the signed command cooldown.
            // Native manual commands can also set -1, so a repair only publishes the override.
            manualOnlyGateGlobalIds = state.Manual;
            Volatile.Write(ref manualGateIdsSnapshot, state.Published);
        }

        private sealed class GateRepairState
        {
            internal readonly HashSet<int> Manual = new HashSet<int>();
            internal HashSet<int> Published;
        }
    }
}
