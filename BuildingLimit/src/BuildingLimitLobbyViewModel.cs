using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace BuildingLimit
{
    public sealed class BuildingLimitLobbyViewModel : LobbyModSettingsBaseViewModel
    {
        public event Action<string> SettingChanged;

        private string buildingLimits = DefaultBuildingLimits;
        private bool updatingEntries;

        public const string DefaultBuildingLimits = @"# -1 = unlimited
MAPPER_WOODSMAN=-1
MAPPER_HUNTER=-1
MAPPER_OXENBASE=-1
MAPPER_QUARRY=-1
MAPPER_IRON_MINE=-1
MAPPER_PITCH_WORKINGS=-1
MAPPER_WHEATFARM=-1
MAPPER_HOPSFARM=-1
MAPPER_APPLEFARM=-1
MAPPER_CATTLEFARM=-1
MAPPER_MILL=-1
MAPPER_BAKER=-1
MAPPER_BREWER=-1
MAPPER_HOVEL=-1
MAPPER_GRANARY=-1
MAPPER_STORES=-1
MAPPER_ARMOURY=-1
MAPPER_TRADEPOST=-1
MAPPER_INN=-1
MAPPER_HEALER=-1
MAPPER_FLETCHER=-1
MAPPER_POLETURNER=-1
MAPPER_BLACKSMITH=-1
MAPPER_ARMOURER=-1
MAPPER_TANNER=-1
MAPPER_STABLES=-1
MAPPER_BARRACKS_WOOD=-1
MAPPER_BARRACKS_STONE=-1
MAPPER_ENGINEERS_GUILD=-1
MAPPER_TUNNELERS_GUILD=-1
MAPPER_OIL_SMELTER=-1
MAPPER_WELL=-1
MAPPER_WATERPOT=-1
MAPPER_CHURCH1=-1
MAPPER_CHURCH2=-1
MAPPER_CHURCH3=-1
MAPPER_TOWER1=-1
MAPPER_TOWER2=-1
MAPPER_TOWER3=-1
MAPPER_TOWER4=-1
MAPPER_TOWER5=-1
MAPPER_GATE_MAIN=-1
MAPPER_GATE_INNER=-1
MAPPER_GATE_WOOD=-1
MAPPER_GATEHOUSE=-1
MAPPER_GATE_POSTERN=-1
MAPPER_DRAWBRIDGE=-1
MAPPER_KILLING_PIT=-1
MAPPER_BRAZIER=-1
MAPPER_MANGONEL=-1
MAPPER_BALLISTA=-1
MAPPER_MAYPOLE=-1
MAPPER_GALLOWS=-1
MAPPER_STOCKS=-1
MAPPER_GARDEN1=-1
MAPPER_CESS_PIT1=-1
MAPPER_BURNING_STAKE=-1
MAPPER_GIBBET=-1
MAPPER_DUNGEON=-1
MAPPER_RACK_STRETCHING=-1
MAPPER_RACK_FLOGGING=-1
MAPPER_CHOPPING_BLOCK=-1
MAPPER_DUNKING_STOOL=-1
MAPPER_DOG_CAGE=-1
MAPPER_STATUE1=-1
MAPPER_SHRINE1=-1
MAPPER_BEE_HIVE=-1
MAPPER_DANCING_BEAR=-1
MAPPER_POND1=-1
MAPPER_BEAR_CAVE=-1
MAPPER_OUTPOST_BEDOUIN=-1
MAPPER_BEDOUIN_STOCKADE=-1";

        public BuildingLimitLobbyViewModel()
        {
            LimitEntries = CreateLimitEntriesWithCallback(DefaultBuildingLimits);
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public IReadOnlyList<LimitEntryViewModel> LimitEntries { get; }

        public RelayCommand ResetToDefaultCommand { get; }

        public void RefreshLocalizedNames()
        {
            Dictionary<eMappers, BuildingLimitDefinition> definitions = BuildingLimitRuntime.CreateBuildingLimitDefinitions();
            foreach (LimitEntryViewModel entry in LimitEntries)
            {
                if (Enum.TryParse(entry.Key, out eMappers mapper) &&
                    definitions.TryGetValue(mapper, out BuildingLimitDefinition definition))
                {
                    entry.DisplayName = BuildingLimitRuntime.GetLocalizedBuildingName(definition);
                }
            }
        }

        [SyncHostOnly]
        public string BuildingLimits
        {
            get => buildingLimits;
            set
            {
                if (Equals(buildingLimits, value))
                    return;

                buildingLimits = value;
                ApplySerializedLimitsToEntries(value);
                SettingChanged?.Invoke(nameof(BuildingLimits));
                OnPropertyChanged(nameof(BuildingLimits));
            }
        }

        private void ResetToDefault()
        {
            BuildingLimits = DefaultBuildingLimits;
        }

        private static IReadOnlyList<LimitEntryViewModel> CreateLimitEntries(string serializedLimits)
        {
            Dictionary<string, int> values = ParseSerializedLimits(serializedLimits);
            List<LimitEntryViewModel> entries = new List<LimitEntryViewModel>();
            string[] lines = DefaultBuildingLimits.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                string[] parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                string key = parts[0].Trim();
                values.TryGetValue(key, out int value);
                entries.Add(new LimitEntryViewModel(key, FormatDisplayName(key), value));
            }

            return entries;
        }

        private static Dictionary<string, int> ParseSerializedLimits(string text)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
                return result;

            string[] lines = text.Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                string[] parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2 || !int.TryParse(parts[1].Trim(), out int value))
                    continue;

                result[parts[0].Trim()] = ClampLimit(value);
            }

            return result;
        }

        private void ApplySerializedLimitsToEntries(string text)
        {
            if (updatingEntries)
                return;

            Dictionary<string, int> values = ParseSerializedLimits(text);
            updatingEntries = true;
            try
            {
                foreach (LimitEntryViewModel entry in LimitEntries)
                {
                    if (values.TryGetValue(entry.Key, out int value))
                        entry.SetLimitFromOwner(value);
                }
            }
            finally
            {
                updatingEntries = false;
            }
        }

        private void OnEntryChanged()
        {
            if (updatingEntries)
                return;

            buildingLimits = BuildSerializedLimits();
            SettingChanged?.Invoke(nameof(BuildingLimits));
            OnPropertyChanged(nameof(BuildingLimits));
        }

        private string BuildSerializedLimits()
        {
            StringBuilder builder = new StringBuilder("# -1 = unlimited");
            foreach (LimitEntryViewModel entry in LimitEntries)
            {
                builder.AppendLine();
                builder.Append(entry.Key);
                builder.Append('=');
                builder.Append(entry.Limit);
            }

            return builder.ToString();
        }

        private static string FormatDisplayName(string key)
        {
            const string prefix = "MAPPER_";
            string name = key.StartsWith(prefix, StringComparison.Ordinal) ? key.Substring(prefix.Length) : key;
            return name.Replace('_', ' ').ToLowerInvariant();
        }

        private static int ClampLimit(int value)
        {
            if (value < -1)
                return -1;
            if (value > 10000)
                return 10000;
            return value;
        }

        public sealed class LimitEntryViewModel : INotifyPropertyChanged
        {
            private readonly Action changed;
            private string displayName;
            private int limit;

            public event PropertyChangedEventHandler PropertyChanged;

            public LimitEntryViewModel(string key, string displayName, int limit, Action changed = null)
            {
                Key = key;
                DisplayName = displayName;
                this.changed = changed;
                this.limit = ClampLimit(limit);
            }

            public string Key { get; }

            public string DisplayName
            {
                get => displayName;
                set
                {
                    if (displayName == value)
                        return;

                    displayName = value;
                    OnPropertyChanged();
                }
            }

            public int Limit
            {
                get => limit;
                private set
                {
                    int clamped = ClampLimit(value);
                    if (limit == clamped)
                        return;

                    limit = clamped;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LimitText));
                    changed?.Invoke();
                }
            }

            public string LimitText
            {
                get => limit.ToString();
                set
                {
                    if (!int.TryParse(value, out int parsed))
                    {
                        OnPropertyChanged();
                        return;
                    }

                    Limit = parsed;
                }
            }

            public void SetLimitFromOwner(int value)
            {
                Limit = value;
            }

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private IReadOnlyList<LimitEntryViewModel> CreateLimitEntriesWithCallback(string serializedLimits)
        {
            List<LimitEntryViewModel> entries = new List<LimitEntryViewModel>();
            foreach (LimitEntryViewModel entry in CreateLimitEntries(serializedLimits))
                entries.Add(new LimitEntryViewModel(entry.Key, entry.DisplayName, entry.Limit, OnEntryChanged));
            return entries;
        }
    }
}
