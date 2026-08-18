using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ImprovedHunters
{
    internal sealed unsafe partial class HunterPclReachability
    {
        // Active-target refresh and snapshots belong to pursuit/pathfinding;
        // the initial candidate filter remains in HunterPclReachability.cs.
        public bool TryRefreshActiveTargetReachability(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            long timestamp,
            out bool reachable)
        {
            reachable = true;
            if (!IsAvailable)
                return false;

            try
            {
                if (!canRun())
                    return false;

                if (!TryCaptureInputs(
                        hunterUnitId,
                        preyUnitId,
                        preyGlobalId,
                        preyType,
                        out ReachabilityInputs inputs,
                        out string failure))
                {
                    LogFailure(
                        $"Improved Hunters active-target PCL input rejected: hunter={hunterUnitId}, " +
                        $"target={preyUnitId}/{preyGlobalId}/{preyType}, reason={failure}.");
                    return false;
                }

                HunterPreyKey key = new HunterPreyKey(
                    hunterUnitId,
                    inputs.HunterGlobalId,
                    preyGlobalId);
                PruneExpiredEntries(timestamp);
                lock (cacheLock)
                {
                    if (activeTargetSnapshots.TryGetValue(key, out ActiveTargetSnapshot snapshot) &&
                        snapshot.Inputs.Equals(inputs) &&
                        timestamp < snapshot.NextProbeAt)
                    {
                        reachable = snapshot.Reachable;
                        return true;
                    }
                }

                int result = GamePlayerManagerAPI.Instance
                    .GetNextReachablePCLToDestinationForPlayer(
                        inputs.PlayerId,
                        inputs.TargetPcl,
                        inputs.SourcePcl,
                        inputs.Mode);
                reachable = result != 0;
                bool logChangedObservation;
                lock (cacheLock)
                {
                    logChangedObservation =
                        !activeTargetSnapshots.TryGetValue(key, out ActiveTargetSnapshot previous) ||
                        !previous.Inputs.Equals(inputs) ||
                        previous.Reachable != reachable;

                    nativeQueries++;
                    activeTargetNativeQueries++;
                    if (reachable)
                        reachableResults++;
                    else
                        unreachableResults++;

                    // Target ranking may reuse the result, while the active
                    // snapshot retains independent refresh/read lifetimes.
                    cache[key] = new CachedResult(
                        inputs,
                        reachable,
                        timestamp,
                        timestamp + CacheLifetime);
                    activeTargetSnapshots[key] = new ActiveTargetSnapshot(
                        inputs,
                        reachable,
                        timestamp,
                        timestamp + ActiveTargetProbeInterval,
                        timestamp + ActiveTargetSnapshotLifetime);
                }

                if (logChangedObservation)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        "Improved Hunters active-target PCL snapshot refreshed: " +
                        $"hunter={hunterUnitId}/{inputs.HunterGlobalId}, " +
                        $"target={preyUnitId}/{preyGlobalId}/{preyType}, " +
                        $"player={inputs.PlayerId}, mode={inputs.Mode}, " +
                        $"sourcePcl={inputs.SourcePcl}, targetPcl={inputs.TargetPcl}, " +
                        $"resultRaw={result}, reachable={reachable}, " +
                        "nextProbeMs=1000, readableMs=2000.");
                }

                return true;
            }
            catch (Exception exception)
            {
                LogFailure(
                    $"Improved Hunters active-target native PCL query failed: hunter={hunterUnitId}, " +
                    $"target={preyUnitId}/{preyGlobalId}/{preyType}, error={exception}.");
                return false;
            }
        }

        public bool TryPromoteSelectionResultToActiveTarget(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            long timestamp)
        {
            if (!IsAvailable || !canRun())
                return false;

            if (!TryCaptureInputs(
                    hunterUnitId,
                    preyUnitId,
                    preyGlobalId,
                    preyType,
                    out ReachabilityInputs inputs,
                    out _))
            {
                return false;
            }

            HunterPreyKey key = new HunterPreyKey(
                hunterUnitId,
                inputs.HunterGlobalId,
                preyGlobalId);
            bool logPromotion;
            long observedAt;
            lock (cacheLock)
            {
                if (!cache.TryGetValue(key, out CachedResult cached) ||
                    !cached.Inputs.Equals(inputs) ||
                    !cached.Reachable ||
                    timestamp >= cached.ExpiresAt)
                {
                    return false;
                }

                observedAt = cached.ObservedAt;
                logPromotion =
                    !activeTargetSnapshots.TryGetValue(key, out ActiveTargetSnapshot previous) ||
                    !previous.Inputs.Equals(inputs) ||
                    !previous.Reachable;
                activeTargetSnapshots[key] = new ActiveTargetSnapshot(
                    inputs,
                    true,
                    cached.ObservedAt,
                    cached.ObservedAt + ActiveTargetProbeInterval,
                    cached.ObservedAt + ActiveTargetSnapshotLifetime);
                activeTargetPromotions++;
            }

            if (logPromotion)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Improved Hunters promoted positive selection PCL result to active-target snapshot: " +
                    $"hunter={hunterUnitId}/{inputs.HunterGlobalId}, " +
                    $"target={preyUnitId}/{preyGlobalId}/{preyType}, " +
                    $"player={inputs.PlayerId}, mode={inputs.Mode}, " +
                    $"sourcePcl={inputs.SourcePcl}, targetPcl={inputs.TargetPcl}, " +
                    $"selectionAgeMs={Math.Max(0, timestamp - observedAt) * 1000 / Stopwatch.Frequency}.");
            }
            return true;
        }

        public bool TryGetActiveTargetReachability(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            long timestamp,
            out bool reachable,
            out long ageMilliseconds,
            out string status)
        {
            reachable = true;
            ageMilliseconds = -1;
            status = "unavailable";
            if (!IsAvailable || !canRun())
                return false;

            // The inline Hunter hook only consumes this independently refreshed
            // snapshot and never enters the native PCL helper recursively.
            if (!TryCaptureInputs(
                    hunterUnitId,
                    preyUnitId,
                    preyGlobalId,
                    preyType,
                    out ReachabilityInputs inputs,
                    out string failure))
            {
                status = $"input-{failure}";
                lock (cacheLock)
                    activeTargetSnapshotMisses++;
                return false;
            }

            HunterPreyKey key = new HunterPreyKey(
                hunterUnitId,
                inputs.HunterGlobalId,
                preyGlobalId);
            lock (cacheLock)
            {
                if (!activeTargetSnapshots.TryGetValue(key, out ActiveTargetSnapshot snapshot))
                {
                    status = "missing";
                    activeTargetSnapshotMisses++;
                    return false;
                }

                if (!snapshot.Inputs.Equals(inputs))
                {
                    status = "inputs-changed";
                    activeTargetSnapshotMisses++;
                    return false;
                }

                long ageTicks = Math.Max(0, timestamp - snapshot.ObservedAt);
                ageMilliseconds = ageTicks * 1000 / Stopwatch.Frequency;
                if (timestamp >= snapshot.UsableUntil)
                {
                    status = "expired";
                    activeTargetSnapshotMisses++;
                    return false;
                }

                activeTargetSnapshotHits++;
                reachable = snapshot.Reachable;
                status = "hit";
                return true;
            }
        }

    }
}
