using SHCDESE.Interop;

namespace ImprovedHunters
{
    internal static class ManualChickenAttackPolicy
    {
        /// <summary>
        /// Identifies non-Hunter units whose regular combat path launches a
        /// projectile. Hunters bypass the compatibility rejection in Vanilla.
        /// </summary>
        public static bool CanOverrideCompatibilityRejection(eChimps attackerType)
        {
            switch (attackerType)
            {
                case eChimps.CHIMP_TYPE_ARCHER:
                case eChimps.CHIMP_TYPE_XBOWMAN:
                case eChimps.CHIMP_TYPE_ARCHER_debug:
                case eChimps.CHIMP_TYPE_CATAPULT:
                case eChimps.CHIMP_TYPE_TREBUCHET:
                case eChimps.CHIMP_TYPE_MANGONEL:
                case eChimps.CHIMP_TYPE_BALLISTA:
                case eChimps.CHIMP_TYPE_ARAB_BOW:
                case eChimps.CHIMP_TYPE_ARAB_SLINGER:
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                case eChimps.CHIMP_TYPE_ARAB_GRENADIER:
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                case eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                    return true;
                default:
                    return false;
            }
        }
    }
}
