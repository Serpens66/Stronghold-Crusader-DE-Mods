using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace BugfixesAndQoL
{
    // Numeric capture only on the calling thread. Formatting and logging belong to Drain.
    internal static class ManualProbeCapture
    {
        internal const int Unknown = 0, BuildingCursor = 1, TileCursor = 2, GroupFallback = 3,
            MoatStart = 4, Placement = 5, MoatWork = 6, Origins = 7;
        internal const int Save = 0, Native = 1, Restore = 2, Administration = 3;
        private static readonly string[] origins = { "unknown", "buildingCursor", "tileCursor", "groupFallback", "moatStart", "placementUnstack", "moatWork" };
        private static readonly string[] phases = { "save", "native.filtered", "restore", "administration" };
        internal struct Context
        {
            internal GateAttackFreezeCapture.Context Unit;
            internal int Sequence, Thread, Origin, Result;
            internal long Operation, EngineRun;
            internal bool Completed;
        }
        internal struct Metric { internal long Calls, Sum, Max; internal Context Context; }
        internal sealed class Record
        {
            internal long Id, EngineRun, Start, Elapsed, Attempts, Probes, Positive, Negative, Incomplete, Fresh, Reused,
                Hits, Misses, RejectCandidates, RejectLearned, RejectHits, NativeAvoided, TickInvalidations, CapacityInvalidations, MapInvalidations,
                SaveManager, SaveGrid, SaveSmall, RestoreManager, RestoreGrid, RestoreSmall;
            internal int Thread, Origin, Building;
            internal DateTime EndUtc;
            internal Context MaxContext;
            internal Context CacheContext;
            internal Context RegionContext;
            internal long MaxProbe;
            internal readonly Metric[] Phases = new Metric[4];
            internal readonly long[] Reasons = new long[Origins];
            internal readonly long[] GroundReasons = new long[21];
            internal long GroundCaptureTicks, GroundCaptureMax;
            internal int SourceRegion, TargetRegion;
            internal void Reset()
            {
                Id = EngineRun = Start = Elapsed = Attempts = Probes = Positive = Negative = Incomplete = Fresh = Reused =
                    Hits = Misses = RejectCandidates = RejectLearned = RejectHits = NativeAvoided = TickInvalidations = CapacityInvalidations = MapInvalidations =
                    SaveManager = SaveGrid = SaveSmall = RestoreManager = RestoreGrid = RestoreSmall = MaxProbe = 0;
                Thread = Origin = Building = 0; MaxContext = CacheContext = RegionContext = default; EndUtc = default;
                Array.Clear(Phases, 0, Phases.Length); Array.Clear(Reasons, 0, Reasons.Length);
                Array.Clear(GroundReasons, 0, GroundReasons.Length);
                GroundCaptureTicks = GroundCaptureMax = 0; SourceRegion = TargetRegion = 0;
            }
        }
        internal struct Scope : IDisposable
        {
            internal Record Value;
            internal int Previous;
            internal int PreviousBuilding;
            internal bool Owner;
            public void Dispose() { EndOperation(this); }
        }
        internal struct Probe
        {
            internal Scope Scope;
            internal Context Context;
            internal long Start, Stamp, Saved, Planned, Restored, GateToken;
            internal long SaveManager, SaveGrid, SaveSmall, RestoreManager, RestoreGrid, RestoreSmall;
            internal int Phase, Result, Origin, Seen;
            internal bool Reused, Completed, Enabled, RestoreCompleted, SnapshotCompleted;
        }
        [ThreadStatic] private static Record active;
        private static readonly ConcurrentQueue<Record> free = new ConcurrentQueue<Record>();
        private static readonly ConcurrentQueue<Record> ready = new ConcurrentQueue<Record>();
        private static readonly object summaryLock = new object();
        private static Record summary = new Record(), delivery = new Record();
        private static long nextId, dropped, suppressed, faults, lastSummary, reportWindow;
        private static int initialized, reports;
        internal static Record Current => active;
        internal static long Faults => Interlocked.Read(ref faults);
        internal static void Initialize()
        {
            try
            {
                if (Interlocked.Exchange(ref initialized, 1) != 0) return;
                for (int i = 0; i < 64; i++) free.Enqueue(new Record());
                lastSummary = Stopwatch.GetTimestamp();
                Shared.DevDiag.Log(() => "MANUAL_PROBE ready thresholdMs=20 summarySeconds=5 pool=64 originalsIncludeActiveFilters=true");
            }
            catch (Exception) { Interlocked.Increment(ref faults); }
        }
        internal static Scope BeginOperation(int origin, int building = 0)
        {
            var scope = new Scope { Value = active, Previous = active?.Origin ?? Unknown, PreviousBuilding = active?.Building ?? 0 };
            try
            {
                if (active != null) { active.Origin = origin; if (building != 0) active.Building = building; return scope; }
                if (!free.TryDequeue(out Record value)) { Interlocked.Increment(ref dropped); return scope; }
                value.Reset(); value.Id = Interlocked.Increment(ref nextId);
                value.EngineRun = GateAttackFreezeCapture.Current?.Id ?? 0;
                value.Thread = Thread.CurrentThread.ManagedThreadId; value.Origin = origin; value.Building = building;
                value.Start = Stopwatch.GetTimestamp(); active = value;
                scope.Value = value; scope.Owner = true;
            }
            catch (Exception) { Interlocked.Increment(ref faults); }
            return scope;
        }
        private static void EndOperation(Scope scope)
        {
            try
            {
                Record value = scope.Value;
                if (value == null) return;
                if (!scope.Owner) { value.Origin = scope.Previous; value.Building = scope.PreviousBuilding; return; }
                active = null;
                value.Elapsed = Stopwatch.GetTimestamp() - value.Start; value.EndUtc = DateTime.UtcNow;
                lock (summaryLock) Merge(summary, value);
                long now = Stopwatch.GetTimestamp(), window = Interlocked.Read(ref reportWindow);
                if (now - window >= Stopwatch.Frequency && Interlocked.CompareExchange(ref reportWindow, now, window) == window)
                    Interlocked.Exchange(ref reports, 0);
                bool report = value.Elapsed >= Stopwatch.Frequency / 50 && value.Probes != 0;
                if (report && Interlocked.Increment(ref reports) <= 4) ready.Enqueue(value);
                else { if (report) Interlocked.Increment(ref suppressed); free.Enqueue(value); }
            }
            catch (Exception) { Interlocked.Increment(ref faults); }
        }
        internal static Probe BeginProbe(Context context, bool reused)
        {
            var probe = new Probe();
            try
            {
                probe.Scope = active == null ? BeginOperation(Unknown) : new Scope { Value = active, Previous = active.Origin, PreviousBuilding = active.Building };
                probe.Enabled = active != null;
                context.Operation = active?.Id ?? 0; context.EngineRun = GateAttackFreezeCapture.Current?.Id ?? 0;
                context.Thread = active?.Thread ?? Thread.CurrentThread.ManagedThreadId; context.Origin = active?.Origin ?? Unknown;
                if (context.Unit.Building == 0) context.Unit.Building = active?.Building ?? 0;
                probe.Context = context; probe.Reused = reused; probe.Origin = active?.Origin ?? Unknown;
                probe.GateToken = GateAttackFreezeCapture.Enter(GateAttackFreezeCapture.ManualProbe, context.Unit);
                probe.Start = probe.Stamp = Stopwatch.GetTimestamp(); probe.Phase = Save; probe.Seen = 1 << Save;
            }
            catch (Exception) { Interlocked.Increment(ref faults); }
            return probe;
        }
        internal static void Mark(ref Probe probe, int phase)
        {
            try { MarkAt(ref probe, phase, Stopwatch.GetTimestamp()); }
            catch (Exception) { Interlocked.Increment(ref faults); }
        }
        internal static void MarkAt(ref Probe probe, int phase, long now)
        {
            if (!probe.Enabled) return;
            long elapsed = Math.Max(0, now - probe.Stamp);
            if (probe.Phase == Save) probe.Saved += elapsed;
            else if (probe.Phase == Native) probe.Planned += elapsed;
            else if (probe.Phase == Restore) probe.Restored += elapsed;
            probe.Stamp = now; probe.Phase = phase; probe.Seen |= 1 << phase;
        }
        // Called only after a successful existing copy, so byte counts describe actual traffic.
        internal static void Copied(ref Probe probe, bool restore, int manager, int grid, int small)
        {
            try
            {
                if (restore) { probe.RestoreManager += manager; probe.RestoreGrid += grid; probe.RestoreSmall += small; }
                else { probe.SaveManager += manager; probe.SaveGrid += grid; probe.SaveSmall += small; }
            }
            catch (Exception) { Interlocked.Increment(ref faults); }
        }
        internal static void Result(ref Probe probe, int result)
        { probe.Result = result; probe.Completed = true; }
        internal static void EndProbe(ref Probe probe)
        {
            try
            {
                long end = Stopwatch.GetTimestamp(); MarkAt(ref probe, Administration, end);
                if (probe.Enabled)
                {
                    long elapsed = Math.Max(0, end - probe.Start);
                    Account(probe.Scope.Value, probe, elapsed);
                    var run = GateAttackFreezeCapture.Current;
                    if (run != null && run.Id == probe.Context.EngineRun) Account(run.Probes, probe, elapsed);
                }
            }
            catch (Exception) { Interlocked.Increment(ref faults); }
            finally
            {
                GateAttackFreezeCapture.Leave(probe.GateToken, probe.Result, probe.Completed);
                probe.Scope.Dispose();
            }
        }
        internal static void Account(Record value, Probe probe, long elapsed)
        {
            value.Attempts++; value.Elapsed += elapsed;
            if ((probe.Seen & (1 << Native)) != 0) { value.Probes++; value.Reasons[probe.Origin]++; }
            probe.Context.Result = probe.Result; probe.Context.Completed = probe.Completed && probe.RestoreCompleted;
            if (!probe.Context.Completed) value.Incomplete++; else if (probe.Result > 0) value.Positive++; else value.Negative++;
            if (probe.Reused) value.Reused++; else if (probe.SnapshotCompleted) value.Fresh++;
            if ((probe.Seen & (1 << Save)) != 0) Add(ref value.Phases[Save], probe.Saved, probe.Context);
            if ((probe.Seen & (1 << Native)) != 0) Add(ref value.Phases[Native], probe.Planned, probe.Context);
            if ((probe.Seen & (1 << Restore)) != 0) Add(ref value.Phases[Restore], probe.Restored, probe.Context);
            Add(ref value.Phases[Administration], Math.Max(0, elapsed - probe.Saved - probe.Planned - probe.Restored), probe.Context);
            if (elapsed >= value.MaxProbe) { value.MaxProbe = elapsed; value.MaxContext = probe.Context; }
            value.SaveManager += probe.SaveManager; value.SaveGrid += probe.SaveGrid; value.SaveSmall += probe.SaveSmall;
            value.RestoreManager += probe.RestoreManager; value.RestoreGrid += probe.RestoreGrid; value.RestoreSmall += probe.RestoreSmall;
        }
        private static void Add(ref Metric metric, long elapsed, Context context)
        {
            metric.Calls++; metric.Sum += elapsed;
            if (elapsed >= metric.Max) { metric.Max = elapsed; metric.Context = context; }
        }
        internal static void Cache(bool hit, Context context = default, bool result = false)
        {
            try
            {
                if (active == null) return;
                if (hit) active.Hits++; else active.Misses++;
                context.Operation = active.Id; context.EngineRun = GateAttackFreezeCapture.Current?.Id ?? 0; context.Thread = active.Thread; context.Origin = active.Origin;
                context.Result = result ? 1 : 0; context.Completed = hit;
                if (context.Unit.Building == 0) context.Unit.Building = active.Building;
                active.CacheContext = context;
                var run = GateAttackFreezeCapture.Current;
                if (run != null && run.Id == context.EngineRun)
                {
                    if (hit) run.Probes.Hits++; else run.Probes.Misses++;
                    run.Probes.CacheContext = context;
                }
            }
            catch (Exception) { Interlocked.Increment(ref faults); }
        }
        internal static void Invalidate(bool tick, bool capacity, bool map)
        {
            try
            {
                if (active != null) { if (tick) active.TickInvalidations++; if (capacity) active.CapacityInvalidations++; if (map) active.MapInvalidations++; }
                else lock (summaryLock) { if (tick) summary.TickInvalidations++; if (capacity) summary.CapacityInvalidations++; if (map) summary.MapInvalidations++; }
                var run = GateAttackFreezeCapture.Current;
                if (run != null) { if (tick) run.Probes.TickInvalidations++; if (capacity) run.Probes.CapacityInvalidations++; if (map) run.Probes.MapInvalidations++; }
            }
            catch (Exception) { Interlocked.Increment(ref faults); }
        }
        internal static void RegionReject(int kind)
        {
            try
            {
                if (active != null) AddRegionReject(active, kind);
                var run = GateAttackFreezeCapture.Current;
                if (run != null) AddRegionReject(run.Probes, kind);
            }
            catch (Exception) { Interlocked.Increment(ref faults); }
        }
        private static void AddRegionReject(Record value, int kind)
        {
            if (kind == 0) value.RejectCandidates++;
            else if (kind == 1) value.RejectLearned++;
            else if (kind == 2) { value.RejectHits++; value.NativeAvoided++; }
        }
        internal static void GroundReject(int kind, long started, int source, int target)
        {
            try
            {
                long elapsed = started == 0 ? 0 : Stopwatch.GetTimestamp() - started;
                if (active != null) AddGroundReject(active, kind, elapsed, source, target);
                var run = GateAttackFreezeCapture.Current;
                if (run != null) AddGroundReject(run.Probes, kind, elapsed, source, target);
            }
            catch (Exception) { Interlocked.Increment(ref faults); }
        }
        private static void AddGroundReject(Record value, int kind, long elapsed, int source, int target)
        {
            if ((uint)kind >= (uint)value.GroundReasons.Length) return;
            value.GroundReasons[kind]++;
            if (kind == 0 || kind == 3)
            { value.GroundCaptureTicks += elapsed; value.GroundCaptureMax = Math.Max(value.GroundCaptureMax, elapsed); }
            if (kind == 7 || kind == 8) { value.SourceRegion = source; value.TargetRegion = target; value.RegionContext = value.CacheContext; }
            if (kind >= 2 && kind <= 6 && kind != 3) value.NativeAvoided++;
        }
        internal static void Merge(Record target, Record source)
        {
            target.Id++; // Summary operation count, never a simulation tick count.
            target.Elapsed += source.Elapsed; target.Attempts += source.Attempts; target.Probes += source.Probes; target.Positive += source.Positive;
            target.Negative += source.Negative; target.Incomplete += source.Incomplete; target.Fresh += source.Fresh; target.Reused += source.Reused;
            target.Hits += source.Hits; target.Misses += source.Misses; target.TickInvalidations += source.TickInvalidations;
            target.RejectCandidates += source.RejectCandidates; target.RejectLearned += source.RejectLearned;
            target.RejectHits += source.RejectHits; target.NativeAvoided += source.NativeAvoided;
            target.GroundCaptureTicks += source.GroundCaptureTicks;
            target.GroundCaptureMax = Math.Max(target.GroundCaptureMax, source.GroundCaptureMax);
            if (source.GroundReasons[7] + source.GroundReasons[8] > 0)
            { target.SourceRegion = source.SourceRegion; target.TargetRegion = source.TargetRegion; target.RegionContext = source.RegionContext; }
            for (int i = 0; i < target.GroundReasons.Length; i++) target.GroundReasons[i] += source.GroundReasons[i];
            if (source.Hits + source.Misses > 0) target.CacheContext = source.CacheContext;
            target.CapacityInvalidations += source.CapacityInvalidations; target.MapInvalidations += source.MapInvalidations;
            target.SaveManager += source.SaveManager; target.SaveGrid += source.SaveGrid; target.SaveSmall += source.SaveSmall;
            target.RestoreManager += source.RestoreManager; target.RestoreGrid += source.RestoreGrid; target.RestoreSmall += source.RestoreSmall;
            for (int i = 0; i < Origins; i++) target.Reasons[i] += source.Reasons[i];
            for (int i = 0; i < 4; i++)
            {
                ref Metric t = ref target.Phases[i]; Metric s = source.Phases[i]; t.Calls += s.Calls; t.Sum += s.Sum;
                if (s.Max >= t.Max) { t.Max = s.Max; t.Context = s.Context; }
            }
            if (source.MaxProbe >= target.MaxProbe)
            {
                target.MaxProbe = source.MaxProbe; target.MaxContext = source.MaxContext;
                target.EngineRun = source.EngineRun; target.Thread = source.Thread; target.Origin = source.Origin;
            }
        }
        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        private static void ContextText(StringBuilder b, Context context)
        {
            var x = context.Unit;
            b.Append(" unit=").Append(x.Unit).Append("/g").Append(x.Global).Append(" type=").Append(x.Type)
                .Append(" tribe=").Append(x.Tribe).Append(" player=").Append(x.Player).Append(" command=").Append(x.Command)
                .Append(" commandSequence=").Append(context.Sequence).Append(" building=").Append(x.Building)
                .Append(" start=").Append(x.StartX).Append(',').Append(x.StartY).Append(" target=").Append(x.TargetX).Append(',').Append(x.TargetY)
                .Append(" sampleOperation=").Append(context.Operation).Append(" sampleEngineRun=").Append(context.EngineRun)
                .Append(" sampleThread=").Append(context.Thread).Append(" reason=").Append(origins[context.Origin])
                .Append(" result=").Append(context.Result).Append(" completed=").Append(context.Completed);
        }
        internal static string Format(Record value, bool aggregate, bool engine = false)
        {
            var b = new StringBuilder(1800); var c = CultureInfo.InvariantCulture;
            b.Append("MANUAL_PROBE ").Append(engine ? "runAggregate run=" : aggregate ? "summary operations=" : "operation=").Append(value.Id)
                .Append(" engineRun=").Append(value.EngineRun).Append(" outsideEngineRun=").Append(value.EngineRun == 0)
                .Append(" thread=").Append(value.Thread).Append(" originalIncludesActiveFilters=true phaseTimesNotAdditiveWithBuilderScopes=true")
                .Append(" totalOperationMs=").Append(Ms(value.Elapsed).ToString("F3", c)).Append(" attempts=").Append(value.Attempts).Append(" probes=").Append(value.Probes)
                .Append(" positive=").Append(value.Positive).Append(" nonpositive=").Append(value.Negative).Append(" incomplete=").Append(value.Incomplete)
                .Append(" freshSnapshots=").Append(value.Fresh).Append(" reusedSnapshots=").Append(value.Reused)
                .Append(" cacheHits=").Append(value.Hits).Append(" cacheMisses=").Append(value.Misses)
                .Append(" rejectCandidates=").Append(value.RejectCandidates).Append(" regionRejectsLearned=").Append(value.RejectLearned)
                .Append(" regionRejectHits=").Append(value.RejectHits).Append(" nativeProbesAvoided=").Append(value.NativeAvoided)
                .Append(" avoidedRestoreBytes=").Append(value.NativeAvoided * 5251508L)
                .Append(" groundValidated=").Append(value.GroundReasons[0]).Append(" groundLearned=").Append(value.GroundReasons[1])
                .Append(" groundHits=").Append(value.GroundReasons[2]).Append(" groundDiscarded=").Append(value.GroundReasons[3])
                .Append(" targetRejects(availability,flags,profile)=").Append(value.GroundReasons[4]).Append(',').Append(value.GroundReasons[5]).Append(',').Append(value.GroundReasons[6])
                .Append(" regions(equal,different)=").Append(value.GroundReasons[7]).Append(',').Append(value.GroundReasons[8])
                .Append(" regionAnswers(positive,zero,ambiguous)=").Append(value.GroundReasons[9]).Append(',').Append(value.GroundReasons[10]).Append(',').Append(value.GroundReasons[11])
                .Append(" regionSample=").Append(value.SourceRegion).Append("->").Append(value.TargetRegion)
                .Append(" groundCaptureMs=").Append(Ms(value.GroundCaptureTicks).ToString("F3", c))
                .Append(" groundCaptureMaxMs=").Append(Ms(value.GroundCaptureMax).ToString("F3", c)).Append(" captureIncludedInNative=true")
                .Append(" groundFailures(generation,data,start,target,row,neighbor,boundary,policy)=")
                .Append(value.GroundReasons[13]).Append(',').Append(value.GroundReasons[14]).Append(',').Append(value.GroundReasons[15]).Append(',').Append(value.GroundReasons[16]).Append(',')
                .Append(value.GroundReasons[17]).Append(',').Append(value.GroundReasons[18]).Append(',').Append(value.GroundReasons[19]).Append(',').Append(value.GroundReasons[20])
                .Append(" invalidationsTick=").Append(value.TickInvalidations).Append(" invalidationsCapacity=").Append(value.CapacityInvalidations)
                .Append(" invalidationsMap=").Append(value.MapInvalidations)
                .Append(" saveBytes(manager,grid,small)=").Append(value.SaveManager).Append(',').Append(value.SaveGrid).Append(',').Append(value.SaveSmall)
                .Append(" restoreBytes(manager,grid,small)=").Append(value.RestoreManager).Append(',').Append(value.RestoreGrid).Append(',').Append(value.RestoreSmall);
            long assigned = 0;
            for (int i = 0; i < 4; i++)
            {
                Metric m = value.Phases[i]; assigned += m.Sum;
                b.Append(" | ").Append(phases[i]).Append(" calls=").Append(m.Calls).Append(" sumMs=").Append(Ms(m.Sum).ToString("F3", c))
                    .Append(" maxMs=").Append(Ms(m.Max).ToString("F3", c));
                if (m.Calls > 0) ContextText(b, m.Context);
            }
            b.Append(" | outsideProbesMs=").Append(Ms(Math.Max(0, value.Elapsed - assigned)).ToString("F3", c))
                .Append(" maxProbeMs=").Append(Ms(value.MaxProbe).ToString("F3", c)); ContextText(b, value.MaxContext);
            if (value.Hits + value.Misses > 0) { b.Append(" | cacheSample"); ContextText(b, value.CacheContext); }
            if (value.GroundReasons[7] + value.GroundReasons[8] > 0) { b.Append(" | regionSampleContext"); ContextText(b, value.RegionContext); }
            for (int i = 0; i < Origins; i++) b.Append(' ').Append(origins[i]).Append('=').Append(value.Reasons[i]);
            return b.ToString();
        }
        internal static void Drain()
        {
            try
            {
                long now = Stopwatch.GetTimestamp();
                for (int i = 0; i < 4 && Stopwatch.GetTimestamp() - now < Stopwatch.Frequency / 1000; i++)
                {
                    if (!ready.TryDequeue(out Record value)) break;
                    try { Shared.DevDiag.Log(() => Format(value, false) + " endUtc=" + value.EndUtc.ToString("o")); }
                    finally { free.Enqueue(value); }
                }
                if (now - lastSummary < 5 * Stopwatch.Frequency) return;
                lastSummary = now;
                ExchangeSummary();
                Shared.DevDiag.Log(() => Format(delivery, true) + " dropped=" + Interlocked.Exchange(ref dropped, 0) +
                    " reportsSuppressed=" + Interlocked.Exchange(ref suppressed, 0) + " diagnosticFaultsTotal=" + Faults);
            }
            catch (Exception) { Interlocked.Increment(ref faults); }
        }
        internal static Record ExchangeSummary()
        {
            lock (summaryLock) { var old = delivery; delivery = summary; summary = old; summary.Reset(); return delivery; }
        }
    }
}
