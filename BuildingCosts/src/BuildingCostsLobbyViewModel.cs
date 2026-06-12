using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.ViewModels;
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

        private string buildingCosts;
        private bool updatingEntries;

        public BuildingCostsLobbyViewModel()
        {
            buildingCosts = BuildDefaultBuildingCosts();
            CostEntries = CreateCostEntriesWithCallback(buildingCosts);
        }

        public IReadOnlyList<BuildingCostEntryViewModel> CostEntries { get; }

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

        public void RefreshLocalizedNames()
        {
            foreach (BuildingCostEntryViewModel entry in CostEntries)
            {
                if (Enum.TryParse(entry.Key, out eMappers mapper) &&
                    BuildingCostDefinitions.TryGet(mapper, out BuildingCostDefinition definition))
                {
                    entry.DisplayName = BuildingCostDefinitions.GetLocalizedBuildingName(definition);
                }
            }
        }

        internal static string BuildDefaultBuildingCosts()
        {
            StringBuilder builder = new StringBuilder("# -1 = unchanged; values 0..1000 set the configured construction cost");
            foreach (BuildingCostDefinition definition in BuildingCostDefinitions.All)
            {
                builder.AppendLine();
                builder.Append(definition.Mapper);
                builder.Append("=-1,-1,-1,-1,-1,-1");
            }

            return builder.ToString();
        }

        internal static Dictionary<string, BuildingCostValues> ParseSerializedCosts(string text)
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

                string[] parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                string[] values = parts[1].Split(',');
                if (values.Length != 5 && values.Length != 6)
                    continue;

                if (!TryParseCost(values[0], out int wood) ||
                    !TryParseCost(values[1], out int stone) ||
                    !TryParseCost(values[2], out int iron) ||
                    !TryParseCost(values[3], out int pitch) ||
                    !TryParseCost(values[4], out int gold))
                    continue;

                int ale = -1;
                if (values.Length == 6 && !TryParseCost(values[5], out ale))
                    continue;

                result[parts[0].Trim()] = new BuildingCostValues(wood, stone, iron, pitch, gold, ale);
            }

            return result;
        }

        private static bool TryParseCost(string text, out int value)
        {
            if (!int.TryParse(text.Trim(), out value))
            {
                value = -1;
                return false;
            }

            value = ClampCost(value);
            return true;
        }

        private void ApplySerializedCostsToEntries(string text)
        {
            if (updatingEntries)
                return;

            Dictionary<string, BuildingCostValues> values = ParseSerializedCosts(text);
            updatingEntries = true;
            try
            {
                foreach (BuildingCostEntryViewModel entry in CostEntries)
                {
                    if (values.TryGetValue(entry.Key, out BuildingCostValues costValues))
                        entry.SetCostsFromOwner(costValues);
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
            StringBuilder builder = new StringBuilder("# -1 = unchanged; values 0..1000 set the configured construction cost");
            foreach (BuildingCostEntryViewModel entry in CostEntries)
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
                builder.Append(',');
                builder.Append(entry.Ale);
            }

            return builder.ToString();
        }

        private static IReadOnlyList<BuildingCostEntryViewModel> CreateCostEntries(string serializedCosts)
        {
            Dictionary<string, BuildingCostValues> values = ParseSerializedCosts(serializedCosts);
            List<BuildingCostEntryViewModel> entries = new List<BuildingCostEntryViewModel>();
            foreach (BuildingCostDefinition definition in BuildingCostDefinitions.All)
            {
                string key = definition.Mapper.ToString();
                if (!values.TryGetValue(key, out BuildingCostValues costValues))
                    costValues = BuildingCostValues.Unchanged;

                entries.Add(new BuildingCostEntryViewModel(key, definition.DisplayName, costValues));
            }

            return entries;
        }

        private IReadOnlyList<BuildingCostEntryViewModel> CreateCostEntriesWithCallback(string serializedCosts)
        {
            List<BuildingCostEntryViewModel> entries = new List<BuildingCostEntryViewModel>();
            foreach (BuildingCostEntryViewModel entry in CreateCostEntries(serializedCosts))
                entries.Add(new BuildingCostEntryViewModel(entry.Key, entry.DisplayName, entry.Values, OnEntryChanged));

            return entries;
        }

        internal static int ClampCost(int value)
        {
            if (value < -1)
                return -1;
            if (value > 1000)
                return 1000;
            return value;
        }

        public readonly struct BuildingCostValues
        {
            public static readonly BuildingCostValues Unchanged = new BuildingCostValues(-1, -1, -1, -1, -1, -1);

            public BuildingCostValues(int wood, int stone, int iron, int pitch, int gold, int ale)
            {
                Wood = ClampCost(wood);
                Stone = ClampCost(stone);
                Iron = ClampCost(iron);
                Pitch = ClampCost(pitch);
                Gold = ClampCost(gold);
                Ale = ClampCost(ale);
            }

            public int Wood { get; }
            public int Stone { get; }
            public int Iron { get; }
            public int Pitch { get; }
            public int Gold { get; }
            public int Ale { get; }
        }

        public sealed class BuildingCostEntryViewModel : INotifyPropertyChanged
        {
            private readonly Action changed;
            private string displayName;
            private int wood;
            private int stone;
            private int iron;
            private int pitch;
            private int gold;
            private int ale;

            public event PropertyChangedEventHandler PropertyChanged;

            public BuildingCostEntryViewModel(string key, string displayName, BuildingCostValues values, Action changed = null)
            {
                Key = key;
                this.displayName = displayName;
                this.changed = changed;
                wood = values.Wood;
                stone = values.Stone;
                iron = values.Iron;
                pitch = values.Pitch;
                gold = values.Gold;
                ale = values.Ale;
            }

            public string Key { get; }

            internal BuildingCostValues Values => new BuildingCostValues(Wood, Stone, Iron, Pitch, Gold, Ale);

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

            public int Ale
            {
                get => ale;
                private set => SetCost(ref ale, value, nameof(AleText));
            }

            public string WoodText
            {
                get => Wood.ToString();
                set => SetCostText(value, cost => Wood = cost);
            }

            public string StoneText
            {
                get => Stone.ToString();
                set => SetCostText(value, cost => Stone = cost);
            }

            public string IronText
            {
                get => Iron.ToString();
                set => SetCostText(value, cost => Iron = cost);
            }

            public string PitchText
            {
                get => Pitch.ToString();
                set => SetCostText(value, cost => Pitch = cost);
            }

            public string GoldText
            {
                get => Gold.ToString();
                set => SetCostText(value, cost => Gold = cost);
            }

            public string AleText
            {
                get => Ale.ToString();
                set => SetCostText(value, cost => Ale = cost);
            }

            public void SetCostsFromOwner(BuildingCostValues values)
            {
                Wood = values.Wood;
                Stone = values.Stone;
                Iron = values.Iron;
                Pitch = values.Pitch;
                Gold = values.Gold;
                Ale = values.Ale;
            }

            private void SetCost(ref int field, int value, string textPropertyName)
            {
                int clamped = ClampCost(value);
                if (field == clamped)
                    return;

                field = clamped;
                OnPropertyChanged(textPropertyName);
                changed?.Invoke();
            }

            private void SetCostText(string text, Action<int> setter)
            {
                if (!int.TryParse(text, out int parsed))
                {
                    OnPropertyChanged();
                    return;
                }

                setter(parsed);
            }

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
