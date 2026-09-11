using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace EnemyGatePathfindingTest
{
    internal readonly struct SamePclCoverageSnapshot
    {
        internal SamePclCoverageSnapshot(bool installed, bool ownerConflict, long calls,
            long preserved, long unsafeRoutes, long replacements, long noRoutes,
            long aiDetours, long cursorlessDetours, long rollbacks,
            long contractFailures, long exceptions)
        {
            Installed = installed; OwnerConflict = ownerConflict; Calls = calls;
            Preserved = preserved; UnsafeRoutes = unsafeRoutes; Replacements = replacements;
            NoRoutes = noRoutes; AiDetours = aiDetours; CursorlessDetours = cursorlessDetours;
            Rollbacks = rollbacks; ContractFailures = contractFailures;
            Exceptions = exceptions;
        }

        internal bool Installed { get; }
        internal bool OwnerConflict { get; }
        internal long Calls { get; }
        internal long Preserved { get; }
        internal long UnsafeRoutes { get; }
        internal long Replacements { get; }
        internal long NoRoutes { get; }
        internal long AiDetours { get; }
        internal long CursorlessDetours { get; }
        internal long Rollbacks { get; }
        internal long ContractFailures { get; }
        internal long Exceptions { get; }
    }

    // Active test implementation for the canonical FBCB9319 builder. It never edits the
    // process-wide direction grid: an unsafe published route is audited and replaced in
    // its already assigned per-unit buffer, or rejected when no policy-safe route exists.
    internal sealed unsafe class SamePclGateRouteRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PathBuilderDelegate(
            IntPtr pathManager, int movementClass, int movementProfile);

        private sealed class DetourCandidate
        {
            private readonly ulong target;
            internal DetourCandidate(ulong target) { this.target = target; }
            internal DetourHandle<PathBuilderDelegate> Handle { get; } =
                new DetourHandle<PathBuilderDelegate>();
            internal bool Committed => Handle.Success && Handle.IsInstalled &&
                Handle.Failure == null && Handle.ResolvedAddress == target;
        }

        private readonly byte* directionGrid;
        private readonly byte* unitManager;
        private readonly IntPtr expectedPathManager;
        private readonly GateGridRouteSearch search = new GateGridRouteSearch();
        private volatile RouteTilePolicySnapshot policy = RouteTilePolicySnapshot.Empty;
        private volatile PlayerKindSnapshot playerKinds = PlayerKindSnapshot.Empty;
        private HookTransaction transaction;
        private DetourCandidate builderDetour;
        private PathBuilderDelegate originalBuilder;
        private PathBuilderDelegate rootedBuilder;
        private readonly bool ownerConflict;
        [ThreadStatic] private static int callbackDepth;

        private long calls, preserved, unsafeRoutes, replacements, noRoutes;
        private long aiDetours, cursorlessDetours;
        private long rollbacks, contractFailures, exceptions;
        private long nextPlayerRefresh;

        internal SamePclGateRouteRuntime(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ScanRegion region,
            ulong libraryBase,
            bool existingHookOwner)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            directionGrid = (byte*)(libraryBase +
                unchecked((ulong)EnemyGatePathfindingNativeDefinition.PathDirectionGridRva));
            unitManager = (byte*)(libraryBase +
                unchecked((ulong)EnemyGatePathfindingNativeDefinition.NativeUnitManagerRva));
            expectedPathManager = (IntPtr)(libraryBase +
                unchecked((ulong)EnemyGatePathfindingNativeDefinition.NativePathManagerRva));
            if (Marshal.SizeOf(typeof(GameUnit)) !=
                    EnemyGatePathfindingNativeDefinition.NativeUnitStride ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() != 0x92)
                throw new InvalidOperationException("managed GameUnit layout differs from audited native layout");
            ownerConflict = existingHookOwner;
            if (existingHookOwner)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "Active Same-PCL enemy-gate builder hook suppressed: BugfixesAndQoL_Serp " +
                    "already owns 0xF4930; hookOwnerConflict=NOT_APPLICABLE.");
                return;
            }

            EnemyGatePathfindingNativeDefinition.ValidateSamePclBuilderContract(memory);
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.PathBuilderPattern,
                EnemyGatePathfindingNativeDefinition.PathBuilderRva,
                referenceHashMatches: true,
                "central tile path builder",
                log);
            if (resolution.Rva != EnemyGatePathfindingNativeDefinition.PathBuilderRva)
                throw new InvalidOperationException("central path builder resolved outside audited RVA");

            rootedBuilder = BuildPathWithEnemyGatePolicy;
            builderDetour = new DetourCandidate(
                libraryBase + unchecked((ulong)resolution.Rva));
            transaction = new HookTransaction(
                region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    OwnsHooks = false
                });
            transaction.AddDetour(
                builderDetour.Handle,
                HookTarget.FromAddress(libraryBase + unchecked((ulong)resolution.Rva)),
                rootedBuilder);
            CommitResult result = transaction.Commit();
            if (!result.IsCompleteSuccess || !builderDetour.Committed)
            {
                transaction.DisableAll();
                throw new InvalidOperationException(
                    $"active Same-PCL builder hook was not installed atomically: {result}");
            }
            originalBuilder = builderDetour.Handle.Original;
            Shared.DebugLogHelper.LogInfo(log,
                $"Active Same-PCL enemy-gate route correction installed: builder=0x{resolution.Rva:X}, " +
                "policy=per-player immutable gate/bridge tiles, globalDirectionGridWrites=0, " +
                $"maximumRouteEdges={EnemyGatePathfindingNativeDefinition.MaximumRouteEdges}.");
        }

        internal bool Installed => builderDetour != null && builderDetour.Committed;
        internal void UpdatePolicy(RouteTilePolicySnapshot updated) =>
            policy = updated ?? RouteTilePolicySnapshot.Empty;

        internal void ProcessDeferred()
        {
            long now = Stopwatch.GetTimestamp();
            if (now < Volatile.Read(ref nextPlayerRefresh))
                return;
            Volatile.Write(ref nextPlayerRefresh, now + Stopwatch.Frequency);
            var values = new bool[9];
            for (int player = 1; player <= 8; player++)
                values[player] = GamePlayerManagerAPI.Instance.IsAIPlayer(player);
            playerKinds = new PlayerKindSnapshot(values);
        }

        internal SamePclCoverageSnapshot GetCoverageSnapshot() =>
            new SamePclCoverageSnapshot(Installed, ownerConflict,
                Read(ref calls), Read(ref preserved), Read(ref unsafeRoutes),
                Read(ref replacements), Read(ref noRoutes), Read(ref aiDetours),
                Read(ref cursorlessDetours), Read(ref rollbacks),
                Read(ref contractFailures), Read(ref exceptions));

        internal void ResetCounters()
        {
            Reset(ref calls); Reset(ref preserved); Reset(ref unsafeRoutes);
            Reset(ref replacements); Reset(ref noRoutes); Reset(ref rollbacks);
            Reset(ref aiDetours); Reset(ref cursorlessDetours);
            Reset(ref contractFailures); Reset(ref exceptions);
        }

        private int BuildPathWithEnemyGatePolicy(
            IntPtr pathManager, int queryPlayerId, int movementProfile)
        {
            int vanilla = originalBuilder(pathManager, queryPlayerId, movementProfile);
            Interlocked.Increment(ref calls);
            if (callbackDepth != 0 || vanilla <= 0)
                return vanilla;
            callbackDepth++;
            try
            {
                RouteTilePolicySnapshot current = policy;
                if (current == null || current.TopologyFingerprint == 0)
                    return vanilla;
                if (!TryResolveContract(pathManager, vanilla, queryPlayerId, current,
                        out byte* path, out _, out int player,
                        out int startX, out int startY, out int targetX, out int targetY))
                {
                    Interlocked.Increment(ref contractFailures);
                    return vanilla;
                }
                if (player <= 0 || player >= current.HasBlockedTiles.Length ||
                    !current.HasBlockedTiles[player])
                {
                    Interlocked.Increment(ref preserved);
                    return vanilla;
                }
                if (!AuditRoute(current, player, path, vanilla,
                        startX, startY, targetX, targetY, out _))
                {
                    Interlocked.Increment(ref unsafeRoutes);
                    byte* backup = stackalloc byte[
                        EnemyGatePathfindingNativeDefinition.NativeUnitPathBufferStride];
                    for (int index = 0; index <
                            EnemyGatePathfindingNativeDefinition.NativeUnitPathBufferStride; index++)
                        backup[index] = path[index];
                    int* length = (int*)((byte*)pathManager.ToPointer() +
                        EnemyGatePathfindingNativeDefinition.PathManagerOutputLengthOffset);
                    int beforeLength = *length;
                    try
                    {
                        GateGridRouteResult replacement = search.Find(
                            current, player, directionGrid,
                            startX, startY, targetX, targetY, captureRoute: true);
                        if (replacement.Status == GateGridRouteStatus.Reachable &&
                            replacement.EncodedDirections != null && replacement.Distance > 0 &&
                            replacement.Distance <= EnemyGatePathfindingNativeDefinition.MaximumRouteEdges)
                        {
                            int encodedBytes = (replacement.Distance + 1) >> 1;
                            for (int index = 0; index <
                                    EnemyGatePathfindingNativeDefinition.NativeUnitPathBufferStride; index++)
                                path[index] = index < encodedBytes
                                    ? replacement.EncodedDirections[index]
                                    : (byte)0;
                            *length = replacement.Distance;
                            if (!AuditRoute(current, player, path, replacement.Distance,
                                    startX, startY, targetX, targetY, out _))
                                throw new InvalidOperationException("replacement route failed its live audit");
                            Interlocked.Increment(ref replacements);
                            if (playerKinds.IsAi(player))
                                Interlocked.Increment(ref aiDetours);
                            else
                                Interlocked.Increment(ref cursorlessDetours);
                            return replacement.Distance;
                        }
                        if (replacement.Status == GateGridRouteStatus.NoRoute ||
                            replacement.Status == GateGridRouteStatus.TargetBlocked)
                        {
                            for (int index = 0; index <
                                    EnemyGatePathfindingNativeDefinition.NativeUnitPathBufferStride; index++)
                                path[index] = 0;
                            *length = 0;
                            Interlocked.Increment(ref noRoutes);
                            return 0;
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref exceptions);
                    }
                    for (int index = 0; index <
                            EnemyGatePathfindingNativeDefinition.NativeUnitPathBufferStride; index++)
                        path[index] = backup[index];
                    *length = beforeLength;
                    Interlocked.Increment(ref rollbacks);
                    return vanilla;
                }
                Interlocked.Increment(ref preserved);
                return vanilla;
            }
            catch
            {
                Interlocked.Increment(ref exceptions);
                return vanilla;
            }
            finally
            {
                callbackDepth--;
            }
        }

        private bool TryResolveContract(
            IntPtr pathManager,
            int result,
            int queryPlayerId,
            RouteTilePolicySnapshot current,
            out byte* path,
            out int unitId,
            out int player,
            out int startX,
            out int startY,
            out int targetX,
            out int targetY)
        {
            path = null; unitId = 0; player = 0;
            startX = startY = targetX = targetY = 0;
            if (pathManager != expectedPathManager || current == null ||
                current.TopologyFingerprint == 0 || result <= 0 ||
                result > EnemyGatePathfindingNativeDefinition.MaximumRouteEdges)
                return false;
            byte* manager = (byte*)pathManager.ToPointer();
            path = *(byte**)(manager +
                EnemyGatePathfindingNativeDefinition.PathManagerOutputBufferOffset);
            if (path == null || *(int*)(manager +
                    EnemyGatePathfindingNativeDefinition.PathManagerOutputLengthOffset) != result)
                return false;
            long offset = path - (unitManager +
                EnemyGatePathfindingNativeDefinition.NativeUnitPathBufferOffset);
            if (offset <= 0 || offset %
                    EnemyGatePathfindingNativeDefinition.NativeUnitPathBufferStride != 0)
                return false;
            unitId = (int)(offset /
                EnemyGatePathfindingNativeDefinition.NativeUnitPathBufferStride);
            if (unitId <= 0 || unitId > EnemyGatePathfindingNativeDefinition.MaximumUnitId)
                return false;
            GameUnit* unit = (GameUnit*)(unitManager +
                EnemyGatePathfindingNativeDefinition.NativeUnitSlotDataOffset +
                unitId * EnemyGatePathfindingNativeDefinition.NativeUnitStride);
            if (unit->r_AliveState != AliveState.IsAlive || unit->r_GlobalId == 0)
                return false;
            player = unit->r_ControllableForPlayerId;
            if (player != queryPlayerId)
                return false;
            startX = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathManagerStartXOffset);
            startY = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathManagerStartYOffset);
            targetX = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathManagerTargetXOffset);
            targetY = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathManagerTargetYOffset);
            return GateGridRouteSearch.GetTileId(current.RowStarts, startX, startY) >= 0 &&
                GateGridRouteSearch.GetTileId(current.RowStarts, targetX, targetY) >= 0;
        }

        private static bool AuditRoute(
            RouteTilePolicySnapshot current,
            int player,
            byte* path,
            int length,
            int startX,
            int startY,
            int targetX,
            int targetY,
            out int firstBlocked)
        {
            firstBlocked = -1;
            int x = startX, y = startY;
            int startTile = GateGridRouteSearch.GetTileId(current.RowStarts, x, y);
            bool escaping = current.IsBlocked(player, startTile);
            for (int index = 0; index < length; index++)
            {
                int direction = (path[index >> 1] >> ((index & 1) * 4)) & 0x0F;
                if (direction < 0 || direction > 7)
                    return false;
                int nx = x + GateGridRouteSearch.Dx[direction];
                int ny = y + GateGridRouteSearch.Dy[direction];
                int next = GateGridRouteSearch.GetTileId(current.RowStarts, nx, ny);
                if (next < 0)
                    return false;
                bool blocked = current.IsBlocked(player, next);
                if (blocked && !escaping)
                {
                    firstBlocked = next;
                    return false;
                }
                if ((direction & 1) != 0 && !escaping)
                {
                    int sideA = GateGridRouteSearch.GetTileId(current.RowStarts, nx, y);
                    int sideB = GateGridRouteSearch.GetTileId(current.RowStarts, x, ny);
                    if ((sideA >= 0 && current.IsBlocked(player, sideA)) ||
                        (sideB >= 0 && current.IsBlocked(player, sideB)))
                    {
                        firstBlocked = sideA >= 0 && current.IsBlocked(player, sideA)
                            ? sideA : sideB;
                        return false;
                    }
                }
                if (!blocked)
                    escaping = false;
                x = nx; y = ny;
            }
            return x == targetX && y == targetY;
        }

        private static long Read(ref long value) => Interlocked.Read(ref value);
        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);

        private sealed class PlayerKindSnapshot
        {
            internal static readonly PlayerKindSnapshot Empty =
                new PlayerKindSnapshot(Array.Empty<bool>());
            private readonly bool[] values;
            internal PlayerKindSnapshot(bool[] values) =>
                this.values = values ?? Array.Empty<bool>();
            internal bool IsAi(int player) =>
                player > 0 && player < values.Length && values[player];
        }
    }

    internal enum GateGridRouteStatus
    {
        Reachable,
        TargetBlocked,
        NoRoute,
        Busy,
        Invalid
    }

    internal readonly struct GateGridRouteResult
    {
        internal GateGridRouteResult(GateGridRouteStatus status, int distance,
            int visited, int blockedEncounters, int firstBlockedTile,
            byte[] encodedDirections)
        {
            Status = status; Distance = distance; Visited = visited;
            BlockedEncounters = blockedEncounters; FirstBlockedTile = firstBlockedTile;
            EncodedDirections = encodedDirections;
        }
        internal GateGridRouteStatus Status { get; }
        internal int Distance { get; }
        internal int Visited { get; }
        internal int BlockedEncounters { get; }
        internal int FirstBlockedTile { get; }
        internal byte[] EncodedDirections { get; }
    }

    internal sealed unsafe class GateGridRouteSearch
    {
        internal static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        internal static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
        private readonly int[] marks = new int[
            EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive];
        private readonly int[] queue = new int[
            EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive];
        private readonly int[] previous = new int[
            EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive];
        private readonly byte[] previousDirection = new byte[
            EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive];
        private int generation, busy;

        internal GateGridRouteResult Find(RouteTilePolicySnapshot policy, int player,
            byte* directionGrid, int startX, int startY, int targetX, int targetY,
            bool captureRoute)
        {
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
                return new GateGridRouteResult(GateGridRouteStatus.Busy, -1, 0, 0, -1, null);
            try
            {
                int start = GetTileId(policy?.RowStarts, startX, startY);
                int target = GetTileId(policy?.RowStarts, targetX, targetY);
                if (policy == null || directionGrid == null || start < 0 || target < 0 ||
                    player <= 0 || player >= policy.HasBlockedTiles.Length)
                    return new GateGridRouteResult(GateGridRouteStatus.Invalid, -1, 0, 0, -1, null);
                if (policy.IsBlocked(player, target))
                    return new GateGridRouteResult(GateGridRouteStatus.TargetBlocked, -1, 0, 1, target, null);
                if (++generation == 0)
                {
                    Array.Clear(marks, 0, marks.Length);
                    generation = 1;
                }
                int head = 0, tail = 0, blocked = 0, firstBlocked = -1;
                marks[start] = generation;
                previous[start] = -1;
                queue[tail++] = Pack(startX, startY);
                while (head < tail)
                {
                    int packed = queue[head++];
                    int x = packed & 0x3FF, y = packed >> 10;
                    int from = GetTileId(policy.RowStarts, x, y);
                    if (from == target)
                        return BuildResult(start, target, head, blocked, firstBlocked, captureRoute);
                    bool escaping = policy.IsBlocked(player, from);
                    byte source = directionGrid[from];
                    for (int direction = 0; direction < 8; direction++)
                    {
                        int nx = x + Dx[direction], ny = y + Dy[direction];
                        int to = GetTileId(policy.RowStarts, nx, ny);
                        if (to < 0 || marks[to] == generation ||
                            !EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(
                                source, directionGrid[to], direction))
                            continue;
                        if (policy.IsBlocked(player, to) && !escaping)
                        {
                            blocked++;
                            if (firstBlocked < 0) firstBlocked = to;
                            continue;
                        }
                        if ((direction & 1) != 0 && !escaping)
                        {
                            int sideA = GetTileId(policy.RowStarts, nx, y);
                            int sideB = GetTileId(policy.RowStarts, x, ny);
                            if ((sideA >= 0 && policy.IsBlocked(player, sideA)) ||
                                (sideB >= 0 && policy.IsBlocked(player, sideB)))
                            {
                                blocked++;
                                if (firstBlocked < 0)
                                    firstBlocked = sideA >= 0 && policy.IsBlocked(player, sideA)
                                        ? sideA : sideB;
                                continue;
                            }
                        }
                        marks[to] = generation;
                        previous[to] = from;
                        previousDirection[to] = unchecked((byte)direction);
                        if (tail >= queue.Length)
                            return new GateGridRouteResult(GateGridRouteStatus.Invalid,
                                -1, head, blocked, firstBlocked, null);
                        queue[tail++] = Pack(nx, ny);
                    }
                }
                return new GateGridRouteResult(GateGridRouteStatus.NoRoute,
                    -1, head, blocked, firstBlocked, null);
            }
            finally
            {
                Volatile.Write(ref busy, 0);
            }
        }

        private GateGridRouteResult BuildResult(int start, int target, int visited,
            int blocked, int firstBlocked, bool capture)
        {
            int distance = 0;
            for (int tile = target; tile != start; tile = previous[tile])
            {
                if (tile < 0 || distance >= EnemyGatePathfindingNativeDefinition.MaximumRouteEdges)
                    return new GateGridRouteResult(GateGridRouteStatus.Invalid,
                        -1, visited, blocked, firstBlocked, null);
                distance++;
            }
            byte[] encoded = capture ? new byte[(distance + 1) >> 1] : null;
            if (capture)
            {
                int tile = target;
                for (int reverseIndex = distance - 1; reverseIndex >= 0; reverseIndex--)
                {
                    int direction = previousDirection[tile];
                    encoded[reverseIndex >> 1] |= unchecked((byte)(direction <<
                        ((reverseIndex & 1) * 4)));
                    tile = previous[tile];
                }
            }
            return new GateGridRouteResult(GateGridRouteStatus.Reachable,
                distance, visited, blocked, firstBlocked, encoded);
        }

        internal static int GetTileId(int[] rows, int x, int y)
        {
            if (rows == null || y < 0 || y >= rows.Length || x < 0 ||
                x >= EnemyGatePathfindingNativeDefinition.MapGridWidth)
                return -1;
            int tile = rows[y] + x;
            return tile >= 0 && tile < EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive
                ? tile : -1;
        }

        private static int Pack(int x, int y) => x | (y << 10);
    }
}
