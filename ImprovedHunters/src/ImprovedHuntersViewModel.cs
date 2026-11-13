using APIShared.ModSettings;
using Noesis;
using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Globalization;

namespace ImprovedHunters
{
    public sealed class ImprovedHuntersViewModel : APIShared.ModSettings.PresetLobbyModSettingsViewModel
    {
        // Vanilla's Hunter passes 6 for every prey (0x12FC70).
        private const int DefaultDeerMeat = 6;
        private const int DefaultGoatMeat = 6;
        private const int DefaultRabbitMeat = 2;
        private const int DefaultCamelMeat = 8;
        private const int DefaultChickenMeat = 1;
        private const int MaximumMeat = 50;

        private bool enableMod = true;
        private bool improvedTargetSelection = true;
        private bool improvedPathfinding = true;
        private bool rabbitsOnlyOnOrder = true;
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
        private bool tannerMeatMode = true;
        private int tannerMeatPerTrip = TannerMeatPolicy.DefaultMeatPerTrip;
        private int hunterSearchRange = HunterSearchRangePolicy.Unlimited;
        private bool hunterHutAlwaysAvailable = true;
        private bool collectFreeCorpses = true;
        private bool deerFleeOnlyInSight = true;
        private bool herdsIgnoreFleeingHerds = true;
        private int herdReturnSeconds = HerdSettingsPolicy.Off;
        private int herdRangeSize = HerdSettingsPolicy.Off;
        private int startHerdMode = HerdSettingsPolicy.StartHerdsOff;
        private int chickensPerPlayer = GranaryChickenSpawnPolicy.DefaultTargetPerPlayer;

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
        public string CollectFreeCorpsesText => SerpLocalization.Get("ImprovedHunters.CollectFreeCorpses");
        public string CollectFreeCorpsesHelpText => SerpLocalization.Get("ImprovedHunters.CollectFreeCorpsesHelp");
        public string HunterHutAlwaysAvailableText => SerpLocalization.Get("ImprovedHunters.HunterHutAlwaysAvailable");
        public string HunterHutAlwaysAvailableHelpText => SerpLocalization.Get("ImprovedHunters.HunterHutAlwaysAvailableHelp");
        public string DeerFleeOnlyInSightText => SerpLocalization.Get("ImprovedHunters.DeerFleeOnlyInSight");
        public string DeerFleeOnlyInSightHelpText => SerpLocalization.Get("ImprovedHunters.DeerFleeOnlyInSightHelp");
        public string HerdsIgnoreFleeingHerdsText => SerpLocalization.Get("ImprovedHunters.HerdsIgnoreFleeingHerds");
        public string HerdsIgnoreFleeingHerdsHelpText => SerpLocalization.Get("ImprovedHunters.HerdsIgnoreFleeingHerdsHelp");
        public string HerdReturnText => SerpLocalization.Get("ImprovedHunters.HerdReturn");
        public string HerdReturnHelpText => SerpLocalization.Get("ImprovedHunters.HerdReturnHelp");
        public string HerdReturnValueText
        {
            get => HerdReturnSeconds == HerdSettingsPolicy.Off
                ? SerpLocalization.Get("ImprovedHunters.HerdReturnOff")
                : string.Format(SerpLocalization.Get("ImprovedHunters.HerdReturnValueFormat"), HerdReturnSeconds);
            set => SetBoundedIntText(value, parsed => HerdReturnSeconds = parsed, nameof(HerdReturnValueText));
        }
        public string HerdRangeText => SerpLocalization.Get("ImprovedHunters.HerdRange");
        public string HerdRangeHelpText => SerpLocalization.Get("ImprovedHunters.HerdRangeHelp");
        public string HerdRangeValueText
        {
            get => HerdRangeSize == HerdSettingsPolicy.Off
                ? SerpLocalization.Get("ImprovedHunters.HerdRangeUnlimited")
                : string.Format(SerpLocalization.Get("ImprovedHunters.HerdRangeValueFormat"), HerdRangeSize);
            set => SetBoundedIntText(value, parsed => HerdRangeSize = parsed, nameof(HerdRangeValueText));
        }
        public string StartHerdsText => SerpLocalization.Get("ImprovedHunters.StartHerds");
        public string StartHerdsHelpText => SerpLocalization.Get("ImprovedHunters.StartHerdsHelp");
        public string[] StartHerdModeOptions => new[]
        {
            SerpLocalization.Get("ImprovedHunters.StartHerdsOff"),
            SerpLocalization.Get("ImprovedHunters.StartHerdsOnEmptyMaps"),
            SerpLocalization.Get("ImprovedHunters.StartHerdsAlways")
        };
        public string HunterSearchRangeText => SerpLocalization.Get("ImprovedHunters.HunterSearchRange");
        public string HunterSearchRangeHelpText => SerpLocalization.Get("ImprovedHunters.HunterSearchRangeHelp");
        public string HunterSearchRangeValueText
        {
            get => HunterSearchRange == HunterSearchRangePolicy.Unlimited
                ? SerpLocalization.Get("ImprovedHunters.HunterSearchRangeUnlimited")
                : string.Format(SerpLocalization.Get("ImprovedHunters.HunterSearchRangeValueFormat"), HunterSearchRange);
            set => SetBoundedIntText(value, parsed => HunterSearchRange = parsed, nameof(HunterSearchRangeValueText));
        }
        public string ImprovedPathfindingText => SerpLocalization.Get("ImprovedHunters.ImprovedPathfinding");
        public string ImprovedPathfindingHelpText => SerpLocalization.Get("ImprovedHunters.ImprovedPathfindingHelp");
        public string RabbitsOnlyOnOrderText => SerpLocalization.Get("ImprovedHunters.RabbitsOnlyOnOrder");
        public string RabbitsOnlyOnOrderHelpText => SerpLocalization.Get("ImprovedHunters.RabbitsOnlyOnOrderHelp");
        public string TannerTitleText => SerpLocalization.Get("ImprovedHunters.TannerTitle");
        public string TannerMeatModeText => SerpLocalization.Get("ImprovedHunters.TannerMeatMode");
        public string TannerMeatModeHelpText => SerpLocalization.Get("ImprovedHunters.TannerMeatModeHelp");
        public string TannerMeatPerTripText => SerpLocalization.Get("ImprovedHunters.TannerMeatPerTrip");
        public string TannerMeatPerTripHelpText => SerpLocalization.Get("ImprovedHunters.TannerMeatPerTripHelp");
        public string TannerMeatPerTripValueText
        {
            get => string.Format(
                CultureInfo.CurrentCulture,
                SerpLocalization.Get("ImprovedHunters.TannerMeatPerTripValueFormat"),
                TannerMeatPerTrip);
            set => SetBoundedIntText(value, parsed => TannerMeatPerTrip = parsed, nameof(TannerMeatPerTripValueText));
        }
        public string DeerText => SerpLocalization.Get("ImprovedHunters.Deer");
        public string GoatText => SerpLocalization.Get("ImprovedHunters.Goat");
        public string RabbitText => SerpLocalization.Get("ImprovedHunters.Rabbit");
        public string CamelText => SerpLocalization.Get("ImprovedHunters.Camel");
        public string ChickenText => SerpLocalization.Get("ImprovedHunters.Chicken");
        public string ChickenHelpText => SerpLocalization.Get("ImprovedHunters.ChickenHelp");
        public string ChickenPopulationTitleText => SerpLocalization.Get("ImprovedHunters.ChickenPopulationTitle");
        public string ChickensPerPlayerText => SerpLocalization.Get("ImprovedHunters.ChickensPerPlayer");
        public string ChickensPerPlayerHelpText => SerpLocalization.Get("ImprovedHunters.ChickensPerPlayerHelp");
        public string ChickensPerPlayerValueText
        {
            get => string.Format(
                CultureInfo.CurrentCulture,
                SerpLocalization.Get("ImprovedHunters.ChickensPerPlayerValueFormat"),
                ChickensPerPlayer);
            set => SetBoundedIntText(
                value,
                parsed => ChickensPerPlayer = parsed,
                nameof(ChickensPerPlayerValueText));
        }
        [SyncHostOnly] public bool EnableMod { get => enableMod; set => SetSetting(ref enableMod, value, nameof(EnableMod)); }
        [SyncHostOnly] public bool ImprovedTargetSelection { get => improvedTargetSelection; set => SetSetting(ref improvedTargetSelection, value, nameof(ImprovedTargetSelection)); }
        [SyncHostOnly] public bool ImprovedPathfinding { get => improvedPathfinding; set => SetSetting(ref improvedPathfinding, value, nameof(ImprovedPathfinding)); }
        [SyncHostOnly] public bool RabbitsOnlyOnOrder { get => rabbitsOnlyOnOrder; set => SetSetting(ref rabbitsOnlyOnOrder, value, nameof(RabbitsOnlyOnOrder)); }
        [SyncHostOnly] public bool TannerMeatMode { get => tannerMeatMode; set => SetSetting(ref tannerMeatMode, value, nameof(TannerMeatMode)); }
        [SyncHostOnly] public int TannerMeatPerTrip
        {
            get => tannerMeatPerTrip;
            set => SetBoundedIntSetting(
                ref tannerMeatPerTrip,
                value,
                TannerMeatPolicy.MinimumMeatPerTrip,
                TannerMeatPolicy.MaximumMeatPerTrip,
                nameof(TannerMeatPerTrip),
                nameof(TannerMeatPerTripValueText));
        }
        [SyncHostOnly] public bool CollectFreeCorpses { get => collectFreeCorpses; set => SetSetting(ref collectFreeCorpses, value, nameof(CollectFreeCorpses)); }
        [SyncHostOnly] public bool HunterHutAlwaysAvailable { get => hunterHutAlwaysAvailable; set => SetSetting(ref hunterHutAlwaysAvailable, value, nameof(HunterHutAlwaysAvailable)); }
        [SyncHostOnly] public bool DeerFleeOnlyInSight { get => deerFleeOnlyInSight; set => SetSetting(ref deerFleeOnlyInSight, value, nameof(DeerFleeOnlyInSight)); }
        [SyncHostOnly] public bool HerdsIgnoreFleeingHerds { get => herdsIgnoreFleeingHerds; set => SetSetting(ref herdsIgnoreFleeingHerds, value, nameof(HerdsIgnoreFleeingHerds)); }
        [SyncHostOnly] public int HerdReturnSeconds
        {
            get => herdReturnSeconds;
            set => SetBoundedIntSetting(ref herdReturnSeconds, HerdSettingsPolicy.ClampReturnSeconds(value), HerdSettingsPolicy.Off, HerdSettingsPolicy.MaximumReturnSeconds, nameof(HerdReturnSeconds), nameof(HerdReturnValueText));
        }
        [SyncHostOnly] public int HerdRangeSize
        {
            get => herdRangeSize;
            set => SetBoundedIntSetting(ref herdRangeSize, HerdSettingsPolicy.ClampRangeSize(value), HerdSettingsPolicy.Off, HerdSettingsPolicy.MaximumRangeSize, nameof(HerdRangeSize), nameof(HerdRangeValueText));
        }
        [SyncHostOnly] public int StartHerdMode
        {
            get => startHerdMode;
            set => SetBoundedIntSetting(ref startHerdMode, HerdSettingsPolicy.ClampStartHerdMode(value), HerdSettingsPolicy.StartHerdsOff, HerdSettingsPolicy.StartHerdsAlways, nameof(StartHerdMode), nameof(StartHerdModeOptions));
        }
        [SyncHostOnly] public int HunterSearchRange
        {
            get => hunterSearchRange;
            set => SetBoundedIntSetting(
                ref hunterSearchRange,
                HunterSearchRangePolicy.Clamp(value),
                HunterSearchRangePolicy.Unlimited,
                HunterSearchRangePolicy.MaximumRange,
                nameof(HunterSearchRange),
                nameof(HunterSearchRangeValueText));
        }
        [SyncHostOnly] public bool HuntDeer { get => huntDeer; set => SetSetting(ref huntDeer, value, nameof(HuntDeer)); }
        [SyncHostOnly] public bool HuntGoat { get => huntGoat; set => SetSetting(ref huntGoat, value, nameof(HuntGoat)); }
        [SyncHostOnly] public bool HuntRabbit { get => huntRabbit; set => SetSetting(ref huntRabbit, value, nameof(HuntRabbit)); }
        [SyncHostOnly] public bool HuntCamel { get => huntCamel; set => SetSetting(ref huntCamel, value, nameof(HuntCamel)); }
        [SyncHostOnly] public bool HuntChicken { get => huntChicken; set => SetSetting(ref huntChicken, value, nameof(HuntChicken)); }
        [SyncHostOnly] public int ChickensPerPlayer
        {
            get => chickensPerPlayer;
            set => SetBoundedIntSetting(
                ref chickensPerPlayer,
                value,
                GranaryChickenSpawnPolicy.MinimumTargetPerPlayer,
                GranaryChickenSpawnPolicy.MaximumTargetPerPlayer,
                nameof(ChickensPerPlayer),
                nameof(ChickensPerPlayerValueText));
        }

