using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace UnitCosts
{
    public sealed class UnitCostsLobbyViewModel : LobbyModSettingsBaseViewModel
    {
        public event Action<string> SettingChanged;

        private string unitCosts = CreateDefaultUnitCosts();
        private bool updatingEntries;

        private static readonly string[] DefaultUnitKeys =
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
            "CHIMP_TYPE_BEDOUIN_DEMOLISHER"
        };

        public UnitCostsLobbyViewModel()
        {
            GoodSlotOptions = CreateGoodOptions(includeHorse: false);
            GoodSlot4Options = CreateGoodOptions(includeHorse: true);
            CostEntries = CreateCostEntriesWithCallback(unitCosts);
        }

        public IReadOnlyList<CostEntryViewModel> CostEntries { get; }
        public ObservableCollection<GoodOptionViewModel> GoodSlotOptions { get; }
        public ObservableCollection<GoodOptionViewModel> GoodSlot4Options { get; }

        public string TitleText => IsGermanLanguage() ? "EINHEITENKOSTEN" : "UNIT COSTS";
        public string HelpText => IsGermanLanguage()
            ? "Good-Slots gelten für europäische Einheiten. unchanged lässt den Vanilla-Slot unverändert; Gold -1 bleibt unverändert."
            : "Good slots apply to European units. unchanged keeps the vanilla slot; gold -1 stays unchanged.";
        public string UnitHeaderText => IsGermanLanguage() ? "Einheit" : "Unit";
        public string Slot1HeaderText => IsGermanLanguage() ? "Slot 1" : "Slot 1";
        public string Slot2HeaderText => IsGermanLanguage() ? "Slot 2" : "Slot 2";
        public string Slot3HeaderText => IsGermanLanguage() ? "Slot 3" : "Slot 3";
        public string Slot4HeaderText => IsGermanLanguage() ? "Slot 4" : "Slot 4";
        public string GoldHeaderText => UnitCostsRuntime.GetLocalizedGoodName(eGoods.STORED_GOLD, "Gold");

        [SyncHostOnly]
        public string UnitCosts
        {
            get => unitCosts;
            set
            {
                if (Equals(unitCosts, value))
                    return;

                unitCosts = value;
                ApplySerializedCostsToEntries(value);
                SettingChanged?.Invoke(nameof(UnitCosts));
                OnPropertyChanged(nameof(UnitCosts));
            }
        }

        public void RefreshLocalizedNames()
        {
            RefreshLocalizedHeaderTexts();
            RefreshGoodOptionNames(GoodSlotOptions);
            RefreshGoodOptionNames(GoodSlot4Options);

            foreach (CostEntryViewModel entry in CostEntries)
            {
                if (!Enum.TryParse(entry.Key, out eChimps unitType))
                    continue;

                entry.DisplayName = UnitCostsRuntime.GetLocalizedUnitName(unitType);
                entry.ToolTip = UnitCostsRuntime.GetUnitSettingsTooltip(unitType);
            }
        }

        public Dictionary<eChimps, UnitCostValues> ParseUnitCosts()
        {
            Dictionary<eChimps, UnitCostValues> result = new Dictionary<eChimps, UnitCostValues>();
            Dictionary<string, UnitCostValues> parsed = ParseSerializedCosts(unitCosts);
            foreach (KeyValuePair<string, UnitCostValues> entry in parsed)
            {
                if (Enum.TryParse(entry.Key, true, out eChimps unitType))
                    result[unitType] = entry.Value;
            }

            return result;
        }

        private void RefreshLocalizedHeaderTexts()
        {
            OnPropertyChanged(nameof(TitleText));
            OnPropertyChanged(nameof(HelpText));
            OnPropertyChanged(nameof(UnitHeaderText));
            OnPropertyChanged(nameof(Slot1HeaderText));
            OnPropertyChanged(nameof(Slot2HeaderText));
            OnPropertyChanged(nameof(Slot3HeaderText));
            OnPropertyChanged(nameof(Slot4HeaderText));
            OnPropertyChanged(nameof(GoldHeaderText));
        }

        private static string CreateDefaultUnitCosts()
        {
            StringBuilder builder = new StringBuilder("# unchanged keeps vanilla good slot; order is slot1,slot2,slot3,slot4,gold");
            foreach (string key in DefaultUnitKeys)
            {
                builder.AppendLine();
                builder.Append(key);
                builder.Append("=UNCHANGED,UNCHANGED,UNCHANGED,UNCHANGED,-1");
            }

            return builder.ToString();
        }

        private IReadOnlyList<CostEntryViewModel> CreateCostEntriesWithCallback(string serializedCosts)
        {
            Dictionary<string, UnitCostValues> values = ParseSerializedCosts(serializedCosts);
            List<CostEntryViewModel> entries = new List<CostEntryViewModel>(DefaultUnitKeys.Length);
            foreach (string key in DefaultUnitKeys)
            {
                if (!values.TryGetValue(key, out UnitCostValues value))
                {
                    value = new UnitCostValues(
                        UnitCostValues.UnchangedKey,
                        UnitCostValues.UnchangedKey,
                        UnitCostValues.UnchangedKey,
                        UnitCostValues.UnchangedKey,
                        -1);
                }

                eChimps unitType = Enum.TryParse(key, out eChimps parsedUnitType) ? parsedUnitType : eChimps.CHIMP_TYPE_NULL;
                entries.Add(new CostEntryViewModel(
                    key,
                    FormatDisplayName(key),
                    UnitCostsRuntime.IsEuropeanRecruit(unitType),
                    GoodSlotOptions,
                    GoodSlot4Options,
                    value,
                    OnEntryChanged));
            }

            return entries;
        }

        private static Dictionary<string, UnitCostValues> ParseSerializedCosts(string text)
        {
            Dictionary<string, UnitCostValues> result = new Dictionary<string, UnitCostValues>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
                return result;

            string[] lines = text.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                string[] keyValue = line.Split(new[] { '=' }, 2);
                if (keyValue.Length != 2)
                    continue;

                string[] costParts = keyValue[1].Split(',');
                if (costParts.Length != 5)
                    continue;

                if (!int.TryParse(costParts[4].Trim(), out int gold))
                    continue;

                result[keyValue[0].Trim()] = new UnitCostValues(
                    costParts[0],
                    costParts[1],
                    costParts[2],
                    costParts[3],
                    gold);
            }

            return result;
        }

        private void ApplySerializedCostsToEntries(string text)
        {
            if (updatingEntries)
                return;

            Dictionary<string, UnitCostValues> values = ParseSerializedCosts(text);
            updatingEntries = true;
            try
            {
                foreach (CostEntryViewModel entry in CostEntries)
                {
                    if (values.TryGetValue(entry.Key, out UnitCostValues value))
                        entry.SetCostsFromOwner(value);
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

            unitCosts = BuildSerializedCosts();
            SettingChanged?.Invoke(nameof(UnitCosts));
            OnPropertyChanged(nameof(UnitCosts));
        }

        private string BuildSerializedCosts()
        {
            StringBuilder builder = new StringBuilder("# unchanged keeps vanilla good slot; order is slot1,slot2,slot3,slot4,gold");
            foreach (CostEntryViewModel entry in CostEntries)
            {
                builder.AppendLine();
                builder.Append(entry.Key);
                builder.Append('=');
                builder.Append(entry.SelectedGood1.Key);
                builder.Append(',');
                builder.Append(entry.SelectedGood2.Key);
                builder.Append(',');
                builder.Append(entry.SelectedGood3.Key);
                builder.Append(',');
                builder.Append(entry.SelectedGood4.Key);
                builder.Append(',');
                builder.Append(entry.Gold);
            }

            return builder.ToString();
        }

        private static ObservableCollection<GoodOptionViewModel> CreateGoodOptions(bool includeHorse)
        {
            ObservableCollection<GoodOptionViewModel> options = new ObservableCollection<GoodOptionViewModel>
            {
                new GoodOptionViewModel(UnitCostValues.UnchangedKey, "unchanged"),
                new GoodOptionViewModel(eGoods.STORED_NULL.ToString(), "none")
            };

            AddGoodOptions(options,
                eGoods.STORED_BOWS,
                eGoods.STORED_CROSSBOWS,
                eGoods.STORED_SPEARS,
                eGoods.STORED_PIKES,
                eGoods.STORED_MACES,
                eGoods.STORED_SWORDS,
                eGoods.STORED_LEATHER_ARMOUR,
                eGoods.STORED_METAL_ARMOUR);

            if (includeHorse)
                options.Add(new GoodOptionViewModel(eGoods._SE_REQUIRE_HORSE.ToString(), "Horse"));

            AddGoodOptions(options,
                eGoods.STORED_WOOD_LOGS,
                eGoods.STORED_WOOD_PLANKS,
                eGoods.STORED_RAW_HOPS,
                eGoods.STORED_STONE_BLOCKS,
                eGoods.STORED_COW_HIDES,
                eGoods.STORED_IRON_INGOTS,
                eGoods.STORED_PITCH_RAW,
                eGoods.STORED_PITCH_REFINED,
                eGoods.STORED_RAW_WHEAT,
                eGoods.STORED_GOLD,
                eGoods.STORED_FLOUR);

            AddGoodOptions(options,
                eGoods.STORED_FOOD_BREAD,
                eGoods.STORED_FOOD_CHEESE,
                eGoods.STORED_FOOD_MEAT,
                eGoods.STORED_FOOD_FRUIT,
                eGoods.STORED_FOOD_ALE);

            return options;
        }

        private static void AddGoodOptions(ObservableCollection<GoodOptionViewModel> options, params eGoods[] goods)
        {
            foreach (eGoods good in goods)
                options.Add(new GoodOptionViewModel(good.ToString(), GetGoodOptionDisplayName(good)));
        }

        private static void RefreshGoodOptionNames(IEnumerable<GoodOptionViewModel> options)
        {
            foreach (GoodOptionViewModel option in options)
            {
                if (option.Key == UnitCostValues.UnchangedKey)
                {
                    option.DisplayName = "unchanged";
                    continue;
                }

                if (option.Key == eGoods.STORED_NULL.ToString())
                {
                    option.DisplayName = IsGermanLanguage() ? "keine" : "none";
                    continue;
                }

                if (option.Key == eGoods._SE_REQUIRE_HORSE.ToString())
                {
                    option.DisplayName = IsGermanLanguage() ? "Pferd" : "Horse";
                    continue;
                }

                if (Enum.TryParse(option.Key, out eGoods good))
                    option.DisplayName = GetGoodOptionDisplayName(good);
            }
        }

        private static string GetGoodOptionDisplayName(eGoods good)
        {
            switch (good)
            {
                case eGoods.STORED_BOWS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Bows");
                case eGoods.STORED_CROSSBOWS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Crossbows");
                case eGoods.STORED_SPEARS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Spears");
                case eGoods.STORED_PIKES: return UnitCostsRuntime.GetLocalizedGoodName(good, "Pikes");
                case eGoods.STORED_MACES: return UnitCostsRuntime.GetLocalizedGoodName(good, "Maces");
                case eGoods.STORED_SWORDS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Swords");
                case eGoods.STORED_LEATHER_ARMOUR: return UnitCostsRuntime.GetLocalizedGoodName(good, "Leather Armour");
                case eGoods.STORED_METAL_ARMOUR: return UnitCostsRuntime.GetLocalizedGoodName(good, "Metal Armour");
                case eGoods.STORED_WOOD_LOGS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Wood Logs");
                case eGoods.STORED_WOOD_PLANKS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Wood");
                case eGoods.STORED_RAW_HOPS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Hops");
                case eGoods.STORED_STONE_BLOCKS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Stone");
                case eGoods.STORED_COW_HIDES: return UnitCostsRuntime.GetLocalizedGoodName(good, "Hides");
                case eGoods.STORED_IRON_INGOTS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Iron");
                case eGoods.STORED_PITCH_RAW: return UnitCostsRuntime.GetLocalizedGoodName(good, "Pitch");
                case eGoods.STORED_PITCH_REFINED: return UnitCostsRuntime.GetLocalizedGoodName(good, "Oil");
                case eGoods.STORED_RAW_WHEAT: return UnitCostsRuntime.GetLocalizedGoodName(good, "Wheat");
                case eGoods.STORED_GOLD: return UnitCostsRuntime.GetLocalizedGoodName(good, "Gold");
                case eGoods.STORED_FLOUR: return UnitCostsRuntime.GetLocalizedGoodName(good, "Flour");
                case eGoods.STORED_FOOD_BREAD: return UnitCostsRuntime.GetLocalizedGoodName(good, "Bread");
                case eGoods.STORED_FOOD_CHEESE: return UnitCostsRuntime.GetLocalizedGoodName(good, "Cheese");
                case eGoods.STORED_FOOD_MEAT: return UnitCostsRuntime.GetLocalizedGoodName(good, "Meat");
                case eGoods.STORED_FOOD_FRUIT: return UnitCostsRuntime.GetLocalizedGoodName(good, "Fruit");
                case eGoods.STORED_FOOD_ALE: return UnitCostsRuntime.GetLocalizedGoodName(good, "Ale");
                default: return good.ToString();
            }
        }

        private static string FormatDisplayName(string key)
        {
            const string prefix = "CHIMP_TYPE_";
            string name = key.StartsWith(prefix, StringComparison.Ordinal) ? key.Substring(prefix.Length) : key;
            return name.Replace('_', ' ').ToLowerInvariant();
        }

        private static bool IsGermanLanguage()
        {
            string language = GameAssetManagerAPI.Instance.CurrentLanguage;
            return !string.IsNullOrWhiteSpace(language) &&
                language.Replace('_', '-').StartsWith("de", StringComparison.OrdinalIgnoreCase);
        }

        public sealed class CostEntryViewModel : INotifyPropertyChanged
        {
            private readonly Action changed;
            private string displayName;
            private string toolTip;
            private GoodOptionViewModel selectedGood1;
            private GoodOptionViewModel selectedGood2;
            private GoodOptionViewModel selectedGood3;
            private GoodOptionViewModel selectedGood4;
            private int gold;

            public event PropertyChangedEventHandler PropertyChanged;

            public CostEntryViewModel(
                string key,
                string displayName,
                bool isEuropeanUnit,
                IReadOnlyList<GoodOptionViewModel> goodSlotOptions,
                IReadOnlyList<GoodOptionViewModel> goodSlot4Options,
                UnitCostValues values,
                Action changed = null)
            {
                Key = key;
                this.displayName = displayName;
                toolTip = key;
                IsEuropeanUnit = isEuropeanUnit;
                GoodSlotOptions = goodSlotOptions;
                GoodSlot4Options = goodSlot4Options;
                this.changed = changed;

                selectedGood1 = FindOption(GoodSlotOptions, values.Slot1);
                selectedGood2 = FindOption(GoodSlotOptions, values.Slot2);
                selectedGood3 = FindOption(GoodSlotOptions, values.Slot3);
                selectedGood4 = FindOption(GoodSlot4Options, values.Slot4);
                gold = values.Gold;
            }

            public string Key { get; }
            public bool IsEuropeanUnit { get; }
            public IReadOnlyList<GoodOptionViewModel> GoodSlotOptions { get; }
            public IReadOnlyList<GoodOptionViewModel> GoodSlot4Options { get; }

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

            public string ToolTip
            {
                get => toolTip;
                set
                {
                    if (toolTip == value)
                        return;

                    toolTip = value;
                    OnPropertyChanged();
                }
            }

            public GoodOptionViewModel SelectedGood1
            {
                get => selectedGood1;
                set => SetSelectedGood(ref selectedGood1, value, GoodSlotOptions, 1);
            }

            public GoodOptionViewModel SelectedGood2
            {
                get => selectedGood2;
                set => SetSelectedGood(ref selectedGood2, value, GoodSlotOptions, 2);
            }

            public GoodOptionViewModel SelectedGood3
            {
                get => selectedGood3;
                set => SetSelectedGood(ref selectedGood3, value, GoodSlotOptions, 3);
            }

            public GoodOptionViewModel SelectedGood4
            {
                get => selectedGood4;
                set => SetSelectedGood(ref selectedGood4, value, GoodSlot4Options, 4);
            }

            public int Gold { get => gold; private set => SetCost(ref gold, value, nameof(GoldText)); }
            public string GoldText { get => gold.ToString(); set => SetTextCost(value, v => Gold = v); }

            public void SetCostsFromOwner(UnitCostValues values)
            {
                SelectedGood1 = FindOption(GoodSlotOptions, values.Slot1);
                SelectedGood2 = FindOption(GoodSlotOptions, values.Slot2);
                SelectedGood3 = FindOption(GoodSlotOptions, values.Slot3);
                SelectedGood4 = FindOption(GoodSlot4Options, values.Slot4);
                Gold = values.Gold;
            }

            private void SetSelectedGood(
                ref GoodOptionViewModel field,
                GoodOptionViewModel value,
                IReadOnlyList<GoodOptionViewModel> options,
                int slot)
            {
                GoodOptionViewModel selected = value ?? FindOption(options, UnitCostValues.UnchangedKey);
                if (IsDuplicateSelection(selected.Key, slot))
                    selected = FindOption(options, UnitCostValues.UnchangedKey);

                if (ReferenceEquals(field, selected))
                    return;

                field = selected;
                OnPropertyChanged();
                changed?.Invoke();
            }

            private bool IsDuplicateSelection(string key, int slot)
            {
                if (key == UnitCostValues.UnchangedKey || key == eGoods.STORED_NULL.ToString())
                    return false;

                return (slot != 1 && selectedGood1?.Key == key) ||
                    (slot != 2 && selectedGood2?.Key == key) ||
                    (slot != 3 && selectedGood3?.Key == key) ||
                    (slot != 4 && selectedGood4?.Key == key);
            }

            private static GoodOptionViewModel FindOption(IReadOnlyList<GoodOptionViewModel> options, string key)
            {
                string normalizedKey = UnitCostValues.NormalizeSlotKey(key);
                foreach (GoodOptionViewModel option in options)
                {
                    if (string.Equals(option.Key, normalizedKey, StringComparison.OrdinalIgnoreCase))
                        return option;
                }

                foreach (GoodOptionViewModel option in options)
                {
                    if (option.Key == UnitCostValues.UnchangedKey)
                        return option;
                }

                return options[0];
            }

            private void SetTextCost(string value, Action<int> setCost)
            {
                if (!int.TryParse(value, out int parsed))
                {
                    OnPropertyChanged();
                    return;
                }

                setCost(parsed);
            }

            private void SetCost(ref int field, int value, string textPropertyName)
            {
                int clamped = UnitCostValues.ClampCost(value);
                if (field == clamped)
                    return;

                field = clamped;
                OnPropertyChanged();
                OnPropertyChanged(textPropertyName);
                changed?.Invoke();
            }

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public sealed class GoodOptionViewModel : INotifyPropertyChanged
        {
            private string displayName;

            public event PropertyChangedEventHandler PropertyChanged;

            public GoodOptionViewModel(string key, string displayName)
            {
                Key = UnitCostValues.NormalizeSlotKey(key);
                this.displayName = displayName;
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                }
            }
        }
    }
}
