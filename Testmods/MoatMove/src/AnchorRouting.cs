extern alias BugfixesRuntime;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SHCDESE.API;
using SHCDESE.Interop;

namespace MoatMove
{
    // Anchor mode (ANCHOR_MODE_PLAN.md, phase 4): every group move passes 11B520 (classic and
    // target orders, BFQoL formations, queue and patrol legs). Its Pre event computes one core
    // task for the group synchronously; inside the call every capable unit asks the core first:
    // a cheaper moat route qualifies in the mode probe, a ground decision keeps Vanilla's mode
    // and the builder publishes the core's ground route instead of running Vanilla's search.
    // Whatever the core cannot answer keeps the Fast/Vanilla chain. Attacks, work and cursor
    // queries keep Fast's rules. Until a player's mask is complete, Fast applies.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        // Private C ABI of native/MoatMoveAnchor.cpp (contract 0x04000008). The DLL is loaded
        // once by full path from the plugin folder and stays loaded for the process.
        private static class AnchorNative
        {
            internal const string Library = "MoatMoveAnchor.dll";
            internal const int Contract = 0x04000008, Ready = 0, Unreachable = 1, OverLimit = 2, Pending = 3, Stale = 4, Invalid = 5, Failed = 6;
            [StructLayout(LayoutKind.Sequential)] internal struct QueryResult { internal int Status, Ground, Moat, Length, Flags, Reserved; internal long Cost; }
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_contract();
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_recommended_threads();
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_create(int width, int height, long limit, out IntPtr context);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_mask_begin(IntPtr context, int player);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_mask_write(IntPtr context, int player, int first, int count, uint[] words, out int changed);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_mask_update(IntPtr context, int player, int[] nodes, uint[] words, int count, out int changed);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_task_create(IntPtr context, int player, int anchor, int ground, int moat, int[] starts, int count, out int id);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_task_restart(IntPtr context, int id);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_task_release(IntPtr context, int id);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_task_status(IntPtr context, int id);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_step(IntPtr context, int maxTasks, int threads, out int computed);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_query(IntPtr context, int id, int start, int slot, int margin, [Out] byte[] bytes, int capacity, out QueryResult result);
            // count units of one task: parallel pass, then deferred sub-anchor queries in caller order;
            // unit i writes its steps to bytes[i * stride]. Same answers as count single queries.
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_query_batch(IntPtr context, int id, int[] starts, int[] slots, int count, int margin, int threads,
                [Out] byte[] bytes, int stride, [Out] QueryResult[] results);
            // Core counters (StatIndex order of MoatMoveAnchor.cpp); reset after each read.
            internal const int StatCount = 21, StatFieldReuses = 20, StatQueries = 4, StatViaChosen = 5, StatSubAnchors = 6, StatSubCapped = 7, StatStepNs = 8, StatQueryNs = 9, StatSubNs = 10,
                StatSettles = 2, StatPeakBytes = 13, StatAnchorNoStart = 16, StatAnchorNoEntry = 17, StatAnchorUnreached = 18, StatAnchorTooFar = 19;
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_stats(IntPtr context, [Out] long[] data, int count, int reset);
            // Throws when the file is missing or another core version is installed.
            internal static void Load(string directory)
            {
                string path = Path.Combine(directory, Library);
                if (!File.Exists(path)) throw new FileNotFoundException("Anchor core missing.", path);
                if (LoadLibraryEx(path, IntPtr.Zero, 0x00000008) == IntPtr.Zero) throw new InvalidOperationException("Anchor core load failed: error " + Marshal.GetLastWin32Error());
                if (mma_contract() != Contract || Marshal.SizeOf(typeof(QueryResult)) != 32) throw new InvalidOperationException("Anchor core contract mismatch.");
            }
        }

