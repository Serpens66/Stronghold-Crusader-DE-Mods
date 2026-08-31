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

namespace EnemyGatePathfindingTest
{
    internal sealed class RouteTilePolicySnapshot
    {
        internal static readonly RouteTilePolicySnapshot Empty = new RouteTilePolicySnapshot(
            new ulong[9][], new ulong[9][], new int[0],
            new Dictionary<int, RouteTileIdentity>(), new bool[9], 0);

        internal RouteTilePolicySnapshot(
            ulong[][] hostileGateBits,
            ulong[][] hostileBridgeBits,
            int[] rowStarts,
            Dictionary<int, RouteTileIdentity> identities,
            bool[] hasBlockedTiles,
            ulong topologyFingerprint)
        {
            HostileGateBits = hostileGateBits ?? new ulong[9][];
            HostileBridgeBits = hostileBridgeBits ?? new ulong[9][];
            RowStarts = rowStarts ?? new int[0];
            Identities = identities ?? new Dictionary<int, RouteTileIdentity>();
            HasBlockedTiles = hasBlockedTiles ?? new bool[9];
            TopologyFingerprint = topologyFingerprint;
        }

        internal ulong[][] HostileGateBits { get; }
        internal ulong[][] HostileBridgeBits { get; }
        internal int[] RowStarts { get; }
        internal Dictionary<int, RouteTileIdentity> Identities { get; }
        internal bool[] HasBlockedTiles { get; }
        internal ulong TopologyFingerprint { get; }

        internal bool IsGateBlocked(int playerId, int tileId) =>
            IsSet(HostileGateBits, playerId, tileId);

        internal bool IsBridgeBlocked(int playerId, int tileId) =>
            IsSet(HostileBridgeBits, playerId, tileId);

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
        {
            GateId = gateId;
            BridgeId = bridgeId;
        }

        internal int GateId { get; }
        internal int BridgeId { get; }

        internal RouteTileIdentity Merge(int gateId, int bridgeId) => new RouteTileIdentity(
            GateId != 0 ? GateId : gateId,
            BridgeId != 0 ? BridgeId : bridgeId);
    }

    internal readonly struct RoutePclCorrelation
    {
        internal RoutePclCorrelation(bool found, int sourcePcl, int targetPcl, long result)
        {
            Found = found;
            SourcePcl = sourcePcl;
            TargetPcl = targetPcl;
            Result = result;
        }

        internal bool Found { get; }
        internal int SourcePcl { get; }
        internal int TargetPcl { get; }
        internal long Result { get; }
    }

    internal sealed unsafe class TileRouteDiagnostics
    {
        // UPDATE REVIEW (CrusaderDE.dll): all delegates below use the Win64 ABI
        // audited for the pinned DLL and must be revalidated after any DLL update.
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CentralMovementPlanDelegate(
            IntPtr unitManager, int unitId, int targetX, int targetY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int MainPathBuilderDelegate(
            IntPtr pathManager, int movementClass, int movementProfile);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AlternatePathBuilderDelegate(IntPtr pathManager);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorReachabilityDelegate(
            IntPtr pathManager, int nativeUnitIndex, int targetX, int targetY);

        private const int MainBuilderKind = 1;
        private const int AlternateBuilderKind = 2;
        private const int ContextUnknown = 0;
        private const int ContextMoveHere = 1;
        private const int ContextCentralPlanner = 2;
        private const int PendingRouteCapacity = 512;
        private const int PendingCursorCapacity = 512;
        private const int MaximumHumanSamples = 48;
        private const int MaximumAiSamples = 64;
        private const int MaximumAiSamplesPerPlayer = 8;
        private const int MaximumNegativeControlSamples = 16;
        private const int MaximumErrorsPerCategory = 8;
        private static readonly long SummaryInterval = Stopwatch.Frequency * 10L;
        private static readonly long OwnerRefreshInterval = Math.Max(1, Stopwatch.Frequency / 4);
        private static readonly long CorrelationWindow = Math.Max(1, Stopwatch.Frequency * 3L / 2L);
        private static readonly int[] DirectionX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] DirectionY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private readonly ManualLogSource log;
        private readonly PendingRoute[] pendingRoutes = new PendingRoute[PendingRouteCapacity];
        private readonly PendingRoute[] drainRoutes = new PendingRoute[PendingRouteCapacity];
        private readonly PendingCursor[] pendingCursors = new PendingCursor[PendingCursorCapacity];
        private readonly PendingCursor[] drainCursors = new PendingCursor[PendingCursorCapacity];
        private readonly PendingCursor[] recentCursors = new PendingCursor[PendingCursorCapacity];
        private readonly int[] aiSamplesByPlayer = new int[9];
        private readonly long[] routesByPlayer = new long[9];
        private volatile RouteTilePolicySnapshot policy = RouteTilePolicySnapshot.Empty;
        private volatile int[] unitOwners = new int[0];
        private Func<long, int, int, int, RoutePclCorrelation> pclCorrelation;
        private Action topologyEpochStarter;

