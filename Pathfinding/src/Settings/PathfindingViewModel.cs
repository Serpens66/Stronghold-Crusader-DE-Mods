// Feature: Lobby settings of the Pathfinding mod.
using APIShared.ModSettings;
using Pathfinding.UnitCommands;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using System;

namespace Pathfinding
{
    public sealed partial class PathfindingViewModel : PresetLobbyModSettingsViewModel, IUnitCommandSettings
    {
        public event Action<string> SettingChanged;

        /// <summary>Localized option texts for the settings page; keys follow the property names.</summary>
        public sealed class SettingsTexts
        {
            public string this[string key] => SerpLocalization.Get("Pathfinding." + key);
        }
        public SettingsTexts Text { get; } = new SettingsTexts();
        public RelayCommand ResetToDefaultCommand { get; }
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public string ResetToDefaultHelpText => SerpLocalization.Get(SerpLocalization.ResetToDefaultHelp);

        // Fire and plague share the detour rule.
        public bool DetourSettingsEnabled => AvoidFire || AvoidPlague;

        public PathfindingViewModel() => ResetToDefaultCommand = new RelayCommand(ResetToDefault);

        private void ResetToDefault()
        {
            if (CanEditHostSettings)
            {
                EnableMod = true; EnablePathfindingCore = true; AnchorSingleMoves = true; EnableFriendlyMoatTraversal = false;
                EnableImprovedManualUnitCommands = true; EnableImprovedMoatFilling = true; EnableLadderAttackPathfindingFix = true;
                EnableEnemyGatePathfindingFix = true; RequireReachableEnemyForAutomaticGateClosing = true;
                EnableImprovedAssassinPathfinding = true; EnableImprovedAssassinPathfindingAI = true;
                EnableAssassinGatehouseClimbFix = true; EnableAssassinCombatResumeFix = true;
                EnableMoveFormationEnhancements = true; EnableExtendedShiftCommandQueue = true;
                EnableTroopMovementFix = true; EnableRallyTerrainSlowdownFix = true; EnableFastRecruitRallyMovement = true;
                EnableMountedStockpileMovementFix = true; EnableAssemblyPointPlacementFix = true;
                AvoidFire = false; EscapeFire = true; FireWaitSeconds = 60; FireReturnSeconds = 120; FireDetourPercent = 50; FireDetourMinTiles = 10;
                AvoidPlague = false; EscapePlague = true; EscapePlagueRaised = false; PlagueWaitSeconds = 90; PlagueReturnSeconds = 180;
            }
            if (CanEditClientSettings)
            {
                EnableClientFeatures = true;
                EnableVanillaPerformanceOptimizations = true;
            }
        }

        protected override string ResolveSettingsUiText(string key, string fallback) => SerpLocalization.Get(key);

        protected override void ConfigurePerPlayerLobbySettings(PerPlayerLobbySettingsBuilder settings)
        {
            settings.ResetSlotsWith(nameof(EnableClientFeatures), () => true)
                .WhenLocalPlayerResolved(playerId => enableClientFeatures.TrySetLocalPlayerId(playerId));
        }

        private bool enableMod = true;
        private bool enablePathfindingCore = true, anchorSingleMoves = true, enableFriendlyMoatTraversal;
        private bool enableImprovedManualUnitCommands = true, enableImprovedMoatFilling = true, enableLadderAttackPathfindingFix = true;
        private bool enableEnemyGatePathfindingFix = true, requireReachableEnemyForAutomaticGateClosing = true;
        private bool enableImprovedAssassinPathfinding = true, enableImprovedAssassinPathfindingAI = true;
        private bool enableAssassinGatehouseClimbFix = true, enableAssassinCombatResumeFix = true;
        private bool enableMoveFormationEnhancements = true, enableExtendedShiftCommandQueue = true;
        private bool enableTroopMovementFix = true, enableRallyTerrainSlowdownFix = true, enableFastRecruitRallyMovement = true;
        private bool enableMountedStockpileMovementFix = true, enableAssemblyPointPlacementFix = true;
        private bool avoidFire, escapeFire = true, avoidPlague, escapePlague = true, escapePlagueRaised;
        private int fireWaitSeconds = 60, fireReturnSeconds = 120, fireDetourPercent = 50, fireDetourMinTiles = 10;
        private int plagueWaitSeconds = 90, plagueReturnSeconds = 180;
        private bool enableVanillaPerformanceOptimizations = true;
        private readonly LocalPerPlayerSetting<bool> enableClientFeatures = new LocalPerPlayerSetting<bool>(true);

