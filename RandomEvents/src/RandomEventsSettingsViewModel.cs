using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;

namespace RandomEvents
{
    public sealed class RandomEventsSettingsViewModel : LobbyModSettingsBaseViewModel
    {
        private bool enableMod = true;
        private int intervalMonths = 3;
        private readonly int[] chances = new int[15];
        private int plagueMin = 1, plagueMax = 10;
        private int lionMin = 1, lionMax = 10;
        private int banditMin = 1, banditMax = 50;
        private int archerMin = 1, archerMax = 50;
        private int theftMin = 1, theftMax = 100;
        private int fireMin = 1, fireMax = 10;
        private int multiplayerEventMode;

        public RandomEventsSettingsViewModel()
        {
            for (int index = 0; index < chances.Length; index++)
                chances[index] = 1;
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public event Action<string> SettingChanged;
        public RelayCommand ResetToDefaultCommand { get; }

        public string ResetToDefaultText => RandomEventsLocalization.Get("Common.ResetToDefault");
        public string EnableModText => RandomEventsLocalization.Get("Common.EnableMod");
        public string IntervalText => RandomEventsLocalization.Get("RandomEvents.Interval");
        public string IntervalHelpText => RandomEventsLocalization.Get("RandomEvents.IntervalHelp");
        public string ChancesTitleText => RandomEventsLocalization.Get("RandomEvents.ChancesTitle");
        public string StrengthTitleText => RandomEventsLocalization.Get("RandomEvents.StrengthTitle");
        public string MinimumText => RandomEventsLocalization.Get("RandomEvents.Minimum");
        public string MaximumText => RandomEventsLocalization.Get("RandomEvents.Maximum");
        public string MultiplayerModeText => RandomEventsLocalization.Get("RandomEvents.MultiplayerMode");
        public string MultiplayerModeHelpText => RandomEventsLocalization.Get("RandomEvents.MultiplayerModeHelp");
        public string[] MultiplayerModeOptions => new[]
        {
            RandomEventsLocalization.Get("RandomEvents.MultiplayerShared"),
            RandomEventsLocalization.Get("RandomEvents.MultiplayerIndividual")
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

        [SyncHostOnly] public bool EnableMod { get => enableMod; set => Set(ref enableMod, value, nameof(EnableMod)); }
        [SyncHostOnly] public int IntervalMonths { get => intervalMonths; set => SetClamped(ref intervalMonths, value, 1, 90, nameof(IntervalMonths)); }
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
        [SyncHostOnly] public int BanditMin { get => banditMin; set => SetMinimum(ref banditMin, ref banditMax, value, 50, nameof(BanditMin), nameof(BanditMax)); }
        [SyncHostOnly] public int BanditMax { get => banditMax; set => SetMaximum(ref banditMin, ref banditMax, value, 50, nameof(BanditMin), nameof(BanditMax)); }
        [SyncHostOnly] public int ArcherMin { get => archerMin; set => SetMinimum(ref archerMin, ref archerMax, value, 50, nameof(ArcherMin), nameof(ArcherMax)); }
        [SyncHostOnly] public int ArcherMax { get => archerMax; set => SetMaximum(ref archerMin, ref archerMax, value, 50, nameof(ArcherMin), nameof(ArcherMax)); }
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
                case RandomEventStrengthKind.Bandits: minimum = BanditMin; maximum = BanditMax; break;
                case RandomEventStrengthKind.Archers: minimum = ArcherMin; maximum = ArcherMax; break;
                case RandomEventStrengthKind.GranaryTheft: minimum = TheftMin; maximum = TheftMax; break;
                case RandomEventStrengthKind.Fire: minimum = FireMin; maximum = FireMax; break;
                default: minimum = 0; maximum = 0; break;
            }
        }

        private int Chance(RandomEventKind kind) => chances[(int)kind];

        private void SetChance(RandomEventKind kind, int value, string propertyName)
        {
            int normalized = Clamp(value, 0, 100);
            int index = (int)kind;
            if (chances[index] == normalized)
                return;
            chances[index] = normalized;
            Changed(propertyName);
        }

        private void ResetToDefault()
        {
            EnableMod = true;
            IntervalMonths = 3;
            MultiplayerEventModeIndex = (int)MultiplayerEventMode.SharedEvents;
            for (int index = 0; index < chances.Length; index++)
            {
                chances[index] = 1;
                Changed(GetChancePropertyName((RandomEventKind)index));
            }
            PlagueMin = 1; PlagueMax = 10;
            LionMin = 1; LionMax = 10;
            BanditMin = 1; BanditMax = 50;
            ArcherMin = 1; ArcherMax = 50;
            TheftMin = 1; TheftMax = 100;
            FireMin = 1; FireMax = 10;
        }

        private void SetMinimum(ref int minimum, ref int maximum, int value, int limit, string minName, string maxName)
        {
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
            int normalized = Clamp(value, 1, limit);
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
            if (Equals(field, value)) return;
            field = value;
            Changed(propertyName);
        }

        private void SetClamped(ref int field, int value, int minimum, int maximum, string propertyName) =>
            Set(ref field, Clamp(value, minimum, maximum), propertyName);

        private void Changed(string propertyName)
        {
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private static int Clamp(int value, int minimum, int maximum) => Math.Max(minimum, Math.Min(maximum, value));
        private static string EventText(string suffix) => RandomEventsLocalization.Get("RandomEvents.Event." + suffix);

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
