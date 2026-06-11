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
        internal static string GetLocalizedBuildingName(BuildingLimitDefinition definition)
        {
            string translationKey = GetBuildingNameTranslationKey(definition);
            if (TryGetLocalizedGameText(translationKey, out string localizedName))
                return localizedName;

            return definition.DisplayName;
        }

        private static bool TryGetLocalizedGameText(string translationKey, out string localizedName)
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

        private static string GetBuildingNameTranslationKey(BuildingLimitDefinition definition)
        {
            switch (definition.Mapper)
            {
                case eMappers.MAPPER_HOVEL:
                    return "TEXT_IN_HOUSE_001";
                case eMappers.MAPPER_HEALER:
                    return "TEXT_IN_HEALERS_001";
                case eMappers.MAPPER_BARRACKS_WOOD:
                case eMappers.MAPPER_BARRACKS_STONE:
                    return "TEXT_IN_BARRACKS_001";
                case eMappers.MAPPER_CHURCH1:
                    return "TEXT_IN_CHURCH_001";
                case eMappers.MAPPER_CHURCH2:
                    return "TEXT_IN_CHURCH_004";
                case eMappers.MAPPER_CHURCH3:
                    return "TEXT_IN_CHURCH_005";
                case eMappers.MAPPER_TOWER1:
                case eMappers.MAPPER_TOWER2:
                case eMappers.MAPPER_TOWER3:
                case eMappers.MAPPER_TOWER4:
                case eMappers.MAPPER_TOWER5:
                case eMappers.MAPPER_MANGONEL:
                case eMappers.MAPPER_BALLISTA:
                    return "TEXT_IN_TOWER_001";
                case eMappers.MAPPER_GATE_MAIN:
                case eMappers.MAPPER_GATE_INNER:
                case eMappers.MAPPER_GATE_WOOD:
                    return "TEXT_IN_GATEHOUSE_001";
                case eMappers.MAPPER_GATE_POSTERN:
                    return "TEXT_IN_POSTERN_GATE_001";
                case eMappers.MAPPER_RACK_STRETCHING:
                    return "TEXT_IN_STRETCHING_RACK_001";
                case eMappers.MAPPER_RACK_FLOGGING:
                    return "TEXT_IN_FLOGGING_RACK_001";
                case eMappers.MAPPER_BEE_HIVE:
                    return "TEXT_IN_BEEHIVE_001";
                case eMappers.MAPPER_OUTPOST_BEDOUIN:
                    return "TEXT_IN_OUTPOST_001";
                case eMappers.MAPPER_BEDOUIN_STOCKADE:
                    return "TEXT_IN_OUTPOST_010";
            }

            string structureName = definition.Structures[0].ToString();
            const string structurePrefix = "STRUCT_";
            if (structureName.StartsWith(structurePrefix, StringComparison.Ordinal))
                structureName = structureName.Substring(structurePrefix.Length);

            return "TEXT_IN_" + structureName + "_001";
        }
    }
}
