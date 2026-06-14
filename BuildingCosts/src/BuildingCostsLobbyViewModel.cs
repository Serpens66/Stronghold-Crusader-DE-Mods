using SHCDESE.API;
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

namespace BuildingCosts
{
    public sealed class BuildingCostsLobbyViewModel : LobbyModSettingsBaseViewModel
    {
        public event Action<string> SettingChanged;

        private string buildingCosts = CreateDefaultBuildingCosts();
        private bool updatingEntries;
        private const string GoodsTextSection = "TEXT_GOODS";

        private static readonly string[] DefaultCostKeys =
        {
            "MAPPER_WOODSMAN",
            "MAPPER_HUNTER",
            "MAPPER_OXENBASE",
            "MAPPER_QUARRY",
            "MAPPER_IRON_MINE",
            "MAPPER_PITCH_WORKINGS",
            "MAPPER_WHEATFARM",
            "MAPPER_HOPSFARM",
            "MAPPER_APPLEFARM",
            "MAPPER_CATTLEFARM",
            "MAPPER_MILL",
            "MAPPER_BAKER",
            "MAPPER_BREWER",
            "MAPPER_HOVEL",
            "MAPPER_GRANARY",
            "MAPPER_STORES",
            "MAPPER_ARMOURY",
            "MAPPER_TRADEPOST",
            "MAPPER_INN",
            "MAPPER_HEALER",
            "MAPPER_FLETCHER",
            "MAPPER_POLETURNER",
            "MAPPER_BLACKSMITH",
            "MAPPER_ARMOURER",
            "MAPPER_TANNER",
            "MAPPER_STABLES",
            "MAPPER_BARRACKS_WOOD",
            "MAPPER_BARRACKS_STONE",
            "MAPPER_ENGINEERS_GUILD",
            "MAPPER_TUNNELERS_GUILD",
            "MAPPER_OIL_SMELTER",
            "MAPPER_WELL",
            "MAPPER_WATERPOT",
            "MAPPER_CHURCH1",
            "MAPPER_CHURCH2",
            "MAPPER_CHURCH3",
            "MAPPER_TOWER1",
            "MAPPER_TOWER2",
            "MAPPER_TOWER3",
            "MAPPER_TOWER4",
            "MAPPER_TOWER5",
            "MAPPER_GATE_MAIN",
            "MAPPER_GATE_INNER",
            "MAPPER_GATE_WOOD",
            "MAPPER_DRAWBRIDGE",
            "MAPPER_KILLING_PIT",
            "MAPPER_BRAZIER",
            "MAPPER_MANGONEL",
            "MAPPER_BALLISTA",
            "MAPPER_MAYPOLE",
            "MAPPER_GALLOWS",
            "MAPPER_STOCKS",
            "MAPPER_GARDEN1",
            "MAPPER_CESS_PIT1",
            "MAPPER_BURNING_STAKE",
            "MAPPER_GIBBET",
            "MAPPER_DUNGEON",
            "MAPPER_RACK_STRETCHING",
            "MAPPER_CHOPPING_BLOCK",
            "MAPPER_DUNKING_STOOL",
            "MAPPER_DOG_CAGE",
            "MAPPER_STATUE1",
            "MAPPER_SHRINE1",
            "MAPPER_DANCING_BEAR",
            "MAPPER_POND1",
            "MAPPER_OUTPOST_BEDOUIN",
            "MAPPER_BEDOUIN_STOCKADE"
        };

