using SHCDESE.Interop;

namespace SkinTest
{
    internal enum SkinFrameChoice
    {
        None,
        Normal,
        Alternate
    }

    internal static class SkinSelectionPolicy
    {
        public static bool IsEuropeanLordMaterial(GM material)
        {
            return material == GM.GM_BODY_LORD ||
                   material == GM.GM_BODY_SCRIBE_LORD ||
                   material == GM.GM_BODY_LORD_FEMALE ||
                   material == GM.GM_BODY_LORD_BESSY;
        }

        public static bool HasEligibleOwner(bool unitFound, int ownerPlayerId, int lordUnitId, bool lordFound, bool europeanLord)
        {
            return unitFound && ownerPlayerId > 0 && lordUnitId > 0 && lordFound && europeanLord;
        }

        public static SkinFrameChoice SelectFrame(bool alternateRequested, bool normalAvailable, bool alternateAvailable)
        {
            if (alternateRequested && alternateAvailable)
                return SkinFrameChoice.Alternate;
            return normalAvailable ? SkinFrameChoice.Normal : SkinFrameChoice.None;
        }

        public static bool CanReplaceVanilla(bool isSwordsman, bool expectedVanillaSprite, bool eligibleOwner, SkinFrameChoice frame)
        {
            return isSwordsman && expectedVanillaSprite && eligibleOwner && frame != SkinFrameChoice.None;
        }
    }
}
