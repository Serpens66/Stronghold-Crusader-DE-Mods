using BepInEx.Logging;
using CrusaderDE;
using APIShared.Presentation;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BuildingLimit
{
    public sealed partial class BuildingLimitRuntime
    {
        private readonly ManualLogSource log;
        private readonly BuildingLimitLobbyViewModel settings;
        private readonly Dictionary<eMappers, BuildingLimitRule> activeBuildingLimitRules = new Dictionary<eMappers, BuildingLimitRule>();
        private readonly Dictionary<eStructs, BuildingLimitRule> activeBuildingLimitRulesByStructure = new Dictionary<eStructs, BuildingLimitRule>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly ActiveBuildingCache activeBuildingCache;
        private readonly TowerSiegeUnitCache towerSiegeUnitCache;
        private bool towerSiegeCacheAvailable;
        private bool settingsPropertyChangedSubscribed;
        private bool hooksSubscribed;
        private bool libraryInitialized;
        private const int BuildingLimitMessageDurationMilliseconds = 3000;
        private static readonly Dictionary<eMappers, BuildingLimitDefinition> BuildingLimitDefinitions = CreateBuildingLimitDefinitions();
        private string buildingLimitMessageTimerHandle;
        private Hook placeMapperItemHook;
        private PlaceMapperItemDelegate placeMapperItemTrampoline;
        private int lastTooltipStruct = int.MinValue;
        private int lastTooltipLocalPlayerId = int.MinValue;
        private int lastTooltipCount = int.MinValue;
        private int lastTooltipLimit = int.MinValue;
        private bool buildingLimitTooltipIsClear = true;


        public BuildingLimitNotificationViewModel BuildingLimitNotification { get; } = new BuildingLimitNotificationViewModel();
        private IReadOnlyList<BuildingTooltipLine> tooltipLines = Array.Empty<BuildingTooltipLine>();
        private string lastTooltipText;

        public BuildingLimitRuntime(ManualLogSource log, BuildingLimitLobbyViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            Shared.GameplayModActivationGate.Initialize(log, BuildingLimitPlugin.PluginGuid, BuildingLimitPlugin.PluginName, () => settings.EnableMod);
            Shared.GameplayModActivationGate.StateChanged += OnModeAllowedChanged;
            activeBuildingCache = new ActiveBuildingCache(log);
            towerSiegeUnitCache = new TowerSiegeUnitCache(log);
        }

        private bool EffectsEnabled => Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod);

        public void SubscribeHooks()
        {
            if (hooksSubscribed)
                return;

            LogDebug("BuildingLimit/src/BuildingLimitRuntime.cs:65", "Subscribing building limit runtime hooks");
            if (!TryInitializeFeature("active-building cache", activeBuildingCache.SubscribeHooks))
            {
                TryInitializeFeature("active-building cache rollback", activeBuildingCache.Dispose);
                LogDebug("BuildingLimit/src/BuildingLimitRuntime.cs:69", "Building limit enforcement remains inactive because its required cache is unavailable.");
                return;
            }
            towerSiegeCacheAvailable = TryInitializeFeature("tower-siege unit cache", towerSiegeUnitCache.SubscribeHooks);
            if (!towerSiegeCacheAvailable)
                LogDebug("BuildingLimit/src/BuildingLimitRuntime.cs:74", "Tower-siege limits remain inactive because their unit cache is unavailable.");
            try
            {
                InstallUpdateRolloverHook();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BuildingLimit/src/BuildingLimitRuntime.cs:81", "Could not install building limit tooltip hook:", ex);
            }
            if (towerSiegeCacheAvailable)
            {
                if (!TryInitializeFeature("tower-siege placement hook", InstallPlaceMapperItemHook))
                    log.LogError("BuildingLimit tower-siege placement enforcement is unavailable; other limits remain active.");
            }

            TrySubscribeFeature("placement validation", () => BuildingR3EventHooks.OnPlacementValidation.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildingPlacementValidation));

            TrySubscribeFeature("gameplay session start", () =>
                Shared.GameplaySessionLifecycle.SubscribeStarted(log, OnSessionStarted, () => OnUnloadMap(null)));

            TrySubscribeFeature("map unload", () => Shared.MissionEvents.Ended
                .Subscribe(OnUnloadMap));

            if (towerSiegeCacheAvailable)
                TrySubscribeFeature("tower-siege reservation completion", () => UnitR3EventHooks.OnUnitCreate.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnTowerSiegeUnitCreated));

            LogDebug("BuildingLimit/src/BuildingLimitRuntime.cs:104", "Building limit runtime hooks subscribed");
            hooksSubscribed = true;
        }

        public void InitializeAfterLibraryLoaded()
        {
            if (libraryInitialized)
                return;

            SubscribeSettingsChanges();
            SubscribeHooks();
            ApplyBuildingLimits();
            LogDebug("BuildingLimit/src/BuildingLimitRuntime.cs:116", "Applied building limit settings");
            libraryInitialized = true;
        }

        private void OnSessionStarted(Shared.GameplaySessionStartedContext context)
        {
            lastPlacementBlocked = false;
            try
            {
                LogDebug("BuildingLimit/src/BuildingLimitRuntime.cs:124", "Gameplay session started: " + context.Kind);
                Shared.DebugLogHelper.LogDebug(log, "BuildingLimit persistent runtime active after startup cleanup: " + context.Kind);
                ClearTowerSiegeReservations();
                ResetBuildingLimitTooltipCache();
                ApplyBuildingLimits();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BuildingLimit/src/BuildingLimitRuntime.cs:132", "Gameplay session initialization failed:", ex);
            }
        }

        private void OnUnloadMap(APIShared.MissionLifecycleNotification args)
        {
            lastPlacementBlocked = false;
            LogDebug("BuildingLimit/src/BuildingLimitRuntime.cs:138", "OnUnloadMap");
            ClearTowerSiegeReservations();
            HideBuildingLimitMessage();
            ClearBuildingLimitTooltip();
            ResetBuildingLimitTooltipCache();
        }

        private void OnModeAllowedChanged(bool allowed)
        {
            if (!libraryInitialized)
                return;

            if (EffectsEnabled)
            {
                ApplyBuildingLimits();
            }
            else
            {
                ClearTowerSiegeReservations();
                HideBuildingLimitMessage();
                ClearBuildingLimitTooltip();
                ResetBuildingLimitTooltipCache();
            }
        }

        private void InstallUpdateRolloverHook()
        {
            if (!BuildingTooltipPresentation.TryRegister(BuildingLimitPlugin.PluginGuid, "Limit", UpdateBuildingLimitTooltip,
                out string reason)) throw new InvalidOperationException(reason);
        }

        private void LogDebug(string site, params object[] parts)
        {
            Shared.DebugLogHelper.LogDebugLimited(log, site, parts);
        }

        private bool TryInitializeFeature(string featureName, Action initialize)
        {
            try
            {
                initialize();
                return true;
            }
            catch (Exception ex)
            {
                Shared.RuntimeHealth.Failure(log, featureName, true, $"BuildingLimit feature '{featureName}' failed; independent features continue: {ex}");
                return false;
            }
        }

        private void TrySubscribeFeature(string featureName, Func<IDisposable> subscribe)
        {
            try
            {
                IDisposable subscription = subscribe();
                if (subscription != null)
                    subscriptions.Add(subscription);
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogErrorLimited(log, "BuildingLimit/src/BuildingLimitRuntime.cs:209", "Building limit subscription failed; independent features continue:", featureName, ex); }
        }

        private void ClearBuildingLimitTooltip()
        {
            if (buildingLimitTooltipIsClear)
                return;

            tooltipLines = Array.Empty<BuildingTooltipLine>();
            buildingLimitTooltipIsClear = true;
        }

        private void ResetBuildingLimitTooltipCache()
        {
            lastTooltipStruct = int.MinValue;
            lastTooltipLocalPlayerId = int.MinValue;
            lastTooltipCount = int.MinValue;
            lastTooltipLimit = int.MinValue;
        }
    }
}
