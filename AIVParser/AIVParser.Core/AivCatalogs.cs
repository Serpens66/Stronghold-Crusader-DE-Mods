using System.Collections.Generic;

namespace AIVParser.Core
{
    public static class AivMapperCatalog
    {
        private static readonly IReadOnlyDictionary<int, AivMapperInfo> KnownMappers =
            CreateKnownMappers();

        public static AivMapperInfo Resolve(int value)
        {
            if (KnownMappers.TryGetValue(value, out AivMapperInfo mapper))
            {
                return mapper;
            }

            return new AivMapperInfo(
                value,
                $"UNKNOWN_MAPPER_{value}",
                AivItemCategory.Unknown,
                false);
        }

        public static bool IsKeep(int value)
        {
            return value >= 60 && value <= 64;
        }

        private static IReadOnlyDictionary<int, AivMapperInfo> CreateKnownMappers()
        {
            var result = new Dictionary<int, AivMapperInfo>();

            Add(result, 25, "MAPPER_WALL", AivItemCategory.HighWallPath);
            Add(result, 26, "MAPPER_CRENAL", AivItemCategory.CrenelPath);
            Add(result, 35, "MAPPER_CRENAL2", AivItemCategory.CrenelPath);
            Add(result, 46, "MAPPER_WOODWALL", AivItemCategory.LowWallPath);
            Add(result, 50, "MAPPER_FLETCHER");
            Add(result, 52, "MAPPER_STORES");
            Add(result, 54, "MAPPER_HOVEL");
            Add(result, 60, "MAPPER_KEEP1", AivItemCategory.Keep);
            Add(result, 61, "MAPPER_KEEP2", AivItemCategory.Keep);
            Add(result, 62, "MAPPER_KEEP3", AivItemCategory.Keep);
            Add(result, 63, "MAPPER_KEEP4", AivItemCategory.Keep);
            Add(result, 64, "MAPPER_KEEP5", AivItemCategory.Keep);
            Add(result, 65, "MAPPER_STABLES");
            Add(result, 74, "MAPPER_MILL");
            Add(result, 75, "MAPPER_BAKER");
            Add(result, 76, "MAPPER_BREWER");
            Add(result, 77, "MAPPER_TRADEPOST");
            Add(result, 79, "MAPPER_BEDOUIN_STOCKADE");
            Add(result, 80, "MAPPER_GRANARY");
            Add(result, 81, "MAPPER_ARMOURY");
            Add(result, 82, "MAPPER_POLETURNER");
            Add(result, 83, "MAPPER_BLACKSMITH");
            Add(result, 84, "MAPPER_ARMOURER");
            Add(result, 85, "MAPPER_TANNER");
            Add(result, 86, "MAPPER_BARRACKS_WOOD");
            Add(result, 87, "MAPPER_BARRACKS_STONE");
            Add(result, 88, "MAPPER_ENGINEERS_GUILD");
            Add(result, 89, "MAPPER_TUNNELERS_GUILD");
            Add(result, 92, "MAPPER_INN");
            Add(result, 93, "MAPPER_HEALER");
            Add(result, 95, "MAPPER_CHURCH1");
            Add(result, 96, "MAPPER_CHURCH2");
            Add(result, 97, "MAPPER_CHURCH3");
            Add(result, 98, "MAPPER_KILLING_PIT", AivItemCategory.Trap);
            Add(result, 99, "MAPPER_PITCH_DITCH", AivItemCategory.PitchDitchPath);
            Add(result, 105, "MAPPER_DRAWBRIDGE");
            Add(result, 106, "MAPPER_MOAT", AivItemCategory.MoatPath);
            Add(result, 110, "MAPPER_TOWER1");
            Add(result, 111, "MAPPER_TOWER2");
            Add(result, 112, "MAPPER_TOWER3");
            Add(result, 113, "MAPPER_TOWER4");
            Add(result, 114, "MAPPER_TOWER5");
            Add(result, 144, "MAPPER_GATE_STONE1A");
            Add(result, 145, "MAPPER_GATE_STONE1B");
            Add(result, 146, "MAPPER_GATE_STONE2A");
            Add(result, 147, "MAPPER_GATE_STONE2B");
            Add(result, 160, "MAPPER_GARDEN1");
            Add(result, 166, "MAPPER_GARDEN7");
            Add(result, 169, "MAPPER_GARDEN10");
            Add(result, 175, "MAPPER_MAYPOLE");
            Add(result, 176, "MAPPER_GALLOWS");
            Add(result, 177, "MAPPER_STOCKS");
            Add(result, 180, "MAPPER_OIL_SMELTER");
            Add(result, 181, "MAPPER_STAIR1", AivItemCategory.Stair);
            Add(result, 182, "MAPPER_STAIR2", AivItemCategory.Stair);
            Add(result, 183, "MAPPER_STAIR3", AivItemCategory.Stair);
            Add(result, 184, "MAPPER_STAIR4", AivItemCategory.Stair);
            Add(result, 185, "MAPPER_STAIR5", AivItemCategory.Stair);
            Add(result, 186, "MAPPER_STAIR6", AivItemCategory.Stair);
            Add(result, 301, "MAPPER_CESS_PIT1");
            Add(result, 305, "MAPPER_BURNING_STAKE");
            Add(result, 306, "MAPPER_GIBBET");
            Add(result, 307, "MAPPER_DUNGEON");
            Add(result, 308, "MAPPER_RACK_STRETCHING");
            Add(result, 310, "MAPPER_CHOPPING_BLOCK");
            Add(result, 312, "MAPPER_DOG_CAGE");
            Add(result, 313, "MAPPER_STATUE1");
            Add(result, 318, "MAPPER_SHRINE1");
            Add(result, 324, "MAPPER_DANCING_BEAR");
            Add(result, 330, "MAPPER_WELL");
            Add(result, 342, "MAPPER_WATERPOT");

            return result;
        }

