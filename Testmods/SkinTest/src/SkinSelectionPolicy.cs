using SHCDESE.Interop;

namespace SkinTest
{
    internal enum SkinFrameChoice
    {
        None,
        Normal,
        Alternate
    }

    internal enum LordCulture
    {
        Unknown,
        European,
        NonEuropean
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

        public static LordCulture ClassifyLordMaterial(GM material)
        {
            if (IsEuropeanLordMaterial(material))
                return LordCulture.European;
            return material == GM.GM_BODY_ARABIC_LORD ||
                   material == GM.GM_BODY_BEDOUIN_LORD ||
                   material == GM.GM_BODY_ARABIC_LORD_FEMALE ||
                   material == GM.GM_BODY_BEDOUIN_LORD_FEMALE
                ? LordCulture.NonEuropean
                : LordCulture.Unknown;
        }

        public static LordCulture ClassifyLordGraphicsType(int graphicsType)
        {
            switch (graphicsType)
            {
                case 0:
                case 3:
                case 4:
                case 5:
                    return LordCulture.European;
                case 1:
                case 2:
                case 6:
                case 7:
                    return LordCulture.NonEuropean;
                default:
                    return LordCulture.Unknown;
            }
        }

        public static bool HasEligibleOwner(bool unitFound, int ownerPlayerId, LordCulture culture)
        {
            return unitFound && ownerPlayerId > 0 && culture == LordCulture.European;
        }

        public static LordCulture ReconcileEarlyAndActualCulture(LordCulture earlyCulture, LordCulture actualCulture)
        {
            return actualCulture == LordCulture.Unknown ? earlyCulture : actualCulture;
        }

        public static bool ShouldUseEuropeanHud(bool activeMap, bool arabicHud, int colour, LordCulture culture)
        {
            return activeMap && !arabicHud && colour >= 1 && colour <= 8 && culture == LordCulture.European;
        }

        public static bool CanInspectEuropeanHud(bool activeMap, bool arabicHud, int colour,
            bool currentIsVanilla)
        {
            return activeMap && !arabicHud && colour >= 1 && colour <= 8 &&
                   currentIsVanilla;
        }

        public static bool IsValidPlayerId(int playerId) => playerId >= 1 && playerId <= 8;

        public static bool CanReplaceBuilding(bool expectedVanillaSprite, bool isRoundTower,
            int ownerPlayerId, LordCulture culture, bool frameAvailable)
        {
            return expectedVanillaSprite && isRoundTower && ownerPlayerId > 0 &&
                   culture == LordCulture.European && frameAvailable;
        }

        public static int ToAtlasFrameIndex(int gameImage)
        {
            // SpriteMapping.getBodyImage converts body-image IDs to zero-based atlas slots.
            return gameImage > 0 ? gameImage - 1 : -1;
        }

        public static int ToCastlePreviewAtlasIndex(int gameImage)
        {
            // SpriteMapping.getBodyImage applies the same one-based conversion to GM_CASTLES previews.
            return gameImage > 0 ? gameImage - 1 : -1;
        }

        public static bool CanReplaceRoundTowerPreview(bool activeMap, int currentAction,
            int currentSubAction, bool exactCursorRenderer, bool expectedVanillaSprite,
            LordCulture culture, bool frameAvailable)
        {
            return activeMap && currentAction == 5 && currentSubAction == 114 &&
                   exactCursorRenderer && expectedVanillaSprite &&
                   culture == LordCulture.European && frameAvailable;
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
