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
        private bool confirmDefaults;
        private string pendingRevision, pendingLabel;
        private PresetSaveSettingViewModel[] filtered = Array.Empty<PresetSaveSettingViewModel>();
        protected override IDynamicPresetSettingsProvider DynamicSettingsProvider => provider;
        protected override string ResolveSettingsUiText(string key, string fallback)
        {
            switch (key)
            {
                case "Common.PresetLoad": return T("SelectPreset");
                case "Common.PresetModeDefault": return T("ModeDefault");
                case "Common.PresetModePlayer": return T("ModeOwn");
                case "Common.PresetModeFixed": return T("ModeSaved");
                case "Common.PresetSaveBulkModeHelp": return T("ModesHelp");
                case "Common.PresetLoadSelectionHelp": return T("SelectionHelp");
                case "Common.PresetLoadConfirm": return T("Stage");
            }
            string text = SerpLocalization.Get(key);
            return string.IsNullOrEmpty(text) || text == key ? fallback : text;
        }
        private static string T(string key) => SerpLocalization.Get("Tweaker." + key);
        internal StatsTweakerPresetViewModel(StatsTweakerConfigurationProvider provider)
        {
            this.provider = provider;
            ReadFilesCommand = new RelayCommand(() => Run(ImportOwnFiles));
            SaveOwnCommand = new RelayCommand(() => Run(() =>
            {
                ImportOwnFiles();
                System_OpenPresetSaveCommand.Execute(null);
                System_PresetSaveBulkModeIndex = (int)PresetSaveBulkMode.Fixed;
            }));
            OpenDefaultsCommand = new RelayCommand(() => Run(() => { RequireEditable(); confirmDefaults = true; }));
            CancelDefaultsCommand = new RelayCommand(() => { confirmDefaults = false; RaiseStatus(); });
            ConfirmDefaultsCommand = new RelayCommand(() => Run(() =>
            {
                RequireEditable();
                if (!confirmDefaults) return;
                provider.ReadOwn(); // Refresh the optimistic concurrency revision from our files.
                provider.StageValues(provider.GetSettings().ToDictionary(x => x.Key, x => x.DefaultValue, StringComparer.Ordinal));
                RememberPending(T("DefaultsPending"));
                confirmDefaults = false;
            }));
            DiscardCommand = new RelayCommand(() => Run(() =>
            {
                provider.Discard(); pendingRevision = pendingLabel = null; status = T("Discarded");
            }));
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
            };
        }
        internal void InitializeSelection() => System_OpenPresetLoadCommand.Execute(null);
        private void RequireEditable()
        {
            if (!CanChangePreset || !CanEditHostSettings) throw new InvalidOperationException(HostReadOnlyNoticeText);
        }
        protected override void ApplyConfirmedPresetSelection(PublishedModSettingsPreset preset)
        {
            RequireEditable();
            ImportOwnFiles();
            status = "";
            base.ApplyConfirmedPresetSelection(preset);
            provider.Stage();
            RememberPending(string.Format(T("PresetPending"), preset.Name));
            confirmDefaults = false;
            RaiseStatus();
        }
        private void RememberPending(string label)
        {
            pendingRevision = PendingRevision;
            pendingLabel = label;
            status = T("Staged");
        }
        private string PendingRevision
        {
            get
            {
                object snapshot = provider.Call("GetPendingConfiguration");
                return snapshot == null ? null : StatsTweakerConfigurationProvider.Read<string>(snapshot, "Revision");
            }
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
            OnPropertyChanged(nameof(TechnicalStatus));
            OnPropertyChanged(nameof(LoadedText));
            OnPropertyChanged(nameof(PendingText));
            OnPropertyChanged(nameof(PendingSummary));
            OnPropertyChanged(nameof(HasPending));
            OnPropertyChanged(nameof(DefaultsConfirmationVisibility));
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
        public string TechnicalStatus => StatsTweakerConfigurationProvider.Read<string>(provider.Call("GetCapabilities"), "Status");
        public bool HasPending { get { try { return PendingRevision != null; } catch { return true; } } }
        public string PendingSummary
        {
            get
            {
                try
                {
                    string revision = PendingRevision;
                    return revision == null ? T("NonePending") : revision == pendingRevision && pendingLabel != null ? pendingLabel : T("UnknownPending");
                }
                catch (Exception ex) { return T("PendingError") + ": " + ex.GetBaseException().Message; }
            }
        }
        public Noesis.Visibility DefaultsConfirmationVisibility => confirmDefaults ? Noesis.Visibility.Visible : Noesis.Visibility.Collapsed;
        public string SaveOwnText => T("SaveOwn");
        public string DefaultsText => T("Defaults");
        public string DefaultsHelp => T("DefaultsHelp");
        public string DefaultsConfirmText => T("DefaultsConfirm");
        public string ShareText => T("Share");
        public string DetailsText => T("Details");
        public string AdvancedText => T("Advanced");
        public string SelectionHelp => T("SelectionHelp");
        public string ModesHelp => T("ModesHelp");
        public string LoadedText => T("Loaded") + ": " + provider.Describe("GetLoadedConfiguration");
        public string PendingText
        {
            get { try { return T("Pending") + ": " + provider.Describe("GetPendingConfiguration"); } catch (Exception ex) { return T("Pending") + ": " + ex.GetBaseException().Message; } }
        }
        public string RestartText => T("Restart");
        public string ReadFilesText => T("ReadFiles");
        public string ReadFilesHelp => T("ReadFilesHelp");
        public string StageText => T("Stage");
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
        public RelayCommand ReadFilesCommand { get; }
        public RelayCommand SaveOwnCommand { get; }
        public RelayCommand OpenDefaultsCommand { get; }
        public RelayCommand CancelDefaultsCommand { get; }
        public RelayCommand ConfirmDefaultsCommand { get; }
        public RelayCommand DiscardCommand { get; }
        public RelayCommand PreviousPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand ImportPresetCommand { get; }
        public RelayCommand ExportPresetCommand { get; }
    }
}
