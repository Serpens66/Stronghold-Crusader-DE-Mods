using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace PlagueHeightTest
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.9.0")]
    [BepInDependency("APIShared_Serp", "0.4.2")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class PlagueHeightTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "PlagueHeightTest_Serp", Name = "Plague Height Test", Version = "0.1.0";
        private static ManualLogSource log;
        private static ConfigEntry<bool> enabledSetting;
        private static PlagueHeightRuntime runtime;
        private static IDisposable lifecycle, loading;
        private static bool registered;
        private static volatile bool inMission;
        private void Awake()
        {
            if (registered) return;
            registered = true; log = Logger;
            enabledSetting = Config.Bind("General", "Enabled", true,
                "Enable height-aware plague damage. Change before a mission; all multiplayer peers must use the same value. In-mission changes are deferred until mission end.");
            enabledSetting.SettingChanged += OnSettingChanged;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version + " loaded; native height tolerance=20.");
        }
        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null || !Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name, true)) return;
            try
            {
                // Connect the persistent mission publisher before any native hook is installed.
                loading = Shared.MissionEvents.Loading.Subscribe(e => { inMission = true; });
                lifecycle = Shared.GameplaySessionLifecycle.SubscribeStarted(log, e => { inMission = true; },
                    () => { inMission = false; runtime?.SetEnabled(enabledSetting.Value); });
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                var candidate = new PlagueHeightRuntime(log, context);
                runtime = candidate;
                candidate.Publish(enabledSetting.Value);
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "PlagueHeightTest initialization failed; height fix inactive: " + ex); }
        }
        private static void OnSettingChanged(object sender, EventArgs args)
        {
            if (!inMission) runtime?.SetEnabled(enabledSetting.Value);
            else Shared.DebugLogHelper.LogInfo(log, "PlagueHeightTest configuration change deferred until mission end.");
        }
        private static void OnTick(int tick)
        {
            try { runtime?.OnTick(tick); }
            catch (Exception ex) { Shared.DebugLogHelper.LogErrorLimited(log, "PlagueHeightTest diagnostics", "PlagueHeightTest diagnostic callback failed: " + ex); }
        }
    }
}
