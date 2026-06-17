using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using Noesis;
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

        private bool enableMod = true;
        private string unitLimits = DefaultUnitLimits;
        private int campfirePeasantsLimit = -1;
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
CHIMP_TYPE_CATAPULT=-1
CHIMP_TYPE_TREBUCHET=-1
CHIMP_TYPE_BATTERING_RAM=-1
CHIMP_TYPE_SIEGE_TOWER=-1
CHIMP_TYPE_PORTABLE_SHIELD=-1
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
CHIMP_TYPE_ARAB_BALLISTA=-1
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
        public string EnableModText => SerpLocalization.Get(SerpLocalization.EnableMod);
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public string TitleText => SerpLocalization.Get(SerpLocalization.UnitLimitsTitle);
        public string HelpText => SerpLocalization.Get(SerpLocalization.UnitLimitsHelp);
        public string CampfirePeasantsText => SerpLocalization.Get(SerpLocalization.UnitLimitsCampfirePeasants);
        public string CampfirePeasantsHelpText => SerpLocalization.Get(SerpLocalization.UnitLimitsCampfirePeasantsHelp);

        public void RefreshLocalizedNames()
        {
            foreach (LimitEntryViewModel entry in LimitEntries)
            {
                if (Enum.TryParse(entry.Key, out eChimps unitType))
                    entry.DisplayName = UnitLimitRuntime.GetLocalizedUnitName(unitType);
            }
        }

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set => Set(ref enableMod, value, nameof(EnableMod));
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

        [SyncHostOnly]
        public int CampfirePeasantsLimit
        {
            get => campfirePeasantsLimit;
            set
            {
                int clamped = ClampCampfirePeasantsLimit(value);
                if (campfirePeasantsLimit == clamped)
                    return;

                campfirePeasantsLimit = clamped;
                SettingChanged?.Invoke(nameof(CampfirePeasantsLimit));
                OnPropertyChanged(nameof(CampfirePeasantsLimit));
                OnPropertyChanged(nameof(CampfirePeasantsLimitText));
            }
        }

        public string CampfirePeasantsLimitText
        {
            get => CampfirePeasantsLimit.ToString();
            set
            {
                if (!int.TryParse(value, out int parsed))
                {
                    OnPropertyChanged(nameof(CampfirePeasantsLimitText));
                    return;
                }

                CampfirePeasantsLimit = parsed;
                OnPropertyChanged(nameof(CampfirePeasantsLimitText));
            }
        }

        private void ResetToDefault()
        {
            UnitLimits = DefaultUnitLimits;
            CampfirePeasantsLimit = -1;
            OnPropertyChanged(nameof(CampfirePeasantsLimitText));
        }

        private static IReadOnlyList<LimitEntryViewModel> CreateLimitEntries(string serializedLimits)
        {
            Dictionary<string, int> values = ParseSerializedLimits(serializedLimits);
            List<LimitEntryViewModel> entries = new List<LimitEntryViewModel>();
            string[] lines = DefaultUnitLimits.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
                entries.Add(new LimitEntryViewModel(key, FormatDisplayName(key), null, value));
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

        private static int ClampCampfirePeasantsLimit(int value)
        {
            if (value < -1)
                return -1;
            if (value > 500)
                return 500;
            return value;
        }

        private void Set<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private static ImageSource GetUnitIconImage(eChimps unitType)
        {
            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_ARCHER: return GetResourceImage("UI-Buildings O001");
                case eChimps.CHIMP_TYPE_SPEARMAN: return GetResourceImage("UI-Buildings O003");
                case eChimps.CHIMP_TYPE_MACEMAN: return GetResourceImage("UI-Buildings O007");
                case eChimps.CHIMP_TYPE_XBOWMAN: return GetResourceImage("UI-Buildings O009");
                case eChimps.CHIMP_TYPE_PIKEMAN: return GetResourceImage("UI-Buildings O005");
                case eChimps.CHIMP_TYPE_SWORDSMAN: return GetResourceImage("UI-Buildings O011");
                case eChimps.CHIMP_TYPE_KNIGHT: return GetResourceImage("UI-Buildings O013");
                case eChimps.CHIMP_TYPE_ENGINEER: return GetResourceImage("UI-Buildings O017");
                case eChimps.CHIMP_TYPE_CATAPULT: return GetResourceImage("UI-Buildings O023");
                case eChimps.CHIMP_TYPE_TREBUCHET: return GetResourceImage("UI-Buildings O025");
                case eChimps.CHIMP_TYPE_BATTERING_RAM: return GetResourceImage("UI-Buildings O027");
                case eChimps.CHIMP_TYPE_SIEGE_TOWER: return GetResourceImage("UI-Buildings O029");
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD: return GetResourceImage("UI-Buildings O031");
                case eChimps.CHIMP_TYPE_MONK: return GetResourceImage("UI-Buildings O021");
                case eChimps.CHIMP_TYPE_LADDERMAN: return GetResourceImage("UI-Buildings O015");
                case eChimps.CHIMP_TYPE_TUNNELER: return GetResourceImage("UI-Buildings O033");
                case eChimps.CHIMP_TYPE_ARAB_BOW: return GetResourceImage("UI-Buildings O035");
                case eChimps.CHIMP_TYPE_ARAB_SLAVE: return GetResourceImage("UI-Buildings O037");
                case eChimps.CHIMP_TYPE_ARAB_SLINGER: return GetResourceImage("UI-Buildings O039");
                case eChimps.CHIMP_TYPE_ARAB_ASSASIN: return GetResourceImage("UI-Buildings O041");
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN: return GetResourceImage("UI-Buildings O043");
                case eChimps.CHIMP_TYPE_ARAB_SWORDSMAN: return GetResourceImage("UI-Buildings O045");
                case eChimps.CHIMP_TYPE_ARAB_GRENADIER: return GetResourceImage("UI-Buildings O047");
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA: return GetResourceImage("UI-Buildings O049");
                case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER: return GetResourceImage("UI-Buildings O051");
                case eChimps.CHIMP_TYPE_BEDOUIN_HEALER: return GetResourceImage("UI-Buildings O053");
                case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH: return GetResourceImage("UI-Buildings O055");
                case eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER: return GetResourceImage("UI-Buildings O057");
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER: return GetResourceImage("UI-Buildings O059");
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL: return GetResourceImage("UI-Buildings O061");
                case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER: return GetResourceImage("UI-Buildings O063");
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER: return GetResourceImage("UI-Buildings O065");
                default: return null;
            }
        }

        private static ImageSource GetResourceImage(string key)
        {
            try
            {
                return GUI.GetApplicationResources()?[key] as ImageSource;
            }
            catch
            {
                return null;
            }
        }

        public sealed class LimitEntryViewModel : INotifyPropertyChanged
        {
            private readonly Action changed;
            private string displayName;
            private int limit;

            public event PropertyChangedEventHandler PropertyChanged;

            public LimitEntryViewModel(string key, string displayName, ImageSource iconImage, int limit, Action changed = null)
            {
                Key = key;
                DisplayName = displayName;
                this.changed = changed;
                this.limit = ClampLimit(limit);
            }

            public string Key { get; }
            public ImageSource IconImage
            {
                get
                {
                    return Enum.TryParse(Key, out eChimps unitType)
                        ? GetUnitIconImage(unitType)
                        : null;
                }
            }

            public string DisplayName
            {
                get => displayName;
                set
                {
                    if (displayName == value)
                        return;

                    displayName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LimitToolTip));
                }
            }

            public string LimitToolTip => string.IsNullOrWhiteSpace(DisplayName) ? SerpLocalization.Get(SerpLocalization.Limit) : DisplayName + " / " + SerpLocalization.Get(SerpLocalization.Limit);

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
                entries.Add(new LimitEntryViewModel(entry.Key, entry.DisplayName, entry.IconImage, entry.Limit, OnEntryChanged));
            return entries;
        }
    }
}
