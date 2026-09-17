using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace FormationTest
{
    [BepInDependency(ScriptExtenderGuid, "2.6.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class FormationTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "FormationTest_Serp";
        public const string PluginName = "Formation Test";
        public const string PluginVersion = "0.1.0";

        private static ManualLogSource persistentLog;
        private static FormationTestRuntime runtime;
        private static FormationPreviewOverlay overlay;
        private static bool librarySubscriptionInstalled;
        private static ConfigEntry<FormationKind> formation;
        private static ConfigEntry<int> density;
        private static ConfigEntry<bool> rearSorting;

        private void Awake()
        {
            persistentLog = Logger;
            formation = Config.Bind(
                "Formation", "Kind", FormationKind.Vanilla,
                "Current process-wide formation selection.");
            density = Config.Bind(
                "Formation", "Density", 2,
                new ConfigDescription("Formation density/spacing.",
                    new AcceptableValueRange<int>(1, 4)));
            rearSorting = Config.Bind(
                "Formation", "RearSorting", false,
                "Places melee units in front and ranged/siege/support units behind.");

            if (overlay == null)
                overlay = FormationPreviewOverlay.CreateProcessLifetimeInstance();

            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; standalonePrototype=true, networkMode=1, HUD=false.");
            if (librarySubscriptionInstalled)
                return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null)
                return;
            try
            {
                bool hashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog,
                    PluginName,
                    requireCurrentVersion: true);
                if (!hashMatches)
                    throw new InvalidOperationException(
                        "The installed CrusaderDE.dll does not match the audited formation prototype hash.");

                var candidate = new FormationTestRuntime(
                    persistentLog,
                    context,
                    formation,
                    density,
                    rearSorting);
                candidate.Initialize();
                runtime = candidate;
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} initialization failed; Vanilla movement remains active: {exception}");
            }
        }
    }
}
