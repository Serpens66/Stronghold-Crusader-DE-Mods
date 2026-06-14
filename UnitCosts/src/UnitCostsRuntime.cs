using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Extensions;
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
        private string materialMessageTimerHandle;
        private DateTime nextSiegeMissingResourcesMessageUtc = DateTime.MinValue;
        private DateTime nextSiegeMissingResourcesSpeechUtc = DateTime.MinValue;
        private bool settingsChangedSubscribed;
        private const string GoodsTextSection = "TEXT_GOODS";
        private bool hooksSubscribed;
        private const int MaterialMessageDurationMilliseconds = 3000;
        private const int SiegeMissingResourcesMessageThrottleMilliseconds = 1000;
        private const int SiegeMissingResourcesSpeechThrottleMilliseconds = 10000;
        private const string MissingResourcesSpeechFileName = "Other_Warning6.wav";
        private const BindingFlags MainViewModelFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo LastTroopBuildChimpField = typeof(MainViewModel).GetField("lastTroopBuildChimp", MainViewModelFlags);
        private static readonly PropertyInfo LastTroopBuildChimpProperty = typeof(MainViewModel).GetProperty("lastTroopBuildChimp", MainViewModelFlags);
        private static readonly FieldInfo LastTroopsAmountToMakeField = typeof(MainViewModel).GetField("lastTroopsAmountToMake", MainViewModelFlags);
        private static readonly PropertyInfo LastTroopsAmountToMakeProperty = typeof(MainViewModel).GetProperty("lastTroopsAmountToMake", MainViewModelFlags);
        private static readonly Dictionary<eChimps, UnitGoodCosts> VanillaEuropeanGoodCosts = new Dictionary<eChimps, UnitGoodCosts>();
        private static readonly Dictionary<eChimps, int> VanillaGoldCosts = new Dictionary<eChimps, int>();

        public UnitCostsNotificationViewModel Notification { get; } = new UnitCostsNotificationViewModel();
        public UnitRecruitmentCostTooltipViewModel RecruitmentCostTooltip { get; } = new UnitRecruitmentCostTooltipViewModel();

        public UnitCostsRuntime(ManualLogSource log, UnitCostsLobbyViewModel settings)
        {
            this.log = log;
            this.settings = settings;
        }

        public void InitializeAfterLibraryLoaded()
        {
            SubscribeHooks();
            SubscribeSettingsChanges();
            CaptureVanillaGoldCosts();
            CaptureVanillaEuropeanGoodCosts();
            ApplyUnitCosts();
            settings.NormalizeExtraCostsAfterNativeGoldChange();
            ApplyHumanExtraUnitCosts();
        }

        private void SubscribeHooks()
        {
            if (hooksSubscribed)
                return;

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap));

            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap));

            subscriptions.Add(UnitR3EventHooks.OnUnitTransition.Observable
                .Subscribe(OnUnitTransition));

            subscriptions.Add(BuildingR3EventHooks.OnPlacementValidation.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildingPlacementValidation));

            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnBuildingSpawn));

            makeTroopGameActionHook = new MakeTroopGameActionHook(log, DecideMakeTroopGameAction);
            createTroopHoverHook = new CreateTroopHoverHook(log, UpdateRecruitmentCostTooltip, ClearRecruitmentCostTooltip);
            siegeBuildHoverHook = new SiegeBuildHoverHook(log, UpdateSiegeBuildCostTooltip, ClearRecruitmentCostTooltip);

            hooksSubscribed = true;
            log.LogDebug("UnitCosts runtime hooks subscribed");
        }

        public void Dispose()
        {
            if (settingsChangedSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsChangedSubscribed = false;
            }

            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            hooksSubscribed = false;
            makeTroopGameActionHook?.Dispose();
            makeTroopGameActionHook = null;
            createTroopHoverHook?.Dispose();
            createTroopHoverHook = null;
            siegeBuildHoverHook?.Dispose();
            siegeBuildHoverHook = null;
            HideMaterialMessage();
        }

        private void SubscribeSettingsChanges()
        {
            if (settingsChangedSubscribed)
                return;

            settings.SettingChanged += OnSettingChanged;
            settingsChangedSubscribed = true;
        }

        private void OnSettingChanged(string propertyName)
        {
            Shared.DebugLogHelper.LogDebug(log, "UnitCosts settings changed:", propertyName);

            if (propertyName == nameof(UnitCostsLobbyViewModel.UnitCosts))
            {
                ApplyUnitCosts();
                settings.NormalizeExtraCostsAfterNativeGoldChange();
                ApplyHumanExtraUnitCosts();
            }

            if (propertyName == nameof(UnitCostsLobbyViewModel.HumanExtraUnitCosts))
                ApplyHumanExtraUnitCosts();
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            try
            {
                ApplyUnitCosts();
                settings.NormalizeExtraCostsAfterNativeGoldChange();
                ApplyHumanExtraUnitCosts();
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

        private void ApplyUnitCosts()
        {
            CaptureVanillaEuropeanGoodCosts();

            Dictionary<eChimps, UnitCostValues> parsedCosts = settings.ParseUnitCosts();
            int changedValues = 0;
            foreach (KeyValuePair<eChimps, UnitCostValues> entry in parsedCosts)
            {
                UnitCostValues values = entry.Value;
                if (values.Gold != -1)
                {
                    GameUnitManagerAPI.Instance.SetUnitGoldCost(entry.Key, values.Gold);
                    if (TryGetSiegeTentStructure(entry.Key, out eStructs siegeTentStructure))
                        GameBuildingManagerAPI.Instance.SetGoldCost(siegeTentStructure, values.Gold);
                    changedValues++;
                }

                if (!IsEuropeanRecruit(entry.Key))
                    continue;

                UnitGoodCosts mergedCosts = MergeWithVanillaGoodCosts(entry.Key, values);
                GameUnitManagerAPI.Instance.SetUnitGoodCosts(entry.Key, mergedCosts);
                changedValues += CountConfiguredGoodSlots(values);
            }

            Shared.DebugLogHelper.LogDebug(log, "Applied unit cost values:", changedValues);
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
        }

        private MakeTroopGameActionDecision DecideMakeTroopGameAction(int amount, eChimps unitType, int rawUnitType)
        {
            try
            {
                return DecideLocalHumanRecruitment(amount, unitType, rawUnitType);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(log, "UnitCosts local recruitment cost check failed:", ex.Message);
                return MakeTroopGameActionDecision.AllowOriginal();
            }
        }

        private MakeTroopGameActionDecision DecideLocalHumanRecruitment(int amount, eChimps unitType, int rawUnitType)
        {
            if (amount <= 0)
                return MakeTroopGameActionDecision.AllowOriginal();

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0)
                return MakeTroopGameActionDecision.AllowOriginal();

            bool hasExtraCosts = TryGetHumanExtraCosts(unitType, out UnitExtraCostValues costs);
            int affordableAmount = amount;
            GetMaxAffordableNativeRecruitAmount(
                playerId,
                unitType,
                out int nativeAffordableAmount,
                out string nativeLimitingReason,
                out eGoods nativeLimitingGood,
                out int nativeLimitingRequiredPerUnit,
                out int nativeLimitingAvailableAmount,
                out int readyPeasants);

            if (nativeAffordableAmount < affordableAmount)
                affordableAmount = nativeAffordableAmount;

            int extraAffordableAmount = -1;
            eGoods extraLimitingGood = eGoods.STORED_NULL;
            int extraLimitingRequiredPerUnit = 0;
            int extraLimitingAvailableAmount = 0;
            bool hasPositiveExtraCost = hasExtraCosts &&
                TryGetMaxAffordableExtraCostAmount(
                playerId,
                costs,
                out extraAffordableAmount,
                out extraLimitingGood,
                out extraLimitingRequiredPerUnit,
                out extraLimitingAvailableAmount);

            if (hasPositiveExtraCost && extraAffordableAmount < affordableAmount)
                affordableAmount = extraAffordableAmount;

            if (affordableAmount >= amount)
                return MakeTroopGameActionDecision.AllowOriginal();

            if (affordableAmount > 0)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "UnitCosts reduced recruitment amount:",
                    "unit", unitType,
                    "player", playerId,
                    "requestedAmount", amount,
                    "allowedAmount", affordableAmount,
                    "nativeAffordable", nativeAffordableAmount,
                    "nativeLimitingReason", nativeLimitingReason,
                    "nativeLimitingGood", nativeLimitingGood,
                    "nativeRequiredPerUnit", nativeLimitingRequiredPerUnit,
                    "nativeAvailable", nativeLimitingAvailableAmount,
                    "readyPeasants", readyPeasants,
                    "extraAffordable", hasPositiveExtraCost ? extraAffordableAmount : -1,
                    "extraLimitingGood", hasPositiveExtraCost ? extraLimitingGood : eGoods.STORED_NULL,
                    "extraRequiredPerUnit", hasPositiveExtraCost ? extraLimitingRequiredPerUnit : 0,
                    "extraAvailable", hasPositiveExtraCost ? extraLimitingAvailableAmount : 0,
                    "rawUnitType", rawUnitType);
                return MakeTroopGameActionDecision.ForwardAmount(affordableAmount);
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "UnitCosts blocked recruitment:",
                "unit", unitType,
                "player", playerId,
                "requestedAmount", amount,
                "allowedAmount", affordableAmount,
                "nativeAffordable", nativeAffordableAmount,
                "nativeLimitingReason", nativeLimitingReason,
                "nativeLimitingGood", nativeLimitingGood,
                "nativeRequiredPerUnit", nativeLimitingRequiredPerUnit,
                "nativeAvailable", nativeLimitingAvailableAmount,
                "readyPeasants", readyPeasants,
                "extraAffordable", hasPositiveExtraCost ? extraAffordableAmount : -1,
                "extraLimitingGood", hasPositiveExtraCost ? extraLimitingGood : eGoods.STORED_NULL,
                "extraRequiredPerUnit", hasPositiveExtraCost ? extraLimitingRequiredPerUnit : 0,
                "extraAvailable", hasPositiveExtraCost ? extraLimitingAvailableAmount : 0,
                "rawUnitType", rawUnitType);
            ShowMissingResourcesMessage();
            return MakeTroopGameActionDecision.BlockAction();
        }

        private void UpdateRecruitmentCostTooltip(MainViewModel mainViewModel)
        {
            if (mainViewModel == null)
            {
                RecruitmentCostTooltip.Clear();
                return;
            }

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0)
            {
                RecruitmentCostTooltip.Clear();
                return;
            }

            eChimps unitType = GetLastTroopBuildChimp(mainViewModel);
            int multiplier = GetLastTroopsAmountToMake(mainViewModel);

            if (!TryGetHumanExtraCosts(unitType, out UnitExtraCostValues costs))
            {
                RecruitmentCostTooltip.Clear();
                return;
            }

            RecruitmentCostTooltip.SetCosts(CreateRecruitmentCostEntries(costs, multiplier));
        }

        private void UpdateSiegeBuildCostTooltip(object parameter)
        {
            if (!TryGetSiegeBuildHoverUnit(parameter, out eChimps unitType))
            {
                RecruitmentCostTooltip.Clear();
                return;
            }

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0)
            {
                RecruitmentCostTooltip.Clear();
                return;
            }

            if (!TryGetHumanExtraCosts(unitType, out UnitExtraCostValues costs))
            {
                RecruitmentCostTooltip.Clear();
                return;
            }

            RecruitmentCostTooltip.SetCosts(CreateRecruitmentCostEntries(costs, 1));
        }

        private List<UnitRecruitmentCostEntry> CreateRecruitmentCostEntries(UnitExtraCostValues costs, int multiplier)
        {
            List<UnitRecruitmentCostEntry> entries = new List<UnitRecruitmentCostEntry>();
            foreach (KeyValuePair<eGoods, int> entry in costs.Costs)
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

                entries.Add(new UnitRecruitmentCostEntry
                {
                    Amount = "   " + amount + " ",
                    Image = GetGoodImage(entry.Key)
                });
            }

            return entries;
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
            RecruitmentCostTooltip.Clear();
        }

        private void OnUnitTransition(UnitTransitionEventArgs args)
        {
            try
            {
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
                ShowMissingResourcesMessageThrottledForSiege();
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
                        ShowMissingResourcesMessageThrottledForSiege();
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

        private static void GetMaxAffordableNativeRecruitAmount(
            int playerId,
            eChimps unitType,
            out int affordableAmount,
            out string limitingReason,
            out eGoods limitingGood,
            out int limitingRequiredPerUnit,
            out int limitingAvailableAmount,
            out int readyPeasants)
        {
            affordableAmount = int.MaxValue;
            limitingReason = "none";
            limitingGood = eGoods.STORED_NULL;
            limitingRequiredPerUnit = 0;
            limitingAvailableAmount = 0;
            readyPeasants = -1;

            if (ConsumesPeasant(unitType) && TryGetReadyPeasantCount(playerId, out readyPeasants))
            {
                CapAffordableAmount(
                    readyPeasants,
                    "peasants",
                    eGoods.STORED_NULL,
                    1,
                    readyPeasants,
                    ref affordableAmount,
                    ref limitingReason,
                    ref limitingGood,
                    ref limitingRequiredPerUnit,
                    ref limitingAvailableAmount);
            }

            int goldCost = GetCurrentUnitGoldCost(unitType);
            if (goldCost > 0)
            {
                int availableGold = Math.Max(0, GamePlayerManagerAPI.Instance.GetGoodAmount(playerId, eGoods.STORED_GOLD));
                CapAffordableAmount(
                    availableGold / goldCost,
                    "gold",
                    eGoods.STORED_GOLD,
                    goldCost,
                    availableGold,
                    ref affordableAmount,
                    ref limitingReason,
                    ref limitingGood,
                    ref limitingRequiredPerUnit,
                    ref limitingAvailableAmount);
            }

            if (!IsEuropeanRecruit(unitType))
                return;

            UnitGoodCosts goodCosts = GameUnitManagerAPI.Instance.GetUnitGoodCosts(unitType);
            Dictionary<eGoods, int> requiredGoods = new Dictionary<eGoods, int>();
            AddNativeGoodRequirement(requiredGoods, goodCosts.cost1);
            AddNativeGoodRequirement(requiredGoods, goodCosts.cost2);
            AddNativeGoodRequirement(requiredGoods, goodCosts.cost3);
            AddNativeGoodRequirement(requiredGoods, goodCosts.cost4);

            foreach (KeyValuePair<eGoods, int> entry in requiredGoods)
            {
                int available = Math.Max(0, GamePlayerManagerAPI.Instance.GetGoodAmount(playerId, entry.Key));
                CapAffordableAmount(
                    available / entry.Value,
                    "goods",
                    entry.Key,
                    entry.Value,
                    available,
                    ref affordableAmount,
                    ref limitingReason,
                    ref limitingGood,
                    ref limitingRequiredPerUnit,
                    ref limitingAvailableAmount);
            }
        }

        private static void AddNativeGoodRequirement(Dictionary<eGoods, int> requiredGoods, eGoods32 good32)
        {
            eGoods good = good32.To16();
            if (good == eGoods.STORED_NULL || good == eGoods._SE_REQUIRE_HORSE)
                return;

            if (requiredGoods.TryGetValue(good, out int requiredPerUnit))
                requiredGoods[good] = requiredPerUnit + 1;
            else
                requiredGoods[good] = 1;
        }

        private static void CapAffordableAmount(
            int candidateAmount,
            string candidateReason,
            eGoods candidateGood,
            int candidateRequiredPerUnit,
            int candidateAvailableAmount,
            ref int affordableAmount,
            ref string limitingReason,
            ref eGoods limitingGood,
            ref int limitingRequiredPerUnit,
            ref int limitingAvailableAmount)
        {
            if (candidateAmount >= affordableAmount)
                return;

            affordableAmount = Math.Max(0, candidateAmount);
            limitingReason = candidateReason;
            limitingGood = candidateGood;
            limitingRequiredPerUnit = candidateRequiredPerUnit;
            limitingAvailableAmount = candidateAvailableAmount;
        }

        private static bool ConsumesPeasant(eChimps unitType)
        {
            return !TryGetSiegeTentStructure(unitType, out _);
        }

        private static bool TryGetReadyPeasantCount(int playerId, out int readyPeasants)
        {
            readyPeasants = 0;
            unsafe
            {
                if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) ||
                    resources == null)
                {
                    return false;
                }

                uint readyPeasantValue = resources->r_ReadyPeasants;
                readyPeasants = readyPeasantValue > (uint)int.MaxValue ? int.MaxValue : (int)readyPeasantValue;
                return true;
            }
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

        private void ShowMissingResourcesMessage()
        {
            PlayWeaponsNeededSpeech();
            DisplayMaterialNotification(SerpLocalization.Get(SerpLocalization.ResourcesMissing));
        }

        private void ShowMissingResourcesMessageThrottledForSiege()
        {
            DateTime now = DateTime.UtcNow;
            if (now >= nextSiegeMissingResourcesSpeechUtc)
            {
                nextSiegeMissingResourcesSpeechUtc = now.AddMilliseconds(SiegeMissingResourcesSpeechThrottleMilliseconds);
                PlayWeaponsNeededSpeech();
            }

            if (now >= nextSiegeMissingResourcesMessageUtc)
            {
                nextSiegeMissingResourcesMessageUtc = now.AddMilliseconds(SiegeMissingResourcesMessageThrottleMilliseconds);
                DisplayMaterialNotification(SerpLocalization.Get(SerpLocalization.ResourcesMissing));
            }
        }

        private void PlayWeaponsNeededSpeech()
        {
            try
            {
                LogDebug("UnitCosts missing resources speech:", MissingResourcesSpeechFileName);

                SFXManager.instance?.playSpeech(
                    1,
                    MissingResourcesSpeechFileName,
                    1f);
            }
            catch (Exception ex)
            {
                LogDebug("Could not play UnitCosts missing resources speech:", ex.Message);
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

        private static void CaptureVanillaEuropeanGoodCosts()
        {
            foreach (eChimps unitType in GetEuropeanRecruitTypes())
            {
                if (!VanillaEuropeanGoodCosts.ContainsKey(unitType))
                    VanillaEuropeanGoodCosts[unitType] = GameUnitManagerAPI.Instance.GetUnitGoodCosts(unitType);
            }
        }

        private static void CaptureVanillaGoldCosts()
        {
            foreach (eChimps unitType in GetRecruitTypes())
                VanillaGoldCosts[unitType] = GetCurrentUnitGoldCost(unitType);
        }

        private static UnitGoodCosts MergeWithVanillaGoodCosts(eChimps unitType, UnitCostValues values)
        {
            UnitGoodCosts vanilla = GetVanillaEuropeanGoodCosts(unitType);
            return new UnitGoodCosts(
                ResolveGoodSlot(values.Slot1, vanilla.cost1),
                ResolveGoodSlot(values.Slot2, vanilla.cost2),
                ResolveGoodSlot(values.Slot3, vanilla.cost3),
                ResolveGoodSlot(values.Slot4, vanilla.cost4));
        }

        private static eGoods32 ResolveGoodSlot(string key, eGoods32 vanillaValue)
        {
            string normalizedKey = UnitCostValues.NormalizeSlotKey(key);
            if (normalizedKey == UnitCostValues.UnchangedKey)
                return vanillaValue;

            if (Enum.TryParse(normalizedKey, out eGoods good))
                return good.To32();

            return vanillaValue;
        }

        private static UnitGoodCosts GetVanillaEuropeanGoodCosts(eChimps unitType)
        {
            if (VanillaEuropeanGoodCosts.TryGetValue(unitType, out UnitGoodCosts costs))
                return costs;

            if (!IsEuropeanRecruit(unitType))
                return new UnitGoodCosts(
                    eGoods.STORED_NULL.To32(),
                    eGoods.STORED_NULL.To32(),
                    eGoods.STORED_NULL.To32(),
                    eGoods.STORED_NULL.To32());

            costs = GameUnitManagerAPI.Instance.GetUnitGoodCosts(unitType);
            VanillaEuropeanGoodCosts[unitType] = costs;
            return costs;
        }

        private static int CountConfiguredGoodSlots(UnitCostValues values)
        {
            int count = 0;
            if (values.Slot1 != UnitCostValues.UnchangedKey) count++;
            if (values.Slot2 != UnitCostValues.UnchangedKey) count++;
            if (values.Slot3 != UnitCostValues.UnchangedKey) count++;
            if (values.Slot4 != UnitCostValues.UnchangedKey) count++;
            return count;
        }

        internal static bool IsEuropeanRecruit(eChimps unitType)
        {
            return unitType >= eChimps.CHIMP_TYPE_ARCHER &&
                unitType <= eChimps.CHIMP_TYPE_KNIGHT;
        }

        private static IEnumerable<eChimps> GetEuropeanRecruitTypes()
        {
            yield return eChimps.CHIMP_TYPE_ARCHER;
            yield return eChimps.CHIMP_TYPE_XBOWMAN;
            yield return eChimps.CHIMP_TYPE_SPEARMAN;
            yield return eChimps.CHIMP_TYPE_PIKEMAN;
            yield return eChimps.CHIMP_TYPE_MACEMAN;
            yield return eChimps.CHIMP_TYPE_SWORDSMAN;
            yield return eChimps.CHIMP_TYPE_KNIGHT;
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

        internal static int GetCurrentUnitGoldCost(eChimps unitType)
        {
            try
            {
                if (TryGetSiegeTentStructure(unitType, out eStructs siegeTentStructure))
                    return Math.Max(0, GameBuildingManagerAPI.Instance.GetGoldCost(siegeTentStructure));

                return Math.Max(0, GameUnitManagerAPI.Instance.GetUnitGoldCost(unitType));
            }
            catch
            {
                return 0;
            }
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

            if (!IsEuropeanRecruit(unitType))
                return builder.ToString();

            try
            {
                UnitGoodCosts costs = GetVanillaEuropeanGoodCosts(unitType);
                AppendVanillaGoodCost(builder, 1, costs.cost1);
                AppendVanillaGoodCost(builder, 2, costs.cost2);
                AppendVanillaGoodCost(builder, 3, costs.cost3);
                AppendVanillaGoodCost(builder, 4, costs.cost4);
            }
            catch
            {
                return builder.ToString();
            }

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

            goldCost = GetCurrentUnitGoldCost(unitType);
            VanillaGoldCosts[unitType] = goldCost;
            return goldCost;
        }

        private static void AppendVanillaGoodCost(StringBuilder builder, int slot, eGoods32 good32)
        {
            eGoods good = good32.To16();
            if (good == eGoods.STORED_NULL)
                return;

            builder.AppendLine();
            builder.Append("vanilla slot ");
            builder.Append(slot);
            builder.Append(": ");
            builder.Append(good);
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
