using Noesis;
using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Globalization;

namespace ImprovedHunters
{
    public sealed class ImprovedHuntersViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        private const int DefaultDeerMeat = -1;
        private const int DefaultGoatMeat = -1;
        private const int VanillaDeerMeat = 6;
        private const int VanillaGoatMeat = 4;
        private const int DefaultRabbitMeat = 2;
        private const int DefaultCamelMeat = 8;
        private const int DefaultChickenMeat = 1;

        private bool enableMod = true;
        private bool improvedTargetSelection = true;
        private bool improvedPathfinding = true;
        private bool allowDeadTargets;
        private bool reliableHunterProjectiles = true;
        private bool huntDeer = true;
        private bool huntGoat = true;
        private bool huntRabbit = true;
        private bool huntCamel = true;
        private bool huntChicken = true;
        private int deerMeat = DefaultDeerMeat;
        private int goatMeat = DefaultGoatMeat;
        private int rabbitMeat = DefaultRabbitMeat;
        private int camelMeat = DefaultCamelMeat;
        private int chickenMeat = DefaultChickenMeat;
        private int maxNeutralChickensPerPlayer = GranaryChickenSpawnPolicy.DefaultMaximumPerPlayer;

        public event Action<string> SettingChanged;

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        public ImprovedHuntersViewModel()
        {
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public RelayCommand ResetToDefaultCommand { get; }

        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string EnableModText => SerpLocalization.Get("Common.EnableMod");
        public string TitleText => SerpLocalization.Get("ImprovedHunters.Title");
        public string BehaviorTitleText => SerpLocalization.Get("ImprovedHunters.BehaviorTitle");
        public string TargetsYieldTitleText => SerpLocalization.Get("ImprovedHunters.TargetsYieldTitle");
        public string HelpText => SerpLocalization.Get("ImprovedHunters.Help");
        public string HuntText => SerpLocalization.Get("ImprovedHunters.Hunt");
        public string MeatText => SerpLocalization.Get("ImprovedHunters.Meat");
        public string MeatHelpText => SerpLocalization.Get("ImprovedHunters.MeatHelp");
        public string ImprovedTargetSelectionText => SerpLocalization.Get("ImprovedHunters.ImprovedTargetSelection");
        public string ImprovedTargetSelectionHelpText => SerpLocalization.Get("ImprovedHunters.ImprovedTargetSelectionHelp");
        public string ImprovedPathfindingText => SerpLocalization.Get("ImprovedHunters.ImprovedPathfinding");
        public string ImprovedPathfindingHelpText => SerpLocalization.Get("ImprovedHunters.ImprovedPathfindingHelp");
        public string AllowDeadTargetsText => SerpLocalization.Get("ImprovedHunters.AllowDeadTargets");
        public string AllowDeadTargetsHelpText => SerpLocalization.Get("ImprovedHunters.AllowDeadTargetsHelp");
        public string ReliableHunterProjectilesText => SerpLocalization.Get("ImprovedHunters.ReliableHunterProjectiles");
        public string ReliableHunterProjectilesHelpText => SerpLocalization.Get("ImprovedHunters.ReliableHunterProjectilesHelp");
        public string DeerText => SerpLocalization.Get("ImprovedHunters.Deer");
        public string GoatText => SerpLocalization.Get("ImprovedHunters.Goat");
        public string RabbitText => SerpLocalization.Get("ImprovedHunters.Rabbit");
        public string CamelText => SerpLocalization.Get("ImprovedHunters.Camel");
        public string ChickenText => SerpLocalization.Get("ImprovedHunters.Chicken");
        public string ChickenHelpText => SerpLocalization.Get("ImprovedHunters.ChickenHelp");
        public string ChickenPopulationTitleText => SerpLocalization.Get("ImprovedHunters.ChickenPopulationTitle");
        public string MaxNeutralChickensPerPlayerText => SerpLocalization.Get("ImprovedHunters.MaxNeutralChickensPerPlayer");
        public string MaxNeutralChickensPerPlayerHelpText => SerpLocalization.Get("ImprovedHunters.MaxNeutralChickensPerPlayerHelp");
        public string MaxNeutralChickensPerPlayerValueText => string.Format(
            CultureInfo.CurrentCulture,
            SerpLocalization.Get("ImprovedHunters.MaxNeutralChickensPerPlayerValueFormat"),
            MaxNeutralChickensPerPlayer);
        [SyncHostOnly] public bool EnableMod { get => enableMod; set => SetSetting(ref enableMod, value, nameof(EnableMod)); }
        [SyncHostOnly] public bool ImprovedTargetSelection { get => improvedTargetSelection; set => SetSetting(ref improvedTargetSelection, value, nameof(ImprovedTargetSelection)); }
        [SyncHostOnly] public bool ImprovedPathfinding { get => improvedPathfinding; set => SetSetting(ref improvedPathfinding, value, nameof(ImprovedPathfinding)); }
        [SyncHostOnly] public bool AllowDeadTargets { get => allowDeadTargets; set => SetSetting(ref allowDeadTargets, value, nameof(AllowDeadTargets)); }
        [SyncHostOnly] public bool ReliableHunterProjectiles { get => reliableHunterProjectiles; set => SetSetting(ref reliableHunterProjectiles, value, nameof(ReliableHunterProjectiles)); }
        [SyncHostOnly] public bool HuntDeer { get => huntDeer; set => SetSetting(ref huntDeer, value, nameof(HuntDeer)); }
        [SyncHostOnly] public bool HuntGoat { get => huntGoat; set => SetSetting(ref huntGoat, value, nameof(HuntGoat)); }
        [SyncHostOnly] public bool HuntRabbit { get => huntRabbit; set => SetSetting(ref huntRabbit, value, nameof(HuntRabbit)); }
        [SyncHostOnly] public bool HuntCamel { get => huntCamel; set => SetSetting(ref huntCamel, value, nameof(HuntCamel)); }
        [SyncHostOnly] public bool HuntChicken { get => huntChicken; set => SetSetting(ref huntChicken, value, nameof(HuntChicken)); }
        [SyncHostOnly] public int MaxNeutralChickensPerPlayer
        {
            get => maxNeutralChickensPerPlayer;
            set => SetBoundedIntSetting(
                ref maxNeutralChickensPerPlayer,
                value,
                GranaryChickenSpawnPolicy.MinimumMaximumPerPlayer,
                GranaryChickenSpawnPolicy.MaximumMaximumPerPlayer,
                nameof(MaxNeutralChickensPerPlayer),
                nameof(MaxNeutralChickensPerPlayerValueText));
        }

        [SyncHostOnly] public int DeerMeat { get => deerMeat; set => SetMeatSetting(ref deerMeat, value, true, nameof(DeerMeat), nameof(DeerMeatText)); }
        [SyncHostOnly] public int GoatMeat { get => goatMeat; set => SetMeatSetting(ref goatMeat, value, true, nameof(GoatMeat), nameof(GoatMeatText)); }
        [SyncHostOnly] public int RabbitMeat { get => rabbitMeat; set => SetMeatSetting(ref rabbitMeat, value, false, nameof(RabbitMeat), nameof(RabbitMeatText)); }
        [SyncHostOnly] public int CamelMeat { get => camelMeat; set => SetMeatSetting(ref camelMeat, value, false, nameof(CamelMeat), nameof(CamelMeatText)); }
        [SyncHostOnly] public int ChickenMeat { get => chickenMeat; set => SetMeatSetting(ref chickenMeat, value, false, nameof(ChickenMeat), nameof(ChickenMeatText)); }

        public string DeerMeatText { get => DeerMeat.ToString(); set => SetMeatText(value, parsed => DeerMeat = parsed, nameof(DeerMeatText)); }
        public string GoatMeatText { get => GoatMeat.ToString(); set => SetMeatText(value, parsed => GoatMeat = parsed, nameof(GoatMeatText)); }
        public string RabbitMeatText { get => RabbitMeat.ToString(); set => SetMeatText(value, parsed => RabbitMeat = parsed, nameof(RabbitMeatText)); }
        public string CamelMeatText { get => CamelMeat.ToString(); set => SetMeatText(value, parsed => CamelMeat = parsed, nameof(CamelMeatText)); }
        public string ChickenMeatText { get => ChickenMeat.ToString(); set => SetMeatText(value, parsed => ChickenMeat = parsed, nameof(ChickenMeatText)); }

        public bool IsKnownAnimal(eChimps type)
        {
            return type == eChimps.CHIMP_TYPE_DEER ||
                type == eChimps.CHIMP_TYPE_GOAT ||
                type == eChimps.CHIMP_TYPE_RABBIT ||
                type == eChimps.CHIMP_TYPE_CAMEL ||
                type == eChimps.CHIMP_TYPE_CHICKEN;
        }

        public bool IsHuntingEnabled(eChimps type)
        {
            if (!EnableMod)
                return false;

            switch (type)
            {
                case eChimps.CHIMP_TYPE_DEER:
                    return HuntDeer;
                case eChimps.CHIMP_TYPE_GOAT:
                    return HuntGoat;
                case eChimps.CHIMP_TYPE_RABBIT:
                    return HuntRabbit;
                case eChimps.CHIMP_TYPE_CAMEL:
                    return HuntCamel;
                case eChimps.CHIMP_TYPE_CHICKEN:
                    return HuntChicken;
                default:
                    return false;
            }
        }

        public int GetMeatAmount(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_DEER:
                    return DeerMeat;
                case eChimps.CHIMP_TYPE_GOAT:
                    return GoatMeat;
                case eChimps.CHIMP_TYPE_RABBIT:
                    return RabbitMeat;
                case eChimps.CHIMP_TYPE_CAMEL:
                    return CamelMeat;
                case eChimps.CHIMP_TYPE_CHICKEN:
                    return ChickenMeat;
                default:
                    return DefaultDeerMeat;
            }
        }

