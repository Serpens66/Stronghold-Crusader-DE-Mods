extern alias BugfixesRuntime;
using BepInEx.Logging;
using APIShared;
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
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.ApproachPublication.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
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
                    unit->r_TribeId != command.TribeId || !runtime.CanDigMoat(unit))
                {
                    continue;
                }

                unitId = candidateId;
                playerId = unit->r_ControllableForPlayerId;
                unitType = unit->r_UnitChimp;
                return;
            }
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
                command.MapEpoch != runtime.mapEpoch || pathManager == IntPtr.Zero)
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

            runtime.LogCommandDiagnostic(
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
    }
}
