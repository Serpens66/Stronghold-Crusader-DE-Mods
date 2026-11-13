using System;

namespace BugfixesAndQoL
{
    internal readonly struct ForeignTroopPlayerColor
    {
        internal readonly int PortraitColorId;
        internal readonly uint LabelArgb;

        internal ForeignTroopPlayerColor(int portraitColorId, uint labelArgb)
        {
            PortraitColorId = portraitColorId;
            LabelArgb = labelArgb;
        }

        internal static readonly ForeignTroopPlayerColor Neutral =
            new ForeignTroopPlayerColor(0, 0xFFD2CCBC);
    }

    // Render-thread only. Never retain a player's mapping across HUD refreshes.
    internal sealed class ForeignTroopPlayerColors
    {
        private readonly ForeignTroopPlayerColor[] colors = new ForeignTroopPlayerColor[9];
        private readonly bool[] captured = new bool[9];

        internal void Reset()
        {
            Array.Clear(colors, 0, colors.Length);
            Array.Clear(captured, 0, captured.Length);
        }

        internal ForeignTroopPlayerColor Get(int owner)
        {
            if (owner < 1 || owner >= colors.Length) return ForeignTroopPlayerColor.Neutral;
            if (!captured[owner])
            {
                colors[owner] = Capture(owner);
                captured[owner] = true;
            }
            return colors[owner];
        }

        private static ForeignTroopPlayerColor Capture(int owner)
        {
            int mappedOwner = SpriteMapping.RemapMPLoadedColour(owner);
            int[] mapping = SpriteMapping.remapColours;
            if (mapping == null || mappedOwner < 1 || mappedOwner >= mapping.Length)
                return ForeignTroopPlayerColor.Neutral;
            int paletteId = mapping[mappedOwner];
            int[] portraitMapping = Platform_Multiplayer.Instance?.UIColourRemap;
            if (paletteId < 1 || paletteId > 8 || portraitMapping == null ||
                paletteId >= portraitMapping.Length || spriteLoader.instance == null)
                return ForeignTroopPlayerColor.Neutral;
            int portraitId = portraitMapping[paletteId];
            if (portraitId < 1 || portraitId > 8) return ForeignTroopPlayerColor.Neutral;

            // Vanilla's sprite palette and UI portrait order are different domains.
            spriteLoader.instance.GetGMMaterial(Enums.GM.GM_BODY_ARCHER, paletteId, 0,
                out UnityEngine.Color tint, 0);
            uint argb = 0xFF000000u | ((uint)(byte)(tint.r * 255f) << 16) |
                ((uint)(byte)(tint.g * 255f) << 8) | (byte)(tint.b * 255f);
            return new ForeignTroopPlayerColor(portraitId, argb);
        }
    }
}