        // Deterministic per-tick budgets: mask preparation is spread over ticks and never
        // depends on elapsed time, so every peer switches from Fast to anchor in the same tick.
        // Resweeps of ready masks get a smaller share: AI games change gates and access every few
        // seconds, and a full resweep of eight masks would otherwise cost ~5 ms every tick. Stale
        // mask parts never publish anything unsafe, because every route passes the live audit.
        private const int AnchorMaskNodesPerTick = 4096, AnchorResweepNodesPerTick = 1024, AnchorMargin = 8, AnchorMaxCost = 255;
        // Marked nodes re-read per player and tick; large terrain bursts finish over later ticks
        // (stale words are caught by the live audit of every published route).
        private const int AnchorRefreshNodesPerTick = 2048;
        private long anchorResweepDiplomacy, anchorResweepPortals, anchorResweepFootprint, anchorResweepGlobal, anchorRefreshBacklog;
        // Vanilla builder calls inside anchor group moves: count, failures and time, and those that
        // follow an exact core "unreachable" for the same unit (found: Vanilla had a route anyway).
        private long anchorVanillaCalls, anchorVanillaFails, anchorVanillaTicks, anchorVanillaFailTicks,
            anchorVanillaAfterUnreachable, anchorVanillaAfterUnreachableTicks, anchorVanillaFoundAfterUnreachable;
        // Fields computed again in the same tick for the same player, anchor and cost profile
        // (reuse candidates; several AI groups sent to one rally point).
        private readonly HashSet<long> anchorTickTaskKeys = new HashSet<long>();
        private int anchorTickTaskKeysTick = -1;
        private long anchorSameTickTasks, anchorSameTickTaskTicks, anchorDiggerGroundRoutes, anchorRebuildsWatched;
        // Managed reachability searches per owner kind: advances, expanded cells, time, longest advance.
        private string reachabilityOwnerKind = "unscoped";
        private readonly Dictionary<string, long[]> reachabilityByOwner = new Dictionary<string, long[]>();
        private int reachabilitySlowLogs, reachabilitySlowTick = -1, reachabilitySlowInTick;
        private void RecordReachabilityAdvance(long elapsed, int expanded, int targetCell, int targetRegion, bool requireMoat, bool exhausted)
        {
            if (!reachabilityByOwner.TryGetValue(reachabilityOwnerKind, out long[] row))
            { if (reachabilityByOwner.Count >= 16) return; reachabilityByOwner[reachabilityOwnerKind] = row = new long[4]; }
            row[0]++; row[1] += expanded; row[2] += elapsed; row[3] = Math.Max(row[3], elapsed);
            // Slow advances (> 15 ms), bounded per map: query shape and whether the frontier ran out.
            if (elapsed * 1000 < 15 * System.Diagnostics.Stopwatch.Frequency || reachabilitySlowLogs >= 40) return;
            int tick = CaptureCurrentGameTick();
            if (tick != reachabilitySlowTick) { reachabilitySlowTick = tick; reachabilitySlowInTick = 0; }
            reachabilitySlowLogs++; reachabilitySlowInTick++;
            Shared.DebugLogHelper.LogInfo(log, $"MoatMove reachability-slow owner={reachabilityOwnerKind} player={runtime.cachePlayerId} " +
                $"start={runtime.cacheStartX},{runtime.cacheStartY} ms={AnchorMs(elapsed)} expanded={expanded} totalExpanded={reachabilityQueueHead} " +
                $"targetCell={(targetCell < 0 ? "none" : (targetCell % MapWidth) + "," + (targetCell / MapWidth))} targetRegion={targetRegion} requireMoat={requireMoat} " +
                $"exhausted={exhausted} tick={tick} slowInTick={reachabilitySlowInTick}.");
        }
        private string FormatReachabilityByOwner()
        {
            var text = new System.Text.StringBuilder();
            foreach (var entry in reachabilityByOwner)
            {
                if (text.Length != 0) text.Append(',');
                text.Append(entry.Key).Append(':').Append(entry.Value[0]).Append('/').Append(entry.Value[1]).Append('/')
                    .Append(AnchorMs(entry.Value[2])).Append('/').Append(AnchorMs(entry.Value[3]));
            }
            return text.ToString();
        }
        // Why the anchor left the current builder call to Vanilla (set at each decline point,
        // counted per Vanilla call inside an anchor move as vanillaWhy).
        private string anchorDecline = "none";
        private readonly Dictionary<string, long> anchorVanillaWhy = new Dictionary<string, long>();
        private int anchorUnreachableUnit, anchorUnreachableStart, anchorUnreachableTarget, anchorUnreachableTask, anchorUnreachableLogs;
        private const long AnchorMemoryLimit = 768L * 1024 * 1024;
        private IntPtr anchorContext;
        private bool anchorAvailable, anchorPlayersRequested;
        // anchorSimThread: thread of the simulation tick; group moves elsewhere keep Vanilla.
        // anchorDispatchTask: field of the group's moat-capable members; anchorGroundTask:
        // ground-only field of its members that cannot enter moats.
        private int anchorThreads = 1, anchorDispatchTask, anchorGroundTask, anchorSimThread = -1;
        // One frame per group move (11B520) in progress; nested moves stack. A frame without Post
        // (a later subscriber skipped the original) is released by the next move end or tick.
        // Members/Stamp: units of the group move; OnUnitMoveHere marks every member that Vanilla
        // actually re-plans, so members left on their old order become visible at Post.
        // Task/GroundTask: AnchorPendingTask until the first unit asks (DiggerSpec/GroundSpec hold
        // the starts captured at Pre); most AI group moves re-plan no member and need no field.
        private struct AnchorDispatchFrame
        {
            internal int Tribe, Task, GroundTask, Previous, PreviousGround, Stamp; internal long Started; internal int[] Members;
            internal AnchorTaskSpec DiggerSpec, GroundSpec;
            // Formation plan of this move (unit id -> target node); null for Vanilla's own spreading.
            internal Dictionary<int, int> Targets;
        }
        // Formation plan delivered by BugfixesAndQoL before its move frame exists (handler order).
        private int anchorPendingFormationTribe = -1;
        private Dictionary<int, int> anchorPendingFormation;
        // Batched answers: one reusable set for the moat-capable task (0) and one for the ground-only
        // task (1). Key (task, start, target) -> set << 24 | unit index; a set answers only for the
        // task it was filled for, so a nested move that refills it never leaks foreign answers.
        private const int AnchorBatchStride = 2000;
        private sealed class AnchorBatchSet
        {
            internal int Task; internal AnchorNative.QueryResult[] Results = new AnchorNative.QueryResult[0];
            internal byte[] Bytes = new byte[0]; internal int[] Starts = new int[0], Slots = new int[0];
        }
        private readonly AnchorBatchSet[] anchorBatchSets = { new AnchorBatchSet(), new AnchorBatchSet() };
        private readonly Dictionary<long, int> anchorBatchIndex = new Dictionary<long, int>();
        private long anchorBatchCalls, anchorBatchUnits, anchorBatchHits, anchorBatchTicks;
        private static long AnchorBatchKey(int task, int start, int target) => ((long)task << 40) | ((long)start << 20) | (uint)target;
        internal sealed class AnchorTaskSpec { internal int Player, Anchor; internal long Ground, Moat; internal int[] Starts; }
        private const int AnchorPendingTask = -1;
        private readonly List<AnchorDispatchFrame> anchorFrames = new List<AnchorDispatchFrame>();
        private readonly int[] anchorUnitStamps = new int[MaximumUnitCount + 2];
        private int anchorStampCounter;
        // Last ended move with unreached members (consumed by the scheduler's diagnostic line).
        private readonly int[] anchorUnreachedSample = new int[8];
        private int anchorEndedTribe, anchorEndedMembers, anchorEndedUnreached, anchorUnreachedLogs;
        // Per player: -1 not requested, -2 failed for this map, otherwise the next node to write.
        private readonly int[] anchorMaskNext = { -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        // Reused per-tick buffers (no allocations in the tick path).
        private readonly uint[] anchorWords = new uint[AnchorMaskNodesPerTick];
        private int[] anchorIds = new int[64], anchorStarts = new int[256];
        private uint[] anchorIdWords = new uint[64];
        // Chunked re-read after access or terrain invalidations: cursor (-1: idle) and nodes left.
        // A new request extends the running round to a full map from the cursor instead of
        // restarting at node 0, so frequent requests (AI games) still reach every node.
        // The mask stays usable; publication still audits every transition live.
        // One cursor for all ready masks: a player-neutral node is read live once per step and
        // adopted by the other players' masks.
        private int anchorSweepCursor = -1, anchorSweepLeft;
        // Position and progress of the running round since its latest request: a node the round
        // re-read since then holds a word that already reflects the requesting change.
        private int anchorSweepRequestStart, anchorSweptSinceRequest;
        private readonly uint[][] anchorSweepWords = new uint[9][];
        // Gate buildings closed to a player (same rule as the cursor portals), refreshed only when
        // the access fingerprint changes; anchor masks never need a cursor topology.
        private readonly HashSet<int>[] anchorBlocked = new HashSet<int>[9];
        // Union of all players' closed gate buildings and the words shared by the player masks.
        private readonly HashSet<int> anchorBlockedUnion = new HashSet<int>();
        private FastSharedWords anchorShared;
        // Access snapshot of the last tick: alliance matrix and the portal fields the access
        // fingerprint hashes, so a gate change re-reads only the changed gate buildings.
        private const int AnchorPortalFields = 6;
        private readonly int[] anchorPortals = new int[200 * AnchorPortalFields], anchorPortalsNext = new int[200 * AnchorPortalFields];
        private int anchorPortalCount = -1; private ulong anchorDiplomacy;
        private long anchorGateRefreshes;
        // Own traversal caches: Fast's caches re-read all known nodes synchronously on access
        // changes, which would stall a tick once the whole map is known.
        private readonly FastTraversalCache[] anchorMaps = new FastTraversalCache[9];
        private ulong anchorAccess; private bool anchorAccessKnown;
        private readonly HashSet<int> anchorLiveTasks = new HashSet<int>();
        private readonly byte[] anchorBuffer = new byte[2000];
        private long anchorTasks, anchorQueries, anchorMoatRoutes, anchorGroundRoutes, anchorGroundOnlyRoutes, anchorGroundDecisions, anchorFallbacks,
            anchorMaskPendingMoves, anchorAiMoves, anchorMaskTicks, anchorFieldTicks, anchorFieldMaxTicks;
        // Breakdown metrics (support log, aggregated per window; Stopwatch ticks unless noted).
        private long anchorHumanMoves, anchorMoveTicks, anchorMoveMaxTicks, anchorPreTicks, anchorPreMaxTicks, anchorQueryTicks, anchorEncodeTicks,
            anchorPublishTicks, anchorPublished, anchorPublishRejects, anchorEncodeRejects, anchorRouteReuses, anchorMembers, anchorUnreachedUnits,
            anchorMovesWithUnreached, anchorRefreshTicks, anchorRefreshNodes, anchorBuildTicks, anchorBuildNodes, anchorSweepTicks, anchorSweepNodes,
            anchorResweeps, anchorAccessChanges, anchorLazyTasks, anchorUnusedMoves;
        // Publication rejections by audit detail (support log; few distinct keys).
        private readonly Dictionary<string, long> anchorPublishReasons = new Dictionary<string, long>();
        // Routes of this tick that passed the live edge audit (reference identity); publication
        // skips its second per-edge check for exactly these (GetReusableQualifiedRoute also
        // requires the same tick, placement revision, start, target and unit identity).
        private readonly HashSet<QualifiedMovementRoute> anchorAuditedRoutes = new HashSet<QualifiedMovementRoute>();
        private long anchorAuditSkips;
        private readonly long[] anchorQueryStatus = new long[8];
        private readonly long[] anchorCoreStats = new long[AnchorNative.StatCount];

        private void InitializeAnchor()
        {
            if (!settings.Anchor) return;
            try
            {
                string directory = settings.PluginDirectory ?? throw new InvalidOperationException("Plugin directory unknown.");
                AnchorNative.Load(directory);
                int code = AnchorNative.mma_create(MapWidth, MapWidth, AnchorMemoryLimit, out anchorContext);
                if (code != 0) throw new InvalidOperationException("Anchor core create failed: code " + code);
                anchorThreads = Math.Max(1, AnchorNative.mma_recommended_threads());
                anchorAvailable = true;
                Shared.DebugLogHelper.LogInfo(log, $"MoatMove anchor core ready; threads={anchorThreads}, memoryLimitMiB={AnchorMemoryLimit >> 20}.");
            }
            catch (Exception ex)
            {
                // Fail-closed for this mode only: commands keep the Fast rules.
                anchorAvailable = false;
                Shared.DebugLogHelper.LogError(log, $"MoatMove anchor core unavailable; Fast rules apply: {ex.Message}");
            }
        }

        private void ReleaseAnchorTask(int task)
        {
            if (task != 0 && anchorLiveTasks.Remove(task)) AnchorNative.mma_task_release(anchorContext, task);
        }

        // Map change: tasks and masks belong to the old map and its traversal caches.
        private void ResetAnchorMap()
        {
            if (!anchorAvailable) return;
            foreach (int task in anchorLiveTasks) AnchorNative.mma_task_release(anchorContext, task);
            anchorLiveTasks.Clear(); anchorFrames.Clear(); anchorDispatchTask = anchorGroundTask = 0; anchorAuditedRoutes.Clear();
            anchorBatchIndex.Clear(); anchorPendingFormationTribe = -1; anchorPendingFormation = null;
            Array.Clear(anchorMaps, 0, anchorMaps.Length); anchorAccessKnown = false; anchorShared = null; anchorBlockedUnion.Clear();
            Array.Clear(reachWords, 0, reachWords.Length); Array.Clear(exhaustedReachability, 0, exhaustedReachability.Length);
            anchorWatchTiles = null; anchorWatchBytes = null; routeTileRuleCursor = -1;
            anchorPlayersRequested = false; anchorUnreachedLogs = 0; anchorUnreachableLogs = 0; reachabilitySlowLogs = 0; anchorEndedUnreached = 0; anchorPortalCount = -1;
            for (int p = 0; p < anchorMaskNext.Length; p++) { anchorMaskNext[p] = -1; anchorBlocked[p]?.Clear(); }
            anchorSweepCursor = -1; anchorSweepLeft = 0; anchorSweepRequestStart = anchorSweptSinceRequest = 0;
        }

        private bool AnchorReady(int player) =>
            anchorAvailable && player >= 1 && player <= 8 && anchorMaskNext[player] >= MapWidth * MapWidth;

        // Callers must be simulation-deterministic (never the local player), so every peer
        // starts and completes the same masks in the same ticks.
        private void RequestAnchorMask(int player)
        {
            if (!anchorAvailable || player < 1 || player > 8 || anchorMaskNext[player] != -1) return;
            if (AnchorNative.mma_mask_begin(anchorContext, player) == 0)
            {
                anchorMaskNext[player] = 0;
                Shared.DebugLogHelper.LogInfo(log, $"MoatMove anchor mask requested; player={player}.");
            }
            else
            {
                anchorMaskNext[player] = -2;
                Shared.DebugLogHelper.LogError(log, $"MoatMove anchor mask unavailable; player={player} keeps Fast rules for this map.");
            }
        }

        // Map start and every 256 ticks: players owning living units (human or AI, bit p for
        // player p) get their masks before their first group move. Players without units build
        // none (the editor reports all eight slots as alive).
        private bool AnchorPlayerScanDue(int tick) => anchorAvailable && (!anchorPlayersRequested || (tick & 255) == 0);

        private void RequestAnchorMasksForPlayers(int playerMask)
        {
            anchorPlayersRequested = true;
            for (int player = 1; player <= 8; player++)
                if ((playerMask & (1 << player)) != 0) RequestAnchorMask(player);
        }

        // Creates and computes one group's task synchronously (fork-join inside the move call).
        // moat == 0 is a ground-only field. Returns the ready task, or 0 when the core cannot
        // answer (the move keeps Vanilla/Fast).
        private int TryComputeAnchorTask(int player, int anchor, long ground, long moat, int[] starts, int count)
        {
            if (!anchorAvailable || count <= 0 || ground <= 0 || moat < 0) return 0;
            long divisor = Gcd(ground, moat); ground /= divisor; moat /= divisor;
            if (ground > AnchorMaxCost || moat > AnchorMaxCost) return 0;
            if (AnchorNative.mma_task_create(anchorContext, player, anchor, (int)ground, (int)moat, starts, count, out int task) != 0) return 0;
            anchorLiveTasks.Add(task); anchorTasks++;
            int tick = CaptureCurrentGameTick();
            if (tick != anchorTickTaskKeysTick) { anchorTickTaskKeys.Clear(); anchorTickTaskKeysTick = tick; }
            bool repeated = !anchorTickTaskKeys.Add(((long)player << 56) | ((long)ground << 48) | ((long)moat << 40) | (uint)anchor);
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int code = AnchorNative.mma_step(anchorContext, anchorLiveTasks.Count, anchorThreads, out _);
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
            if (repeated) { anchorSameTickTasks++; anchorSameTickTaskTicks += elapsed; }
            anchorFieldTicks += elapsed; anchorFieldMaxTicks = Math.Max(anchorFieldMaxTicks, elapsed);
            if (code == 0 && AnchorNative.mma_task_status(anchorContext, task) == AnchorNative.Ready) return task;
            ReleaseAnchorTask(task); anchorFallbacks++;
            return 0;
        }

        // Pre of a group move: unit decisions inside this call query the task (0: none). A spec
        // instead of a task is computed on the first query (ResolveAnchorTask).
        private void BeginAnchorFrame(int tribe, int task, int groundTask = 0, int[] members = null,
            AnchorTaskSpec diggerSpec = null, AnchorTaskSpec groundSpec = null)
        {
            if (diggerSpec != null) task = AnchorPendingTask;
            if (groundSpec != null) groundTask = AnchorPendingTask;
            Dictionary<int, int> targets = null;
            if (anchorPendingFormationTribe == tribe) { targets = anchorPendingFormation; anchorPendingFormationTribe = -1; anchorPendingFormation = null; }
            anchorFrames.Add(new AnchorDispatchFrame { Tribe = tribe, Task = task, GroundTask = groundTask,
                Previous = anchorDispatchTask, PreviousGround = anchorGroundTask, Stamp = ++anchorStampCounter,
                Started = System.Diagnostics.Stopwatch.GetTimestamp(), Members = members, DiggerSpec = diggerSpec, GroundSpec = groundSpec, Targets = targets });
            // A move without tasks (unsupported owner, pending mask) hides an enclosing move's tasks.
            anchorDispatchTask = task; anchorGroundTask = groundTask;
        }

        // Current task of the innermost move (ground: the ground-only task), computing a pending
        // one now. The current tasks always belong to the innermost open frame. Queries happen in
        // Vanilla's deterministic unit order, so every peer computes the same task at the same point.
        private int ResolveAnchorTask(bool ground)
        {
            int task = ground ? anchorGroundTask : anchorDispatchTask;
            if (task != AnchorPendingTask) return task;
            int top = anchorFrames.Count - 1;
            AnchorDispatchFrame frame = anchorFrames[top];
            AnchorTaskSpec spec = ground ? frame.GroundSpec : frame.DiggerSpec;
            task = spec == null ? 0 : TryComputeAnchorTask(spec.Player, spec.Anchor, spec.Ground, spec.Moat, spec.Starts, spec.Starts.Length);
            if (task != 0) anchorLazyTasks++;
            if (ground) { frame.GroundTask = task; anchorGroundTask = task; } else { frame.Task = task; anchorDispatchTask = task; }
            anchorFrames[top] = frame;
            if (task != 0 && frame.Targets != null && frame.Members != null) BatchAnchorQueries(task, ground, spec.Player, frame);
            return task;
        }

        // BugfixesAndQoL's formation plan (11B520 Pre). Attached to this tribe's open move frame, or
        // kept until that frame begins; dropped at the next tick.
        private void ObserveAnchorFormation(int tribeId, int[] unitIds, uint[] globalIds, int[] targetX, int[] targetY)
        {
            if (!anchorAvailable || unitIds == null || targetX == null || targetY == null) return;
            var targets = new Dictionary<int, int>(unitIds.Length);
            for (int i = 0; i < unitIds.Length && i < targetX.Length && i < targetY.Length; i++)
                if ((uint)targetX[i] < MapWidth && (uint)targetY[i] < MapWidth) targets[unitIds[i]] = targetY[i] * MapWidth + targetX[i];
            int top = anchorFrames.Count - 1;
            if (top >= 0 && anchorFrames[top].Tribe == tribeId && anchorFrames[top].Targets == null)
            {
                AnchorDispatchFrame frame = anchorFrames[top]; frame.Targets = targets; anchorFrames[top] = frame;
                return;
            }
            anchorPendingFormationTribe = tribeId; anchorPendingFormation = targets;
        }

        // All members of the task's class with a planned target, answered at once on the core's
        // worker threads. Members iterate in capture order, so every peer computes the same answers;
        // a unit later finds its answer by (task, native start, target), anything else asks singly.
        private void BatchAnchorQueries(int task, bool ground, int player, in AnchorDispatchFrame frame)
        {
            AnchorBatchSet set = anchorBatchSets[ground ? 1 : 0];
            int capacity = frame.Members.Length, count = 0;
            if (set.Starts.Length < capacity)
            {
                int size = Math.Max(capacity, set.Starts.Length * 2);
                set.Starts = new int[size]; set.Slots = new int[size];
                set.Results = new AnchorNative.QueryResult[size]; set.Bytes = new byte[size * AnchorBatchStride];
            }
            foreach (int id in frame.Members)
            {
                if (!frame.Targets.TryGetValue(id, out int target) ||
                    !APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null ||
                    !APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_ControllableForPlayerId != player ||
                    runtime.CanDigMoat(unit) == ground) continue;
                GetNativeMovementStart(unit, out int sx, out int sy);
                if ((uint)sx >= MapWidth || (uint)sy >= MapWidth) continue;
                set.Starts[count] = sy * MapWidth + sx; set.Slots[count] = target; count++;
            }
            set.Task = 0;
            if (count == 0) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int code = AnchorNative.mma_query_batch(anchorContext, task, set.Starts, set.Slots, count, AnchorMargin, anchorThreads,
                set.Bytes, AnchorBatchStride, set.Results);
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
            anchorBatchTicks += elapsed; anchorQueryTicks += elapsed; anchorBatchCalls++;
            if (code != 0) return;
            set.Task = task; anchorBatchUnits += count;
            int tag = (ground ? 1 : 0) << 24;
            for (int i = 0; i < count; i++)
            {
                long key = AnchorBatchKey(task, set.Starts[i], set.Slots[i]);
                if (!anchorBatchIndex.ContainsKey(key)) anchorBatchIndex[key] = tag | i;
            }
        }

        // OnUnitMoveHere: Vanilla re-plans this unit inside the innermost group move.
        private void MarkAnchorUnit(int unitId)
        {
            if (anchorFrames.Count != 0 && (uint)unitId < (uint)anchorUnitStamps.Length)
                anchorUnitStamps[unitId] = anchorFrames[anchorFrames.Count - 1].Stamp;
        }

        // Post of a group move: ends its frame and any frame left open above it.
        private void EndAnchorFrame(int tribe)
        {
            while (anchorFrames.Count != 0)
            {
                AnchorDispatchFrame frame = anchorFrames[anchorFrames.Count - 1];
                anchorFrames.RemoveAt(anchorFrames.Count - 1);
                anchorDispatchTask = frame.Previous; anchorGroundTask = frame.PreviousGround;
                ReleaseAnchorTask(frame.Task); ReleaseAnchorTask(frame.GroundTask);
                if (frame.Task != 0 || frame.GroundTask != 0) RecordAnchorMoveEnd(frame);
                if (frame.Task == AnchorPendingTask && frame.GroundTask <= 0 || frame.GroundTask == AnchorPendingTask && frame.Task <= 0)
                    anchorUnusedMoves++;
                if (frame.Tribe == tribe) return;
            }
        }

        private void RecordAnchorMoveEnd(in AnchorDispatchFrame frame)
        {
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - frame.Started;
            anchorMoveTicks += elapsed; anchorMoveMaxTicks = Math.Max(anchorMoveMaxTicks, elapsed);
            if (frame.Members == null) return;
            int unreached = 0;
            foreach (int id in frame.Members)
                if ((uint)id < (uint)anchorUnitStamps.Length && anchorUnitStamps[id] != frame.Stamp)
                { if (unreached < anchorUnreachedSample.Length) anchorUnreachedSample[unreached] = id; unreached++; }
            anchorMembers += frame.Members.Length; anchorUnreachedUnits += unreached;
            anchorEndedTribe = frame.Tribe; anchorEndedMembers = frame.Members.Length; anchorEndedUnreached = unreached;
            if (unreached != 0) anchorMovesWithUnreached++;
        }

        // Group moves never span ticks; anything still open belongs to a skipped original.
        private void ClearAnchorFrames()
        {
            while (anchorFrames.Count != 0) EndAnchorFrame(anchorFrames[0].Tribe);
            anchorDispatchTask = anchorGroundTask = 0;
        }

        private FastTraversalCache AnchorTraversal(int player) => anchorMaps[player] ?? CreateAnchorTraversal(player);

        // Own method so the lambda's closure is allocated only here, not on every AnchorTraversal
        // call (the resweep calls it per node and player).
        private FastTraversalCache CreateAnchorTraversal(int player)
        {
            RefreshAnchorBlocked(player);
            anchorShared = anchorShared ?? new FastSharedWords(MapWidth * MapWidth);
            return anchorMaps[player] = new FastTraversalCache(MapWidth, MapWidth,
                (int a, int b, int direction, bool ground, out bool wet, out bool structure) =>
                    AnchorLiveEdge(player, a, b, direction, ground, out wet, out structure), GroundEqualsFriendly,
                anchorShared, AnchorPlayerNeutral);
        }

        private bool AnchorLiveEdge(int player, int from, int to, int direction, bool ground, out bool wet, out bool structure) =>
            LiveEdge(player, from, to, direction, ground, anchorBlocked[player], out wet, out structure);

        // First phase of the E2610 portal table (FBCB9319), as in RefreshCursorPortals: a gate
        // building is closed to the player unless it is open or owned by the player or an ally.
        private void RefreshAnchorBlocked(int player)
        {
            HashSet<int> blocked = anchorBlocked[player] ?? (anchorBlocked[player] = new HashSet<int>());
            blocked.Clear();
            if (runtime.nativePathManager == IntPtr.Zero) return;
            int* context = (int*)runtime.nativePathManager;
            int count = Math.Min(Math.Max(context[0], 1), 200);
            for (int id = 1; id < count; id++)
            {
                int offset = id * 0x81;
                if (context[offset + 0x809] != 1) continue;
                int building = context[offset + 0x80C], owner = context[offset + 0x882];
                bool friendly = owner == player || owner > 0 && owner <= 8 && GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(player, owner);
                bool open = runtime.nativePortalGateStates != null && building > 0 && building <= 10000 &&
                    *(short*)(runtime.nativePortalGateStates + building * 0x32C) != 0;
                bool allowed = context[offset + 0x80A] != 1 && context[offset + 0x80F] != 0 && (friendly || open);
                if (!allowed && building > 0) blocked.Add(building);
            }
            anchorBlockedUnion.Clear();
            foreach (HashSet<int> set in anchorBlocked) if (set != null) anchorBlockedUnion.UnionWith(set);
        }

        // A node's mask word is the same for every player unless the node or a neighbour is a
        // completed moat (ownership, corner and moat-entry rules), a gate building closed to some
        // player lies on them (LiveEdge), or the gate route policy's direction mask of the node
        // differs between players with a mask (TryGetTraversalEdge reads it for the from-tile).
        private bool AnchorPlayerNeutral(int node)
        {
            if ((uint)node >= (uint)(MapWidth * MapWidth)) return false;
            int x = node % MapWidth, y = node / MapWidth;
            WeightedMoatRoutePlanner planner = runtime.weightedMoatRoutePlanner;
            int from = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (!IsValidTileId(from)) return false;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((uint)nx >= MapWidth || (uint)ny >= MapWidth) continue;
                    int tile = GameTileManagerAPI.Instance.GetTileId(nx, ny), planned = planner.GetTileId(nx, ny);
                    if (IsValidTileId(tile) && (planner.IsCompletedMoat(tile) || anchorBlockedUnion.Count != 0 && anchorBlockedUnion.Contains(runtime.nativeBuildingLayer[tile])))
                        return false;
                    if (IsValidTileId(planned) && planner.IsCompletedMoat(planned)) return false;
                }
            APIShared.IEnemyGatePathPolicy policy = APIShared.EnemyGatePathPolicyBridge.Current;
            if (policy == null || !policy.HasPublishedMask) return true;
            // BugfixesAndQoL's gate routing answers from a per-snapshot table (one byte per tile).
            System.Func<int, bool> table = GatePolicyNeutrality.Current;
            if (table != null) return table(from);
            int first = 0;
            for (int p = 1; p <= 8; p++)
            {
                if (anchorMaps[p] == null) continue;
                if (first == 0) { first = p; continue; }
                for (int d = 0; d < 8; d++)
                    if (policy.IsDirectionAllowed(first, from, d) != policy.IsDirectionAllowed(p, from, d)) return false;
            }
            return true;
        }

