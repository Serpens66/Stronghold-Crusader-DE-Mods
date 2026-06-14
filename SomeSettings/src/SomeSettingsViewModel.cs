using Noesis;
using SHCDESE.API.Components.Network;
using SHCDESE.Interop;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;

namespace SomeSettings
{
    public sealed class SomeSettingsViewModel : LobbyModSettingsBaseViewModel
    {
        public event Action<string> SettingChanged;

        private string woodRefundPercentText = "-1";
        private string stoneRefundPercentText = "-1";
        private string ironRefundPercentText = "-1";
        private string pitchRefundPercentText = "-1";
        private string goldRefundPercentText = "-1";
        private int multiplyGoodsGainAI = 1;
        private int multiplyGoodsGainHuman = 1;
        private int multiplyGoodsGainInMoneyAI;
        private int multiplyGoodsGainInMoneyHuman;
        private bool keepStorageContent;

        public SomeSettingsViewModel()
        {
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public RelayCommand ResetToDefaultCommand { get; }
        public ImageSource WoodRefundIcon => GetGoodIconImage(eGoods.STORED_WOOD_PLANKS);
        public ImageSource StoneRefundIcon => GetGoodIconImage(eGoods.STORED_STONE_BLOCKS);
        public ImageSource IronRefundIcon => GetGoodIconImage(eGoods.STORED_IRON_INGOTS);
        public ImageSource PitchRefundIcon => GetGoodIconImage(eGoods.STORED_PITCH_RAW);
        public ImageSource GoldRefundIcon => GetGoodIconImage(eGoods.STORED_GOLD);
        public ImageSource KeepStorageFruitIcon => GetGoodIconImage(eGoods.STORED_FOOD_FRUIT);
        public ImageSource KeepStorageWoodIcon => GetGoodIconImage(eGoods.STORED_WOOD_PLANKS);
        public ImageSource KeepStorageBowsIcon => GetGoodIconImage(eGoods.STORED_BOWS);
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public string BulldozeTitleText => SerpLocalization.Get(SerpLocalization.BulldozeTitle);
        public string BulldozeHelpText => SerpLocalization.Get(SerpLocalization.BulldozeHelp);
        public string WoodRefundText => SerpLocalization.Get(SerpLocalization.WoodRefund);
        public string StoneRefundText => SerpLocalization.Get(SerpLocalization.StoneRefund);
        public string IronRefundText => SerpLocalization.Get(SerpLocalization.IronRefund);
        public string PitchRefundText => SerpLocalization.Get(SerpLocalization.PitchRefund);
        public string GoldRefundText => SerpLocalization.Get(SerpLocalization.GoldRefund);
        public string VanillaValue50Text => SerpLocalization.Get(SerpLocalization.VanillaValue50);
        public string KeepStorageContentText => SerpLocalization.Get(SerpLocalization.KeepStorageContent);
        public string KeepStorageContentHelpText => SerpLocalization.Get(SerpLocalization.KeepStorageContentHelp);
        public string EconomyBuffsTitleText => SerpLocalization.Get(SerpLocalization.EconomyBuffsTitle);
        public string AiText => SerpLocalization.Get(SerpLocalization.Ai);
        public string HumanText => SerpLocalization.Get(SerpLocalization.Human);
        public string MultiplyGoodsGainText => SerpLocalization.Get(SerpLocalization.MultiplyGoodsGain);
        public string MultiplyGoodsGainHelpText => SerpLocalization.Get(SerpLocalization.MultiplyGoodsGainHelp);
        public string MultiplyGoodsAsMoneyText => SerpLocalization.Get(SerpLocalization.MultiplyGoodsAsMoney);
        public string MultiplyGoodsAsMoneyHelpText => SerpLocalization.Get(SerpLocalization.MultiplyGoodsAsMoneyHelp);

        [SyncHostOnly]
        public string WoodRefundPercentText
        {
            get => woodRefundPercentText;
            set => SetTextSetting(ref woodRefundPercentText, value, nameof(WoodRefundPercentText));
        }

        [SyncHostOnly]
        public string StoneRefundPercentText
        {
            get => stoneRefundPercentText;
            set => SetTextSetting(ref stoneRefundPercentText, value, nameof(StoneRefundPercentText));
        }

        [SyncHostOnly]
        public string IronRefundPercentText
        {
            get => ironRefundPercentText;
            set => SetTextSetting(ref ironRefundPercentText, value, nameof(IronRefundPercentText));
        }

        [SyncHostOnly]
        public string PitchRefundPercentText
        {
            get => pitchRefundPercentText;
            set => SetTextSetting(ref pitchRefundPercentText, value, nameof(PitchRefundPercentText));
        }

        [SyncHostOnly]
        public string GoldRefundPercentText
        {
            get => goldRefundPercentText;
            set => SetTextSetting(ref goldRefundPercentText, value, nameof(GoldRefundPercentText));
        }

        [SyncHostOnly]
        public bool KeepStorageContent
        {
            get => keepStorageContent;
            set
            {
                if (keepStorageContent == value)
                    return;

                keepStorageContent = value;
                SettingChanged?.Invoke(nameof(KeepStorageContent));
                OnPropertyChanged(nameof(KeepStorageContent));
            }
        }

        public int WoodRefundPercent => ParsePercentOrUnchanged(WoodRefundPercentText);
        public int StoneRefundPercent => ParsePercentOrUnchanged(StoneRefundPercentText);
        public int IronRefundPercent => ParsePercentOrUnchanged(IronRefundPercentText);
        public int PitchRefundPercent => ParsePercentOrUnchanged(PitchRefundPercentText);
        public int GoldRefundPercent => ParsePercentOrUnchanged(GoldRefundPercentText);

        [SyncHostOnly] public int MultiplyGoodsGainAI { get => multiplyGoodsGainAI; set => SetIntSetting(ref multiplyGoodsGainAI, value, nameof(MultiplyGoodsGainAI), nameof(MultiplyGoodsGainAIText)); }
        [SyncHostOnly] public int MultiplyGoodsGainHuman { get => multiplyGoodsGainHuman; set => SetIntSetting(ref multiplyGoodsGainHuman, value, nameof(MultiplyGoodsGainHuman), nameof(MultiplyGoodsGainHumanText)); }
        [SyncHostOnly] public int MultiplyGoodsGainInMoneyAI { get => multiplyGoodsGainInMoneyAI; set => SetIntSetting(ref multiplyGoodsGainInMoneyAI, value, nameof(MultiplyGoodsGainInMoneyAI), nameof(MultiplyGoodsGainInMoneyAIText)); }
        [SyncHostOnly] public int MultiplyGoodsGainInMoneyHuman { get => multiplyGoodsGainInMoneyHuman; set => SetIntSetting(ref multiplyGoodsGainInMoneyHuman, value, nameof(MultiplyGoodsGainInMoneyHuman), nameof(MultiplyGoodsGainInMoneyHumanText)); }

        public string MultiplyGoodsGainAIText { get => MultiplyGoodsGainAI.ToString(); set => SetIntText(value, parsed => MultiplyGoodsGainAI = parsed, nameof(MultiplyGoodsGainAIText)); }
        public string MultiplyGoodsGainHumanText { get => MultiplyGoodsGainHuman.ToString(); set => SetIntText(value, parsed => MultiplyGoodsGainHuman = parsed, nameof(MultiplyGoodsGainHumanText)); }
        public string MultiplyGoodsGainInMoneyAIText { get => MultiplyGoodsGainInMoneyAI.ToString(); set => SetIntText(value, parsed => MultiplyGoodsGainInMoneyAI = parsed, nameof(MultiplyGoodsGainInMoneyAIText)); }
        public string MultiplyGoodsGainInMoneyHumanText { get => MultiplyGoodsGainInMoneyHuman.ToString(); set => SetIntText(value, parsed => MultiplyGoodsGainInMoneyHuman = parsed, nameof(MultiplyGoodsGainInMoneyHumanText)); }

        private void ResetToDefault()
        {
            WoodRefundPercentText = "-1";
            StoneRefundPercentText = "-1";
            IronRefundPercentText = "-1";
            PitchRefundPercentText = "-1";
            GoldRefundPercentText = "-1";
            KeepStorageContent = false;
            MultiplyGoodsGainAI = 1;
            MultiplyGoodsGainHuman = 1;
            MultiplyGoodsGainInMoneyAI = 0;
            MultiplyGoodsGainInMoneyHuman = 0;
        }

        private void SetTextSetting(ref string field, string value, string propertyName)
        {
            string normalized = NormalizePercentText(value);
            if (field == normalized)
                return;

            field = normalized;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private void SetIntSetting(ref int field, int value, string propertyName, string textPropertyName)
        {
            if (field == value)
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(textPropertyName);
        }

        private void SetIntText(string text, Action<int> setValue, string textPropertyName)
        {
            if (!int.TryParse(text, out int parsed))
            {
                OnPropertyChanged(textPropertyName);
                return;
            }

            setValue(parsed);
        }

        private static string NormalizePercentText(string value)
        {
            int parsed = ParsePercentOrUnchanged(value);
            return parsed.ToString();
        }

        private static int ParsePercentOrUnchanged(string value)
        {
            if (!int.TryParse(value, out int parsed))
                return -1;

            if (parsed < -1)
                return -1;

            if (parsed > 100)
                return 100;

            return parsed;
        }

        private static ImageSource GetGoodIconImage(eGoods good)
        {
            try
            {
                return CrusaderDE.MainViewModel.Instance?.getSmallGoodsIcon((int)good);
            }
            catch
            {
                return null;
            }
        }
    }
}
