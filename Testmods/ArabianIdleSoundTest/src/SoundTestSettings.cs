using APIShared.ModSettings;
using APIShared.Audio;
using System.Linq;
using SHCDESE.NoesisUtil;
using System;

namespace ArabianIdleSoundTest
{
    // Local presentation preferences: never synchronized as gameplay/host settings.
    public sealed class SoundTestSettings : PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true;
        private int probeIndex, situationIndex, unitTypeIndex, cooldownMilliseconds;
        internal static readonly int[] UnitTypes = { -1, 5, 22, 23, 24, 25, 26, 27, 28, 29, 30, 37, 55, 70, 71, 72, 73, 74, 75, 76, 77, 78, 80, 81, 82, 83, 84, 85 };
        internal static readonly SoundSituation[] Situations = Enum.GetValues(typeof(SoundSituation)).Cast<SoundSituation>().Where(x => x != SoundSituation.Unknown).ToArray();
        public event Action SettingChanged;
        public SoundTestSettings() { ResetToDefaultCommand = new RelayCommand(ResetDefaults); }
        public RelayCommand ResetToDefaultCommand { get; }
        [PresetLocal]
        public bool EnableMod
        {
            get => enableMod;
            set
            {
                if (!CanMutateSetting() || value == enableMod) return;
                enableMod = value; SettingChanged?.Invoke(); OnPropertyChanged(nameof(EnableMod));
            }
        }
        [PresetLocal]
        public int ProbeIndex
        {
            get => probeIndex;
            set
            {
                if (value < 0 || value > 4 || !CanMutateSetting() || value == probeIndex) return;
                probeIndex = value; SettingChanged?.Invoke(); OnPropertyChanged(nameof(ProbeIndex));
            }
        }
        [PresetLocal]
        public int SituationIndex
        {
            get => situationIndex;
            set {
                if (value < 0 || value >= Situations.Length || !CanMutateSetting() || value == situationIndex) return;
                situationIndex = value; SettingChanged?.Invoke(); OnPropertyChanged(nameof(SituationIndex));
            }
        }
        [PresetLocal]
        public int UnitTypeIndex
        {
            get => unitTypeIndex;
            set {
                if (value < 0 || value >= UnitTypes.Length || !CanMutateSetting() || value == unitTypeIndex) return;
                unitTypeIndex = value; SettingChanged?.Invoke(); OnPropertyChanged(nameof(UnitTypeIndex));
            }
        }
        [PresetLocal]
        public int CooldownMilliseconds
        {
            get => cooldownMilliseconds;
            set {
                if (value < 0 || value > 60000 || !CanMutateSetting() || value == cooldownMilliseconds) return;
                cooldownMilliseconds = value; SettingChanged?.Invoke(); OnPropertyChanged(nameof(CooldownMilliseconds)); OnPropertyChanged(nameof(CooldownText));
            }
        }
        // Text remains a UI projection; presets persist only the validated integer.
        public string CooldownText
        {
            get => cooldownMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            set {
                if (int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsed)) CooldownMilliseconds = parsed;
                OnPropertyChanged(nameof(CooldownText));
            }
        }
        public string UnitTypeTitleText => SerpLocalization.Get("AudioTest.UnitTypeTitle");
        public string UnitTypeHelpText => SerpLocalization.Get("AudioTest.UnitTypeHelp");
        public string[] UnitTypeOptions => UnitTypes.Select(x => SerpLocalization.Get("AudioTest.UnitType." + x)).ToArray();
        public string CooldownTitleText => SerpLocalization.Get("AudioTest.CooldownTitle");
        public string CooldownHelpText => SerpLocalization.Get("AudioTest.CooldownHelp");
        public string SituationTitleText => SerpLocalization.Get("AudioTest.SituationTitle");
        public string SituationHelpText => SerpLocalization.Get("AudioTest.SituationHelp");
        public string[] SituationOptions => Situations.Select(x => SerpLocalization.Get("AudioTest.Situation." + x)).ToArray();
        private void ResetDefaults()
        {
            if (!CanEditClientSettings) return;
            EnableMod = true; ProbeIndex = 0; SituationIndex = 0; UnitTypeIndex = 0; CooldownMilliseconds = 0;
        }
        protected override string ResolveSettingsUiText(string key, string fallback) => SerpLocalization.Get(key);
        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string ProbeTitleText => SerpLocalization.Get("AudioTest.ProbeTitle");
        public string ProbeHelpText => SerpLocalization.Get("AudioTest.ProbeHelp");
        public string ScopeText => SerpLocalization.Get("AudioTest.Scope");
        public string[] ProbeOptions => new[] {
            SerpLocalization.Get("AudioTest.Volume25"), SerpLocalization.Get("AudioTest.Replace"),
            SerpLocalization.Get("AudioTest.Suppress"), SerpLocalization.Get("AudioTest.PanLeft"), SerpLocalization.Get("AudioTest.CustomClip") };
    }
}
