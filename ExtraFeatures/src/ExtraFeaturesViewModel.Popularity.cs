using APIShared.Economy;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesViewModel
    {
        private volatile bool missionLocked;
        private readonly SynchronizationContext uiContext = SynchronizationContext.Current;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;
        private void InitializePopularitySettings()
        {
            PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CanEditHostSettings) || e.PropertyName == nameof(EnableMod)) RefreshPopularityEditors(); };
        }
        public bool PopularityBetweenMissions => !missionLocked;
        public bool CanConfigurePopularity => PopularityBetweenMissions && CanEditHostSettings;
        public string PopularityScopeText => SerpLocalization.Get("ExtraFeatures.Popularity.Scope");
        public string PopularityRuleText => SerpLocalization.Get("ExtraFeatures.Popularity.Rule");
        internal void SetPopularityMissionLocked(bool locked)
        {
            missionLocked = locked;
            if (Thread.CurrentThread.ManagedThreadId == uiThread) RefreshPopularityEditors();
            else if (uiContext != null) uiContext.Post(_ => RefreshPopularityEditors(), null);
        }
        private bool ChangePopularity<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (!PopularityBetweenMissions || !CanMutateSetting(name) || Equals(field, value)) return false;
            field = value; OnPropertyChanged(name); return true;
        }
        private void RefreshPopularityEditors()
        {
            OnPropertyChanged(nameof(PopularityBetweenMissions)); OnPropertyChanged(nameof(CanConfigurePopularity));
            OnPropertyChanged(nameof(RationsPopModifierNoneHumanEditable));
            OnPropertyChanged(nameof(RationsPopModifierNoneAIEditable));
            OnPropertyChanged(nameof(RationsPopModifierHalfHumanEditable));
            OnPropertyChanged(nameof(RationsPopModifierHalfAIEditable));
            OnPropertyChanged(nameof(RationsPopModifierFullHumanEditable));
            OnPropertyChanged(nameof(RationsPopModifierFullAIEditable));
            OnPropertyChanged(nameof(RationsPopModifierExtraHumanEditable));
            OnPropertyChanged(nameof(RationsPopModifierExtraAIEditable));
            OnPropertyChanged(nameof(RationsPopModifierDoubleHumanEditable));
            OnPropertyChanged(nameof(RationsPopModifierDoubleAIEditable));
            OnPropertyChanged(nameof(RationsPopModifierNoPreferredFoodHumanEditable));
            OnPropertyChanged(nameof(RationsPopModifierNoPreferredFoodAIEditable));
            OnPropertyChanged(nameof(RationsPopModifierNoPopulationHumanEditable));
            OnPropertyChanged(nameof(RationsPopModifierNoPopulationAIEditable));
            OnPropertyChanged(nameof(FoodVarietyPopModifierTwoTypesHumanEditable));
            OnPropertyChanged(nameof(FoodVarietyPopModifierTwoTypesAIEditable));
            OnPropertyChanged(nameof(FoodVarietyPopModifierThreeTypesHumanEditable));
            OnPropertyChanged(nameof(FoodVarietyPopModifierThreeTypesAIEditable));
            OnPropertyChanged(nameof(FoodVarietyPopModifierFourTypesHumanEditable));
            OnPropertyChanged(nameof(FoodVarietyPopModifierFourTypesAIEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierOneHumanEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierOneAIEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierTwoHumanEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierTwoAIEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierThreeHumanEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierThreeAIEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierFourHumanEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierFourAIEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierFiveHumanEditable));
            OnPropertyChanged(nameof(OverpopulationPopModifierTierFiveAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierHugeDonationHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierHugeDonationAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierBigDonationHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierBigDonationAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierSmallDonationHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierSmallDonationAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierNoneHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierNoneAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierLowHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierLowAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierModerateHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierModerateAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierHighHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierHighAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierMeanHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierMeanAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierUsuriousHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierUsuriousAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierCruelHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierCruelAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierMostCruelHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierMostCruelAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierCruellestHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierCruellestAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierUnaffordableDonationHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierUnaffordableDonationAIEditable));
            OnPropertyChanged(nameof(TaxPopModifierUnknownModeHumanEditable));
            OnPropertyChanged(nameof(TaxPopModifierUnknownModeAIEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierOneHumanEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierOneAIEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierTwoHumanEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierTwoAIEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierThreeHumanEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierThreeAIEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierFourHumanEditable));
            OnPropertyChanged(nameof(ReligionPopModifierTierFourAIEditable));
            OnPropertyChanged(nameof(ChurchPopModifierHumanEditable));
            OnPropertyChanged(nameof(ChurchPopModifierAIEditable));
            OnPropertyChanged(nameof(CathedralPopModifierHumanEditable));
            OnPropertyChanged(nameof(CathedralPopModifierAIEditable));
            OnPropertyChanged(nameof(AlePopModifierTierOneHumanEditable));
            OnPropertyChanged(nameof(AlePopModifierTierOneAIEditable));
            OnPropertyChanged(nameof(AlePopModifierTierTwoHumanEditable));
            OnPropertyChanged(nameof(AlePopModifierTierTwoAIEditable));
            OnPropertyChanged(nameof(AlePopModifierTierThreeHumanEditable));
            OnPropertyChanged(nameof(AlePopModifierTierThreeAIEditable));
            OnPropertyChanged(nameof(AlePopModifierTierFourHumanEditable));
            OnPropertyChanged(nameof(AlePopModifierTierFourAIEditable));
            OnPropertyChanged(nameof(FairsPopularityEventModifierHumanEditable));
            OnPropertyChanged(nameof(FairsPopularityEventModifierAIEditable));
            OnPropertyChanged(nameof(FearNegative1HumanEditable));
            OnPropertyChanged(nameof(FearNegative1AIEditable));
            OnPropertyChanged(nameof(FearNegative2HumanEditable));
            OnPropertyChanged(nameof(FearNegative2AIEditable));
            OnPropertyChanged(nameof(FearNegative3HumanEditable));
            OnPropertyChanged(nameof(FearNegative3AIEditable));
            OnPropertyChanged(nameof(FearNegative4HumanEditable));
            OnPropertyChanged(nameof(FearNegative4AIEditable));
            OnPropertyChanged(nameof(FearNegative5HumanEditable));
            OnPropertyChanged(nameof(FearNegative5AIEditable));
            OnPropertyChanged(nameof(FearPositive1HumanEditable));
            OnPropertyChanged(nameof(FearPositive1AIEditable));
            OnPropertyChanged(nameof(FearPositive2HumanEditable));
            OnPropertyChanged(nameof(FearPositive2AIEditable));
            OnPropertyChanged(nameof(FearPositive3HumanEditable));
            OnPropertyChanged(nameof(FearPositive3AIEditable));
            OnPropertyChanged(nameof(FearPositive4HumanEditable));
            OnPropertyChanged(nameof(FearPositive4AIEditable));
            OnPropertyChanged(nameof(FearPositive5HumanEditable));
            OnPropertyChanged(nameof(FearPositive5AIEditable));
        }
        private bool rationsHumanEnabled;
        [SyncHostOnly]
        public bool RationsHumanEnabled
        {
            get => rationsHumanEnabled;
            set { if (ChangePopularity(ref rationsHumanEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool rationsAIEnabled;
        [SyncHostOnly]
        public bool RationsAIEnabled
        {
            get => rationsAIEnabled;
            set { if (ChangePopularity(ref rationsAIEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool varietyHumanEnabled;
        [SyncHostOnly]
        public bool VarietyHumanEnabled
        {
            get => varietyHumanEnabled;
            set { if (ChangePopularity(ref varietyHumanEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool varietyAIEnabled;
        [SyncHostOnly]
        public bool VarietyAIEnabled
        {
            get => varietyAIEnabled;
            set { if (ChangePopularity(ref varietyAIEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool crowdingHumanEnabled;
        [SyncHostOnly]
        public bool CrowdingHumanEnabled
        {
            get => crowdingHumanEnabled;
            set { if (ChangePopularity(ref crowdingHumanEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool crowdingAIEnabled;
        [SyncHostOnly]
        public bool CrowdingAIEnabled
        {
            get => crowdingAIEnabled;
            set { if (ChangePopularity(ref crowdingAIEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool taxesHumanEnabled;
        [SyncHostOnly]
        public bool TaxesHumanEnabled
        {
            get => taxesHumanEnabled;
            set { if (ChangePopularity(ref taxesHumanEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool taxesAIEnabled;
        [SyncHostOnly]
        public bool TaxesAIEnabled
        {
            get => taxesAIEnabled;
            set { if (ChangePopularity(ref taxesAIEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool religionHumanEnabled;
        [SyncHostOnly]
        public bool ReligionHumanEnabled
        {
            get => religionHumanEnabled;
            set { if (ChangePopularity(ref religionHumanEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool religionAIEnabled;
        [SyncHostOnly]
        public bool ReligionAIEnabled
        {
            get => religionAIEnabled;
            set { if (ChangePopularity(ref religionAIEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool aleHumanEnabled;
        [SyncHostOnly]
        public bool AleHumanEnabled
        {
            get => aleHumanEnabled;
            set { if (ChangePopularity(ref aleHumanEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool aleAIEnabled;
        [SyncHostOnly]
        public bool AleAIEnabled
        {
            get => aleAIEnabled;
            set { if (ChangePopularity(ref aleAIEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool fairsHumanEnabled;
        [SyncHostOnly]
        public bool FairsHumanEnabled
        {
            get => fairsHumanEnabled;
            set { if (ChangePopularity(ref fairsHumanEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool fairsAIEnabled;
        [SyncHostOnly]
        public bool FairsAIEnabled
        {
            get => fairsAIEnabled;
            set { if (ChangePopularity(ref fairsAIEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool negativeFearHumanEnabled;
        [SyncHostOnly]
        public bool NegativeFearHumanEnabled
        {
            get => negativeFearHumanEnabled;
            set { if (ChangePopularity(ref negativeFearHumanEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool negativeFearAIEnabled;
        [SyncHostOnly]
        public bool NegativeFearAIEnabled
        {
            get => negativeFearAIEnabled;
            set { if (ChangePopularity(ref negativeFearAIEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool positiveFearHumanEnabled;
        [SyncHostOnly]
        public bool PositiveFearHumanEnabled
        {
            get => positiveFearHumanEnabled;
            set { if (ChangePopularity(ref positiveFearHumanEnabled, value)) RefreshPopularityEditors(); }
        }
        private bool positiveFearAIEnabled;
        [SyncHostOnly]
        public bool PositiveFearAIEnabled
        {
            get => positiveFearAIEnabled;
            set { if (ChangePopularity(ref positiveFearAIEnabled, value)) RefreshPopularityEditors(); }
        }
        private int rationsPopModifierNoneHuman = -8;
        [SyncHostOnly]
        public int RationsPopModifierNoneHuman
        {
            get => rationsPopModifierNoneHuman;
            set { if (ChangePopularity(ref rationsPopModifierNoneHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierNoneHumanValueText)); }
        }
        public bool RationsPopModifierNoneHumanEditable => CanConfigurePopularity && EnableMod && RationsHumanEnabled;
        public string RationsPopModifierNoneHumanValueText
        {
            get => RationsPopModifierNoneHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierNoneHuman = parsed; OnPropertyChanged(nameof(RationsPopModifierNoneHumanValueText)); }
        }
        private int rationsPopModifierNoneAI = -8;
        [SyncHostOnly]
        public int RationsPopModifierNoneAI
        {
            get => rationsPopModifierNoneAI;
            set { if (ChangePopularity(ref rationsPopModifierNoneAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierNoneAIValueText)); }
        }
        public bool RationsPopModifierNoneAIEditable => CanConfigurePopularity && EnableMod && RationsAIEnabled;
        public string RationsPopModifierNoneAIValueText
        {
            get => RationsPopModifierNoneAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierNoneAI = parsed; OnPropertyChanged(nameof(RationsPopModifierNoneAIValueText)); }
        }
        private int rationsPopModifierHalfHuman = -4;
        [SyncHostOnly]
        public int RationsPopModifierHalfHuman
        {
            get => rationsPopModifierHalfHuman;
            set { if (ChangePopularity(ref rationsPopModifierHalfHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierHalfHumanValueText)); }
        }
        public bool RationsPopModifierHalfHumanEditable => CanConfigurePopularity && EnableMod && RationsHumanEnabled;
        public string RationsPopModifierHalfHumanValueText
        {
            get => RationsPopModifierHalfHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierHalfHuman = parsed; OnPropertyChanged(nameof(RationsPopModifierHalfHumanValueText)); }
        }
        private int rationsPopModifierHalfAI = -4;
        [SyncHostOnly]
        public int RationsPopModifierHalfAI
        {
            get => rationsPopModifierHalfAI;
            set { if (ChangePopularity(ref rationsPopModifierHalfAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierHalfAIValueText)); }
        }
        public bool RationsPopModifierHalfAIEditable => CanConfigurePopularity && EnableMod && RationsAIEnabled;
        public string RationsPopModifierHalfAIValueText
        {
            get => RationsPopModifierHalfAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierHalfAI = parsed; OnPropertyChanged(nameof(RationsPopModifierHalfAIValueText)); }
        }
        private int rationsPopModifierFullHuman = 0;
        [SyncHostOnly]
        public int RationsPopModifierFullHuman
        {
            get => rationsPopModifierFullHuman;
            set { if (ChangePopularity(ref rationsPopModifierFullHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierFullHumanValueText)); }
        }
        public bool RationsPopModifierFullHumanEditable => CanConfigurePopularity && EnableMod && RationsHumanEnabled;
        public string RationsPopModifierFullHumanValueText
        {
            get => RationsPopModifierFullHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierFullHuman = parsed; OnPropertyChanged(nameof(RationsPopModifierFullHumanValueText)); }
        }
        private int rationsPopModifierFullAI = 0;
        [SyncHostOnly]
        public int RationsPopModifierFullAI
        {
            get => rationsPopModifierFullAI;
            set { if (ChangePopularity(ref rationsPopModifierFullAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierFullAIValueText)); }
        }
        public bool RationsPopModifierFullAIEditable => CanConfigurePopularity && EnableMod && RationsAIEnabled;
        public string RationsPopModifierFullAIValueText
        {
            get => RationsPopModifierFullAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierFullAI = parsed; OnPropertyChanged(nameof(RationsPopModifierFullAIValueText)); }
        }
        private int rationsPopModifierExtraHuman = 4;
        [SyncHostOnly]
        public int RationsPopModifierExtraHuman
        {
            get => rationsPopModifierExtraHuman;
            set { if (ChangePopularity(ref rationsPopModifierExtraHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierExtraHumanValueText)); }
        }
        public bool RationsPopModifierExtraHumanEditable => CanConfigurePopularity && EnableMod && RationsHumanEnabled;
        public string RationsPopModifierExtraHumanValueText
        {
            get => RationsPopModifierExtraHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierExtraHuman = parsed; OnPropertyChanged(nameof(RationsPopModifierExtraHumanValueText)); }
        }
        private int rationsPopModifierExtraAI = 4;
        [SyncHostOnly]
        public int RationsPopModifierExtraAI
        {
            get => rationsPopModifierExtraAI;
            set { if (ChangePopularity(ref rationsPopModifierExtraAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierExtraAIValueText)); }
        }
        public bool RationsPopModifierExtraAIEditable => CanConfigurePopularity && EnableMod && RationsAIEnabled;
        public string RationsPopModifierExtraAIValueText
        {
            get => RationsPopModifierExtraAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierExtraAI = parsed; OnPropertyChanged(nameof(RationsPopModifierExtraAIValueText)); }
        }
        private int rationsPopModifierDoubleHuman = 8;
        [SyncHostOnly]
        public int RationsPopModifierDoubleHuman
        {
            get => rationsPopModifierDoubleHuman;
            set { if (ChangePopularity(ref rationsPopModifierDoubleHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierDoubleHumanValueText)); }
        }
        public bool RationsPopModifierDoubleHumanEditable => CanConfigurePopularity && EnableMod && RationsHumanEnabled;
        public string RationsPopModifierDoubleHumanValueText
        {
            get => RationsPopModifierDoubleHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierDoubleHuman = parsed; OnPropertyChanged(nameof(RationsPopModifierDoubleHumanValueText)); }
        }
        private int rationsPopModifierDoubleAI = 8;
        [SyncHostOnly]
        public int RationsPopModifierDoubleAI
        {
            get => rationsPopModifierDoubleAI;
            set { if (ChangePopularity(ref rationsPopModifierDoubleAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierDoubleAIValueText)); }
        }
        public bool RationsPopModifierDoubleAIEditable => CanConfigurePopularity && EnableMod && RationsAIEnabled;
        public string RationsPopModifierDoubleAIValueText
        {
            get => RationsPopModifierDoubleAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierDoubleAI = parsed; OnPropertyChanged(nameof(RationsPopModifierDoubleAIValueText)); }
        }
        private int rationsPopModifierNoPreferredFoodHuman = -8;
        [SyncHostOnly]
        public int RationsPopModifierNoPreferredFoodHuman
        {
            get => rationsPopModifierNoPreferredFoodHuman;
            set { if (ChangePopularity(ref rationsPopModifierNoPreferredFoodHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierNoPreferredFoodHumanValueText)); }
        }
        public bool RationsPopModifierNoPreferredFoodHumanEditable => CanConfigurePopularity && EnableMod && RationsHumanEnabled;
        public string RationsPopModifierNoPreferredFoodHumanValueText
        {
            get => RationsPopModifierNoPreferredFoodHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierNoPreferredFoodHuman = parsed; OnPropertyChanged(nameof(RationsPopModifierNoPreferredFoodHumanValueText)); }
        }
        private int rationsPopModifierNoPreferredFoodAI = -8;
        [SyncHostOnly]
        public int RationsPopModifierNoPreferredFoodAI
        {
            get => rationsPopModifierNoPreferredFoodAI;
            set { if (ChangePopularity(ref rationsPopModifierNoPreferredFoodAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierNoPreferredFoodAIValueText)); }
        }
        public bool RationsPopModifierNoPreferredFoodAIEditable => CanConfigurePopularity && EnableMod && RationsAIEnabled;
        public string RationsPopModifierNoPreferredFoodAIValueText
        {
            get => RationsPopModifierNoPreferredFoodAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierNoPreferredFoodAI = parsed; OnPropertyChanged(nameof(RationsPopModifierNoPreferredFoodAIValueText)); }
        }
        private int rationsPopModifierNoPopulationHuman = 8;
        [SyncHostOnly]
        public int RationsPopModifierNoPopulationHuman
        {
            get => rationsPopModifierNoPopulationHuman;
            set { if (ChangePopularity(ref rationsPopModifierNoPopulationHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierNoPopulationHumanValueText)); }
        }
        public bool RationsPopModifierNoPopulationHumanEditable => CanConfigurePopularity && EnableMod && RationsHumanEnabled;
        public string RationsPopModifierNoPopulationHumanValueText
        {
            get => RationsPopModifierNoPopulationHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierNoPopulationHuman = parsed; OnPropertyChanged(nameof(RationsPopModifierNoPopulationHumanValueText)); }
        }
        private int rationsPopModifierNoPopulationAI = 8;
        [SyncHostOnly]
        public int RationsPopModifierNoPopulationAI
        {
            get => rationsPopModifierNoPopulationAI;
            set { if (ChangePopularity(ref rationsPopModifierNoPopulationAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(RationsPopModifierNoPopulationAIValueText)); }
        }
        public bool RationsPopModifierNoPopulationAIEditable => CanConfigurePopularity && EnableMod && RationsAIEnabled;
        public string RationsPopModifierNoPopulationAIValueText
        {
            get => RationsPopModifierNoPopulationAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) RationsPopModifierNoPopulationAI = parsed; OnPropertyChanged(nameof(RationsPopModifierNoPopulationAIValueText)); }
        }
        private int foodVarietyPopModifierTwoTypesHuman = 1;
        [SyncHostOnly]
        public int FoodVarietyPopModifierTwoTypesHuman
        {
            get => foodVarietyPopModifierTwoTypesHuman;
            set { if (ChangePopularity(ref foodVarietyPopModifierTwoTypesHuman, Math.Max(-6, Math.Min(6, value)))) OnPropertyChanged(nameof(FoodVarietyPopModifierTwoTypesHumanValueText)); }
        }
        public bool FoodVarietyPopModifierTwoTypesHumanEditable => CanConfigurePopularity && EnableMod && VarietyHumanEnabled;
        public string FoodVarietyPopModifierTwoTypesHumanValueText
        {
            get => FoodVarietyPopModifierTwoTypesHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FoodVarietyPopModifierTwoTypesHuman = parsed; OnPropertyChanged(nameof(FoodVarietyPopModifierTwoTypesHumanValueText)); }
        }
        private int foodVarietyPopModifierTwoTypesAI = 1;
        [SyncHostOnly]
        public int FoodVarietyPopModifierTwoTypesAI
        {
            get => foodVarietyPopModifierTwoTypesAI;
            set { if (ChangePopularity(ref foodVarietyPopModifierTwoTypesAI, Math.Max(-6, Math.Min(6, value)))) OnPropertyChanged(nameof(FoodVarietyPopModifierTwoTypesAIValueText)); }
        }
        public bool FoodVarietyPopModifierTwoTypesAIEditable => CanConfigurePopularity && EnableMod && VarietyAIEnabled;
        public string FoodVarietyPopModifierTwoTypesAIValueText
        {
            get => FoodVarietyPopModifierTwoTypesAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FoodVarietyPopModifierTwoTypesAI = parsed; OnPropertyChanged(nameof(FoodVarietyPopModifierTwoTypesAIValueText)); }
        }
        private int foodVarietyPopModifierThreeTypesHuman = 2;
        [SyncHostOnly]
        public int FoodVarietyPopModifierThreeTypesHuman
        {
            get => foodVarietyPopModifierThreeTypesHuman;
            set { if (ChangePopularity(ref foodVarietyPopModifierThreeTypesHuman, Math.Max(-6, Math.Min(6, value)))) OnPropertyChanged(nameof(FoodVarietyPopModifierThreeTypesHumanValueText)); }
        }
        public bool FoodVarietyPopModifierThreeTypesHumanEditable => CanConfigurePopularity && EnableMod && VarietyHumanEnabled;
        public string FoodVarietyPopModifierThreeTypesHumanValueText
        {
            get => FoodVarietyPopModifierThreeTypesHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FoodVarietyPopModifierThreeTypesHuman = parsed; OnPropertyChanged(nameof(FoodVarietyPopModifierThreeTypesHumanValueText)); }
        }
        private int foodVarietyPopModifierThreeTypesAI = 2;
        [SyncHostOnly]
        public int FoodVarietyPopModifierThreeTypesAI
        {
            get => foodVarietyPopModifierThreeTypesAI;
            set { if (ChangePopularity(ref foodVarietyPopModifierThreeTypesAI, Math.Max(-6, Math.Min(6, value)))) OnPropertyChanged(nameof(FoodVarietyPopModifierThreeTypesAIValueText)); }
        }
        public bool FoodVarietyPopModifierThreeTypesAIEditable => CanConfigurePopularity && EnableMod && VarietyAIEnabled;
        public string FoodVarietyPopModifierThreeTypesAIValueText
        {
            get => FoodVarietyPopModifierThreeTypesAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FoodVarietyPopModifierThreeTypesAI = parsed; OnPropertyChanged(nameof(FoodVarietyPopModifierThreeTypesAIValueText)); }
        }
        private int foodVarietyPopModifierFourTypesHuman = 3;
        [SyncHostOnly]
        public int FoodVarietyPopModifierFourTypesHuman
        {
            get => foodVarietyPopModifierFourTypesHuman;
            set { if (ChangePopularity(ref foodVarietyPopModifierFourTypesHuman, Math.Max(-6, Math.Min(6, value)))) OnPropertyChanged(nameof(FoodVarietyPopModifierFourTypesHumanValueText)); }
        }
        public bool FoodVarietyPopModifierFourTypesHumanEditable => CanConfigurePopularity && EnableMod && VarietyHumanEnabled;
        public string FoodVarietyPopModifierFourTypesHumanValueText
        {
            get => FoodVarietyPopModifierFourTypesHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FoodVarietyPopModifierFourTypesHuman = parsed; OnPropertyChanged(nameof(FoodVarietyPopModifierFourTypesHumanValueText)); }
        }
        private int foodVarietyPopModifierFourTypesAI = 3;
        [SyncHostOnly]
        public int FoodVarietyPopModifierFourTypesAI
        {
            get => foodVarietyPopModifierFourTypesAI;
            set { if (ChangePopularity(ref foodVarietyPopModifierFourTypesAI, Math.Max(-6, Math.Min(6, value)))) OnPropertyChanged(nameof(FoodVarietyPopModifierFourTypesAIValueText)); }
        }
        public bool FoodVarietyPopModifierFourTypesAIEditable => CanConfigurePopularity && EnableMod && VarietyAIEnabled;
        public string FoodVarietyPopModifierFourTypesAIValueText
        {
            get => FoodVarietyPopModifierFourTypesAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FoodVarietyPopModifierFourTypesAI = parsed; OnPropertyChanged(nameof(FoodVarietyPopModifierFourTypesAIValueText)); }
        }
        private int overpopulationPopModifierTierOneHuman = -2;
        [SyncHostOnly]
        public int OverpopulationPopModifierTierOneHuman
        {
            get => overpopulationPopModifierTierOneHuman;
            set { if (ChangePopularity(ref overpopulationPopModifierTierOneHuman, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierOneHumanValueText)); }
        }
        public bool OverpopulationPopModifierTierOneHumanEditable => CanConfigurePopularity && EnableMod && CrowdingHumanEnabled;
        public string OverpopulationPopModifierTierOneHumanValueText
        {
            get => OverpopulationPopModifierTierOneHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierOneHuman = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierOneHumanValueText)); }
        }
        private int overpopulationPopModifierTierOneAI = -2;
        [SyncHostOnly]
        public int OverpopulationPopModifierTierOneAI
        {
            get => overpopulationPopModifierTierOneAI;
            set { if (ChangePopularity(ref overpopulationPopModifierTierOneAI, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierOneAIValueText)); }
        }
        public bool OverpopulationPopModifierTierOneAIEditable => CanConfigurePopularity && EnableMod && CrowdingAIEnabled;
        public string OverpopulationPopModifierTierOneAIValueText
        {
            get => OverpopulationPopModifierTierOneAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierOneAI = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierOneAIValueText)); }
        }
        private int overpopulationPopModifierTierTwoHuman = -4;
        [SyncHostOnly]
        public int OverpopulationPopModifierTierTwoHuman
        {
            get => overpopulationPopModifierTierTwoHuman;
            set { if (ChangePopularity(ref overpopulationPopModifierTierTwoHuman, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierTwoHumanValueText)); }
        }
        public bool OverpopulationPopModifierTierTwoHumanEditable => CanConfigurePopularity && EnableMod && CrowdingHumanEnabled;
        public string OverpopulationPopModifierTierTwoHumanValueText
        {
            get => OverpopulationPopModifierTierTwoHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierTwoHuman = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierTwoHumanValueText)); }
        }
        private int overpopulationPopModifierTierTwoAI = -4;
        [SyncHostOnly]
        public int OverpopulationPopModifierTierTwoAI
        {
            get => overpopulationPopModifierTierTwoAI;
            set { if (ChangePopularity(ref overpopulationPopModifierTierTwoAI, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierTwoAIValueText)); }
        }
        public bool OverpopulationPopModifierTierTwoAIEditable => CanConfigurePopularity && EnableMod && CrowdingAIEnabled;
        public string OverpopulationPopModifierTierTwoAIValueText
        {
            get => OverpopulationPopModifierTierTwoAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierTwoAI = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierTwoAIValueText)); }
        }
        private int overpopulationPopModifierTierThreeHuman = -6;
        [SyncHostOnly]
        public int OverpopulationPopModifierTierThreeHuman
        {
            get => overpopulationPopModifierTierThreeHuman;
            set { if (ChangePopularity(ref overpopulationPopModifierTierThreeHuman, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierThreeHumanValueText)); }
        }
        public bool OverpopulationPopModifierTierThreeHumanEditable => CanConfigurePopularity && EnableMod && CrowdingHumanEnabled;
        public string OverpopulationPopModifierTierThreeHumanValueText
        {
            get => OverpopulationPopModifierTierThreeHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierThreeHuman = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierThreeHumanValueText)); }
        }
        private int overpopulationPopModifierTierThreeAI = -6;
        [SyncHostOnly]
        public int OverpopulationPopModifierTierThreeAI
        {
            get => overpopulationPopModifierTierThreeAI;
            set { if (ChangePopularity(ref overpopulationPopModifierTierThreeAI, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierThreeAIValueText)); }
        }
        public bool OverpopulationPopModifierTierThreeAIEditable => CanConfigurePopularity && EnableMod && CrowdingAIEnabled;
        public string OverpopulationPopModifierTierThreeAIValueText
        {
            get => OverpopulationPopModifierTierThreeAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierThreeAI = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierThreeAIValueText)); }
        }
        private int overpopulationPopModifierTierFourHuman = -8;
        [SyncHostOnly]
        public int OverpopulationPopModifierTierFourHuman
        {
            get => overpopulationPopModifierTierFourHuman;
            set { if (ChangePopularity(ref overpopulationPopModifierTierFourHuman, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierFourHumanValueText)); }
        }
        public bool OverpopulationPopModifierTierFourHumanEditable => CanConfigurePopularity && EnableMod && CrowdingHumanEnabled;
        public string OverpopulationPopModifierTierFourHumanValueText
        {
            get => OverpopulationPopModifierTierFourHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierFourHuman = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierFourHumanValueText)); }
        }
        private int overpopulationPopModifierTierFourAI = -8;
        [SyncHostOnly]
        public int OverpopulationPopModifierTierFourAI
        {
            get => overpopulationPopModifierTierFourAI;
            set { if (ChangePopularity(ref overpopulationPopModifierTierFourAI, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierFourAIValueText)); }
        }
        public bool OverpopulationPopModifierTierFourAIEditable => CanConfigurePopularity && EnableMod && CrowdingAIEnabled;
        public string OverpopulationPopModifierTierFourAIValueText
        {
            get => OverpopulationPopModifierTierFourAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierFourAI = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierFourAIValueText)); }
        }
        private int overpopulationPopModifierTierFiveHuman = -10;
        [SyncHostOnly]
        public int OverpopulationPopModifierTierFiveHuman
        {
            get => overpopulationPopModifierTierFiveHuman;
            set { if (ChangePopularity(ref overpopulationPopModifierTierFiveHuman, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierFiveHumanValueText)); }
        }
        public bool OverpopulationPopModifierTierFiveHumanEditable => CanConfigurePopularity && EnableMod && CrowdingHumanEnabled;
        public string OverpopulationPopModifierTierFiveHumanValueText
        {
            get => OverpopulationPopModifierTierFiveHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierFiveHuman = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierFiveHumanValueText)); }
        }
        private int overpopulationPopModifierTierFiveAI = -10;
        [SyncHostOnly]
        public int OverpopulationPopModifierTierFiveAI
        {
            get => overpopulationPopModifierTierFiveAI;
            set { if (ChangePopularity(ref overpopulationPopModifierTierFiveAI, Math.Max(-20, Math.Min(20, value)))) OnPropertyChanged(nameof(OverpopulationPopModifierTierFiveAIValueText)); }
        }
        public bool OverpopulationPopModifierTierFiveAIEditable => CanConfigurePopularity && EnableMod && CrowdingAIEnabled;
        public string OverpopulationPopModifierTierFiveAIValueText
        {
            get => OverpopulationPopModifierTierFiveAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) OverpopulationPopModifierTierFiveAI = parsed; OnPropertyChanged(nameof(OverpopulationPopModifierTierFiveAIValueText)); }
        }
        private int taxPopModifierHugeDonationHuman = 7;
        [SyncHostOnly]
        public int TaxPopModifierHugeDonationHuman
        {
            get => taxPopModifierHugeDonationHuman;
            set { if (ChangePopularity(ref taxPopModifierHugeDonationHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierHugeDonationHumanValueText)); }
        }
        public bool TaxPopModifierHugeDonationHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierHugeDonationHumanValueText
        {
            get => TaxPopModifierHugeDonationHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierHugeDonationHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierHugeDonationHumanValueText)); }
        }
        private int taxPopModifierHugeDonationAI = 7;
        [SyncHostOnly]
        public int TaxPopModifierHugeDonationAI
        {
            get => taxPopModifierHugeDonationAI;
            set { if (ChangePopularity(ref taxPopModifierHugeDonationAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierHugeDonationAIValueText)); }
        }
        public bool TaxPopModifierHugeDonationAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierHugeDonationAIValueText
        {
            get => TaxPopModifierHugeDonationAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierHugeDonationAI = parsed; OnPropertyChanged(nameof(TaxPopModifierHugeDonationAIValueText)); }
        }
        private int taxPopModifierBigDonationHuman = 5;
        [SyncHostOnly]
        public int TaxPopModifierBigDonationHuman
        {
            get => taxPopModifierBigDonationHuman;
            set { if (ChangePopularity(ref taxPopModifierBigDonationHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierBigDonationHumanValueText)); }
        }
        public bool TaxPopModifierBigDonationHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierBigDonationHumanValueText
        {
            get => TaxPopModifierBigDonationHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierBigDonationHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierBigDonationHumanValueText)); }
        }
        private int taxPopModifierBigDonationAI = 5;
        [SyncHostOnly]
        public int TaxPopModifierBigDonationAI
        {
            get => taxPopModifierBigDonationAI;
            set { if (ChangePopularity(ref taxPopModifierBigDonationAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierBigDonationAIValueText)); }
        }
        public bool TaxPopModifierBigDonationAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierBigDonationAIValueText
        {
            get => TaxPopModifierBigDonationAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierBigDonationAI = parsed; OnPropertyChanged(nameof(TaxPopModifierBigDonationAIValueText)); }
        }
        private int taxPopModifierSmallDonationHuman = 3;
        [SyncHostOnly]
        public int TaxPopModifierSmallDonationHuman
        {
            get => taxPopModifierSmallDonationHuman;
            set { if (ChangePopularity(ref taxPopModifierSmallDonationHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierSmallDonationHumanValueText)); }
        }
        public bool TaxPopModifierSmallDonationHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierSmallDonationHumanValueText
        {
            get => TaxPopModifierSmallDonationHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierSmallDonationHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierSmallDonationHumanValueText)); }
        }
        private int taxPopModifierSmallDonationAI = 3;
        [SyncHostOnly]
        public int TaxPopModifierSmallDonationAI
        {
            get => taxPopModifierSmallDonationAI;
            set { if (ChangePopularity(ref taxPopModifierSmallDonationAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierSmallDonationAIValueText)); }
        }
        public bool TaxPopModifierSmallDonationAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierSmallDonationAIValueText
        {
            get => TaxPopModifierSmallDonationAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierSmallDonationAI = parsed; OnPropertyChanged(nameof(TaxPopModifierSmallDonationAIValueText)); }
        }
        private int taxPopModifierNoneHuman = 1;
        [SyncHostOnly]
        public int TaxPopModifierNoneHuman
        {
            get => taxPopModifierNoneHuman;
            set { if (ChangePopularity(ref taxPopModifierNoneHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierNoneHumanValueText)); }
        }
        public bool TaxPopModifierNoneHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierNoneHumanValueText
        {
            get => TaxPopModifierNoneHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierNoneHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierNoneHumanValueText)); }
        }
        private int taxPopModifierNoneAI = 1;
        [SyncHostOnly]
        public int TaxPopModifierNoneAI
        {
            get => taxPopModifierNoneAI;
            set { if (ChangePopularity(ref taxPopModifierNoneAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierNoneAIValueText)); }
        }
        public bool TaxPopModifierNoneAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierNoneAIValueText
        {
            get => TaxPopModifierNoneAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierNoneAI = parsed; OnPropertyChanged(nameof(TaxPopModifierNoneAIValueText)); }
        }
        private int taxPopModifierLowHuman = -2;
        [SyncHostOnly]
        public int TaxPopModifierLowHuman
        {
            get => taxPopModifierLowHuman;
            set { if (ChangePopularity(ref taxPopModifierLowHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierLowHumanValueText)); }
        }
        public bool TaxPopModifierLowHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierLowHumanValueText
        {
            get => TaxPopModifierLowHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierLowHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierLowHumanValueText)); }
        }
        private int taxPopModifierLowAI = -2;
        [SyncHostOnly]
        public int TaxPopModifierLowAI
        {
            get => taxPopModifierLowAI;
            set { if (ChangePopularity(ref taxPopModifierLowAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierLowAIValueText)); }
        }
        public bool TaxPopModifierLowAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierLowAIValueText
        {
            get => TaxPopModifierLowAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierLowAI = parsed; OnPropertyChanged(nameof(TaxPopModifierLowAIValueText)); }
        }
        private int taxPopModifierModerateHuman = -4;
        [SyncHostOnly]
        public int TaxPopModifierModerateHuman
        {
            get => taxPopModifierModerateHuman;
            set { if (ChangePopularity(ref taxPopModifierModerateHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierModerateHumanValueText)); }
        }
        public bool TaxPopModifierModerateHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierModerateHumanValueText
        {
            get => TaxPopModifierModerateHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierModerateHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierModerateHumanValueText)); }
        }
        private int taxPopModifierModerateAI = -4;
        [SyncHostOnly]
        public int TaxPopModifierModerateAI
        {
            get => taxPopModifierModerateAI;
            set { if (ChangePopularity(ref taxPopModifierModerateAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierModerateAIValueText)); }
        }
        public bool TaxPopModifierModerateAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierModerateAIValueText
        {
            get => TaxPopModifierModerateAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierModerateAI = parsed; OnPropertyChanged(nameof(TaxPopModifierModerateAIValueText)); }
        }
        private int taxPopModifierHighHuman = -6;
        [SyncHostOnly]
        public int TaxPopModifierHighHuman
        {
            get => taxPopModifierHighHuman;
            set { if (ChangePopularity(ref taxPopModifierHighHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierHighHumanValueText)); }
        }
        public bool TaxPopModifierHighHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierHighHumanValueText
        {
            get => TaxPopModifierHighHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierHighHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierHighHumanValueText)); }
        }
        private int taxPopModifierHighAI = -6;
        [SyncHostOnly]
        public int TaxPopModifierHighAI
        {
            get => taxPopModifierHighAI;
            set { if (ChangePopularity(ref taxPopModifierHighAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierHighAIValueText)); }
        }
        public bool TaxPopModifierHighAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierHighAIValueText
        {
            get => TaxPopModifierHighAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierHighAI = parsed; OnPropertyChanged(nameof(TaxPopModifierHighAIValueText)); }
        }
        private int taxPopModifierMeanHuman = -8;
        [SyncHostOnly]
        public int TaxPopModifierMeanHuman
        {
            get => taxPopModifierMeanHuman;
            set { if (ChangePopularity(ref taxPopModifierMeanHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierMeanHumanValueText)); }
        }
        public bool TaxPopModifierMeanHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierMeanHumanValueText
        {
            get => TaxPopModifierMeanHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierMeanHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierMeanHumanValueText)); }
        }
        private int taxPopModifierMeanAI = -8;
        [SyncHostOnly]
        public int TaxPopModifierMeanAI
        {
            get => taxPopModifierMeanAI;
            set { if (ChangePopularity(ref taxPopModifierMeanAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierMeanAIValueText)); }
        }
        public bool TaxPopModifierMeanAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierMeanAIValueText
        {
            get => TaxPopModifierMeanAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierMeanAI = parsed; OnPropertyChanged(nameof(TaxPopModifierMeanAIValueText)); }
        }
        private int taxPopModifierUsuriousHuman = -12;
        [SyncHostOnly]
        public int TaxPopModifierUsuriousHuman
        {
            get => taxPopModifierUsuriousHuman;
            set { if (ChangePopularity(ref taxPopModifierUsuriousHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierUsuriousHumanValueText)); }
        }
        public bool TaxPopModifierUsuriousHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierUsuriousHumanValueText
        {
            get => TaxPopModifierUsuriousHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierUsuriousHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierUsuriousHumanValueText)); }
        }
        private int taxPopModifierUsuriousAI = -12;
        [SyncHostOnly]
        public int TaxPopModifierUsuriousAI
        {
            get => taxPopModifierUsuriousAI;
            set { if (ChangePopularity(ref taxPopModifierUsuriousAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierUsuriousAIValueText)); }
        }
        public bool TaxPopModifierUsuriousAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierUsuriousAIValueText
        {
            get => TaxPopModifierUsuriousAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierUsuriousAI = parsed; OnPropertyChanged(nameof(TaxPopModifierUsuriousAIValueText)); }
        }
        private int taxPopModifierCruelHuman = -16;
        [SyncHostOnly]
        public int TaxPopModifierCruelHuman
        {
            get => taxPopModifierCruelHuman;
            set { if (ChangePopularity(ref taxPopModifierCruelHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierCruelHumanValueText)); }
        }
        public bool TaxPopModifierCruelHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierCruelHumanValueText
        {
            get => TaxPopModifierCruelHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierCruelHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierCruelHumanValueText)); }
        }
        private int taxPopModifierCruelAI = -16;
        [SyncHostOnly]
        public int TaxPopModifierCruelAI
        {
            get => taxPopModifierCruelAI;
            set { if (ChangePopularity(ref taxPopModifierCruelAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierCruelAIValueText)); }
        }
        public bool TaxPopModifierCruelAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierCruelAIValueText
        {
            get => TaxPopModifierCruelAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierCruelAI = parsed; OnPropertyChanged(nameof(TaxPopModifierCruelAIValueText)); }
        }
        private int taxPopModifierMostCruelHuman = -20;
        [SyncHostOnly]
        public int TaxPopModifierMostCruelHuman
        {
            get => taxPopModifierMostCruelHuman;
            set { if (ChangePopularity(ref taxPopModifierMostCruelHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierMostCruelHumanValueText)); }
        }
        public bool TaxPopModifierMostCruelHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierMostCruelHumanValueText
        {
            get => TaxPopModifierMostCruelHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierMostCruelHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierMostCruelHumanValueText)); }
        }
        private int taxPopModifierMostCruelAI = -20;
        [SyncHostOnly]
        public int TaxPopModifierMostCruelAI
        {
            get => taxPopModifierMostCruelAI;
            set { if (ChangePopularity(ref taxPopModifierMostCruelAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierMostCruelAIValueText)); }
        }
        public bool TaxPopModifierMostCruelAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierMostCruelAIValueText
        {
            get => TaxPopModifierMostCruelAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierMostCruelAI = parsed; OnPropertyChanged(nameof(TaxPopModifierMostCruelAIValueText)); }
        }
        private int taxPopModifierCruellestHuman = -24;
        [SyncHostOnly]
        public int TaxPopModifierCruellestHuman
        {
            get => taxPopModifierCruellestHuman;
            set { if (ChangePopularity(ref taxPopModifierCruellestHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierCruellestHumanValueText)); }
        }
        public bool TaxPopModifierCruellestHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierCruellestHumanValueText
        {
            get => TaxPopModifierCruellestHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierCruellestHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierCruellestHumanValueText)); }
        }
        private int taxPopModifierCruellestAI = -24;
        [SyncHostOnly]
        public int TaxPopModifierCruellestAI
        {
            get => taxPopModifierCruellestAI;
            set { if (ChangePopularity(ref taxPopModifierCruellestAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierCruellestAIValueText)); }
        }
        public bool TaxPopModifierCruellestAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierCruellestAIValueText
        {
            get => TaxPopModifierCruellestAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierCruellestAI = parsed; OnPropertyChanged(nameof(TaxPopModifierCruellestAIValueText)); }
        }
        private int taxPopModifierUnaffordableDonationHuman = 1;
        [SyncHostOnly]
        public int TaxPopModifierUnaffordableDonationHuman
        {
            get => taxPopModifierUnaffordableDonationHuman;
            set { if (ChangePopularity(ref taxPopModifierUnaffordableDonationHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierUnaffordableDonationHumanValueText)); }
        }
        public bool TaxPopModifierUnaffordableDonationHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierUnaffordableDonationHumanValueText
        {
            get => TaxPopModifierUnaffordableDonationHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierUnaffordableDonationHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierUnaffordableDonationHumanValueText)); }
        }
        private int taxPopModifierUnaffordableDonationAI = 1;
        [SyncHostOnly]
        public int TaxPopModifierUnaffordableDonationAI
        {
            get => taxPopModifierUnaffordableDonationAI;
            set { if (ChangePopularity(ref taxPopModifierUnaffordableDonationAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierUnaffordableDonationAIValueText)); }
        }
        public bool TaxPopModifierUnaffordableDonationAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierUnaffordableDonationAIValueText
        {
            get => TaxPopModifierUnaffordableDonationAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierUnaffordableDonationAI = parsed; OnPropertyChanged(nameof(TaxPopModifierUnaffordableDonationAIValueText)); }
        }
        private int taxPopModifierUnknownModeHuman = -24;
        [SyncHostOnly]
        public int TaxPopModifierUnknownModeHuman
        {
            get => taxPopModifierUnknownModeHuman;
            set { if (ChangePopularity(ref taxPopModifierUnknownModeHuman, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierUnknownModeHumanValueText)); }
        }
        public bool TaxPopModifierUnknownModeHumanEditable => CanConfigurePopularity && EnableMod && TaxesHumanEnabled;
        public string TaxPopModifierUnknownModeHumanValueText
        {
            get => TaxPopModifierUnknownModeHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierUnknownModeHuman = parsed; OnPropertyChanged(nameof(TaxPopModifierUnknownModeHumanValueText)); }
        }
        private int taxPopModifierUnknownModeAI = -24;
        [SyncHostOnly]
        public int TaxPopModifierUnknownModeAI
        {
            get => taxPopModifierUnknownModeAI;
            set { if (ChangePopularity(ref taxPopModifierUnknownModeAI, Math.Max(-48, Math.Min(48, value)))) OnPropertyChanged(nameof(TaxPopModifierUnknownModeAIValueText)); }
        }
        public bool TaxPopModifierUnknownModeAIEditable => CanConfigurePopularity && EnableMod && TaxesAIEnabled;
        public string TaxPopModifierUnknownModeAIValueText
        {
            get => TaxPopModifierUnknownModeAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) TaxPopModifierUnknownModeAI = parsed; OnPropertyChanged(nameof(TaxPopModifierUnknownModeAIValueText)); }
        }
        private int religionPopModifierTierOneHuman = 2;
        [SyncHostOnly]
        public int ReligionPopModifierTierOneHuman
        {
            get => religionPopModifierTierOneHuman;
            set { if (ChangePopularity(ref religionPopModifierTierOneHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierOneHumanValueText)); }
        }
        public bool ReligionPopModifierTierOneHumanEditable => CanConfigurePopularity && EnableMod && ReligionHumanEnabled;
        public string ReligionPopModifierTierOneHumanValueText
        {
            get => ReligionPopModifierTierOneHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierOneHuman = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierOneHumanValueText)); }
        }
        private int religionPopModifierTierOneAI = 2;
        [SyncHostOnly]
        public int ReligionPopModifierTierOneAI
        {
            get => religionPopModifierTierOneAI;
            set { if (ChangePopularity(ref religionPopModifierTierOneAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierOneAIValueText)); }
        }
        public bool ReligionPopModifierTierOneAIEditable => CanConfigurePopularity && EnableMod && ReligionAIEnabled;
        public string ReligionPopModifierTierOneAIValueText
        {
            get => ReligionPopModifierTierOneAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierOneAI = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierOneAIValueText)); }
        }
        private int religionPopModifierTierTwoHuman = 4;
        [SyncHostOnly]
        public int ReligionPopModifierTierTwoHuman
        {
            get => religionPopModifierTierTwoHuman;
            set { if (ChangePopularity(ref religionPopModifierTierTwoHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierTwoHumanValueText)); }
        }
        public bool ReligionPopModifierTierTwoHumanEditable => CanConfigurePopularity && EnableMod && ReligionHumanEnabled;
        public string ReligionPopModifierTierTwoHumanValueText
        {
            get => ReligionPopModifierTierTwoHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierTwoHuman = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierTwoHumanValueText)); }
        }
        private int religionPopModifierTierTwoAI = 4;
        [SyncHostOnly]
        public int ReligionPopModifierTierTwoAI
        {
            get => religionPopModifierTierTwoAI;
            set { if (ChangePopularity(ref religionPopModifierTierTwoAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierTwoAIValueText)); }
        }
        public bool ReligionPopModifierTierTwoAIEditable => CanConfigurePopularity && EnableMod && ReligionAIEnabled;
        public string ReligionPopModifierTierTwoAIValueText
        {
            get => ReligionPopModifierTierTwoAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierTwoAI = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierTwoAIValueText)); }
        }
        private int religionPopModifierTierThreeHuman = 6;
        [SyncHostOnly]
        public int ReligionPopModifierTierThreeHuman
        {
            get => religionPopModifierTierThreeHuman;
            set { if (ChangePopularity(ref religionPopModifierTierThreeHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierThreeHumanValueText)); }
        }
        public bool ReligionPopModifierTierThreeHumanEditable => CanConfigurePopularity && EnableMod && ReligionHumanEnabled;
        public string ReligionPopModifierTierThreeHumanValueText
        {
            get => ReligionPopModifierTierThreeHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierThreeHuman = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierThreeHumanValueText)); }
        }
        private int religionPopModifierTierThreeAI = 6;
        [SyncHostOnly]
        public int ReligionPopModifierTierThreeAI
        {
            get => religionPopModifierTierThreeAI;
            set { if (ChangePopularity(ref religionPopModifierTierThreeAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierThreeAIValueText)); }
        }
        public bool ReligionPopModifierTierThreeAIEditable => CanConfigurePopularity && EnableMod && ReligionAIEnabled;
        public string ReligionPopModifierTierThreeAIValueText
        {
            get => ReligionPopModifierTierThreeAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierThreeAI = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierThreeAIValueText)); }
        }
        private int religionPopModifierTierFourHuman = 8;
        [SyncHostOnly]
        public int ReligionPopModifierTierFourHuman
        {
            get => religionPopModifierTierFourHuman;
            set { if (ChangePopularity(ref religionPopModifierTierFourHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierFourHumanValueText)); }
        }
        public bool ReligionPopModifierTierFourHumanEditable => CanConfigurePopularity && EnableMod && ReligionHumanEnabled;
        public string ReligionPopModifierTierFourHumanValueText
        {
            get => ReligionPopModifierTierFourHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierFourHuman = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierFourHumanValueText)); }
        }
        private int religionPopModifierTierFourAI = 8;
        [SyncHostOnly]
        public int ReligionPopModifierTierFourAI
        {
            get => religionPopModifierTierFourAI;
            set { if (ChangePopularity(ref religionPopModifierTierFourAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ReligionPopModifierTierFourAIValueText)); }
        }
        public bool ReligionPopModifierTierFourAIEditable => CanConfigurePopularity && EnableMod && ReligionAIEnabled;
        public string ReligionPopModifierTierFourAIValueText
        {
            get => ReligionPopModifierTierFourAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ReligionPopModifierTierFourAI = parsed; OnPropertyChanged(nameof(ReligionPopModifierTierFourAIValueText)); }
        }
        private int churchPopModifierHuman = 1;
        [SyncHostOnly]
        public int ChurchPopModifierHuman
        {
            get => churchPopModifierHuman;
            set { if (ChangePopularity(ref churchPopModifierHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ChurchPopModifierHumanValueText)); }
        }
        public bool ChurchPopModifierHumanEditable => CanConfigurePopularity && EnableMod && ReligionHumanEnabled;
        public string ChurchPopModifierHumanValueText
        {
            get => ChurchPopModifierHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ChurchPopModifierHuman = parsed; OnPropertyChanged(nameof(ChurchPopModifierHumanValueText)); }
        }
        private int churchPopModifierAI = 1;
        [SyncHostOnly]
        public int ChurchPopModifierAI
        {
            get => churchPopModifierAI;
            set { if (ChangePopularity(ref churchPopModifierAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(ChurchPopModifierAIValueText)); }
        }
        public bool ChurchPopModifierAIEditable => CanConfigurePopularity && EnableMod && ReligionAIEnabled;
        public string ChurchPopModifierAIValueText
        {
            get => ChurchPopModifierAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) ChurchPopModifierAI = parsed; OnPropertyChanged(nameof(ChurchPopModifierAIValueText)); }
        }
        private int cathedralPopModifierHuman = 2;
        [SyncHostOnly]
        public int CathedralPopModifierHuman
        {
            get => cathedralPopModifierHuman;
            set { if (ChangePopularity(ref cathedralPopModifierHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(CathedralPopModifierHumanValueText)); }
        }
        public bool CathedralPopModifierHumanEditable => CanConfigurePopularity && EnableMod && ReligionHumanEnabled;
        public string CathedralPopModifierHumanValueText
        {
            get => CathedralPopModifierHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) CathedralPopModifierHuman = parsed; OnPropertyChanged(nameof(CathedralPopModifierHumanValueText)); }
        }
        private int cathedralPopModifierAI = 2;
        [SyncHostOnly]
        public int CathedralPopModifierAI
        {
            get => cathedralPopModifierAI;
            set { if (ChangePopularity(ref cathedralPopModifierAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(CathedralPopModifierAIValueText)); }
        }
        public bool CathedralPopModifierAIEditable => CanConfigurePopularity && EnableMod && ReligionAIEnabled;
        public string CathedralPopModifierAIValueText
        {
            get => CathedralPopModifierAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) CathedralPopModifierAI = parsed; OnPropertyChanged(nameof(CathedralPopModifierAIValueText)); }
        }
        private int alePopModifierTierOneHuman = 2;
        [SyncHostOnly]
        public int AlePopModifierTierOneHuman
        {
            get => alePopModifierTierOneHuman;
            set { if (ChangePopularity(ref alePopModifierTierOneHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierOneHumanValueText)); }
        }
        public bool AlePopModifierTierOneHumanEditable => CanConfigurePopularity && EnableMod && AleHumanEnabled;
        public string AlePopModifierTierOneHumanValueText
        {
            get => AlePopModifierTierOneHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierOneHuman = parsed; OnPropertyChanged(nameof(AlePopModifierTierOneHumanValueText)); }
        }
        private int alePopModifierTierOneAI = 2;
        [SyncHostOnly]
        public int AlePopModifierTierOneAI
        {
            get => alePopModifierTierOneAI;
            set { if (ChangePopularity(ref alePopModifierTierOneAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierOneAIValueText)); }
        }
        public bool AlePopModifierTierOneAIEditable => CanConfigurePopularity && EnableMod && AleAIEnabled;
        public string AlePopModifierTierOneAIValueText
        {
            get => AlePopModifierTierOneAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierOneAI = parsed; OnPropertyChanged(nameof(AlePopModifierTierOneAIValueText)); }
        }
        private int alePopModifierTierTwoHuman = 4;
        [SyncHostOnly]
        public int AlePopModifierTierTwoHuman
        {
            get => alePopModifierTierTwoHuman;
            set { if (ChangePopularity(ref alePopModifierTierTwoHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierTwoHumanValueText)); }
        }
        public bool AlePopModifierTierTwoHumanEditable => CanConfigurePopularity && EnableMod && AleHumanEnabled;
        public string AlePopModifierTierTwoHumanValueText
        {
            get => AlePopModifierTierTwoHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierTwoHuman = parsed; OnPropertyChanged(nameof(AlePopModifierTierTwoHumanValueText)); }
        }
        private int alePopModifierTierTwoAI = 4;
        [SyncHostOnly]
        public int AlePopModifierTierTwoAI
        {
            get => alePopModifierTierTwoAI;
            set { if (ChangePopularity(ref alePopModifierTierTwoAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierTwoAIValueText)); }
        }
        public bool AlePopModifierTierTwoAIEditable => CanConfigurePopularity && EnableMod && AleAIEnabled;
        public string AlePopModifierTierTwoAIValueText
        {
            get => AlePopModifierTierTwoAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierTwoAI = parsed; OnPropertyChanged(nameof(AlePopModifierTierTwoAIValueText)); }
        }
        private int alePopModifierTierThreeHuman = 6;
        [SyncHostOnly]
        public int AlePopModifierTierThreeHuman
        {
            get => alePopModifierTierThreeHuman;
            set { if (ChangePopularity(ref alePopModifierTierThreeHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierThreeHumanValueText)); }
        }
        public bool AlePopModifierTierThreeHumanEditable => CanConfigurePopularity && EnableMod && AleHumanEnabled;
        public string AlePopModifierTierThreeHumanValueText
        {
            get => AlePopModifierTierThreeHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierThreeHuman = parsed; OnPropertyChanged(nameof(AlePopModifierTierThreeHumanValueText)); }
        }
        private int alePopModifierTierThreeAI = 6;
        [SyncHostOnly]
        public int AlePopModifierTierThreeAI
        {
            get => alePopModifierTierThreeAI;
            set { if (ChangePopularity(ref alePopModifierTierThreeAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierThreeAIValueText)); }
        }
        public bool AlePopModifierTierThreeAIEditable => CanConfigurePopularity && EnableMod && AleAIEnabled;
        public string AlePopModifierTierThreeAIValueText
        {
            get => AlePopModifierTierThreeAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierThreeAI = parsed; OnPropertyChanged(nameof(AlePopModifierTierThreeAIValueText)); }
        }
        private int alePopModifierTierFourHuman = 8;
        [SyncHostOnly]
        public int AlePopModifierTierFourHuman
        {
            get => alePopModifierTierFourHuman;
            set { if (ChangePopularity(ref alePopModifierTierFourHuman, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierFourHumanValueText)); }
        }
        public bool AlePopModifierTierFourHumanEditable => CanConfigurePopularity && EnableMod && AleHumanEnabled;
        public string AlePopModifierTierFourHumanValueText
        {
            get => AlePopModifierTierFourHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierFourHuman = parsed; OnPropertyChanged(nameof(AlePopModifierTierFourHumanValueText)); }
        }
        private int alePopModifierTierFourAI = 8;
        [SyncHostOnly]
        public int AlePopModifierTierFourAI
        {
            get => alePopModifierTierFourAI;
            set { if (ChangePopularity(ref alePopModifierTierFourAI, Math.Max(-16, Math.Min(16, value)))) OnPropertyChanged(nameof(AlePopModifierTierFourAIValueText)); }
        }
        public bool AlePopModifierTierFourAIEditable => CanConfigurePopularity && EnableMod && AleAIEnabled;
        public string AlePopModifierTierFourAIValueText
        {
            get => AlePopModifierTierFourAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) AlePopModifierTierFourAI = parsed; OnPropertyChanged(nameof(AlePopModifierTierFourAIValueText)); }
        }
        private int fairsPopularityEventModifierHuman = 16;
        [SyncHostOnly]
        public int FairsPopularityEventModifierHuman
        {
            get => fairsPopularityEventModifierHuman;
            set { if (ChangePopularity(ref fairsPopularityEventModifierHuman, Math.Max(-32, Math.Min(32, value)))) OnPropertyChanged(nameof(FairsPopularityEventModifierHumanValueText)); }
        }
        public bool FairsPopularityEventModifierHumanEditable => CanConfigurePopularity && EnableMod && FairsHumanEnabled;
        public string FairsPopularityEventModifierHumanValueText
        {
            get => FairsPopularityEventModifierHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FairsPopularityEventModifierHuman = parsed; OnPropertyChanged(nameof(FairsPopularityEventModifierHumanValueText)); }
        }
        private int fairsPopularityEventModifierAI = 16;
        [SyncHostOnly]
        public int FairsPopularityEventModifierAI
        {
            get => fairsPopularityEventModifierAI;
            set { if (ChangePopularity(ref fairsPopularityEventModifierAI, Math.Max(-32, Math.Min(32, value)))) OnPropertyChanged(nameof(FairsPopularityEventModifierAIValueText)); }
        }
        public bool FairsPopularityEventModifierAIEditable => CanConfigurePopularity && EnableMod && FairsAIEnabled;
        public string FairsPopularityEventModifierAIValueText
        {
            get => FairsPopularityEventModifierAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FairsPopularityEventModifierAI = parsed; OnPropertyChanged(nameof(FairsPopularityEventModifierAIValueText)); }
        }
        private int fearNegative1Human = -1;
        [SyncHostOnly]
        public int FearNegative1Human
        {
            get => fearNegative1Human;
            set { if (ChangePopularity(ref fearNegative1Human, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative1HumanValueText)); }
        }
        public bool FearNegative1HumanEditable => CanConfigurePopularity && EnableMod && NegativeFearHumanEnabled;
        public string FearNegative1HumanValueText
        {
            get => FearNegative1Human.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative1Human = parsed; OnPropertyChanged(nameof(FearNegative1HumanValueText)); }
        }
        private int fearNegative1AI = -1;
        [SyncHostOnly]
        public int FearNegative1AI
        {
            get => fearNegative1AI;
            set { if (ChangePopularity(ref fearNegative1AI, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative1AIValueText)); }
        }
        public bool FearNegative1AIEditable => CanConfigurePopularity && EnableMod && NegativeFearAIEnabled;
        public string FearNegative1AIValueText
        {
            get => FearNegative1AI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative1AI = parsed; OnPropertyChanged(nameof(FearNegative1AIValueText)); }
        }
        private int fearNegative2Human = -2;
        [SyncHostOnly]
        public int FearNegative2Human
        {
            get => fearNegative2Human;
            set { if (ChangePopularity(ref fearNegative2Human, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative2HumanValueText)); }
        }
        public bool FearNegative2HumanEditable => CanConfigurePopularity && EnableMod && NegativeFearHumanEnabled;
        public string FearNegative2HumanValueText
        {
            get => FearNegative2Human.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative2Human = parsed; OnPropertyChanged(nameof(FearNegative2HumanValueText)); }
        }
        private int fearNegative2AI = -2;
        [SyncHostOnly]
        public int FearNegative2AI
        {
            get => fearNegative2AI;
            set { if (ChangePopularity(ref fearNegative2AI, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative2AIValueText)); }
        }
        public bool FearNegative2AIEditable => CanConfigurePopularity && EnableMod && NegativeFearAIEnabled;
        public string FearNegative2AIValueText
        {
            get => FearNegative2AI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative2AI = parsed; OnPropertyChanged(nameof(FearNegative2AIValueText)); }
        }
        private int fearNegative3Human = -3;
        [SyncHostOnly]
        public int FearNegative3Human
        {
            get => fearNegative3Human;
            set { if (ChangePopularity(ref fearNegative3Human, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative3HumanValueText)); }
        }
        public bool FearNegative3HumanEditable => CanConfigurePopularity && EnableMod && NegativeFearHumanEnabled;
        public string FearNegative3HumanValueText
        {
            get => FearNegative3Human.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative3Human = parsed; OnPropertyChanged(nameof(FearNegative3HumanValueText)); }
        }
        private int fearNegative3AI = -3;
        [SyncHostOnly]
        public int FearNegative3AI
        {
            get => fearNegative3AI;
            set { if (ChangePopularity(ref fearNegative3AI, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative3AIValueText)); }
        }
        public bool FearNegative3AIEditable => CanConfigurePopularity && EnableMod && NegativeFearAIEnabled;
        public string FearNegative3AIValueText
        {
            get => FearNegative3AI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative3AI = parsed; OnPropertyChanged(nameof(FearNegative3AIValueText)); }
        }
        private int fearNegative4Human = -4;
        [SyncHostOnly]
        public int FearNegative4Human
        {
            get => fearNegative4Human;
            set { if (ChangePopularity(ref fearNegative4Human, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative4HumanValueText)); }
        }
        public bool FearNegative4HumanEditable => CanConfigurePopularity && EnableMod && NegativeFearHumanEnabled;
        public string FearNegative4HumanValueText
        {
            get => FearNegative4Human.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative4Human = parsed; OnPropertyChanged(nameof(FearNegative4HumanValueText)); }
        }
        private int fearNegative4AI = -4;
        [SyncHostOnly]
        public int FearNegative4AI
        {
            get => fearNegative4AI;
            set { if (ChangePopularity(ref fearNegative4AI, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative4AIValueText)); }
        }
        public bool FearNegative4AIEditable => CanConfigurePopularity && EnableMod && NegativeFearAIEnabled;
        public string FearNegative4AIValueText
        {
            get => FearNegative4AI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative4AI = parsed; OnPropertyChanged(nameof(FearNegative4AIValueText)); }
        }
        private int fearNegative5Human = -5;
        [SyncHostOnly]
        public int FearNegative5Human
        {
            get => fearNegative5Human;
            set { if (ChangePopularity(ref fearNegative5Human, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative5HumanValueText)); }
        }
        public bool FearNegative5HumanEditable => CanConfigurePopularity && EnableMod && NegativeFearHumanEnabled;
        public string FearNegative5HumanValueText
        {
            get => FearNegative5Human.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative5Human = parsed; OnPropertyChanged(nameof(FearNegative5HumanValueText)); }
        }
        private int fearNegative5AI = -5;
        [SyncHostOnly]
        public int FearNegative5AI
        {
            get => fearNegative5AI;
            set { if (ChangePopularity(ref fearNegative5AI, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearNegative5AIValueText)); }
        }
        public bool FearNegative5AIEditable => CanConfigurePopularity && EnableMod && NegativeFearAIEnabled;
        public string FearNegative5AIValueText
        {
            get => FearNegative5AI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearNegative5AI = parsed; OnPropertyChanged(nameof(FearNegative5AIValueText)); }
        }
        private int fearPositive1Human = 1;
        [SyncHostOnly]
        public int FearPositive1Human
        {
            get => fearPositive1Human;
            set { if (ChangePopularity(ref fearPositive1Human, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive1HumanValueText)); }
        }
        public bool FearPositive1HumanEditable => CanConfigurePopularity && EnableMod && PositiveFearHumanEnabled;
        public string FearPositive1HumanValueText
        {
            get => FearPositive1Human.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive1Human = parsed; OnPropertyChanged(nameof(FearPositive1HumanValueText)); }
        }
        private int fearPositive1AI = 1;
        [SyncHostOnly]
        public int FearPositive1AI
        {
            get => fearPositive1AI;
            set { if (ChangePopularity(ref fearPositive1AI, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive1AIValueText)); }
        }
        public bool FearPositive1AIEditable => CanConfigurePopularity && EnableMod && PositiveFearAIEnabled;
        public string FearPositive1AIValueText
        {
            get => FearPositive1AI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive1AI = parsed; OnPropertyChanged(nameof(FearPositive1AIValueText)); }
        }
        private int fearPositive2Human = 2;
        [SyncHostOnly]
        public int FearPositive2Human
        {
            get => fearPositive2Human;
            set { if (ChangePopularity(ref fearPositive2Human, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive2HumanValueText)); }
        }
        public bool FearPositive2HumanEditable => CanConfigurePopularity && EnableMod && PositiveFearHumanEnabled;
        public string FearPositive2HumanValueText
        {
            get => FearPositive2Human.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive2Human = parsed; OnPropertyChanged(nameof(FearPositive2HumanValueText)); }
        }
        private int fearPositive2AI = 2;
        [SyncHostOnly]
        public int FearPositive2AI
        {
            get => fearPositive2AI;
            set { if (ChangePopularity(ref fearPositive2AI, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive2AIValueText)); }
        }
        public bool FearPositive2AIEditable => CanConfigurePopularity && EnableMod && PositiveFearAIEnabled;
        public string FearPositive2AIValueText
        {
            get => FearPositive2AI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive2AI = parsed; OnPropertyChanged(nameof(FearPositive2AIValueText)); }
        }
        private int fearPositive3Human = 3;
        [SyncHostOnly]
        public int FearPositive3Human
        {
            get => fearPositive3Human;
            set { if (ChangePopularity(ref fearPositive3Human, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive3HumanValueText)); }
        }
        public bool FearPositive3HumanEditable => CanConfigurePopularity && EnableMod && PositiveFearHumanEnabled;
        public string FearPositive3HumanValueText
        {
            get => FearPositive3Human.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive3Human = parsed; OnPropertyChanged(nameof(FearPositive3HumanValueText)); }
        }
        private int fearPositive3AI = 3;
        [SyncHostOnly]
        public int FearPositive3AI
        {
            get => fearPositive3AI;
            set { if (ChangePopularity(ref fearPositive3AI, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive3AIValueText)); }
        }
        public bool FearPositive3AIEditable => CanConfigurePopularity && EnableMod && PositiveFearAIEnabled;
        public string FearPositive3AIValueText
        {
            get => FearPositive3AI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive3AI = parsed; OnPropertyChanged(nameof(FearPositive3AIValueText)); }
        }
        private int fearPositive4Human = 4;
        [SyncHostOnly]
        public int FearPositive4Human
        {
            get => fearPositive4Human;
            set { if (ChangePopularity(ref fearPositive4Human, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive4HumanValueText)); }
        }
        public bool FearPositive4HumanEditable => CanConfigurePopularity && EnableMod && PositiveFearHumanEnabled;
        public string FearPositive4HumanValueText
        {
            get => FearPositive4Human.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive4Human = parsed; OnPropertyChanged(nameof(FearPositive4HumanValueText)); }
        }
        private int fearPositive4AI = 4;
        [SyncHostOnly]
        public int FearPositive4AI
        {
            get => fearPositive4AI;
            set { if (ChangePopularity(ref fearPositive4AI, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive4AIValueText)); }
        }
        public bool FearPositive4AIEditable => CanConfigurePopularity && EnableMod && PositiveFearAIEnabled;
        public string FearPositive4AIValueText
        {
            get => FearPositive4AI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive4AI = parsed; OnPropertyChanged(nameof(FearPositive4AIValueText)); }
        }
        private int fearPositive5Human = 5;
        [SyncHostOnly]
        public int FearPositive5Human
        {
            get => fearPositive5Human;
            set { if (ChangePopularity(ref fearPositive5Human, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive5HumanValueText)); }
        }
        public bool FearPositive5HumanEditable => CanConfigurePopularity && EnableMod && PositiveFearHumanEnabled;
        public string FearPositive5HumanValueText
        {
            get => FearPositive5Human.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive5Human = parsed; OnPropertyChanged(nameof(FearPositive5HumanValueText)); }
        }
        private int fearPositive5AI = 5;
        [SyncHostOnly]
        public int FearPositive5AI
        {
            get => fearPositive5AI;
            set { if (ChangePopularity(ref fearPositive5AI, Math.Max(-25, Math.Min(25, value)))) OnPropertyChanged(nameof(FearPositive5AIValueText)); }
        }
        public bool FearPositive5AIEditable => CanConfigurePopularity && EnableMod && PositiveFearAIEnabled;
        public string FearPositive5AIValueText
        {
            get => FearPositive5AI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) FearPositive5AI = parsed; OnPropertyChanged(nameof(FearPositive5AIValueText)); }
        }
        public string RationsPopModifierNoneTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNone");
        public string RationsPopModifierNoneHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNone.Human.Title");
        public string RationsPopModifierNoneHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNone.Human.Help");
        public string RationsPopModifierNoneAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNone.AI.Title");
        public string RationsPopModifierNoneAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNone.AI.Help");
        public string RationsPopModifierHalfTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierHalf");
        public string RationsPopModifierHalfHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierHalf.Human.Title");
        public string RationsPopModifierHalfHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierHalf.Human.Help");
        public string RationsPopModifierHalfAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierHalf.AI.Title");
        public string RationsPopModifierHalfAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierHalf.AI.Help");
        public string RationsPopModifierFullTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierFull");
        public string RationsPopModifierFullHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierFull.Human.Title");
        public string RationsPopModifierFullHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierFull.Human.Help");
        public string RationsPopModifierFullAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierFull.AI.Title");
        public string RationsPopModifierFullAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierFull.AI.Help");
        public string RationsPopModifierExtraTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierExtra");
        public string RationsPopModifierExtraHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierExtra.Human.Title");
        public string RationsPopModifierExtraHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierExtra.Human.Help");
        public string RationsPopModifierExtraAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierExtra.AI.Title");
        public string RationsPopModifierExtraAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierExtra.AI.Help");
        public string RationsPopModifierDoubleTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierDouble");
        public string RationsPopModifierDoubleHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierDouble.Human.Title");
        public string RationsPopModifierDoubleHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierDouble.Human.Help");
        public string RationsPopModifierDoubleAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierDouble.AI.Title");
        public string RationsPopModifierDoubleAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierDouble.AI.Help");
        public string RationsPopModifierNoPreferredFoodTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNoPreferredFood");
        public string RationsPopModifierNoPreferredFoodHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNoPreferredFood.Human.Title");
        public string RationsPopModifierNoPreferredFoodHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNoPreferredFood.Human.Help");
        public string RationsPopModifierNoPreferredFoodAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNoPreferredFood.AI.Title");
        public string RationsPopModifierNoPreferredFoodAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNoPreferredFood.AI.Help");
        public string RationsPopModifierNoPopulationTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNoPopulation");
        public string RationsPopModifierNoPopulationHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNoPopulation.Human.Title");
        public string RationsPopModifierNoPopulationHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNoPopulation.Human.Help");
        public string RationsPopModifierNoPopulationAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNoPopulation.AI.Title");
        public string RationsPopModifierNoPopulationAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.RationsPopModifierNoPopulation.AI.Help");
        public string FoodVarietyPopModifierTwoTypesTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierTwoTypes");
        public string FoodVarietyPopModifierTwoTypesHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierTwoTypes.Human.Title");
        public string FoodVarietyPopModifierTwoTypesHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierTwoTypes.Human.Help");
        public string FoodVarietyPopModifierTwoTypesAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierTwoTypes.AI.Title");
        public string FoodVarietyPopModifierTwoTypesAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierTwoTypes.AI.Help");
        public string FoodVarietyPopModifierThreeTypesTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierThreeTypes");
        public string FoodVarietyPopModifierThreeTypesHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierThreeTypes.Human.Title");
        public string FoodVarietyPopModifierThreeTypesHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierThreeTypes.Human.Help");
        public string FoodVarietyPopModifierThreeTypesAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierThreeTypes.AI.Title");
        public string FoodVarietyPopModifierThreeTypesAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierThreeTypes.AI.Help");
        public string FoodVarietyPopModifierFourTypesTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierFourTypes");
        public string FoodVarietyPopModifierFourTypesHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierFourTypes.Human.Title");
        public string FoodVarietyPopModifierFourTypesHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierFourTypes.Human.Help");
        public string FoodVarietyPopModifierFourTypesAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierFourTypes.AI.Title");
        public string FoodVarietyPopModifierFourTypesAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FoodVarietyPopModifierFourTypes.AI.Help");
        public string OverpopulationPopModifierTierOneTitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierOne");
        public string OverpopulationPopModifierTierOneHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierOne.Human.Title");
        public string OverpopulationPopModifierTierOneHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierOne.Human.Help");
        public string OverpopulationPopModifierTierOneAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierOne.AI.Title");
        public string OverpopulationPopModifierTierOneAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierOne.AI.Help");
        public string OverpopulationPopModifierTierTwoTitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierTwo");
        public string OverpopulationPopModifierTierTwoHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierTwo.Human.Title");
        public string OverpopulationPopModifierTierTwoHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierTwo.Human.Help");
        public string OverpopulationPopModifierTierTwoAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierTwo.AI.Title");
        public string OverpopulationPopModifierTierTwoAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierTwo.AI.Help");
        public string OverpopulationPopModifierTierThreeTitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierThree");
        public string OverpopulationPopModifierTierThreeHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierThree.Human.Title");
        public string OverpopulationPopModifierTierThreeHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierThree.Human.Help");
        public string OverpopulationPopModifierTierThreeAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierThree.AI.Title");
        public string OverpopulationPopModifierTierThreeAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierThree.AI.Help");
        public string OverpopulationPopModifierTierFourTitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierFour");
        public string OverpopulationPopModifierTierFourHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierFour.Human.Title");
        public string OverpopulationPopModifierTierFourHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierFour.Human.Help");
        public string OverpopulationPopModifierTierFourAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierFour.AI.Title");
        public string OverpopulationPopModifierTierFourAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierFour.AI.Help");
        public string OverpopulationPopModifierTierFiveTitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierFive");
        public string OverpopulationPopModifierTierFiveHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierFive.Human.Title");
        public string OverpopulationPopModifierTierFiveHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierFive.Human.Help");
        public string OverpopulationPopModifierTierFiveAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierFive.AI.Title");
        public string OverpopulationPopModifierTierFiveAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.OverpopulationPopModifierTierFive.AI.Help");
        public string TaxPopModifierHugeDonationTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierHugeDonation");
        public string TaxPopModifierHugeDonationHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierHugeDonation.Human.Title");
        public string TaxPopModifierHugeDonationHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierHugeDonation.Human.Help");
        public string TaxPopModifierHugeDonationAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierHugeDonation.AI.Title");
        public string TaxPopModifierHugeDonationAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierHugeDonation.AI.Help");
        public string TaxPopModifierBigDonationTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierBigDonation");
        public string TaxPopModifierBigDonationHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierBigDonation.Human.Title");
        public string TaxPopModifierBigDonationHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierBigDonation.Human.Help");
        public string TaxPopModifierBigDonationAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierBigDonation.AI.Title");
        public string TaxPopModifierBigDonationAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierBigDonation.AI.Help");
        public string TaxPopModifierSmallDonationTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierSmallDonation");
        public string TaxPopModifierSmallDonationHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierSmallDonation.Human.Title");
        public string TaxPopModifierSmallDonationHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierSmallDonation.Human.Help");
        public string TaxPopModifierSmallDonationAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierSmallDonation.AI.Title");
        public string TaxPopModifierSmallDonationAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierSmallDonation.AI.Help");
        public string TaxPopModifierNoneTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierNone");
        public string TaxPopModifierNoneHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierNone.Human.Title");
        public string TaxPopModifierNoneHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierNone.Human.Help");
        public string TaxPopModifierNoneAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierNone.AI.Title");
        public string TaxPopModifierNoneAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierNone.AI.Help");
        public string TaxPopModifierLowTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierLow");
        public string TaxPopModifierLowHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierLow.Human.Title");
        public string TaxPopModifierLowHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierLow.Human.Help");
        public string TaxPopModifierLowAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierLow.AI.Title");
        public string TaxPopModifierLowAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierLow.AI.Help");
        public string TaxPopModifierModerateTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierModerate");
        public string TaxPopModifierModerateHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierModerate.Human.Title");
        public string TaxPopModifierModerateHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierModerate.Human.Help");
        public string TaxPopModifierModerateAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierModerate.AI.Title");
        public string TaxPopModifierModerateAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierModerate.AI.Help");
        public string TaxPopModifierHighTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierHigh");
        public string TaxPopModifierHighHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierHigh.Human.Title");
        public string TaxPopModifierHighHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierHigh.Human.Help");
        public string TaxPopModifierHighAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierHigh.AI.Title");
        public string TaxPopModifierHighAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierHigh.AI.Help");
        public string TaxPopModifierMeanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierMean");
        public string TaxPopModifierMeanHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierMean.Human.Title");
        public string TaxPopModifierMeanHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierMean.Human.Help");
        public string TaxPopModifierMeanAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierMean.AI.Title");
        public string TaxPopModifierMeanAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierMean.AI.Help");
        public string TaxPopModifierUsuriousTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUsurious");
        public string TaxPopModifierUsuriousHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUsurious.Human.Title");
        public string TaxPopModifierUsuriousHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUsurious.Human.Help");
        public string TaxPopModifierUsuriousAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUsurious.AI.Title");
        public string TaxPopModifierUsuriousAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUsurious.AI.Help");
        public string TaxPopModifierCruelTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierCruel");
        public string TaxPopModifierCruelHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierCruel.Human.Title");
        public string TaxPopModifierCruelHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierCruel.Human.Help");
        public string TaxPopModifierCruelAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierCruel.AI.Title");
        public string TaxPopModifierCruelAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierCruel.AI.Help");
        public string TaxPopModifierMostCruelTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierMostCruel");
        public string TaxPopModifierMostCruelHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierMostCruel.Human.Title");
        public string TaxPopModifierMostCruelHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierMostCruel.Human.Help");
        public string TaxPopModifierMostCruelAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierMostCruel.AI.Title");
        public string TaxPopModifierMostCruelAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierMostCruel.AI.Help");
        public string TaxPopModifierCruellestTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierCruellest");
        public string TaxPopModifierCruellestHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierCruellest.Human.Title");
        public string TaxPopModifierCruellestHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierCruellest.Human.Help");
        public string TaxPopModifierCruellestAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierCruellest.AI.Title");
        public string TaxPopModifierCruellestAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierCruellest.AI.Help");
        public string TaxPopModifierUnaffordableDonationTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUnaffordableDonation");
        public string TaxPopModifierUnaffordableDonationHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUnaffordableDonation.Human.Title");
        public string TaxPopModifierUnaffordableDonationHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUnaffordableDonation.Human.Help");
        public string TaxPopModifierUnaffordableDonationAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUnaffordableDonation.AI.Title");
        public string TaxPopModifierUnaffordableDonationAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUnaffordableDonation.AI.Help");
        public string TaxPopModifierUnknownModeTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUnknownMode");
        public string TaxPopModifierUnknownModeHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUnknownMode.Human.Title");
        public string TaxPopModifierUnknownModeHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUnknownMode.Human.Help");
        public string TaxPopModifierUnknownModeAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUnknownMode.AI.Title");
        public string TaxPopModifierUnknownModeAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.TaxPopModifierUnknownMode.AI.Help");
        public string ReligionPopModifierTierOneTitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierOne");
        public string ReligionPopModifierTierOneHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierOne.Human.Title");
        public string ReligionPopModifierTierOneHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierOne.Human.Help");
        public string ReligionPopModifierTierOneAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierOne.AI.Title");
        public string ReligionPopModifierTierOneAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierOne.AI.Help");
        public string ReligionPopModifierTierTwoTitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierTwo");
        public string ReligionPopModifierTierTwoHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierTwo.Human.Title");
        public string ReligionPopModifierTierTwoHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierTwo.Human.Help");
        public string ReligionPopModifierTierTwoAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierTwo.AI.Title");
        public string ReligionPopModifierTierTwoAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierTwo.AI.Help");
        public string ReligionPopModifierTierThreeTitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierThree");
        public string ReligionPopModifierTierThreeHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierThree.Human.Title");
        public string ReligionPopModifierTierThreeHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierThree.Human.Help");
        public string ReligionPopModifierTierThreeAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierThree.AI.Title");
        public string ReligionPopModifierTierThreeAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierThree.AI.Help");
        public string ReligionPopModifierTierFourTitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierFour");
        public string ReligionPopModifierTierFourHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierFour.Human.Title");
        public string ReligionPopModifierTierFourHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierFour.Human.Help");
        public string ReligionPopModifierTierFourAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierFour.AI.Title");
        public string ReligionPopModifierTierFourAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.ReligionPopModifierTierFour.AI.Help");
        public string ChurchPopModifierTitle => SerpLocalization.Get("ExtraFeatures.Popularity.ChurchPopModifier");
        public string ChurchPopModifierHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.ChurchPopModifier.Human.Title");
        public string ChurchPopModifierHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.ChurchPopModifier.Human.Help");
        public string ChurchPopModifierAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.ChurchPopModifier.AI.Title");
        public string ChurchPopModifierAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.ChurchPopModifier.AI.Help");
        public string CathedralPopModifierTitle => SerpLocalization.Get("ExtraFeatures.Popularity.CathedralPopModifier");
        public string CathedralPopModifierHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.CathedralPopModifier.Human.Title");
        public string CathedralPopModifierHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.CathedralPopModifier.Human.Help");
        public string CathedralPopModifierAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.CathedralPopModifier.AI.Title");
        public string CathedralPopModifierAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.CathedralPopModifier.AI.Help");
        public string AlePopModifierTierOneTitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierOne");
        public string AlePopModifierTierOneHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierOne.Human.Title");
        public string AlePopModifierTierOneHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierOne.Human.Help");
        public string AlePopModifierTierOneAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierOne.AI.Title");
        public string AlePopModifierTierOneAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierOne.AI.Help");
        public string AlePopModifierTierTwoTitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierTwo");
        public string AlePopModifierTierTwoHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierTwo.Human.Title");
        public string AlePopModifierTierTwoHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierTwo.Human.Help");
        public string AlePopModifierTierTwoAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierTwo.AI.Title");
        public string AlePopModifierTierTwoAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierTwo.AI.Help");
        public string AlePopModifierTierThreeTitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierThree");
        public string AlePopModifierTierThreeHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierThree.Human.Title");
        public string AlePopModifierTierThreeHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierThree.Human.Help");
        public string AlePopModifierTierThreeAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierThree.AI.Title");
        public string AlePopModifierTierThreeAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierThree.AI.Help");
        public string AlePopModifierTierFourTitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierFour");
        public string AlePopModifierTierFourHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierFour.Human.Title");
        public string AlePopModifierTierFourHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierFour.Human.Help");
        public string AlePopModifierTierFourAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierFour.AI.Title");
        public string AlePopModifierTierFourAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.AlePopModifierTierFour.AI.Help");
        public string FairsPopularityEventModifierTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FairsPopularityEventModifier");
        public string FairsPopularityEventModifierHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FairsPopularityEventModifier.Human.Title");
        public string FairsPopularityEventModifierHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FairsPopularityEventModifier.Human.Help");
        public string FairsPopularityEventModifierAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FairsPopularityEventModifier.AI.Title");
        public string FairsPopularityEventModifierAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FairsPopularityEventModifier.AI.Help");
        public string FearNegative1Title => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative1");
        public string FearNegative1HumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative1.Human.Title");
        public string FearNegative1HumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative1.Human.Help");
        public string FearNegative1AITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative1.AI.Title");
        public string FearNegative1AIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative1.AI.Help");
        public string FearNegative2Title => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative2");
        public string FearNegative2HumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative2.Human.Title");
        public string FearNegative2HumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative2.Human.Help");
        public string FearNegative2AITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative2.AI.Title");
        public string FearNegative2AIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative2.AI.Help");
        public string FearNegative3Title => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative3");
        public string FearNegative3HumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative3.Human.Title");
        public string FearNegative3HumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative3.Human.Help");
        public string FearNegative3AITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative3.AI.Title");
        public string FearNegative3AIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative3.AI.Help");
        public string FearNegative4Title => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative4");
        public string FearNegative4HumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative4.Human.Title");
        public string FearNegative4HumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative4.Human.Help");
        public string FearNegative4AITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative4.AI.Title");
        public string FearNegative4AIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative4.AI.Help");
        public string FearNegative5Title => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative5");
        public string FearNegative5HumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative5.Human.Title");
        public string FearNegative5HumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative5.Human.Help");
        public string FearNegative5AITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative5.AI.Title");
        public string FearNegative5AIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearNegative5.AI.Help");
        public string FearPositive1Title => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive1");
        public string FearPositive1HumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive1.Human.Title");
        public string FearPositive1HumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive1.Human.Help");
        public string FearPositive1AITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive1.AI.Title");
        public string FearPositive1AIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive1.AI.Help");
        public string FearPositive2Title => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive2");
        public string FearPositive2HumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive2.Human.Title");
        public string FearPositive2HumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive2.Human.Help");
        public string FearPositive2AITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive2.AI.Title");
        public string FearPositive2AIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive2.AI.Help");
        public string FearPositive3Title => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive3");
        public string FearPositive3HumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive3.Human.Title");
        public string FearPositive3HumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive3.Human.Help");
        public string FearPositive3AITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive3.AI.Title");
        public string FearPositive3AIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive3.AI.Help");
        public string FearPositive4Title => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive4");
        public string FearPositive4HumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive4.Human.Title");
        public string FearPositive4HumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive4.Human.Help");
        public string FearPositive4AITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive4.AI.Title");
        public string FearPositive4AIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive4.AI.Help");
        public string FearPositive5Title => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive5");
        public string FearPositive5HumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive5.Human.Title");
        public string FearPositive5HumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive5.Human.Help");
        public string FearPositive5AITitle => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive5.AI.Title");
        public string FearPositive5AIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.FearPositive5.AI.Help");
        public string RationsTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Rations");
        public string RationsHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Rations.Human.Title");
        public string RationsHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Rations.Human.Help");
        public string RationsAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.Rations.AI.Title");
        public string RationsAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Rations.AI.Help");
        public string VarietyTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Variety");
        public string VarietyHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Variety.Human.Title");
        public string VarietyHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Variety.Human.Help");
        public string VarietyAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.Variety.AI.Title");
        public string VarietyAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Variety.AI.Help");
        public string CrowdingTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Crowding");
        public string CrowdingHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Crowding.Human.Title");
        public string CrowdingHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Crowding.Human.Help");
        public string CrowdingAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.Crowding.AI.Title");
        public string CrowdingAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Crowding.AI.Help");
        public string TaxesTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Taxes");
        public string TaxesHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Taxes.Human.Title");
        public string TaxesHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Taxes.Human.Help");
        public string TaxesAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.Taxes.AI.Title");
        public string TaxesAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Taxes.AI.Help");
        public string ReligionTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Religion");
        public string ReligionHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Religion.Human.Title");
        public string ReligionHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Religion.Human.Help");
        public string ReligionAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.Religion.AI.Title");
        public string ReligionAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Religion.AI.Help");
        public string AleTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Ale");
        public string AleHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Ale.Human.Title");
        public string AleHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Ale.Human.Help");
        public string AleAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.Ale.AI.Title");
        public string AleAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Ale.AI.Help");
        public string FairsTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Fairs");
        public string FairsHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Fairs.Human.Title");
        public string FairsHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Fairs.Human.Help");
        public string FairsAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.Fairs.AI.Title");
        public string FairsAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.Fairs.AI.Help");
        public string NegativeFearTitle => SerpLocalization.Get("ExtraFeatures.Popularity.NegativeFear");
        public string NegativeFearHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.NegativeFear.Human.Title");
        public string NegativeFearHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.NegativeFear.Human.Help");
        public string NegativeFearAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.NegativeFear.AI.Title");
        public string NegativeFearAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.NegativeFear.AI.Help");
        public string PositiveFearTitle => SerpLocalization.Get("ExtraFeatures.Popularity.PositiveFear");
        public string PositiveFearHumanTitle => SerpLocalization.Get("ExtraFeatures.Popularity.PositiveFear.Human.Title");
        public string PositiveFearHumanHelp => SerpLocalization.Get("ExtraFeatures.Popularity.PositiveFear.Human.Help");
        public string PositiveFearAITitle => SerpLocalization.Get("ExtraFeatures.Popularity.PositiveFear.AI.Title");
        public string PositiveFearAIHelp => SerpLocalization.Get("ExtraFeatures.Popularity.PositiveFear.AI.Help");
        public string FearTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Fear");
        internal Dictionary<PopularitySource, PopularityOverrideValues> CaptureOrdinaryContributions()
        {
            var values = new Dictionary<PopularitySource, PopularityOverrideValues>();
            if (!EnableMod) return values;
            if (RationsHumanEnabled || RationsAIEnabled) values.Add(PopularitySource.RationsPopModifierNone, new PopularityOverrideValues(RationsHumanEnabled ? (int?)RationsPopModifierNoneHuman : null, RationsAIEnabled ? (int?)RationsPopModifierNoneAI : null));
            if (RationsHumanEnabled || RationsAIEnabled) values.Add(PopularitySource.RationsPopModifierHalf, new PopularityOverrideValues(RationsHumanEnabled ? (int?)RationsPopModifierHalfHuman : null, RationsAIEnabled ? (int?)RationsPopModifierHalfAI : null));
            if (RationsHumanEnabled || RationsAIEnabled) values.Add(PopularitySource.RationsPopModifierFull, new PopularityOverrideValues(RationsHumanEnabled ? (int?)RationsPopModifierFullHuman : null, RationsAIEnabled ? (int?)RationsPopModifierFullAI : null));
            if (RationsHumanEnabled || RationsAIEnabled) values.Add(PopularitySource.RationsPopModifierExtra, new PopularityOverrideValues(RationsHumanEnabled ? (int?)RationsPopModifierExtraHuman : null, RationsAIEnabled ? (int?)RationsPopModifierExtraAI : null));
            if (RationsHumanEnabled || RationsAIEnabled) values.Add(PopularitySource.RationsPopModifierDouble, new PopularityOverrideValues(RationsHumanEnabled ? (int?)RationsPopModifierDoubleHuman : null, RationsAIEnabled ? (int?)RationsPopModifierDoubleAI : null));
            if (RationsHumanEnabled || RationsAIEnabled) values.Add(PopularitySource.RationsPopModifierNoPreferredFood, new PopularityOverrideValues(RationsHumanEnabled ? (int?)RationsPopModifierNoPreferredFoodHuman : null, RationsAIEnabled ? (int?)RationsPopModifierNoPreferredFoodAI : null));
            if (RationsHumanEnabled || RationsAIEnabled) values.Add(PopularitySource.RationsPopModifierNoPopulation, new PopularityOverrideValues(RationsHumanEnabled ? (int?)RationsPopModifierNoPopulationHuman : null, RationsAIEnabled ? (int?)RationsPopModifierNoPopulationAI : null));
            if (VarietyHumanEnabled || VarietyAIEnabled) values.Add(PopularitySource.FoodVarietyPopModifierTwoTypes, new PopularityOverrideValues(VarietyHumanEnabled ? (int?)FoodVarietyPopModifierTwoTypesHuman : null, VarietyAIEnabled ? (int?)FoodVarietyPopModifierTwoTypesAI : null));
            if (VarietyHumanEnabled || VarietyAIEnabled) values.Add(PopularitySource.FoodVarietyPopModifierThreeTypes, new PopularityOverrideValues(VarietyHumanEnabled ? (int?)FoodVarietyPopModifierThreeTypesHuman : null, VarietyAIEnabled ? (int?)FoodVarietyPopModifierThreeTypesAI : null));
            if (VarietyHumanEnabled || VarietyAIEnabled) values.Add(PopularitySource.FoodVarietyPopModifierFourTypes, new PopularityOverrideValues(VarietyHumanEnabled ? (int?)FoodVarietyPopModifierFourTypesHuman : null, VarietyAIEnabled ? (int?)FoodVarietyPopModifierFourTypesAI : null));
            if (CrowdingHumanEnabled || CrowdingAIEnabled) values.Add(PopularitySource.OverpopulationPopModifierTierOne, new PopularityOverrideValues(CrowdingHumanEnabled ? (int?)OverpopulationPopModifierTierOneHuman : null, CrowdingAIEnabled ? (int?)OverpopulationPopModifierTierOneAI : null));
            if (CrowdingHumanEnabled || CrowdingAIEnabled) values.Add(PopularitySource.OverpopulationPopModifierTierTwo, new PopularityOverrideValues(CrowdingHumanEnabled ? (int?)OverpopulationPopModifierTierTwoHuman : null, CrowdingAIEnabled ? (int?)OverpopulationPopModifierTierTwoAI : null));
            if (CrowdingHumanEnabled || CrowdingAIEnabled) values.Add(PopularitySource.OverpopulationPopModifierTierThree, new PopularityOverrideValues(CrowdingHumanEnabled ? (int?)OverpopulationPopModifierTierThreeHuman : null, CrowdingAIEnabled ? (int?)OverpopulationPopModifierTierThreeAI : null));
            if (CrowdingHumanEnabled || CrowdingAIEnabled) values.Add(PopularitySource.OverpopulationPopModifierTierFour, new PopularityOverrideValues(CrowdingHumanEnabled ? (int?)OverpopulationPopModifierTierFourHuman : null, CrowdingAIEnabled ? (int?)OverpopulationPopModifierTierFourAI : null));
            if (CrowdingHumanEnabled || CrowdingAIEnabled) values.Add(PopularitySource.OverpopulationPopModifierTierFive, new PopularityOverrideValues(CrowdingHumanEnabled ? (int?)OverpopulationPopModifierTierFiveHuman : null, CrowdingAIEnabled ? (int?)OverpopulationPopModifierTierFiveAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierHugeDonation, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierHugeDonationHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierHugeDonationAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierBigDonation, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierBigDonationHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierBigDonationAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierSmallDonation, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierSmallDonationHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierSmallDonationAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierNone, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierNoneHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierNoneAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierLow, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierLowHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierLowAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierModerate, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierModerateHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierModerateAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierHigh, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierHighHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierHighAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierMean, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierMeanHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierMeanAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierUsurious, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierUsuriousHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierUsuriousAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierCruel, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierCruelHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierCruelAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierMostCruel, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierMostCruelHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierMostCruelAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierCruellest, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierCruellestHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierCruellestAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierUnaffordableDonation, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierUnaffordableDonationHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierUnaffordableDonationAI : null));
            if (TaxesHumanEnabled || TaxesAIEnabled) values.Add(PopularitySource.TaxPopModifierUnknownMode, new PopularityOverrideValues(TaxesHumanEnabled ? (int?)TaxPopModifierUnknownModeHuman : null, TaxesAIEnabled ? (int?)TaxPopModifierUnknownModeAI : null));
            if (ReligionHumanEnabled || ReligionAIEnabled) values.Add(PopularitySource.ReligionPopModifierTierOne, new PopularityOverrideValues(ReligionHumanEnabled ? (int?)ReligionPopModifierTierOneHuman : null, ReligionAIEnabled ? (int?)ReligionPopModifierTierOneAI : null));
            if (ReligionHumanEnabled || ReligionAIEnabled) values.Add(PopularitySource.ReligionPopModifierTierTwo, new PopularityOverrideValues(ReligionHumanEnabled ? (int?)ReligionPopModifierTierTwoHuman : null, ReligionAIEnabled ? (int?)ReligionPopModifierTierTwoAI : null));
            if (ReligionHumanEnabled || ReligionAIEnabled) values.Add(PopularitySource.ReligionPopModifierTierThree, new PopularityOverrideValues(ReligionHumanEnabled ? (int?)ReligionPopModifierTierThreeHuman : null, ReligionAIEnabled ? (int?)ReligionPopModifierTierThreeAI : null));
            if (ReligionHumanEnabled || ReligionAIEnabled) values.Add(PopularitySource.ReligionPopModifierTierFour, new PopularityOverrideValues(ReligionHumanEnabled ? (int?)ReligionPopModifierTierFourHuman : null, ReligionAIEnabled ? (int?)ReligionPopModifierTierFourAI : null));
            if (ReligionHumanEnabled || ReligionAIEnabled) values.Add(PopularitySource.ChurchPopModifier, new PopularityOverrideValues(ReligionHumanEnabled ? (int?)ChurchPopModifierHuman : null, ReligionAIEnabled ? (int?)ChurchPopModifierAI : null));
            if (ReligionHumanEnabled || ReligionAIEnabled) values.Add(PopularitySource.CathedralPopModifier, new PopularityOverrideValues(ReligionHumanEnabled ? (int?)CathedralPopModifierHuman : null, ReligionAIEnabled ? (int?)CathedralPopModifierAI : null));
            if (AleHumanEnabled || AleAIEnabled) values.Add(PopularitySource.AlePopModifierTierOne, new PopularityOverrideValues(AleHumanEnabled ? (int?)AlePopModifierTierOneHuman : null, AleAIEnabled ? (int?)AlePopModifierTierOneAI : null));
            if (AleHumanEnabled || AleAIEnabled) values.Add(PopularitySource.AlePopModifierTierTwo, new PopularityOverrideValues(AleHumanEnabled ? (int?)AlePopModifierTierTwoHuman : null, AleAIEnabled ? (int?)AlePopModifierTierTwoAI : null));
            if (AleHumanEnabled || AleAIEnabled) values.Add(PopularitySource.AlePopModifierTierThree, new PopularityOverrideValues(AleHumanEnabled ? (int?)AlePopModifierTierThreeHuman : null, AleAIEnabled ? (int?)AlePopModifierTierThreeAI : null));
            if (AleHumanEnabled || AleAIEnabled) values.Add(PopularitySource.AlePopModifierTierFour, new PopularityOverrideValues(AleHumanEnabled ? (int?)AlePopModifierTierFourHuman : null, AleAIEnabled ? (int?)AlePopModifierTierFourAI : null));
            if (FairsHumanEnabled || FairsAIEnabled) values.Add(PopularitySource.FairsPopularityEventModifier, new PopularityOverrideValues(FairsHumanEnabled ? (int?)FairsPopularityEventModifierHuman : null, FairsAIEnabled ? (int?)FairsPopularityEventModifierAI : null));
            return values;
        }
        internal Dictionary<int, PopularityOverrideValues> CaptureFearContributions()
        {
            var values = new Dictionary<int, PopularityOverrideValues>();
            if (!EnableMod) return values;
            if (NegativeFearHumanEnabled || NegativeFearAIEnabled) values.Add(-1, new PopularityOverrideValues(NegativeFearHumanEnabled ? (int?)FearNegative1Human : null, NegativeFearAIEnabled ? (int?)FearNegative1AI : null));
            if (NegativeFearHumanEnabled || NegativeFearAIEnabled) values.Add(-2, new PopularityOverrideValues(NegativeFearHumanEnabled ? (int?)FearNegative2Human : null, NegativeFearAIEnabled ? (int?)FearNegative2AI : null));
            if (NegativeFearHumanEnabled || NegativeFearAIEnabled) values.Add(-3, new PopularityOverrideValues(NegativeFearHumanEnabled ? (int?)FearNegative3Human : null, NegativeFearAIEnabled ? (int?)FearNegative3AI : null));
            if (NegativeFearHumanEnabled || NegativeFearAIEnabled) values.Add(-4, new PopularityOverrideValues(NegativeFearHumanEnabled ? (int?)FearNegative4Human : null, NegativeFearAIEnabled ? (int?)FearNegative4AI : null));
            if (NegativeFearHumanEnabled || NegativeFearAIEnabled) values.Add(-5, new PopularityOverrideValues(NegativeFearHumanEnabled ? (int?)FearNegative5Human : null, NegativeFearAIEnabled ? (int?)FearNegative5AI : null));
            if (PositiveFearHumanEnabled || PositiveFearAIEnabled) values.Add(1, new PopularityOverrideValues(PositiveFearHumanEnabled ? (int?)FearPositive1Human : null, PositiveFearAIEnabled ? (int?)FearPositive1AI : null));
            if (PositiveFearHumanEnabled || PositiveFearAIEnabled) values.Add(2, new PopularityOverrideValues(PositiveFearHumanEnabled ? (int?)FearPositive2Human : null, PositiveFearAIEnabled ? (int?)FearPositive2AI : null));
            if (PositiveFearHumanEnabled || PositiveFearAIEnabled) values.Add(3, new PopularityOverrideValues(PositiveFearHumanEnabled ? (int?)FearPositive3Human : null, PositiveFearAIEnabled ? (int?)FearPositive3AI : null));
            if (PositiveFearHumanEnabled || PositiveFearAIEnabled) values.Add(4, new PopularityOverrideValues(PositiveFearHumanEnabled ? (int?)FearPositive4Human : null, PositiveFearAIEnabled ? (int?)FearPositive4AI : null));
            if (PositiveFearHumanEnabled || PositiveFearAIEnabled) values.Add(5, new PopularityOverrideValues(PositiveFearHumanEnabled ? (int?)FearPositive5Human : null, PositiveFearAIEnabled ? (int?)FearPositive5AI : null));
            return values;
        }
        internal void ResetPopularityDefaults()
        {
            RationsHumanEnabled = false;
            RationsAIEnabled = false;
            VarietyHumanEnabled = false;
            VarietyAIEnabled = false;
            CrowdingHumanEnabled = false;
            CrowdingAIEnabled = false;
            TaxesHumanEnabled = false;
            TaxesAIEnabled = false;
            ReligionHumanEnabled = false;
            ReligionAIEnabled = false;
            AleHumanEnabled = false;
            AleAIEnabled = false;
            FairsHumanEnabled = false;
            FairsAIEnabled = false;
            NegativeFearHumanEnabled = false;
            NegativeFearAIEnabled = false;
            PositiveFearHumanEnabled = false;
            PositiveFearAIEnabled = false;
            RationsPopModifierNoneHuman = -8;
            RationsPopModifierNoneAI = -8;
            RationsPopModifierHalfHuman = -4;
            RationsPopModifierHalfAI = -4;
            RationsPopModifierFullHuman = 0;
            RationsPopModifierFullAI = 0;
            RationsPopModifierExtraHuman = 4;
            RationsPopModifierExtraAI = 4;
            RationsPopModifierDoubleHuman = 8;
            RationsPopModifierDoubleAI = 8;
            RationsPopModifierNoPreferredFoodHuman = -8;
            RationsPopModifierNoPreferredFoodAI = -8;
            RationsPopModifierNoPopulationHuman = 8;
            RationsPopModifierNoPopulationAI = 8;
            FoodVarietyPopModifierTwoTypesHuman = 1;
            FoodVarietyPopModifierTwoTypesAI = 1;
            FoodVarietyPopModifierThreeTypesHuman = 2;
            FoodVarietyPopModifierThreeTypesAI = 2;
            FoodVarietyPopModifierFourTypesHuman = 3;
            FoodVarietyPopModifierFourTypesAI = 3;
            OverpopulationPopModifierTierOneHuman = -2;
            OverpopulationPopModifierTierOneAI = -2;
            OverpopulationPopModifierTierTwoHuman = -4;
            OverpopulationPopModifierTierTwoAI = -4;
            OverpopulationPopModifierTierThreeHuman = -6;
            OverpopulationPopModifierTierThreeAI = -6;
            OverpopulationPopModifierTierFourHuman = -8;
            OverpopulationPopModifierTierFourAI = -8;
            OverpopulationPopModifierTierFiveHuman = -10;
            OverpopulationPopModifierTierFiveAI = -10;
            TaxPopModifierHugeDonationHuman = 7;
            TaxPopModifierHugeDonationAI = 7;
            TaxPopModifierBigDonationHuman = 5;
            TaxPopModifierBigDonationAI = 5;
            TaxPopModifierSmallDonationHuman = 3;
            TaxPopModifierSmallDonationAI = 3;
            TaxPopModifierNoneHuman = 1;
            TaxPopModifierNoneAI = 1;
            TaxPopModifierLowHuman = -2;
            TaxPopModifierLowAI = -2;
            TaxPopModifierModerateHuman = -4;
            TaxPopModifierModerateAI = -4;
            TaxPopModifierHighHuman = -6;
            TaxPopModifierHighAI = -6;
            TaxPopModifierMeanHuman = -8;
            TaxPopModifierMeanAI = -8;
            TaxPopModifierUsuriousHuman = -12;
            TaxPopModifierUsuriousAI = -12;
            TaxPopModifierCruelHuman = -16;
            TaxPopModifierCruelAI = -16;
            TaxPopModifierMostCruelHuman = -20;
            TaxPopModifierMostCruelAI = -20;
            TaxPopModifierCruellestHuman = -24;
            TaxPopModifierCruellestAI = -24;
            TaxPopModifierUnaffordableDonationHuman = 1;
            TaxPopModifierUnaffordableDonationAI = 1;
            TaxPopModifierUnknownModeHuman = -24;
            TaxPopModifierUnknownModeAI = -24;
            ReligionPopModifierTierOneHuman = 2;
            ReligionPopModifierTierOneAI = 2;
            ReligionPopModifierTierTwoHuman = 4;
            ReligionPopModifierTierTwoAI = 4;
            ReligionPopModifierTierThreeHuman = 6;
            ReligionPopModifierTierThreeAI = 6;
            ReligionPopModifierTierFourHuman = 8;
            ReligionPopModifierTierFourAI = 8;
            ChurchPopModifierHuman = 1;
            ChurchPopModifierAI = 1;
            CathedralPopModifierHuman = 2;
            CathedralPopModifierAI = 2;
            AlePopModifierTierOneHuman = 2;
            AlePopModifierTierOneAI = 2;
            AlePopModifierTierTwoHuman = 4;
            AlePopModifierTierTwoAI = 4;
            AlePopModifierTierThreeHuman = 6;
            AlePopModifierTierThreeAI = 6;
            AlePopModifierTierFourHuman = 8;
            AlePopModifierTierFourAI = 8;
            FairsPopularityEventModifierHuman = 16;
            FairsPopularityEventModifierAI = 16;
            FearNegative1Human = -1;
            FearNegative1AI = -1;
            FearNegative2Human = -2;
            FearNegative2AI = -2;
            FearNegative3Human = -3;
            FearNegative3AI = -3;
            FearNegative4Human = -4;
            FearNegative4AI = -4;
            FearNegative5Human = -5;
            FearNegative5AI = -5;
            FearPositive1Human = 1;
            FearPositive1AI = 1;
            FearPositive2Human = 2;
            FearPositive2AI = 2;
            FearPositive3Human = 3;
            FearPositive3AI = 3;
            FearPositive4Human = 4;
            FearPositive4AI = 4;
            FearPositive5Human = 5;
            FearPositive5AI = 5;
        }
    }
}
