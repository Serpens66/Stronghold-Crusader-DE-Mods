using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace FormationTest
{
    [BepInDependency(ScriptExtenderGuid, "2.8.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class FormationTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "FormationTest_Serp";
        public const string PluginName = "Formation Test";
        public const string PluginVersion = "0.1.0";

        private static ManualLogSource persistentLog;
        private static FormationTestRuntime runtime;
        private static FormationMenuViewModel formationMenu;
        private static bool librarySubscriptionInstalled;
        private static ConfigEntry<FormationKind> formation;
        private static ConfigEntry<int> density;
        private static ConfigEntry<bool> legacyRearSorting;
        private static ConfigEntry<RangedPlacementMode> placementMode;
        private static ConfigEntry<bool> showRoleMarkers;
        private static ConfigEntry<int> defaultsRevision;

        private void Awake()
        {
            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
            persistentLog = Logger;
            formation = Config.Bind(
                "Formation", "Kind", FormationKind.Block,
                "Current process-wide formation selection.");
            density = Config.Bind(
                "Formation", "Density", 2,
                new ConfigDescription("Formation density/spacing.",
                    new AcceptableValueRange<int>(1, 4)));
            legacyRearSorting = Config.Bind(
                "Formation", "RearSorting", false,
                "Legacy migration value; use RangedPlacement instead.");
            placementMode = Config.Bind(
                "Formation", "RangedPlacement", RangedPlacementMode.Off,
                "Placement of ranged, siege, healer, shield, and support units.");
            showRoleMarkers = Config.Bind(
                "Formation", "ShowRoleMarkers", true,
                "Show colored role markers above the native green formation preview.");
            defaultsRevision = Config.Bind(
                "Formation", "DefaultsRevision", 0,
                "Internal prototype defaults migration revision.");
            FormationDefaultsMigration migration = FormationDefaultsMigration.Resolve(
                formation.Value,
                defaultsRevision.Value);
            if (migration.RevisionChanged)
            {
                formation.Value = migration.Kind;
                defaultsRevision.Value = migration.Revision;
                Shared.DebugLogHelper.LogInfo(
                    persistentLog,
                    $"Formation defaults migrated: revision={migration.Revision}, " +
                    $"kind={migration.Kind}, kindChanged={migration.KindChanged}.");
            }

            PlacementDefaultsMigration placementMigration =
                PlacementDefaultsMigration.Resolve(
                    defaultsRevision.Value,
                    legacyRearSorting.Value,
                    placementMode.Value);
            if (placementMigration.Changed)
            {
                placementMode.Value = placementMigration.Mode;
                defaultsRevision.Value = placementMigration.Revision;
                Shared.DebugLogHelper.LogInfo(
                    persistentLog,
                    $"Formation placement migrated: revision={placementMigration.Revision}, " +
                    $"mode={placementMigration.Mode}.");
            }
            if (migration.RevisionChanged || placementMigration.Changed)
                Config.Save();

            formationMenu = new FormationMenuViewModel(
                persistentLog, Config, formation, density, placementMode,
                showRoleMarkers);
            FormationPreviewOverlay.Initialize(
                persistentLog, showRoleMarkers.Value);

            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; standalonePrototype=true, networkMode=1, HUD=true.");
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
                try
                {
                    GameXAMLManagerAPI.Instance.RegisterBinding(
                        "FormationTestButtonHost", formationMenu);
                    GameXAMLManagerAPI.Instance.RegisterBinding(
                        "FormationTestMenuHost", formationMenu);
                    GameXAMLManagerAPI.Instance.RegisterBinding(
                        "FormationTestRolloverHost", formationMenu);
                }
                catch (Exception uiException)
                {
                    Shared.DebugLogHelper.LogWarning(
                        persistentLog,
                        $"Formation menu unavailable; movement runtime remains active: {uiException.Message}");
                }

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
                    placementMode,
                    formationMenu);
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
