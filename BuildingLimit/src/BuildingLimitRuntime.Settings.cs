using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace BuildingLimit
{
    public sealed partial class BuildingLimitRuntime
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

            if (propertyName == nameof(BuildingLimitLobbyViewModel.BuildingLimits))
                ApplyBuildingLimits();
        }
    }
}