        // Same inputs as ReadFastAccessFingerprint: alliances (returned) and per portal its
        // stable access fields plus the gate state of its building.
        private ulong ReadAnchorAccess(int[] portals, out int count)
        {
            ulong diplomacy = 0;
            for (int a = 1; a <= 8; a++) for (int b = 1; b <= 8; b++)
                if (GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(a, b)) diplomacy |= 1UL << ((a - 1) * 8 + b - 1);
            Array.Clear(portals, 0, portals.Length); count = 0;
            if (runtime.nativePathManager == IntPtr.Zero) return diplomacy;
            int* context = (int*)runtime.nativePathManager;
            count = Math.Min(Math.Max(context[0], 1), 200);
            for (int id = 1; id < count; id++)
            {
                int offset = id * 0x81, at = id * AnchorPortalFields, building = context[offset + 0x80C];
                portals[at] = context[offset + 0x809]; portals[at + 1] = context[offset + 0x80A]; portals[at + 2] = context[offset + 0x80F];
                portals[at + 3] = context[offset + 0x882]; portals[at + 4] = building;
                portals[at + 5] = runtime.nativePortalGateStates != null && building > 0 && building <= 10000
                    ? *(short*)(runtime.nativePortalGateStates + building * 0x32C) : 0;
            }
            return diplomacy;
        }

