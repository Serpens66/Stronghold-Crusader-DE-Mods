using Noesis;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SerpsModsHost
{
    public sealed class SerpsModsDiagnosticsViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        private readonly object sync = new object();
        private readonly List<string> errors = new List<string>();
        private Action refreshAction;
        private string packVersion = "unknown";
        private int expectedCount;
        private int validatedCount;
        private int registeredCount;
        private ModSettingsSearchViewModel search;

        public SerpsModsDiagnosticsViewModel()
        {
            RefreshCommand = new RelayCommand(() => refreshAction?.Invoke());
            ClearErrorsCommand = new RelayCommand(ClearErrors);
        }

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ClearErrorsCommand { get; }
        public ModSettingsSearchViewModel Search => search;
        public string TitleText => SerpLocalization.Get(SerpLocalization.SerpsModsStatusTitle);
        public string SummaryTitleText => SerpLocalization.Get(SerpLocalization.SerpsModsSummaryTitle);
        public string ErrorsTitleText => SerpLocalization.Get(SerpLocalization.SerpsModsErrorsTitle);
        public string RefreshText => SerpLocalization.Get(SerpLocalization.SerpsModsRefresh);
        public string RefreshHelpText => SerpLocalization.Get(SerpLocalization.SerpsModsRefreshHelp);
        public string ClearErrorsText => SerpLocalization.Get(SerpLocalization.SerpsModsClearErrors);
        public string ClearErrorsHelpText => SerpLocalization.Get(SerpLocalization.SerpsModsClearErrorsHelp);
        public string NoErrorsText => SerpLocalization.Get(SerpLocalization.SerpsModsNoErrors);
        public string SummaryText => SerpLocalization.Get(
            SerpLocalization.SerpsModsSummaryFormat,
            "Version", packVersion,
            "Expected", expectedCount,
            "Validated", validatedCount,
            "Registered", registeredCount);
        public string ErrorsText
        {
            get
            {
                lock (sync)
                    return errors.Count == 0 ? NoErrorsText : string.Join(Environment.NewLine, errors.ToArray());
            }
        }
        public Visibility ErrorVisibility
        {
            get
            {
                lock (sync)
                    return errors.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public void SetRefreshAction(Action action) => refreshAction = action;

        public void SetSearch(ModSettingsSearchViewModel value)
        {
            search = value;
            OnPropertyChanged(nameof(Search));
        }

        public void SetStatus(string version, int expected, int validated, int registered)
        {
            packVersion = string.IsNullOrWhiteSpace(version) ? "unknown" : version;
            expectedCount = expected;
            validatedCount = validated;
            registeredCount = registered;
            OnPropertyChanged(nameof(SummaryText));
        }

        public void RecordError(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {text.Trim()}";
            lock (sync)
            {
                if (errors.Count > 199)
                    errors.RemoveAt(0);
                errors.Add(line);
            }
            OnPropertyChanged(nameof(ErrorsText));
            OnPropertyChanged(nameof(ErrorVisibility));
        }

        private void ClearErrors()
        {
            lock (sync)
                errors.Clear();
            OnPropertyChanged(nameof(ErrorsText));
            OnPropertyChanged(nameof(ErrorVisibility));
        }
    }
}
