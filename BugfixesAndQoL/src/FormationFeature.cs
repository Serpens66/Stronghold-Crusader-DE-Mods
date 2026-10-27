using BugfixesAndQoL.UnitCommands;
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
        internal static bool RuntimeAvailable => runtime != null && runtime.LocalInputEnabled;
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
            ConfigureAspectMemory(config, existing, out var rememberAspect, out var rememberedAspects);
            kind = config.Bind("Formation", "Kind",
                FormationSelectionMigration.ResolveKind(existing, legacy),
                "Local formation selection; sent with each synchronized move command.");
            density = config.Bind("Formation", "Density", FormationSelectionMigration.ResolveDensity(existing, legacy),
                new ConfigDescription("Local formation spacing.", new AcceptableValueRange<int>(1, 4)));
            placement = config.Bind("Formation", "RangedPlacement",
                FormationSelectionMigration.ResolveEnum(existing, legacy, "RangedPlacement", RangedPlacementMode.Off),
                "Local placement of ranged, siege, healer, shield and support units.");
            var roles = config.Bind("Formation", "ShowRoleMarkers",
                FormationSelectionMigration.ResolveRoleMarkers(existing, legacy), "Show colored roles over the green formation preview.");
            FormationPreviewOverlay.Initialize(log, roles.Value);
            menu = new FormationMenuViewModel(log, config, kind, density, placement, roles, rememberAspect, rememberedAspects);
            menu.Enabled = () => runtime != null && runtime.LocalInputEnabled;
            RegisterPresentationBindings();
            FormationHudButton.Configure(menu, log);
            config.Save();
            settings.PropertyChanged += (_, __) => {
                if (!settings.EnableMod || !settings.EnableMoveFormationEnhancements)
                { runtime?.ResetTransientState(); menu.CloseMenu(); FormationPreviewOverlay.Clear(); }
            };
        }

        private static void ConfigureAspectMemory(ConfigFile config,
            System.Collections.Generic.Dictionary<string, string> existing,
            out ConfigEntry<bool> enabled, out ConfigEntry<string>[] aspects)
        {
            bool autoSave = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                bool initial = true;
                if (!existing.ContainsKey("RememberAspectRatio") && existing.TryGetValue("RememberRows", out string old) &&
                    bool.TryParse(old, out bool oldEnabled)) initial = oldEnabled;
                enabled = config.Bind("Formation", "RememberAspectRatio", initial,
                    "Remember wheel-selected width/depth ratios separately for each arrangement across restarts.");
                aspects = new ConfigEntry<string>[6];
                foreach (FormationKind kind in new[] { FormationKind.Block, FormationKind.Line, FormationKind.Column, FormationKind.Wedge })
                {
                    var entry = config.Bind("Formation", "Remembered" + kind + "AspectRatio", "auto",
                        "Local width:depth ratio; auto keeps this arrangement's automatic proportions. Each positive component is at most 10000.");
                    FormationAspectRatio.TryParse(entry.Value, out FormationAspectRatio aspect);
                    entry.Value = aspect.ToString();
                    aspects[(int)kind] = entry;
                    string key = "Remembered" + kind + "Rows";
                    if (existing.ContainsKey(key))
                    {
                        // Bind consumes BepInEx's orphaned entry; Remove then retires
                        // only that known key. Its old selection size was not stored.
                        config.Bind("Formation", key, 0, "Obsolete absolute row preference.");
                        config.Remove(new ConfigDefinition("Formation", key));
                    }
                }
                if (existing.ContainsKey("RememberRows"))
                {
                    config.Bind("Formation", "RememberRows", true, "Obsolete memory switch.");
                    config.Remove(new ConfigDefinition("Formation", "RememberRows"));
                }
            }
            finally { config.SaveOnConfigSet = autoSave; }
        }

        private static void RegisterPresentationBindings()
        {
            foreach (string host in new[] {
                "BugfixesAndQoLFormationMenuHost",
                "BugfixesAndQoLFormationRolloverHost" })
            {
                try { GameXAMLManagerAPI.Instance.RegisterBinding(host, menu); }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/FormationFeature.cs:71", "FORMATION_BINDING_FAILED: host=" + host + "; " + exception);
                }
            }
        }

        private static string ReadConfig(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : string.Empty; }
            catch (IOException ex) { Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/FormationFeature.cs:80", "Formation migration source unavailable: " + ex.Message); return string.Empty; }
        }

        internal static void Initialize(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                if (!Shared.DebugLogHelper.IsCurrentNativeLibraryVersion())
                    throw new InvalidOperationException("Unaudited native formation layout.");
                var commands = UnitCommandPathAPI.Runtime;
                var markers = LargeMoveTargetMarkerRuntime.Renderer;
                if (commands == null || markers == null)
                    throw new InvalidOperationException("Command runtime or local marker renderer unavailable.");
                // Root the candidate before publishing any hook or subscription. Initialization
                // failure leaves the rooted candidate logically inactive until process exit.
                runtime = new FormationRuntime(log, context, kind, density, placement, menu, settings, commands, markers);
                runtime.Initialize();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/FormationFeature.cs:101", "Formation initialization failed; Vanilla remains active: " + ex);
            }
            finally
            {
                Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => menu.RefreshAvailability());
            }
        }
    }
}
