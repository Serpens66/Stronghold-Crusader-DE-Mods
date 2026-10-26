using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace BetterPathfindingTest.Core
{
    // Versioned, bounded, little-endian replay data. No native pointers are serialized.
    public static class SnapshotCodec
    {
        private const int RawMagic = 0x31504642, QueryMagic = 0x31514642;
        private static BinaryWriter Writer(string path) => new BinaryWriter(new GZipStream(File.Create(path), CompressionMode.Compress));
        private static BinaryReader Reader(string path) => new BinaryReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress));
        private static void Bytes(BinaryWriter w, byte[] values) { values = values ?? new byte[0]; w.Write(values.Length); w.Write(values); }
        private static void Ints(BinaryWriter w, int[] values) { w.Write(values.Length); foreach (int value in values) w.Write(value); }
        private static void Shorts(BinaryWriter w, short[] values) { w.Write(values.Length); foreach (short value in values) w.Write(value); }
        private static void UShorts(BinaryWriter w, ushort[] values) { w.Write(values.Length); foreach (ushort value in values) w.Write(value); }
        private static int Count(BinaryReader r, int max) { int count = r.ReadInt32(); if (count < 0 || count > max) throw new InvalidDataException("Replay length."); return count; }
        private static byte[] Bytes(BinaryReader r, int max) { int count = Count(r, max); byte[] values = r.ReadBytes(count); if (values.Length != count) throw new EndOfStreamException(); return values; }
        private static int[] Ints(BinaryReader r, int max) { var values = new int[Count(r, max)]; for (int i = 0; i < values.Length; i++) values[i] = r.ReadInt32(); return values; }
        private static short[] Shorts(BinaryReader r, int max) { var values = new short[Count(r, max)]; for (int i = 0; i < values.Length; i++) values[i] = r.ReadInt16(); return values; }
        private static ushort[] UShorts(BinaryReader r, int max) { var values = new ushort[Count(r, max)]; for (int i = 0; i < values.Length; i++) values[i] = r.ReadUInt16(); return values; }
        private static string Text(BinaryReader r) { string value = r.ReadString(); if (value.Length > 4096) throw new InvalidDataException("Replay text."); return value; }
        private static void End(BinaryReader r) { if (r.BaseStream.ReadByte() != -1) throw new InvalidDataException("Replay trailing bytes."); }
        public static void WriteCaptured(string path, CapturedMap map)
        {
            map.Validate(); using (BinaryWriter w = Writer(path))
            {
                w.Write(RawMagic); w.Write(1); w.Write(map.NativeHash); w.Write(map.ManagedHash); w.Write(map.Identity); w.Write(map.GateCoverage); w.Write(map.Context); w.Write(map.Session); w.Write(map.Width); w.Write(map.Height); w.Write(map.MapSize); w.Write(map.CopyMilliseconds); w.Write(map.LockWaitMilliseconds);
                Bytes(w, map.Available); Bytes(w, map.Heights); Bytes(w, map.NativeEdges); Bytes(w, map.MoatOwner);
                w.Write(map.Flags.Length); foreach (uint value in map.Flags) w.Write(value);
                Ints(w, map.Rows); UShorts(w, map.Structures); UShorts(w, map.Organisms); UShorts(w, map.BuildingTypes); Shorts(w, map.StructureHeights); Shorts(w, map.VegetationStates); Bytes(w, map.LowWalls);
                foreach (bool value in map.Allied) w.Write(value); Ints(w, map.Players);
                for (int i = 0; i < 9; i++) Bytes(w, map.GateMasks[i]);
                w.Write(map.Units.Length); foreach (UnitSeed u in map.Units) { w.Write(u.Id); w.Write(u.Player); w.Write(u.Node); w.Write(u.Target); w.Write(u.Speed); w.Write(u.Speed2); w.Write(u.Bonus); w.Write(u.Extra); w.Write(u.Additional); w.Write(u.Phase); w.Write(u.OnMoat); }
            }
        }
        public static CapturedMap ReadCaptured(string path)
        {
            using (BinaryReader r = Reader(path))
            {
                if (r.ReadInt32() != RawMagic || r.ReadInt32() != 1) throw new InvalidDataException("Captured replay version.");
                var m = new CapturedMap { NativeHash = Text(r), ManagedHash = Text(r), Identity = Text(r), GateCoverage = Text(r), Context = Text(r), Session = r.ReadInt64(), Width = r.ReadInt32(), Height = r.ReadInt32(), MapSize = r.ReadInt32(), CopyMilliseconds = r.ReadDouble(), LockWaitMilliseconds = r.ReadDouble() };
                m.Available = Bytes(r, 640000); m.Heights = Bytes(r, 320800); m.NativeEdges = Bytes(r, 320800); m.MoatOwner = Bytes(r, 320800);
                m.Flags = new uint[Count(r, 320800)]; for (int i = 0; i < m.Flags.Length; i++) m.Flags[i] = r.ReadUInt32();
                m.Rows = Ints(r, 2400); m.Structures = UShorts(r, 320800); m.Organisms = UShorts(r, 320800); m.BuildingTypes = UShorts(r, 10001); m.StructureHeights = Shorts(r, 10001); m.VegetationStates = Shorts(r, 65536); m.LowWalls = Bytes(r, 10001);
                for (int i = 0; i < 81; i++) m.Allied[i] = r.ReadBoolean(); m.Players = Ints(r, 8);
                for (int i = 0; i < 9; i++) { byte[] mask = Bytes(r, 320800); m.GateMasks[i] = mask.Length == 0 ? null : mask; }
                m.Units = new UnitSeed[Count(r, 10000)];
                for (int i = 0; i < m.Units.Length; i++) m.Units[i] = new UnitSeed { Id = r.ReadInt32(), Player = r.ReadInt32(), Node = r.ReadInt32(), Target = r.ReadInt32(), Speed = r.ReadInt32(), Speed2 = r.ReadInt32(), Bonus = r.ReadInt32(), Extra = r.ReadInt32(), Additional = r.ReadInt32(), Phase = r.ReadInt32(), OnMoat = r.ReadBoolean() };
                End(r); m.Validate(); return m;
            }
        }
        public static void WriteQueries(string path, GridSnapshot map, IList<RouteQuery> queries)
        {
            using (BinaryWriter w = Writer(path))
            {
                w.Write(QueryMagic); w.Write(1); w.Write(map.Width); w.Write(map.Height); w.Write(map.Player); Bytes(w, map.CopyValidity()); uint[] words = map.CopyEdges(); w.Write(words.Length); foreach (uint word in words) w.Write(word);
                w.Write(queries.Count);
                foreach (RouteQuery q in queries)
                {
                    w.Write(q.Start); w.Write(q.Target); w.Write(q.GroundCost); w.Write(q.MoatCost); w.Write(q.MaximumEdges); w.Write(q.RequireMoat); w.Write(q.ExcludeStructures);
                    w.Write(q.Bounds.Length); foreach (CostBound b in q.Bounds) { w.Write(b.Ground); w.Write(b.Moat); w.Write(b.Maximum); }
                    w.Write(q.Terminals.Length); foreach (TerminalEdge t in q.Terminals) { w.Write(t.From); w.Write(t.To); w.Write(t.Moat); w.Write(t.Structure); }
                }
            }
        }
        public static RouteQuery[] ReadQueries(string path, out GridSnapshot map)
        {
            using (BinaryReader r = Reader(path))
            {
                if (r.ReadInt32() != QueryMagic || r.ReadInt32() != 1) throw new InvalidDataException("Query replay version.");
                int width = r.ReadInt32(), height = r.ReadInt32(), player = r.ReadInt32(); byte[] valid = Bytes(r, 640000); uint[] words = new uint[Count(r, 640000)]; for (int i = 0; i < words.Length; i++) words[i] = r.ReadUInt32(); map = new GridSnapshot(width, height, valid, words, player);
                var queries = new RouteQuery[Count(r, 4096)];
                for (int i = 0; i < queries.Length; i++)
                {
                    var q = new RouteQuery { Start = r.ReadInt32(), Target = r.ReadInt32(), GroundCost = r.ReadInt64(), MoatCost = r.ReadInt64(), MaximumEdges = r.ReadInt32(), RequireMoat = r.ReadBoolean(), ExcludeStructures = r.ReadBoolean() };
                    q.Bounds = new CostBound[Count(r, 32)]; for (int j = 0; j < q.Bounds.Length; j++) q.Bounds[j] = new CostBound(r.ReadInt64(), r.ReadInt64(), r.ReadInt64());
                    q.Terminals = new TerminalEdge[Count(r, 64)]; for (int j = 0; j < q.Terminals.Length; j++) q.Terminals[j] = new TerminalEdge(r.ReadInt32(), r.ReadInt32(), r.ReadBoolean(), r.ReadBoolean());
                    q.Validate(map); queries[i] = q;
                }
                End(r); return queries;
            }
        }
    }
}
