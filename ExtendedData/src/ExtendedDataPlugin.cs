using APIShared.ModSettings;
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
    [BepInDependency("APIShared_Serp", "0.16.3")]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [ExcludeFromSavegameModSettings]
    public sealed class ExtendedDataPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ExtendedData_Serp";
        public const string PluginName = "Extended Data";
        public const string PluginVersion = "1.0.19";
        public const bool ExtendedDataModSettingsOptOut = true;
        // Advertised before LibraryLoaded so APIShared can reject older competing dialog hooks.
        public const bool UsesApiSharedSaveLoadDialog = true;

        private static ExtendedDataRuntime runtime;
        private static bool deferredCompatibilityRefreshPending;

        public ExtendedDataSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.InitializeDiagnostics(Config, Logger);
            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
            ExtendedDataLaunchOriginApi.RegisterModeProvider();
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try
            {
                CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
                if (!APIShared.Savegames.SaveLoadDialogEvents.IsAvailable)
                    throw new InvalidOperationException("APIShared load/save requester is unavailable; ExtendedData runtime was not published.");
                Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName);
                Settings = new ExtendedDataSettingsViewModel();
                APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Shared.DebugLogHelper.ForExternalCode(Logger),
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/ExtendedDataSettings.xaml",
                    logRoutineActivity: Shared.DebugLogHelper.DiagnosticsEnabled);
                string customTrailsRoot = ConfigSettings.GetUserCustomTrailsPath();
                var runtimeCandidate = new ExtendedDataRuntime(Logger, customTrailsRoot, Settings);
                // Initialize publishes permanent hooks incrementally. Keep their owner rooted even
                // if a later registration fails; failure cleanup only deactivates those hooks.
                runtime = runtimeCandidate;
                try
                {
                    runtimeCandidate.Initialize();
                }
                catch
                {
                    // Published hooks cannot be removed, including on initialization failure.
                    runtimeCandidate.Dispose();
                    throw;
                }

                runtime = runtimeCandidate;
                ScheduleDeferredCompatibilityRefresh();
                Settings.FeatureActivationChanged += runtime.SetActivation;
                Plugin.ModSettingsHubViewModel.PropertyChanged += (_, __) =>
                {
                    runtime?.RefreshModCompatibility();
                };
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtendedData/src/ExtendedDataPlugin.cs:74", "ExtendedData initialization failed: " + ex);
            }
        }

        private void OnDestroy()
        {
            // The BepInEx manager object is destroyed during startup; runtime hooks must survive it.
            Shared.DebugLogHelper.LogDebugLimited(Logger, "ExtendedData/src/ExtendedDataPlugin.cs:81", "ExtendedData OnDestroy called; keeping process-lifetime runtime active.");
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
