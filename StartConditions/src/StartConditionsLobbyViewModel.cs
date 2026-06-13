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

namespace StartConditions
{
    public sealed class StartConditionsLobbyViewModel : LobbyModSettingsBaseViewModel
    {
        public event Action<string> SettingChanged;

        private int setStartGoldAI = -1;
        private int setStartGoldHuman = -1;
        private int addStartGoldAI = 0;
        private int addStartGoldHuman = 0;
        private int multiplyStartTroopsAI = 0;
        private int multiplyStartTroopsHuman = 0;
        private string startGoodsAI = DefaultStartGoodsAI;
        private string startGoodsHuman = DefaultStartGoodsHuman;
        private string addStartTroopsAI = DefaultTroops;
        private string addStartTroopsHuman = DefaultTroops;
        private bool updatingEntries;
        private bool loggedGoodsLocalizationDiagnostics;

        public const string DefaultStartGoodsAI = @"STORED_WOOD_PLANKS=-1
STORED_RAW_HOPS=-1
STORED_STONE_BLOCKS=-1
STORED_IRON_INGOTS=-1
STORED_PITCH_RAW=-1
STORED_RAW_WHEAT=-1
STORED_FOOD_BREAD=-1
STORED_FOOD_CHEESE=-1
STORED_FOOD_MEAT=-1
STORED_FOOD_FRUIT=-1
STORED_FOOD_ALE=-1
STORED_FLOUR=-1
STORED_BOWS=-1
STORED_CROSSBOWS=-1
STORED_SPEARS=-1
STORED_PIKES=-1
STORED_MACES=-1
STORED_SWORDS=-1
STORED_LEATHER_ARMOUR=-1
STORED_METAL_ARMOUR=-1";

        public const string DefaultStartGoodsHuman = @"STORED_WOOD_PLANKS=-1
STORED_RAW_HOPS=-1
STORED_STONE_BLOCKS=-1
STORED_IRON_INGOTS=-1
STORED_PITCH_RAW=-1
STORED_RAW_WHEAT=-1
STORED_FOOD_BREAD=-1
STORED_FOOD_CHEESE=-1
STORED_FOOD_MEAT=-1
STORED_FOOD_FRUIT=-1
STORED_FOOD_ALE=-1
STORED_FLOUR=-1
STORED_BOWS=-1
STORED_CROSSBOWS=-1
STORED_SPEARS=-1
STORED_PIKES=-1
STORED_MACES=-1
STORED_SWORDS=-1
STORED_LEATHER_ARMOUR=-1
STORED_METAL_ARMOUR=-1";

        private const string VanillaSkirmishGoods = @"STORED_WOOD_PLANKS=100
STORED_RAW_HOPS=0
STORED_STONE_BLOCKS=50
STORED_IRON_INGOTS=0
STORED_PITCH_RAW=0
STORED_RAW_WHEAT=0
STORED_FOOD_BREAD=50
STORED_FOOD_CHEESE=0
STORED_FOOD_MEAT=0
STORED_FOOD_FRUIT=0
STORED_FOOD_ALE=0
STORED_FLOUR=0
STORED_BOWS=0
STORED_CROSSBOWS=0
STORED_SPEARS=0
STORED_PIKES=0
STORED_MACES=0
STORED_SWORDS=0
STORED_LEATHER_ARMOUR=0
STORED_METAL_ARMOUR=0";

        private const string VanillaDeathmatchGoods = @"STORED_WOOD_PLANKS=150
STORED_RAW_HOPS=20
STORED_STONE_BLOCKS=150
STORED_IRON_INGOTS=25
STORED_PITCH_RAW=48
STORED_RAW_WHEAT=25
STORED_FOOD_BREAD=200
STORED_FOOD_CHEESE=0
STORED_FOOD_MEAT=0
STORED_FOOD_FRUIT=0
STORED_FOOD_ALE=10
STORED_FLOUR=0
STORED_BOWS=0
STORED_CROSSBOWS=0
STORED_SPEARS=0
STORED_PIKES=0
STORED_MACES=0
STORED_SWORDS=0
STORED_LEATHER_ARMOUR=0
STORED_METAL_ARMOUR=0";

