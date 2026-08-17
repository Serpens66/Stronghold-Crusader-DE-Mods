using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ImprovedHunters
{
    /// <summary>
    /// Conservative wrapper around Vanilla's player-aware PCL connectivity
    /// query. Only a validated zero result is treated as unreachable.
    /// </summary>
    internal sealed unsafe class HunterPclReachability : IDisposable
    {
        private const int MaxFailureLogs = 20;
        private static readonly long CacheLifetime = Stopwatch.Frequency;
        private static readonly long ActiveTargetProbeInterval = Stopwatch.Frequency;
        private static readonly long ActiveTargetSnapshotLifetime = Stopwatch.Frequency * 2;
        private static readonly long CacheCleanupInterval = Stopwatch.Frequency * 10;

        private readonly ManualLogSource log;
        private readonly Func<bool> canRun;
        private readonly object cacheLock = new object();
        private readonly Dictionary<HunterPreyKey, CachedResult> cache =
            new Dictionary<HunterPreyKey, CachedResult>();
        private readonly Dictionary<HunterPreyKey, ActiveTargetSnapshot> activeTargetSnapshots =
            new Dictionary<HunterPreyKey, ActiveTargetSnapshot>();
        private bool available;
        private bool disposed;
        private int failureLogs;
        private long nativeQueries;
        private long cacheHits;
        private long reachableResults;
        private long unreachableResults;
        private long activeTargetNativeQueries;
        private long activeTargetPromotions;
        private long activeTargetSnapshotHits;
        private long activeTargetSnapshotMisses;
        private long nextCacheCleanupAt;

        public HunterPclReachability(
            ManualLogSource log,
            bool referenceHashMatches,
            Func<bool> canRun)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.canRun = canRun ?? throw new ArgumentNullException(nameof(canRun));

            if (!referenceHashMatches)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Improved Hunters native PCL reachability filter unavailable: " +
                    "the installed native DLL differs from the audited build; target selection remains unchanged.");
                return;
            }

            available = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters native PCL reachability filter initialized: " +
                "query=GamePlayerManagerAPI.GetNextReachablePCLToDestinationForPlayer, " +
                "mode=live-GameUnit+0x35C, selectionCacheSeconds=1, " +
                "activeTargetProbeSeconds=1, activeTargetSnapshotSeconds=2, zeroResultFilterOnly=True, " +
                "positiveResultLeavesVanillaAuthoritative=True.");
        }

        public bool IsAvailable => available && !disposed;

        public bool TryIsReachable(
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
                        $"Improved Hunters native PCL reachability input rejected: hunter={hunterUnitId}, " +
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
                    if (cache.TryGetValue(key, out CachedResult cached) &&
                        timestamp < cached.ExpiresAt &&
                        cached.Inputs.Equals(inputs))
                    {
                        cacheHits++;
                        reachable = cached.Reachable;
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
                lock (cacheLock)
                {
                    nativeQueries++;
                    if (reachable)
                        reachableResults++;
                    else
                        unreachableResults++;

                    cache[key] = new CachedResult(
                        inputs,
                        reachable,
                        timestamp,
                        timestamp + CacheLifetime);
                }

                return true;
            }
            catch (Exception exception)
            {
                LogFailure(
                    $"Improved Hunters native PCL reachability query failed: hunter={hunterUnitId}, " +
                    $"target={preyUnitId}/{preyGlobalId}/{preyType}, error={exception}.");
                return false;
            }
        }

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

        public string GetDiagnosticSummary()
        {
            lock (cacheLock)
            {
                return $"available={IsAvailable}, nativeQueries={nativeQueries}, cacheHits={cacheHits}, " +
                    $"reachable={reachableResults}, unreachable={unreachableResults}, cachedPairs={cache.Count}, " +
                    $"activeNativeQueries={activeTargetNativeQueries}, activePromotions={activeTargetPromotions}, " +
                    $"activeSnapshotHits={activeTargetSnapshotHits}, " +
                    $"activeSnapshotMisses={activeTargetSnapshotMisses}, activeSnapshots={activeTargetSnapshots.Count}";
            }
        }

        public void ResetForMap()
        {
            lock (cacheLock)
            {
                cache.Clear();
                activeTargetSnapshots.Clear();
                nativeQueries = 0;
                cacheHits = 0;
                reachableResults = 0;
                unreachableResults = 0;
                activeTargetNativeQueries = 0;
                activeTargetPromotions = 0;
                activeTargetSnapshotHits = 0;
                activeTargetSnapshotMisses = 0;
                nextCacheCleanupAt = 0;
            }

            failureLogs = 0;
        }

        private void PruneExpiredEntries(long timestamp)
        {
            lock (cacheLock)
            {
                if (timestamp < nextCacheCleanupAt)
                    return;

                nextCacheCleanupAt = timestamp + CacheCleanupInterval;
                List<HunterPreyKey> expired = null;
                foreach (KeyValuePair<HunterPreyKey, CachedResult> pair in cache)
                {
                    if (timestamp < pair.Value.ExpiresAt)
                        continue;

                    if (expired == null)
                        expired = new List<HunterPreyKey>();

                    expired.Add(pair.Key);
                }

                if (expired != null)
                {
                    for (int index = 0; index < expired.Count; index++)
                        cache.Remove(expired[index]);
                }

                expired = null;
                foreach (KeyValuePair<HunterPreyKey, ActiveTargetSnapshot> pair in activeTargetSnapshots)
                {
                    if (timestamp < pair.Value.UsableUntil)
                        continue;

                    if (expired == null)
                        expired = new List<HunterPreyKey>();

                    expired.Add(pair.Key);
                }

                if (expired != null)
                {
                    for (int index = 0; index < expired.Count; index++)
                        activeTargetSnapshots.Remove(expired[index]);
                }
            }
        }

        private static bool TryCaptureInputs(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            out ReachabilityInputs inputs,
            out string failure)
        {
            inputs = default;
            failure = string.Empty;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (hunterUnitId <= 0 ||
                !unitApi.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                hunter == null ||
                hunter->r_AliveState != AliveState.IsAlive ||
                hunter->r_CurrentHealth == 0 ||
                hunter->r_GlobalId == 0 ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
            {
                failure = "invalid-live-hunter";
                return false;
            }

            if (preyUnitId <= 0 ||
                !unitApi.TryGetUnitById(preyUnitId, out GameUnit* prey) ||
                prey == null ||
                prey->r_AliveState != AliveState.IsAlive ||
                prey->r_CurrentHealth == 0 ||
                prey->r_GlobalId != preyGlobalId ||
                prey->r_UnitChimp != preyType)
            {
                failure = "invalid-live-prey-identity";
                return false;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int sourceTileX = hunter->r_CurrentTilePositionX;
            int sourceTileY = hunter->r_CurrentTilePositionY;
            int targetTileX = prey->r_CurrentTilePositionX;
            int targetTileY = prey->r_CurrentTilePositionY;
            if (!tileApi.IsTileInsideMapBounds(sourceTileX, sourceTileY) ||
                !tileApi.IsTileInsideMapBounds(targetTileX, targetTileY))
            {
                failure = "tile-outside-map";
                return false;
            }

            int sourceTileId = tileApi.GetTileId(sourceTileX, sourceTileY);
            int targetTileId = tileApi.GetTileId(targetTileX, targetTileY);
            if (!tileApi.IsValidTileId(sourceTileId) || !tileApi.IsValidTileId(targetTileId))
            {
                failure = "invalid-tile-id";
                return false;
            }

            Span<ushort> pathConnections = tileApi.TileManager.PathConnectionGrid;
            if ((uint)sourceTileId >= (uint)pathConnections.Length ||
                (uint)targetTileId >= (uint)pathConnections.Length)
            {
                failure = "path-connection-grid-index-out-of-range";
                return false;
            }

            inputs = new ReachabilityInputs(
                hunter->r_GlobalId,
                preyUnitId,
                preyType,
                hunter->r_ControllableForPlayerId,
                hunter->N000001CA,
                pathConnections[sourceTileId],
                pathConnections[targetTileId]);
            return true;
        }

        private void LogFailure(string message)
        {
            if (failureLogs >= MaxFailureLogs)
                return;

            failureLogs++;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"{message} The candidate remains available to Vanilla " +
                $"({failureLogs}/{MaxFailureLogs}).");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            available = false;
            lock (cacheLock)
            {
                cache.Clear();
                activeTargetSnapshots.Clear();
            }
        }

        private readonly struct HunterPreyKey : IEquatable<HunterPreyKey>
        {
            private readonly int hunterUnitId;
            private readonly uint hunterGlobalId;
            private readonly uint preyGlobalId;

            public HunterPreyKey(int hunterUnitId, uint hunterGlobalId, uint preyGlobalId)
            {
                this.hunterUnitId = hunterUnitId;
                this.hunterGlobalId = hunterGlobalId;
                this.preyGlobalId = preyGlobalId;
            }

            public bool Equals(HunterPreyKey other)
            {
                return hunterUnitId == other.hunterUnitId &&
                    hunterGlobalId == other.hunterGlobalId &&
                    preyGlobalId == other.preyGlobalId;
            }

            public override bool Equals(object obj)
            {
                return obj is HunterPreyKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = hunterUnitId;
                    hash = (hash * 397) ^ hunterGlobalId.GetHashCode();
                    return (hash * 397) ^ preyGlobalId.GetHashCode();
                }
            }
        }

        private readonly struct ReachabilityInputs : IEquatable<ReachabilityInputs>
        {
            public readonly uint HunterGlobalId;
            public readonly int PreyUnitId;
            public readonly eChimps PreyType;
            public readonly int PlayerId;
            public readonly int Mode;
            public readonly int SourcePcl;
            public readonly int TargetPcl;

            public ReachabilityInputs(
                uint hunterGlobalId,
                int preyUnitId,
                eChimps preyType,
                int playerId,
                int mode,
                int sourcePcl,
                int targetPcl)
            {
                HunterGlobalId = hunterGlobalId;
                PreyUnitId = preyUnitId;
                PreyType = preyType;
                PlayerId = playerId;
                Mode = mode;
                SourcePcl = sourcePcl;
                TargetPcl = targetPcl;
            }

            public bool Equals(ReachabilityInputs other)
            {
                return HunterGlobalId == other.HunterGlobalId &&
                    PreyUnitId == other.PreyUnitId &&
                    PreyType == other.PreyType &&
                    PlayerId == other.PlayerId &&
                    Mode == other.Mode &&
                    SourcePcl == other.SourcePcl &&
                    TargetPcl == other.TargetPcl;
            }

            public override bool Equals(object obj)
            {
                return obj is ReachabilityInputs other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = HunterGlobalId.GetHashCode();
                    hash = (hash * 397) ^ PreyUnitId;
                    hash = (hash * 397) ^ (int)PreyType;
                    hash = (hash * 397) ^ PlayerId;
                    hash = (hash * 397) ^ Mode;
                    hash = (hash * 397) ^ SourcePcl;
                    return (hash * 397) ^ TargetPcl;
                }
            }
        }

        private readonly struct CachedResult
        {
            public readonly ReachabilityInputs Inputs;
            public readonly bool Reachable;
            public readonly long ObservedAt;
            public readonly long ExpiresAt;

            public CachedResult(
                ReachabilityInputs inputs,
                bool reachable,
                long observedAt,
                long expiresAt)
            {
                Inputs = inputs;
                Reachable = reachable;
                ObservedAt = observedAt;
                ExpiresAt = expiresAt;
            }
        }

        private readonly struct ActiveTargetSnapshot
        {
            public readonly ReachabilityInputs Inputs;
            public readonly bool Reachable;
            public readonly long ObservedAt;
            public readonly long NextProbeAt;
            public readonly long UsableUntil;

            public ActiveTargetSnapshot(
                ReachabilityInputs inputs,
                bool reachable,
                long observedAt,
                long nextProbeAt,
                long usableUntil)
            {
                Inputs = inputs;
                Reachable = reachable;
                ObservedAt = observedAt;
                NextProbeAt = nextProbeAt;
                UsableUntil = usableUntil;
            }
        }
    }
}
