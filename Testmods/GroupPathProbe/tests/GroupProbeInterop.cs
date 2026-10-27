using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using GroupPathProbe.Core;

// Offline adapter for native/GroupProbe.cpp. Never part of a game package.
internal sealed class GroupProbeInterop : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Unit { internal int Start, Slot, Group, Index; }
    [StructLayout(LayoutKind.Sequential)] private struct Group { internal int Anchor, Ground, Moat, Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct Result { internal int Index, Status, Ground, Moat, Offset, Length; internal long Cost; }
    private const string Library = "GroupProbe.dll";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int gp_contract();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int gp_create(int width, int height, byte[] valid, uint[] edges, long limit, out IntPtr context);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void gp_destroy(IntPtr context);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int gp_run(IntPtr context, int mode, Unit[] units, int count, Group[] groups, int groupCount, int margin, out int outputBytes);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int gp_copy(IntPtr context, [Out] Result[] results, int count, [Out] byte[] bytes, int capacity);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int gp_stats(IntPtr context, [Out] long[] data, int count);
    internal static readonly string[] StatNames = { "settleEvents", "entries", "blocks", "splits", "fallbacks", "fieldNs", "extractNs", "maxGroupNs", "peakBytes", "allocations", "localNs", "anchorNs" };
    internal const int Exact = 0, Anchor = 1, OverLimit = 2;
    private static IntPtr library;
    private IntPtr context;
    internal GridSnapshot Map { get; }
    internal double LastMilliseconds;
    internal int[] LastStatus = new int[0];
    internal long[] Stats { get { var data = new long[StatNames.Length]; Code(gp_stats(context, data, data.Length)); return data; } }
    internal GroupProbeInterop(GridSnapshot map, long bytes = 1024L * 1024 * 1024)
    {
        Map = map;
        if (library == IntPtr.Zero) library = NativeLibrary.Load(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.local/native/GroupProbe.dll")));
        if (gp_contract() != 0x03001016 || Marshal.SizeOf<Unit>() != 16 || Marshal.SizeOf<Group>() != 16 || Marshal.SizeOf<Result>() != 32) throw new InvalidOperationException("Group probe ABI");
        Code(gp_create(map.Width, map.Height, map.CopyValidity(), map.CopyEdges(), bytes, out context));
    }
    private static void Code(int value) { if (value == 2) throw new SearchBudgetException("group-probe-memory-budget"); if (value != 0) throw new InvalidOperationException("Group probe error=" + value); }
    // Every query belongs to one group; a group shares anchor and cost profile.
    internal RouteResult[] Run(int mode, IList<RouteQuery> queries, int[] groupOf, int[] anchors, int margin = 2)
    {
        var units = new Unit[queries.Count]; var groups = new Group[anchors.Length];
        for (int g = 0; g < anchors.Length; g++) groups[g].Anchor = anchors[g];
        for (int i = 0; i < units.Length; i++)
        {
            RouteQuery q = queries[i]; int g = groupOf[i];
            if (q.RequireMoat || q.ExcludeStructures || q.Bounds.Length != 0 || q.Terminals.Length != 0) throw new NotSupportedException("Group probe covers scalar ground/moat costs only.");
            if (groups[g].Ground == 0) { groups[g].Ground = checked((int)q.GroundCost); groups[g].Moat = checked((int)q.MoatCost); }
            else if (groups[g].Ground != q.GroundCost || groups[g].Moat != q.MoatCost) throw new ArgumentException("Mixed profile inside one group.");
            units[i] = new Unit { Start = q.Start, Slot = q.Target, Group = g, Index = i };
        }
        var clock = Stopwatch.StartNew();
        Code(gp_run(context, mode, units, units.Length, groups, groups.Length, margin, out int bytes));
        var native = new Result[units.Length]; var buffer = new byte[bytes];
        Code(gp_copy(context, native, native.Length, buffer, buffer.Length));
        var results = new RouteResult[units.Length]; LastStatus = new int[units.Length];
        foreach (Result n in native)
        {
            if (n.Index < 0 || n.Index >= results.Length || results[n.Index] != null || n.Offset < 0 || n.Length < 0 || n.Offset > buffer.Length - n.Length) throw new InvalidOperationException("Group probe output identity/bounds");
            var path = new byte[n.Length]; Array.Copy(buffer, n.Offset, path, 0, path.Length);
            LastStatus[n.Index] = n.Status;
            results[n.Index] = new RouteResult { Status = n.Status == 0 ? SearchStatus.Found : n.Status == 1 ? SearchStatus.Unreachable : SearchStatus.Inconclusive, Reason = n.Status == OverLimit ? "over-2000-steps" : null, Ground = n.Ground, Moat = n.Moat, Cost = n.Cost, Directions = path };
        }
        LastMilliseconds = clock.Elapsed.TotalMilliseconds;
        return results;
    }
    public void Dispose() { if (context != IntPtr.Zero) { gp_destroy(context); context = IntPtr.Zero; } }
}
