using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace UnitCosts
{
    public sealed class UnitCostsRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly UnitCostsLobbyViewModel settings;
        private bool settingsChangedSubscribed;
        private bool hooksSubscribed;
        private static readonly Dictionary<eChimps, UnitGoodCosts> VanillaEuropeanGoodCosts = new Dictionary<eChimps, UnitGoodCosts>();

        public UnitCostsRuntime(ManualLogSource log, UnitCostsLobbyViewModel settings)
        {
            this.log = log;
            this.settings = settings;
        }

        public void InitializeAfterLibraryLoaded()
        {
            SubscribeHooks();
            SubscribeSettingsChanges();
            CaptureVanillaEuropeanGoodCosts();
            ApplyUnitCosts();
        }

        private void SubscribeHooks()
        {
            if (hooksSubscribed)
                return;

            MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap);

            hooksSubscribed = true;
            log.LogInfo("UnitCosts runtime hooks subscribed");
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
                ApplyUnitCosts();
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            try
            {
                ApplyUnitCosts();
            }
            catch (Exception ex)
            {
                log.LogInfo("UnitCosts OnStartMap failed: " + ex);
            }
        }

        private void ApplyUnitCosts()
        {
            CaptureVanillaEuropeanGoodCosts();

            Dictionary<eChimps, UnitCostValues> parsedCosts = settings.ParseUnitCosts();
            int changedValues = 0;
            foreach (KeyValuePair<eChimps, UnitCostValues> entry in parsedCosts)
            {
                UnitCostValues values = entry.Value;
                if (values.Gold != -1)
                {
                    GameUnitManagerAPI.Instance.SetUnitGoldCost(entry.Key, values.Gold);
                    changedValues++;
                }

                if (!IsEuropeanRecruit(entry.Key))
                    continue;

                UnitGoodCosts mergedCosts = MergeWithVanillaGoodCosts(entry.Key, values);
                GameUnitManagerAPI.Instance.SetUnitGoodCosts(entry.Key, mergedCosts);
                changedValues += CountConfiguredGoodSlots(values);
            }

            log.LogInfo("Applied unit cost values: " + changedValues);
        }

        private static void CaptureVanillaEuropeanGoodCosts()
        {
            foreach (eChimps unitType in GetEuropeanRecruitTypes())
            {
                if (!VanillaEuropeanGoodCosts.ContainsKey(unitType))
                    VanillaEuropeanGoodCosts[unitType] = GameUnitManagerAPI.Instance.GetUnitGoodCosts(unitType);
            }
        }

        private static UnitGoodCosts MergeWithVanillaGoodCosts(eChimps unitType, UnitCostValues values)
        {
            UnitGoodCosts vanilla = GetVanillaEuropeanGoodCosts(unitType);
            return new UnitGoodCosts(
                ResolveGoodSlot(values.Slot1, vanilla.cost1),
                ResolveGoodSlot(values.Slot2, vanilla.cost2),
                ResolveGoodSlot(values.Slot3, vanilla.cost3),
                ResolveGoodSlot(values.Slot4, vanilla.cost4));
        }

        private static eGoods32 ResolveGoodSlot(string key, eGoods32 vanillaValue)
        {
            string normalizedKey = UnitCostValues.NormalizeSlotKey(key);
            if (normalizedKey == UnitCostValues.UnchangedKey)
                return vanillaValue;

            if (Enum.TryParse(normalizedKey, out eGoods good))
                return good.To32();

            return vanillaValue;
        }

        private static UnitGoodCosts GetVanillaEuropeanGoodCosts(eChimps unitType)
        {
            if (VanillaEuropeanGoodCosts.TryGetValue(unitType, out UnitGoodCosts costs))
                return costs;

            if (!IsEuropeanRecruit(unitType))
                return new UnitGoodCosts(
                    eGoods.STORED_NULL.To32(),
                    eGoods.STORED_NULL.To32(),
                    eGoods.STORED_NULL.To32(),
                    eGoods.STORED_NULL.To32());

            costs = GameUnitManagerAPI.Instance.GetUnitGoodCosts(unitType);
            VanillaEuropeanGoodCosts[unitType] = costs;
            return costs;
        }

        private static int CountConfiguredGoodSlots(UnitCostValues values)
        {
            int count = 0;
            if (values.Slot1 != UnitCostValues.UnchangedKey) count++;
            if (values.Slot2 != UnitCostValues.UnchangedKey) count++;
            if (values.Slot3 != UnitCostValues.UnchangedKey) count++;
            if (values.Slot4 != UnitCostValues.UnchangedKey) count++;
            return count;
        }

        internal static bool IsEuropeanRecruit(eChimps unitType)
        {
            return unitType >= eChimps.CHIMP_TYPE_ARCHER &&
                unitType <= eChimps.CHIMP_TYPE_KNIGHT;
        }

        private static IEnumerable<eChimps> GetEuropeanRecruitTypes()
        {
            yield return eChimps.CHIMP_TYPE_ARCHER;
            yield return eChimps.CHIMP_TYPE_XBOWMAN;
            yield return eChimps.CHIMP_TYPE_SPEARMAN;
            yield return eChimps.CHIMP_TYPE_PIKEMAN;
            yield return eChimps.CHIMP_TYPE_MACEMAN;
            yield return eChimps.CHIMP_TYPE_SWORDSMAN;
            yield return eChimps.CHIMP_TYPE_KNIGHT;
        }

        internal static string GetUnitSettingsTooltip(eChimps unitType)
        {
            StringBuilder builder = new StringBuilder(unitType.ToString());
            if (!IsEuropeanRecruit(unitType))
                return builder.ToString();

            try
            {
                UnitGoodCosts costs = GetVanillaEuropeanGoodCosts(unitType);
                AppendVanillaGoodCost(builder, 1, costs.cost1);
                AppendVanillaGoodCost(builder, 2, costs.cost2);
                AppendVanillaGoodCost(builder, 3, costs.cost3);
                AppendVanillaGoodCost(builder, 4, costs.cost4);
            }
            catch
            {
                return builder.ToString();
            }

            return builder.ToString();
        }

        private static void AppendVanillaGoodCost(StringBuilder builder, int slot, eGoods32 good32)
        {
            eGoods good = good32.To16();
            if (good == eGoods.STORED_NULL)
                return;

            builder.AppendLine();
            builder.Append("vanilla slot ");
            builder.Append(slot);
            builder.Append(": ");
            builder.Append(good);
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
