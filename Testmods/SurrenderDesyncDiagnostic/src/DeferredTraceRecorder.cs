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
        internal int DataLength;
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

    // The live worker only serializes owned bytes. Projection and comparison run offline.
    internal sealed class DeferredTraceRecorder
    {
        private const long QueueTargetBytes = 64L * 1024 * 1024;
        private const long SegmentBytes = 128L * 1024 * 1024;
        // Test-only instance knobs default to the production values and are never configured by the mod.
        internal long QueueLimitBytes = QueueTargetBytes;
        internal long SegmentLimitBytes = SegmentBytes;
        internal int ArtificialWriteDelayMilliseconds = 0;
        private readonly object queueGate = new object();
        private readonly Queue<TraceWork> queue = new Queue<TraceWork>();
        private readonly ManualLogSource log;
        private readonly Thread thread;
        private long queuedBytes;
        private long highWaterBytes;
        private long backpressureCount;
        private long backpressureTicks;
        private BinaryWriter writer;
        private FileStream stream;
        private string traceBase;
        private int segment;
        private bool incomplete;
        private bool ioFailed;
        private volatile string failedTraceBase;
        private bool probe;
        private int snapshotCount;
        private int categoryCount;
        private int eventCount;
        private DateTime lastFlushUtc;

        internal DeferredTraceRecorder(ManualLogSource logger)
        {
            log = logger;
            thread = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.BelowNormal,
                Name = "Surrender diagnostic binary writer" };
            thread.Start();
        }

        internal bool Enqueue(TraceWork work)
        {
            long waitStart = 0;
            lock (queueGate)
            {
                while (work.Kind == TraceWorkKind.Capture && queue.Count != 0 &&
                    work.Size > QueueLimitBytes - queuedBytes && !FailedFor(work.TraceBase))
                {
                    if (waitStart == 0) waitStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    Monitor.Wait(queueGate);
                }
                if (waitStart != 0)
                {
                    backpressureCount++;
                    backpressureTicks += System.Diagnostics.Stopwatch.GetTimestamp() - waitStart;
                }
                if (FailedFor(work.TraceBase)) return false;
                queue.Enqueue(work);
                queuedBytes += work.Size;
                if (queuedBytes > highWaterBytes) highWaterBytes = queuedBytes;
                Monitor.Pulse(queueGate);
                return true;
            }
        }

        internal bool FailedFor(string candidate) => candidate != null &&
            string.Equals(failedTraceBase, candidate, StringComparison.Ordinal);

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
                    catch (Exception ex) { Fail("FLUSH", ex); }
                    continue;
                }
                long size = work.Size;
                try { Process(work); }
                catch (Exception ex) { Fail("WRITE", ex); }
                finally
                {
                    try { work.Snapshot?.Release(); }
                    catch (Exception ex) { Fail("BUFFER_RELEASE", ex); }
                    lock (queueGate)
                    {
                        queuedBytes -= size;
                        Monitor.PulseAll(queueGate);
                    }
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
                    if (!ioFailed) { Rollover(); writer.Write((byte)1); writer.Write(work.Text ?? ""); eventCount++; }
                    break;
                case TraceWorkKind.Capture:
                    if (!ioFailed) Capture(work.Snapshot);
                    break;
                case TraceWorkKind.Finish: Finish(work.Reason); break;
            }
            FlushIfDue();
        }

        private void Start(TraceWork work)
        {
            if (writer != null) Finish("unexpected-new-trace");
            traceBase = work.TraceBase;
            probe = work.Probe;
            segment = snapshotCount = categoryCount = eventCount = 0;
            incomplete = ioFailed = false;
            failedTraceBase = null;
            OpenSegment();
        }

        private void OpenSegment()
        {
            string path = traceBase + "-" + (++segment).ToString("D4", CultureInfo.InvariantCulture) + ".sdd";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024);
            writer = new BinaryWriter(stream, new UTF8Encoding(false), true);
            writer.Write(0x34444453); // SDD4, little endian
            writer.Write(segment);
            writer.Write(typeof(DeferredTraceRecorder).Assembly.ManifestModule.ModuleVersionId.ToString("D"));
            lastFlushUtc = DateTime.UtcNow;
        }

        private void Rollover()
        {
            if (stream.Position < SegmentLimitBytes) return;
            CloseWriter();
            OpenSegment();
        }

        private void Capture(TraceSnapshot snapshot)
        {
            if (snapshot == null) throw new InvalidDataException("Missing snapshot");
            if (ArtificialWriteDelayMilliseconds > 0)
                Thread.Sleep(ArtificialWriteDelayMilliseconds);
            Rollover();
            writer.Write((byte)2);
            writer.Write(snapshot.MapTick);
            writer.Write(snapshot.DirectorTick);
            writer.Write(snapshot.Order);
            writer.Write(snapshot.Phase ?? "");
            writer.Write(snapshot.LocalState ?? "");
            writer.Write(snapshot.Categories.Count);
            foreach (TraceCategory category in snapshot.Categories)
            {
                writer.Write(category.Name ?? "");
                writer.Write(category.Error ?? "");
                if (category.Error != null) incomplete = true;
                writer.Write(category.Records.Count);
                foreach (TraceRecord record in category.Records)
                {
                    writer.Write(record.Key ?? "");
                    writer.Write(record.Type?.FullName ?? "");
                    writer.Write(record.Data == null ? -1 : record.DataLength);
                    if (record.Data == null) writer.Write(record.Text ?? "");
                    else writer.Write(record.Data, 0, record.DataLength);
                }
                writer.Write(category.GridLength);
                writer.Write(category.GridElementBytes);
                if (category.Grid != null)
                    writer.Write(category.Grid, 0, checked(category.GridLength * category.GridElementBytes));
                categoryCount++;
            }
            snapshotCount++;
        }

        private void Finish(string reason)
        {
            try
            {
                if (!ioFailed && writer != null)
                {
                    Rollover();
                    writer.Write((byte)3);
                    writer.Write(reason ?? "");
                    writer.Write(incomplete);
                    writer.Write(snapshotCount);
                    writer.Write(categoryCount);
                    writer.Write(eventCount);
                    writer.Write(highWaterBytes);
                    writer.Write(backpressureCount);
                    writer.Write(backpressureTicks);
                    CloseWriter();
                }
                string first = traceBase + "-0001.sdd";
                bool durable = !ioFailed && !incomplete && File.Exists(first) &&
                    new FileInfo(first).Length > 0;
                if (probe && durable)
                    durable = BinaryTraceComparison.ValidateProbe(traceBase + "-*.sdd");
                double waitMs = 1000.0 * backpressureTicks / System.Diagnostics.Stopwatch.Frequency;
                if (probe)
                    Log((durable && snapshotCount == 1 && categoryCount == 10 ? "STATE_PROBE_OK " :
                        "STATE_PROBE_FAILED ") + "file=" + first + " snapshots=" + snapshotCount +
                        " categories=" + categoryCount + " queueHighWater=" + highWaterBytes);
                else
                    Log("CAPTURE_FINISHED " + traceBase + " reason=" + reason + " durable=" + durable +
                        " incomplete=" + incomplete + " snapshots=" + snapshotCount +
                        " queueHighWater=" + highWaterBytes + " backpressureCount=" + backpressureCount +
                        " backpressureMs=" + waitMs.ToString("F2", CultureInfo.InvariantCulture));
            }
            catch (Exception ex) { Fail("FINISH", ex); }
            finally { try { CloseWriter(); } catch { } }
        }

        private void FlushIfDue()
        {
            if (writer != null && DateTime.UtcNow - lastFlushUtc >= TimeSpan.FromSeconds(1))
            {
                writer.Flush();
                stream.Flush();
                lastFlushUtc = DateTime.UtcNow;
            }
        }

        private void CloseWriter()
        {
            if (writer == null) return;
            writer.Flush();
            stream.Flush(true);
            writer.Dispose();
            stream.Dispose();
            writer = null;
            stream = null;
        }

        private void Fail(string reason, Exception ex)
        {
            incomplete = ioFailed = true;
            failedTraceBase = traceBase;
            Log("TRACE_INCOMPLETE " + reason + " " + ex);
            try { writer?.Dispose(); stream?.Dispose(); } catch { }
            writer = null;
            stream = null;
            lock (queueGate) Monitor.PulseAll(queueGate);
        }

        private void Log(string message) => Shared.DebugLogHelper.LogInfo(log, "[SurrenderDiag] " + message);
    }
}
