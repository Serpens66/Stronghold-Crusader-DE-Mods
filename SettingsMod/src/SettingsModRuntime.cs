using BepInEx.Logging;
using System;

namespace SettingsMod
{
    public sealed partial class SettingsModRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly SettingsModLobbyViewModel settings;
        private bool settingsPropertyChangedSubscribed;
        private bool hooksSubscribed;
        private bool libraryInitialized;

        public SettingsModRuntime(ManualLogSource log, SettingsModLobbyViewModel settings)
        {
            this.log = log;
            this.settings = settings;
        }

        public void SubscribeHooks()
        {
            if (hooksSubscribed)
                return;

            LogInfo("SettingsMod has no runtime hooks after feature split.");
            hooksSubscribed = true;
        }

        public void InitializeAfterLibraryLoaded()
        {
            if (libraryInitialized)
                return;

            ApplyAdvancedSettings();
            SubscribeSettingsChanges();
            LogInfo("Applied immediate settings");
            libraryInitialized = true;
        }

        public void Dispose()
        {
            if (settingsPropertyChangedSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsPropertyChangedSubscribed = false;
            }

        }

        private void LogInfo(params object[] parts)
        {
            log.LogInfo(string.Join(" ", parts));
        }
    }
}
