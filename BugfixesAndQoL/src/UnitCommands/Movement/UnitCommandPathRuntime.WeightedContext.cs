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
        // The extender owns 0x196280. Its synchronous event also covers the direct
        // group calls which never enter our temporary-path probe at 0x18E1E0.
        internal void ResolveCommandDiagnosticContext(
            int unitId,
            GameUnit* unit,
            out TribeAICommand command,
            out string commandContext,
            out int commandSequence)
        {
            if (activePlan != null && activePlan.PostCombatRepath &&
                activePlan.UnitId == unitId)
            {
                if (trackedMoatMoves.TryGetValue(unitId, out MoatMoveTracker tracker) &&
                    tracker.MapEpoch == mapEpoch &&
                    tracker.TargetX == activePlan.TargetX &&
                    tracker.TargetY == activePlan.TargetY)
                {
                    command = tracker.WeightedCommand;
                    commandSequence = tracker.WeightedCommandSequence;
                }
                else
                {
                    command = (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
                    commandSequence = 0;
                }
                commandContext = "post-combat-resume";
                return;
            }

            if (activeAttackCommand != null &&
                activeAttackCommand.TribeId == unit->r_TribeId &&
                activeAttackCommand.CandidateUnitIds.Contains(unitId))
            {
                command = activeAttackCommand.Command;
                commandContext = $"target-order-{activeAttackCommand.Command}";
                commandSequence = activeAttackCommand.Sequence;
                return;
            }

            if (activeMoveCommand != null && activeMoveCommand.TribeId == unit->r_TribeId)
            {
                command = TribeAICommand.MoveHerePosition;
                string orderKind = activeMoveCommand.IsPatrolPath ? "patrol-leg" : "move-order";
                string orderPhase = activeMoveCommand.IsNewOrder ? "new" : "continuation";
                commandContext = $"{orderKind}-{orderPhase}-{activeMoveCommand.MoveType}";
                commandSequence = activeMoveCommand.Sequence;
                return;
            }

            command = (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
            commandContext = $"unit-state-{command}";
            commandSequence = 0;
        }

        internal void CaptureMoveCommandGroupSummary(MoveCommandScope command)
        {
            if (command == null || command.GroupSummaryCaptured)
                return;
            command.GroupSummaryCaptured = true;
            if (command == null ||
                !TryCaptureOrderedActiveGroupUnits(
                    nativeTribeManager, command.TribeId, out int[] unitIds))
            {
                return;
            }

            command.ActiveUnitIdsAtDispatch = unitIds;
            foreach (int unitId in unitIds)
            {
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit))
                {
                    continue;
                }

                command.ActiveUnitsAtDispatch++;
                if (CanDigMoat(unit))
                    command.DiggersAtDispatch++;
                if (IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)))
                    command.UnitsOnMoatAtDispatch++;
                int playerId = unit->r_ControllableForPlayerId;
                if ((uint)playerId < 32)
                    command.PlayerMaskAtDispatch |= 1u << playerId;
            }


        }

        internal void EnsureMoveCommandGroupSummary(MoveCommandScope command)
        {
            if (command != null && !command.GroupSummaryCaptured)
                CaptureMoveCommandGroupSummary(command);
        }

        internal bool TryCaptureWeightedMovementCostProfile(
            GameUnit* unit,
            out WeightedMovementCostProfile profile,
            out string rejectionReason)
        {
            profile = default;
            rejectionReason = "invalid-unit";
            if (unit == null)
                return false;

            int tileId = GameTileManagerAPI.Instance.GetTileId(
                unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
            if (!IsValidTileId(tileId))
            {
                rejectionReason = "invalid-speed-snapshot-tile";
                return false;
            }

            int currentSpeed = unchecked((short)unit->r_CurrentSpeed);
            int currentSpeed2 = unchecked((short)unit->r_CurrentSpeed2);
            int speedBonus = unchecked((short)unit->r_SpeedBonus);
            int additionalSubsteps = *(short*)((byte*)unit + UnitAdditionalMovementSubstepsOffset);
            int extraDelay = unchecked((short)unit->r_PathPlanRelated1);
            int moatPhase = *((byte*)unit + UnitMoatSlowdownPhaseOffset);
            bool completedMoat = IsCompletedMoatTile(tileId);
            bool valid = WeightedMovementCostProfile.TryCreate(
                currentSpeed,
                currentSpeed2,
                speedBonus,
                additionalSubsteps,
                extraDelay,
                moatPhase,
                completedMoat,
                out profile,
                out rejectionReason);
            if (valid && !completedMoat && moatPhase == 0 &&
                currentSpeed2 - currentSpeed == 3)
            {
                // 0x19B260 keeps the +3 moat-exit delay for the update in which the
                // phase has already decayed to zero. A single snapshot cannot safely
                // distinguish that residual from another transient +3 adjustment.
                rejectionReason = "ambiguous-moat-exit-residual";
                return false;
            }
            if (valid && !completedMoat && moatPhase != 0 &&
                (tileFlags[tileId] & AlternativeTerrainDelayTileFlag) != 0)
            {
                // 0x19B260 uses a separate +2 branch here instead of the +3 moat
                // decay. The two contributions cannot be separated from this snapshot.
                rejectionReason = "ambiguous-terrain-and-moat-delay";
                return false;
            }
            return valid;
        }

        internal bool IsPublishedWalkableBuildingApproach(int unitId, int targetTileId)
        {
            if (!IsValidTileId(targetTileId) || !IsWalkableBuildingApproachEndpoint(targetTileId))
                return false;
            AttackCommandScope command = activeAttackCommand;
            if (command == null || !IsBuildingAttackCommand(command.Command) ||
                !command.CandidateUnitIds.Contains(unitId))
                return false;
            return command.PublishedBuildingApproaches.ContainsKey(targetTileId);
        }

        internal CompletedMoatRelationship ResolveCompletedMoatRelationship(
            int playerId, int tileId)
        {
            if (!IsCompletedMoatTile(tileId))
                return CompletedMoatRelationship.Invalid;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (tileManager == IntPtr.Zero || !playerApi.IsPlayerIdValid(playerId))
                return CompletedMoatRelationship.Invalid;

            int moatId = getMoatIdAtTile(tileManager, tileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (!IsValidMoatRecordId(moatId, moatCount))
                return CompletedMoatRelationship.Invalid;
            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            if (*(int*)moatRecord != tileId) return CompletedMoatRelationship.Invalid;
            int ownerId = moatRecord[MoatOwnerOffset];
            if (!playerApi.IsPlayerIdValid(ownerId))
                return CompletedMoatRelationship.Invalid;
            return ownerId == playerId || playerApi.IsPlayerAlliedTo(playerId, ownerId)
                ? CompletedMoatRelationship.Friendly
                : CompletedMoatRelationship.Enemy;
        }

        internal bool IsFriendlyCompletedMoatForWeightedShadow(int playerId, int tileId) =>
            ResolveCompletedMoatRelationship(playerId, tileId) ==
                CompletedMoatRelationship.Friendly;

        internal bool IsIsolatedActiveGroupUnit(int unitId, int tribeId)
        {
            if (tribeId < 0 || tribeId >= MaximumTribeCount || getGroupUnitId == null)
                return false;
            byte* tribeRecord = (byte*)nativeTribeManager.ToPointer() +
                tribeId * TribeRecordSize;
            return *(short*)(tribeRecord + TribeUnitCountOffset) == 1 &&
                getGroupUnitId(nativeTribeManager, tribeId, 0) == unitId;
        }

        internal void LogWeightedPublicationDecision(int unitId, string message)
        {
            if (lastWeightedPublicationDecisionByUnit.TryGetValue(
                    unitId, out string previous) &&
                string.Equals(previous, message, StringComparison.Ordinal))
            {
                return;
            }
            lastWeightedPublicationDecisionByUnit[unitId] = message;
            BufferOrLogCommandDiagnostic(message.StartsWith("Bugfixes and QoL friendly-moat-movement ", StringComparison.Ordinal)
                ? message.Substring("Bugfixes and QoL friendly-moat-movement ".Length) : message);
        }

        internal void LogWeightedShadowDecision(
            BuilderWeightedScope shadow, string decision)
        {
            RecordFillRouteDecision(shadow, decision);
            RecordWeightedCommandDecision(shadow, decision);
        }

        internal void RecordWeightedCommandDecision(BuilderWeightedScope shadow, string decision)
        {
            double searchMilliseconds = shadow.AccumulatedSearchMilliseconds;
            bool published = string.Equals(
                decision, "weighted-path-published", StringComparison.Ordinal);
            if (activeAttackCommand != null &&
                activeAttackCommand.TribeId == shadow.TribeId &&
                activeAttackCommand.CandidateUnitIds.Contains(shadow.UnitId))
            {
                activeAttackCommand.WeightedUnitIds.Add(shadow.UnitId);
                activeAttackCommand.WeightedDecisions++;
                if (published)
                    activeAttackCommand.WeightedPublished++;
                activeAttackCommand.WeightedSearchMilliseconds += searchMilliseconds;
                activeAttackCommand.WeightedMaximumSearchMilliseconds = Math.Max(
                    activeAttackCommand.WeightedMaximumSearchMilliseconds, searchMilliseconds);
                return;
            }

            if (activeMoveCommand != null && activeMoveCommand.TribeId == shadow.TribeId)
            {
                activeMoveCommand.WeightedUnitIds.Add(shadow.UnitId);
                activeMoveCommand.WeightedDecisions++;
                if (published)
                    activeMoveCommand.WeightedPublished++;
                activeMoveCommand.WeightedSearchMilliseconds += searchMilliseconds;
                activeMoveCommand.WeightedMaximumSearchMilliseconds = Math.Max(
                    activeMoveCommand.WeightedMaximumSearchMilliseconds, searchMilliseconds);
            }
        }

        internal sealed class BuilderWeightedScope
        {
            public PlanScope FillPlan { get; set; }
            public uint UnitGlobalId { get; set; }
            public BuilderWeightedScope(
                int mapEpoch,
                int unitId,
                eChimps unitType,
                int playerId,
                int tribeId,
                TribeAICommand command,
                string commandContext,
                int commandSequence,
                int startX,
                int startY,
                int targetX,
                int targetY,
                int snapshotCurrentX,
                int snapshotCurrentY,
                uint aiState,
                uint rawCommand,
                string workKind,
                string workPhase,
                WeightedMovementCostProfile costProfile,
                bool allowReservedTarget,
                bool calibratable)
            {
                MapEpoch = mapEpoch;
                UnitId = unitId;
                UnitType = unitType;
                PlayerId = playerId;
                TribeId = tribeId;
                Command = command;
                CommandContext = commandContext;
                CommandSequence = commandSequence;
                StartX = startX;
                StartY = startY;
                TargetX = targetX;
                TargetY = targetY;
                SnapshotCurrentX = snapshotCurrentX;
                SnapshotCurrentY = snapshotCurrentY;
                AiState = aiState;
                RawCommand = rawCommand;
                WorkKind = workKind;
                WorkPhase = workPhase;
                CostProfile = costProfile;
                AllowReservedTarget = allowReservedTarget;
                Calibratable = calibratable;
                Candidate = WeightedMoatRouteSummary.Failed("not-evaluated", 0);
            }

            public int MapEpoch { get; }
            public int UnitId { get; }
            public eChimps UnitType { get; }
            public int PlayerId { get; }
            public int TribeId { get; }
            public TribeAICommand Command { get; }
            public string CommandContext { get; }
            public int CommandSequence { get; }
            public int StartX { get; }
            public int StartY { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int SnapshotCurrentX { get; }
            public int SnapshotCurrentY { get; }
            public uint AiState { get; }
            public uint RawCommand { get; }
            public string WorkKind { get; }
            public string WorkPhase { get; }
            public string CaptureSource => "unit-builder";
            public WeightedMovementCostProfile CostProfile { get; }
            public bool AllowReservedTarget { get; }
            public bool CandidateFound { get; set; }
            public WeightedMoatRouteSummary Candidate { get; set; }
            public WeightedMoatEncodedRoute CandidateRoute { get; set; }
            public bool Calibratable { get; }
            public double AccumulatedSearchMilliseconds { get; set; }
            public int SearchPasses { get; set; }
            public long OptimisticLowerBoundTicks { get; set; } = long.MaxValue;
            public int PublishedBuilderResult { get; set; } = -1;
        }

    }
}