        [SyncHostOnly] public bool EnableMod { get => enableMod; set => Set(ref enableMod, value, nameof(EnableMod)); }
        [SyncHostOnly] public bool EnablePathfindingCore { get => enablePathfindingCore; set => Set(ref enablePathfindingCore, value, nameof(EnablePathfindingCore)); }
        [SyncHostOnly] public bool AnchorSingleMoves { get => anchorSingleMoves; set => Set(ref anchorSingleMoves, value, nameof(AnchorSingleMoves)); }
        [SyncHostOnly] public bool EnableFriendlyMoatTraversal { get => enableFriendlyMoatTraversal; set => Set(ref enableFriendlyMoatTraversal, value, nameof(EnableFriendlyMoatTraversal)); }
        [SyncHostOnly] public bool EnableImprovedManualUnitCommands { get => enableImprovedManualUnitCommands; set => Set(ref enableImprovedManualUnitCommands, value, nameof(EnableImprovedManualUnitCommands)); }
        [SyncHostOnly] public bool EnableImprovedMoatFilling { get => enableImprovedMoatFilling; set => Set(ref enableImprovedMoatFilling, value, nameof(EnableImprovedMoatFilling)); }
        [SyncHostOnly] public bool EnableLadderAttackPathfindingFix { get => enableLadderAttackPathfindingFix; set => Set(ref enableLadderAttackPathfindingFix, value, nameof(EnableLadderAttackPathfindingFix)); }
        [SyncHostOnly] public bool EnableEnemyGatePathfindingFix { get => enableEnemyGatePathfindingFix; set => Set(ref enableEnemyGatePathfindingFix, value, nameof(EnableEnemyGatePathfindingFix)); }
        [SyncHostOnly] public bool RequireReachableEnemyForAutomaticGateClosing { get => requireReachableEnemyForAutomaticGateClosing; set => Set(ref requireReachableEnemyForAutomaticGateClosing, value, nameof(RequireReachableEnemyForAutomaticGateClosing)); }
        [SyncHostOnly] public bool EnableImprovedAssassinPathfinding { get => enableImprovedAssassinPathfinding; set => Set(ref enableImprovedAssassinPathfinding, value, nameof(EnableImprovedAssassinPathfinding)); }
        [SyncHostOnly] public bool EnableImprovedAssassinPathfindingAI { get => enableImprovedAssassinPathfindingAI; set => Set(ref enableImprovedAssassinPathfindingAI, value, nameof(EnableImprovedAssassinPathfindingAI)); }
        [SyncHostOnly] public bool EnableAssassinGatehouseClimbFix { get => enableAssassinGatehouseClimbFix; set => Set(ref enableAssassinGatehouseClimbFix, value, nameof(EnableAssassinGatehouseClimbFix)); }
        [SyncHostOnly] public bool EnableAssassinCombatResumeFix { get => enableAssassinCombatResumeFix; set => Set(ref enableAssassinCombatResumeFix, value, nameof(EnableAssassinCombatResumeFix)); }
        [SyncHostOnly] public bool EnableMoveFormationEnhancements { get => enableMoveFormationEnhancements; set => Set(ref enableMoveFormationEnhancements, value, nameof(EnableMoveFormationEnhancements)); }
        [SyncHostOnly] public bool EnableExtendedShiftCommandQueue { get => enableExtendedShiftCommandQueue; set => Set(ref enableExtendedShiftCommandQueue, value, nameof(EnableExtendedShiftCommandQueue)); }
        [SyncHostOnly] public bool EnableTroopMovementFix { get => enableTroopMovementFix; set => Set(ref enableTroopMovementFix, value, nameof(EnableTroopMovementFix)); }
        [SyncHostOnly] public bool EnableRallyTerrainSlowdownFix { get => enableRallyTerrainSlowdownFix; set => Set(ref enableRallyTerrainSlowdownFix, value, nameof(EnableRallyTerrainSlowdownFix)); }
        [SyncHostOnly] public bool EnableFastRecruitRallyMovement { get => enableFastRecruitRallyMovement; set => Set(ref enableFastRecruitRallyMovement, value, nameof(EnableFastRecruitRallyMovement)); }
        [SyncHostOnly] public bool EnableMountedStockpileMovementFix { get => enableMountedStockpileMovementFix; set => Set(ref enableMountedStockpileMovementFix, value, nameof(EnableMountedStockpileMovementFix)); }
        [SyncHostOnly] public bool EnableAssemblyPointPlacementFix { get => enableAssemblyPointPlacementFix; set => Set(ref enableAssemblyPointPlacementFix, value, nameof(EnableAssemblyPointPlacementFix)); }
        [SyncHostOnly] public bool AvoidFire { get => avoidFire; set => Set(ref avoidFire, value, nameof(AvoidFire)); }
        [SyncHostOnly] public bool EscapeFire { get => escapeFire; set => Set(ref escapeFire, value, nameof(EscapeFire)); }
        [SyncHostOnly] public int FireWaitSeconds { get => fireWaitSeconds; set => SetBounded(ref fireWaitSeconds, value, 1, 1800, nameof(FireWaitSeconds)); }
        [SyncHostOnly] public int FireReturnSeconds { get => fireReturnSeconds; set => SetBounded(ref fireReturnSeconds, value, 1, 1800, nameof(FireReturnSeconds)); }
        [SyncHostOnly] public int FireDetourPercent { get => fireDetourPercent; set => SetBounded(ref fireDetourPercent, value, 0, 1000, nameof(FireDetourPercent)); }
        [SyncHostOnly] public int FireDetourMinTiles { get => fireDetourMinTiles; set => SetBounded(ref fireDetourMinTiles, value, 0, 2000, nameof(FireDetourMinTiles)); }
        [SyncHostOnly] public bool AvoidPlague { get => avoidPlague; set => Set(ref avoidPlague, value, nameof(AvoidPlague)); }
        [SyncHostOnly] public bool EscapePlague { get => escapePlague; set => Set(ref escapePlague, value, nameof(EscapePlague)); }
        [SyncHostOnly] public bool EscapePlagueRaised { get => escapePlagueRaised; set => Set(ref escapePlagueRaised, value, nameof(EscapePlagueRaised)); }
        [SyncHostOnly] public int PlagueWaitSeconds { get => plagueWaitSeconds; set => SetBounded(ref plagueWaitSeconds, value, 1, 1800, nameof(PlagueWaitSeconds)); }
        [SyncHostOnly] public int PlagueReturnSeconds { get => plagueReturnSeconds; set => SetBounded(ref plagueReturnSeconds, value, 1, 1800, nameof(PlagueReturnSeconds)); }