        private CentralMovementPlanDelegate originalCentralPlan;
        private CentralMovementPlanDelegate rootedCentralPlan;
        private MainPathBuilderDelegate originalMainBuilder;
        private MainPathBuilderDelegate rootedMainBuilder;
        private AlternatePathBuilderDelegate originalAlternateBuilder;
        private AlternatePathBuilderDelegate rootedAlternateBuilder;
        private CursorReachabilityDelegate originalCursor;
        private CursorReachabilityDelegate rootedCursor;
        private NativeDetour centralPlanDetour;
        private NativeDetour mainBuilderDetour;
        private NativeDetour alternateBuilderDetour;
        private NativeDetour cursorDetour;

        private long pendingRouteWrite;
        private long pendingRouteRead;
        private int pendingRouteGate;
        private long pendingCursorWrite;
        private long pendingCursorRead;
        private int pendingCursorGate;
        private int recentCursorCount;
        private int recentCursorNext;
        private int epochActive;
        private int epochNumber;
        private long nextSummaryAt;
        private long nextOwnerRefreshAt;
        private int humanSamples;
        private int aiSamples;
        private int negativeSamples;
        private int negativeControlsQueued;
        private int routeErrors;
        private int deferredErrors;

        private long mainBuilderCalls;
        private long alternateBuilderCalls;
        private long positiveBuilderResults;
        private long negativeBuilderResults;
        private long humanRoutes;
        private long aiRoutes;
        private long unknownRoutes;
        private long gateCrossings;
        private long bridgeCrossings;
        private long bothCrossings;
        private long noStructureCrossings;
        private long invalidPathManagers;
        private long invalidPathLengths;
        private long invalidPathBuffers;
        private long undecodableRoutes;
        private long unknownContexts;
        private long unknownBuilderCalls;
        private long cursorCalls;
        private long positiveCursorResults;
        private long negativeCursorResults;
        private long correlatedPcl;
        private long correlatedCursor;
        private long droppedRoutes;
        private long droppedCursors;

        [ThreadStatic]
        private static RouteContext activeContext;
        [ThreadStatic]
        private static int moveHereDepth;

