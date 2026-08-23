using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Globalization;

namespace RandomEvents
{
    public sealed class RandomEventsSettingsViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true;
        private int intervalMonths = 3;
        private int cooldownMonths = 6;
        private readonly int[] chances = new int[15];
        private int plagueMin = 1, plagueMax = 10;
        private int lionMin = 1, lionMax = 10;
        private double banditMin = 0.1, banditMax = 5.0;
        private double archerMin = 0.1, archerMax = 5.0;
        private int theftMin = 1, theftMax = 100;
        private int fireMin = 1, fireMax = 10;
        private int multiplayerEventMode;

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        public RandomEventsSettingsViewModel()
        {
            for (int index = 0; index < chances.Length; index++)
                chances[index] = 2;
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public RelayCommand ResetToDefaultCommand { get; }

        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string EnableModText => SerpLocalization.Get("Common.EnableMod");
        public string IntervalText => SerpLocalization.Get("RandomEvents.Interval");
        public string IntervalHelpText => SerpLocalization.Get("RandomEvents.IntervalHelp");
        public string CooldownText => SerpLocalization.Get("RandomEvents.Cooldown");
        public string CooldownHelpText => SerpLocalization.Get("RandomEvents.CooldownHelp");
        public string ChancesTitleText => SerpLocalization.Get("RandomEvents.ChancesTitle");
        public string PositiveEventsTitleText => SerpLocalization.Get("RandomEvents.PositiveEventsTitle");
        public string NegativeEventsTitleText => SerpLocalization.Get("RandomEvents.NegativeEventsTitle");
        public string ScheduleTitleText => SerpLocalization.Get("RandomEvents.ScheduleTitle");
        public string MultiplayerTitleText => SerpLocalization.Get("RandomEvents.MultiplayerTitle");
        public string ChanceHelpText => SerpLocalization.Get("RandomEvents.ChanceHelp");
        public string StrengthTitleText => SerpLocalization.Get("RandomEvents.StrengthTitle");
        public string ScaledStrengthHelpText => SerpLocalization.Get("RandomEvents.ScaledStrengthHelp");
        public string PlagueStrengthHelpText => SerpLocalization.Get("RandomEvents.PlagueStrengthHelp");
        public string LionStrengthHelpText => SerpLocalization.Get("RandomEvents.LionStrengthHelp");
        public string TheftStrengthHelpText => SerpLocalization.Get("RandomEvents.TheftStrengthHelp");
        public string FireStrengthHelpText => SerpLocalization.Get("RandomEvents.FireStrengthHelp");
        public string MinimumText => SerpLocalization.Get("RandomEvents.Minimum");
        public string MaximumText => SerpLocalization.Get("RandomEvents.Maximum");
        public string MultiplayerModeText => SerpLocalization.Get("RandomEvents.MultiplayerMode");
        public string MultiplayerModeHelpText => SerpLocalization.Get("RandomEvents.MultiplayerModeHelp");
        public string[] MultiplayerModeOptions => new[]
        {
            SerpLocalization.Get("RandomEvents.MultiplayerShared"),
            SerpLocalization.Get("RandomEvents.MultiplayerIndividual")
        };

        public string FairText => EventText("Fair");
        public string PlagueText => EventText("Plague");
        public string WheatInfestationText => EventText("WheatInfestation");
        public string HopsBeetlesText => EventText("HopsBeetles");
        public string AppleBlightText => EventText("AppleBlight");
        public string TreeBlightText => EventText("TreeBlight");
        public string RabbitsText => EventText("Rabbits");
        public string LionAttackText => EventText("LionAttack");
        public string BanditsText => EventText("Bandits");
        public string MadCowsText => EventText("MadCows");
        public string ArchersText => EventText("Archers");
        public string MarriageText => EventText("Marriage");
        public string BardText => EventText("Bard");
        public string GranaryTheftText => EventText("GranaryTheft");
        public string FireText => EventText("Fire");

        public string IntervalMonthsValueText { get => FormatLocalizedValue("RandomEvents.MonthsValueFormat", IntervalMonths); set => SetIntValueText(value, parsed => IntervalMonths = parsed, nameof(IntervalMonthsValueText)); }
        public string CooldownMonthsValueText { get => FormatLocalizedValue("RandomEvents.MonthsValueFormat", CooldownMonths); set => SetIntValueText(value, parsed => CooldownMonths = parsed, nameof(CooldownMonthsValueText)); }
        public string FairChanceValueText { get => FormatPercent(FairChance); set => SetIntValueText(value, parsed => FairChance = parsed, nameof(FairChanceValueText)); }
        public string PlagueChanceValueText { get => FormatPercent(PlagueChance); set => SetIntValueText(value, parsed => PlagueChance = parsed, nameof(PlagueChanceValueText)); }
        public string WheatInfestationChanceValueText { get => FormatPercent(WheatInfestationChance); set => SetIntValueText(value, parsed => WheatInfestationChance = parsed, nameof(WheatInfestationChanceValueText)); }
        public string HopsBeetlesChanceValueText { get => FormatPercent(HopsBeetlesChance); set => SetIntValueText(value, parsed => HopsBeetlesChance = parsed, nameof(HopsBeetlesChanceValueText)); }
        public string AppleBlightChanceValueText { get => FormatPercent(AppleBlightChance); set => SetIntValueText(value, parsed => AppleBlightChance = parsed, nameof(AppleBlightChanceValueText)); }
        public string TreeBlightChanceValueText { get => FormatPercent(TreeBlightChance); set => SetIntValueText(value, parsed => TreeBlightChance = parsed, nameof(TreeBlightChanceValueText)); }
        public string RabbitsChanceValueText { get => FormatPercent(RabbitsChance); set => SetIntValueText(value, parsed => RabbitsChance = parsed, nameof(RabbitsChanceValueText)); }
        public string LionAttackChanceValueText { get => FormatPercent(LionAttackChance); set => SetIntValueText(value, parsed => LionAttackChance = parsed, nameof(LionAttackChanceValueText)); }
        public string BanditsChanceValueText { get => FormatPercent(BanditsChance); set => SetIntValueText(value, parsed => BanditsChance = parsed, nameof(BanditsChanceValueText)); }
        public string MadCowsChanceValueText { get => FormatPercent(MadCowsChance); set => SetIntValueText(value, parsed => MadCowsChance = parsed, nameof(MadCowsChanceValueText)); }
        public string ArchersChanceValueText { get => FormatPercent(ArchersChance); set => SetIntValueText(value, parsed => ArchersChance = parsed, nameof(ArchersChanceValueText)); }
        public string MarriageChanceValueText { get => FormatPercent(MarriageChance); set => SetIntValueText(value, parsed => MarriageChance = parsed, nameof(MarriageChanceValueText)); }
        public string BardChanceValueText { get => FormatPercent(BardChance); set => SetIntValueText(value, parsed => BardChance = parsed, nameof(BardChanceValueText)); }
        public string GranaryTheftChanceValueText { get => FormatPercent(GranaryTheftChance); set => SetIntValueText(value, parsed => GranaryTheftChance = parsed, nameof(GranaryTheftChanceValueText)); }
        public string FireChanceValueText { get => FormatPercent(FireChance); set => SetIntValueText(value, parsed => FireChance = parsed, nameof(FireChanceValueText)); }
        public string PlagueMinValueText { get => PlagueMin.ToString(CultureInfo.InvariantCulture); set => SetIntValueText(value, parsed => PlagueMin = parsed, nameof(PlagueMinValueText)); }
        public string PlagueMaxValueText { get => PlagueMax.ToString(CultureInfo.InvariantCulture); set => SetIntValueText(value, parsed => PlagueMax = parsed, nameof(PlagueMaxValueText)); }
        public string LionMinValueText { get => FormatLocalizedValue("RandomEvents.GroupsValueFormat", LionMin); set => SetIntValueText(value, parsed => LionMin = parsed, nameof(LionMinValueText)); }
        public string LionMaxValueText { get => FormatLocalizedValue("RandomEvents.GroupsValueFormat", LionMax); set => SetIntValueText(value, parsed => LionMax = parsed, nameof(LionMaxValueText)); }
        public string BanditMinValueText { get => FormatFactor(BanditMin); set => SetDoubleValueText(value, parsed => BanditMin = parsed, nameof(BanditMinValueText)); }
        public string BanditMaxValueText { get => FormatFactor(BanditMax); set => SetDoubleValueText(value, parsed => BanditMax = parsed, nameof(BanditMaxValueText)); }
        public string ArcherMinValueText { get => FormatFactor(ArcherMin); set => SetDoubleValueText(value, parsed => ArcherMin = parsed, nameof(ArcherMinValueText)); }
        public string ArcherMaxValueText { get => FormatFactor(ArcherMax); set => SetDoubleValueText(value, parsed => ArcherMax = parsed, nameof(ArcherMaxValueText)); }
        public string TheftMinValueText { get => FormatPercent(TheftMin); set => SetIntValueText(value, parsed => TheftMin = parsed, nameof(TheftMinValueText)); }
        public string TheftMaxValueText { get => FormatPercent(TheftMax); set => SetIntValueText(value, parsed => TheftMax = parsed, nameof(TheftMaxValueText)); }
        public string FireMinValueText { get => FireMin.ToString(CultureInfo.InvariantCulture); set => SetIntValueText(value, parsed => FireMin = parsed, nameof(FireMinValueText)); }
        public string FireMaxValueText { get => FireMax.ToString(CultureInfo.InvariantCulture); set => SetIntValueText(value, parsed => FireMax = parsed, nameof(FireMaxValueText)); }

        [SyncHostOnly] public bool EnableMod { get => enableMod; set => Set(ref enableMod, value, nameof(EnableMod)); }
        [SyncHostOnly] public int IntervalMonths { get => intervalMonths; set => SetClamped(ref intervalMonths, value, 1, 90, nameof(IntervalMonths)); }
        [SyncHostOnly] public int CooldownMonths { get => cooldownMonths; set => SetClamped(ref cooldownMonths, value, 0, 90, nameof(CooldownMonths)); }
        [SyncHostOnly] public int MultiplayerEventModeIndex { get => multiplayerEventMode; set => SetClamped(ref multiplayerEventMode, value, 0, 1, nameof(MultiplayerEventModeIndex)); }

        [SyncHostOnly] public int FairChance { get => Chance(RandomEventKind.Fair); set => SetChance(RandomEventKind.Fair, value, nameof(FairChance)); }
        [SyncHostOnly] public int PlagueChance { get => Chance(RandomEventKind.Plague); set => SetChance(RandomEventKind.Plague, value, nameof(PlagueChance)); }
        [SyncHostOnly] public int WheatInfestationChance { get => Chance(RandomEventKind.WheatInfestation); set => SetChance(RandomEventKind.WheatInfestation, value, nameof(WheatInfestationChance)); }
        [SyncHostOnly] public int HopsBeetlesChance { get => Chance(RandomEventKind.HopsBeetles); set => SetChance(RandomEventKind.HopsBeetles, value, nameof(HopsBeetlesChance)); }
        [SyncHostOnly] public int AppleBlightChance { get => Chance(RandomEventKind.AppleBlight); set => SetChance(RandomEventKind.AppleBlight, value, nameof(AppleBlightChance)); }
        [SyncHostOnly] public int TreeBlightChance { get => Chance(RandomEventKind.TreeBlight); set => SetChance(RandomEventKind.TreeBlight, value, nameof(TreeBlightChance)); }
        [SyncHostOnly] public int RabbitsChance { get => Chance(RandomEventKind.Rabbits); set => SetChance(RandomEventKind.Rabbits, value, nameof(RabbitsChance)); }
        [SyncHostOnly] public int LionAttackChance { get => Chance(RandomEventKind.LionAttack); set => SetChance(RandomEventKind.LionAttack, value, nameof(LionAttackChance)); }
        [SyncHostOnly] public int BanditsChance { get => Chance(RandomEventKind.Bandits); set => SetChance(RandomEventKind.Bandits, value, nameof(BanditsChance)); }
        [SyncHostOnly] public int MadCowsChance { get => Chance(RandomEventKind.MadCows); set => SetChance(RandomEventKind.MadCows, value, nameof(MadCowsChance)); }
        [SyncHostOnly] public int ArchersChance { get => Chance(RandomEventKind.Archers); set => SetChance(RandomEventKind.Archers, value, nameof(ArchersChance)); }
        [SyncHostOnly] public int MarriageChance { get => Chance(RandomEventKind.Marriage); set => SetChance(RandomEventKind.Marriage, value, nameof(MarriageChance)); }
        [SyncHostOnly] public int BardChance { get => Chance(RandomEventKind.Bard); set => SetChance(RandomEventKind.Bard, value, nameof(BardChance)); }
        [SyncHostOnly] public int GranaryTheftChance { get => Chance(RandomEventKind.GranaryTheft); set => SetChance(RandomEventKind.GranaryTheft, value, nameof(GranaryTheftChance)); }
        [SyncHostOnly] public int FireChance { get => Chance(RandomEventKind.Fire); set => SetChance(RandomEventKind.Fire, value, nameof(FireChance)); }

        [SyncHostOnly] public int PlagueMin { get => plagueMin; set => SetMinimum(ref plagueMin, ref plagueMax, value, 10, nameof(PlagueMin), nameof(PlagueMax)); }
        [SyncHostOnly] public int PlagueMax { get => plagueMax; set => SetMaximum(ref plagueMin, ref plagueMax, value, 10, nameof(PlagueMin), nameof(PlagueMax)); }
        [SyncHostOnly] public int LionMin { get => lionMin; set => SetMinimum(ref lionMin, ref lionMax, value, 10, nameof(LionMin), nameof(LionMax)); }
        [SyncHostOnly] public int LionMax { get => lionMax; set => SetMaximum(ref lionMin, ref lionMax, value, 10, nameof(LionMin), nameof(LionMax)); }
        [SyncHostOnly] public double BanditMin { get => banditMin; set => SetScaledMinimum(ref banditMin, ref banditMax, value, nameof(BanditMin), nameof(BanditMax)); }
        [SyncHostOnly] public double BanditMax { get => banditMax; set => SetScaledMaximum(ref banditMin, ref banditMax, value, nameof(BanditMin), nameof(BanditMax)); }
        [SyncHostOnly] public double ArcherMin { get => archerMin; set => SetScaledMinimum(ref archerMin, ref archerMax, value, nameof(ArcherMin), nameof(ArcherMax)); }
        [SyncHostOnly] public double ArcherMax { get => archerMax; set => SetScaledMaximum(ref archerMin, ref archerMax, value, nameof(ArcherMin), nameof(ArcherMax)); }
        [SyncHostOnly] public int TheftMin { get => theftMin; set => SetMinimum(ref theftMin, ref theftMax, value, 100, nameof(TheftMin), nameof(TheftMax)); }
        [SyncHostOnly] public int TheftMax { get => theftMax; set => SetMaximum(ref theftMin, ref theftMax, value, 100, nameof(TheftMin), nameof(TheftMax)); }
        [SyncHostOnly] public int FireMin { get => fireMin; set => SetMinimum(ref fireMin, ref fireMax, value, 10, nameof(FireMin), nameof(FireMax)); }
        [SyncHostOnly] public int FireMax { get => fireMax; set => SetMaximum(ref fireMin, ref fireMax, value, 10, nameof(FireMin), nameof(FireMax)); }

        internal int[] SnapshotChances() => (int[])chances.Clone();

        internal void GetStrengthRange(RandomEventStrengthKind kind, out int minimum, out int maximum)
        {
            switch (kind)
            {
                case RandomEventStrengthKind.Plague: minimum = PlagueMin; maximum = PlagueMax; break;
                case RandomEventStrengthKind.LionAttack: minimum = LionMin; maximum = LionMax; break;
                case RandomEventStrengthKind.Bandits: minimum = EncodeScaledStrength(BanditMin); maximum = EncodeScaledStrength(BanditMax); break;
                case RandomEventStrengthKind.Archers: minimum = EncodeScaledStrength(ArcherMin); maximum = EncodeScaledStrength(ArcherMax); break;
                case RandomEventStrengthKind.GranaryTheft: minimum = TheftMin; maximum = TheftMax; break;
                case RandomEventStrengthKind.Fire: minimum = FireMin; maximum = FireMax; break;
                default: minimum = 0; maximum = 0; break;
            }
        }

        private int Chance(RandomEventKind kind) => chances[(int)kind];

        private void SetChance(RandomEventKind kind, int value, string propertyName)
        {
            if (!CanMutateSetting(propertyName))
                return;

            int normalized = Clamp(value, 0, 100);
            int index = (int)kind;
            if (chances[index] == normalized)
                return;
            chances[index] = normalized;
            Changed(propertyName);
        }

        private void ResetToDefault()
        {
            if (!CanEditHostSettings)
                return;

            EnableMod = true;
            IntervalMonths = 3;
            CooldownMonths = 6;
            MultiplayerEventModeIndex = (int)MultiplayerEventMode.SharedEvents;
            for (int index = 0; index < chances.Length; index++)
                SetChance((RandomEventKind)index, 2, GetChancePropertyName((RandomEventKind)index));
            PlagueMin = 1; PlagueMax = 10;
            LionMin = 1; LionMax = 10;
            BanditMin = 0.1; BanditMax = 5.0;
            ArcherMin = 0.1; ArcherMax = 5.0;
            TheftMin = 1; TheftMax = 100;
            FireMin = 1; FireMax = 10;
        }

        private void SetMinimum(ref int minimum, ref int maximum, int value, int limit, string minName, string maxName)
        {
            if (!CanMutateSetting(minName))
                return;

            int normalized = Clamp(value, 1, limit);
            bool minChanged = minimum != normalized;
            minimum = normalized;
            bool maxChanged = maximum < minimum;
            if (maxChanged)
                maximum = minimum;
            if (minChanged) Changed(minName);
            if (maxChanged) Changed(maxName);
        }

        private void SetMaximum(ref int minimum, ref int maximum, int value, int limit, string minName, string maxName)
        {
            if (!CanMutateSetting(maxName))
                return;

            int normalized = Clamp(value, 1, limit);
            bool maxChanged = maximum != normalized;
            maximum = normalized;
            bool minChanged = minimum > maximum;
            if (minChanged)
                minimum = maximum;
            if (maxChanged) Changed(maxName);
            if (minChanged) Changed(minName);
        }

        private void SetScaledMinimum(ref double minimum, ref double maximum, double value, string minName, string maxName)
        {
            if (!CanMutateSetting(minName))
                return;

            double normalized = NormalizeScaledStrength(value);
            bool minChanged = minimum != normalized;
            minimum = normalized;
            bool maxChanged = maximum < minimum;
            if (maxChanged)
                maximum = minimum;
            if (minChanged) Changed(minName);
            if (maxChanged) Changed(maxName);
        }

        private void SetScaledMaximum(ref double minimum, ref double maximum, double value, string minName, string maxName)
        {
            if (!CanMutateSetting(maxName))
                return;

            double normalized = NormalizeScaledStrength(value);
            bool maxChanged = maximum != normalized;
            maximum = normalized;
            bool minChanged = minimum > maximum;
            if (minChanged)
                minimum = maximum;
            if (maxChanged) Changed(maxName);
            if (minChanged) Changed(minName);
        }

        private void Set<T>(ref T field, T value, string propertyName)
        {
            if (!CanMutateSetting(propertyName))
                return;

            if (Equals(field, value)) return;
            field = value;
            Changed(propertyName);
        }

        private void SetClamped(ref int field, int value, int minimum, int maximum, string propertyName) =>
            Set(ref field, Clamp(value, minimum, maximum), propertyName);

        private void Changed(string propertyName)
        {
            OnPropertyChanged(propertyName);
            string valueTextProperty = GetValueTextPropertyName(propertyName);
            if (valueTextProperty != null)
                OnPropertyChanged(valueTextProperty);
        }

        private static string GetValueTextPropertyName(string propertyName)
        {
            if (propertyName.EndsWith("Chance", StringComparison.Ordinal))
                return propertyName + "ValueText";

            switch (propertyName)
            {
                case nameof(IntervalMonths): return nameof(IntervalMonthsValueText);
                case nameof(CooldownMonths): return nameof(CooldownMonthsValueText);
                case nameof(PlagueMin): return nameof(PlagueMinValueText);
                case nameof(PlagueMax): return nameof(PlagueMaxValueText);
                case nameof(LionMin): return nameof(LionMinValueText);
                case nameof(LionMax): return nameof(LionMaxValueText);
                case nameof(BanditMin): return nameof(BanditMinValueText);
                case nameof(BanditMax): return nameof(BanditMaxValueText);
                case nameof(ArcherMin): return nameof(ArcherMinValueText);
                case nameof(ArcherMax): return nameof(ArcherMaxValueText);
                case nameof(TheftMin): return nameof(TheftMinValueText);
                case nameof(TheftMax): return nameof(TheftMaxValueText);
                case nameof(FireMin): return nameof(FireMinValueText);
                case nameof(FireMax): return nameof(FireMaxValueText);
                default: return null;
            }
        }

        private void SetIntValueText(string text, Action<int> setValue, string textPropertyName)
        {
            if (Shared.NumericTextInput.TryParseInt(text, out int parsed))
                setValue(parsed);

            // Invalid and clamped input returns to the authoritative formatted value.
            OnPropertyChanged(textPropertyName);
        }

        private void SetDoubleValueText(string text, Action<double> setValue, string textPropertyName)
        {
            if (Shared.NumericTextInput.TryParseDouble(text, out double parsed))
                setValue(parsed);

            OnPropertyChanged(textPropertyName);
        }

        private static string FormatLocalizedValue(string key, int value) =>
            string.Format(CultureInfo.CurrentCulture, SerpLocalization.Get(key), value);

        private static string FormatPercent(int value) =>
            value.ToString(CultureInfo.InvariantCulture) + "%";

        private static string FormatFactor(double value) =>
            value.ToString("0.0", CultureInfo.InvariantCulture) + "x";

        private static int Clamp(int value, int minimum, int maximum) => Math.Max(minimum, Math.Min(maximum, value));
        private static double NormalizeScaledStrength(double value) =>
            Math.Round(Math.Max(0.1, Math.Min(5.0, value)), 1, MidpointRounding.AwayFromZero);
        private static int EncodeScaledStrength(double value) =>
            (int)Math.Round(NormalizeScaledStrength(value) * 10.0, MidpointRounding.AwayFromZero);
        private static string EventText(string suffix) => SerpLocalization.Get("RandomEvents.Event." + suffix);

        private static string GetChancePropertyName(RandomEventKind kind) =>
            kind == RandomEventKind.WheatInfestation ? nameof(WheatInfestationChance) :
            kind == RandomEventKind.HopsBeetles ? nameof(HopsBeetlesChance) :
            kind == RandomEventKind.AppleBlight ? nameof(AppleBlightChance) :
            kind == RandomEventKind.TreeBlight ? nameof(TreeBlightChance) :
            kind == RandomEventKind.LionAttack ? nameof(LionAttackChance) :
            kind == RandomEventKind.MadCows ? nameof(MadCowsChance) :
            kind == RandomEventKind.GranaryTheft ? nameof(GranaryTheftChance) : kind + "Chance";
    }
}
