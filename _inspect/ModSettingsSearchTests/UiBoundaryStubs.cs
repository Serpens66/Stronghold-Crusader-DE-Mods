// Deliberately no Noesis tree API: the actual host/catalog sources must compile and
// run using managed metadata alone. These doubles replace only external boundaries.
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
namespace BepInEx.Logging { public sealed class ManualLogSource { } }
namespace Noesis { public enum Visibility { Visible, Collapsed } }
namespace SHCDESE.NoesisUtil
{
    public sealed class RelayCommand
    {
        private readonly Action action;
        public RelayCommand(Action action) { this.action = action; }
        public void Execute(object parameter = null) => action();
    }
}
namespace SHCDESE.API.Components.ModManager
{
    public sealed class LobbyModSettingsEntry
    {
        public string Name { get; set; }
        public object ViewModel { get; set; }
        public object View => throw new InvalidOperationException("Native view was accessed");
    }
}
namespace SHCDESE.API
{
    public sealed class GameXAMLManagerAPI
    {
        public static GameXAMLManagerAPI Instance = new GameXAMLManagerAPI();
        public ObservableCollection<Components.ModManager.LobbyModSettingsEntry> RegisteredModSettings { get; } =
            new ObservableCollection<Components.ModManager.LobbyModSettingsEntry>();
    }
}
namespace SHCDESE.ViewModels
{
    public sealed class LobbyModSettingsHubViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public Noesis.Visibility WindowVisibility { get; set; }
        public SHCDESE.API.Components.ModManager.LobbyModSettingsEntry SelectedTab { get; set; }
        public void Open()
        {
            WindowVisibility = Noesis.Visibility.Visible;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WindowVisibility)));
        }
    }
}
namespace SHCDESE.BepInEx.Bootstrap
{
    public static class Plugin
    {
        public static SHCDESE.ViewModels.LobbyModSettingsHubViewModel ModSettingsHubViewModel =
            new SHCDESE.ViewModels.LobbyModSettingsHubViewModel();
    }
}
namespace APIShared.Internal
{
    public static class DebugLogHelper
    {
        public static int Warnings;
        public static void LogWarning(object log, string message) { Warnings++; }
    }
}
namespace Shared
{
    public static class DebugLogHelper
    {
        public static bool DiagnosticsEnabled => false;
        public static int Warnings;
        public static void LogWarningLimited(object log, string key, string message) { Warnings++; }
        // Any caught failure in the host must fail this harness, not disappear into a log double.
        public static void LogErrorLimited(object log, string key, string message) => throw new Exception(message);
    }
}
namespace SerpsModsHost
{
    public sealed class SerpsModsDiagnosticsViewModel { }
    public static class SerpLocalization
    {
        public const string SerpsModsSearchLabel="", SerpsModsSearchHelp="", SerpsModsSearchClearHelp="",
            SerpsModsSearchModFilterHelp="", SerpsModsSearchIncludeToolTips="", SerpsModsSearchIncludeToolTipsHelp="",
            SerpsModsSearchResultsTitle="", SerpsModsSearchNoResults="", SerpsModsSearchResultCount="",
            SerpsModsSearchAllMods="", SerpsModsSearchOpenResultHelp="";
        public static string Get(string key, params object[] values) => key;
    }
}
