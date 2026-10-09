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
        internal sealed class TilePairAggregate
        {
            public TilePairAggregate(string first)
            {
                First = first;
                Last = first;
            }

            public int Count { get; internal set; }
            public string First { get; }
            public string Last { get; internal set; }

            public void Observe(string pair)
            {
                Count++;
                Last = pair;
            }
        }

        internal sealed class MoveCommandScope
        {
            internal const int MaximumDiagnosticDetailsPerStage = 3;
            internal readonly Dictionary<string, int> retainedDiagnosticsByStage =
                new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly MovementOptionsSnapshot Options;
            internal readonly RequiredRouteMetrics Required;
            internal readonly RequiredRouteCache RequiredCache;
            public MoveCommandScope(
                int sequence,
                int tribeId,
                int targetX,
                int targetY,
                bool isPatrolPath,
                bool isNewOrder,
                TribeMoveType moveType,
                int parentAttackCommandSequence,
                TribeAICommand parentAttackCommand,
                MovementOptionsSnapshot currentOptions)
            {
                Options = activeAttackCommand?.Options ?? currentOptions;
                Required = activeAttackCommand?.Required ?? new RequiredRouteMetrics();
                RequiredCache = Options.RequiredOnly && activeAttackCommand != null
                    ? activeAttackCommand.RequiredCache
                    : new RequiredRouteCache();
                Sequence = sequence;
                TribeId = tribeId;
                TargetX = targetX;
                TargetY = targetY;
                IsPatrolPath = isPatrolPath;
                IsNewOrder = isNewOrder;
                MoveType = moveType;
                ParentAttackCommandSequence = parentAttackCommandSequence;
                ParentAttackCommand = parentAttackCommand;
                StartTimestamp = Stopwatch.GetTimestamp();
            }

            public int Sequence { get; }
            public int TribeId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public bool IsPatrolPath { get; }
            public bool IsNewOrder { get; }
            public TribeMoveType MoveType { get; }
            public int ParentAttackCommandSequence { get; }
            public TribeAICommand ParentAttackCommand { get; }
            public long StartTimestamp { get; }
            public double ElapsedMilliseconds { get; set; }
            public int ActiveUnitsAtDispatch { get; set; }
            internal bool NativeCommonFallback;
            public bool GroupSummaryCaptured { get; set; }
            public int[] ActiveUnitIdsAtDispatch { get; set; } = Array.Empty<int>();
            public int DiggersAtDispatch { get; set; }
            public int UnitsOnMoatAtDispatch { get; set; }
            public uint PlayerMaskAtDispatch { get; set; }
            public int CentralPlannerCalls { get; set; }
            public int UnitMoveCalls { get; set; }
            public int UnitMoveCompleted { get; set; }
            public int UnitMovePositive { get; set; }
            public int UnitMoveWithoutBuilder { get; set; }
            public int UnitMoveAlreadyArrived { get; set; }
            public int UnitMoveAbandoned { get; set; }
            public int BuilderIntermediateTargets { get; set; }
            public Dictionary<string, int> ContractRejectionReasons { get; } = new Dictionary<string, int>();
            public int FloodCalls { get; set; }
            public int FloodVanillaPositive { get; set; }
            public int FloodFillBypasses { get; set; }
            public bool FloodOwnerRouteEvaluated { get; set; }
            public bool FloodOwnerRouteAllowed { get; set; }
            public int ModeCalls { get; set; }
            public int RegionCalls { get; set; }
            public int BuilderCalls { get; set; }
            public int VanillaBuilderCalls { get; set; }
            public int FallbackBuilderCalls { get; set; }
            public int FallbackContractRejections { get; set; }
            public int FallbackRollbacks { get; set; }
            public int PositiveBuilderCalls { get; set; }
            public int LastBuilderResult { get; set; } = int.MinValue;
            public int LastVanillaBuilderResult { get; set; } = int.MinValue;
            public int PreBuilderFailures { get; set; }
            public int PreBuilderRecovered { get; set; }
            public Dictionary<string, int> PreBuilderRejectionReasons { get; } =
                new Dictionary<string, int>(StringComparer.Ordinal);
            public bool MoatRelevant { get; set; }
            public bool BuilderReached { get; set; }
            public int WeightedDecisions { get; set; }
            public int WeightedPublished { get; set; }
            public double WeightedSearchMilliseconds { get; set; }
            public double WeightedMaximumSearchMilliseconds { get; set; }
            public HashSet<int> WeightedUnitIds { get; } = new HashSet<int>();
            public int TargetedRouteSearches { get; set; }
            public int TargetedRouteSearchPasses { get; set; }
            public int TargetedRouteCacheHits { get; set; }
            public int TargetedRouteExpandedNodes { get; set; }
            public double TargetedRouteSearchMilliseconds { get; set; }
            public double TargetedRouteMaximumSearchMilliseconds { get; set; }
            public Dictionary<RouteDecisionKey, TargetedRouteDecision> TargetedRouteDecisions =>
                RequiredCache.Decisions;
            public string LastGroupMoatModeDiagnostic { get; set; }
            public int EarlyRegionCalls { get; set; }
            public int EarlyRegionBypasses { get; set; }
            public Dictionary<string, EarlyGroupRegionDecision> EarlyRegionDecisions { get; } =
                new Dictionary<string, EarlyGroupRegionDecision>();
            public HashSet<string> EarlyRegionLogSignatures { get; } =
                new HashSet<string>(StringComparer.Ordinal);
            public bool HasQueuePreSnapshot { get; set; }
            public NativeWaypointQueueSnapshot QueuePreSnapshot { get; set; }
            public bool HasQueuePostSnapshot { get; set; }
            public NativeWaypointQueueSnapshot QueuePostSnapshot { get; set; }
            public int DiagnosticMessages { get; internal set; }
            public int DiagnosticCharacters { get; internal set; }
            public Dictionary<string, int> SuppressedDiagnostics { get; } =
                new Dictionary<string, int>(StringComparer.Ordinal);
            public List<string> Diagnostics { get; } = new List<string>();

            public void BufferDiagnostic(string message)
            {
                message = message ?? string.Empty;
                DiagnosticMessages++;
                DiagnosticCharacters += message.Length;
                string stage = AttackCommandScope.GetDiagnosticStage(message);
                retainedDiagnosticsByStage.TryGetValue(stage, out int retained);
                if (retained < MaximumDiagnosticDetailsPerStage)
                {
                    retainedDiagnosticsByStage[stage] = retained + 1;
                    Diagnostics.Add(message);
                    return;
                }
                SuppressedDiagnostics.TryGetValue(stage, out int suppressed);
                SuppressedDiagnostics[stage] = suppressed + 1;
            }
        }

        internal readonly struct RouteDecisionKey : IEquatable<RouteDecisionKey>
        {
            internal readonly int epoch, tick, player, start, target, work;
            internal readonly bool reserved;
            internal readonly long revision;
            internal readonly WeightedMovementCostProfile profile;
            public RouteDecisionKey(int epoch, int tick, int player, int start, int target, bool reserved,
                int work, long revision, WeightedMovementCostProfile profile)
            { this.epoch=epoch; this.tick=tick; this.player=player; this.start=start; this.target=target;
                this.reserved=reserved; this.work=work; this.revision=revision; this.profile=profile; }
            public bool Equals(RouteDecisionKey k) => epoch==k.epoch && tick==k.tick && player==k.player &&
                start==k.start && target==k.target && reserved==k.reserved && work==k.work && revision==k.revision && profile.Equals(k.profile);
            public override bool Equals(object other) => other is RouteDecisionKey k && Equals(k);
            public override int GetHashCode() { unchecked { int h=epoch; h=h*397^tick; h=h*397^player;
                h=h*397^start; h=h*397^target; h=h*397^work; h=h*397^revision.GetHashCode();
                return (h*397^profile.GetHashCode())*397^(reserved?1:0); } }
        }

        internal sealed class QualifiedMovementRoute
        {
            public readonly int StartX, StartY, TargetX, TargetY, Player, Epoch, Tick;
            public readonly long Revision;
            public readonly WeightedMoatEncodedRoute Route;
            public readonly WeightedMoatRouteSummary Summary;
            public readonly WeightedMovementCostProfile Profile;
            public readonly bool Optimal;
            public QualifiedMovementRoute(int sx,int sy,int tx,int ty,int player,int epoch,int tick,long revision,
                WeightedMoatEncodedRoute route,WeightedMoatRouteSummary summary,WeightedMovementCostProfile profile,bool optimal)
            { StartX=sx;StartY=sy;TargetX=tx;TargetY=ty;Player=player;Epoch=epoch;Tick=tick;Revision=revision;
                Route=route;Summary=summary;Profile=profile;Optimal=optimal; }
        }

        internal readonly struct TargetedRouteDecision
        {
            public TargetedRouteDecision(
                bool requiredFriendlyMoat, RouteProbeSummary summary, QualifiedMovementRoute route = null)
            {
                RequiredFriendlyMoat = requiredFriendlyMoat;
                Summary = summary;
                Route = route;
            }

            public bool RequiredFriendlyMoat { get; }
            public RouteProbeSummary Summary { get; }
            public QualifiedMovementRoute Route { get; }
        }

        internal sealed class UnitMoveFrame
        {
            internal readonly MovementOptionsSnapshot Options;
            public UnitMoveFrame(UnitMoveHereEventArgs args, UnitMoveFrame parent,
                int mapEpoch, int tick, MoveCommandScope command,
                MovementOptionsSnapshot currentOptions)
            {
                Args = args;
                Parent = parent;
                Options = command?.Options ?? parent?.Options ?? currentOptions;
                MapEpoch = mapEpoch;
                Tick = tick;
                Command = command;
            }
            public UnitMoveHereEventArgs Args { get; }
            public UnitMoveFrame Parent { get; }
            public int MapEpoch { get; }
            public int Tick { get; }
            public MoveCommandScope Command { get; }
            public PlanScope Plan { get; set; }
            public PlanScope InheritedPlan { get; set; }
            public bool BuilderReached { get; set; }
            public bool NativeModeReached, RegionReached, RecoveryAttempted;
            public string RecoveryRejection;
            public short PrePortalRegion, FailedDestinationRegion, FailedPortalRegion;
            public bool RecoveryApplied;
            public PlacementUnit Placement;
        }

        internal sealed class PlanScope
        {
            public PlanScope(int unitId, int targetX, int targetY)
            {
                UnitId = unitId;
                TargetX = targetX;
                TargetY = targetY;
            }

            public int UnitId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int RouteStartX { get; set; } = -1;
            public int RouteStartY { get; set; } = -1;
            public bool ExactRouteEndpoints { get; set; }
            public bool NativeGroundPrecheck { get; set; }
            public bool VanillaFailureProven { get; set; }
            public bool PublishedUsesMoat { get; set; }
            public WeightedMoatEncodedRoute QualifiedTerminalRoute { get; set; }
            public QualifiedMovementRoute QualifiedRoute { get; set; }
            public WeightedMoatRouteSummary QualifiedTerminalSummary { get; set; }
            public int PlayerId { get; set; } = -1;
            public uint UnitGlobalId { get; set; }
            public bool IdentityBound { get; set; }
            public bool ModeObserved { get; set; }
            public bool VanillaModeDetected { get; set; }
            public bool FriendlyRouteQualified { get; set; }
            public bool OwnerRouteProbeCompleted { get; set; }
            public bool AttackMovementQualified { get; set; }
            public bool PostCombatRepath { get; set; }
            public bool MoatWorkMovement { get; set; }
            public MoatWorkSelectionScope MoatWorkSearch { get; set; }
            public int MoatWorkTargetTileId { get; set; }
        }

        internal struct RouteProbeSummary
        {
            public RouteProbeSummary(int playerId)
            {
                PlayerId = playerId;
                FriendlyMoatTiles = 0;
                EnemyMoatTiles = 0;
                InvalidMoatTiles = 0;
                ObservedOwnerMask = 0;
                StartRegion = 0;
                TargetRegion = 0;
                RouteFound = false;
                AttackProbeEvaluated = false;
                ReachedWithMoat = false;
                ReachedWithoutMoat = false;
                EnemyOnlyReachable = false;
                TraversedRegionCount = 0;
                ReachabilityCacheHits = 0;
                StructuralEdgesObserved = 0;
                RouteDistance = int.MaxValue;
                TargetedExpandedNodes = 0;
                TargetedSearchMilliseconds = 0;
            }

            public int PlayerId;
            public int FriendlyMoatTiles;
            public int EnemyMoatTiles;
            public int InvalidMoatTiles;
            public uint ObservedOwnerMask;
            public int StartRegion;
            public int TargetRegion;
            public bool RouteFound;
            public bool AttackProbeEvaluated;
            public bool ReachedWithMoat;
            public bool ReachedWithoutMoat;
            public bool EnemyOnlyReachable;
            public int TraversedRegionCount;
            public int ReachabilityCacheHits;
            public int StructuralEdgesObserved;
            public int RouteDistance;
            public int TargetedExpandedNodes;
            public double TargetedSearchMilliseconds;

            public void MergeObservations(RouteProbeSummary other)
            {
                PlayerId = other.PlayerId;
                FriendlyMoatTiles = Math.Max(FriendlyMoatTiles, other.FriendlyMoatTiles);
                EnemyMoatTiles = Math.Max(EnemyMoatTiles, other.EnemyMoatTiles);
                InvalidMoatTiles = Math.Max(InvalidMoatTiles, other.InvalidMoatTiles);
                ObservedOwnerMask |= other.ObservedOwnerMask;
                if (StartRegion == 0)
                    StartRegion = other.StartRegion;
                if (other.TargetRegion != 0)
                    TargetRegion = other.TargetRegion;
                RouteFound |= other.RouteFound;
                AttackProbeEvaluated |= other.AttackProbeEvaluated;
                ReachedWithMoat |= other.ReachedWithMoat;
                ReachedWithoutMoat |= other.ReachedWithoutMoat;
                EnemyOnlyReachable |= other.EnemyOnlyReachable;
                TraversedRegionCount = Math.Max(
                    TraversedRegionCount, other.TraversedRegionCount);
                ReachabilityCacheHits = Math.Max(
                    ReachabilityCacheHits, other.ReachabilityCacheHits);
                StructuralEdgesObserved = Math.Max(
                    StructuralEdgesObserved, other.StructuralEdgesObserved);
                if (other.RouteDistance != int.MaxValue &&
                    (RouteDistance == int.MaxValue || other.RouteDistance < RouteDistance))
                {
                    RouteDistance = other.RouteDistance;
                }
                TargetedExpandedNodes += other.TargetedExpandedNodes;
                TargetedSearchMilliseconds += other.TargetedSearchMilliseconds;
            }

            public void ObserveOwner(int ownerId)
            {
                if (ownerId >= 0 && ownerId < 32)
                    ObservedOwnerMask |= 1u << ownerId;
            }

            public string ToLogFields()
            {
                string attackFields = AttackProbeEvaluated
                    ? $" attackWithMoat={ReachedWithMoat} attackWithoutMoat={ReachedWithoutMoat} " +
                      $"attackTileGraphEvaluated=True"
                    : string.Empty;
                return $"route={RouteFound} friendlyTiles={FriendlyMoatTiles} " +
                    $"enemyTiles={EnemyMoatTiles} invalidTiles={InvalidMoatTiles} " +
                    $"ownerMask=0x{ObservedOwnerMask:X} regions={StartRegion}->{TargetRegion} " +
                    $"groundReachable={ReachedWithoutMoat} " +
                    $"friendlyReachable={ReachedWithMoat} " +
                    $"enemyOnlyReachable={EnemyOnlyReachable} " +
                    $"traversedRegions={TraversedRegionCount} " +
                    $"reachabilityCacheHits={ReachabilityCacheHits}" +
                    $" structuralEdges={StructuralEdgesObserved} " +
                    $"targetedExpanded={TargetedExpandedNodes} " +
                    $"targetedSearchMs={TargetedSearchMilliseconds:F3}" +
                    attackFields;
            }
        }

        internal struct CursorGroupRouteSummary
        {
            public string SelectionSignature;
            public int SelectedUnits;
            public int DiggerUnits;
            public int LegallyReachableUnits;
            public int FriendlyMoatSeparatedUnits;
            public int RepresentativeUnitId;
            public int RepresentativeStartX;
            public int RepresentativeStartY;
            public int RepresentativeStartTileId;
            public bool RepresentativeCanDig;
            public bool AllowFallback;
            public RouteProbeSummary ObservedRoute;
        }

    }
}