        [SyncHostOnly] public int DeerMeat { get => deerMeat; set => SetMeatSetting(ref deerMeat, value, DefaultDeerMeat, nameof(DeerMeat), nameof(DeerMeatText)); }
        [SyncHostOnly] public int GoatMeat { get => goatMeat; set => SetMeatSetting(ref goatMeat, value, DefaultGoatMeat, nameof(GoatMeat), nameof(GoatMeatText)); }
        [SyncHostOnly] public int RabbitMeat { get => rabbitMeat; set => SetMeatSetting(ref rabbitMeat, value, DefaultRabbitMeat, nameof(RabbitMeat), nameof(RabbitMeatText)); }
        [SyncHostOnly] public int CamelMeat { get => camelMeat; set => SetMeatSetting(ref camelMeat, value, DefaultCamelMeat, nameof(CamelMeat), nameof(CamelMeatText)); }
        [SyncHostOnly] public int ChickenMeat { get => chickenMeat; set => SetMeatSetting(ref chickenMeat, value, DefaultChickenMeat, nameof(ChickenMeat), nameof(ChickenMeatText)); }

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
            RabbitsOnlyOnOrder = true;
            HuntDeer = true;
            HuntGoat = true;
            HuntRabbit = true;
            HuntCamel = true;
            HuntChicken = true;
            ChickensPerPlayer = GranaryChickenSpawnPolicy.DefaultTargetPerPlayer;
            DeerMeat = DefaultDeerMeat;
            GoatMeat = DefaultGoatMeat;
            RabbitMeat = DefaultRabbitMeat;
            CamelMeat = DefaultCamelMeat;
            ChickenMeat = DefaultChickenMeat;
            TannerMeatMode = true;
            TannerMeatPerTrip = TannerMeatPolicy.DefaultMeatPerTrip;
            HunterSearchRange = HunterSearchRangePolicy.Unlimited;
            HunterHutAlwaysAvailable = true;
            CollectFreeCorpses = true;
            DeerFleeOnlyInSight = true;
            HerdsIgnoreFleeingHerds = true;
            HerdReturnSeconds = HerdSettingsPolicy.Off;
            HerdRangeSize = HerdSettingsPolicy.Off;
            StartHerdMode = HerdSettingsPolicy.StartHerdsOff;
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
            int defaultValue,
            string propertyName,
            string textPropertyName)
        {
            if (!CanMutateSettingWithDependents(propertyName, textPropertyName))
                return;

            int clamped = ClampMeat(value, defaultValue);
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
            // Shows the clamped value even when the stored value did not change.
            OnPropertyChanged(textPropertyName);
        }

        private void SetBoundedIntText(string text, Action<int> setValue, string textPropertyName)
        {
            if (!Shared.NumericTextInput.TryParseInt(text, out int parsed))
            {
                OnPropertyChanged(textPropertyName);
                return;
            }

            setValue(parsed);
            // Also restore the normalized/clamped display when the numeric value did not change.
            OnPropertyChanged(textPropertyName);
        }

        // A stored -1 from a pre-release build meant Vanilla's yield, which is the default now.
        private static int ClampMeat(int value, int defaultValue)
        {
            if (value < 0)
                return defaultValue;

            return Math.Min(value, MaximumMeat);
        }
    }
}
