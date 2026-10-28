using System;
using System.Collections.Generic;
using Noesis;

namespace Spectator
{
    /// <summary>Opt-in for toolbar popups, including lazily created scroll content.
    /// Uses the menu's rendered scale rather than a second resolution preset. UI thread only.</summary>
    public static class MenuToolTipSizing
    {
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(MenuToolTipSizing), new PropertyMetadata(false, Changed));
        public static bool GetEnabled(DependencyObject value) => (bool)value.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject value, bool enabled) => value.SetValue(EnabledProperty, enabled);
        private static FrameworkElement reference, viewport;
        private static readonly HashSet<ToolTip> openTips = new HashSet<ToolTip>();

        internal static void SetReference(FrameworkElement value, FrameworkElement canvas = null)
        {
            if (ReferenceEquals(reference, value) && ReferenceEquals(viewport, canvas ?? value)) return;
            CloseAll();
            reference = value; viewport = canvas ?? value;
        }

        internal static void CloseAll()
        {
            // Closed removes entries synchronously; never mutate the set being enumerated.
            foreach (var tip in new List<ToolTip>(openTips)) tip.IsOpen = false;
            openTips.Clear();
        }

        private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs args)
        {
            if (!(value is ToolTip tip)) return;
            tip.Opened -= Opened;
            tip.Loaded -= Opened;
            tip.SizeChanged -= SizeChanged;
            tip.Closed -= Closed;
            openTips.Remove(tip);
            if (!(bool)args.NewValue) { tip.IsOpen = false; return; }
            SHCDESE.UI.ToolTipResolutionScale.SetEnabled(tip, false);
            tip.FontSize = ToolbarGeometry.MenuFontSize;
            tip.MaxWidth = 420;
            tip.Padding = new Thickness(10, 8, 10, 8);
            tip.BorderThickness = new Thickness(1.5f);
            tip.Opened += Opened;
            tip.Loaded += Opened;
            tip.SizeChanged += SizeChanged;
            tip.Closed += Closed;
        }

        private static void Closed(object sender, RoutedEventArgs args) => openTips.Remove((ToolTip)sender);
        private static void SizeChanged(object sender, SizeChangedEventArgs args) => Opened(sender, null);
        private static void Opened(object sender, RoutedEventArgs args)
        {
            var tip = (ToolTip)sender;
            if (!tip.IsOpen) return;
            if (reference == null || !reference.IsVisible) { tip.IsOpen = false; return; }
            openTips.Add(tip);
            if (!tip.IsVisible) return; // Loaded/SizeChanged retries after the popup has joined its view.
            var surface = tip.PlacementTarget as FrameworkElement ?? reference;
            if (!surface.IsVisible) { tip.IsOpen = false; return; }
            Point menuOrigin = surface.PointToScreen(new Point(0, 0));
            Point menuUnit = surface.PointToScreen(new Point(100, 100));
            Point popupOrigin = tip.PointToScreen(new Point(0, 0));
            Point popupUnit = tip.PointToScreen(new Point(100, 100));
            if (!ToolbarGeometry.TryTooltipScale(menuUnit.X - menuOrigin.X, menuUnit.Y - menuOrigin.Y,
                popupUnit.X - popupOrigin.X, popupUnit.Y - popupOrigin.Y, out float x, out float y)) return;
            float font = ToolbarGeometry.MenuFontSize * y;
            float width = Math.Max(1, Math.Min(420, viewport.ActualWidth - 20)) * x;
            // Idempotent layout handler: only write changed values, never register a frame callback.
            if (Math.Abs(tip.FontSize - font) > 0.01f) tip.FontSize = font;
            if (Math.Abs(tip.MaxWidth - width) > 0.01f) tip.MaxWidth = width;
            var padding = new Thickness(10 * x, 8 * y, 10 * x, 8 * y);
            if (!tip.Padding.Equals(padding)) tip.Padding = padding;
            var border = new Thickness(1.5f * x, 1.5f * y, 1.5f * x, 1.5f * y);
            if (!tip.BorderThickness.Equals(border)) tip.BorderThickness = border;
            if (tip.Content is TextBlock text)
            {
                if (Math.Abs(text.FontSize - font) > 0.01f) text.FontSize = font;
                float contentWidth = Math.Max(1, width - 23 * x);
                if (Math.Abs(text.MaxWidth - contentWidth) > 0.01f) text.MaxWidth = contentWidth;
            }
        }
    }
}
