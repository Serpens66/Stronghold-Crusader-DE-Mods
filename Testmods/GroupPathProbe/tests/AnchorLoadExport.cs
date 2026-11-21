using System;
using System.Collections.Generic;
using System.IO;
using GroupPathProbe.Core;

// Exports the captured and synthetic group loads as a plain binary file for the native route
// shape benchmark (Pathfinding/tools/RouteShape). Words are already in the FastTraversalCache
// layout of the anchor core, so the benchmark needs neither this probe nor the capture codec.
// Format (little-endian): "RSB1", map count, per map: width, height, n uint32 words;
// load count, per load: name (int32 length + UTF-8), map index, group count, anchors,
// query count, per query: start, target, group, ground cost, moat cost.
internal static class AnchorLoadExport
{
    internal static int Run(string capture, string output)
    {
        CapturedMap raw = SnapshotCodec.ReadCaptured(capture);
        GridSnapshot captured = raw.Build(raw.Players[0]);
        var loads = GroupWorkloads.Captured(captured, raw.Units);
        GridSnapshot moat = GroupWorkloads.MoatMap();
        loads.Add(GroupWorkloads.MoatMix(moat));
        var maps = new List<GridSnapshot> { captured, moat };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        using (var w = new BinaryWriter(File.Create(output)))
        {
            w.Write(0x31425352); w.Write(maps.Count);
            foreach (GridSnapshot map in maps)
            {
                w.Write(map.Width); w.Write(map.Height);
                for (int n = 0; n < map.Count; n++) w.Write(AnchorCoreInterop.FastWord(map.Word(n)));
            }
            w.Write(loads.Count);
            foreach (GroupLoad load in loads)
            {
                byte[] name = System.Text.Encoding.UTF8.GetBytes(load.Name);
                w.Write(name.Length); w.Write(name); w.Write(maps.IndexOf(load.Map));
                w.Write(load.Anchors.Count); foreach (int anchor in load.Anchors) w.Write(anchor);
                w.Write(load.Queries.Count);
                for (int i = 0; i < load.Queries.Count; i++)
                {
                    RouteQuery q = load.Queries[i];
                    w.Write(q.Start); w.Write(q.Target); w.Write(load.GroupOf[i]); w.Write(checked((int)q.GroundCost)); w.Write(checked((int)q.MoatCost));
                }
            }
        }
        Console.WriteLine("EXPORT maps=" + maps.Count + " loads=" + loads.Count + " file=" + Path.GetFullPath(output));
        return 0;
    }
}
