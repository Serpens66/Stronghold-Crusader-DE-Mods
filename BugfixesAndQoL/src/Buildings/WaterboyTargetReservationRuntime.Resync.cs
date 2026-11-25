using APIShared.Multiplayer;
using SHCDESE.Interop;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class WaterboyTargetReservationRuntime
    {
        private const string ResyncId = "waterboy-reservations";
        private const int RepairUnitSlots = 9999, RepairBuildingSlots = 3999;
        private const int RepairRowBytes = sizeof(byte) * 2 + sizeof(ushort) * 4 + sizeof(uint) * 4;
        private ReservationLedger preparedLedger;

        private bool RepairActive => nativeMapReady && correctionAvailable && optimizationEnabled;

        private void RegisterRepair()
        {
            resyncReady = MultiplayerResyncEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid,
                ResyncId, BeginRepair, CompleteRepair, out string reason) &&
                MultiplayerResyncData.TryRegister<ReservationLedger>(BugfixesAndQoLPlugin.PluginGuid,
                    ResyncId, 1, 1 + RepairUnitSlots * RepairRowBytes, CaptureRepair, PrepareRepair, CommitRepair, out reason);
            if (!resyncReady)
                Shared.DebugLogHelper.LogError(log, "Waterboy multiplayer data registration failed: " + reason);
        }

        private byte[] CaptureRepair()
        {
            if (!RepairActive) return Array.Empty<byte>();
            var rows = new List<WaterboyRepairRow>();
            ledger.CopyRepairHistoryTo(rows);
            // Suppressions can outlive a deleted owner until that owner next seeks a target.
            // Bound the record by living owners, never by ledger history over the whole map.
            rows.RemoveAll(row => !TryResolveRepairRow(row, out _));
            rows.Sort((left, right) => left.Owner.GlobalId.CompareTo(right.Owner.GlobalId));
            var result = new byte[1 + rows.Count * RepairRowBytes];
            result[0] = 1;
            for (int index = 0; index < rows.Count; index++)
            {
                var entry = rows[index];
                var bytes = result.AsSpan(1 + index * RepairRowBytes, RepairRowBytes);
                bytes[0] = (byte)entry.Player;
                bytes[1] = entry.Suppressed ? (byte)1 : (byte)0;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(2), (ushort)entry.Owner.Slot);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(4), entry.Owner.GlobalId);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(8), (ushort)entry.Target.Slot);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(10), entry.Target.GlobalId);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(14), entry.CompoundKey);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(18), entry.X);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(20), entry.Y);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(22), entry.ProgressTick);
            }
            return result;
        }

        private ReservationLedger PrepareRepair(ReadOnlyMemory<byte> record)
        {
            if (!repairing || record.IsEmpty == RepairActive)
                throw new InvalidDataException("Waterboy reservation availability differs during repair.");
            var result = new ReservationLedger();
            if (record.IsEmpty) return result;
            if (record.Span[0] != 1 || (record.Length - 1) % RepairRowBytes != 0 ||
                !Shared.MissionEvents.TryGetCurrentMapTick(out int tick))
                throw new InvalidDataException("Invalid waterboy repair header or current map clock.");

            uint previousOwner = 0;
            var bytes = record.Span.Slice(1);
            while (!bytes.IsEmpty)
            {
                var owner = new NativeIdentity(BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(2)),
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4)));
                var target = new NativeIdentity(BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(8)),
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(10)));
                var row = new WaterboyRepairRow(bytes[0], owner, target,
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(14)),
                    BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(18)),
                    BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(20)),
                    BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(22)), bytes[1] != 0);
                if (bytes[1] > 1 || !IsValidPlayerId(row.Player) || !owner.IsValid || !target.IsValid ||
                    owner.Slot > RepairUnitSlots || target.Slot > RepairBuildingSlots || owner.GlobalId <= previousOwner ||
                    (row.Suppressed && row.ProgressTick != 0))
                    throw new InvalidDataException("Invalid waterboy owner or target in repair.");
                previousOwner = owner.GlobalId;
                bytes = bytes.Slice(RepairRowBytes);
                if (!TryResolveRepairRow(row, out GameUnit* unit)) continue;

                if (row.Suppressed)
                {
                    result.RestoreSuppression(row);
                    continue;
                }
                ushort x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
                int progressTick = x == row.X && y == row.Y ? row.ProgressTick : tick;
                if (result.Claim(row.Player, owner, target, row.CompoundKey, x, y, progressTick) != ReservationClaimResult.Claimed)
                    throw new InvalidDataException("Conflicting waterboy reservations in host history.");
            }
            for (int player = 1; player <= SHCDESE.API.GamePlayerManagerAPI.MAX_PLAYERS; player++)
                SeedPlayerReservations(result, player, tick, preserveHistory: true);
            return result;
        }

        private static bool TryResolveRepairRow(WaterboyRepairRow row, out GameUnit* unit)
        {
            unit = null;
            if (row.Owner.Slot > RepairUnitSlots || row.Target.Slot > RepairBuildingSlots ||
                !TryResolveLivingFireman(row.Owner, out unit) || unit->r_ControllableForPlayerId != row.Player ||
                !TryResolveBurningBuilding(row.Target, out GameBuilding* target) || ReadCompoundKey(target) != row.CompoundKey)
                return false;

            // A suppression can still prevent an idle/retargeting owner from re-selecting
            // this fire even when the worker no longer has an active native target.
            if (row.Suppressed) return unit->r_CurrentTilePositionX == row.X && unit->r_CurrentTilePositionY == row.Y;
            return (unit->r_AIState == ReservationLedger.WalkingState || unit->r_AIState == ReservationLedger.ExtinguishingState) &&
                ReadTargetIdentity(unit).Equals(row.Target);
        }

        private void CommitRepair(ReservationLedger state) => preparedLedger = state;
    }
}
