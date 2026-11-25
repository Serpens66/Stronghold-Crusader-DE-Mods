using APIShared.Multiplayer;
using APIShared.Simulation;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;

namespace AIDefenseTest
{
    internal sealed unsafe partial class AIDefenseTestRuntime
    {
        // The audited building manager has 3,999 usable records. At most one defender is
        // registered per tower, and capture only includes towers still occupying a record.
        private const int MaximumRepairDefenders = 3999;
        private const int RepairHeaderBytes = sizeof(int) + sizeof(ushort);
        private const int RepairEntryBytes = 2 * sizeof(ushort) + 3 * sizeof(uint) + sizeof(byte);
        private const int MaximumRepairBytes = RepairHeaderBytes + MaximumRepairDefenders * RepairEntryBytes;
        private bool repairRegistered;
        private bool repairPending;
        private bool repairing;
        private int repairRunDepth;

        private void RegisterRepair()
        {
            repairRegistered = EngineRunEvents.TryRegister(
                AIDefenseTestPlugin.PluginGuid, "protected-defenders-run-boundary",
                () => repairRunDepth++, OnRepairRunEnded, out string reason) &&
                MultiplayerResyncEvents.TryRegister(
                AIDefenseTestPlugin.PluginGuid, "protected-defenders-suspend",
                () => repairPending = true, ResumeAfterRepair, out reason) &&
                MultiplayerResyncData.TryRegister(AIDefenseTestPlugin.PluginGuid, "protected-defenders",
                    1, MaximumRepairBytes, CaptureRepair, PrepareRepair, CommitRepair, out reason);
            if (!repairRegistered)
                Shared.DebugLogHelper.LogError(log, "AI Defense Test multiplayer repair unavailable: " + reason);
        }

        private void OnRepairRunEnded()
        {
            // The executed Chore54 can precede normal commands in the same native due list.
            // Keep their protection active through the entire admitted run.
            if (--repairRunDepth == 0 && repairPending)
                repairing = true;
        }

        private void ResumeAfterRepair() => repairPending = repairing = false;

        private byte[] CaptureRepair()
        {
            if (!mapActive)
                return Array.Empty<byte>();

            Dictionary<uint, int> towerSlots = ReadLiveTowerSlots();
            var unitIds = new List<int>(protectedByUnitId.Keys);
            unitIds.Sort();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(nextScanTick);
                ushort count = 0;
                writer.Write(count);
                foreach (int unitId in unitIds)
                {
                    ProtectedDefender defender = protectedByUnitId[unitId];
                    if (!towerSlots.ContainsKey(defender.TowerGlobalId))
                        continue;
                    writer.Write((ushort)unitId);
                    writer.Write(defender.UnitGlobalId);
                    writer.Write(defender.TowerGlobalId);
                    writer.Write((byte)defender.OwnerPlayerId);
                    writer.Write((ushort)defender.PrivateTribeId);
                    writer.Write(defender.PrivateTribeGlobalId);
                    count++;
                }
                stream.Position = sizeof(int);
                writer.Write(count);
                return stream.ToArray();
            }
        }

