using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;
using System.Collections.ObjectModel;

namespace AIAttackTest
{
    public sealed class AIAttackTestSettings : LobbyModSettingsBaseViewModel
    {
        private bool enableMod = true;
        private string attackScalingMode = AIAttackPolicy.RelativeMode;
        private int relativeAttackGrowthPercent = 50;
        private bool attackLordAfterBreach = true;
        private int initialDefenseOnlyMonths;

        public AIAttackTestSettings()
        {
            PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(IsHost))
                    OnPropertyChanged(nameof(CanEditRelativeGrowth));
            };
        }

        public ObservableCollection<string> AttackScalingModes { get; } =
            new ObservableCollection<string>
            {
                AIAttackPolicy.VanillaMode,
                AIAttackPolicy.RelativeMode,
            };

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set
            {
                if (SetSynced(ref enableMod, value))
                    OnPropertyChanged(nameof(CanEditRelativeGrowth));
            }
        }

        [SyncHostOnly]
        public string AttackScalingMode
        {
            get => attackScalingMode;
            set
            {
                string normalized = value == AIAttackPolicy.RelativeMode
                    ? AIAttackPolicy.RelativeMode
                    : AIAttackPolicy.VanillaMode;
                if (SetSynced(ref attackScalingMode, normalized))
                    OnPropertyChanged(nameof(CanEditRelativeGrowth));
            }
        }

        [SyncHostOnly]
        public int RelativeAttackGrowthPercent
        {
            get => relativeAttackGrowthPercent;
            set => SetSynced(
                ref relativeAttackGrowthPercent,
                AIAttackPolicy.ClampGrowthPercent(value));
        }

        [SyncHostOnly]
        public bool AttackLordAfterBreach
        {
            get => attackLordAfterBreach;
            set => SetSynced(ref attackLordAfterBreach, value);
        }

        [SyncHostOnly]
        public int InitialDefenseOnlyMonths
        {
            get => initialDefenseOnlyMonths;
            set => SetSynced(
                ref initialDefenseOnlyMonths,
                AIAttackPolicy.ClampDefenseMonths(value));
        }

        public bool CanEditRelativeGrowth =>
            IsHost && EnableMod && AttackScalingMode == AIAttackPolicy.RelativeMode;
    }
}
