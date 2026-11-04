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
        public void SetStyleValue(DependencyProperty p, object value) { values[p] = value; }
    }
    public class BindingBase { public virtual object Value { get; set; } }
    public class Binding : BindingBase
    {
        private readonly string path;
        public object Source;
        public Binding(string path) { this.path = path; }
        public override object Value { get => Source.GetType().GetProperty(path).GetValue(Source); set => base.Value = value; }
    }
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
    public class UIElement : DependencyObject
    {
        public static readonly DependencyProperty VisibilityProperty = DependencyProperty.RegisterAttached("Visibility", typeof(object), typeof(UIElement), new PropertyMetadata("Visible",null));
        public object Visibility { get => GetValue(VisibilityProperty); set => SetValue(VisibilityProperty,value); }
        public bool IsHitTestVisible = true;
    }
    public class FrameworkElement : UIElement
    {
        private static DependencyProperty Metric(string name, object value) => DependencyProperty.RegisterAttached(name, value.GetType(), typeof(FrameworkElement), new PropertyMetadata(value,null));
        public static readonly DependencyProperty WidthProperty = Metric("Width",float.NaN), HeightProperty = Metric("Height",float.NaN),
            MaxWidthProperty = Metric("MaxWidth",float.PositiveInfinity), MaxHeightProperty = Metric("MaxHeight",float.PositiveInfinity),
            MarginProperty = Metric("Margin",new Thickness(0)), HorizontalAlignmentProperty = Metric("HorizontalAlignment","Stretch"),
            VerticalAlignmentProperty = Metric("VerticalAlignment","Stretch"), FlowDirectionProperty = Metric("FlowDirection","LeftToRight");
        public float Width { get => (float)GetValue(WidthProperty); set => SetValue(WidthProperty,value); }
        public float Height { get => (float)GetValue(HeightProperty); set => SetValue(HeightProperty,value); }
        public float MaxWidth { get => (float)GetValue(MaxWidthProperty); set => SetValue(MaxWidthProperty,value); }
        public float MaxHeight { get => (float)GetValue(MaxHeightProperty); set => SetValue(MaxHeightProperty,value); }
        public Thickness Margin { get => (Thickness)GetValue(MarginProperty); set => SetValue(MarginProperty,value); }
        public object HorizontalAlignment { get => GetValue(HorizontalAlignmentProperty); set => SetValue(HorizontalAlignmentProperty,value); }
        public object VerticalAlignment { get => GetValue(VerticalAlignmentProperty); set => SetValue(VerticalAlignmentProperty,value); }
        public object FlowDirection { get => GetValue(FlowDirectionProperty); set => SetValue(FlowDirectionProperty,value); }
        public bool IsVisible = true;
        public float ActualWidth = 800, Scale = 1;
        public Point PointToScreen(Point value) => new Point(value.X * Scale, value.Y * Scale);
    }
    public class Brush { }
    public class SolidColorBrush : Brush { public Color Color; public SolidColorBrush(Color colour) { Color=colour; } }
    public struct Color
    {
        public byte A, R, G, B;
        public static Color FromArgb(byte a, byte r, byte g, byte b) => new Color { A=a, R=r, G=g, B=b };
    }
    public class DropShadowEffect { public float ShadowDepth, BlurRadius; public Color Color; }
    public enum FontWeight { Normal = 400, Bold = 700 }
    public class TextElement : FrameworkElement
    {
        public static readonly DependencyProperty FontSizeProperty = DependencyProperty.RegisterAttached("FontSize", typeof(float), typeof(TextElement), new PropertyMetadata(12f,null));
        public static readonly DependencyProperty FontWeightProperty = DependencyProperty.RegisterAttached("FontWeight", typeof(FontWeight), typeof(TextElement), new PropertyMetadata(FontWeight.Normal,null));
        public static readonly DependencyProperty StrokeProperty = DependencyProperty.RegisterAttached("Stroke", typeof(Brush), typeof(TextElement), new PropertyMetadata(null,null));
        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.RegisterAttached("StrokeThickness", typeof(float), typeof(TextElement), new PropertyMetadata(0f,null));
        public Brush Foreground;
        public float FontSize { get => (float)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty,value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty,value); }
        public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty,value); }
        public float StrokeThickness { get => (float)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty,value); }
    }
    public class Run : TextElement { public string Text; }
    public class TextBlock : FrameworkElement
    {
        public static readonly DependencyProperty FontSizeProperty = DependencyProperty.RegisterAttached("FontSize", typeof(float), typeof(TextBlock), new PropertyMetadata(12f,null));
        private static DependencyProperty Metric(string name, object value) => DependencyProperty.RegisterAttached(name, value.GetType(), typeof(TextBlock), new PropertyMetadata(value,null));
        public static readonly DependencyProperty FontWeightProperty = Metric("FontWeight",FontWeight.Normal), FontFamilyProperty = Metric("FontFamily","Default"),
            FontStretchProperty = Metric("FontStretch","Normal"), FontStyleProperty = Metric("FontStyle","Normal"),
            LineHeightProperty = Metric("LineHeight",16f), LineStackingStrategyProperty = Metric("LineStackingStrategy","BlockLineHeight"),
            TextWrappingProperty = Metric("TextWrapping","NoWrap"), TextTrimmingProperty = Metric("TextTrimming","None"),
            TextAlignmentProperty = Metric("TextAlignment","Left"), PaddingProperty = Metric("Padding",new Thickness(0));
        public float FontSize { get => (float)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty,value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty,value); }
        public object FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty,value); }
        public object FontStretch { get => GetValue(FontStretchProperty); set => SetValue(FontStretchProperty,value); }
        public object FontStyle { get => GetValue(FontStyleProperty); set => SetValue(FontStyleProperty,value); }
        public float LineHeight { get => (float)GetValue(LineHeightProperty); set => SetValue(LineHeightProperty,value); }
        public object LineStackingStrategy { get => GetValue(LineStackingStrategyProperty); set => SetValue(LineStackingStrategyProperty,value); }
        public object TextWrapping { get => GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty,value); }
        public object TextTrimming { get => GetValue(TextTrimmingProperty); set => SetValue(TextTrimmingProperty,value); }
        public object TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty,value); }
        public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty,value); }
        public Brush Foreground { get; set; }
        public DropShadowEffect Effect;
    }
    public class ToolTip : FrameworkElement
    {
        public FrameworkElement PlacementTarget;
        public object Content;
        public float FontSize { get; set; }
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
    public static class ToolTipService
    {
        private static readonly Dictionary<DependencyObject, object> tips = new Dictionary<DependencyObject, object>();
        public static object GetToolTip(DependencyObject owner) => tips.TryGetValue(owner, out var tip) ? tip : null;
        public static void SetToolTip(DependencyObject owner, object tip) { tips[owner] = tip; }
    }
}
namespace APIShared.ModSettings
{
    // Replace only the external service boundary. The actual API owns pixel math
    // and is tested in APIShared; these checks prove this adapter delegates to it.
    public static class SharedToolTipSizing
    {
        public sealed class Request { public Noesis.FrameworkElement Source; public float Font; public bool Enabled; public int ActiveOwners; }
        private static readonly Dictionary<Noesis.DependencyObject,Request> requests = new Dictionary<Noesis.DependencyObject,Request>();
        private static Request For(Noesis.DependencyObject value) { if(!requests.TryGetValue(value,out var request)) requests[value]=request=new Request(); return request; }
        public static Request Get(Noesis.DependencyObject value)=>For(value);
        public static bool GetEnabled(Noesis.DependencyObject value)=>For(value).Enabled;
        public static void SetReferenceElement(Noesis.DependencyObject value,Noesis.FrameworkElement source)=>For(value).Source=source;
        public static void SetReferenceFontSize(Noesis.DependencyObject value,float size)=>For(value).Font=size;
        public static void SetEnabled(Noesis.DependencyObject value,bool enabled)
        {
            var request=For(value);
            // Like the real DP setter, unchanged native True does not replay Changed
            // or recreate the managed service state. Source/font writes do not either.
            if(request.Enabled==enabled)return;
            if(enabled && request.Font!=15)throw new Exception("Enable before reference font.");
            request.Enabled=enabled;request.ActiveOwners=enabled?1:0;
        }
        public static void LoseManagedState(Noesis.DependencyObject value)=>For(value).ActiveOwners=0;
        public static void ApplyEnabledStyleWithoutCallback(Noesis.DependencyObject value)
        { var request=For(value);request.Enabled=true;request.ActiveOwners=0; }
    }
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
            var sizing = APIShared.ModSettings.SharedToolTipSizing.Get(tip);
            Check(sizing.Enabled && ReferenceEquals(sizing.Source,menu) && sizing.Font==15,
                "programmatic popup delegates reference/font ownership to the shared service");
            tip.IsOpen = true;
            // Simulate the shared owner's output, without recreating its scale algorithm.
            tip.FontSize = 28;
            Check(((Noesis.TextBlock)tip.Content).FontSize == 28, "rich content follows shared owner font through a live binding");
            tip.FinishLayout(); Check(tip.FontSize == 28, "repeated layout is stable");
            tip.Content = new Noesis.TextBlock { FontSize=7 };
            MenuToolTipSizing.SetEnabled(tip,true);
            tip.FontSize=15;
            Check(((Noesis.TextBlock)tip.Content).FontSize==15,"reused status tooltip binds replacement content without a hard child size");
            MenuToolTipSizing.CloseAll(); Check(!tip.IsOpen, "collapse closes own popup");
            tip.PlacementTarget = new Noesis.FrameworkElement { Scale = 2.5f };
            tip.Scale = 1; tip.IsOpen = true;
            Check(ReferenceEquals(sizing.Source,menu), "different target transforms retain the common toolbar reference");
            var button = new Noesis.FrameworkElement { Scale = .5f };
            var plain = new Noesis.ToolTip { Scale = 2, PlacementTarget = button, Content = "Goods / Gold" };
            // Model a style-supplied opt-in already present when the HUD cache encounters it.
            plain.SetStyleValue(MenuToolTipSizing.EnabledProperty, true);
            APIShared.ModSettings.SharedToolTipSizing.ApplyEnabledStyleWithoutCallback(plain);
            Noesis.ToolTipService.SetToolTip(button, plain);
            MenuToolTipSizing.InitializeToolTip(button);
            plain.IsOpen = true;
            var xamlSizing=APIShared.ModSettings.SharedToolTipSizing.Get(plain);
            Check(xamlSizing.Enabled && xamlSizing.ActiveOwners==1 && ReferenceEquals(xamlSizing.Source,sizing.Source) && xamlSizing.Font==sizing.Font,
                "XAML string and programmatic rich popups delegate the same source and logical size");
            plain.FinishLayout(); MenuToolTipSizing.InitializeToolTip(button);
            Check(plain.FontSize == 0, "local adapter never writes popup pixel sizes");
            plain.IsOpen=false;
            APIShared.ModSettings.SharedToolTipSizing.LoseManagedState(plain);
            APIShared.ModSettings.SharedToolTipSizing.SetEnabled(plain,true);
            Check(xamlSizing.Enabled && xamlSizing.ActiveOwners==0,"native True alone cannot repair a collected managed sizing registration");
            plain.IsOpen=true;
            Check(xamlSizing.ActiveOwners==1 && ReferenceEquals(xamlSizing.Source,menu) && xamlSizing.Font==15,
                "opening re-arms a closed XAML tooltip whose native opt-in outlived managed state");
            plain.IsOpen=false;plain.IsVisible=false;
            APIShared.ModSettings.SharedToolTipSizing.LoseManagedState(plain);
            plain.IsOpen=true;
            Check(xamlSizing.ActiveOwners==0,"invisible opening waits for the existing loaded retry");
            plain.IsVisible=true;plain.FinishLayout();plain.FinishLayout();
            Check(xamlSizing.ActiveOwners==1,"delayed loaded retry restores exactly one effective shared sizing owner");
            var ownButton = new Noesis.FrameworkElement();
            var ownTip = new Noesis.ToolTip { Content="Stop placing", FontSize=15 };
            Noesis.ToolTipService.SetToolTip(ownButton,ownTip);
            Check(!MenuToolTipSizing.GetEnabled(ownTip) && !APIShared.ModSettings.SharedToolTipSizing.GetEnabled(ownTip),
                "owned XAML popup can initially retain default False attached values");
            MenuToolTipSizing.InitializeToolTip(ownButton,true);
            ownTip.IsOpen=true;
            var ownSizing=APIShared.ModSettings.SharedToolTipSizing.Get(ownTip);
            Check(MenuToolTipSizing.GetEnabled(ownTip) && ownSizing.Enabled && ownSizing.ActiveOwners==1 &&
                ReferenceEquals(ownSizing.Source,menu) && ownSizing.Font==15,
                "explicit owned-subtree initialization registers XAML tips without relying on style callbacks");
            var lazy=new Noesis.ToolTip(); lazy.SetValue(MenuToolTipSizing.EnabledProperty,true); lazy.IsOpen=true;
            Check(ReferenceEquals(APIShared.ModSettings.SharedToolTipSizing.Get(lazy).Source,menu),
                "late XAML attached callback registers shared sizing even without the HUD cache traversal");
            var nativeOwner = new Noesis.FrameworkElement();
            var nativeTip = new Noesis.ToolTip { FontSize = 9 };
            Noesis.ToolTipService.SetToolTip(nativeOwner, nativeTip);
            MenuToolTipSizing.InitializeToolTip(nativeOwner); nativeTip.IsOpen = true;
            Check(nativeTip.FontSize == 9, "HUD cache initialization leaves non-opted-in native tooltips alone");
            tip.IsOpen = false; tip.PlacementTarget.IsVisible = false; tip.IsOpen = true;
            Check(!tip.IsOpen, "hidden HUD rejects pending popup");
            tip.PlacementTarget = null;
            menu.IsVisible = false; tip.IsOpen = true; Check(!tip.IsOpen, "disabled toolbar rejects a pending hover"); menu.IsVisible = true;
            tip.IsVisible = false; tip.IsOpen = true; menu.Scale = 3; tip.Scale = 1;
            tip.IsVisible = true; tip.FinishLayout();
            Check(ReferenceEquals(APIShared.ModSettings.SharedToolTipSizing.Get(tip).Source,menu), "loaded event rebinds a delayed popup to current reference");
            var second = new Noesis.ToolTip(); MenuToolTipSizing.SetEnabled(second, true); second.IsOpen = true;
            MenuToolTipSizing.SetReference(null);
            Check(!tip.IsOpen && !second.IsOpen, "HUD detach closes every open popup without set-enumeration errors");
            second.IsOpen = true; Check(!second.IsOpen, "late old-HUD opening is rejected");
            var replacement=new Noesis.FrameworkElement { Scale=2 };MenuToolTipSizing.SetReference(replacement);
            lazy.IsOpen=true;
            Check(ReferenceEquals(APIShared.ModSettings.SharedToolTipSizing.Get(lazy).Source,replacement),"recreated HUD reference is applied on opening existing XAML tips");
            MenuToolTipSizing.SetEnabled(tip, false); tip.FontSize = 7; tip.IsOpen = true; tip.FinishLayout();
            Check(tip.FontSize == 7, "detached sizing handlers cannot alter the popup");
            Check(!sizing.Enabled && sizing.Source==null,"detachment releases shared service and old HUD source");
            tip.IsOpen = false; MenuToolTipSizing.SetReference(null);
        }
    }
}
