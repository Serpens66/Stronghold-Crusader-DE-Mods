using APIShared.ModSettings;
using Noesis;
using SHCDESE.Interop;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace BuildingCosts
{
    public sealed partial class BuildingCostsLobbyViewModel
    {
        // Null means this property was absent in an older preset/save. An explicitly empty
        // new property must win over the old, previously unsafe Brazier cost row.
        private string humanExtraBuildingCosts;
        private bool updatingHumanEntries;
        internal static readonly eGoods[] HumanCostGoods = {
            eGoods.STORED_GOLD, eGoods.STORED_BOWS, eGoods.STORED_CROSSBOWS,
            eGoods.STORED_SPEARS, eGoods.STORED_PIKES, eGoods.STORED_MACES,
            eGoods.STORED_SWORDS, eGoods.STORED_LEATHER_ARMOUR, eGoods.STORED_METAL_ARMOUR,
            eGoods.STORED_WOOD_PLANKS, eGoods.STORED_RAW_HOPS, eGoods.STORED_STONE_BLOCKS,
            eGoods.STORED_IRON_INGOTS, eGoods.STORED_PITCH_RAW, eGoods.STORED_RAW_WHEAT,
            eGoods.STORED_FLOUR, eGoods.STORED_FOOD_BREAD, eGoods.STORED_FOOD_CHEESE,
            eGoods.STORED_FOOD_MEAT, eGoods.STORED_FOOD_FRUIT, eGoods.STORED_FOOD_ALE
        };

        public string BaseCostsTitleText => SerpLocalization.Get("BuildingCosts.BaseTitle");
        public string HumanCostsTitleText => SerpLocalization.Get("BuildingCosts.HumanTitle");
        public string HumanCostsHelpText => SerpLocalization.Get("BuildingCosts.HumanHelp");
        public IReadOnlyList<HumanCostEntryViewModel> HumanCostEntries { get; private set; }

        [SyncHostOnly]
        public string HumanExtraBuildingCosts
        {
            get => humanExtraBuildingCosts ?? SerializeHumanCosts(GetLegacyHumanCosts());
            set
            {
                if (!CanMutateSetting(nameof(HumanExtraBuildingCosts))) return;
                string normalized = SerializeHumanCosts(ParseHumanCosts(value));
                if (humanExtraBuildingCosts == normalized) return;
                humanExtraBuildingCosts = normalized;
                RefreshHumanCostEntries();
                SettingChanged?.Invoke(nameof(HumanExtraBuildingCosts));
                OnPropertyChanged(nameof(HumanExtraBuildingCosts));
            }
        }

        public Dictionary<eMappers, IReadOnlyDictionary<eGoods, int>> ParseHumanExtraBuildingCosts()
        {
            var parsed = ParseHumanCosts(HumanExtraBuildingCosts);
            var result = new Dictionary<eMappers, IReadOnlyDictionary<eGoods, int>>();
            foreach (var row in parsed)
                if (Enum.TryParse(row.Key, true, out eMappers mapper) && Array.Exists(DefaultCostKeys, k => string.Equals(k, row.Key, StringComparison.OrdinalIgnoreCase)))
                    result[mapper] = row.Value;
            return result;
        }

        private void InitializeHumanCostEntries()
        {
            var rows = new List<HumanCostEntryViewModel>();
            foreach (string key in DefaultCostKeys)
            {
                var cells = new List<HumanCostCellViewModel>();
                foreach (var good in HumanCostGoods)
                    cells.Add(new HumanCostCellViewModel(key, good,
                        () => CanMutateSetting(nameof(HumanExtraBuildingCosts)), OnHumanEntryChanged));
                rows.Add(new HumanCostEntryViewModel(key, cells));
            }
            HumanCostEntries = rows;
            RefreshHumanCostEntries();
            RefreshHumanCostNames();
        }

        private Dictionary<string, Dictionary<eGoods, int>> GetLegacyHumanCosts()
        {
            var result = new Dictionary<string, Dictionary<eGoods, int>>(StringComparer.OrdinalIgnoreCase);
            if (ParseSerializedCosts(buildingCosts).TryGetValue("MAPPER_BRAZIER", out BuildingCostValues old))
            {
                result["MAPPER_BRAZIER"] = new Dictionary<eGoods, int> {
                    { eGoods.STORED_WOOD_PLANKS, Math.Max(0, old.Wood) },
                    { eGoods.STORED_STONE_BLOCKS, Math.Max(0, old.Stone) },
                    { eGoods.STORED_IRON_INGOTS, Math.Max(0, old.Iron) },
                    { eGoods.STORED_PITCH_RAW, Math.Max(0, old.Pitch) },
                    { eGoods.STORED_GOLD, Math.Max(0, old.Gold) }
                };
            }
            return result;
        }

        private static Dictionary<string, Dictionary<eGoods, int>> ParseHumanCosts(string text)
        {
            var result = new Dictionary<string, Dictionary<eGoods, int>>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in (text ?? "").Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = line.Trim().Split(new[] { '=' }, 2);
                if (pair.Length != 2 || !Array.Exists(DefaultCostKeys, k => string.Equals(k, pair[0], StringComparison.OrdinalIgnoreCase))) continue;
                var goods = new Dictionary<eGoods, int>();
                foreach (string cell in pair[1].Split(','))
                {
                    string[] field = cell.Split(':');
                    if (field.Length == 2 && Enum.TryParse(field[0].Trim(), out eGoods good) &&
                        Array.IndexOf(HumanCostGoods, good) >= 0 &&
                        int.TryParse(field[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
                        goods[good] = ClampHumanCost(good, amount);
                }
                result[pair[0]] = goods;
            }
            return result;
        }

        private static string CreateDefaultHumanExtraBuildingCosts() => SerializeHumanCosts(null);

        private static string SerializeHumanCosts(Dictionary<string, Dictionary<eGoods, int>> rows)
        {
            var text = new StringBuilder("# 0 = no extra cost; goods use enum names");
            foreach (string key in DefaultCostKeys)
            {
                text.AppendLine(); text.Append(key).Append('=');
                Dictionary<eGoods, int> row = null;
                if (rows != null) rows.TryGetValue(key, out row);
                bool separator = false;
                foreach (var good in HumanCostGoods)
                {
                    int value = 0;
                    if (row != null) row.TryGetValue(good, out value);
                    if (value == 0) continue;
                    if (separator) text.Append(',');
                    text.Append(good).Append(':').Append(ClampHumanCost(good, value).ToString(CultureInfo.InvariantCulture));
                    separator = true;
                }
            }
            return text.ToString();
        }

        private void RefreshHumanCostEntries()
        {
            if (HumanCostEntries == null) return;
            var values = ParseHumanCosts(HumanExtraBuildingCosts);
            updatingHumanEntries = true;
            try
            {
                foreach (var row in HumanCostEntries)
                {
                    values.TryGetValue(row.Key, out var goods);
                    foreach (var cell in row.CostCells)
                    {
                        int amount = 0;
                        if (goods != null) goods.TryGetValue(cell.Good, out amount);
                        cell.SetFromOwner(amount);
                    }
                }
            }
            finally { updatingHumanEntries = false; }
        }

        private void RefreshHumanCostNames()
        {
            if (HumanCostEntries == null) return;
            var definitions = BuildingCostsRuntime.CreateBuildingCostDefinitions();
            foreach (var row in HumanCostEntries)
            {
                string name = FormatDisplayName(row.Key);
                if (Enum.TryParse(row.Key, out eMappers mapper) && definitions.TryGetValue(mapper, out var definition))
                    name = BuildingCostsRuntime.GetLocalizedBuildingName(definition);
                row.DisplayName = name + (row.Key == "MAPPER_BRAZIER" ? " (" + SerpLocalization.Get(SerpLocalization.Human) + ")" : "");
                foreach (var cell in row.CostCells) cell.RefreshName(row.DisplayName);
            }
            OnPropertyChanged(nameof(BaseCostsTitleText));
            OnPropertyChanged(nameof(HumanCostsTitleText));
            OnPropertyChanged(nameof(HumanCostsHelpText));
        }

        private void OnHumanEntryChanged()
        {
            if (updatingHumanEntries) return;
            var rows = new Dictionary<string, Dictionary<eGoods, int>>();
            foreach (var row in HumanCostEntries)
            {
                var goods = new Dictionary<eGoods, int>();
                foreach (var cell in row.CostCells) goods[cell.Good] = cell.Amount;
                rows[row.Key] = goods;
            }
            humanExtraBuildingCosts = SerializeHumanCosts(rows);
            SettingChanged?.Invoke(nameof(HumanExtraBuildingCosts));
            OnPropertyChanged(nameof(HumanExtraBuildingCosts));
        }

        private static int ClampHumanCost(eGoods good, int amount) => good == eGoods.STORED_GOLD
            ? Math.Max(-10000, Math.Min(10000, amount)) : Math.Max(0, Math.Min(100, amount));

        public sealed class HumanCostEntryViewModel : INotifyPropertyChanged
        {
            private string displayName;
            internal HumanCostEntryViewModel(string key, IReadOnlyList<HumanCostCellViewModel> cells) { Key = key; CostCells = cells; }
            public string Key { get; }
            public string DisplayName { get => displayName; internal set { displayName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName))); } }
            public ImageSource IconImage => GetBuildingIconImage(Key);
            public IReadOnlyList<HumanCostCellViewModel> CostCells { get; }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        public sealed class HumanCostCellViewModel : INotifyPropertyChanged
        {
            private readonly Func<bool> canEdit;
            private readonly Action changed;
            private int amount;
            private string rowName;
            internal HumanCostCellViewModel(string row, eGoods good, Func<bool> canEdit, Action changed)
            { Key = "human." + row + "." + good; Good = good; this.canEdit = canEdit; this.changed = changed; }
            public string Key { get; }
            public eGoods Good { get; }
            public string Name => GetLocalizedGoodName(Good, Good.ToString());
            public string SearchTitle => rowName + " / " + Name;
            public string ToolTip => SearchTitle + ": " + SerpLocalization.Get("BuildingCosts.HumanCellHelp");
            public ImageSource IconImage => GetGoodIconImage(Good);
            public string AmountText { get => amount.ToString(CultureInfo.InvariantCulture); set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) Set(parsed, true); else Notify(nameof(AmountText)); } }
            public int SliderAmount { get => Math.Max(0, Math.Min(100, amount)); set => Set(value, true); }
            public int Amount => amount;
            internal void SetFromOwner(int value) => Set(value, false);
            private void Set(int value, bool notifyOwner)
            {
                if (notifyOwner && !canEdit()) { Notify(nameof(AmountText)); return; }
                int next = ClampHumanCost(Good, value);
                if (amount == next) { Notify(nameof(AmountText)); return; }
                amount = next; Notify(nameof(Amount)); Notify(nameof(AmountText)); Notify(nameof(SliderAmount));
                if (notifyOwner) changed();
            }
            internal void RefreshName(string row) { rowName = row; Notify(nameof(Name)); Notify(nameof(SearchTitle)); Notify(nameof(ToolTip)); Notify(nameof(IconImage)); }
            private void Notify([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
