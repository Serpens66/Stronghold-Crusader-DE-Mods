using APIShared.UnitCommands;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.IO;

namespace BugfixesAndQoL
{
    internal static class FormationFeature
    {
        private static ManualLogSource log;
        private static BugfixesAndQoLViewModel settings;
        private static FormationMenuViewModel menu;
        private static FormationRuntime runtime;
        internal static bool RuntimeAvailable => runtime != null && runtime.Enabled;
        private static ConfigEntry<FormationKind> kind;
        private static ConfigEntry<int> density;
        private static ConfigEntry<RangedPlacementMode> placement;

        internal static void Configure(ManualLogSource logger, ConfigFile config, BugfixesAndQoLViewModel options)
        {
            log = logger;
            settings = options;
            var existing = FormationSelectionMigration.ReadFormationSection(ReadConfig(config.ConfigFilePath));
            var legacy = FormationSelectionMigration.ReadFormationSection(
                ReadConfig(Path.Combine(Paths.ConfigPath, "FormationTest_Serp.cfg")));
            kind = config.Bind("Formation", "Kind",
                FormationSelectionMigration.ResolveEnum(existing, legacy, "Kind", FormationKind.Block),
                "Local formation selection; sent with each synchronized move command.");
            density = config.Bind("Formation", "Density", FormationSelectionMigration.ResolveDensity(existing, legacy),
                new ConfigDescription("Local formation spacing.", new AcceptableValueRange<int>(1, 4)));
            placement = config.Bind("Formation", "RangedPlacement",
                FormationSelectionMigration.ResolveEnum(existing, legacy, "RangedPlacement", RangedPlacementMode.Off),
                "Local placement of ranged, siege, healer, shield and support units.");
            var roles = config.Bind("Formation", "ShowRoleMarkers",
                FormationSelectionMigration.ResolveRoleMarkers(existing, legacy), "Show colored roles over the green formation preview.");
            FormationPreviewOverlay.Initialize(log, roles.Value);
            menu = new FormationMenuViewModel(log, config, kind, density, placement, roles);
            menu.Enabled = () => runtime != null && runtime.Enabled;
            config.Save();
            settings.PropertyChanged += (_, __) => {
                if (!settings.EnableMod || !settings.EnableMoveFormationEnhancements)
                { runtime?.ResetTransientState(); menu.CloseMenu(); FormationPreviewOverlay.Clear(); }
            };
        }

        private static string ReadConfig(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : string.Empty; }
            catch (IOException ex) { Shared.DebugLogHelper.LogWarning(log, "Formation migration source unavailable: " + ex.Message); return string.Empty; }
        }

        internal static void Initialize(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                if (!Shared.DebugLogHelper.IsCurrentNativeLibraryVersion())
                    throw new InvalidOperationException("Unaudited native formation layout.");
                var commands = UnitCommandPathAPI.Runtime;
                var markers = UnitCommandPathAPI.MoveMarkers;
                if (commands == null || markers == null)
                    throw new InvalidOperationException("Shared command or marker capability unavailable.");
                // Root the candidate before publishing any hook or subscription. Initialization
                // failure leaves the rooted candidate logically inactive until process exit.
                runtime = new FormationRuntime(log, context, kind, density, placement, menu, settings, commands, markers);
                runtime.Initialize();
                try
                {
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLFormationButtonHost", menu);
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLFormationMenuHost", menu);
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLFormationRolloverHost", menu);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogWarning(log, "Formation menu unavailable; movement runtime remains active: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "Formation initialization failed; Vanilla remains active: " + ex);
            }
        }
    }
}
