using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using APIShared.ModSettings;
using Noesis;
using SHCDESE.API;

namespace UiAcceptance
{
    // Measures the real opened Noesis visual tree; never supplies a font, reference
    // or sizing callback. args: owner name, command:<parameter>, side:<tooltip text>
    // or overview:<tooltip text>, [logical size=15], [open|hover|hover-right], [screenshot label].
    internal static class SpectatorTooltipPixels
    {
        internal static IEnumerable<Wait> Run(Step step)
        {
            string selector = step.Arg(0);
            bool sideHud = selector.StartsWith("side:", StringComparison.Ordinal);
            bool overview = selector.StartsWith("overview:", StringComparison.Ordinal);
            string interaction = step.Arg(2, "open");
            bool rightClick = interaction == "hover-right";
            bool hover = interaction == "hover" || rightClick;
            string screenshot = step.Arg(3, "");
            int logical = step.Int(1, 15);
            Runner.Require(logical > 0, "tooltip logical size must be positive");
            FrameworkElement source = null, owner = null;
            yield return new Until("visible tooltip owner " + selector, () =>
            {
                source = overview ? OverviewSettings() : GameXAMLManagerAPI.Instance.FindGlobalElement(sideHud
                    ? "APISharedHudExtrasButtonsHost" : "SpectatorEditorHumanPanel");
                return source != null && source.IsVisible && (owner = FindOwner(source, selector)) != null;
            });
            if (sideHud || overview) source = owner;
            var tip = ToolTipService.GetToolTip(owner) as ToolTip;
            Runner.Require(tip != null, "owner " + selector + " has no explicit ToolTip");
            UIElement previousTarget = tip.PlacementTarget;
            bool previousOpen = tip.IsOpen;
            NoesisView view = hover ? FatControler.instance?.NGview : null;
            Runner.Require(!hover || view != null, "no Noesis view for hover");
            bool previousMouse = view?.EnableMouse == true;
            Point outside = default, center = default;
            try
            {
                DumpState(selector, "before", source, owner, tip);
                tip.IsOpen = false;
                if (hover)
                {
                    // Send events to the game view without moving the Windows cursor.
                    view.EnableMouse = false;
                    outside = owner.PointToScreen(new Point(owner.ActualWidth + 6, owner.ActualHeight / 2));
                    center = owner.PointToScreen(new Point(owner.ActualWidth / 2, owner.ActualHeight / 2));
                    view.MouseMove((int)outside.X, (int)outside.Y);
                    yield return NextFrame.Instance;
                    view.MouseMove((int)center.X, (int)center.Y);
                }
                else
                {
                    tip.PlacementTarget = owner;
                    tip.IsOpen = true;
                }
                // Wait for the pointer delay and popup attachment, then two layout frames.
                yield return new Until("opened, loaded tooltip " + selector, () => tip.IsOpen && tip.IsLoaded && tip.IsVisible);
                yield return NextFrame.Instance;
                yield return NextFrame.Instance;
                DumpState(selector, "loaded", source, owner, tip);
                Runner.Require(tip.IsOpen && tip.IsLoaded && tip.IsVisible,
                    "tooltip " + selector + " did not become open/loaded/visible");

                float sourceScale = ScaleY(source);
                float expectedPixels = logical * sourceScale;
                Runner.Require(Positive(expectedPixels), "invalid source screen scale for " + selector);
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
                    " source units (expected " + Number(expectedPixels) + " physical pixels; tolerance 0.5)");
                if (!string.IsNullOrEmpty(screenshot))
                {
                    // The layout is already measurable during Noesis's fade-in.
                    yield return new Delay(300);
                    Runner.Require(tip.IsOpen, "tooltip closed before screenshot capture: " + selector);
                    GameSteps.CaptureScreenshot(screenshot);
                    yield return new Delay(500);
                    Runner.Require(tip.IsOpen, "tooltip closed during screenshot capture: " + selector);
                }
                if (rightClick)
                {
                    view.MouseButtonDown((int)center.X, (int)center.Y, MouseButton.Right);
                    view.MouseButtonUp((int)center.X, (int)center.Y, MouseButton.Right);
                    yield return new Until("right-click opened overview settings and closed the tooltip",
                        () => !tip.IsOpen && OverviewSettings()?.IsVisible == true);
                }
                else if (hover)
                {
                    view.MouseMove((int)outside.X, (int)outside.Y);
                    yield return new Until("mouse leave closed " + selector, () => !tip.IsOpen);
                }
            }
            finally
            {
                try { tip.IsOpen = false; tip.PlacementTarget = previousTarget; if (previousOpen) tip.IsOpen = true; }
                catch (Exception error) { AcceptLog.Warn("tooltip test cleanup failed: " + error.Message); }
                finally { if (view != null) view.EnableMouse = previousMouse; }
            }
        }

        private static FrameworkElement OverviewSettings() => GameSteps.FindType("Spectator.LordOverview.OverviewRuntime")?
            .GetField("settingsWindow", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as FrameworkElement;

        private static FrameworkElement FindOwner(FrameworkElement source, string selector)
        {
            bool command = selector.StartsWith("command:", StringComparison.Ordinal);
            bool tooltipText = selector.StartsWith("side:", StringComparison.Ordinal) || selector.StartsWith("overview:", StringComparison.Ordinal);
            string value = command || tooltipText ? selector.Substring(selector.IndexOf(':') + 1) : selector;
            foreach (var node in Visuals(source))
            {
                if (!(node is FrameworkElement element) || !element.IsVisible || element.ActualWidth <= 0) continue;
                if (tooltipText)
                {
                    if (ToolTipService.GetToolTip(element) is ToolTip tip && Equals(tip.Content, value)) return element;
                    continue;
                }
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
            Point origin = tip.IsLoaded ? tip.PointToScreen(new Point(0, 0)) : new Point(float.NaN, float.NaN);
            AcceptLog.Info("TOOLTIP_STATE owner=" + selector + " phase=" + phase + " ownerName=" + owner.Name +
                " sharedEnabled=" + SharedToolTipSizing.GetEnabled(tip) + " reference=" + reference?.Name +
                " referenceFont=" + Number(SharedToolTipSizing.GetReferenceFontSize(tip)) +
                " tipFont=" + Number(tip.FontSize) + " open=" + tip.IsOpen + " loaded=" + tip.IsLoaded +
                " visible=" + tip.IsVisible + " tipScaleY=" + Number(ScaleY(tip)) +
                " sourceScaleY=" + Number(ScaleY(source)) + " screenOrigin=" + Number(origin.X) + "," + Number(origin.Y) +
                " contentType=" + tip.Content?.GetType().FullName);
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
