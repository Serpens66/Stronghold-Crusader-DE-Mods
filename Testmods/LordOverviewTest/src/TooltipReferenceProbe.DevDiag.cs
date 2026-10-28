#if SERP_DEVDIAG
using Noesis;

namespace LordOverviewTest
{
    /// <summary>Measures actually opened popups without changing their geometry or scaler.
    /// The settings styles are wrapped in place; no tab is selected and no detached view is laid out.</summary>
    public static class TooltipReferenceProbe
    {
        private sealed class State { internal bool Observing; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ToolTip, State> states =
            new System.Runtime.CompilerServices.ConditionalWeakTable<ToolTip, State>();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, object> views =
            new System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, object>();
        private static readonly System.Collections.Generic.HashSet<string> recorded =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        private static readonly System.Collections.Generic.Dictionary<string, int> counts =
            new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
        private static int started, pending;

        public static readonly DependencyProperty SurfaceProperty = DependencyProperty.RegisterAttached(
            "Surface", typeof(string), typeof(TooltipReferenceProbe), new PropertyMetadata("", Changed));
        public static string GetSurface(DependencyObject value) => (string)value.GetValue(SurfaceProperty);
        public static void SetSurface(DependencyObject value, string surface) => value.SetValue(SurfaceProperty, surface);

