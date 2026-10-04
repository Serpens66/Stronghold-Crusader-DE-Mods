using MessagePack;
using Shared;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SerpsModsHost
{
    public sealed class StatsTweakerPresetViewModel : PresetLobbyModSettingsViewModel
    {
        private const int PageSize = 64;
        private readonly StatsTweakerConfigurationProvider provider;
        private string filter = "", group = "", status = "", presetFile = "";
        private int page;
        private PresetSaveSettingViewModel[] filtered = Array.Empty<PresetSaveSettingViewModel>();
        protected override IDynamicPresetSettingsProvider DynamicSettingsProvider => provider;
        protected override string ResolveSettingsUiText(string key, string fallback)
        {
            string text = SerpLocalization.Get(key);
            return string.IsNullOrEmpty(text) || text == key ? fallback : text;
        }
        private static string T(string key) => SerpLocalization.Get("Tweaker." + key);
        internal StatsTweakerPresetViewModel(StatsTweakerConfigurationProvider provider)
        {
            this.provider = provider;
            DiscardCommand = new RelayCommand(() => Run(System_DiscardPendingConfiguration));
            PreviousPageCommand = new RelayCommand(() => { if (page > 0) page--; RefreshRows(false); });
            NextPageCommand = new RelayCommand(() => { if ((page + 1) * PageSize < filtered.Length) page++; RefreshRows(false); });
            ImportPresetCommand = new RelayCommand(() => Run(() =>
            {
                if (!CanChangePreset) throw new InvalidOperationException(HostReadOnlyNoticeText);
                string path = Path.GetFullPath(PresetFile);
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > 8 * 1024 * 1024) throw new InvalidDataException("Preset exceeds 8 MiB.");
                    using (var reader = new StreamReader(stream)) System_ImportPresetJson(reader.ReadToEnd());
                }
                status = T("Imported");
            }));
            ExportPresetCommand = new RelayCommand(() => Run(() =>
            {
                string json = System_ExportSelectedPresetJson();
                // Never silently replace a user's existing file.
                using (var stream = new FileStream(Path.GetFullPath(PresetFile), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false))) writer.Write(json);
                status = T("Exported");
            }));
            PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(System_PresetSaveSettings)) RefreshRows(true);
                if (args.PropertyName == nameof(System_HasPendingConfiguration)) OnPropertyChanged(nameof(ApplicationPendingVisibility));
            };
        }
        internal void ImportOwnFiles()
        {
            if (!CanChangePreset) throw new InvalidOperationException(HostReadOnlyNoticeText);
            var own = provider.ReadOwn();
            provider.ReplaceValues(own); // This is our own working copy, never received host state.
            System_ApplyWorkingSnapshot(provider.GetSettings().ToDictionary(item => item.Key,
                item => MessagePackSerializer.Serialize(item.ValueType, own[item.Key]), StringComparer.Ordinal));
            status = T("ReadDone");
            RaiseStatus();
        }
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { status = ex.GetBaseException().Message; }
            RaiseStatus();
        }
        protected override void OnSettingsSnapshotApplied() => RaiseStatus();
        private void RaiseStatus()
        {
            OnPropertyChanged(nameof(StatusText));
        }
        private void RefreshRows(bool reset)
        {
            if (reset) page = 0;
            filtered = System_PresetSaveSettings.Where(item =>
                (string.IsNullOrEmpty(group) || item.Group == group) &&
                (string.IsNullOrWhiteSpace(filter) || item.PropertyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(item => item.Group, StringComparer.Ordinal).ThenBy(item => item.PropertyName, StringComparer.Ordinal).ToArray();
            page = Math.Min(page, Math.Max(0, (filtered.Length - 1) / PageSize));
            OnPropertyChanged(nameof(FilteredRows));
            OnPropertyChanged(nameof(Groups));
            OnPropertyChanged(nameof(PageText));
        }
        public PresetSaveSettingViewModel[] FilteredRows => filtered.Skip(page * PageSize).Take(PageSize).ToArray();
        public string[] Groups => new[] { "" }.Concat(System_PresetSaveSettings.Select(item => item.Group).Distinct().OrderBy(x => x)).ToArray();
        public string Filter { get => filter; set { filter = value ?? ""; RefreshRows(true); } }
        public string Group { get => group; set { if (group == (value ?? "")) return; group = value ?? ""; RefreshRows(true); } }
        public string PageText => (page + 1) + " / " + Math.Max(1, (filtered.Length + PageSize - 1) / PageSize) + " · " + filtered.Length;
        public string PresetFile { get => presetFile; set { presetFile = value ?? ""; OnPropertyChanged(nameof(PresetFile)); } }
        public string StatusText => status;
        public Noesis.Visibility ApplicationPendingVisibility => System_HasPendingConfiguration ? Noesis.Visibility.Visible : Noesis.Visibility.Collapsed;
        public string DiscardText => T("Discard");
        public string FilterText => T("Filter");
        public string GroupText => T("Group");
        public string FilterHelp => T("FilterHelp");
        public string PreviousText => T("Previous");
        public string NextText => T("Next");
        public string PresetFileText => T("PresetFile");
        public string PresetFileHelp => T("PresetFileHelp");
        public string ImportText => T("Import");
        public string ExportText => T("Export");
        public RelayCommand DiscardCommand { get; }
        public RelayCommand PreviousPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand ImportPresetCommand { get; }
        public RelayCommand ExportPresetCommand { get; }
    }
}