        public int GetExpectedMeatAmount(eChimps type)
        {
            int configured = GetMeatAmount(type);
            if (configured >= 0)
                return configured;

            // Only Vanilla Hunter prey may use -1. Keep target scoring stable
            // while the actual yield event remains entirely Vanilla-owned.
            switch (type)
            {
                case eChimps.CHIMP_TYPE_DEER:
                    return VanillaDeerMeat;
                case eChimps.CHIMP_TYPE_GOAT:
                    return VanillaGoatMeat;
                default:
                    return 0;
            }
        }

        private void ResetToDefault()
        {
            if (!CanEditHostSettings)
                return;

            EnableMod = true;
            ImprovedTargetSelection = true;
            ImprovedPathfinding = true;
            AllowDeadTargets = false;
            ReliableHunterProjectiles = true;
            HuntDeer = true;
            HuntGoat = true;
            HuntRabbit = true;
            HuntCamel = true;
            HuntChicken = true;
            MaxNeutralChickensPerPlayer = GranaryChickenSpawnPolicy.DefaultMaximumPerPlayer;
            DeerMeat = DefaultDeerMeat;
            GoatMeat = DefaultGoatMeat;
            RabbitMeat = DefaultRabbitMeat;
            CamelMeat = DefaultCamelMeat;
            ChickenMeat = DefaultChickenMeat;
        }

