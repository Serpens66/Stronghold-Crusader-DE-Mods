using APIShared.Multiplayer;
using APIShared.Simulation;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace StartConditions
{
    public sealed unsafe partial class StartConditionsRuntime
    {
        private const string AIStartTroopResyncRecord = "ai-start-troops";
        private const int RepairHeaderBytes = sizeof(int) + 1;
        private const int RepairRowBytes = 3 * sizeof(int) + 1;
        private const int MaximumRepairBytes =
            RepairHeaderBytes + AIStartTroopIsolationSaveState.MaximumRecords * RepairRowBytes;
        private bool aiStartTroopResyncReady;
        private bool aiStartTroopRealMultiplayer;
        private bool aiStartTroopRepairing;
        private bool aiStartTroopHooksSuspended;

        private void RegisterAIStartTroopResync()
        {
            aiStartTroopResyncReady = EngineRunEvents.TryRegister(StartConditionsPlugin.PluginGuid,
                AIStartTroopResyncRecord, () => { }, SuspendAIStartTroopHooksDuringRepair, out string reason) &&
                MultiplayerResyncEvents.TryRegister(StartConditionsPlugin.PluginGuid, AIStartTroopResyncRecord,
                    BeginAIStartTroopRepair, CompleteAIStartTroopRepair, out reason) &&
                MultiplayerResyncData.TryRegister<ProtectedTroopRepairState>(
                    StartConditionsPlugin.PluginGuid, AIStartTroopResyncRecord, 1, MaximumRepairBytes,
                    CaptureAIStartTroopRepair, PrepareAIStartTroopRepair, CommitAIStartTroopRepair, out reason);
            if (!aiStartTroopResyncReady)
                LogError("AI start troop isolation multiplayer repair is unavailable:", reason);
        }

        private void BeginAIStartTroopRepair() =>
            aiStartTroopRepairing = aiStartTroopResyncReady && aiStartTroopMapActive && aiStartTroopRealMultiplayer;

        // Preserve protection for the rest of the due-command list that started repair.
        private void SuspendAIStartTroopHooksDuringRepair() => aiStartTroopHooksSuspended = aiStartTroopRepairing;

        private void CompleteAIStartTroopRepair() => aiStartTroopRepairing = aiStartTroopHooksSuspended = false;

        private byte[] CaptureAIStartTroopRepair()
        {
            if (!aiStartTroopRepairing)
                return Array.Empty<byte>();
            var data = new byte[RepairHeaderBytes + protectedAIStartTroopsByUnitId.Count * RepairRowBytes];
            BinaryPrimitives.WriteInt32LittleEndian(data, nextAIStartTroopValidationTick);
            data[sizeof(int)] = aiStartTroopValidationRequired ? (byte)1 : (byte)0;
            int offset = RepairHeaderBytes;
            // Validation processes this sequence when attempts contend for a native tribe slot.
            foreach (ProtectedAIStartTroop troop in protectedAIStartTroopsByUnitId.Values)
            {
                Span<byte> row = data.AsSpan(offset, RepairRowBytes);
                BinaryPrimitives.WriteUInt32LittleEndian(row, troop.UnitGlobalId);
                BinaryPrimitives.WriteInt32LittleEndian(row.Slice(sizeof(int)), troop.OwnerPlayerId);
                BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(2 * sizeof(int)), troop.PrivateTribeGlobalId);
                row[3 * sizeof(int)] = troop.PendingDeletion ? (byte)1 : (byte)0;
                offset += RepairRowBytes;
            }
            return data;
        }

        private ProtectedTroopRepairState PrepareAIStartTroopRepair(ReadOnlyMemory<byte> data)
        {
            if (!aiStartTroopRepairing)
            {
                if (!data.IsEmpty)
                    throw new InvalidDataException("AI start troop isolation has no active repair mission.");
                return null;
            }
            if (data.Length < RepairHeaderBytes || data.Length > MaximumRepairBytes ||
                (data.Length - RepairHeaderBytes) % RepairRowBytes != 0 || data.Span[sizeof(int)] > 1)
                throw new InvalidDataException("Invalid AI start troop repair size or validation flag.");

            var prepared = new ProtectedTroopRepairState(BinaryPrimitives.ReadInt32LittleEndian(data.Span),
                data.Span[sizeof(int)] != 0);
            if (data.Length == RepairHeaderBytes)
                return prepared;

            var unitIds = new Dictionary<uint, int>();
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                if (units[spanIndex].r_GlobalId != 0 && IsActiveAIStartTroopUnit(units[spanIndex].r_AliveState))
                {
                    uint globalId = units[spanIndex].r_GlobalId;
                    if (unitIds.ContainsKey(globalId))
                        unitIds[globalId] = 0; // An ambiguous native identity cannot acquire protection.
                    else
                        unitIds.Add(globalId, spanIndex + 1);
                }
            }
            var seenUnits = new HashSet<uint>();
            var seenTribes = new HashSet<uint>();
            for (int offset = RepairHeaderBytes; offset < data.Length; offset += RepairRowBytes)
            {
                ReadOnlySpan<byte> row = data.Span.Slice(offset, RepairRowBytes);
                var identity = new AIStartTroopIsolationSaveRecord(BinaryPrimitives.ReadUInt32LittleEndian(row),
                    BinaryPrimitives.ReadInt32LittleEndian(row.Slice(sizeof(int))),
                    BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(2 * sizeof(int))));
                byte pendingDeletion = row[3 * sizeof(int)];
                if (identity.UnitGlobalId == 0 || identity.UnitGlobalId > int.MaxValue ||
                    !seenUnits.Add(identity.UnitGlobalId) || identity.OwnerPlayerId < 1 ||
                    identity.OwnerPlayerId > GamePlayerManagerAPI.MAX_PLAYERS || pendingDeletion > 1 ||
                    identity.PrivateTribeGlobalId > int.MaxValue ||
                    identity.PrivateTribeGlobalId != 0 && !seenTribes.Add(identity.PrivateTribeGlobalId))
                    throw new InvalidDataException("Invalid or duplicate AI start troop repair identity.");
                if (!unitIds.TryGetValue(identity.UnitGlobalId, out int unitId) || unitId == 0)
                    continue;
                ref GameUnit unit = ref units[unitId - 1];
                if (!AIStartTroopRestorePolicy.ValidateUnit(identity, unit.r_GlobalId, unit.r_AliveState,
                        unit.r_ControllableForPlayerId, GamePlayerManagerAPI.Instance.IsAIPlayer(identity.OwnerPlayerId),
                        unit.r_AITribeRole, unit.r_AITribeRoleRelatedUnknown, out _))
                    continue;

                var troop = new ProtectedAIStartTroop(unitId, identity.UnitGlobalId, identity.OwnerPlayerId)
                {
                    PendingDeletion = pendingDeletion != 0
                };
                int tribeId = unit.r_TribeId;
                if (identity.PrivateTribeGlobalId != 0 && GameTribeManagerAPI.Instance.IsValidId(tribeId) &&
                    GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null &&
                    AIStartTroopRestorePolicy.ValidatePrivateTribe(identity, tribeId, tribe->r_GlobalId,
                        tribe->r_AliveState, tribe->r_PlayerIdOwner, unit.r_TribeId, out _) &&
                    TribeContainsOnlyProtectedUnit(tribe, unitId))
                {
                    troop.PrivateTribeId = tribeId;
                    troop.PrivateTribeGlobalId = identity.PrivateTribeGlobalId;
                    prepared.ByTribe.Add(tribeId, troop);
                }
                // Keep the protected unit or pending deletion, even if its former private tribe
                // is gone. Normal validation resolves a missing association on the recorded tick.
                prepared.ByUnit.Add(unitId, troop);
            }
            return prepared;
        }

        private void CommitAIStartTroopRepair(ProtectedTroopRepairState prepared)
        {
            if (prepared == null)
                return;
            protectedAIStartTroopsByUnitId = prepared.ByUnit;
            protectedAIStartTroopsByTribeId = prepared.ByTribe;
            nextAIStartTroopValidationTick = prepared.NextValidation;
            aiStartTroopValidationRequired = prepared.ValidationRequired;
        }

        private sealed class ProtectedTroopRepairState
        {
            internal readonly Dictionary<int, ProtectedAIStartTroop> ByUnit = new Dictionary<int, ProtectedAIStartTroop>();
            internal readonly Dictionary<int, ProtectedAIStartTroop> ByTribe = new Dictionary<int, ProtectedAIStartTroop>();
            internal readonly int NextValidation;
            internal readonly bool ValidationRequired;

            internal ProtectedTroopRepairState(int nextValidation, bool validationRequired)
            {
                NextValidation = nextValidation;
                ValidationRequired = validationRequired;
            }
        }
    }
}