        public const string DefaultTroops = @"CHIMP_TYPE_ARCHER=0
CHIMP_TYPE_SPEARMAN=0
CHIMP_TYPE_MACEMAN=0
CHIMP_TYPE_XBOWMAN=0
CHIMP_TYPE_PIKEMAN=0
CHIMP_TYPE_SWORDSMAN=0
CHIMP_TYPE_KNIGHT=0
CHIMP_TYPE_ENGINEER=0
CHIMP_TYPE_MONK=0
CHIMP_TYPE_LADDERMAN=0
CHIMP_TYPE_TUNNELER=0
CHIMP_TYPE_ARAB_BOW=0
CHIMP_TYPE_ARAB_SLAVE=0
CHIMP_TYPE_ARAB_SLINGER=0
CHIMP_TYPE_ARAB_ASSASIN=0
CHIMP_TYPE_ARAB_HORSEMAN=0
CHIMP_TYPE_ARAB_SWORDSMAN=0
CHIMP_TYPE_ARAB_GRENADIER=0
CHIMP_TYPE_BEDOUIN_CAMEL_LANCER=0
CHIMP_TYPE_BEDOUIN_HEALER=0
CHIMP_TYPE_BEDOUIN_EUNUCH=0
CHIMP_TYPE_BEDOUIN_AMBUSHER=0
CHIMP_TYPE_BEDOUIN_SKIRMISHER=0
CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL=0
CHIMP_TYPE_BEDOUIN_SAPPER=0
CHIMP_TYPE_BEDOUIN_DEMOLISHER=0";

