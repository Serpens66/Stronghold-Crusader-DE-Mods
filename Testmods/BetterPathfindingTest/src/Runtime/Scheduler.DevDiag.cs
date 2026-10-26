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
        private readonly CaptureState schedule = new CaptureState(() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
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
                if (failed) return;
                if (note.Kind == MissionLifecycleKind.Start)
                {
                    if (!schedule.Start(note.Context.SessionId)) return;
                    RetireSession();
                    session = new SessionWork(note.Context.SessionId, note.Context.StartKind + "; " + note.Context.MapName + "; " + note.Context.Mode, output);
                    Shared.DevDiag.Log(() => "map-start session=" + session.Id + "; capture queued; post-startup lifecycle active.");
                }
                else if (note.IsBeforeInitialization || note.Kind == MissionLifecycleKind.End && session != null && session.Id == note.Context.SessionId)
                { if (session != null) schedule.Retire(session.Id); RetireSession(); }
            }
        }
        private void RetireSession()
        {
            SessionWork old = session; session = null;
            if (old == null) return;
            old.Cancel.Cancel(); Queue(old.Release);
        }
        private void FailSession(Exception error)
        {
            schedule.Fail(); SessionWork current = session;
            if (current == null) return;
            Shared.DevDiag.Log(() => "phase=capture-failed session=" + current.Id + " ordinal=" + (schedule.Captures + 1) + " status=INCOMPLETE error=" + error);
            current.Cancel.Cancel(); Queue(current.FailureJob(error, schedule.Captures + 1)); Queue(current.Release);
        }
        private void StopObserver(Exception error)
        {
            failed = true; RetireSession();
            string reportOutput = output;
            Queue(() => SessionWork.Unsupported(reportOutput, error));
            Shared.DevDiag.Log(() => "UNSUPPORTED runtime contract; observer stopped: " + error);
        }
        internal void Frame()
        {
            int now = Time.frameCount; if (frame == now) return; frame = now;
            lock (sync)
            {
                if (failed) return;
                if (!registered)
                {
                    if (!hasLibrary || api == null) return;
                    try
                    {
                        engine = new EngineGate(); capture = new SnapshotCapture(library);
                        if (!ApiShared.ForMod(Plugin.PluginGuid).TryGetMissionLifecycle(out IMissionLifecycleCapability missions, out NativeCapabilityDiagnostic diagnostic) ||
                            !missions.TryRegisterObserver("automatic-comparison", Observe, Observe, Observe, out diagnostic)) throw new NotSupportedException("Mission lifecycle unavailable: " + diagnostic);
                        registered = true;
                        Shared.DevDiag.Log(() => "READY; engine Monitor contract validated against installed assembly; render callback survives startup cleanup; reports=" + output);
                    }
                    catch (Exception ex)
                    {
                        StopObserver(ex); return;
                    }
                }
                SessionWork current = session;
                if (current == null || current.Cancel.IsCancellationRequested || !schedule.Pending) return;
                if (schedule.Expired) { FailSession(new TimeoutException("Capture timeout after 30 seconds: " + schedule.WaitingReason)); return; }
                schedule.WaitingReason = "engine-lock";
                // No Monitor.Enter/wait on the Unity thread: retry on a later rendered frame.
                if (!engine.TryEnter()) return;
                try
                {
                    CaptureEnvelope envelope = capture.Copy(current.Id, current.Context, schedule.WaitMilliseconds);
                    schedule.Captured(); int ordinal = schedule.Captures;
                    Queue(current.Job(envelope, ordinal));
                    Shared.DevDiag.Log(() => "snapshot-copied session=" + current.Id + " ordinal=" + ordinal + " copyMs=" + envelope.Map.CopyMilliseconds.ToString("F3") + "; post-startup render active.");
                }
                catch (CaptureContractException ex) { StopObserver(ex); }
                catch (CaptureNotReadyException ex) { schedule.WaitingReason = ex.Message; }
                catch (Exception ex) { FailSession(ex); }
                finally { engine.Exit(); }
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
