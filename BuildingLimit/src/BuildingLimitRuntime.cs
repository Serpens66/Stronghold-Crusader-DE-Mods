using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace BuildingLimit
{
    public sealed partial class BuildingLimitRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BuildingLimitLobbyViewModel settings;
        private readonly Dictionary<eMappers, BuildingLimitRule> activeBuildingLimitRules = new Dictionary<eMappers, BuildingLimitRule>();
        // private readonly List<int> matchingBuildingIds = new List<int>();
        private readonly ActiveBuildingCache activeBuildingCache;
        private bool settingsPropertyChangedSubscribed;
        private bool hooksSubscribed;
        private bool libraryInitialized;
        private const int BuildingLimitMessageDurationMilliseconds = 3000;
        private static readonly Dictionary<eMappers, BuildingLimitDefinition> BuildingLimitDefinitions = CreateBuildingLimitDefinitions();
        private string buildingLimitMessageTimerHandle;

        public BuildingLimitNotificationViewModel BuildingLimitNotification { get; } = new BuildingLimitNotificationViewModel();

        public BuildingLimitRuntime(ManualLogSource log, BuildingLimitLobbyViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            activeBuildingCache = new ActiveBuildingCache(log);
        }

        public void SubscribeHooks()
        {
            if (hooksSubscribed)
                return;

            LogInfo("Subscribing building limit runtime hooks");
            activeBuildingCache.SubscribeHooks();

            BuildingR3EventHooks.OnPlacementValidation.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildingPlacementValidation);

            MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap);

            MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnLoadSave);

            MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap);

            LogInfo("Building limit runtime hooks subscribed");
            hooksSubscribed = true;
        }

        public void InitializeAfterLibraryLoaded()
        {
            if (libraryInitialized)
                return;

            ApplyBuildingLimits();
            SubscribeSettingsChanges();
            LogInfo("Applied building limit settings");
            libraryInitialized = true;
        }

        public void Dispose()
        {
            if (settingsPropertyChangedSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsPropertyChangedSubscribed = false;
            }

            HideBuildingLimitMessage();
            activeBuildingCache.Dispose();
            activeBuildingLimitRules.Clear();
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            try
            {
                LogInfo("OnStartMap");
                ApplyBuildingLimits();
            }
            catch (Exception ex)
            {
                LogInfo("OnStartMap failed:", ex);
            }
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
        {
            LogInfo("OnLoadSave");
            ApplyBuildingLimits();
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            LogInfo("OnUnloadMap");
            HideBuildingLimitMessage();
            // matchingBuildingIds.Clear();
        }

        private void LogInfo(params object[] parts)
        {
            log.LogInfo(string.Join(" ", parts));
        }
    }
}