        [SyncPerPlayer]
        public bool EnableClientFeatures
        {
            get => enableClientFeatures.Value;
            set
            {
                if (!enableClientFeatures.SetValue(value)) return;
                SettingChanged?.Invoke(nameof(EnableClientFeatures));
                OnPropertyChanged(nameof(EnableClientFeatures));
            }
        }
        public bool[] EnableClientFeaturesData => enableClientFeatures.Data;

        // Result-identical replacements of Vanilla simulation code; they change no result, therefore local.
        [PresetLocal]
        public bool EnableVanillaPerformanceOptimizations
        {
            get => enableVanillaPerformanceOptimizations;
            set => Set(ref enableVanillaPerformanceOptimizations, value, nameof(EnableVanillaPerformanceOptimizations));
        }
        internal bool VanillaPerformanceOptimizationsActive => EnableClientFeatures && EnableVanillaPerformanceOptimizations;
        bool IUnitCommandSettings.VanillaPerformanceOptimizationsActive => VanillaPerformanceOptimizationsActive;

        private void Set<T>(ref T field, T value, string name)
        {
            if (!CanMutateSetting(name) || Equals(field, value)) return;
            field = value;
            SettingChanged?.Invoke(name);
            OnPropertyChanged(name);
            if (name == nameof(AvoidFire) || name == nameof(AvoidPlague)) OnPropertyChanged(nameof(DetourSettingsEnabled));
        }

        private void SetBounded(ref int field, int value, int minimum, int maximum, string name) =>
            Set(ref field, Math.Max(minimum, Math.Min(maximum, value)), name);
    }
}
