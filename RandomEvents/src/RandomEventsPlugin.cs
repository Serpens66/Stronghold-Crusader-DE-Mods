using APIShared.ModSettings;
using BepInEx;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace RandomEvents
{
    [BepInDependency(ScriptExtenderGuid, "2.14.0")]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("APIShared_Serp", "0.49.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class RandomEventsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "RandomEvents_Serp";
        public const string PluginName = "Random Events";
        public const string PluginVersion = "1.0.60";

        private RandomEventsRuntime runtime;
        // Process-lived late foreign-patch check of the native detours; rooted by the mission publisher.
        private static IDisposable foreignPatchRecheck;

        public RandomEventsSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.InitializeDiagnostics(Config, Logger);
            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
            Settings = new RandomEventsSettingsViewModel();
            runtime = new RandomEventsRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try
            {
                // Register unconditionally and before settings so every peer receives the same packet ID.
                runtime.InitializeNetwork();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "RandomEvents/RandomEventsPlugin/OnCrusaderLibraryLoaded.packet-registration-failed-synchronized", $"Random Events packet registration failed; synchronized events remain unavailable: {ex}");
            }

            try
            {
                Shared.DirectLaunchSettingsNotice.Configure(Settings, PluginGuid);
                APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Shared.DebugLogHelper.ForExternalCode(Logger),
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/RandomEventsSettings.xaml",
                    logRoutineActivity: Shared.DebugLogHelper.DiagnosticsEnabled);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "RandomEvents/RandomEventsPlugin/OnCrusaderLibraryLoaded.settings-registration-failed-gameplay", $"Random Events settings registration failed; gameplay runtime stopped fail-closed: {ex}");
                return;
            }

            bool referenceHashMatches = false;
            try
            {
                referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    Logger,
                    "Random Events native integration",
                    requireCurrentVersion: false);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "RandomEvents/RandomEventsPlugin/OnCrusaderLibraryLoaded.native-version-check-failed", $"Random Events native version check failed; compatible managed features continue: {ex}");
            }

            try
            {
                // Load snapshot for the foreign-patch check of every native detour (AGENTS/native-hooks.md).
                Shared.NativePatchGuard.Bind(context, Logger);
                runtime.InitializeNative(context.ModuleHandle, context.Memory, referenceHashMatches);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "RandomEvents/RandomEventsPlugin/OnCrusaderLibraryLoaded.native-initialization-stage-failed", $"Random Events native initialization stage failed; lifecycle initialization continues: {ex}");
            }

            try
            {
                // Mods loaded after us can overwrite a published entry; compare again at every mission and editor start.
                var recheckLog = Logger; // The callback outlives this destroyed plugin component.
                if (foreignPatchRecheck == null)
                    foreignPatchRecheck = Shared.MissionEvents.Started.Subscribe(_ =>
                    {
                        try { Shared.NativePatchGuard.Recheck(); }
                        catch (Exception ex)
                        {
                            Shared.DebugLogHelper.LogErrorLimited(recheckLog, "RandomEvents/NativePatchGuard/Recheck", $"Random Events foreign-patch recheck failed: {ex}");
                        }
                    });
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "RandomEvents/RandomEventsPlugin/OnCrusaderLibraryLoaded.foreign-patch-recheck", $"Random Events foreign-patch recheck subscription failed: {ex}");
            }

            try
            {
                runtime.Initialize();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Random Events initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "RandomEvents/RandomEventsPlugin/OnCrusaderLibraryLoaded.lifecycle-initialization-failed-already", $"Random Events lifecycle initialization failed; already initialized event types remain available: {ex}");
            }
        }

    }
}
