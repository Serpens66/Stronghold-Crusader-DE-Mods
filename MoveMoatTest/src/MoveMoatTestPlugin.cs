using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace MoveMoatTest
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MoveMoatTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "MoveMoatTest_Serp";
        private const string PluginName = "Move Moat Test";
        private const string PluginVersion = "1.0.0";

        // BepInEx destroys this component during startup. Static ownership keeps the
        // native detours and their delegates rooted for the complete process.
        internal static readonly MoveMoatSettings Settings = new MoveMoatSettings();
        private static ManualLogSource persistentLog;
        private static MoveMoatPathTest feature;
        private static bool librarySubscriptionInstalled;

        // 1 means MoveMoat owns the shared hooks, 0 lets the caller install its standalone hooks.
        public static int RegisterImprovedMoatFillingProvider(
            string ownerGuid,
            Func<bool> enabledProvider) =>
            feature?.RegisterImprovedMoatFillingProvider(ownerGuid, enabledProvider) ?? 0;

        private void Awake()
        {
            persistentLog = Logger;
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; friendly/allied completed-moat movement is enabled only for Vanilla moat-digging unit types.");

            if (librarySubscriptionInstalled)
                return;

            Shared.LobbyModSettingsPresetRegistration.Register(this, Logger, PluginGuid, Settings,
                "ScriptExtenderUI/MoveMoatSettings.xaml");
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void OnCrusaderLibraryLoaded(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory)
        {
            if (feature != null)
                return;

            try
            {
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog,
                    PluginName,
                    requireCurrentVersion: true);
                if (!referenceHashMatches)
                    return;

                feature = new MoveMoatPathTest(
                    persistentLog,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    referenceHashMatches);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not install the central moat-path test; Vanilla behavior remains active: {ex}");
            }
        }
    }
}
