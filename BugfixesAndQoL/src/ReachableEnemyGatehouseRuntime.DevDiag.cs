using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace BugfixesAndQoL
{
    // Counters observe the existing decisions; they never query the game themselves.
    internal sealed class GatehouseDiagnosticTotals
    {
        internal struct Gate { internal int Building, Unit, Tick, Player, Source, Entry, Exit, Mode; internal long Calls, Ticks; }
        internal long Queries, CallbackTicks, RouteCalls, RouteTicks, FootprintCalls, FootprintTicks, CollectTicks, ExteriorTicks;
        internal long DirectHits, DirectMisses, FullHits, FullMisses, Candidates, Bridges, Approaches, Overflow;
        internal long TraceCalls, TraceTicks;
        internal long FootprintCacheHits, FootprintScans, CollectionTileProbes, LayerProbes, PassHits, PassMismatches, RangeSkips;
        internal int LastTick = -1, Player, Source, Entry, Exit, Mode;
        private readonly Dictionary<int, Gate> gates = new Dictionary<int, Gate>(256);
        internal void Reset()
        {
            Queries = CallbackTicks = RouteCalls = RouteTicks = FootprintCalls = FootprintTicks = CollectTicks = ExteriorTicks = 0;
            DirectHits = DirectMisses = FullHits = FullMisses = Candidates = Bridges = Approaches = Overflow = 0;
            TraceCalls = TraceTicks = 0;
            FootprintCacheHits = FootprintScans = CollectionTileProbes = LayerProbes = PassHits = PassMismatches = RangeSkips = 0;
            LastTick = -1; Player = Source = Entry = Exit = Mode = 0; gates.Clear();
        }
        internal void Query(int building, int unit, long elapsed)
        {
            Queries++; CallbackTicks += elapsed;
            if (!gates.TryGetValue(building, out Gate gate))
            { if (gates.Count == 256) { Overflow++; return; } gate.Building = building; }
            gate.Calls++; gate.Ticks += elapsed; gate.Unit = unit; gate.Tick = LastTick;
            gate.Player = Player; gate.Source = Source; gate.Entry = Entry; gate.Exit = Exit; gate.Mode = Mode;
            gates[building] = gate;
        }
        internal string Detail()
        {
            double Ms(long t) => t * 1000.0 / Stopwatch.Frequency;
            // Logging only: no hot-path sorting or per-query context/string allocation.
            var top = new Gate[4];
            foreach (var gate in gates.Values)
                for (int i = 0; i < top.Length; i++) if (gate.Ticks > top[i].Ticks)
                { for (int j = top.Length - 1; j > i; j--) top[j] = top[j - 1]; top[i] = gate; break; }
            var rows = new List<string>(4);
            foreach (var g in top) if (g.Calls != 0) rows.Add(string.Format(CultureInfo.InvariantCulture,
                "{0}:calls={1}:ms={2:F3}:lastUnit={3}:tick={4}:player={5}:pcl={6}/{7}/{8}:mode={9}",
                g.Building, g.Calls, Ms(g.Ticks), g.Unit, g.Tick, g.Player, g.Source, g.Entry, g.Exit, g.Mode));
            return string.Format(CultureInfo.InvariantCulture,
                "queries={0} callbackInclusiveMs={1:F3} directHits={2} directMisses={3} fullHits={4} fullMisses={5} " +
                "routeCalls={6} routeInclusiveMs={7:F3} footprintCalls={8} footprintInclusiveMs={9:F3} candidates={10} " +
                "drawbridgeCollectInclusiveMs={11:F3} bridges={12} approaches={13} exteriorInclusiveMs={14:F3} gateOverflow={15} topGates={16}",
                Queries, Ms(CallbackTicks), DirectHits, DirectMisses, FullHits, FullMisses, RouteCalls, Ms(RouteTicks),
                FootprintCalls, Ms(FootprintTicks), Candidates, Ms(CollectTicks), Bridges, Approaches, Ms(ExteriorTicks), Overflow, string.Join(",", rows)) +
                " exteriorTraceCalls=" + TraceCalls + " exteriorTraceInclusiveMs=" + Ms(TraceTicks).ToString("F3", CultureInfo.InvariantCulture) +
                " geometryCacheHits=" + FootprintCacheHits + " footprintScans=" + FootprintScans +
                " collectionTileProbes=" + CollectionTileProbes + " layerProbes=" + LayerProbes + " passHits=" + PassHits + " passMismatches=" + PassMismatches + " rangeSkips=" + RangeSkips + " geometryCountersVersion=3";
        }
    }

    internal sealed unsafe partial class ReachableEnemyGatehouseRuntime
    {
        partial void DiagInitialize()
        {
            GatehouseDiagnosticRuntime.Install(() => "hostEnabled=" + settings.EnableMod +
                " clientEnabled=" + settings.EnableClientFeatures + " gateReachabilityEnabled=" + settings.RequireReachableEnemyForAutomaticGateClosing +
                " gateReachabilityEffective=" + (settings.EnableMod && settings.RequireReachableEnemyForAutomaticGateClosing && reachabilityAvailable) +
                " nativeCompatible=" + reachabilityAvailable + " vanillaPerformanceSetting=" + settings.EnableVanillaPerformanceOptimizations);
        }
    }

    internal static class GatehouseDiagnosticRuntime
    {
        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.GatehouseDiagnostic");
        private static Func<string> settings;
        private static int installed;
        [ThreadStatic] private static GatehouseDiagnosticTotals[] levels;
        [ThreadStatic] private static GatehouseDiagnosticTotals window;
        [ThreadStatic] private static int depth;
        [ThreadStatic] private static GatehouseDiagnosticTotals overflow;
        [ThreadStatic] private static long windowStarted;
        internal static GatehouseDiagnosticTotals Current
        {
            get
            {
                if (levels == null) levels = new GatehouseDiagnosticTotals[8];
                if (depth >= levels.Length) return overflow ?? (overflow = new GatehouseDiagnosticTotals());
                int index = depth;
                return levels[index] ?? (levels[index] = new GatehouseDiagnosticTotals());
            }
        }
        internal static void Install(Func<string> snapshot)
        {
            settings = snapshot;
            if (Interlocked.Exchange(ref installed, 1) != 0) return;
            try
            {
                harmony.Patch(AccessTools.Method(typeof(EngineInterface), nameof(EngineInterface.run), new[] { typeof(bool) }),
                    prefix: new HarmonyMethod(typeof(GatehouseDiagnosticRuntime), nameof(Begin)),
                    postfix: new HarmonyMethod(typeof(GatehouseDiagnosticRuntime), nameof(End)),
                    finalizer: new HarmonyMethod(typeof(GatehouseDiagnosticRuntime), nameof(Failed)), ilmanipulator: null);
                Shared.DevDiag.Log(() => "BFQOL_GATE_DIAGNOSTIC_READY " + settings());
            }
            catch (Exception error) { Shared.DevDiag.Log(() => "BFQOL_GATE_DIAGNOSTIC unavailable: " + error.GetType().Name); }
        }
        internal struct Scope { internal long Start; internal bool Completed; internal int Depth; }
        internal static void Begin(out Scope __state)
        {
            Shared.DevDiag.BeginBatch();
            __state = new Scope { Start = Stopwatch.GetTimestamp(), Depth = depth };
            depth++; if (depth < 8) Current.Reset();
        }
        private static void End(ref Scope __state) => Finish(ref __state);
        private static Exception Failed(ref Scope __state, Exception __exception)
        { if (__exception != null) Finish(ref __state); return __exception; }
        internal static void Finish(ref Scope scope)
        {
            if (scope.Completed || scope.Start == 0) return;
            scope.Completed = true;
            if (depth >= 8) { depth = scope.Depth; Current.Overflow++; Shared.DevDiag.EndBatch(); return; }
            try
            {
                long ended = Stopwatch.GetTimestamp(); var value = Current;
                if (ended - scope.Start >= Stopwatch.Frequency / 10)
                    Shared.DevDiagRuntime.Spike("bfqol.gatehouse.run", ended - scope.Start,
                        "startStamp=" + scope.Start + " endStamp=" + ended + " " + value.Detail());
                if (window == null) window = new GatehouseDiagnosticTotals();
                window.Queries += value.Queries; window.CallbackTicks += value.CallbackTicks;
                window.RouteCalls += value.RouteCalls; window.RouteTicks += value.RouteTicks;
                window.FootprintCalls += value.FootprintCalls; window.FootprintTicks += value.FootprintTicks;
                window.CollectTicks += value.CollectTicks; window.ExteriorTicks += value.ExteriorTicks;
                window.TraceCalls += value.TraceCalls; window.TraceTicks += value.TraceTicks;
                window.FootprintCacheHits += value.FootprintCacheHits; window.FootprintScans += value.FootprintScans;
                window.CollectionTileProbes += value.CollectionTileProbes; window.LayerProbes += value.LayerProbes; window.PassHits += value.PassHits; window.PassMismatches += value.PassMismatches; window.RangeSkips += value.RangeSkips;
                window.DirectHits += value.DirectHits; window.DirectMisses += value.DirectMisses;
                window.FullHits += value.FullHits; window.FullMisses += value.FullMisses;
                window.Candidates += value.Candidates; window.Bridges += value.Bridges; window.Approaches += value.Approaches;
                window.Overflow += value.Overflow;
                if (windowStarted == 0) windowStarted = scope.Start;
                if (ended - windowStarted >= Stopwatch.Frequency * 5)
                {
                    LogWindow(windowStarted, ended, window);
                    window.Reset(); windowStarted = ended;
                }
            }
            catch (Exception) { /* Diagnostic errors cannot replace the original result/exception. */ }
            finally { depth = scope.Depth; Shared.DevDiag.EndBatch(); }
        }
        // Keep the logging closure outside Finish: no display class on every run call.
        private static void LogWindow(long started, long ended, GatehouseDiagnosticTotals totals)
        {
            string detail = totals.Detail();
            Shared.DevDiag.Log(() => "gatehouseWindow startStamp=" + started + " endStamp=" + ended +
                " thread=" + Thread.CurrentThread.ManagedThreadId + " " + detail + " " + (settings == null ? "settings=unavailable" : settings()));
        }
        internal static long Start() => Stopwatch.GetTimestamp();
        internal struct QueryScope { internal long Started; internal int Tick, Player, Source, Entry, Exit, Mode; }
        internal static QueryScope QueryStart()
        {
            var c = Current;
            var scope = new QueryScope { Started = Stopwatch.GetTimestamp(), Tick = c.LastTick, Player = c.Player,
                Source = c.Source, Entry = c.Entry, Exit = c.Exit, Mode = c.Mode };
            c.LastTick = -1; c.Player = c.Source = c.Entry = c.Exit = c.Mode = 0;
            return scope;
        }
        internal static void Query(int building, int unit, QueryScope scope)
        {
            try { Current.Query(building, unit, Stopwatch.GetTimestamp() - scope.Started); }
            catch (Exception) { }
            finally { var c = Current; c.LastTick = scope.Tick; c.Player = scope.Player; c.Source = scope.Source; c.Entry = scope.Entry; c.Exit = scope.Exit; c.Mode = scope.Mode; }
        }
        internal static void Context(int tick, int player, int source, int entry, int exit, int mode)
        { var c = Current; c.LastTick = tick; c.Player = player; c.Source = source; c.Entry = entry; c.Exit = exit; c.Mode = mode; }
    }
}
