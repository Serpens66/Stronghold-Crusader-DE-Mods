using BepInEx;
using SHCDESE.API.LowLevel;
using SHCDESE.API;
using SHCDESE.BepInEx.Bootstrap;
using System;
using System.IO;
using UnityEngine;

namespace ExtendedData
{
    [BepInDependency("000shcdese", "2.4.0")]
    [BepInDependency("APIShared_Serp", "0.4.0")]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ExtendedDataPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ExtendedData_Serp";
        public const string PluginName = "Extended Data";
        public const string PluginVersion = "1.0.2";
        public const bool ExtendedDataModSettingsOptOut = true;

        private static ExtendedDataRuntime runtime;
        private static bool deferredCompatibilityRefreshPending;

        public ExtendedDataSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
            Shared.DebugLogHelper.LogInfo(Logger, PluginName + " " + PluginVersion + " loaded.");
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try
            {
                CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
                Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName);
                Settings = new ExtendedDataSettingsViewModel();
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/ExtendedDataSettings.xaml");
                string customTrailsRoot = ConfigSettings.GetUserCustomTrailsPath();
                var runtimeCandidate = new ExtendedDataRuntime(Logger, customTrailsRoot, Settings);
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
                Shared.DebugLogHelper.LogError(Logger, "ExtendedData initialization failed: " + ex);
            }
        }

        private void OnDestroy()
        {
            // The BepInEx manager object is destroyed during startup; runtime hooks must survive it.
            Shared.DebugLogHelper.LogDebug(Logger, "ExtendedData OnDestroy called; keeping process-lifetime runtime active.");
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
