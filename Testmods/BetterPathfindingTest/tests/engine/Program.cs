using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;
using APIShared;
using BetterPathfindingTest;
using BetterPathfindingTest.Core;
using BetterPathfindingTest.Api;
using BetterPathfindingTest.Service;

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
        try { Run(); Evidence(args[0], args[1]); RuntimeFailures(args[0]); Console.WriteLine("PASS: loader=" + args[1] + "; installed managed/native-layout contracts, capture state and worker failures; no native game method called."); return 0; }
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
        ManagedContracts.ValidatePackedLengths(320800, 320800);
        bool capacityRejected = false; try { ManagedContracts.ValidatePackedLengths(320800, 320799); } catch (CaptureContractException) { capacityRejected = true; }
        Check(capacityRejected, "SDK capacity mismatch was not classified as a permanent contract failure.");
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
        time += 0.5; Check(state.Pending && state.Captures == 1, "Periodic capture not due."); state.Retire(1); Check(state.Pending, "Stale retirement killed new session."); state.Retire(2); Check(!state.Pending, "Map retirement ignored.");
        Check(state.Start(3), "Next map cannot start."); state.Captured(); time += 0.5; state.Captured(); Check(!state.Pending && state.Captures == 2, "Periodic capture interval."); time += 0.5; Check(state.Pending, "Resident capture stopped after two snapshots."); state.Captured(); time += 0.1; state.Hint(); Check(!state.Pending, "Hint ignored minimum spacing."); time += 0.15; Check(state.Pending, "Event hint did not accelerate capture.");
        Check(GateCapture.Read(null)[1] == null, "Standalone policy path.");
        Check(GateCapture.Read(new Policy())[1].PlayerId == 1, "Valid gate publication.");
        foreach (var policy in new[] { new Policy { MissingPlayer = 2 }, new Policy { StalePlayer = 3 }, new Policy { BecomesStale = 1 } })
        { bool retry = false; try { GateCapture.Read(policy); } catch (CaptureNotReadyException) { retry = true; } Check(retry, "Missing/stale gate publication accepted."); }
        bool rejected = false; try { GateCapture.Read(new Policy { WrongPlayer = 2 }); } catch (InvalidOperationException) { rejected = true; } Check(rejected, "Wrong-player gate publication accepted.");
        int calls = 0; var counter = new AllocationCounter(() => () => { calls++; throw new NotImplementedException(); });
        Check(counter.Read() == -1 && counter.Read() == -1 && calls == 1, "Unsupported Mono allocation counter keeps failing.");
        Check(new AllocationCounter(() => { throw new NotSupportedException(); }).Read() == -1, "Allocation discovery failure propagated.");
        Check(new AllocationCounter(() => () => 0).Read() == -1, "Mono zero counter was treated as allocation-free.");
        long selectedMemory = NavigationMemory.SelectBudget(); Check(selectedMemory >= 0 && selectedMemory <= 1024L * 1024 * 1024, "Windows physical-memory budget outside limits.");
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
        var jobs = new Queue<Action>(); Action drain = () => { int count = 0; while (jobs.Count != 0) { if (++count > 1000) throw new Exception("Worker did not drain."); jobs.Dequeue()(); } };
        double time = 0; bool deny = false; int reports = 0;
        var writer = new ResidentReports(output, 1, (path, text) => { if (deny) throw new IOException("fixture denied"); reports++; }, path => Directory.CreateDirectory(path));
        var service = new ResidentNavigation(jobs.Enqueue, 256L * 1024 * 1024, () => time);
        Action<double> submit = at => {
            CaptureEnvelope envelope = Raw(root); envelope.Map.Session = 1; envelope.LocalPlayer = 1;
            service.Offer(new OwnedNavigationSnapshot { Session = 1, LocalPlayer = 1, Width = envelope.Map.Width, Height = envelope.Map.Height,
                CapturedAt = at, Bytes = envelope.Map.BufferBytes + 4096, Prepare = b => envelope.MaterializeGates(b),
                BuildGround = (player,b) => envelope.Map.Build(player, budget:b), Persist = () => writer.Persist(envelope, at) });
        };
        service.Begin(1, 1); submit(0); drain();
        Check(service.GetStatus(1).IsReady && reports > 0 && File.Exists(Path.Combine(writer.DirectoryPath, "snapshot-1.raw.gz")), "Initial resident capture/readiness.");
        var status = service.GetStatus(1);
        writer.Status(status, service.PeakBytes, service.Metrics());
        var probeInput = service.ProbeInputs(1, new UnitSeed[0], 4, out GridSnapshot probeGraph);
        writer.Requests(status.Context, probeGraph, probeInput); Check(File.Exists(Path.Combine(writer.DirectoryPath, "api-probe.bpf.gz")), "API replay input not persisted.");
        Check(service.Submit(status.Context, new[] { new NavigationRequest(17, 0, 0, 3, 2) }, out NavigationTicket ticket) == NavigationSubmission.Queued, "Framework API queue.");
        drain(); Check(service.Poll(ticket).State == NavigationBatchState.Completed && service.Poll(ticket).Routes[0].State == NavigationRouteState.Found, "Framework private native/API positive path."); service.Release(ticket);
        deny = true; time = 10; submit(time); drain(); Check(service.GetStatus(1).IsReady, "Reporting failure stopped valid resident data.");
        deny = false; time = 11; submit(time); drain(); Check(File.Exists(Path.Combine(writer.DirectoryPath, "snapshot-2.raw.gz")), "Second raw capture missing.");
        service.End(1); drain(); Check(service.SourceBytes == 0 && service.ClientBytes == 0 && service.GetStatus(1).State == NavigationState.NotLoaded, "Resident map cleanup.");
        service.Begin(2, 1); var raw = Raw(root); raw.Map.Session = 2;
        service.Offer(new OwnedNavigationSnapshot { Session = 2, LocalPlayer = 1, Width = 4, Height = 3, Bytes = 8192, BuildGround = (p,b) => raw.Map.Build(p, budget:b), Persist = () => { throw new IOException("directory fixture"); } }); drain();
        Check(service.GetStatus(1).IsReady, "Later session failed after report errors."); service.End(2); drain();
        CapturedMap malformed = Raw(root).Map; malformed.Heights = null; bool invalid = false;
        try { malformed.Validate(); } catch (ArgumentException) { invalid = true; } Check(invalid, "Missing array did not fail with a validation diagnostic.");
        malformed = Raw(root).Map; malformed.GateMasks[8] = new byte[1]; invalid = false;
        try { malformed.Validate(); } catch (ArgumentException) { invalid = true; } Check(invalid, "Non-selected player mask length was not validated.");
        var cancel = new CancellationTokenSource(); cancel.Cancel(); bool cancelled = false;
        try { Raw(root).Map.Build(1, budget: new WorkBudget(cancellation: cancel.Token)); } catch (OperationCanceledException) { cancelled = true; } Check(cancelled, "Graph construction ignored cancellation.");
    }
}
