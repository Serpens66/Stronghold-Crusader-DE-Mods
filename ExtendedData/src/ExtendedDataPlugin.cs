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
    [BepInDependency("APIShared_Serp", "0.27.0")]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // ExtendedData's own settings are never mission content: APIShared does not capture or restore
    // them in savegames, and Map/Trail capture (TrailMissionSettingsCoordinator.FindCompatibleViewModels)
    // uses the same participant set, so Maps and Trails never contain them either. They are therefore
    // already mission-independent in practice; [MissionIndependent] is deliberately not used because it
    // would also move host values into personal storage (APIShared/docs/MOD_SETTINGS.md). Mission
    // content in saves (Lord data, Coop/launch origin) uses ExtendedData's own ModSaveData handlers.
    [ExcludeFromSavegameModSettings]
    public sealed class ExtendedDataPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ExtendedData_Serp";
        public const string PluginName = "Extended Data";
        public const string PluginVersion = "1.0.24";
        public const bool ExtendedDataModSettingsOptOut = true;
        // Advertised before LibraryLoaded so APIShared can reject older competing dialog hooks.
        public const bool UsesApiSharedSaveLoadDialog = true;

        private static ExtendedDataRuntime runtime;
        private static APIShared.Presentation.IUiFrameWorkRegistration compatibilityRefresh;

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
                    // Routine preset activity is development diagnostics (build.bat /devdiag only).
#if SERP_DEVDIAG
                    logRoutineActivity: true);
#else
                    logRoutineActivity: false);
#endif
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
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtendedData/ExtendedDataPlugin/OnLibraryLoaded", "ExtendedData initialization failed: " + ex);
            }
        }

        private void OnDestroy()
        {
            // The BepInEx manager object is destroyed during startup; runtime hooks must survive it.
            Shared.DevDiag.Log(() => "ExtendedData OnDestroy called; keeping process-lifetime runtime active.");
        }

        private static void ScheduleDeferredCompatibilityRefresh()
        {
            if (compatibilityRefresh == null && !APIShared.ApiShared.ForMod(PluginGuid).Ui.TryRegisterBeforeRender(
                "compatibility-refresh", RefreshCompatibilityAfterRegistrations, out compatibilityRefresh, out string reason, 100))
                throw new InvalidOperationException("Deferred compatibility refresh unavailable: " + reason);
            compatibilityRefresh.Request();
        }

        private static void RefreshCompatibilityAfterRegistrations()
        {
            runtime?.RefreshModCompatibility();
        }

    }
}
