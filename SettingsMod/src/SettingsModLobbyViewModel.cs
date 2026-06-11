using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;
using System;

namespace SettingsMod
{
    public sealed class SettingsModLobbyViewModel : LobbyModSettingsBaseViewModel
    {
        public event Action<string> SettingChanged;

        private bool advancedOptionsEnabled = true;
        private bool advancedSkirmishOptionsEnabled = true;
        private bool betterHealers = true;
        private bool fasterPeasants = false;
        private bool globalImprovedSiegeBehaviour = true;
        private bool globalMoreAggressiveSiegeBehaviour = true;
        private bool improvedArabSwordsman = true;
        private bool improvedFletchers = true;
        private bool improvedLadderman = true;
        private bool improvedSpearman = true;
        private bool nerfEunuchs = true;
        private bool noKnockdownWalls = true;
        private bool rebalancedHorseArchers = true;
        private bool uncappedPeasants = false;
        private string enemyHealthModifier = "VeryStrong";

        [SyncHostOnly] public bool AdvancedOptionsEnabled { get => advancedOptionsEnabled; set => Set(ref advancedOptionsEnabled, value, nameof(AdvancedOptionsEnabled)); }
        [SyncHostOnly] public bool AdvancedSkirmishOptionsEnabled { get => advancedSkirmishOptionsEnabled; set => Set(ref advancedSkirmishOptionsEnabled, value, nameof(AdvancedSkirmishOptionsEnabled)); }
        [SyncHostOnly] public bool BetterHealers { get => betterHealers; set => Set(ref betterHealers, value, nameof(BetterHealers)); }
        [SyncHostOnly] public bool FasterPeasants { get => fasterPeasants; set => Set(ref fasterPeasants, value, nameof(FasterPeasants)); }
        [SyncHostOnly] public bool GlobalImprovedSiegeBehaviour { get => globalImprovedSiegeBehaviour; set => Set(ref globalImprovedSiegeBehaviour, value, nameof(GlobalImprovedSiegeBehaviour)); }
        [SyncHostOnly] public bool GlobalMoreAggressiveSiegeBehaviour { get => globalMoreAggressiveSiegeBehaviour; set => Set(ref globalMoreAggressiveSiegeBehaviour, value, nameof(GlobalMoreAggressiveSiegeBehaviour)); }
        [SyncHostOnly] public bool ImprovedArabSwordsman { get => improvedArabSwordsman; set => Set(ref improvedArabSwordsman, value, nameof(ImprovedArabSwordsman)); }
        [SyncHostOnly] public bool ImprovedFletchers { get => improvedFletchers; set => Set(ref improvedFletchers, value, nameof(ImprovedFletchers)); }
        [SyncHostOnly] public bool ImprovedLadderman { get => improvedLadderman; set => Set(ref improvedLadderman, value, nameof(ImprovedLadderman)); }
        [SyncHostOnly] public bool ImprovedSpearman { get => improvedSpearman; set => Set(ref improvedSpearman, value, nameof(ImprovedSpearman)); }
        [SyncHostOnly] public bool NerfEunuchs { get => nerfEunuchs; set => Set(ref nerfEunuchs, value, nameof(NerfEunuchs)); }
        [SyncHostOnly] public bool NoKnockdownWalls { get => noKnockdownWalls; set => Set(ref noKnockdownWalls, value, nameof(NoKnockdownWalls)); }
        [SyncHostOnly] public bool RebalancedHorseArchers { get => rebalancedHorseArchers; set => Set(ref rebalancedHorseArchers, value, nameof(RebalancedHorseArchers)); }
        [SyncHostOnly] public bool UncappedPeasants { get => uncappedPeasants; set => Set(ref uncappedPeasants, value, nameof(UncappedPeasants)); }
        [SyncHostOnly] public string EnemyHealthModifier { get => enemyHealthModifier; set => Set(ref enemyHealthModifier, value, nameof(EnemyHealthModifier)); }

        private void Set<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }
    }
}
