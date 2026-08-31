using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
            new ulong[9][], new ulong[9][], new int[0],
            new Dictionary<int, RouteTileIdentity>(), new bool[9], 0);

        internal RouteTilePolicySnapshot(ulong[][] hostileGateBits, ulong[][] hostileBridgeBits,
            int[] rowStarts, Dictionary<int, RouteTileIdentity> identities,
            bool[] hasBlockedTiles, ulong topologyFingerprint,
            RouteBlockedTile[][] blockedTiles = null)
        {
            HostileGateBits = hostileGateBits ?? new ulong[9][];
            HostileBridgeBits = hostileBridgeBits ?? new ulong[9][];
            RowStarts = rowStarts ?? new int[0];
            Identities = identities ?? new Dictionary<int, RouteTileIdentity>();
            HasBlockedTiles = hasBlockedTiles ?? new bool[9];
            TopologyFingerprint = topologyFingerprint;
            // Coordinates must come directly from validated footprints. Tile-ID to X/Y
            // inversion is ambiguous on the isometric row layout and is never attempted.
            BlockedTiles = blockedTiles ?? new RouteBlockedTile[9][];
        }

        internal ulong[][] HostileGateBits { get; }
        internal ulong[][] HostileBridgeBits { get; }
        internal int[] RowStarts { get; }
        internal Dictionary<int, RouteTileIdentity> Identities { get; }
        internal bool[] HasBlockedTiles { get; }
        internal RouteBlockedTile[][] BlockedTiles { get; }
        internal ulong TopologyFingerprint { get; }
        internal bool IsGateBlocked(int playerId, int tileId) => IsSet(HostileGateBits, playerId, tileId);
        internal bool IsBridgeBlocked(int playerId, int tileId) => IsSet(HostileBridgeBits, playerId, tileId);
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
            GateId != 0 ? GateId : gateId, BridgeId != 0 ? BridgeId : bridgeId);
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
        // UPDATE REVIEW (CrusaderDE.dll): F4930 has exactly two direct callers in the
        // complete .text XRef scan. Both delegates use the audited Win64 ABI.
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CentralMovementPlanDelegate(IntPtr manager, int unitId, int x, int y);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int MainPathBuilderDelegate(IntPtr manager, int movementClass, int profile);

        private const int ContextUnknown = 0;
        private const int ContextMoveHere = 1;
        private const int ContextCentralPlanner = 2;
        private const int SampleCapacity = 256;
        private const int MaximumSamples = 80;
        private const int MaximumAiSamplesPerPlayer = 8;
        private static readonly long SummaryInterval = Stopwatch.Frequency * 10L;
        private static readonly long UnitRefreshInterval = Math.Max(1, Stopwatch.Frequency / 4);
        private static readonly long CursorCacheInterval = Math.Max(1, Stopwatch.Frequency / 20);
        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private readonly ManualLogSource log;
        private readonly int* cursorX;
        private readonly int* cursorY;
        private readonly byte* directionGrid;
        private readonly int[] overlayMarks = new int[EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive];
        private readonly int[] overlayTiles = new int[EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive];
        private readonly byte[] overlayOriginal = new byte[EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive];
        private readonly int[] bfsVisited = new int[EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive];
        private readonly int[] bfsQueue = new int[EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive];
        private readonly PendingDecision[] pending = new PendingDecision[SampleCapacity];
        private readonly PendingDecision[] drain = new PendingDecision[SampleCapacity];
        private readonly int[] aiSamples = new int[9];
        private readonly long[] buildersByPlayer = new long[9];
        private volatile RouteTilePolicySnapshot policy = RouteTilePolicySnapshot.Empty;
        private volatile UnitSnapshot units = UnitSnapshot.Empty;
        private Action epochStarter;

        private CentralMovementPlanDelegate originalPlan;
        private CentralMovementPlanDelegate rootedPlan;
        private MainPathBuilderDelegate originalBuilder;
        private MainPathBuilderDelegate rootedBuilder;
        private NativeDetour planDetour;
        private NativeDetour builderDetour;
        private HookTransaction cursorTransaction;
        private HookRef<X64InlineHook> cursorHook = new HookRef<X64InlineHook>();

        private long pendingWrite, pendingRead;
        private int pendingGate, overlayGate, bfsGate, overlayGeneration, bfsGeneration;
        private int confirmedBuilderThread, epochActive, epochRequested, epochNumber, samplesLogged;
        private long nextSummaryAt, nextUnitRefreshAt;
        private long cursorCacheUntil;
        private ulong cursorCacheFingerprint;
        private int cursorCacheUnit, cursorCachePlayer, cursorCacheStartX, cursorCacheStartY;
        private int cursorCacheTargetX, cursorCacheTargetY, cursorCacheResult;
        private long builderCalls, vanillaPositive, vanillaNegative;
        private long gateCrossings, bridgeCrossings, bothCrossings, noCrossings;
        private long rerouteAttempts, rerouteSuccesses, rerouteBlocked, rerouteStillCrossed;
        private long overlayRestores, overlayRestoreMismatches, overlayBusy, wrongThread;
        private long unknownContexts, invalidPaths, callbackErrors;
        private long cursorPositiveSeen, cursorChecked, cursorCacheHits;
        private long cursorAllowedDetour, cursorBlocked, cursorFailOpen;
        private long droppedSamples, humanBuilders, aiBuilders, unknownBuilders;

        [ThreadStatic] private static RouteContext activeContext;
        [ThreadStatic] private static int moveHereDepth;

        internal TileRouteDiagnostics(ManualLogSource log, ReadOnlySpan<byte> memory,
            ulong libraryBase, int* cursorX, int* cursorY, bool installNativeHooks)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.cursorX = cursorX;
            this.cursorY = cursorY;
            directionGrid = (byte*)(libraryBase +
                unchecked((ulong)EnemyGatePathfindingNativeDefinition.PathDirectionGridRva));
            if (!installNativeHooks)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "Functional tile-route hooks were not installed because MoveMoatTest_Serp is loaded; " +
                    "the Different-PCL filter remains active, but Same-PCL tile correction is disabled.");
                return;
            }

            Shared.NativeResolution plan = Resolve(memory,
                EnemyGatePathfindingNativeDefinition.CentralMovementPlanPattern,
                EnemyGatePathfindingNativeDefinition.CentralMovementPlanRva,
                "central per-unit movement planner");
            Shared.NativeResolution builder = Resolve(memory,
                EnemyGatePathfindingNativeDefinition.MainPathBuilderPattern,
                EnemyGatePathfindingNativeDefinition.MainPathBuilderRva,
                "main tile path builder");
            Shared.NativeResolution cursor = Resolve(memory,
                EnemyGatePathfindingNativeDefinition.CursorPclDecisionPattern,
                EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva -
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionOffsetInPattern,
                "ordinary movement cursor PCL decision");
            int cursorRva = cursor.Rva + EnemyGatePathfindingNativeDefinition.CursorPclDecisionOffsetInPattern;
            if (cursorRva != EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva)
                throw new InvalidOperationException("cursor PCL decision resolved outside its audited RVA");

            rootedPlan = ObservePlan;
            rootedBuilder = BuildPlayerAwareRoute;
            NativeDetour pendingPlan = null, pendingBuilder = null;
            bool planApplied = false, builderApplied = false;
            try
            {
                pendingPlan = CreateDetour(libraryBase + unchecked((ulong)plan.Rva), rootedPlan);
                originalPlan = pendingPlan.GenerateTrampoline<CentralMovementPlanDelegate>();
                pendingBuilder = CreateDetour(libraryBase + unchecked((ulong)builder.Rva), rootedBuilder);
                originalBuilder = pendingBuilder.GenerateTrampoline<MainPathBuilderDelegate>();
                pendingPlan.Apply(); planApplied = true;
                pendingBuilder.Apply(); builderApplied = true;

                // UPDATE REVIEW (Zhuqiaomon/Script Extender 1.42.0): AfterCallback runs
                // the callback before relocating TEST/LEA/MOV at this particular site.
                cursorTransaction = new HookTransaction(memory, libraryBase, loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                cursorTransaction.AddContextHook(ref cursorHook,
                    libraryBase + unchecked((ulong)cursorRva), FilterPositiveCursorPcl,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: EnemyGatePathfindingNativeDefinition.CursorPclDecisionHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                cursorTransaction.Commit();
                if (!cursorHook.Success)
                    throw new InvalidOperationException("cursor PCL decision hook was not installed");
                planDetour = pendingPlan;
                builderDetour = pendingBuilder;
            }
            catch
            {
                UndoAndDispose(pendingBuilder, builderApplied);
                UndoAndDispose(pendingPlan, planApplied);
                throw;
            }

            Shared.DebugLogHelper.LogInfo(log,
                "Functional tile-route hooks installed: " +
                $"centralPlan=0x{plan.Rva:X} ({plan.Method}), mainBuilder=0x{builder.Rva:X} ({builder.Method}), " +
                $"cursorPclDecision=0x{cursorRva:X} ({cursor.Method}+0x" +
                $"{EnemyGatePathfindingNativeDefinition.CursorPclDecisionOffsetInPattern:X}), " +
                $"directionGrid=0x{EnemyGatePathfindingNativeDefinition.PathDirectionGridRva:X}. " +
                "E32B0 and E9FF0 are deliberately not hooked.");
        }

        internal bool HooksInstalled => planDetour != null && builderDetour != null &&
            cursorTransaction != null && cursorHook.Success;
        internal void SetPclCorrelation(Func<long, int, int, int, RoutePclCorrelation> unused) { }
        internal void SetTopologyEpochStarter(Action starter) => epochStarter = starter;
        internal void UpdatePolicy(RouteTilePolicySnapshot updated) { if (updated != null) policy = updated; }
        internal void RequestEpoch() => Volatile.Write(ref epochRequested, 1);

        internal void BeginEpoch(string reason)
        {
            if (Interlocked.CompareExchange(ref epochActive, 1, 0) != 0) return;
            Volatile.Write(ref epochRequested, 0);
            epochStarter?.Invoke();
            Interlocked.Increment(ref epochNumber);
            ResetCounters();
        }

        internal void EndEpoch(string reason)
        {
            if (Volatile.Read(ref epochActive) == 0) return;
            ProcessDeferred();
            if (Interlocked.CompareExchange(ref epochActive, 0, 1) == 1)
            {
                LogSummary("final", reason ?? "unspecified");
                policy = RouteTilePolicySnapshot.Empty;
                units = UnitSnapshot.Empty;
            }
        }

        internal void OnMoveHere(UnitMoveHereEventArgs args)
        {
            // UPDATE REVIEW (Script Extender 1.42.0): Pre/Post synchronously encloses
            // MoveHere's direct F4930 call.
            if (args == null || !HooksInstalled) return;
            if (args.Phase == EventHookPhase.Pre)
            {
                moveHereDepth++;
                activeContext = new RouteContext(args.UnitId, ReadOwner(args.UnitId),
                    args.TileX, args.TileY, ContextMoveHere);
            }
            else if (args.Phase == EventHookPhase.Post && moveHereDepth > 0)
            {
                moveHereDepth--;
                if (moveHereDepth == 0) activeContext = default;
            }
        }

        internal void ProcessDeferred()
        {
            if (!HooksInstalled) return;
            try
            {
                if (Volatile.Read(ref epochActive) == 0 &&
                    Interlocked.Exchange(ref epochRequested, 0) != 0)
                    BeginEpoch("first deferred native query; supports map editor");
                long now = Stopwatch.GetTimestamp();
                RefreshUnits(now);
                if (Volatile.Read(ref epochActive) == 0) return;
                int count = DrainSamples();
                for (int i = 0; i < count; i++) LogSample(drain[i]);
                if (now >= Volatile.Read(ref nextSummaryAt))
                {
                    Volatile.Write(ref nextSummaryAt, now + SummaryInterval);
                    LogSummary("periodic", "10-second interval");
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref callbackErrors);
                Shared.DebugLogHelper.LogWarning(log,
                    $"Deferred functional route diagnostics failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private int ObservePlan(IntPtr manager, int unitId, int targetX, int targetY)
        {
            RouteContext previous = activeContext;
            bool replace = moveHereDepth == 0;
            if (replace)
                activeContext = new RouteContext(unitId, ReadOwner(unitId), targetX, targetY,
                    ContextCentralPlanner);
            try { return originalPlan(manager, unitId, targetX, targetY); }
            finally { if (replace) activeContext = previous; }
        }

        private int BuildPlayerAwareRoute(IntPtr pathManager, int movementClass, int profile)
        {
            Interlocked.Increment(ref builderCalls);
            RouteContext context = activeContext;
            RecordBuilderPlayer(context.PlayerId);
            int vanillaResult = originalBuilder(pathManager, movementClass, profile);
            if (vanillaResult <= 0)
            { Interlocked.Increment(ref vanillaNegative); return vanillaResult; }
            Interlocked.Increment(ref vanillaPositive);

            RouteTilePolicySnapshot current = policy;
            if (Volatile.Read(ref epochActive) == 0 || !CanApply(current, context.PlayerId))
            {
                if (context.PlayerId <= 0 || context.PlayerId > 8)
                    Interlocked.Increment(ref unknownContexts);
                return vanillaResult;
            }
            RouteAnalysis vanilla = AnalyzePath(pathManager, current, context.PlayerId);
            if (!vanilla.Valid)
            { Interlocked.Increment(ref invalidPaths); return vanillaResult; }
            if (!vanilla.CrossesBlocked)
            { Interlocked.Increment(ref noCrossings); return vanillaResult; }
            RecordCrossing(vanilla);

            int thread = Environment.CurrentManagedThreadId;
            int known = Volatile.Read(ref confirmedBuilderThread);
            if (known == 0)
            { Interlocked.CompareExchange(ref confirmedBuilderThread, thread, 0); known = Volatile.Read(ref confirmedBuilderThread); }
            if (known != thread)
            { Interlocked.Increment(ref wrongThread); return vanillaResult; }
            if (Interlocked.CompareExchange(ref overlayGate, 1, 0) != 0)
            { Interlocked.Increment(ref overlayBusy); return vanillaResult; }

            int overlayCount = 0;
            bool overlayComplete = true;
            int rerouteResult = vanillaResult;
            RouteAnalysis reroute = default;
            bool rerunCompleted = false;
            try
            {
                ApplyOverlay(current, context.PlayerId, ref overlayCount, ref overlayComplete);
                if (!overlayComplete || overlayCount <= 0)
                {
                    Interlocked.Increment(ref invalidPaths);
                    return vanillaResult;
                }
                Interlocked.Increment(ref rerouteAttempts);
                rerouteResult = originalBuilder(pathManager, movementClass, profile);
                rerunCompleted = true;
                if (rerouteResult > 0) reroute = AnalyzePath(pathManager, current, context.PlayerId);
            }
            catch
            {
                Interlocked.Increment(ref callbackErrors);
                return rerunCompleted ? rerouteResult : vanillaResult;
            }
            finally
            {
                RestoreOverlay(overlayCount);
                Volatile.Write(ref overlayGate, 0);
            }

            int effective = rerouteResult;
            int action;
            if (rerouteResult <= 0)
            { Interlocked.Increment(ref rerouteBlocked); action = 2; }
            else if (!reroute.Valid)
            { Interlocked.Increment(ref invalidPaths); action = 4; }
            else if (reroute.CrossesBlocked)
            { Interlocked.Increment(ref rerouteStillCrossed); effective = 0; action = 3; }
            else
            { Interlocked.Increment(ref rerouteSuccesses); action = 1; }
            QueueSample(new PendingDecision(1, action, context, vanilla.StartX, vanilla.StartY,
                vanilla.TargetX, vanilla.TargetY, vanillaResult, effective,
                vanilla.Length, reroute.Length, vanilla.FirstHitTile,
                vanilla.GateHits, vanilla.BridgeHits));
            return effective;
        }

        private void FilterPositiveCursorPcl(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                X64SmartCPUContext* regs = context.Pointer;
                if (regs == null || unchecked((uint)regs->RAX) == 0) return;
                Interlocked.Increment(ref cursorPositiveSeen);
                int unitId = unchecked((int)(uint)regs->R14);
                UnitSnapshot currentUnits = units;
                RouteTilePolicySnapshot current = policy;
                if (Volatile.Read(ref epochActive) == 0 ||
                    !currentUnits.TryGet(unitId, out int player, out int startX, out int startY) ||
                    !CanApply(current, player) || cursorX == null || cursorY == null)
                { Interlocked.Increment(ref cursorFailOpen); return; }
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
                    reachable = SearchWithoutBlocked(current, player, startX, startY, targetX, targetY);
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
                { Interlocked.Increment(ref cursorFailOpen); return; }
                if (reachable != 0)
                { Interlocked.Increment(ref cursorAllowedDetour); return; }

                // The relocated TEST consumes zero before the later MOV EAX,1.
                regs->RAX = 0;
                Interlocked.Increment(ref cursorBlocked);
                QueueSample(new PendingDecision(2, 2,
                    new RouteContext(unitId, player, targetX, targetY, ContextUnknown),
                    startX, startY, targetX, targetY, 1, 0, 0, 0,
                    GetTileId(current.RowStarts, targetX, targetY), 0, 0));
            }
            catch
            {
                Interlocked.Increment(ref cursorFailOpen);
                Interlocked.Increment(ref callbackErrors);
            }
        }

        private int SearchWithoutBlocked(RouteTilePolicySnapshot current, int player,
            int startX, int startY, int targetX, int targetY)
        {
            if (Interlocked.CompareExchange(ref bfsGate, 1, 0) != 0) return -1;
            try
            {
                int start = GetTileId(current.RowStarts, startX, startY);
                int target = GetTileId(current.RowStarts, targetX, targetY);
                if (start < 0 || target < 0) return -1;
                if (current.IsBlocked(player, target)) return 0;
                if (start == target) return 1;
                int generation = unchecked(++bfsGeneration);
                if (generation == 0)
                { Array.Clear(bfsVisited, 0, bfsVisited.Length); generation = ++bfsGeneration; }
                int read = 0, write = 0;
                bfsVisited[start] = generation;
                bfsQueue[write++] = Pack(startX, startY);
                while (read < write)
                {
                    int packed = bfsQueue[read++];
                    int x = packed & 0x3FF;
                    int y = packed >> 10;
                    int tile = GetTileId(current.RowStarts, x, y);
                    if (tile < 0) return -1;
                    byte sourceEdges = directionGrid[tile];
                    for (int direction = 0; direction < 8; direction++)
                    {
                        int nx = x + Dx[direction], ny = y + Dy[direction];
                        int next = GetTileId(current.RowStarts, nx, ny);
                        if (next < 0 || bfsVisited[next] == generation || current.IsBlocked(player, next))
                            continue;
                        if (!EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(
                                sourceEdges, directionGrid[next], direction))
                            continue;
                        if (next == target) return 1;
                        if (write >= bfsQueue.Length) return -1;
                        bfsVisited[next] = generation;
                        bfsQueue[write++] = Pack(nx, ny);
                    }
                }
                return 0;
            }
            finally { Volatile.Write(ref bfsGate, 0); }
        }

        private void ApplyOverlay(RouteTilePolicySnapshot current, int player,
            ref int count, ref bool complete)
        {
            RouteBlockedTile[] blocked = current.BlockedTiles[player];
            if (blocked == null || blocked.Length == 0) return;
            int generation = unchecked(++overlayGeneration);
            if (generation == 0)
            { Array.Clear(overlayMarks, 0, overlayMarks.Length); generation = ++overlayGeneration; }
            for (int i = 0; i < blocked.Length; i++)
            {
                RouteBlockedTile blockedTile = blocked[i];
                if (!SaveCell(blockedTile.TileId, generation, ref count))
                { complete = false; return; }
                directionGrid[blockedTile.TileId] = 0;
                for (int direction = 0; direction < 8; direction++)
                {
                    int neighbor = GetTileId(current.RowStarts,
                        blockedTile.X + Dx[direction], blockedTile.Y + Dy[direction]);
                    if (neighbor < 0) continue;
                    if (!SaveCell(neighbor, generation, ref count))
                    { complete = false; return; }
                    directionGrid[neighbor] = EnemyGatePathfindingPolicy.CloseNeighborEdge(
                        directionGrid[neighbor], direction);
                }
            }
        }

        private bool SaveCell(int tileId, int generation, ref int count)
        {
            if (tileId < 0 || tileId >= overlayMarks.Length)
                return false;
            if (overlayMarks[tileId] == generation)
                return true;
            // The unique native tile count cannot exceed this array. If a future DLL
            // changes that invariant, silently preserve the already saved prefix and
            // let the outer operation fail open after its normal verification.
            if (count >= overlayTiles.Length) return false;
            overlayMarks[tileId] = generation;
            overlayTiles[count] = tileId;
            overlayOriginal[count] = directionGrid[tileId];
            count++;
            return true;
        }

        private void RestoreOverlay(int count)
        {
            for (int i = count - 1; i >= 0; i--) directionGrid[overlayTiles[i]] = overlayOriginal[i];
            bool exact = true;
            for (int i = 0; i < count; i++)
                if (directionGrid[overlayTiles[i]] != overlayOriginal[i]) exact = false;
            Interlocked.Increment(ref overlayRestores);
            if (!exact) Interlocked.Increment(ref overlayRestoreMismatches);
        }

        private RouteAnalysis AnalyzePath(IntPtr pathManager, RouteTilePolicySnapshot current, int player)
        {
            if (pathManager == IntPtr.Zero) return default;
            byte* manager = (byte*)pathManager;
            int length = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathLengthOffset);
            if (length <= 0 || length > EnemyGatePathfindingNativeDefinition.MaximumDecodedPathLength)
                return default;
            byte* directions = *(byte**)(manager + EnemyGatePathfindingNativeDefinition.PathDirectionBufferOffset);
            if (directions == null) return default;
            int sx = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathStartXOffset);
            int sy = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathStartYOffset);
            int tx = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathTargetXOffset);
            int ty = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathTargetYOffset);
            if (!TrySelectVariant(directions, length, sx, sy, tx, ty,
                    out bool fromTarget, out bool invert)) return default;
            int x = fromTarget ? tx : sx, y = fromTarget ? ty : sy;
            int first = -1, gateHits = 0, bridgeHits = 0;
            for (int step = -1; step < length; step++)
            {
                if (step >= 0)
                {
                    int direction = ReadDirection(directions, step);
                    int sign = invert ? -1 : 1;
                    x += Dx[direction] * sign;
                    y += Dy[direction] * sign;
                }
                int tile = GetTileId(current.RowStarts, x, y);
                if (tile < 0) return default;
                bool gate = current.IsGateBlocked(player, tile);
                bool bridge = current.IsBridgeBlocked(player, tile);
                if (!gate && !bridge) continue;
                if (first < 0) first = tile;
                if (gate) gateHits++;
                if (bridge) bridgeHits++;
            }
            return new RouteAnalysis(true, sx, sy, tx, ty, length, first, gateHits, bridgeHits);
        }

        private static bool TrySelectVariant(byte* directions, int length,
            int sx, int sy, int tx, int ty, out bool fromTarget, out bool invert)
        {
            fromTarget = invert = false;
            for (int variant = 0; variant < 4; variant++)
            {
                bool candidateFromTarget = (variant & 2) != 0;
                bool candidateInvert = (variant & 1) != 0;
                int x = candidateFromTarget ? tx : sx, y = candidateFromTarget ? ty : sy;
                bool valid = true;
                for (int step = 0; step < length; step++)
                {
                    int direction = ReadDirection(directions, step);
                    if (direction > 7) { valid = false; break; }
                    int sign = candidateInvert ? -1 : 1;
                    x += Dx[direction] * sign;
                    y += Dy[direction] * sign;
                    if (x < 0 || x >= EnemyGatePathfindingNativeDefinition.MapGridWidth ||
                        y < 0 || y >= EnemyGatePathfindingNativeDefinition.MapGridWidth)
                    { valid = false; break; }
                }
                if (valid && x == (candidateFromTarget ? sx : tx) &&
                    y == (candidateFromTarget ? sy : ty))
                { fromTarget = candidateFromTarget; invert = candidateInvert; return true; }
            }
            return false;
        }

        private static int ReadDirection(byte* directions, int step) =>
            (directions[step >> 1] >> ((step & 1) * 4)) & 0x0F;
        private static int GetTileId(int[] rows, int x, int y)
        {
            if (rows == null || y < 0 || y >= rows.Length || x < 0 ||
                x >= EnemyGatePathfindingNativeDefinition.MapGridWidth) return -1;
            int tile = rows[y] + x;
            return tile >= 0 && tile < EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive ? tile : -1;
        }
        private static int Pack(int x, int y) => x | (y << 10);
        private static bool CanApply(RouteTilePolicySnapshot current, int player) =>
            current != null && player > 0 && player < current.HasBlockedTiles.Length &&
            current.HasBlockedTiles[player] && player < current.BlockedTiles.Length;

        private void RefreshUnits(long now)
        {
            if (now < Volatile.Read(ref nextUnitRefreshAt)) return;
            Volatile.Write(ref nextUnitRefreshAt, now + UnitRefreshInterval);
            // UPDATE REVIEW (Script Extender 1.42.0): IDs are one-based, Span indices zero-based.
            Span<GameUnit> span = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            int[] owners = new int[span.Length + 1];
            short[] xs = new short[span.Length + 1], ys = new short[span.Length + 1];
            for (int i = 0; i < span.Length; i++)
            {
                owners[i + 1] = span[i].r_ControllableForPlayerId;
                xs[i + 1] = unchecked((short)span[i].r_CurrentTilePositionX);
                ys[i + 1] = unchecked((short)span[i].r_CurrentTilePositionY);
            }
            units = new UnitSnapshot(owners, xs, ys);
        }

        private int ReadOwner(int unitId)
        { UnitSnapshot current = units; return unitId > 0 && unitId < current.Owners.Length ? current.Owners[unitId] : 0; }
        private void RecordCrossing(RouteAnalysis r)
        {
            if (r.GateHits > 0 && r.BridgeHits > 0) Interlocked.Increment(ref bothCrossings);
            else if (r.GateHits > 0) Interlocked.Increment(ref gateCrossings);
            else Interlocked.Increment(ref bridgeCrossings);
        }
        private void RecordBuilderPlayer(int player)
        {
            if (player > 0 && player < buildersByPlayer.Length) Interlocked.Increment(ref buildersByPlayer[player]);
            else Interlocked.Increment(ref unknownBuilders);
        }

        private void QueueSample(PendingDecision sample)
        {
            if (Interlocked.CompareExchange(ref pendingGate, 1, 0) != 0)
            { Interlocked.Increment(ref droppedSamples); return; }
            try
            {
                long write = pendingWrite;
                if (write - pendingRead >= SampleCapacity)
                { Interlocked.Increment(ref droppedSamples); return; }
                pending[(int)(write % SampleCapacity)] = sample;
                pendingWrite = write + 1;
            }
            finally { Volatile.Write(ref pendingGate, 0); }
        }
        private int DrainSamples()
        {
            if (Interlocked.CompareExchange(ref pendingGate, 1, 0) != 0) return 0;
            try
            {
                long read = pendingRead, write = pendingWrite;
                int count = (int)Math.Min(SampleCapacity, Math.Max(0, write - read));
                for (int i = 0; i < count; i++) drain[i] = pending[(int)((read + i) % SampleCapacity)];
                pendingRead = read + count;
                return count;
            }
            finally { Volatile.Write(ref pendingGate, 0); }
        }

        private void LogSample(PendingDecision s)
        {
            int role = ResolveRole(s.Context.PlayerId);
            if (role == SamePclBridgeDiagnostics.AiRole)
            {
                int player = s.Context.PlayerId;
                if (player <= 0 || player >= aiSamples.Length || ++aiSamples[player] > MaximumAiSamplesPerPlayer) return;
            }
            if (++samplesLogged > MaximumSamples) return;
            RouteTileIdentity identity = default;
            if (s.HitTile >= 0) policy.TryGetIdentity(s.HitTile, out identity);
            Shared.DebugLogHelper.LogInfo(log,
                "Functional route sample: " +
                $"kind={(s.Kind == 1 ? "builder" : "cursor")}, action={FormatAction(s.Action)}, " +
                $"role={FormatRole(role)}, player={s.Context.PlayerId}, unit={s.Context.UnitId}, " +
                $"context={FormatContext(s.Context.Kind)}, source={s.StartX}/{s.StartY}, target={s.TargetX}/{s.TargetY}, " +
                $"vanilla={s.VanillaResult}, effective={s.EffectiveResult}, length={s.VanillaLength}->{s.EffectiveLength}, " +
                $"gateHits={s.GateHits}, bridgeHits={s.BridgeHits}, firstHit={s.HitTile}" +
                $"(gate#{identity.GateId},bridge#{identity.BridgeId}).");
        }

        private void LogSummary(string kind, string reason)
        {
            RefreshRoleCounters();
            Shared.DebugLogHelper.LogInfo(log,
                $"Functional tile-route {kind} summary: epoch={epochNumber}, reason={reason}, " +
                $"builders(total={Read(ref builderCalls)},human={Read(ref humanBuilders)},ai={Read(ref aiBuilders)}," +
                $"unknown={Read(ref unknownBuilders)},vanillaPositive={Read(ref vanillaPositive)},vanillaNegative={Read(ref vanillaNegative)}), " +
                $"crossings(gate={Read(ref gateCrossings)},bridge={Read(ref bridgeCrossings)},both={Read(ref bothCrossings)},none={Read(ref noCrossings)}), " +
                $"reroute(attempts={Read(ref rerouteAttempts)},success={Read(ref rerouteSuccesses)},blocked={Read(ref rerouteBlocked)}," +
                $"stillCrossed={Read(ref rerouteStillCrossed)}), cursor(positiveSeen={Read(ref cursorPositiveSeen)}," +
                $"checked={Read(ref cursorChecked)},cacheHits={Read(ref cursorCacheHits)}," +
                $"detourAllowed={Read(ref cursorAllowedDetour)},blocked={Read(ref cursorBlocked)}," +
                $"failOpen={Read(ref cursorFailOpen)}), overlay(restores={Read(ref overlayRestores)}," +
                $"restoreMismatch={Read(ref overlayRestoreMismatches)},busy={Read(ref overlayBusy)},wrongThread={Read(ref wrongThread)}), " +
                $"failOpen(context={Read(ref unknownContexts)},path={Read(ref invalidPaths)}), errors={Read(ref callbackErrors)}, " +
                $"droppedSamples={Read(ref droppedSamples)}, policyFingerprint=0x{policy.TopologyFingerprint:X16}.");
        }

        private void RefreshRoleCounters()
        {
            long human = 0, ai = 0, unknown = Read(ref unknownBuilders);
            for (int player = 1; player <= 8; player++)
            {
                long count = Read(ref buildersByPlayer[player]);
                int role = ResolveRole(player);
                if (role == SamePclBridgeDiagnostics.HumanRole) human += count;
                else if (role == SamePclBridgeDiagnostics.AiRole) ai += count;
                else unknown += count;
            }
            Interlocked.Exchange(ref humanBuilders, human);
            Interlocked.Exchange(ref aiBuilders, ai);
            Interlocked.Exchange(ref unknownBuilders, unknown);
        }

        private void ResetCounters()
        {
            pendingWrite = pendingRead = 0; samplesLogged = 0;
            Array.Clear(aiSamples, 0, aiSamples.Length); Array.Clear(buildersByPlayer, 0, buildersByPlayer.Length);
            Reset(ref builderCalls); Reset(ref vanillaPositive); Reset(ref vanillaNegative);
            Reset(ref gateCrossings); Reset(ref bridgeCrossings); Reset(ref bothCrossings); Reset(ref noCrossings);
            Reset(ref rerouteAttempts); Reset(ref rerouteSuccesses); Reset(ref rerouteBlocked); Reset(ref rerouteStillCrossed);
            Reset(ref overlayRestores); Reset(ref overlayRestoreMismatches); Reset(ref overlayBusy); Reset(ref wrongThread);
            Reset(ref unknownContexts); Reset(ref invalidPaths); Reset(ref callbackErrors);
            Reset(ref cursorPositiveSeen); Reset(ref cursorChecked); Reset(ref cursorCacheHits);
            Reset(ref cursorAllowedDetour);
            Reset(ref cursorBlocked); Reset(ref cursorFailOpen); Reset(ref droppedSamples);
            Reset(ref humanBuilders); Reset(ref aiBuilders); Reset(ref unknownBuilders);
            cursorCacheUntil = 0; cursorCacheFingerprint = 0; cursorCacheUnit = 0;
            long now = Stopwatch.GetTimestamp(); nextSummaryAt = now + SummaryInterval; nextUnitRefreshAt = 0;
        }

        private static int ResolveRole(int player)
        {
            try
            {
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                if (!players.IsPlayerIdValid(player)) return SamePclBridgeDiagnostics.UnknownRole;
                return players.IsAIPlayer(player) ? SamePclBridgeDiagnostics.AiRole : SamePclBridgeDiagnostics.HumanRole;
            }
            catch { return SamePclBridgeDiagnostics.UnknownRole; }
        }
        private static string FormatAction(int action) => action == 1 ? "rerouted" :
            action == 2 ? "blocked" : action == 3 ? "rejected-still-crossing" : "fail-open";
        private static string FormatRole(int role) => role == SamePclBridgeDiagnostics.HumanRole ? "human" :
            role == SamePclBridgeDiagnostics.AiRole ? "ai" : "unknown";
        private static string FormatContext(int kind) => kind == ContextMoveHere ? "MoveHere" :
            kind == ContextCentralPlanner ? "central-planner" : "cursor";
        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);
        private static long Read(ref long value) => Interlocked.Read(ref value);
        private Shared.NativeResolution Resolve(ReadOnlySpan<byte> memory, string pattern, int rva, string label) =>
            Shared.NativePatternResolver.ResolveUnique(memory, pattern, rva, true, label, log);
        private static NativeDetour CreateDetour<T>(ulong address, T callback) where T : Delegate =>
            new NativeDetour((IntPtr)unchecked((long)address), Marshal.GetFunctionPointerForDelegate(callback),
                new NativeDetourConfig { ManualApply = true });
        private static void UndoAndDispose(NativeDetour detour, bool applied)
        { if (applied) detour?.Undo(); detour?.Dispose(); }

        private readonly struct RouteContext
        {
            internal RouteContext(int unit, int player, int x, int y, int kind)
            { UnitId = unit; PlayerId = player; TargetX = x; TargetY = y; Kind = kind; }
            internal int UnitId { get; }
            internal int PlayerId { get; }
            internal int TargetX { get; }
            internal int TargetY { get; }
            internal int Kind { get; }
        }
        private readonly struct RouteAnalysis
        {
            internal RouteAnalysis(bool valid, int sx, int sy, int tx, int ty, int length,
                int hit, int gates, int bridges)
            { Valid = valid; StartX = sx; StartY = sy; TargetX = tx; TargetY = ty;
                Length = length; FirstHitTile = hit; GateHits = gates; BridgeHits = bridges; }
            internal bool Valid { get; }
            internal int StartX { get; }
            internal int StartY { get; }
            internal int TargetX { get; }
            internal int TargetY { get; }
            internal int Length { get; }
            internal int FirstHitTile { get; }
            internal int GateHits { get; }
            internal int BridgeHits { get; }
            internal bool CrossesBlocked => GateHits > 0 || BridgeHits > 0;
        }
        private sealed class UnitSnapshot
        {
            internal static readonly UnitSnapshot Empty = new UnitSnapshot(new int[0], new short[0], new short[0]);
            internal UnitSnapshot(int[] owners, short[] xs, short[] ys) { Owners = owners; Xs = xs; Ys = ys; }
            internal int[] Owners { get; }
            internal short[] Xs { get; }
            internal short[] Ys { get; }
            internal bool TryGet(int unit, out int player, out int x, out int y)
            {
                player = x = y = 0;
                if (unit <= 0 || unit >= Owners.Length || unit >= Xs.Length || unit >= Ys.Length) return false;
                player = Owners[unit]; x = Xs[unit]; y = Ys[unit];
                return player > 0 && player <= 8 && x >= 0 && y >= 0;
            }
        }
        private readonly struct PendingDecision
        {
            internal PendingDecision(int kind, int action, RouteContext context, int sx, int sy,
                int tx, int ty, int vanilla, int effective, int vanillaLength, int effectiveLength,
                int hit, int gates, int bridges)
            { Kind = kind; Action = action; Context = context; StartX = sx; StartY = sy; TargetX = tx;
                TargetY = ty; VanillaResult = vanilla; EffectiveResult = effective;
                VanillaLength = vanillaLength; EffectiveLength = effectiveLength; HitTile = hit;
                GateHits = gates; BridgeHits = bridges; }
            internal int Kind { get; }
            internal int Action { get; }
            internal RouteContext Context { get; }
            internal int StartX { get; }
            internal int StartY { get; }
            internal int TargetX { get; }
            internal int TargetY { get; }
            internal int VanillaResult { get; }
            internal int EffectiveResult { get; }
            internal int VanillaLength { get; }
            internal int EffectiveLength { get; }
            internal int HitTile { get; }
            internal int GateHits { get; }
            internal int BridgeHits { get; }
        }
    }
}
