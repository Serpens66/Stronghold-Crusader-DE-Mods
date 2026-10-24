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

    private static FrameworkElement CreateHost(out Border shield, out ComboBox dropdown, out Popup popup)
    {
        var host = new FrameworkElement();
        shield = new Border();
        popup = new Popup { Child = new FrameworkElement { X = 700, Y = 190, ActualWidth = 520, ActualHeight = 200 } };
        dropdown = new ComboBox { X = 700, Y = 150, ActualWidth = 520, ActualHeight = 40 };
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
    public class RoutedEventArgs : EventArgs { public bool Handled { get; set; } }
    public class MouseEventArgs : RoutedEventArgs
    {
        public double X, Y;
        public Point GetPosition(FrameworkElement element) => new Point { X = X - element.X, Y = Y - element.Y };
    }
    public sealed class MouseButtonEventArgs : MouseEventArgs { }
    public sealed class KeyEventArgs : RoutedEventArgs { public Key Key; }
    public enum Key { Escape }
    public sealed class DependencyPropertyChangedEventArgs : EventArgs { public object NewValue; }
    public struct Point { public double X, Y; }
    public class FrameworkElement
    {
        public double X, Y, ActualWidth, ActualHeight;
        public bool IsVisible = true, IsMouseCaptured;
        public Dictionary<string, object> Names = new();
        public event EventHandler<MouseButtonEventArgs> PreviewMouseDown, PreviewMouseUp;
        public event EventHandler<KeyEventArgs> PreviewKeyDown;
        public event EventHandler<DependencyPropertyChangedEventArgs> IsVisibleChanged;
        public event EventHandler<RoutedEventArgs> Unloaded;
        public object FindName(string name) => Names[name];
        public bool CaptureMouse() { IsMouseCaptured = true; return true; }
        public void ReleaseMouseCapture() => IsMouseCaptured = false;
        public MouseButtonEventArgs Down(double x, double y) { var e = new MouseButtonEventArgs { X = x, Y = y }; PreviewMouseDown?.Invoke(this, e); return e; }
        public MouseButtonEventArgs Up(double x, double y) { var e = new MouseButtonEventArgs { X = x, Y = y }; PreviewMouseUp?.Invoke(this, e); return e; }
        public KeyEventArgs PressKey(Key key) { var e = new KeyEventArgs { Key = key }; PreviewKeyDown?.Invoke(this, e); return e; }
        public void Hide() { IsVisible = false; IsVisibleChanged?.Invoke(this, new DependencyPropertyChangedEventArgs { NewValue = false }); }
        public void Unload() => Unloaded?.Invoke(this, new RoutedEventArgs());
    }
    public sealed class Border : FrameworkElement { }
    public sealed class ComboBox : FrameworkElement { public FrameworkTemplate Template; }
    public sealed class Popup { public bool IsOpen; public FrameworkElement Child; }
    public sealed class FrameworkTemplate { public Popup Popup; public object FindName(string name, FrameworkElement parent) => Popup; }
}
