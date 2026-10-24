using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CastlePlanner
{
    internal static class VisualDiagnosticSavePlayers
    {
        // A local save has no obligation to populate the multiplayer game-member roster.
        // IDs supplied by MissionContext are already normalized by the lifecycle owner.
        internal static int[] Select(bool realMultiplayer, int? localPlayer, IEnumerable<int> humanRoster)
        {
            var selected = new SortedSet<int>();
            if (realMultiplayer && humanRoster != null)
                foreach (int id in humanRoster) if (id > 0) selected.Add(id);
            if (localPlayer.HasValue && localPlayer.Value > 0) selected.Add(localPlayer.Value);
            var result = new int[selected.Count]; selected.CopyTo(result); return result;
        }
    }
    internal sealed class VisualDiagnosticSessionState
    {
        private readonly long session;
        internal bool Active { get; private set; }
        private bool ended;
        internal VisualDiagnosticSessionState(long session, bool isSave) { this.session = session; Active = !isSave; }
        internal bool ConfirmSave(long id)
        {
            if (ended || Active || id != session) return false;
            Active = true; return true;
        }
        internal void End() { ended = true; Active = false; }
    }
    // Tested independently of Unity. Original input arrays never escape the callback.
    internal static class VisualDiagnosticPackets
    {
        internal const int Width = 21;
        internal static int Count(short[] source, int requested, out bool bounded)
        {
            bounded = false;
            if (source == null) { bounded = true; return 0; }
            int capacity = source.Length / Width;
            if (requested >= 0) { bounded = requested > capacity; return Math.Min(requested, capacity); }
            for (int i = 0; i < capacity; i++) if (source[i * Width] < 0) return i;
            bounded = true; return capacity;
        }
        internal static short[] Copy(short[] source, int ordinal)
        {
            var result = new short[Width];
            Array.Copy(source, checked(ordinal * Width), result, 0, Width);
            return result;
        }
        internal static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }

    internal sealed class VisualDiagnosticTraceBuffer
    {
        internal const int MaximumBytes = 8 * 1024 * 1024;
        private readonly Queue<string> entries = new Queue<string>();
        private readonly Dictionary<string, string> previous = new Dictionary<string, string>();
        private readonly int maximumBytes;
        private int bytes;
        private int previousBytes;
        private long sequence;
        internal long Dropped { get; private set; }
        internal int Bytes => bytes;
        internal VisualDiagnosticTraceBuffer(int maximumBytes = MaximumBytes) { this.maximumBytes = maximumBytes; }
        // Order-bearing events bypass deduplication. Baseline states use a bounded identity cache.
        internal void Add(string metadata, string kind, string key, string payload, bool ordered = false)
        {
            string identity = kind + ":" + key;
            if (!ordered && previous.TryGetValue(identity, out string old) && old == payload) return;
            if (!ordered)
            {
                int payloadBytes = Encoding.UTF8.GetByteCount(payload);
                if (previous.Count >= 16384 || previousBytes + payloadBytes > maximumBytes)
                { previous.Clear(); previousBytes = 0; }
                if (previous.TryGetValue(identity, out string prior)) previousBytes -= Encoding.UTF8.GetByteCount(prior);
                if (payloadBytes <= maximumBytes) { previous[identity] = payload; previousBytes += payloadBytes; }
            }
            string line = (++sequence) + "\t" + metadata + "\t" + kind + "\t" + key + "\t" + payload + "\r\n";
            int size = Encoding.UTF8.GetByteCount(line);
            if (size > maximumBytes) { Dropped++; return; }
            while (bytes + size > maximumBytes && entries.Count != 0)
            { bytes -= Encoding.UTF8.GetByteCount(entries.Dequeue()); Dropped++; }
            entries.Enqueue(line); bytes += size;
        }
        internal string Drain()
        {
            var text = new StringBuilder(bytes);
            while (entries.Count != 0) text.Append(entries.Dequeue());
            bytes = 0; return text.ToString();
        }
    }

    internal static class VisualDiagnosticCall
    {
        // Observation exceptions are isolated from the exactly-once original call.
        internal static void Observe(Action action, Action<Exception> report)
        { try { action(); } catch (Exception error) { try { report(error); } catch { } } }
        internal static void Around(Action before, Action original, Action after, Action<Exception> report)
        { Observe(before, report); original(); Observe(after, report); }
    }

    internal readonly struct VisualDiagnosticRegion
    {
        internal readonly int MinX, MinY, MaxX, MaxY;
        internal VisualDiagnosticRegion(int minX, int minY, int maxX, int maxY)
        { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }
        internal bool Contains(int x, int y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
        internal bool Intersects(int x, int y, int size) => x <= MaxX && y <= MaxY &&
            (long)x + Math.Max(1, size) - 1 >= MinX && (long)y + Math.Max(1, size) - 1 >= MinY;
        internal bool Valid => MinX <= MaxX && MinY <= MaxY && (long)MaxX - MinX < 110 && (long)MaxY - MinY < 110;
        internal static bool IsReadableTile(int id, int capacity) => id >= 0 && id < capacity;
        public override string ToString() => $"{MinX},{MinY}..{MaxX},{MaxY}";
    }

    internal sealed class VisualDiagnosticIdentities
    {
        private readonly System.Collections.Generic.Dictionary<int, uint> identities = new System.Collections.Generic.Dictionary<int, uint>();
        internal bool Observe(int id, uint global, out uint previous)
        {
            bool changed = identities.TryGetValue(id, out previous) && previous != global;
            identities[id] = global;
            return changed;
        }
    }

    internal sealed class VisualDiagnosticWindow
    {
        private readonly double started;
        private double nextChange;
        private int lastRotation = int.MinValue, lastX = int.MinValue, lastY, lastBuilding;
        private bool lastVisible, initial, oneSecond, fiveSeconds;
        internal VisualDiagnosticWindow(double now) { started = now; }
        internal bool Expired(double now) => now - started >= 185;
        internal string Take(double now, int rotation, bool visible, int x, int y, int building)
        {
            if (Expired(now) || now < nextChange) return null;
            string phase = null;
            if (!initial) { initial = true; phase = "first-ui-frame"; }
            else if (!oneSecond && now - started >= 1) { oneSecond = true; phase = "after-one-second"; }
            else if (!fiveSeconds && now - started >= 5) { fiveSeconds = true; phase = "after-five-seconds"; }
            else if (now >= nextChange)
            {
                if (rotation != lastRotation) phase = "camera-rotation";
                else if (visible != lastVisible) phase = "blueprint-visibility";
                else if (x != lastX || y != lastY || building != lastBuilding) phase = "hover-change";
                else if (x != int.MinValue) phase = "hover-observe";
            }
            if (phase == null || now < nextChange) return null;
            lastRotation = rotation; lastVisible = visible; lastX = x; lastY = y; lastBuilding = building;
            nextChange = now + 0.5;
            return phase;
        }
    }

    // Only immutable strings cross to this worker. Neither pointers nor Unity objects escape.
    internal sealed class VisualDiagnosticWriter
    {
        internal const long MaximumBytes = 32L * 1024 * 1024;
        private readonly BlockingCollection<WriteItem> queue = new BlockingCollection<WriteItem>(8);
        private readonly Action<string> report;
        private readonly string directory;
        private readonly long maximumBytes;
        private readonly object gate = new object();
        private readonly Task worker;
        private long acceptedBytes, dropped;
        private int sequence;
        private int acceptedCaptures;
        private bool closed, failed;
        internal VisualDiagnosticWriter(string directory, Action<string> report, long maximumBytes = MaximumBytes)
        {
            this.directory = directory; this.report = report; this.maximumBytes = maximumBytes;
            worker = Task.Run((Action)WriteLoop);
        }
        internal Task Completion => worker;
        internal long Dropped { get { lock (gate) return dropped; } }
        internal bool Enqueue(string phase, string text)
        {
            lock (gate)
            {
                long bytes = Encoding.UTF8.GetByteCount(text);
                if (closed || failed || bytes > maximumBytes - acceptedBytes ||
                    !queue.TryAdd(new WriteItem(++sequence, phase, text)))
                {
                    dropped++;
                    if (dropped == 1 || (dropped & (dropped - 1)) == 0)
                        Report($"capture-dropped count={dropped}, closed={closed}, failed={failed}, acceptedBytes={acceptedBytes}, limit={maximumBytes}");
                    return false;
                }
                acceptedBytes += bytes;
                acceptedCaptures++;
                return true;
            }
        }
        internal void Complete()
        {
            lock (gate)
            {
                if (closed) return;
                closed = true; queue.CompleteAdding();
                Report($"capture-ended acceptedCaptures={acceptedCaptures}, acceptedBytes={acceptedBytes}, dropped={dropped}, directory={directory}");
            }
        }
        private void WriteLoop()
        {
            try
            {
                Directory.CreateDirectory(directory);
                foreach (WriteItem item in queue.GetConsumingEnumerable())
                {
                    string path = Path.Combine(directory, item.Sequence.ToString("D4") + "-" + item.Phase + ".tsv");
                    File.WriteAllText(path, item.Text, new UTF8Encoding(false));
                    Report($"snapshot-written phase={item.Phase}, file={path}");
                }
                Report("writer-drained directory=" + directory);
            }
            catch (Exception ex)
            {
                lock (gate) failed = true;
                Report("writer-failed error=" + ex.GetBaseException().Message + ", directory=" + directory);
            }
        }
        private void Report(string text) { try { report(text); } catch { } }
        private sealed class WriteItem
        {
            internal readonly int Sequence;
            internal readonly string Phase, Text;
            internal WriteItem(int sequence, string phase, string text) { Sequence = sequence; Phase = phase; Text = text; }
        }
    }
}
