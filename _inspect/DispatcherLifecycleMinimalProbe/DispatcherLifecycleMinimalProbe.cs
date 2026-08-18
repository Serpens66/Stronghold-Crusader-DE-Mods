using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using System;
using System.Threading;
using UnityEngine;

namespace DispatcherLifecycleMinimalProbe
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(
        "DispatcherLifecycleMinimalProbe",
        "Dispatcher Lifecycle Minimal Probe",
        "1.0.0")]
    public sealed class ProbePlugin : BaseUnityPlugin
    {
        private static ManualLogSource log;
        private static UnityMainThreadDispatcher earlyDispatcher;
        private static int startupThreadId;
        private static int renderProbeCompleted;
        private static int workerCallerThreadId = -1;
        private static int workerInstanceWasNull = -1;
        private static int dispatchExecutionThreadId = -1;

        private void Awake()
        {
            log = Logger;
            startupThreadId = Thread.CurrentThread.ManagedThreadId;
            earlyDispatcher = UnityMainThreadDispatcher.Instance;

            DispatcherLifecycleObserver observer =
                earlyDispatcher.gameObject.AddComponent<DispatcherLifecycleObserver>();
            observer.Initialize(log, "early", startupThreadId);

            Application.onBeforeRender += OnFirstBeforeRender;

            Log(
                "PROBE START: " +
                $"thread={startupThreadId}, frame={Time.frameCount}, " +
                $"earlyClrNull={ReferenceEquals(earlyDispatcher, null)}, " +
                $"earlyUnityNull={earlyDispatcher == null}.");
        }

        private static void OnFirstBeforeRender()
        {
            if (Interlocked.Exchange(ref renderProbeCompleted, 1) != 0)
                return;

            Application.onBeforeRender -= OnFirstBeforeRender;

            bool earlyClrNull = ReferenceEquals(earlyDispatcher, null);
            bool earlyUnityNull = earlyDispatcher == null;

            Log(
                "FIRST BEFORE-RENDER: " +
                $"thread={Thread.CurrentThread.ManagedThreadId}, " +
                $"frame={Time.frameCount}, earlyClrNull={earlyClrNull}, " +
                $"earlyUnityNull={earlyUnityNull}.");

            Thread worker = new Thread(RunBackgroundProbe)
            {
                IsBackground = true,
                Name = "DispatcherLifecycleMinimalProbe"
            };
            worker.Start();
            bool workerFinished = worker.Join(2000);

            Log(
                "BACKGROUND RESULT BEFORE REPAIR: " +
                $"finished={workerFinished}, callerThread={workerCallerThreadId}, " +
                $"instanceWasNull={workerInstanceWasNull}, " +
                $"dispatchExecutionThread={dispatchExecutionThreadId}, " +
                $"startupThread={startupThreadId}.");

            // Deliberately resolve Instance only after recording the original state.
            UnityMainThreadDispatcher currentDispatcher =
                UnityMainThreadDispatcher.Instance;

            Log(
                "INSTANCE AFTER OBSERVATION: " +
                $"currentUnityNull={currentDispatcher == null}, " +
                $"sameClrInstance={ReferenceEquals(earlyDispatcher, currentDispatcher)}.");
        }

        private static void RunBackgroundProbe()
        {
            workerCallerThreadId = Thread.CurrentThread.ManagedThreadId;
            workerInstanceWasNull =
                UnityMainThreadDispatcher.Instance == null ? 1 : 0;

            UnityMainThreadDispatcher.Dispatch(() =>
            {
                dispatchExecutionThreadId =
                    Thread.CurrentThread.ManagedThreadId;
            });
        }

        internal static void Log(string message)
        {
            log.LogInfo(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }
    }

    internal sealed class DispatcherLifecycleObserver : MonoBehaviour
    {
        private string label;
        private int expectedThreadId;
        private int updateCount;
        private bool initialized;

        internal void Initialize(
            ManualLogSource diagnosticLog,
            string observerLabel,
            int mainThreadId)
        {
            label = observerLabel;
            expectedThreadId = mainThreadId;
            initialized = true;

            ProbePlugin.Log(
                $"OBSERVER INITIALIZED: label={label}, " +
                $"thread={Thread.CurrentThread.ManagedThreadId}, " +
                $"expectedThread={expectedThreadId}, frame={Time.frameCount}.");
        }

        private void Start()
        {
            ProbePlugin.Log(
                $"OBSERVER START: label={label}, initialized={initialized}, " +
                $"thread={Thread.CurrentThread.ManagedThreadId}, " +
                $"frame={Time.frameCount}.");
        }

        private void Update()
        {
            updateCount++;
            if (updateCount == 1)
            {
                ProbePlugin.Log(
                    $"OBSERVER FIRST UPDATE: label={label}, " +
                    $"thread={Thread.CurrentThread.ManagedThreadId}, " +
                    $"expectedThread={expectedThreadId}, frame={Time.frameCount}.");
            }
        }

        private void OnDisable()
        {
            ProbePlugin.Log(
                $"OBSERVER DISABLED: label={label}, initialized={initialized}, " +
                $"updates={updateCount}, thread={Thread.CurrentThread.ManagedThreadId}, " +
                $"frame={Time.frameCount}.");
        }

        private void OnDestroy()
        {
            ProbePlugin.Log(
                $"OBSERVER DESTROYED: label={label}, initialized={initialized}, " +
                $"updates={updateCount}, thread={Thread.CurrentThread.ManagedThreadId}, " +
                $"frame={Time.frameCount}.");
        }
    }
}
