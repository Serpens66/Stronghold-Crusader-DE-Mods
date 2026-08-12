// Standalone reproduction for the SHCDE-SE UnityMainThreadDispatcher lifecycle issue.
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using System;
using System.Threading;
using UnityEngine;

namespace DispatcherLifecycleProbe
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(
        "DispatcherLifecycleProbe",
        "SHCDE Dispatcher Lifecycle Probe",
        "1.0.0")]
    public sealed class UnityMainThreadDispatcherMinimalDiagnostics : BaseUnityPlugin
    {
        private const int WorkerJoinTimeoutMilliseconds = 2000;

        private static ManualLogSource diagnosticLog;
        private static UnityMainThreadDispatcher earlyDispatcher;
        private static int startupThreadId;
        private static int renderProbeCompleted;
        private static int workerCallerThreadId = -1;
        private static int workerInstanceClrNull = -1;
        private static int workerInstanceUnityNull = -1;
        private static int dispatchExecutionThreadId = -1;
        private static string workerError;

        private void Awake()
        {
            diagnosticLog = Logger;
            startupThreadId = Thread.CurrentThread.ManagedThreadId;

            try
            {
                earlyDispatcher = UnityMainThreadDispatcher.Instance;
                if (earlyDispatcher == null)
                {
                    LogError(
                        "PROBE INCONCLUSIVE: Instance returned null in diagnostic Awake; " +
                        $"thread={startupThreadId}.");
                    return;
                }

                DispatcherLifecycleObserver observer =
                    earlyDispatcher.gameObject.AddComponent<DispatcherLifecycleObserver>();
                observer.Initialize("early", startupThreadId);

                Application.onBeforeRender += OnFirstBeforeRender;

                LogInfo(
                    "PROBE START: " +
                    $"thread={startupThreadId}, frame={Time.frameCount}, " +
                    $"earlyClrNull={ReferenceEquals(earlyDispatcher, null)}, " +
                    $"earlyUnityNull={earlyDispatcher == null}.");
            }
            catch (Exception ex)
            {
                LogError($"PROBE SETUP FAILED: {ex}");
            }
        }

        private static void OnFirstBeforeRender()
        {
            if (Interlocked.Exchange(ref renderProbeCompleted, 1) != 0)
                return;

            Application.onBeforeRender -= OnFirstBeforeRender;

            try
            {
                bool earlyClrNull = ReferenceEquals(earlyDispatcher, null);
                bool earlyUnityNull = earlyDispatcher == null;
                int callbackThreadId = Thread.CurrentThread.ManagedThreadId;

                LogInfo(
                    "FIRST BEFORE-RENDER: " +
                    $"thread={callbackThreadId}, startupThread={startupThreadId}, " +
                    $"frame={Time.frameCount}, earlyClrNull={earlyClrNull}, " +
                    $"earlyUnityNull={earlyUnityNull}.");

                // The main thread remains blocked during this bounded join, so this probe
                // cannot accidentally recreate the dispatcher between the two worker calls.
                Thread worker = new Thread(RunBackgroundProbe)
                {
                    IsBackground = true,
                    Name = "SHCDE_DispatcherLifecycleMinimalProbe"
                };
                worker.Start();
                bool workerFinished = worker.Join(WorkerJoinTimeoutMilliseconds);

                LogInfo(
                    "BACKGROUND RESULT BEFORE REPAIR: " +
                    $"finished={workerFinished}, callerThread={workerCallerThreadId}, " +
                    $"instanceClrNull={workerInstanceClrNull}, " +
                    $"instanceUnityNull={workerInstanceUnityNull}, " +
                    $"dispatchExecutionThread={dispatchExecutionThreadId}, " +
                    $"startupThread={startupThreadId}, error={workerError ?? "none"}.");

                if (!workerFinished)
                {
                    LogError(
                        "PROBE INCONCLUSIVE: background calls did not finish within " +
                        $"{WorkerJoinTimeoutMilliseconds} ms; main-thread repair skipped.");
                    return;
                }

                // Resolve Instance only after all baseline observations. On the recorded
                // main thread this recreates the dispatcher if its Unity object was destroyed.
                UnityMainThreadDispatcher currentDispatcher =
                    UnityMainThreadDispatcher.Instance;

                LogInfo(
                    "INSTANCE AFTER OBSERVATION: " +
                    $"currentClrNull={ReferenceEquals(currentDispatcher, null)}, " +
                    $"currentUnityNull={currentDispatcher == null}, " +
                    $"sameClrInstance={ReferenceEquals(earlyDispatcher, currentDispatcher)}, " +
                    $"thread={Thread.CurrentThread.ManagedThreadId}.");
            }
            catch (Exception ex)
            {
                LogError($"PROBE CALLBACK FAILED: {ex}");
            }
        }

        private static void RunBackgroundProbe()
        {
            try
            {
                workerCallerThreadId = Thread.CurrentThread.ManagedThreadId;
                UnityMainThreadDispatcher workerInstance =
                    UnityMainThreadDispatcher.Instance;
                workerInstanceClrNull = ReferenceEquals(workerInstance, null) ? 1 : 0;
                workerInstanceUnityNull = workerInstance == null ? 1 : 0;

                UnityMainThreadDispatcher.Dispatch(() =>
                {
                    dispatchExecutionThreadId =
                        Thread.CurrentThread.ManagedThreadId;
                });
            }
            catch (Exception ex)
            {
                workerError = ex.ToString();
            }
        }

        internal static void LogObserver(string message)
        {
            LogInfo(message);
        }

        private static void LogInfo(string message)
        {
            diagnosticLog?.LogInfo(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }

        private static void LogError(string message)
        {
            diagnosticLog?.LogError(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }
    }

    internal sealed class DispatcherLifecycleObserver : MonoBehaviour
    {
        private string label;
        private int expectedThreadId;
        private int updateCount;
        private bool initialized;

        internal void Initialize(string observerLabel, int mainThreadId)
        {
            label = observerLabel;
            expectedThreadId = mainThreadId;
            initialized = true;

            UnityMainThreadDispatcherMinimalDiagnostics.LogObserver(
                $"OBSERVER INITIALIZED: label={label}, " +
                $"thread={Thread.CurrentThread.ManagedThreadId}, " +
                $"expectedThread={expectedThreadId}, frame={Time.frameCount}.");
        }

        private void Start()
        {
            UnityMainThreadDispatcherMinimalDiagnostics.LogObserver(
                $"OBSERVER START: label={label}, initialized={initialized}, " +
                $"thread={Thread.CurrentThread.ManagedThreadId}, " +
                $"frame={Time.frameCount}.");
        }

        private void Update()
        {
            updateCount++;
            if (updateCount == 1)
            {
                UnityMainThreadDispatcherMinimalDiagnostics.LogObserver(
                    $"OBSERVER FIRST UPDATE: label={label}, " +
                    $"thread={Thread.CurrentThread.ManagedThreadId}, " +
                    $"expectedThread={expectedThreadId}, frame={Time.frameCount}.");
            }
        }

        private void OnDisable()
        {
            UnityMainThreadDispatcherMinimalDiagnostics.LogObserver(
                $"OBSERVER DISABLED: label={label}, initialized={initialized}, " +
                $"updates={updateCount}, thread={Thread.CurrentThread.ManagedThreadId}, " +
                $"frame={Time.frameCount}.");
        }

        private void OnDestroy()
        {
            UnityMainThreadDispatcherMinimalDiagnostics.LogObserver(
                $"OBSERVER DESTROYED: label={label}, initialized={initialized}, " +
                $"updates={updateCount}, thread={Thread.CurrentThread.ManagedThreadId}, " +
                $"frame={Time.frameCount}.");
        }
    }
}
