using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
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
            OnPropertyChanged(nameof(FearNegative1Editable));
            OnPropertyChanged(nameof(FearNegative2Editable));
            OnPropertyChanged(nameof(FearNegative3Editable));
            OnPropertyChanged(nameof(FearNegative4Editable));
            OnPropertyChanged(nameof(FearNegative5Editable));
            OnPropertyChanged(nameof(FearPositive1Editable));
            OnPropertyChanged(nameof(FearPositive2Editable));
            OnPropertyChanged(nameof(FearPositive3Editable));
            OnPropertyChanged(nameof(FearPositive4Editable));
            OnPropertyChanged(nameof(FearPositive5Editable));
        }
        private bool negativeFearEnabled;
        [SyncHostOnly]
        public bool NegativeFearEnabled
        {
            get => negativeFearEnabled;
            set { if (Change(ref negativeFearEnabled, value)) RefreshEditors(); }
        }
        public string NegativeFearTitle => SerpLocalization.Get("Popularity.NegativeFear");
        public string NegativeFearHelp => SerpLocalization.Get("Popularity.NegativeFear.Help");
        private bool positiveFearEnabled;
        [SyncHostOnly]
        public bool PositiveFearEnabled
        {
            get => positiveFearEnabled;
            set { if (Change(ref positiveFearEnabled, value)) RefreshEditors(); }
        }
        public string PositiveFearTitle => SerpLocalization.Get("Popularity.PositiveFear");
        public string PositiveFearHelp => SerpLocalization.Get("Popularity.PositiveFear.Help");
        // Capture at mission checkpoints; APIShared copies this table before
        // publishing it to simulation and report readers.
        internal Dictionary<int, int> CaptureFearContributions()
        {
            var values = new Dictionary<int, int>();
            if (!EnableMod) return values;
            if (NegativeFearEnabled)
            {
                values.Add(-1, FearNegative1);
                values.Add(-2, FearNegative2);
                values.Add(-3, FearNegative3);
                values.Add(-4, FearNegative4);
                values.Add(-5, FearNegative5);
            }
            if (PositiveFearEnabled)
            {
                values.Add(1, FearPositive1);
                values.Add(2, FearPositive2);
                values.Add(3, FearPositive3);
                values.Add(4, FearPositive4);
                values.Add(5, FearPositive5);
            }
            return values;
        }
        private bool rationsPopModifierNoneEnabled;
        [SyncHostOnly]
        public bool RationsPopModifierNoneEnabled
        {
            get => rationsPopModifierNoneEnabled;
            set { if (Change(ref rationsPopModifierNoneEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierNoneEditable)); }
        }
        private int rationsPopModifierNone = -8;
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
        [SyncHostOnly]
        public bool RationsPopModifierHalfEnabled
        {
            get => rationsPopModifierHalfEnabled;
            set { if (Change(ref rationsPopModifierHalfEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierHalfEditable)); }
        }
        private int rationsPopModifierHalf = -4;
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
        [SyncHostOnly]
        public bool RationsPopModifierFullEnabled
        {
            get => rationsPopModifierFullEnabled;
            set { if (Change(ref rationsPopModifierFullEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierFullEditable)); }
        }
        private int rationsPopModifierFull = 0;
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
        [SyncHostOnly]
        public bool RationsPopModifierExtraEnabled
        {
            get => rationsPopModifierExtraEnabled;
            set { if (Change(ref rationsPopModifierExtraEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierExtraEditable)); }
        }
        private int rationsPopModifierExtra = 4;
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
        [SyncHostOnly]
        public bool RationsPopModifierDoubleEnabled
        {
            get => rationsPopModifierDoubleEnabled;
            set { if (Change(ref rationsPopModifierDoubleEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierDoubleEditable)); }
        }
        private int rationsPopModifierDouble = 8;
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
        [SyncHostOnly]
        public bool RationsPopModifierNoPreferredFoodEnabled
        {
            get => rationsPopModifierNoPreferredFoodEnabled;
            set { if (Change(ref rationsPopModifierNoPreferredFoodEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierNoPreferredFoodEditable)); }
        }
        private int rationsPopModifierNoPreferredFood = -8;
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
        [SyncHostOnly]
        public bool RationsPopModifierNoPopulationEnabled
        {
            get => rationsPopModifierNoPopulationEnabled;
            set { if (Change(ref rationsPopModifierNoPopulationEnabled, value)) OnPropertyChanged(nameof(RationsPopModifierNoPopulationEditable)); }
        }
        private int rationsPopModifierNoPopulation = 8;
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
        [SyncHostOnly]
        public bool FoodVarietyPopModifierTwoTypesEnabled
        {
            get => foodVarietyPopModifierTwoTypesEnabled;
            set { if (Change(ref foodVarietyPopModifierTwoTypesEnabled, value)) OnPropertyChanged(nameof(FoodVarietyPopModifierTwoTypesEditable)); }
        }
        private int foodVarietyPopModifierTwoTypes = 1;
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
        [SyncHostOnly]
        public bool FoodVarietyPopModifierThreeTypesEnabled
        {
            get => foodVarietyPopModifierThreeTypesEnabled;
            set { if (Change(ref foodVarietyPopModifierThreeTypesEnabled, value)) OnPropertyChanged(nameof(FoodVarietyPopModifierThreeTypesEditable)); }
        }
        private int foodVarietyPopModifierThreeTypes = 2;
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
        [SyncHostOnly]
        public bool FoodVarietyPopModifierFourTypesEnabled
        {
            get => foodVarietyPopModifierFourTypesEnabled;
            set { if (Change(ref foodVarietyPopModifierFourTypesEnabled, value)) OnPropertyChanged(nameof(FoodVarietyPopModifierFourTypesEditable)); }
        }
        private int foodVarietyPopModifierFourTypes = 3;
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
        [SyncHostOnly]
        public bool OverpopulationPopModifierTierOneEnabled
        {
            get => overpopulationPopModifierTierOneEnabled;
            set { if (Change(ref overpopulationPopModifierTierOneEnabled, value)) OnPropertyChanged(nameof(OverpopulationPopModifierTierOneEditable)); }
        }
        private int overpopulationPopModifierTierOne = -2;
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
        [SyncHostOnly]
        public bool OverpopulationPopModifierTierTwoEnabled
        {
            get => overpopulationPopModifierTierTwoEnabled;
            set { if (Change(ref overpopulationPopModifierTierTwoEnabled, value)) OnPropertyChanged(nameof(OverpopulationPopModifierTierTwoEditable)); }
        }
        private int overpopulationPopModifierTierTwo = -4;
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
        [SyncHostOnly]
        public bool OverpopulationPopModifierTierThreeEnabled
        {
            get => overpopulationPopModifierTierThreeEnabled;
            set { if (Change(ref overpopulationPopModifierTierThreeEnabled, value)) OnPropertyChanged(nameof(OverpopulationPopModifierTierThreeEditable)); }
        }
        private int overpopulationPopModifierTierThree = -6;
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
        [SyncHostOnly]
        public bool OverpopulationPopModifierTierFourEnabled
        {
            get => overpopulationPopModifierTierFourEnabled;
            set { if (Change(ref overpopulationPopModifierTierFourEnabled, value)) OnPropertyChanged(nameof(OverpopulationPopModifierTierFourEditable)); }
        }
        private int overpopulationPopModifierTierFour = -8;
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
        [SyncHostOnly]
        public bool OverpopulationPopModifierTierFiveEnabled
        {
            get => overpopulationPopModifierTierFiveEnabled;
            set { if (Change(ref overpopulationPopModifierTierFiveEnabled, value)) OnPropertyChanged(nameof(OverpopulationPopModifierTierFiveEditable)); }
        }
        private int overpopulationPopModifierTierFive = -10;
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
        [SyncHostOnly]
        public bool TaxPopModifierHugeDonationEnabled
        {
            get => taxPopModifierHugeDonationEnabled;
            set { if (Change(ref taxPopModifierHugeDonationEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierHugeDonationEditable)); }
        }
        private int taxPopModifierHugeDonation = 7;
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
        [SyncHostOnly]
        public bool TaxPopModifierBigDonationEnabled
        {
            get => taxPopModifierBigDonationEnabled;
            set { if (Change(ref taxPopModifierBigDonationEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierBigDonationEditable)); }
        }
        private int taxPopModifierBigDonation = 5;
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
        [SyncHostOnly]
        public bool TaxPopModifierSmallDonationEnabled
        {
            get => taxPopModifierSmallDonationEnabled;
            set { if (Change(ref taxPopModifierSmallDonationEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierSmallDonationEditable)); }
        }
        private int taxPopModifierSmallDonation = 3;
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
        [SyncHostOnly]
        public bool TaxPopModifierNoneEnabled
        {
            get => taxPopModifierNoneEnabled;
            set { if (Change(ref taxPopModifierNoneEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierNoneEditable)); }
        }
        private int taxPopModifierNone = 1;
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
        [SyncHostOnly]
        public bool TaxPopModifierLowEnabled
        {
            get => taxPopModifierLowEnabled;
            set { if (Change(ref taxPopModifierLowEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierLowEditable)); }
        }
        private int taxPopModifierLow = -2;
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
        [SyncHostOnly]
        public bool TaxPopModifierModerateEnabled
        {
            get => taxPopModifierModerateEnabled;
            set { if (Change(ref taxPopModifierModerateEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierModerateEditable)); }
        }
        private int taxPopModifierModerate = -4;
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
        [SyncHostOnly]
        public bool TaxPopModifierHighEnabled
        {
            get => taxPopModifierHighEnabled;
            set { if (Change(ref taxPopModifierHighEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierHighEditable)); }
        }
        private int taxPopModifierHigh = -6;
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
        [SyncHostOnly]
        public bool TaxPopModifierMeanEnabled
        {
            get => taxPopModifierMeanEnabled;
            set { if (Change(ref taxPopModifierMeanEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierMeanEditable)); }
        }
        private int taxPopModifierMean = -8;
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
        [SyncHostOnly]
        public bool TaxPopModifierUsuriousEnabled
        {
            get => taxPopModifierUsuriousEnabled;
            set { if (Change(ref taxPopModifierUsuriousEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierUsuriousEditable)); }
        }
        private int taxPopModifierUsurious = -12;
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
        [SyncHostOnly]
        public bool TaxPopModifierCruelEnabled
        {
            get => taxPopModifierCruelEnabled;
            set { if (Change(ref taxPopModifierCruelEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierCruelEditable)); }
        }
        private int taxPopModifierCruel = -16;
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
        [SyncHostOnly]
        public bool TaxPopModifierMostCruelEnabled
        {
            get => taxPopModifierMostCruelEnabled;
            set { if (Change(ref taxPopModifierMostCruelEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierMostCruelEditable)); }
        }
        private int taxPopModifierMostCruel = -20;
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
        [SyncHostOnly]
        public bool TaxPopModifierCruellestEnabled
        {
            get => taxPopModifierCruellestEnabled;
            set { if (Change(ref taxPopModifierCruellestEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierCruellestEditable)); }
        }
        private int taxPopModifierCruellest = -24;
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
        [SyncHostOnly]
        public bool TaxPopModifierUnaffordableDonationEnabled
        {
            get => taxPopModifierUnaffordableDonationEnabled;
            set { if (Change(ref taxPopModifierUnaffordableDonationEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierUnaffordableDonationEditable)); }
        }
        private int taxPopModifierUnaffordableDonation = 1;
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
        [SyncHostOnly]
        public bool TaxPopModifierUnknownModeEnabled
        {
            get => taxPopModifierUnknownModeEnabled;
            set { if (Change(ref taxPopModifierUnknownModeEnabled, value)) OnPropertyChanged(nameof(TaxPopModifierUnknownModeEditable)); }
        }
        private int taxPopModifierUnknownMode = -24;
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
        [SyncHostOnly]
        public bool ReligionPopModifierTierOneEnabled
        {
            get => religionPopModifierTierOneEnabled;
            set { if (Change(ref religionPopModifierTierOneEnabled, value)) OnPropertyChanged(nameof(ReligionPopModifierTierOneEditable)); }
        }
        private int religionPopModifierTierOne = 2;
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
        [SyncHostOnly]
        public bool ReligionPopModifierTierTwoEnabled
        {
            get => religionPopModifierTierTwoEnabled;
            set { if (Change(ref religionPopModifierTierTwoEnabled, value)) OnPropertyChanged(nameof(ReligionPopModifierTierTwoEditable)); }
        }
        private int religionPopModifierTierTwo = 4;
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
        [SyncHostOnly]
        public bool ReligionPopModifierTierThreeEnabled
        {
            get => religionPopModifierTierThreeEnabled;
            set { if (Change(ref religionPopModifierTierThreeEnabled, value)) OnPropertyChanged(nameof(ReligionPopModifierTierThreeEditable)); }
        }
        private int religionPopModifierTierThree = 6;
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
        [SyncHostOnly]
        public bool ReligionPopModifierTierFourEnabled
        {
            get => religionPopModifierTierFourEnabled;
            set { if (Change(ref religionPopModifierTierFourEnabled, value)) OnPropertyChanged(nameof(ReligionPopModifierTierFourEditable)); }
        }
        private int religionPopModifierTierFour = 8;
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
        [SyncHostOnly]
        public bool ChurchPopModifierEnabled
        {
            get => churchPopModifierEnabled;
            set { if (Change(ref churchPopModifierEnabled, value)) OnPropertyChanged(nameof(ChurchPopModifierEditable)); }
        }
        private int churchPopModifier = 1;
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
        [SyncHostOnly]
        public bool CathedralPopModifierEnabled
        {
            get => cathedralPopModifierEnabled;
            set { if (Change(ref cathedralPopModifierEnabled, value)) OnPropertyChanged(nameof(CathedralPopModifierEditable)); }
        }
        private int cathedralPopModifier = 2;
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
        [SyncHostOnly]
        public bool AlePopModifierTierOneEnabled
        {
            get => alePopModifierTierOneEnabled;
            set { if (Change(ref alePopModifierTierOneEnabled, value)) OnPropertyChanged(nameof(AlePopModifierTierOneEditable)); }
        }
        private int alePopModifierTierOne = 2;
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
        [SyncHostOnly]
        public bool AlePopModifierTierTwoEnabled
        {
            get => alePopModifierTierTwoEnabled;
            set { if (Change(ref alePopModifierTierTwoEnabled, value)) OnPropertyChanged(nameof(AlePopModifierTierTwoEditable)); }
        }
        private int alePopModifierTierTwo = 4;
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
        [SyncHostOnly]
        public bool AlePopModifierTierThreeEnabled
        {
            get => alePopModifierTierThreeEnabled;
            set { if (Change(ref alePopModifierTierThreeEnabled, value)) OnPropertyChanged(nameof(AlePopModifierTierThreeEditable)); }
        }
        private int alePopModifierTierThree = 6;
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
        [SyncHostOnly]
        public bool AlePopModifierTierFourEnabled
        {
            get => alePopModifierTierFourEnabled;
            set { if (Change(ref alePopModifierTierFourEnabled, value)) OnPropertyChanged(nameof(AlePopModifierTierFourEditable)); }
        }
        private int alePopModifierTierFour = 8;
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
        [SyncHostOnly]
        public bool FairsPopularityEventModifierEnabled
        {
            get => fairsPopularityEventModifierEnabled;
            set { if (Change(ref fairsPopularityEventModifierEnabled, value)) OnPropertyChanged(nameof(FairsPopularityEventModifierEditable)); }
        }
        private int fairsPopularityEventModifier = 16;
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
        private int fearNegative1 = -1;
        [SyncHostOnly]
        public int FearNegative1
        {
            get => fearNegative1;
            set { if (Change(ref fearNegative1, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative1ValueText)); }
        }
        public bool FearNegative1Editable => CanConfigure && EnableMod && NegativeFearEnabled;
        public string FearNegative1ValueText
        {
            get => FearNegative1.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative1 = parsed; OnPropertyChanged(nameof(FearNegative1ValueText)); }
        }
        public string FearNegative1Title => SerpLocalization.Get("Popularity.FearNegative1");
        public string FearNegative1Help => SerpLocalization.Get("Popularity.FearNegative1.Help");
        private int fearNegative2 = -2;
        [SyncHostOnly]
        public int FearNegative2
        {
            get => fearNegative2;
            set { if (Change(ref fearNegative2, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative2ValueText)); }
        }
        public bool FearNegative2Editable => CanConfigure && EnableMod && NegativeFearEnabled;
        public string FearNegative2ValueText
        {
            get => FearNegative2.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative2 = parsed; OnPropertyChanged(nameof(FearNegative2ValueText)); }
        }
        public string FearNegative2Title => SerpLocalization.Get("Popularity.FearNegative2");
        public string FearNegative2Help => SerpLocalization.Get("Popularity.FearNegative2.Help");
        private int fearNegative3 = -3;
        [SyncHostOnly]
        public int FearNegative3
        {
            get => fearNegative3;
            set { if (Change(ref fearNegative3, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative3ValueText)); }
        }
        public bool FearNegative3Editable => CanConfigure && EnableMod && NegativeFearEnabled;
        public string FearNegative3ValueText
        {
            get => FearNegative3.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative3 = parsed; OnPropertyChanged(nameof(FearNegative3ValueText)); }
        }
        public string FearNegative3Title => SerpLocalization.Get("Popularity.FearNegative3");
        public string FearNegative3Help => SerpLocalization.Get("Popularity.FearNegative3.Help");
        private int fearNegative4 = -4;
        [SyncHostOnly]
        public int FearNegative4
        {
            get => fearNegative4;
            set { if (Change(ref fearNegative4, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative4ValueText)); }
        }
        public bool FearNegative4Editable => CanConfigure && EnableMod && NegativeFearEnabled;
        public string FearNegative4ValueText
        {
            get => FearNegative4.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative4 = parsed; OnPropertyChanged(nameof(FearNegative4ValueText)); }
        }
        public string FearNegative4Title => SerpLocalization.Get("Popularity.FearNegative4");
        public string FearNegative4Help => SerpLocalization.Get("Popularity.FearNegative4.Help");
        private int fearNegative5 = -5;
        [SyncHostOnly]
        public int FearNegative5
        {
            get => fearNegative5;
            set { if (Change(ref fearNegative5, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative5ValueText)); }
        }
        public bool FearNegative5Editable => CanConfigure && EnableMod && NegativeFearEnabled;
        public string FearNegative5ValueText
        {
            get => FearNegative5.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative5 = parsed; OnPropertyChanged(nameof(FearNegative5ValueText)); }
        }
        public string FearNegative5Title => SerpLocalization.Get("Popularity.FearNegative5");
        public string FearNegative5Help => SerpLocalization.Get("Popularity.FearNegative5.Help");
        private int fearPositive1 = 1;
        [SyncHostOnly]
        public int FearPositive1
        {
            get => fearPositive1;
            set { if (Change(ref fearPositive1, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive1ValueText)); }
        }
        public bool FearPositive1Editable => CanConfigure && EnableMod && PositiveFearEnabled;
        public string FearPositive1ValueText
        {
            get => FearPositive1.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive1 = parsed; OnPropertyChanged(nameof(FearPositive1ValueText)); }
        }
        public string FearPositive1Title => SerpLocalization.Get("Popularity.FearPositive1");
        public string FearPositive1Help => SerpLocalization.Get("Popularity.FearPositive1.Help");
        private int fearPositive2 = 2;
        [SyncHostOnly]
        public int FearPositive2
        {
            get => fearPositive2;
            set { if (Change(ref fearPositive2, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive2ValueText)); }
        }
        public bool FearPositive2Editable => CanConfigure && EnableMod && PositiveFearEnabled;
        public string FearPositive2ValueText
        {
            get => FearPositive2.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive2 = parsed; OnPropertyChanged(nameof(FearPositive2ValueText)); }
        }
        public string FearPositive2Title => SerpLocalization.Get("Popularity.FearPositive2");
        public string FearPositive2Help => SerpLocalization.Get("Popularity.FearPositive2.Help");
        private int fearPositive3 = 3;
        [SyncHostOnly]
        public int FearPositive3
        {
            get => fearPositive3;
            set { if (Change(ref fearPositive3, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive3ValueText)); }
        }
        public bool FearPositive3Editable => CanConfigure && EnableMod && PositiveFearEnabled;
        public string FearPositive3ValueText
        {
            get => FearPositive3.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive3 = parsed; OnPropertyChanged(nameof(FearPositive3ValueText)); }
        }
        public string FearPositive3Title => SerpLocalization.Get("Popularity.FearPositive3");
        public string FearPositive3Help => SerpLocalization.Get("Popularity.FearPositive3.Help");
        private int fearPositive4 = 4;
        [SyncHostOnly]
        public int FearPositive4
        {
            get => fearPositive4;
            set { if (Change(ref fearPositive4, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive4ValueText)); }
        }
        public bool FearPositive4Editable => CanConfigure && EnableMod && PositiveFearEnabled;
        public string FearPositive4ValueText
        {
            get => FearPositive4.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive4 = parsed; OnPropertyChanged(nameof(FearPositive4ValueText)); }
        }
        public string FearPositive4Title => SerpLocalization.Get("Popularity.FearPositive4");
        public string FearPositive4Help => SerpLocalization.Get("Popularity.FearPositive4.Help");
        private int fearPositive5 = 5;
        [SyncHostOnly]
        public int FearPositive5
        {
            get => fearPositive5;
            set { if (Change(ref fearPositive5, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive5ValueText)); }
        }
        public bool FearPositive5Editable => CanConfigure && EnableMod && PositiveFearEnabled;
        public string FearPositive5ValueText
        {
            get => FearPositive5.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive5 = parsed; OnPropertyChanged(nameof(FearPositive5ValueText)); }
        }
        public string FearPositive5Title => SerpLocalization.Get("Popularity.FearPositive5");
        public string FearPositive5Help => SerpLocalization.Get("Popularity.FearPositive5.Help");
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
