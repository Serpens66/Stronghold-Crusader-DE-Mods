using SHCDESE.API.Components.Network;
using System.Globalization;
using System.Threading;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesViewModel
    {
        private int periodicGoldHuman, periodicGoldAI;
        private volatile bool periodicGoldMissionLocked;
        public bool CanConfigurePeriodicGold => CanEditHostSettings && !periodicGoldMissionLocked;
        public string PeriodicGoldTitle => SerpLocalization.Get("ExtraFeatures.PeriodicGold.Title");
        public string PeriodicGoldHumanTitle => SerpLocalization.Get("ExtraFeatures.PeriodicGold.Human.Title");
        public string PeriodicGoldAITitle => SerpLocalization.Get("ExtraFeatures.PeriodicGold.AI.Title");
        public string PeriodicGoldHelp => SerpLocalization.Get("ExtraFeatures.PeriodicGold.Help");

        [SyncHostOnly]
        public int PeriodicGoldHuman
        {
            get => periodicGoldHuman;
            set { if (!periodicGoldMissionLocked) SetIntSetting(ref periodicGoldHuman, value, int.MinValue, int.MaxValue, nameof(PeriodicGoldHuman), nameof(PeriodicGoldHumanValueText)); }
        }
        [SyncHostOnly]
        public int PeriodicGoldAI
        {
            get => periodicGoldAI;
            set { if (!periodicGoldMissionLocked) SetIntSetting(ref periodicGoldAI, value, int.MinValue, int.MaxValue, nameof(PeriodicGoldAI), nameof(PeriodicGoldAIValueText)); }
        }
        public string PeriodicGoldHumanValueText
        {
            get => PeriodicGoldHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) PeriodicGoldHuman = parsed; OnPropertyChanged(nameof(PeriodicGoldHumanValueText)); }
        }
        public string PeriodicGoldAIValueText
        {
            get => PeriodicGoldAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) PeriodicGoldAI = parsed; OnPropertyChanged(nameof(PeriodicGoldAIValueText)); }
        }

        internal void SetPeriodicGoldMissionLocked(bool locked)
        {
            if (periodicGoldMissionLocked == locked) return;
            periodicGoldMissionLocked = locked;
            if (Thread.CurrentThread.ManagedThreadId == uiThread) OnPropertyChanged(nameof(CanConfigurePeriodicGold));
            else if (uiContext != null) uiContext.Post(_ => OnPropertyChanged(nameof(CanConfigurePeriodicGold)), null);
        }
    }
}
