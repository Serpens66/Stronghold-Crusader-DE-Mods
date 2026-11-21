using System;
using System.IO;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace OccupiedWallDemolitionTest
{
    [BepInPlugin(Guid, "Occupied Wall Demolition Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.53.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string Guid = "OccupiedWallDemolitionTest_Serp";
        private const string Scenario = "occupied-wall-demolition";
        private static WallHooks hooks;
        private static Fixture activeFixture;
        private static ManualLogSource log;
        private static string requestPath;
        private static System.Guid runId;
        private static bool libraryInitialized, missionsRegistered, started;

        private void Awake()
        {
            log = Logger;
            requestPath = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(requestPath))
                return;
            string[] lines = File.ReadAllLines(requestPath);
            if (lines.Length < 2 || lines[1] != Scenario)
                return;
            if (!System.Guid.TryParseExact(lines[0], "N", out runId) || runId == System.Guid.Empty)
            {
                log.LogError(Stamp("Invalid occupied-wall automation request."));
                return;
            }

            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            ApiShared.ForMod(Guid).WhenReady(_ => RegisterMissions());
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (libraryInitialized)
                return;
            try
            {
                hooks = new WallHooks(context, log);
            }
            catch (Exception error)
            {
                log.LogError(Stamp("WALL_HOOKS_UNAVAILABLE " + error));
            }
            libraryInitialized = true;
            QueueFixture();
        }

        private static void RegisterMissions()
        {
            var api = ApiShared.ForMod(Guid);
            if (!api.TryGetMissionLifecycle(out var missions, out var diagnostic) ||
                !missions.TryRegisterObserver(Scenario,
                    note => hooks?.MissionChanged(note.Context.Mode.IsSingleplayerSkirmish &&
                        !note.Context.Mode.IsRealMultiplayer && !note.Context.IsSave && !note.Context.IsEditor),
                    _ => {
                        hooks?.MissionChanged(false);
                        StopFixture();
                    }, null, out diagnostic))
            {
                log.LogError(Stamp("WALL_MISSION_UNAVAILABLE " + diagnostic.Reason));
                return;
            }
            missionsRegistered = true;
            QueueFixture();
        }

        private static void QueueFixture()
        {
            if (libraryInitialized && missionsRegistered && !ApiShared.ForMod(Guid).Ui.TryPost(StartFixture))
                log.LogError(Stamp("WALL_AUTOMATION_UNAVAILABLE Unity dispatcher rejected the test."));
        }

        private static void StartFixture()
        {
            if (started)
                return;
            started = true;
            File.Delete(requestPath);
            var api = ApiShared.ForMod(Guid);
            if (hooks == null)
            {
                api.Automation.Start(runId, new[] {
                    new APIShared.Automation.AutomationStep("native-contract", TimeSpan.FromSeconds(5),
                        _ => throw new InvalidOperationException("Native wall hooks are unavailable."))
                }, log, quitWhenDone: true);
                return;
            }

            activeFixture = new Fixture(message => log.LogInfo(Stamp(message)), hooks);
            api.Automation.Start(runId, activeFixture.Steps(), log, _ => StopFixture(), quitWhenDone: true);
        }

        private static void StopFixture()
        {
            var fixture = activeFixture;
            activeFixture = null;
            fixture?.Stop();
        }

        internal static string Stamp(string message) => $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
    }
}
