#if SERP_DEVDIAG
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using APIShared;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using UnityEngine;

namespace BetterPathfindingTest
{
    public sealed partial class Plugin
    {
        private static DiagnosticRuntime runtime;
        static partial void DiagAttach(ManualLogSource log)
        {
            Shared.DevDiag.Attach(log);
            runtime = new DiagnosticRuntime();
            // Static publishers and the static runtime survive SHCDE's startup component cleanup.
            Application.onBeforeRender += runtime.Frame;
            CrusaderLibrary.Instance.LibraryLoaded += runtime.LibraryLoaded;
            ApiShared.WhenReady(api => runtime.ApiReady(api));
        }
    }
    internal sealed class DiagnosticRuntime
    {
        private readonly object sync = new object();
        private readonly BackgroundQueue background = new BackgroundQueue();
        private readonly string output;
        private IApiShared api;
        private CrusaderLibraryLoadContext library;
        private bool hasLibrary, registered, failed;
        private EngineGate engine;
        private SnapshotCapture capture;
        private SessionWork session;
        private int frame = -1;
        internal DiagnosticRuntime()
        {
            output = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "cache", Plugin.PluginGuid);
        }
        internal void LibraryLoaded(CrusaderLibraryLoadContext context) { lock (sync) { library = context; hasLibrary = true; } }
        internal void ApiReady(IApiShared value) { lock (sync) api = value; }
        private void Queue(Action job) => background.Enqueue(job);
        private void Observe(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (note.Kind == MissionLifecycleKind.Start)
                {
                    if (session != null && session.Id == note.Context.SessionId) return;
                    session?.Cancel.Cancel();
                    session = new SessionWork(note.Context.SessionId, note.Context.StartKind + "; " + note.Context.MapName + "; " + note.Context.Mode, output);
                    Shared.DevDiag.Log(() => "map-start session=" + session.Id + "; capture queued; post-startup lifecycle active.");
                }
                else if (note.IsBeforeInitialization || note.Kind == MissionLifecycleKind.End && session != null && session.Id == note.Context.SessionId)
                { session?.Cancel.Cancel(); session = null; }
            }
        }
        internal void Frame()
        {
            int now = Time.frameCount; if (frame == now) return; frame = now;
            lock (sync)
            {
                if (failed) return;
                try
                {
                    if (!registered)
                    {
                        if (!hasLibrary || api == null) return;
                        engine = new EngineGate(); capture = new SnapshotCapture(library);
                        if (!ApiShared.ForMod(Plugin.PluginGuid).TryGetMissionLifecycle(out IMissionLifecycleCapability missions, out NativeCapabilityDiagnostic diagnostic) ||
                            !missions.TryRegisterObserver("automatic-comparison", Observe, Observe, Observe, out diagnostic)) throw new NotSupportedException("Mission lifecycle unavailable: " + diagnostic);
                        registered = true;
                        Shared.DevDiag.Log(() => "READY; engine Monitor contract validated against installed assembly; render callback survives startup cleanup; reports=" + output);
                    }
                    SessionWork current = session;
                    if (current == null || current.Captures >= 2 || current.Cancel.IsCancellationRequested || Stopwatch.GetTimestamp() < current.Due) return;
                    if (current.Attempt == 0) current.Attempt = Stopwatch.GetTimestamp();
                    // No Monitor.Enter/wait on the Unity thread: retry on a later rendered frame.
                    if (!engine.TryEnter()) return;
                    CaptureEnvelope envelope;
                    try
                    {
                        if (current.Cancel.IsCancellationRequested) return;
                        double wait = (Stopwatch.GetTimestamp() - current.Attempt) * 1000.0 / Stopwatch.Frequency;
                        envelope = capture.Copy(current.Id, current.Context, wait);
                    }
                    finally { engine.Exit(); }
                    int ordinal = ++current.Captures; current.Attempt = 0;
                    current.Due = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 10;
                    Queue(current.Job(envelope, ordinal));
                    Shared.DevDiag.Log(() => "snapshot-copied session=" + current.Id + " ordinal=" + ordinal + " copyMs=" + envelope.Map.CopyMilliseconds.ToString("F3") + "; post-startup render active.");
                }
                catch (Exception ex)
                {
                    failed = true; session?.Cancel.Cancel();
                    string reportOutput = output;
                    Queue(() => SessionWork.Unsupported(reportOutput, ex));
                    Shared.DevDiag.Log(() => "UNSUPPORTED capture contract; observer stopped: " + ex);
                }
            }
        }
    }
    // The thread target owns no native library context, capture adapter or Unity objects.
    internal sealed class BackgroundQueue
    {
        private readonly ConcurrentQueue<Action> jobs = new ConcurrentQueue<Action>();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        internal BackgroundQueue()
        { new Thread(Work) { IsBackground = true, Name = "BetterPathfindingTest", Priority = System.Threading.ThreadPriority.BelowNormal }.Start(); }
        internal void Enqueue(Action job) { jobs.Enqueue(job); wake.Set(); }
        private void Work()
        {
            for (;;)
            {
                wake.WaitOne();
                while (jobs.TryDequeue(out Action job))
                { try { job(); } catch (Exception ex) { Shared.DevDiag.Log(() => "worker failed: " + ex); } }
            }
        }
    }
}
#endif