        private RepairState PrepareRepair(ReadOnlyMemory<byte> bytes)
        {
            var state = new RepairState();
            if (bytes.IsEmpty)
            {
                if (mapActive)
                    throw new InvalidDataException("Only one peer has active AI defense state.");
                return state;
            }
            if (!mapActive || bytes.Length < RepairHeaderBytes || bytes.Length > MaximumRepairBytes)
                throw new InvalidDataException("AI defense state is unavailable or the record size is invalid.");

            Dictionary<uint, int> towerSlots = ReadLiveTowerSlots();
            var tiles = GameTileManagerAPI.Instance;
            using (var stream = new MemoryStream(bytes.ToArray(), writable: false))
            using (var reader = new BinaryReader(stream))
            {
                state.NextScanTick = reader.ReadInt32();
                int count = reader.ReadUInt16();
                if (bytes.Length != RepairHeaderBytes + count * RepairEntryBytes)
                    throw new InvalidDataException("Invalid AI defense repair entry count.");

                int previousUnitId = 0;
                for (int index = 0; index < count; index++)
                {
                    int unitId = reader.ReadUInt16();
                    uint unitGlobalId = reader.ReadUInt32();
                    uint towerGlobalId = reader.ReadUInt32();
                    int owner = reader.ReadByte();
                    int tribeId = reader.ReadUInt16();
                    uint tribeGlobalId = reader.ReadUInt32();
                    if (unitId <= previousUnitId || unitGlobalId == 0 || towerGlobalId == 0 ||
                        owner == 0 || owner > GamePlayerManagerAPI.MAX_PLAYERS ||
                        tribeId >= GameTribeManagerAPI.TRIBE_SLOT_COUNT || (tribeId == 0) != (tribeGlobalId == 0))
                        throw new InvalidDataException("Invalid AI defense repair identity.");
                    previousUnitId = unitId;

                    if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) || unit == null ||
                        unit->r_GlobalId != unitGlobalId || !IsUnitActive(unit->r_AliveState) ||
                        unit->r_UnitChimp != DefenderType || unit->r_ControllableForPlayerId != owner ||
                        !towerSlots.TryGetValue(towerGlobalId, out int towerId) ||
                        !APIShared.BuildingAccess.TryGetById(towerId, out GameBuilding* tower, out _) ||
                        tower == null || tower->r_PlayerIdOwner != owner)
                        continue;

                    List<uint> towerTiles = GetTowerTileIds(tower, tiles);
                    if (towerTiles.Count == 0)
                        continue;
                    var defender = new ProtectedDefender(unitId, unitGlobalId, towerGlobalId, owner, towerTiles);
                    if (tribeId > 0 && unit->r_TribeId == tribeId &&
                        GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null &&
                        tribe->r_GlobalId == tribeGlobalId && tribe->r_PlayerIdOwner == owner &&
                        IsTribeActive(tribe->r_AliveState))
                    {
                        defender.PrivateTribeId = tribeId;
                        defender.PrivateTribeGlobalId = tribeGlobalId;
                        state.ByTribe.Add(tribeId, defender);
                    }
                    state.ByUnit.Add(unitId, defender);
                    state.ByTower.Add(towerGlobalId, defender);
                }
            }
            return state;
        }

        private static Dictionary<uint, int> ReadLiveTowerSlots()
        {
            var slots = new Dictionary<uint, int>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            if (buildings.Length > MaximumRepairDefenders)
                throw new InvalidDataException("AI defense building capacity exceeds the audited repair bound.");
            var players = GamePlayerManagerAPI.Instance;
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                int owner = building.r_PlayerIdOwner;
                if (building.r_AliveState == AliveState.IsAlive && building.r_GlobalId != 0 &&
                    TowerTypes.Contains(building.r_BuildingType) &&
                    players.IsPlayerIdValid(owner) && players.IsAIPlayer(owner))
                    slots.Add(building.r_GlobalId, spanIndex + 1);
            }
            return slots;
        }

        private void CommitRepair(RepairState state)
        {
            protectedByUnitId = state.ByUnit;
            protectedByTowerGlobalId = state.ByTower;
            protectedByPrivateTribeId = state.ByTribe;
            permittedAssignmentUnitId = permittedAssignmentTribeId = 0;
            nextScanTick = state.NextScanTick;
        }

        private sealed class RepairState
        {
            internal readonly Dictionary<int, ProtectedDefender> ByUnit = new Dictionary<int, ProtectedDefender>();
            internal readonly Dictionary<uint, ProtectedDefender> ByTower = new Dictionary<uint, ProtectedDefender>();
            internal readonly Dictionary<int, ProtectedDefender> ByTribe = new Dictionary<int, ProtectedDefender>();
            internal int NextScanTick;
        }
    }
}