        public BuildingCostsLobbyViewModel()
        {
            CostEntries = CreateCostEntriesWithCallback(buildingCosts);
            RefreshCostEntryToolTips();
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public IReadOnlyList<CostEntryViewModel> CostEntries { get; }

        public RelayCommand ResetToDefaultCommand { get; }

        public string TitleText => IsGermanLanguage() ? "BAUKOSTEN" : "BUILDING COSTS";
        public string HelpText => IsGermanLanguage()
            ? "-1 = unverändert. Werte von 0 bis 1000 setzen die nativen Baukosten für dieses Material (Mensch und KI)."
            : "-1 = unchanged. Values 0 to 1000 set the native construction cost for that material (Human and AI).";
        public string BuildingHeaderText => IsGermanLanguage() ? "Gebäude" : "Building";
        public string WoodHeaderText => GetLocalizedGoodName(eGoods.STORED_WOOD_PLANKS, "Wood");
        public string StoneHeaderText => GetLocalizedGoodName(eGoods.STORED_STONE_BLOCKS, "Stone");
        public string IronHeaderText => GetLocalizedGoodName(eGoods.STORED_IRON_INGOTS, "Iron");
        public string PitchHeaderText => GetLocalizedGoodName(eGoods.STORED_PITCH_RAW, "Pitch");
        public string GoldHeaderText => GetLocalizedGoodName(eGoods.STORED_GOLD, "Gold");
        public ImageSource WoodHeaderIcon => GetGoodIconImage(eGoods.STORED_WOOD_PLANKS);
        public ImageSource StoneHeaderIcon => GetGoodIconImage(eGoods.STORED_STONE_BLOCKS);
        public ImageSource IronHeaderIcon => GetGoodIconImage(eGoods.STORED_IRON_INGOTS);
        public ImageSource PitchHeaderIcon => GetGoodIconImage(eGoods.STORED_PITCH_RAW);
        public ImageSource GoldHeaderIcon => GetGoodIconImage(eGoods.STORED_GOLD);

        [SyncHostOnly]
        public string BuildingCosts
        {
            get => buildingCosts;
            set
            {
                if (Equals(buildingCosts, value))
                    return;

                buildingCosts = value;
                ApplySerializedCostsToEntries(value);
                SettingChanged?.Invoke(nameof(BuildingCosts));
                OnPropertyChanged(nameof(BuildingCosts));
            }
        }

        private void ResetToDefault()
        {
            BuildingCosts = CreateDefaultBuildingCosts();
        }

        public void RefreshLocalizedNames()
        {
            RefreshLocalizedHeaderTexts();
            Dictionary<eMappers, BuildingCostDefinition> definitions = BuildingCostsRuntime.CreateBuildingCostDefinitions();
            foreach (CostEntryViewModel entry in CostEntries)
            {
                if (Enum.TryParse(entry.Key, out eMappers mapper) &&
                    definitions.TryGetValue(mapper, out BuildingCostDefinition definition))
                {
                    entry.DisplayName = BuildingCostsRuntime.GetLocalizedBuildingName(definition);
                }
            }

            RefreshCostEntryToolTips();
        }

        private void RefreshLocalizedHeaderTexts()
        {
            OnPropertyChanged(nameof(TitleText));
            OnPropertyChanged(nameof(HelpText));
            OnPropertyChanged(nameof(BuildingHeaderText));
            OnPropertyChanged(nameof(WoodHeaderText));
            OnPropertyChanged(nameof(StoneHeaderText));
            OnPropertyChanged(nameof(IronHeaderText));
            OnPropertyChanged(nameof(PitchHeaderText));
            OnPropertyChanged(nameof(GoldHeaderText));
            OnPropertyChanged(nameof(WoodHeaderIcon));
            OnPropertyChanged(nameof(StoneHeaderIcon));
            OnPropertyChanged(nameof(IronHeaderIcon));
            OnPropertyChanged(nameof(PitchHeaderIcon));
            OnPropertyChanged(nameof(GoldHeaderIcon));
            RefreshCostEntryToolTips();
        }

        private void RefreshCostEntryToolTips()
        {
            foreach (CostEntryViewModel entry in CostEntries)
            {
                entry.WoodToolTip = FormatCellToolTip(entry.DisplayName, WoodHeaderText);
                entry.StoneToolTip = FormatCellToolTip(entry.DisplayName, StoneHeaderText);
                entry.IronToolTip = FormatCellToolTip(entry.DisplayName, IronHeaderText);
                entry.PitchToolTip = FormatCellToolTip(entry.DisplayName, PitchHeaderText);
                entry.GoldToolTip = FormatCellToolTip(entry.DisplayName, GoldHeaderText);
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

        public void SetVanillaCostToolTips(Dictionary<eMappers, BuildingCostValues> vanillaCosts)
        {
            foreach (CostEntryViewModel entry in CostEntries)
            {
                if (!Enum.TryParse(entry.Key, true, out eMappers mapper) ||
                    vanillaCosts == null ||
                    !vanillaCosts.TryGetValue(mapper, out BuildingCostValues values))
                {
                    entry.BuildingToolTip = entry.Key;
                    continue;
                }

                entry.BuildingToolTip = FormatBuildingToolTip(entry.Key, values);
            }
        }

        private string FormatBuildingToolTip(string key, BuildingCostValues values)
        {
            List<string> parts = new List<string>(5);
            AddVanillaCostPart(parts, WoodHeaderText, values.Wood);
            AddVanillaCostPart(parts, StoneHeaderText, values.Stone);
            AddVanillaCostPart(parts, IronHeaderText, values.Iron);
            AddVanillaCostPart(parts, PitchHeaderText, values.Pitch);
            AddVanillaCostPart(parts, GoldHeaderText, values.Gold);

            string vanillaText = parts.Count == 0
                ? "Vanilla: keine Kosten"
                : "Vanilla: " + string.Join(", ", parts);

            return key + Environment.NewLine + vanillaText;
        }

        private static void AddVanillaCostPart(List<string> parts, string label, int amount)
        {
            if (amount == 0)
                return;

            parts.Add(label + " " + amount);
        }

        private static string GetLocalizedGoodName(eGoods good, string fallback)
        {
            int index = (int)good;
            string translationKey = GetTranslationKey(GoodsTextSection, index);

            if (TryGetGameTextDictionaryValue(translationKey, out string localizedName))
                return localizedName;

            if (TryGetLocalizedGameTextExOnly(GoodsTextSection, index, out localizedName))
                return localizedName;

            return fallback;
        }

        private static bool TryGetGameTextDictionaryValue(string translationKey, out string localizedName)
        {
            localizedName = null;
            if (string.IsNullOrEmpty(translationKey))
                return false;

            if (CrusaderDE.Translate.Instance?.GameTexts != null &&
                CrusaderDE.Translate.Instance.GameTexts.TryGetValue(translationKey, out localizedName) &&
                !string.IsNullOrWhiteSpace(localizedName))
            {
                return true;
            }

            localizedName = null;
            return false;
        }

        private static bool TryGetLocalizedGameTextExOnly(string sectionName, int index, out string localizedName)
        {
            localizedName = null;
            if (string.IsNullOrEmpty(sectionName) || index < 0)
                return false;

            try
            {
                localizedName = GameTranslateAPI.Instance.GetLookUpTextEx(sectionName, index);
                if (!string.IsNullOrWhiteSpace(localizedName) &&
                    !string.Equals(localizedName, GetTranslationKey(sectionName, index), StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }

            localizedName = null;
            return false;
        }

        private static bool TryGetLocalizedGameText(string sectionName, int index, out string localizedName)
        {
            localizedName = null;
            if (string.IsNullOrEmpty(sectionName) || index < 0)
                return false;

            try
            {
                localizedName = GameTranslateAPI.Instance.GetLookUpTextEx(sectionName, index);
                if (!string.IsNullOrWhiteSpace(localizedName))
                    return true;
            }
            catch (Exception)
            {
            }

            return TryGetLocalizedGameTextKey(GetTranslationKey(sectionName, index), out localizedName);
        }

        private static bool TryGetLocalizedGameTextKey(string translationKey, out string localizedName)
        {
            localizedName = null;
            if (string.IsNullOrEmpty(translationKey))
                return false;

            try
            {
                localizedName = GameTranslateAPI.Instance.GetLookUpText(translationKey);
                if (!string.IsNullOrWhiteSpace(localizedName) &&
                    !string.Equals(localizedName, translationKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }

            if (CrusaderDE.Translate.Instance?.GameTexts != null &&
                CrusaderDE.Translate.Instance.GameTexts.TryGetValue(translationKey, out localizedName) &&
                !string.IsNullOrWhiteSpace(localizedName))
            {
                return true;
            }

            localizedName = null;
            return false;
        }

        private static string GetTranslationKey(string sectionName, int index)
        {
            return sectionName + "_" + (index + 1).ToString("D3");
        }

        private static bool IsGermanLanguage()
        {
            string language = GameAssetManagerAPI.Instance.CurrentLanguage;
            return !string.IsNullOrWhiteSpace(language) &&
                language.Replace('_', '-').StartsWith("de", StringComparison.OrdinalIgnoreCase);
        }

        public Dictionary<eMappers, BuildingCostValues> ParseBuildingCosts()
        {
            Dictionary<eMappers, BuildingCostValues> result = new Dictionary<eMappers, BuildingCostValues>();
            Dictionary<string, BuildingCostValues> parsed = ParseSerializedCosts(buildingCosts);
            foreach (KeyValuePair<string, BuildingCostValues> entry in parsed)
            {
                if (Enum.TryParse(entry.Key, true, out eMappers mapper))
                    result[mapper] = entry.Value;
            }

            return result;
        }

        private static string CreateDefaultBuildingCosts()
        {
            StringBuilder builder = new StringBuilder("# -1 = unchanged; order is wood,stone,iron,pitch,gold");
            foreach (string key in DefaultCostKeys)
            {
                builder.AppendLine();
                builder.Append(key);
                builder.Append("=-1,-1,-1,-1,-1");
            }

            return builder.ToString();
        }

        private IReadOnlyList<CostEntryViewModel> CreateCostEntriesWithCallback(string serializedCosts)
        {
            Dictionary<string, BuildingCostValues> values = ParseSerializedCosts(serializedCosts);
            List<CostEntryViewModel> entries = new List<CostEntryViewModel>();
            foreach (string key in DefaultCostKeys)
            {
                if (!values.TryGetValue(key, out BuildingCostValues value))
                    value = new BuildingCostValues(-1, -1, -1, -1, -1);

                entries.Add(new CostEntryViewModel(key, FormatDisplayName(key), null, value, OnEntryChanged));
            }

            return entries;
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

        private static ImageSource GetBuildingIconImage(string key)
        {
            if (!Enum.TryParse(key, out eMappers mapper))
                return null;

            Dictionary<eMappers, BuildingCostDefinition> definitions = BuildingCostsRuntime.CreateBuildingCostDefinitions();
            if (!definitions.TryGetValue(mapper, out BuildingCostDefinition definition) || definition.Structures.Length == 0)
                return null;

            return GetResourceImage(GetBuildingResourceKey(definition.Structures[0]));
        }

        private static string GetBuildingResourceKey(eStructs structure)
        {
            switch (structure)
            {
                case eStructs.STRUCT_WOODCUTTERS_HUT: return "UI-Buildings D003";
                case eStructs.STRUCT_HUNTERS_HUT: return "UI-Buildings E001";
                case eStructs.STRUCT_OXEN_BASE: return "UI-Buildings D007";
                case eStructs.STRUCT_QUARRY: return "UI-Buildings D005";
                case eStructs.STRUCT_IRON_MINE: return "UI-Buildings D009";
                case eStructs.STRUCT_PITCH_DIGGER: return "UI-Buildings D011";
                case eStructs.STRUCT_WHEATFARM: return "UI-Buildings E007";
                case eStructs.STRUCT_HOPSFARM: return "UI-Buildings E009";
                case eStructs.STRUCT_APPLEFARM: return "UI-Buildings E005";
                case eStructs.STRUCT_CATTLEFARM: return "UI-Buildings E003";
                case eStructs.STRUCT_MILL: return "UI-Buildings J005";
                case eStructs.STRUCT_BAKERS_WORKSHOP: return "UI-Buildings J003";
                case eStructs.STRUCT_BREWERS_WORKSHOP: return "UI-Buildings J007";
                case eStructs.STRUCT_HOVEL: return "UI-Buildings F001";
                case eStructs.STRUCT_GRANARY: return "UI-Buildings J001";
                case eStructs.STRUCT_GOODS_YARD: return "UI-Buildings D001";
                case eStructs.STRUCT_ARMOURY: return "UI-Buildings C013";
                case eStructs.STRUCT_TRADEPOST: return "UI-Buildings D013";
                case eStructs.STRUCT_INN: return "UI-Buildings J009";
                case eStructs.STRUCT_HEALER: return "UI-Buildings F009";
                case eStructs.STRUCT_FLETCHERS_WORKSHOP: return "UI-Buildings I001";
                case eStructs.STRUCT_POLETURNERS_WORKSHOP: return "UI-Buildings I003";
                case eStructs.STRUCT_BLACKSMITHS_WORKSHOP: return "UI-Buildings I005";
                case eStructs.STRUCT_ARMOURERS_WORKSHOP: return "UI-Buildings I009";
                case eStructs.STRUCT_TANNERS_WORKSHOP: return "UI-Buildings I007";
                case eStructs.STRUCT_STABLES: return "UI-Buildings M007";
                case eStructs.STRUCT_BARRACKS_WOOD: return "UI-Buildings C011";
                case eStructs.STRUCT_BARRACKS_STONE: return "UI-Buildings C009";
                case eStructs.STRUCT_ENGINEERS_GUILD: return "UI-Buildings M001";
                case eStructs.STRUCT_TUNNELLERS_GUILD: return "UI-Buildings M009";
                case eStructs.STRUCT_OIL_SMELTER: return "UI-Buildings M011";
                case eStructs.STRUCT_WELL: return "UI-Buildings F011";
                case eStructs.STRUCT_WATERPOT: return "UI-Buildings F013";
                case eStructs.STRUCT_CHURCH1: return "UI-Buildings F003";
                case eStructs.STRUCT_CHURCH2: return "UI-Buildings F005";
                case eStructs.STRUCT_CHURCH3: return "UI-Buildings F007";
                case eStructs.STRUCT_TOWER1: return "UI-Buildings K001";
                case eStructs.STRUCT_TOWER2: return "UI-Buildings K003";
                case eStructs.STRUCT_TOWER3: return "UI-Buildings K005";
                case eStructs.STRUCT_TOWER4: return "UI-Buildings K007";
                case eStructs.STRUCT_TOWER5: return "UI-Buildings K009";
                case eStructs.STRUCT_GATE_MAIN:
                case eStructs.STRUCT_GATE_STONE2A:
                case eStructs.STRUCT_GATE_STONE2B: return "UI-Buildings L005";
                case eStructs.STRUCT_GATE_INNER:
                case eStructs.STRUCT_GATE_STONE1A:
                case eStructs.STRUCT_GATE_STONE1B: return "UI-Buildings L003";
                case eStructs.STRUCT_GATE_WOOD:
                case eStructs.STRUCT_GATE_WOOD1A:
                case eStructs.STRUCT_GATE_WOOD1B:
                case eStructs.STRUCT_GATE_WOOD1C:
                case eStructs.STRUCT_GATE_WOOD1D: return "UI-Buildings L001";
                case eStructs.STRUCT_DRAWBRIDGE: return "UI-Buildings L007";
                case eStructs.STRUCT_KILLING_PIT: return "UI-Buildings L013";
                case eStructs.STRUCT_BRAZIER: return "UI-Buildings L015";
                case eStructs.STRUCT_MANGONEL: return "UI-Buildings M003";
                case eStructs.STRUCT_BALLISTA: return "UI-Buildings M005";
                case eStructs.STRUCT_MAYPOLE: return "UI-Buildings H001";
                case eStructs.STRUCT_GALLOWS: return "UI-Buildings G001";
                case eStructs.STRUCT_STOCKS: return "UI-Buildings G005";
                case eStructs.STRUCT_GARDEN: return "UI-Buildings H005";
                case eStructs.STRUCT_CESS_PIT: return "UI-Buildings G003";
                case eStructs.STRUCT_BURNING_STAKE: return "UI-Buildings G009";
                case eStructs.STRUCT_GIBBET: return "UI-Buildings G015";
                case eStructs.STRUCT_DUNGEON: return "UI-Buildings G011";
                case eStructs.STRUCT_RACK_STRETCHING: return "UI-Buildings G013";
                case eStructs.STRUCT_CHOPPING_BLOCK: return "UI-Buildings G017";
                case eStructs.STRUCT_DUNKING_STOOL: return "UI-Buildings G019";
                case eStructs.STRUCT_DOG_CAGE: return "UI-Buildings L009";
                case eStructs.STRUCT_STATUE: return "UI-Buildings H007";
                case eStructs.STRUCT_SHRINE: return "UI-Buildings H009";
                case eStructs.STRUCT_DANCING_BEAR: return "UI-Buildings H003";
                case eStructs.STRUCT_POND: return "UI-Buildings H011";
                case eStructs.STRUCT_OUTPOST_BEDOUIN: return "UI-Buildings N075";
                case eStructs.STRUCT_BEDOUIN_STOCKADE: return "UI-Buildings C015";
                default: return null;
            }
        }

        private static ImageSource GetResourceImage(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            try
            {
                return GUI.GetApplicationResources()?[key] as ImageSource;
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<string, BuildingCostValues> ParseSerializedCosts(string text)
        {
            Dictionary<string, BuildingCostValues> result = new Dictionary<string, BuildingCostValues>(StringComparer.OrdinalIgnoreCase);
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

                if (!int.TryParse(costParts[0].Trim(), out int wood) ||
                    !int.TryParse(costParts[1].Trim(), out int stone) ||
                    !int.TryParse(costParts[2].Trim(), out int iron) ||
                    !int.TryParse(costParts[3].Trim(), out int pitch) ||
                    !int.TryParse(costParts[4].Trim(), out int gold))
                {
                    continue;
                }

                result[keyValue[0].Trim()] = new BuildingCostValues(wood, stone, iron, pitch, gold);
            }

            return result;
        }

        private void ApplySerializedCostsToEntries(string text)
        {
            if (updatingEntries)
                return;

            Dictionary<string, BuildingCostValues> values = ParseSerializedCosts(text);
            updatingEntries = true;
            try
            {
                foreach (CostEntryViewModel entry in CostEntries)
                {
                    if (values.TryGetValue(entry.Key, out BuildingCostValues value))
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

            buildingCosts = BuildSerializedCosts();
            SettingChanged?.Invoke(nameof(BuildingCosts));
            OnPropertyChanged(nameof(BuildingCosts));
        }

        private string BuildSerializedCosts()
        {
            StringBuilder builder = new StringBuilder("# -1 = unchanged; order is wood,stone,iron,pitch,gold");
            foreach (CostEntryViewModel entry in CostEntries)
            {
                builder.AppendLine();
                builder.Append(entry.Key);
                builder.Append('=');
                builder.Append(entry.Wood);
                builder.Append(',');
                builder.Append(entry.Stone);
                builder.Append(',');
                builder.Append(entry.Iron);
                builder.Append(',');
                builder.Append(entry.Pitch);
                builder.Append(',');
                builder.Append(entry.Gold);
            }

            return builder.ToString();
        }

        private static string FormatDisplayName(string key)
        {
            const string prefix = "MAPPER_";
            string name = key.StartsWith(prefix, StringComparison.Ordinal) ? key.Substring(prefix.Length) : key;
            return name.Replace('_', ' ').ToLowerInvariant();
        }

        public sealed class CostEntryViewModel : INotifyPropertyChanged
        {
            private readonly Action changed;
            private string displayName;
            private string buildingToolTip;
            private string woodToolTip;
            private string stoneToolTip;
            private string ironToolTip;
            private string pitchToolTip;
            private string goldToolTip;
            private int wood;
            private int stone;
            private int iron;
            private int pitch;
            private int gold;

            public event PropertyChangedEventHandler PropertyChanged;

            public CostEntryViewModel(string key, string displayName, ImageSource iconImage, BuildingCostValues values, Action changed = null)
            {
                Key = key;
                this.displayName = displayName;
                buildingToolTip = key;
                this.changed = changed;
                wood = values.Wood;
                stone = values.Stone;
                iron = values.Iron;
                pitch = values.Pitch;
                gold = values.Gold;
            }

            public string Key { get; }
            public ImageSource IconImage => GetBuildingIconImage(Key);

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

            public string BuildingToolTip
            {
                get => buildingToolTip;
                set => SetToolTip(ref buildingToolTip, value);
            }

            public string WoodToolTip
            {
                get => woodToolTip;
                set => SetToolTip(ref woodToolTip, value);
            }

            public string StoneToolTip
            {
                get => stoneToolTip;
                set => SetToolTip(ref stoneToolTip, value);
            }

            public string IronToolTip
            {
                get => ironToolTip;
                set => SetToolTip(ref ironToolTip, value);
            }

            public string PitchToolTip
            {
                get => pitchToolTip;
                set => SetToolTip(ref pitchToolTip, value);
            }

            public string GoldToolTip
            {
                get => goldToolTip;
                set => SetToolTip(ref goldToolTip, value);
            }

            public int Wood
            {
                get => wood;
                private set => SetCost(ref wood, value, nameof(WoodText));
            }

            public int Stone
            {
                get => stone;
                private set => SetCost(ref stone, value, nameof(StoneText));
            }

            public int Iron
            {
                get => iron;
                private set => SetCost(ref iron, value, nameof(IronText));
            }

            public int Pitch
            {
                get => pitch;
                private set => SetCost(ref pitch, value, nameof(PitchText));
            }

            public int Gold
            {
                get => gold;
                private set => SetCost(ref gold, value, nameof(GoldText));
            }

            public string WoodText
            {
                get => wood.ToString();
                set => SetTextCost(value, v => Wood = v);
            }

            public string StoneText
            {
                get => stone.ToString();
                set => SetTextCost(value, v => Stone = v);
            }

            public string IronText
            {
                get => iron.ToString();
                set => SetTextCost(value, v => Iron = v);
            }

            public string PitchText
            {
                get => pitch.ToString();
                set => SetTextCost(value, v => Pitch = v);
            }

            public string GoldText
            {
                get => gold.ToString();
                set => SetTextCost(value, v => Gold = v);
            }

            public void SetCostsFromOwner(BuildingCostValues values)
            {
                Wood = values.Wood;
                Stone = values.Stone;
                Iron = values.Iron;
                Pitch = values.Pitch;
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
                int clamped = BuildingCostValues.ClampCost(value);
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
    }
}
