using System.ComponentModel;
using ExtendedData;
using Noesis;
using SHCDESE.NoesisUtil;

internal static class CoopTrailMenuTests
{
    internal static void Selection()
    {
        var settings = new ExtendedDataSettingsViewModel();
        var menu = new CoopTrailMenuViewModel(settings);
        Require(menu.Options.SequenceEqual(new[] { "Vanilla", "Alpha (2)", "Beta (4)" }), "menu changed option labels/order");
        Require(menu.Options.GetType() == typeof(string[]), "menu reused visual item containers");
        menu.SelectedIndex = 2;
        Require(settings.ActiveCoopPackageId == "beta" && menu.SelectedIndex == 2 && settings.SelectionWrites == 1,
            "menu did not use the authoritative selection setter");
        settings.ActiveCoopPackageId = "alpha";
        Require(menu.SelectedIndex == 1, "external/host/preset selection did not reach menu");
        menu.SelectedIndex = 0;
        Require(settings.ActiveCoopPackageId == "", "Vanilla selection did not clear the package ID");
        settings.ActiveCoopPackageId = "beta";
        menu.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(menu.Options)) menu.SelectedIndex = 0;
        };
        settings.SetOptions(new[] { "", "beta", "alpha" }, new[] { "Vanilla", "Beta renamed (4)", "Alpha (2)" });
        Require(settings.ActiveCoopPackageId == "beta" && menu.SelectedIndex == 1 && menu.Options[1] == "Beta renamed (4)",
            "catalog refresh/ItemsSource reset changed the active package");
        settings.CanEditCoopPackage = false;
        menu.SelectedIndex = 2;
        menu.IsDropDownOpen = true;
        Require(settings.ActiveCoopPackageId == "beta" && !menu.CanEdit && !menu.IsInputBlocked, "read-only menu accepted an edit");
        settings.CanEditCoopPackage = true;
        settings.RejectSelection = true;
        menu.SelectedIndex = 2;
        Require(menu.SelectedIndex == 1, "rejected setting mutation left a false selection");
        menu.IsDropDownOpen = true;
        settings.CanEditCoopPackage = false;
        Require(!menu.IsDropDownOpen && !menu.IsInputBlocked, "permission change retained the popup shield");
        settings.CanEditCoopPackage = true;
        settings.ActiveCoopPackageId = "missing";
        Require(menu.SelectedIndex == 0 && settings.ActiveCoopPackageId == "missing", "missing package overwrote stored identity");
        menu.Disable();
        menu.SelectedIndex = 1;
        Require(!menu.CanEdit && settings.ActiveCoopPackageId == "missing", "retired runtime accepted selection");
    }

    internal static void Input()
    {
        var menu = new CoopTrailMenuViewModel(new ExtendedDataSettingsViewModel());
        var host = CreateHost(out var shield, out var dropdown, out var popup);
        ((INoesisElementBindingAware)menu).OnNoesisElementBound(host);
        var closedClick = host.Down(900, 450);
        Require(!closedClick.Handled && !menu.IsInputBlocked, "closed dropdown blocked Vanilla");
        menu.IsDropDownOpen = true;
        popup.IsOpen = true;
        Require(!host.Down(750, 165).Handled, "dropdown button lost its normal handling");
        Require(!host.Down(750, 230).Handled, "popup entry lost its normal handling");
        var outside = host.Down(900, 450);
        Require(outside.Handled && !menu.IsDropDownOpen && menu.IsInputBlocked && shield.IsMouseCaptured,
            "outside down escaped to Vanilla or released shield early");
        Require(host.Up(1100, 500).Handled && !menu.IsInputBlocked && !shield.IsMouseCaptured,
            "outside release escaped to Vanilla or left a stuck capture");
        Require(!host.Down(900, 450).Handled, "a separate later Vanilla click was swallowed");
        menu.IsDropDownOpen = true;
        Require(host.PressKey(Key.Escape).Handled && !menu.IsInputBlocked, "Escape did not close exclusively");
        menu.IsDropDownOpen = true;
        host.Hide();
        Require(!menu.IsInputBlocked, "menu navigation left a shield active");
        host.IsVisible = true;
        menu.IsDropDownOpen = true;
        ((INoesisElementBindingAware)menu).OnNoesisElementBound(host);
        Require(menu.IsDropDownOpen, "repeated auto-binding reset the current popup");
        var replacement = CreateHost(out _, out _, out _);
        ((INoesisElementBindingAware)menu).OnNoesisElementBound(replacement);
        Require(!menu.IsInputBlocked, "recreated menu retained old popup state");
        menu.IsDropDownOpen = true;
        host.Hide();
        Require(menu.IsDropDownOpen, "detached view still received callbacks");
        replacement.Unload();
        Require(!menu.IsInputBlocked, "unloaded view retained popup state");
    }

    internal static void Hover()
    {
        foreach (int count in new[] { 1, 10, 11, 1000 })
        {
            var settings = SettingsWithCount(count);
            var menu = new CoopTrailMenuViewModel(settings);
            var host = CreateHost(out _, out var dropdown, out var popup);
            var viewport = new FrameworkElement { X = 700, Y = 196, ActualWidth = 490, ActualHeight = 144 };
            var scroll = new ScrollViewer();
            scroll.Template.Names["ItemsViewport"] = viewport;
            dropdown.Template.Names["CoopTrailScroll"] = scroll;
            var first = new ComboBoxItem { Parent = viewport };
            var second = new ComboBoxItem { Parent = viewport };
            var firstLayer = new Border { Visibility = Visibility.Collapsed };
            var secondLayer = new Border { Visibility = Visibility.Collapsed };
            first.Template.Names["PointerHover"] = firstLayer;
            second.Template.Names["PointerHover"] = secondLayer;
            var text = new FrameworkElement { Parent = first };
            ((INoesisElementBindingAware)menu).OnNoesisElementBound(host);
            int hits = VisualTreeHelper.HitTests;
            host.Move(720, 210);
            Require(VisualTreeHelper.HitTests == hits, "closed menu performed hover work");
            menu.IsDropDownOpen = true;
            VisualTreeHelper.Hit = text;
            popup.IsOpen = true;
            host.Layout();
            Require(firstLayer.Visibility == Visibility.Visible, "first popup layout did not establish hover");
            int writes = firstLayer.VisibilityWrites;
            hits = VisualTreeHelper.HitTests;
            for (int move = 1; move <= 20; move++) host.Move(720 + move, 210);
            Require(firstLayer.VisibilityWrites == writes && VisualTreeHelper.HitTests == hits + 20,
                "same-row hover mutated presentation or performed multiple hit tests");
            hits = VisualTreeHelper.HitTests;
            popup.Child.Move(740, 210);
            Require(VisualTreeHelper.HitTests == hits, "duplicate popup/view event performed a second hit test");
            text.Parent = second;
            host.Move(740, 270);
            Require(firstLayer.Visibility == Visibility.Collapsed && secondLayer.Visibility == Visibility.Visible,
                "hover retained the old row or ignored the actual hit ancestor");
            hits = VisualTreeHelper.HitTests;
            host.Leave(1300, 270);
            Require(secondLayer.Visibility == Visibility.Collapsed && VisualTreeHelper.HitTests == hits,
                "leaving viewport retained hover or hit-tested the whole menu");
            host.Move(740, 270);
            Require(secondLayer.Visibility == Visibility.Visible, "re-entry did not restore current hover");
            host.Move(1200, 270); // Scrollbar, outside the actual content viewport.
            Require(secondLayer.Visibility == Visibility.Collapsed, "scrollbar inherited a row highlight");
            host.Move(740, 270);
            scroll.Scroll();
            text.Parent = first;
            Require(secondLayer.Visibility == Visibility.Collapsed, "scroll retained a recycled container's hover");
            host.Layout();
            Require(firstLayer.Visibility == Visibility.Visible, "stationary-pointer scrolling did not recompute hover");
            first.ChangeContext();
            Require(firstLayer.Visibility == Visibility.Collapsed, "recycled item retained hover before layout");
            host.Layout();
            Require(firstLayer.Visibility == Visibility.Visible && settings.SelectionWrites == 0,
                "recycling lost current hover or hover changed the stored selection");
            first.Unload();
            Require(firstLayer.Visibility == Visibility.Collapsed, "unloaded item retained hover");
            host.Move(740, 270);
            menu.IsDropDownOpen = false;
            popup.IsOpen = false;
            hits = VisualTreeHelper.HitTests;
            host.Move(740, 270);
            host.Layout();
            Require(firstLayer.Visibility == Visibility.Collapsed && VisualTreeHelper.HitTests == hits,
                "closed popup retained hover work or pending layout callbacks");
            menu.IsDropDownOpen = true;
            popup.IsOpen = true;
            host.Layout();
            Require(firstLayer.Visibility == Visibility.Visible, "reopened popup lost its event bindings");
            host.Hide();
            Require(firstLayer.Visibility == Visibility.Collapsed, "hidden menu retained hover");
            host.Unload();
            VisualTreeHelper.Hit = null;
        }
    }

    internal static void Geometry()
    {
        foreach (var resolution in new[] { (1280,720), (1920,1080), (2560,1440), (3840,2160),
                     (1024,768), (1920,1200), (3440,1440), (800,600) })
        foreach (float scale in new[] { .75f, 1f, 1.5f, 2f })
        foreach (int count in new[] { 1, 10, 11, 1000 })
        {
            var root = new FrameworkElement { ActualWidth = resolution.Item1, ActualHeight = resolution.Item2 };
            var host = CreateHost(out _, out var dropdown, out var popup);
            host.Parent = root;
            host.Scale = dropdown.Scale = scale;
            host.ActualWidth = root.ActualWidth / scale;
            host.ActualHeight = root.ActualHeight / scale;
            var menu = new CoopTrailMenuViewModel(SettingsWithCount(count));
            ((INoesisElementBindingAware)menu).OnNoesisElementBound(host);
            void CheckBounds()
            {
                float right = dropdown.Margin.Left + dropdown.Width;
                float bottom = dropdown.Margin.Top + dropdown.ActualHeight + dropdown.MaxDropDownHeight;
                bool verticalFits = popup.Placement == PlacementMode.Top
                    ? dropdown.Margin.Top - dropdown.MaxDropDownHeight >= -.01f
                    : bottom <= host.ActualHeight + .01f;
                Require(dropdown.Margin.Left >= 0 && right <= host.ActualWidth && verticalFits,
                    $"popup exceeds transformed view bounds at {resolution}, scale {scale}");
                Require(dropdown.MaxDropDownHeight <= 10 * 48 + 14,
                    "popup exceeded ten rows including its chrome");
                float content = dropdown.MaxDropDownHeight - 14;
                Require(content >= 0 && content % 48 == 0 && content / 48 <= Math.Min(count, 10),
                    "popup height was not limited to whole visible rows");
                if (host.ActualHeight - 190 >= Math.Min(count, 10) * 48 + 14)
                    Require(content / 48 == Math.Min(count, 10), "available rows were unnecessarily lost");
            }
            CheckBounds();
            menu.IsDropDownOpen = true;
            popup.IsOpen = true;
            root.ActualWidth *= .8f;
            root.ActualHeight *= .8f;
            host.ActualWidth = root.ActualWidth / scale;
            host.ActualHeight = root.ActualHeight / scale;
            root.Resize();
            CheckBounds();
            // All hit-test coordinates must undergo the same Viewbox mapping as layout.
            var viewport = new FrameworkElement { X = 50, Y = 80, Scale = scale, ActualWidth = 200, ActualHeight = 48 };
            Mouse.X = 50 + 199 * scale; Mouse.Y = 80 + 47 * scale;
            Point position = Mouse.GetPosition(viewport);
            Require(CoopTrailMenuGeometry.Contains(position.X, position.Y, viewport.ActualWidth, viewport.ActualHeight),
                "scaled row interior rejected");
            Mouse.X = 50 + 200 * scale;
            position = Mouse.GetPosition(viewport);
            Require(!CoopTrailMenuGeometry.Contains(position.X, position.Y, viewport.ActualWidth, viewport.ActualHeight),
                "scaled viewport edge was not excluded");
            host.Unload();
        }
        Require(CoopTrailMenuGeometry.Height(float.NaN, 1000) == 0 &&
                CoopTrailMenuGeometry.Height(-1, 1000) == 0 && CoopTrailMenuGeometry.Height(20, 1000) <= 20,
            "invalid or extremely small bounds escaped view limits");
    }

    private static ExtendedDataSettingsViewModel SettingsWithCount(int count)
    {
        var settings = new ExtendedDataSettingsViewModel();
        settings.SetOptions(Enumerable.Range(0, count).Select(i => i == 0 ? "" : "id" + i).ToArray(),
            Enumerable.Range(0, count).Select(i => i == 0 ? "Vanilla" : "Package " + i).ToArray());
        return settings;
    }

    private static FrameworkElement CreateHost(out Border shield, out ComboBox dropdown, out Popup popup)
    {
        var host = new FrameworkElement { ActualWidth = 1920, ActualHeight = 1080 };
        shield = new Border();
        popup = new Popup { Child = new FrameworkElement { X = 700, Y = 190, ActualWidth = 520, ActualHeight = 200 } };
        dropdown = new ComboBox { X = 700, Y = 150, ActualWidth = 520, ActualHeight = 40 };
        dropdown.Parent = host;
        dropdown.Template = new FrameworkTemplate { Popup = popup };
        host.Names["ExtendedDataCoopTrailInputShield"] = shield;
        host.Names["ExtendedDataCoopTrailDropdown"] = dropdown;
        return host;
    }

    private static void Require(bool success, string message)
    {
        if (!success) throw new InvalidOperationException(message);
    }
}

