using Noesis;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace BuildingCosts
{
    public sealed class BuildingCostMissingNotificationViewModel : INotifyPropertyChanged
    {
        private const int DisplayMilliseconds = 3000;
        private const string DefaultMessage = "Missing building materials";
        private static readonly TimeSpan Cooldown = TimeSpan.FromMilliseconds(750);
        private static readonly Dictionary<string, string> MissingBuildingMaterialsMessages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = DefaultMessage,
            ["en-US"] = DefaultMessage,
            ["en-GB"] = DefaultMessage,
            ["de"] = "Baumaterial fehlt",
            ["de-DE"] = "Baumaterial fehlt",
            ["fr"] = "Materiaux de construction manquants",
            ["fr-FR"] = "Materiaux de construction manquants",
            ["it"] = "Materiali da costruzione mancanti",
            ["it-IT"] = "Materiali da costruzione mancanti",
            ["es"] = "Faltan materiales de construccion",
            ["es-ES"] = "Faltan materiales de construccion",
            ["pt"] = "Faltam materiais de construcao",
            ["pt-BR"] = "Faltam materiais de construcao",
            ["pl"] = "Brakuje materialow budowlanych",
            ["pl-PL"] = "Brakuje materialow budowlanych",
            ["ru"] = "Ne khvataet stroitelnykh materialov",
            ["ru-RU"] = "Ne khvataet stroitelnykh materialov",
            ["tr"] = "Insaat malzemesi eksik",
            ["tr-TR"] = "Insaat malzemesi eksik",
            ["ja"] = "Kenchiku shizai ga fusoku shiteimasu",
            ["ja-JP"] = "Kenchiku shizai ga fusoku shiteimasu",
            ["ko"] = "Geonchuk jajae ga bujokhamnida",
            ["ko-KR"] = "Geonchuk jajae ga bujokhamnida",
            ["zh"] = "Queshao jianzhu cailiao",
            ["zh-CN"] = "Queshao jianzhu cailiao",
            ["zh-TW"] = "Queshao jianzhu cailiao",
        };

        private string message = string.Empty;
        private bool isVisible;
        private DateTime lastShownAt = DateTime.MinValue;
        private DateTime visibleUntil = DateTime.MinValue;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Message
        {
            get => message;
            private set
            {
                if (message == value)
                    return;

                message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        public bool IsVisible
        {
            get => isVisible;
            private set
            {
                if (isVisible == value)
                    return;

                isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
                OnPropertyChanged(nameof(Visibility));
            }
        }

        public Visibility Visibility => IsVisible ? Noesis.Visibility.Visible : Noesis.Visibility.Collapsed;

        public void ShowMissingBuildingMaterials()
        {
            Show(GetMissingBuildingMaterialsMessage());
        }

        public void Show(string message)
        {
            DateTime now = DateTime.UtcNow;
            if (now - lastShownAt < Cooldown)
                return;

            lastShownAt = now;
            visibleUntil = now.AddMilliseconds(DisplayMilliseconds);
            Message = message;
            IsVisible = true;
        }

        public void Update()
        {
            if (IsVisible && DateTime.UtcNow >= visibleUntil)
                IsVisible = false;
        }

        private static string GetMissingBuildingMaterialsMessage()
        {
            string language = GameAssetManagerAPI.Instance.CurrentLanguage;
            if (TryGetMissingBuildingMaterialsMessage(language, out string message))
                return message;

            if (TryGetMissingBuildingMaterialsMessage(CultureInfo.CurrentUICulture.Name, out message))
                return message;

            return TryGetMissingBuildingMaterialsMessage(CultureInfo.CurrentCulture.Name, out message) ? message : DefaultMessage;
        }

        private static bool TryGetMissingBuildingMaterialsMessage(string language, out string message)
        {
            message = DefaultMessage;
            if (string.IsNullOrWhiteSpace(language))
                return false;

            string normalizedLanguage = language.Replace('_', '-');
            if (MissingBuildingMaterialsMessages.TryGetValue(normalizedLanguage, out string localizedMessage))
            {
                message = localizedMessage;
                return true;
            }

            int separatorIndex = normalizedLanguage.IndexOf('-');
            if (separatorIndex > 0 && MissingBuildingMaterialsMessages.TryGetValue(normalizedLanguage.Substring(0, separatorIndex), out localizedMessage))
            {
                message = localizedMessage;
                return true;
            }

            return false;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
