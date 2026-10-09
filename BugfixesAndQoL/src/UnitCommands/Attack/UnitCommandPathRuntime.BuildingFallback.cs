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
        internal BuildingConsumerFallbackResult TryApplyBuildingConsumerFallback(
            AttackApproachDiagnosticScope scope,
            IntPtr tribeManager,
            BuildingApproachCandidate[] vanillaCandidates,
            AttackApproachState vanillaAfter)
        {
            if (scope == null || scope.OwnerCommand == null ||
                scope.OwnerCommand.MapEpoch != mapEpoch ||
                scope.OwnerCommand.TribeId != scope.TribeId ||
                !IsBuildingAttackCommand(scope.OwnerCommand.Command) ||
                tribeManager == IntPtr.Zero || tribeManager != nativeTribeManager ||
                nativePathManager == IntPtr.Zero)
            {
                return BuildingConsumerFallbackResult.Rejected("invalid-command-scope");
            }

            AttackCommandScope command = scope.OwnerCommand;
            if (!TryCaptureOrderedActiveGroupUnits(
                    tribeManager, scope.TribeId, out int[] groupUnitIds))
            {
                return BuildingConsumerFallbackResult.Rejected("invalid-command-group");
            }

            BuildingFallbackWorkBuffers work = RentBuildingFallbackWorkBuffers();
            try
            {
                List<int> diggerUnitIds = work.DiggerUnitIds;
                int playerId = -1;
                foreach (int unitId in groupUnitIds)
                {
                    if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                        unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                        unit->r_TribeId != scope.TribeId || !CanDigMoat(unit))
                    {
                        continue;
                    }

                    int candidatePlayerId = unit->r_ControllableForPlayerId;
                    if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(candidatePlayerId))
                        continue;
                    if (playerId < 0)
                        playerId = candidatePlayerId;
                    if (candidatePlayerId == playerId)
                        diggerUnitIds.Add(unitId);
                }
                if (diggerUnitIds.Count == 0 || playerId < 0)
                    return BuildingConsumerFallbackResult.Rejected("no-active-vanilla-digger");
                if (!TryValidateHostileBuildingTarget(
                        command.TargetValue1,
                        unchecked((uint)command.TargetValue2),
                        playerId,
                        out GameBuilding* building))
                {
                    return BuildingConsumerFallbackResult.Rejected("invalid-hostile-building");
                }

                BuildingConsumerPerformanceScope performance = activeBuildingConsumerPerformance;
                if (performance != null) performance.DiggerUnits = diggerUnitIds.Count;
                var retained = work.Retained;
                foreach (var candidate in CaptureBuildingApproachCandidates(nativePathManager))
                    if (IsLegalBuildingCandidate(command, building, playerId, candidate))
                        retained[candidate.ApproachTileId] = candidate;

                var candidates = work.Candidates;
                var targets = work.Targets;
                var pending = work.Pending;
                var reserved = work.Reserved;
                int rejected = 0;
                foreach (var original in vanillaCandidates)
                {
                    if (!IsLegalBuildingCandidate(command, building, playerId, original)) { rejected++; continue; }
                    var candidate = original;
                    var pos = GameTileManagerAPI.Instance.GetTileVectorFromId(candidate.ApproachTileId);
                    int cell = pos.Y * MapWidth + pos.X;
                    if (candidate.FootprintTileId != 0) reserved.Add(cell);
                    if (retained.TryGetValue(candidate.ApproachTileId, out var native) &&
                        native.FootprintTileId == candidate.FootprintTileId && native.Score > 0 && native.Score < VanillaUnreachableCandidateScore)
                        candidate.Score = native.Score;
                    else
                    { candidate.Score = VanillaUnreachableCandidateScore; pending.Add(candidates.Count); targets.Add(cell); }
                    candidates.Add(candidate);
                }
                if (performance != null) performance.ValidCandidates = candidates.Count;
                if (pending.Count == 0 && rejected == 0)
                    return BuildingConsumerFallbackResult.NotAttempted("vanilla-usable");

                int leaderId = *(short*)((byte*)tribeManager + scope.TribeId * TribeRecordSize + TribeLeadUnitIdOffset);
                var leaderStarts = work.LeaderStarts;
                var otherStarts = work.OtherStarts;
                foreach (int id in diggerUnitIds)
                {
                    if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null) continue;
                    // The native candidate consumer uses current coordinates, not MoveHere's next-step start.
                    int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
                    if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                    int tile = GameTileManagerAPI.Instance.GetTileId(x,y);
                    if (!IsValidTileId(tile) || (IsCompletedMoatTile(tile) &&
                        ResolveCompletedMoatRelationship(playerId,tile) != CompletedMoatRelationship.Friendly)) continue;
                    if (id == leaderId) leaderStarts.Add(y * MapWidth + x);
                    else otherStarts.Add(y * MapWidth + x);
                }
                if (pending.Count != 0)
                {
                    if (RequiredOnlyMode &&
                        !HasFastFriendlyMoatBridgeForCells(playerId, leaderStarts, targets) &&
                        !HasFastFriendlyMoatBridgeForCells(playerId, otherStarts, targets))
                    {
                        return BuildingConsumerFallbackResult.Rejected(
                            "no-friendly-moat-bridge");
                    }
                    MoatCandidateField field = buildingCandidateFields.Count != 0
                        ? buildingCandidateFields.Pop() : new MoatCandidateField(MapWidth, MapWidth);
                    MoatSearchEdge normal = (int from, int to, int d, out bool moat, out bool structure) =>
                        BuildingCandidateEdge(playerId,from,to,d,false,false,out moat,out structure);
                    MoatSearchEdge terminal = (int from, int to, int d, out bool moat, out bool structure) =>
                        BuildingCandidateEdge(playerId,from,to,d,true,reserved.Contains(to),out moat,out structure);
                    weightedMoatRoutePlanner.BeginReachabilityProbe();
                    try
                    {
                        long fieldStarted = Stopwatch.GetTimestamp();
                        int[] distances = ResolveMovementCandidates(field, leaderStarts, targets, normal, terminal, out int expanded);
                        if (RequiredOnlyMode) RecordFastFieldSearch(field, fieldStarted);
                        if (performance != null) { performance.ReachabilityMapsBuilt++; performance.SearchNodes += expanded; }
                        var remaining = work.Remaining; var remainingIndices = work.RemainingIndices;
                        for (int i=0;i<distances.Length;i++)
                        {
                            if (distances[i] >= 0)
                            { var c=candidates[pending[i]]; c.Score=distances[i]+1; candidates[pending[i]]=c; }
                            else { remaining.Add(targets[i]); remainingIndices.Add(pending[i]); }
                        }
                        if (remaining.Count != 0 && otherStarts.Count != 0)
                        {
                            int supplementBase=0;
                            foreach(var c in candidates) if(c.Score<VanillaUnreachableCandidateScore) supplementBase=Math.Max(supplementBase,c.Score);
                            fieldStarted = Stopwatch.GetTimestamp();
                            distances = ResolveMovementCandidates(field, otherStarts, remaining, normal, terminal, out expanded);
                            if (RequiredOnlyMode) RecordFastFieldSearch(field, fieldStarted);
                            if (performance != null) { performance.ReachabilityMapsBuilt++; performance.SearchNodes += expanded; }
                            for (int i=0;i<distances.Length;i++) if (distances[i]>=0)
                            { var c=candidates[remainingIndices[i]]; c.Score=supplementBase+distances[i]+1; candidates[remainingIndices[i]]=c; }
                        }
                    }
                    finally { weightedMoatRoutePlanner.EndReachabilityProbe(); buildingCandidateFields.Push(field); }
                }
                // 123090 only sorts the paired prefix; null-footprint staging entries keep producer order.
                int prefix=0;
                while (prefix<candidates.Count && candidates[prefix].FootprintTileId!=0) prefix++;
                for(int i=1;i<prefix;i++)
                {
                    var c=candidates[i]; int j=i;
                    while(j>0 && candidates[j-1].Score>c.Score) { candidates[j]=candidates[j-1]; j--; }
                    candidates[j]=c;
                }
                candidates.RemoveAll(c => c.Score >= VanillaUnreachableCandidateScore);
                var snapshot = CaptureBuildingApproachBuffer(nativePathManager);
                try { WriteBuildingApproachCandidates(nativePathManager,candidates); }
                catch { RestoreBuildingApproachBuffer(nativePathManager,snapshot); throw; }
                if (performance != null)
                    foreach(var c in candidates) { if(c.FootprintTileId==0) performance.ApproachOnly++; else performance.AttackPlaces++; }
                return BuildingConsumerFallbackResult.Applied(diggerUnitIds.Count,candidates.Count,0,0,0,0,rejected,default);
            }
            finally
            {
                ReturnBuildingFallbackWorkBuffers(work);
            }
        }

        internal BuildingFallbackWorkBuffers RentBuildingFallbackWorkBuffers()
        {
            lock (buildingFallbackWorkBuffers)
                return buildingFallbackWorkBuffers.Count != 0
                    ? buildingFallbackWorkBuffers.Pop() : new BuildingFallbackWorkBuffers();
        }

        internal void ReturnBuildingFallbackWorkBuffers(BuildingFallbackWorkBuffers work)
        {
            work.Clear();
            lock (buildingFallbackWorkBuffers)
                buildingFallbackWorkBuffers.Push(work);
        }

        internal sealed class BuildingFallbackWorkBuffers
        {
            public readonly List<int> DiggerUnitIds = new List<int>();
            public readonly Dictionary<int, BuildingApproachCandidate> Retained =
                new Dictionary<int, BuildingApproachCandidate>();
            public readonly List<BuildingApproachCandidate> Candidates = new List<BuildingApproachCandidate>();
            public readonly List<int> Targets = new List<int>();
            public readonly List<int> Pending = new List<int>();
            public readonly HashSet<int> Reserved = new HashSet<int>();
            public readonly List<int> LeaderStarts = new List<int>();
            public readonly List<int> OtherStarts = new List<int>();
            public readonly List<int> Remaining = new List<int>();
            public readonly List<int> RemainingIndices = new List<int>();

            public void Clear()
            {
                DiggerUnitIds.Clear();
                Retained.Clear();
                Candidates.Clear();
                Targets.Clear();
                Pending.Clear();
                Reserved.Clear();
                LeaderStarts.Clear();
                OtherStarts.Clear();
                Remaining.Clear();
                RemainingIndices.Clear();
            }
        }

        internal readonly struct BuildingConsumerFallbackResult
        {
            internal BuildingConsumerFallbackResult(
                bool wasApplied,
                string reason,
                int diggerUnits,
                int publishedCandidates,
                int walkableReservations,
                int missingContexts,
                int invalidContexts,
                int reservedBlocked,
                int ownerRouteRejected,
                RouteProbeSummary summary)
            {
                WasApplied = wasApplied;
                Reason = reason;
                DiggerUnits = diggerUnits;
                PublishedCandidates = publishedCandidates;
                WalkableReservations = walkableReservations;
                MissingContexts = missingContexts;
                InvalidContexts = invalidContexts;
                ReservedBlocked = reservedBlocked;
                OwnerRouteRejected = ownerRouteRejected;
                Summary = summary;
            }

            public bool WasApplied { get; }
            public string Reason { get; }
            public int DiggerUnits { get; }
            public int PublishedCandidates { get; }
            public int WalkableReservations { get; }
            public int MissingContexts { get; }
            public int InvalidContexts { get; }
            public int ReservedBlocked { get; }
            public int OwnerRouteRejected { get; }
            public RouteProbeSummary Summary { get; }

            public static BuildingConsumerFallbackResult NotAttempted(string reason) =>
                new BuildingConsumerFallbackResult(
                    false, reason, 0, 0, 0, 0, 0, 0, 0, default);

            public static BuildingConsumerFallbackResult Rejected(
                string reason,
                int diggerUnits = 0,
                int publishedCandidates = 0,
                int walkableReservations = 0,
                int missingContexts = 0,
                int invalidContexts = 0,
                int reservedBlocked = 0,
                int ownerRouteRejected = 0,
                RouteProbeSummary summary = default) =>
                new BuildingConsumerFallbackResult(
                    false,
                    reason,
                    diggerUnits,
                    publishedCandidates,
                    walkableReservations,
                    missingContexts,
                    invalidContexts,
                    reservedBlocked,
                    ownerRouteRejected,
                    summary);

            public static BuildingConsumerFallbackResult Applied(
                int diggerUnits,
                int publishedCandidates,
                int walkableReservations,
                int missingContexts,
                int invalidContexts,
                int reservedBlocked,
                int ownerRouteRejected,
                RouteProbeSummary summary) =>
                new BuildingConsumerFallbackResult(
                    true,
                    "owner-qualified-friendly-moat-candidates",
                    diggerUnits,
                    publishedCandidates,
                    walkableReservations,
                    missingContexts,
                    invalidContexts,
                    reservedBlocked,
                    ownerRouteRejected,
                    summary);
        }

    }
}
