using Noesis;

namespace Spectator
{
    internal sealed class ToolbarPalette
    {
        internal readonly Brush Ink, Button, Hover, Border, Active, Input, Grip, GripHover, Force;
        internal static readonly ToolbarPalette Paper = new ToolbarPalette(false), Classic = new ToolbarPalette(true);
        private ToolbarPalette(bool classic)
        {
            Ink = Brush(classic ? 0xFFF2EAC2 : 0xFF37200E);
            Button = Brush(classic ? 0x780F0C0A : 0x88764B28);
            Hover = Brush(classic ? 0xA04A2118 : 0xAAAE7840);
            Border = Brush(classic ? 0xB072241C : 0x99744221);
            Active = Brush(classic ? 0xA04A2518 : 0x70AD7540);
            Input = Brush(classic ? 0xFF271610 : 0xFFFFE8B8);
            Grip = Brush(classic ? 0xFF6F1712 : 0xFFB58B51);
            Force = Brush(classic ? 0xFF795A40 : 0xFFF9E6BC);
            GripHover = Brush(classic ? 0xFF913025 : 0xFFD8B477);
        }
        private static Brush Brush(uint value) => new SolidColorBrush(Color.FromArgb(
            (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value));
    }
}
