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
            var shadow=StatusInlineFormat.Shadow();
            check(shadow.ShadowDepth==1 && shadow.BlurRadius==1 && shadow.Color.A==230 &&
                shadow.Color.R==20 && shadow.Color.G==15 && shadow.Color.B==10,
                "toolbar coloured text uses the subtle existing overview shadow on its owning TextBlock");
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
                var plainShadow = StatusInlineFormat.CreateShadow(owner, value, null, true);
                var playerShadow = StatusInlineFormat.CreateShadow(owner, value, red, true);
                check(plainShadow.Text == plain.Text && playerShadow.Text == player.Text &&
                    plainShadow.FontSize == plain.FontSize && playerShadow.FontSize == player.FontSize &&
                    plainShadow.FontWeight == plain.FontWeight && playerShadow.FontWeight == player.FontWeight &&
                    plainShadow.StrokeThickness == plain.StrokeThickness && playerShadow.StrokeThickness == player.StrokeThickness,
                    "shadow layer preserves the entire prose/player text and its glyph metrics");
                check(((SolidColorBrush)plainShadow.Foreground).Color.A == 0 &&
                    ((SolidColorBrush)plainShadow.Stroke).Color.A == 0,
                    "uncoloured prose has transparent fill AND contour in the rear layer");
                check(((SolidColorBrush)playerShadow.Foreground).Color.A == 255 &&
                    ReferenceEquals(playerShadow.Foreground, playerShadow.Stroke),
                    "only player-coloured names/numbers produce an opaque rear glyph and matching shadow contour");
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
                    var ordinaryShadow = StatusInlineFormat.CreateShadow(owner, value, colour, false);
                    check(ordinaryShadow.ReadLocalValue(TextElement.FontSizeProperty) == DependencyProperty.UnsetValue &&
                        ordinaryShadow.ReadLocalValue(TextElement.FontWeightProperty) == DependencyProperty.UnsetValue && ordinaryShadow.Text == value,
                        "popup shadow fragments inherit the same live tooltip font as their front fragments");
                }
                owner.Foreground = paperInk;
            }
            var rear = new TextBlock();
            owner.Effect = shadow;
            StatusInlineFormat.BindShadow(rear, owner);
            check(owner.Effect == null && rear.Effect != null && !rear.IsHitTestVisible && ((SolidColorBrush)rear.Foreground).Color.A == 0,
                "main text remains unfiltered and the selective rear layer never owns a click surface");
            owner.FontSize = 15; owner.FontWeight = FontWeight.Bold;
            owner.Width = 308; owner.Height = 33; owner.MaxHeight = 32; owner.MaxWidth = 420;
            owner.LineHeight = 16; owner.TextWrapping = "Wrap"; owner.TextTrimming = "CharacterEllipsis";
            owner.Padding = new Thickness(2); owner.Margin = new Thickness(4,0,0,0);
            check(rear.FontSize == 15 && rear.FontWeight == owner.FontWeight && rear.Width == 308 && rear.Height == 33 &&
                rear.MaxHeight == 32 && rear.MaxWidth == 420 && rear.LineHeight == 16 &&
                Equals(rear.TextWrapping, owner.TextWrapping) && Equals(rear.TextTrimming, owner.TextTrimming) &&
                rear.Padding.Left == 2 && rear.Margin.Left == 4,
                "live rear metrics follow the owning footer/popup font, wrap, ellipsis, width, height and spacing");
            owner.FontSize = 30; owner.Width = 352;
            check(rear.FontSize == 30 && rear.Width == 352,
                "later tooltip scaling and Goods/Orders layout changes cannot desynchronize the two glyph layers");
        }
    }
}
