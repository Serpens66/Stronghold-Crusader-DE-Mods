using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace BuildingCosts
{
    internal static class BuildingCostDefinitions
    {
        private static readonly BuildingCostDefinition[] Definitions =
        {
            Create(eMappers.MAPPER_WOODSMAN, eStructs.STRUCT_WOODCUTTERS_HUT, "woodcutters"),
            Create(eMappers.MAPPER_HUNTER, eStructs.STRUCT_HUNTERS_HUT, "hunters"),
            Create(eMappers.MAPPER_OXENBASE, eStructs.STRUCT_OXEN_BASE, "ox tethers"),
            Create(eMappers.MAPPER_QUARRY, eStructs.STRUCT_QUARRY, "quarries"),
            Create(eMappers.MAPPER_IRON_MINE, eStructs.STRUCT_IRON_MINE, "iron mines"),
            Create(eMappers.MAPPER_PITCH_WORKINGS, eStructs.STRUCT_PITCH_DIGGER, "pitch rigs"),
            Create(eMappers.MAPPER_WHEATFARM, eStructs.STRUCT_WHEATFARM, "wheat farms"),
            Create(eMappers.MAPPER_HOPSFARM, eStructs.STRUCT_HOPSFARM, "hop farms"),
            Create(eMappers.MAPPER_APPLEFARM, eStructs.STRUCT_APPLEFARM, "apple orchards"),
            Create(eMappers.MAPPER_CATTLEFARM, eStructs.STRUCT_CATTLEFARM, "dairy farms"),
            Create(eMappers.MAPPER_MILL, eStructs.STRUCT_MILL, "mills"),
            Create(eMappers.MAPPER_BAKER, eStructs.STRUCT_BAKERS_WORKSHOP, "bakeries"),
            Create(eMappers.MAPPER_BREWER, eStructs.STRUCT_BREWERS_WORKSHOP, "breweries"),
            Create(eMappers.MAPPER_HOVEL, eStructs.STRUCT_HOVEL, "hovels"),
            Create(eMappers.MAPPER_GRANARY, eStructs.STRUCT_GRANARY, "granaries"),
            Create(eMappers.MAPPER_STORES, eStructs.STRUCT_GOODS_YARD, "stockpiles"),
            Create(eMappers.MAPPER_ARMOURY, eStructs.STRUCT_ARMOURY, "armouries"),
            Create(eMappers.MAPPER_TRADEPOST, eStructs.STRUCT_TRADEPOST, "marketplaces"),
            Create(eMappers.MAPPER_INN, eStructs.STRUCT_INN, "inns"),
            Create(eMappers.MAPPER_HEALER, eStructs.STRUCT_HEALER, "apothecaries"),
            Create(eMappers.MAPPER_FLETCHER, eStructs.STRUCT_FLETCHERS_WORKSHOP, "fletchers"),
            Create(eMappers.MAPPER_POLETURNER, eStructs.STRUCT_POLETURNERS_WORKSHOP, "poleturners"),
            Create(eMappers.MAPPER_BLACKSMITH, eStructs.STRUCT_BLACKSMITHS_WORKSHOP, "blacksmiths"),
            Create(eMappers.MAPPER_ARMOURER, eStructs.STRUCT_ARMOURERS_WORKSHOP, "armourers"),
            Create(eMappers.MAPPER_TANNER, eStructs.STRUCT_TANNERS_WORKSHOP, "tanners"),
            Create(eMappers.MAPPER_STABLES, eStructs.STRUCT_STABLES, "stables"),
            Create(eMappers.MAPPER_BARRACKS_WOOD, eStructs.STRUCT_BARRACKS_WOOD, "wooden barracks"),
            Create(eMappers.MAPPER_BARRACKS_STONE, eStructs.STRUCT_BARRACKS_STONE, "stone barracks"),
            Create(eMappers.MAPPER_ENGINEERS_GUILD, eStructs.STRUCT_ENGINEERS_GUILD, "engineers guilds"),
            Create(eMappers.MAPPER_TUNNELERS_GUILD, eStructs.STRUCT_TUNNELLERS_GUILD, "tunnelers guilds"),
            Create(eMappers.MAPPER_OIL_SMELTER, eStructs.STRUCT_OIL_SMELTER, "oil smelters"),
            Create(eMappers.MAPPER_WELL, eStructs.STRUCT_WELL, "wells"),
            Create(eMappers.MAPPER_WATERPOT, eStructs.STRUCT_WATERPOT, "water pots"),
            Create(eMappers.MAPPER_CHURCH1, eStructs.STRUCT_CHURCH1, "chapels"),
            Create(eMappers.MAPPER_CHURCH2, eStructs.STRUCT_CHURCH2, "churches"),
            Create(eMappers.MAPPER_CHURCH3, eStructs.STRUCT_CHURCH3, "cathedrals"),
            Create(eMappers.MAPPER_TOWER1, eStructs.STRUCT_TOWER1, "lookout towers"),
            Create(eMappers.MAPPER_TOWER2, eStructs.STRUCT_TOWER2, "perimeter turrets"),
            Create(eMappers.MAPPER_TOWER3, eStructs.STRUCT_TOWER3, "defence turrets"),
            Create(eMappers.MAPPER_TOWER4, eStructs.STRUCT_TOWER4, "square towers"),
            Create(eMappers.MAPPER_TOWER5, eStructs.STRUCT_TOWER5, "round towers"),
            Create(eMappers.MAPPER_GATE_MAIN, eStructs.STRUCT_GATE_MAIN, "large stone gatehouses"),
            Create(eMappers.MAPPER_GATE_INNER, eStructs.STRUCT_GATE_INNER, "small stone gatehouses"),
            Create(eMappers.MAPPER_GATE_WOOD, eStructs.STRUCT_GATE_WOOD, "wooden gatehouses"),
            Create(eMappers.MAPPER_GATEHOUSE, eStructs.STRUCT_GATEHOUSE, "gatehouses"),
            Create(eMappers.MAPPER_GATE_POSTERN, eStructs.STRUCT_GATE_POSTERN, "postern gates"),
            Create(eMappers.MAPPER_DRAWBRIDGE, eStructs.STRUCT_DRAWBRIDGE, "drawbridges"),
            Create(eMappers.MAPPER_KILLING_PIT, eStructs.STRUCT_KILLING_PIT, "killing pits"),
            Create(eMappers.MAPPER_BRAZIER, eStructs.STRUCT_BRAZIER, "braziers"),
            Create(eMappers.MAPPER_MANGONEL, eStructs.STRUCT_MANGONEL, "tower mangonels"),
            Create(eMappers.MAPPER_BALLISTA, eStructs.STRUCT_BALLISTA, "tower ballistae"),
            Create(eMappers.MAPPER_MAYPOLE, eStructs.STRUCT_MAYPOLE, "maypoles"),
            Create(eMappers.MAPPER_GALLOWS, eStructs.STRUCT_GALLOWS, "gallows"),
            Create(eMappers.MAPPER_STOCKS, eStructs.STRUCT_STOCKS, "stocks"),
            Create(eMappers.MAPPER_GARDEN1, eStructs.STRUCT_GARDEN, "gardens"),
            Create(eMappers.MAPPER_CESS_PIT1, eStructs.STRUCT_CESS_PIT, "cesspits"),
            Create(eMappers.MAPPER_BURNING_STAKE, eStructs.STRUCT_BURNING_STAKE, "burning stakes"),
            Create(eMappers.MAPPER_GIBBET, eStructs.STRUCT_GIBBET, "gibbets"),
            Create(eMappers.MAPPER_DUNGEON, eStructs.STRUCT_DUNGEON, "dungeons"),
            Create(eMappers.MAPPER_RACK_STRETCHING, eStructs.STRUCT_RACK_STRETCHING, "stretching racks"),
            Create(eMappers.MAPPER_RACK_FLOGGING, eStructs.STRUCT_RACK_FLOGGING, "flogging racks"),
            Create(eMappers.MAPPER_CHOPPING_BLOCK, eStructs.STRUCT_CHOPPING_BLOCK, "chopping blocks"),
            Create(eMappers.MAPPER_DUNKING_STOOL, eStructs.STRUCT_DUNKING_STOOL, "dunking stools"),
            Create(eMappers.MAPPER_DOG_CAGE, eStructs.STRUCT_DOG_CAGE, "dog cages"),
            Create(eMappers.MAPPER_STATUE1, eStructs.STRUCT_STATUE, "statues"),
            Create(eMappers.MAPPER_SHRINE1, eStructs.STRUCT_SHRINE, "shrines"),
            Create(eMappers.MAPPER_BEE_HIVE, eStructs.STRUCT_BEE_HIVE, "bee hives"),
            Create(eMappers.MAPPER_DANCING_BEAR, eStructs.STRUCT_DANCING_BEAR, "dancing bears"),
            Create(eMappers.MAPPER_POND1, eStructs.STRUCT_POND, "ponds"),
            Create(eMappers.MAPPER_BEAR_CAVE, eStructs.STRUCT_BEAR_CAVE, "bear caves"),
            Create(eMappers.MAPPER_OUTPOST_BEDOUIN, eStructs.STRUCT_OUTPOST_BEDOUIN, "Bedouin outposts"),
            Create(eMappers.MAPPER_BEDOUIN_STOCKADE, eStructs.STRUCT_BEDOUIN_STOCKADE, "Bedouin stockades"),
        };

        public static IReadOnlyList<BuildingCostDefinition> All => Definitions;

        public static bool TryGet(eMappers mapper, out BuildingCostDefinition definition)
        {
            foreach (BuildingCostDefinition current in Definitions)
            {
                if (current.Mapper == mapper)
                {
                    definition = current;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public static string GetLocalizedBuildingName(BuildingCostDefinition definition)
        {
            string translationKey = GetBuildingNameTranslationKey(definition);
            if (TryGetLocalizedGameText(translationKey, out string localizedName))
                return localizedName;

            return definition.DisplayName;
        }

        private static BuildingCostDefinition Create(eMappers mapper, eStructs structure, string displayName)
        {
            return new BuildingCostDefinition(mapper, structure, displayName);
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
                    return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GameTranslateAPI lookup failed: " + translationKey + " " + ex.Message);
            }

            if (Translate.Instance?.GameTexts != null &&
                Translate.Instance.GameTexts.TryGetValue(translationKey, out localizedName) &&
                !string.IsNullOrWhiteSpace(localizedName))
                return true;

            localizedName = null;
            return false;
        }

        private static string GetBuildingNameTranslationKey(BuildingCostDefinition definition)
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

            string structureName = definition.Structure.ToString();
            const string structurePrefix = "STRUCT_";
            if (structureName.StartsWith(structurePrefix, StringComparison.Ordinal))
                structureName = structureName.Substring(structurePrefix.Length);

            return "TEXT_IN_" + structureName + "_001";
        }
    }
}
