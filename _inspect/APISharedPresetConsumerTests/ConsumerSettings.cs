using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.Components.Network;
using Shared;

namespace APISharedPresetConsumerTests
{
    public sealed class ConsumerSettings : PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true;
        private int localValue = 7;

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set
            {
                if (!CanMutateSetting() || enableMod == value)
                    return;
                enableMod = value;
                OnPropertyChanged(nameof(EnableMod));
            }
        }

        [PresetLocal]
        public int LocalValue
        {
            get => localValue;
            set
            {
                if (!CanMutateSetting() || localValue == value)
                    return;
                localValue = value;
                OnPropertyChanged(nameof(LocalValue));
            }
        }

        public static void Register(
            BaseUnityPlugin plugin,
            ManualLogSource log,
            ConsumerSettings settings)
        {
            LobbyModSettingsPresetRegistration.Register(
                plugin,
                log,
                "ThirdParty.PresetConsumer",
                settings,
                "ScriptExtenderUI/ThirdPartyPresetSettings.xaml");
        }
    }
}
