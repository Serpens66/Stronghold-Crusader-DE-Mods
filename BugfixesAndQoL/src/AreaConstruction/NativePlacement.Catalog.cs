using SHCDESE.Interop;

namespace BugfixesAndQoL.AreaConstruction
{
    internal sealed unsafe partial class NativePlacement
    {
        private static bool IsChurch(eMappers mapper) =>
            mapper == eMappers.MAPPER_CHURCH1 || mapper == eMappers.MAPPER_CHURCH2 ||
            mapper == eMappers.MAPPER_CHURCH3;

        internal static bool IsFarm(eMappers mapper) =>
            mapper == eMappers.MAPPER_WHEATFARM || mapper == eMappers.MAPPER_HOPSFARM ||
            mapper == eMappers.MAPPER_APPLEFARM || mapper == eMappers.MAPPER_CATTLEFARM;

        internal static int FootprintHeight(eMappers mapper, int size) =>
            mapper == eMappers.MAPPER_OIL_SMELTER ? 2 * size : size;

        // Base frame of the native cursor sheet. The live preview supplies
        // the actual image variation where Vanilla selects one.
        private static int RegularBuildingFrame(eMappers mapper)
        {
            switch (mapper)
            {
                case eMappers.MAPPER_TUNNEL_CONSTRUCTION: return 136;
                case eMappers.MAPPER_CATAPULT:
                case eMappers.MAPPER_TREBUCHET:
                case eMappers.MAPPER_SIEGE_TOWER:
                case eMappers.MAPPER_BATTERING_RAM:
                case eMappers.MAPPER_PORTABLE_SHIELD:
                case eMappers.MAPPER_ARAB_BALLISTA: return 161;
                case eMappers.MAPPER_HUNTER: return 560;
                case eMappers.MAPPER_OXENBASE: return 355;
                case eMappers.MAPPER_STABLES: return 569;
                case eMappers.MAPPER_INN: return 427;
                case eMappers.MAPPER_HEALER: return 664;
                case eMappers.MAPPER_QUARRY: return 174;
                case eMappers.MAPPER_IRON_MINE: return 477;
                case eMappers.MAPPER_PITCH_WORKINGS: return 494;
                case eMappers.MAPPER_CHURCH1: return 251;
                case eMappers.MAPPER_CHURCH2: return 170;
                case eMappers.MAPPER_CHURCH3: return 1;
                case eMappers.MAPPER_OUTPOST_BEDOUIN: return 1315;
                case eMappers.MAPPER_OUTPOST: return 1290;
                case eMappers.MAPPER_OUTPOST_ARAB: return 1265;
                case eMappers.MAPPER_GALLOWS: return 700;
                case eMappers.MAPPER_STOCKS: return 756;
                case eMappers.MAPPER_MAYPOLE: return 655;
                case eMappers.MAPPER_WELL: return 1001;
                case eMappers.MAPPER_WATERPOT: return 1176;
                case eMappers.MAPPER_DOG_CAGE: return 1044;
                case eMappers.MAPPER_BURNING_STAKE: return 929;
                case eMappers.MAPPER_GIBBET: return 938;
                case eMappers.MAPPER_DUNGEON: return 967;
                case eMappers.MAPPER_RACK_STRETCHING: return 992;
                case eMappers.MAPPER_CHOPPING_BLOCK: return 1035;
                case eMappers.MAPPER_DUNKING_STOOL: return 1010;
                case eMappers.MAPPER_DANCING_BEAR: return 1081;
                case eMappers.MAPPER_OIL_SMELTER: return 708;
                case eMappers.MAPPER_STATUE1: return 1053;
                case eMappers.MAPPER_STATUE2: return 1057;
                case eMappers.MAPPER_STATUE3: return 1061;
                case eMappers.MAPPER_STATUE4: return 1065;
                case eMappers.MAPPER_STATUE5: return 1069;
                case eMappers.MAPPER_SHRINE1: return 1073;
                case eMappers.MAPPER_SHRINE2: return 1077;
                case eMappers.MAPPER_SHRINE3: return 1106;
                case eMappers.MAPPER_SHRINE4: return 1110;
                case eMappers.MAPPER_SHRINE5: return 1150;
                case eMappers.MAPPER_CESS_PIT1: return 829;
                case eMappers.MAPPER_CESS_PIT2: return 854;
                case eMappers.MAPPER_CESS_PIT3: return 879;
                case eMappers.MAPPER_CESS_PIT4: return 904;
                case eMappers.MAPPER_WHEATFARM: return 391;
                case eMappers.MAPPER_HOPSFARM: return 400;
                case eMappers.MAPPER_APPLEFARM: return 409;
                case eMappers.MAPPER_CATTLEFARM: return 418;
                default: return LandscapeFrame(mapper);
            }
        }

        private static int LandscapeFrame(eMappers mapper)
        {
            switch (mapper)
            {
                case eMappers.MAPPER_GARDEN1: return 198;
                case eMappers.MAPPER_GARDEN2: return 202;
                case eMappers.MAPPER_GARDEN3: return 206;
                case eMappers.MAPPER_GARDEN4: return 210;
                case eMappers.MAPPER_GARDEN5: return 214;
                case eMappers.MAPPER_GARDEN6: return 218;
                case eMappers.MAPPER_GARDEN7: return 222;
                case eMappers.MAPPER_GARDEN8: return 231;
                case eMappers.MAPPER_GARDEN9: return 240;
                case eMappers.MAPPER_GARDEN10: return 249;
                case eMappers.MAPPER_GARDEN11: return 265;
                case eMappers.MAPPER_GARDEN12: return 281;
                case eMappers.MAPPER_POND1: return 321;
                case eMappers.MAPPER_POND2: return 346;
                case eMappers.MAPPER_POND3: return 371;
                case eMappers.MAPPER_POND4: return 407;
                case eMappers.MAPPER_POND5: return 443;
                case eMappers.MAPPER_POND6: return 507;
                case eMappers.MAPPER_POND7: return 571;
                case eMappers.MAPPER_POND8: return 652;
                default: return 0;
            }
        }
    }
}
