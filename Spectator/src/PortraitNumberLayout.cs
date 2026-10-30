using System;
using Noesis;

namespace Spectator
{
    // A line box contains font ascent/descent space. Anchor both rendered contours
    // from the text bounds instead of bottom-aligning that box inside the portrait.
    internal sealed class PortraitNumberLayout
    {
        private readonly TextBlock number, outline;
        private readonly FrameworkElement host;
        private bool dirty = true;

        internal PortraitNumberLayout(TextBlock number, TextBlock outline)
        {
            if (number == null || outline == null) throw new MissingMemberException("Portrait number layers missing.");
            this.number = number; this.outline = outline;
            host = VisualTreeHelper.GetParent(number) as FrameworkElement;
            if (host == null || outline == null) throw new MissingMemberException("Portrait number layers missing.");
            host.Loaded += OnLoaded;
            host.SizeChanged += OnSize;
            outline.SizeChanged += OnSize;
        }

        private void OnLoaded(object sender, RoutedEventArgs args) => dirty = true;
        private void OnSize(object sender, SizeChangedEventArgs args) => dirty = true;
        internal void Invalidate() => dirty = true;

        internal void Refresh()
        {
            if (!dirty || !host.IsLoaded || !host.IsVisible || host.ActualHeight <= 0) return;
            var formatted = outline.FormattedText;
            if (formatted == null) return;
            Rect ink = formatted.Bounds;
            if (ink.Width <= 0 || ink.Height <= 0) return;
            ToolbarLayout.NumberOffset(host.ActualHeight, ink.X, ink.Bottom, out float x, out float y);
            number.RenderTransform = new TranslateTransform(x, y);
            outline.RenderTransform = new TranslateTransform(x, y);
            dirty = false;
        }

        internal void Detach()
        {
            host.Loaded -= OnLoaded;
            host.SizeChanged -= OnSize;
            outline.SizeChanged -= OnSize;
        }
    }
}
