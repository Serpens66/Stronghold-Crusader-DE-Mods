using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;
using APIShared;
using BetterPathfindingTest;
using BetterPathfindingTest.Core;

internal static class EngineContract
{
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, request) =>
        {
            string name = new AssemblyName(request.Name).Name + ".dll";
            foreach (string directory in new[] { Path.Combine(args[0], "Stronghold Crusader Definitive Edition_Data", "Managed"), Path.Combine(args[0], "BepInEx", "core"), Path.Combine(args[0], "BepInEx", "plugins", "000shcdese"), Path.Combine(args[0], "BepInEx", "plugins", "APIShared_Serp") })
            {
                string path = Path.Combine(directory, name); if (!File.Exists(path)) continue;
                // Auxiliary installed DLLs may carry a download-zone mark. Load their
                // trusted installed bytes without altering the files or CLR-wide settings.
                return name != "Assembly-CSharp.dll" || args[1] == "memory" ? Assembly.Load(File.ReadAllBytes(path)) : Assembly.LoadFrom(path);
            }
            return null;
        };
        try { Run(); Evidence(args[0], args[1]); RuntimeFailures(args[0]); Console.WriteLine("PASS: loader=" + args[1] + "; installed managed/native-layout contracts, capture state and worker failures; no native method called."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Run()
    {
        var gate = new EngineGate(); if (!gate.TryEnter()) throw new Exception("Uncontended lock failed."); gate.Exit();
        using (var ready = new ManualResetEvent(false)) using (var release = new ManualResetEvent(false))
        {
            var thread = new Thread(() => { if (!gate.TryEnter()) throw new Exception("Test owner lock failed."); ready.Set(); release.WaitOne(); gate.Exit(); });
            thread.Start(); if (!ready.WaitOne(5000)) throw new Exception("Test owner unavailable.");
            try { if (gate.TryEnter()) { gate.Exit(); throw new Exception("Contended lock succeeded."); } }
            finally { release.Set(); thread.Join(); }
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""); }
    private static void Evidence(string root, string mode)
    {
        Assembly assembly = typeof(EngineInterface).Assembly;
        Check((assembly.Location.Length == 0) == (mode == "memory"), "Loader scenario was not reproduced.");
        ManagedContracts.Validate();
        var evidence = new ManagedEvidence(root, assembly, Hash);
        Check(evidence.InstalledHash.Length == 64 && evidence.InstalledHash == Hash(evidence.InstalledPath), "Installed game image hash differs.");
        Check(evidence.LoadedName == assembly.FullName && evidence.LoadedMvid == assembly.ManifestModule.ModuleVersionId.ToString("D"), "Loaded identity missing.");
        var missing = new ManagedEvidence(root, assembly, path => { throw new IOException("fixture unavailable"); });
        Check(missing.InstalledHash == "unavailable" && missing.MetadataError.Length != 0, "Optional metadata must not block capture.");
    }
    private sealed class Policy : IEnemyGatePathPolicy, IEnemyGateRoutePolicyProvider
    {
        internal int WrongPlayer, MissingPlayer, StalePlayer, BecomesStale;
        private bool allCaptured;
        public bool HasPublishedMask => true;
        public bool TryCaptureRoutePolicy(int player, out IEnemyGateRoutePolicySnapshot snapshot)
        { snapshot = new PolicySnapshot(this, player == WrongPlayer ? 0 : player); if (player == 8) allCaptured = true; return player != MissingPlayer; }
        private sealed class PolicySnapshot : IEnemyGateRoutePolicySnapshot
        {
            private readonly Policy owner;
            internal PolicySnapshot(Policy owner, int player) { this.owner = owner; PlayerId = player; }
            public int PlayerId { get; }
            public bool IsCurrent => PlayerId != owner.StalePlayer && !(owner.allCaptured && PlayerId == owner.BecomesStale);
            public bool IsDirectionAllowed(int tile, int direction) => false;
        }
        public bool IsDirectionAllowed(int player, int tile, int direction) => false;
        public int ResolveTribePlayer(int tribe) => 0;
        public int ResolveBuildingPlayer(int player, int tribe) => 0;
        public int ResolveCursorPlayer(int tribe) => 0;
        public object EnterNativeSearch(int player, EnemyGateSearchKind kind) => null;
        public void ExitNativeSearch(object scope, EnemyGateSearchKind kind, bool complete, bool success) { }
    }
    private static void RuntimeFailures(string root)
    {
        double time = 10; var state = new CaptureState(() => time);
        Check(state.Start(1) && state.Pending && !state.Start(1), "Duplicate start.");
        time = 39.999; Check(!state.Expired && state.Captures == 0, "Busy lock must leave capture pending.");
        time = 40; Check(state.Expired, "Capture deadline."); state.Fail(); Check(!state.Pending, "Failed session still retries.");
        Check(!state.Start(1) && state.Start(2), "New session cannot recover."); state.Captured(); Check(!state.Pending, "Second capture too early.");
        time += 10; Check(state.Pending && state.Captures == 1, "Second capture not due."); state.Retire(1); Check(state.Pending, "Stale retirement killed new session."); state.Retire(2); Check(!state.Pending, "Map retirement ignored.");
        Check(state.Start(3), "Next map cannot start."); state.Captured(); time += 10; state.Captured(); Check(!state.Pending && state.Captures == 2, "More than two captures.");
        Check(GateCapture.Read(null)[1] == null, "Standalone policy path.");
        Check(GateCapture.Read(new Policy())[1].PlayerId == 1, "Valid gate publication.");
        foreach (var policy in new[] { new Policy { MissingPlayer = 2 }, new Policy { StalePlayer = 3 }, new Policy { BecomesStale = 1 } })
        { bool retry = false; try { GateCapture.Read(policy); } catch (CaptureNotReadyException) { retry = true; } Check(retry, "Missing/stale gate publication accepted."); }
        bool rejected = false; try { GateCapture.Read(new Policy { WrongPlayer = 2 }); } catch (InvalidOperationException) { rejected = true; } Check(rejected, "Wrong-player gate publication accepted.");
        int calls = 0; var counter = new AllocationCounter(() => () => { calls++; throw new NotImplementedException(); });
        Check(counter.Read() == -1 && counter.Read() == -1 && calls == 1, "Unsupported Mono allocation counter keeps failing.");
        Check(new AllocationCounter(() => { throw new NotSupportedException(); }).Read() == -1, "Allocation discovery failure propagated.");
        var order = new List<string>(); RunPhases.Execute(true, () => order.Add("controlled"), () => order.Add("update"), () => order.Add("workloads")); Check(string.Join(",", order) == "controlled,update,workloads", "Updates lost their budget priority.");
        double tick = 0; int pulses = 0; var pulse = new ProgressPulse(() => tick, message => pulses++); for (int i = 0; i < 100; i++) pulse.Check(); Check(pulses == 0, "Heartbeat flood."); tick = 5; pulse.Check(); pulse.Check(); tick = 10; pulse.Check(); Check(pulses == 2, "Heartbeat interval.");
        bool cleaned = false; var error = RunGuard.Execute(() => { throw new IOException(); }, () => cleaned = true); Check(cleaned && error is IOException, "I/O failure bypassed cleanup.");
        WorkerFailure(root);
    }
    private static CaptureEnvelope Raw(string root)
    {
        const int width = 4, height = 3; var map = new CapturedMap { Width = width, Height = height, Available = new byte[width * height], Rows = new int[height * 3], Flags = new uint[width * height], Heights = new byte[width * height], NativeEdges = new byte[width * height], MoatOwner = new byte[width * height], Structures = new ushort[width * height], Organisms = new ushort[width * height], BuildingTypes = new ushort[1], StructureHeights = new short[1], LowWalls = new byte[1], VegetationStates = new short[1], Players = new[] { 1 } };
        for (int y = 0; y < height; y++) map.Rows[y * 3] = y * width;
        for (int n = 0; n < map.Flags.Length; n++) { map.Available[n] = 1; map.NativeEdges[n] = 255; }
        return new CaptureEnvelope { Map = map, Gates = new IEnemyGateRoutePolicySnapshot[9], Evidence = new ManagedEvidence(root, typeof(EngineInterface).Assembly, path => "fixture") };
    }
    private static void WorkerFailure(string root)
    {
        string output = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../results/worker-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
        bool deny = false; int reports = 0, complete = 0;
        var worker = new SessionWork(1, "fixture", output, (path, text) => {
            if (deny) throw new IOException("fixture write denied"); reports++;
            if (path.EndsWith("snapshot-1.json", StringComparison.Ordinal))
            { var data = (Dictionary<string, object>)Shared.DependencyFreeJson.Parse(text); Check(Convert.ToInt32(data["incomplete"]) == 0 && Convert.ToInt32(data["errors"]) == 0, "Worker comparison incomplete/failed."); complete = Convert.ToInt32(data["compared"]); }
        }, path => Directory.CreateDirectory(path));
        worker.Process(Raw(root), 1); Check(worker.HasPreviousCapture && reports > 0, "First valid snapshot not retained.");
        Check(complete == 1346, "Worker did not exercise every workload.");
        deny = true; worker.Process(Raw(root), 2); Check(!worker.HasPreviousCapture, "Report failure retained paired snapshots.");
        int recoveredReports = 0; var next = new SessionWork(2, "fixture", output, (path, text) => recoveredReports++, path => Directory.CreateDirectory(path)); next.Process(Raw(root), 1); Check(next.HasPreviousCapture && recoveredReports > 0, "Later session did not recover."); next.Release();
        var blocked = new SessionWork(3, "fixture", output, (path, text) => { throw new IOException(); }, path => { throw new IOException(); }); blocked.Process(Raw(root), 1); blocked.FailureJob(new IOException("capture fixture"), 1)(); Check(!blocked.HasPreviousCapture, "Directory failure retained snapshot.");
        CapturedMap malformed = Raw(root).Map; malformed.Heights = null; bool invalid = false;
        try { malformed.Validate(); } catch (ArgumentException) { invalid = true; } Check(invalid, "Missing array did not fail with a validation diagnostic.");
        malformed = Raw(root).Map; malformed.GateMasks[8] = new byte[1]; invalid = false;
        try { malformed.Validate(); } catch (ArgumentException) { invalid = true; } Check(invalid, "Non-selected player mask length was not validated.");
        var cancel = new CancellationTokenSource(); cancel.Cancel(); bool cancelled = false;
        try { Raw(root).Map.Build(1, budget: new WorkBudget(cancellation: cancel.Token)); } catch (OperationCanceledException) { cancelled = true; } Check(cancelled, "Graph construction ignored cancellation.");
    }
}
