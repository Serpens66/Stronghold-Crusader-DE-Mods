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
        internal void ObserveAttackApproachFloodBuilder(
            IntPtr pathManager,
            int tribeId,
            int targetContext,
            uint targetX,
            uint targetY,
            int requestedResults,
            int sourceRegion,
            int movementClass)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            IEnemyGatePathPolicy gate = BeginEnemyGateSearch(
                movementClass, EnemyGateSearchKind.Attack, out object gateScope);
            object bridgeSearch = EnemyBridgeDiagnosticBridge.BeginSearch("Attack", movementClass);
            bool bridgeCompleted = false;
            try
            {
                ObserveAttackApproachFloodBuilderWithMoat(
                    pathManager, tribeId, targetContext, targetX, targetY,
                    requestedResults, sourceRegion, movementClass);
                bridgeCompleted = true;
            }
            finally
            {
                EnemyBridgeDiagnosticBridge.EndSearch(bridgeSearch, bridgeCompleted, 0);
                EndEnemyGateSearch(gate, gateScope, EnemyGateSearchKind.Attack, bridgeCompleted, false);
                Shared.DevDiag.StopSpike(ref measured, "uc.attack.approachFlood", 20000);
            }
        }

        internal void ObserveAttackApproachFloodBuilderWithMoat(
            IntPtr pathManager, int tribeId, int targetContext, uint targetX, uint targetY,
            int requestedResults, int sourceRegion, int movementClass)
        {
            // The attack scope only serves friendly-moat routes (MoatMove). Without the
            // traversal provider this hook runs Vanilla plus the independent ladder fix.
            if (TraversalEnabled)
                Traversal.AttackApproachFloodTraversal(pathManager, tribeId, targetContext,
                    targetX, targetY, requestedResults, sourceRegion, movementClass);
            else
                RunAttackApproachFloodWithVanillaLadderFix(pathManager, tribeId, targetContext,
                    targetX, targetY, requestedResults, sourceRegion, movementClass);
        }

        internal readonly struct AttackRegionFallbackDecision
        {
            internal AttackRegionFallbackDecision(
                bool allowed,
                string reason,
                RouteProbeSummary summary,
                int approachX,
                int approachY)
            {
                Allowed = allowed;
                Reason = reason;
                Summary = summary;
                ApproachX = approachX;
                ApproachY = approachY;
            }

            public bool Allowed { get; }
            public string Reason { get; }
            public RouteProbeSummary Summary { get; }
            public int ApproachX { get; }
            public int ApproachY { get; }

            public static AttackRegionFallbackDecision Allow(
                RouteProbeSummary summary,
                int approachX,
                int approachY,
                string reason = "required-friendly-moat-route") =>
                new AttackRegionFallbackDecision(
                    true, reason, summary, approachX, approachY);

            public static AttackRegionFallbackDecision Reject(
                string reason,
                RouteProbeSummary summary = default,
                int approachX = -1,
                int approachY = -1) =>
                new AttackRegionFallbackDecision(false, reason, summary, approachX, approachY);
        }

        internal readonly struct AttackApproachState
        {
            public AttackApproachState(
                int generation,
                int depth,
                int queueHead,
                int queueTail,
                int resultCount,
                int usableResultCount,
                int malformedResultCount,
                int firstResultTile,
                int firstCompanionTile,
                int firstScore)
            {
                Generation = generation;
                Depth = depth;
                QueueHead = queueHead;
                QueueTail = queueTail;
                ResultCount = resultCount;
                UsableResultCount = usableResultCount;
                MalformedResultCount = malformedResultCount;
                FirstResultTile = firstResultTile;
                FirstCompanionTile = firstCompanionTile;
                FirstScore = firstScore;
            }

            public int Generation { get; }
            public int Depth { get; }
            public int QueueHead { get; }
            public int QueueTail { get; }
            public int ResultCount { get; }
            public int UsableResultCount { get; }
            public int MalformedResultCount { get; }
            public int FirstResultTile { get; }
            public int FirstCompanionTile { get; }
            public int FirstScore { get; }

            public string ToLogFields() =>
                $"generation={Generation} depth={Depth} queue={QueueHead}->{QueueTail} " +
                $"results={ResultCount} usable={UsableResultCount} malformed={MalformedResultCount} " +
                $"first={FirstResultTile}/{FirstCompanionTile}/{FirstScore}";
        }

    }
}
