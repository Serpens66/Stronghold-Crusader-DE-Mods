using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using Noesis;
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

        private bool enableMod = true;
        private string unitCosts = CreateDefaultUnitCosts();
        private string humanExtraUnitCosts = CreateDefaultHumanExtraUnitCosts();
        private bool updatingEntries;

        internal static readonly eGoods[] HumanExtraCostGoods =
        {
            eGoods.STORED_GOLD,
            eGoods.STORED_BOWS,
            eGoods.STORED_CROSSBOWS,
            eGoods.STORED_SPEARS,
            eGoods.STORED_PIKES,
            eGoods.STORED_MACES,
            eGoods.STORED_SWORDS,
            eGoods.STORED_LEATHER_ARMOUR,
            eGoods.STORED_METAL_ARMOUR,
            eGoods.STORED_WOOD_PLANKS,
            eGoods.STORED_RAW_HOPS,
            eGoods.STORED_STONE_BLOCKS,
            eGoods.STORED_IRON_INGOTS,
            eGoods.STORED_PITCH_RAW,
            eGoods.STORED_RAW_WHEAT,
            eGoods.STORED_FLOUR,
            eGoods.STORED_FOOD_BREAD,
            eGoods.STORED_FOOD_CHEESE,
            eGoods.STORED_FOOD_MEAT,
            eGoods.STORED_FOOD_FRUIT,
            eGoods.STORED_FOOD_ALE
        };

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
            "CHIMP_TYPE_CATAPULT",
            "CHIMP_TYPE_TREBUCHET",
            "CHIMP_TYPE_BATTERING_RAM",
            "CHIMP_TYPE_SIEGE_TOWER",
            "CHIMP_TYPE_PORTABLE_SHIELD",
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
            "CHIMP_TYPE_ARAB_BALLISTA",
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
            ExtraCostHeaders = CreateExtraCostHeaders();
            ExtraCostEntries = CreateExtraCostEntriesWithCallback(humanExtraUnitCosts);
            RefreshCostEntryToolTips();
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public IReadOnlyList<CostEntryViewModel> CostEntries { get; }
        public IReadOnlyList<ExtraCostEntryViewModel> ExtraCostEntries { get; }
        public IReadOnlyList<ExtraCostHeaderViewModel> ExtraCostHeaders { get; }
        public ObservableCollection<GoodOptionViewModel> GoodSlotOptions { get; }
        public ObservableCollection<GoodOptionViewModel> GoodSlot4Options { get; }
        public RelayCommand ResetToDefaultCommand { get; }
        public string EnableModText => SerpLocalization.Get(SerpLocalization.EnableMod);
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);

        public string TitleText => SerpLocalization.Get(SerpLocalization.UnitCostsTitle);
        public string HelpText => SerpLocalization.Get(SerpLocalization.UnitCostsHelp);
        public string ExtraTitleText => SerpLocalization.Get(SerpLocalization.UnitCostsExtraTitle);
        public string ExtraHelpText => SerpLocalization.Get(SerpLocalization.UnitCostsExtraHelp);
        public string UnitHeaderText => SerpLocalization.Get(SerpLocalization.UnitHeader);
        public string Slot1HeaderText => SerpLocalization.Get(SerpLocalization.Slot1Header);
        public string Slot2HeaderText => SerpLocalization.Get(SerpLocalization.Slot2Header);
        public string Slot3HeaderText => SerpLocalization.Get(SerpLocalization.Slot3Header);
        public string Slot4HeaderText => SerpLocalization.Get(SerpLocalization.Slot4HorseHeader);
        public string GoldHeaderText => UnitCostsRuntime.GetLocalizedGoodName(eGoods.STORED_GOLD, "Gold");
        public ImageSource GoldHeaderIcon => GetGoodIconImage(eGoods.STORED_GOLD);

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set => Set(ref enableMod, value, nameof(EnableMod));
        }

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
                NormalizeExtraCostsAfterNativeGoldChange();
            }
        }

        [SyncHostOnly]
        public string HumanExtraUnitCosts
        {
            get => humanExtraUnitCosts;
            set
            {
                if (Equals(humanExtraUnitCosts, value))
                    return;

                humanExtraUnitCosts = value;
                ApplySerializedExtraCostsToEntries(value);
                SettingChanged?.Invoke(nameof(HumanExtraUnitCosts));
                OnPropertyChanged(nameof(HumanExtraUnitCosts));
            }
        }

        private void ResetToDefault()
        {
            UnitCosts = CreateDefaultUnitCosts();
            HumanExtraUnitCosts = CreateDefaultHumanExtraUnitCosts();
        }

        private void Set<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        public void RefreshLocalizedNames()
        {
            RefreshLocalizedHeaderTexts();
            RefreshGoodOptionNames(GoodSlotOptions);
            RefreshGoodOptionNames(GoodSlot4Options);
            foreach (ExtraCostHeaderViewModel header in ExtraCostHeaders)
                header.DisplayName = GetGoodOptionDisplayName(header.Good);

            foreach (CostEntryViewModel entry in CostEntries)
            {
                if (!Enum.TryParse(entry.Key, out eChimps unitType))
                    continue;

                entry.DisplayName = UnitCostsRuntime.GetLocalizedUnitName(unitType);
                entry.ToolTip = UnitCostsRuntime.GetUnitSettingsTooltip(unitType);
            }

            foreach (ExtraCostEntryViewModel entry in ExtraCostEntries)
            {
                if (!Enum.TryParse(entry.Key, out eChimps unitType))
                    continue;

                entry.DisplayName = UnitCostsRuntime.GetLocalizedUnitName(unitType);
                entry.ToolTip = UnitCostsRuntime.GetUnitSettingsTooltip(unitType);
            }

            RefreshCostEntryToolTips();
            RefreshExtraCostCellToolTips();
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

        public Dictionary<eChimps, UnitExtraCostValues> ParseHumanExtraUnitCosts()
        {
            Dictionary<eChimps, UnitExtraCostValues> result = new Dictionary<eChimps, UnitExtraCostValues>();
            Dictionary<string, UnitExtraCostValues> parsed = ParseSerializedExtraCosts(humanExtraUnitCosts);
            foreach (KeyValuePair<string, UnitExtraCostValues> entry in parsed)
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
            OnPropertyChanged(nameof(ExtraTitleText));
            OnPropertyChanged(nameof(ExtraHelpText));
            OnPropertyChanged(nameof(UnitHeaderText));
            OnPropertyChanged(nameof(Slot1HeaderText));
            OnPropertyChanged(nameof(Slot2HeaderText));
            OnPropertyChanged(nameof(Slot3HeaderText));
            OnPropertyChanged(nameof(Slot4HeaderText));
            OnPropertyChanged(nameof(GoldHeaderText));
            OnPropertyChanged(nameof(GoldHeaderIcon));
            RefreshCostEntryToolTips();
        }

        private void RefreshCostEntryToolTips()
        {
            foreach (CostEntryViewModel entry in CostEntries)
            {
                entry.GoldToolTip = FormatCellToolTip(entry.DisplayName, GoldHeaderText);
                entry.Slot1ToolTip = FormatCellToolTip(entry.DisplayName, Slot1HeaderText);
                entry.Slot2ToolTip = FormatCellToolTip(entry.DisplayName, Slot2HeaderText);
                entry.Slot3ToolTip = FormatCellToolTip(entry.DisplayName, Slot3HeaderText);
                entry.Slot4ToolTip = FormatCellToolTip(entry.DisplayName, Slot4HeaderText);
            }
        }

        private void RefreshExtraCostCellToolTips()
        {
            foreach (ExtraCostEntryViewModel entry in ExtraCostEntries)
            {
                foreach (ExtraCostCellViewModel cell in entry.CostCells)
                    cell.ToolTip = FormatCellToolTip(entry.DisplayName, GetGoodOptionDisplayName(cell.Good));
            }
        }

        private static string FormatCellToolTip(string rowName, string columnName)
        {
            if (string.IsNullOrWhiteSpace(rowName))
                return columnName ?? "";

            if (string.IsNullOrWhiteSpace(columnName))
                return rowName;

            return rowName + " / " + columnName;
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

        private static string CreateDefaultHumanExtraUnitCosts()
        {
            StringBuilder builder = new StringBuilder("# 0 = no extra cost; order is ");
            AppendExtraCostHeaderComment(builder);
            foreach (string key in DefaultUnitKeys)
            {
                builder.AppendLine();
                builder.Append(key);
                builder.Append('=');
                for (int i = 0; i < HumanExtraCostGoods.Length; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    builder.Append('0');
                }
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

        private IReadOnlyList<ExtraCostHeaderViewModel> CreateExtraCostHeaders()
        {
            List<ExtraCostHeaderViewModel> headers = new List<ExtraCostHeaderViewModel>(HumanExtraCostGoods.Length);
            foreach (eGoods good in HumanExtraCostGoods)
                headers.Add(new ExtraCostHeaderViewModel(good, GetGoodOptionDisplayName(good), null));

            return headers;
        }

        private IReadOnlyList<ExtraCostEntryViewModel> CreateExtraCostEntriesWithCallback(string serializedCosts)
        {
            Dictionary<string, UnitExtraCostValues> values = ParseSerializedExtraCosts(serializedCosts);
            List<ExtraCostEntryViewModel> entries = new List<ExtraCostEntryViewModel>(DefaultUnitKeys.Length);
            foreach (string key in DefaultUnitKeys)
            {
                if (!values.TryGetValue(key, out UnitExtraCostValues value))
                    value = CreateEmptyExtraCosts();

                eChimps unitType = Enum.TryParse(key, out eChimps parsedUnitType) ? parsedUnitType : eChimps.CHIMP_TYPE_NULL;
                entries.Add(new ExtraCostEntryViewModel(key, FormatDisplayName(key), unitType, null, value, OnExtraEntryChanged));
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

        private static Dictionary<string, UnitExtraCostValues> ParseSerializedExtraCosts(string text)
        {
            Dictionary<string, UnitExtraCostValues> result = new Dictionary<string, UnitExtraCostValues>(StringComparer.OrdinalIgnoreCase);
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
                if (costParts.Length != HumanExtraCostGoods.Length)
                    continue;

                Dictionary<eGoods, int> costs = new Dictionary<eGoods, int>();
                bool valid = true;
                for (int i = 0; i < HumanExtraCostGoods.Length; i++)
                {
                    if (!int.TryParse(costParts[i].Trim(), out int amount))
                    {
                        valid = false;
                        break;
                    }

                    eGoods good = HumanExtraCostGoods[i];
                    int currentGoldCost = 0;
                    if (good == eGoods.STORED_GOLD &&
                        Enum.TryParse(keyValue[0].Trim(), true, out eChimps unitType))
                    {
                        currentGoldCost = UnitCostsRuntime.GetCurrentUnitGoldCost(unitType);
                    }

                    costs[good] = UnitExtraCostValues.ClampCost(good, amount, currentGoldCost);
                }

                if (valid)
                    result[keyValue[0].Trim()] = new UnitExtraCostValues(costs);
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

        private void ApplySerializedExtraCostsToEntries(string text)
        {
            if (updatingEntries)
                return;

            Dictionary<string, UnitExtraCostValues> values = ParseSerializedExtraCosts(text);
            updatingEntries = true;
            try
            {
                foreach (ExtraCostEntryViewModel entry in ExtraCostEntries)
                {
                    if (values.TryGetValue(entry.Key, out UnitExtraCostValues value))
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
            NormalizeExtraCostsAfterNativeGoldChange();
        }

        private void OnExtraEntryChanged()
        {
            if (updatingEntries)
                return;

            humanExtraUnitCosts = BuildSerializedExtraCosts();
            SettingChanged?.Invoke(nameof(HumanExtraUnitCosts));
            OnPropertyChanged(nameof(HumanExtraUnitCosts));
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

        internal void NormalizeExtraCostsAfterNativeGoldChange()
        {
            ApplySerializedExtraCostsToEntries(humanExtraUnitCosts);
            string normalized = BuildSerializedExtraCosts();
            if (humanExtraUnitCosts == normalized)
                return;

            humanExtraUnitCosts = normalized;
            SettingChanged?.Invoke(nameof(HumanExtraUnitCosts));
            OnPropertyChanged(nameof(HumanExtraUnitCosts));
        }

        private string BuildSerializedExtraCosts()
        {
            StringBuilder builder = new StringBuilder("# 0 = no extra cost; order is ");
            AppendExtraCostHeaderComment(builder);
            foreach (ExtraCostEntryViewModel entry in ExtraCostEntries)
            {
                builder.AppendLine();
                builder.Append(entry.Key);
                builder.Append('=');
                for (int i = 0; i < entry.CostCells.Count; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    builder.Append(entry.CostCells[i].Amount);
                }
            }

            return builder.ToString();
        }

        private static void AppendExtraCostHeaderComment(StringBuilder builder)
        {
            for (int i = 0; i < HumanExtraCostGoods.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(HumanExtraCostGoods[i]);
            }
        }

        private static UnitExtraCostValues CreateEmptyExtraCosts()
        {
            Dictionary<eGoods, int> costs = new Dictionary<eGoods, int>();
            foreach (eGoods good in HumanExtraCostGoods)
                costs[good] = 0;

            return new UnitExtraCostValues(costs);
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
                eGoods.STORED_WOOD_PLANKS,
                eGoods.STORED_RAW_HOPS,
                eGoods.STORED_STONE_BLOCKS,
                eGoods.STORED_IRON_INGOTS,
                eGoods.STORED_PITCH_RAW,
                eGoods.STORED_RAW_WHEAT,
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
                    option.DisplayName = SerpLocalization.Get(SerpLocalization.None);
                    continue;
                }

                if (option.Key == eGoods._SE_REQUIRE_HORSE.ToString())
                {
                    option.DisplayName = SerpLocalization.Get(SerpLocalization.Horse);
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
                case eGoods.STORED_WOOD_PLANKS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Wood");
                case eGoods.STORED_RAW_HOPS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Hops");
                case eGoods.STORED_STONE_BLOCKS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Stone");
                case eGoods.STORED_IRON_INGOTS: return UnitCostsRuntime.GetLocalizedGoodName(good, "Iron");
                case eGoods.STORED_PITCH_RAW: return UnitCostsRuntime.GetLocalizedGoodName(good, "Pitch");
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

        private static ImageSource GetGoodIconImage(eGoods good)
        {
            try
            {
                return CrusaderDE.MainViewModel.Instance?.getSmallGoodsIcon((int)good);
            }
            catch
            {
                return null;
            }
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

        private static string FormatDisplayName(string key)
        {
            const string prefix = "CHIMP_TYPE_";
            string name = key.StartsWith(prefix, StringComparison.Ordinal) ? key.Substring(prefix.Length) : key;
            return name.Replace('_', ' ').ToLowerInvariant();
        }

        public sealed class CostEntryViewModel : INotifyPropertyChanged
        {
            private readonly Action changed;
            private string displayName;
            private string toolTip;
            private string goldToolTip;
            private string slot1ToolTip;
            private string slot2ToolTip;
            private string slot3ToolTip;
            private string slot4ToolTip;
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
            public ImageSource IconImage
            {
                get
                {
                    return Enum.TryParse(Key, out eChimps unitType)
                        ? GetUnitIconImage(unitType)
                        : null;
                }
            }
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

            public string GoldToolTip
            {
                get => goldToolTip;
                set => SetToolTip(ref goldToolTip, value);
            }

            public string Slot1ToolTip
            {
                get => slot1ToolTip;
                set => SetToolTip(ref slot1ToolTip, value);
            }

            public string Slot2ToolTip
            {
                get => slot2ToolTip;
                set => SetToolTip(ref slot2ToolTip, value);
            }

            public string Slot3ToolTip
            {
                get => slot3ToolTip;
                set => SetToolTip(ref slot3ToolTip, value);
            }

            public string Slot4ToolTip
            {
                get => slot4ToolTip;
                set => SetToolTip(ref slot4ToolTip, value);
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

            private void SetToolTip(ref string field, string value, [CallerMemberName] string propertyName = null)
            {
                if (field == value)
                    return;

                field = value;
                OnPropertyChanged(propertyName);
            }

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public sealed class ExtraCostHeaderViewModel : INotifyPropertyChanged
        {
            private string displayName;

            public event PropertyChangedEventHandler PropertyChanged;

            public ExtraCostHeaderViewModel(eGoods good, string displayName, ImageSource iconImage)
            {
                Good = good;
                this.displayName = displayName;
            }

            public eGoods Good { get; }
            public ImageSource IconImage => GetGoodIconImage(Good);

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

        public sealed class ExtraCostEntryViewModel : INotifyPropertyChanged
        {
            private readonly Action changed;
            private readonly eChimps unitType;
            private string displayName;
            private string toolTip;

            public event PropertyChangedEventHandler PropertyChanged;

            public ExtraCostEntryViewModel(string key, string displayName, eChimps unitType, ImageSource iconImage, UnitExtraCostValues values, Action changed = null)
            {
                Key = key;
                this.unitType = unitType;
                this.displayName = displayName;
                toolTip = key;
                this.changed = changed;
                CostCells = new ObservableCollection<ExtraCostCellViewModel>();
                foreach (eGoods good in HumanExtraCostGoods)
                    CostCells.Add(new ExtraCostCellViewModel(good, values.GetCost(good), () => GetMinCost(good), OnCellChanged));
            }

            public string Key { get; }
            public ImageSource IconImage => GetUnitIconImage(unitType);
            public ObservableCollection<ExtraCostCellViewModel> CostCells { get; }

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

            public void SetCostsFromOwner(UnitExtraCostValues values)
            {
                foreach (ExtraCostCellViewModel cell in CostCells)
                    cell.SetAmountFromOwner(values.GetCost(cell.Good));
            }

            private void OnCellChanged()
            {
                changed?.Invoke();
            }

            private int GetMinCost(eGoods good)
            {
                if (good != eGoods.STORED_GOLD)
                    return 0;

                return -UnitCostsRuntime.GetCurrentUnitGoldCost(unitType);
            }

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public sealed class ExtraCostCellViewModel : INotifyPropertyChanged
        {
            private readonly Action changed;
            private readonly Func<int> getMinAmount;
            private string toolTip;
            private int amount;

            public event PropertyChangedEventHandler PropertyChanged;

            public ExtraCostCellViewModel(eGoods good, int amount, Func<int> getMinAmount, Action changed = null)
            {
                Good = good;
                this.getMinAmount = getMinAmount;
                this.amount = ClampAmount(amount);
                toolTip = GetGoodOptionDisplayName(good);
                this.changed = changed;
            }

            public eGoods Good { get; }
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

            public int Amount { get => amount; private set => SetAmount(value, true); }
            public string AmountText { get => amount.ToString(); set => SetTextAmount(value); }

            public void SetAmountFromOwner(int value)
            {
                SetAmount(value, false);
            }

            private void SetTextAmount(string value)
            {
                if (!int.TryParse(value, out int parsed))
                {
                    OnPropertyChanged(nameof(AmountText));
                    return;
                }

                Amount = parsed;
            }

            private void SetAmount(int value, bool notifyOwner)
            {
                int clamped = ClampAmount(value);
                if (amount == clamped)
                    return;

                amount = clamped;
                OnPropertyChanged(nameof(Amount));
                OnPropertyChanged(nameof(AmountText));
                if (notifyOwner)
                    changed?.Invoke();
            }

            private int ClampAmount(int value)
            {
                if (Good == eGoods.STORED_GOLD)
                {
                    int minAmount = getMinAmount != null ? getMinAmount() : 0;
                    if (value < minAmount)
                        return minAmount;
                }
                else if (value < 0)
                {
                    return 0;
                }

                if (value > 1000)
                    return 1000;

                return value;
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

            public ImageSource IconImage
            {
                get
                {
                    if (Enum.TryParse(Key, out eGoods good) &&
                        good != eGoods.STORED_NULL &&
                        good != eGoods._SE_REQUIRE_HORSE)
                    {
                        return GetGoodIconImage(good);
                    }

                    return null;
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                }
            }
        }
    }
}
