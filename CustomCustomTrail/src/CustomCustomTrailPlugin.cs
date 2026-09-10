using BepInEx;
using SHCDESE.API.LowLevel;
using SHCDESE.API;
using SHCDESE.BepInEx.Bootstrap;
using System;
using System.IO;
using UnityEngine;

namespace CustomCustomTrail
{
    [BepInDependency("000shcdese", "2.4.0")]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class CustomCustomTrailPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "CustomCustomTrail_Serp";
        public const string PluginName = "Custom Custom Trail";
        public const string PluginVersion = "1.3.51";
        public const bool CustomCustomTrailModSettingsOptOut = true;

        private static CustomCustomTrailRuntime runtime;
        private static bool deferredCompatibilityRefreshPending;

        public CustomCustomTrailSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger, PluginName + " " + PluginVersion + " loaded.");
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try
            {
                CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
                Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName);
                Settings = new CustomCustomTrailSettingsViewModel();
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/CustomCustomTrailSettings.xaml");
                string customTrailsRoot = ConfigSettings.GetUserCustomTrailsPath();
                var runtimeCandidate = new CustomCustomTrailRuntime(Logger, customTrailsRoot, Settings);
                try
                {
                    runtimeCandidate.Initialize();
                }
                catch
                {
                    // Dispose is only valid while rolling back an unpublished candidate.
                    runtimeCandidate.Dispose();
                    throw;
                }

                runtime = runtimeCandidate;
                ScheduleDeferredCompatibilityRefresh();
                Settings.RuntimeActivationChanged += runtime.SetEnabled;
                Plugin.ModSettingsHubViewModel.PropertyChanged += (_, __) =>
                {
                    runtime?.RefreshModCompatibility();
                };
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, "CustomCustomTrail initialization failed: " + ex);
            }
        }

        private void OnDestroy()
        {
            // The BepInEx manager object is destroyed during startup; runtime hooks must survive it.
            Shared.DebugLogHelper.LogDebug(Logger, "CustomCustomTrail OnDestroy called; keeping process-lifetime runtime active.");
        }

        private static void ScheduleDeferredCompatibilityRefresh()
        {
            if (deferredCompatibilityRefreshPending)
                return;

            deferredCompatibilityRefreshPending = true;
            Application.onBeforeRender += RefreshCompatibilityAfterRegistrations;
        }

        private static void RefreshCompatibilityAfterRegistrations()
        {
            Application.onBeforeRender -= RefreshCompatibilityAfterRegistrations;
            deferredCompatibilityRefreshPending = false;
            runtime?.RefreshModCompatibility();
        }

    }
}
