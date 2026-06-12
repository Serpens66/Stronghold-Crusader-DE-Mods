using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
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
            CostEntries = CreateCostEntriesWithCallback(unitCosts);
        }

        public IReadOnlyList<CostEntryViewModel> CostEntries { get; }

        public string TitleText => IsGermanLanguage() ? "EINHEITENKOSTEN" : "UNIT COSTS";
        public string HelpText => IsGermanLanguage()
            ? "-1 = unverändert. Werte von 0 bis 1000 setzen die Kosten für diese Einheit und Ressource."
            : "-1 = unchanged. Values 0 to 1000 set the cost for that unit and resource.";
        public string UnitHeaderText => IsGermanLanguage() ? "Einheit" : "Unit";
        public string BowsHeaderText => UnitCostsRuntime.GetLocalizedGoodName(eGoods.STORED_BOWS, "Bows");
        public string CrossbowsHeaderText => UnitCostsRuntime.GetLocalizedGoodName(eGoods.STORED_CROSSBOWS, "Crossbows");
        public string SpearsHeaderText => UnitCostsRuntime.GetLocalizedGoodName(eGoods.STORED_SPEARS, "Spears");
        public string PikesHeaderText => UnitCostsRuntime.GetLocalizedGoodName(eGoods.STORED_PIKES, "Pikes");
        public string MacesHeaderText => UnitCostsRuntime.GetLocalizedGoodName(eGoods.STORED_MACES, "Maces");
        public string SwordsHeaderText => UnitCostsRuntime.GetLocalizedGoodName(eGoods.STORED_SWORDS, "Swords");
        public string LeatherArmourHeaderText => UnitCostsRuntime.GetLocalizedGoodName(eGoods.STORED_LEATHER_ARMOUR, "Leather Armour");
        public string MetalArmourHeaderText => UnitCostsRuntime.GetLocalizedGoodName(eGoods.STORED_METAL_ARMOUR, "Metal Armour");
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
            foreach (CostEntryViewModel entry in CostEntries)
            {
                if (Enum.TryParse(entry.Key, out eChimps unitType))
                    entry.DisplayName = UnitCostsRuntime.GetLocalizedUnitName(unitType);
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
            OnPropertyChanged(nameof(BowsHeaderText));
            OnPropertyChanged(nameof(CrossbowsHeaderText));
            OnPropertyChanged(nameof(SpearsHeaderText));
            OnPropertyChanged(nameof(PikesHeaderText));
            OnPropertyChanged(nameof(MacesHeaderText));
            OnPropertyChanged(nameof(SwordsHeaderText));
            OnPropertyChanged(nameof(LeatherArmourHeaderText));
            OnPropertyChanged(nameof(MetalArmourHeaderText));
            OnPropertyChanged(nameof(GoldHeaderText));
        }

        private static string CreateDefaultUnitCosts()
        {
            StringBuilder builder = new StringBuilder("# -1 = unchanged; order is bows,crossbows,spears,pikes,maces,swords,leatherArmour,metalArmour,gold");
            foreach (string key in DefaultUnitKeys)
            {
                builder.AppendLine();
                builder.Append(key);
                builder.Append("=-1,-1,-1,-1,-1,-1,-1,-1,-1");
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
                    value = new UnitCostValues(-1, -1, -1, -1, -1, -1, -1, -1, -1);

                entries.Add(new CostEntryViewModel(key, FormatDisplayName(key), value, OnEntryChanged));
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
                if (costParts.Length != 9)
                    continue;

                if (!int.TryParse(costParts[0].Trim(), out int bows) ||
                    !int.TryParse(costParts[1].Trim(), out int crossbows) ||
                    !int.TryParse(costParts[2].Trim(), out int spears) ||
                    !int.TryParse(costParts[3].Trim(), out int pikes) ||
                    !int.TryParse(costParts[4].Trim(), out int maces) ||
                    !int.TryParse(costParts[5].Trim(), out int swords) ||
                    !int.TryParse(costParts[6].Trim(), out int leatherArmour) ||
                    !int.TryParse(costParts[7].Trim(), out int metalArmour) ||
                    !int.TryParse(costParts[8].Trim(), out int gold))
                {
                    continue;
                }

                result[keyValue[0].Trim()] = new UnitCostValues(
                    bows,
                    crossbows,
                    spears,
                    pikes,
                    maces,
                    swords,
                    leatherArmour,
                    metalArmour,
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
            StringBuilder builder = new StringBuilder("# -1 = unchanged; order is bows,crossbows,spears,pikes,maces,swords,leatherArmour,metalArmour,gold");
            foreach (CostEntryViewModel entry in CostEntries)
            {
                builder.AppendLine();
                builder.Append(entry.Key);
                builder.Append('=');
                builder.Append(entry.Bows);
                builder.Append(',');
                builder.Append(entry.Crossbows);
                builder.Append(',');
                builder.Append(entry.Spears);
                builder.Append(',');
                builder.Append(entry.Pikes);
                builder.Append(',');
                builder.Append(entry.Maces);
                builder.Append(',');
                builder.Append(entry.Swords);
                builder.Append(',');
                builder.Append(entry.LeatherArmour);
                builder.Append(',');
                builder.Append(entry.MetalArmour);
                builder.Append(',');
                builder.Append(entry.Gold);
            }

            return builder.ToString();
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
            private int bows;
            private int crossbows;
            private int spears;
            private int pikes;
            private int maces;
            private int swords;
            private int leatherArmour;
            private int metalArmour;
            private int gold;

            public event PropertyChangedEventHandler PropertyChanged;

            public CostEntryViewModel(string key, string displayName, UnitCostValues values, Action changed = null)
            {
                Key = key;
                this.displayName = displayName;
                this.changed = changed;
                bows = values.Bows;
                crossbows = values.Crossbows;
                spears = values.Spears;
                pikes = values.Pikes;
                maces = values.Maces;
                swords = values.Swords;
                leatherArmour = values.LeatherArmour;
                metalArmour = values.MetalArmour;
                gold = values.Gold;
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

            public int Bows { get => bows; private set => SetCost(ref bows, value, nameof(BowsText)); }
            public int Crossbows { get => crossbows; private set => SetCost(ref crossbows, value, nameof(CrossbowsText)); }
            public int Spears { get => spears; private set => SetCost(ref spears, value, nameof(SpearsText)); }
            public int Pikes { get => pikes; private set => SetCost(ref pikes, value, nameof(PikesText)); }
            public int Maces { get => maces; private set => SetCost(ref maces, value, nameof(MacesText)); }
            public int Swords { get => swords; private set => SetCost(ref swords, value, nameof(SwordsText)); }
            public int LeatherArmour { get => leatherArmour; private set => SetCost(ref leatherArmour, value, nameof(LeatherArmourText)); }
            public int MetalArmour { get => metalArmour; private set => SetCost(ref metalArmour, value, nameof(MetalArmourText)); }
            public int Gold { get => gold; private set => SetCost(ref gold, value, nameof(GoldText)); }

            public string BowsText { get => bows.ToString(); set => SetTextCost(value, v => Bows = v); }
            public string CrossbowsText { get => crossbows.ToString(); set => SetTextCost(value, v => Crossbows = v); }
            public string SpearsText { get => spears.ToString(); set => SetTextCost(value, v => Spears = v); }
            public string PikesText { get => pikes.ToString(); set => SetTextCost(value, v => Pikes = v); }
            public string MacesText { get => maces.ToString(); set => SetTextCost(value, v => Maces = v); }
            public string SwordsText { get => swords.ToString(); set => SetTextCost(value, v => Swords = v); }
            public string LeatherArmourText { get => leatherArmour.ToString(); set => SetTextCost(value, v => LeatherArmour = v); }
            public string MetalArmourText { get => metalArmour.ToString(); set => SetTextCost(value, v => MetalArmour = v); }
            public string GoldText { get => gold.ToString(); set => SetTextCost(value, v => Gold = v); }

            public void SetCostsFromOwner(UnitCostValues values)
            {
                Bows = values.Bows;
                Crossbows = values.Crossbows;
                Spears = values.Spears;
                Pikes = values.Pikes;
                Maces = values.Maces;
                Swords = values.Swords;
                LeatherArmour = values.LeatherArmour;
                MetalArmour = values.MetalArmour;
                Gold = values.Gold;
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
    }
}
