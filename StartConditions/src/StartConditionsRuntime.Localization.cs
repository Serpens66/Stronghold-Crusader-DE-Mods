using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using System;

namespace StartConditions
{
    public sealed partial class StartConditionsRuntime
    {
        private const string GoodsTextSection = "TEXT_GOODS";

        internal static string GetLocalizedUnitName(eChimps unitType)
        {
            int translationIndex = GetUnitNameTranslationIndex(unitType);
            if (TryGetLocalizedGameText("TEXT_CHIMP_NAMES", translationIndex, out string localizedName))
                return localizedName;

            return FormatEnumName(unitType.ToString(), "CHIMP_TYPE_");
        }

        internal static string GetLocalizedGoodName(eGoods good)
        {
            if (TryGetLocalizedGoodName(good, out string localizedName, out _, out _))
                return localizedName;

            return FormatEnumName(good.ToString(), "STORED_");
        }

        internal static bool TryGetLocalizedGoodName(eGoods good, out string localizedName, out string translationKey, out bool found)
        {
            int index = (int)good;
            translationKey = GetTranslationKey(GoodsTextSection, index);
            found = false;

            if (TryGetGameTextDictionaryValue(translationKey, out localizedName))
            {
                found = true;
                return true;
            }

            if (TryGetLocalizedGameTextExOnly(GoodsTextSection, index, out localizedName))
            {
                found = true;
                return true;
            }

            localizedName = FormatEnumName(good.ToString(), "STORED_");
            return false;
        }

        private static bool TryGetGameTextDictionaryValue(string translationKey, out string localizedName)
        {
            localizedName = null;
            if (string.IsNullOrEmpty(translationKey))
                return false;

            if (CrusaderDE.Translate.Instance?.GameTexts != null &&
                CrusaderDE.Translate.Instance.GameTexts.TryGetValue(translationKey, out localizedName) &&
                !string.IsNullOrWhiteSpace(localizedName))
            {
                return true;
            }

            localizedName = null;
            return false;
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
            catch (Exception)
            {
            }

            return TryGetLocalizedGameTextKey(GetTranslationKey(sectionName, index), out localizedName);
        }

        private static bool TryGetLocalizedGameTextExOnly(string sectionName, int index, out string localizedName)
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
            catch (Exception)
            {
            }

            localizedName = null;
            return false;
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
            catch (Exception)
            {
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

        private static string GetTranslationKey(string sectionName, int index)
        {
            return sectionName + "_" + (index + 1).ToString("D3");
        }

        private static string FormatEnumName(string enumName, string prefix)
        {
            string name = enumName.StartsWith(prefix, StringComparison.Ordinal) ? enumName.Substring(prefix.Length) : enumName;
            return name.Replace('_', ' ').ToLowerInvariant();
        }
    }
}
