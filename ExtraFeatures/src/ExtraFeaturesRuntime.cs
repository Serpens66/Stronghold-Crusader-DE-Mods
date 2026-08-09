// Feature: Lifecycle orchestration and shared event guards for Extra Features.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesRuntime : IDisposable
    {
        private static readonly int GoodsCount = (int)eGoods.Count;
        private static readonly TimeSpan MarketBuyGuardLifetime = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan RefundGuardLifetime = TimeSpan.FromSeconds(2);
        private const int MarketBuyAmount = 5;
        private const int MarketBuyShiftAmount = 25;
        private const float VanillaRefundMultiplier = 0.5f;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly HashSet<string> resourceAddReentryGuards = new HashSet<string>();
        private readonly Dictionary<string, ResourceEventCountGuard> marketBuyResourceGuards = new Dictionary<string, ResourceEventCountGuard>();
        private readonly Dictionary<string, ResourceEventCountGuard> refundResourceGuards = new Dictionary<string, ResourceEventCountGuard>();
        private readonly KnightDismountRuntime knightDismountRuntime;
        private readonly QuarryPileRelocationRuntime quarryPileRelocationRuntime;
        private readonly ChurchPriestCountRuntime churchPriestCountRuntime;

        private PendingStockpileRefund pendingStockpileRefund;
        private CtrlMarketTradeHook ctrlMarketTradeHook;
        private SingleBuildingPauseHook singleBuildingPauseHook;
        private AIEconomyProtectionHook aiEconomyProtectionHook;
        private FastRecruitMovementBridge fastRecruitMovementBridge;
        private PlagueDurationPatch plagueDurationPatch;
        private IntPtr libraryHandle;
        private int libraryLength;
        private bool nativeLibraryAvailable;
        private bool fixedLayoutHashValidated;
        private bool knightFixedLayoutErrorLogged;
        private bool quarryFixedLayoutErrorLogged;
        private bool fastRecruitInitializationAttempted;
        private bool hooksSubscribed;
        private bool settingsSubscribed;
        private bool ctrlMarketTradeHookUnavailable;
        private bool plagueDurationPatchUnavailable;

        public ExtraFeaturesRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            knightDismountRuntime = new KnightDismountRuntime(log, settings);
            quarryPileRelocationRuntime = new QuarryPileRelocationRuntime(log, settings);
            churchPriestCountRuntime = new ChurchPriestCountRuntime(log, settings);
            settings.SettingChanged += OnSettingChanged;
            settingsSubscribed = true;
        }

        public object KnightDismountButton => knightDismountRuntime.ButtonViewModel;
        public object QuarryPileRelocationButton => quarryPileRelocationRuntime.ButtonViewModel;

        public void InitializeNative(IntPtr newLibraryHandle, ReadOnlySpan<byte> memory, bool isFixedLayoutHashValidated)
        {
            if (nativeLibraryAvailable)
                return;
            if (newLibraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            libraryHandle = newLibraryHandle;
            libraryLength = memory.Length;
            fixedLayoutHashValidated = isFixedLayoutHashValidated;
            nativeLibraryAvailable = true;

            if (fixedLayoutHashValidated)
            {
                try
                {
                    knightDismountRuntime.InstallNativeFunctions(newLibraryHandle, memory);
                }
                catch (Exception ex)
                {
                    LogFeatureFailure("knight mount/dismount native functions", ex);
                }

                try
                {
                    quarryPileRelocationRuntime.InstallNativeFunctions(newLibraryHandle, memory);
                }
                catch (Exception ex)
                {
                    LogFeatureFailure("quarry-pile relocation native functions", ex);
                }
            }

            try
            {
                churchPriestCountRuntime.InitializeNative(newLibraryHandle, memory);
            }
            catch (Exception ex)
            {
                LogFeatureFailure("church priest counts", ex);
            }

            InitializePlagueDurationPatch(newLibraryHandle, memory);

            InstallCtrlMarketTradeHook();
            TryRunFeature("fast recruit rally movement", ApplyFastRecruitRallyMovementSetting);
        }

        public void ApplySettings()
        {
            if (!settings.EnableMod)
                return;

            TryRunFeature("shared event hooks", SubscribeHooks);
            TryRunFeature("bulldoze refunds", ApplyRefundSettings);
            TryRunFeature("market price multipliers", ApplyMarketPriceMultipliers);
            TryRunFeature("church priest counts", churchPriestCountRuntime.ApplySetting);
            ApplyPlagueDurationSetting();
            TryRunFeature("fast recruit rally movement", ApplyFastRecruitRallyMovementSetting);
        }

        public void InstallAIEconomyProtectionHook(IntPtr nativeLibraryHandle, ReadOnlySpan<byte> memory)
        {
            if (aiEconomyProtectionHook != null)
                return;

            try
            {
                aiEconomyProtectionHook = new AIEconomyProtectionHook(log, settings, nativeLibraryHandle, memory);
                singleBuildingPauseHook?.SetSleepStateSynchronizer(aiEconomyProtectionHook.SynchronizeSleepStatesNow);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Extra Features AI economy protection hook could not be installed: {ex}");
            }
        }

        public void Dispose()
        {
            UnsubscribeHooks();
            aiEconomyProtectionHook?.Dispose();
            aiEconomyProtectionHook = null;
            fastRecruitMovementBridge?.Dispose();
            fastRecruitMovementBridge = null;
            plagueDurationPatch?.Dispose();
            plagueDurationPatch = null;
            nativeLibraryAvailable = false;
            libraryHandle = IntPtr.Zero;
            libraryLength = 0;

            if (settingsSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsSubscribed = false;
            }
        }

        private void SubscribeHooks()
        {
            if (!settings.EnableMod || hooksSubscribed)
                return;

            TrySubscribeFeature("bulldoze tracking", () =>
                BuildingR3EventHooks.OnBuildingBulldoze.Observable.Subscribe(OnBuildingBulldoze));
            TrySubscribeFeature("refund tracking", () =>
                BuildingR3EventHooks.OnBuildingRefund.Observable.Subscribe(OnBuildingRefund));
            TrySubscribeFeature("goods gain tracking", () =>
                BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(OnGoodsyardAddGood));
            TrySubscribeFeature("market trade tracking", () =>
                PlayerR3EventHooks.OnPlayerMarketInteraction.Observable.Subscribe(OnPlayerMarketInteraction));
            TrySubscribeFeature("church priest spawn handling", () =>
                BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(churchPriestCountRuntime.ApplySpawnedBuilding));
            TrySubscribeFeature("map-load settings", () =>
                MapLoaderR3EventHooks.OnLoadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ApplyMapLoadedSettings()));
            TrySubscribeFeature("save-load settings", () =>
                MapLoaderR3EventHooks.OnLoadSave.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ApplyMapLoadedSettings()));
            TrySubscribeFeature("map-unload cleanup", () =>
                MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnUnloadMap));
            InstallCtrlMarketTradeHook();
            InstallSingleBuildingPauseHook();
            hooksSubscribed = true;
            ReconcileFixedLayoutFeatures();
            Shared.DebugLogHelper.LogDebug(log, "Extra Features feature hooks reconciled.");
        }

        private void UnsubscribeHooks()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            knightDismountRuntime.Dispose();
            quarryPileRelocationRuntime.Dispose();
            ctrlMarketTradeHook?.Dispose();
            ctrlMarketTradeHook = null;
            singleBuildingPauseHook?.Dispose();
            singleBuildingPauseHook = null;
            ClearResourceEventGuards();
            pendingStockpileRefund = null;
            hooksSubscribed = false;
        }

        private void InstallCtrlMarketTradeHook()
        {
            if (ctrlMarketTradeHook != null || ctrlMarketTradeHookUnavailable || !nativeLibraryAvailable)
                return;

            try
            {
                ctrlMarketTradeHook = new CtrlMarketTradeHook(log, settings, libraryHandle, GetNativeLibraryMemory());
                if (!fixedLayoutHashValidated)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Extra Features Ctrl single-unit market hooks are running on an unknown CrusaderDE.dll because all required native instruction patterns were validated.");
                }
            }
            catch (Exception ex)
            {
                // Native signatures stay unchanged for the process lifetime, so do not retry noisily.
                ctrlMarketTradeHookUnavailable = true;
                Shared.DebugLogHelper.LogError(log, $"Extra Features Ctrl single-unit market hooks could not be installed: {ex}");
            }
        }

        private void InstallSingleBuildingPauseHook()
        {
            try
            {
                singleBuildingPauseHook = new SingleBuildingPauseHook(log, settings);
                if (aiEconomyProtectionHook != null)
                    singleBuildingPauseHook.SetSleepStateSynchronizer(aiEconomyProtectionHook.SynchronizeSleepStatesNow);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Extra Features single-building pause hook could not be installed: {ex}");
            }
        }

        private void TryRunFeature(string featureName, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogFeatureFailure(featureName, ex);
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
            catch (Exception ex)
            {
                LogFeatureFailure(featureName, ex);
            }
        }

        private void LogFeatureFailure(string featureName, Exception ex)
        {
            Shared.DebugLogHelper.LogError(
                log,
                $"Extra Features feature '{featureName}' failed and remains inactive: {ex}");
        }

        private void ReconcileFixedLayoutFeatures()
        {
            if (!nativeLibraryAvailable || !settings.EnableMod)
                return;

            if (!fixedLayoutHashValidated)
            {
                if (settings.EnableKnightDismount && !knightFixedLayoutErrorLogged)
                {
                    knightFixedLayoutErrorLogged = true;
                    Shared.DebugLogHelper.LogError(log, "Extra Features knight mount/dismount remains inactive because its fixed native layout is not validated for this CrusaderDE.dll.");
                }
                if (settings.EnableQuarryPileRelocation && !quarryFixedLayoutErrorLogged)
                {
                    quarryFixedLayoutErrorLogged = true;
                    Shared.DebugLogHelper.LogError(log, "Extra Features quarry-pile relocation remains inactive because its fixed native layout is not validated for this CrusaderDE.dll.");
                }
                return;
            }

            TryRunFeature("knight mount/dismount", knightDismountRuntime.Initialize);
            TryRunFeature("quarry-pile relocation", quarryPileRelocationRuntime.Initialize);
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableMod))
            {
                if (settings.EnableMod)
                {
                    SubscribeHooks();
                    ApplySettings();
                }
                else
                {
                    RestoreDefaultSettings();
                    fastRecruitMovementBridge?.Dispose();
                    fastRecruitMovementBridge = null;
                    fastRecruitInitializationAttempted = false;
                    UnsubscribeHooks();
                }
                return;
            }

            if (!settings.EnableMod)
                return;

            if (propertyName == nameof(ExtraFeaturesViewModel.EnableKnightDismount))
            {
                ReconcileFixedLayoutFeatures();
                TryRunFeature("knight mount/dismount visibility", knightDismountRuntime.RefreshButtonVisibility);
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableQuarryPileRelocation))
            {
                ReconcileFixedLayoutFeatures();
                TryRunFeature("quarry-pile relocation", quarryPileRelocationRuntime.ApplySetting);
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableExtraChurchPriests))
            {
                TryRunFeature("church priest counts", churchPriestCountRuntime.ApplySetting);
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.EnableFastRecruitRallyMovement))
            {
                TryRunFeature("fast recruit rally movement", ApplyFastRecruitRallyMovementSetting);
                return;
            }
            if (propertyName == nameof(ExtraFeaturesViewModel.PlagueDurationMultiplier))
            {
                ApplyPlagueDurationSetting();
                return;
            }

            ApplySettings();
        }

        private void ApplyMapLoadedSettings()
        {
            TryRunFeature("market price multipliers", ApplyMarketPriceMultipliers);
            TryRunFeature("church priest counts", churchPriestCountRuntime.ApplySetting);
            ApplyPlagueDurationSetting();
        }

        private void InitializePlagueDurationPatch(IntPtr nativeLibraryHandle, ReadOnlySpan<byte> memory)
        {
            if (plagueDurationPatch != null || plagueDurationPatchUnavailable)
                return;

            try
            {
                plagueDurationPatch = new PlagueDurationPatch(nativeLibraryHandle, memory);
                ApplyPlagueDurationSetting();
                if (plagueDurationPatchUnavailable)
                    return;

                if (!fixedLayoutHashValidated)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Extra Features plague duration is running on an unknown CrusaderDE.dll because its native instruction signature and Vanilla lifetime were validated.");
                }
            }
            catch (Exception ex)
            {
                DisablePlagueDurationPatch(ex);
            }
        }

        private void ApplyPlagueDurationSetting()
        {
            if (plagueDurationPatch == null || plagueDurationPatchUnavailable)
                return;

            try
            {
                plagueDurationPatch.Apply(
                    settings.PlagueDurationMultiplier,
                    settings.EnableMod);
            }
            catch (Exception ex)
            {
                DisablePlagueDurationPatch(ex);
            }
        }

        private void DisablePlagueDurationPatch(Exception failure)
        {
            if (plagueDurationPatchUnavailable)
                return;

            plagueDurationPatchUnavailable = true;
            Exception restoreFailure = null;
            if (plagueDurationPatch != null)
            {
                try
                {
                    plagueDurationPatch.RestoreVanilla();
                }
                catch (Exception ex)
                {
                    restoreFailure = ex;
                }
            }

            plagueDurationPatch = null;
            string restoreDetails = restoreFailure == null
                ? string.Empty
                : $" Vanilla restoration also failed: {restoreFailure}";
            Shared.DebugLogHelper.LogError(
                log,
                $"Extra Features plague duration multiplier is disabled for this process; all other features remain available: {failure}.{restoreDetails}");
        }

        private void RestoreDefaultSettings()
        {
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            buildingApi.WoodRefundMultiplier.SetValue(VanillaRefundMultiplier);
            buildingApi.StoneRefundMultiplier.SetValue(VanillaRefundMultiplier);
            buildingApi.IronRefundMultiplier.SetValue(VanillaRefundMultiplier);
            buildingApi.PitchRefundMultiplier.SetValue(VanillaRefundMultiplier);
            buildingApi.GoldRefundMultiplier.SetValue(VanillaRefundMultiplier);
            RestoreTradeBasePrices();
            churchPriestCountRuntime.ApplySetting();
            ApplyPlagueDurationSetting();
        }

        private void ApplyFastRecruitRallyMovementSetting()
        {
            if (!nativeLibraryAvailable)
                return;

            if (!settings.EnableMod || !settings.EnableFastRecruitRallyMovement)
            {
                fastRecruitMovementBridge?.Dispose();
                fastRecruitMovementBridge = null;
                fastRecruitInitializationAttempted = false;
                return;
            }

            if (fastRecruitMovementBridge != null || fastRecruitInitializationAttempted)
                return;

            fastRecruitInitializationAttempted = true;
            FastRecruitMovementBridge bridge = new FastRecruitMovementBridge(log);
            if (bridge.IsActive)
                fastRecruitMovementBridge = bridge;
            else
                bridge.Dispose();
        }

        private unsafe ReadOnlySpan<byte> GetNativeLibraryMemory()
        {
            // The game DLL stays loaded for the process lifetime.
            return new ReadOnlySpan<byte>(libraryHandle.ToPointer(), libraryLength);
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            ClearResourceEventGuards();
            singleBuildingPauseHook?.ClearOverrides("map unload");
        }

        private static string BuildResourceEventKey(int playerId, eGoods good) => playerId + ":" + (int)good;

        private void ClearResourceEventGuards()
        {
            resourceAddReentryGuards.Clear();
            marketBuyResourceGuards.Clear();
            refundResourceGuards.Clear();
        }

        private sealed class PendingStockpileRefund
        {
            public int PlayerId;
            public int Owner;
            public int RefundBuildingId;
            public DateTime CreatedAt;
            public int PartsRemaining;
            public HashSet<int> ProcessedBuildingIds = new HashSet<int>();
        }

        private sealed class ResourceEventCountGuard
        {
            public int RemainingAmount { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}
