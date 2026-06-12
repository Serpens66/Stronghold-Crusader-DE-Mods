using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace UnitLimit
{
    public sealed class UnitLimitLobbyViewModel : LobbyModSettingsBaseViewModel
    {
        public event Action<string> SettingChanged;

        private string unitLimits = DefaultUnitLimits;
        private bool updatingEntries;

        public const string DefaultUnitLimits = @"# -1 = unlimited
CHIMP_TYPE_ARCHER=-1
CHIMP_TYPE_SPEARMAN=-1
CHIMP_TYPE_MACEMAN=-1
CHIMP_TYPE_XBOWMAN=-1
CHIMP_TYPE_PIKEMAN=-1
CHIMP_TYPE_SWORDSMAN=-1
CHIMP_TYPE_KNIGHT=-1
CHIMP_TYPE_ENGINEER=-1
CHIMP_TYPE_MONK=-1
CHIMP_TYPE_LADDERMAN=-1
CHIMP_TYPE_TUNNELER=-1
CHIMP_TYPE_ARAB_BOW=-1
CHIMP_TYPE_ARAB_SLAVE=-1
CHIMP_TYPE_ARAB_SLINGER=-1
CHIMP_TYPE_ARAB_ASSASIN=-1
CHIMP_TYPE_ARAB_HORSEMAN=-1
CHIMP_TYPE_ARAB_SWORDSMAN=-1
CHIMP_TYPE_ARAB_GRENADIER=-1
CHIMP_TYPE_BEDOUIN_CAMEL_LANCER=-1
CHIMP_TYPE_BEDOUIN_HEALER=-1
CHIMP_TYPE_BEDOUIN_EUNUCH=-1
CHIMP_TYPE_BEDOUIN_AMBUSHER=-1
CHIMP_TYPE_BEDOUIN_SKIRMISHER=-1
CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL=-1
CHIMP_TYPE_BEDOUIN_SAPPER=-1
CHIMP_TYPE_BEDOUIN_DEMOLISHER=-1";

        public UnitLimitLobbyViewModel()
        {
            LimitEntries = CreateLimitEntriesWithCallback(DefaultUnitLimits);
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public IReadOnlyList<LimitEntryViewModel> LimitEntries { get; }

        public RelayCommand ResetToDefaultCommand { get; }

        public void RefreshLocalizedNames()
        {
            foreach (LimitEntryViewModel entry in LimitEntries)
            {
                if (Enum.TryParse(entry.Key, out eChimps unitType))
                    entry.DisplayName = UnitLimitRuntime.GetLocalizedUnitName(unitType);
            }
        }

        [SyncHostOnly]
        public string UnitLimits
        {
            get => unitLimits;
            set
            {
                if (Equals(unitLimits, value))
                    return;

                unitLimits = value;
                ApplySerializedLimitsToEntries(value);
                SettingChanged?.Invoke(nameof(UnitLimits));
                OnPropertyChanged(nameof(UnitLimits));
            }
        }

        private void ResetToDefault()
        {
            UnitLimits = DefaultUnitLimits;
        }

        private static IReadOnlyList<LimitEntryViewModel> CreateLimitEntries(string serializedLimits)
        {
            Dictionary<string, int> values = ParseSerializedLimits(serializedLimits);
            string[] keys =
            {
                "CHIMP_TYPE_ARCHER",
                "CHIMP_TYPE_SPEARMAN",
                "CHIMP_TYPE_MACEMAN",
                "CHIMP_TYPE_XBOWMAN",
                "CHIMP_TYPE_PIKEMAN",
                "CHIMP_TYPE_SWORDSMAN",
                "CHIMP_TYPE_KNIGHT",
                "CHIMP_TYPE_ENGINEER",
                "CHIMP_TYPE_MONK",
                "CHIMP_TYPE_LADDERMAN",
                "CHIMP_TYPE_TUNNELER",
                "CHIMP_TYPE_ARAB_BOW",
                "CHIMP_TYPE_ARAB_SLAVE",
                "CHIMP_TYPE_ARAB_SLINGER",
                "CHIMP_TYPE_ARAB_ASSASIN",
                "CHIMP_TYPE_ARAB_HORSEMAN",
                "CHIMP_TYPE_ARAB_SWORDSMAN",
                "CHIMP_TYPE_ARAB_GRENADIER",
                "CHIMP_TYPE_BEDOUIN_CAMEL_LANCER",
                "CHIMP_TYPE_BEDOUIN_HEALER",
                "CHIMP_TYPE_BEDOUIN_EUNUCH",
                "CHIMP_TYPE_BEDOUIN_AMBUSHER",
                "CHIMP_TYPE_BEDOUIN_SKIRMISHER",
                "CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL",
                "CHIMP_TYPE_BEDOUIN_SAPPER",
                "CHIMP_TYPE_BEDOUIN_DEMOLISHER",
            };

            List<LimitEntryViewModel> entries = new List<LimitEntryViewModel>(keys.Length);
            foreach (string key in keys)
            {
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

            unitLimits = BuildSerializedLimits();
            SettingChanged?.Invoke(nameof(UnitLimits));
            OnPropertyChanged(nameof(UnitLimits));
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
            const string prefix = "CHIMP_TYPE_";
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
