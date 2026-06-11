using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace SettingsMod
{
    public sealed partial class SettingsModRuntime
    {
        private void SubscribeSettingsChanges()
        {
            if (settingsPropertyChangedSubscribed)
                return;

            settings.SettingChanged += OnSettingChanged;
            settingsPropertyChangedSubscribed = true;
        }

        private void OnSettingChanged(string propertyName)
        {
            LogInfo("Settings changed:", propertyName);

            if (propertyName == nameof(SettingsModLobbyViewModel.MultiplyStartTroopsAI) ||
                propertyName == nameof(SettingsModLobbyViewModel.MultiplyStartTroopsHuman))
            {
                LogInfo("Start troop default patching is currently disabled.");
                return;
            }

            if (IsAdvancedSettingProperty(propertyName))
            {
                ApplyAdvancedSettings();
                return;
            }
        }

        private static bool IsAdvancedSettingProperty(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(SettingsModLobbyViewModel.AdvancedOptionsEnabled):
                case nameof(SettingsModLobbyViewModel.AdvancedSkirmishOptionsEnabled):
                case nameof(SettingsModLobbyViewModel.BetterHealers):
                case nameof(SettingsModLobbyViewModel.FasterPeasants):
                case nameof(SettingsModLobbyViewModel.GlobalImprovedSiegeBehaviour):
                case nameof(SettingsModLobbyViewModel.GlobalMoreAggressiveSiegeBehaviour):
                case nameof(SettingsModLobbyViewModel.ImprovedArabSwordsman):
                case nameof(SettingsModLobbyViewModel.ImprovedFletchers):
                case nameof(SettingsModLobbyViewModel.ImprovedLadderman):
                case nameof(SettingsModLobbyViewModel.ImprovedSpearman):
                case nameof(SettingsModLobbyViewModel.NerfEunuchs):
                case nameof(SettingsModLobbyViewModel.NoKnockdownWalls):
                case nameof(SettingsModLobbyViewModel.RebalancedHorseArchers):
                case nameof(SettingsModLobbyViewModel.UncappedPeasants):
                case nameof(SettingsModLobbyViewModel.EnemyHealthModifier):
                    return true;
                default:
                    return false;
            }
        }

        private void ApplyAdvancedSettings()
        {
            GamePlayerManagerAPI player = GamePlayerManagerAPI.Instance;
            player.SetAdvancedOptionsEnabled(settings.AdvancedOptionsEnabled);
            player.SetAdvancedSkirmishOptionsEnabled(settings.AdvancedSkirmishOptionsEnabled);
            player.SetBetterHealers(settings.BetterHealers);
            player.SetFasterPeasants(settings.FasterPeasants);
            player.SetGlobalImprovedSiegeBehaviour(settings.GlobalImprovedSiegeBehaviour);
            player.SetGlobalMoreAggressiveSiegeBehaviour(settings.GlobalMoreAggressiveSiegeBehaviour);
            player.SetImprovedArabSwordsman(settings.ImprovedArabSwordsman);
            player.SetImprovedFletchers(settings.ImprovedFletchers);
            player.SetImprovedLadderman(settings.ImprovedLadderman);
            player.SetImprovedSpearman(settings.ImprovedSpearman);
            player.SetNerfEunuchs(settings.NerfEunuchs);
            player.SetNoKnockdownWalls(settings.NoKnockdownWalls);
            player.SetRebalancedHorseArchers(settings.RebalancedHorseArchers);
            player.SetUncappedPeasants(settings.UncappedPeasants);

            if (Enum.TryParse(settings.EnemyHealthModifier, true, out EnemyHPModifier modifier))
                player.SetEnemyHealthModifier(modifier);
            else if (!string.IsNullOrWhiteSpace(settings.EnemyHealthModifier))
                LogInfo("Unknown EnemyHealthModifier:", settings.EnemyHealthModifier);
        }
    }
}
