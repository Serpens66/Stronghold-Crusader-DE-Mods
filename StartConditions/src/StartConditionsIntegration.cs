using System;

namespace StartConditions
{
    public sealed class StartConditionsOverrideSettings : IStartConditionsSettings
    {
        public event Action<string> SettingChanged;
        public bool EnableMod { get; set; } = true;
        public int SetStartGoldAI { get; set; } = -1;
        public int SetStartGoldHuman { get; set; } = -1;
        public int AddStartGoldAI { get; set; }
        public int AddStartGoldHuman { get; set; }
        public int MultiplyStartTroopsAI { get; set; } = 1;
        public int MultiplyStartTroopsHuman { get; set; } = 1;
        public string StartGoodsAI { get; set; } = string.Empty;
        public string StartGoodsHuman { get; set; } = string.Empty;
        public string AddStartTroopsAI { get; set; } = string.Empty;
        public string AddStartTroopsHuman { get; set; } = string.Empty;

        public void NotifyChanged(string propertyName) => SettingChanged?.Invoke(propertyName);
    }

    public static class StartConditionsIntegration
    {
        public static event Action MissionOverrideChanged;
        private static readonly object Sync = new object();
        private static string overrideOwner;
        private static StartConditionsOverrideSettings missionOverride;

        public static bool HasMissionOverride
        {
            get
            {
                lock (Sync)
                    return missionOverride != null;
            }
        }

        public static void SetMissionOverride(string owner, StartConditionsOverrideSettings settings)
        {
            if (string.IsNullOrWhiteSpace(owner))
                throw new ArgumentException("An override owner is required.", nameof(owner));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            lock (Sync)
            {
                overrideOwner = owner;
                missionOverride = settings;
            }
            MissionOverrideChanged?.Invoke();
        }

        public static void ClearMissionOverride(string owner)
        {
            lock (Sync)
            {
                if (missionOverride == null || !string.Equals(overrideOwner, owner, StringComparison.Ordinal))
                    return;

                overrideOwner = null;
                missionOverride = null;
            }
            MissionOverrideChanged?.Invoke();
        }

        internal static IStartConditionsSettings GetEffectiveSettings(IStartConditionsSettings normalSettings)
        {
            lock (Sync)
                return missionOverride ?? normalSettings;
        }
    }
}
