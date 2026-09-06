using Noesis;

namespace Shared
{
    public static class ToolTipPresentation
    {
        private const int FourKMinimumHeight = 1800;
        private const int FourteenFortyMinimumHeight = 1300;

        // Noesis FontSize and MaxWidth are floats. Returning the exact CLR type is
        // required because x:Static values are not converted like XAML literals.
        public static float FontSize => 50.0f;

        public static float MaximumWidth => 1000.0f;

        public static int CurrentScreenWidth => UnityEngine.Screen.width;

        public static int CurrentScreenHeight => UnityEngine.Screen.height;

        public static float AutomaticFontSize => IsFourK ? 45.0f : IsFourteenForty ? 30.0f : 23.0f;

        public static float AutomaticMaximumWidth => IsFourK ? 1380.0f : IsFourteenForty ? 1020.0f : 780.0f;

        public static Thickness AutomaticPadding => IsFourK
            ? new Thickness(39.0f, 30.0f, 39.0f, 30.0f)
            : IsFourteenForty
                ? new Thickness(30.0f, 22.5f, 30.0f, 22.5f)
                : new Thickness(24.0f, 18.0f, 24.0f, 18.0f);

        public static Thickness AutomaticBorderThickness =>
            new Thickness(IsFourK ? 7.5f : IsFourteenForty ? 6.0f : 4.5f);

        private static bool IsFourK => CurrentScreenHeight >= FourKMinimumHeight;

        private static bool IsFourteenForty => CurrentScreenHeight >= FourteenFortyMinimumHeight;
    }
}
