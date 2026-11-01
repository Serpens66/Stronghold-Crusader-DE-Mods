using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;

namespace Spectator
{
    public sealed partial class SpectatorPlugin
    {
        private LegacySpectatorSettings legacySettings;
        private bool settingsRegistered, migratingSettings, migrationErrorLogged;

        private void PrepareLegacySettings()
        {
            const string previousGuid = "BugfixesAndQoL_Serp";
            string oldDirectory = Chainloader.PluginInfos.TryGetValue(previousGuid, out PluginInfo old)
                ? Path.GetDirectoryName(old.Location) : Path.Combine(Paths.PluginPath, previousGuid);
            var candidate = new LegacySpectatorSettings(LegacySpectatorSettings.ResolveDocuments(),
                Path.Combine(Path.GetDirectoryName(Info.Location), "LobbyModSettings", PluginGuid + ".msgpack"),
                Path.Combine(oldDirectory, "LobbyModSettings", previousGuid + ".msgpack"));
            legacySettings = candidate;
            Settings.PropertyChanged += OnMigrationSettingsChanged;
            candidate.Prepare(new Dictionary<string, bool> {
                { nameof(Settings.EnableSpectatorPerspective), Settings.EnableSpectatorPerspective },
                { nameof(Settings.EnableEliminatedPlayersBecomeSpectators), Settings.EnableEliminatedPlayersBecomeSpectators }
            });
            // The plugin is statically rooted; settings events survive the startup component cleanup.
        }

        private void OnMigrationSettingsChanged(object sender, PropertyChangedEventArgs args)
        {
            if (!settingsRegistered || migratingSettings) return;
            try
            {
                if (!Settings.IsRestoringSnapshot && CanMigrateSetting(args.PropertyName) && !SpectatorRuntime.HasMission &&
                    !Settings.IsMissionPresetSelected && Array.IndexOf(LegacySpectatorSettings.Keys, args.PropertyName) >= 0)
                    legacySettings?.PreserveEdit(args.PropertyName);
                if (args.PropertyName == nameof(Settings.CanEditHostSettings) || args.PropertyName == nameof(Settings.CanEditClientSettings)) TryMigrateLegacySettings();
            }
            catch (Exception error) { LogMigrationError(error); }
        }

        private bool CanMigrateSetting(string key) => key == nameof(Settings.EnableSpectatorPerspective)
            ? Settings.CanEditClientSettings : Settings.CanEditHostSettings;

        private void TryMigrateLegacySettings()
        {
            if (legacySettings == null || migratingSettings || SpectatorRuntime.HasMission || Settings.IsMissionPresetSelected) return;
            migratingSettings = true;
            try
            {
                legacySettings.Prepare(new Dictionary<string, bool> {
                    { nameof(Settings.EnableSpectatorPerspective), true },
                    { nameof(Settings.EnableEliminatedPlayersBecomeSpectators), true }
                });
                legacySettings.Apply(CanMigrateSetting, Settings.System_CreateCurrentWorkingSnapshot,
                    Settings.System_ApplyWorkingSnapshot);
            }
            catch (Exception error) { LogMigrationError(error); }
            finally { migratingSettings = false; }
        }

        private void LogMigrationError(Exception error)
        {
            if (migrationErrorLogged) return;
            migrationErrorLogged = true;
            Shared.DebugLogHelper.LogError(Logger, "SPECTATOR_LEGACY_SETTINGS_DEFERRED: " + error);
        }
    }
}
