using System;
using System.Globalization;
using APIShared;
using APIShared.ModSettings;
using APIShared.Simulation;
using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;

namespace AIHunterPreyTest
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.55.0")]
    [BepInDependency("ImprovedHunters_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class AIHunterPreyPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "AIHunterPreyTest_Serp";
        public const string PluginName = "AI Hunter Prey Test";
        public const string PluginVersion = "0.1.0";
        private static AIHunterSettings settings;
        private static AIHunterRuntime runtime;
        private static ManualLogSource log;
        private static bool initialized;
        private static APIShared.Presentation.IUiFrameWorkRegistration outputWork;
        private static readonly CompositeDisposable subscriptions = new CompositeDisposable();

        private void Awake()
        {
            if (settings != null) return;
            log = Logger;
            settings = new AIHunterSettings();
            LobbyModSettingsPresetRegistration.Register(this, log, PluginGuid, settings,
                "ScriptExtenderUI/AIHunterPreyTestSettings.xaml");
            CrusaderLibrary.Instance.LibraryLoaded += Loaded;
            Info("LOADED version=" + PluginVersion + "; standalone test; ordinary AI placement/costs; no input automation");
        }

        private static void Loaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                runtime = new AIHunterRuntime(settings, new HunterRetirementNative(log, context));
                ApiShared.ForMod(PluginGuid).WhenReady(client =>
                {
                    if (!client.Ui.TryPost(Initialize)) Error("INIT_FAILED Unity dispatch unavailable");
                });
            }
            catch (Exception error) { Error("INIT_FAILED " + error); }
        }

        private static void Initialize()
        {
            if (initialized) return;
            try
            {
                var client = ApiShared.ForMod(PluginGuid);
                if (!client.Ui.TryRegisterBeforeRender("hunter-output", () => runtime.Drain(), out outputWork, out string outputReason))
                    throw new InvalidOperationException(outputReason);
                runtime.OutputQueued = () => outputWork.Request();
                if (!client.TryGetMissionLifecycle(out IMissionLifecycleCapability lifecycle, out var diagnostic))
                    throw new InvalidOperationException(diagnostic?.Reason);
                if (!lifecycle.TryRegisterObserver("hunter-world", runtime.Start, runtime.End, runtime.InitializeMission, out diagnostic))
                    throw new InvalidOperationException(diagnostic?.Reason);
                BuildingR3EventHooks.OnPlacementValidation.Observable
                    .Where(args => args.Phase == EventHookPhase.Post).Subscribe(runtime.ValidatePlacement).AddTo(subscriptions);
                if (!SimulationTickEvents.TryRegister(PluginGuid, "hunter-world", runtime.Tick, out string reason, 500))
                    throw new InvalidOperationException(reason);
                initialized = true;
                outputWork.Request();
                Info("READY publisher=APIShared.MissionLifecycle/SimulationTickEvents; processRooted=true; firstCheckDelay=200; interval=4800 map ticks");
            }
            catch (Exception error) { runtime?.Disable(); Error("INIT_FAILED " + error); }
        }

        internal static new void Info(string message) => log?.LogInfo(Stamp(message));
        internal static void Error(string message) => log?.LogError(Stamp(message));
        private static string Stamp(string message) => "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] AI_HUNTER " + message;
    }
}
