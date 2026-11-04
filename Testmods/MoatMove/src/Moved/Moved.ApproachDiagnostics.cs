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
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.ApproachDiagnostics.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void LogBuildingConsumerCandidates(
            AttackApproachDiagnosticScope scope,
            BuildingApproachCandidate[] before,
            AttackApproachState vanillaAfter,
            BuildingConsumerFallbackResult fallback,
            LadderBuildingCandidateRestoreResult ladderRestore)
        {
#if SERP_DEVDIAG
            int producerPairs = 0;
            int producerApproachOnly = 0;
            foreach (BuildingApproachCandidate candidate in before)
            {
                if (candidate.ApproachTileId > 0 && candidate.FootprintTileId > 0)
                    producerPairs++;
                else
                    producerApproachOnly++;
            }
            runtime.LogCommandDiagnostic(
                $"stage=building-consumer-candidates commandSeq={scope.CommandSequence} " +
                $"building={scope.OwnerCommand.TargetValue1}/{scope.OwnerCommand.TargetValue2} " +
                $"producerRaw={before.Length} producerPairs={producerPairs} " +
                $"producerApproachOnly={producerApproachOnly} " +
                $"vanillaRaw={vanillaAfter.ResultCount} " +
                $"vanillaScoredPairs={vanillaAfter.UsableResultCount} " +
                $"vanillaMalformed={vanillaAfter.MalformedResultCount} " +
                $"afterMoatPairs={ladderRestore.AfterMoatPairs} " +
                $"ladderProofs={ladderRestore.ProvenTransitions} " +
                $"ladderEligible={ladderRestore.EligiblePairs} " +
                $"ladderRestored={ladderRestore.RestoredPairs} " +
                $"ladderReason={ladderRestore.Reason} " +
                $"finalPairs={scope.After.UsableResultCount} " +
                $"finalFirst={scope.After.FirstResultTile}/" +
                $"{scope.After.FirstCompanionTile}/{scope.After.FirstScore}.");
            runtime.LogCommandDiagnostic(
                $"stage=building-consumer-fallback commandSeq={scope.CommandSequence} " +
                $"building={scope.OwnerCommand.TargetValue1}/{scope.OwnerCommand.TargetValue2} " +
                $"applied={fallback.WasApplied} reason={fallback.Reason} " +
                $"diggers={fallback.DiggerUnits} published={fallback.PublishedCandidates} " +
                $"walkableReservations={fallback.WalkableReservations} " +
                $"missingContext={fallback.MissingContexts} " +
                $"invalidContext={fallback.InvalidContexts} " +
                $"reservedBlocked={fallback.ReservedBlocked} " +
                $"ownerRouteRejected={fallback.OwnerRouteRejected} " +
                fallback.Summary.ToLogFields());
#endif
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void LogBuildingConsumerPerformance(
            BuildingConsumerPerformanceScope performance,
            BuildingConsumerFallbackResult fallback)
        {
#if SERP_DEVDIAG
            if (performance == null)
                return;

            runtime.LogCommandDiagnostic(
                $"stage=building-consumer-performance " +
                $"commandSeq={performance.CommandSequence} building={performance.BuildingId} " +
                $"vanillaMs={performance.VanillaMilliseconds:F3} " +
                $"fallbackMs={performance.FallbackMilliseconds:F3} " +
                $"rawCandidates={performance.RawCandidates} " +
                $"validCandidates={performance.ValidCandidates} " +
                $"diggers={performance.DiggerUnits} " +
                $"routeEvaluations={performance.RouteEvaluations} sharedNodes={performance.SearchNodes} " +
                $"attackPlaces={performance.AttackPlaces} approachOnly={performance.ApproachOnly} " +
                $"reachabilityMaps={performance.ReachabilityMapsBuilt} " +
                $"reachabilityCacheHits={performance.ReachabilityCacheHits} " +
                $"moatOwnerCache={performance.MoatOwnerCacheHits}/" +
                $"{performance.MoatOwnerCacheMisses} " +
                $"applied={fallback.WasApplied} reason={fallback.Reason}");
#endif
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void LogBuildingApproachPerformance(
            BuildingApproachPerformanceScope performance)
        {
#if SERP_DEVDIAG
            if (performance == null)
                return;

            double estimatedVanillaMilliseconds = Math.Max(
                0.0,
                performance.TotalMilliseconds - performance.RegionFallbackMilliseconds);
            runtime.LogCommandDiagnostic(
                $"stage=building-approach-performance " +
                $"commandSeq={performance.CommandSequence} building={performance.BuildingId} " +
                $"totalMs={performance.TotalMilliseconds:F3} " +
                $"fallbackMs={performance.RegionFallbackMilliseconds:F3} " +
                $"vanillaEstimatedMs={estimatedVanillaMilliseconds:F3} " +
                $"fallbackEvaluations={performance.RegionFallbackEvaluations} " +
                $"routeEvaluations={performance.RouteEvaluations} sharedNodes={performance.SharedNodes} " +
                $"indexMs={performance.IndexMilliseconds:F3} " +
                $"indexScans={performance.IndexScans} " +
                $"nativeTiles={performance.IndexedNativeTiles} " +
                $"footprintTiles={performance.IndexedFootprintTiles} " +
                $"approachTiles={performance.IndexedApproachTiles} " +
                $"regions={performance.ApproachTilesByRegion?.Count ?? 0} " +
                $"reachabilityMaps={performance.ReachabilityMapsBuilt} " +
                $"reachabilityCacheHits={performance.ReachabilityCacheHits} " +
                $"moatOwnerCache={performance.MoatOwnerCacheHits}/" +
                $"{performance.MoatOwnerCacheMisses}");
#endif
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void LogAttackApproachDiagnostic(AttackApproachDiagnosticScope scope)
        {
#if SERP_DEVDIAG
            if (!scope.OwnerCommand.AttackApproachDiagnosticSignatures.Add(
                scope.GetSemanticSignature()))
            {
                return;
            }

            runtime.LogCommandDiagnostic(
                $"stage=attack-approach kind={scope.Kind} commandSeq={scope.CommandSequence} " +
                $"command={scope.Command} tribe={scope.TribeId} unit={scope.UnitId} " +
                $"type={scope.UnitType} player={scope.PlayerId} targetContext={scope.TargetContext} " +
                $"target=({scope.TargetX},{scope.TargetY}) requestedResults={scope.RequestedResults} " +
                $"sourceRegion={scope.SourceRegion} movementClass={scope.MovementClass} " +
                $"consumerVariant={scope.ConsumerVariantText} " +
                $"before=[{scope.Before.ToLogFields()}] after=[{scope.After.ToLogFields()}] " +
                $"regionPairs={scope.FormatRegionPairs()} tilePairs={scope.FormatTilePairs()}");
#endif
        }
    }
}
