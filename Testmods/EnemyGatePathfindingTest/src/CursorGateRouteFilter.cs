using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace EnemyGatePathfindingTest
{
    internal readonly struct CursorCoverageSnapshot
    {
        internal CursorCoverageSnapshot(long checkedRoutes, long reachable, long targetBlocked,
            long policyBlocked, long vanillaNoRoute, long forcedDetour, long blockedEncounters,
            long failures, long errors)
        {
            CheckedRoutes = checkedRoutes; Reachable = reachable; TargetBlocked = targetBlocked;
            PolicyBlocked = policyBlocked; VanillaNoRoute = vanillaNoRoute;
            ForcedDetour = forcedDetour;
            BlockedEncounters = blockedEncounters; Failures = failures; Errors = errors;
        }

        internal long CheckedRoutes { get; }
        internal long Reachable { get; }
        internal long TargetBlocked { get; }
        internal long PolicyBlocked { get; }
        internal long VanillaNoRoute { get; }
        internal long ForcedDetour { get; }
        internal long BlockedEncounters { get; }
        internal long Failures { get; }
        internal long Errors { get; }
    }

    internal readonly struct RouteBlockedTile
    {
        internal RouteBlockedTile(int tileId, int x, int y)
        { TileId = tileId; X = x; Y = y; }
        internal int TileId { get; }
        internal int X { get; }
        internal int Y { get; }
    }

    internal sealed class RouteTilePolicySnapshot
    {
        internal static readonly RouteTilePolicySnapshot Empty = new RouteTilePolicySnapshot(
            new ulong[9][], new ulong[9][], Array.Empty<int>(),
            new Dictionary<int, RouteTileIdentity>(), new bool[9], 0,
            directionMasks: new byte[9][]);

        internal RouteTilePolicySnapshot(
            ulong[][] hostileGateBits,
            ulong[][] hostileBridgeBits,
            int[] rowStarts,
            Dictionary<int, RouteTileIdentity> identities,
            bool[] hasBlockedTiles,
            ulong topologyFingerprint,
            RouteBlockedTile[][] blockedTiles = null,
            byte[][] directionMasks = null,
            int maskedDirectedEdges = 0,
            int ambiguousPassages = 0,
            string directionMaskDiagnostics = null)
        {
            HostileGateBits = hostileGateBits ?? new ulong[9][];
            HostileBridgeBits = hostileBridgeBits ?? new ulong[9][];
            RowStarts = rowStarts ?? Array.Empty<int>();
            Identities = identities ?? new Dictionary<int, RouteTileIdentity>();
            HasBlockedTiles = hasBlockedTiles ?? new bool[9];
            TopologyFingerprint = topologyFingerprint;
            BlockedTiles = blockedTiles ?? new RouteBlockedTile[9][];
            DirectionMasks = directionMasks ?? new byte[9][];
            int nonEmptyMasks = 0;
            for (int player = 1; player < DirectionMasks.Length; player++)
                if (DirectionMasks[player] != null) nonEmptyMasks++;
            NonEmptyPlayerMaskCount = nonEmptyMasks;
            MaskedDirectedEdges = maskedDirectedEdges;
            AmbiguousPassages = ambiguousPassages;
            DirectionMaskDiagnostics = directionMaskDiagnostics ?? "none";
        }

        internal ulong[][] HostileGateBits { get; }
        internal ulong[][] HostileBridgeBits { get; }
        internal int[] RowStarts { get; }
        internal Dictionary<int, RouteTileIdentity> Identities { get; }
        internal bool[] HasBlockedTiles { get; }
        internal RouteBlockedTile[][] BlockedTiles { get; }
        // Each byte contains the eight Vanilla direction bits that remain legal
        // when leaving this tile. A null player entry is the all-0xFF fast path.
        internal byte[][] DirectionMasks { get; }
        internal int NonEmptyPlayerMaskCount { get; }
        internal int MaskedDirectedEdges { get; }
        internal int AmbiguousPassages { get; }
        internal string DirectionMaskDiagnostics { get; }
        internal ulong TopologyFingerprint { get; }
        internal bool IsGateBlocked(int playerId, int tileId) =>
            IsSet(HostileGateBits, playerId, tileId);
        internal bool IsBridgeBlocked(int playerId, int tileId) =>
            IsSet(HostileBridgeBits, playerId, tileId);
        internal bool IsBlocked(int playerId, int tileId) =>
            IsGateBlocked(playerId, tileId) || IsBridgeBlocked(playerId, tileId);
        internal bool IsDirectionAllowed(int playerId, int tileId, int direction)
        {
            if (direction < 0 || direction > 7 || playerId <= 0 ||
                playerId >= DirectionMasks.Length || tileId < 0)
                return true;
            byte[] masks = DirectionMasks[playerId];
            return masks == null || tileId >= masks.Length ||
                (masks[tileId] & (1 << direction)) != 0;
        }
        internal bool TryGetIdentity(int tileId, out RouteTileIdentity identity) =>
            Identities.TryGetValue(tileId, out identity);

        private static bool IsSet(ulong[][] byPlayer, int playerId, int tileId)
        {
            if (playerId <= 0 || playerId >= byPlayer.Length || tileId < 0)
                return false;
            ulong[] bits = byPlayer[playerId];
            int word = tileId >> 6;
            return bits != null && word < bits.Length &&
                (bits[word] & (1UL << (tileId & 63))) != 0;
        }
    }

    internal readonly struct RouteTileIdentity
    {
        internal RouteTileIdentity(int gateId, int bridgeId)
        { GateId = gateId; BridgeId = bridgeId; }
        internal int GateId { get; }
        internal int BridgeId { get; }
        internal RouteTileIdentity Merge(int gateId, int bridgeId) => new RouteTileIdentity(
            GateId != 0 ? GateId : gateId,
            BridgeId != 0 ? BridgeId : bridgeId);
    }

    internal sealed unsafe class CursorGateRouteFilter
    {
        private static readonly long UnitRefreshInterval = Math.Max(1, Stopwatch.Frequency / 4);
        private static readonly long CursorCacheInterval = Math.Max(1, Stopwatch.Frequency / 20);
        private static readonly long CursorDiagnosticInterval = Math.Max(1, Stopwatch.Frequency / 10);
        private const int MaximumDiagnosticVisitedNodes = 160000;
        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private readonly ManualLogSource log;
        private readonly int* cursorX;
        private readonly int* cursorY;
        private readonly byte* directionGrid;
        private readonly int[] bfsVisited =
            new int[EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive];
        private readonly int[] bfsQueue =
            new int[EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive];
        private volatile RouteTilePolicySnapshot policy = RouteTilePolicySnapshot.Empty;
        private volatile UnitSnapshot units = UnitSnapshot.Empty;
        private Action epochStarter;
        private HookTransaction cursorTransaction;
        private readonly HookHandle<X64InlineHook> cursorHook = new HookHandle<X64InlineHook>();

        private int bfsGate;
        private int bfsGeneration;
        private int epochActive;
        private int epochRequested;
        private int epochNumber;
        private long nextUnitRefreshAt;
        private long cursorCacheUntil;
        private long nextCursorSearchAt;
        private ulong cursorCacheFingerprint;
        private int cursorCacheUnit;
        private int cursorCachePlayer;
        private int cursorCacheStartX;
        private int cursorCacheStartY;
        private int cursorCacheTargetX;
        private int cursorCacheTargetY;
        private int cursorCacheResult;
        private long cursorPositiveSeen;
        private long cursorChecked;
        private long cursorCacheHits;
        private long cursorAllowedDetour;
        private long cursorBlocked;
        private long cursorEpochInactive;
        private long cursorMissingUnit;
        private long cursorInvalidPolicyOrPlayer;
        private long cursorMissingPointer;
        private long cursorThrottled;
        private long bfsFreshRuns;
        private long bfsReachable;
        private long bfsForcedDetour;
        private long bfsTargetBlocked;
        private long bfsPolicyBlocked;
        private long bfsVanillaNoRoute;
        private long bfsInvalidCoordinates;
        private long bfsBusy;
        private long bfsQueueOverflow;
        private long bfsInvalidGrid;
        private long bfsBudgetExceeded;
        private long bfsVisitedTotal;
        private long bfsVisitedMaximum;
        private long bfsBlockedEncounterTotal;
        private long bfsBlockedEncounterMaximum;
        private long bfsElapsedTicksTotal;
        private long bfsElapsedTicksMaximum;
        private readonly long[] cursorPlayers = new long[9];
        private readonly CursorSample[] cursorSamples =
            new CursorSample[Enum.GetValues(typeof(CursorSearchOutcome)).Length];
        private long callbackErrors;

        internal CursorGateRouteFilter(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ScanRegion region,
            ulong libraryBase,
            int* cursorX,
            int* cursorY,
            bool installNativeHooks)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.cursorX = cursorX;
            this.cursorY = cursorY;
            directionGrid = (byte*)(libraryBase +
                unchecked((ulong)EnemyGatePathfindingNativeDefinition.PathDirectionGridRva));
            if (!installNativeHooks)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "Cursor tile-policy hook was not installed because BugfixesAndQoL_Serp owns " +
                    "overlapping cursor code. The snapshot Different-PCL filter remains active.");
                return;
            }

            Shared.NativeResolution cursor = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.CursorPclDecisionPattern,
                EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva -
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionOffsetInPattern,
                referenceHashMatches: true,
                "ordinary movement cursor PCL decision",
                log);
            int cursorRva = cursor.Rva +
                EnemyGatePathfindingNativeDefinition.CursorPclDecisionOffsetInPattern;
            if (cursorRva != EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva)
                throw new InvalidOperationException("cursor PCL decision resolved outside its audited RVA");

            using (var probe = new X64InlineHook(
                libraryBase + unchecked((ulong)cursorRva),
                EnemyGatePathfindingNativeDefinition.CursorPclDecisionHookLength))
            {
                if (probe.DisplacedByteCount !=
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionHookLength)
                {
                    throw new InvalidOperationException(
                        "Unexpected RedBird cursor PCL decision span before installation.");
                }
            }

            // RedBird at this exact site:
            // AfterCallback executes the managed callback before relocating TEST/LEA/MOV.
            // This audited integer-only span has no live XMM/SIMD state.
            cursorTransaction = new HookTransaction(
                region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    OwnsHooks = false
                });
            cursorTransaction.AddContextHook(
                cursorHook,
                HookTarget.FromAddress(libraryBase + unchecked((ulong)cursorRva)),
                FilterPositiveCursorPcl,
                new ContextHookOptions
                {
                    Registers = X64SmartCPUContextRegs.All,
                    HookSize = EnemyGatePathfindingNativeDefinition.CursorPclDecisionHookLength,
                    ErrorMode = CallbackErrorMode.LogAndContinue,
                    Placement = OverwrittenInstructionPlacement.AfterCallback
                });
            CommitResult commitResult = cursorTransaction.Commit();
            if (!commitResult.IsCompleteSuccess || !cursorHook.Success)
                throw new InvalidOperationException(
                    $"read-only cursor PCL decision hook was not installed: {commitResult}");
            if (cursorHook.Hook.DisplacedByteCount !=
                EnemyGatePathfindingNativeDefinition.CursorPclDecisionHookLength)
            {
                cursorTransaction.DisableAll();
                throw new InvalidOperationException(
                    "RedBird committed an unexpected cursor PCL decision span; the hook was rolled back.");
            }

            Shared.DebugLogHelper.LogInfo(log,
                "Crash-safe cursor route hook installed: " +
                $"cursorPclDecision=0x{cursorRva:X} ({cursor.Method}+0x" +
                $"{EnemyGatePathfindingNativeDefinition.CursorPclDecisionOffsetInPattern:X}), " +
                $"displaced={cursorHook.Hook.DisplacedByteCount}, " +
                $"readOnlyDirectionGrid=0x{EnemyGatePathfindingNativeDefinition.PathDirectionGridRva:X}. " +
                "No builder/planner detour and no Direction-Grid writer exists in this build.");
        }

        internal bool HooksInstalled =>
            cursorTransaction != null && cursorHook.Success;

        internal void SetTopologyEpochStarter(Action starter) => epochStarter = starter;

        internal void UpdatePolicy(RouteTilePolicySnapshot updated) =>
            policy = updated ?? RouteTilePolicySnapshot.Empty;

        internal void BeginEpoch(string reason)
        {
            if (Interlocked.CompareExchange(ref epochActive, 1, 0) != 0)
                return;
            Volatile.Write(ref epochRequested, 0);
            epochStarter?.Invoke();
            Interlocked.Increment(ref epochNumber);
            ResetCounters();
        }

        internal void EndEpoch(string reason)
        {
            if (Interlocked.CompareExchange(ref epochActive, 0, 1) != 1)
                return;
            policy = RouteTilePolicySnapshot.Empty;
            units = UnitSnapshot.Empty;
        }

        internal CursorCoverageSnapshot GetCoverageSnapshot()
        {
            long failures = Read(ref cursorEpochInactive) + Read(ref cursorMissingUnit) +
                Read(ref cursorInvalidPolicyOrPlayer) + Read(ref cursorMissingPointer) +
                Read(ref bfsInvalidCoordinates) + Read(ref bfsBusy) +
                Read(ref bfsQueueOverflow) + Read(ref bfsInvalidGrid);
            return new CursorCoverageSnapshot(
                Read(ref cursorChecked), Read(ref bfsReachable), Read(ref bfsTargetBlocked),
                Read(ref bfsPolicyBlocked), Read(ref bfsVanillaNoRoute),
                Read(ref bfsForcedDetour),
                Read(ref bfsBlockedEncounterTotal), failures, Read(ref callbackErrors));
        }

        internal void ProcessDeferred()
        {
            if (!HooksInstalled)
                return;
            try
            {
                if (Volatile.Read(ref epochActive) == 0 &&
                    Interlocked.Exchange(ref epochRequested, 0) != 0)
                    BeginEpoch("first cursor query; supports map editor");

                long now = Stopwatch.GetTimestamp();
                RefreshUnits(now);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref callbackErrors);
                Shared.DebugLogHelper.LogWarning(log,
                    "Deferred cursor diagnostics failed without changing native behavior: " +
                    $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private void FilterPositiveCursorPcl(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                if (registers == null || unchecked((uint)registers->RAX) == 0)
                    return;
                Interlocked.Increment(ref cursorPositiveSeen);
                if (Volatile.Read(ref epochActive) == 0)
                {
                    Volatile.Write(ref epochRequested, 1);
                    Interlocked.Increment(ref cursorEpochInactive);
                    CaptureFailureSample(CursorSearchOutcome.EpochInactive,
                        unchecked((int)(uint)registers->R14), 0, 0, 0, 0, 0,
                        policy.TopologyFingerprint);
                    return;
                }

                int unitId = unchecked((int)(uint)registers->R14);
                UnitSnapshot currentUnits = units;
                RouteTilePolicySnapshot current = policy;
                if (!currentUnits.TryGet(unitId, out int player, out int startX, out int startY))
                {
                    Interlocked.Increment(ref cursorMissingUnit);
                    CaptureFailureSample(CursorSearchOutcome.MissingUnit,
                        unitId, 0, 0, 0, 0, 0, current.TopologyFingerprint);
                    return;
                }
                if (!CanApply(current, player))
                {
                    Interlocked.Increment(ref cursorInvalidPolicyOrPlayer);
                    CaptureFailureSample(CursorSearchOutcome.InvalidPolicyOrPlayer,
                        unitId, player, startX, startY, 0, 0,
                        current.TopologyFingerprint);
                    return;
                }
                if (cursorX == null || cursorY == null)
                {
                    Interlocked.Increment(ref cursorMissingPointer);
                    CaptureFailureSample(CursorSearchOutcome.MissingPointer,
                        unitId, player, startX, startY, 0, 0,
                        current.TopologyFingerprint);
                    return;
                }
                if (player > 0 && player < cursorPlayers.Length)
                    Interlocked.Increment(ref cursorPlayers[player]);

                int targetX = *cursorX;
                int targetY = *cursorY;
                Interlocked.Increment(ref cursorChecked);
                long now = Stopwatch.GetTimestamp();
                CursorSearchOutcome outcome;
                if (now <= Volatile.Read(ref cursorCacheUntil) &&
                    cursorCacheFingerprint == current.TopologyFingerprint &&
                    cursorCacheUnit == unitId && cursorCachePlayer == player &&
                    cursorCacheStartX == startX && cursorCacheStartY == startY &&
                    cursorCacheTargetX == targetX && cursorCacheTargetY == targetY)
                {
                    outcome = (CursorSearchOutcome)cursorCacheResult;
                    Interlocked.Increment(ref cursorCacheHits);
                }
                else
                {
                    if (now < Volatile.Read(ref nextCursorSearchAt))
                    {
                        Interlocked.Increment(ref cursorThrottled);
                        Interlocked.Increment(ref cursorAllowedDetour);
                        return;
                    }
                    Volatile.Write(ref nextCursorSearchAt, now + CursorDiagnosticInterval);
                    long started = Stopwatch.GetTimestamp();
                    CursorSearchResult result = SearchCausally(
                        current, player, startX, startY, targetX, targetY);
                    long elapsed = Stopwatch.GetTimestamp() - started;
                    outcome = result.Outcome;
                    RecordFreshSearch(result, elapsed, unitId, player,
                        startX, startY, targetX, targetY, current);
                    cursorCacheFingerprint = current.TopologyFingerprint;
                    cursorCacheUnit = unitId;
                    cursorCachePlayer = player;
                    cursorCacheStartX = startX;
                    cursorCacheStartY = startY;
                    cursorCacheTargetX = targetX;
                    cursorCacheTargetY = targetY;
                    cursorCacheResult = (int)outcome;
                    Volatile.Write(ref cursorCacheUntil, now + CursorCacheInterval);
                }

                if (outcome != CursorSearchOutcome.Reachable &&
                    outcome != CursorSearchOutcome.ForcedDetour &&
                    outcome != CursorSearchOutcome.VanillaNoRoute &&
                    outcome != CursorSearchOutcome.TargetBlocked &&
                    outcome != CursorSearchOutcome.PolicyBlocked)
                {
                    return;
                }
                if (outcome == CursorSearchOutcome.Reachable ||
                    outcome == CursorSearchOutcome.ForcedDetour ||
                    outcome == CursorSearchOutcome.VanillaNoRoute)
                {
                    Interlocked.Increment(ref cursorAllowedDetour);
                    return;
                }

                // Vanilla's relocated TEST consumes this value after the callback.
                registers->RAX = 0;
                Interlocked.Increment(ref cursorBlocked);
            }
            catch
            {
                Interlocked.Increment(ref callbackErrors);
                CaptureFailureSample(CursorSearchOutcome.Exception,
                    0, 0, 0, 0, 0, 0, policy.TopologyFingerprint);
            }
        }

        private CursorSearchResult SearchCausally(
            RouteTilePolicySnapshot current,
            int player,
            int startX,
            int startY,
            int targetX,
            int targetY)
        {
            if (Interlocked.CompareExchange(ref bfsGate, 1, 0) != 0)
                return new CursorSearchResult(CursorSearchOutcome.Busy, 0, 0, -1);
            try
            {
                CursorSearchResult unrestricted = SearchCore(
                    current, player, startX, startY, targetX, targetY, false);
                if (unrestricted.Outcome != CursorSearchOutcome.Reachable)
                    return unrestricted.Outcome == CursorSearchOutcome.InvalidCoordinates ||
                        unrestricted.Outcome == CursorSearchOutcome.QueueOverflow ||
                        unrestricted.Outcome == CursorSearchOutcome.BudgetExceeded ||
                        unrestricted.Outcome == CursorSearchOutcome.InvalidGrid
                        ? unrestricted
                        : new CursorSearchResult(CursorSearchOutcome.VanillaNoRoute,
                            unrestricted.Visited, 0, -1, unrestricted.Distance);
                CursorSearchResult filtered = SearchCore(
                    current, player, startX, startY, targetX, targetY, true);
                if (filtered.Outcome == CursorSearchOutcome.InvalidCoordinates ||
                    filtered.Outcome == CursorSearchOutcome.QueueOverflow ||
                    filtered.Outcome == CursorSearchOutcome.BudgetExceeded ||
                    filtered.Outcome == CursorSearchOutcome.InvalidGrid)
                    return filtered;
                CausalRouteDecision decision = EnemyGatePathfindingPolicy.ClassifyCausalRoute(
                    true, unrestricted.Distance,
                    filtered.Outcome == CursorSearchOutcome.Reachable, filtered.Distance,
                    filtered.Outcome == CursorSearchOutcome.TargetBlocked,
                    filtered.BlockedEncounters);
                CursorSearchOutcome outcome = decision == CausalRouteDecision.ForcedDetour
                    ? CursorSearchOutcome.ForcedDetour
                    : decision == CausalRouteDecision.TargetBlocked
                        ? CursorSearchOutcome.TargetBlocked
                        : decision == CausalRouteDecision.PolicyBlocked
                            ? CursorSearchOutcome.PolicyBlocked
                            : decision == CausalRouteDecision.VanillaNoRoute
                                ? CursorSearchOutcome.VanillaNoRoute
                                : CursorSearchOutcome.Reachable;
                return new CursorSearchResult(outcome,
                    unrestricted.Visited + filtered.Visited,
                    filtered.BlockedEncounters, filtered.FirstBlockedTile,
                    filtered.Distance);
            }
            finally
            {
                Volatile.Write(ref bfsGate, 0);
            }
        }

        private CursorSearchResult SearchCore(
            RouteTilePolicySnapshot current,
            int player,
            int startX,
            int startY,
            int targetX,
            int targetY,
            bool applyPolicy)
        {
                int start = GetTileId(current.RowStarts, startX, startY);
                int target = GetTileId(current.RowStarts, targetX, targetY);
                if (start < 0 || target < 0)
                    return new CursorSearchResult(
                        CursorSearchOutcome.InvalidCoordinates, 0, 0, -1);
                if (start == target)
                    return new CursorSearchResult(CursorSearchOutcome.Reachable, 1, 0, -1, 0);

                int generation = unchecked(++bfsGeneration);
                if (generation == 0)
                {
                    Array.Clear(bfsVisited, 0, bfsVisited.Length);
                    generation = ++bfsGeneration;
                }
                int read = 0;
                int write = 0;
                int blockedEncounters = 0;
                int firstBlockedTile = -1;
                int distance = 0;
                bfsVisited[start] = generation;
                bfsQueue[write++] = Pack(startX, startY);
                int levelEnd = write;
                while (read < write)
                {
                    if (read >= MaximumDiagnosticVisitedNodes)
                        return new CursorSearchResult(
                            CursorSearchOutcome.BudgetExceeded, read, blockedEncounters,
                            firstBlockedTile);
                    if (read == levelEnd)
                    {
                        levelEnd = write;
                        distance++;
                    }
                    int packed = bfsQueue[read++];
                    int x = packed & 0x3FF;
                    int y = packed >> 10;
                    int tile = GetTileId(current.RowStarts, x, y);
                    if (tile < 0)
                        return new CursorSearchResult(
                            CursorSearchOutcome.InvalidGrid, read, blockedEncounters,
                            firstBlockedTile);
                    byte sourceEdges = directionGrid[tile];
                    for (int direction = 0; direction < 8; direction++)
                    {
                        int nextX = x + Dx[direction];
                        int nextY = y + Dy[direction];
                        int next = GetTileId(current.RowStarts, nextX, nextY);
                        if (next < 0 || bfsVisited[next] == generation)
                            continue;
                        if (!EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(
                                sourceEdges, directionGrid[next], direction))
                            continue;
                        if (applyPolicy && !current.IsDirectionAllowed(player, tile, direction))
                        {
                            blockedEncounters++;
                            if (firstBlockedTile < 0)
                                firstBlockedTile = current.TryGetIdentity(tile, out _) ? tile : next;
                            continue;
                        }
                        if (next == target)
                            return new CursorSearchResult(
                                CursorSearchOutcome.Reachable, read, blockedEncounters,
                                firstBlockedTile, distance + 1);
                        if (write >= bfsQueue.Length)
                            return new CursorSearchResult(
                                CursorSearchOutcome.QueueOverflow, read, blockedEncounters,
                                firstBlockedTile);
                        bfsVisited[next] = generation;
                        bfsQueue[write++] = Pack(nextX, nextY);
                    }
                }
                return new CursorSearchResult(
                    CursorSearchOutcome.NoRoute, read, blockedEncounters, firstBlockedTile, -1);
        }

        private void RefreshUnits(long now)
        {
            if (now < Volatile.Read(ref nextUnitRefreshAt))
                return;
            Volatile.Write(ref nextUnitRefreshAt, now + UnitRefreshInterval);
            // Script Extender contract: IDs are one-based while Span
            // indices are zero-based. This API work occurs only in the deferred path.
            Span<GameUnit> span = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            int[] owners = new int[span.Length + 1];
            short[] xs = new short[span.Length + 1];
            short[] ys = new short[span.Length + 1];
            for (int index = 0; index < span.Length; index++)
            {
                owners[index + 1] = span[index].r_ControllableForPlayerId;
                xs[index + 1] = unchecked((short)span[index].r_CurrentTilePositionX);
                ys[index + 1] = unchecked((short)span[index].r_CurrentTilePositionY);
            }
            units = new UnitSnapshot(owners, xs, ys);
        }

        private void RecordFreshSearch(
            CursorSearchResult result,
            long elapsedTicks,
            int unitId,
            int player,
            int startX,
            int startY,
            int targetX,
            int targetY,
            RouteTilePolicySnapshot current)
        {
            Interlocked.Increment(ref bfsFreshRuns);
            Interlocked.Add(ref bfsVisitedTotal, result.Visited);
            Interlocked.Add(ref bfsBlockedEncounterTotal, result.BlockedEncounters);
            Interlocked.Add(ref bfsElapsedTicksTotal, elapsedTicks);
            UpdateMaximum(ref bfsVisitedMaximum, result.Visited);
            UpdateMaximum(ref bfsBlockedEncounterMaximum, result.BlockedEncounters);
            UpdateMaximum(ref bfsElapsedTicksMaximum, elapsedTicks);
            switch (result.Outcome)
            {
                case CursorSearchOutcome.Reachable:
                    Interlocked.Increment(ref bfsReachable);
                    break;
                case CursorSearchOutcome.ForcedDetour:
                    Interlocked.Increment(ref bfsReachable);
                    Interlocked.Increment(ref bfsForcedDetour);
                    break;
                case CursorSearchOutcome.TargetBlocked: Interlocked.Increment(ref bfsTargetBlocked); break;
                case CursorSearchOutcome.PolicyBlocked: Interlocked.Increment(ref bfsPolicyBlocked); break;
                case CursorSearchOutcome.VanillaNoRoute: Interlocked.Increment(ref bfsVanillaNoRoute); break;
                case CursorSearchOutcome.InvalidCoordinates:
                    Interlocked.Increment(ref bfsInvalidCoordinates); break;
                case CursorSearchOutcome.Busy: Interlocked.Increment(ref bfsBusy); break;
                case CursorSearchOutcome.QueueOverflow:
                    Interlocked.Increment(ref bfsQueueOverflow); break;
                case CursorSearchOutcome.InvalidGrid:
                    Interlocked.Increment(ref bfsInvalidGrid); break;
                case CursorSearchOutcome.BudgetExceeded:
                    Interlocked.Increment(ref bfsBudgetExceeded); break;
            }

            ref CursorSample sample = ref cursorSamples[(int)result.Outcome];
            if (Interlocked.CompareExchange(ref sample.State, 1, 0) != 0)
                return;
            sample.Outcome = (int)result.Outcome;
            sample.UnitId = unitId;
            sample.Player = player;
            sample.StartX = startX;
            sample.StartY = startY;
            sample.TargetX = targetX;
            sample.TargetY = targetY;
            sample.Visited = result.Visited;
            sample.BlockedEncounters = result.BlockedEncounters;
            sample.FirstBlockedTile = result.FirstBlockedTile;
            if (result.FirstBlockedTile >= 0 &&
                current.TryGetIdentity(result.FirstBlockedTile, out RouteTileIdentity identity))
            {
                sample.GateId = identity.GateId;
                sample.BridgeId = identity.BridgeId;
            }
            sample.ElapsedTicks = elapsedTicks;
            sample.Fingerprint = current.TopologyFingerprint;
            Volatile.Write(ref sample.State, 2);
        }

        private void CaptureFailureSample(
            CursorSearchOutcome outcome,
            int unitId,
            int player,
            int startX,
            int startY,
            int targetX,
            int targetY,
            ulong fingerprint)
        {
            ref CursorSample sample = ref cursorSamples[(int)outcome];
            if (Interlocked.CompareExchange(ref sample.State, 1, 0) != 0)
                return;
            sample.Outcome = (int)outcome;
            sample.UnitId = unitId;
            sample.Player = player;
            sample.StartX = startX;
            sample.StartY = startY;
            sample.TargetX = targetX;
            sample.TargetY = targetY;
            sample.FirstBlockedTile = -1;
            sample.Fingerprint = fingerprint;
            Volatile.Write(ref sample.State, 2);
        }

        private void LogNewSamples()
        {
            double tickToMicroseconds = 1000000.0 / Stopwatch.Frequency;
            for (int index = 0; index < cursorSamples.Length; index++)
            {
                ref CursorSample sample = ref cursorSamples[index];
                if (Volatile.Read(ref sample.State) != 2 ||
                    Interlocked.CompareExchange(ref sample.Reported, 1, 0) != 0)
                    continue;
                Shared.DebugLogHelper.LogInfo(log,
                    $"Enemy-gate first cursor sample: outcome={(CursorSearchOutcome)sample.Outcome}, " +
                    $"unit={sample.UnitId}, player={sample.Player}, " +
                    $"start={sample.StartX}/{sample.StartY}, target={sample.TargetX}/{sample.TargetY}, " +
                    $"visited={sample.Visited}, blockedEncounters={sample.BlockedEncounters}, " +
                    $"firstBlockedTile={sample.FirstBlockedTile}, gate={sample.GateId}, bridge={sample.BridgeId}, " +
                    $"elapsedUs={(sample.ElapsedTicks * tickToMicroseconds).ToString("F1", CultureInfo.InvariantCulture)}, " +
                    $"policyFingerprint=0x{sample.Fingerprint:X16}.");
            }
        }

        private static void UpdateMaximum(ref long target, long candidate)
        {
            long observed;
            while (candidate > (observed = Interlocked.Read(ref target)) &&
                Interlocked.CompareExchange(ref target, candidate, observed) != observed)
            { }
        }

        private static string FormatCoverage(long value) => value == 0
            ? "0:NOT_OBSERVED"
            : value.ToString();

        internal void LogCheckpoint(string kind, string reason)
        {
            var players = new System.Text.StringBuilder();
            for (int player = 1; player <= 8; player++)
            {
                long count = Read(ref cursorPlayers[player]);
                if (count == 0) continue;
                if (players.Length > 0) players.Append(',');
                players.Append('p').Append(player).Append('=').Append(count);
            }
            if (players.Length == 0) players.Append("none:NOT_OBSERVED");
            double tickToMicroseconds = 1000000.0 / Stopwatch.Frequency;
            Shared.DebugLogHelper.LogInfo(log,
                $"Enemy-gate cursor checkpoint: kind={kind}, epoch={epochNumber}, reason={reason}, " +
                "builderFix=active-status-in-same-pcl-checkpoint, directionGridWrites=0, " +
                $"cursor(positiveSeen={Read(ref cursorPositiveSeen)},checked={Read(ref cursorChecked)}," +
                $"cacheHits={Read(ref cursorCacheHits)},detourAllowed={Read(ref cursorAllowedDetour)}," +
                $"blocked={Read(ref cursorBlocked)}), players({players}), " +
                $"failOpen(epochInactive={Read(ref cursorEpochInactive)},missingUnit={Read(ref cursorMissingUnit)}," +
                $"invalidPolicyOrPlayer={Read(ref cursorInvalidPolicyOrPlayer)},missingPointer={Read(ref cursorMissingPointer)}," +
                $"throttled={Read(ref cursorThrottled)},invalidCoordinates={Read(ref bfsInvalidCoordinates)},busy={Read(ref bfsBusy)}," +
                $"queueOverflow={Read(ref bfsQueueOverflow)},invalidGrid={Read(ref bfsInvalidGrid)}), " +
                $"bfs(fresh={Read(ref bfsFreshRuns)},visitedTotal={Read(ref bfsVisitedTotal)}," +
                $"reachable={FormatCoverage(Read(ref bfsReachable))}," +
                $"forcedDetour={FormatCoverage(Read(ref bfsForcedDetour))}," +
                $"targetBlocked={FormatCoverage(Read(ref bfsTargetBlocked))}," +
                $"policyBlocked={FormatCoverage(Read(ref bfsPolicyBlocked))}," +
                $"vanillaNoRoute={FormatCoverage(Read(ref bfsVanillaNoRoute))}," +
                $"budgetExceeded={Read(ref bfsBudgetExceeded)},budgetNodes={MaximumDiagnosticVisitedNodes}," +
                $"visitedMax={Read(ref bfsVisitedMaximum)},blockedEncountersTotal={Read(ref bfsBlockedEncounterTotal)}," +
                $"blockedEncountersMax={Read(ref bfsBlockedEncounterMaximum)},elapsedUsTotal=" +
                $"{(Read(ref bfsElapsedTicksTotal) * tickToMicroseconds).ToString("F1", CultureInfo.InvariantCulture)}," +
                $"elapsedUsMax={(Read(ref bfsElapsedTicksMaximum) * tickToMicroseconds).ToString("F1", CultureInfo.InvariantCulture)}), " +
                $"errors={Read(ref callbackErrors)}, policyFingerprint=0x{policy.TopologyFingerprint:X16}.");
            LogNewSamples();
        }

        private void ResetCounters()
        {
            Reset(ref cursorPositiveSeen);
            Reset(ref cursorChecked);
            Reset(ref cursorCacheHits);
            Reset(ref cursorAllowedDetour);
            Reset(ref cursorBlocked);
            Reset(ref cursorEpochInactive);
            Reset(ref cursorMissingUnit);
            Reset(ref cursorInvalidPolicyOrPlayer);
            Reset(ref cursorMissingPointer);
            Reset(ref cursorThrottled);
            Reset(ref bfsFreshRuns);
            Reset(ref bfsReachable);
            Reset(ref bfsForcedDetour);
            Reset(ref bfsTargetBlocked);
            Reset(ref bfsPolicyBlocked);
            Reset(ref bfsVanillaNoRoute);
            Reset(ref bfsInvalidCoordinates);
            Reset(ref bfsBusy);
            Reset(ref bfsQueueOverflow);
            Reset(ref bfsInvalidGrid);
            Reset(ref bfsBudgetExceeded);
            Reset(ref bfsVisitedTotal);
            Reset(ref bfsVisitedMaximum);
            Reset(ref bfsBlockedEncounterTotal);
            Reset(ref bfsBlockedEncounterMaximum);
            Reset(ref bfsElapsedTicksTotal);
            Reset(ref bfsElapsedTicksMaximum);
            Array.Clear(cursorPlayers, 0, cursorPlayers.Length);
            Array.Clear(cursorSamples, 0, cursorSamples.Length);
            Reset(ref callbackErrors);
            cursorCacheUntil = 0;
            nextCursorSearchAt = 0;
            cursorCacheFingerprint = 0;
            cursorCacheUnit = 0;
            nextUnitRefreshAt = 0;
        }

        private enum CursorSearchOutcome
        {
            Reachable,
            ForcedDetour,
            TargetBlocked,
            PolicyBlocked,
            VanillaNoRoute,
            NoRoute,
            InvalidCoordinates,
            Busy,
            QueueOverflow,
            InvalidGrid,
            EpochInactive,
            MissingUnit,
            InvalidPolicyOrPlayer,
            MissingPointer,
            BudgetExceeded,
            Exception
        }

        private readonly struct CursorSearchResult
        {
            internal CursorSearchResult(
                CursorSearchOutcome outcome,
                int visited,
                int blockedEncounters,
                int firstBlockedTile,
                int distance = -1)
            {
                Outcome = outcome;
                Visited = visited;
                BlockedEncounters = blockedEncounters;
                FirstBlockedTile = firstBlockedTile;
                Distance = distance;
            }

            internal CursorSearchOutcome Outcome { get; }
            internal int Visited { get; }
            internal int BlockedEncounters { get; }
            internal int FirstBlockedTile { get; }
            internal int Distance { get; }
        }

        private struct CursorSample
        {
            internal int State;
            internal int Reported;
            internal int Outcome;
            internal int UnitId;
            internal int Player;
            internal int StartX;
            internal int StartY;
            internal int TargetX;
            internal int TargetY;
            internal int Visited;
            internal int BlockedEncounters;
            internal int FirstBlockedTile;
            internal int GateId;
            internal int BridgeId;
            internal long ElapsedTicks;
            internal ulong Fingerprint;
        }

        private static int GetTileId(int[] rows, int x, int y)
        {
            if (rows == null || y < 0 || y >= rows.Length || x < 0 ||
                x >= EnemyGatePathfindingNativeDefinition.MapGridWidth)
                return -1;
            int tile = rows[y] + x;
            return tile >= 0 && tile < EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive
                ? tile
                : -1;
        }

        private static int Pack(int x, int y) => x | (y << 10);

        private static bool CanApply(RouteTilePolicySnapshot current, int player) =>
            current != null && player > 0 && player < current.HasBlockedTiles.Length &&
            current.HasBlockedTiles[player];

        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);
        private static long Read(ref long value) => Interlocked.Read(ref value);

        private sealed class UnitSnapshot
        {
            internal static readonly UnitSnapshot Empty =
                new UnitSnapshot(Array.Empty<int>(), Array.Empty<short>(), Array.Empty<short>());

            internal UnitSnapshot(int[] owners, short[] xs, short[] ys)
            { Owners = owners; Xs = xs; Ys = ys; }

            internal int[] Owners { get; }
            internal short[] Xs { get; }
            internal short[] Ys { get; }

            internal bool TryGet(int unitId, out int player, out int x, out int y)
            {
                player = 0;
                x = 0;
                y = 0;
                if (unitId <= 0 || unitId >= Owners.Length ||
                    unitId >= Xs.Length || unitId >= Ys.Length)
                    return false;
                player = Owners[unitId];
                x = Xs[unitId];
                y = Ys[unitId];
                return player > 0 && player <= 8 && x >= 0 && y >= 0;
            }
        }
    }
}

