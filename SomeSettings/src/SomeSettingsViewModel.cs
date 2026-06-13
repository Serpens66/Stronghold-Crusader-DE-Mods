using SHCDESE.API.Components.Network;
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
        private bool keepStorageContent;

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

        private void SetTextSetting(ref string field, string value, string propertyName)
        {
            string normalized = NormalizePercentText(value);
            if (field == normalized)
                return;

            field = normalized;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
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
    }
}
