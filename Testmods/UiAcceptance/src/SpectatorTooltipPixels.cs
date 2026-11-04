using System;
using System.Collections.Generic;
using System.Globalization;
using APIShared.ModSettings;
using Noesis;
using SHCDESE.API;

namespace UiAcceptance
{
    // Measures the real opened Noesis visual tree; never supplies a font, reference
    // or sizing callback. args: owner name or command:<parameter>, [logical size=15].
    internal static class SpectatorTooltipPixels
    {
        internal static IEnumerable<Wait> Run(Step step)
        {
            string selector = step.Arg(0);
            int logical = step.Int(1, 15);
            Runner.Require(logical > 0, "tooltip logical size must be positive");
            FrameworkElement source = null, owner = null;
            yield return new Until("visible Spectator tooltip owner " + selector, () =>
            {
                source = GameXAMLManagerAPI.Instance.FindGlobalElement("SpectatorEditorHumanPanel");
                return source != null && source.IsVisible && (owner = FindOwner(source, selector)) != null;
            });
            var tip = ToolTipService.GetToolTip(owner) as ToolTip;
            Runner.Require(tip != null, "owner " + selector + " has no explicit ToolTip");
            UIElement previousTarget = tip.PlacementTarget;
            bool previousOpen = tip.IsOpen;
            try
            {
                DumpState(selector, "before", source, owner, tip);
                tip.IsOpen = false;
                tip.PlacementTarget = owner;
                tip.IsOpen = true;
                // A short bounded frame wait lets the actual popup join its Noesis
                // view, followed by two full layout frames. No size is written here.
                for (int frames = 0; frames < 120 && (!tip.IsLoaded || !tip.IsVisible); frames++)
                    yield return NextFrame.Instance;
                yield return NextFrame.Instance;
                yield return NextFrame.Instance;
                DumpState(selector, "loaded", source, owner, tip);
                Runner.Require(tip.IsOpen && tip.IsLoaded && tip.IsVisible,
                    "tooltip " + selector + " did not become open/loaded/visible");

                float sourceScale = ScaleY(source);
                float expectedPixels = logical * sourceScale;
                Runner.Require(Positive(expectedPixels), "invalid toolbar screen scale for " + selector);
                var rendered = new List<TextBlock>();
                foreach (var node in Visuals(tip))
                    if (node is TextBlock text && text.IsLoaded && text.IsVisible &&
                        text.ActualWidth > 0 && text.ActualHeight > 0) rendered.Add(text);
                Runner.Require(rendered.Count > 0, "opened tooltip " + selector + " has no rendered TextBlock descendants");
                bool equal = true;
                for (int index = 0; index < rendered.Count; index++)
                {
                    TextBlock text = rendered[index];
                    float scale = ScaleY(text), pixels = text.FontSize * scale;
                    bool match = Positive(pixels) && Math.Abs(pixels - expectedPixels) <= .5f;
                    equal &= match;
                    AcceptLog.Info("TOOLTIP_PIXELS owner=" + selector + " textIndex=" + index +
                        " name=" + text.Name + " font=" + Number(text.FontSize) + " scaleY=" + Number(scale) +
                        " pixels=" + Number(pixels) + " expected=" + Number(expectedPixels) +
                        " match=" + match + " text=" + ShortText(text.Text));
                }
                FrameworkElement reference = SharedToolTipSizing.GetReferenceElement(tip);
                Runner.Require(SharedToolTipSizing.GetEnabled(tip) && reference == source &&
                    Math.Abs(SharedToolTipSizing.GetReferenceFontSize(tip) - logical) < .01f,
                    "tooltip " + selector + " does not use the common shared sizing reference/font");
                Runner.Require(equal, "tooltip " + selector + " rendered font differs from " + logical +
                    " toolbar units (expected " + Number(expectedPixels) + " physical pixels; tolerance 0.5)");
            }
            finally
            {
                try { tip.IsOpen = false; tip.PlacementTarget = previousTarget; if (previousOpen) tip.IsOpen = true; }
                catch (Exception error) { AcceptLog.Warn("tooltip test cleanup failed: " + error.Message); }
            }
        }

        private static FrameworkElement FindOwner(FrameworkElement source, string selector)
        {
            bool command = selector.StartsWith("command:", StringComparison.Ordinal);
            string value = command ? selector.Substring("command:".Length) : selector;
            foreach (var node in Visuals(source))
            {
                if (!(node is FrameworkElement element) || !element.IsVisible || element.ActualWidth <= 0) continue;
                if (command ? element is Button button && string.Equals(button.CommandParameter as string, value, StringComparison.Ordinal)
                    : string.Equals(element.Name, value, StringComparison.Ordinal)) return element;
            }
            return null;
        }

        private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            var pending = new Stack<DependencyObject>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                DependencyObject node = pending.Pop();
                yield return node;
                int children = VisualTreeHelper.GetChildrenCount(node);
                for (int index = children - 1; index >= 0; index--) pending.Push(VisualTreeHelper.GetChild(node, index));
            }
        }

        private static void DumpState(string selector, string phase, FrameworkElement source, FrameworkElement owner, ToolTip tip)
        {
            FrameworkElement reference = SharedToolTipSizing.GetReferenceElement(tip);
            AcceptLog.Info("TOOLTIP_STATE owner=" + selector + " phase=" + phase + " ownerName=" + owner.Name +
                " sharedEnabled=" + SharedToolTipSizing.GetEnabled(tip) + " reference=" + reference?.Name +
                " referenceFont=" + Number(SharedToolTipSizing.GetReferenceFontSize(tip)) +
                " tipFont=" + Number(tip.FontSize) + " open=" + tip.IsOpen + " loaded=" + tip.IsLoaded +
                " visible=" + tip.IsVisible + " tipScaleY=" + Number(ScaleY(tip)) +
                " sourceScaleY=" + Number(ScaleY(source)) + " contentType=" + tip.Content?.GetType().FullName);
        }

        private static float ScaleY(FrameworkElement element)
        {
            if (element == null || !element.IsLoaded || !element.IsVisible) return float.NaN;
            Point origin = element.PointToScreen(new Point(0, 0));
            Point unit = element.PointToScreen(new Point(0, 100));
            return (unit.Y - origin.Y) / 100;
        }
        private static bool Positive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string ShortText(string text)
        {
            string escaped = (text ?? "").Replace("\r", "\\r").Replace("\n", "\\n");
            return escaped.Length <= 100 ? escaped : escaped.Substring(0, 100) + "...";
        }
    }
}
