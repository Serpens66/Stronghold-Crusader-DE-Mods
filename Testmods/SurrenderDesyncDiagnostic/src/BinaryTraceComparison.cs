using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SurrenderDesyncDiagnostic
{
    // Read-only offline parser. It keeps only offsets during indexing and loads one snapshot pair at a time.
    public static class BinaryTraceComparison
    {
        private const int Magic = 0x34444453;
        private sealed class CaptureRef
        {
            internal string Path;
            internal long Offset;
            internal int Tick;
            internal long Order;
            internal string Phase;
            internal string Key;
        }
        private sealed class TraceIndex
        {
            internal readonly List<CaptureRef> Captures = new List<CaptureRef>();
            internal readonly List<string> Events = new List<string>();
            internal bool Finished;
            internal bool Incomplete;
            internal int FooterSnapshots;
            internal int FooterCategories;
            internal int FooterEvents;
            internal int ActualCategories;
            internal int ActualEvents;
        }

        public static bool ValidateProbe(string pattern)
        {
            try
            {
                TraceIndex index = Index(pattern);
                if (!index.Finished || index.Incomplete || index.Captures.Count != 1 ||
                    index.FooterCategories != 10) return false;
                TraceSnapshot snapshot = Read(index.Captures[0]);
                if (snapshot.Categories.Count != 10 || snapshot.Categories.Any(c => c.Error != null))
                    return false;
                foreach (TraceCategory category in snapshot.Categories) Project(category);
                return true;
            }
            catch { return false; }
        }

        public static string Compare(string hostPattern, string clientPattern)
        {
            TraceIndex host = Index(hostPattern), client = Index(clientPattern);
            if (!host.Finished || host.Incomplete)
                throw new InvalidDataException("Host trace incomplete or missing completion marker: " + hostPattern);
            if (!client.Finished || client.Incomplete)
                throw new InvalidDataException("Client trace incomplete or missing completion marker: " + clientPattern);
            var clientByKey = client.Captures.ToDictionary(c => c.Key, StringComparer.Ordinal);
            int common = 0, uncertainProjectileSamples = 0;
            string firstUncertain = null;
            foreach (CaptureRef leftRef in host.Captures.OrderBy(c => c.Tick).ThenBy(c => c.Order))
            {
                if (!clientByKey.TryGetValue(leftRef.Key, out CaptureRef rightRef)) continue;
                common++;
                TraceSnapshot left = Read(leftRef), right = Read(rightRef);
                var rightCategories = right.Categories.ToDictionary(c => c.Name, StringComparer.Ordinal);
                if (left.Categories.Count != right.Categories.Count)
                    return "FIRST_DIFFERENCE mapTick=" + left.MapTick + " phase=" + left.Phase +
                        " category-list host=" + string.Join(",", left.Categories.Select(c => c.Name)) +
                        " client=" + string.Join(",", right.Categories.Select(c => c.Name));
                foreach (TraceCategory lc in left.Categories.OrderBy(c => c.Name, StringComparer.Ordinal))
                {
                    if (!rightCategories.TryGetValue(lc.Name, out TraceCategory rc))
                        return "FIRST_DIFFERENCE mapTick=" + left.MapTick + " phase=" + left.Phase +
                            " category=" + lc.Name + " client=<category-absent>";
                    if (lc.Error != null || rc.Error != null)
                        throw new InvalidDataException("Capture gap at map tick " + left.MapTick + ": " + lc.Name);
                    SortedDictionary<string, string> a = Project(lc), b = Project(rc);
                    string ah = Hash(a), bh = Hash(b);
                    if (ah == bh) continue;
                    bool uncertainHere = false;
                    foreach (string key in a.Keys.Concat(b.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal))
                    {
                        string av = a.TryGetValue(key, out string x) ? x : "<absent>";
                        string bv = b.TryGetValue(key, out string y) ? y : "<absent>";
                        if (av == bv) continue;
                        string field = FirstFieldDifference(av, bv);
                        string report = "mapTick=" + left.MapTick + " phase=" + left.Phase +
                            " category=" + lc.Name + " object=" + key + " " + field +
                            " hostHash=" + ah + " clientHash=" + bh;
                        if (lc.Name == "projectiles" && key.EndsWith("/0", StringComparison.Ordinal))
                        {
                            uncertainHere = true;
                            if (firstUncertain == null) firstUncertain = report;
                            continue;
                        }
                        return "FIRST_DIFFERENCE " + report +
                            " precedingUnkeyedProjectileSamples=" + uncertainProjectileSamples;
                    }
                    if (uncertainHere) uncertainProjectileSamples++;
                    else return "HASH_DIFFERENCE_WITHOUT_CAPTURED_FIELD mapTick=" + left.MapTick +
                        " phase=" + left.Phase + " category=" + lc.Name;
                }
            }
            if (common == 0) throw new InvalidDataException("No common map-tick/phase snapshots");
            if (firstUncertain != null)
                return "ONLY_UNKEYED_PROJECTILE_DIFFERENCE " + firstUncertain +
                    " differingSamples=" + uncertainProjectileSamples + " commonSnapshots=" + common +
                    "; all other captured fields match. Remaining gaps: unknown, nested, pointer and unsupported native state.";
            return "NO_CAPTURED_DIFFERENCE commonSnapshots=" + common +
                "; remaining gaps: unknown, nested, pointer and unsupported native state.";
        }

        public static string[] Events(string pattern)
        {
            TraceIndex index = Index(pattern);
            if (!index.Finished || index.Incomplete)
                throw new InvalidDataException("Trace incomplete or missing completion marker: " + pattern);
            return index.Events.ToArray();
        }

        private static TraceIndex Index(string pattern)
        {
            string directory = Path.GetDirectoryName(pattern);
            string[] files = Directory.GetFiles(directory, Path.GetFileName(pattern))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (files.Length == 0) throw new FileNotFoundException("No binary trace segments", pattern);
            var result = new TraceIndex();
            var occurrence = new Dictionary<string, int>(StringComparer.Ordinal);
            string buildId = null;
            try
            {
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    using (var stream = new FileStream(files[fileIndex], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new BinaryReader(stream, Encoding.UTF8))
                    {
                    if (reader.ReadInt32() != Magic || reader.ReadInt32() != fileIndex + 1)
                        throw new InvalidDataException("Invalid segment header: " + files[fileIndex]);
                    string id = reader.ReadString();
                    if (buildId == null) buildId = id;
                    if (id != buildId || id != typeof(BinaryTraceComparison).Assembly.ManifestModule.ModuleVersionId.ToString("D"))
                        throw new InvalidDataException("Trace and comparison DLL builds differ: " + files[fileIndex]);
                    while (stream.Position < stream.Length)
                    {
                        if (result.Finished) throw new InvalidDataException("Data after completion footer");
                        long offset = stream.Position;
                        byte kind = reader.ReadByte();
                        switch (kind)
                        {
                            case 1:
                                string line = reader.ReadString();
                                if (line.StartsWith("I\t", StringComparison.Ordinal)) result.Incomplete = true;
                                result.Events.Add(line);
                                result.ActualEvents++;
                                break;
                            case 2:
                                var capture = new CaptureRef { Path = files[fileIndex], Offset = offset };
                                capture.Tick = reader.ReadInt32();
                                reader.ReadInt32(); // director tick
                                capture.Order = reader.ReadInt64();
                                capture.Phase = reader.ReadString();
                                reader.ReadString(); // local UI
                                string identity = capture.Tick + "|" + capture.Phase;
                                occurrence.TryGetValue(identity, out int count);
                                occurrence[identity] = ++count;
                                capture.Key = identity + "|" + count;
                                int categories = reader.ReadInt32();
                                if (categories < 0) throw new InvalidDataException("Negative category count");
                                result.ActualCategories += categories;
                                for (int c = 0; c < categories; c++)
                                {
                                    reader.ReadString(); // category
                                    if (reader.ReadString().Length != 0) result.Incomplete = true;
                                    int records = reader.ReadInt32();
                                    if (records < 0) throw new InvalidDataException("Negative record count");
                                    for (int r = 0; r < records; r++)
                                    {
                                        reader.ReadString(); // key
                                        reader.ReadString(); // type
                                        int bytes = reader.ReadInt32();
                                        if (bytes == -1) reader.ReadString();
                                        else Skip(stream, bytes);
                                    }
                                    int gridLength = reader.ReadInt32();
                                    int elementBytes = reader.ReadInt32();
                                    Skip(stream, checked(gridLength * elementBytes));
                                }
                                result.Captures.Add(capture);
                                break;
                            case 3:
                                reader.ReadString(); // reason
                                result.Incomplete |= reader.ReadBoolean();
                                result.FooterSnapshots = reader.ReadInt32();
                                result.FooterCategories = reader.ReadInt32();
                                result.FooterEvents = reader.ReadInt32();
                                reader.ReadInt64(); // high water
                                reader.ReadInt64(); // waits
                                reader.ReadInt64(); // wait ticks
                                result.Finished = true;
                                if (fileIndex != files.Length - 1 || stream.Position != stream.Length)
                                    throw new InvalidDataException("Completion marker is not final");
                                break;
                            default: throw new InvalidDataException("Unknown binary record kind " + kind);
                        }
                    }
                    }
                }
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidDataException("Trace incomplete or missing completion marker: " + pattern, ex);
            }
            if (result.Finished && (result.FooterSnapshots != result.Captures.Count ||
                result.FooterCategories != result.ActualCategories ||
                result.FooterEvents != result.ActualEvents))
                throw new InvalidDataException("Trace footer counts do not match records");
            return result;
        }

        private static TraceSnapshot Read(CaptureRef capture)
        {
            using (var stream = new FileStream(capture.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                stream.Position = capture.Offset;
                if (reader.ReadByte() != 2) throw new InvalidDataException("Capture offset is invalid");
                var snapshot = new TraceSnapshot
                {
                    MapTick = reader.ReadInt32(), DirectorTick = reader.ReadInt32(),
                    Order = reader.ReadInt64(), Phase = reader.ReadString(), LocalState = reader.ReadString()
                };
                int categories = reader.ReadInt32();
                for (int c = 0; c < categories; c++)
                {
                    var category = new TraceCategory { Name = reader.ReadString(), Error = reader.ReadString() };
                    if (category.Error.Length == 0) category.Error = null;
                    int records = reader.ReadInt32();
                    for (int r = 0; r < records; r++)
                    {
                        string key = reader.ReadString(), typeName = reader.ReadString();
                        int length = reader.ReadInt32();
                        if (length == -1)
                            category.Records.Add(new TraceRecord { Key = key, Text = reader.ReadString() });
                        else
                        {
                            Type type = typeof(GamePlayerResources).Assembly.GetType(typeName, true);
                            byte[] data = reader.ReadBytes(length);
                            if (data.Length != length) throw new EndOfStreamException();
                            category.Records.Add(new TraceRecord { Key = key, Type = type, Data = data,
                                DataLength = length });
                        }
                    }
                    category.GridLength = reader.ReadInt32();
                    category.GridElementBytes = reader.ReadInt32();
                    int gridBytes = checked(category.GridLength * category.GridElementBytes);
                    if (gridBytes != 0)
                    {
                        category.Grid = reader.ReadBytes(gridBytes);
                        if (category.Grid.Length != gridBytes) throw new EndOfStreamException();
                    }
                    snapshot.Categories.Add(category);
                }
                return snapshot;
            }
        }

        private static void Skip(Stream stream, int count)
        {
            if (count < 0 || count > stream.Length - stream.Position)
                throw new EndOfStreamException();
            stream.Seek(count, SeekOrigin.Current);
        }

        private static SortedDictionary<string, string> Project(TraceCategory category)
        {
            var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (TraceRecord record in category.Records)
            {
                if (record.Data == null) rows.Add(record.Key, record.Text ?? "");
                else
                {
                    if (record.DataLength < Marshal.SizeOf(record.Type))
                        throw new InvalidDataException("Short native record: " + record.Key);
                    var handle = GCHandle.Alloc(record.Data, GCHandleType.Pinned);
                    try { rows.Add(record.Key, SurrenderDesyncDiagnosticRuntime.Fields(record.Type,
                        handle.AddrOfPinnedObject())); }
                    finally { handle.Free(); }
                }
            }
            if (category.Grid != null)
            {
                if (category.GridElementBytes != 1 && category.GridElementBytes != 2)
                    throw new InvalidDataException("Unsupported grid element width: " + category.Name);
                for (int start = 0; start < category.GridLength; start += 256)
                {
                    var text = new StringBuilder();
                    for (int n = start; n < Math.Min(start + 256, category.GridLength); n++)
                        text.Append(category.GridElementBytes == 1 ? category.Grid[n] :
                            BitConverter.ToUInt16(category.Grid, n * 2)).Append(',');
                    rows.Add(category.Name + "/" + start + "/0", text.ToString());
                }
            }
            return rows;
        }

        private static string Hash(SortedDictionary<string, string> rows)
        {
            using (var sha = SHA256.Create())
            {
                var value = new StringBuilder();
                foreach (var row in rows) value.Append(row.Key).Append('=').Append(row.Value).Append(';');
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value.ToString())))
                    .Replace("-", "");
            }
        }

        private static string FirstFieldDifference(string left, string right)
        {
            Dictionary<string, string> a = Fields(left), b = Fields(right);
            foreach (string field in a.Keys.Concat(b.Keys).Distinct().OrderBy(f => f, StringComparer.Ordinal))
            {
                string av = a.TryGetValue(field, out string x) ? x : "<absent>";
                string bv = b.TryGetValue(field, out string y) ? y : "<absent>";
                if (av != bv) return "field=" + field + " host=" + av + " client=" + bv;
            }
            return "host=" + left + " client=" + right;
        }

        private static Dictionary<string, string> Fields(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string part in text.Split(','))
            {
                int equals = part.IndexOf('=');
                if (equals >= 0) result[part.Substring(0, equals)] = part.Substring(equals + 1);
            }
            return result;
        }
    }
}
