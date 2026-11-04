using System;
using System.Collections.Generic;
using Noesis;
using APIShared.ModSettings;

namespace Spectator
{
    /// <summary>Opt-in for toolbar popups, including lazily created scroll content.
    /// Supplies the shared sizing service's toolbar reference and closes stale popups. UI thread only.</summary>
    public static class MenuToolTipSizing
    {
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(MenuToolTipSizing), new PropertyMetadata(false, Changed));
        public static bool GetEnabled(DependencyObject value) => (bool)value.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject value, bool enabled)
        {
            bool unchanged = GetEnabled(value) == enabled;
            value.SetValue(EnabledProperty, enabled);
            // A XAML style may already supply True before the explicit HUD initialization.
            // Setting that same value need not invoke the dependency-property callback.
            if (unchanged && value is ToolTip tip) Configure(tip, enabled);
        }
        private static FrameworkElement reference;
        private static readonly HashSet<ToolTip> openTips = new HashSet<ToolTip>();

        internal static void InitializeToolTip(DependencyObject owner, bool owned = false)
        {
            // Called by the existing HUD cache traversal, never by a frame-time tree walk.
            // The current XAML popups do not materialize their attached style opt-ins.
            // Explicitly initialize our subtree; native HUD tips still require opt-in.
            if (ToolTipService.GetToolTip(owner) is ToolTip tip && (owned || GetEnabled(tip))) SetEnabled(tip, true);
        }

        internal static void SetReference(FrameworkElement value)
        {
            if (ReferenceEquals(reference, value)) return;
            CloseAll();
            reference = value;
        }

        internal static void CloseAll()
        {
            // Closed removes entries synchronously; never mutate the set being enumerated.
            foreach (var tip in new List<ToolTip>(openTips)) tip.IsOpen = false;
            openTips.Clear();
        }

        private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs args)
        {
            if (value is ToolTip tip) Configure(tip, (bool)args.NewValue);
        }

        private static void Configure(ToolTip tip, bool enabled)
        {
            tip.Opened -= Opened;
            tip.Loaded -= Opened;
            tip.Closed -= Closed;
            openTips.Remove(tip);
            if (!enabled)
            {
                tip.IsOpen = false;
                SharedToolTipSizing.SetEnabled(tip, false);
                SharedToolTipSizing.SetReferenceElement(tip, null);
                return;
            }
            tip.Opened += Opened;
            tip.Loaded += Opened;
            tip.Closed += Closed;
            BindSharedSizing(tip);
            Opened(tip, null);
        }

        private static void Closed(object sender, RoutedEventArgs args) => openTips.Remove((ToolTip)sender);
        private static void BindSharedSizing(ToolTip tip)
        {
            // Noesis can recreate a CLR proxy while native attached values survive.
            // Re-arm the service even when native Enabled is already True: a same-
            // value SetValue cannot restore its missing managed registration.
            if (SharedToolTipSizing.GetEnabled(tip)) SharedToolTipSizing.SetEnabled(tip, false);
            // Set source and logical size before enabling; APIShared owns every
            // pixel calculation, retry and exclusive scaler registration.
            SharedToolTipSizing.SetReferenceElement(tip, reference);
            SharedToolTipSizing.SetReferenceFontSize(tip, 15);
            SharedToolTipSizing.SetEnabled(tip, true);
            if (tip.Content is TextBlock text)
                BindingOperations.SetBinding(text, TextBlock.FontSizeProperty,
                    new Binding(nameof(ToolTip.FontSize)) { Source = tip });
        }
        private static void Opened(object sender, RoutedEventArgs args)
        {
            var tip = (ToolTip)sender;
            if (!tip.IsOpen) return;
            if (reference == null || !reference.IsVisible) { tip.IsOpen = false; return; }
            openTips.Add(tip);
            if (!tip.IsVisible) return; // Loaded retries binding; APIShared owns layout/size retries.
            var surface = tip.PlacementTarget as FrameworkElement ?? reference;
            if (!surface.IsVisible) { tip.IsOpen = false; return; }
            BindSharedSizing(tip);
        }
    }
}
