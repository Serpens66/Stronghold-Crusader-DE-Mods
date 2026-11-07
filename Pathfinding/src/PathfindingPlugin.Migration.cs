using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;

namespace Pathfinding
{
    // One-time import of the settings these features had in BugfixesAndQoL. Only reads the old file;
    // presence in the new settings wins, and a later explicit edit always wins (LegacyBfqolSettings).
    public sealed partial class PathfindingPlugin
    {
        private const string PreviousGuid = "BugfixesAndQoL_Serp";
        private LegacyBfqolSettings legacySettings;
        private bool settingsRegistered, migratingSettings, migrationErrorLogged;

        private Dictionary<string, bool> CurrentLegacyDefaults()
        {
            var values = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (string key in LegacyBfqolSettings.Keys) values.Add(key, true);
            return values;
        }

        private void PrepareLegacySettings()
        {
            string oldDirectory = Chainloader.PluginInfos.TryGetValue(PreviousGuid, out PluginInfo old)
                ? Path.GetDirectoryName(old.Location) : Path.Combine(Paths.PluginPath, PreviousGuid);
            var candidate = new LegacyBfqolSettings(LegacyBfqolSettings.ResolveDocuments(),
                Path.Combine(Path.GetDirectoryName(Info.Location), "LobbyModSettings", PluginGuid + ".msgpack"),
                Path.Combine(oldDirectory, "LobbyModSettings", PreviousGuid + ".msgpack"));
            legacySettings = candidate;
            Settings.PropertyChanged += OnMigrationSettingsChanged;
            candidate.Prepare(CurrentLegacyDefaults());
            // The plugin is statically rooted; settings events survive the startup component cleanup.
        }

        private void OnMigrationSettingsChanged(object sender, PropertyChangedEventArgs args)
        {
            if (!settingsRegistered || migratingSettings) return;
            try
            {
                if (!Settings.IsRestoringSnapshot && Settings.CanEditHostSettings && !PathfindingRuntime.HasMission &&
                    !Settings.IsMissionPresetSelected && Array.IndexOf(LegacyBfqolSettings.Keys, args.PropertyName) >= 0)
                    legacySettings?.PreserveEdit(args.PropertyName);
                if (args.PropertyName == nameof(Settings.CanEditHostSettings)) TryMigrateLegacySettings();
            }
            catch (Exception error) { LogMigrationError(error); }
        }

        private void TryMigrateLegacySettings()
        {
            if (legacySettings == null || migratingSettings || PathfindingRuntime.HasMission || Settings.IsMissionPresetSelected) return;
            migratingSettings = true;
            try
            {
                legacySettings.Apply(_ => Settings.CanEditHostSettings, Settings.System_CreateCurrentWorkingSnapshot,
                    Settings.System_ApplyWorkingSnapshot);
            }
            catch (Exception error) { LogMigrationError(error); }
            finally { migratingSettings = false; }
        }

        private void LogMigrationError(Exception error)
        {
            if (migrationErrorLogged) return;
            migrationErrorLogged = true;
            Shared.DebugLogHelper.LogError(Logger, "PATHFINDING_LEGACY_SETTINGS_DEFERRED: " + error);
        }

        // The remembered formation choices were BepInEx values of BugfixesAndQoL. Taken over once, before
        // the formation feature binds them; an existing own section always wins.
        private void ImportLegacyFormationConfig()
        {
            try
            {
                string own = Config.ConfigFilePath;
                string text = File.Exists(own) ? File.ReadAllText(own) : string.Empty;
                if (text.Contains("[Formation]")) return;
                string old = Path.Combine(Path.GetDirectoryName(own), PreviousGuid + ".cfg");
                if (!File.Exists(old)) return;
                var lines = new List<string>();
                bool inside = false;
                foreach (string line in File.ReadAllLines(old))
                {
                    if (line.StartsWith("[", StringComparison.Ordinal)) inside = line.Trim() == "[Formation]";
                    if (inside) lines.Add(line);
                }
                if (lines.Count == 0) return;
                File.AppendAllText(own, Environment.NewLine + string.Join(Environment.NewLine, lines) + Environment.NewLine);
                Config.Reload();
            }
            catch (Exception error) { LogMigrationError(error); }
        }
    }
}
