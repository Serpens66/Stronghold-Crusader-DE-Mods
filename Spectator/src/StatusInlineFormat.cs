using Noesis;

namespace Spectator
{
    // Footer emphasis belongs to the actual runs. Tooltips retain their inherited styling.
    internal static class StatusInlineFormat
    {
        private static readonly Brush transparent = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        private static readonly Brush shadowInk = new SolidColorBrush(Color.FromArgb(255, 20, 15, 10));
        // Match the existing Lord overview's subtle dark text shadow. For mixed
        // text, only the separate rear layer receives this effect.
        internal static DropShadowEffect Shadow() => new DropShadowEffect {
            ShadowDepth = 1, BlurRadius = 1, Color = Color.FromArgb(230, 20, 15, 10)
        };

        // The full text remains in both layers, so transparent prose retains every
        // advance and wrap opportunity. Only player-coloured glyphs cast a shadow.
        internal static Run CreateShadow(TextBlock owner, string value, Brush colour, bool emphasize)
        {
            var run = Create(owner, value, colour, emphasize);
            run.Foreground = run.Stroke = colour == null ? transparent : shadowInk;
            return run;
        }

        internal static void BindShadow(TextBlock shadow, TextBlock owner)
        {
            owner.Effect = null;
            shadow.Effect = Shadow(); shadow.IsHitTestVisible = false;
            // Ellipsis/fallback glyphs must not acquire the default black fill.
            shadow.Foreground = transparent;
            Mirror(shadow, owner, TextBlock.FontFamilyProperty, nameof(TextBlock.FontFamily));
            Mirror(shadow, owner, TextBlock.FontSizeProperty, nameof(TextBlock.FontSize));
            Mirror(shadow, owner, TextBlock.FontStretchProperty, nameof(TextBlock.FontStretch));
            Mirror(shadow, owner, TextBlock.FontStyleProperty, nameof(TextBlock.FontStyle));
            Mirror(shadow, owner, TextBlock.FontWeightProperty, nameof(TextBlock.FontWeight));
            Mirror(shadow, owner, TextBlock.LineHeightProperty, nameof(TextBlock.LineHeight));
            Mirror(shadow, owner, TextBlock.LineStackingStrategyProperty, nameof(TextBlock.LineStackingStrategy));
            Mirror(shadow, owner, TextBlock.TextWrappingProperty, nameof(TextBlock.TextWrapping));
            Mirror(shadow, owner, TextBlock.TextTrimmingProperty, nameof(TextBlock.TextTrimming));
            Mirror(shadow, owner, TextBlock.TextAlignmentProperty, nameof(TextBlock.TextAlignment));
            Mirror(shadow, owner, TextBlock.PaddingProperty, nameof(TextBlock.Padding));
            Mirror(shadow, owner, FrameworkElement.MarginProperty, nameof(FrameworkElement.Margin));
            Mirror(shadow, owner, FrameworkElement.WidthProperty, nameof(FrameworkElement.Width));
            Mirror(shadow, owner, FrameworkElement.HeightProperty, nameof(FrameworkElement.Height));
            Mirror(shadow, owner, FrameworkElement.MaxWidthProperty, nameof(FrameworkElement.MaxWidth));
            Mirror(shadow, owner, FrameworkElement.MaxHeightProperty, nameof(FrameworkElement.MaxHeight));
            Mirror(shadow, owner, FrameworkElement.HorizontalAlignmentProperty, nameof(FrameworkElement.HorizontalAlignment));
            Mirror(shadow, owner, FrameworkElement.VerticalAlignmentProperty, nameof(FrameworkElement.VerticalAlignment));
            Mirror(shadow, owner, FrameworkElement.FlowDirectionProperty, nameof(FrameworkElement.FlowDirection));
            Mirror(shadow, owner, UIElement.VisibilityProperty, nameof(UIElement.Visibility));
        }

        private static void Mirror(TextBlock shadow, TextBlock owner, DependencyProperty property, string name) =>
            BindingOperations.SetBinding(shadow, property, new Binding(name) { Source = owner });

        internal static Run Create(TextBlock owner, string value, Brush colour, bool emphasize)
        {
            var run = new Run { Text = value };
            if (colour != null) run.Foreground = colour;
            if (!emphasize) return run;
            run.FontSize = 14;
            run.FontWeight = FontWeight.Bold;
            run.StrokeThickness = .25f;
            if (colour != null) run.Stroke = colour;
            else
                // Follow palette changes immediately, including a theme switch during dragging.
                BindingOperations.SetBinding(run, TextElement.StrokeProperty,
                    new Binding(nameof(TextBlock.Foreground)) { Source = owner });
            return run;
        }
    }
}
