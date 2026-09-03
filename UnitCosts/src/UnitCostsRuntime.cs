using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace UnitCosts
{
    public sealed class UnitCostsRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly UnitCostsLobbyViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<eChimps, UnitExtraCostValues> humanExtraCosts = new Dictionary<eChimps, UnitExtraCostValues>();
        private MakeTroopGameActionHook makeTroopGameActionHook;
        private CreateTroopHoverHook createTroopHoverHook;
        private SiegeBuildHoverHook siegeBuildHoverHook;
        private RecruitmentAvailabilityUiHook recruitmentAvailabilityUiHook;
        private string materialMessageTimerHandle;
        private DateTime nextSiegeMissingResourcesMessageUtc = DateTime.MinValue;
        private DateTime nextSiegeMissingResourcesSpeechUtc = DateTime.MinValue;
        private eChimps currentTooltipUnitType = eChimps.CHIMP_TYPE_NULL;
        private int currentTooltipMultiplier = 1;
        private bool hasCurrentTooltipUnitType;
        private bool currentTooltipUsesRecruitAmount;
        private readonly List<RecruitmentCostSnapshotEntry> recruitmentCostSnapshot = new List<RecruitmentCostSnapshotEntry>();
        private int recruitmentCostSnapshotPlayerId;
        private eChimps recruitmentCostSnapshotUnitType = eChimps.CHIMP_TYPE_NULL;
        private int recruitmentCostSnapshotMultiplier;
        private bool recruitmentCostSnapshotValid;
        private bool settingsChangedSubscribed;
        private bool libraryInitialized;
        private const string GoodsTextSection = "TEXT_GOODS";
        private bool hooksSubscribed;
        private const int MaterialMessageDurationMilliseconds = 3000;
        private const int SiegeMissingResourcesMessageThrottleMilliseconds = 1000;
        private const int SiegeMissingResourcesSpeechThrottleMilliseconds = 10000;
        private const string MissingWeaponsSpeechFileName = "Other_Warning6.wav";
        private static readonly string[] MissingGoldSpeechFileNames = { "Units_Warning3.wav", "Units_Warning4.wav" };
        private static readonly Random SpeechRandom = new Random();
        private const BindingFlags MainViewModelFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo LastTroopBuildChimpField = typeof(MainViewModel).GetField("lastTroopBuildChimp", MainViewModelFlags);
        private static readonly PropertyInfo LastTroopBuildChimpProperty = typeof(MainViewModel).GetProperty("lastTroopBuildChimp", MainViewModelFlags);
        private static readonly FieldInfo LastTroopsAmountToMakeField = typeof(MainViewModel).GetField("lastTroopsAmountToMake", MainViewModelFlags);
        private static readonly PropertyInfo LastTroopsAmountToMakeProperty = typeof(MainViewModel).GetProperty("lastTroopsAmountToMake", MainViewModelFlags);
        private static readonly UnitGoldCostSnapshot<eChimps> VanillaGoldCosts = new UnitGoldCostSnapshot<eChimps>();

        public UnitCostsNotificationViewModel Notification { get; } = new UnitCostsNotificationViewModel();
        public UnitRecruitmentCostTooltipViewModel RecruitmentCostTooltip { get; } = new UnitRecruitmentCostTooltipViewModel();

        public UnitCostsRuntime(ManualLogSource log, UnitCostsLobbyViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            Shared.GameplayModActivationGate.Initialize(log, UnitCostsPlugin.PluginGuid, UnitCostsPlugin.PluginName, () => settings.EnableMod);
            Shared.GameplayModActivationGate.StateChanged += OnModeAllowedChanged;
        }

        private bool EffectsEnabled => Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod);

        public void InitializeAfterLibraryLoaded()
        {
            SubscribeSettingsChanges();
            TryInitializeFeature("Vanilla gold-cost capture", CaptureVanillaGoldCosts);
            libraryInitialized = true;
            if (!EffectsEnabled)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts disabled; runtime hooks not subscribed");
                return;
            }

            SubscribeHooks();
            TryInitializeFeature("native gold costs", ApplyUnitCosts);
            TryInitializeFeature("extra-cost normalization", settings.NormalizeExtraCostsAfterNativeGoldChange);
            TryInitializeFeature("human extra costs", ApplyHumanExtraUnitCosts);
        }

        private void SubscribeHooks()
        {
            if (hooksSubscribed)
                return;

            TrySubscribeFeature("map start", () => MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnStartMap));

            TrySubscribeFeature("map unload", () => MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnUnloadMap));

            TrySubscribeFeature("unit transition", () => UnitR3EventHooks.OnUnitTransition.Observable.Subscribe(OnUnitTransition));

            TrySubscribeFeature("placement validation", () => BuildingR3EventHooks.OnPlacementValidation.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnBuildingPlacementValidation));

            TrySubscribeFeature("building spawn", () => BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnBuildingSpawn));

            TryInitializeFeature("recruitment action enforcement", () => makeTroopGameActionHook = new MakeTroopGameActionHook(log, DecideMakeTroopGameAction));
            TryInitializeFeature("recruitment tooltip", () => createTroopHoverHook = new CreateTroopHoverHook(log, UpdateRecruitmentCostTooltip, ClearRecruitmentCostTooltip));
            TryInitializeFeature("siege tooltip", () => siegeBuildHoverHook = new SiegeBuildHoverHook(log, UpdateSiegeBuildCostTooltip, ClearRecruitmentCostTooltip));
            TryInitializeFeature("recruitment availability UI", () => recruitmentAvailabilityUiHook = new RecruitmentAvailabilityUiHook(log, RefreshRecruitmentUi));

            hooksSubscribed = true;
            Shared.DebugLogHelper.LogDebug(log, "UnitCosts runtime hooks subscribed");
        }

        public void Dispose()
        {
            Shared.GameplayModActivationGate.StateChanged -= OnModeAllowedChanged;
            UnsubscribeHooks();
            if (settingsChangedSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsChangedSubscribed = false;
            }
        }

        private void UnsubscribeHooks()
        {
            foreach (IDisposable subscription in subscriptions)
            {
                try { subscription.Dispose(); }
                catch (Exception ex) { Shared.DebugLogHelper.LogError(log, $"UnitCosts subscription cleanup failed: {ex}"); }
            }

            subscriptions.Clear();
            hooksSubscribed = false;
            TryDisposeFeature("recruitment action enforcement", makeTroopGameActionHook);
            makeTroopGameActionHook = null;
            TryDisposeFeature("recruitment tooltip", createTroopHoverHook);
            createTroopHoverHook = null;
            TryDisposeFeature("siege tooltip", siegeBuildHoverHook);
            siegeBuildHoverHook = null;
            TryDisposeFeature("recruitment availability UI", recruitmentAvailabilityUiHook);
            recruitmentAvailabilityUiHook = null;
            HideMaterialMessage();
        }

        private void SubscribeSettingsChanges()
        {
            if (settingsChangedSubscribed)
                return;

            settings.SettingChanged += OnSettingChanged;
            settingsChangedSubscribed = true;
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
                Shared.DebugLogHelper.LogError(log, $"UnitCosts feature '{featureName}' failed; independent features continue: {ex}");
            }
        }

        private void TryInitializeFeature(string featureName, Action initialize)
        {
            try { initialize(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"UnitCosts feature '{featureName}' failed; independent features continue: {ex}");
            }
        }

        private void TryDisposeFeature(string featureName, IDisposable feature)
        {
            if (feature == null)
                return;
            try { feature.Dispose(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"UnitCosts feature '{featureName}' cleanup failed; independent features continue: {ex}");
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            Shared.DebugLogHelper.LogDebug(log, "UnitCosts settings changed:", propertyName);

            if (propertyName == nameof(UnitCostsLobbyViewModel.EnableMod))
            {
                if (EffectsEnabled)
                {
                    SubscribeHooks();
                    TryInitializeFeature("Vanilla gold-cost capture", CaptureVanillaGoldCosts);
                    TryInitializeFeature("native gold costs", ApplyUnitCosts);
                    TryInitializeFeature("extra-cost normalization", settings.NormalizeExtraCostsAfterNativeGoldChange);
                    TryInitializeFeature("human extra costs", ApplyHumanExtraUnitCosts);
                }
                else
                {
                    try { RestoreVanillaUnitCosts(); }
                    finally { UnsubscribeHooks(); }
                }

                return;
            }

            if (!EffectsEnabled)
                return;

            if (propertyName == nameof(UnitCostsLobbyViewModel.UnitCosts))
            {
                TryInitializeFeature("native gold costs", ApplyUnitCosts);
                TryInitializeFeature("extra-cost normalization", settings.NormalizeExtraCostsAfterNativeGoldChange);
                TryInitializeFeature("human extra costs", ApplyHumanExtraUnitCosts);
            }

            if (propertyName == nameof(UnitCostsLobbyViewModel.HumanExtraUnitCosts))
                TryInitializeFeature("human extra costs", ApplyHumanExtraUnitCosts);
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            try
            {
                TryInitializeFeature("native gold costs", ApplyUnitCosts);
                TryInitializeFeature("extra-cost normalization", settings.NormalizeExtraCostsAfterNativeGoldChange);
                TryInitializeFeature("human extra costs", ApplyHumanExtraUnitCosts);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts OnStartMap failed:", ex);
            }
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            HideMaterialMessage();
        }

        private void OnModeAllowedChanged(bool allowed)
        {
            if (!libraryInitialized)
                return;

            if (EffectsEnabled)
            {
                SubscribeHooks();
                TryInitializeFeature("Vanilla gold-cost capture", CaptureVanillaGoldCosts);
                TryInitializeFeature("native gold costs", ApplyUnitCosts);
                TryInitializeFeature("extra-cost normalization", settings.NormalizeExtraCostsAfterNativeGoldChange);
                TryInitializeFeature("human extra costs", ApplyHumanExtraUnitCosts);
            }
            else
            {
                try { RestoreVanillaUnitCosts(); }
                finally { UnsubscribeHooks(); }
            }
        }

        private void ApplyUnitCosts()
        {
            Dictionary<eChimps, UnitCostValues> parsedCosts = settings.ParseUnitCosts();
            int changedValues = 0;
            foreach (KeyValuePair<eChimps, UnitCostValues> entry in parsedCosts)
            {
                UnitCostValues values = entry.Value;
                int goldCost = values.Gold;
                if (goldCost == -1 && !VanillaGoldCosts.TryGetValue(entry.Key, out goldCost))
                    continue;

                try
                {
                    SetUnitGoldCost(entry.Key, goldCost);
                    if (values.Gold != -1)
                        changedValues++;
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(log, $"UnitCosts could not apply {entry.Key}; remaining units continue: {ex}");
                }
            }

            Shared.DebugLogHelper.LogDebug(log, "Applied unit cost values:", changedValues);
        }

        private void RestoreVanillaUnitCosts()
        {
            int restoredValues = 0;
            foreach (KeyValuePair<eChimps, int> entry in VanillaGoldCosts.Entries)
            {
                try
                {
                    SetUnitGoldCost(entry.Key, entry.Value);
                    restoredValues++;
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(log, $"UnitCosts could not restore {entry.Key}; remaining units continue: {ex}");
                }
            }

            humanExtraCosts.Clear();
            ClearRecruitmentCostTooltip();
            Shared.DebugLogHelper.LogDebug(log, "Restored vanilla unit cost values:", restoredValues);
        }

        private static void SetUnitGoldCost(eChimps unitType, int goldCost)
        {
            GameUnitManagerAPI.Instance.SetUnitGoldCost(unitType, goldCost);
            if (TryGetSiegeTentStructure(unitType, out eStructs siegeTentStructure))
                GameBuildingManagerAPI.Instance.SetGoldCost(siegeTentStructure, goldCost);
        }

        private void ApplyHumanExtraUnitCosts()
        {
            humanExtraCosts.Clear();
            Dictionary<eChimps, UnitExtraCostValues> parsedCosts = settings.ParseHumanExtraUnitCosts();
            int configuredUnits = 0;
            foreach (KeyValuePair<eChimps, UnitExtraCostValues> entry in parsedCosts)
            {
                humanExtraCosts[entry.Key] = entry.Value;
                if (entry.Value.HasAnyCost())
                    configuredUnits++;
            }

            Shared.DebugLogHelper.LogDebug(log, "Applied human extra unit cost rows:", configuredUnits);
            RefreshCurrentRecruitmentCostTooltip();
        }

        private MakeTroopGameActionDecision DecideMakeTroopGameAction(
            int amount,
            eChimps unitType,
            int rawUnitType,
            bool interpretCtrlSentinel)
        {
            try
            {
                return DecideLocalHumanRecruitment(amount, unitType, rawUnitType, interpretCtrlSentinel);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts local recruitment cost check failed:", ex.Message);
                return MakeTroopGameActionDecision.AllowOriginal();
            }
        }

        private MakeTroopGameActionDecision DecideLocalHumanRecruitment(
            int amount,
            eChimps unitType,
            int rawUnitType,
            bool interpretCtrlSentinel)
        {
            if (!IsUnitCostModeAllowed())
                return MakeTroopGameActionDecision.AllowOriginal();

            if (amount <= 0)
                return MakeTroopGameActionDecision.AllowOriginal();

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0)
                return MakeTroopGameActionDecision.AllowOriginal();

            // Without positive extra costs this mod must not reinterpret
            // Vanilla's Ctrl sentinel or duplicate native affordability checks.
            if (!TryGetHumanExtraCosts(unitType, out UnitExtraCostValues costs))
                return MakeTroopGameActionDecision.AllowOriginal();

            int extraAffordableAmount = -1;
            eGoods extraLimitingGood = eGoods.STORED_NULL;
            int extraLimitingRequiredPerUnit = 0;
            int extraLimitingAvailableAmount = 0;
            bool hasPositiveExtraCost = TryGetMaxAffordableExtraCostAmount(
                playerId,
                costs,
                out extraAffordableAmount,
                out extraLimitingGood,
                out extraLimitingRequiredPerUnit,
                out extraLimitingAvailableAmount);

            if (!hasPositiveExtraCost)
                return MakeTroopGameActionDecision.AllowOriginal();

            int vanillaRequestedAmount = amount;
            if (interpretCtrlSentinel &&
                !TryGetCurrentVanillaRecruitAmount(unitType, out vanillaRequestedAmount))
            {
                // Keep the extra-cost ceiling even when the transient UI preview
                // is unavailable. Native recruitment still applies all Vanilla costs.
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"UnitCosts could not resolve Vanilla's current Ctrl amount; forwarding the " +
                    $"extra-cost ceiling instead: unit={unitType}, player={playerId}, " +
                    $"extraAffordable={extraAffordableAmount}, rawUnitType={rawUnitType}.");
                if (extraAffordableAmount > 0)
                    return MakeTroopGameActionDecision.ForwardAmount(extraAffordableAmount);

                ShowMissingResourcesMessage(extraLimitingGood);
                return MakeTroopGameActionDecision.BlockAction();
            }

            Shared.RecruitmentConstraintDecision constraint =
                Shared.RecruitmentRequestPolicy.ApplyMaximum(
                    amount,
                    vanillaRequestedAmount,
                    extraAffordableAmount,
                    interpretCtrlSentinel);

            Shared.DebugLogHelper.LogDebug(
                log,
                "UnitCosts extra-cost recruitment decision:",
                "unit", unitType,
                "player", playerId,
                "incomingAmount", amount,
                "interpretCtrlSentinel", interpretCtrlSentinel,
                "vanillaRequestedAmount", vanillaRequestedAmount,
                "effectiveRequestedAmount", constraint.EffectiveRequestedAmount,
                "extraAffordable", extraAffordableAmount,
                "extraLimitingGood", extraLimitingGood,
                "extraRequiredPerUnit", extraLimitingRequiredPerUnit,
                "extraAvailable", extraLimitingAvailableAmount,
                "constraintAction", constraint.Action,
                "forwardedAmount", constraint.AmountToForward,
                "rawUnitType", rawUnitType);

            switch (constraint.Action)
            {
                case Shared.RecruitmentConstraintAction.PreserveOriginal:
                    return MakeTroopGameActionDecision.AllowOriginal();
                case Shared.RecruitmentConstraintAction.ForwardAmount:
                    return MakeTroopGameActionDecision.ForwardAmount(constraint.AmountToForward);
                default:
                    ShowMissingResourcesMessage(extraLimitingGood);
                    return MakeTroopGameActionDecision.BlockAction();
            }
        }

        private void UpdateRecruitmentCostTooltip(MainViewModel mainViewModel)
        {
            if (mainViewModel == null)
            {
                ClearRecruitmentCostEntries();
                return;
            }

            eChimps unitType = GetLastTroopBuildChimp(mainViewModel);
            int multiplier = GetLastTroopsAmountToMake(mainViewModel);
            ShowRecruitmentCostTooltip(unitType, multiplier, true);
        }

        private void UpdateSiegeBuildCostTooltip(object parameter)
        {
            if (!TryGetSiegeBuildHoverUnit(parameter, out eChimps unitType))
            {
                ClearRecruitmentCostEntries();
                return;
            }

            ShowRecruitmentCostTooltip(unitType, 1, false);
        }

        private void ShowRecruitmentCostTooltip(eChimps unitType, int multiplier, bool useRecruitAmount)
        {
            currentTooltipUnitType = unitType;
            currentTooltipMultiplier = Math.Max(1, multiplier);
            currentTooltipUsesRecruitAmount = useRecruitAmount;
            hasCurrentTooltipUnitType = true;
            RefreshCurrentRecruitmentCostTooltip();
        }

        private void RefreshCurrentRecruitmentCostTooltip()
        {
            if (!hasCurrentTooltipUnitType)
                return;

            if (!IsUnitCostModeAllowed())
            {
                ClearRecruitmentCostEntries();
                return;
            }

            if (currentTooltipUsesRecruitAmount && MainViewModel.Instance != null)
                currentTooltipMultiplier = Math.Max(1, GetLastTroopsAmountToMake(MainViewModel.Instance));

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0 || !TryGetHumanExtraCosts(currentTooltipUnitType, out UnitExtraCostValues costs))
            {
                ClearRecruitmentCostEntries();
                return;
            }

            if (RecruitmentCostStateMatches(playerId, costs, currentTooltipMultiplier))
                return;

            RecruitmentCostTooltip.SetCosts(CreateRecruitmentCostEntries(playerId, costs, currentTooltipMultiplier));
        }

        private void RefreshRecruitmentUi()
        {
            RefreshRecruitmentButtonAvailability();
            RefreshCurrentRecruitmentCostTooltip();
        }

        private List<UnitRecruitmentCostEntry> CreateRecruitmentCostEntries(int playerId, UnitExtraCostValues costs, int multiplier)
        {
            List<UnitRecruitmentCostEntry> entries = new List<UnitRecruitmentCostEntry>();
            recruitmentCostSnapshot.Clear();
            foreach (KeyValuePair<eGoods, int> entry in costs.CostEntries)
            {
                int amount = entry.Value * multiplier;
                if (entry.Key == eGoods.STORED_GOLD)
                {
                    if (amount == 0)
                        continue;
                }
                else if (amount <= 0)
                {
                    continue;
                }

                int availableAmount = GetAvailableGoodAmount(playerId, entry.Key);
                recruitmentCostSnapshot.Add(new RecruitmentCostSnapshotEntry(entry.Key, entry.Value, availableAmount));
                entries.Add(new UnitRecruitmentCostEntry
                {
                    Amount = "   " + amount + " ",
                    AmountAvailable = $"({availableAmount})",
                    Image = GetGoodImage(entry.Key)
                });
            }

            recruitmentCostSnapshotPlayerId = playerId;
            recruitmentCostSnapshotUnitType = currentTooltipUnitType;
            recruitmentCostSnapshotMultiplier = multiplier;
            recruitmentCostSnapshotValid = true;
            return entries;
        }

        private bool RecruitmentCostStateMatches(int playerId, UnitExtraCostValues costs, int multiplier)
        {
            if (!recruitmentCostSnapshotValid ||
                recruitmentCostSnapshotPlayerId != playerId ||
                recruitmentCostSnapshotUnitType != currentTooltipUnitType ||
                recruitmentCostSnapshotMultiplier != multiplier)
            {
                return false;
            }

            int snapshotIndex = 0;
            foreach (KeyValuePair<eGoods, int> entry in costs.CostEntries)
            {
                int amount = entry.Value * multiplier;
                if ((entry.Key == eGoods.STORED_GOLD && amount == 0) ||
                    (entry.Key != eGoods.STORED_GOLD && amount <= 0))
                {
                    continue;
                }

                if (snapshotIndex >= recruitmentCostSnapshot.Count)
                    return false;

                RecruitmentCostSnapshotEntry snapshot = recruitmentCostSnapshot[snapshotIndex++];
                if (snapshot.Good != entry.Key ||
                    snapshot.AmountPerUnit != entry.Value ||
                    snapshot.AvailableAmount != GetAvailableGoodAmount(playerId, entry.Key))
                {
                    return false;
                }
            }

            return snapshotIndex == recruitmentCostSnapshot.Count;
        }

        private static int GetAvailableGoodAmount(int playerId, eGoods good)
        {
            return Math.Max(0, GamePlayerManagerAPI.Instance.GetGoodAmount(playerId, good));
        }

        internal void RefreshRecruitmentButtonAvailability()
        {
            if (!IsUnitCostModeAllowed() || !EffectsEnabled || humanExtraCosts.Count == 0)
                return;

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0)
                return;

            MainViewModel mainViewModel = MainViewModel.Instance;
            if (mainViewModel?.HUDBuildingPanel == null)
                return;

            eChimps hoveredUnitType = GetLastTroopBuildChimp(mainViewModel);
            int amount = hoveredUnitType == eChimps.CHIMP_NUM_TYPES ? 1 : GetLastTroopsAmountToMake(mainViewModel);
            HUD_Buildings panel = mainViewModel.HUDBuildingPanel;

            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_ARCHER, panel.RefRecruitArcherButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_SPEARMAN, panel.RefRecruitSpearmanButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_MACEMAN, panel.RefRecruitMacemanButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_XBOWMAN, panel.RefRecruitXBowmanButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_PIKEMAN, panel.RefRecruitPikemanButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_SWORDSMAN, panel.RefRecruitSwordsmanButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_KNIGHT, panel.RefRecruitKnightButton);

            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_ENGINEER, panel.RefRecruitEngineerButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_LADDERMAN, panel.RefRecruitLaddermanButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_TUNNELER, panel.RefRecruitTunellerButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_MONK, panel.RefRecruitMonkButton);

            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_ARAB_BOW, panel.RefRecruitArabBowButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_ARAB_SLAVE, panel.RefRecruitArabSlaveButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_ARAB_SLINGER, panel.RefRecruitArabSlingerButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_ARAB_ASSASIN, panel.RefRecruitArabAssassinButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_ARAB_HORSEMAN, panel.RefRecruitArabHorseArcherButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_ARAB_SWORDSMAN, panel.RefRecruitArabSwordsmanButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_ARAB_GRENADIER, panel.RefRecruitArabGrenadierButton);

            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER, panel.RefRecruitBedouinCamelLancerButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_BEDOUIN_HEALER, panel.RefRecruitBedouinHealerButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, panel.RefRecruitBedouinEunuchButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER, panel.RefRecruitBedouinAmbusherButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER, panel.RefRecruitBedouinSkirmisherButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL, panel.RefRecruitBedouinHeavyCamelButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_BEDOUIN_SAPPER, panel.RefRecruitBedouinSapperButton);
            DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER, panel.RefRecruitBedouinDemolisherButton);
        }

        private void DisableRecruitmentButtonIfMissingExtraCosts(
            int playerId,
            int amount,
            eChimps unitType,
            Noesis.UIElement button)
        {
            if (button == null || !button.IsEnabled)
                return;

            if (!TryGetHumanExtraCosts(unitType, out UnitExtraCostValues costs))
                return;

            if (!HasEnoughExtraCosts(playerId, costs, amount, out eGoods _, out int _, out int _))
                button.IsEnabled = false;
        }

        private static bool TryGetSiegeBuildHoverUnit(object parameter, out eChimps unitType)
        {
            switch (parameter as string)
            {
                case "UnitBuildCat":
                    unitType = eChimps.CHIMP_TYPE_CATAPULT;
                    return true;
                case "UnitBuildTreb":
                    unitType = eChimps.CHIMP_TYPE_TREBUCHET;
                    return true;
                case "UnitBuildRam":
                    unitType = eChimps.CHIMP_TYPE_BATTERING_RAM;
                    return true;
                case "UnitBuildTower":
                    unitType = eChimps.CHIMP_TYPE_SIEGE_TOWER;
                    return true;
                case "UnitbuildMantlet":
                    unitType = eChimps.CHIMP_TYPE_PORTABLE_SHIELD;
                    return true;
                case "UnitbuildArabBallista":
                    unitType = eChimps.CHIMP_TYPE_ARAB_BALLISTA;
                    return true;
                default:
                    unitType = eChimps.CHIMP_TYPE_NULL;
                    return false;
            }
        }

        private static bool TryGetSiegeUnitFromMapper(eMappers mapper, out eChimps unitType)
        {
            switch (mapper)
            {
                case eMappers.MAPPER_CATAPULT:
                    unitType = eChimps.CHIMP_TYPE_CATAPULT;
                    return true;
                case eMappers.MAPPER_TREBUCHET:
                    unitType = eChimps.CHIMP_TYPE_TREBUCHET;
                    return true;
                case eMappers.MAPPER_BATTERING_RAM:
                    unitType = eChimps.CHIMP_TYPE_BATTERING_RAM;
                    return true;
                case eMappers.MAPPER_SIEGE_TOWER:
                    unitType = eChimps.CHIMP_TYPE_SIEGE_TOWER;
                    return true;
                case eMappers.MAPPER_PORTABLE_SHIELD:
                    unitType = eChimps.CHIMP_TYPE_PORTABLE_SHIELD;
                    return true;
                case eMappers.MAPPER_PEOPLE_ARAB_BALLISTA:
                case eMappers.MAPPER_ARAB_BALLISTA:
                    unitType = eChimps.CHIMP_TYPE_ARAB_BALLISTA;
                    return true;
                default:
                    unitType = eChimps.CHIMP_TYPE_NULL;
                    return false;
            }
        }

        private void ClearRecruitmentCostTooltip()
        {
            hasCurrentTooltipUnitType = false;
            currentTooltipUnitType = eChimps.CHIMP_TYPE_NULL;
            currentTooltipMultiplier = 1;
            currentTooltipUsesRecruitAmount = false;
            ClearRecruitmentCostEntries();
        }

        private void ClearRecruitmentCostEntries()
        {
            recruitmentCostSnapshot.Clear();
            recruitmentCostSnapshotValid = false;
            RecruitmentCostTooltip.Clear();
        }

        private readonly struct RecruitmentCostSnapshotEntry
        {
            public RecruitmentCostSnapshotEntry(eGoods good, int amountPerUnit, int availableAmount)
            {
                Good = good;
                AmountPerUnit = amountPerUnit;
                AvailableAmount = availableAmount;
            }

            public eGoods Good { get; }
            public int AmountPerUnit { get; }
            public int AvailableAmount { get; }
        }

        private void OnUnitTransition(UnitTransitionEventArgs args)
        {
            try
            {
                if (!IsUnitCostModeAllowed())
                    return;

                if (args.Phase != EventHookPhase.Pre)
                    return;

                if (args.Source != UnitTransitionSource.EuropeanBarracks &&
                    args.Source != UnitTransitionSource.MercenaryOutpost)
                    return;

                int playerId = args.PlayerOwnerId;
                if (!IsHumanPlayer(playerId))
                    return;

                if (!TryGetHumanExtraCosts(args.NextUnitType, out UnitExtraCostValues costs))
                    return;

                if (!HasEnoughExtraCosts(playerId, costs, 1, out eGoods missingGood, out int requiredAmount, out int availableAmount))
                {
                    LogDebug(
                        "UnitCosts transition extra cost missing:",
                        "unit", args.NextUnitType,
                        "player", playerId,
                        "missing", missingGood,
                        "required", requiredAmount,
                        "available", availableAmount);
                    return;
                }

                ApplyExtraCosts(playerId, costs, 1);
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts applied human extra costs:", args.NextUnitType, "player", playerId);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts OnUnitTransition failed:", ex.Message);
            }
        }

        private void OnBuildingPlacementValidation(BuildingPlacementValidationEventArgs args)
        {
            try
            {
                if (!IsUnitCostModeAllowed())
                    return;

                if (!IsHumanPlayer(args.PlayerId) || !IsLocalPlayer(args.PlayerId))
                    return;

                if (!TryGetSiegeUnitFromMapper(args.Mappers, out eChimps unitType))
                    return;

                if (!TryGetHumanExtraCosts(unitType, out UnitExtraCostValues costs))
                    return;

                if (HasEnoughExtraCosts(args.PlayerId, costs, 1, out eGoods missingGood, out int requiredAmount, out int availableAmount))
                    return;

                args.CustomValidationRules = true;
                args.ForceBlockPlacementState = true;
                LogDebug(
                    "UnitCosts blocked siege placement:",
                    "unit", unitType,
                    "player", args.PlayerId,
                    "missing", missingGood,
                    "required", requiredAmount,
                    "available", availableAmount,
                    "mapper", args.Mappers);
                ShowMissingResourcesMessageThrottledForSiege(missingGood);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts siege placement validation failed:", ex.Message);
            }
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            try
            {
                if (!IsUnitCostModeAllowed())
                    return;

                if (!IsHumanPlayer(args.PlayerId))
                    return;

                if (!TryGetSiegeUnitFromTentStructure(args.Building, out eChimps unitType))
                    return;

                if (!TryGetHumanExtraCosts(unitType, out UnitExtraCostValues costs))
                    return;

                if (!HasEnoughExtraCosts(args.PlayerId, costs, 1, out eGoods missingGood, out int requiredAmount, out int availableAmount))
                {
                    LogDebug(
                        "UnitCosts siege extra cost skipped after spawn because resources are missing:",
                        "unit", unitType,
                        "player", args.PlayerId,
                        "missing", missingGood,
                        "required", requiredAmount,
                        "available", availableAmount,
                        "building", args.Building);
                    if (IsLocalPlayer(args.PlayerId))
                        ShowMissingResourcesMessageThrottledForSiege(missingGood);
                    return;
                }

                ApplyExtraCosts(args.PlayerId, costs, 1);
                LogDebug(
                    "UnitCosts applied siege extra costs:",
                    "unit", unitType,
                    "player", args.PlayerId,
                    "building", args.Building);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts OnBuildingSpawn failed:", ex.Message);
            }
        }

        private bool TryGetHumanExtraCosts(eChimps unitType, out UnitExtraCostValues costs)
        {
            if (humanExtraCosts.TryGetValue(unitType, out costs) && costs.HasAnyCost())
                return true;

            costs = null;
            return false;
        }

        private static bool HasEnoughExtraCosts(
            int playerId,
            UnitExtraCostValues costs,
            int multiplier,
            out eGoods missingGood,
            out int requiredAmount,
            out int availableAmount)
        {
            foreach (KeyValuePair<eGoods, int> entry in costs.Costs)
            {
                int required = entry.Value * multiplier;
                if (required <= 0)
                    continue;

                int available = GamePlayerManagerAPI.Instance.GetGoodAmount(playerId, entry.Key);
                if (available < required)
                {
                    missingGood = entry.Key;
                    requiredAmount = required;
                    availableAmount = available;
                    return false;
                }
            }

            missingGood = eGoods.STORED_NULL;
            requiredAmount = 0;
            availableAmount = 0;
            return true;
        }

        private static bool TryGetMaxAffordableExtraCostAmount(
            int playerId,
            UnitExtraCostValues costs,
            out int affordableAmount,
            out eGoods limitingGood,
            out int limitingRequiredPerUnit,
            out int limitingAvailableAmount)
        {
            bool hasPositiveCost = false;
            affordableAmount = int.MaxValue;
            limitingGood = eGoods.STORED_NULL;
            limitingRequiredPerUnit = 0;
            limitingAvailableAmount = 0;

            foreach (KeyValuePair<eGoods, int> entry in costs.Costs)
            {
                int requiredPerUnit = entry.Value;
                if (requiredPerUnit <= 0)
                    continue;

                hasPositiveCost = true;
                int available = Math.Max(0, GamePlayerManagerAPI.Instance.GetGoodAmount(playerId, entry.Key));
                int affordableForGood = available / requiredPerUnit;
                if (affordableForGood >= affordableAmount)
                    continue;

                affordableAmount = affordableForGood;
                limitingGood = entry.Key;
                limitingRequiredPerUnit = requiredPerUnit;
                limitingAvailableAmount = available;
            }

            if (hasPositiveCost)
                return true;

            affordableAmount = 0;
            return false;
        }

        private static void ApplyExtraCosts(int playerId, UnitExtraCostValues costs, int multiplier)
        {
            foreach (KeyValuePair<eGoods, int> entry in costs.Costs)
            {
                int amount = entry.Value * multiplier;
                if (amount > 0)
                {
                    GamePlayerManagerAPI.Instance.RemoveGood(playerId, entry.Key, amount);
                }
                else if (amount < 0 && entry.Key == eGoods.STORED_GOLD)
                {
                    GamePlayerManagerAPI.Instance.TryAddGood(playerId, entry.Key, -amount);
                }
            }
        }

        private static int GetLocalHumanPlayerId()
        {
            int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return IsHumanPlayer(playerId) ? playerId : -1;
        }

        private static bool IsUnitCostModeAllowed() =>
            Shared.GameplayFeatureModePolicy.IsAllowed(
                UnitCostsPlugin.PluginGuid,
                Shared.GameplayFeatureId.UnitCostEnforcement,
                Shared.GameplayModActivationGate.Snapshot);

        private static bool IsHumanPlayer(int playerId)
        {
            try
            {
                return GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) &&
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsLocalPlayer(int playerId)
        {
            return playerId == GetLocalHumanPlayerId();
        }

        private void LogDebug(params object[] parts)
        {
            Shared.DebugLogHelper.LogDebug(log, parts);
        }

        private void ShowMissingResourcesMessage(eGoods missingGood)
        {
            PlayMissingResourcesSpeech(missingGood);
            DisplayMaterialNotification(SerpLocalization.Get(SerpLocalization.ResourcesMissing));
        }

        private void ShowMissingResourcesMessageThrottledForSiege(eGoods missingGood)
        {
            DateTime now = DateTime.UtcNow;
            if (now >= nextSiegeMissingResourcesSpeechUtc)
            {
                nextSiegeMissingResourcesSpeechUtc = now.AddMilliseconds(SiegeMissingResourcesSpeechThrottleMilliseconds);
                PlayMissingResourcesSpeech(missingGood);
            }

            if (now >= nextSiegeMissingResourcesMessageUtc)
            {
                nextSiegeMissingResourcesMessageUtc = now.AddMilliseconds(SiegeMissingResourcesMessageThrottleMilliseconds);
                DisplayMaterialNotification(SerpLocalization.Get(SerpLocalization.ResourcesMissing));
            }
        }

        private void PlayMissingResourcesSpeech(eGoods missingGood)
        {
            try
            {
                string speechFileName = missingGood == eGoods.STORED_GOLD
                    ? GetRandomSpeechFileName(MissingGoldSpeechFileNames)
                    : MissingWeaponsSpeechFileName;
                LogDebug("UnitCosts missing resources speech:", "good", missingGood, "file", speechFileName);

                SFXManager.instance?.playSpeech(
                    1,
                    speechFileName,
                    1f);
            }
            catch (Exception ex)
            {
                LogDebug("Could not play UnitCosts missing resources speech:", ex.Message);
            }
        }

        private static string GetRandomSpeechFileName(string[] speechFileNames)
        {
            lock (SpeechRandom)
            {
                return speechFileNames[SpeechRandom.Next(speechFileNames.Length)];
            }
        }

        private void DisplayMaterialNotification(string message)
        {
            Notification.Show(message);
            CancelMaterialMessageTimer();
            materialMessageTimerHandle = GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(
                MaterialMessageDurationMilliseconds,
                OnMaterialMessageTimerElapsed,
                null);
        }

        private void OnMaterialMessageTimerElapsed()
        {
            materialMessageTimerHandle = null;
            Notification.Hide();
        }

        private void HideMaterialMessage()
        {
            CancelMaterialMessageTimer();
            Notification.Hide();
        }

        private void CancelMaterialMessageTimer()
        {
            if (string.IsNullOrEmpty(materialMessageTimerHandle))
                return;

            try
            {
                GameTimeManagerAPI.Instance.GetTimerEngine().RemoveAction(materialMessageTimerHandle);
            }
            catch (Exception ex)
            {
                LogDebug("Could not cancel UnitCosts material message timer:", ex.Message);
            }

            materialMessageTimerHandle = null;
        }

        private static eChimps GetLastTroopBuildChimp(MainViewModel mainViewModel)
        {
            object value = GetMainViewModelMemberValue(
                mainViewModel,
                LastTroopBuildChimpField,
                LastTroopBuildChimpProperty);
            if (value == null)
                return eChimps.CHIMP_TYPE_ARCHER;

            try
            {
                return (eChimps)Convert.ToInt32(value);
            }
            catch
            {
                return eChimps.CHIMP_TYPE_ARCHER;
            }
        }

        private static int GetLastTroopsAmountToMake(MainViewModel mainViewModel)
        {
            object value = GetMainViewModelMemberValue(
                mainViewModel,
                LastTroopsAmountToMakeField,
                LastTroopsAmountToMakeProperty);
            if (value == null)
                return 1;

            try
            {
                return Math.Max(1, Convert.ToInt32(value));
            }
            catch
            {
                return 1;
            }
        }

        private static bool TryGetCurrentVanillaRecruitAmount(eChimps unitType, out int amount)
        {
            amount = 0;
            MainViewModel mainViewModel = MainViewModel.Instance;
            if (mainViewModel == null)
                return false;

            object unitValue = GetMainViewModelMemberValue(
                mainViewModel,
                LastTroopBuildChimpField,
                LastTroopBuildChimpProperty);
            object amountValue = GetMainViewModelMemberValue(
                mainViewModel,
                LastTroopsAmountToMakeField,
                LastTroopsAmountToMakeProperty);
            if (unitValue == null || amountValue == null)
                return false;

            try
            {
                if ((eChimps)Convert.ToInt32(unitValue) != unitType)
                    return false;

                amount = Math.Max(0, Convert.ToInt32(amountValue));
                return true;
            }
            catch
            {
                amount = 0;
                return false;
            }
        }

        private static object GetMainViewModelMemberValue(MainViewModel mainViewModel, FieldInfo field, PropertyInfo property)
        {
            if (mainViewModel == null)
                return null;

            if (field != null)
                return field.GetValue(mainViewModel);

            return property?.GetValue(mainViewModel);
        }

        private static Noesis.ImageSource GetGoodImage(eGoods good)
        {
            return MainViewModel.Instance.getSmallGoodsIcon((int)good);
        }

        private void CaptureVanillaGoldCosts()
        {
            foreach (eChimps unitType in GetRecruitTypes())
            {
                if (!VanillaGoldCosts.CaptureIfMissing(unitType, () => ReadVanillaUnitGoldCost(unitType), out Exception error))
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"UnitCosts could not capture the Vanilla gold cost for {unitType}; " +
                        $"-1 and immediate restoration remain disabled for this unit: {error}");
                }
            }
        }

        internal static bool IsEuropeanRecruit(eChimps unitType)
        {
            return unitType >= eChimps.CHIMP_TYPE_ARCHER &&
                unitType <= eChimps.CHIMP_TYPE_KNIGHT;
        }

        private static IEnumerable<eChimps> GetRecruitTypes()
        {
            yield return eChimps.CHIMP_TYPE_ARCHER;
            yield return eChimps.CHIMP_TYPE_SPEARMAN;
            yield return eChimps.CHIMP_TYPE_MACEMAN;
            yield return eChimps.CHIMP_TYPE_XBOWMAN;
            yield return eChimps.CHIMP_TYPE_PIKEMAN;
            yield return eChimps.CHIMP_TYPE_SWORDSMAN;
            yield return eChimps.CHIMP_TYPE_KNIGHT;
            yield return eChimps.CHIMP_TYPE_ENGINEER;
            yield return eChimps.CHIMP_TYPE_CATAPULT;
            yield return eChimps.CHIMP_TYPE_TREBUCHET;
            yield return eChimps.CHIMP_TYPE_BATTERING_RAM;
            yield return eChimps.CHIMP_TYPE_SIEGE_TOWER;
            yield return eChimps.CHIMP_TYPE_PORTABLE_SHIELD;
            yield return eChimps.CHIMP_TYPE_MONK;
            yield return eChimps.CHIMP_TYPE_LADDERMAN;
            yield return eChimps.CHIMP_TYPE_TUNNELER;
            yield return eChimps.CHIMP_TYPE_ARAB_BOW;
            yield return eChimps.CHIMP_TYPE_ARAB_SLAVE;
            yield return eChimps.CHIMP_TYPE_ARAB_SLINGER;
            yield return eChimps.CHIMP_TYPE_ARAB_ASSASIN;
            yield return eChimps.CHIMP_TYPE_ARAB_HORSEMAN;
            yield return eChimps.CHIMP_TYPE_ARAB_SWORDSMAN;
            yield return eChimps.CHIMP_TYPE_ARAB_GRENADIER;
            yield return eChimps.CHIMP_TYPE_ARAB_BALLISTA;
            yield return eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER;
            yield return eChimps.CHIMP_TYPE_BEDOUIN_HEALER;
            yield return eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH;
            yield return eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER;
            yield return eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER;
            yield return eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL;
            yield return eChimps.CHIMP_TYPE_BEDOUIN_SAPPER;
            yield return eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER;
        }

        private static int ReadVanillaUnitGoldCost(eChimps unitType)
        {
            if (TryGetSiegeTentStructure(unitType, out eStructs siegeTentStructure))
            {
                int defaultSiegeTentCost = GameBuildingManagerAPI.Instance.GetDefaultCost(siegeTentStructure).Gold;
                return UnitGoldCostSnapshotPolicy.SelectVanillaCost(true, 0, defaultSiegeTentCost);
            }

            int currentUnitCost = GameUnitManagerAPI.Instance.GetUnitGoldCost(unitType);
            return UnitGoldCostSnapshotPolicy.SelectVanillaCost(false, currentUnitCost, 0);
        }

        private static bool TryGetSiegeTentStructure(eChimps unitType, out eStructs siegeTentStructure)
        {
            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_CATAPULT:
                    siegeTentStructure = eStructs.STRUCT_SIEGE_TENT_CATAPULT;
                    return true;
                case eChimps.CHIMP_TYPE_TREBUCHET:
                    siegeTentStructure = eStructs.STRUCT_SIEGE_TENT_TREBUCHET;
                    return true;
                case eChimps.CHIMP_TYPE_BATTERING_RAM:
                    siegeTentStructure = eStructs.STRUCT_SIEGE_TENT_BATTERING_RAM;
                    return true;
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:
                    siegeTentStructure = eStructs.STRUCT_SIEGE_TENT_SIEGE_TOWER;
                    return true;
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD:
                    siegeTentStructure = eStructs.STRUCT_SIEGE_TENT_PORTABLE_SHIELD;
                    return true;
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                    siegeTentStructure = eStructs.STRUCT_SIEGE_TENT_ARAB_BALLISTA;
                    return true;
                default:
                    siegeTentStructure = eStructs.STRUCT_NULL;
                    return false;
            }
        }

        private static bool TryGetSiegeUnitFromTentStructure(eStructs siegeTentStructure, out eChimps unitType)
        {
            switch (siegeTentStructure)
            {
                case eStructs.STRUCT_SIEGE_TENT_CATAPULT:
                    unitType = eChimps.CHIMP_TYPE_CATAPULT;
                    return true;
                case eStructs.STRUCT_SIEGE_TENT_TREBUCHET:
                    unitType = eChimps.CHIMP_TYPE_TREBUCHET;
                    return true;
                case eStructs.STRUCT_SIEGE_TENT_BATTERING_RAM:
                    unitType = eChimps.CHIMP_TYPE_BATTERING_RAM;
                    return true;
                case eStructs.STRUCT_SIEGE_TENT_SIEGE_TOWER:
                    unitType = eChimps.CHIMP_TYPE_SIEGE_TOWER;
                    return true;
                case eStructs.STRUCT_SIEGE_TENT_PORTABLE_SHIELD:
                    unitType = eChimps.CHIMP_TYPE_PORTABLE_SHIELD;
                    return true;
                case eStructs.STRUCT_SIEGE_TENT_ARAB_BALLISTA:
                    unitType = eChimps.CHIMP_TYPE_ARAB_BALLISTA;
                    return true;
                default:
                    unitType = eChimps.CHIMP_TYPE_NULL;
                    return false;
            }
        }

        internal static string GetUnitSettingsTooltip(eChimps unitType)
        {
            StringBuilder builder = new StringBuilder(unitType.ToString());
            AppendVanillaGoldCost(builder, unitType);

            return builder.ToString();
        }

        private static void AppendVanillaGoldCost(StringBuilder builder, eChimps unitType)
        {
            int goldCost = GetVanillaTooltipGoldCost(unitType);
            builder.AppendLine();
            builder.Append("vanilla gold: ");
            builder.Append(goldCost);
        }

        private static int GetVanillaTooltipGoldCost(eChimps unitType)
        {
            if (TryGetSiegeTentStructure(unitType, out eStructs siegeTentStructure))
                return GetVanillaSiegeTentGoldCost(siegeTentStructure);

            return GetVanillaGoldCost(unitType);
        }

        private static int GetVanillaSiegeTentGoldCost(eStructs siegeTentStructure)
        {
            try
            {
                return Math.Max(0, GameBuildingManagerAPI.Instance.GetDefaultCost(siegeTentStructure).Gold);
            }
            catch
            {
                return 0;
            }
        }

        private static int GetVanillaGoldCost(eChimps unitType)
        {
            if (VanillaGoldCosts.TryGetValue(unitType, out int goldCost))
                return goldCost;

            try
            {
                return ReadVanillaUnitGoldCost(unitType);
            }
            catch
            {
                return 0;
            }
        }

        internal static string GetLocalizedUnitName(eChimps unitType)
        {
            int translationIndex = GetUnitNameTranslationIndex(unitType);
            if (TryGetLocalizedGameText("TEXT_CHIMP_NAMES", translationIndex, out string localizedName))
                return localizedName;

            string name = unitType.ToString();
            const string prefix = "CHIMP_TYPE_";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                name = name.Substring(prefix.Length);
            return name.Replace('_', ' ').ToLowerInvariant();
        }

        internal static string GetLocalizedGoodName(eGoods good, string fallback)
        {
            int index = (int)good;
            string translationKey = GetTranslationKey(GoodsTextSection, index);

            if (TryGetGameTextDictionaryValue(translationKey, out string localizedName))
                return localizedName;

            if (TryGetLocalizedGameTextExOnly(GoodsTextSection, index, out localizedName))
                return localizedName;

            return fallback;
        }

        private static bool TryGetGameTextDictionaryValue(string translationKey, out string localizedName)
        {
            localizedName = null;
            if (string.IsNullOrEmpty(translationKey))
                return false;

            if (CrusaderDE.Translate.Instance?.GameTexts != null &&
                CrusaderDE.Translate.Instance.GameTexts.TryGetValue(translationKey, out localizedName) &&
                !string.IsNullOrWhiteSpace(localizedName))
            {
                return true;
            }

            localizedName = null;
            return false;
        }

        private static bool TryGetLocalizedGameTextExOnly(string sectionName, int index, out string localizedName)
        {
            localizedName = null;
            if (string.IsNullOrEmpty(sectionName) || index < 0)
                return false;

            try
            {
                localizedName = GameTranslateAPI.Instance.GetLookUpTextEx(sectionName, index);
                if (!string.IsNullOrWhiteSpace(localizedName) &&
                    !string.Equals(localizedName, GetTranslationKey(sectionName, index), StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }

            localizedName = null;
            return false;
        }

        private static int GetUnitNameTranslationIndex(eChimps unitType)
        {
            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_TUNNELER: return 5;
                case eChimps.CHIMP_TYPE_LADDERMAN: return 29;
                case eChimps.CHIMP_TYPE_ENGINEER: return 30;
                case eChimps.CHIMP_TYPE_MONK: return 37;
                default:
                    return (int)unitType;
            }
        }

        private static bool TryGetLocalizedGameText(string sectionName, int index, out string localizedName)
        {
            localizedName = null;
            if (string.IsNullOrEmpty(sectionName) || index < 0)
                return false;

            try
            {
                localizedName = GameTranslateAPI.Instance.GetLookUpTextEx(sectionName, index);
                if (!string.IsNullOrWhiteSpace(localizedName))
                    return true;
            }
            catch (Exception)
            {
            }

            return TryGetLocalizedGameTextKey(GetTranslationKey(sectionName, index), out localizedName);
        }

        private static bool TryGetLocalizedGameTextKey(string translationKey, out string localizedName)
        {
            localizedName = null;
            if (string.IsNullOrEmpty(translationKey))
                return false;

            try
            {
                localizedName = GameTranslateAPI.Instance.GetLookUpText(translationKey);
                if (!string.IsNullOrWhiteSpace(localizedName) &&
                    !string.Equals(localizedName, translationKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }

            if (CrusaderDE.Translate.Instance?.GameTexts != null &&
                CrusaderDE.Translate.Instance.GameTexts.TryGetValue(translationKey, out localizedName) &&
                !string.IsNullOrWhiteSpace(localizedName))
            {
                return true;
            }

            localizedName = null;
            return false;
        }

        private static string GetTranslationKey(string sectionName, int index)
        {
            return sectionName + "_" + (index + 1).ToString("D3");
        }
    }
}
