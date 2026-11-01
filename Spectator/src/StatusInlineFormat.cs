using Noesis;

namespace Spectator
{
    // Footer emphasis belongs to the actual runs. Tooltips retain their inherited styling.
    internal static class StatusInlineFormat
    {
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
