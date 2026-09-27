using BepInEx;
using BepInEx.Logging;
using ExtendedData;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace AIResourceReserveTest
{
    [BepInDependency("000shcdese", "2.11.0")]
    [BepInDependency("APIShared_Serp", "0.4.2")]
    [BepInDependency("ExtendedData_Serp", "1.0.3")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class AIResourceReserveTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "AIResourceReserveTest_Serp";
        public const string Name = "AI Resource Reserve Test";
        public const string Version = "0.1.0";

        private static ManualLogSource log;
        private static AIResourceReserveRuntime runtime;
        private static IDisposable sessionSubscription;
        private static bool libraryRegistered;

        private void Awake()
        {
            log = Logger;
            Shared.DebugLogHelper.LogInfo(log, $"{Name} {Version} loaded; waiting for native library.");
            if (libraryRegistered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            libraryRegistered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name, requireCurrentVersion: true))
                return;
            if (!ExtendedDataModDataApi.SupportsSinglePlayerSelections)
            {
                Shared.DebugLogHelper.LogError(log,
                    "AI reserve requires ExtendedData selected-Lord support for single-player sessions.");
                return;
            }

            IDisposable candidateSubscription = null;
            bool candidateTickRegistered = false;
            try
            {
                var candidate = new AIResourceReserveRuntime(log);
                candidateSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(
                    log, candidate.OnSessionStarted, candidate.OnSessionEnded);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                candidateTickRegistered = true;

                // Both references survive SHCDE's normal destruction of the plugin component.
                runtime = candidate;
                sessionSubscription = candidateSubscription;
                Shared.DebugLogHelper.LogInfo(log,
                    "AI_RESERVE_READY: publisher=GameTimeManagerAPI.OnTick, session=APIShared mission lifecycle.");
            }
            catch (Exception error)
            {
                if (candidateTickRegistered)
                    GameTimeManagerAPI.Instance.OnTick -= OnTick;
                candidateSubscription?.Dispose(); // Unpublished initialization only.
                Shared.DebugLogHelper.LogError(log, "AI reserve initialization failed: " + error);
            }
        }

        private static void OnTick(int tick) => runtime?.OnTick(tick);
    }
}
