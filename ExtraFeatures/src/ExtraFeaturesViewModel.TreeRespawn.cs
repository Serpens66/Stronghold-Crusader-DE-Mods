using SHCDESE.API.Components.Network;
using System.Globalization;
using System.Threading;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesViewModel
    {
        private int treeRespawnMinutes = TreeRespawnState.Off;
        private volatile bool treeRespawnMissionLocked;

        [SyncHostOnly]
        public int TreeRespawnMinutes
        {
            get => treeRespawnMinutes;
            set
            {
                if (!treeRespawnMissionLocked || IsApplyingSettingsSnapshot)
                    SetIntSetting(ref treeRespawnMinutes, value, TreeRespawnState.Off, TreeRespawnState.MaximumMinutes,
                        nameof(TreeRespawnMinutes), nameof(TreeRespawnValueText));
            }
        }

        public bool CanConfigureTreeRespawn => CanEditHostSettings && !treeRespawnMissionLocked;
        public string TreeRespawnTitle => SerpLocalization.Get("ExtraFeatures.TreeRespawn.Title");
        public string TreeRespawnHelp => SerpLocalization.Get("ExtraFeatures.TreeRespawn.Help");
        public string TreeRespawnValueText => TreeRespawnMinutes == TreeRespawnState.Off
            ? SerpLocalization.Get("ExtraFeatures.TreeRespawn.Off")
            : TreeRespawnMinutes.ToString(CultureInfo.InvariantCulture) + " min";

        internal void SetTreeRespawnMissionLocked(bool locked)
        {
            if (treeRespawnMissionLocked == locked) return;
            treeRespawnMissionLocked = locked;
            if (Thread.CurrentThread.ManagedThreadId == uiThread) OnPropertyChanged(nameof(CanConfigureTreeRespawn));
            else if (uiContext != null) uiContext.Post(_ => OnPropertyChanged(nameof(CanConfigureTreeRespawn)), null);
        }
    }
}
