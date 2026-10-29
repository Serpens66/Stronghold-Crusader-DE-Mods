using System;
using System.Collections.Generic;

// Noesis boundary double. The production attached behavior is compiled unchanged;
// these events model delayed creation, layout and teardown, not native rendering.
namespace Noesis
{
    public class RoutedEventArgs : EventArgs { }
    public class SizeChangedEventArgs : RoutedEventArgs { }
    public class DependencyPropertyChangedEventArgs : EventArgs { public object NewValue; }
    public class PropertyMetadata
    {
        public object Default;
        public Action<DependencyObject, DependencyPropertyChangedEventArgs> Changed;
        public PropertyMetadata(object value, Action<DependencyObject, DependencyPropertyChangedEventArgs> changed) { Default = value; Changed = changed; }
    }
    public class DependencyProperty
    {
        public static readonly object UnsetValue = new object();
        internal PropertyMetadata Metadata;
        public static DependencyProperty RegisterAttached(string name, Type type, Type owner, PropertyMetadata metadata) => new DependencyProperty { Metadata = metadata };
    }
    public class DependencyObject
    {
        private readonly Dictionary<DependencyProperty, object> values = new Dictionary<DependencyProperty, object>();
        internal readonly Dictionary<DependencyProperty, BindingBase> Bindings = new Dictionary<DependencyProperty, BindingBase>();
        public object ReadLocalValue(DependencyProperty p) => values.TryGetValue(p, out var value) ? value : DependencyProperty.UnsetValue;
        public void ClearValue(DependencyProperty p) { values.Remove(p); Bindings.Remove(p); }
        public object GetValue(DependencyProperty p) => Bindings.TryGetValue(p, out var binding) ? binding.Value : values.TryGetValue(p, out var value) ? value : p.Metadata.Default;
        public void SetValue(DependencyProperty p, object value)
        { Bindings.Remove(p); if (Equals(GetValue(p), value)) return; values[p] = value; p.Metadata.Changed?.Invoke(this, new DependencyPropertyChangedEventArgs { NewValue = value }); }
    }
    public class BindingBase { public object Value; }
    public static class BindingOperations
    {
        public static BindingBase GetBindingBase(DependencyObject target, DependencyProperty property) => target.Bindings.TryGetValue(property, out var binding) ? binding : null;
        public static void SetBinding(DependencyObject target, DependencyProperty property, BindingBase binding) { target.SetValue(property, binding.Value); target.Bindings[property] = binding; }
    }
    public struct Point { public float X, Y; public Point(float x, float y) { X = x; Y = y; } }
    public struct Thickness
    {
        public float Left, Top, Right, Bottom;
        public Thickness(float all) : this(all, all, all, all) { }
        public Thickness(float left, float top, float right, float bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
    }
    public class FrameworkElement : DependencyObject
    {
        public bool IsVisible = true;
        public float ActualWidth = 800, Scale = 1;
        public Point PointToScreen(Point value) => new Point(value.X * Scale, value.Y * Scale);
    }
    public class TextBlock : FrameworkElement { public float FontSize, MaxWidth = float.PositiveInfinity; }
    public class ToolTip : FrameworkElement
    {
        public FrameworkElement PlacementTarget;
        public object Content;
        public float FontSize, MaxWidth;
        public Thickness Padding, BorderThickness;
        private bool open;
        public bool IsOpen
        {
            get => open;
            set { if (open == value) return; open = value; if (value) Opened?.Invoke(this, new RoutedEventArgs()); else Closed?.Invoke(this, new RoutedEventArgs()); }
        }
        public event EventHandler<RoutedEventArgs> Opened, Closed, Loaded;
        public event EventHandler<SizeChangedEventArgs> SizeChanged;
        public void FinishLayout() { Loaded?.Invoke(this, new RoutedEventArgs()); SizeChanged?.Invoke(this, new SizeChangedEventArgs()); }
    }
}
namespace SHCDESE.UI
{
    public static class ToolTipResolutionScale { public static void SetEnabled(Noesis.DependencyObject value, bool enabled) { if (enabled) throw new Exception("Competing scaler enabled."); } }
}
namespace Spectator
{
    internal static class MenuToolTipTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        internal static void Run()
        {
            var menu = new Noesis.FrameworkElement { Scale = 2, ActualWidth = 800 };
            MenuToolTipSizing.SetReference(menu);
            // Created after the collapsed content was initialized: no tree walk is needed.
            var tip = new Noesis.ToolTip { Scale = 1, Content = new Noesis.TextBlock() };
            MenuToolTipSizing.SetEnabled(tip, true);
            tip.IsOpen = true;
            Check(tip.FontSize == 28 && ((Noesis.TextBlock)tip.Content).FontSize == 28, "plain and rich popup match menu pixels");
            Check(tip.MaxWidth == 840 && tip.Padding.Left == 20, "popup width and spacing follow the same scale");
            tip.FinishLayout(); Check(tip.FontSize == 28, "repeated layout is stable");
            tip.IsOpen = false; menu.Scale = 1.5f; tip.Scale = 1.5f; tip.IsOpen = true;
            Check(tip.FontSize == 14, "already scaled popup must not be scaled twice");
            MenuToolTipSizing.CloseAll(); Check(!tip.IsOpen, "collapse closes own popup");
            tip.PlacementTarget = new Noesis.FrameworkElement { Scale = 2.5f };
            tip.Scale = 1; tip.IsOpen = true;
            Check(tip.FontSize == 35, "second HUD surface uses its own rendered scale");
            tip.IsOpen = false; tip.PlacementTarget.IsVisible = false; tip.IsOpen = true;
            Check(!tip.IsOpen, "hidden HUD rejects pending popup");
            tip.PlacementTarget = null;
            menu.IsVisible = false; tip.IsOpen = true; Check(!tip.IsOpen, "disabled toolbar rejects a pending hover"); menu.IsVisible = true;
            tip.IsVisible = false; tip.IsOpen = true; menu.Scale = 3; tip.Scale = 1;
            tip.IsVisible = true; tip.FinishLayout(); Check(tip.FontSize == 42, "loaded event sizes a delayed popup");
            var second = new Noesis.ToolTip(); MenuToolTipSizing.SetEnabled(second, true); second.IsOpen = true;
            MenuToolTipSizing.SetReference(null);
            Check(!tip.IsOpen && !second.IsOpen, "HUD detach closes every open popup without set-enumeration errors");
            second.IsOpen = true; Check(!second.IsOpen, "late old-HUD opening is rejected");
            MenuToolTipSizing.SetReference(new Noesis.FrameworkElement { Scale = 2 });
            MenuToolTipSizing.SetEnabled(tip, false); tip.FontSize = 7; tip.IsOpen = true; tip.FinishLayout();
            Check(tip.FontSize == 7, "detached sizing handlers cannot alter the popup");
            tip.IsOpen = false; MenuToolTipSizing.SetReference(null);
        }
    }
}
