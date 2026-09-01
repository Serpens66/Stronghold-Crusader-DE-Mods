using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace EnemyGatePathfindingTest
{
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
            new Dictionary<int, RouteTileIdentity>(), new bool[9], 0);

        internal RouteTilePolicySnapshot(
            ulong[][] hostileGateBits,
            ulong[][] hostileBridgeBits,
            int[] rowStarts,
            Dictionary<int, RouteTileIdentity> identities,
            bool[] hasBlockedTiles,
            ulong topologyFingerprint,
            RouteBlockedTile[][] blockedTiles = null)
        {
            HostileGateBits = hostileGateBits ?? new ulong[9][];
            HostileBridgeBits = hostileBridgeBits ?? new ulong[9][];
            RowStarts = rowStarts ?? Array.Empty<int>();
            Identities = identities ?? new Dictionary<int, RouteTileIdentity>();
            HasBlockedTiles = hasBlockedTiles ?? new bool[9];
            TopologyFingerprint = topologyFingerprint;
            BlockedTiles = blockedTiles ?? new RouteBlockedTile[9][];
        }

        internal ulong[][] HostileGateBits { get; }
        internal ulong[][] HostileBridgeBits { get; }
        internal int[] RowStarts { get; }
        internal Dictionary<int, RouteTileIdentity> Identities { get; }
        internal bool[] HasBlockedTiles { get; }
        internal RouteBlockedTile[][] BlockedTiles { get; }
        internal ulong TopologyFingerprint { get; }
        internal bool IsGateBlocked(int playerId, int tileId) =>
            IsSet(HostileGateBits, playerId, tileId);
        internal bool IsBridgeBlocked(int playerId, int tileId) =>
            IsSet(HostileBridgeBits, playerId, tileId);
        internal bool IsBlocked(int playerId, int tileId) =>
            IsGateBlocked(playerId, tileId) || IsBridgeBlocked(playerId, tileId);
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

    internal readonly struct RoutePclCorrelation
    {
        internal RoutePclCorrelation(bool found, int sourcePcl, int targetPcl, long result)
        { Found = found; SourcePcl = sourcePcl; TargetPcl = targetPcl; Result = result; }
        internal bool Found { get; }
        internal int SourcePcl { get; }
        internal int TargetPcl { get; }
        internal long Result { get; }
    }

    internal sealed unsafe class TileRouteDiagnostics
    {
        private static readonly long SummaryInterval = Stopwatch.Frequency * 10L;
        private static readonly long UnitRefreshInterval = Math.Max(1, Stopwatch.Frequency / 4);
        private static readonly long CursorCacheInterval = Math.Max(1, Stopwatch.Frequency / 20);
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
        private HookRef<X64InlineHook> cursorHook = new HookRef<X64InlineHook>();

        private int bfsGate;
        private int bfsGeneration;
        private int epochActive;
        private int epochRequested;
        private int epochNumber;
        private long nextSummaryAt;
        private long nextUnitRefreshAt;
        private long cursorCacheUntil;
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
        private long cursorFailOpen;
        private long callbackErrors;

        internal TileRouteDiagnostics(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
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
                    "Cursor tile-policy hook was not installed because MoveMoatTest_Serp owns " +
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

            // UPDATE REVIEW (Zhuqiaomon/Script Extender 1.42.0): at this exact site,
            // AfterCallback executes the managed callback before relocating TEST/LEA/MOV.
            cursorTransaction = new HookTransaction(
                memory,
                libraryBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);
            cursorTransaction.AddContextHook(
                ref cursorHook,
                libraryBase + unchecked((ulong)cursorRva),
                FilterPositiveCursorPcl,
                regs: X64SmartCPUContextRegs.All,
                hookSize: EnemyGatePathfindingNativeDefinition.CursorPclDecisionHookLength,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);
            cursorTransaction.Commit();
            if (!cursorHook.Success)
                throw new InvalidOperationException("read-only cursor PCL decision hook was not installed");

            Shared.DebugLogHelper.LogInfo(log,
                "Crash-safe cursor route hook installed: " +
                $"cursorPclDecision=0x{cursorRva:X} ({cursor.Method}+0x" +
                $"{EnemyGatePathfindingNativeDefinition.CursorPclDecisionOffsetInPattern:X}), " +
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
            LogSummary("final", reason ?? "unspecified");
            policy = RouteTilePolicySnapshot.Empty;
            units = UnitSnapshot.Empty;
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
                if (Volatile.Read(ref epochActive) != 0 &&
                    now >= Volatile.Read(ref nextSummaryAt))
                {
                    Volatile.Write(ref nextSummaryAt, now + SummaryInterval);
                    LogSummary("periodic", "10-second interval");
                }
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
                    Interlocked.Increment(ref cursorFailOpen);
                    return;
                }

                int unitId = unchecked((int)(uint)registers->R14);
                UnitSnapshot currentUnits = units;
                RouteTilePolicySnapshot current = policy;
                if (!currentUnits.TryGet(unitId, out int player, out int startX, out int startY) ||
                    !CanApply(current, player) || cursorX == null || cursorY == null)
                {
                    Interlocked.Increment(ref cursorFailOpen);
                    return;
                }

                int targetX = *cursorX;
                int targetY = *cursorY;
                Interlocked.Increment(ref cursorChecked);
                long now = Stopwatch.GetTimestamp();
                int reachable;
                if (now <= Volatile.Read(ref cursorCacheUntil) &&
                    cursorCacheFingerprint == current.TopologyFingerprint &&
                    cursorCacheUnit == unitId && cursorCachePlayer == player &&
                    cursorCacheStartX == startX && cursorCacheStartY == startY &&
                    cursorCacheTargetX == targetX && cursorCacheTargetY == targetY)
                {
                    reachable = cursorCacheResult;
                    Interlocked.Increment(ref cursorCacheHits);
                }
                else
                {
                    reachable = SearchWithoutBlocked(
                        current, player, startX, startY, targetX, targetY);
                    cursorCacheFingerprint = current.TopologyFingerprint;
                    cursorCacheUnit = unitId;
                    cursorCachePlayer = player;
                    cursorCacheStartX = startX;
                    cursorCacheStartY = startY;
                    cursorCacheTargetX = targetX;
                    cursorCacheTargetY = targetY;
                    cursorCacheResult = reachable;
                    Volatile.Write(ref cursorCacheUntil, now + CursorCacheInterval);
                }

                if (reachable < 0)
                {
                    Interlocked.Increment(ref cursorFailOpen);
                    return;
                }
                if (reachable != 0)
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
                Interlocked.Increment(ref cursorFailOpen);
                Interlocked.Increment(ref callbackErrors);
            }
        }

        private int SearchWithoutBlocked(
            RouteTilePolicySnapshot current,
            int player,
            int startX,
            int startY,
            int targetX,
            int targetY)
        {
            if (Interlocked.CompareExchange(ref bfsGate, 1, 0) != 0)
                return -1;
            try
            {
                int start = GetTileId(current.RowStarts, startX, startY);
                int target = GetTileId(current.RowStarts, targetX, targetY);
                if (start < 0 || target < 0)
                    return -1;
                if (current.IsBlocked(player, target))
                    return 0;
                if (start == target)
                    return 1;

                int generation = unchecked(++bfsGeneration);
                if (generation == 0)
                {
                    Array.Clear(bfsVisited, 0, bfsVisited.Length);
                    generation = ++bfsGeneration;
                }
                int read = 0;
                int write = 0;
                bfsVisited[start] = generation;
                bfsQueue[write++] = Pack(startX, startY);
                while (read < write)
                {
                    int packed = bfsQueue[read++];
                    int x = packed & 0x3FF;
                    int y = packed >> 10;
                    int tile = GetTileId(current.RowStarts, x, y);
                    if (tile < 0)
                        return -1;
                    byte sourceEdges = directionGrid[tile];
                    for (int direction = 0; direction < 8; direction++)
                    {
                        int nextX = x + Dx[direction];
                        int nextY = y + Dy[direction];
                        int next = GetTileId(current.RowStarts, nextX, nextY);
                        if (next < 0 || bfsVisited[next] == generation ||
                            current.IsBlocked(player, next))
                            continue;
                        if (!EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(
                                sourceEdges, directionGrid[next], direction))
                            continue;
                        if (next == target)
                            return 1;
                        if (write >= bfsQueue.Length)
                            return -1;
                        bfsVisited[next] = generation;
                        bfsQueue[write++] = Pack(nextX, nextY);
                    }
                }
                return 0;
            }
            finally
            {
                Volatile.Write(ref bfsGate, 0);
            }
        }

        private void RefreshUnits(long now)
        {
            if (now < Volatile.Read(ref nextUnitRefreshAt))
                return;
            Volatile.Write(ref nextUnitRefreshAt, now + UnitRefreshInterval);
            // UPDATE REVIEW (Script Extender 1.42.0): IDs are one-based while Span
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

        private void LogSummary(string kind, string reason)
        {
            Shared.DebugLogHelper.LogInfo(log,
                $"Crash-safe tile-route {kind} summary: epoch={epochNumber}, reason={reason}, " +
                "builderFix=disabled-unvalidated-local-edge-coverage, directionGridWrites=0, " +
                $"cursor(positiveSeen={Read(ref cursorPositiveSeen)},checked={Read(ref cursorChecked)}," +
                $"cacheHits={Read(ref cursorCacheHits)},detourAllowed={Read(ref cursorAllowedDetour)}," +
                $"blocked={Read(ref cursorBlocked)},failOpen={Read(ref cursorFailOpen)}), " +
                $"errors={Read(ref callbackErrors)}, policyFingerprint=0x{policy.TopologyFingerprint:X16}.");
        }

        private void ResetCounters()
        {
            Reset(ref cursorPositiveSeen);
            Reset(ref cursorChecked);
            Reset(ref cursorCacheHits);
            Reset(ref cursorAllowedDetour);
            Reset(ref cursorBlocked);
            Reset(ref cursorFailOpen);
            Reset(ref callbackErrors);
            cursorCacheUntil = 0;
            cursorCacheFingerprint = 0;
            cursorCacheUnit = 0;
            long now = Stopwatch.GetTimestamp();
            nextSummaryAt = now + SummaryInterval;
            nextUnitRefreshAt = 0;
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
