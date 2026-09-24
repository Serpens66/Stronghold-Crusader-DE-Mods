using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace WaterboyTargetReservationTest
{
    [BepInDependency(ScriptExtenderGuid, "2.7.2")]
    [BepInDependency("APIShared_Serp", "0.4.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class WaterboyTargetReservationPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "WaterboyTargetReservationTest_Serp";
        private const string PluginName = "Waterboy Target Reservation Test";
        private const string PluginVersion = "0.1.0";

        private static ManualLogSource persistentLog;
        private static WaterboyTargetReservationRuntime runtime;
        private static WaterboySettings settings;
        private static WaterboyTargetReservationPlugin plugin;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            plugin = this;
            persistentLog = Logger;
            Shared.DebugLogHelper.LogWarning(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; testMod=true, gameplaySynchronized=true, settings=true.");

            if (librarySubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null)
                return;

            try
            {
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog,
                    PluginName,
                    requireCurrentVersion: true);
                if (!referenceHashMatches)
                {
                    Shared.DebugLogHelper.LogWarning(
                        persistentLog,
                        $"{PluginName} remains inactive because the canonical CrusaderDE.dll hash did not match; Vanilla remains active.");
                    return;
                }

                settings = new WaterboySettings();
                Shared.LobbyModSettingsPresetRegistration.Register(
                    plugin,
                    persistentLog,
                    PluginGuid,
                    settings,
                    "ScriptExtenderUI/WaterboyTargetReservationTestSettings.xaml");
                runtime = new WaterboyTargetReservationRuntime(
                    persistentLog, settings, context, referenceHashMatches);
                try
                {
                    GameXAMLManagerAPI.Instance.RegisterBinding(
                        "WaterboyTargetReservationTestModeButtonHost",
                        runtime.ButtonViewModel);
                }
                catch (Exception exception)
                {
                    runtime.DisableButton("XAML binding registration failed", exception);
                }
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not initialize; Vanilla remains active: {exception}");
            }
        }
    }
}
