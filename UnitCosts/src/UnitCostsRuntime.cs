using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace UnitCosts
{
    public sealed class UnitCostsRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly UnitCostsLobbyViewModel settings;
        private bool settingsChangedSubscribed;

        public UnitCostsRuntime(ManualLogSource log, UnitCostsLobbyViewModel settings)
        {
            this.log = log;
            this.settings = settings;
        }

        public void InitializeAfterLibraryLoaded()
        {
            SubscribeSettingsChanges();
            LogConfiguredUnitCosts();
        }

        public void Dispose()
        {
            if (settingsChangedSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsChangedSubscribed = false;
            }
        }

        private void SubscribeSettingsChanges()
        {
            if (settingsChangedSubscribed)
                return;

            settings.SettingChanged += OnSettingChanged;
            settingsChangedSubscribed = true;
        }

        private void OnSettingChanged(string propertyName)
        {
            log.LogInfo("UnitCosts settings changed: " + propertyName);

            if (propertyName == nameof(UnitCostsLobbyViewModel.UnitCosts))
                LogConfiguredUnitCosts();
        }

        private void LogConfiguredUnitCosts()
        {
            Dictionary<eChimps, UnitCostValues> parsedCosts = settings.ParseUnitCosts();
            int configuredMaterials = 0;
            foreach (UnitCostValues values in parsedCosts.Values)
            {
                configuredMaterials += CountConfigured(values.Bows);
                configuredMaterials += CountConfigured(values.Crossbows);
                configuredMaterials += CountConfigured(values.Spears);
                configuredMaterials += CountConfigured(values.Pikes);
                configuredMaterials += CountConfigured(values.Maces);
                configuredMaterials += CountConfigured(values.Swords);
                configuredMaterials += CountConfigured(values.LeatherArmour);
                configuredMaterials += CountConfigured(values.MetalArmour);
                configuredMaterials += CountConfigured(values.Gold);
            }

            log.LogInfo("Configured unit cost materials: " + configuredMaterials);
        }

        private static int CountConfigured(int value)
        {
            return value != -1 ? 1 : 0;
        }

        internal static string GetLocalizedUnitName(eChimps unitType)
        {
            int translationIndex = GetUnitNameTranslationIndex(unitType);
            if (TryGetLocalizedGameText("TEXT_CHIMP_NAMES", translationIndex, out string localizedName))
                return localizedName;

            string name = unitType.ToString();
            const string prefix = "CHIMP_TYPE_";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                name = name.Substring(prefix.Length);
            return name.Replace('_', ' ').ToLowerInvariant();
        }

        internal static string GetLocalizedGoodName(eGoods good, string fallback)
        {
            int index = (int)good;
            string[] sections =
            {
                "TEXT_GOODS_NAMES",
                "TEXT_GOOD_NAMES",
                "TEXT_GOODS",
                "TEXT_GOOD",
            };

            foreach (string section in sections)
            {
                if (TryGetLocalizedGameText(section, index, out string localizedName))
                    return localizedName;
            }

            string[] keyPrefixes =
            {
                "TEXT_GOODS_NAMES_",
                "TEXT_GOOD_NAMES_",
                "TEXT_GOODS_",
                "TEXT_GOOD_",
            };

            foreach (string keyPrefix in keyPrefixes)
            {
                if (TryGetLocalizedGameTextKey(keyPrefix + (index + 1).ToString("D3"), out string localizedName))
                    return localizedName;
            }

            return fallback;
        }

        private static int GetUnitNameTranslationIndex(eChimps unitType)
        {
            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_TUNNELER: return 5;
                case eChimps.CHIMP_TYPE_LADDERMAN: return 29;
                case eChimps.CHIMP_TYPE_ENGINEER: return 30;
                case eChimps.CHIMP_TYPE_MONK: return 37;
                default:
                    return (int)unitType;
            }
        }

        private static bool TryGetLocalizedGameText(string sectionName, int index, out string localizedName)
        {
            localizedName = null;
            if (string.IsNullOrEmpty(sectionName) || index < 0)
                return false;

            try
            {
                localizedName = GameTranslateAPI.Instance.GetLookUpTextEx(sectionName, index);
                if (!string.IsNullOrWhiteSpace(localizedName))
                    return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GameTranslateAPI lookup failed: " + sectionName + " " + index + " " + ex.Message);
            }

            return TryGetLocalizedGameTextKey(sectionName + "_" + (index + 1).ToString("D3"), out localizedName);
        }

        private static bool TryGetLocalizedGameTextKey(string translationKey, out string localizedName)
        {
            localizedName = null;
            if (string.IsNullOrEmpty(translationKey))
                return false;

            try
            {
                localizedName = GameTranslateAPI.Instance.GetLookUpText(translationKey);
                if (!string.IsNullOrWhiteSpace(localizedName) &&
                    !string.Equals(localizedName, translationKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GameTranslateAPI lookup failed: " + translationKey + " " + ex.Message);
            }

            if (CrusaderDE.Translate.Instance?.GameTexts != null &&
                CrusaderDE.Translate.Instance.GameTexts.TryGetValue(translationKey, out localizedName) &&
                !string.IsNullOrWhiteSpace(localizedName))
            {
                return true;
            }

            localizedName = null;
            return false;
        }
    }
}
