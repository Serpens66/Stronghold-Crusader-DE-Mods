$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
function ReadText($path) { [IO.File]::ReadAllText((Join-Path $root $path)).Replace("`r`n", "`n") }
function WriteText($path, $text) {
    $expected = $text.Replace("`r`n", "`n").Replace("`n", "`r`n")
    $target = Join-Path $root $path
    [IO.File]::WriteAllText($target, $expected)
    if (![string]::Equals([IO.File]::ReadAllText($target), $expected, [StringComparison]::Ordinal)) { throw "Write mismatch: $path" }
}
$path='ExtendedData/src/ExtendedDataSettingsViewModel.cs'
$s=ReadText $path
$s=$s.Replace('private string[] fixedTrailPropertyIds = Array.Empty<string>();', @'
private string[] fixedTrailPropertyIds = Array.Empty<string>();
        private HashSet<string> playerModeIndex = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> fixedModeIndex = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Tuple<object, string, TrailModSelectionItem>> selectionCache =
            new Dictionary<string, Tuple<object, string, TrailModSelectionItem>>(StringComparer.Ordinal);
'@)
$s=$s.Replace('fixedTrailPropertyIds.Contains(id, StringComparer.Ordinal)', 'fixedModeIndex.Contains(id)').Replace('playerTrailPropertyIds.Contains(id, StringComparer.Ordinal)', 'playerModeIndex.Contains(id)')
$s=$s.Replace(@'
                string[] normalized = NormalizePropertyIds(value);
                SetTrailPropertyModeIds(
                    playerTrailPropertyIds.Where(id => !normalized.Contains(id, StringComparer.Ordinal)).ToArray(),
                    normalized);
'@,@'
                SetTrailPropertyModeIds(playerTrailPropertyIds, NormalizePropertyIds(value));
'@)
$s=$s.Replace('var player = new List<string>();', 'var player = new HashSet<string>(StringComparer.Ordinal);').Replace('var fixedValues = new List<string>();','var fixedValues = new HashSet<string>(StringComparer.Ordinal);')
# Only the whole-document lists became sets; the per-mod lists retain AddRange.
$start=$s.IndexOf('internal void ApplyTrailSettingModes('); $end=$s.IndexOf('internal void ApplyTrailSettingModesForMod', $start)
$block=$s.Substring($start,$end-$start).Replace('player.AddRange(', 'player.UnionWith(').Replace('fixedValues.AddRange(', 'fixedValues.UnionWith(')
$s=$s.Substring(0,$start)+$block+$s.Substring($end)
$s=$s.Replace('foreach (TrailSettingSelectionItem setting in mod.Settings)', 'foreach (TrailSettingGroupDefinition setting in mod.Definitions)')
$s=$s.Replace('player = player.Where(id => !fixedValues.Contains(id, StringComparer.Ordinal)).ToArray();', "var fixedSet = new HashSet<string>(fixedValues, StringComparer.Ordinal);`n            player = player.Where(id => !fixedSet.Contains(id)).ToArray();")
$s=$s.Replace('fixedTrailPropertyIds = fixedValues;', "fixedTrailPropertyIds = fixedValues;`n            playerModeIndex = new HashSet<string>(player, StringComparer.Ordinal);`n            fixedModeIndex = fixedSet;")
$s=$s.Replace('internal void RefreshModCompatibility(IEnumerable<TrailModCompatibilityInfo> entries)', 'internal void RefreshModCompatibility(IEnumerable<TrailModCompatibilityInfo> entries, Action<string> diagnostics = null)')
$s=$s.Replace('TrailModCompatibilityInfo[] catalog = (entries', "var watch = Stopwatch.StartNew();`n            int rebuilt = 0;`n            TrailModCompatibilityInfo[] catalog = (entries")
$old=@'
                .Select(entry => new TrailModSelectionItem(
                    entry.ModId,
                    entry.DisplayName,
                    BuildSettingGroups(entry),
                    GetTrailPropertyMode,
                    SetTrailPropertiesMode,
                    TrailSettingModeHelpText))
'@
$new=@'
                .Select(entry =>
                {
                    string signature = entry.DisplayName + "\n" + TrailSettingModeHelpText + "\n" +
                        SerpLocalization.Get("ExtendedData.Mode.ModDefault") + "\n" +
                        SerpLocalization.Get("ExtendedData.Mode.Player") + "\n" +
                        SerpLocalization.Get("ExtendedData.Mode.Fixed") + "\n" +
                        SerpLocalization.Get("ExtendedData.Mode.Mixed") + "\n" +
                        System.Globalization.CultureInfo.CurrentCulture.Name + "\n" +
                        string.Join("\n", entry.Properties.Select(p => p.Name + ":" + p.PropertyType.AssemblyQualifiedName + ":" + p.IsDefined(typeof(Shared.RequiresRestartAttribute), true)));
                    if (selectionCache.TryGetValue(entry.ModId, out var cached) &&
                        ReferenceEquals(cached.Item1, entry.Endpoint) && cached.Item2 == signature)
                        return cached.Item3;
                    var model = new TrailModSelectionItem(entry.ModId, entry.DisplayName, BuildSettingGroups(entry),
                        GetTrailPropertyMode, SetTrailPropertiesMode, TrailSettingModeHelpText, diagnostics);
                    if (cached != null) { model.Filter = cached.Item3.Filter; model.IsExpanded = cached.Item3.IsExpanded; }
                    selectionCache[entry.ModId] = Tuple.Create(entry.Endpoint, signature, model);
                    rebuilt++;
                    return model;
                })
'@
if (!$s.Contains($old)) {throw 'compatibility anchor missing'}
$s=$s.Replace($old,$new)
$s=$s.Replace('            incompatibleTrailModsText = string.Join', @'
            var retained = new HashSet<string>(compatibleTrailMods.Select(x => x.ModId), StringComparer.Ordinal);
            foreach (string id in selectionCache.Keys.Where(x => !retained.Contains(x)).ToArray()) selectionCache.Remove(id);
            if (rebuilt != 0) diagnostics?.Invoke("[PresetPerf] compatibility models: rebuilt=" + rebuilt + ", options=" + catalog.Sum(x => x.Properties.Length) + ", ms=" + watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            incompatibleTrailModsText = string.Join
'@)
$start=$s.IndexOf('    public sealed class TrailModSelectionItem'); $end=$s.IndexOf('        internal static ComboBoxItem[] CreateModeOptions', $start)
$s=$s.Substring(0,$start)+@'
    public sealed class TrailModSelectionItem : INotifyPropertyChanged
    {
        private readonly Action<string, IEnumerable<string>, TrailSettingMode> changed;
        private readonly Func<string, string, TrailSettingMode> getMode;
        private readonly DeferredSettingsPage<TrailSettingGroupDefinition, TrailSettingSelectionItem> page;
        private readonly Action<string> diagnostics;
        internal TrailModSelectionItem(string modId, string displayName, IEnumerable<TrailSettingGroupDefinition> definitions,
            Func<string, string, TrailSettingMode> getMode, Action<string, IEnumerable<string>, TrailSettingMode> changed,
            string helpText, Action<string> diagnostics = null)
        {
            ModId = modId; DisplayName = displayName; HelpText = helpText;
            this.changed = changed; this.getMode = getMode; this.diagnostics = diagnostics;
            ModeOptions = CreateModeOptions(includeMixed: true);
            page = new DeferredSettingsPage<TrailSettingGroupDefinition, TrailSettingSelectionItem>(definitions,
                (d, term) => d.DisplayName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 || d.PropertyNames.Any(x => x.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0),
                d => new TrailSettingSelectionItem(modId, d.Key, d.DisplayName, d.PropertyNames, getMode, changed, OnSettingChanged, helpText));
            SearchText = string.Join(" ", new[] { displayName }.Concat(page.Definitions.Select(x => x.DisplayName)));
            PreviousPageCommand = new ActionCommand(() => { page.Page--; RefreshPage(); });
            NextPageCommand = new ActionCommand(() => { page.Page++; RefreshPage(); });
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public string ModId { get; }
        public string DisplayName { get; }
        public string HelpText { get; }
        public string SearchText { get; }
        public ComboBoxItem[] ModeOptions { get; }
        internal IReadOnlyList<TrailSettingGroupDefinition> Definitions => page.Definitions;
        public TrailSettingSelectionItem[] Settings
        {
            get
            {
                var watch = Stopwatch.StartNew(); int before = page.CreatedRows;
                var rows = page.Rows;
                if (page.CreatedRows != before) diagnostics?.Invoke("[PresetPerf] visible rows: mod=" + ModId + ", created=" + (page.CreatedRows - before) + ", total=" + page.Count + ", ms=" + watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                return rows;
            }
        }
        public bool IsExpanded { get => page.Expanded; set { if (page.Expanded == value) return; page.Expanded = value; RefreshPage(); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded))); } }
        public string Filter { get => page.Filter; set { page.Filter = value; RefreshPage(); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Filter))); } }
        public Visibility PagingVisibility => IsExpanded && page.Count > DeferredSettingsPage<TrailSettingGroupDefinition, TrailSettingSelectionItem>.PageSize ? Visibility.Visible : Visibility.Collapsed;
        public string PageText => (page.Page + 1) + " / " + page.PageCount + " · " + page.FilteredCount;
        public string FilterHelp => SerpLocalization.Get("ExtendedData.Options.Filter");
        public string PreviousHelp => SerpLocalization.Get("ExtendedData.Options.Previous");
        public string NextHelp => SerpLocalization.Get("ExtendedData.Options.Next");
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        private void RefreshPage()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Settings)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PagingVisibility)));
        }
        public int SelectedModeIndex
        {
            get
            {
                int first = -1;
                foreach (var definition in page.Definitions)
                    foreach (string property in definition.PropertyNames)
                    {
                        int current = (int)getMode(ModId, property);
                        if (first < 0) first = current; else if (first != current) return 3;
                    }
                return first < 0 ? 3 : first;
            }
            set
            {
                if (value >= 0 && value <= (int)TrailSettingMode.Fixed)
                    changed?.Invoke(ModId, page.Definitions.SelectMany(x => x.PropertyNames), (TrailSettingMode)value);
            }
        }
        internal void RefreshState()
        {
            foreach (var setting in page.MaterializedRows) setting.RefreshState();
            OnSettingChanged();
        }
        private void OnSettingChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedModeIndex)));

'@+$s.Substring($end)
WriteText $path $s
$path='ExtendedData/src/TrailMissionSettingsCoordinator.cs'; $s=ReadText $path
$s=$s.Replace('string incompatibilityReason)', 'string incompatibilityReason, object endpoint = null)')
$s=$s.Replace('ModId = modId;', 'ModId = modId; Endpoint = endpoint;')
$s=$s.Replace('public string ModId { get; }', "public object Endpoint { get; }`n        public string ModId { get; }")
$s=$s.Replace('                        incompatibility));','                        incompatibility, sharedCompatible ? (object)sharedParticipants[modId] : entry?.ViewModel));')
WriteText $path $s
$path='ExtendedData/src/ExtendedDataRuntime.cs'; $s=ReadText $path
$s=$s.Replace('            settings.RefreshModCompatibility(catalog);', '            settings.RefreshModCompatibility(catalog, message => LogInfo(message));')
WriteText $path $s