        internal static void Start()
        {
            if (System.Threading.Interlocked.Exchange(ref started, 1) != 0) return;
            // The SE collection and the static dispatcher callback survive plugin startup cleanup.
            SHCDESE.API.GameXAMLManagerAPI.Instance.RegisteredModSettings.CollectionChanged += SettingsChanged;
            QueueAttach();
        }
        private static void SettingsChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args) => QueueAttach();
        private static void QueueAttach()
        {
            if (System.Threading.Interlocked.Exchange(ref pending, 1) != 0) return;
            SHCDESE.API.UnityMainThreadDispatcher.EnqueueStatic(AttachSettings);
        }
        private static void AttachSettings()
        {
            System.Threading.Interlocked.Exchange(ref pending, 0);
            try
            {
                foreach (var entry in SHCDESE.API.GameXAMLManagerAPI.Instance.RegisteredModSettings)
                {
                    string assembly = entry.ViewModel?.GetType().Assembly.GetName().Name;
                    if (assembly != "BugfixesAndQoL" && assembly != "ExtraFeatures") continue;
                    var view = entry.View;
                    if (view == null || views.TryGetValue(view, out _)) continue;
                    object key = typeof(ToolTip);
                    if (!view.Resources.Contains(key) || !(view.Resources[key] is Style original)) continue;
                    view.Resources[key] = Observe(original, assembly + ".ModSettings");
                    views.Add(view, new object());
                    Shared.DevDiag.Log(() => "TOOLTIP_REFERENCE_ATTACHED surface=" + assembly + ".ModSettings");
                }
            }
            catch (System.Exception error)
            {
                Shared.DevDiag.Log(() => "TOOLTIP_REFERENCE_FAILED stage=attach error=" + error);
            }
        }
        internal static Style Observe(Style original, string surface)
        {
            var style = new Style { TargetType = typeof(ToolTip), BasedOn = original };
            style.Setters.Add(new Setter { Property = SurfaceProperty, Value = surface });
            return style;
        }
        private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs args)
        {
            if (!(value is ToolTip tip)) return;
            tip.Opened -= Opened;
            tip.Loaded -= Opened;
            tip.Closed -= Closed;
            tip.Unloaded -= Closed;
            Stop(tip);
            if (string.IsNullOrEmpty((string)args.NewValue)) return;
            states.GetValue(tip, _ => new State());
            tip.Opened += Opened;
            tip.Loaded += Opened;
            tip.Closed += Closed;
            tip.Unloaded += Closed;
        }
        private static void Opened(object sender, RoutedEventArgs args)
        {
            var tip = (ToolTip)sender;
            if (!tip.IsOpen || !states.TryGetValue(tip, out State state)) return;
            if (!state.Observing) { tip.LayoutUpdated += Layout; state.Observing = true; }
            Measure(tip);
        }
        private static void Closed(object sender, RoutedEventArgs args) => Stop((ToolTip)sender);
        private static void Stop(ToolTip tip)
        {
            tip.LayoutUpdated -= Layout;
            if (states.TryGetValue(tip, out State state)) state.Observing = false;
        }
        private static void Layout(object sender, Noesis.EventArgs args) => Measure((ToolTip)sender);
        private static bool FinitePositive(double value) => value > 0 && !double.IsInfinity(value);
        private static string Number(double value) => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        private static string ThicknessText(Thickness value) => Number(value.Left) + "," + Number(value.Top) + "," + Number(value.Right) + "," + Number(value.Bottom);
        private static string ScreenThickness(Thickness value, double x, double y) =>
            Number(value.Left * x) + "," + Number(value.Top * y) + "," + Number(value.Right * x) + "," + Number(value.Bottom * y);
        private static bool Scale(Visual visual, out double x, out double y)
        {
            var origin = visual.PointToScreen(new Point(0, 0));
            var horizontal = visual.PointToScreen(new Point(100, 0));
            var vertical = visual.PointToScreen(new Point(0, 100));
            x = System.Math.Sqrt(System.Math.Pow(horizontal.X - origin.X, 2) + System.Math.Pow(horizontal.Y - origin.Y, 2)) / 100;
            y = System.Math.Sqrt(System.Math.Pow(vertical.X - origin.X, 2) + System.Math.Pow(vertical.Y - origin.Y, 2)) / 100;
            return FinitePositive(x) && FinitePositive(y);
        }
        private static T Find<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T result) return result;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = Find<T>(VisualTreeHelper.GetChild(root, i));
                if (child != null) return child;
            }
            return null;
        }
        private static void Measure(ToolTip tip)
        {
            if (!tip.IsOpen || !tip.IsLoaded || !tip.IsVisible || tip.View == null) return;
            string surface = GetSurface(tip);
            string key = surface + "/" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height;
            if (counts.TryGetValue(key, out int count) && count >= 8) { Stop(tip); return; }
            try
            {
                var text = Find<TextBlock>(tip);
                var border = Find<Border>(tip);
                if (text == null || border == null || !Scale(tip, out double x, out double y) ||
                    !Scale(text, out double textX, out double textY) || !Scale(border, out double borderX, out double borderY)) return;
                string metrics = " fontLocal=" + Number(text.FontSize) + " fontPixels=" + Number(text.FontSize * textY) +
                    " popupScale=" + Number(x) + "," + Number(y) + " textScale=" + Number(textX) + "," + Number(textY) +
                    " maxWidthLocal=" + Number(tip.MaxWidth) + " maxWidthPixels=" + Number(tip.MaxWidth * x) +
                    " paddingLocal=" + ThicknessText(border.Padding) + " paddingPixels=" + ScreenThickness(border.Padding, borderX, borderY) +
                    " borderLocal=" + ThicknessText(border.BorderThickness) + " borderPixels=" + ScreenThickness(border.BorderThickness, borderX, borderY);
                // A successful sample ends observation. No tree walk or string formatting remains
                // on later layouts during this hover; Loaded/Open retries cover lazy popup attachment.
                Stop(tip);
                if (!recorded.Add(key + metrics)) return;
                counts[key] = count + 1;
                string message = "TOOLTIP_REFERENCE surface=" + surface + " screen=" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + metrics;
                Shared.DevDiag.Log(() => message);
            }
            catch (System.Exception error)
            {
                Stop(tip);
                Shared.DevDiag.Log(() => "TOOLTIP_REFERENCE_FAILED stage=measure surface=" + surface + " error=" + error);
            }
        }
    }
}
#endif