        private static void Add(
            IDictionary<int, AivMapperInfo> target,
            int value,
            string name,
            AivItemCategory category = AivItemCategory.Building)
        {
            target.Add(value, new AivMapperInfo(value, name, category, true));
        }
    }

    public static class AivMiscTypeCatalog
    {
        private static readonly IReadOnlyDictionary<int, string> KnownEngineTypes =
            new Dictionary<int, string>
            {
                [1] = "ENGINEER",
                [2] = "MANGONEL",
                [3] = "BALLISTA",
                [4] = "TREBUCHET",
                [5] = "FIRE_BALLISTA",
                [6] = "ARCHER",
                [7] = "CROSSBOWMAN",
                [8] = "SPEARMAN",
                [9] = "PIKEMAN",
                [10] = "MACEMAN",
                [11] = "SWORDSMAN",
                [12] = "KNIGHT",
                [13] = "SLAVE",
                [14] = "SLINGER",
                [15] = "ASSASSIN",
                [16] = "ARABIAN_ARCHER",
                [17] = "HORSE_ARCHER",
                [18] = "ARABIAN_SWORDSMAN",
                [19] = "FIRE_THROWER",
                [20] = "BRAZIER",
                [21] = "FLAG",
                // DE appends the Bedouin unit sequence after the HD unit-slot values.
                [23] = "BEDOUIN_CAMEL_LANCER",
                [24] = "BEDOUIN_HEALER",
                [25] = "BEDOUIN_EUNUCH",
                [26] = "BEDOUIN_AMBUSHER",
                [27] = "BEDOUIN_SKIRMISHER",
                [28] = "BEDOUIN_HEAVY_CAMEL",
                [29] = "BEDOUIN_SAPPER",
                [30] = "BEDOUIN_DEMOLISHER"
            };

        public static AivMiscTypeInfo Resolve(int jsonValue)
        {
            // This mirrors AIVLoader.SaveData.GetRawData() exactly.
            int engineValue = jsonValue > 9000 ? jsonValue - 9000 : jsonValue;
            if (KnownEngineTypes.TryGetValue(engineValue, out string name))
            {
                return new AivMiscTypeInfo(jsonValue, engineValue, name, true);
            }

            return new AivMiscTypeInfo(
                jsonValue,
                engineValue,
                $"UNKNOWN_MISC_{engineValue}",
                false);
        }
    }
}
