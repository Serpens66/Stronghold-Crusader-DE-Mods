using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Path = System.IO.Path;

namespace BugfixesAndQoL
{
    public sealed class TrailDeletionViewModel : INotifyPropertyChanged
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private ListView importList;
        internal static TrailDeletionViewModel Current { get; private set; }

        internal TrailDeletionViewModel(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            DeleteTrailCommand = new RelayCommand(DeleteSelected, CanDelete);
            settings.PropertyChanged += SettingsChanged;
            Current = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public RelayCommand DeleteTrailCommand { get; }
        private bool Enabled => settings.EnableMod && settings.ShowLoadSaveDialogControls;
        public Visibility DeleteTrailVisibility => Enabled ? Visibility.Visible : Visibility.Collapsed;
        public bool DeleteTrailEnabled => CanDelete();
        public string DeleteTrailText => SerpLocalization.Get("BugfixesAndQoL.DeleteTrail");
        public HorizontalAlignment BackupAlignment => Enabled ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        public Thickness BackupMargin => Enabled ? new Thickness(40, 0, 0, 60) : new Thickness(0, 0, 0, 60);

        internal void Attach(ListView list)
        {
            importList = list;
            Refresh();
        }

        internal void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeleteTrailEnabled)));
            DeleteTrailCommand.RaiseCanExecuteChanged();
        }

        private void SettingsChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(BugfixesAndQoLViewModel.EnableMod) &&
                args.PropertyName != nameof(BugfixesAndQoLViewModel.ShowLoadSaveDialogControls))
                return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeleteTrailVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackupAlignment)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackupMargin)));
            Refresh();
        }

        private bool CanDelete() => Enabled && TryResolve(importList?.SelectedItem as FileRow, out _);

        private bool TryResolve(FileRow row, out string path)
        {
            path = null;
            if (row == null || string.IsNullOrWhiteSpace(row.Text1))
                return false;
            try
            {
                string root = ConfigSettings.GetUserCustomTrailsPath();
                MapFileManager.CustomTrailInfo source = row.trail;
                if (source == null)
                {
                    var matches = MapFileManager.Instance.GetCustomTrails()
                        .Where(item => string.Equals(item.Name, row.Text1, StringComparison.Ordinal)).ToArray();
                    if (matches.Length != 1) return false;
                    source = matches[0];
                }
                if (!string.Equals(source.Name, row.Text1, StringComparison.Ordinal)) return false;
                string sourcePath = string.IsNullOrWhiteSpace(source.FullPath)
                    ? Path.Combine(root, source.Name) : source.FullPath;
                return TrailDeletionPolicy.TryResolve(source.Name, sourcePath, source.workshop, root, out path);
            }
            catch (Exception) { return false; }
        }

        private void DeleteSelected()
        {
            FileRow row = importList?.SelectedItem as FileRow;
            if (!Enabled || !TryResolve(row, out string path)) return;
            ListView list = importList;
            try
            {
                HUD_ConfirmationPopup.ShowConfirmationMessage(
                    SerpLocalization.Get("BugfixesAndQoL.DeleteTrailConfirmTitle"),
                    () => DeleteConfirmed(list, row, path), Refresh,
                    SerpLocalization.Get("BugfixesAndQoL.DeleteTrailConfirmMessage", "TrailName", row.Text1),
                    MPConf: true, tall: true);
            }
            catch (Exception exception) { ShowError(row.Text1, exception); }
        }

        private void DeleteConfirmed(ListView list, FileRow row, string requestedPath)
        {
            try
            {
                var rows = list.ItemsSource as ObservableCollection<FileRow>;
                if (!Enabled || rows == null || !rows.Contains(row) ||
                    !TryResolve(row, out string currentPath) ||
                    !string.Equals(currentPath, requestedPath, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("The confirmed Trail source is no longer available or safe.");
                TrailDeletionPolicy.Delete(row.Text1, requestedPath, false, ConfigSettings.GetUserCustomTrailsPath());
                Shared.DebugLogHelper.LogInfo(log, $"Bugfixes and QoL permanently deleted local Trail [{requestedPath}]; no backup created.");
                rows.Remove(row);
                list.SelectedItem = null;
                if (FRONT_ManageTrail.Instance.FindName("ImportImportButton") is Button importButton)
                {
                    importButton.IsEnabled = false;
                    importButton.Opacity = 0.7f;
                }
                if (FRONT_ManageTrail.Instance.FindName("Import") is Button openButton)
                {
                    openButton.IsEnabled = rows.Count > 0;
                    openButton.Opacity = rows.Count > 0 ? 1f : 0.7f;
                }
                MapFileManager.Instance.RescanCustomTrailsFolder();
            }
            catch (Exception exception) { ShowError(row.Text1, exception); }
            finally { Refresh(); }
        }

        private void ShowError(string name, Exception exception)
        {
            Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL Trail deletion failed for [{name}]: {exception}");
            try
            {
                HUD_ConfirmationPopup.ShowConfirmationOKMessage(
                    SerpLocalization.Get("BugfixesAndQoL.DeleteTrailErrorTitle"), () => { },
                    SerpLocalization.Get("BugfixesAndQoL.DeleteTrailErrorMessage", "TrailName", name));
                MainViewModel.Instance.Show_HUD_Confirmation = false;
                MainViewModel.Instance.Show_HUD_ConfirmationMP = true;
                MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
            }
            catch (Exception popupException)
            {
                Shared.DebugLogHelper.LogError(log, "Bugfixes and QoL could not display the Trail deletion error: " + popupException);
            }
        }
    }

    public static class TrailDeletionListBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(TrailDeletionListBehavior), new PropertyMetadata(false, Changed));
        public static bool GetIsEnabled(DependencyObject value) => (bool)value.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject value, bool enabled) => value.SetValue(IsEnabledProperty, enabled);
        private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (!(target is ListView list)) return;
            list.Loaded -= Loaded;
            list.SelectionChanged -= SelectionChanged;
            if (args.NewValue is bool enabled && enabled)
            {
                list.Loaded += Loaded;
                list.SelectionChanged += SelectionChanged;
                TrailDeletionViewModel.Current?.Attach(list);
            }
        }
        private static void Loaded(object sender, RoutedEventArgs args) =>
            TrailDeletionViewModel.Current?.Attach(sender as ListView);
        private static void SelectionChanged(object sender, SelectionChangedEventArgs args) =>
            TrailDeletionViewModel.Current?.Attach(sender as ListView);
    }
}
