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
        internal void LogBuildingConsumerCandidates(
            AttackApproachDiagnosticScope scope,
            BuildingApproachCandidate[] before,
            AttackApproachState vanillaAfter,
            BuildingConsumerFallbackResult fallback,
            LadderBuildingCandidateRestoreResult ladderRestore)
        {
            int producerPairs = 0;
            int producerApproachOnly = 0;
            foreach (BuildingApproachCandidate candidate in before)
            {
                if (candidate.ApproachTileId > 0 && candidate.FootprintTileId > 0)
                    producerPairs++;
                else
                    producerApproachOnly++;
            }
            LogCommandDiagnostic(
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
            LogCommandDiagnostic(
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
        }

        internal void LogBuildingConsumerPerformance(
            BuildingConsumerPerformanceScope performance,
            BuildingConsumerFallbackResult fallback)
        {
            if (performance == null)
                return;

            LogCommandDiagnostic(
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
        }

        internal void LogBuildingApproachPerformance(
            BuildingApproachPerformanceScope performance)
        {
            if (performance == null)
                return;

            double estimatedVanillaMilliseconds = Math.Max(
                0.0,
                performance.TotalMilliseconds - performance.RegionFallbackMilliseconds);
            LogCommandDiagnostic(
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
        }

        internal static AttackApproachState CaptureAttackApproachState(
            IntPtr pathManager,
            bool requirePairedResult = false,
            bool requireReachableScore = true)
        {
            if (pathManager == IntPtr.Zero)
                return default;

            byte* manager = (byte*)pathManager.ToPointer();
            int resultCount = 0;
            int usableResultCount = 0;
            int malformedResultCount = 0;
            int firstResultTile = 0;
            int firstCompanionTile = 0;
            int firstScore = 0;
            bool usablePrefix = true;
            for (int index = 0; index < VanillaAttackFloodResultCapacity; index++)
            {
                byte* result = manager + PathManagerFloodResultTileOffset +
                    index * PathManagerFloodResultStride;
                int tileId = *(int*)result;
                int classification = *(int*)(result + 4);
                int score = *(int*)(result + 8);
                if (tileId == 0 && (requirePairedResult || classification == 0))
                    break;
                if (resultCount == 0)
                {
                    firstResultTile = tileId;
                    firstCompanionTile = classification;
                    firstScore = score;
                }
                resultCount++;
                // DA020's fallback phase emits approach-only entries with a zero footprint.
                // Its consumer cannot use those as building attack pairs.
                bool usable = tileId > 0 && (!requirePairedResult ||
                    (classification > 0 &&
                    (!requireReachableScore || score != VanillaUnreachableCandidateScore)));
                if (usablePrefix && usable)
                    usableResultCount++;
                else
                    usablePrefix = false;
                if (!usable)
                    malformedResultCount++;
            }

            return new AttackApproachState(
                *(int*)(manager + PathManagerFloodGenerationOffset),
                *(int*)(manager + PathManagerFloodDepthOffset),
                *(int*)(manager + PathManagerFloodQueueHeadOffset),
                *(int*)(manager + PathManagerFloodQueueTailOffset),
                resultCount,
                usableResultCount,
                malformedResultCount,
                firstResultTile,
                firstCompanionTile,
                firstScore);
        }

        internal void LogAttackApproachDiagnostic(AttackApproachDiagnosticScope scope)
        {
            if (!scope.OwnerCommand.AttackApproachDiagnosticSignatures.Add(
                scope.GetSemanticSignature()))
            {
                return;
            }

            LogCommandDiagnostic(
                $"stage=attack-approach kind={scope.Kind} commandSeq={scope.CommandSequence} " +
                $"command={scope.Command} tribe={scope.TribeId} unit={scope.UnitId} " +
                $"type={scope.UnitType} player={scope.PlayerId} targetContext={scope.TargetContext} " +
                $"target=({scope.TargetX},{scope.TargetY}) requestedResults={scope.RequestedResults} " +
                $"sourceRegion={scope.SourceRegion} movementClass={scope.MovementClass} " +
                $"consumerVariant={scope.ConsumerVariantText} " +
                $"before=[{scope.Before.ToLogFields()}] after=[{scope.After.ToLogFields()}] " +
                $"regionPairs={scope.FormatRegionPairs()} tilePairs={scope.FormatTilePairs()}");
        }

        internal void DiagnoseAttackApproachTilePair(
            AttackApproachDiagnosticScope scope,
            int targetTileId,
            int selectedUnitTileId,
            byte useCache,
            int vanillaResult)
        {
            bool ownerRoute = false;
            RouteProbeSummary summary = default;
            int startX = -1;
            int startY = -1;
            int targetX = -1;
            int targetY = -1;
            try
            {
                if (IsValidTileId(selectedUnitTileId) && IsValidTileId(targetTileId))
                {
                    UnmanagedVector2<ushort> start =
                        GameTileManagerAPI.Instance.GetTileVectorFromId(selectedUnitTileId);
                    UnmanagedVector2<ushort> target =
                        GameTileManagerAPI.Instance.GetTileVectorFromId(targetTileId);
                    startX = start.X;
                    startY = start.Y;
                    targetX = target.X;
                    targetY = target.Y;

                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("attack-approach-tile-pair-probe", ex);
            }

            scope.ObserveTilePair(
                targetTileId,
                selectedUnitTileId,
                IsValidTileId(selectedUnitTileId) ? pathRegionGrid[selectedUnitTileId] : 0,
                IsValidTileId(targetTileId) ? pathRegionGrid[targetTileId] : 0,
                startX,
                startY,
                targetX,
                targetY,
                useCache,
                vanillaResult,
                ownerRoute,
                summary);
        }

        internal sealed class BuildingApproachPerformanceScope
        {
            public BuildingApproachPerformanceScope(int commandSequence, int buildingId)
            {
                CommandSequence = commandSequence;
                BuildingId = buildingId;
            }

            public int CommandSequence { get; }
            public int BuildingId { get; }
            public int RegionFallbackEvaluations { get; set; }
            public int RouteEvaluations { get; set; }
            public long SharedNodes { get; set; }
            public int IndexScans { get; set; }
            public int IndexedNativeTiles { get; set; }
            public int IndexedFootprintTiles { get; set; }
            public int IndexedApproachTiles { get; set; }
            public int ReachabilityMapsBuilt { get; set; }
            public int ReachabilityCacheHits { get; set; }
            public int MoatOwnerCacheHits { get; set; }
            public int MoatOwnerCacheMisses { get; set; }
            public long TotalElapsedTicks { get; set; }
            public long RegionFallbackElapsedTicks { get; set; }
            public long IndexElapsedTicks { get; set; }
            public Dictionary<int, List<int>> ApproachTilesByRegion { get; set; }
            public Dictionary<int, int> MoatOwnerByTile { get; } =
                new Dictionary<int, int>();

            public double TotalMilliseconds =>
                TotalElapsedTicks * 1000.0 / Stopwatch.Frequency;
            public double RegionFallbackMilliseconds =>
                RegionFallbackElapsedTicks * 1000.0 / Stopwatch.Frequency;
            public double IndexMilliseconds =>
                IndexElapsedTicks * 1000.0 / Stopwatch.Frequency;
        }

        internal sealed class BuildingConsumerPerformanceScope
        {
            public BuildingConsumerPerformanceScope(
                int commandSequence, int buildingId, int rawCandidates)
            {
                CommandSequence = commandSequence;
                BuildingId = buildingId;
                RawCandidates = rawCandidates;
            }

            public int CommandSequence { get; }
            public int BuildingId { get; }
            public int RawCandidates { get; }
            public int ValidCandidates { get; set; }
            public int DiggerUnits { get; set; }
            public int RouteEvaluations { get; set; }
            public long SearchNodes { get; set; }
            public int ApproachOnly { get; set; }
            public int AttackPlaces { get; set; }
            public int ReachabilityMapsBuilt { get; set; }
            public int ReachabilityCacheHits { get; set; }
            public int MoatOwnerCacheHits { get; set; }
            public int MoatOwnerCacheMisses { get; set; }
            public long VanillaElapsedTicks { get; set; }
            public long FallbackElapsedTicks { get; set; }
            public Dictionary<int, int> MoatOwnerByTile { get; } =
                new Dictionary<int, int>();

            public double VanillaMilliseconds =>
                VanillaElapsedTicks * 1000.0 / Stopwatch.Frequency;
            public double FallbackMilliseconds =>
                FallbackElapsedTicks * 1000.0 / Stopwatch.Frequency;
        }

        internal sealed class AttackApproachDiagnosticScope
        {
            internal readonly Dictionary<string, int> regionPairCounts =
                new Dictionary<string, int>();
            internal readonly Dictionary<string, TilePairAggregate> tilePairGroups =
                new Dictionary<string, TilePairAggregate>();

            public AttackApproachDiagnosticScope(
                AttackCommandScope ownerCommand,
                AttackApproachKind kind,
                int commandSequence,
                TribeAICommand command,
                int tribeId,
                int targetContext,
                int targetX,
                int targetY,
                int requestedResults,
                int sourceRegion,
                int movementClass,
                int unitId,
                int playerId,
                eChimps unitType,
                AttackApproachState before)
            {
                OwnerCommand = ownerCommand;
                Kind = kind;
                CommandSequence = commandSequence;
                Command = command;
                TribeId = tribeId;
                TargetContext = targetContext;
                TargetX = targetX;
                TargetY = targetY;
                RequestedResults = requestedResults;
                SourceRegion = sourceRegion;
                MovementClass = movementClass;
                UnitId = unitId;
                PlayerId = playerId;
                UnitType = unitType;
                Before = before;
            }

            public AttackCommandScope OwnerCommand { get; }
            public AttackApproachKind Kind { get; }
            public int CommandSequence { get; }
            public TribeAICommand Command { get; }
            public int TribeId { get; }
            public int TargetContext { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int RequestedResults { get; }
            public int SourceRegion { get; }
            public int MovementClass { get; }
            public int UnitId { get; }
            public int PlayerId { get; }
            public eChimps UnitType { get; }
            public AttackApproachState Before { get; }
            public AttackApproachState After { get; set; }
            public Dictionary<string, AttackRegionFallbackDecision> RegionFallbackDecisions { get; } =
                new Dictionary<string, AttackRegionFallbackDecision>(StringComparer.Ordinal);
            public int? ConsumerVariant { get; set; }
            public string ConsumerVariantText =>
                ConsumerVariant.HasValue ? ConsumerVariant.Value.ToString() : "not-applicable";

            public string GetSemanticSignature() =>
                $"{Kind}:{Command}:{TribeId}:{TargetContext}:{TargetX}:{TargetY}:" +
                $"{RequestedResults}:{SourceRegion}:{MovementClass}:{UnitId}:{PlayerId}:{UnitType}:" +
                $"{ConsumerVariantText}:" +
                $"{After.ResultCount}:{After.UsableResultCount}:{After.MalformedResultCount}:" +
                $"{After.FirstResultTile}:{After.FirstCompanionTile}:{After.FirstScore}:" +
                $"{FormatRegionPairs()}:{FormatTilePairs()}";

            public void ObserveRegionPair(
                int movementClass,
                int sourceRegion,
                int targetRegion,
                int routeKind,
                int vanillaResult)
            {
                string key = $"class={movementClass},regions={sourceRegion}->{targetRegion}," +
                    $"routeKind={routeKind},vanilla={vanillaResult}";
                regionPairCounts.TryGetValue(key, out int count);
                regionPairCounts[key] = count + 1;
            }

            public void ObserveTilePair(
                int targetTileId,
                int selectedUnitTileId,
                int selectedRegion,
                int targetRegion,
                int startX,
                int startY,
                int targetX,
                int targetY,
                byte useCache,
                int vanillaResult,
                bool ownerRoute,
                RouteProbeSummary summary)
            {
                string key = $"regions={selectedRegion}->{targetRegion},cache={useCache}," +
                    $"vanilla={vanillaResult},ownerBfs={ownerRoute},friendly={summary.FriendlyMoatTiles}," +
                    $"enemy={summary.EnemyMoatTiles}";
                string pair = $"({startX},{startY})/{selectedUnitTileId}->" +
                    $"({targetX},{targetY})/{targetTileId}";
                if (!tilePairGroups.TryGetValue(key, out TilePairAggregate aggregate))
                {
                    aggregate = new TilePairAggregate(pair);
                    tilePairGroups[key] = aggregate;
                }
                aggregate.Observe(pair);
            }

            public string FormatRegionPairs()
            {
                if (regionPairCounts.Count == 0)
                    return "none";
                List<string> values = new List<string>(regionPairCounts.Count);
                foreach (KeyValuePair<string, int> pair in regionPairCounts)
                    values.Add($"{{{pair.Key},calls={pair.Value}}}");
                values.Sort(StringComparer.Ordinal);
                return string.Join(";", values);
            }

            public string FormatTilePairs()
            {
                if (tilePairGroups.Count == 0)
                    return "none";
                List<string> values = new List<string>(tilePairGroups.Count);
                foreach (KeyValuePair<string, TilePairAggregate> pair in tilePairGroups)
                {
                    values.Add(
                        $"{{{pair.Key},calls={pair.Value.Count},first={pair.Value.First}," +
                        $"last={pair.Value.Last}}}");
                }
                values.Sort(StringComparer.Ordinal);
                return string.Join(";", values);
            }
        }

    }
}
