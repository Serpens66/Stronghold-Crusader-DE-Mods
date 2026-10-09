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
        internal void ResolveAttackApproachRepresentative(
            AttackCommandScope command,
            out int unitId,
            out int playerId,
            out eChimps unitType)
        {
            unitId = -1;
            playerId = -1;
            unitType = default;
            foreach (int candidateId in command.CandidateUnitIds)
            {
                if (!APIShared.UnitAccess.TryGetById(candidateId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_TribeId != command.TribeId || !CanDigMoat(unit))
                {
                    continue;
                }

                unitId = candidateId;
                playerId = unit->r_ControllableForPlayerId;
                unitType = unit->r_UnitChimp;
                return;
            }
        }

        internal bool TryCaptureOrderedActiveGroupUnits(
            IntPtr tribeManager, int tribeId, out int[] unitIds)
        {
            unitIds = Array.Empty<int>();
            if (tribeManager == IntPtr.Zero || tribeManager != nativeTribeManager ||
                tribeId < 0 || tribeId >= MaximumTribeCount || getGroupUnitId == null)
            {
                return false;
            }

            byte* tribeRecord = (byte*)tribeManager.ToPointer() + tribeId * TribeRecordSize;
            int unitCount = *(short*)(tribeRecord + TribeUnitCountOffset);
            if (unitCount <= 0 || unitCount > MaximumUnitCount)
                return false;

            List<int> active = new List<int>(unitCount);
            for (int ordinal = 0; ordinal < unitCount; ordinal++)
            {
                int unitId = getGroupUnitId(tribeManager, tribeId, ordinal);
                if (unitId <= 0 ||
                    !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_TribeId != tribeId ||
                    *(ushort*)((byte*)unit + UnitGroupInactiveStateOffset) != 0)
                {
                    continue;
                }
                active.Add(unitId);
            }

            unitIds = active.ToArray();
            return unitIds.Length > 0;
        }

        internal static BuildingApproachCandidate[] CaptureBuildingApproachCandidates(
            IntPtr pathManager)
        {
            if (pathManager == IntPtr.Zero)
                return Array.Empty<BuildingApproachCandidate>();

            byte* manager = (byte*)pathManager.ToPointer();
            List<BuildingApproachCandidate> candidates = new List<BuildingApproachCandidate>();
            for (int index = 0; index < VanillaAttackFloodResultCapacity; index++)
            {
                byte* entry = manager + PathManagerFloodResultTileOffset +
                    index * PathManagerFloodResultStride;
                BuildingApproachCandidate candidate = new BuildingApproachCandidate(
                    *(int*)(entry + BuildingCandidateApproachTileOffset),
                    *(int*)(entry + BuildingCandidateFootprintTileOffset),
                    *(int*)(entry + BuildingCandidateScoreOffset));
                if (candidate.ApproachTileId == 0)
                    break;
                candidates.Add(candidate);
            }
            return candidates.ToArray();
        }

        internal static BuildingApproachCandidate[] CaptureBuildingApproachBuffer(
            IntPtr pathManager)
        {
            BuildingApproachCandidate[] buffer =
                new BuildingApproachCandidate[VanillaAttackFloodResultCapacity];
            byte* manager = (byte*)pathManager.ToPointer();
            for (int index = 0; index < buffer.Length; index++)
            {
                byte* entry = manager + PathManagerFloodResultTileOffset +
                    index * PathManagerFloodResultStride;
                buffer[index] = new BuildingApproachCandidate(
                    *(int*)(entry + BuildingCandidateApproachTileOffset),
                    *(int*)(entry + BuildingCandidateFootprintTileOffset),
                    *(int*)(entry + BuildingCandidateScoreOffset));
            }
            return buffer;
        }

        internal static void RestoreBuildingApproachBuffer(
            IntPtr pathManager, BuildingApproachCandidate[] buffer)
        {
            byte* manager = (byte*)pathManager.ToPointer();
            int count = Math.Min(buffer.Length, VanillaAttackFloodResultCapacity);
            for (int index = 0; index < count; index++)
                WriteBuildingApproachCandidate(manager, index, buffer[index]);
        }

        internal static void WriteBuildingApproachCandidates(
            IntPtr pathManager, List<BuildingApproachCandidate> candidates)
        {
            byte* manager = (byte*)pathManager.ToPointer();
            int count = Math.Min(candidates.Count, VanillaAttackFloodResultCapacity - 1);
            for (int index = 0; index < count; index++)
                WriteBuildingApproachCandidate(manager, index, candidates[index]);
            WriteBuildingApproachCandidate(manager, count, default);
        }

        internal static void WriteBuildingApproachCandidate(
            byte* manager, int index, BuildingApproachCandidate candidate)
        {
            byte* entry = manager + PathManagerFloodResultTileOffset +
                index * PathManagerFloodResultStride;
            *(int*)(entry + BuildingCandidateApproachTileOffset) = candidate.ApproachTileId;
            *(int*)(entry + BuildingCandidateFootprintTileOffset) = candidate.FootprintTileId;
            *(int*)(entry + BuildingCandidateScoreOffset) = candidate.Score;
        }

        internal void PublishBuildingApproachPairs(
            AttackCommandScope command, IntPtr pathManager)
        {
            if (command == null || !IsBuildingAttackCommand(command.Command))
                return;

            command.PublishedBuildingApproaches.Clear();
            foreach (BuildingApproachCandidate candidate in
                CaptureBuildingApproachCandidates(pathManager))
            {
                if (candidate.ApproachTileId <= 0) break;
                if (!command.PublishedBuildingApproaches.TryGetValue(
                        candidate.ApproachTileId, out HashSet<int> footprintTiles))
                {
                    footprintTiles = new HashSet<int>();
                    command.PublishedBuildingApproaches[candidate.ApproachTileId] = footprintTiles;
                }
                footprintTiles.Add(candidate.FootprintTileId);
            }
        }

        internal void PublishUnitAttackApproachTiles(
            AttackCommandScope command,
            IntPtr pathManager)
        {
            if (command == null || command.Command != TribeAICommand.AttackUnit ||
                command.MapEpoch != mapEpoch || pathManager == IntPtr.Zero)
            {
                return;
            }

            command.PublishedUnitAttackApproaches.Clear();
            byte* manager = (byte*)pathManager.ToPointer();
            for (int index = 0; index < VanillaAttackFloodResultCapacity; index++)
            {
                byte* entry = manager + PathManagerFloodResultTileOffset +
                    index * PathManagerFloodResultStride;
                int tileId = *(int*)entry;
                int usableForAttack = *(int*)(entry + 4);
                if (tileId == 0 && usableForAttack == 0)
                    break;
                if (IsValidTileId(tileId) && usableForAttack != 0)
                    command.PublishedUnitAttackApproaches.Add(tileId);
            }

            LogCommandDiagnostic(
                $"stage=attack-unit-approaches commandSeq={command.Sequence} " +
                $"target={command.TargetValue1}/{command.TargetValue2} " +
                $"published={command.PublishedUnitAttackApproaches.Count}");
        }

        internal static bool TryGetPublishedBuildingFootprint(
            Dictionary<int, HashSet<int>> approaches,
            int approachTileId,
            out int footprintTileId)
        {
            footprintTileId = -1;
            if (approaches == null ||
                !approaches.TryGetValue(approachTileId, out HashSet<int> footprintTiles))
            {
                return false;
            }
            foreach (int candidate in footprintTiles)
            {
                footprintTileId = candidate;
                return true;
            }
            return false;
        }

        internal static bool HasPublishedBuildingApproachPair(
            Dictionary<int, HashSet<int>> approaches,
            int approachTileId,
            int footprintTileId) =>
            approaches != null &&
            approaches.TryGetValue(approachTileId, out HashSet<int> footprintTiles) &&
            footprintTiles.Contains(footprintTileId);

    }
}
