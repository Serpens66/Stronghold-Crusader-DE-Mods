using System;
using System.Collections.Generic;

namespace SpawnCastle
{
    internal static class BlueprintBuildingIconCatalog
    {
        private static readonly IReadOnlyDictionary<string, string> ResourceKeys =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MAPPER_FLETCHER"] = "UI-Buildings I001",
                ["MAPPER_WOODSMAN"] = "UI-Buildings D003",
                ["MAPPER_STORES"] = "UI-Buildings D001",
                ["MAPPER_HOVEL"] = "UI-Buildings F001",
                ["MAPPER_OXENBASE"] = "UI-Buildings D007",
                ["MAPPER_QUARRY"] = "UI-Buildings D005",
                ["MAPPER_STABLES"] = "UI-Buildings M007",
                ["MAPPER_WHEATFARM"] = "UI-Buildings E007",
                ["MAPPER_HOPSFARM"] = "UI-Buildings E009",
                ["MAPPER_APPLEFARM"] = "UI-Buildings E005",
                ["MAPPER_CATTLEFARM"] = "UI-Buildings E003",
                ["MAPPER_MILL"] = "UI-Buildings J005",
                ["MAPPER_BAKER"] = "UI-Buildings J003",
                ["MAPPER_BREWER"] = "UI-Buildings J007",
                ["MAPPER_TRADEPOST"] = "UI-Buildings D013",
                ["MAPPER_HUNTER"] = "UI-Buildings E001",
                ["MAPPER_BEDOUIN_STOCKADE"] = "UI-Buildings C015",
                ["MAPPER_GRANARY"] = "UI-Buildings J001",
                ["MAPPER_ARMOURY"] = "UI-Buildings C013",
                ["MAPPER_POLETURNER"] = "UI-Buildings I003",
                ["MAPPER_BLACKSMITH"] = "UI-Buildings I005",
                ["MAPPER_ARMOURER"] = "UI-Buildings I009",
                ["MAPPER_TANNER"] = "UI-Buildings I007",
                ["MAPPER_BARRACKS_WOOD"] = "UI-Buildings C011",
                ["MAPPER_BARRACKS_STONE"] = "UI-Buildings C009",
                ["MAPPER_ENGINEERS_GUILD"] = "UI-Buildings M001",
                ["MAPPER_TUNNELERS_GUILD"] = "UI-Buildings M009",
                ["MAPPER_IRON_MINE"] = "UI-Buildings D009",
                ["MAPPER_PITCH_WORKINGS"] = "UI-Buildings D011",
                ["MAPPER_INN"] = "UI-Buildings J009",
                ["MAPPER_HEALER"] = "UI-Buildings F009",
                ["MAPPER_CHURCH1"] = "UI-Buildings F003",
                ["MAPPER_CHURCH2"] = "UI-Buildings F005",
                ["MAPPER_CHURCH3"] = "UI-Buildings F007",
                ["MAPPER_DRAWBRIDGE"] = "UI-Buildings L007",
                ["MAPPER_TOWER1"] = "UI-Buildings K001",
                ["MAPPER_TOWER2"] = "UI-Buildings K003",
                ["MAPPER_TOWER3"] = "UI-Buildings K005",
                ["MAPPER_TOWER4"] = "UI-Buildings K007",
                ["MAPPER_TOWER5"] = "UI-Buildings K009",
                ["MAPPER_GATE_STONE1A"] = "UI-Buildings L003",
                ["MAPPER_GATE_STONE1B"] = "UI-Buildings L003",
                ["MAPPER_GATE_STONE2A"] = "UI-Buildings L005",
                ["MAPPER_GATE_STONE2B"] = "UI-Buildings L005",
                ["MAPPER_GARDEN1"] = "UI-Buildings H005",
                ["MAPPER_GARDEN7"] = "UI-Buildings H005",
                ["MAPPER_GARDEN10"] = "UI-Buildings H005",
                ["MAPPER_MAYPOLE"] = "UI-Buildings H001",
                ["MAPPER_GALLOWS"] = "UI-Buildings G001",
                ["MAPPER_STOCKS"] = "UI-Buildings G005",
                ["MAPPER_OIL_SMELTER"] = "UI-Buildings M011",
                ["MAPPER_CESS_PIT1"] = "UI-Buildings G003",
                ["MAPPER_BURNING_STAKE"] = "UI-Buildings G009",
                ["MAPPER_GIBBET"] = "UI-Buildings G015",
                ["MAPPER_DUNGEON"] = "UI-Buildings G011",
                ["MAPPER_RACK_STRETCHING"] = "UI-Buildings G013",
                ["MAPPER_CHOPPING_BLOCK"] = "UI-Buildings G017",
                ["MAPPER_DUNKING_STOOL"] = "UI-Buildings G019",
                ["MAPPER_DOG_CAGE"] = "UI-Buildings L009",
                ["MAPPER_STATUE1"] = "UI-Buildings H007",
                ["MAPPER_SHRINE1"] = "UI-Buildings H009",
                ["MAPPER_DANCING_BEAR"] = "UI-Buildings H003",
                ["MAPPER_POND1"] = "UI-Buildings H011",
                ["MAPPER_POND3"] = "UI-Buildings H011",
                ["MAPPER_WELL"] = "UI-Buildings F011",
                ["MAPPER_WATERPOT"] = "UI-Buildings F013"
            };

        public static string Resolve(string mapperName)
        {
            return mapperName != null &&
                ResourceKeys.TryGetValue(mapperName, out string key)
                    ? key
                    : null;
        }
    }
}
