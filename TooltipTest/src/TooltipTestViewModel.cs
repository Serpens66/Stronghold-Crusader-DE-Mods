using SHCDESE.NoesisUtil;
using System.Globalization;

namespace TooltipTest
{
    public sealed class TooltipTestViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        private bool sampleCheckbox = true;
        private int sampleAmount = 50;
        private int sampleMode;

        public TooltipTestViewModel()
        {
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        protected override string ResolveSettingsUiText(string key, string fallback) => SerpLocalization.Get(key);

        public RelayCommand ResetToDefaultCommand { get; }
        public string TitleText => SerpLocalization.Get("TooltipTest.Title");
        public string IntroText => SerpLocalization.Get("TooltipTest.Intro");
        public string ComparisonTitleText => SerpLocalization.Get("TooltipTest.ComparisonTitle");
        public string ControlsTitleText => SerpLocalization.Get("TooltipTest.ControlsTitle");
        public string OldStyleText => SerpLocalization.Get("TooltipTest.OldStyle");
        public string AutoStyleText => SerpLocalization.Get("TooltipTest.AutoStyle");
        public string Fixed1080Text => SerpLocalization.Get("TooltipTest.Fixed1080");
        public string Fixed1440Text => SerpLocalization.Get("TooltipTest.Fixed1440");
        public string Fixed4KText => SerpLocalization.Get("TooltipTest.Fixed4K");
        public string ComparisonHelpText => SerpLocalization.Get("TooltipTest.ComparisonHelp");
        public string CheckboxText => SerpLocalization.Get("TooltipTest.Checkbox");
        public string CheckboxHelpText => SerpLocalization.Get("TooltipTest.CheckboxHelp");
        public string AmountText => SerpLocalization.Get("TooltipTest.Amount");
        public string AmountHelpText => SerpLocalization.Get("TooltipTest.AmountHelp");
        public string ModeText => SerpLocalization.Get("TooltipTest.Mode");
        public string ModeHelpText => SerpLocalization.Get("TooltipTest.ModeHelp");
        public string[] ModeOptions => new[]
        {
            SerpLocalization.Get("TooltipTest.ModeVanilla"),
            SerpLocalization.Get("TooltipTest.ModeBalanced"),
            SerpLocalization.Get("TooltipTest.ModeExtreme")
        };
        public string LongText => SerpLocalization.Get("TooltipTest.LongText");
        public string LongHelpText => SerpLocalization.Get("TooltipTest.LongHelp");

        [Shared.PresetLocal]
        public bool SampleCheckbox
        {
            get => sampleCheckbox;
            set { if (sampleCheckbox == value) return; sampleCheckbox = value; OnPropertyChanged(nameof(SampleCheckbox)); }
        }

        [Shared.PresetLocal]
        public int SampleAmount
        {
            get => sampleAmount;
            set
            {
                int bounded = value < 0 ? 0 : value > 100 ? 100 : value;
                if (sampleAmount == bounded) return;
                sampleAmount = bounded;
                OnPropertyChanged(nameof(SampleAmount));
                OnPropertyChanged(nameof(SampleAmountText));
            }
        }

        public string SampleAmountText
        {
            get => SampleAmount.ToString(CultureInfo.CurrentCulture);
            set
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed))
                    SampleAmount = parsed;
                else
                    OnPropertyChanged(nameof(SampleAmountText));
            }
        }

        [Shared.PresetLocal]
        public int SampleMode
        {
            get => sampleMode;
            set
            {
                int bounded = value < 0 ? 0 : value > 2 ? 2 : value;
                if (sampleMode == bounded) return;
                sampleMode = bounded;
                OnPropertyChanged(nameof(SampleMode));
            }
        }

        private void ResetToDefault()
        {
            SampleCheckbox = true;
            SampleAmount = 50;
            SampleMode = 0;
        }
    }
}
