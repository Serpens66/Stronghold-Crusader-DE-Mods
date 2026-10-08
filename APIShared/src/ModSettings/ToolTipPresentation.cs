namespace APIShared.ModSettings
{
    /// <summary>Shared Noesis tooltip sizes for mod settings. External XAML can use these values without linking Shared sources.</summary>
    public static class ToolTipPresentation
    {
        /// <summary>The established common tooltip font size, as the float required by Noesis.</summary>
        public static float FontSize => APIShared.Internal.ToolTipPresentation.FontSize;
        /// <summary>The established common maximum width, as the float required by Noesis.</summary>
        public static float MaximumWidth => APIShared.Internal.ToolTipPresentation.MaximumWidth;
        /// <summary>Resolution-sensitive font size. Read on the Unity thread.</summary>
        public static float AutomaticFontSize => APIShared.Internal.ToolTipPresentation.AutomaticFontSize;
        /// <summary>Resolution-sensitive maximum width. Read on the Unity thread.</summary>
        public static float AutomaticMaximumWidth => APIShared.Internal.ToolTipPresentation.AutomaticMaximumWidth;
    }
}
