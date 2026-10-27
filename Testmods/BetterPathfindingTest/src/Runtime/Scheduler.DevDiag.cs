#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using APIShared;
using BepInEx.Logging;
using BetterPathfindingTest.Api;
using BetterPathfindingTest.Core;
using BetterPathfindingTest.Service;
using SHCDESE.API.LowLevel;
using UnityEngine;
using R3;
using SHCDESE.EventAPI;

namespace BetterPathfindingTest
{
    public sealed partial class Plugin
    {
        private static DiagnosticRuntime runtime;
        static partial void DiagAttach(ManualLogSource log)
        {
            Shared.DevDiag.Attach(log); runtime = new DiagnosticRuntime();
            Application.onBeforeRender += runtime.Frame;
            CrusaderLibrary.Instance.LibraryLoaded += runtime.LibraryLoaded;
            ApiShared.WhenReady(api => runtime.ApiReady(api));
        }
    }
    internal sealed class DiagnosticRuntime
    {
        private readonly object sync = new object();
        private readonly BackgroundQueue background = new BackgroundQueue();
        private readonly ResidentNavigation service;
        private readonly string output;
        private IApiShared api;
        private CrusaderLibraryLoadContext library;
        private bool hasLibrary, registered, failed;
        private EngineGate engine;
        private SnapshotCapture capture;
        private ResidentReports reports;
        private string context;
        private readonly CaptureState schedule = new CaptureState(Seconds);
        private int frame = -1;
        private double lastPulse, lastCaptureLog;
        private long probedGeneration;
        private NavigationTicket probe;
        private bool probePending;
        private bool probeQueued;
        private int captureHint;
        private readonly List<IDisposable> hints = new List<IDisposable>();
        private static double Seconds() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        internal DiagnosticRuntime()
        {
            output = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "cache", Plugin.PluginGuid);
            service = new ResidentNavigation(background.Enqueue, 256L * 1024 * 1024, log: message => Shared.DevDiag.Log(() => message), selectMemory: () => NavigationMemory.SelectBudget());
            NavigationApi.Bind(service);
        }
        internal void LibraryLoaded(CrusaderLibraryLoadContext value) { lock (sync) { library = value; hasLibrary = true; } }
        internal void ApiReady(IApiShared value) { lock (sync) api = value; }
        private void Observe(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (failed) return;
                if (note.Kind == MissionLifecycleKind.Start)
                {
                    if (!schedule.Start(note.Context.SessionId)) return;
                    context = note.Context.StartKind + "; " + note.Context.MapName + "; " + note.Context.Mode;
                    service.Begin(note.Context.SessionId); reports = new ResidentReports(output, note.Context.SessionId);
                    probePending = probeQueued = false; probedGeneration = 0; lastPulse = lastCaptureLog = 0;
                    Shared.DevDiag.Log(() => "map-start session=" + note.Context.SessionId + "; resident initialization queued; memoryLimit=" + service.MaximumBytes);
                }
                else if (note.IsBeforeInitialization || note.Kind == MissionLifecycleKind.End && note.Context.SessionId == schedule.Session)
                {
                    ResidentReports ended = reports; long id = schedule.Session;
                    schedule.Retire(id); service.End(id); reports = null; probePending = probeQueued = false;
                    if (ended != null) background.Enqueue(() => { try { ended.Status(service.GetStatus(0), service.PeakBytes, service.Metrics()); } catch (Exception ex) { Shared.DevDiag.Log(() => "phase=report-failed end=" + ex.Message); } });
                }
            }
        }
        private void StopObserver(Exception error)
        {
            failed = true; schedule.Fail(); service.Fail(error.Message, true);
            background.Enqueue(() => ResidentReports.Unsupported(output, error));
            Shared.DevDiag.Log(() => "UNSUPPORTED runtime contract; observer stopped: " + error);
        }
        private void SessionFailure(Exception error)
        { schedule.Fail(); service.Fail(error.Message); Shared.DevDiag.Log(() => "phase=capture-failed session=" + schedule.Session + " error=" + error); }
        internal void Frame()
        {
            int number = Time.frameCount; if (frame == number) return; frame = number;
            lock (sync)
            {
                if (failed) return;
                if (!registered)
                {
                    if (!hasLibrary || api == null) return;
                    try
                    {
                        AreaNavigator.InitializeLibrary(System.IO.Path.Combine(BepInEx.Paths.PluginPath, Plugin.PluginGuid, "BetterPathfindingNative.dll"));
                        engine = new EngineGate(); capture = new SnapshotCapture(library);
                        if (!ApiShared.ForMod(Plugin.PluginGuid).TryGetMissionLifecycle(out IMissionLifecycleCapability missions, out NativeCapabilityDiagnostic diagnostic) ||
                            !missions.TryRegisterObserver("resident-navigation", Observe, Observe, Observe, out diagnostic)) throw new NotSupportedException("Mission lifecycle unavailable: " + diagnostic);
                        registered = true;
                        AttachHints();
                        Shared.DevDiag.Log(() => "READY observer; resident API awaits a map; validated engine Monitor and persistent render callback; reports=" + output);
                    }
                    catch (Exception ex) { StopObserver(ex); return; }
                }
                service.Tick(); double now = Seconds();
                if (Interlocked.Exchange(ref captureHint, 0) != 0) schedule.Hint();
                if (reports != null && now - lastPulse >= 5)
                {
                    lastPulse = now; ResidentReports target = reports;
                    background.Enqueue(() => {
                        try { var status = service.GetStatus(0); target.Status(status, service.PeakBytes, service.Metrics()); Shared.DevDiag.Log(() => "phase=resident-status session=" + status.Context.Session + " state=" + status.State + " generation=" + status.Context.Generation + " pending=" + status.PendingBatches + " performanceAccepted=false"); }
                        catch (Exception ex) { Shared.DevDiag.Log(() => "phase=report-failed resident=" + ex.Message); }
                    });
                }
                PollProbe();
                if (!schedule.Pending || reports == null) return;
                if (schedule.Expired) { SessionFailure(new TimeoutException("Capture timeout after 30 seconds: " + schedule.WaitingReason)); return; }
                if (!service.TryBeginCapture()) { var state = service.GetStatus(0).State; if (state == NavigationState.Unsupported || state == NavigationState.Faulted) schedule.Fail(); return; }
                bool locked = false;
                try
                {
                    schedule.WaitingReason = "engine-lock";
                    if (!engine.TryEnter()) return; locked = true;
                    CaptureEnvelope envelope = capture.Copy(schedule.Session, context, schedule.WaitMilliseconds);
                    double capturedAt = Seconds(); ResidentReports target = reports;
                    var owned = new OwnedNavigationSnapshot {
                        Session = envelope.Map.Session, LocalPlayer = envelope.LocalPlayer, Width = envelope.Map.Width, Height = envelope.Map.Height,
                        CapturedAt = capturedAt, Bytes = envelope.Map.BufferBytes + envelope.Map.Flags.LongLength * 9 + 4L * 1024 * 1024,
                        Prepare = budget => { try { envelope.MaterializeGates(budget); } catch (CaptureNotReadyException ex) { throw new NavigationSourcePendingException(ex.Message); } },
                        BuildGround = (player, budget) => Ground(envelope.Map, player, budget),
                        Persist = () => target.Persist(envelope, capturedAt)
                    };
                    if (service.Offer(owned))
                    {
                        schedule.Captured();
                        if (now - lastCaptureLog >= 5) { lastCaptureLog = now; Shared.DevDiag.Log(() => "snapshot-copied session=" + envelope.Map.Session + " sequence=" + schedule.Captures + " copyMs=" + envelope.Map.CopyMilliseconds.ToString("F3") + "; continuous resident capture"); }
                        int perspective = service.GetStatus(0).Context.PlayerId;
                        if (!probePending && !probeQueued && probedGeneration == 0) QueueProbe(envelope.Map.Units, perspective);
                    }
                }
                catch (CaptureContractException ex) { StopObserver(ex); }
                catch (CaptureNotReadyException ex) { schedule.WaitingReason = ex.Message; }
                catch (Exception ex) { SessionFailure(ex); }
                finally { if (locked) engine.Exit(); service.EndCapture(); }
            }
        }
        internal static GridSnapshot Ground(CapturedMap raw, int player, WorkBudget budget)
        {
            GridSnapshot original = raw.Build(player, budget: budget); uint[] words = original.CopyEdges();
            for (int i = 0; i < words.Length; i++) { if ((i & 2047) == 0) budget.Check(); words[i] = (words[i] & 255U & ~(words[i] >> 8)) | (words[i] & 0xff0000U); }
            return new GridSnapshot(original.Width, original.Height, original.CopyValidity(), words);
        }
        private void AttachHints()
        {
            // Hints request a coherent capture. They are not an exhaustive mutation journal.
            hints.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(e => { if (e.Phase == EventHookPhase.Post) Interlocked.Exchange(ref captureHint, 1); }));
            hints.Add(BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(e => { if (e.Phase == EventHookPhase.Post) Interlocked.Exchange(ref captureHint, 1); }));
            hints.Add(BuildingR3EventHooks.OnBuildWall.Observable.Subscribe(e => { if (e.Phase == EventHookPhase.Post) Interlocked.Exchange(ref captureHint, 1); }));
            hints.Add(VegetationR3EventHooks.OnVegetationDelete.Observable.Subscribe(e => { if (e.Phase == EventHookPhase.Post) Interlocked.Exchange(ref captureHint, 1); }));
            hints.Add(MapEditorR3EventHooks.OnSetTileType.Observable.Subscribe(e => { if (e.Phase == EventHookPhase.Post) Interlocked.Exchange(ref captureHint, 1); }));
            hints.Add(MapEditorR3EventHooks.OnSetTileHeightToPreset.Observable.Subscribe(e => { if (e.Phase == EventHookPhase.Post) Interlocked.Exchange(ref captureHint, 1); }));
            hints.Add(MapEditorR3EventHooks.OnSetTileHeightToNumber.Observable.Subscribe(e => { if (e.Phase == EventHookPhase.Post) Interlocked.Exchange(ref captureHint, 1); }));
            hints.Add(MapEditorR3EventHooks.OnRaiseTileHeight.Observable.Subscribe(e => { if (e.Phase == EventHookPhase.Post) Interlocked.Exchange(ref captureHint, 1); }));
            hints.Add(MapEditorR3EventHooks.OnEvenTileHeight.Observable.Subscribe(e => { if (e.Phase == EventHookPhase.Post) Interlocked.Exchange(ref captureHint, 1); }));
        }
        private void QueueProbe(UnitSeed[] seeds, int player)
        {
            long id = schedule.Session; ResidentReports target = reports; probeQueued = true;
            background.Enqueue(() => {
                try
                {
                    lock (sync) if (id != schedule.Session || target != reports || probePending || probedGeneration != 0) return;
                    var status = NavigationApi.GetStatus(player); if (!status.IsReady) return;
                    var input = service.ProbeInputs(player, seeds, 32, out GridSnapshot graph); if (input.Length == 0) return;
                    try { target.Requests(status.Context, graph, input); } catch (Exception ex) { Shared.DevDiag.Log(() => "phase=report-failed api-requests=" + ex.Message); }
                    lock (sync)
                    {
                        if (id != schedule.Session || target != reports) return;
                        if (NavigationApi.SubmitBatch(status.Context, input, out NavigationTicket ticket) == NavigationSubmission.Queued) { probe = ticket; probePending = true; probedGeneration = status.Context.Generation; }
                    }
                }
                catch (Exception ex) { Shared.DevDiag.Log(() => "phase=api-probe-failed session=" + id + " error=" + ex.Message); }
                finally { lock (sync) if (id == schedule.Session) probeQueued = false; }
            });
        }
        private void PollProbe()
        {
            if (!probePending) return;
            NavigationBatchResult result = NavigationApi.Poll(probe); if (result.State == NavigationBatchState.Pending) return;
            probePending = false; ResidentReports target = reports;
            if (result.State != NavigationBatchState.Completed) probedGeneration = 0;
            if (target != null) background.Enqueue(() => { try { target.Probe(result); } catch (Exception ex) { Shared.DevDiag.Log(() => "phase=report-failed api-probe=" + ex.Message); } });
            NavigationApi.Release(probe);
        }
    }
    internal sealed class BackgroundQueue
    {
        private readonly ConcurrentQueue<Action> jobs = new ConcurrentQueue<Action>();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        internal BackgroundQueue() { new Thread(Work) { IsBackground = true, Name = "BetterPathfindingTest", Priority = System.Threading.ThreadPriority.BelowNormal }.Start(); }
        internal void Enqueue(Action job) { jobs.Enqueue(job); wake.Set(); }
        private void Work() { for (;;) { wake.WaitOne(); while (jobs.TryDequeue(out Action job)) { try { job(); } catch (Exception ex) { Shared.DevDiag.Log(() => "worker failed: " + ex); } } } }
    }
}
#endif