// Boundary doubles for the production menu adapter, not a substitute for Noesis
// hit testing, routed-event ordering or the existing settings persistence/MP suites.
namespace ExtendedData
{
    internal sealed class ExtendedDataSettingsViewModel : INotifyPropertyChanged
    {
        private string selectedId = "";
        private bool canEdit = true;
        private string[] ids = { "", "alpha", "beta" };
        public ExtendedDataSettingsViewModel() => SetOptions(ids, new[] { "Vanilla", "Alpha (2)", "Beta (4)" });
        public event PropertyChangedEventHandler PropertyChanged;
        public ComboBoxItem[] CoopPackageOptions { get; private set; }
        public int SelectionWrites { get; private set; }
        public bool RejectSelection { get; set; }
        public string CoopPackageText => "Coop package";
        public string CoopPackageHelpText => "Select a package";
        public bool CanEditHostSettings => canEdit;
        public bool CanEditCoopPackage { get => canEdit; set { canEdit = value; Changed(nameof(CanEditCoopPackage)); } }
        public string ActiveCoopPackageId { get => selectedId; set { selectedId = value; Changed(nameof(SelectedCoopPackage)); } }
        public ComboBoxItem SelectedCoopPackage
        {
            get { int index = Array.IndexOf(ids, selectedId); return CoopPackageOptions[index < 0 ? 0 : index]; }
            set { SelectionWrites++; if (!RejectSelection) ActiveCoopPackageId = ids[Array.IndexOf(CoopPackageOptions, value)]; }
        }
        internal void SetOptions(string[] keys, string[] labels)
        {
            ids = keys;
            CoopPackageOptions = labels.Select(label => new ComboBoxItem { Content = label }).ToArray();
            Changed(nameof(CoopPackageOptions));
            Changed(nameof(SelectedCoopPackage));
        }
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

namespace SHCDESE.NoesisUtil
{
    internal interface INoesisElementBindingAware { void OnNoesisElementBound(FrameworkElement element); }
}

namespace Noesis
{
    public class EventArgs : System.EventArgs { }
    public delegate void EventHandler(object sender, EventArgs args);
    public class RoutedEventArgs : System.EventArgs { public bool Handled { get; set; } }
    public class MouseEventArgs : RoutedEventArgs
    {
        public float X, Y;
        public Point GetPosition(FrameworkElement element) => new Point((X - element.X) / element.Scale, (Y - element.Y) / element.Scale);
    }
    public sealed class MouseButtonEventArgs : MouseEventArgs { }
    public sealed class KeyEventArgs : RoutedEventArgs { public Key Key; }
    public enum Key { Escape }
    public sealed class DependencyPropertyChangedEventArgs : System.EventArgs { public object NewValue; }
    public sealed class SizeChangedEventArgs : System.EventArgs { }
    public sealed class ScrollChangedEventArgs : System.EventArgs { }
    public struct Point { public float X, Y; public Point(float x, float y) { X = x; Y = y; } }
    public struct Thickness { public float Left, Top; public Thickness(float l, float t, float r, float b) { Left = l; Top = t; } }
    public enum HorizontalAlignment { Left, Center }
    public enum PlacementMode { Top, Bottom }
    public class DependencyObject { public DependencyObject Parent; }
    public class FrameworkElement : DependencyObject
    {
        public float X, Y, ActualWidth, ActualHeight, Scale = 1, Width, MaxDropDownHeight;
        public Thickness Margin;
        public HorizontalAlignment HorizontalAlignment;
        private Visibility visibility;
        public int VisibilityWrites;
        public Visibility Visibility { get => visibility; set { visibility = value; VisibilityWrites++; } }
        public bool IsVisible = true, IsMouseCaptured;
        public Dictionary<string, object> Names = new();
        public event System.EventHandler<MouseButtonEventArgs> PreviewMouseDown, PreviewMouseUp;
        public event System.EventHandler<MouseEventArgs> PreviewMouseMove, MouseLeave;
        public event System.EventHandler<KeyEventArgs> PreviewKeyDown;
        public event System.EventHandler<DependencyPropertyChangedEventArgs> IsVisibleChanged, DataContextChanged;
        public event System.EventHandler<RoutedEventArgs> Unloaded, Loaded;
        public event System.EventHandler<SizeChangedEventArgs> SizeChanged;
        public event EventHandler LayoutUpdated;
        public object FindName(string name) => Names.TryGetValue(name, out var result) ? result : null;
        public Point TranslatePoint(Point point, FrameworkElement relativeTo) =>
            new Point((X + point.X * Scale - relativeTo.X) / relativeTo.Scale, (Y + point.Y * Scale - relativeTo.Y) / relativeTo.Scale);
        public bool CaptureMouse() { IsMouseCaptured = true; return true; }
        public void ReleaseMouseCapture() => IsMouseCaptured = false;
        public MouseButtonEventArgs Down(float x, float y) { var e = new MouseButtonEventArgs { X = x, Y = y }; PreviewMouseDown?.Invoke(this, e); return e; }
        public MouseButtonEventArgs Up(float x, float y) { var e = new MouseButtonEventArgs { X = x, Y = y }; PreviewMouseUp?.Invoke(this, e); return e; }
        public KeyEventArgs PressKey(Key key) { var e = new KeyEventArgs { Key = key }; PreviewKeyDown?.Invoke(this, e); return e; }
        public void Hide() { IsVisible = false; IsVisibleChanged?.Invoke(this, new DependencyPropertyChangedEventArgs { NewValue = false }); }
        public void Unload() => Unloaded?.Invoke(this, new RoutedEventArgs());
        public void Load() => Loaded?.Invoke(this, new RoutedEventArgs());
        public void Layout() => LayoutUpdated?.Invoke(this, new EventArgs());
        public void Resize() => SizeChanged?.Invoke(this, new SizeChangedEventArgs());
        public void ChangeContext() => DataContextChanged?.Invoke(this, new DependencyPropertyChangedEventArgs());
        public void Move(float x, float y) { Mouse.X = x; Mouse.Y = y; PreviewMouseMove?.Invoke(this, new MouseEventArgs { X = x, Y = y }); }
        public void Leave(float x, float y) { Mouse.X = x; Mouse.Y = y; MouseLeave?.Invoke(this, new MouseEventArgs { X = x, Y = y }); }
    }
    public sealed class Border : FrameworkElement { }
    public sealed class ComboBox : FrameworkElement { public FrameworkTemplate Template; public void ApplyTemplate() { } }
    public sealed class ScrollViewer : FrameworkElement
    {
        public FrameworkTemplate Template = new FrameworkTemplate();
        public event System.EventHandler<ScrollChangedEventArgs> ScrollChanged;
        public void Scroll() => ScrollChanged?.Invoke(this, new ScrollChangedEventArgs());
    }
    public sealed class Popup
    {
        private bool isOpen;
        public bool IsOpen { get => isOpen; set { if (isOpen == value) return; isOpen = value; if (value) Opened?.Invoke(this, new EventArgs()); else Closed?.Invoke(this, new EventArgs()); } }
        public event EventHandler Opened, Closed;
        public FrameworkElement Child;
        public PlacementMode Placement;
    }
    public sealed class FrameworkTemplate
    {
        public Popup Popup;
        public Dictionary<string, object> Names = new();
        public object FindName(string name, FrameworkElement parent) => name == "PART_Popup" ? Popup : Names.GetValueOrDefault(name);
    }
    public static class Mouse
    {
        public static float X, Y;
        public static Point GetPosition(FrameworkElement relativeTo) => new Point((X - relativeTo.X) / relativeTo.Scale, (Y - relativeTo.Y) / relativeTo.Scale);
    }
    public struct HitTestResult { public DependencyObject VisualHit; }
    public static class VisualTreeHelper
    {
        public static DependencyObject Hit;
        public static int HitTests;
        public static DependencyObject GetParent(DependencyObject element) => element.Parent;
        public static HitTestResult HitTest(FrameworkElement element, Point point) { HitTests++; return new HitTestResult { VisualHit = Hit }; }
    }
}
