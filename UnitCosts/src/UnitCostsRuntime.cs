using Shared;
using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace UnitCosts
{
    public sealed class UnitCostsRuntime
    {
        private readonly ManualLogSource log;
        private readonly UnitCostsLobbyViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private IDisposable unitTransitionSubscription;
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
        private bool recruitmentCostSnapshotRequiresHorse;
        private int recruitmentCostSnapshotAvailableHorses;
        private bool settingsChangedSubscribed;
        private bool libraryInitialized;
        private volatile bool callbacksEnabled;
        private volatile int noWeaponsUiMask;
        private bool loggedPostStartup;
        private const string GoodsTextSection = "TEXT_GOODS";
        private bool hooksSubscribed;
        private const int MaterialMessageDurationMilliseconds = 3000;
        private const int SiegeMissingResourcesMessageThrottleMilliseconds = 1000;
        private const int SiegeMissingResourcesSpeechThrottleMilliseconds = 10000;
        private const string MissingWeaponsSpeechFileName = "Other_Warning6.wav";
        private static readonly string[] MissingGoldSpeechFileNames = { "Units_Warning3.wav", "Units_Warning4.wav" };
        private static readonly Random SpeechRandom = new Random();
        // Reused per thread: the horse count runs per frame for tooltip/button checks.
        [ThreadStatic] private static List<int> horseStableIdsBuffer;
        private static readonly UnitGoldCostSnapshot<eChimps> VanillaGoldCosts = new UnitGoldCostSnapshot<eChimps>();
        private readonly UnitCostOverrides costOverrides = new UnitCostOverrides(
            unit => GameUnitManagerAPI.Instance.GetUnitGoldCost(unit),
            (unit, value) => GameUnitManagerAPI.Instance.SetUnitGoldCost(unit, value),
            tent => GameBuildingManagerAPI.Instance.GetGoldCost(tent),
            (tent, value) => GameBuildingManagerAPI.Instance.SetGoldCost(tent, value),
            unit => GameUnitManagerAPI.Instance.GetUnitGoodCosts(unit),
            (unit, value) => GameUnitManagerAPI.Instance.SetUnitGoodCosts(unit, value));

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
            TryInitializeFeature("horse-link lifecycle", SubscribeUnitTransition);
            TryInitializeFeature("Vanilla gold-cost capture", CaptureVanillaGoldCosts);
            libraryInitialized = true;
            SubscribeHooks();
            if (!EffectsEnabled)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts disabled; runtime callbacks inactive");
                return;
            }

            TryInitializeFeature("native gold costs", ApplyUnitCosts);
            TryInitializeFeature("extra-cost normalization", settings.NormalizeExtraCostsAfterNativeGoldChange);
            TryInitializeFeature("human extra costs", ApplyHumanExtraUnitCosts);
            callbacksEnabled = true;
        }

        private void SubscribeHooks()
        {
            if (hooksSubscribed)
                return;

            TrySubscribeFeature("gameplay session start", () =>
                Shared.GameplaySessionLifecycle.SubscribeStarted(log, OnSessionStarted));

            TrySubscribeFeature("map unload", () => Shared.MissionEvents.Ended
                    .Subscribe(OnUnloadMap));

            TrySubscribeFeature("placement validation", () => BuildingR3EventHooks.OnPlacementValidation.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnBuildingPlacementValidation));

            TrySubscribeFeature("building spawn", () => BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnBuildingSpawn));

            TryInitializeFeature("recruitment action enforcement", () => makeTroopGameActionHook = new MakeTroopGameActionHook(log, () => callbacksEnabled && EffectsEnabled, DecideMakeTroopGameAction));
            TryInitializeFeature("recruitment tooltip", () => createTroopHoverHook = new CreateTroopHoverHook(log, () => callbacksEnabled && EffectsEnabled, UpdateRecruitmentCostTooltip, ClearRecruitmentCostTooltip));
            TryInitializeFeature("siege tooltip", () => siegeBuildHoverHook = new SiegeBuildHoverHook(log, () => callbacksEnabled && EffectsEnabled, UpdateSiegeBuildCostTooltip, ClearRecruitmentCostTooltip));
            TryInitializeFeature("recruitment availability UI", () => recruitmentAvailabilityUiHook = new RecruitmentAvailabilityUiHook(log, () => callbacksEnabled && EffectsEnabled, IsNoWeaponsUiActive, RefreshRecruitmentUi));

            hooksSubscribed = true;
            Shared.DebugLogHelper.LogDebug(log, "UnitCosts runtime hooks subscribed");
        }

        private void SubscribeSettingsChanges()
        {
            if (settingsChangedSubscribed)
                return;

            settings.SettingChanged += OnSettingChanged;
            settingsChangedSubscribed = true;
        }

        private void SubscribeUnitTransition()
        {
            if (unitTransitionSubscription != null)
                return;

            unitTransitionSubscription = UnitR3EventHooks.OnUnitTransition.Observable.Subscribe(OnUnitTransition);
            if (unitTransitionSubscription == null)
                throw new InvalidOperationException("Unit transition subscription returned null.");

            Shared.DebugLogHelper.LogDebug(log, "UnitCosts horse-link lifecycle subscribed");
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
                Shared.DebugLogHelper.LogErrorLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:160", $"UnitCosts feature '{featureName}' failed; independent features continue: {ex}");
            }
        }

        private void TryInitializeFeature(string featureName, Action initialize)
        {
            try { initialize(); }
            catch (Exception ex)
            {
                Shared.RuntimeHealth.Failure(log, featureName, true, $"UnitCosts feature '{featureName}' failed; independent features continue: {ex}");
            }
        }

        private void OnSettingChanged(string propertyName)
        {

            if (propertyName == nameof(UnitCostsLobbyViewModel.EnableMod))
            {
                if (EffectsEnabled)
                {
                    callbacksEnabled = false;
                    TryInitializeFeature("Vanilla gold-cost capture", CaptureVanillaGoldCosts);
                    TryInitializeFeature("native gold costs", ApplyUnitCosts);
                    TryInitializeFeature("extra-cost normalization", settings.NormalizeExtraCostsAfterNativeGoldChange);
                    TryInitializeFeature("human extra costs", ApplyHumanExtraUnitCosts);
                    callbacksEnabled = true;
                }
                else
                {
                    callbacksEnabled = false;
                    RestoreOwnedUnitCosts();
                    HideMaterialMessage();
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

        private void OnSessionStarted(Shared.GameplaySessionStartedContext context)
        {
            try
            {
                if (!loggedPostStartup)
                {
                    loggedPostStartup = true;
                    Shared.DebugLogHelper.LogDebug(log, "UnitCosts runtime confirmed after startup cleanup: gameplay session started.");
                }
                if (!EffectsEnabled)
                    return;
                TryInitializeFeature("native gold costs", ApplyUnitCosts);
                TryInitializeFeature("extra-cost normalization", settings.NormalizeExtraCostsAfterNativeGoldChange);
                TryInitializeFeature("human extra costs", ApplyHumanExtraUnitCosts);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts gameplay-session initialization failed:", ex);
            }
        }

        private void OnUnloadMap(APIShared.MissionLifecycleNotification args)
        {
            RestoreOwnedUnitCosts(endSession: true);
            HideMaterialMessage();
        }

        private void OnModeAllowedChanged(bool allowed)
        {
            if (!libraryInitialized)
                return;

            if (EffectsEnabled)
            {
                callbacksEnabled = false;
                TryInitializeFeature("Vanilla gold-cost capture", CaptureVanillaGoldCosts);
                TryInitializeFeature("native gold costs", ApplyUnitCosts);
                TryInitializeFeature("extra-cost normalization", settings.NormalizeExtraCostsAfterNativeGoldChange);
                TryInitializeFeature("human extra costs", ApplyHumanExtraUnitCosts);
                callbacksEnabled = true;
            }
            else
            {
                callbacksEnabled = false;
                RestoreOwnedUnitCosts();
                HideMaterialMessage();
            }
        }

        private void ApplyUnitCosts()
        {
            Dictionary<eChimps, UnitCostValues> parsedCosts = settings.ParseUnitCosts();
            int changedValues = 0;
            int appliedNoWeaponsMask = 0;
            foreach (KeyValuePair<eChimps, UnitCostValues> entry in parsedCosts)
            {
                UnitCostValues values = entry.Value;
                TryInitializeFeature("gold cost for " + entry.Key, () => costOverrides.ApplyGold(entry.Key, values.Gold));
                if (TryGetSiegeTentStructure(entry.Key, out eStructs tent))
                    TryInitializeFeature("siege tent cost for " + entry.Key, () => costOverrides.ApplyTentGold(tent, values.Gold));
                try
                {
                    if (IsEuropeanRecruit(entry.Key))
                    {
                        costOverrides.ApplyMaterials(entry.Key, values.NoWeapons);
                        if (values.NoWeapons)
                            appliedNoWeaponsMask |= 1 << ((int)entry.Key - (int)eChimps.CHIMP_TYPE_ARCHER);
                    }
                    if (values.Gold != -1)
                        changedValues++;
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:284", $"UnitCosts could not apply {entry.Key}; remaining units continue: {ex}");
                }
            }

            noWeaponsUiMask = appliedNoWeaponsMask;
            Shared.DebugLogHelper.LogDebugLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:289", "Applied unit cost values:", changedValues);
        }

        private bool IsNoWeaponsUiActive(eChimps unitType)
        {
            int index = (int)unitType - (int)eChimps.CHIMP_TYPE_ARCHER;
            return callbacksEnabled && EffectsEnabled && index >= 0 && index < 7 &&
                (noWeaponsUiMask & (1 << index)) != 0;
        }

        private void RestoreOwnedUnitCosts(bool endSession = false)
        {
            costOverrides.RestoreAll(ex => Shared.DebugLogHelper.LogErrorLimited(log,
                "UnitCosts.cost-restoration", $"UnitCosts could not restore an owned cost; independent cells continue: {ex}"), endSession);
            noWeaponsUiMask = 0;
            humanExtraCosts.Clear();
            configuredRecruitmentButtons.Clear();
            ClearRecruitmentCostTooltip();
            Shared.DebugLogHelper.LogDebugLimited(log, "UnitCosts.owned-cost-restoration", "Released UnitCosts-owned cost values.");
        }

        private void ApplyHumanExtraUnitCosts()
        {
            humanExtraCosts.Clear();
            configuredRecruitmentButtons.Clear();
            Dictionary<eChimps, UnitExtraCostValues> parsedCosts = settings.ParseHumanExtraUnitCosts();
            int configuredUnits = 0;
            foreach (KeyValuePair<eChimps, UnitExtraCostValues> entry in parsedCosts)
            {
                humanExtraCosts[entry.Key] = entry.Value;
                if (entry.Value.HasAnyCost())
                    configuredUnits++;
            }

            RebuildConfiguredRecruitmentButtons();
            Shared.DebugLogHelper.LogDebugLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:380", "Applied human extra unit cost rows:", configuredUnits);
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
                Shared.DebugLogHelper.LogErrorLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:396", "UnitCosts local recruitment cost check failed:", ex);
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
            bool extraLimitingHorse = false;
            int extraLimitingRequiredPerUnit = 0;
            int extraLimitingAvailableAmount = 0;
            bool hasPositiveExtraCost = TryGetMaxAffordableExtraCostAmount(
                playerId,
                costs,
                out extraAffordableAmount,
                out extraLimitingGood,
                out extraLimitingHorse,
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
                Shared.DebugLogHelper.LogWarningLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:445", $"UnitCosts could not resolve Vanilla's current Ctrl amount; forwarding the " +
                    $"extra-cost ceiling instead: unit={unitType}, player={playerId}, " +
                    $"extraAffordable={extraAffordableAmount}, rawUnitType={rawUnitType}.");
                if (extraAffordableAmount > 0)
                    return MakeTroopGameActionDecision.ForwardAmount(extraAffordableAmount);

                ShowMissingResourcesMessage(extraLimitingGood, extraLimitingHorse);
                return MakeTroopGameActionDecision.BlockAction();
            }

            APIShared.Recruitment.RecruitmentConstraintDecision constraint =
                APIShared.Recruitment.RecruitmentRequestPolicy.ApplyMaximum(
                    amount,
                    vanillaRequestedAmount,
                    extraAffordableAmount,
                    interpretCtrlSentinel);

            Shared.DebugLogHelper.LogDebugLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:464", "UnitCosts extra-cost recruitment decision:",
                "unit", unitType,
                "player", playerId,
                "incomingAmount", amount,
                "interpretCtrlSentinel", interpretCtrlSentinel,
                "vanillaRequestedAmount", vanillaRequestedAmount,
                "effectiveRequestedAmount", constraint.EffectiveRequestedAmount,
                "extraAffordable", extraAffordableAmount,
                "extraLimitingGood", extraLimitingGood,
                "extraLimitingHorse", extraLimitingHorse,
                "extraRequiredPerUnit", extraLimitingRequiredPerUnit,
                "extraAvailable", extraLimitingAvailableAmount,
                "constraintAction", constraint.Action,
                "forwardedAmount", constraint.AmountToForward,
                "rawUnitType", rawUnitType);

            switch (constraint.Action)
            {
                case APIShared.Recruitment.RecruitmentConstraintAction.PreserveOriginal:
                    return MakeTroopGameActionDecision.AllowOriginal();
                case APIShared.Recruitment.RecruitmentConstraintAction.ForwardAmount:
                    return MakeTroopGameActionDecision.ForwardAmount(constraint.AmountToForward);
                default:
                    ShowMissingResourcesMessage(extraLimitingGood, extraLimitingHorse);
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

            int availableHorses = 0;
            if (costs.RequiresHorse)
            {
                availableHorses = CountAvailableHorseSlots(playerId);
                entries.Add(new UnitRecruitmentCostEntry
                {
                    Amount = "   " + multiplier + " ",
                    AmountAvailable = $"({availableHorses})",
                    Image = GetHorseImage()
                });
            }

            recruitmentCostSnapshotPlayerId = playerId;
            recruitmentCostSnapshotUnitType = currentTooltipUnitType;
            recruitmentCostSnapshotMultiplier = multiplier;
            recruitmentCostSnapshotRequiresHorse = costs.RequiresHorse;
            recruitmentCostSnapshotAvailableHorses = availableHorses;
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

            if (recruitmentCostSnapshotRequiresHorse != costs.RequiresHorse ||
                (costs.RequiresHorse && recruitmentCostSnapshotAvailableHorses != CountAvailableHorseSlots(playerId)))
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

        private readonly System.Collections.Generic.List<RecruitmentButtonRule> configuredRecruitmentButtons =
            new System.Collections.Generic.List<RecruitmentButtonRule>();
        private static readonly RecruitmentButtonRule[] RecruitmentButtonRules =
        {
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARCHER, panel => panel.RefRecruitArcherButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_SPEARMAN, panel => panel.RefRecruitSpearmanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_MACEMAN, panel => panel.RefRecruitMacemanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_XBOWMAN, panel => panel.RefRecruitXBowmanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_PIKEMAN, panel => panel.RefRecruitPikemanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_SWORDSMAN, panel => panel.RefRecruitSwordsmanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_KNIGHT, panel => panel.RefRecruitKnightButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ENGINEER, panel => panel.RefRecruitEngineerButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_LADDERMAN, panel => panel.RefRecruitLaddermanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_TUNNELER, panel => panel.RefRecruitTunellerButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_MONK, panel => panel.RefRecruitMonkButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_BOW, panel => panel.RefRecruitArabBowButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_SLAVE, panel => panel.RefRecruitArabSlaveButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_SLINGER, panel => panel.RefRecruitArabSlingerButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_ASSASIN, panel => panel.RefRecruitArabAssassinButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_HORSEMAN, panel => panel.RefRecruitArabHorseArcherButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_SWORDSMAN, panel => panel.RefRecruitArabSwordsmanButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_ARAB_GRENADIER, panel => panel.RefRecruitArabGrenadierButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER, panel => panel.RefRecruitBedouinCamelLancerButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, panel => panel.RefRecruitBedouinHealerButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, panel => panel.RefRecruitBedouinEunuchButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER, panel => panel.RefRecruitBedouinAmbusherButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER, panel => panel.RefRecruitBedouinSkirmisherButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL, panel => panel.RefRecruitBedouinHeavyCamelButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_SAPPER, panel => panel.RefRecruitBedouinSapperButton),
            new RecruitmentButtonRule(eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER, panel => panel.RefRecruitBedouinDemolisherButton),
        };

        private readonly struct RecruitmentButtonRule
        {
            internal RecruitmentButtonRule(eChimps unitType, System.Func<HUD_Buildings, Noesis.UIElement> getButton)
            {
                UnitType = unitType;
                GetButton = getButton;
            }
            internal eChimps UnitType { get; }
            internal System.Func<HUD_Buildings, Noesis.UIElement> GetButton { get; }
        }

        private void RebuildConfiguredRecruitmentButtons()
        {
            configuredRecruitmentButtons.Clear();
            foreach (RecruitmentButtonRule rule in RecruitmentButtonRules)
                if (TryGetHumanExtraCosts(rule.UnitType, out _))
                    configuredRecruitmentButtons.Add(rule);
        }

        internal void RefreshRecruitmentButtonAvailability()
        {
            if (configuredRecruitmentButtons.Count == 0 || !EffectsEnabled || !IsUnitCostModeAllowed())
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

            // Resolve from the current panel; retain Vanilla/other-mod disabled states.
            foreach (RecruitmentButtonRule rule in configuredRecruitmentButtons)
                DisableRecruitmentButtonIfMissingExtraCosts(playerId, amount, rule.UnitType, rule.GetButton(panel));
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

            if (!HasEnoughExtraCosts(playerId, costs, amount, out eGoods _, out bool _, out int _, out int _))
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
                if (args.Phase != EventHookPhase.Pre)
                    return;

                if (args.Source == UnitTransitionSource.Disband)
                {
                    TryConsumeStableHorseForDisband(args.UnitId, args.PlayerOwnerId);
                    return;
                }

                // Source test first: most transitions (AI, peasants, upgrades) never reach the mode policy.
                if (args.Source != UnitTransitionSource.EuropeanBarracks &&
                    args.Source != UnitTransitionSource.MercenaryOutpost)
                    return;

                if (!EffectsEnabled || !IsUnitCostModeAllowed())
                    return;

                int playerId = args.PlayerOwnerId;
                if (!IsHumanPlayer(playerId))
                    return;

                if (!TryGetHumanExtraCosts(args.NextUnitType, out UnitExtraCostValues costs))
                    return;

                if (!HasEnoughExtraCosts(playerId, costs, 1, out eGoods missingGood, out bool missingHorse, out int requiredAmount, out int availableAmount))
                {
                    LogDebug("UnitCosts/src/UnitCostsRuntime.cs:859",
                        "UnitCosts transition extra cost missing:",
                        "unit", args.NextUnitType,
                        "player", playerId,
                        "missing", missingHorse ? "HORSE" : missingGood.ToString(),
                        "required", requiredAmount,
                        "available", availableAmount);
                    return;
                }

                HorseAllocation horseAllocation = default;
                bool horseLinked = false;
                if (costs.RequiresHorse)
                {
                    // This hook is UnitCosts' existing successful recruitment commit point.
                    // Reserve the horse before charging the remaining extra costs.
                    if (!TryFindAvailableHorseAllocation(playerId, out horseAllocation) ||
                        !TryLinkStableHorse(horseAllocation, args.UnitId, playerId))
                    {
                        LogDebug("UnitCosts/src/UnitCostsRuntime.cs:878",
                            "UnitCosts transition horse link failed:",
                            "unit", args.NextUnitType,
                            "unitId", args.UnitId,
                            "player", playerId);
                        return;
                    }

                    horseLinked = true;
                }

                try
                {
                    ApplyExtraCosts(playerId, costs, 1);
                }
                catch
                {
                    if (horseLinked)
                        RollbackStableHorseLink(horseAllocation, args.UnitId);
                    throw;
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:903", "UnitCosts OnUnitTransition failed:", ex);
            }
        }

        private void OnBuildingPlacementValidation(BuildingPlacementValidationEventArgs args)
        {
            try
            {
                if (!EffectsEnabled || !IsUnitCostModeAllowed())
                    return;

                if (!IsHumanPlayer(args.PlayerId) || !IsLocalPlayer(args.PlayerId))
                    return;

                if (!TryGetSiegeUnitFromMapper(args.Mappers, out eChimps unitType))
                    return;

                if (!TryGetHumanExtraCosts(unitType, out UnitExtraCostValues costs))
                    return;

                if (HasEnoughExtraCosts(args.PlayerId, costs, 1, out eGoods missingGood, out bool missingHorse, out int requiredAmount, out int availableAmount))
                    return;

                args.CustomValidationRules = true;
                args.ForceBlockPlacementState = true;
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:928",
                    "UnitCosts blocked siege placement:",
                    "unit", unitType,
                    "player", args.PlayerId,
                    "missing", missingHorse ? "HORSE" : missingGood.ToString(),
                    "required", requiredAmount,
                    "available", availableAmount,
                    "mapper", args.Mappers);
                ShowMissingResourcesMessageThrottledForSiege(missingGood);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:940", "UnitCosts siege placement validation failed:", ex);
            }
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            try
            {
                if (!EffectsEnabled || !IsUnitCostModeAllowed())
                    return;

                if (!IsHumanPlayer(args.PlayerId))
                    return;

                if (!TryGetSiegeUnitFromTentStructure(args.Building, out eChimps unitType))
                    return;

                if (!TryGetHumanExtraCosts(unitType, out UnitExtraCostValues costs))
                    return;

                if (!HasEnoughExtraCosts(args.PlayerId, costs, 1, out eGoods missingGood, out bool missingHorse, out int requiredAmount, out int availableAmount))
                {
                    LogDebug("UnitCosts/src/UnitCostsRuntime.cs:962",
                        "UnitCosts siege extra cost skipped after spawn because resources are missing:",
                        "unit", unitType,
                        "player", args.PlayerId,
                        "missing", missingHorse ? "HORSE" : missingGood.ToString(),
                        "required", requiredAmount,
                        "available", availableAmount,
                        "building", args.Building);
                    if (IsLocalPlayer(args.PlayerId))
                        ShowMissingResourcesMessageThrottledForSiege(missingGood);
                    return;
                }

                ApplyExtraCosts(args.PlayerId, costs, 1);
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:976",
                    "UnitCosts applied siege extra costs:",
                    "unit", unitType,
                    "player", args.PlayerId,
                    "building", args.Building);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:984", "UnitCosts OnBuildingSpawn failed:", ex);
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
            out bool missingHorse,
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
                    missingHorse = false;
                    requiredAmount = required;
                    availableAmount = available;
                    return false;
                }
            }

            if (costs.RequiresHorse)
            {
                int availableHorses = CountAvailableHorseSlots(playerId);
                if (availableHorses < multiplier)
                {
                    missingGood = eGoods.STORED_NULL;
                    missingHorse = true;
                    requiredAmount = multiplier;
                    availableAmount = availableHorses;
                    return false;
                }
            }

            missingGood = eGoods.STORED_NULL;
            missingHorse = false;
            requiredAmount = 0;
            availableAmount = 0;
            return true;
        }

        private static bool TryGetMaxAffordableExtraCostAmount(
            int playerId,
            UnitExtraCostValues costs,
            out int affordableAmount,
            out eGoods limitingGood,
            out bool limitingHorse,
            out int limitingRequiredPerUnit,
            out int limitingAvailableAmount)
        {
            bool hasPositiveCost = false;
            affordableAmount = int.MaxValue;
            limitingGood = eGoods.STORED_NULL;
            limitingHorse = false;
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
                limitingHorse = false;
                limitingRequiredPerUnit = requiredPerUnit;
                limitingAvailableAmount = available;
            }

            if (costs.RequiresHorse)
            {
                hasPositiveCost = true;
                int availableHorses = CountAvailableHorseSlots(playerId);
                if (availableHorses < affordableAmount)
                {
                    affordableAmount = UnitExtraHorseCostPolicy.ApplyHorseAffordabilityLimit(
                        affordableAmount,
                        availableHorses);
                    limitingGood = eGoods.STORED_NULL;
                    limitingHorse = true;
                    limitingRequiredPerUnit = 1;
                    limitingAvailableAmount = availableHorses;
                }
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

        private static int CountAvailableHorseSlots(int playerId)
        {
            int count = 0;
            List<int> stableIds = horseStableIdsBuffer ?? (horseStableIdsBuffer = new List<int>());
            stableIds.Clear();
            GameBuildingManagerAPI.Instance.GetAllBuildings(
                stableIds,
                AliveState.IsAlive,
                eStructs.STRUCT_STABLES,
                PlayerRelationship.Self,
                playerId);

            // A sum is order-independent, so no sort here (allocation keeps its sorted order).
            foreach (int stableId in stableIds)
            {
                unsafe
                {
                    if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(stableId, out GameBuilding* stable) ||
                        !IsUsableStable(stable, playerId))
                    {
                        continue;
                    }

                    count += GetAvailableStableHorseCount(stable);
                }
            }

            return count;
        }

        private static bool TryFindAvailableHorseAllocation(int playerId, out HorseAllocation allocation)
        {
            List<int> stableIds = new List<int>();
            GameBuildingManagerAPI.Instance.GetAllBuildings(
                stableIds,
                AliveState.IsAlive,
                eStructs.STRUCT_STABLES,
                PlayerRelationship.Self,
                playerId);
            stableIds.Sort();

            foreach (int stableId in stableIds)
            {
                unsafe
                {
                    if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(stableId, out GameBuilding* stable) ||
                        !IsUsableStable(stable, playerId) ||
                        GetAvailableStableHorseCount(stable) <= 0)
                    {
                        continue;
                    }

                    for (int slot = 0; slot < StableHorseSlotCount; slot++)
                    {
                        if (!IsStableHorseSlotFree(stable, slot))
                            continue;

                        allocation = new HorseAllocation(stableId, (int)stable->r_GlobalId, playerId, slot);
                        return true;
                    }
                }
            }

            allocation = default;
            return false;
        }

        private unsafe bool TryLinkStableHorse(HorseAllocation allocation, int unitId, int playerId)
        {
            if (unitId <= 0 || unitId > ushort.MaxValue ||
                allocation.StableId <= 0 || allocation.StableId > ushort.MaxValue ||
                allocation.Slot < 0 || allocation.Slot >= StableHorseSlotCount ||
                allocation.OwnerPlayerId != playerId)
            {
                return false;
            }

            if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                (APIShared.UnitAccess.TryGetById(unitId, out _, out _) ? GameUnitManagerAPI.Instance.GetOwner(unitId) : -1) != playerId ||
                unit->r_GlobalId == 0 ||
                unit->r_LinkedStableBuildingId != 0 ||
                unit->r_LinkedStableGlobalId != 0)
            {
                return false;
            }

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(allocation.StableId, out GameBuilding* stable) ||
                !IsUsableStable(stable, playerId) ||
                (int)stable->r_GlobalId != allocation.StableGlobalId ||
                GetAvailableStableHorseCount(stable) <= 0 ||
                !IsStableHorseSlotFree(stable, allocation.Slot))
            {
                return false;
            }

            int unitGlobalId = (int)unit->r_GlobalId;
            GameBuildingManagerAPI.Instance.SetStablesUnitIdLink(
                allocation.StableId,
                allocation.Slot,
                unitId,
                unitGlobalId,
                bidirectional: true);

            bool slotMatches = GetStableHorseSlotUnitId(stable, allocation.Slot) == unitId &&
                GetStableHorseSlotGlobalId(stable, allocation.Slot) == unitGlobalId;
            bool backlinkMatches = unit->r_LinkedStableBuildingId == allocation.StableId &&
                unit->r_LinkedStableGlobalId == (uint)allocation.StableGlobalId;
            if (slotMatches && backlinkMatches)
                return true;

            if (slotMatches)
            {
                GameBuildingManagerAPI.Instance.UnlinkStablesUnitIdLink(
                    allocation.StableId,
                    allocation.Slot,
                    bidirectional: true);
            }
            if (unit->r_LinkedStableBuildingId == allocation.StableId &&
                unit->r_LinkedStableGlobalId == (uint)allocation.StableGlobalId)
            {
                unit->r_LinkedStableBuildingId = 0;
                unit->r_LinkedStableGlobalId = 0;
            }

            LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1240",
                "UnitCosts rolled back incomplete stable horse link:",
                "stableId", allocation.StableId,
                "slot", allocation.Slot,
                "unitId", unitId,
                "slotMatches", slotMatches,
                "backlinkMatches", backlinkMatches);
            return false;
        }

        private unsafe void RollbackStableHorseLink(HorseAllocation allocation, int unitId)
        {
            if (allocation.Slot < 0 || allocation.Slot >= StableHorseSlotCount ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(allocation.StableId, out GameBuilding* stable) ||
                (int)stable->r_GlobalId != allocation.StableGlobalId ||
                GetStableHorseSlotUnitId(stable, allocation.Slot) != unitId)
            {
                return;
            }

            GameBuildingManagerAPI.Instance.UnlinkStablesUnitIdLink(
                allocation.StableId,
                allocation.Slot,
                bidirectional: true);
        }

        private unsafe void TryConsumeStableHorseForDisband(int unitId, int playerId)
        {
            if (unitId <= 0 || unitId > ushort.MaxValue ||
                !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                (APIShared.UnitAccess.TryGetById(unitId, out _, out _) ? GameUnitManagerAPI.Instance.GetOwner(unitId) : -1) != playerId ||
                unit->r_GlobalId == 0)
            {
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1273",
                    "UnitCosts ignored invalid disband horse-link source:",
                    "unitId", unitId,
                    "player", playerId);
                return;
            }

            int stableId = unit->r_LinkedStableBuildingId;
            uint stableGlobalId = unit->r_LinkedStableGlobalId;
            uint unitGlobalId = unit->r_GlobalId;
            if (stableId <= 0 || stableId > ushort.MaxValue || stableGlobalId == 0)
                return;

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(stableId, out GameBuilding* stable) ||
                !IsUsableStable(stable, playerId) ||
                stable->r_GlobalId != stableGlobalId)
            {
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1290",
                    "UnitCosts kept inconsistent disband horse link:",
                    "unitId", unitId,
                    "unitGlobalId", unitGlobalId,
                    "stableId", stableId,
                    "stableGlobalId", stableGlobalId,
                    "reason", "stable validation failed");
                return;
            }

            int matchingSlot = -1;
            for (int slot = 0; slot < StableHorseSlotCount; slot++)
            {
                if (GetStableHorseSlotUnitId(stable, slot) != unitId ||
                    unchecked((uint)GetStableHorseSlotGlobalId(stable, slot)) != unitGlobalId)
                {
                    continue;
                }

                if (matchingSlot >= 0)
                {
                    LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1311",
                        "UnitCosts kept ambiguous disband horse link:",
                        "unitId", unitId,
                        "unitGlobalId", unitGlobalId,
                        "stableId", stableId,
                        "firstSlot", matchingSlot,
                        "secondSlot", slot);
                    return;
                }

                matchingSlot = slot;
            }

            if (matchingSlot < 0)
            {
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1326",
                    "UnitCosts kept unmatched disband horse backlink:",
                    "unitId", unitId,
                    "unitGlobalId", unitGlobalId,
                    "stableId", stableId);
                return;
            }

            int totalBefore = stable->r_TotalHorses;
            int usedBefore = stable->r_UsedHorses;
            int rechargeBefore = stable->r_HorseRechargeTimer;
            if (!TryCountOccupiedStableHorseSlots(stable, out int occupiedSlotsBefore) ||
                !UnitExtraHorseCostPolicy.IsOccupiedStableHorseSlotCountValid(
                    totalBefore,
                    occupiedSlotsBefore) ||
                !UnitExtraHorseCostPolicy.TryCalculateConsumedHorseTotal(
                    totalBefore,
                    usedBefore,
                    out int totalAfter))
            {
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1346",
                    "UnitCosts kept invalid disband horse accounting:",
                    "unitId", unitId,
                    "stableId", stableId,
                    "total", totalBefore,
                    "used", usedBefore,
                    "occupiedSlots", occupiedSlotsBefore,
                    "recharge", rechargeBefore);
                return;
            }

            stable->r_TotalHorses = (byte)totalAfter;
            try
            {
                GameBuildingManagerAPI.Instance.UnlinkStablesUnitIdLink(
                    stableId,
                    matchingSlot,
                    bidirectional: true);
            }
            catch
            {
                RollbackDisbandHorseConsumption(
                    stableId,
                    stableGlobalId,
                    playerId,
                    matchingSlot,
                    unitId,
                    unitGlobalId,
                    totalBefore,
                    totalAfter,
                    usedBefore,
                    rechargeBefore,
                    occupiedSlotsBefore);
                throw;
            }

            bool slotCleared = GetStableHorseSlotUnitId(stable, matchingSlot) == 0 &&
                GetStableHorseSlotGlobalId(stable, matchingSlot) == 0;
            bool backlinkCleared = unit->r_LinkedStableBuildingId == 0 &&
                unit->r_LinkedStableGlobalId == 0;
            bool slotTopologyMatches = TryCountOccupiedStableHorseSlots(stable, out int occupiedSlotsAfter) &&
                occupiedSlotsAfter == occupiedSlotsBefore - 1 &&
                UnitExtraHorseCostPolicy.IsOccupiedStableHorseSlotCountValid(
                    totalAfter,
                    occupiedSlotsAfter);
            bool accountingMatches = stable->r_TotalHorses == totalAfter &&
                stable->r_UsedHorses == usedBefore &&
                stable->r_HorseRechargeTimer == rechargeBefore;
            if (!slotCleared || !backlinkCleared || !slotTopologyMatches || !accountingMatches)
            {
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1396",
                    "UnitCosts disband horse consumption verification failed:",
                    "unitId", unitId,
                    "stableId", stableId,
                    "slot", matchingSlot,
                    "slotCleared", slotCleared,
                    "backlinkCleared", backlinkCleared,
                    "slotTopologyMatches", slotTopologyMatches,
                    "occupiedSlotsBefore", occupiedSlotsBefore,
                    "occupiedSlotsAfter", occupiedSlotsAfter,
                    "accountingMatches", accountingMatches);
                RollbackDisbandHorseConsumption(
                    stableId,
                    stableGlobalId,
                    playerId,
                    matchingSlot,
                    unitId,
                    unitGlobalId,
                    totalBefore,
                    totalAfter,
                    usedBefore,
                    rechargeBefore,
                    occupiedSlotsBefore);
                return;
            }

            Shared.DebugLogHelper.LogDebugLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:1422", "UnitCosts consumed stable horse on disband:",
                "unitId", unitId,
                "stableId", stableId,
                "slot", matchingSlot,
                "totalBefore", totalBefore,
                "totalAfter", totalAfter);
        }

        private unsafe void RollbackDisbandHorseConsumption(
            int stableId,
            uint stableGlobalId,
            int playerId,
            int slot,
            int unitId,
            uint unitGlobalId,
            int totalBefore,
            int totalAfter,
            int usedBefore,
            int rechargeBefore,
            int occupiedSlotsBefore)
        {
            if (slot < 0 || slot >= StableHorseSlotCount ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(stableId, out GameBuilding* stable) ||
                !IsUsableStable(stable, playerId) ||
                stable->r_GlobalId != stableGlobalId ||
                !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                unit->r_GlobalId != unitGlobalId)
            {
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1452",
                    "UnitCosts could not reacquire disband horse rollback objects:",
                    "unitId", unitId,
                    "stableId", stableId,
                    "slot", slot);
                return;
            }

            bool slotIsFree = IsStableHorseSlotFree(stable, slot);
            bool slotAlreadyRestored = GetStableHorseSlotUnitId(stable, slot) == unitId &&
                unchecked((uint)GetStableHorseSlotGlobalId(stable, slot)) == unitGlobalId;
            bool backlinkIsFree = unit->r_LinkedStableBuildingId == 0 &&
                unit->r_LinkedStableGlobalId == 0;
            bool backlinkAlreadyRestored = unit->r_LinkedStableBuildingId == stableId &&
                unit->r_LinkedStableGlobalId == stableGlobalId;
            bool backlinkCanRestore = backlinkIsFree || backlinkAlreadyRestored;
            bool totalCanRestore = stable->r_TotalHorses == totalAfter ||
                stable->r_TotalHorses == totalBefore;
            bool vanillaAccountingUnchanged = stable->r_UsedHorses == usedBefore &&
                stable->r_HorseRechargeTimer == rechargeBefore;
            bool slotTopologyCanRestore = TryCountOccupiedStableHorseSlots(stable, out int occupiedSlotsCurrent) &&
                ((slotIsFree && occupiedSlotsCurrent == occupiedSlotsBefore - 1) ||
                 (slotAlreadyRestored && occupiedSlotsCurrent == occupiedSlotsBefore));
            if ((!slotIsFree && !slotAlreadyRestored) ||
                !backlinkCanRestore || !totalCanRestore || !vanillaAccountingUnchanged ||
                !slotTopologyCanRestore)
            {
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1479",
                    "UnitCosts skipped unsafe disband horse rollback:",
                    "unitId", unitId,
                    "stableId", stableId,
                    "slot", slot,
                    "slotIsFree", slotIsFree,
                    "slotAlreadyRestored", slotAlreadyRestored,
                    "backlinkCanRestore", backlinkCanRestore,
                    "totalCanRestore", totalCanRestore,
                    "vanillaAccountingUnchanged", vanillaAccountingUnchanged,
                    "slotTopologyCanRestore", slotTopologyCanRestore,
                    "occupiedSlotsCurrent", occupiedSlotsCurrent);
                return;
            }

            stable->r_TotalHorses = (byte)totalBefore;
            if (slotIsFree || !backlinkAlreadyRestored)
            {
                GameBuildingManagerAPI.Instance.SetStablesUnitIdLink(
                    stableId,
                    slot,
                    unitId,
                    unchecked((int)unitGlobalId),
                    bidirectional: true);
            }

            bool rollbackMatches = GetStableHorseSlotUnitId(stable, slot) == unitId &&
                unchecked((uint)GetStableHorseSlotGlobalId(stable, slot)) == unitGlobalId &&
                unit->r_LinkedStableBuildingId == stableId &&
                unit->r_LinkedStableGlobalId == stableGlobalId &&
                stable->r_TotalHorses == totalBefore &&
                stable->r_UsedHorses == usedBefore &&
                stable->r_HorseRechargeTimer == rechargeBefore &&
                TryCountOccupiedStableHorseSlots(stable, out int occupiedSlotsRestored) &&
                occupiedSlotsRestored == occupiedSlotsBefore;
            LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1514",
                rollbackMatches
                    ? "UnitCosts rolled back disband horse consumption:"
                    : "UnitCosts disband horse rollback verification failed:",
                "unitId", unitId,
                "stableId", stableId,
                "slot", slot);
        }

        private static unsafe bool TryCountOccupiedStableHorseSlots(
            GameBuilding* stable,
            out int occupiedSlots)
        {
            occupiedSlots = 0;
            for (int slot = 0; slot < StableHorseSlotCount; slot++)
            {
                int linkedUnitId = GetStableHorseSlotUnitId(stable, slot);
                long linkedUnitGlobalId = GetStableHorseSlotGlobalId(stable, slot);
                if (!UnitExtraHorseCostPolicy.IsStableHorseSlotPairComplete(
                        linkedUnitId,
                        linkedUnitGlobalId))
                {
                    return false;
                }

                if (linkedUnitId != 0)
                    occupiedSlots++;
            }

            return true;
        }

        private const int StableHorseSlotCount = 4;

        private static unsafe bool IsUsableStable(GameBuilding* stable, int playerId)
        {
            return stable != null &&
                stable->r_AliveState == AliveState.IsAlive &&
                stable->r_BuildingType == eStructs.STRUCT_STABLES &&
                stable->r_PlayerIdOwner == playerId;
        }

        private static unsafe int GetAvailableStableHorseCount(GameBuilding* stable)
        {
            // Vanilla owns both counters. Free link pairs additionally cap availability while
            // r_UsedHorses is waiting for the regular stable update to recount them.
            return UnitExtraHorseCostPolicy.CalculateAvailableHorseSlots(
                stable->r_TotalHorses,
                stable->r_UsedHorses,
                CountFreeStableHorseSlots(stable));
        }

        private static unsafe int CountFreeStableHorseSlots(GameBuilding* stable)
        {
            int count = 0;
            for (int slot = 0; slot < StableHorseSlotCount; slot++)
            {
                if (IsStableHorseSlotFree(stable, slot))
                    count++;
            }

            return count;
        }

        private static unsafe bool IsStableHorseSlotFree(GameBuilding* stable, int slot) =>
            GetStableHorseSlotUnitId(stable, slot) == 0 &&
            GetStableHorseSlotGlobalId(stable, slot) == 0;

        private static unsafe int GetStableHorseSlotUnitId(GameBuilding* stable, int slot)
        {
            // The Script Extender getter reads these ushort fields through int*, so use
            // explicit, range-checked field access until that API is corrected upstream.
            switch (slot)
            {
                case 0: return stable->r_UsedHorse1UnitId;
                case 1: return stable->r_UsedHorse2UnitId;
                case 2: return stable->r_UsedHorse3UnitId;
                case 3: return stable->r_UsedHorse4UnitId;
                default: return -1;
            }
        }

        private static unsafe int GetStableHorseSlotGlobalId(GameBuilding* stable, int slot)
        {
            switch (slot)
            {
                case 0: return (int)stable->r_UsedHorse1GlobalId;
                case 1: return (int)stable->r_UsedHorse2GlobalId;
                case 2: return (int)stable->r_UsedHorse3GlobalId;
                case 3: return (int)stable->r_UsedHorse4GlobalId;
                default: return -1;
            }
        }

        private readonly struct HorseAllocation
        {
            public HorseAllocation(int stableId, int stableGlobalId, int ownerPlayerId, int slot)
            {
                StableId = stableId;
                StableGlobalId = stableGlobalId;
                OwnerPlayerId = ownerPlayerId;
                Slot = slot;
            }

            public int StableId { get; }
            public int StableGlobalId { get; }
            public int OwnerPlayerId { get; }
            public int Slot { get; }
        }

        private static int GetLocalHumanPlayerId()
        {
            int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return IsHumanPlayer(playerId) ? playerId : -1;
        }

        private static bool IsUnitCostModeAllowed() =>
            Shared.GameplayModActivationGate.IsFeatureAllowed(UnitCostsPlugin.PluginGuid, Shared.GameplayFeatureId.UnitCostEnforcement);

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

        private void LogDebug(string site, params object[] parts)
        {
            Shared.DebugLogHelper.LogDebugLimited(log, site, parts);
        }

        private void ShowMissingResourcesMessage(eGoods missingGood, bool missingHorse = false)
        {
            PlayMissingResourcesSpeech(missingHorse ? eGoods.STORED_NULL : missingGood);
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
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1688", "UnitCosts missing resources speech:", "good", missingGood, "file", speechFileName);

                SFXManager.instance?.playSpeech(
                    1,
                    speechFileName,
                    1f);
            }
            catch (Exception ex)
            {
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1697", "Could not play UnitCosts missing resources speech:", ex.Message);
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
                LogDebug("UnitCosts/src/UnitCostsRuntime.cs:1742", "Could not cancel UnitCosts material message timer:", ex.Message);
            }

            materialMessageTimerHandle = null;
        }

        // Both fields are public in the shipped Assembly-CSharp; typed reads avoid per-frame boxing.
        private static eChimps GetLastTroopBuildChimp(MainViewModel mainViewModel)
        {
            if (mainViewModel == null)
                return eChimps.CHIMP_TYPE_ARCHER;

            return (eChimps)(int)mainViewModel.lastTroopBuildChimp;
        }

        private static int GetLastTroopsAmountToMake(MainViewModel mainViewModel)
        {
            if (mainViewModel == null)
                return 1;

            return Math.Max(1, mainViewModel.lastTroopsAmountToMake);
        }

        private static bool TryGetCurrentVanillaRecruitAmount(eChimps unitType, out int amount)
        {
            amount = 0;
            MainViewModel mainViewModel = MainViewModel.Instance;
            if (mainViewModel == null)
                return false;

            if ((eChimps)(int)mainViewModel.lastTroopBuildChimp != unitType)
                return false;

            amount = Math.Max(0, mainViewModel.lastTroopsAmountToMake);
            return true;
        }

        private static Noesis.ImageSource GetGoodImage(eGoods good)
        {
            return MainViewModel.Instance.getSmallGoodsIcon((int)good);
        }

        private static Noesis.ImageSource GetHorseImage()
        {
            try
            {
                return Noesis.GUI.GetApplicationResources()?["UI-Buttons M025"] as Noesis.ImageSource;
            }
            catch
            {
                return null;
            }
        }

        private void CaptureVanillaGoldCosts()
        {
            foreach (eChimps unitType in GetRecruitTypes())
            {
                if (!VanillaGoldCosts.CaptureIfMissing(unitType, () => ReadVanillaUnitGoldCost(unitType), out Exception error))
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "UnitCosts/src/UnitCostsRuntime.cs:1853", $"UnitCosts could not capture the Vanilla gold cost for {unitType}; " +
                        $"The startup cost tooltip is unavailable for this unit: {error}");
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