        public StartConditionsLobbyViewModel()
        {
            StartGoodEntries = CreateGoodEntriesWithCallback(DefaultStartGoodsAI, DefaultStartGoodsHuman);
            StartTroopEntries = CreateTroopEntriesWithCallback(DefaultTroops, DefaultTroops);
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public IReadOnlyList<AmountEntryViewModel> StartGoodEntries { get; }

        public IReadOnlyList<AmountEntryViewModel> StartTroopEntries { get; }

        public RelayCommand ResetToDefaultCommand { get; }

        public void RefreshLocalizedNames(Action<string> logInfo = null)
        {
            bool logGoodsDiagnostics = !loggedGoodsLocalizationDiagnostics && logInfo != null;
            if (logGoodsDiagnostics)
            {
                loggedGoodsLocalizationDiagnostics = true;
                logInfo("Goods localization diagnostics begin");
            }

            foreach (AmountEntryViewModel entry in StartGoodEntries)
            {
                if (Enum.TryParse(entry.Key, out eGoods good))
                {
                    StartConditionsRuntime.TryGetLocalizedGoodName(good, out string displayName, out string translationKey, out bool found);
                    entry.DisplayName = displayName;

                    if (logGoodsDiagnostics)
                    {
                        logInfo("Good " + good + " index=" + (int)good + " key=" + translationKey + " found=" + found + " name=" + displayName);
                    }
                }
            }

            if (logGoodsDiagnostics)
                logInfo("Goods localization diagnostics end");

            foreach (AmountEntryViewModel entry in StartTroopEntries)
            {
                if (Enum.TryParse(entry.Key, out eChimps unitType))
                    entry.DisplayName = StartConditionsRuntime.GetLocalizedUnitName(unitType);
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

        [SyncHostOnly] public int SetStartGoldAI { get => setStartGoldAI; set => SetInt(ref setStartGoldAI, value, nameof(SetStartGoldAI), nameof(SetStartGoldAIText)); }
        [SyncHostOnly] public int SetStartGoldHuman { get => setStartGoldHuman; set => SetInt(ref setStartGoldHuman, value, nameof(SetStartGoldHuman), nameof(SetStartGoldHumanText)); }
        [SyncHostOnly] public int AddStartGoldAI { get => addStartGoldAI; set => SetInt(ref addStartGoldAI, value, nameof(AddStartGoldAI), nameof(AddStartGoldAIText)); }
        [SyncHostOnly] public int AddStartGoldHuman { get => addStartGoldHuman; set => SetInt(ref addStartGoldHuman, value, nameof(AddStartGoldHuman), nameof(AddStartGoldHumanText)); }
        [SyncHostOnly] public int MultiplyStartTroopsAI { get => multiplyStartTroopsAI; set => SetInt(ref multiplyStartTroopsAI, value, nameof(MultiplyStartTroopsAI), nameof(MultiplyStartTroopsAIText)); }
        [SyncHostOnly] public int MultiplyStartTroopsHuman { get => multiplyStartTroopsHuman; set => SetInt(ref multiplyStartTroopsHuman, value, nameof(MultiplyStartTroopsHuman), nameof(MultiplyStartTroopsHumanText)); }

        public string SetStartGoldAIText { get => SetStartGoldAI.ToString(); set => SetIntText(value, parsed => SetStartGoldAI = parsed, nameof(SetStartGoldAIText)); }
        public string SetStartGoldHumanText { get => SetStartGoldHuman.ToString(); set => SetIntText(value, parsed => SetStartGoldHuman = parsed, nameof(SetStartGoldHumanText)); }
        public string AddStartGoldAIText { get => AddStartGoldAI.ToString(); set => SetIntText(value, parsed => AddStartGoldAI = parsed, nameof(AddStartGoldAIText)); }
        public string AddStartGoldHumanText { get => AddStartGoldHuman.ToString(); set => SetIntText(value, parsed => AddStartGoldHuman = parsed, nameof(AddStartGoldHumanText)); }
        public string MultiplyStartTroopsAIText { get => MultiplyStartTroopsAI.ToString(); set => SetIntText(value, parsed => MultiplyStartTroopsAI = parsed, nameof(MultiplyStartTroopsAIText)); }
        public string MultiplyStartTroopsHumanText { get => MultiplyStartTroopsHuman.ToString(); set => SetIntText(value, parsed => MultiplyStartTroopsHuman = parsed, nameof(MultiplyStartTroopsHumanText)); }

        [SyncHostOnly]
        public string StartGoodsAI
        {
            get => startGoodsAI;
            set
            {
                if (Equals(startGoodsAI, value))
                    return;

                startGoodsAI = value;
                ApplySerializedAmountsToEntries(StartGoodEntries, startGoodsAI, startGoodsHuman);
                SettingChanged?.Invoke(nameof(StartGoodsAI));
                OnPropertyChanged(nameof(StartGoodsAI));
            }
        }

        [SyncHostOnly]
        public string StartGoodsHuman
        {
            get => startGoodsHuman;
            set
            {
                if (Equals(startGoodsHuman, value))
                    return;

                startGoodsHuman = value;
                ApplySerializedAmountsToEntries(StartGoodEntries, startGoodsAI, startGoodsHuman);
                SettingChanged?.Invoke(nameof(StartGoodsHuman));
                OnPropertyChanged(nameof(StartGoodsHuman));
            }
        }

        [SyncHostOnly]
        public string AddStartTroopsAI
        {
            get => addStartTroopsAI;
            set
            {
                if (Equals(addStartTroopsAI, value))
                    return;

                addStartTroopsAI = value;
                ApplySerializedAmountsToEntries(StartTroopEntries, addStartTroopsAI, addStartTroopsHuman);
                SettingChanged?.Invoke(nameof(AddStartTroopsAI));
                OnPropertyChanged(nameof(AddStartTroopsAI));
            }
        }

        [SyncHostOnly]
        public string AddStartTroopsHuman
        {
            get => addStartTroopsHuman;
            set
            {
                if (Equals(addStartTroopsHuman, value))
                    return;

                addStartTroopsHuman = value;
                ApplySerializedAmountsToEntries(StartTroopEntries, addStartTroopsAI, addStartTroopsHuman);
                SettingChanged?.Invoke(nameof(AddStartTroopsHuman));
                OnPropertyChanged(nameof(AddStartTroopsHuman));
            }
        }

        private void ResetToDefault()
        {
            SetStartGoldAI = -1;
            SetStartGoldHuman = -1;
            AddStartGoldAI = 0;
            AddStartGoldHuman = 0;
            MultiplyStartTroopsAI = 0;
            MultiplyStartTroopsHuman = 0;
            StartGoodsAI = DefaultStartGoodsAI;
            StartGoodsHuman = DefaultStartGoodsHuman;
            AddStartTroopsAI = DefaultTroops;
            AddStartTroopsHuman = DefaultTroops;
        }

        private static IReadOnlyList<AmountEntryViewModel> CreateGoodEntries(string aiSerialized, string humanSerialized)
        {
            Dictionary<string, int> aiValues = ParseSerializedAmounts(aiSerialized);
            Dictionary<string, int> humanValues = ParseSerializedAmounts(humanSerialized);
            Dictionary<string, int> normalCrusaderValues = ParseSerializedAmounts(VanillaSkirmishGoods);
            Dictionary<string, int> deathmatchValues = ParseSerializedAmounts(VanillaDeathmatchGoods);
            Array values = Enum.GetValues(typeof(eGoods));
            List<AmountEntryViewModel> entries = new List<AmountEntryViewModel>(values.Length);
            foreach (eGoods good in values)
            {
                if (!StartConditionsRuntime.IsConfigurableStoredGood(good))
                    continue;

                string key = good.ToString();
                entries.Add(new AmountEntryViewModel(
                    key,
                    FormatDisplayName(key, "STORED_"),
                    null,
                    GetValueOrDefault(aiValues, key, -1),
                    GetValueOrDefault(humanValues, key, -1),
                    FormatVanillaAmount(normalCrusaderValues, key),
                    FormatVanillaAmount(deathmatchValues, key)));
            }

            return entries;
        }

        private static IReadOnlyList<AmountEntryViewModel> CreateTroopEntries(string aiSerialized, string humanSerialized)
        {
            Dictionary<string, int> aiValues = ParseSerializedAmounts(aiSerialized);
            Dictionary<string, int> humanValues = ParseSerializedAmounts(humanSerialized);
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

            List<AmountEntryViewModel> entries = new List<AmountEntryViewModel>(keys.Length);
            foreach (string key in keys)
            {
                eChimps unitType = Enum.TryParse(key, out eChimps parsedUnitType) ? parsedUnitType : eChimps.CHIMP_TYPE_NULL;
                entries.Add(new AmountEntryViewModel(
                    key,
                    FormatDisplayName(key, "CHIMP_TYPE_"),
                    null,
                    GetValueOrDefault(aiValues, key, 0),
                    GetValueOrDefault(humanValues, key, 0),
                    string.Empty,
                    string.Empty));
            }

            return entries;
        }

        private static Dictionary<string, int> ParseSerializedAmounts(string text)
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

                result[parts[0].Trim()] = ClampAmount(value);
            }

            return result;
        }

        private void ApplySerializedAmountsToEntries(IReadOnlyList<AmountEntryViewModel> entries, string aiSerialized, string humanSerialized)
        {
            if (updatingEntries)
                return;

            Dictionary<string, int> aiValues = ParseSerializedAmounts(aiSerialized);
            Dictionary<string, int> humanValues = ParseSerializedAmounts(humanSerialized);
            updatingEntries = true;
            try
            {
                foreach (AmountEntryViewModel entry in entries)
                {
                    entry.SetAmountsFromOwner(
                        GetValueOrDefault(aiValues, entry.Key, entry.AIAmount),
                        GetValueOrDefault(humanValues, entry.Key, entry.HumanAmount));
                }
            }
            finally
            {
                updatingEntries = false;
            }
        }

        private void OnGoodsEntryChanged()
        {
            if (updatingEntries)
                return;

            startGoodsAI = BuildSerializedAmounts(StartGoodEntries, true);
            startGoodsHuman = BuildSerializedAmounts(StartGoodEntries, false);
            SettingChanged?.Invoke(nameof(StartGoodsAI));
            SettingChanged?.Invoke(nameof(StartGoodsHuman));
            OnPropertyChanged(nameof(StartGoodsAI));
            OnPropertyChanged(nameof(StartGoodsHuman));
        }

        private void OnTroopsEntryChanged()
        {
            if (updatingEntries)
                return;

            addStartTroopsAI = BuildSerializedAmounts(StartTroopEntries, true);
            addStartTroopsHuman = BuildSerializedAmounts(StartTroopEntries, false);
            SettingChanged?.Invoke(nameof(AddStartTroopsAI));
            SettingChanged?.Invoke(nameof(AddStartTroopsHuman));
            OnPropertyChanged(nameof(AddStartTroopsAI));
            OnPropertyChanged(nameof(AddStartTroopsHuman));
        }

        private static string BuildSerializedAmounts(IReadOnlyList<AmountEntryViewModel> entries, bool ai)
        {
            StringBuilder builder = new StringBuilder("# -1 = unchanged");
            foreach (AmountEntryViewModel entry in entries)
            {
                builder.AppendLine();
                builder.Append(entry.Key);
                builder.Append('=');
                builder.Append(ai ? entry.AIAmount : entry.HumanAmount);
            }

            return builder.ToString();
        }

        private static int GetValueOrDefault(Dictionary<string, int> values, string key, int defaultValue)
        {
            return values.TryGetValue(key, out int value) ? value : defaultValue;
        }

        private static string FormatVanillaAmount(Dictionary<string, int> values, string key)
        {
            return values.TryGetValue(key, out int value) ? value.ToString() : "0";
        }

        private static string FormatDisplayName(string key, string prefix)
        {
            string name = key.StartsWith(prefix, StringComparison.Ordinal) ? key.Substring(prefix.Length) : key;
            return name.Replace('_', ' ').ToLowerInvariant();
        }

        private static int ClampAmount(int value)
        {
            if (value < -1)
                return -1;
            if (value > 100000)
                return 100000;
            return value;
        }

        private IReadOnlyList<AmountEntryViewModel> CreateGoodEntriesWithCallback(string aiSerialized, string humanSerialized)
        {
            List<AmountEntryViewModel> entries = new List<AmountEntryViewModel>();
            foreach (AmountEntryViewModel entry in CreateGoodEntries(aiSerialized, humanSerialized))
                entries.Add(new AmountEntryViewModel(
                    entry.Key,
                    entry.DisplayName,
                    entry.IconImage,
                    entry.AIAmount,
                    entry.HumanAmount,
                    entry.NormalCrusaderAmountText,
                    entry.DeathmatchAmountText,
                    OnGoodsEntryChanged));
            return entries;
        }

        private IReadOnlyList<AmountEntryViewModel> CreateTroopEntriesWithCallback(string aiSerialized, string humanSerialized)
        {
            List<AmountEntryViewModel> entries = new List<AmountEntryViewModel>();
            foreach (AmountEntryViewModel entry in CreateTroopEntries(aiSerialized, humanSerialized))
                entries.Add(new AmountEntryViewModel(
                    entry.Key,
                    entry.DisplayName,
                    entry.IconImage,
                    entry.AIAmount,
                    entry.HumanAmount,
                    entry.NormalCrusaderAmountText,
                    entry.DeathmatchAmountText,
                    OnTroopsEntryChanged));
            return entries;
        }

        private void Set<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private void SetInt(ref int field, int value, string propertyName, string textPropertyName)
        {
            if (field == value)
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(textPropertyName);
        }

        private void SetIntText(string text, Action<int> setValue, string textPropertyName)
        {
            if (!int.TryParse(text, out int parsed))
            {
                OnPropertyChanged(textPropertyName);
                return;
            }

            setValue(parsed);
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

        public sealed class AmountEntryViewModel : INotifyPropertyChanged
        {
            private readonly Action changed;
            private string displayName;
            private int aiAmount;
            private int humanAmount;

            public event PropertyChangedEventHandler PropertyChanged;

            public AmountEntryViewModel(
                string key,
                string displayName,
                ImageSource iconImage,
                int aiAmount,
                int humanAmount,
                string normalCrusaderAmountText = "",
                string deathmatchAmountText = "",
                Action changed = null)
            {
                Key = key;
                DisplayName = displayName;
                NormalCrusaderAmountText = normalCrusaderAmountText;
                DeathmatchAmountText = deathmatchAmountText;
                this.changed = changed;
                this.aiAmount = ClampAmount(aiAmount);
                this.humanAmount = ClampAmount(humanAmount);
            }

            public string Key { get; }
            public ImageSource IconImage
            {
                get
                {
                    if (Enum.TryParse(Key, out eGoods good))
                        return GetGoodIconImage(good);
                    if (Enum.TryParse(Key, out eChimps unitType))
                        return GetUnitIconImage(unitType);
                    return null;
                }
            }

            public string NormalCrusaderAmountText { get; }

            public string DeathmatchAmountText { get; }

            public string DisplayName
            {
                get => displayName;
                set
                {
                    if (displayName == value)
                        return;

                    displayName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AIAmountToolTip));
                    OnPropertyChanged(nameof(HumanAmountToolTip));
                    OnPropertyChanged(nameof(NormalCrusaderAmountToolTip));
                    OnPropertyChanged(nameof(DeathmatchAmountToolTip));
                }
            }

            public string AIAmountToolTip => FormatCellToolTip(DisplayName, "AI");

            public string HumanAmountToolTip => FormatCellToolTip(DisplayName, "Human");

            public string NormalCrusaderAmountToolTip => FormatCellToolTip(DisplayName, "Normal/Crusade");

            public string DeathmatchAmountToolTip => FormatCellToolTip(DisplayName, "Deathmatch");

            public int AIAmount
            {
                get => aiAmount;
                private set
                {
                    int clamped = ClampAmount(value);
                    if (aiAmount == clamped)
                        return;

                    aiAmount = clamped;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AIAmountText));
                    changed?.Invoke();
                }
            }

            public string AIAmountText
            {
                get => aiAmount.ToString();
                set
                {
                    if (!int.TryParse(value, out int parsed))
                    {
                        OnPropertyChanged();
                        return;
                    }

                    AIAmount = parsed;
                }
            }

            public int HumanAmount
            {
                get => humanAmount;
                private set
                {
                    int clamped = ClampAmount(value);
                    if (humanAmount == clamped)
                        return;

                    humanAmount = clamped;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HumanAmountText));
                    changed?.Invoke();
                }
            }

            public string HumanAmountText
            {
                get => humanAmount.ToString();
                set
                {
                    if (!int.TryParse(value, out int parsed))
                    {
                        OnPropertyChanged();
                        return;
                    }

                    HumanAmount = parsed;
                }
            }

            public void SetAmountsFromOwner(int aiAmount, int humanAmount)
            {
                AIAmount = aiAmount;
                HumanAmount = humanAmount;
            }

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
