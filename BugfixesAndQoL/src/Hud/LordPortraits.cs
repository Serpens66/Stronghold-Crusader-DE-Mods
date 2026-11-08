using APIShared;
using Noesis;

namespace BugfixesAndQoL
{
    internal static class LordPortraits
    {
        // Atlas rows follow LordSkin, starting at Crusader. Column zero is uncoloured,
        // followed by the eight Vanilla UI colours in PlayerColours order.
        private const int CellWidth = 72;
        private const int CellHeight = 123;
        private const int SkinCount = 8;
        private const int ColourCount = 9;
        private static readonly ImageSource[,] portraits = new ImageSource[SkinCount, ColourCount];

        internal static ImageSource Get(LordSkin skin, int colour)
        {
            int row = (int)skin - (int)LordSkin.Crusader;
            if (colour < 0 || colour >= ColourCount) colour = 0;
            if (row >= 0 && row < SkinCount && portraits[row, colour] is ImageSource cached)
                return cached;

            var resources = GUI.GetApplicationResources();
            if (resources == null) return null;
            if (row >= 0 && row < SkinCount && resources.Contains("BugfixesAndQoL-LordSource") &&
                resources["BugfixesAndQoL-LordSource"] is BitmapSource atlas)
            {
                var rect = new Int32Rect(colour * CellWidth, row * CellHeight, CellWidth, CellHeight);
                return portraits[row, colour] = new CroppedBitmap(atlas, rect);
            }
            return resources.Contains("shield_crown") ? resources["shield_crown"] as ImageSource : null;
        }
    }
}
