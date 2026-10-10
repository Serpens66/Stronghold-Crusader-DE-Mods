using System;
using System.Collections.Generic;
using System.ComponentModel;
namespace BepInEx.Logging { public class ManualLogSource { } }
namespace Shared {
    public static class DebugLogHelper {
        public static void LogInfo(object log, string message) { }
        public static void LogError(object log, string message) { }
    }
}
public static class SerpLocalization {
    public static string Get(string key, params object[] values) => key;
}
namespace SHCDESE.NoesisUtil {
    public class RelayCommand {
        private readonly Action action; private readonly Func<bool> can;
        public RelayCommand(Action action, Func<bool> can) { this.action = action; this.can = can; }
        public void RaiseCanExecuteChanged() { }
        public void Execute() { if (can()) action(); }
    }
}
namespace Noesis {
    public class Path { }
    public enum Visibility { Visible, Collapsed }
    public enum HorizontalAlignment { Left, Center, Right }
    public struct Thickness { public Thickness(int a, int b, int c, int d) { } }
    public class RoutedEventArgs : EventArgs { }
    public class SelectionChangedEventArgs : RoutedEventArgs { }
    public class DependencyPropertyChangedEventArgs : EventArgs { public object NewValue; }
    public class PropertyMetadata { public PropertyMetadata(bool value, Action<DependencyObject, DependencyPropertyChangedEventArgs> callback) { } }
    public class DependencyProperty { public static DependencyProperty RegisterAttached(string n, Type t, Type owner, PropertyMetadata m) => new DependencyProperty(); }
    public class DependencyObject {
        public object GetValue(DependencyProperty value) => false;
        public void SetValue(DependencyProperty property, object value) { }
    }
    public class ListView : DependencyObject {
        public object ItemsSource; public object SelectedItem;
        public event EventHandler<RoutedEventArgs> Loaded { add { } remove { } }
        public event EventHandler<SelectionChangedEventArgs> SelectionChanged { add { } remove { } }
    }
    public class Button { public bool IsEnabled; public float Opacity; }
}
namespace BugfixesAndQoL {
    public class BugfixesAndQoLViewModel : INotifyPropertyChanged {
        public bool EnableMod = true; public bool ShowLoadSaveDialogControls = true;
        public event PropertyChangedEventHandler PropertyChanged;
        public void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
namespace CrusaderDE {
    public class FileRow { public string Text1; public MapFileManager.CustomTrailInfo trail; }
    public class FRONT_ManageTrail {
        public static FRONT_ManageTrail Instance = new FRONT_ManageTrail();
        public Noesis.Button Import = new Noesis.Button();
        public Noesis.Button Action = new Noesis.Button();
        public object FindName(string name) => name == "Import" ? Import : Action;
    }
    public class HUD_ConfirmationPopup {
        public static Action Yes; public static Action No; public static bool MP; public static int Errors;
        public static void ShowConfirmationMessage(string title, Action yes, Action no, string message, bool MPConf, bool tall) { Yes = yes; No = no; MP = MPConf; }
        public static void ShowConfirmationOKMessage(string title, Action action, string message, bool Sands = false) { Errors++; }
    }
    public class FrontendMenus { public void UpdateFrontMenuPopupScale() { } }
    public class MainViewModel {
        public static MainViewModel Instance = new MainViewModel();
        public bool Show_HUD_Confirmation, Show_HUD_ConfirmationMP;
        public FrontendMenus FrontEndMenu = new FrontendMenus();
    }
}
public class ConfigSettings {
    public static string Root;
    public static string GetUserCustomTrailsPath() => Root;
}
public class MapFileManager {
    public class CustomTrailInfo { public string Name; public string FullPath; public bool workshop; }
    public static MapFileManager Instance = new MapFileManager();
    public List<CustomTrailInfo> Trails = new List<CustomTrailInfo>();
    public int Rescans;
    public List<CustomTrailInfo> GetCustomTrails() => Trails;
    public void RescanCustomTrailsFolder() { Rescans++; }
}
