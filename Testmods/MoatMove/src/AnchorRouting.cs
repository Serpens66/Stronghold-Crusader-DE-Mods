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
        // Private C ABI of native/MoatMoveAnchor.cpp (contract 0x04000005). The DLL is loaded
        // once by full path from the plugin folder and stays loaded for the process.
        private static class AnchorNative
        {
            internal const string Library = "MoatMoveAnchor.dll";
            internal const int Contract = 0x04000005, Ready = 0, Unreachable = 1, OverLimit = 2, Pending = 3, Stale = 4, Invalid = 5, Failed = 6;
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
            // Core counters (StatIndex order of MoatMoveAnchor.cpp); reset after each read.
            internal const int StatCount = 20, StatQueries = 4, StatViaChosen = 5, StatSubAnchors = 6, StatSubCapped = 7, StatStepNs = 8, StatQueryNs = 9, StatSubNs = 10,
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
        }
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
        private readonly int[] anchorSweep = { -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        private readonly int[] anchorSweepLeft = new int[9];
        // Gate buildings closed to a player (same rule as the cursor portals), refreshed only when
        // the access fingerprint changes; anchor masks never need a cursor topology.
        private readonly HashSet<int>[] anchorBlocked = new HashSet<int>[9];
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
            anchorLiveTasks.Clear(); anchorFrames.Clear(); anchorDispatchTask = anchorGroundTask = 0;
            Array.Clear(anchorMaps, 0, anchorMaps.Length); anchorAccessKnown = false;
            anchorPlayersRequested = false; anchorUnreachedLogs = 0; anchorEndedUnreached = 0;
            for (int p = 0; p < anchorMaskNext.Length; p++) { anchorMaskNext[p] = -1; anchorSweep[p] = -1; anchorSweepLeft[p] = 0; anchorBlocked[p]?.Clear(); }
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
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int code = AnchorNative.mma_step(anchorContext, anchorLiveTasks.Count, anchorThreads, out _);
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
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
            anchorFrames.Add(new AnchorDispatchFrame { Tribe = tribe, Task = task, GroundTask = groundTask,
                Previous = anchorDispatchTask, PreviousGround = anchorGroundTask, Stamp = ++anchorStampCounter,
                Started = System.Diagnostics.Stopwatch.GetTimestamp(), Members = members, DiggerSpec = diggerSpec, GroundSpec = groundSpec });
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
            return task;
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

        private FastTraversalCache AnchorTraversal(int player)
        {
            FastTraversalCache map = anchorMaps[player];
            if (map == null)
            {
                RefreshAnchorBlocked(player);
                map = anchorMaps[player] = new FastTraversalCache(MapWidth, MapWidth,
                    (int a, int b, int direction, bool ground, out bool wet, out bool structure) =>
                        AnchorLiveEdge(player, a, b, direction, ground, out wet, out structure));
            }
            return map;
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
        }

        // Access/gate changes and global invalidations: re-read every prepared mask in chunks.
        private void ResweepAnchorMasks()
        {
            anchorResweeps++;
            for (int p = 1; p <= 8; p++)
                if (anchorMaskNext[p] >= 0) { if (anchorSweep[p] < 0) anchorSweep[p] = 0; anchorSweepLeft[p] = MapWidth * MapWidth; }
        }

        // A failed live audit names the stale node: its area is re-read at the next tick.
        private void MarkAnchorArea(int node)
        {
            if ((uint)node >= (uint)(MapWidth * MapWidth)) return;
            int cx = node % MapWidth, cy = node / MapWidth;
            for (int y = Math.Max(0, cy - 2); y <= Math.Min(MapWidth - 1, cy + 2); y++)
                for (int x = Math.Max(0, cx - 2); x <= Math.Min(MapWidth - 1, cx + 2); x++)
                    foreach (var map in anchorMaps) map?.Mark(y * MapWidth + x);
        }

        // Called once per simulation tick before Fast's command loop.
        private void AnchorTick(int tick)
        {
            if (!anchorAvailable) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            anchorSimThread = Environment.CurrentManagedThreadId;
            if (anchorFrames.Count != 0) ClearAnchorFrames();
            ulong access = ReadFastAccessFingerprint();
            if (anchorAccessKnown && access != anchorAccess)
            {
                anchorAccessChanges++;
                for (int p = 1; p <= 8; p++) if (anchorMaps[p] != null) RefreshAnchorBlocked(p);
                ResweepAnchorMasks();
            }
            anchorAccess = access; anchorAccessKnown = true;
            // Nodes marked after failed live audits (small sets).
            for (int p = 1; p <= 8; p++)
            {
                FastTraversalCache map = anchorMaps[p];
                if (map == null) continue;
                IReadOnlyCollection<int> changed = map.Refresh();
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
                if (anchorMaskNext[p] >= total) Shared.DebugLogHelper.LogInfo(log, $"MoatMove anchor mask ready; player={p}.");
            }
            long built = System.Diagnostics.Stopwatch.GetTimestamp();
            anchorBuildTicks += built - phase;
            budget = Math.Min(budget, AnchorResweepNodesPerTick);
            for (int p = 1; p <= 8 && budget > 0; p++)
            {
                int sweep = anchorSweep[p];
                if (sweep < 0 || anchorMaskNext[p] < total) continue;
                FastTraversalCache map = AnchorTraversal(p);
                int count = Math.Min(Math.Min(budget, anchorSweepLeft[p]), total - sweep);
                for (int i = 0; i < count; i++) anchorWords[i] = map.Reread(sweep + i);
                AnchorNative.mma_mask_write(anchorContext, p, sweep, count, anchorWords, out _);
                anchorSweepLeft[p] -= count; budget -= count; anchorSweepNodes += count;
                anchorSweep[p] = anchorSweepLeft[p] <= 0 ? -1 : (sweep + count) % total;
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
            if (!TryAnchorRequiredRoute(plan, false, sx, sy, out _, out bool moat, ResolveAnchorTask(!digger), publishGround: true)) return false;
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
            if (anchorGroundTask == 0 || frame == null || pathManager != runtime.nativePathManager || !runtime.RequiredOnlyMode) return false;
            int unitId = frame.Args.UnitId;
            if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) || unit == null ||
                !APIShared.UnitAccess.IsReallyAlive(unit) || runtime.CanDigMoat(unit)) return false;
            byte* manager = (byte*)pathManager.ToPointer();
            int targetX = *(int*)(manager + 0x10), targetY = *(int*)(manager + 0x14);
            // Region intermediates keep Vanilla; only the unit's own move target is published.
            if (targetX != frame.Args.TileX || targetY != frame.Args.TileY) return false;
            int* variant = (int*)(manager + PathManagerRouteVariantOffset);
            int originalVariant = *variant, mode = *runtime.moatPathMode;
            if (originalVariant != 0 && originalVariant != 1) return false;
            var plan = new PlanScope(unitId, targetX, targetY);
            if (!TryCaptureUnitFallbackPathBuffer(pathManager, plan, unit, out byte* path, out int before, out byte[] backup)) return false;
            long publishStarted = System.Diagnostics.Stopwatch.GetTimestamp(); int ready = 0;
            // Without a core route nothing is attempted (counted as a missing answer, not a rejection).
            bool prepared = TryPrepareAnchorGround(plan, unit); string details = null;
            bool replaced = prepared &&
                TryReplaceUnsafeFallbackPath(pathManager, path, backup, before, plan, unit, out ready, out details, requireMoat: false);
            if (prepared) RecordAnchorPublish(publishStarted, replaced, "nonDigger", details);
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
            int code = AnchorNative.mma_query(anchorContext, task, start, target, AnchorMargin,
                anchorBuffer, anchorBuffer.Length, out AnchorNative.QueryResult result);
            long answered = System.Diagnostics.Stopwatch.GetTimestamp();
            anchorQueryTicks += answered - queried;
            anchorQueryStatus[code != 0 ? 7 : Math.Min(Math.Max(result.Status, 0), 6)]++;
            if (code != 0 || result.Status != AnchorNative.Ready) return false;
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
            if (!encoded) { anchorEncodeRejects++; return false; }
            plan.QualifiedRoute = new QualifiedMovementRoute(sx, sy, plan.TargetX, plan.TargetY, plan.PlayerId,
                runtime.mapEpoch, CaptureCurrentGameTick(), runtime.placementRevision, route, found, default, false);
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

        private string FormatAnchorReasons()
        {
            var text = new System.Text.StringBuilder();
            foreach (var entry in anchorPublishReasons) { if (text.Length != 0) text.Append(','); text.Append(entry.Key).Append('=').Append(entry.Value); }
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
                $"lazyTasks={anchorLazyTasks} unusedMoves={anchorUnusedMoves} publishReasons=[{FormatAnchorReasons()}] " +
                $"queryStatus(ready,unreachable,overLimit,pending,stale,invalid,failed,error)={string.Join(",", anchorQueryStatus)} " +
                $"members={anchorMembers} unreachedMembers={anchorUnreachedUnits} movesWithUnreached={anchorMovesWithUnreached} " +
                $"tick(refreshMs={AnchorMs(anchorRefreshTicks)} refreshNodes={anchorRefreshNodes} buildMs={AnchorMs(anchorBuildTicks)} " +
                $"buildNodes={anchorBuildNodes} sweepMs={AnchorMs(anchorSweepTicks)} sweepNodes={anchorSweepNodes} resweeps={anchorResweeps} " +
                $"accessChanges={anchorAccessChanges}) core(queries={anchorCoreStats[AnchorNative.StatQueries]} " +
                $"subAnchors={anchorCoreStats[AnchorNative.StatSubAnchors]} subCapped={anchorCoreStats[AnchorNative.StatSubCapped]} " +
                $"stepMs={anchorCoreStats[AnchorNative.StatStepNs] / 1e6:F3} queryMs={anchorCoreStats[AnchorNative.StatQueryNs] / 1e6:F3} " +
                $"subMs={anchorCoreStats[AnchorNative.StatSubNs] / 1e6:F3} settles={anchorCoreStats[AnchorNative.StatSettles]} via={anchorCoreStats[AnchorNative.StatViaChosen]} " +
                $"anchorMiss(noStart,noEntry,unreached,tooFar)={anchorCoreStats[AnchorNative.StatAnchorNoStart]},{anchorCoreStats[AnchorNative.StatAnchorNoEntry]}," +
                $"{anchorCoreStats[AnchorNative.StatAnchorUnreached]},{anchorCoreStats[AnchorNative.StatAnchorTooFar]} peakMiB={anchorCoreStats[AnchorNative.StatPeakBytes] >> 20}).");
            anchorHumanMoves = anchorMoveTicks = anchorMoveMaxTicks = anchorPreTicks = anchorPreMaxTicks = anchorQueryTicks = anchorEncodeTicks =
                anchorPublishTicks = anchorPublished = anchorPublishRejects = anchorEncodeRejects = anchorRouteReuses = anchorMembers =
                anchorUnreachedUnits = anchorMovesWithUnreached = anchorRefreshTicks = anchorRefreshNodes = anchorBuildTicks = anchorBuildNodes =
                anchorSweepTicks = anchorSweepNodes = anchorResweeps = anchorAccessChanges = anchorLazyTasks = anchorUnusedMoves = 0;
            Array.Clear(anchorQueryStatus, 0, anchorQueryStatus.Length); anchorPublishReasons.Clear();
        }
    }
}
