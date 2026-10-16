using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace PopularityTest
{
    public sealed class PopularitySettings : PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true;
        private volatile bool missionLocked;
        private readonly SynchronizationContext uiContext = SynchronizationContext.Current;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;
        public PopularitySettings()
        {
            ResetToDefaultCommand = new RelayCommand(ResetDefaults);
            PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CanEditHostSettings)) RefreshEditors(); };
        }
        public bool BetweenMissions => !missionLocked;
        public bool CanConfigure => BetweenMissions && CanEditHostSettings;
        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set { if (Change(ref enableMod, value)) RefreshEditors(); }
        }
        public RelayCommand ResetToDefaultCommand { get; }
        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string ScopeText => SerpLocalization.Get("Popularity.Scope");
        public string RuleText => SerpLocalization.Get("Popularity.Rule");
        protected override string ResolveSettingsUiText(string key, string fallback)
        { string text = SerpLocalization.Get(key); return text == key ? fallback : text; }
        internal void SetMissionLocked(bool locked)
        {
            missionLocked = locked;
            // Never raise Noesis-bound notifications on a simulation publisher thread.
            if (Thread.CurrentThread.ManagedThreadId == uiThread) RefreshEditors();
            else if (uiContext != null) uiContext.Post(_ => RefreshEditors(), null);
        }
        private bool Change<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (!BetweenMissions || !CanMutateSetting(name) || Equals(field, value)) return false;
            field = value; OnPropertyChanged(name); return true;
        }
        private void RefreshEditors()
        {
            OnPropertyChanged(nameof(BetweenMissions)); OnPropertyChanged(nameof(CanConfigure));
            OnPropertyChanged(nameof(RationsPopModifierNoneEditable));
            OnPropertyChanged(nameof(RationsPopModifierHalfEditable));
            OnPropertyChanged(nameof(RationsPopModifierFullEditable));
            OnPropertyChanged(nameof(RationsPopModifierExtraEditable));
            OnPropertyChanged(nameof(RationsPopModifierDoubleEditable));
            OnPropertyChanged(nameof(RationsPopModifierNoPreferredFoodEditable));
            OnPropertyChanged(nameof(RationsPopModifierNoPopulationEditable));
            OnPropertyChanged(nameof(FoodVarietyPopModifierTwoTypesEditable));
            OnPropertyChanged(nameof(FoodVarietyPopModifierThreeTypesEditable));
            OnPropertyChanged(nameof(FoodVarietyPopModifierFourTypesEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierOneEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierTwoEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierThreeEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierFourEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierFiveEditable));
            OnPropertyChanged(nameof(TaxPopModifierHugeDonationEditable));
            OnPropertyChanged(nameof(TaxPopModifierBigDonationEditable));
            OnPropertyChanged(nameof(TaxPopModifierSmallDonationEditable));
            OnPropertyChanged(nameof(TaxPopModifierNoneEditable));
            OnPropertyChanged(nameof(TaxPopModifierLowEditable));
            OnPropertyChanged(nameof(TaxPopModifierModerateEditable));
            OnPropertyChanged(nameof(TaxPopModifierHighEditable));
            OnPropertyChanged(nameof(TaxPopModifierMeanEditable));
            OnPropertyChanged(nameof(TaxPopModifierUsuriousEditable));
            OnPropertyChanged(nameof(TaxPopModifierCruelEditable));
            OnPropertyChanged(nameof(TaxPopModifierMostCruelEditable));
            OnPropertyChanged(nameof(TaxPopModifierCruellestEditable));
            OnPropertyChanged(nameof(TaxPopModifierUnaffordableDonationEditable));
            OnPropertyChanged(nameof(TaxPopModifierUnknownModeEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierOneEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierTwoEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierThreeEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierFourEditable));
            OnPropertyChanged(nameof(ChurchPopModifierEditable));
            OnPropertyChanged(nameof(CathedralPopModifierEditable));
            OnPropertyChanged(nameof(AlePopModifierTierOneEditable));
            OnPropertyChanged(nameof(AlePopModifierTierTwoEditable));
            OnPropertyChanged(nameof(AlePopModifierTierThreeEditable));
            OnPropertyChanged(nameof(AlePopModifierTierFourEditable));
            OnPropertyChanged(nameof(FairsPopularityEventModifierEditable));
            OnPropertyChanged(nameof(GoodBadThingPopModifierMultiplierEditable));
        }
        private void ResetDefaults()
        {
            if (!CanConfigure) return;
            EnableMod = true;
            RationsPopModifierNoneEnabled = false; RationsPopModifierNone = -8;
            RationsPopModifierHalfEnabled = false; RationsPopModifierHalf = -4;
            RationsPopModifierFullEnabled = false; RationsPopModifierFull = 0;
            RationsPopModifierExtraEnabled = false; RationsPopModifierExtra = 4;
            RationsPopModifierDoubleEnabled = false; RationsPopModifierDouble = 8;
            RationsPopModifierNoPreferredFoodEnabled = false; RationsPopModifierNoPreferredFood = -8;
            RationsPopModifierNoPopulationEnabled = false; RationsPopModifierNoPopulation = 8;
            FoodVarietyPopModifierTwoTypesEnabled = false; FoodVarietyPopModifierTwoTypes = 1;
            FoodVarietyPopModifierThreeTypesEnabled = false; FoodVarietyPopModifierThreeTypes = 2;
            FoodVarietyPopModifierFourTypesEnabled = false; FoodVarietyPopModifierFourTypes = 3;
            OverpopulationPopModifierTierOneEnabled = false; OverpopulationPopModifierTierOne = -2;
            OverpopulationPopModifierTierTwoEnabled = false; OverpopulationPopModifierTierTwo = -4;
            OverpopulationPopModifierTierThreeEnabled = false; OverpopulationPopModifierTierThree = -6;
            OverpopulationPopModifierTierFourEnabled = false; OverpopulationPopModifierTierFour = -8;
            OverpopulationPopModifierTierFiveEnabled = false; OverpopulationPopModifierTierFive = -10;
            TaxPopModifierHugeDonationEnabled = false; TaxPopModifierHugeDonation = 7;
            TaxPopModifierBigDonationEnabled = false; TaxPopModifierBigDonation = 5;
            TaxPopModifierSmallDonationEnabled = false; TaxPopModifierSmallDonation = 3;
            TaxPopModifierNoneEnabled = false; TaxPopModifierNone = 1;
            TaxPopModifierLowEnabled = false; TaxPopModifierLow = -2;
            TaxPopModifierModerateEnabled = false; TaxPopModifierModerate = -4;
            TaxPopModifierHighEnabled = false; TaxPopModifierHigh = -6;
            TaxPopModifierMeanEnabled = false; TaxPopModifierMean = -8;
            TaxPopModifierUsuriousEnabled = false; TaxPopModifierUsurious = -12;
            TaxPopModifierCruelEnabled = false; TaxPopModifierCruel = -16;
            TaxPopModifierMostCruelEnabled = false; TaxPopModifierMostCruel = -20;
            TaxPopModifierCruellestEnabled = false; TaxPopModifierCruellest = -24;
            TaxPopModifierUnaffordableDonationEnabled = false; TaxPopModifierUnaffordableDonation = 1;
            TaxPopModifierUnknownModeEnabled = false; TaxPopModifierUnknownMode = -24;
            ReligionPopModifierTierOneEnabled = false; ReligionPopModifierTierOne = 2;
            ReligionPopModifierTierTwoEnabled = false; ReligionPopModifierTierTwo = 4;
            ReligionPopModifierTierThreeEnabled = false; ReligionPopModifierTierThree = 6;
            ReligionPopModifierTierFourEnabled = false; ReligionPopModifierTierFour = 8;
            ChurchPopModifierEnabled = false; ChurchPopModifier = 1;
            CathedralPopModifierEnabled = false; CathedralPopModifier = 2;
            AlePopModifierTierOneEnabled = false; AlePopModifierTierOne = 2;
            AlePopModifierTierTwoEnabled = false; AlePopModifierTierTwo = 4;
            AlePopModifierTierThreeEnabled = false; AlePopModifierTierThree = 6;
            AlePopModifierTierFourEnabled = false; AlePopModifierTierFour = 8;
            FairsPopularityEventModifierEnabled = false; FairsPopularityEventModifier = 16;
            GoodBadThingPopModifierMultiplierEnabled = false; GoodBadThingPopModifierMultiplier = 1;
        }
        private bool rationsPopModifierNoneEnabled;
        private int rationsPopModifierNone = -8;
        [SyncHostOnly]
        public bool RationsPopModifierNoneEnabled
        {
            get => rationsPopModifierNoneEnabled;
            set { if (Change(ref rationsPopModifierNoneEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierNoneEditable)); }
        }
        [SyncHostOnly]
        public int RationsPopModifierNone
        {
            get => rationsPopModifierNone;
            set { if (Change(ref rationsPopModifierNone, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierNoneValueText)); }
        }
        public bool RationsPopModifierNoneEditable => CanConfigure && EnableMod && RationsPopModifierNoneEnabled;
        public string RationsPopModifierNoneValueText
        {
            get => RationsPopModifierNone.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierNone = parsed; OnPropertyChanged(nameof(RationsPopModifierNoneValueText)); }
        }
        public string RationsPopModifierNoneTitle => SerpLocalization.Get("Popularity.RationsPopModifierNone");
        public string RationsPopModifierNoneHelp => SerpLocalization.Get("Popularity.RationsPopModifierNone.Help");
        private bool rationsPopModifierHalfEnabled;
        private int rationsPopModifierHalf = -4;
        [SyncHostOnly]
        public bool RationsPopModifierHalfEnabled
        {
            get => rationsPopModifierHalfEnabled;
            set { if (Change(ref rationsPopModifierHalfEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierHalfEditable)); }
        }
        [SyncHostOnly]
        public int RationsPopModifierHalf
        {
            get => rationsPopModifierHalf;
            set { if (Change(ref rationsPopModifierHalf, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierHalfValueText)); }
        }
        public bool RationsPopModifierHalfEditable => CanConfigure && EnableMod && RationsPopModifierHalfEnabled;
        public string RationsPopModifierHalfValueText
        {
            get => RationsPopModifierHalf.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierHalf = parsed; OnPropertyChanged(nameof(RationsPopModifierHalfValueText)); }
        }
        public string RationsPopModifierHalfTitle => SerpLocalization.Get("Popularity.RationsPopModifierHalf");
        public string RationsPopModifierHalfHelp => SerpLocalization.Get("Popularity.RationsPopModifierHalf.Help");
        private bool rationsPopModifierFullEnabled;
        private int rationsPopModifierFull = 0;
        [SyncHostOnly]
        public bool RationsPopModifierFullEnabled
        {
            get => rationsPopModifierFullEnabled;
            set { if (Change(ref rationsPopModifierFullEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierFullEditable)); }
        }
        [SyncHostOnly]
        public int RationsPopModifierFull
        {
            get => rationsPopModifierFull;
            set { if (Change(ref rationsPopModifierFull, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierFullValueText)); }
        }
        public bool RationsPopModifierFullEditable => CanConfigure && EnableMod && RationsPopModifierFullEnabled;
        public string RationsPopModifierFullValueText
        {
            get => RationsPopModifierFull.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierFull = parsed; OnPropertyChanged(nameof(RationsPopModifierFullValueText)); }
        }
        public string RationsPopModifierFullTitle => SerpLocalization.Get("Popularity.RationsPopModifierFull");
        public string RationsPopModifierFullHelp => SerpLocalization.Get("Popularity.RationsPopModifierFull.Help");
        private bool rationsPopModifierExtraEnabled;
        private int rationsPopModifierExtra = 4;
        [SyncHostOnly]
        public bool RationsPopModifierExtraEnabled
        {
            get => rationsPopModifierExtraEnabled;
            set { if (Change(ref rationsPopModifierExtraEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierExtraEditable)); }
        }
        [SyncHostOnly]
        public int RationsPopModifierExtra
        {
            get => rationsPopModifierExtra;
            set { if (Change(ref rationsPopModifierExtra, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierExtraValueText)); }
        }
        public bool RationsPopModifierExtraEditable => CanConfigure && EnableMod && RationsPopModifierExtraEnabled;
        public string RationsPopModifierExtraValueText
        {
            get => RationsPopModifierExtra.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierExtra = parsed; OnPropertyChanged(nameof(RationsPopModifierExtraValueText)); }
        }
        public string RationsPopModifierExtraTitle => SerpLocalization.Get("Popularity.RationsPopModifierExtra");
        public string RationsPopModifierExtraHelp => SerpLocalization.Get("Popularity.RationsPopModifierExtra.Help");
        private bool rationsPopModifierDoubleEnabled;
        private int rationsPopModifierDouble = 8;
        [SyncHostOnly]
        public bool RationsPopModifierDoubleEnabled
        {
            get => rationsPopModifierDoubleEnabled;
            set { if (Change(ref rationsPopModifierDoubleEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierDoubleEditable)); }
        }
        [SyncHostOnly]
        public int RationsPopModifierDouble
        {
            get => rationsPopModifierDouble;
            set { if (Change(ref rationsPopModifierDouble, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierDoubleValueText)); }
        }
        public bool RationsPopModifierDoubleEditable => CanConfigure && EnableMod && RationsPopModifierDoubleEnabled;
        public string RationsPopModifierDoubleValueText
        {
            get => RationsPopModifierDouble.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierDouble = parsed; OnPropertyChanged(nameof(RationsPopModifierDoubleValueText)); }
        }
        public string RationsPopModifierDoubleTitle => SerpLocalization.Get("Popularity.RationsPopModifierDouble");
        public string RationsPopModifierDoubleHelp => SerpLocalization.Get("Popularity.RationsPopModifierDouble.Help");
        private bool rationsPopModifierNoPreferredFoodEnabled;
        private int rationsPopModifierNoPreferredFood = -8;
        [SyncHostOnly]
        public bool RationsPopModifierNoPreferredFoodEnabled
        {
            get => rationsPopModifierNoPreferredFoodEnabled;
            set { if (Change(ref rationsPopModifierNoPreferredFoodEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierNoPreferredFoodEditable)); }
        }
        [SyncHostOnly]
        public int RationsPopModifierNoPreferredFood
        {
            get => rationsPopModifierNoPreferredFood;
            set { if (Change(ref rationsPopModifierNoPreferredFood, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierNoPreferredFoodValueText)); }
        }
        public bool RationsPopModifierNoPreferredFoodEditable => CanConfigure && EnableMod && RationsPopModifierNoPreferredFoodEnabled;
        public string RationsPopModifierNoPreferredFoodValueText
        {
            get => RationsPopModifierNoPreferredFood.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierNoPreferredFood = parsed; OnPropertyChanged(nameof(RationsPopModifierNoPreferredFoodValueText)); }
        }
        public string RationsPopModifierNoPreferredFoodTitle => SerpLocalization.Get("Popularity.RationsPopModifierNoPreferredFood");
        public string RationsPopModifierNoPreferredFoodHelp => SerpLocalization.Get("Popularity.RationsPopModifierNoPreferredFood.Help");
        private bool rationsPopModifierNoPopulationEnabled;
        private int rationsPopModifierNoPopulation = 8;
        [SyncHostOnly]
        public bool RationsPopModifierNoPopulationEnabled
        {
            get => rationsPopModifierNoPopulationEnabled;
            set { if (Change(ref rationsPopModifierNoPopulationEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierNoPopulationEditable)); }
        }
        [SyncHostOnly]
        public int RationsPopModifierNoPopulation
        {
            get => rationsPopModifierNoPopulation;
            set { if (Change(ref rationsPopModifierNoPopulation, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierNoPopulationValueText)); }
        }
        public bool RationsPopModifierNoPopulationEditable => CanConfigure && EnableMod && RationsPopModifierNoPopulationEnabled;
        public string RationsPopModifierNoPopulationValueText
        {
            get => RationsPopModifierNoPopulation.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierNoPopulation = parsed; OnPropertyChanged(nameof(RationsPopModifierNoPopulationValueText)); }
        }
        public string RationsPopModifierNoPopulationTitle => SerpLocalization.Get("Popularity.RationsPopModifierNoPopulation");
        public string RationsPopModifierNoPopulationHelp => SerpLocalization.Get("Popularity.RationsPopModifierNoPopulation.Help");
        private bool foodVarietyPopModifierTwoTypesEnabled;
        private int foodVarietyPopModifierTwoTypes = 1;
        [SyncHostOnly]
        public bool FoodVarietyPopModifierTwoTypesEnabled
        {
            get => foodVarietyPopModifierTwoTypesEnabled;
            set { if (Change(ref foodVarietyPopModifierTwoTypesEnabled, value)) OnPropertyChanged(nameof(FoodVarietyPopModifierTwoTypesEditable)); }
        }
        [SyncHostOnly]
        public int FoodVarietyPopModifierTwoTypes
        {
            get => foodVarietyPopModifierTwoTypes;
            set { if (Change(ref foodVarietyPopModifierTwoTypes, Math.Max(-6, Math.Min(6, value)))) OnPropertyChanged(nameof(FoodVarietyPopModifierTwoTypesValueText)); }
        }
        public bool FoodVarietyPopModifierTwoTypesEditable => CanConfigure && EnableMod && FoodVarietyPopModifierTwoTypesEnabled;
        public string FoodVarietyPopModifierTwoTypesValueText
        {
            get => FoodVarietyPopModifierTwoTypes.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FoodVarietyPopModifierTwoTypes = parsed; OnPropertyChanged(nameof(FoodVarietyPopModifierTwoTypesValueText)); }
        }
        public string FoodVarietyPopModifierTwoTypesTitle => SerpLocalization.Get("Popularity.FoodVarietyPopModifierTwoTypes");
        public string FoodVarietyPopModifierTwoTypesHelp => SerpLocalization.Get("Popularity.FoodVarietyPopModifierTwoTypes.Help");
        private bool foodVarietyPopModifierThreeTypesEnabled;
        private int foodVarietyPopModifierThreeTypes = 2;
        [SyncHostOnly]
        public bool FoodVarietyPopModifierThreeTypesEnabled
        {
            get => foodVarietyPopModifierThreeTypesEnabled;
            set { if (Change(ref foodVarietyPopModifierThreeTypesEnabled, value)) OnPropertyChanged(nameof(FoodVarietyPopModifierThreeTypesEditable)); }
        }
        [SyncHostOnly]
        public int FoodVarietyPopModifierThreeTypes
        {
            get => foodVarietyPopModifierThreeTypes;
            set { if (Change(ref foodVarietyPopModifierThreeTypes, Math.Max(-6, Math.Min(6, value)))) OnPropertyChanged(nameof(FoodVarietyPopModifierThreeTypesValueText)); }
        }
        public bool FoodVarietyPopModifierThreeTypesEditable => CanConfigure && EnableMod && FoodVarietyPopModifierThreeTypesEnabled;
        public string FoodVarietyPopModifierThreeTypesValueText
        {
            get => FoodVarietyPopModifierThreeTypes.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FoodVarietyPopModifierThreeTypes = parsed; OnPropertyChanged(nameof(FoodVarietyPopModifierThreeTypesValueText)); }
        }
        public string FoodVarietyPopModifierThreeTypesTitle => SerpLocalization.Get("Popularity.FoodVarietyPopModifierThreeTypes");
        public string FoodVarietyPopModifierThreeTypesHelp => SerpLocalization.Get("Popularity.FoodVarietyPopModifierThreeTypes.Help");
        private bool foodVarietyPopModifierFourTypesEnabled;
        private int foodVarietyPopModifierFourTypes = 3;
        [SyncHostOnly]
        public bool FoodVarietyPopModifierFourTypesEnabled
        {
            get => foodVarietyPopModifierFourTypesEnabled;
            set { if (Change(ref foodVarietyPopModifierFourTypesEnabled, value)) OnPropertyChanged(nameof(FoodVarietyPopModifierFourTypesEditable)); }
        }
        [SyncHostOnly]
        public int FoodVarietyPopModifierFourTypes
        {
            get => foodVarietyPopModifierFourTypes;
            set { if (Change(ref foodVarietyPopModifierFourTypes, Math.Max(-6, Math.Min(6, value)))) OnPropertyChanged(nameof(FoodVarietyPopModifierFourTypesValueText)); }
        }
        public bool FoodVarietyPopModifierFourTypesEditable => CanConfigure && EnableMod && FoodVarietyPopModifierFourTypesEnabled;
        public string FoodVarietyPopModifierFourTypesValueText
        {
            get => FoodVarietyPopModifierFourTypes.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FoodVarietyPopModifierFourTypes = parsed; OnPropertyChanged(nameof(FoodVarietyPopModifierFourTypesValueText)); }
        }
        public string FoodVarietyPopModifierFourTypesTitle => SerpLocalization.Get("Popularity.FoodVarietyPopModifierFourTypes");
        public string FoodVarietyPopModifierFourTypesHelp => SerpLocalization.Get("Popularity.FoodVarietyPopModifierFourTypes.Help");
        private bool overpopulationPopModifierTierOneEnabled;
        private int overpopulationPopModifierTierOne = -2;
        [SyncHostOnly]
        public bool OverpopulationPopModifierTierOneEnabled
        {
            get => overpopulationPopModifierTierOneEnabled;
            set { if (Change(ref overpopulationPopModifierTierOneEnabled, value)) OnPropertyChanged(nameof(OverpopulationPopModifierTierOneEditable)); }
        }
        [SyncHostOnly]
        public int OverpopulationPopModifierTierOne
        {
            get => overpopulationPopModifierTierOne;
            set { if (Change(ref overpopulationPopModifierTierOne, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierOneValueText)); }
        }
        public bool OverpopulationPopModifierTierOneEditable => CanConfigure && EnableMod && OverpopulationPopModifierTierOneEnabled;
        public string OverpopulationPopModifierTierOneValueText
        {
            get => OverpopulationPopModifierTierOne.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierOne = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierOneValueText)); }
        }
        public string OverpopulationPopModifierTierOneTitle => SerpLocalization.Get("Popularity.OverpopulationPopModifierTierOne");
        public string OverpopulationPopModifierTierOneHelp => SerpLocalization.Get("Popularity.OverpopulationPopModifierTierOne.Help");
        private bool overpopulationPopModifierTierTwoEnabled;
        private int overpopulationPopModifierTierTwo = -4;
        [SyncHostOnly]
        public bool OverpopulationPopModifierTierTwoEnabled
        {
            get => overpopulationPopModifierTierTwoEnabled;
            set { if (Change(ref overpopulationPopModifierTierTwoEnabled, value)) OnPropertyChanged(nameof(OverpopulationPopModifierTierTwoEditable)); }
        }
        [SyncHostOnly]
        public int OverpopulationPopModifierTierTwo
        {
            get => overpopulationPopModifierTierTwo;
            set { if (Change(ref overpopulationPopModifierTierTwo, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierTwoValueText)); }
        }
        public bool OverpopulationPopModifierTierTwoEditable => CanConfigure && EnableMod && OverpopulationPopModifierTierTwoEnabled;
        public string OverpopulationPopModifierTierTwoValueText
        {
            get => OverpopulationPopModifierTierTwo.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierTwo = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierTwoValueText)); }
        }
        public string OverpopulationPopModifierTierTwoTitle => SerpLocalization.Get("Popularity.OverpopulationPopModifierTierTwo");
        public string OverpopulationPopModifierTierTwoHelp => SerpLocalization.Get("Popularity.OverpopulationPopModifierTierTwo.Help");
        private bool overpopulationPopModifierTierThreeEnabled;
        private int overpopulationPopModifierTierThree = -6;
        [SyncHostOnly]
        public bool OverpopulationPopModifierTierThreeEnabled
        {
            get => overpopulationPopModifierTierThreeEnabled;
            set { if (Change(ref overpopulationPopModifierTierThreeEnabled, value)) OnPropertyChanged(nameof(OverpopulationPopModifierTierThreeEditable)); }
        }
        [SyncHostOnly]
        public int OverpopulationPopModifierTierThree
        {
            get => overpopulationPopModifierTierThree;
            set { if (Change(ref overpopulationPopModifierTierThree, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierThreeValueText)); }
        }
        public bool OverpopulationPopModifierTierThreeEditable => CanConfigure && EnableMod && OverpopulationPopModifierTierThreeEnabled;
        public string OverpopulationPopModifierTierThreeValueText
        {
            get => OverpopulationPopModifierTierThree.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierThree = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierThreeValueText)); }
        }
        public string OverpopulationPopModifierTierThreeTitle => SerpLocalization.Get("Popularity.OverpopulationPopModifierTierThree");
        public string OverpopulationPopModifierTierThreeHelp => SerpLocalization.Get("Popularity.OverpopulationPopModifierTierThree.Help");
        private bool overpopulationPopModifierTierFourEnabled;
        private int overpopulationPopModifierTierFour = -8;
        [SyncHostOnly]
        public bool OverpopulationPopModifierTierFourEnabled
        {
            get => overpopulationPopModifierTierFourEnabled;
            set { if (Change(ref overpopulationPopModifierTierFourEnabled, value)) OnPropertyChanged(nameof(OverpopulationPopModifierTierFourEditable)); }
        }
        [SyncHostOnly]
        public int OverpopulationPopModifierTierFour
        {
            get => overpopulationPopModifierTierFour;
            set { if (Change(ref overpopulationPopModifierTierFour, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierFourValueText)); }
        }
        public bool OverpopulationPopModifierTierFourEditable => CanConfigure && EnableMod && OverpopulationPopModifierTierFourEnabled;
        public string OverpopulationPopModifierTierFourValueText
        {
            get => OverpopulationPopModifierTierFour.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierFour = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierFourValueText)); }
        }
        public string OverpopulationPopModifierTierFourTitle => SerpLocalization.Get("Popularity.OverpopulationPopModifierTierFour");
        public string OverpopulationPopModifierTierFourHelp => SerpLocalization.Get("Popularity.OverpopulationPopModifierTierFour.Help");
        private bool overpopulationPopModifierTierFiveEnabled;
        private int overpopulationPopModifierTierFive = -10;
        [SyncHostOnly]
        public bool OverpopulationPopModifierTierFiveEnabled
        {
            get => overpopulationPopModifierTierFiveEnabled;
            set { if (Change(ref overpopulationPopModifierTierFiveEnabled, value)) OnPropertyChanged(nameof(OverpopulationPopModifierTierFiveEditable)); }
        }
        [SyncHostOnly]
        public int OverpopulationPopModifierTierFive
        {
            get => overpopulationPopModifierTierFive;
            set { if (Change(ref overpopulationPopModifierTierFive, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierFiveValueText)); }
        }
        public bool OverpopulationPopModifierTierFiveEditable => CanConfigure && EnableMod && OverpopulationPopModifierTierFiveEnabled;
        public string OverpopulationPopModifierTierFiveValueText
        {
            get => OverpopulationPopModifierTierFive.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierFive = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierFiveValueText)); }
        }
        public string OverpopulationPopModifierTierFiveTitle => SerpLocalization.Get("Popularity.OverpopulationPopModifierTierFive");
        public string OverpopulationPopModifierTierFiveHelp => SerpLocalization.Get("Popularity.OverpopulationPopModifierTierFive.Help");
        private bool taxPopModifierHugeDonationEnabled;
        private int taxPopModifierHugeDonation = 7;
        [SyncHostOnly]
        public bool TaxPopModifierHugeDonationEnabled
        {
            get => taxPopModifierHugeDonationEnabled;
            set { if (Change(ref taxPopModifierHugeDonationEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierHugeDonationEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierHugeDonation
        {
            get => taxPopModifierHugeDonation;
            set { if (Change(ref taxPopModifierHugeDonation, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierHugeDonationValueText)); }
        }
        public bool TaxPopModifierHugeDonationEditable => CanConfigure && EnableMod && TaxPopModifierHugeDonationEnabled;
        public string TaxPopModifierHugeDonationValueText
        {
            get => TaxPopModifierHugeDonation.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierHugeDonation = parsed; OnPropertyChanged(nameof(TaxPopModifierHugeDonationValueText)); }
        }
        public string TaxPopModifierHugeDonationTitle => SerpLocalization.Get("Popularity.TaxPopModifierHugeDonation");
        public string TaxPopModifierHugeDonationHelp => SerpLocalization.Get("Popularity.TaxPopModifierHugeDonation.Help");
        private bool taxPopModifierBigDonationEnabled;
        private int taxPopModifierBigDonation = 5;
        [SyncHostOnly]
        public bool TaxPopModifierBigDonationEnabled
        {
            get => taxPopModifierBigDonationEnabled;
            set { if (Change(ref taxPopModifierBigDonationEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierBigDonationEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierBigDonation
        {
            get => taxPopModifierBigDonation;
            set { if (Change(ref taxPopModifierBigDonation, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierBigDonationValueText)); }
        }
        public bool TaxPopModifierBigDonationEditable => CanConfigure && EnableMod && TaxPopModifierBigDonationEnabled;
        public string TaxPopModifierBigDonationValueText
        {
            get => TaxPopModifierBigDonation.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierBigDonation = parsed; OnPropertyChanged(nameof(TaxPopModifierBigDonationValueText)); }
        }
        public string TaxPopModifierBigDonationTitle => SerpLocalization.Get("Popularity.TaxPopModifierBigDonation");
        public string TaxPopModifierBigDonationHelp => SerpLocalization.Get("Popularity.TaxPopModifierBigDonation.Help");
        private bool taxPopModifierSmallDonationEnabled;
        private int taxPopModifierSmallDonation = 3;
        [SyncHostOnly]
        public bool TaxPopModifierSmallDonationEnabled
        {
            get => taxPopModifierSmallDonationEnabled;
            set { if (Change(ref taxPopModifierSmallDonationEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierSmallDonationEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierSmallDonation
        {
            get => taxPopModifierSmallDonation;
            set { if (Change(ref taxPopModifierSmallDonation, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierSmallDonationValueText)); }
        }
        public bool TaxPopModifierSmallDonationEditable => CanConfigure && EnableMod && TaxPopModifierSmallDonationEnabled;
        public string TaxPopModifierSmallDonationValueText
        {
            get => TaxPopModifierSmallDonation.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierSmallDonation = parsed; OnPropertyChanged(nameof(TaxPopModifierSmallDonationValueText)); }
        }
        public string TaxPopModifierSmallDonationTitle => SerpLocalization.Get("Popularity.TaxPopModifierSmallDonation");
        public string TaxPopModifierSmallDonationHelp => SerpLocalization.Get("Popularity.TaxPopModifierSmallDonation.Help");
        private bool taxPopModifierNoneEnabled;
        private int taxPopModifierNone = 1;
        [SyncHostOnly]
        public bool TaxPopModifierNoneEnabled
        {
            get => taxPopModifierNoneEnabled;
            set { if (Change(ref taxPopModifierNoneEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierNoneEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierNone
        {
            get => taxPopModifierNone;
            set { if (Change(ref taxPopModifierNone, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierNoneValueText)); }
        }
        public bool TaxPopModifierNoneEditable => CanConfigure && EnableMod && TaxPopModifierNoneEnabled;
        public string TaxPopModifierNoneValueText
        {
            get => TaxPopModifierNone.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierNone = parsed; OnPropertyChanged(nameof(TaxPopModifierNoneValueText)); }
        }
        public string TaxPopModifierNoneTitle => SerpLocalization.Get("Popularity.TaxPopModifierNone");
        public string TaxPopModifierNoneHelp => SerpLocalization.Get("Popularity.TaxPopModifierNone.Help");
        private bool taxPopModifierLowEnabled;
        private int taxPopModifierLow = -2;
        [SyncHostOnly]
        public bool TaxPopModifierLowEnabled
        {
            get => taxPopModifierLowEnabled;
            set { if (Change(ref taxPopModifierLowEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierLowEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierLow
        {
            get => taxPopModifierLow;
            set { if (Change(ref taxPopModifierLow, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierLowValueText)); }
        }
        public bool TaxPopModifierLowEditable => CanConfigure && EnableMod && TaxPopModifierLowEnabled;
        public string TaxPopModifierLowValueText
        {
            get => TaxPopModifierLow.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierLow = parsed; OnPropertyChanged(nameof(TaxPopModifierLowValueText)); }
        }
        public string TaxPopModifierLowTitle => SerpLocalization.Get("Popularity.TaxPopModifierLow");
        public string TaxPopModifierLowHelp => SerpLocalization.Get("Popularity.TaxPopModifierLow.Help");
        private bool taxPopModifierModerateEnabled;
        private int taxPopModifierModerate = -4;
        [SyncHostOnly]
        public bool TaxPopModifierModerateEnabled
        {
            get => taxPopModifierModerateEnabled;
            set { if (Change(ref taxPopModifierModerateEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierModerateEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierModerate
        {
            get => taxPopModifierModerate;
            set { if (Change(ref taxPopModifierModerate, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierModerateValueText)); }
        }
        public bool TaxPopModifierModerateEditable => CanConfigure && EnableMod && TaxPopModifierModerateEnabled;
        public string TaxPopModifierModerateValueText
        {
            get => TaxPopModifierModerate.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierModerate = parsed; OnPropertyChanged(nameof(TaxPopModifierModerateValueText)); }
        }
        public string TaxPopModifierModerateTitle => SerpLocalization.Get("Popularity.TaxPopModifierModerate");
        public string TaxPopModifierModerateHelp => SerpLocalization.Get("Popularity.TaxPopModifierModerate.Help");
        private bool taxPopModifierHighEnabled;
        private int taxPopModifierHigh = -6;
        [SyncHostOnly]
        public bool TaxPopModifierHighEnabled
        {
            get => taxPopModifierHighEnabled;
            set { if (Change(ref taxPopModifierHighEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierHighEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierHigh
        {
            get => taxPopModifierHigh;
            set { if (Change(ref taxPopModifierHigh, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierHighValueText)); }
        }
        public bool TaxPopModifierHighEditable => CanConfigure && EnableMod && TaxPopModifierHighEnabled;
        public string TaxPopModifierHighValueText
        {
            get => TaxPopModifierHigh.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierHigh = parsed; OnPropertyChanged(nameof(TaxPopModifierHighValueText)); }
        }
        public string TaxPopModifierHighTitle => SerpLocalization.Get("Popularity.TaxPopModifierHigh");
        public string TaxPopModifierHighHelp => SerpLocalization.Get("Popularity.TaxPopModifierHigh.Help");
        private bool taxPopModifierMeanEnabled;
        private int taxPopModifierMean = -8;
        [SyncHostOnly]
        public bool TaxPopModifierMeanEnabled
        {
            get => taxPopModifierMeanEnabled;
            set { if (Change(ref taxPopModifierMeanEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierMeanEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierMean
        {
            get => taxPopModifierMean;
            set { if (Change(ref taxPopModifierMean, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierMeanValueText)); }
        }
        public bool TaxPopModifierMeanEditable => CanConfigure && EnableMod && TaxPopModifierMeanEnabled;
        public string TaxPopModifierMeanValueText
        {
            get => TaxPopModifierMean.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierMean = parsed; OnPropertyChanged(nameof(TaxPopModifierMeanValueText)); }
        }
        public string TaxPopModifierMeanTitle => SerpLocalization.Get("Popularity.TaxPopModifierMean");
        public string TaxPopModifierMeanHelp => SerpLocalization.Get("Popularity.TaxPopModifierMean.Help");
        private bool taxPopModifierUsuriousEnabled;
        private int taxPopModifierUsurious = -12;
        [SyncHostOnly]
        public bool TaxPopModifierUsuriousEnabled
        {
            get => taxPopModifierUsuriousEnabled;
            set { if (Change(ref taxPopModifierUsuriousEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierUsuriousEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierUsurious
        {
            get => taxPopModifierUsurious;
            set { if (Change(ref taxPopModifierUsurious, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierUsuriousValueText)); }
        }
        public bool TaxPopModifierUsuriousEditable => CanConfigure && EnableMod && TaxPopModifierUsuriousEnabled;
        public string TaxPopModifierUsuriousValueText
        {
            get => TaxPopModifierUsurious.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierUsurious = parsed; OnPropertyChanged(nameof(TaxPopModifierUsuriousValueText)); }
        }
        public string TaxPopModifierUsuriousTitle => SerpLocalization.Get("Popularity.TaxPopModifierUsurious");
        public string TaxPopModifierUsuriousHelp => SerpLocalization.Get("Popularity.TaxPopModifierUsurious.Help");
        private bool taxPopModifierCruelEnabled;
        private int taxPopModifierCruel = -16;
        [SyncHostOnly]
        public bool TaxPopModifierCruelEnabled
        {
            get => taxPopModifierCruelEnabled;
            set { if (Change(ref taxPopModifierCruelEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierCruelEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierCruel
        {
            get => taxPopModifierCruel;
            set { if (Change(ref taxPopModifierCruel, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierCruelValueText)); }
        }
        public bool TaxPopModifierCruelEditable => CanConfigure && EnableMod && TaxPopModifierCruelEnabled;
        public string TaxPopModifierCruelValueText
        {
            get => TaxPopModifierCruel.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierCruel = parsed; OnPropertyChanged(nameof(TaxPopModifierCruelValueText)); }
        }
        public string TaxPopModifierCruelTitle => SerpLocalization.Get("Popularity.TaxPopModifierCruel");
        public string TaxPopModifierCruelHelp => SerpLocalization.Get("Popularity.TaxPopModifierCruel.Help");
        private bool taxPopModifierMostCruelEnabled;
        private int taxPopModifierMostCruel = -20;
        [SyncHostOnly]
        public bool TaxPopModifierMostCruelEnabled
        {
            get => taxPopModifierMostCruelEnabled;
            set { if (Change(ref taxPopModifierMostCruelEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierMostCruelEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierMostCruel
        {
            get => taxPopModifierMostCruel;
            set { if (Change(ref taxPopModifierMostCruel, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierMostCruelValueText)); }
        }
        public bool TaxPopModifierMostCruelEditable => CanConfigure && EnableMod && TaxPopModifierMostCruelEnabled;
        public string TaxPopModifierMostCruelValueText
        {
            get => TaxPopModifierMostCruel.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierMostCruel = parsed; OnPropertyChanged(nameof(TaxPopModifierMostCruelValueText)); }
        }
        public string TaxPopModifierMostCruelTitle => SerpLocalization.Get("Popularity.TaxPopModifierMostCruel");
        public string TaxPopModifierMostCruelHelp => SerpLocalization.Get("Popularity.TaxPopModifierMostCruel.Help");
        private bool taxPopModifierCruellestEnabled;
        private int taxPopModifierCruellest = -24;
        [SyncHostOnly]
        public bool TaxPopModifierCruellestEnabled
        {
            get => taxPopModifierCruellestEnabled;
            set { if (Change(ref taxPopModifierCruellestEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierCruellestEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierCruellest
        {
            get => taxPopModifierCruellest;
            set { if (Change(ref taxPopModifierCruellest, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierCruellestValueText)); }
        }
        public bool TaxPopModifierCruellestEditable => CanConfigure && EnableMod && TaxPopModifierCruellestEnabled;
        public string TaxPopModifierCruellestValueText
        {
            get => TaxPopModifierCruellest.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierCruellest = parsed; OnPropertyChanged(nameof(TaxPopModifierCruellestValueText)); }
        }
        public string TaxPopModifierCruellestTitle => SerpLocalization.Get("Popularity.TaxPopModifierCruellest");
        public string TaxPopModifierCruellestHelp => SerpLocalization.Get("Popularity.TaxPopModifierCruellest.Help");
        private bool taxPopModifierUnaffordableDonationEnabled;
        private int taxPopModifierUnaffordableDonation = 1;
        [SyncHostOnly]
        public bool TaxPopModifierUnaffordableDonationEnabled
        {
            get => taxPopModifierUnaffordableDonationEnabled;
            set { if (Change(ref taxPopModifierUnaffordableDonationEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierUnaffordableDonationEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierUnaffordableDonation
        {
            get => taxPopModifierUnaffordableDonation;
            set { if (Change(ref taxPopModifierUnaffordableDonation, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierUnaffordableDonationValueText)); }
        }
        public bool TaxPopModifierUnaffordableDonationEditable => CanConfigure && EnableMod && TaxPopModifierUnaffordableDonationEnabled;
        public string TaxPopModifierUnaffordableDonationValueText
        {
            get => TaxPopModifierUnaffordableDonation.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierUnaffordableDonation = parsed; OnPropertyChanged(nameof(TaxPopModifierUnaffordableDonationValueText)); }
        }
        public string TaxPopModifierUnaffordableDonationTitle => SerpLocalization.Get("Popularity.TaxPopModifierUnaffordableDonation");
        public string TaxPopModifierUnaffordableDonationHelp => SerpLocalization.Get("Popularity.TaxPopModifierUnaffordableDonation.Help");
        private bool taxPopModifierUnknownModeEnabled;
        private int taxPopModifierUnknownMode = -24;
        [SyncHostOnly]
        public bool TaxPopModifierUnknownModeEnabled
        {
            get => taxPopModifierUnknownModeEnabled;
            set { if (Change(ref taxPopModifierUnknownModeEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierUnknownModeEditable)); }
        }
        [SyncHostOnly]
        public int TaxPopModifierUnknownMode
        {
            get => taxPopModifierUnknownMode;
            set { if (Change(ref taxPopModifierUnknownMode, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierUnknownModeValueText)); }
        }
        public bool TaxPopModifierUnknownModeEditable => CanConfigure && EnableMod && TaxPopModifierUnknownModeEnabled;
        public string TaxPopModifierUnknownModeValueText
        {
            get => TaxPopModifierUnknownMode.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierUnknownMode = parsed; OnPropertyChanged(nameof(TaxPopModifierUnknownModeValueText)); }
        }
        public string TaxPopModifierUnknownModeTitle => SerpLocalization.Get("Popularity.TaxPopModifierUnknownMode");
        public string TaxPopModifierUnknownModeHelp => SerpLocalization.Get("Popularity.TaxPopModifierUnknownMode.Help");
        private bool religionPopModifierTierOneEnabled;
        private int religionPopModifierTierOne = 2;
        [SyncHostOnly]
        public bool ReligionPopModifierTierOneEnabled
        {
            get => religionPopModifierTierOneEnabled;
            set { if (Change(ref religionPopModifierTierOneEnabled, value)) OnPropertyChanged(nameof(ReligionPopModifierTierOneEditable)); }
        }
        [SyncHostOnly]
        public int ReligionPopModifierTierOne
        {
            get => religionPopModifierTierOne;
            set { if (Change(ref religionPopModifierTierOne, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierOneValueText)); }
        }
        public bool ReligionPopModifierTierOneEditable => CanConfigure && EnableMod && ReligionPopModifierTierOneEnabled;
        public string ReligionPopModifierTierOneValueText
        {
            get => ReligionPopModifierTierOne.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierOne = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierOneValueText)); }
        }
        public string ReligionPopModifierTierOneTitle => SerpLocalization.Get("Popularity.ReligionPopModifierTierOne");
        public string ReligionPopModifierTierOneHelp => SerpLocalization.Get("Popularity.ReligionPopModifierTierOne.Help");
        private bool religionPopModifierTierTwoEnabled;
        private int religionPopModifierTierTwo = 4;
        [SyncHostOnly]
        public bool ReligionPopModifierTierTwoEnabled
        {
            get => religionPopModifierTierTwoEnabled;
            set { if (Change(ref religionPopModifierTierTwoEnabled, value)) OnPropertyChanged(nameof(ReligionPopModifierTierTwoEditable)); }
        }
        [SyncHostOnly]
        public int ReligionPopModifierTierTwo
        {
            get => religionPopModifierTierTwo;
            set { if (Change(ref religionPopModifierTierTwo, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierTwoValueText)); }
        }
        public bool ReligionPopModifierTierTwoEditable => CanConfigure && EnableMod && ReligionPopModifierTierTwoEnabled;
        public string ReligionPopModifierTierTwoValueText
        {
            get => ReligionPopModifierTierTwo.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierTwo = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierTwoValueText)); }
        }
        public string ReligionPopModifierTierTwoTitle => SerpLocalization.Get("Popularity.ReligionPopModifierTierTwo");
        public string ReligionPopModifierTierTwoHelp => SerpLocalization.Get("Popularity.ReligionPopModifierTierTwo.Help");
        private bool religionPopModifierTierThreeEnabled;
        private int religionPopModifierTierThree = 6;
        [SyncHostOnly]
        public bool ReligionPopModifierTierThreeEnabled
        {
            get => religionPopModifierTierThreeEnabled;
            set { if (Change(ref religionPopModifierTierThreeEnabled, value)) OnPropertyChanged(nameof(ReligionPopModifierTierThreeEditable)); }
        }
        [SyncHostOnly]
        public int ReligionPopModifierTierThree
        {
            get => religionPopModifierTierThree;
            set { if (Change(ref religionPopModifierTierThree, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierThreeValueText)); }
        }
        public bool ReligionPopModifierTierThreeEditable => CanConfigure && EnableMod && ReligionPopModifierTierThreeEnabled;
        public string ReligionPopModifierTierThreeValueText
        {
            get => ReligionPopModifierTierThree.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierThree = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierThreeValueText)); }
        }
        public string ReligionPopModifierTierThreeTitle => SerpLocalization.Get("Popularity.ReligionPopModifierTierThree");
        public string ReligionPopModifierTierThreeHelp => SerpLocalization.Get("Popularity.ReligionPopModifierTierThree.Help");
        private bool religionPopModifierTierFourEnabled;
        private int religionPopModifierTierFour = 8;
        [SyncHostOnly]
        public bool ReligionPopModifierTierFourEnabled
        {
            get => religionPopModifierTierFourEnabled;
            set { if (Change(ref religionPopModifierTierFourEnabled, value)) OnPropertyChanged(nameof(ReligionPopModifierTierFourEditable)); }
        }
        [SyncHostOnly]
        public int ReligionPopModifierTierFour
        {
            get => religionPopModifierTierFour;
            set { if (Change(ref religionPopModifierTierFour, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierFourValueText)); }
        }
        public bool ReligionPopModifierTierFourEditable => CanConfigure && EnableMod && ReligionPopModifierTierFourEnabled;
        public string ReligionPopModifierTierFourValueText
        {
            get => ReligionPopModifierTierFour.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierFour = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierFourValueText)); }
        }
        public string ReligionPopModifierTierFourTitle => SerpLocalization.Get("Popularity.ReligionPopModifierTierFour");
        public string ReligionPopModifierTierFourHelp => SerpLocalization.Get("Popularity.ReligionPopModifierTierFour.Help");
        private bool churchPopModifierEnabled;
        private int churchPopModifier = 1;
        [SyncHostOnly]
        public bool ChurchPopModifierEnabled
        {
            get => churchPopModifierEnabled;
            set { if (Change(ref churchPopModifierEnabled, value)) OnPropertyChanged(nameof(ChurchPopModifierEditable)); }
        }
        [SyncHostOnly]
        public int ChurchPopModifier
        {
            get => churchPopModifier;
            set { if (Change(ref churchPopModifier, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ChurchPopModifierValueText)); }
        }
        public bool ChurchPopModifierEditable => CanConfigure && EnableMod && ChurchPopModifierEnabled;
        public string ChurchPopModifierValueText
        {
            get => ChurchPopModifier.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ChurchPopModifier = parsed; OnPropertyChanged(nameof(ChurchPopModifierValueText)); }
        }
        public string ChurchPopModifierTitle => SerpLocalization.Get("Popularity.ChurchPopModifier");
        public string ChurchPopModifierHelp => SerpLocalization.Get("Popularity.ChurchPopModifier.Help");
        private bool cathedralPopModifierEnabled;
        private int cathedralPopModifier = 2;
        [SyncHostOnly]
        public bool CathedralPopModifierEnabled
        {
            get => cathedralPopModifierEnabled;
            set { if (Change(ref cathedralPopModifierEnabled, value)) OnPropertyChanged(nameof(CathedralPopModifierEditable)); }
        }
        [SyncHostOnly]
        public int CathedralPopModifier
        {
            get => cathedralPopModifier;
            set { if (Change(ref cathedralPopModifier, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(CathedralPopModifierValueText)); }
        }
        public bool CathedralPopModifierEditable => CanConfigure && EnableMod && CathedralPopModifierEnabled;
        public string CathedralPopModifierValueText
        {
            get => CathedralPopModifier.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) CathedralPopModifier = parsed; OnPropertyChanged(nameof(CathedralPopModifierValueText)); }
        }
        public string CathedralPopModifierTitle => SerpLocalization.Get("Popularity.CathedralPopModifier");
        public string CathedralPopModifierHelp => SerpLocalization.Get("Popularity.CathedralPopModifier.Help");
        private bool alePopModifierTierOneEnabled;
        private int alePopModifierTierOne = 2;
        [SyncHostOnly]
        public bool AlePopModifierTierOneEnabled
        {
            get => alePopModifierTierOneEnabled;
            set { if (Change(ref alePopModifierTierOneEnabled, value)) OnPropertyChanged(nameof(AlePopModifierTierOneEditable)); }
        }
        [SyncHostOnly]
        public int AlePopModifierTierOne
        {
            get => alePopModifierTierOne;
            set { if (Change(ref alePopModifierTierOne, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierOneValueText)); }
        }
        public bool AlePopModifierTierOneEditable => CanConfigure && EnableMod && AlePopModifierTierOneEnabled;
        public string AlePopModifierTierOneValueText
        {
            get => AlePopModifierTierOne.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierOne = parsed; OnPropertyChanged(nameof(AlePopModifierTierOneValueText)); }
        }
        public string AlePopModifierTierOneTitle => SerpLocalization.Get("Popularity.AlePopModifierTierOne");
        public string AlePopModifierTierOneHelp => SerpLocalization.Get("Popularity.AlePopModifierTierOne.Help");
        private bool alePopModifierTierTwoEnabled;
        private int alePopModifierTierTwo = 4;
        [SyncHostOnly]
        public bool AlePopModifierTierTwoEnabled
        {
            get => alePopModifierTierTwoEnabled;
            set { if (Change(ref alePopModifierTierTwoEnabled, value)) OnPropertyChanged(nameof(AlePopModifierTierTwoEditable)); }
        }
        [SyncHostOnly]
        public int AlePopModifierTierTwo
        {
            get => alePopModifierTierTwo;
            set { if (Change(ref alePopModifierTierTwo, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierTwoValueText)); }
        }
        public bool AlePopModifierTierTwoEditable => CanConfigure && EnableMod && AlePopModifierTierTwoEnabled;
        public string AlePopModifierTierTwoValueText
        {
            get => AlePopModifierTierTwo.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierTwo = parsed; OnPropertyChanged(nameof(AlePopModifierTierTwoValueText)); }
        }
        public string AlePopModifierTierTwoTitle => SerpLocalization.Get("Popularity.AlePopModifierTierTwo");
        public string AlePopModifierTierTwoHelp => SerpLocalization.Get("Popularity.AlePopModifierTierTwo.Help");
        private bool alePopModifierTierThreeEnabled;
        private int alePopModifierTierThree = 6;
        [SyncHostOnly]
        public bool AlePopModifierTierThreeEnabled
        {
            get => alePopModifierTierThreeEnabled;
            set { if (Change(ref alePopModifierTierThreeEnabled, value)) OnPropertyChanged(nameof(AlePopModifierTierThreeEditable)); }
        }
        [SyncHostOnly]
        public int AlePopModifierTierThree
        {
            get => alePopModifierTierThree;
            set { if (Change(ref alePopModifierTierThree, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierThreeValueText)); }
        }
        public bool AlePopModifierTierThreeEditable => CanConfigure && EnableMod && AlePopModifierTierThreeEnabled;
        public string AlePopModifierTierThreeValueText
        {
            get => AlePopModifierTierThree.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierThree = parsed; OnPropertyChanged(nameof(AlePopModifierTierThreeValueText)); }
        }
        public string AlePopModifierTierThreeTitle => SerpLocalization.Get("Popularity.AlePopModifierTierThree");
        public string AlePopModifierTierThreeHelp => SerpLocalization.Get("Popularity.AlePopModifierTierThree.Help");
        private bool alePopModifierTierFourEnabled;
        private int alePopModifierTierFour = 8;
        [SyncHostOnly]
        public bool AlePopModifierTierFourEnabled
        {
            get => alePopModifierTierFourEnabled;
            set { if (Change(ref alePopModifierTierFourEnabled, value)) OnPropertyChanged(nameof(AlePopModifierTierFourEditable)); }
        }
        [SyncHostOnly]
        public int AlePopModifierTierFour
        {
            get => alePopModifierTierFour;
            set { if (Change(ref alePopModifierTierFour, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierFourValueText)); }
        }
        public bool AlePopModifierTierFourEditable => CanConfigure && EnableMod && AlePopModifierTierFourEnabled;
        public string AlePopModifierTierFourValueText
        {
            get => AlePopModifierTierFour.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierFour = parsed; OnPropertyChanged(nameof(AlePopModifierTierFourValueText)); }
        }
        public string AlePopModifierTierFourTitle => SerpLocalization.Get("Popularity.AlePopModifierTierFour");
        public string AlePopModifierTierFourHelp => SerpLocalization.Get("Popularity.AlePopModifierTierFour.Help");
        private bool fairsPopularityEventModifierEnabled;
        private int fairsPopularityEventModifier = 16;
        [SyncHostOnly]
        public bool FairsPopularityEventModifierEnabled
        {
            get => fairsPopularityEventModifierEnabled;
            set { if (Change(ref fairsPopularityEventModifierEnabled, value)) OnPropertyChanged(nameof(FairsPopularityEventModifierEditable)); }
        }
        [SyncHostOnly]
        public int FairsPopularityEventModifier
        {
            get => fairsPopularityEventModifier;
            set { if (Change(ref fairsPopularityEventModifier, Math.Max(-32, Math.Min(32, value)))) OnPropertyChanged(nameof(FairsPopularityEventModifierValueText)); }
        }
        public bool FairsPopularityEventModifierEditable => CanConfigure && EnableMod && FairsPopularityEventModifierEnabled;
        public string FairsPopularityEventModifierValueText
        {
            get => FairsPopularityEventModifier.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FairsPopularityEventModifier = parsed; OnPropertyChanged(nameof(FairsPopularityEventModifierValueText)); }
        }
        public string FairsPopularityEventModifierTitle => SerpLocalization.Get("Popularity.FairsPopularityEventModifier");
        public string FairsPopularityEventModifierHelp => SerpLocalization.Get("Popularity.FairsPopularityEventModifier.Help");
        private bool goodBadThingPopModifierMultiplierEnabled;
        private int goodBadThingPopModifierMultiplier = 1;
        [SyncHostOnly]
        public bool GoodBadThingPopModifierMultiplierEnabled
        {
            get => goodBadThingPopModifierMultiplierEnabled;
            set { if (Change(ref goodBadThingPopModifierMultiplierEnabled, value)) OnPropertyChanged(nameof(GoodBadThingPopModifierMultiplierEditable)); }
        }
        [SyncHostOnly]
        public int GoodBadThingPopModifierMultiplier
        {
            get => goodBadThingPopModifierMultiplier;
            set { if (Change(ref goodBadThingPopModifierMultiplier, Math.Max(-2, Math.Min(2, value)))) OnPropertyChanged(nameof(GoodBadThingPopModifierMultiplierValueText)); }
        }
        public bool GoodBadThingPopModifierMultiplierEditable => CanConfigure && EnableMod && GoodBadThingPopModifierMultiplierEnabled;
        public string GoodBadThingPopModifierMultiplierValueText
        {
            get => GoodBadThingPopModifierMultiplier.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) GoodBadThingPopModifierMultiplier = parsed; OnPropertyChanged(nameof(GoodBadThingPopModifierMultiplierValueText)); }
        }
        public string GoodBadThingPopModifierMultiplierTitle => SerpLocalization.Get("Popularity.GoodBadThingPopModifierMultiplier");
        public string GoodBadThingPopModifierMultiplierHelp => SerpLocalization.Get("Popularity.GoodBadThingPopModifierMultiplier.Help");
        public string RationsTitle => SerpLocalization.Get("Popularity.Rations");
        public string VarietyTitle => SerpLocalization.Get("Popularity.Variety");
        public string CrowdingTitle => SerpLocalization.Get("Popularity.Crowding");
        public string TaxesTitle => SerpLocalization.Get("Popularity.Taxes");
        public string ReligionTitle => SerpLocalization.Get("Popularity.Religion");
        public string AleTitle => SerpLocalization.Get("Popularity.Ale");
        public string FairsTitle => SerpLocalization.Get("Popularity.Fairs");
        public string FearTitle => SerpLocalization.Get("Popularity.Fear");
    }
}
