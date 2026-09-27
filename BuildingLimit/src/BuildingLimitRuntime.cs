using BepInEx.Logging;
using CrusaderDE;
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
        private Hook updateRolloverHook;
        private Hook placeMapperItemHook;
        private UpdateRolloverDelegate updateRolloverTrampoline;
        private PlaceMapperItemDelegate placeMapperItemTrampoline;
        private FieldInfo hoverStructField;
        private FieldInfo selectedStructField;
        private int lastTooltipStruct = int.MinValue;
        private int lastTooltipLocalPlayerId = int.MinValue;
        private int lastTooltipCount = int.MinValue;
        private int lastTooltipLimit = int.MinValue;
        private bool buildingLimitTooltipIsClear = true;

        private delegate void UpdateRolloverDelegate(HUD_Main self);

        public BuildingLimitNotificationViewModel BuildingLimitNotification { get; } = new BuildingLimitNotificationViewModel();
        public BuildingLimitTooltipViewModel BuildingLimitTooltip { get; } = new BuildingLimitTooltipViewModel();

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

            LogDebug("Subscribing building limit runtime hooks");
            if (!TryInitializeFeature("active-building cache", activeBuildingCache.SubscribeHooks))
            {
                TryInitializeFeature("active-building cache rollback", activeBuildingCache.Dispose);
                LogDebug("Building limit enforcement remains inactive because its required cache is unavailable.");
                return;
            }
            towerSiegeCacheAvailable = TryInitializeFeature("tower-siege unit cache", towerSiegeUnitCache.SubscribeHooks);
            if (!towerSiegeCacheAvailable)
                LogDebug("Tower-siege limits remain inactive because their unit cache is unavailable.");
            try
            {
                InstallUpdateRolloverHook();
            }
            catch (Exception ex)
            {
                LogDebug("Could not install building limit tooltip hook:", ex);
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

            LogDebug("Building limit runtime hooks subscribed");
            hooksSubscribed = true;
        }

        public void InitializeAfterLibraryLoaded()
        {
            if (libraryInitialized)
                return;

            SubscribeSettingsChanges();
            SubscribeHooks();
            ApplyBuildingLimits();
            LogDebug("Applied building limit settings");
            libraryInitialized = true;
        }

        private void OnSessionStarted(Shared.GameplaySessionStartedContext context)
        {
            try
            {
                LogDebug("Gameplay session started: " + context.Kind);
                log.LogInfo("BuildingLimit persistent runtime active after startup cleanup: " + context.Kind);
                ClearTowerSiegeReservations();
                ResetBuildingLimitTooltipCache();
                ApplyBuildingLimits();
            }
            catch (Exception ex)
            {
                LogDebug("Gameplay session initialization failed:", ex);
            }
        }

        private void OnUnloadMap(APIShared.MissionLifecycleNotification args)
        {
            LogDebug("OnUnloadMap");
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
            MethodInfo updateRolloverTarget = typeof(HUD_Main).GetMethod(
                "UpdateRollover",
                BindingFlags.Public | BindingFlags.Instance);

            if (updateRolloverTarget == null)
                throw new MissingMethodException(typeof(HUD_Main).FullName, "UpdateRollover");

            hoverStructField = typeof(HUD_Main).GetField("HoverStruct", BindingFlags.NonPublic | BindingFlags.Instance);
            selectedStructField = typeof(HUD_Main).GetField("SelectedStruct", BindingFlags.NonPublic | BindingFlags.Instance);
            if (hoverStructField == null || selectedStructField == null)
                throw new MissingFieldException(typeof(HUD_Main).FullName, "HoverStruct/SelectedStruct");

            Hook candidate = new Hook(updateRolloverTarget, new UpdateRolloverDelegate(UpdateRolloverHookImpl));
            try
            {
                UpdateRolloverDelegate trampoline = candidate.GenerateTrampoline<UpdateRolloverDelegate>();
                updateRolloverTrampoline = trampoline;
                updateRolloverHook = candidate;
            }
            catch
            {
                candidate.Dispose(); // The hook was never published to the runtime.
                throw;
            }
            LogDebug("HUD_Main.UpdateRollover building limit hook installed");
        }

        private void UpdateRolloverHookImpl(HUD_Main self)
        {
            updateRolloverTrampoline(self);
            UpdateBuildingLimitTooltip(self);
        }

        private void LogDebug(params object[] parts)
        {
            Shared.DebugLogHelper.LogDebug(log, parts);
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
                LogDebug("Building limit feature failed; independent features continue:", featureName, ex);
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
            catch (Exception ex) { LogDebug("Building limit subscription failed; independent features continue:", featureName, ex); }
        }

        private void ClearBuildingLimitTooltip()
        {
            if (buildingLimitTooltipIsClear)
                return;

            BuildingLimitTooltip.Clear();
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