        internal TileRouteDiagnostics(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool installNativeHooks)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (!installNativeHooks)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "Tile-route native diagnostics were not installed because MoveMoatTest_Serp is loaded; " +
                    "the existing PCL and topology diagnostics remain active without overlapping hooks.");
                return;
            }

            Shared.NativeResolution plan = Resolve(memory,
                EnemyGatePathfindingNativeDefinition.CentralMovementPlanPattern,
                EnemyGatePathfindingNativeDefinition.CentralMovementPlanRva,
                "central per-unit movement planner");
            Shared.NativeResolution main = Resolve(memory,
                EnemyGatePathfindingNativeDefinition.MainPathBuilderPattern,
                EnemyGatePathfindingNativeDefinition.MainPathBuilderRva,
                "main tile path builder");
            Shared.NativeResolution alternate = Resolve(memory,
                EnemyGatePathfindingNativeDefinition.AlternatePathBuilderPattern,
                EnemyGatePathfindingNativeDefinition.AlternatePathBuilderRva,
                "alternate tile path builder");
            Shared.NativeResolution cursor = Resolve(memory,
                EnemyGatePathfindingNativeDefinition.CursorReachabilityPattern,
                EnemyGatePathfindingNativeDefinition.CursorReachabilityRva,
                "ordinary movement cursor reachability");

            rootedCentralPlan = ObserveCentralPlan;
            rootedMainBuilder = ObserveMainBuilder;
            rootedAlternateBuilder = ObserveAlternateBuilder;
            rootedCursor = ObserveCursor;
            NativeDetour pendingPlan = null;
            NativeDetour pendingMain = null;
            NativeDetour pendingAlternate = null;
            NativeDetour pendingCursor = null;
            bool planApplied = false;
            bool mainApplied = false;
            bool alternateApplied = false;
            bool cursorApplied = false;
            try
            {
                pendingPlan = CreateDetour(libraryBase + unchecked((ulong)plan.Rva), rootedCentralPlan);
                originalCentralPlan = pendingPlan.GenerateTrampoline<CentralMovementPlanDelegate>();
                pendingMain = CreateDetour(libraryBase + unchecked((ulong)main.Rva), rootedMainBuilder);
                originalMainBuilder = pendingMain.GenerateTrampoline<MainPathBuilderDelegate>();
                pendingAlternate = CreateDetour(
                    libraryBase + unchecked((ulong)alternate.Rva), rootedAlternateBuilder);
                originalAlternateBuilder = pendingAlternate.GenerateTrampoline<AlternatePathBuilderDelegate>();
                pendingCursor = CreateDetour(libraryBase + unchecked((ulong)cursor.Rva), rootedCursor);
                originalCursor = pendingCursor.GenerateTrampoline<CursorReachabilityDelegate>();

                pendingPlan.Apply(); planApplied = true;
                pendingMain.Apply(); mainApplied = true;
                pendingAlternate.Apply(); alternateApplied = true;
                pendingCursor.Apply(); cursorApplied = true;
                centralPlanDetour = pendingPlan;
                mainBuilderDetour = pendingMain;
                alternateBuilderDetour = pendingAlternate;
                cursorDetour = pendingCursor;
            }
            catch
            {
                UndoAndDispose(pendingCursor, cursorApplied);
                UndoAndDispose(pendingAlternate, alternateApplied);
                UndoAndDispose(pendingMain, mainApplied);
                UndoAndDispose(pendingPlan, planApplied);
                throw;
            }

            Shared.DebugLogHelper.LogInfo(log,
                "Observational tile-route hooks installed: " +
                $"centralPlan=0x{plan.Rva:X} ({plan.Method}), mainBuilder=0x{main.Rva:X} ({main.Method}), " +
                $"alternateBuilder=0x{alternate.Rva:X} ({alternate.Method}), cursor=0x{cursor.Rva:X} ({cursor.Method}); " +
                "all Vanilla return values and path buffers remain unchanged.");
        }

        internal bool HooksInstalled => centralPlanDetour != null && mainBuilderDetour != null &&
            alternateBuilderDetour != null && cursorDetour != null;

        internal void SetPclCorrelation(
            Func<long, int, int, int, RoutePclCorrelation> correlation) =>
            pclCorrelation = correlation;

        internal void SetTopologyEpochStarter(Action starter) => topologyEpochStarter = starter;

        internal void UpdatePolicy(RouteTilePolicySnapshot updated)
        {
            if (updated != null)
                policy = updated;
        }

        internal void BeginEpoch(string reason)
        {
            if (Interlocked.CompareExchange(ref epochActive, 1, 0) != 0)
                return;
            topologyEpochStarter?.Invoke();
            Interlocked.Increment(ref epochNumber);
            ResetCounters();
        }

        internal void EndEpoch(string reason)
        {
            if (Volatile.Read(ref epochActive) == 0)
                return;
            ProcessDeferred();
            if (Interlocked.CompareExchange(ref epochActive, 0, 1) != 1)
                return;
            LogSummary("final", reason ?? "unspecified");
        }

        internal void OnMoveHere(UnitMoveHereEventArgs args)
        {
            // UPDATE REVIEW (Script Extender 1.42.0): Pre/Post must continue to
            // synchronously enclose the native MoveHere builder calls.
            if (args == null || !HooksInstalled)
                return;
            if (args.Phase == EventHookPhase.Pre)
            {
                EnsureEpoch();
                moveHereDepth++;
                activeContext = new RouteContext(
                    args.UnitId, ReadUnitOwner(args.UnitId), args.TileX, args.TileY, ContextMoveHere);
            }
            else if (args.Phase == EventHookPhase.Post && moveHereDepth > 0)
            {
                moveHereDepth--;
                if (moveHereDepth == 0)
                    activeContext = default;
            }
        }

        internal void ProcessDeferred()
        {
            if (!HooksInstalled)
                return;
            try
            {
                long now = Stopwatch.GetTimestamp();
                RefreshUnitOwnersIfDue(now);
                if (Volatile.Read(ref epochActive) == 0)
                    return;
                DrainCursors();
                int routeCount = DrainRoutes();
                for (int index = 0; index < routeCount; index++)
                    ProcessRoute(drainRoutes[index]);
                if (now >= Volatile.Read(ref nextSummaryAt))
                {
                    Volatile.Write(ref nextSummaryAt, now + SummaryInterval);
                    LogSummary("periodic", "10-second interval");
                }
            }
            catch (Exception ex)
            {
                int count = Interlocked.Increment(ref deferredErrors);
                if (count <= MaximumErrorsPerCategory)
                {
                    Shared.DebugLogHelper.LogWarning(log,
                        $"Deferred tile-route diagnostics failed ({count}/{MaximumErrorsPerCategory}) " +
                        $"without changing game state: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private int ObserveCentralPlan(IntPtr unitManager, int unitId, int targetX, int targetY)
        {
            RouteContext previous = activeContext;
            bool replace = moveHereDepth == 0;
            if (replace)
            {
                EnsureEpoch();
                activeContext = new RouteContext(
                    unitId, ReadUnitOwner(unitId), targetX, targetY, ContextCentralPlanner);
            }
            try
            {
                return originalCentralPlan(unitManager, unitId, targetX, targetY);
            }
            finally
            {
                if (replace)
                    activeContext = previous;
            }
        }

        private int ObserveMainBuilder(IntPtr pathManager, int movementClass, int movementProfile)
        {
            EnsureEpoch();
            Interlocked.Increment(ref mainBuilderCalls);
            RecordBuilderRole();
            int result = originalMainBuilder(pathManager, movementClass, movementProfile);
            ObserveBuilderResult(pathManager, result, MainBuilderKind);
            return result;
        }

        private int ObserveAlternateBuilder(IntPtr pathManager)
        {
            EnsureEpoch();
            Interlocked.Increment(ref alternateBuilderCalls);
            RecordBuilderRole();
            int result = originalAlternateBuilder(pathManager);
            ObserveBuilderResult(pathManager, result, AlternateBuilderKind);
            return result;
        }

        private int ObserveCursor(IntPtr pathManager, int nativeUnitIndex, int targetX, int targetY)
        {
            EnsureEpoch();
            int result = originalCursor(pathManager, nativeUnitIndex, targetX, targetY);
            Interlocked.Increment(ref cursorCalls);
            if (result == 0) Interlocked.Increment(ref negativeCursorResults);
            else Interlocked.Increment(ref positiveCursorResults);
            QueueCursor(new PendingCursor(
                Stopwatch.GetTimestamp(), nativeUnitIndex, ReadUnitOwner(nativeUnitIndex),
                targetX, targetY, result));
            return result;
        }

        private void ObserveBuilderResult(IntPtr pathManager, int result, int builderKind)
        {
            // UPDATE REVIEW (CrusaderDE.dll): PathManager offsets, packed-nibble
            // order, direction vectors and the 2000-step limit are DLL-specific.
            if (result <= 0)
            {
                Interlocked.Increment(ref negativeBuilderResults);
                return;
            }
            Interlocked.Increment(ref positiveBuilderResults);
            try
            {
                if (pathManager == IntPtr.Zero)
                {
                    Interlocked.Increment(ref invalidPathManagers);
                    return;
                }
                byte* manager = (byte*)pathManager;
                int length = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathLengthOffset);
                if (length <= 0 || length > EnemyGatePathfindingNativeDefinition.MaximumDecodedPathLength)
                {
                    Interlocked.Increment(ref invalidPathLengths);
                    return;
                }
                byte* directions = *(byte**)(manager +
                    EnemyGatePathfindingNativeDefinition.PathDirectionBufferOffset);
                if (directions == null)
                {
                    Interlocked.Increment(ref invalidPathBuffers);
                    return;
                }
                int startX = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathStartXOffset);
                int startY = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathStartYOffset);
                int targetX = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathTargetXOffset);
                int targetY = *(int*)(manager + EnemyGatePathfindingNativeDefinition.PathTargetYOffset);
                RouteContext context = activeContext;
                if (context.PlayerId <= 0 || context.PlayerId > 8)
                {
                    Interlocked.Increment(ref unknownContexts);
                    return;
                }
                RouteTilePolicySnapshot current = policy;
                if (context.PlayerId >= current.HasBlockedTiles.Length ||
                    !current.HasBlockedTiles[context.PlayerId])
                {
                    Interlocked.Increment(ref noStructureCrossings);
                    return;
                }
                if (!TrySelectVariant(directions, length, startX, startY, targetX, targetY,
                        out bool fromTarget, out bool invert))
                {
                    Interlocked.Increment(ref undecodableRoutes);
                    return;
                }
                int x = fromTarget ? targetX : startX;
                int y = fromTarget ? targetY : startY;
                int firstHit = 0;
                int lastHit = 0;
                int gateHits = 0;
                int bridgeHits = 0;
                for (int step = -1; step < length; step++)
                {
                    if (step >= 0)
                    {
                        int direction = ReadDirection(directions, step);
                        int sign = invert ? -1 : 1;
                        x += DirectionX[direction] * sign;
                        y += DirectionY[direction] * sign;
                    }
                    int tileId = GetTileId(current.RowStarts, x, y);
                    if (tileId < 0)
                    {
                        Interlocked.Increment(ref undecodableRoutes);
                        return;
                    }
                    bool gate = current.IsGateBlocked(context.PlayerId, tileId);
                    bool bridge = current.IsBridgeBlocked(context.PlayerId, tileId);
                    if (!gate && !bridge)
                        continue;
                    if (firstHit == 0) firstHit = tileId;
                    lastHit = tileId;
                    if (gate) gateHits++;
                    if (bridge) bridgeHits++;
                }
                if (gateHits > 0 && bridgeHits > 0) Interlocked.Increment(ref bothCrossings);
                else if (gateHits > 0) Interlocked.Increment(ref gateCrossings);
                else if (bridgeHits > 0) Interlocked.Increment(ref bridgeCrossings);
                else
                {
                    Interlocked.Increment(ref noStructureCrossings);
                    if (Interlocked.Increment(ref negativeControlsQueued) > MaximumNegativeControlSamples)
                        return;
                }
                QueueRoute(new PendingRoute(
                    Stopwatch.GetTimestamp(), builderKind, context, startX, startY,
                    targetX, targetY, result, length, firstHit, lastHit,
                    gateHits, bridgeHits, gateHits + bridgeHits, true));
            }
            catch
            {
                Interlocked.Increment(ref routeErrors);
            }
        }

        private void ProcessRoute(PendingRoute route)
        {
            int role = ResolveRole(route.Context.PlayerId);

            RoutePclCorrelation pcl = pclCorrelation == null
                ? default
                : pclCorrelation(route.Timestamp, route.Context.PlayerId, route.TargetX, route.TargetY);
            if (pcl.Found) Interlocked.Increment(ref correlatedPcl);
            PendingCursor cursor = FindRecentCursor(route);
            if (cursor.Timestamp != 0) Interlocked.Increment(ref correlatedCursor);

            if (!ShouldLogSample(route, role))
                return;
            RouteTileIdentity firstIdentity = default;
            RouteTileIdentity lastIdentity = default;
            RouteTilePolicySnapshot current = policy;
            if (route.FirstHitTile > 0) current.TryGetIdentity(route.FirstHitTile, out firstIdentity);
            if (route.LastHitTile > 0) current.TryGetIdentity(route.LastHitTile, out lastIdentity);
            Shared.DebugLogHelper.LogInfo(log,
                "Tile-route diagnostic sample: " +
                $"role={FormatRole(role)}, player={route.Context.PlayerId}, unit={route.Context.UnitId}, " +
                $"context={FormatContext(route.Context.Kind)}, builder={FormatBuilder(route.BuilderKind)}, " +
                $"source={route.StartX}/{route.StartY}, target={route.TargetX}/{route.TargetY}, " +
                $"result={route.Result}, length={route.PathLength}, gateHits={route.GateHits}, " +
                $"bridgeHits={route.BridgeHits}, firstHit={route.FirstHitTile}" +
                $"(gate#{firstIdentity.GateId},bridge#{firstIdentity.BridgeId}), lastHit={route.LastHitTile}" +
                $"(gate#{lastIdentity.GateId},bridge#{lastIdentity.BridgeId}), " +
                $"pclCorrelation={(pcl.Found ? pcl.SourcePcl + "/" + pcl.TargetPcl + "/result=" + pcl.Result : "none")}, " +
                $"cursorCorrelation={(cursor.Timestamp != 0 ? cursor.Result + "@" + cursor.TargetX + "/" + cursor.TargetY : "none")}." );
        }

        private bool ShouldLogSample(PendingRoute route, int role)
        {
            bool relevant = route.GateHits > 0 || route.BridgeHits > 0;
            if (!relevant)
                return Interlocked.Increment(ref negativeSamples) <= MaximumNegativeControlSamples;
            if (role == SamePclBridgeDiagnostics.HumanRole)
                return Interlocked.Increment(ref humanSamples) <= MaximumHumanSamples;
            if (role != SamePclBridgeDiagnostics.AiRole)
                return false;
            int player = route.Context.PlayerId;
            if (player <= 0 || player >= aiSamplesByPlayer.Length ||
                Interlocked.Increment(ref aiSamplesByPlayer[player]) > MaximumAiSamplesPerPlayer)
                return false;
            return Interlocked.Increment(ref aiSamples) <= MaximumAiSamples;
        }

        private PendingCursor FindRecentCursor(PendingRoute route)
        {
            PendingCursor best = default;
            long bestAge = long.MaxValue;
            for (int index = 0; index < recentCursorCount; index++)
            {
                PendingCursor cursor = recentCursors[index];
                long age = route.Timestamp - cursor.Timestamp;
                if (age < 0 || age > CorrelationWindow || age >= bestAge ||
                    cursor.PlayerId != route.Context.PlayerId ||
                    cursor.TargetX != route.TargetX || cursor.TargetY != route.TargetY)
                    continue;
                best = cursor;
                bestAge = age;
            }
            return best;
        }

        private void RefreshUnitOwnersIfDue(long now)
        {
            // UPDATE REVIEW (Script Extender 1.42.0): unit IDs are one-based while
            // GetUnitsAsSpan is zero-based; owner means r_ControllableForPlayerId.
            if (now < Volatile.Read(ref nextOwnerRefreshAt))
                return;
            Volatile.Write(ref nextOwnerRefreshAt, now + OwnerRefreshInterval);
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            int[] owners = new int[units.Length + 1];
            for (int index = 0; index < units.Length; index++)
                owners[index + 1] = units[index].r_ControllableForPlayerId;
            unitOwners = owners;
        }

        private int ReadUnitOwner(int unitId)
        {
            int[] owners = unitOwners;
            return unitId > 0 && unitId < owners.Length ? owners[unitId] : 0;
        }

        private void QueueRoute(PendingRoute route)
        {
            if (Interlocked.CompareExchange(ref pendingRouteGate, 1, 0) != 0)
            {
                Interlocked.Increment(ref droppedRoutes);
                return;
            }
            try
            {
                long write = pendingRouteWrite;
                if (write - pendingRouteRead >= PendingRouteCapacity)
                {
                    Interlocked.Increment(ref droppedRoutes);
                    return;
                }
                pendingRoutes[(int)(write % PendingRouteCapacity)] = route;
                pendingRouteWrite = write + 1;
            }
            finally { Volatile.Write(ref pendingRouteGate, 0); }
        }

        private void QueueCursor(PendingCursor cursor)
        {
            if (Interlocked.CompareExchange(ref pendingCursorGate, 1, 0) != 0)
            {
                Interlocked.Increment(ref droppedCursors);
                return;
            }
            try
            {
                long write = pendingCursorWrite;
                if (write - pendingCursorRead >= PendingCursorCapacity)
                {
                    Interlocked.Increment(ref droppedCursors);
                    return;
                }
                pendingCursors[(int)(write % PendingCursorCapacity)] = cursor;
                pendingCursorWrite = write + 1;
            }
            finally { Volatile.Write(ref pendingCursorGate, 0); }
        }

        private int DrainRoutes()
        {
            if (Interlocked.CompareExchange(ref pendingRouteGate, 1, 0) != 0)
                return 0;
            try
            {
                long read = pendingRouteRead;
                long write = pendingRouteWrite;
                int count = (int)Math.Min(PendingRouteCapacity, Math.Max(0, write - read));
                for (int index = 0; index < count; index++)
                    drainRoutes[index] = pendingRoutes[(int)((read + index) % PendingRouteCapacity)];
                pendingRouteRead = read + count;
                return count;
            }
            finally { Volatile.Write(ref pendingRouteGate, 0); }
        }

        private void DrainCursors()
        {
            if (Interlocked.CompareExchange(ref pendingCursorGate, 1, 0) != 0)
                return;
            try
            {
                long read = pendingCursorRead;
                long write = pendingCursorWrite;
                int count = (int)Math.Min(PendingCursorCapacity, Math.Max(0, write - read));
                for (int index = 0; index < count; index++)
                {
                    PendingCursor cursor = pendingCursors[(int)((read + index) % PendingCursorCapacity)];
                    drainCursors[index] = cursor;
                    recentCursors[recentCursorNext] = cursor;
                    recentCursorNext = (recentCursorNext + 1) % PendingCursorCapacity;
                    if (recentCursorCount < PendingCursorCapacity) recentCursorCount++;
                }
                pendingCursorRead = read + count;
            }
            finally { Volatile.Write(ref pendingCursorGate, 0); }
        }

        private static bool TrySelectVariant(
            byte* directions, int length, int startX, int startY, int targetX, int targetY,
            out bool fromTarget, out bool invert)
        {
            fromTarget = false;
            invert = false;
            for (int variant = 0; variant < 4; variant++)
            {
                bool candidateFromTarget = (variant & 2) != 0;
                bool candidateInvert = (variant & 1) != 0;
                int x = candidateFromTarget ? targetX : startX;
                int y = candidateFromTarget ? targetY : startY;
                bool valid = true;
                for (int step = 0; step < length; step++)
                {
                    int direction = ReadDirection(directions, step);
                    if (direction > 7)
                    {
                        valid = false;
                        break;
                    }
                    int sign = candidateInvert ? -1 : 1;
                    x += DirectionX[direction] * sign;
                    y += DirectionY[direction] * sign;
                    if (x < 0 || x >= EnemyGatePathfindingNativeDefinition.MapGridWidth ||
                        y < 0 || y >= EnemyGatePathfindingNativeDefinition.MapGridWidth)
                    {
                        valid = false;
                        break;
                    }
                }
                int expectedX = candidateFromTarget ? startX : targetX;
                int expectedY = candidateFromTarget ? startY : targetY;
                if (valid && x == expectedX && y == expectedY)
                {
                    fromTarget = candidateFromTarget;
                    invert = candidateInvert;
                    return true;
                }
            }
            return false;
        }

        private static int ReadDirection(byte* directions, int step) =>
            (directions[step >> 1] >> ((step & 1) * 4)) & 0x0F;

        private static int GetTileId(int[] rowStarts, int x, int y)
        {
            if (rowStarts == null || y < 0 || y >= rowStarts.Length || x < 0 ||
                x >= EnemyGatePathfindingNativeDefinition.MapGridWidth)
                return -1;
            int tileId = rowStarts[y] + x;
            return tileId >= 0 && tileId < EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive
                ? tileId : -1;
        }

        private void EnsureEpoch()
        {
            if (Volatile.Read(ref epochActive) == 0)
                BeginEpoch("first native route or cursor query; supports map editor");
        }

        private void ResetCounters()
        {
            pendingRouteWrite = pendingRouteRead = 0;
            pendingCursorWrite = pendingCursorRead = 0;
            recentCursorCount = recentCursorNext = 0;
            humanSamples = aiSamples = negativeSamples = negativeControlsQueued = 0;
            Array.Clear(aiSamplesByPlayer, 0, aiSamplesByPlayer.Length);
            Array.Clear(routesByPlayer, 0, routesByPlayer.Length);
            Reset(ref mainBuilderCalls); Reset(ref alternateBuilderCalls);
            Reset(ref positiveBuilderResults); Reset(ref negativeBuilderResults);
            Reset(ref humanRoutes); Reset(ref aiRoutes); Reset(ref unknownRoutes);
            Reset(ref gateCrossings); Reset(ref bridgeCrossings); Reset(ref bothCrossings);
            Reset(ref noStructureCrossings); Reset(ref invalidPathManagers);
            Reset(ref invalidPathLengths); Reset(ref invalidPathBuffers); Reset(ref undecodableRoutes);
            Reset(ref unknownContexts); Reset(ref unknownBuilderCalls);
            Reset(ref cursorCalls); Reset(ref positiveCursorResults);
            Reset(ref negativeCursorResults); Reset(ref correlatedPcl); Reset(ref correlatedCursor);
            Reset(ref droppedRoutes); Reset(ref droppedCursors);
            routeErrors = deferredErrors = 0;
            long now = Stopwatch.GetTimestamp();
            nextSummaryAt = now + SummaryInterval;
            nextOwnerRefreshAt = 0;
        }

        private void LogSummary(string kind, string reason)
        {
            RefreshRouteRoleCounters();
            Shared.DebugLogHelper.LogInfo(log,
                $"Tile-route {kind} summary: epoch={epochNumber}, reason={reason}, " +
                $"builders(main={Read(ref mainBuilderCalls)},alternate={Read(ref alternateBuilderCalls)}," +
                $"positive={Read(ref positiveBuilderResults)},negative={Read(ref negativeBuilderResults)}), " +
                $"roles(human={Read(ref humanRoutes)},ai={Read(ref aiRoutes)},unknown={Read(ref unknownRoutes)}), " +
                $"crossings(gateOnly={Read(ref gateCrossings)},bridgeOnly={Read(ref bridgeCrossings)}," +
                $"both={Read(ref bothCrossings)},none={Read(ref noStructureCrossings)}), " +
                $"cursor(calls={Read(ref cursorCalls)},positive={Read(ref positiveCursorResults)}," +
                $"negative={Read(ref negativeCursorResults)}), correlations(pcl={Read(ref correlatedPcl)}," +
                $"cursor={Read(ref correlatedCursor)}), failOpen(pathManager={Read(ref invalidPathManagers)}," +
                $"length={Read(ref invalidPathLengths)},buffer={Read(ref invalidPathBuffers)}," +
                $"decode={Read(ref undecodableRoutes)},context={Read(ref unknownContexts)}), " +
                $"dropped(route={Read(ref droppedRoutes)},cursor={Read(ref droppedCursors)}), " +
                $"errors(route={routeErrors},deferred={deferredErrors}), " +
                $"policyFingerprint=0x{policy.TopologyFingerprint:X16}.");
        }

        private void RefreshRouteRoleCounters()
        {
            long human = 0;
            long ai = 0;
            long unknown = Read(ref unknownBuilderCalls);
            for (int player = 1; player <= 8; player++)
            {
                long count = Read(ref routesByPlayer[player]);
                int role = ResolveRole(player);
                if (role == SamePclBridgeDiagnostics.HumanRole) human += count;
                else if (role == SamePclBridgeDiagnostics.AiRole) ai += count;
                else unknown += count;
            }
            Interlocked.Exchange(ref humanRoutes, human);
            Interlocked.Exchange(ref aiRoutes, ai);
            Interlocked.Exchange(ref unknownRoutes, unknown);
        }

        private void RecordBuilderRole()
        {
            int playerId = activeContext.PlayerId;
            if (playerId > 0 && playerId < routesByPlayer.Length)
                Interlocked.Increment(ref routesByPlayer[playerId]);
            else
                Interlocked.Increment(ref unknownBuilderCalls);
        }

        private static int ResolveRole(int playerId)
        {
            try
            {
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                if (!players.IsPlayerIdValid(playerId)) return SamePclBridgeDiagnostics.UnknownRole;
                return players.IsAIPlayer(playerId)
                    ? SamePclBridgeDiagnostics.AiRole : SamePclBridgeDiagnostics.HumanRole;
            }
            catch { return SamePclBridgeDiagnostics.UnknownRole; }
        }

        private static string FormatRole(int role) => role == SamePclBridgeDiagnostics.HumanRole
            ? "human" : role == SamePclBridgeDiagnostics.AiRole ? "ai" : "unknown";
        private static string FormatBuilder(int kind) => kind == MainBuilderKind ? "main-F4930" : "alternate-E32B0";
        private static string FormatContext(int kind) => kind == ContextMoveHere
            ? "MoveHere" : kind == ContextCentralPlanner ? "central-planner" : "unknown";
        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);
        private static long Read(ref long value) => Interlocked.Read(ref value);

        private Shared.NativeResolution Resolve(
            ReadOnlySpan<byte> memory, string pattern, int rva, string label) =>
            Shared.NativePatternResolver.ResolveUnique(
                memory, pattern, rva, referenceHashMatches: true, label, log);

        private static NativeDetour CreateDetour<TDelegate>(ulong address, TDelegate callback)
            where TDelegate : Delegate => new NativeDetour(
                (IntPtr)unchecked((long)address),
                Marshal.GetFunctionPointerForDelegate(callback),
                new NativeDetourConfig { ManualApply = true });

        private static void UndoAndDispose(NativeDetour detour, bool applied)
        {
            if (applied) detour?.Undo();
            detour?.Dispose();
        }

        private readonly struct RouteContext
        {
            internal RouteContext(int unitId, int playerId, int targetX, int targetY, int kind)
            { UnitId = unitId; PlayerId = playerId; TargetX = targetX; TargetY = targetY; Kind = kind; }
            internal int UnitId { get; }
            internal int PlayerId { get; }
            internal int TargetX { get; }
            internal int TargetY { get; }
            internal int Kind { get; }
        }

        private readonly struct PendingRoute
        {
            internal PendingRoute(long timestamp, int builderKind, RouteContext context,
                int startX, int startY, int targetX, int targetY, int result, int pathLength,
                int firstHitTile, int lastHitTile, int gateHits, int bridgeHits,
                int totalHits, bool decoded)
            {
                Timestamp = timestamp; BuilderKind = builderKind; Context = context;
                StartX = startX; StartY = startY; TargetX = targetX; TargetY = targetY;
                Result = result; PathLength = pathLength; FirstHitTile = firstHitTile;
                LastHitTile = lastHitTile; GateHits = gateHits; BridgeHits = bridgeHits;
                TotalHits = totalHits; Decoded = decoded;
            }
            internal long Timestamp { get; }
            internal int BuilderKind { get; }
            internal RouteContext Context { get; }
            internal int StartX { get; }
            internal int StartY { get; }
            internal int TargetX { get; }
            internal int TargetY { get; }
            internal int Result { get; }
            internal int PathLength { get; }
            internal int FirstHitTile { get; }
            internal int LastHitTile { get; }
            internal int GateHits { get; }
            internal int BridgeHits { get; }
            internal int TotalHits { get; }
            internal bool Decoded { get; }
        }

        private readonly struct PendingCursor
        {
            internal PendingCursor(long timestamp, int unitId, int playerId,
                int targetX, int targetY, int result)
            { Timestamp = timestamp; UnitId = unitId; PlayerId = playerId;
                TargetX = targetX; TargetY = targetY; Result = result; }
            internal long Timestamp { get; }
            internal int UnitId { get; }
            internal int PlayerId { get; }
            internal int TargetX { get; }
            internal int TargetY { get; }
            internal int Result { get; }
        }
    }
}
