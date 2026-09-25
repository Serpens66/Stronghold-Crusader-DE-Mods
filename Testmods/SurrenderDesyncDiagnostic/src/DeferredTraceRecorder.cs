using BepInEx.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace SurrenderDesyncDiagnostic
{
    internal enum TraceWorkKind { Start, Line, Capture, Finish }

    internal sealed class TraceRecord
    {
        internal string Key;
        internal Type Type;
        internal byte[] Data;
        internal string Text;
    }

    internal sealed class TraceCategory
    {
        internal string Name;
        internal readonly List<TraceRecord> Records = new List<TraceRecord>();
        internal byte[] Grid;
        internal int GridLength;
        internal int GridElementBytes;
        internal string Error;

        internal long RentedBytes
        {
            get
            {
                long size = Grid?.Length ?? 0;
                foreach (TraceRecord record in Records) size += record.Data?.Length ?? 0;
                return size;
            }
        }

        internal void Release()
        {
            foreach (TraceRecord record in Records)
                if (record.Data != null) ArrayPool<byte>.Shared.Return(record.Data);
            Records.Clear();
            if (Grid != null) ArrayPool<byte>.Shared.Return(Grid);
            Grid = null;
        }
    }

    internal sealed class TraceSnapshot
    {
        internal int MapTick;
        internal int DirectorTick;
        internal long Order;
        internal string Phase;
        internal string LocalState;
        internal readonly List<TraceCategory> Categories = new List<TraceCategory>();

        internal long RentedBytes
        {
            get
            {
                long size = 0;
                foreach (TraceCategory category in Categories) size += category.RentedBytes;
                return size;
            }
        }

        internal void Release()
        {
            foreach (TraceCategory category in Categories) category.Release();
        }
    }

    internal sealed class TraceWork
    {
        internal TraceWorkKind Kind;
        internal string TraceBase;
        internal string Text;
        internal string Reason;
        internal bool Probe;
        internal TraceSnapshot Snapshot;
        internal long Size => Snapshot?.RentedBytes ?? 0;
    }

    internal sealed class DeferredTraceRecorder
    {
        private const long MaxQueuedBytes = 512L * 1024 * 1024;
        private const long SegmentBytes = 32L * 1024 * 1024;
        private readonly object queueGate = new object();
        private readonly Queue<TraceWork> queue = new Queue<TraceWork>();
        private readonly ManualLogSource log;
        private readonly Thread thread;
        private long queuedBytes;
        private long highWaterBytes;
        private StreamWriter writer;
        private string traceBase;
        private int segment;
        private bool incomplete;
        private bool ioFailed;
        private volatile string failedTraceBase;
        private bool probe;
        private int sampleCount;
        private int changeCount;
        private readonly Dictionary<string, string> previous = new Dictionary<string, string>();
        private DateTime lastFlushUtc;

        internal DeferredTraceRecorder(ManualLogSource logger)
        {
            log = logger;
            thread = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.BelowNormal,
                Name = "Surrender diagnostic writer" };
            thread.Start();
        }

        internal bool Enqueue(TraceWork work)
        {
            lock (queueGate)
            {
                if (work.Size > MaxQueuedBytes - queuedBytes) return false;
                queue.Enqueue(work);
                queuedBytes += work.Size;
                if (queuedBytes > highWaterBytes) highWaterBytes = queuedBytes;
                Monitor.Pulse(queueGate);
                return true;
            }
        }

        internal bool FailedFor(string candidate) =>
            candidate != null && string.Equals(failedTraceBase, candidate, StringComparison.Ordinal);

        private void Run()
        {
            while (true)
            {
                TraceWork work = null;
                lock (queueGate)
                {
                    if (queue.Count == 0) Monitor.Wait(queueGate, 1000);
                    if (queue.Count != 0) work = queue.Dequeue();
                }
                if (work == null)
                {
                    try { FlushIfDue(); }
                    catch (Exception ex)
                    {
                        MarkIncomplete("FLUSH", ex);
                        ioFailed = true;
                        failedTraceBase = traceBase;
                        try { writer?.Dispose(); } catch { }
                        writer = null;
                    }
                    continue;
                }
                long size = work.Size;
                try { Process(work); }
                catch (Exception ex)
                {
                    MarkIncomplete("WORKER", ex);
                    ioFailed = true;
                    failedTraceBase = traceBase;
                    try { writer?.Dispose(); } catch { }
                    writer = null;
                }
                finally
                {
                    try { work.Snapshot?.Release(); }
                    catch (Exception ex) { MarkIncomplete("BUFFER_RELEASE", ex); }
                    finally { lock (queueGate) queuedBytes -= size; }
                }
            }
        }

        private void Process(TraceWork work)
        {
            switch (work.Kind)
            {
                case TraceWorkKind.Start: Start(work); break;
                case TraceWorkKind.Line:
                    if (work.Text != null && work.Text.StartsWith("I\t", StringComparison.Ordinal)) incomplete = true;
                    if (!ioFailed) Write(work.Text);
                    break;
                case TraceWorkKind.Capture: if (!ioFailed) Capture(work.Snapshot); break;
                case TraceWorkKind.Finish: Finish(work.Reason); break;
            }
            FlushIfDue();
        }

        private void FlushIfDue()
        {
            if (writer != null && DateTime.UtcNow - lastFlushUtc >= TimeSpan.FromSeconds(1))
            {
                writer.Flush();
                lastFlushUtc = DateTime.UtcNow;
            }
        }

        private void Start(TraceWork work)
        {
            if (writer != null) Finish("unexpected-new-trace");
            traceBase = work.TraceBase;
            probe = work.Probe;
            segment = sampleCount = changeCount = 0;
            incomplete = false;
            ioFailed = false;
            failedTraceBase = null;
            previous.Clear();
            try
            {
                OpenSegment();
                Write("V\t3\tmapTick\tdirectorTick\torder\tphase\tcategory/object\tfields");
                Write("G\tunknown-fields,pointers,padding,nested-or-array-fields,visual-presentation-fields,unavailable-APIs-excluded;local-UI-separate");
            }
            catch (Exception ex)
            {
                MarkIncomplete("OPEN", ex);
                ioFailed = true;
                failedTraceBase = traceBase;
            }
        }

        private void OpenSegment()
        {
            string path = traceBase + "-" + (++segment).ToString("D4", CultureInfo.InvariantCulture) + ".tsv";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.Read, 65536), new UTF8Encoding(false), 65536);
            writer.WriteLine("V\t3\tsegment=" + segment);
            lastFlushUtc = DateTime.UtcNow;
        }

        private void Write(string line)
        {
            if (writer == null) { incomplete = true; return; }
            if (writer.BaseStream.Position >= SegmentBytes)
            {
                writer.Flush(); writer.Dispose(); writer = null;
                OpenSegment();
            }
            writer.WriteLine(line);
        }

        private void Capture(TraceSnapshot snapshot)
        {
            if (snapshot == null) { MarkIncomplete("NO_SNAPSHOT", null); return; }
            var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var hashes = new SortedDictionary<string, StringBuilder>(StringComparer.Ordinal);
            foreach (TraceCategory category in snapshot.Categories)
            {
                if (category.Error != null)
                {
                    MarkIncomplete("CAPTURE_" + category.Name, new InvalidOperationException(category.Error));
                    continue;
                }
                hashes[category.Name] = new StringBuilder();
                if (category.Grid != null) AddGridRows(rows, category);
                foreach (TraceRecord record in category.Records)
                {
                    if (record.Data == null) rows.Add(record.Key, record.Text ?? "");
                    else
                    {
                        var handle = System.Runtime.InteropServices.GCHandle.Alloc(record.Data,
                            System.Runtime.InteropServices.GCHandleType.Pinned);
                        try { rows.Add(record.Key, SurrenderDesyncDiagnosticRuntime.Fields(record.Type, handle.AddrOfPinnedObject())); }
                        finally { handle.Free(); }
                    }
                }
            }
            foreach (var row in rows)
            {
                string category = row.Key.Substring(0, row.Key.IndexOf('/'));
                hashes[category].Append(row.Key).Append('=').Append(row.Value).Append(';');
                if (!previous.TryGetValue(row.Key, out string old) || old != row.Value)
                {
                    Write($"C\t{snapshot.MapTick}\t{snapshot.DirectorTick}\t{snapshot.Order}\t{snapshot.Phase}\t{row.Key}\t{Escape(row.Value)}");
                    changeCount++;
                }
            }
            foreach (var old in previous)
                if (!rows.ContainsKey(old.Key))
                {
                    Write($"C\t{snapshot.MapTick}\t{snapshot.DirectorTick}\t{snapshot.Order}\t{snapshot.Phase}\t{old.Key}\t<removed>");
                    changeCount++;
                }
            foreach (var hash in hashes)
            {
                Write($"S\t{snapshot.MapTick}\t{snapshot.DirectorTick}\t{snapshot.Order}\t{snapshot.Phase}\t{hash.Key}\t{ResyncDiagnosticHistory.ComputeSha256(hash.Value.ToString())}");
                sampleCount++;
            }
            Write($"L\t{snapshot.MapTick}\t{snapshot.DirectorTick}\t{snapshot.Order}\t{snapshot.Phase}\t{Escape(snapshot.LocalState)}");
            previous.Clear();
            foreach (var row in rows) previous.Add(row.Key, row.Value);
        }

        private static void AddGridRows(IDictionary<string, string> rows, TraceCategory category)
        {
            for (int start = 0; start < category.GridLength; start += 256)
            {
                var value = new StringBuilder(2048);
                for (int index = start; index < Math.Min(start + 256, category.GridLength); index++)
                {
                    if (category.GridElementBytes == 1) value.Append(category.Grid[index]);
                    else value.Append(BitConverter.ToUInt16(category.Grid, index * 2));
                    value.Append(',');
                }
                rows[category.Name + "/" + start + "/0"] = value.ToString();
            }
        }

        private void Finish(string reason)
        {
            try
            {
                long highWater;
                lock (queueGate) highWater = highWaterBytes;
                Write("F\t" + Escape(reason) + "\tstatus=" + (incomplete ? "incomplete" : "complete") +
                    "\tsamples=" + sampleCount + "\tchanges=" + changeCount + "\tqueueHighWater=" + highWater);
                writer?.Flush();
                writer?.Dispose();
                writer = null;
                string first = traceBase + "-0001.tsv";
                bool durable = !incomplete && File.Exists(first) && new FileInfo(first).Length > 0;
                if (probe)
                    Log((durable && sampleCount == 10 && changeCount > 0 ? "STATE_PROBE_OK " : "STATE_PROBE_FAILED ") +
                        "file=" + first + " samples=" + sampleCount + " changes=" + changeCount + " queueHighWater=" + highWater);
                else Log("CAPTURE_FINISHED " + traceBase + " reason=" + reason + " durable=" + durable +
                    " incomplete=" + incomplete + " samples=" + sampleCount + " queueHighWater=" + highWater);
            }
            catch (Exception ex) { MarkIncomplete("CLOSE", ex); }
            finally { writer = null; previous.Clear(); traceBase = null; }
        }

        private void MarkIncomplete(string reason, Exception ex)
        {
            incomplete = true;
            Log("TRACE_INCOMPLETE " + reason + " " + ex);
            try { writer?.WriteLine("I\t" + Escape(reason)); writer?.Flush(); }
            catch { /* The log remains the error channel if the file fails. */ }
        }

        private static string Escape(string value) => (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        private void Log(string message) => Shared.DebugLogHelper.LogInfo(log, "[SurrenderDiag] " + message);
    }
}
