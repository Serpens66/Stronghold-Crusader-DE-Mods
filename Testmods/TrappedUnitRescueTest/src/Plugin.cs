using System;
using System.IO;
using System.Threading;
using APIShared;
using APIShared.ModSettings;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace TrappedUnitRescueTest
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.53.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Pathfinding_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "TrappedUnitRescueTest_Serp", Name = "Trapped Unit Rescue Test", Version = "0.1.0";
        private static ManualLogSource log;
        private static Settings settings;
        private static RescueRuntime runtime;
        private static FixesIntegration fixes;
        private static Fixture fixture;
        private static IMissionLifecycleCapability missions;
        private static int active, fixtureActive;
        private static bool initialized, libraryReady, settingsReady;
        private static Plugin instance;

        private void Awake()
        {
            if (initialized) return;
            initialized = true; instance = this; log = Logger; settings = new Settings();
            // Static fields, LibraryLoaded and public mission/automation publishers retain all work
            // after SHCDE destroys early plugin components. There is deliberately no Unity teardown.
            CrusaderLibrary.Instance.LibraryLoaded += LibraryLoaded;
            ApiShared.ForMod(Guid).WhenReady(_ => {
                if (!ApiShared.ForMod(Guid).Ui.TryPost(InitializeSettings)) Stamp("INIT_FAILED Unity dispatcher unavailable");
            });
        }

        private static void LibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (libraryReady) return;
            try
            {
                var native = NativeRelocation.Create();
                fixes = new FixesIntegration(log);
                fixes.Initialize(context);
                runtime = new RescueRuntime(native, () => Volatile.Read(ref active) != 0, Stamp);
                libraryReady = true;
                Stamp("NATIVE_READY; experimental setting defaults off; gatehouse farmer fix retained");
                if (settingsReady) StartRequestedFixture();
            }
            catch (Exception ex) { SetActive(false); Stamp("INIT_FAILED " + ex); }
        }

        private static void InitializeSettings()
        {
            try
            {
                LobbyModSettingsPresetRegistration.Register(instance, log, Name, settings,
                    "ScriptExtenderUI/TrappedUnitRescueTestSettings.xaml");
                if (!ApiShared.ForMod(Guid).TryGetMissionLifecycle(out missions, out var diagnostic))
                    throw new InvalidOperationException(diagnostic.Reason);
                if (!missions.TryRegisterObserver("rescue-session", n => {
                    // Automatic mutation is currently confined to explicit disposable fixtures.
                    // Merely loading this test plugin never turns a user save into an experiment.
                    SetActive(Volatile.Read(ref fixtureActive) != 0 && settings.EnableMod && settings.EnableExperimentalRescue);
                }, n => { SetActive(false); fixture?.Stop(); }, null, out var observerDiagnostic))
                    throw new InvalidOperationException(observerDiagnostic.Reason);
                settingsReady = true;
                Stamp("SETTINGS_READY; no request means no automatic scenario or rescue");
                if (libraryReady) StartRequestedFixture();
            }
            catch (Exception ex) { SetActive(false); Stamp("INIT_FAILED settings: " + ex); }
        }

        private static void StartRequestedFixture()
        {
            if (fixture != null) return;
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            if (lines.Length < 2 || !Fixture.Owns(lines[1])) return;
            if (!System.Guid.TryParseExact(lines[0], "N", out var runId) || runId == System.Guid.Empty)
            { Stamp("Invalid owned request; not consumed"); return; }
            string map = instance.Config.Bind("Fixture", "Map", "test AI.map", "Disposable local Skirmish map; suitability is checked before each scenario.").Value;
            fixture = new Fixture(map, lines[1], Stamp, runtime, enabled => {
                settings.EnableMod = true; settings.EnableExperimentalRescue = enabled;
                Interlocked.Exchange(ref fixtureActive, 1);
                SetActive(enabled);
            });
            File.Delete(request);
            ApiShared.ForMod(Guid).Automation.Start(runId, fixture.Steps(), log,
                result => {
                    SetActive(false); Interlocked.Exchange(ref fixtureActive, 0); fixture.Stop();
                    Stamp("finished status=" + result.Status + " detail=" + result.Detail);
                }, quitWhenDone: true);
        }

        private static void SetActive(bool enabled)
        {
            bool allow = enabled && runtime != null && fixes != null && fixes.Ready &&
                missions?.Current != null && Volatile.Read(ref fixtureActive) != 0;
            // Disable foreign suppression first; enable it only after the replacement is available.
            if (!allow) fixes?.SetEnabled(false);
            Volatile.Write(ref active, allow ? 1 : 0);
            if (allow) fixes.SetEnabled(true);
        }
        private static void Stamp(string message) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] RESCUE_TEST " + message);
    }
}
