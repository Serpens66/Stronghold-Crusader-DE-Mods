using Noesis;
using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;

namespace ImprovedHunters
{
    public sealed class ImprovedHuntersViewModel : LobbyModSettingsBaseViewModel
    {
        private const int DefaultDeerMeat = 6;
        private const int DefaultGoatMeat = 4;
        private const int DefaultRabbitMeat = 2;
        private const int DefaultCamelMeat = 8;
        private const int DefaultChickenMeat = 1;
        private const int DefaultCowMeat = 6;

        private bool enableMod = true;
        private bool improvedPathfinding = true;
        private bool huntDeer = true;
        private bool huntGoat = true;
        private bool huntRabbit = true;
        private bool huntCamel = true;
        private bool huntChicken = true;
        private bool huntCow = false;
        private int deerMeat = DefaultDeerMeat;
        private int goatMeat = DefaultGoatMeat;
        private int rabbitMeat = DefaultRabbitMeat;
        private int camelMeat = DefaultCamelMeat;
        private int chickenMeat = DefaultChickenMeat;
        private int cowMeat = DefaultCowMeat;

        public event Action<string> SettingChanged;

        public ImprovedHuntersViewModel()
        {
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public RelayCommand ResetToDefaultCommand { get; }

        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string EnableModText => SerpLocalization.Get("Common.EnableMod");
        public string TitleText => SerpLocalization.Get("ImprovedHunters.Title");
        public string HelpText => SerpLocalization.Get("ImprovedHunters.Help");
        public string HuntText => SerpLocalization.Get("ImprovedHunters.Hunt");
        public string MeatText => SerpLocalization.Get("ImprovedHunters.Meat");
        public string MeatHelpText => SerpLocalization.Get("ImprovedHunters.MeatHelp");
        public string ImprovedPathfindingText => SerpLocalization.Get("ImprovedHunters.ImprovedPathfinding");
        public string ImprovedPathfindingHelpText => SerpLocalization.Get("ImprovedHunters.ImprovedPathfindingHelp");
        public string DeerText => SerpLocalization.Get("ImprovedHunters.Deer");
        public string GoatText => SerpLocalization.Get("ImprovedHunters.Goat");
        public string RabbitText => SerpLocalization.Get("ImprovedHunters.Rabbit");
        public string CamelText => SerpLocalization.Get("ImprovedHunters.Camel");
        public string ChickenText => SerpLocalization.Get("ImprovedHunters.Chicken");
        public string ChickenHelpText => SerpLocalization.Get("ImprovedHunters.ChickenHelp");
        public string CowText => SerpLocalization.Get("ImprovedHunters.Cow");
        public string CowHelpText => SerpLocalization.Get("ImprovedHunters.CowHelp");

        [SyncHostOnly] public bool EnableMod { get => enableMod; set => SetSetting(ref enableMod, value, nameof(EnableMod)); }
        [SyncHostOnly] public bool ImprovedPathfinding { get => improvedPathfinding; set => SetSetting(ref improvedPathfinding, value, nameof(ImprovedPathfinding)); }
        [SyncHostOnly] public bool HuntDeer { get => huntDeer; set => SetSetting(ref huntDeer, value, nameof(HuntDeer)); }
        [SyncHostOnly] public bool HuntGoat { get => huntGoat; set => SetSetting(ref huntGoat, value, nameof(HuntGoat)); }
        [SyncHostOnly] public bool HuntRabbit { get => huntRabbit; set => SetSetting(ref huntRabbit, value, nameof(HuntRabbit)); }
        [SyncHostOnly] public bool HuntCamel { get => huntCamel; set => SetSetting(ref huntCamel, value, nameof(HuntCamel)); }
        [SyncHostOnly] public bool HuntChicken { get => huntChicken; set => SetSetting(ref huntChicken, value, nameof(HuntChicken)); }
        [SyncHostOnly] public bool HuntCow { get => huntCow; set => SetSetting(ref huntCow, value, nameof(HuntCow)); }

        [SyncHostOnly] public int DeerMeat { get => deerMeat; set => SetMeatSetting(ref deerMeat, value, nameof(DeerMeat), nameof(DeerMeatText)); }
        [SyncHostOnly] public int GoatMeat { get => goatMeat; set => SetMeatSetting(ref goatMeat, value, nameof(GoatMeat), nameof(GoatMeatText)); }
        [SyncHostOnly] public int RabbitMeat { get => rabbitMeat; set => SetMeatSetting(ref rabbitMeat, value, nameof(RabbitMeat), nameof(RabbitMeatText)); }
        [SyncHostOnly] public int CamelMeat { get => camelMeat; set => SetMeatSetting(ref camelMeat, value, nameof(CamelMeat), nameof(CamelMeatText)); }
        [SyncHostOnly] public int ChickenMeat { get => chickenMeat; set => SetMeatSetting(ref chickenMeat, value, nameof(ChickenMeat), nameof(ChickenMeatText)); }
        [SyncHostOnly] public int CowMeat { get => cowMeat; set => SetMeatSetting(ref cowMeat, value, nameof(CowMeat), nameof(CowMeatText)); }

        public string DeerMeatText { get => DeerMeat.ToString(); set => SetMeatText(value, parsed => DeerMeat = parsed, nameof(DeerMeatText)); }
        public string GoatMeatText { get => GoatMeat.ToString(); set => SetMeatText(value, parsed => GoatMeat = parsed, nameof(GoatMeatText)); }
        public string RabbitMeatText { get => RabbitMeat.ToString(); set => SetMeatText(value, parsed => RabbitMeat = parsed, nameof(RabbitMeatText)); }
        public string CamelMeatText { get => CamelMeat.ToString(); set => SetMeatText(value, parsed => CamelMeat = parsed, nameof(CamelMeatText)); }
        public string ChickenMeatText { get => ChickenMeat.ToString(); set => SetMeatText(value, parsed => ChickenMeat = parsed, nameof(ChickenMeatText)); }
        public string CowMeatText { get => CowMeat.ToString(); set => SetMeatText(value, parsed => CowMeat = parsed, nameof(CowMeatText)); }

        public bool IsKnownAnimal(eChimps type)
        {
            return type == eChimps.CHIMP_TYPE_DEER ||
                type == eChimps.CHIMP_TYPE_GOAT ||
                type == eChimps.CHIMP_TYPE_RABBIT ||
                type == eChimps.CHIMP_TYPE_CAMEL ||
                type == eChimps.CHIMP_TYPE_CHICKEN ||
                type == eChimps.CHIMP_TYPE_COW;
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
                case eChimps.CHIMP_TYPE_COW:
                    return HuntCow;
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
                case eChimps.CHIMP_TYPE_COW:
                    return CowMeat;
                default:
                    return DefaultDeerMeat;
            }
        }

        private void ResetToDefault()
        {
            EnableMod = true;
            ImprovedPathfinding = true;
            HuntDeer = true;
            HuntGoat = true;
            HuntRabbit = true;
            HuntCamel = true;
            HuntChicken = true;
            HuntCow = false;
            DeerMeat = DefaultDeerMeat;
            GoatMeat = DefaultGoatMeat;
            RabbitMeat = DefaultRabbitMeat;
            CamelMeat = DefaultCamelMeat;
            ChickenMeat = DefaultChickenMeat;
            CowMeat = DefaultCowMeat;
        }

        private void SetSetting<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private void SetMeatSetting(ref int field, int value, string propertyName, string textPropertyName)
        {
            int clamped = ClampMeat(value);
            if (field == clamped)
                return;

            field = clamped;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(textPropertyName);
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

        private static int ClampMeat(int value)
        {
            if (value < 0)
                return 0;

            if (value > 100)
                return 100;

            return value;
        }
    }
}