        // Marks the footprint (+-2 tiles) of every gate building whose portal entry changed, old
        // and new building alike. False when a footprint is unknown; the caller then resweeps.
        private bool MarkChangedGates(int count)
        {
            for (int id = 1; id < count; id++)
            {
                int at = id * AnchorPortalFields; bool same = true;
                for (int f = 0; f < AnchorPortalFields; f++) if (anchorPortals[at + f] != anchorPortalsNext[at + f]) { same = false; break; }
                if (same) continue;
                for (int k = 0; k < 2; k++)
                {
                    int building = k == 0 ? anchorPortals[at + 4] : anchorPortalsNext[at + 4];
                    if (building <= 0 || k == 1 && building == anchorPortals[at + 4]) continue;
                    if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(building, out GameBuilding* record) || record == null ||
                        !Shared.GameBuildingFootprint.TryGetBounds(record, out Shared.GameBuildingFootprintBounds bounds)) return false;
                    for (int y = Math.Max(0, bounds.MinY - 2); y <= Math.Min(MapWidth - 1, bounds.MaxY + 2); y++)
                        for (int x = Math.Max(0, bounds.MinX - 2); x <= Math.Min(MapWidth - 1, bounds.MaxX + 2); x++)
                        { foreach (var map in anchorMaps) map?.Mark(y * MapWidth + x); foreach (var map in reachWords) map?.Mark(y * MapWidth + x); }
                    anchorGateRefreshes++;
                }
            }
            return true;
        }

        // Vanilla's direction bytes (0x51890D0, FBCB9319) also change without a tile report: next
        // to a castle gate a cached word stayed closed while the live edge was open (pf-iter-10;
        // dozens of native functions reference the table). A fixed chunk per tick is compared
        // with a copy and every changed tile is marked like a tile report. The first pass only
        // records (tile ids and bytes), so no tick pays the whole map. Same chunk order on every
        // peer, so the marks are deterministic.
        private const int AnchorWatchNodesPerTick = 40000;
        private int[] anchorWatchTiles; private byte[] anchorWatchBytes;
        private int anchorWatchCursor, anchorWatchPrimed; private long anchorWatchChanges;
        // Whether the node's anchor words already reflect the latest resweep request.
        private bool AnchorSweptSinceRequest(int node)
        {
            if (anchorSweepCursor < 0) return true;
            int total = MapWidth * MapWidth;
            return ((node - anchorSweepRequestStart) % total + total) % total < anchorSweptSinceRequest;
        }

        // APIShared route tile rules changed since the last tick: a changed tile's incoming edges
        // belong to its neighbours' words, so the tile and its eight neighbours are re-read. Lost
        // changes (journal overflow) re-read every mask.
        private long routeTileRuleCursor = -1, routeTileRuleChanges;
        private readonly int[] routeTileRuleBuffer = new int[4096];
        private void ReadRouteTileRuleChanges()
        {
            // A new map's masks read the current rules while they are built; follow from here.
            if (routeTileRuleCursor < 0) { routeTileRuleCursor = APIShared.Pathfinding.RouteTileRules.Version; return; }
            if (routeTileRuleCursor == APIShared.Pathfinding.RouteTileRules.Version) return;
            int count = APIShared.Pathfinding.RouteTileRules.ReadChanges(ref routeTileRuleCursor, routeTileRuleBuffer);
            if (count < 0) { MarkFastTopologyAll(); return; }
            for (int i = 0; i < count; i++)
            {
                int x = routeTileRuleBuffer[i] % APIShared.Pathfinding.RouteTileRules.MapSize, y = routeTileRuleBuffer[i] / APIShared.Pathfinding.RouteTileRules.MapSize;
                if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (IsValidTileId(tile)) { MarkFastTopologyTile(tile, 1); routeTileRuleChanges++; }
            }
        }

        // Tile id of every node once the watch's first pass completed, else null (ids are fixed per map).
        private int[] AnchorNodeTiles() =>
            anchorWatchTiles != null && anchorWatchPrimed >= MapWidth * MapWidth ? anchorWatchTiles : null;

        private void WatchNativeDirections()
        {
            byte* masks = runtime.nativeMovementMasks;
            int total = MapWidth * MapWidth;
            if (masks == null || total <= 0) return;
            if (anchorWatchTiles == null || anchorWatchTiles.Length != total)
            { anchorWatchTiles = new int[total]; anchorWatchBytes = new byte[total]; anchorWatchCursor = anchorWatchPrimed = 0; }
            int end = Math.Min(total, anchorWatchCursor + AnchorWatchNodesPerTick);
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            for (int node = anchorWatchCursor; node < end; node++)
            {
                if (node >= anchorWatchPrimed)
                {
                    int id = tiles.GetTileId(node % MapWidth, node / MapWidth);
                    anchorWatchTiles[node] = IsValidTileId(id) ? id : -1;
                    anchorWatchBytes[node] = IsValidTileId(id) ? masks[id] : (byte)0;
                    continue;
                }
                int tile = anchorWatchTiles[node];
                if (tile < 0) continue;
                byte current = masks[tile];
                if (current == anchorWatchBytes[node]) continue;
                anchorWatchBytes[node] = current; anchorWatchChanges++;
                MarkFastTopologyTile(tile, 1);
            }
            if (end > anchorWatchPrimed) anchorWatchPrimed = end;
            anchorWatchCursor = end >= total ? 0 : end;
        }

        // Access/gate changes and global invalidations: re-read every prepared mask in chunks.
        private void ResweepAnchorMasks()
        {
            anchorResweeps++;
            // The reachability words follow the same access/global invalidations. They are dropped
            // rather than re-read in full at the next search (a 184 ms gap, pf-iter-15) and seeded
            // again from anchor words this round has re-read (SeedReachabilityWord).
            foreach (var map in reachWords) map?.Forget();
            if (anchorSweepCursor < 0) anchorSweepCursor = 0;
            anchorSweepLeft = MapWidth * MapWidth;
            anchorSweepRequestStart = anchorSweepCursor; anchorSweptSinceRequest = 0;
        }

        // A failed live audit names the stale node: its area is re-read at the next tick.
        private void MarkAnchorArea(int node)
        {
            if ((uint)node >= (uint)(MapWidth * MapWidth)) return;
            int cx = node % MapWidth, cy = node / MapWidth;
            for (int y = Math.Max(0, cy - 2); y <= Math.Min(MapWidth - 1, cy + 2); y++)
                for (int x = Math.Max(0, cx - 2); x <= Math.Min(MapWidth - 1, cx + 2); x++)
                    { foreach (var map in anchorMaps) map?.Mark(y * MapWidth + x); foreach (var map in reachWords) map?.Mark(y * MapWidth + x); }
        }

        // Called once per simulation tick before Fast's command loop.
        private void AnchorTick(int tick)
        {
            if (!anchorAvailable) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            anchorSimThread = Environment.CurrentManagedThreadId;
            if (anchorFrames.Count != 0) ClearAnchorFrames();
            if (anchorAuditedRoutes.Count != 0) anchorAuditedRoutes.Clear();
            if (anchorBatchIndex.Count != 0) anchorBatchIndex.Clear();
            anchorPendingFormationTribe = -1; anchorPendingFormation = null;
            ulong access = ReadFastAccessFingerprint();
            if (!anchorAccessKnown || access != anchorAccess)
            {
                ulong diplomacy = ReadAnchorAccess(anchorPortalsNext, out int count);
                if (anchorAccessKnown)
                {
                    anchorAccessChanges++;
                    for (int p = 1; p <= 8; p++) if (anchorMaps[p] != null) RefreshAnchorBlocked(p);
                    // Alliances or the portal table layout changed: every mask may differ.
                    if (diplomacy != anchorDiplomacy) { anchorResweepDiplomacy++; ResweepAnchorMasks(); }
                    else if (count != anchorPortalCount) { anchorResweepPortals++; ResweepAnchorMasks(); }
                    else if (!MarkChangedGates(count)) { anchorResweepFootprint++; ResweepAnchorMasks(); }
                }
                Array.Copy(anchorPortalsNext, anchorPortals, anchorPortals.Length);
                anchorPortalCount = count; anchorDiplomacy = diplomacy;
            }
            anchorAccess = access; anchorAccessKnown = true;
            WatchNativeDirections();
            ReadRouteTileRuleChanges();
            // Marked nodes (tile reports, gate footprints, failed live audits, native byte changes).
            AdvanceFastMarkEpoch();
            for (int p = 1; p <= 8; p++)
            {
                FastTraversalCache map = anchorMaps[p];
                if (map == null) continue;
                IReadOnlyCollection<int> changed = map.Refresh(AnchorRefreshNodesPerTick);
                anchorRefreshBacklog = Math.Max(anchorRefreshBacklog, map.DirtyCount);
                anchorRefreshNodes += changed.Count;
                AnchorMaskChanged(p, changed, map);
            }
            long phase = System.Diagnostics.Stopwatch.GetTimestamp();
            anchorRefreshTicks += phase - started;
            int budget = AnchorMaskNodesPerTick, total = MapWidth * MapWidth;
            // Unfinished masks first (human players before AI), so frequent access changes and
            // their resweeps of ready masks never starve a mask that is still being built.
            for (int pass = 0; pass < 2 && budget > 0; pass++)
            for (int p = 1; p <= 8 && budget > 0; p++)
            {
                int next = anchorMaskNext[p];
                if (next < 0 || next >= total || GamePlayerManagerAPI.Instance.IsAIPlayer(p) != (pass == 1)) continue;
                FastTraversalCache map = AnchorTraversal(p);
                int count = Math.Min(budget, total - next);
                for (int i = 0; i < count; i++) anchorWords[i] = map.Word(next + i);
                if (AnchorNative.mma_mask_write(anchorContext, p, next, count, anchorWords, out _) != 0)
                {
                    anchorMaskNext[p] = -2;
                    Shared.DebugLogHelper.LogError(log, $"MoatMove anchor mask write failed; player={p} keeps Fast rules for this map.");
                    continue;
                }
                anchorMaskNext[p] = next + count; budget -= count; anchorBuildNodes += count;
                if (anchorMaskNext[p] >= total)
                {
                    Shared.DebugLogHelper.LogInfo(log, $"MoatMove anchor mask ready; player={p}.");
                    // Its earlier chunks predate a running round's request: extend the round.
                    if (anchorSweepCursor >= 0) ResweepAnchorMasks();
                }
            }
            long built = System.Diagnostics.Stopwatch.GetTimestamp();
            anchorBuildTicks += built - phase;
            budget = Math.Min(budget, AnchorResweepNodesPerTick);
            if (anchorSweepCursor >= 0 && budget > 0)
            {
                int sweep = anchorSweepCursor, count = Math.Min(Math.Min(budget, anchorSweepLeft), total - sweep);
                for (int i = 0; i < count; i++)
                {
                    int node = sweep + i; bool neutral = anchorShared != null && AnchorPlayerNeutral(node), have = false; uint word = 0;
                    for (int p = 1; p <= 8; p++)
                    {
                        if (anchorMaskNext[p] < total) continue;
                        FastTraversalCache map = AnchorTraversal(p);
                        uint[] words = anchorSweepWords[p] ?? (anchorSweepWords[p] = new uint[AnchorMaskNodesPerTick]);
                        if (neutral && have) words[i] = map.Adopt(node, word);
                        else
                        {
                            words[i] = map.Reread(node); anchorSweepNodes++;
                            if (neutral) { word = words[i]; have = true; }
                        }
                    }
                }
                for (int p = 1; p <= 8; p++)
                    if (anchorMaskNext[p] >= total && anchorSweepWords[p] != null)
                        AnchorNative.mma_mask_write(anchorContext, p, sweep, count, anchorSweepWords[p], out _);
                anchorSweepLeft -= count; anchorSweptSinceRequest += count;
                anchorSweepCursor = anchorSweepLeft <= 0 ? -1 : (sweep + count) % total;
            }
            long ended = System.Diagnostics.Stopwatch.GetTimestamp();
            anchorSweepTicks += ended - built; anchorMaskTicks += ended - started;
        }

        // Changed cache words of a player are mirrored into the native mask.
        private void AnchorMaskChanged(int player, IReadOnlyCollection<int> nodes, FastTraversalCache map)
        {
            if (!anchorAvailable || player < 1 || player > 8 || anchorMaskNext[player] < 0 || nodes.Count == 0) return;
            if (anchorIds.Length < nodes.Count)
            {
                int size = Math.Max(nodes.Count, anchorIds.Length * 2);
                anchorIds = new int[size]; anchorIdWords = new uint[size];
            }
            int count = 0;
            foreach (int node in nodes) { anchorIds[count] = node; anchorIdWords[count] = map.Word(node); count++; }
            AnchorNative.mma_mask_update(anchorContext, player, anchorIds, anchorIdWords, count, out _);
        }

        // Start list of a new task in the reused buffer; the core copies it.
        private int[] AnchorStartBuffer(int count)
        {
            if (anchorStarts.Length < count) anchorStarts = new int[Math.Max(count, anchorStarts.Length * 2)];
            return anchorStarts;
        }

        private static long Gcd(long a, long b) { while (b != 0) { long t = a % b; a = b; b = t; } return a; }

        // Part A: mode probe during an anchor dispatch. Only the core answers here; without an
        // answer the unit keeps the Vanilla-first chain (no synchronous Fast fields per unit).
        internal bool TryAnchorQualifiesMoat(PlanScope plan, GameUnit* unit)
        {
            if (!anchorAvailable || anchorDispatchTask == 0 || plan == null || unit == null ||
                plan.MoatWorkMovement || plan.AttackMovementQualified || !runtime.CanDigMoat(unit) ||
                (uint)plan.TargetX >= MapWidth || (uint)plan.TargetY >= MapWidth ||
                (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != unit->r_ControllableForPlayerId))) return false;
            plan.PlayerId = unit->r_ControllableForPlayerId; plan.UnitGlobalId = unit->r_GlobalId; plan.IdentityBound = true;
            GetNativeMovementStart(unit, out int sx, out int sy);
            if (plan.RouteStartX >= 0) { sx = plan.RouteStartX; sy = plan.RouteStartY; }
            if ((uint)sx >= MapWidth || (uint)sy >= MapWidth) return false;
            // A ground decision keeps its finished route on the plan; the builder reuses it.
            return TryAnchorRequiredRoute(plan, false, sx, sy, out _, out bool moat, ResolveAnchorTask(false), publishGround: true) && moat;
        }

        // Part B: during an anchor dispatch the builder publishes the core's ground route, so
        // Vanilla's per-unit search (F4930) is skipped. Moat routes qualify in the mode probe;
        // intermediate builder targets, work and attack plans keep Vanilla. Units that cannot
        // enter moats query the group's ground-only task.
        internal bool TryPrepareAnchorGround(PlanScope plan, GameUnit* unit)
        {
            bool digger = unit != null && runtime.CanDigMoat(unit);
            anchorDecline = plan == null || unit == null ? "prepare:noPlan" : (digger ? anchorDispatchTask : anchorGroundTask) == 0 ? "prepare:noTask" :
                plan.ExactRouteEndpoints ? "prepare:exactEndpoints" : plan.MoatWorkMovement ? "prepare:moatWork" :
                plan.AttackMovementQualified ? "prepare:attack" : plan.ModeObserved && plan.VanillaModeDetected ? "prepare:vanillaMode" : "prepare:identity";
            if (!anchorAvailable || (digger ? anchorDispatchTask : anchorGroundTask) == 0 || plan == null || unit == null ||
                plan.ExactRouteEndpoints || plan.MoatWorkMovement || plan.AttackMovementQualified ||
                // Vanilla's own moat mode (e.g. a start on the moat) keeps Vanilla's route.
                (plan.ModeObserved && plan.VanillaModeDetected) ||
                (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != unit->r_ControllableForPlayerId))) return false;
            GetNativeMovementStart(unit, out int sx, out int sy);
            if ((uint)sx >= MapWidth || (uint)sy >= MapWidth || (uint)plan.TargetX >= MapWidth || (uint)plan.TargetY >= MapWidth) return false;
            plan.PlayerId = unit->r_ControllableForPlayerId; plan.UnitGlobalId = unit->r_GlobalId; plan.IdentityBound = true;
            // Same tick, start, target and identity as the mode probe's ground decision: no second query.
            QualifiedMovementRoute decided = runtime.GetReusableQualifiedRoute(plan, unit);
            if (decided != null && decided.Summary.MoatEdges == 0) { anchorRouteReuses++; return true; }
            anchorDecline = "prepare:bounds";
            bool answered = TryAnchorRequiredRoute(plan, false, sx, sy, out _, out bool moat, ResolveAnchorTask(!digger), publishGround: true);
            // Exactly unreachable for the moat task: the ground task decides with Vanilla's ground rules.
            if (!answered && digger && anchorUnreachableUnit == plan.UnitId)
            {
                answered = TryAnchorRequiredRoute(plan, false, sx, sy, out _, out moat, ResolveAnchorTask(true), publishGround: true);
                if (answered) anchorDiggerGroundRoutes++;
            }
            if (!answered) return false;
            anchorDecline = moat ? "prepare:moatAnswer" : "prepare:noRoute";
            // A moat answer here belongs to the mode probe; this builder call stays Vanilla.
            if (moat) { plan.QualifiedRoute = null; return false; }
            return plan.QualifiedRoute != null;
        }

        // Builder call of a unit without a BugfixesAndQoL plan (it cannot enter moats) during an
        // anchor dispatch: publishes the ground-only task's route into the bound unit buffer
        // with the same live audit, or leaves buffer, variant and mode untouched for Vanilla.
        private bool TryPublishAnchorGroundWithoutPlan(IntPtr pathManager, UnitMoveFrame frame, out int published)
        {
            published = 0;
            anchorDecline = "noPlan:noGroundTask";
            if (anchorGroundTask == 0 || frame == null || pathManager != runtime.nativePathManager || !runtime.RequiredOnlyMode) return false;
            anchorDecline = "noPlan:unit";
            int unitId = frame.Args.UnitId;
            if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) || unit == null ||
                !APIShared.UnitAccess.IsReallyAlive(unit) || runtime.CanDigMoat(unit)) return false;
            byte* manager = (byte*)pathManager.ToPointer();
            int targetX = *(int*)(manager + 0x10), targetY = *(int*)(manager + 0x14);
            // Region intermediates keep Vanilla; only the unit's own move target is published.
            anchorDecline = "noPlan:intermediateTarget";
            if (targetX != frame.Args.TileX || targetY != frame.Args.TileY) return false;
            anchorDecline = "noPlan:variant";
            int* variant = (int*)(manager + PathManagerRouteVariantOffset);
            int originalVariant = *variant, mode = *runtime.moatPathMode;
            if (originalVariant != 0 && originalVariant != 1) return false;
            var plan = new PlanScope(unitId, targetX, targetY);
            anchorDecline = "noPlan:buffer";
            if (!TryCaptureUnitFallbackPathBuffer(pathManager, plan, unit, out byte* path, out int before, out byte[] backup)) return false;
            long publishStarted = System.Diagnostics.Stopwatch.GetTimestamp(); int ready = 0;
            // Without a core route nothing is attempted (counted as a missing answer, not a rejection).
            bool prepared = TryPrepareAnchorGround(plan, unit); string details = null;
            bool replaced = prepared &&
                TryReplaceUnsafeFallbackPath(pathManager, path, backup, before, plan, unit, out ready, out details, requireMoat: false);
            if (prepared) RecordAnchorPublish(publishStarted, replaced, "nonDigger", details);
            if (prepared && !replaced) anchorDecline = "noPlan:publishReject";
            if (replaced)
            {
                // Same terminal state as the ground publication of moat-capable members.
                *variant = 0; *runtime.moatPathMode = 0; published = ready; anchorGroundOnlyRoutes++;
                return true;
            }
            RestoreFallbackPathBuffer(pathManager, path, backup, before);
            *variant = originalVariant; *runtime.moatPathMode = mode;
            return false;
        }

        private static string AnchorStatusName(int status) =>
            status == AnchorNative.Unreachable ? "unreachable" : status == AnchorNative.OverLimit ? "overLimit" : status == AnchorNative.Pending ? "pending" :
            status == AnchorNative.Stale ? "stale" : status == AnchorNative.Invalid ? "invalid" : status == AnchorNative.Failed ? "failed" : "other";

        // Vanilla's builder, timed while an anchor group move is open (see anchorVanillaCalls).
        private int CallVanillaBuilderTimed(IntPtr pathManager, int movementClass, int movementProfile, bool reconstruction, int unitId)
        {
            if (anchorDispatchTask == 0 && anchorGroundTask == 0)
                return runtime.CallVanillaBuilder(pathManager, movementClass, movementProfile, reconstruction);
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int result = runtime.CallVanillaBuilder(pathManager, movementClass, movementProfile, reconstruction);
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
            anchorVanillaCalls++; anchorVanillaTicks += elapsed;
            if (anchorVanillaWhy.Count < 32 || anchorVanillaWhy.ContainsKey(anchorDecline))
                anchorVanillaWhy[anchorDecline] = anchorVanillaWhy.TryGetValue(anchorDecline, out long seen) ? seen + 1 : 1;
            if (result <= 0) { anchorVanillaFails++; anchorVanillaFailTicks += elapsed; }
            if (unitId != 0 && unitId == anchorUnreachableUnit)
            {
                anchorVanillaAfterUnreachable++; anchorVanillaAfterUnreachableTicks += elapsed;
                if (result > 0) { anchorVanillaFoundAfterUnreachable++; LogAnchorUnreachableFound(pathManager, unitId, result); }
            }
            return result;
        }

        // Vanilla built a route for a unit the core answered unreachable; bounded per map. Shows
        // whether the builder searched another target (region intermediate) or the mask lacks edges.
        private void LogAnchorUnreachableFound(IntPtr pathManager, int unitId, int length)
        {
            if (anchorUnreachableLogs >= 40) return;
            anchorUnreachableLogs++;
            byte* manager = (byte*)pathManager.ToPointer();
            int builderX = *(int*)(manager + 0x10), builderY = *(int*)(manager + 0x14);
            int sx = anchorUnreachableStart % MapWidth, sy = anchorUnreachableStart / MapWidth;
            int tx = anchorUnreachableTarget % MapWidth, ty = anchorUnreachableTarget / MapWidth;
            string unitText = "missing"; uint startWord = 0, targetWord = 0;
            if (APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) && unit != null)
            {
                int player = unit->r_ControllableForPlayerId;
                unitText = $"{unit->r_UnitChimp}/player={player}/digger={runtime.CanDigMoat(unit)}/at={unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}";
                if (player >= 1 && player <= 8 && anchorMaps[player] != null)
                { startWord = anchorMaps[player].Word(anchorUnreachableStart); targetWord = anchorMaps[player].Word(anchorUnreachableTarget); }
            }
            Shared.DebugLogHelper.LogInfo(log, $"MoatMove anchor-unreachable-found unit={unitId} {unitText} start={sx},{sy} slot={tx},{ty} " +
                $"builderTarget={builderX},{builderY} sameTarget={builderX == tx && builderY == ty} vanillaLength={length} " +
                $"task={anchorUnreachableTask} groundTask={anchorUnreachableTask == anchorGroundTask} " +
                $"startWord=0x{startWord:X8} slotWord=0x{targetWord:X8} " +
                $"startEdges=[{DescribeAnchorNode(unit, anchorUnreachableStart, false)}] slotEdges=[{DescribeAnchorNode(unit, anchorUnreachableTarget, true)}] " +
                $"vanillaRoute=[{DescribeFirstMissingEdge(manager, unit, anchorUnreachableTask == anchorGroundTask)}].");
        }

        // Walks Vanilla's packed route (low nibble first, from the manager's start) over the
        // player's cached anchor words and reports the first step the mask lacks: cached word,
        // live verdict with rule, native direction byte and buildings of both tiles.
        private string DescribeFirstMissingEdge(byte* manager, GameUnit* unit, bool ground)
        {
            if (unit == null) return "none";
            int player = unit->r_ControllableForPlayerId;
            FastTraversalCache map = player >= 1 && player <= 8 ? anchorMaps[player] : null;
            byte* path = *(byte**)(manager + PathManagerOutputBufferOffset);
            int length = *(int*)(manager + PathManagerOutputLengthOffset), x = *(int*)(manager + 0x08), y = *(int*)(manager + 0x0C);
            if (map == null || path == null || length <= 0 || length > WeightedMoatRoutePlanner.MaximumRouteEdges) return "unavailable";
            for (int i = 0; i < length; i++)
            {
                int d = (path[i >> 1] >> ((i & 1) * 4)) & 0x0F;
                if (d >= 8) return $"invalid-direction@{i}";
                int nx = x + WeightedMoatRoutePlanner.DirectionX[d], ny = y + WeightedMoatRoutePlanner.DirectionY[d];
                if ((uint)x >= MapWidth || (uint)y >= MapWidth || (uint)nx >= MapWidth || (uint)ny >= MapWidth) return $"outside@{i}";
                int node = y * MapWidth + x, next = ny * MapWidth + nx;
                uint word = map.Word(node);
                if ((word & (1U << ((ground ? 8 : 0) + d))) == 0)
                {
                    runtime.weightedMoatRoutePlanner.edgeRejectionRule = "anchor-blocked-or-invalid";
                    bool live = LiveEdge(player, node, next, d, ground, anchorBlocked[player], out _, out _);
                    string rule = live ? "ok" : runtime.weightedMoatRoutePlanner.edgeRejectionRule;
                    int a = GameTileManagerAPI.Instance.GetTileId(x, y), b = GameTileManagerAPI.Instance.GetTileId(nx, ny);
                    return $"missing@{i}/{length} {x},{y}->{nx},{ny} d={d} word=0x{word:X8} live={rule} " +
                        $"native=0x{(IsValidTileId(a) ? runtime.nativeMovementMasks[a] : 0):X2} " +
                        $"buildings={(IsValidTileId(a) ? runtime.nativeBuildingLayer[a] : -1)},{(IsValidTileId(b) ? runtime.nativeBuildingLayer[b] : -1)} " +
                        $"blocked={(IsValidTileId(a) && anchorBlocked[player]?.Contains(runtime.nativeBuildingLayer[a]) == true) || (IsValidTileId(b) && anchorBlocked[player]?.Contains(runtime.nativeBuildingLayer[b]) == true)}";
                }
                x = nx; y = ny;
            }
            return $"complete/{length} end={x},{y}";
        }

        // Native DA590 direction byte of a node and, per direction, the anchor mask's live verdict
        // with BugfixesAndQoL's rejection rule (incoming edges for a slot, outgoing for a start).
        private string DescribeAnchorNode(GameUnit* unit, int node, bool incoming)
        {
            if (unit == null || (uint)node >= (uint)(MapWidth * MapWidth)) return "none";
            int player = unit->r_ControllableForPlayerId, x = node % MapWidth, y = node / MapWidth;
            int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
            var text = new System.Text.StringBuilder();
            text.Append("native=0x").Append(IsValidTileId(tile) ? runtime.nativeMovementMasks[tile].ToString("X2") : "--")
                .Append(" building=").Append(IsValidTileId(tile) ? runtime.nativeBuildingLayer[tile] : -1);
            for (int d = 0; d < 8; d++)
            {
                int ox = x + WeightedMoatRoutePlanner.DirectionX[d], oy = y + WeightedMoatRoutePlanner.DirectionY[d];
                if ((uint)ox >= MapWidth || (uint)oy >= MapWidth) continue;
                int other = oy * MapWidth + ox;
                // Incoming edge other -> node runs in the opposite direction.
                int from = incoming ? other : node, to = incoming ? node : other, direction = incoming ? (d + 4) & 7 : d;
                // LiveEdge returns before the planner (which resets the rule) for blocked gates and invalid tiles.
                runtime.weightedMoatRoutePlanner.edgeRejectionRule = "anchor-blocked-or-invalid";
                bool ok = LiveEdge(player, from, to, direction, !runtime.CanDigMoat(unit), anchorBlocked[player], out _, out bool structure);
                text.Append(' ').Append(d).Append(':').Append(ok ? "ok" : runtime.weightedMoatRoutePlanner.edgeRejectionRule ?? "blocked")
                    .Append(structure ? "/s" : "");
            }
            return text.ToString();
        }

        internal bool IsAnchorAuditedRoute(QualifiedMovementRoute route)
        {
            if (route == null || !anchorAuditedRoutes.Contains(route)) return false;
            anchorAuditSkips++;
            return true;
        }

        // Builder publication inside an anchor group move (time includes the live audit).
        // Rejections are counted per route kind and audit detail.
        private void RecordAnchorPublish(long started, bool published, string kind, string details)
        {
            if (anchorDispatchTask == 0 && anchorGroundTask == 0) return;
            anchorPublishTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
            if (published) { anchorPublished++; return; }
            anchorPublishRejects++;
            string key = kind + ":" + (string.IsNullOrEmpty(details) ? "unknown" : details);
            // Details may carry a free-form summary reason; the set of keys stays small and bounded.
            if (anchorPublishReasons.Count < 32 || anchorPublishReasons.ContainsKey(key))
                anchorPublishReasons[key] = anchorPublishReasons.TryGetValue(key, out long n) ? n + 1 : 1;
        }

        // Cost-minimizing decision for a unit of the command currently being dispatched.
        // Returns false when the anchor core cannot answer; the caller then applies Fast.
        // With publishGround a ground decision also carries the core's ground route.
        private bool TryAnchorRequiredRoute(PlanScope plan, bool reserved, int sx, int sy,
            out RouteProbeSummary summary, out bool moatRoute, int task, bool publishGround = false)
        {
            summary = default; moatRoute = false;
            if (!anchorAvailable || task <= 0 || reserved) return false;
            int start = sy * MapWidth + sx, target = plan.TargetY * MapWidth + plan.TargetX;
            anchorQueries++;
            long queried = System.Diagnostics.Stopwatch.GetTimestamp();
            int code; AnchorNative.QueryResult result;
            AnchorBatchSet batch = null; int batched = 0;
            if (anchorBatchIndex.TryGetValue(AnchorBatchKey(task, start, target), out int entry))
            { batch = anchorBatchSets[entry >> 24]; batched = entry & 0xFFFFFF; if (batch.Task != task) batch = null; }
            if (batch != null)
            {
                // Answered by this move's batch: the same core answer, no second query.
                code = 0; result = batch.Results[batched]; anchorBatchHits++;
                if (result.Status == AnchorNative.Ready && result.Length > 0)
                    Buffer.BlockCopy(batch.Bytes, batched * AnchorBatchStride, anchorBuffer, 0, Math.Min(result.Length, anchorBuffer.Length));
            }
            else
                code = AnchorNative.mma_query(anchorContext, task, start, target, AnchorMargin, anchorBuffer, anchorBuffer.Length, out result);
            long answered = System.Diagnostics.Stopwatch.GetTimestamp();
            anchorQueryTicks += answered - queried;
            anchorQueryStatus[code != 0 ? 7 : Math.Min(Math.Max(result.Status, 0), 6)]++;
            if (code == 0 && result.Status == AnchorNative.Unreachable)
            { anchorUnreachableUnit = plan.UnitId; anchorUnreachableStart = start; anchorUnreachableTarget = target; anchorUnreachableTask = task; }
            else if (anchorUnreachableUnit == plan.UnitId) anchorUnreachableUnit = 0;
            if (code != 0 || result.Status != AnchorNative.Ready)
            {
                anchorDecline = code != 0 ? "query:error" : "query:" + AnchorStatusName(result.Status);
                return false;
            }
            plan.QualifiedRoute = null; plan.QualifiedTerminalRoute = default; plan.QualifiedTerminalSummary = default;
            if (result.Moat == 0 && !publishGround)
            {
                // The cheapest route stays on the ground: Vanilla keeps its own mode.
                anchorGroundDecisions++;
                summary = FastSummary(plan.PlayerId, sx, sy, plan.TargetX, plan.TargetY, true, default);
                return true;
            }
            var bytes = new byte[(result.Length + 1) / 2];
            for (int i = 0; i < result.Length; i++) bytes[i >> 1] |= (byte)(anchorBuffer[i] << ((i & 1) * 4));
            // Every route passes the same live audit as Fast's own routes before publication.
            bool encoded = TryFinishFastEncoded(plan.PlayerId, start, target, bytes, result.Length, result.Moat == 0, 0,
                out WeightedMoatRouteSummary found, out WeightedMoatEncodedRoute route, anchor: true);
            anchorEncodeTicks += System.Diagnostics.Stopwatch.GetTimestamp() - answered;
            if (!encoded) { anchorEncodeRejects++; anchorDecline = "query:auditReject"; return false; }
            plan.QualifiedRoute = new QualifiedMovementRoute(sx, sy, plan.TargetX, plan.TargetY, plan.PlayerId,
                runtime.mapEpoch, CaptureCurrentGameTick(), runtime.placementRevision, route, found, default, false);
            anchorAuditedRoutes.Add(plan.QualifiedRoute);
            summary = FastSummary(plan.PlayerId, sx, sy, plan.TargetX, plan.TargetY, found.MoatEdges == 0, found);
            moatRoute = found.MoatEdges > 0;
            if (moatRoute) anchorMoatRoutes++; else anchorGroundRoutes++;
            return true;
        }

        private void LogAndResetAnchorMetrics()
        {
            if (!anchorAvailable || anchorTasks == 0 && anchorQueries == 0 && anchorMaskPendingMoves == 0) return;
            Shared.DebugLogHelper.LogInfo(log, "MoatMove stage=anchor-performance " +
                $"tasks={anchorTasks} liveTasks={anchorLiveTasks.Count} queries={anchorQueries} moatRoutes={anchorMoatRoutes} " +
                $"groundRoutes={anchorGroundRoutes} nonDiggerRoutes={anchorGroundOnlyRoutes} groundDecisions={anchorGroundDecisions} fallbacks={anchorFallbacks} maskPendingMoves={anchorMaskPendingMoves} aiMoves={anchorAiMoves} " +
                $"fieldMs={anchorFieldTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F3} maxFieldMs={anchorFieldMaxTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F3} " +
                $"tickMs={anchorMaskTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F3}.");
            anchorTasks = anchorQueries = anchorMoatRoutes = anchorGroundRoutes = anchorGroundOnlyRoutes = anchorGroundDecisions = anchorFallbacks =
                anchorMaskPendingMoves = anchorAiMoves = anchorMaskTicks = anchorFieldTicks = anchorFieldMaxTicks = 0;
            LogAndResetAnchorBreakdown();
        }

        private string FormatAnchorReasons(Dictionary<string, long> reasons = null)
        {
            var text = new System.Text.StringBuilder();
            foreach (var entry in reasons ?? anchorPublishReasons) { if (text.Length != 0) text.Append(','); text.Append(entry.Key).Append('=').Append(entry.Value); }
            return text.ToString();
        }

        private static string AnchorMs(long ticks) => (ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("F3");

        // Where a group move's time goes (move = Pre to Post of 11B520, pre = members and fields,
        // query/encode/publish = per-unit core answers, route audit and buffer publication),
        // why answers fail (core status, audit, publication) and AnchorTick's phases.
        private void LogAndResetAnchorBreakdown()
        {
            if (AnchorNative.mma_stats(anchorContext, anchorCoreStats, anchorCoreStats.Length, 1) != 0)
                Array.Clear(anchorCoreStats, 0, anchorCoreStats.Length);
            Shared.DebugLogHelper.LogInfo(log, "MoatMove stage=anchor-breakdown " +
                $"humanMoves={anchorHumanMoves} moveMs={AnchorMs(anchorMoveTicks)} maxMoveMs={AnchorMs(anchorMoveMaxTicks)} " +
                $"preMs={AnchorMs(anchorPreTicks)} maxPreMs={AnchorMs(anchorPreMaxTicks)} queryMs={AnchorMs(anchorQueryTicks)} " +
                $"encodeMs={AnchorMs(anchorEncodeTicks)} publishMs={AnchorMs(anchorPublishTicks)} published={anchorPublished} " +
                $"publishRejects={anchorPublishRejects} encodeRejects={anchorEncodeRejects} routeReuses={anchorRouteReuses} " +
                $"lazyTasks={anchorLazyTasks} unusedMoves={anchorUnusedMoves} auditSkips={anchorAuditSkips} publishReasons=[{FormatAnchorReasons()}] " +
                $"batch(calls={anchorBatchCalls} units={anchorBatchUnits} hits={anchorBatchHits} ms={AnchorMs(anchorBatchTicks)}) " +
                $"queryStatus(ready,unreachable,overLimit,pending,stale,invalid,failed,error)={string.Join(",", anchorQueryStatus)} " +
                $"members={anchorMembers} unreachedMembers={anchorUnreachedUnits} movesWithUnreached={anchorMovesWithUnreached} " +
                $"vanilla(vanillaCalls={anchorVanillaCalls} vanillaFails={anchorVanillaFails} vanillaMs={AnchorMs(anchorVanillaTicks)} vanillaFailMs={AnchorMs(anchorVanillaFailTicks)} " +
                $"afterUnreachable={anchorVanillaAfterUnreachable} afterUnreachableMs={AnchorMs(anchorVanillaAfterUnreachableTicks)} " +
                $"foundAfterUnreachable={anchorVanillaFoundAfterUnreachable}) vanillaWhy=[{FormatAnchorReasons(anchorVanillaWhy)}] sameTickTasks={anchorSameTickTasks} sameTickTaskMs={AnchorMs(anchorSameTickTaskTicks)} diggerGroundRoutes={anchorDiggerGroundRoutes} nativeByteChanges={anchorWatchChanges} rebuildsWatched={anchorRebuildsWatched} tileRuleChanges={routeTileRuleChanges} exhaustedReach(kept={ExhaustedReachabilityKept} hits={ExhaustedReachabilityHits}) reach(advances/expanded/ms/maxMs)=[{FormatReachabilityByOwner()}] " +
                $"tick(refreshMs={AnchorMs(anchorRefreshTicks)} refreshNodes={anchorRefreshNodes} buildMs={AnchorMs(anchorBuildTicks)} " +
                $"buildNodes={anchorBuildNodes} sweepMs={AnchorMs(anchorSweepTicks)} sweepNodes={anchorSweepNodes} resweeps={anchorResweeps} " +
                $"accessChanges={anchorAccessChanges} gateRefreshes={anchorGateRefreshes} sharedReuses={anchorShared?.Reuses ?? 0} " +
                $"resweepWhy(diplomacy={anchorResweepDiplomacy} portals={anchorResweepPortals} footprint={anchorResweepFootprint} global={anchorResweepGlobal}) " +
                $"refreshBacklog={anchorRefreshBacklog}) core(queries={anchorCoreStats[AnchorNative.StatQueries]} " +
                $"subAnchors={anchorCoreStats[AnchorNative.StatSubAnchors]} subCapped={anchorCoreStats[AnchorNative.StatSubCapped]} " +
                $"stepMs={anchorCoreStats[AnchorNative.StatStepNs] / 1e6:F3} queryMs={anchorCoreStats[AnchorNative.StatQueryNs] / 1e6:F3} " +
                $"subMs={anchorCoreStats[AnchorNative.StatSubNs] / 1e6:F3} settles={anchorCoreStats[AnchorNative.StatSettles]} via={anchorCoreStats[AnchorNative.StatViaChosen]} " +
                $"anchorMiss(noStart,noEntry,unreached,tooFar)={anchorCoreStats[AnchorNative.StatAnchorNoStart]},{anchorCoreStats[AnchorNative.StatAnchorNoEntry]}," +
                $"{anchorCoreStats[AnchorNative.StatAnchorUnreached]},{anchorCoreStats[AnchorNative.StatAnchorTooFar]} peakMiB={anchorCoreStats[AnchorNative.StatPeakBytes] >> 20} fieldReuses={anchorCoreStats[AnchorNative.StatFieldReuses]}).");
            anchorHumanMoves = anchorMoveTicks = anchorMoveMaxTicks = anchorPreTicks = anchorPreMaxTicks = anchorQueryTicks = anchorEncodeTicks =
                anchorPublishTicks = anchorPublished = anchorPublishRejects = anchorEncodeRejects = anchorRouteReuses = anchorMembers =
                anchorUnreachedUnits = anchorMovesWithUnreached = anchorRefreshTicks = anchorRefreshNodes = anchorBuildTicks = anchorBuildNodes =
                anchorSweepTicks = anchorSweepNodes = anchorResweeps = anchorAccessChanges = anchorGateRefreshes = anchorLazyTasks = anchorUnusedMoves = anchorAuditSkips = anchorBatchCalls = anchorBatchUnits = anchorBatchHits = anchorBatchTicks =
                anchorResweepDiplomacy = anchorResweepPortals = anchorResweepFootprint = anchorResweepGlobal = anchorRefreshBacklog =
                anchorVanillaCalls = anchorVanillaFails = anchorVanillaTicks = anchorVanillaFailTicks = anchorVanillaAfterUnreachable =
                anchorVanillaAfterUnreachableTicks = anchorVanillaFoundAfterUnreachable = anchorSameTickTasks = anchorSameTickTaskTicks = anchorDiggerGroundRoutes = anchorWatchChanges = anchorRebuildsWatched = 0;
            Array.Clear(anchorQueryStatus, 0, anchorQueryStatus.Length); anchorPublishReasons.Clear(); anchorVanillaWhy.Clear(); reachabilityByOwner.Clear();
        }
    }
}
