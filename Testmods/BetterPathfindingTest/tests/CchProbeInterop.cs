using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using BetterPathfindingTest.Core;

// Offline first-decision-gate adapter. Not compiled into the game assembly.
internal sealed class CchProbeInterop : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Pair { internal int Start, Target, Maximum, Index; }
    [StructLayout(LayoutKind.Sequential)] private struct Result { internal int Status, Index, Steps, Offset, Length, Reserved; internal double Milliseconds; }
    private const string Library = "CchProbe.dll";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int cch_probe_contract();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int cch_probe_create(int width, int height, byte[] valid, uint[] edges, long limit, out IntPtr context);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void cch_probe_destroy(IntPtr context);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int cch_probe_step(IntPtr context, int microseconds);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int cch_probe_stats(IntPtr context, [Out] long[] data, int count);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int cch_probe_begin(IntPtr context, Pair[] queries, int count, int full);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int cch_probe_batch_step(IntPtr context, int microseconds);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int cch_probe_copy(IntPtr context, [Out] Result[] result, int count, [Out] byte[] bytes, int capacity);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int cch_probe_replace(IntPtr context, byte[] valid, uint[] words);
    private static IntPtr library;
    private IntPtr context;
    internal long[] Stats { get { var data = new long[16]; Code(cch_probe_stats(context, data, data.Length)); return data; } }
    internal double BuildMilliseconds, MaximumSliceMilliseconds, LastBatchMilliseconds;
    internal GridSnapshot Map { get; private set; }
    internal CchProbeInterop(GridSnapshot map, long bytes = 1024L * 1024 * 1024, double seconds = 300, Action<long[]> progress = null)
    {
        Map = map;
        if (library == IntPtr.Zero) library = NativeLibrary.Load(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.local/cch/CchProbe.dll")));
        if (cch_probe_contract() != 0x02001020 || Marshal.SizeOf<Pair>() != 16 || Marshal.SizeOf<Result>() != 32) throw new InvalidOperationException("CCH probe ABI");
        var clock = Stopwatch.StartNew();
        try
        {
            Code(cch_probe_create(map.Width, map.Height, map.CopyValidity(), map.CopyEdges(), bytes - map.BufferBytes, out context));
            double nextProgress = 5;
            for (;;)
            {
                if (clock.Elapsed.TotalSeconds > seconds) throw new SearchBudgetException("cch-initialization-time-budget");
                var slice = Stopwatch.StartNew(); int status = cch_probe_step(context, 2000); MaximumSliceMilliseconds = Math.Max(MaximumSliceMilliseconds, slice.Elapsed.TotalMilliseconds);
                if (clock.Elapsed.TotalSeconds >= nextProgress) { progress?.Invoke(Stats); nextProgress = clock.Elapsed.TotalSeconds + 5; }
                if (status != 1) { Code(status); break; }
            }
            BuildMilliseconds = clock.Elapsed.TotalMilliseconds;
        }
        catch { Dispose(); throw; }
    }
    private static void Code(int value) { if (value == 2) throw new SearchBudgetException("cch-memory-budget"); if (value != 0) throw new InvalidOperationException("CCH probe error=" + value); }
    internal void Replace(GridSnapshot next)
    {
        if (next.Width != Map.Width || next.Height != Map.Height) throw new ArgumentException("CCH replacement dimensions");
        Code(cch_probe_replace(context, next.CopyValidity(), next.CopyEdges()));
        var clock = Stopwatch.StartNew();
        for (;;) { if (clock.Elapsed.TotalSeconds > 300) throw new SearchBudgetException("cch-update-time-budget"); int status = cch_probe_step(context, 2000); if (status != 1) { Code(status); break; } }
        Map = next;
    }
    internal RouteResult[] Find(IList<RouteQuery> queries, bool full = true, double seconds = 120)
    {
        var pairs = new Pair[queries.Count];
        for (int i = 0; i < pairs.Length; i++)
        {
            var q = queries[i]; q.Validate(Map);
            if (q.RequireMoat || q.ExcludeStructures || q.Bounds.Length != 0 || q.Terminals.Length != 0 || q.GroundCost != 1) throw new NotSupportedException("CCH gate covers unit-cost normal ground only.");
            pairs[i] = new Pair { Start = q.Start, Target = q.Target, Maximum = q.MaximumEdges, Index = i };
        }
        var clock = Stopwatch.StartNew();
        Code(cch_probe_begin(context, pairs, pairs.Length, full ? 1 : 0));
        double nextProgress = 5;
        for (;;)
        {
            if (clock.Elapsed.TotalSeconds > seconds) throw new SearchBudgetException("cch-query-time-budget");
            var slice = Stopwatch.StartNew(); int status = cch_probe_batch_step(context, 2000); MaximumSliceMilliseconds = Math.Max(MaximumSliceMilliseconds, slice.Elapsed.TotalMilliseconds);
            if (clock.Elapsed.TotalSeconds >= nextProgress) { Console.WriteLine("PROGRESS CCH batch completed=" + Stats[10] + "/" + pairs.Length + " elapsedMs=" + clock.ElapsedMilliseconds); nextProgress = clock.Elapsed.TotalSeconds + 5; }
            if (status != 1) { Code(status); break; }
        }
        var native = new Result[pairs.Length]; var bytes = new byte[checked((int)Stats[11])];
        Code(cch_probe_copy(context, native, native.Length, bytes, bytes.Length));
        var results = new RouteResult[native.Length];
        foreach (Result n in native)
        {
            if (n.Index < 0 || n.Index >= results.Length || results[n.Index] != null || n.Offset < 0 || n.Length < 0 || n.Offset > bytes.Length - n.Length) throw new InvalidOperationException("CCH output identity/bounds");
            var path = new byte[n.Length]; Array.Copy(bytes, n.Offset, path, 0, path.Length);
            results[n.Index] = new RouteResult { Status = n.Status == 0 ? SearchStatus.Found : SearchStatus.Unreachable, Cost = n.Steps, Ground = n.Steps, Directions = path, CompletionMs = n.Milliseconds };
        }
        LastBatchMilliseconds = clock.Elapsed.TotalMilliseconds; return results;
    }
    public void Dispose() { if (context != IntPtr.Zero) { cch_probe_destroy(context); context = IntPtr.Zero; } }
}
