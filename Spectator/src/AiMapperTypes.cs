using SHCDESE.Interop;

namespace Spectator
{
    internal static class AiMapperTypes
    {
        internal static bool NeedsCrew(eChimps type) => type == eChimps.CHIMP_TYPE_CATAPULT ||
            type == eChimps.CHIMP_TYPE_TREBUCHET || type == eChimps.CHIMP_TYPE_BATTERING_RAM ||
            type == eChimps.CHIMP_TYPE_SIEGE_TOWER || type == eChimps.CHIMP_TYPE_PORTABLE_SHIELD ||
            type == eChimps.CHIMP_TYPE_ARAB_BALLISTA;
        // Native FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2,
        // canonical 93750 placement switch: explicit enum domains, including the oil-carrier
        // variant (same engineer type, different native post-initialization state).
        internal static bool TryGet(eMappers mapper, out eChimps type)
        {
            switch (mapper)
            {
                case eMappers.MAPPER_PEOPLE_ARCHERS: type = eChimps.CHIMP_TYPE_ARCHER; return true;
                case eMappers.MAPPER_PEOPLE_SPEARMEN: type = eChimps.CHIMP_TYPE_SPEARMAN; return true;
                case eMappers.MAPPER_PEOPLE_PIKEMEN: type = eChimps.CHIMP_TYPE_PIKEMAN; return true;
                case eMappers.MAPPER_PEOPLE_MACEMEN: type = eChimps.CHIMP_TYPE_MACEMAN; return true;
                case eMappers.MAPPER_PEOPLE_XBOWMEN: type = eChimps.CHIMP_TYPE_XBOWMAN; return true;
                case eMappers.MAPPER_PEOPLE_SWORDSMEN: type = eChimps.CHIMP_TYPE_SWORDSMAN; return true;
                case eMappers.MAPPER_PEOPLE_KNIGHTS: type = eChimps.CHIMP_TYPE_KNIGHT; return true;
                case eMappers.MAPPER_PEOPLE_LADDERMEN: type = eChimps.CHIMP_TYPE_LADDERMAN; return true;
                case eMappers.MAPPER_PEOPLE_ENGINEERS:
                case eMappers.MAPPER_PEOPLE_ENGINEERS_POTS: type = eChimps.CHIMP_TYPE_ENGINEER; return true;
                case eMappers.MAPPER_PEOPLE_MONKS: type = eChimps.CHIMP_TYPE_MONK; return true;
                case eMappers.MAPPER_PEOPLE_CATAPULTS: type = eChimps.CHIMP_TYPE_CATAPULT; return true;
                case eMappers.MAPPER_PEOPLE_TREBUCHETS: type = eChimps.CHIMP_TYPE_TREBUCHET; return true;
                case eMappers.MAPPER_PEOPLE_BATTERING_RAMS: type = eChimps.CHIMP_TYPE_BATTERING_RAM; return true;
                case eMappers.MAPPER_PEOPLE_SIEGE_TOWERS: type = eChimps.CHIMP_TYPE_SIEGE_TOWER; return true;
                case eMappers.MAPPER_PEOPLE_PORTABLE_SHIELDS: type = eChimps.CHIMP_TYPE_PORTABLE_SHIELD; return true;
                case eMappers.MAPPER_PEOPLE_TUNNELERS: type = eChimps.CHIMP_TYPE_TUNNELER; return true;
                case eMappers.MAPPER_PEOPLE_ARAB_BOW: type = eChimps.CHIMP_TYPE_ARAB_BOW; return true;
                case eMappers.MAPPER_PEOPLE_ARAB_SLAVE: type = eChimps.CHIMP_TYPE_ARAB_SLAVE; return true;
                case eMappers.MAPPER_PEOPLE_ARAB_SLINGER: type = eChimps.CHIMP_TYPE_ARAB_SLINGER; return true;
                case eMappers.MAPPER_PEOPLE_ARAB_ASSASIN: type = eChimps.CHIMP_TYPE_ARAB_ASSASIN; return true;
                case eMappers.MAPPER_PEOPLE_ARAB_HORSEMAN: type = eChimps.CHIMP_TYPE_ARAB_HORSEMAN; return true;
                case eMappers.MAPPER_PEOPLE_ARAB_SWORDSMAN: type = eChimps.CHIMP_TYPE_ARAB_SWORDSMAN; return true;
                case eMappers.MAPPER_PEOPLE_ARAB_GRENADIER: type = eChimps.CHIMP_TYPE_ARAB_GRENADIER; return true;
                case eMappers.MAPPER_PEOPLE_ARAB_BALLISTA: type = eChimps.CHIMP_TYPE_ARAB_BALLISTA; return true;
                case eMappers.MAPPER_PEOPLE_BEDOUIN_CAMEL_LANCER: type = eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER; return true;
                case eMappers.MAPPER_PEOPLE_BEDOUIN_HEALER: type = eChimps.CHIMP_TYPE_BEDOUIN_HEALER; return true;
                case eMappers.MAPPER_PEOPLE_BEDOUIN_EUNUCH: type = eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH; return true;
                case eMappers.MAPPER_PEOPLE_BEDOUIN_AMBUSHER: type = eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER; return true;
                case eMappers.MAPPER_PEOPLE_BEDOUIN_SKIRMISHER: type = eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER; return true;
                case eMappers.MAPPER_PEOPLE_BEDOUIN_HEAVY_CAMEL: type = eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL; return true;
                case eMappers.MAPPER_PEOPLE_BEDOUIN_SAPPER: type = eChimps.CHIMP_TYPE_BEDOUIN_SAPPER; return true;
                case eMappers.MAPPER_PEOPLE_BEDOUIN_DEMOLISHER: type = eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER; return true;
                default: type = default; return false;
            }
        }
    }
}
