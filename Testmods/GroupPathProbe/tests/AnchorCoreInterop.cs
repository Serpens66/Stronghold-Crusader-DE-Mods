using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using GroupPathProbe.Core;

// Offline adapter for Testmods/MoatMove/native/MoatMoveAnchor.cpp (anchor mode, phase 1).
internal sealed class AnchorCoreInterop : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct QueryResult { internal int Status, Ground, Moat, Length, Flags, Reserved; internal long Cost; }
    private const string Library = "MoatMoveAnchor.dll";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_contract();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int mma_recommended_threads();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_create(int width, int height, long limit, out IntPtr context);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void mma_destroy(IntPtr context);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_mask_begin(IntPtr context, int player);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_mask_write(IntPtr context, int player, int first, int count, uint[] words, out int changed);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_mask_update(IntPtr context, int player, int[] nodes, uint[] words, int count, out int changed);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_penalty_update(IntPtr context, int player, int[] nodes, byte[] values, int count, out int changed);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_single_route(IntPtr context, int player, int target, int ground, int moat, int start, byte[] bytes, int capacity, out QueryResult result);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_mask_state(IntPtr context, int player, out long written, out ulong revision);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_task_create(IntPtr context, int player, int anchor, int ground, int moat, int[] starts, int count, out int id);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_task_restart(IntPtr context, int id);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_task_release(IntPtr context, int id);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_task_status(IntPtr context, int id);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_step(IntPtr context, int maxTasks, int threads, out int computed);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_query(IntPtr context, int id, int start, int slot, int margin, [Out] byte[] bytes, int capacity, out QueryResult result);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_stats(IntPtr context, [Out] long[] data, int count, int reset);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int mma_query_batch(IntPtr context, int id, int[] starts, int[] slots, int count, int margin, int threads, [Out] byte[] bytes, int stride, [Out] QueryResult[] results);
    internal static readonly string[] StatNames = { "tasksComputed", "tasksFailed", "settleEvents", "entries", "queries", "viaChosen", "subAnchors", "subCapped", "stepNs", "queryNs", "subNs", "maskWrites", "maskChanges", "peakBytes", "allocations", "workers",
        "anchorNoStart", "anchorNoEntry", "anchorUnreached", "anchorTooFar", "fieldReuses", "singleQueries", "singleSettles", "singleRestarts", "singleNs" };
    internal static int Stat(string name) => Array.IndexOf(StatNames, name);
    internal const int Found = 0, Unreachable = 1, OverLimit = 2, Pending = 3, Stale = 4, Invalid = 5, Failed = 6, Ready = 0;
    internal const int NoMemory = 2;
    private static IntPtr library;
    private IntPtr context;
    private readonly byte[] buffer = new byte[2000];
    internal int LastFlags;
    internal int Width { get; }
    internal static void Load()
    {
        if (library != IntPtr.Zero) return;
        library = NativeLibrary.Load(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../MoatMove/.local/anchor/MoatMoveAnchor.dll")));
        if (mma_contract() != 0x0400000A || Marshal.SizeOf<QueryResult>() != 32) throw new InvalidOperationException("Anchor core ABI");
    }
    internal AnchorCoreInterop(int width, int height, long limit = 512L * 1024 * 1024)
    {
        Load(); Width = width;
        int code = mma_create(width, height, limit, out context);
        if (code == NoMemory) throw new SearchBudgetException("anchor-core-memory");
        Code(code);
    }
    private static void Code(int value) { if (value == NoMemory) throw new SearchBudgetException("anchor-core-memory"); if (value != 0) throw new InvalidOperationException("Anchor core error=" + value); }
    // GridSnapshot words (allowed 0..7, moat 8..15) to the FastTraversalCache layout used
    // by MoatMove (friendly 0..7, ground 8..15, wet 16..23). Snapshot words have one edge
    // set: its dry edges are the ground edges.
    internal static uint FastWord(uint snapshotWord) =>
        (snapshotWord & 0xFFU) | ((snapshotWord & ~(snapshotWord >> 8) & 0xFFU) << 8) | (((snapshotWord >> 8) & 0xFFU) << 16);
    internal void BeginMask(int player) => Code(mma_mask_begin(context, player));
    internal void WriteWords(int player, int first, uint[] words) => Code(mma_mask_write(context, player, first, words.Length, words, out _));
    internal int WriteMask(int player, GridSnapshot map, int first, int count)
    {
        var words = new uint[count]; for (int i = 0; i < count; i++) words[i] = FastWord(map.Word(first + i));
        Code(mma_mask_write(context, player, first, count, words, out int changed)); return changed;
    }
    internal void WriteWholeMask(int player, GridSnapshot map, int chunk)
    {
        BeginMask(player);
        for (int first = 0; first < map.Count; first += chunk) WriteMask(player, map, first, Math.Min(chunk, map.Count - first));
    }
    internal int UpdateMask(int player, GridSnapshot map, IList<int> nodes)
    {
        var ids = new int[nodes.Count]; var words = new uint[nodes.Count];
        for (int i = 0; i < ids.Length; i++) { ids[i] = nodes[i]; words[i] = FastWord(map.Word(nodes[i])); }
        Code(mma_mask_update(context, player, ids, words, ids.Length, out int changed)); return changed;
    }
    internal bool MaskReady(int player) => mma_mask_state(context, player, out _, out _) == 0;
    internal int UpdatePenalties(int player, int[] nodes, byte[] values) { Code(mma_penalty_update(context, player, nodes, values, nodes.Length, out int changed)); return changed; }
    internal int CreateTask(int player, int anchor, long ground, long moat, int[] starts)
    { Code(mma_task_create(context, player, anchor, checked((int)ground), checked((int)moat), starts, starts.Length, out int id)); return id; }
    internal int Step(int maxTasks, int threads) { int code = mma_step(context, maxTasks, threads, out int computed); Code(code); return computed; }
    internal int StepCode(int maxTasks, int threads) => mma_step(context, maxTasks, threads, out _);
    internal int TaskStatus(int id) => mma_task_status(context, id);
    internal void Restart(int id) => Code(mma_task_restart(context, id));
    internal void Release(int id) => Code(mma_task_release(context, id));
    // Native status mapped onto RouteResult; Pending/Stale/Failed become Inconclusive with a reason.
    internal RouteResult Query(int id, int start, int slot, int margin)
    {
        Code(mma_query(context, id, start, slot, margin, buffer, buffer.Length, out QueryResult r));
        LastFlags = r.Flags;
        if (r.Status == Found) { var path = new byte[r.Length]; Array.Copy(buffer, path, r.Length); return new RouteResult { Status = SearchStatus.Found, Ground = r.Ground, Moat = r.Moat, Cost = r.Cost, Directions = path }; }
        if (r.Status == Unreachable) return RouteResult.Failed(SearchStatus.Unreachable, "anchor-unreachable");
        return RouteResult.Failed(SearchStatus.Inconclusive, r.Status == OverLimit ? "over-2000-steps" : r.Status == Pending ? "pending" : r.Status == Stale ? "stale" : r.Status == Failed ? "failed" : "invalid");
    }
    // One unit's exact route through the resumable single-target fields.
    internal RouteResult SingleRoute(int player, int target, long ground, long moat, int start)
    {
        Code(mma_single_route(context, player, target, (int)ground, (int)moat, start, buffer, buffer.Length, out QueryResult r));
        return Convert(r, buffer, 0);
    }
    // All units of one task at once (parallel pass, then deferred sub-anchor queries in order).
    internal RouteResult[] QueryBatch(int id, int[] starts, int[] slots, int margin, int threads)
    {
        const int stride = 2000;
        var bytes = new byte[Math.Max(1, starts.Length) * stride]; var raw = new QueryResult[starts.Length];
        Code(mma_query_batch(context, id, starts, slots, starts.Length, margin, threads, bytes, stride, raw));
        var results = new RouteResult[starts.Length];
        for (int i = 0; i < raw.Length; i++) results[i] = Convert(raw[i], bytes, i * stride);
        return results;
    }
    private static RouteResult Convert(QueryResult r, byte[] bytes, int offset)
    {
        if (r.Status == Found) { var path = new byte[r.Length]; Array.Copy(bytes, offset, path, 0, r.Length); return new RouteResult { Status = SearchStatus.Found, Ground = r.Ground, Moat = r.Moat, Cost = r.Cost, Directions = path }; }
        if (r.Status == Unreachable) return RouteResult.Failed(SearchStatus.Unreachable, "anchor-unreachable");
        return RouteResult.Failed(SearchStatus.Inconclusive, r.Status == OverLimit ? "over-2000-steps" : r.Status == Pending ? "pending" : r.Status == Stale ? "stale" : r.Status == Failed ? "failed" : "invalid");
    }
    internal long[] Stats(bool reset = false) { var data = new long[StatNames.Length]; Code(mma_stats(context, data, data.Length, reset ? 1 : 0)); return data; }
    public void Dispose() { if (context != IntPtr.Zero) { mma_destroy(context); context = IntPtr.Zero; } }
}
