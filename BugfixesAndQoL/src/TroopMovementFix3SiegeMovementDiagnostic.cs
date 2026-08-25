// TEMP_SIEGE_MOVEMENT_DIAGNOSTIC_BEGIN
// Remove this entire file, its csproj entry, and all identically marked call
// sites after the siege movement test has been evaluated.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal static unsafe class TroopMovementFix3SiegeMovementDiagnostic
    {
        private const string Marker = "TEMP_SIEGE_MOVEMENT_DIAGNOSTIC";
        private const int MaximumSnapshotUnits = 64;
        private const int MaximumCadenceLogsPerUnit = 24;

        private const int TribeFreeUnitSpeedsOffset = 0x542;
        private const int TribeMinimumSpeedOffset = 0x54C;
        private const int TribeMovementSpeedOffset = 0x54E;
        private const int TribeMaximumSpeedOffset = 0x550;
        private const int TribeMovementState1Offset = 0x552;
        private const int TribeMovementState2Offset = 0x556;
        private const int TribePatrolModeOffset = 0x558;
        private const int TribeMovementState3Offset = 0x55A;
        private const int TribeAverageSpeedOffset = 0x55C;
        private const int TribeMovementState4Offset = 0x55E;

        private static readonly List<int> UnitIds = new List<int>(128);
        private static readonly Dictionary<ulong, CadenceObservation>
            CadenceObservations =
                new Dictionary<ulong, CadenceObservation>();

        public static bool LogOrderSnapshot(
            ManualLogSource log,
            int tribeId,
            string phase,
            TribeMoveType moveType,
            long? returnValue)
        {
            if (tribeId <= 0 ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(
                    tribeId,
                    out GameTribe* tribe) ||
                tribe == null)
            {
                return false;
            }

            UnitIds.Clear();
            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, UnitIds))
                return false;

            bool containsTrackedSiege = false;
            int activeUnits = 0;
            foreach (int unitId in UnitIds)
            {
                if (TryGetAliveUnit(unitId, out GameUnit* unit))
                {
                    activeUnits++;
                    if (IsTrackedSiegeType(unit->r_UnitChimp))
                        containsTrackedSiege = true;
                }
            }

            if (!containsTrackedSiege)
                return false;

            byte* tribeBytes = (byte*)tribe;
            TroopMovementFix3ModLog.Info(
                log,
                $"[{Marker}] {phase} tribeId={tribeId}, " +
                $"moveType={moveType}, returnValue=" +
                $"{(returnValue.HasValue ? returnValue.Value.ToString() : "n/a")}, " +
                $"listedUnits={UnitIds.Count}, activeUnits={activeUnits}, " +
                $"tribeFreeUnitSpeeds={ReadUInt16(tribeBytes, TribeFreeUnitSpeedsOffset)}, " +
                $"tribeMinimumSpeed={ReadUInt16(tribeBytes, TribeMinimumSpeedOffset)}, " +
                $"tribeMovementSpeed={ReadUInt16(tribeBytes, TribeMovementSpeedOffset)}, " +
                $"tribeMaximumSpeed={ReadUInt16(tribeBytes, TribeMaximumSpeedOffset)}, " +
                $"tribeAverageSpeed={ReadUInt16(tribeBytes, TribeAverageSpeedOffset)}, " +
                $"tribeState1={ReadUInt32(tribeBytes, TribeMovementState1Offset)}, " +
                $"tribeState2={ReadUInt16(tribeBytes, TribeMovementState2Offset)}, " +
                $"tribePatrolMode={ReadUInt16(tribeBytes, TribePatrolModeOffset)}, " +
                $"tribeState3={ReadUInt16(tribeBytes, TribeMovementState3Offset)}, " +
                $"tribeState4={ReadUInt16(tribeBytes, TribeMovementState4Offset)}.");

            int loggedUnits = 0;
            foreach (int unitId in UnitIds)
            {
                if (loggedUnits >= MaximumSnapshotUnits)
                    break;
                if (!TryGetAliveUnit(unitId, out GameUnit* unit))
                    continue;

                loggedUnits++;
                eChimps unitType = unit->r_UnitChimp;
                TroopMovementFix3ModLog.Info(
                    log,
                    $"[{Marker}] {phase}_UNIT tribeId={tribeId}, " +
                    $"unitId={unitId}, globalId={unit->r_GlobalId}, " +
                    $"unitType={unitType}, trackedSiege={IsTrackedSiegeType(unitType)}, " +
                    $"defaultSpeed={GameUnitManagerAPI.Instance.GetDefaultSpeed(unitType)}, " +
                    $"currentSpeed={unit->r_CurrentSpeed}, " +
                    $"effectiveSpeed={unit->r_CurrentSpeed2}, " +
                    $"speedBonus={unchecked((short)unit->r_SpeedBonus)}, " +
                    $"animation=0x{unit->N000000F4:X}, aiState={unit->r_AIState}, " +
                    $"lastTribeCommand={unit->r_AI_LastIssuedTribeCommand}, " +
                    $"cadenceState=0x{unit->r_AliveTicks1:X8}, " +
                    $"tile={unit->r_CurrentPositionTileId}.");
            }

            if (activeUnits > loggedUnits)
            {
                TroopMovementFix3ModLog.Info(
                    log,
                    $"[{Marker}] {phase}_UNIT_LIMIT tribeId={tribeId}, " +
                    $"loggedUnits={loggedUnits}, omittedActiveUnits=" +
                    $"{activeUnits - loggedUnits}.");
            }

            return true;
        }

        public static void LogCadenceSample(
            ManualLogSource log,
            GameUnit* unit,
            SynchronizedMovementCadence cadence,
            ushort requestedRunningSpeedBonus,
            ushort speedBonusBefore,
            uint animationBefore)
        {
            if (unit == null || !IsTrackedSiegeType(unit->r_UnitChimp))
                return;

            ulong identity = unit->r_GlobalId != 0
                ? unit->r_GlobalId
                : unchecked((ulong)unit);
            var current = new CadenceObservation(
                unit->r_TribeId,
                unit->r_CurrentSpeed,
                unit->r_CurrentSpeed2,
                speedBonusBefore,
                unit->r_SpeedBonus,
                animationBefore,
                unit->N000000F4,
                unit->r_AIState,
                unit->r_AI_LastIssuedTribeCommand,
                unit->r_AliveTicks1);

            if (CadenceObservations.TryGetValue(
                    identity,
                    out CadenceObservation previous) &&
                previous.LogCount >= MaximumCadenceLogsPerUnit)
            {
                return;
            }

            if (CadenceObservations.TryGetValue(identity, out previous) &&
                previous.HasSameValues(current))
            {
                return;
            }

            int logCount = previous.LogCount + 1;
            current.LogCount = logCount;
            CadenceObservations[identity] = current;
            TroopMovementFix3ModLog.Info(
                log,
                $"[{Marker}] CADENCE_SAMPLE sample={logCount}/" +
                $"{MaximumCadenceLogsPerUnit}, globalId={unit->r_GlobalId}, " +
                $"unitType={unit->r_UnitChimp}, tribeId={unit->r_TribeId}, " +
                $"cadence={cadence}, requestedRunningSpeedBonus=" +
                $"{unchecked((short)requestedRunningSpeedBonus)}, " +
                $"currentSpeed={unit->r_CurrentSpeed}, " +
                $"effectiveSpeed={unit->r_CurrentSpeed2}, " +
                $"speedBonusBefore={unchecked((short)speedBonusBefore)}, " +
                $"speedBonusAfter={unchecked((short)unit->r_SpeedBonus)}, " +
                $"animationBefore=0x{animationBefore:X}, " +
                $"animationAfter=0x{unit->N000000F4:X}, " +
                $"aiState={unit->r_AIState}, " +
                $"lastTribeCommand={unit->r_AI_LastIssuedTribeCommand}, " +
                $"cadenceState=0x{unit->r_AliveTicks1:X8}, " +
                $"tile={unit->r_CurrentPositionTileId}.");
        }

        public static void Reset()
        {
            UnitIds.Clear();
            CadenceObservations.Clear();
        }

        private static bool IsTrackedSiegeType(eChimps unitType)
        {
            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_CATAPULT:
                case eChimps.CHIMP_TYPE_TREBUCHET:
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:
                case eChimps.CHIMP_TYPE_BATTERING_RAM:
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD:
                case eChimps.CHIMP_TYPE_BALLISTA:
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetAliveUnit(int unitId, out GameUnit* unit)
        {
            unit = null;
            return unitId > 0 &&
                   GameUnitManagerAPI.Instance.TryGetUnitById(
                       unitId,
                       out unit) &&
                   unit != null &&
                   unit->r_AliveState == AliveState.IsAlive;
        }

        private static ushort ReadUInt16(byte* data, int offset)
        {
            return *(ushort*)(data + offset);
        }

        private static uint ReadUInt32(byte* data, int offset)
        {
            return *(uint*)(data + offset);
        }

        private struct CadenceObservation
        {
            public CadenceObservation(
                int tribeId,
                ushort currentSpeed,
                ushort effectiveSpeed,
                ushort speedBonusBefore,
                ushort speedBonusAfter,
                uint animationBefore,
                uint animationAfter,
                ushort aiState,
                ushort lastTribeCommand,
                uint cadenceState)
            {
                TribeId = tribeId;
                CurrentSpeed = currentSpeed;
                EffectiveSpeed = effectiveSpeed;
                SpeedBonusBefore = speedBonusBefore;
                SpeedBonusAfter = speedBonusAfter;
                AnimationBefore = animationBefore;
                AnimationAfter = animationAfter;
                AiState = aiState;
                LastTribeCommand = lastTribeCommand;
                CadenceState = cadenceState;
                LogCount = 0;
            }

            public int TribeId;
            public ushort CurrentSpeed;
            public ushort EffectiveSpeed;
            public ushort SpeedBonusBefore;
            public ushort SpeedBonusAfter;
            public uint AnimationBefore;
            public uint AnimationAfter;
            public ushort AiState;
            public ushort LastTribeCommand;
            public uint CadenceState;
            public int LogCount;

            public bool HasSameValues(CadenceObservation other)
            {
                return TribeId == other.TribeId &&
                       CurrentSpeed == other.CurrentSpeed &&
                       EffectiveSpeed == other.EffectiveSpeed &&
                       SpeedBonusBefore == other.SpeedBonusBefore &&
                       SpeedBonusAfter == other.SpeedBonusAfter &&
                       AnimationBefore == other.AnimationBefore &&
                       AnimationAfter == other.AnimationAfter &&
                       AiState == other.AiState &&
                       LastTribeCommand == other.LastTribeCommand;
            }
        }
    }
}
// TEMP_SIEGE_MOVEMENT_DIAGNOSTIC_END
