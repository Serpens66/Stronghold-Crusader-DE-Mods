using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace BetterPathfindingTest.Core
{
    // Private owned-data ABI. No game pointers, commands or native path buffers.
    public sealed class AreaNavigator : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct Request
        { internal int Start, Target, Maximum, RequireWet, Exclude, BoundOffset, BoundCount, TerminalOffset, TerminalCount, Reserved; internal long Ground, Moat; }
        [StructLayout(LayoutKind.Sequential)] private struct Bound { internal long Ground, Moat, Maximum; }
        [StructLayout(LayoutKind.Sequential)] private struct Terminal { internal int From, To, Wet, Structure; }
        [StructLayout(LayoutKind.Sequential)] private struct Result
        { internal int Status, Index, Ground, Moat, Offset, Length; internal long Cost; internal double CompletedMs; }
        [StructLayout(LayoutKind.Sequential)] public struct Statistics
        { public long Regions, Vertices, Pruned, Fields, Hits, Macros, Expanded, Bytes, Peak, Refinements, LocalFields, LocalHits, Evictions, Completed, OutputBytes, CachedFields; }
        private const string Library = "BetterPathfindingNative.dll";
        private static readonly object LibraryLock = new object();
        private static IntPtr module;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int bpf_contract();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int bpf_create(int width, int height, byte[] valid, uint[] words, long bytes, out IntPtr context);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void bpf_destroy(IntPtr context);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int bpf_begin(IntPtr context, Request[] requests, int count, Bound[] bounds, int boundCount, Terminal[] terminals, int terminalCount);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int bpf_step(IntPtr context, int operations);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int bpf_copy(IntPtr context, [Out] Result[] results, int count, [Out] byte[] bytes, int capacity, out int needed);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int bpf_stats(IntPtr context, out Statistics statistics);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int bpf_set_limit(IntPtr context, long bytes);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int bpf_allocations(IntPtr context, out long bytes, out long calls);
        public static void InitializeLibrary(string absolutePath = null)
        {
            lock (LibraryLock)
            {
                if (module != IntPtr.Zero) return;
                if (IntPtr.Size != 8) throw new NotSupportedException("Native area navigation requires a 64-bit process.");
                if (absolutePath == null)
                {
                    absolutePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Library);
                    if (!File.Exists(absolutePath)) absolutePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Testmods/BetterPathfindingTest/BepInEx/plugins/BetterPathfindingTest_Serp", Library));
                }
                if (!Path.IsPathRooted(absolutePath) || !File.Exists(absolutePath)) throw new NotSupportedException("Private native backend missing: " + absolutePath);
                module = LoadLibraryEx(absolutePath, IntPtr.Zero, 8);
                if (module == IntPtr.Zero) throw new NotSupportedException("Cannot load x64 navigation backend; Win32=" + Marshal.GetLastWin32Error());
                if (bpf_contract() != 0x02003828 || Marshal.SizeOf(typeof(Request)) != 56 || Marshal.SizeOf(typeof(Result)) != 40 || Marshal.SizeOf(typeof(Statistics)) != 128)
                    throw new NotSupportedException("Private native navigation ABI mismatch.");
                // Process-owned module; no unload while queued jobs might execute it.
            }
        }
        private IntPtr context;
        private long nativeOwned, fixedBytes, returnedBuffers;
        private GridSnapshot map;
        public GridSnapshot Map => map;
        public readonly WorkBudget Budget;
        public Statistics Stats { get; private set; }
        public long ChangedNodes { get; private set; }
        public long InvalidatedFields { get; private set; }
        public double MaximumSliceMs { get; private set; }
        public long NativeAllocatedBytes { get; private set; }
        public long NativeAllocationCalls { get; private set; }
        public AreaNavigator(GridSnapshot map, WorkBudget budget)
        {
            this.map = map; Budget = budget; InitializeLibrary();
            fixedBytes = map.BufferBytes + 4L * 1024 * 1024; Budget.Reserve(fixedBytes);
            try { Create(); } catch { Dispose(); throw; }
        }
        private static void CheckCode(int code)
        { if (code == 2) throw new SearchBudgetException("native-memory-budget"); if (code == 3) throw new ArgumentException("Native snapshot/request/ABI contract rejected."); if (code != 0) throw new InvalidOperationException("Native navigation backend failed; code=" + code); }
        private void Create()
        {
            Budget.Check(); byte[] valid = map.CopyValidity(); uint[] words = map.CopyEdges();
            CheckCode(bpf_create(map.Width, map.Height, valid, words, Budget.MaximumBytes - Budget.Bytes, out context)); Synchronize(); Budget.Check();
        }
        private void Synchronize()
        {
            if (context == IntPtr.Zero) return;
            CheckCode(bpf_stats(context, out Statistics stats)); Stats = stats;
            CheckCode(bpf_allocations(context, out long allocated, out long allocationCalls)); NativeAllocatedBytes = allocated; NativeAllocationCalls = allocationCalls;
            long difference = stats.Bytes - nativeOwned;
            if (difference > 0) Budget.Reserve(difference); else if (difference < 0) Budget.Release(-difference);
            nativeOwned = stats.Bytes;
            long transient = stats.Peak - stats.Bytes;
            if (transient > 0) { Budget.Reserve(transient); Budget.Release(transient); }
        }
        public RouteResult FindPath(RouteQuery query) => FindPaths(new[] { query })[0];
        public RouteResult[] FindPaths(IList<RouteQuery> queries)
        {
            if (queries.Count == 0) return new RouteResult[0];
            if (queries.Count > 10000) throw new ArgumentException("Diagnostic batches are limited to 10,000 requests.");
            long owned = 0;
            try
            {
                Budget.Check(); int boundCount = 0, terminalCount = 0;
                foreach (RouteQuery q in queries) { q.Validate(map); boundCount = checked(boundCount + q.Bounds.Length); terminalCount = checked(terminalCount + q.Terminals.Length); }
                long inputBytes = queries.Count * 96L + boundCount * 24L + terminalCount * 16L; Budget.Reserve(inputBytes); owned = inputBytes;
                var requests = new Request[queries.Count]; var bounds = new Bound[boundCount]; var terminals = new Terminal[terminalCount]; int bi = 0, ti = 0;
                for (int i = 0; i < queries.Count; i++)
                {
                    RouteQuery q = queries[i];
                    requests[i] = new Request { Start = q.Start, Target = q.Target, Maximum = q.MaximumEdges, RequireWet = q.RequireMoat ? 1 : 0, Exclude = q.ExcludeStructures ? 1 : 0, Ground = q.GroundCost, Moat = q.MoatCost, BoundOffset = bi, BoundCount = q.Bounds.Length, TerminalOffset = ti, TerminalCount = q.Terminals.Length, Reserved = i };
                    foreach (CostBound b in q.Bounds) bounds[bi++] = new Bound { Ground = b.Ground, Moat = b.Moat, Maximum = b.Maximum };
                    foreach (TerminalEdge t in q.Terminals) terminals[ti++] = new Terminal { From = t.From, To = t.To, Wet = t.Moat ? 1 : 0, Structure = t.Structure ? 1 : 0 };
                }
                CheckCode(bpf_set_limit(context, Budget.MaximumBytes - Budget.Bytes + nativeOwned));
                CheckCode(bpf_begin(context, requests, requests.Length, bounds, bounds.Length, terminals, terminals.Length)); int code;
                do { Budget.Check(); var watch = System.Diagnostics.Stopwatch.StartNew(); code = bpf_step(context, 256); MaximumSliceMs = Math.Max(MaximumSliceMs, watch.Elapsed.TotalMilliseconds); } while (code == 1);
                if (code != 0 && code != 2) CheckCode(code);
                Synchronize(); Statistics statistics = Stats;
                long copied = statistics.OutputBytes; Budget.Reserve(copied); owned += copied;
                var nativeResults = new Result[queries.Count]; var directions = new byte[checked((int)copied)];
                int completed = bpf_copy(context, nativeResults, nativeResults.Length, directions, directions.Length, out int needed);
                if (completed < 0 || completed != statistics.Completed || needed != directions.Length) throw new InvalidOperationException("Native result-copy contract.");
                long returned = copied + queries.Count * 128L;
                Budget.Reserve(returned); returnedBuffers += returned;
                var results = new RouteResult[queries.Count];
                for (int i = 0; i < results.Length; i++) results[i] = RouteResult.Failed(SearchStatus.Inconclusive, code == 2 ? "native-memory-budget" : "native-not-completed");
                var assigned = new bool[queries.Count];
                for (int i = 0; i < completed; i++)
                {
                    Result r = nativeResults[i];
                    if (r.Index < 0 || r.Index >= results.Length || assigned[r.Index] || r.Status < 0 || r.Status > 2 || r.Offset < 0 || r.Length < 0 || r.Offset > directions.Length - r.Length) throw new InvalidOperationException("Native result bounds/identity.");
                    assigned[r.Index] = true;
                    byte[] path = new byte[r.Length]; if (r.Length != 0) Array.Copy(directions, r.Offset, path, 0, r.Length);
                    results[r.Index] = new RouteResult { Status = (SearchStatus)r.Status, Cost = r.Cost, Ground = r.Ground, Moat = r.Moat, Directions = path, CompletionMs = r.CompletedMs, Reason = r.Status == 1 ? "area-field-unreachable" : "" };
                }
                return results;
            }
            catch (SearchBudgetException ex)
            { var results = new RouteResult[queries.Count]; for (int i = 0; i < results.Length; i++) results[i] = RouteResult.Failed(SearchStatus.Inconclusive, ex.Message); return results; }
            finally { if (owned != 0) Budget.Release(owned); }
        }
        public bool CanReach(RouteQuery query, out SearchStatus status)
        { RouteQuery q = query.Copy(); q.MaximumEdges = 2000000; q.RequireMoat = false; q.Bounds = new CostBound[0]; RouteResult result = FindPath(q); status = result.Status; return status == SearchStatus.Found; }
        public RouteResult[] EvaluateCandidates(RouteQuery template, int[] candidates)
        { var queries = new List<RouteQuery>(); foreach (int candidate in candidates) { RouteQuery q = template.Copy(); q.Target = candidate; var terminal = new List<TerminalEdge>(); foreach (TerminalEdge edge in template.Terminals) if (edge.To == candidate) terminal.Add(edge); q.Terminals = terminal.ToArray(); queries.Add(q); } return FindPaths(queries); }
        public void Update(GridSnapshot next)
        {
            if (next.Width != map.Width || next.Height != map.Height || next.Player != map.Player) throw new ArgumentException("Incompatible update.");
            ChangedNodes = 0;
            for (int n = 0; n < map.Count; n++) { if ((n & 4095) == 0) Budget.Check(); if (map.Word(n) != next.Word(n) || map.IsValid(n) != next.IsValid(n)) ChangedNodes++; }
            if (ChangedNodes == 0) { map = next; return; }
            InvalidatedFields += Stats.CachedFields;
            // Conservative context replacement invalidates ALL fields, including new
            // shortcuts outside old paths. Incremental repair is not claimed here.
            bpf_destroy(context); context = IntPtr.Zero; Budget.Release(nativeOwned); nativeOwned = 0; map = next; Create();
        }
        public void Dispose()
        { if (context != IntPtr.Zero) { bpf_destroy(context); context = IntPtr.Zero; } if (nativeOwned != 0) { Budget.Release(nativeOwned); nativeOwned = 0; } if (returnedBuffers != 0) { Budget.Release(returnedBuffers); returnedBuffers = 0; } if (fixedBytes != 0) { Budget.Release(fixedBytes); fixedBytes = 0; } }
    }
}
