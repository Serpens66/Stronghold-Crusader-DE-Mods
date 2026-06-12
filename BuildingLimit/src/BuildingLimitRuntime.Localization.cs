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

        private static bool TryGetBuildMenuTranslationKey(BuildingLimitDefinition definition, out string translationKey)
        {
            foreach (eStructs structure in definition.Structures)
            {
                if (BuildMenuTranslationKeys.TryGetValue(structure, out translationKey))
                    return true;
            }

            translationKey = null;
            return false;
        }

        private static readonly Dictionary<eStructs, string> BuildMenuTranslationKeys = new Dictionary<eStructs, string>
        {
            { eStructs.STRUCT_WOODCUTTERS_HUT, "TEXT_BUBBLE_HELP_TEXT_043" },
            { eStructs.STRUCT_HUNTERS_HUT, "TEXT_BUBBLE_HELP_TEXT_063" },
            { eStructs.STRUCT_OXEN_BASE, "TEXT_BUBBLE_HELP_TEXT_051" },
            { eStructs.STRUCT_QUARRY, "TEXT_BUBBLE_HELP_TEXT_042" },
            { eStructs.STRUCT_IRON_MINE, "TEXT_BUBBLE_HELP_TEXT_044" },
            { eStructs.STRUCT_PITCH_DIGGER, "TEXT_BUBBLE_HELP_TEXT_045" },
            { eStructs.STRUCT_WHEATFARM, "TEXT_BUBBLE_HELP_TEXT_059" },
            { eStructs.STRUCT_HOPSFARM, "TEXT_BUBBLE_HELP_TEXT_061" },
            { eStructs.STRUCT_APPLEFARM, "TEXT_BUBBLE_HELP_TEXT_060" },
            { eStructs.STRUCT_CATTLEFARM, "TEXT_BUBBLE_HELP_TEXT_062" },
            { eStructs.STRUCT_MILL, "TEXT_BUBBLE_HELP_TEXT_049" },
            { eStructs.STRUCT_BAKERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_057" },
            { eStructs.STRUCT_BREWERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_058" },
            { eStructs.STRUCT_HOVEL, "TEXT_BUBBLE_HELP_TEXT_040" },
            { eStructs.STRUCT_GRANARY, "TEXT_BUBBLE_HELP_TEXT_047" },
            { eStructs.STRUCT_GOODS_YARD, "TEXT_BUBBLE_HELP_TEXT_046" },
            { eStructs.STRUCT_ARMOURY, "TEXT_BUBBLE_HELP_TEXT_009" },
            { eStructs.STRUCT_TRADEPOST, "TEXT_BUBBLE_HELP_TEXT_050" },
            { eStructs.STRUCT_INN, "TEXT_BUBBLE_HELP_TEXT_065" },
            { eStructs.STRUCT_HEALER, "TEXT_BUBBLE_HELP_TEXT_066" },
            { eStructs.STRUCT_FLETCHERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_055" },
            { eStructs.STRUCT_POLETURNERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_056" },
            { eStructs.STRUCT_BLACKSMITHS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_052" },
            { eStructs.STRUCT_ARMOURERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_053" },
            { eStructs.STRUCT_TANNERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_054" },
            { eStructs.STRUCT_STABLES, "TEXT_BUBBLE_HELP_TEXT_012" },
            { eStructs.STRUCT_BARRACKS_WOOD, "TEXT_BUBBLE_HELP_TEXT_011" },
            { eStructs.STRUCT_BARRACKS_STONE, "TEXT_BUBBLE_HELP_TEXT_010" },
            { eStructs.STRUCT_ENGINEERS_GUILD, "TEXT_BUBBLE_HELP_TEXT_067" },
            { eStructs.STRUCT_TUNNELLERS_GUILD, "TEXT_BUBBLE_HELP_TEXT_068" },
            { eStructs.STRUCT_OIL_SMELTER, "TEXT_BUBBLE_HELP_TEXT_140" },
            { eStructs.STRUCT_WELL, "TEXT_BUBBLE_HELP_TEXT_048" },
            { eStructs.STRUCT_WATERPOT, "TEXT_IN_WATERPOT_001" },
            { eStructs.STRUCT_CHURCH1, "TEXT_BUBBLE_HELP_TEXT_102" },
            { eStructs.STRUCT_CHURCH2, "TEXT_BUBBLE_HELP_TEXT_103" },
            { eStructs.STRUCT_CHURCH3, "TEXT_BUBBLE_HELP_TEXT_104" },
            { eStructs.STRUCT_TOWER1, "TEXT_BUBBLE_HELP_TEXT_022" },
            { eStructs.STRUCT_TOWER2, "TEXT_BUBBLE_HELP_TEXT_023" },
            { eStructs.STRUCT_TOWER3, "TEXT_BUBBLE_HELP_TEXT_024" },
            { eStructs.STRUCT_TOWER4, "TEXT_BUBBLE_HELP_TEXT_025" },
            { eStructs.STRUCT_TOWER5, "TEXT_BUBBLE_HELP_TEXT_026" },
            { eStructs.STRUCT_GATE_STONE2A, "TEXT_BUBBLE_HELP_TEXT_092" },
            { eStructs.STRUCT_GATE_STONE2B, "TEXT_BUBBLE_HELP_TEXT_092" },
            { eStructs.STRUCT_GATE_STONE1A, "TEXT_BUBBLE_HELP_TEXT_030" },
            { eStructs.STRUCT_GATE_STONE1B, "TEXT_BUBBLE_HELP_TEXT_030" },
            { eStructs.STRUCT_GATE_WOOD1A, "TEXT_BUBBLE_HELP_TEXT_029" },
            { eStructs.STRUCT_GATE_WOOD1B, "TEXT_BUBBLE_HELP_TEXT_029" },
            { eStructs.STRUCT_GATE_WOOD1C, "TEXT_BUBBLE_HELP_TEXT_029" },
            { eStructs.STRUCT_GATE_WOOD1D, "TEXT_BUBBLE_HELP_TEXT_029" },
            { eStructs.STRUCT_DRAWBRIDGE, "TEXT_BUBBLE_HELP_TEXT_031" },
            { eStructs.STRUCT_KILLING_PIT, "TEXT_BUBBLE_HELP_TEXT_018" },
            { eStructs.STRUCT_BRAZIER, "TEXT_BUBBLE_HELP_TEXT_017" },
            { eStructs.STRUCT_MANGONEL, "TEXT_BUBBLE_HELP_TEXT_134" },
            { eStructs.STRUCT_BALLISTA, "TEXT_BUBBLE_HELP_TEXT_198" },
            { eStructs.STRUCT_MAYPOLE, "TEXT_BUBBLE_HELP_TEXT_073" },
            { eStructs.STRUCT_GALLOWS, "TEXT_BUBBLE_HELP_TEXT_072" },
            { eStructs.STRUCT_STOCKS, "TEXT_BUBBLE_HELP_TEXT_076" },
            { eStructs.STRUCT_GARDEN, "TEXT_BUBBLE_HELP_TEXT_105" },
            { eStructs.STRUCT_CESS_PIT, "TEXT_BUBBLE_HELP_TEXT_272" },
            { eStructs.STRUCT_BURNING_STAKE, "TEXT_BUBBLE_HELP_TEXT_273" },
            { eStructs.STRUCT_GIBBET, "TEXT_BUBBLE_HELP_TEXT_274" },
            { eStructs.STRUCT_DUNGEON, "TEXT_BUBBLE_HELP_TEXT_275" },
            { eStructs.STRUCT_RACK_STRETCHING, "TEXT_BUBBLE_HELP_TEXT_276" },
            { eStructs.STRUCT_CHOPPING_BLOCK, "TEXT_BUBBLE_HELP_TEXT_278" },
            { eStructs.STRUCT_DUNKING_STOOL, "TEXT_BUBBLE_HELP_TEXT_279" },
            { eStructs.STRUCT_DOG_CAGE, "TEXT_BUBBLE_HELP_TEXT_280" },
            { eStructs.STRUCT_STATUE, "TEXT_BUBBLE_HELP_TEXT_281" },
            { eStructs.STRUCT_SHRINE, "TEXT_BUBBLE_HELP_TEXT_282" },
            { eStructs.STRUCT_DANCING_BEAR, "TEXT_BUBBLE_HELP_TEXT_284" },
            { eStructs.STRUCT_POND, "TEXT_BUBBLE_HELP_TEXT_285" },
            { eStructs.STRUCT_OUTPOST_BEDOUIN, "TEXT_BUBBLE_HELP_TEXT_041" },
            { eStructs.STRUCT_BEDOUIN_STOCKADE, "TEXT_BUBBLE_HELP_TEXT_349" },
        };

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
            if (TryGetBuildMenuTranslationKey(definition, out string translationKey))
                return translationKey;

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
                    return "TEXT_IN_TOWER_001";
                case eMappers.MAPPER_MANGONEL:
                    return "TEXT_BUBBLE_HELP_TEXT_134";
                case eMappers.MAPPER_BALLISTA:
                    return "TEXT_BUBBLE_HELP_TEXT_198";
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
                case eMappers.MAPPER_BRAZIER:
                    return "TEXT_BUBBLE_HELP_TEXT_017";
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
