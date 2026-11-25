using SHCDESE.Interop;

namespace TrappedUnitRescueTest
{
    // Type admission is explicit: native A9E is also1 for several animals. This
    // predicate alone never authorizes a native move, park or job reconstruction.
    internal static class CommandResumePolicy
    {
        internal static bool IsKnownType(eChimps type)
        {
            switch(type)
            {
                case eChimps.CHIMP_TYPE_ARCHER: case eChimps.CHIMP_TYPE_XBOWMAN:
                case eChimps.CHIMP_TYPE_SPEARMAN: case eChimps.CHIMP_TYPE_PIKEMAN:
                case eChimps.CHIMP_TYPE_MACEMAN: case eChimps.CHIMP_TYPE_SWORDSMAN:
                case eChimps.CHIMP_TYPE_KNIGHT: case eChimps.CHIMP_TYPE_LADDERMAN:
                case eChimps.CHIMP_TYPE_ENGINEER: case eChimps.CHIMP_TYPE_MONK:
                case eChimps.CHIMP_TYPE_CATAPULT: case eChimps.CHIMP_TYPE_TREBUCHET:
                case eChimps.CHIMP_TYPE_SIEGE_TOWER: case eChimps.CHIMP_TYPE_BATTERING_RAM:
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD: case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                case eChimps.CHIMP_TYPE_ARAB_BOW: case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                case eChimps.CHIMP_TYPE_ARAB_SLINGER: case eChimps.CHIMP_TYPE_ARAB_ASSASIN:
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN: case eChimps.CHIMP_TYPE_ARAB_SWORDSMAN:
                case eChimps.CHIMP_TYPE_ARAB_GRENADIER: case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEALER: case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                case eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER: case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL: case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:return true;
                default:return false;
            }
        }
        internal static bool IsCommandState(eChimps type,int state)=>IsKnownType(type) &&
            (state==0 || state==101 || state==1 && type!=eChimps.CHIMP_TYPE_ENGINEER);
        // Trebuchet1535F0 does not consume AI101 as an ordinary arrival phase.
        internal static bool IsIndependentRescueType(eChimps type)=>IsKnownType(type) && type!=eChimps.CHIMP_TYPE_TREBUCHET;
        internal static bool IsSiege(eChimps type)=>type==eChimps.CHIMP_TYPE_CATAPULT || type==eChimps.CHIMP_TYPE_TREBUCHET ||
            type==eChimps.CHIMP_TYPE_SIEGE_TOWER || type==eChimps.CHIMP_TYPE_BATTERING_RAM ||
            type==eChimps.CHIMP_TYPE_PORTABLE_SHIELD || type==eChimps.CHIMP_TYPE_ARAB_BALLISTA;
    }
}
