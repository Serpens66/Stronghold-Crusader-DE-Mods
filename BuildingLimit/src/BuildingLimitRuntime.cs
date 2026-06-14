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
    public sealed partial class BuildingLimitRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BuildingLimitLobbyViewModel settings;
        private readonly Dictionary<eMappers, BuildingLimitRule> activeBuildingLimitRules = new Dictionary<eMappers, BuildingLimitRule>();
        private readonly Dictionary<eStructs, BuildingLimitRule> activeBuildingLimitRulesByStructure = new Dictionary<eStructs, BuildingLimitRule>();
        // private readonly List<int> matchingBuildingIds = new List<int>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly ActiveBuildingCache activeBuildingCache;
        private bool settingsPropertyChangedSubscribed;
        private bool hooksSubscribed;
        private bool libraryInitialized;
        private const int BuildingLimitMessageDurationMilliseconds = 3000;
        private static readonly Dictionary<eMappers, BuildingLimitDefinition> BuildingLimitDefinitions = CreateBuildingLimitDefinitions();
        private string buildingLimitMessageTimerHandle;
        private Hook updateRolloverHook;
        private UpdateRolloverDelegate updateRolloverTrampoline;
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
            activeBuildingCache = new ActiveBuildingCache(log);
        }

        public void SubscribeHooks()
        {
            if (!settings.EnableMod)
                return;

            if (hooksSubscribed)
                return;

            LogDebug("Subscribing building limit runtime hooks");
            activeBuildingCache.SubscribeHooks();
            try
            {
                InstallUpdateRolloverHook();
            }
            catch (Exception ex)
            {
                LogDebug("Could not install building limit tooltip hook:", ex);
            }

            subscriptions.Add(BuildingR3EventHooks.OnPlacementValidation.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildingPlacementValidation));

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap));

            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnLoadSave));

            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap));

            LogDebug("Building limit runtime hooks subscribed");
            hooksSubscribed = true;
        }

        public void InitializeAfterLibraryLoaded()
        {
            if (libraryInitialized)
                return;

            SubscribeSettingsChanges();
            if (!settings.EnableMod)
            {
                LogDebug("Building limit disabled; runtime hooks not subscribed");
                libraryInitialized = true;
                return;
            }

            SubscribeHooks();
            ApplyBuildingLimits();
            LogDebug("Applied building limit settings");
            libraryInitialized = true;
        }

        public void Dispose()
        {
            UnsubscribeHooks();
            if (settingsPropertyChangedSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsPropertyChangedSubscribed = false;
            }
        }

        private void UnsubscribeHooks()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            hooksSubscribed = false;
            HideBuildingLimitMessage();
            ClearBuildingLimitTooltip();
            ResetBuildingLimitTooltipCache();
            updateRolloverHook?.Dispose();
            updateRolloverHook = null;
            updateRolloverTrampoline = null;
            activeBuildingCache.Dispose();
            activeBuildingLimitRules.Clear();
            activeBuildingLimitRulesByStructure.Clear();
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            try
            {
                LogDebug("OnStartMap");
                ResetBuildingLimitTooltipCache();
                ApplyBuildingLimits();
            }
            catch (Exception ex)
            {
                LogDebug("OnStartMap failed:", ex);
            }
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
        {
            LogDebug("OnLoadSave");
            ResetBuildingLimitTooltipCache();
            ApplyBuildingLimits();
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            LogDebug("OnUnloadMap");
            HideBuildingLimitMessage();
            ClearBuildingLimitTooltip();
            ResetBuildingLimitTooltipCache();
            // matchingBuildingIds.Clear();
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

            updateRolloverHook = new Hook(updateRolloverTarget, new UpdateRolloverDelegate(UpdateRolloverHookImpl));
            updateRolloverTrampoline = updateRolloverHook.GenerateTrampoline<UpdateRolloverDelegate>();
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
