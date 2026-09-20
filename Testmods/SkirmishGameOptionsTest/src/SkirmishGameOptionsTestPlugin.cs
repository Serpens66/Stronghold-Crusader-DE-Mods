using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using ExtraFeatures;
using SHCDESE.API.LowLevel;
using System;

namespace SkirmishGameOptionsTest
{
    [BepInDependency("000shcdese", "2.7.1")]
    [BepInDependency("ExtraFeatures_Serp", "1.0.98")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SkirmishGameOptionsTestPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "SkirmishGameOptionsTest_Serp";
        public const string PluginName = "Skirmish Game Options Test";
        public const string PluginVersion = "0.1.0";

        private static ManualLogSource persistentLog;
        private static SkirmishGameOptionsRuntime runtime;
        private static NoDogsNativePatch noDogsPatch;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            if (!Chainloader.PluginInfos.TryGetValue("ExtraFeatures_Serp", out PluginInfo pluginInfo) ||
                !(pluginInfo.Instance is ExtraFeaturesPlugin extraFeaturesPlugin) ||
                extraFeaturesPlugin.Settings == null)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    "SKIRMISH_GAME_OPTIONS_TEST_DISABLED: ExtraFeatures 1.0.98 settings are unavailable.");
                return;
            }

            runtime = new SkirmishGameOptionsRuntime(
                persistentLog,
                extraFeaturesPlugin.Settings);
            runtime.Initialize();

            if (!librarySubscriptionInstalled)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
                librarySubscriptionInstalled = true;
            }

            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"SKIRMISH_GAME_OPTIONS_TEST_LOADED: version={PluginVersion}, isolatedTestMod=true.");
        }

        private static void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (context == null || noDogsPatch != null)
                return;

            try
            {
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog,
                    PluginName,
                    requireCurrentVersion: true);
                noDogsPatch = new NoDogsNativePatch(
                    persistentLog,
                    context.ModuleHandle,
                    context.Region,
                    context.Memory,
                    referenceHashMatches);
                runtime?.SetNoDogsNativeAvailable(true);
            }
            catch (Exception exception)
            {
                runtime?.SetNoDogsNativeAvailable(false);
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    "SKIRMISH_GAME_OPTIONS_TEST_NO_DOGS_PATCH_FAILED: " + exception);
            }
        }
    }
}
