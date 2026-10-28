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
    // Anchor mode (ANCHOR_MODE_PLAN.md): Fast's deferred command flow with the native anchor
    // core. The core decides by weighted cost, but the per-unit pipeline stays Vanilla-first:
    // it asks only after Vanilla found no ground route, so ground-reachable units keep Vanilla.
    // Attacks, work, waypoints and cursor queries keep Fast's rules. Until a player's mask is
    // complete, that player's commands use the Fast path.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        // Private C ABI of native/MoatMoveAnchor.cpp (contract 0x04000002). The DLL is loaded
        // once by full path from the plugin folder and stays loaded for the process.
        private static class AnchorNative
        {
            internal const string Library = "MoatMoveAnchor.dll";
            internal const int Contract = 0x04000002, Ready = 0, Unreachable = 1, OverLimit = 2, Pending = 3, Stale = 4, Invalid = 5, Failed = 6;
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
        private const int AnchorMaskNodesPerTick = 4096, AnchorTasksPerTick = 16, AnchorMargin = 8, AnchorMaxCost = 255;
        // Restarts of a stale task before its command continues on the Fast path, so frequent
        // terrain changes cannot hold a command back indefinitely.
        private const int AnchorMaxRestarts = 3;
        private const long AnchorMemoryLimit = 768L * 1024 * 1024;
        private IntPtr anchorContext;
        private bool anchorAvailable, anchorPlayersRequested;
        private int anchorThreads = 1, anchorDispatchTask, anchorTaskBudget;
        // Per player: -1 not requested, -2 failed for this map, otherwise the next node to write.
        private readonly int[] anchorMaskNext = { -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        // Reused per-tick buffers (no allocations in the tick path).
        private readonly uint[] anchorWords = new uint[AnchorMaskNodesPerTick];
        private int[] anchorIds = new int[64], anchorStarts = new int[256];
        private uint[] anchorIdWords = new uint[64];
        // Chunked re-read position after access changes (-1: none). The mask stays usable;
        // publication still audits every transition live.
        private readonly int[] anchorSweep = { -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        // Own traversal caches: Fast's caches re-read all known nodes synchronously on access
        // changes, which would stall a tick once the whole map is known.
        private readonly FastTraversalCache[] anchorMaps = new FastTraversalCache[9];
        private ulong anchorAccess; private bool anchorAccessKnown;
        private readonly HashSet<int> anchorLiveTasks = new HashSet<int>();
        private readonly byte[] anchorBuffer = new byte[2000];
        private long anchorTasks, anchorQueries, anchorMoatRoutes, anchorGroundDecisions, anchorFallbacks, anchorStale, anchorMaskTicks;

        private sealed class AnchorSearch : IDisposable
        {
            internal FriendlyMoatTraversalProvider Owner; internal int Task, Restarts;
            public void Dispose() { Owner?.ReleaseAnchorTask(Task); Task = 0; Owner = null; }
        }

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
            anchorLiveTasks.Clear(); anchorDispatchTask = 0; Array.Clear(anchorMaps, 0, anchorMaps.Length); anchorAccessKnown = false;
            anchorPlayersRequested = false;
            for (int p = 0; p < anchorMaskNext.Length; p++) { anchorMaskNext[p] = -1; anchorSweep[p] = -1; }
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

        // Map start: living human players get their masks before their first command.
        private void RequestAnchorMasksForPlayers()
        {
            anchorPlayersRequested = true;
            foreach (int player in GamePlayerManagerAPI.Instance.GetAlivePlayerIds())
                if (!GamePlayerManagerAPI.Instance.IsAIPlayer(player)) RequestAnchorMask(player);
        }

        // Computes pending tasks within this tick's deterministic task budget.
        private void StepAnchorTasks()
        {
            if (anchorTaskBudget <= 0 || anchorLiveTasks.Count == 0) return;
            if (AnchorNative.mma_step(anchorContext, anchorTaskBudget, anchorThreads, out int computed) == 0)
                anchorTaskBudget -= Math.Max(1, computed);
            else anchorTaskBudget = 0;
        }

        private FastTraversalCache AnchorTraversal(int player)
        {
            FastTraversalCache map = anchorMaps[player];
            if (map == null)
            {
                EnsureCursorTopology(player, false);
                map = anchorMaps[player] = new FastTraversalCache(MapWidth, MapWidth,
                    (int a, int b, int direction, bool ground, out bool wet, out bool structure) =>
                        FastLiveEdge(player, a, b, direction, ground, out wet, out structure));
            }
            return map;
        }

        // Stale task: restart it a bounded number of times, then the command uses Fast.
        private bool TryRestartAnchorTask(AnchorSearch search)
        {
            if (++search.Restarts > AnchorMaxRestarts) return false;
            AnchorNative.mma_task_restart(anchorContext, search.Task); anchorStale++;
            return true;
        }

        // Access/gate changes and failed live audits: re-read every prepared mask in chunks.
        private void ResweepAnchorMasks()
        {
            for (int p = 1; p <= 8; p++) if (anchorMaskNext[p] >= 0) anchorSweep[p] = 0;
        }

        // Called once per simulation tick before Fast's command loop.
        private void AnchorTick(int tick)
        {
            if (!anchorAvailable) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            anchorTaskBudget = AnchorTasksPerTick;
            // Players can become alive after the first ticks; the tick test keeps peers aligned.
            if (!anchorPlayersRequested || (tick & 255) == 0) RequestAnchorMasksForPlayers();
            ulong access = ReadFastAccessFingerprint();
            if (anchorAccessKnown && access != anchorAccess) ResweepAnchorMasks();
            anchorAccess = access; anchorAccessKnown = true;
            // Local terrain changes marked by the topology hooks (small sets).
            for (int p = 1; p <= 8; p++)
            {
                FastTraversalCache map = anchorMaps[p];
                if (map == null) continue;
                EnsureCursorTopology(p, false);
                AnchorMaskChanged(p, map.Refresh(), map);
            }
            int budget = AnchorMaskNodesPerTick, total = MapWidth * MapWidth;
            for (int p = 1; p <= 8 && budget > 0; p++)
            {
                int sweep = anchorSweep[p];
                if (sweep < 0 || anchorMaskNext[p] < total) continue;
                FastTraversalCache map = AnchorTraversal(p);
                int count = Math.Min(budget, total - sweep);
                for (int i = 0; i < count; i++) anchorWords[i] = map.Reread(sweep + i);
                AnchorNative.mma_mask_write(anchorContext, p, sweep, count, anchorWords, out _);
                anchorSweep[p] = sweep + count >= total ? -1 : sweep + count; budget -= count;
            }
            for (int p = 1; p <= 8 && budget > 0; p++)
            {
                int next = anchorMaskNext[p];
                if (next < 0 || next >= total) continue;
                FastTraversalCache map = AnchorTraversal(p);
                int count = Math.Min(budget, total - next);
                for (int i = 0; i < count; i++) anchorWords[i] = map.Word(next + i);
                if (AnchorNative.mma_mask_write(anchorContext, p, next, count, anchorWords, out _) != 0)
                {
                    anchorMaskNext[p] = -2;
                    Shared.DebugLogHelper.LogError(log, $"MoatMove anchor mask write failed; player={p} keeps Fast rules for this map.");
                    continue;
                }
                anchorMaskNext[p] = next + count; budget -= count;
                if (anchorMaskNext[p] >= total) Shared.DebugLogHelper.LogInfo(log, $"MoatMove anchor mask ready; player={p}.");
            }
            StepAnchorTasks();
            anchorMaskTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
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

        // Cost-minimizing decision for a unit of the command currently being dispatched.
        // Returns false when the anchor core cannot answer; the caller then applies Fast.
        private bool TryAnchorRequiredRoute(PlanScope plan, bool reserved, int sx, int sy,
            out RouteProbeSummary summary, out bool moatRoute)
        {
            summary = default; moatRoute = false;
            if (!anchorAvailable || anchorDispatchTask == 0 || reserved) return false;
            int start = sy * MapWidth + sx, target = plan.TargetY * MapWidth + plan.TargetX;
            anchorQueries++;
            if (AnchorNative.mma_query(anchorContext, anchorDispatchTask, start, target, AnchorMargin,
                    anchorBuffer, anchorBuffer.Length, out AnchorNative.QueryResult result) != 0 ||
                result.Status != AnchorNative.Ready) return false;
            plan.QualifiedRoute = null; plan.QualifiedTerminalRoute = default; plan.QualifiedTerminalSummary = default;
            if (result.Moat == 0)
            {
                // The cheapest route stays on the ground: Vanilla keeps its own route.
                anchorGroundDecisions++;
                summary = FastSummary(plan.PlayerId, sx, sy, plan.TargetX, plan.TargetY, true, default);
                return true;
            }
            var bytes = new byte[(result.Length + 1) / 2];
            for (int i = 0; i < result.Length; i++) bytes[i >> 1] |= (byte)(anchorBuffer[i] << ((i & 1) * 4));
            if (!TryFinishFastEncoded(plan.PlayerId, start, target, bytes, result.Length, false, 0,
                    out WeightedMoatRouteSummary found, out WeightedMoatEncodedRoute route)) return false;
            plan.QualifiedRoute = new QualifiedMovementRoute(sx, sy, plan.TargetX, plan.TargetY, plan.PlayerId,
                runtime.mapEpoch, CaptureCurrentGameTick(), runtime.placementRevision, route, found, default, false);
            summary = FastSummary(plan.PlayerId, sx, sy, plan.TargetX, plan.TargetY, false, found);
            moatRoute = found.MoatEdges > 0; anchorMoatRoutes++;
            return true;
        }

        private void LogAndResetAnchorMetrics()
        {
            if (!anchorAvailable || anchorTasks == 0 && anchorQueries == 0) return;
            Shared.DebugLogHelper.LogInfo(log, "MoatMove stage=anchor-performance " +
                $"tasks={anchorTasks} liveTasks={anchorLiveTasks.Count} queries={anchorQueries} moatRoutes={anchorMoatRoutes} " +
                $"groundDecisions={anchorGroundDecisions} fastFallbacks={anchorFallbacks} stale={anchorStale} " +
                $"tickMs={anchorMaskTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F3}.");
            anchorTasks = anchorQueries = anchorMoatRoutes = anchorGroundDecisions = anchorFallbacks = anchorStale = anchorMaskTicks = 0;
        }
    }
}
