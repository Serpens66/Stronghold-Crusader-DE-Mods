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

            MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap);

            MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap);

            UnitR3EventHooks.OnUnitTransition.Observable
                .Subscribe(OnUnitTransition);

            BuildingR3EventHooks.OnPlacementValidation.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildingPlacementValidation);

            BuildingR3EventHooks.OnBuildingSpawn.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnBuildingSpawn);

            makeTroopGameActionHook = new MakeTroopGameActionHook(log, ShouldBlockMakeTroopGameAction);
            createTroopHoverHook = new CreateTroopHoverHook(log, UpdateRecruitmentCostTooltip, ClearRecruitmentCostTooltip);
            siegeBuildHoverHook = new SiegeBuildHoverHook(log, UpdateSiegeBuildCostTooltip, ClearRecruitmentCostTooltip);

            hooksSubscribed = true;
            log.LogInfo("UnitCosts runtime hooks subscribed");
        }

        public void Dispose()
        {
            if (settingsChangedSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsChangedSubscribed = false;
            }

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
            log.LogInfo("UnitCosts settings changed: " + propertyName);

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
                log.LogInfo("UnitCosts OnStartMap failed: " + ex);
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

            log.LogInfo("Applied unit cost values: " + changedValues);
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

            log.LogInfo("Applied human extra unit cost rows: " + configuredUnits);
        }

        private bool ShouldBlockMakeTroopGameAction(int amount, eChimps unitType, int rawUnitType)
        {
            try
            {
                return ShouldBlockLocalHumanRecruitment(amount, unitType, rawUnitType);
            }
            catch (Exception ex)
            {
                log.LogInfo("UnitCosts local recruitment cost check failed: " + ex.Message);
                return false;
            }
        }

        private bool ShouldBlockLocalHumanRecruitment(int amount, eChimps unitType, int rawUnitType)
        {
            if (amount <= 0)
                return false;

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0)
                return false;

            if (!TryGetHumanExtraCosts(unitType, out UnitExtraCostValues costs))
                return false;

            if (HasEnoughExtraCosts(playerId, costs, amount, out eGoods missingGood, out int requiredAmount, out int availableAmount))
                return false;

            LogInfo(
                "UnitCosts blocked recruitment:",
                "unit", unitType,
                "player", playerId,
                "missing", missingGood,
                "required", requiredAmount,
                "available", availableAmount,
                "amount", amount,
                "rawUnitType", rawUnitType);
            ShowMissingResourcesMessage();
            return true;
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
                    LogInfo(
                        "UnitCosts transition extra cost missing:",
                        "unit", args.NextUnitType,
                        "player", playerId,
                        "missing", missingGood,
                        "required", requiredAmount,
                        "available", availableAmount);
                    return;
                }

                ApplyExtraCosts(playerId, costs, 1);
                log.LogInfo("UnitCosts applied human extra costs: " + args.NextUnitType + " player " + playerId);
            }
            catch (Exception ex)
            {
                log.LogInfo("UnitCosts OnUnitTransition failed: " + ex.Message);
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
                LogInfo(
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
                log.LogInfo("UnitCosts siege placement validation failed: " + ex.Message);
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
                    LogInfo(
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
                LogInfo(
                    "UnitCosts applied siege extra costs:",
                    "unit", unitType,
                    "player", args.PlayerId,
                    "building", args.Building);
            }
            catch (Exception ex)
            {
                log.LogInfo("UnitCosts OnBuildingSpawn failed: " + ex.Message);
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

        private void LogInfo(params object[] parts)
        {
            log.LogInfo(string.Join(" ", parts));
        }

        private void ShowMissingResourcesMessage()
        {
            PlayWeaponsNeededSpeech();
            DisplayMaterialNotification(IsGermanLanguage() ? "Material fehlt" : "Resources missing");
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
                DisplayMaterialNotification(IsGermanLanguage() ? "Material fehlt" : "Resources missing");
            }
        }

        private void PlayWeaponsNeededSpeech()
        {
            try
            {
                LogInfo("UnitCosts missing resources speech:", MissingResourcesSpeechFileName);

                SFXManager.instance?.playSpeech(
                    1,
                    MissingResourcesSpeechFileName,
                    1f);
            }
            catch (Exception ex)
            {
                LogInfo("Could not play UnitCosts missing resources speech:", ex.Message);
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
                LogInfo("Could not cancel UnitCosts material message timer:", ex.Message);
            }

            materialMessageTimerHandle = null;
        }

        private static eChimps GetLastTroopBuildChimp(MainViewModel mainViewModel)
        {
            object value = GetMainViewModelMemberValue(mainViewModel, "lastTroopBuildChimp");
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
            object value = GetMainViewModelMemberValue(mainViewModel, "lastTroopsAmountToMake");
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

        private static object GetMainViewModelMemberValue(MainViewModel mainViewModel, string memberName)
        {
            if (mainViewModel == null)
                return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = typeof(MainViewModel).GetField(memberName, flags);
            if (field != null)
                return field.GetValue(mainViewModel);

            PropertyInfo property = typeof(MainViewModel).GetProperty(memberName, flags);
            return property?.GetValue(mainViewModel);
        }

        private static Noesis.ImageSource GetGoodImage(eGoods good)
        {
            return MainViewModel.Instance.getSmallGoodsIcon((int)good);
        }

        private static bool IsGermanLanguage()
        {
            string language = GameAssetManagerAPI.Instance.CurrentLanguage;
            return !string.IsNullOrEmpty(language) &&
                language.StartsWith("de", StringComparison.OrdinalIgnoreCase);
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
            {
                if (!VanillaGoldCosts.ContainsKey(unitType))
                    VanillaGoldCosts[unitType] = GetCurrentUnitGoldCost(unitType);
            }
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
            int goldCost = GetVanillaGoldCost(unitType);
            builder.AppendLine();
            builder.Append("vanilla gold: ");
            builder.Append(goldCost);
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