        private void SetSetting<T>(ref T field, T value, string propertyName)
        {
            if (!CanMutateSetting(propertyName))
                return;

            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private void SetMeatSetting(
            ref int field,
            int value,
            bool allowVanilla,
            string propertyName,
            string textPropertyName)
        {
            if (!CanMutateSettingWithDependents(propertyName, textPropertyName))
                return;

            int clamped = ClampMeat(value, allowVanilla);
            if (field == clamped)
                return;

            field = clamped;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(textPropertyName);
        }

        private void SetBoundedIntSetting(
            ref int field,
            int value,
            int minimum,
            int maximum,
            string propertyName,
            string dependentPropertyName)
        {
            if (!CanMutateSettingWithDependents(propertyName, dependentPropertyName))
                return;

            int clamped = Math.Max(minimum, Math.Min(maximum, value));
            if (field == clamped)
                return;

            field = clamped;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(dependentPropertyName);
        }

        private void SetMeatText(string text, Action<int> setValue, string textPropertyName)
        {
            if (!int.TryParse(text, out int parsed))
            {
                OnPropertyChanged(textPropertyName);
                return;
            }

            setValue(parsed);
        }

        private static int ClampMeat(int value, bool allowVanilla)
        {
            int minimum = allowVanilla ? -1 : 0;
            if (value < minimum)
                return minimum;

            if (value > 100)
                return 100;

            return value;
        }
    }
}
