using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime
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
            LogDebug("Settings changed:", propertyName);

            if (propertyName == nameof(UnitLimitLobbyViewModel.EnableMod))
            {
                if (settings.EnableMod)
                {
                    SubscribeHooks();
                    ApplyUnitLimits(false);
                    ApplyCampfirePeasantsLimit();
                }
                else
                {
                    RestoreCampfirePeasantsCap();
                    UnsubscribeHooks();
                }

                return;
            }

            if (!settings.EnableMod)
                return;

            if (propertyName == nameof(UnitLimitLobbyViewModel.UnitLimits))
                ApplyUnitLimits();

            if (propertyName == nameof(UnitLimitLobbyViewModel.CampfirePeasantsLimit))
                ApplyCampfirePeasantsLimit();
        }
    }
}
