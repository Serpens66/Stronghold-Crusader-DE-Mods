using System;
using Noesis;

namespace Spectator
{
    // Executes the actual run factory against the existing Noesis boundary double.
    // This catches explicit/inherited formatting regressions, not font rasterization.
    internal static class StatusInlineFormatTests
    {
        internal static void Run(Action<bool, string> check)
        {
            var paperInk = new Brush(); var classicInk = new Brush();
            var red = new Brush(); var green = new Brush();
            var owner = new TextBlock { FontSize = 11, Foreground = paperInk };
            foreach (string value in new[] { "Credited 2147483647 gold to ", "1 · Rat, Duc de Puce", "Order cancelled.", "Select a target below.", "Action failed.", ".", new string('W', 300) })
            {
                var plain = StatusInlineFormat.Create(owner, value, null, true);
                var player = StatusInlineFormat.Create(owner, value, red, true);
                foreach (var run in new[] { plain, player })
                    check(run.Text == value && run.FontSize == 14 && run.FontWeight == FontWeight.Bold && run.StrokeThickness == .25f,
                        "every footer fragment explicitly owns 14-unit bold glyphs and the thin emphasis contour, without shortening its text");
                check(ReferenceEquals(plain.Stroke, paperInk), "plain emphasis matches the current parchment ink");
                check(ReferenceEquals(player.Foreground, red) && ReferenceEquals(player.Stroke, red), "player fill and contour use the same brush");
                owner.Foreground = classicInk;
                check(ReferenceEquals(plain.Stroke, classicInk) && ReferenceEquals(player.Stroke, red), "live palette changes affect plain strokes immediately, leaving player colours intact");
                var recoloured = StatusInlineFormat.Create(owner, value, green, true);
                check(ReferenceEquals(recoloured.Stroke, green) && ReferenceEquals(recoloured.Foreground, green), "rebuilding after a player-colour change updates both glyph layers");
                foreach (Brush colour in new[] { null, red })
                {
                    var ordinary = StatusInlineFormat.Create(owner, value, colour, false);
                    check(ordinary.ReadLocalValue(TextElement.FontSizeProperty) == DependencyProperty.UnsetValue &&
                        ordinary.ReadLocalValue(TextElement.FontWeightProperty) == DependencyProperty.UnsetValue &&
                        ordinary.ReadLocalValue(TextElement.StrokeProperty) == DependencyProperty.UnsetValue &&
                        ordinary.ReadLocalValue(TextElement.StrokeThicknessProperty) == DependencyProperty.UnsetValue,
                        "tooltips keep their inherited formatting, including delayed tooltip sizing");
                    check(ReferenceEquals(ordinary.Foreground, colour) && ordinary.Text == value, "ordinary fragments preserve full text and player colours");
                }
                owner.Foreground = paperInk;
            }
        }
    }
}
