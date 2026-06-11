using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using System;

namespace StartConditions
{
    public sealed partial class StartConditionsRuntime
    {
        internal static string GetLocalizedUnitName(eChimps unitType)
        {
            int translationIndex = GetUnitNameTranslationIndex(unitType);
            if (TryGetLocalizedGameText("TEXT_CHIMP_NAMES", translationIndex, out string localizedName))
                return localizedName;

            return FormatEnumName(unitType.ToString(), "CHIMP_TYPE_");
        }

        internal static string GetLocalizedGoodName(eGoods good)
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

            return FormatEnumName(good.ToString(), "STORED_");
        }

        internal static bool IsConfigurableStoredGood(eGoods good)
        {
            return good.IsGoodsyardGood() ||
                   good.IsGranaryFood() ||
                   good.IsArmouryGood() ||
                   good == eGoods.STORED_FOOD_ALE;
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
                if (!string.IsNullOrWhiteSpace(localizedName))
                    return true;
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

        private static string FormatEnumName(string enumName, string prefix)
        {
            string name = enumName.StartsWith(prefix, StringComparison.Ordinal) ? enumName.Substring(prefix.Length) : enumName;
            return name.Replace('_', ' ').ToLowerInvariant();
        }
    }
}
