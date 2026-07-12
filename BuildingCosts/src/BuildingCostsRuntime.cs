using CrusaderDE;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BuildingCosts
{
    public sealed class BuildingCostsRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BuildingCostsLobbyViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<eStructs, CostMaterialMask> overriddenCostMaterials = new Dictionary<eStructs, CostMaterialMask>();
        private bool settingsChangedSubscribed;
        private bool hooksSubscribed;
        private bool libraryInitialized;
        private bool vanillaCostTooltipReadFailureLogged;
        private Hook updateRolloverHook;
        private UpdateRolloverDelegate updateRolloverTrampoline;
        private FieldInfo hoverStructField;
        private FieldInfo selectedStructField;
        private int lastTooltipStruct = int.MinValue;
        private int lastLocalPlayerId = int.MinValue;
        private int lastResourceSignature = int.MinValue;
        private bool lastDetailedTooltipVisible;
        private bool lastCompactTooltipVisible;
        private bool tooltipIsClear = true;

        private static readonly Dictionary<eMappers, BuildingCostDefinition> BuildingCostDefinitions = CreateBuildingCostDefinitions();
        private delegate void UpdateRolloverDelegate(HUD_Main self);

        [Flags]
        private enum CostMaterialMask
        {
            None = 0,
            Wood = 1,
            Stone = 2,
            Iron = 4,
            Pitch = 8,
            Gold = 16
        }

        public BuildingCostsRuntime(ManualLogSource log, BuildingCostsLobbyViewModel settings)
        {
            this.log = log;
            this.settings = settings;
        }

        public void SubscribeHooks()
        {
            if (!settings.EnableMod)
                return;

            if (hooksSubscribed)
                return;

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap));

            try
            {
                InstallUpdateRolloverHook();
            }
            catch (Exception ex)
            {
                LogDebug("Could not install building cost tooltip hook:", ex);
            }

            hooksSubscribed = true;
            LogDebug("Building cost runtime hooks subscribed");
        }

        public void InitializeAfterLibraryLoaded()
        {
            if (libraryInitialized)
                return;

            SubscribeSettingsChanges();
            if (!settings.EnableMod)
            {
                libraryInitialized = true;
                LogDebug("Building costs disabled; runtime hooks not subscribed");
                return;
            }

            SubscribeHooks();
            InitializeVanillaCostTooltips();
            ApplyBuildingCosts();
            libraryInitialized = true;
            LogDebug("Applied initial building cost settings");
        }

        public void Dispose()
        {
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
                subscription.Dispose();

            subscriptions.Clear();
            hooksSubscribed = false;
            updateRolloverHook?.Dispose();
            updateRolloverHook = null;
            updateRolloverTrampoline = null;
            ClearBuildingCostTooltip();
            ResetTooltipCache();
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
            LogDebug("HUD_Main.UpdateRollover hook installed");
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
            LogDebug("Settings changed:", propertyName);

            if (propertyName == nameof(BuildingCostsLobbyViewModel.EnableMod))
            {
                if (settings.EnableMod)
                {
                    SubscribeHooks();
                    InitializeVanillaCostTooltips();
                    ApplyBuildingCosts();
                }
                else
                {
                    RestoreDefaultBuildingCosts();
                    UnsubscribeHooks();
                }

                return;
            }

            if (!settings.EnableMod)
                return;

            if (propertyName == nameof(BuildingCostsLobbyViewModel.BuildingCosts))
                ApplyBuildingCosts();
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            try
            {
                LogDebug("OnStartMap");
                ApplyBuildingCosts();
                ResetTooltipCache();
            }
            catch (Exception ex)
            {
                LogDebug("OnStartMap failed:", ex);
            }
        }

        private void InitializeVanillaCostTooltips()
        {
            Dictionary<eMappers, BuildingCostValues> vanillaCosts = new Dictionary<eMappers, BuildingCostValues>();

            foreach (BuildingCostsLobbyViewModel.CostEntryViewModel entry in settings.CostEntries)
            {
                if (!Enum.TryParse(entry.Key, true, out eMappers mapper) ||
                    !BuildingCostDefinitions.TryGetValue(mapper, out BuildingCostDefinition definition) ||
                    definition.Structures.Length == 0)
                {
                    continue;
                }

                eStructs structure = definition.Structures[0];
                try
                {
                    BuildingCost cost = GameBuildingManagerAPI.Instance.GetDefaultCost(structure);
                    vanillaCosts[mapper] = new BuildingCostValues(
                        cost.Wood,
                        cost.Stone,
                        cost.Iron,
                        cost.Pitch,
                        cost.Gold);
                }
                catch (Exception ex)
                {
                    if (!vanillaCostTooltipReadFailureLogged)
                    {
                        vanillaCostTooltipReadFailureLogged = true;
                        LogDebug("Could not read vanilla building costs for options tooltip:", mapper, structure, ex);
                    }
                }
            }

            settings.SetVanillaCostToolTips(vanillaCosts);
        }

        private void ApplyBuildingCosts()
        {
            Dictionary<eMappers, BuildingCostValues> parsedCosts = settings.ParseBuildingCosts();
            int changedMaterials = 0;

            foreach (KeyValuePair<eMappers, BuildingCostValues> entry in parsedCosts)
            {
                if (!BuildingCostDefinitions.TryGetValue(entry.Key, out BuildingCostDefinition definition))
                {
                    LogDebug("Building cost mapper is not supported:", entry.Key);
                    continue;
                }

                foreach (eStructs structure in definition.Structures)
                    changedMaterials += ApplyStructureCosts(structure, entry.Value);
            }

            LogDebug("Applied building cost materials:", changedMaterials);
            ResetTooltipCache();
        }

        private void RestoreDefaultBuildingCosts()
        {
            int restoredMaterials = 0;
            foreach (KeyValuePair<eStructs, CostMaterialMask> entry in overriddenCostMaterials)
            {
                eStructs structure = entry.Key;
                CostMaterialMask materials = entry.Value;
                BuildingCost cost = GameBuildingManagerAPI.Instance.GetDefaultCost(structure);

                if ((materials & CostMaterialMask.Wood) != 0)
                {
                    GameBuildingManagerAPI.Instance.SetWoodCost(structure, cost.Wood);
                    restoredMaterials++;
                }

                if ((materials & CostMaterialMask.Stone) != 0)
                {
                    GameBuildingManagerAPI.Instance.SetStoneCost(structure, cost.Stone);
                    restoredMaterials++;
                }

                if ((materials & CostMaterialMask.Iron) != 0)
                {
                    GameBuildingManagerAPI.Instance.SetIronIngotCost(structure, cost.Iron);
                    restoredMaterials++;
                }

                if ((materials & CostMaterialMask.Pitch) != 0)
                {
                    GameBuildingManagerAPI.Instance.SetRawPitchCost(structure, cost.Pitch);
                    restoredMaterials++;
                }

                if ((materials & CostMaterialMask.Gold) != 0)
                {
                    GameBuildingManagerAPI.Instance.SetGoldCost(structure, cost.Gold);
                    restoredMaterials++;
                }
            }

            overriddenCostMaterials.Clear();
            LogDebug("Restored default building cost materials:", restoredMaterials);
            ResetTooltipCache();
        }

        private int ApplyStructureCosts(eStructs structure, BuildingCostValues values)
        {
            int changed = 0;
            overriddenCostMaterials.TryGetValue(structure, out CostMaterialMask overriddenMaterials);
            CostMaterialMask restoreMaterials = CostMaterialMask.None;

            if (values.Wood == -1)
            {
                restoreMaterials |= overriddenMaterials & CostMaterialMask.Wood;
            }
            else
            {
                GameBuildingManagerAPI.Instance.SetWoodCost(structure, values.Wood);
                overriddenMaterials |= CostMaterialMask.Wood;
                changed++;
            }

            if (values.Stone == -1)
            {
                restoreMaterials |= overriddenMaterials & CostMaterialMask.Stone;
            }
            else
            {
                GameBuildingManagerAPI.Instance.SetStoneCost(structure, values.Stone);
                overriddenMaterials |= CostMaterialMask.Stone;
                changed++;
            }

            if (values.Iron == -1)
            {
                restoreMaterials |= overriddenMaterials & CostMaterialMask.Iron;
            }
            else
            {
                GameBuildingManagerAPI.Instance.SetIronIngotCost(structure, values.Iron);
                overriddenMaterials |= CostMaterialMask.Iron;
                changed++;
            }

            if (values.Pitch == -1)
            {
                restoreMaterials |= overriddenMaterials & CostMaterialMask.Pitch;
            }
            else
            {
                GameBuildingManagerAPI.Instance.SetRawPitchCost(structure, values.Pitch);
                overriddenMaterials |= CostMaterialMask.Pitch;
                changed++;
            }

            if (values.Gold == -1)
            {
                restoreMaterials |= overriddenMaterials & CostMaterialMask.Gold;
            }
            else
            {
                GameBuildingManagerAPI.Instance.SetGoldCost(structure, values.Gold);
                overriddenMaterials |= CostMaterialMask.Gold;
                changed++;
            }

            if (restoreMaterials != CostMaterialMask.None)
            {
                BuildingCost defaultCost = GameBuildingManagerAPI.Instance.GetDefaultCost(structure);

                if ((restoreMaterials & CostMaterialMask.Wood) != 0)
                {
                    GameBuildingManagerAPI.Instance.SetWoodCost(structure, defaultCost.Wood);
                    changed++;
                }

                if ((restoreMaterials & CostMaterialMask.Stone) != 0)
                {
                    GameBuildingManagerAPI.Instance.SetStoneCost(structure, defaultCost.Stone);
                    changed++;
                }

                if ((restoreMaterials & CostMaterialMask.Iron) != 0)
                {
                    GameBuildingManagerAPI.Instance.SetIronIngotCost(structure, defaultCost.Iron);
                    changed++;
                }

                if ((restoreMaterials & CostMaterialMask.Pitch) != 0)
                {
                    GameBuildingManagerAPI.Instance.SetRawPitchCost(structure, defaultCost.Pitch);
                    changed++;
                }

                if ((restoreMaterials & CostMaterialMask.Gold) != 0)
                {
                    GameBuildingManagerAPI.Instance.SetGoldCost(structure, defaultCost.Gold);
                    changed++;
                }

                overriddenMaterials &= ~restoreMaterials;
            }

            if (overriddenMaterials == CostMaterialMask.None)
                overriddenCostMaterials.Remove(structure);
            else
                overriddenCostMaterials[structure] = overriddenMaterials;

            return changed;
        }

        private void UpdateRolloverHookImpl(HUD_Main self)
        {
            updateRolloverTrampoline(self);
            UpdateBuildingCostTooltip(self);
        }

        private void UpdateBuildingCostTooltip(HUD_Main hud)
        {
            try
            {
                bool detailedTooltipVisible = MainViewModel.Instance.RolloverBuilding_TooltipVis;
                bool compactTooltipVisible = MainViewModel.Instance.RolloverBuilding_TooltipVisNot;
                int hoverStruct = (int)hoverStructField.GetValue(hud);
                int selectedStruct = (int)selectedStructField.GetValue(hud);
                int tooltipStruct = hoverStruct != 0 ? hoverStruct : selectedStruct;

                if (tooltipStruct <= 0 || (!detailedTooltipVisible && !compactTooltipVisible))
                {
                    lastTooltipStruct = tooltipStruct;
                    lastDetailedTooltipVisible = detailedTooltipVisible;
                    lastCompactTooltipVisible = compactTooltipVisible;
                    lastLocalPlayerId = int.MinValue;
                    lastResourceSignature = int.MinValue;
                    BuildingCostsPlugin.BuildingCostTooltipViewModel.SetPlacement(
                        detailedTooltipVisible,
                        compactTooltipVisible);
                    ClearBuildingCostTooltip();
                    return;
                }

                eStructs building = ResolveTooltipBuilding(tooltipStruct);
                if (building == eStructs.STRUCT_NULL)
                {
                    ClearBuildingCostTooltip();
                    return;
                }

                int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                int resourceSignature = GetTooltipResourceSignature(localPlayerId);

                if (tooltipStruct == lastTooltipStruct &&
                    detailedTooltipVisible == lastDetailedTooltipVisible &&
                    compactTooltipVisible == lastCompactTooltipVisible &&
                    localPlayerId == lastLocalPlayerId &&
                    resourceSignature == lastResourceSignature)
                {
                    return;
                }

                lastTooltipStruct = tooltipStruct;
                lastDetailedTooltipVisible = detailedTooltipVisible;
                lastCompactTooltipVisible = compactTooltipVisible;
                lastLocalPlayerId = localPlayerId;
                lastResourceSignature = resourceSignature;

                BuildingCostsPlugin.BuildingCostTooltipViewModel.SetPlacement(
                    detailedTooltipVisible,
                    compactTooltipVisible);

                List<BuildingCostTooltipEntry> entries = CreateAdditionalTooltipEntries(building, localPlayerId);
                BuildingCostsPlugin.BuildingCostTooltipViewModel.SetTooltip("", entries);
                tooltipIsClear = false;
            }
            catch (Exception ex)
            {
                LogDebug("Error updating building cost tooltip:", ex);
                ClearBuildingCostTooltip();
                ResetTooltipCache();
            }
        }

        private void ClearBuildingCostTooltip()
        {
            if (tooltipIsClear)
                return;

            BuildingCostsPlugin.BuildingCostTooltipViewModel.Clear();
            tooltipIsClear = true;
        }

        private void ResetTooltipCache()
        {
            lastTooltipStruct = int.MinValue;
            lastLocalPlayerId = int.MinValue;
            lastResourceSignature = int.MinValue;
            lastDetailedTooltipVisible = false;
            lastCompactTooltipVisible = false;
        }

        private static int GetTooltipResourceSignature(int playerId)
        {
            unchecked
            {
                int signature = 17;
                signature = (signature * 31) + GetAvailableAmount(playerId, eGoods.STORED_WOOD_PLANKS);
                signature = (signature * 31) + GetAvailableAmount(playerId, eGoods.STORED_STONE_BLOCKS);
                signature = (signature * 31) + GetAvailableAmount(playerId, eGoods.STORED_IRON_INGOTS);
                signature = (signature * 31) + GetAvailableAmount(playerId, eGoods.STORED_PITCH_RAW);
                signature = (signature * 31) + GetAvailableAmount(playerId, eGoods.STORED_GOLD);
                return signature;
            }
        }

        private static eStructs ResolveTooltipBuilding(int tooltipStruct)
        {
            if (IsWallTooltipStruct(tooltipStruct))
                return eStructs.STRUCT_NULL;

            List<eStructs> candidates = new List<eStructs>(3);
            eMappers mapper = (eMappers)tooltipStruct;
            if (BuildingCostDefinitions.TryGetValue(mapper, out BuildingCostDefinition definition) &&
                definition.Structures.Length > 0)
            {
                AddTooltipBuildingCandidate(candidates, definition.Structures[0]);
            }

            eStructs mapped = mapper.ConvertToEStructs();
            if (IsSupportedTooltipStructure(mapped))
                AddTooltipBuildingCandidate(candidates, mapped);

            if (Enum.IsDefined(typeof(eMappers), mapper) && candidates.Count == 0)
                return eStructs.STRUCT_NULL;

            eStructs direct = (eStructs)tooltipStruct;
            if (Enum.IsDefined(typeof(eStructs), direct) && IsSupportedTooltipStructure(direct))
                AddTooltipBuildingCandidate(candidates, direct);

            foreach (eStructs candidate in candidates)
            {
                if (HasAnyNativeCost(candidate))
                    return candidate;
            }

            return eStructs.STRUCT_NULL;
        }

        private static bool IsWallTooltipStruct(int tooltipStruct)
        {
            eStructs structure = (eStructs)tooltipStruct;
            if (structure == eStructs.STRUCT_WOOD_WALL ||
                structure == eStructs.STRUCT_STONE_WALL ||
                structure == eStructs.STRUCT_CRENAL_WALL)
            {
                return true;
            }

            eMappers mapper = (eMappers)tooltipStruct;
            return mapper == eMappers.MAPPER_WALL ||
                mapper == eMappers.MAPPER_CRENAL ||
                mapper == eMappers.MAPPER_WOODWALL;
        }

        private static bool IsSupportedTooltipStructure(eStructs building)
        {
            if (building == eStructs.STRUCT_NULL)
                return false;

            foreach (BuildingCostDefinition definition in BuildingCostDefinitions.Values)
            {
                foreach (eStructs structure in definition.Structures)
                {
                    if (structure == building)
                        return true;
                }
            }

            return false;
        }

        private static void AddTooltipBuildingCandidate(List<eStructs> candidates, eStructs building)
        {
            if (building == eStructs.STRUCT_NULL || candidates.Contains(building))
                return;

            candidates.Add(building);
        }

        private static bool HasAnyNativeCost(eStructs building)
        {
            try
            {
                return GameBuildingManagerAPI.Instance.GetWoodCost(building) != 0 ||
                    GameBuildingManagerAPI.Instance.GetStoneCost(building) != 0 ||
                    GameBuildingManagerAPI.Instance.GetIronIngotCost(building) != 0 ||
                    GameBuildingManagerAPI.Instance.GetRawPitchCost(building) != 0 ||
                    GameBuildingManagerAPI.Instance.GetGoldCost(building) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static List<BuildingCostTooltipEntry> CreateAdditionalTooltipEntries(eStructs building, int playerId)
        {
            List<BuildingCostTooltipEntry> entries = new List<BuildingCostTooltipEntry>(3);
            int vanillaSlotsUsed = 0;

            AddAdditionalTooltipEntry(entries, playerId, eGoods.STORED_WOOD_PLANKS, GameBuildingManagerAPI.Instance.GetWoodCost(building), ref vanillaSlotsUsed);
            AddAdditionalTooltipEntry(entries, playerId, eGoods.STORED_STONE_BLOCKS, GameBuildingManagerAPI.Instance.GetStoneCost(building), ref vanillaSlotsUsed);
            AddAdditionalTooltipEntry(entries, playerId, eGoods.STORED_IRON_INGOTS, GameBuildingManagerAPI.Instance.GetIronIngotCost(building), ref vanillaSlotsUsed);
            AddAdditionalTooltipEntry(entries, playerId, eGoods.STORED_PITCH_RAW, GameBuildingManagerAPI.Instance.GetRawPitchCost(building), ref vanillaSlotsUsed);
            AddAdditionalTooltipEntry(entries, playerId, eGoods.STORED_GOLD, GameBuildingManagerAPI.Instance.GetGoldCost(building), ref vanillaSlotsUsed);

            return entries;
        }

        private static void AddAdditionalTooltipEntry(List<BuildingCostTooltipEntry> entries, int playerId, eGoods good, int amount, ref int vanillaSlotsUsed)
        {
            if (amount == 0)
                return;

            if (vanillaSlotsUsed < 2)
            {
                vanillaSlotsUsed++;
                return;
            }

            AddTooltipEntry(entries, playerId, good, amount);
        }

        private static void AddTooltipEntry(List<BuildingCostTooltipEntry> entries, int playerId, eGoods good, int amount)
        {
            if (amount == 0)
                return;

            entries.Add(new BuildingCostTooltipEntry
            {
                AmountRequired = $"   {amount} ",
                AmountAvailable = $"({GetAvailableAmount(playerId, good)})",
                Image = GetGoodImage(good)
            });
        }

        private static int GetAvailableAmount(int playerId, eGoods good)
        {
            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            switch (good)
            {
                case eGoods.STORED_WOOD_PLANKS:
                    return state.game_type == 6 ? state.keep_storage[2] : state.resources[2];
                case eGoods.STORED_STONE_BLOCKS:
                    return state.game_type == 6 ? state.keep_storage[4] : state.resources[4];
                case eGoods.STORED_IRON_INGOTS:
                    return state.resources[6];
                case eGoods.STORED_PITCH_RAW:
                    return state.resources[7];
                case eGoods.STORED_GOLD:
                    return state.game_type == 6 ? state.keep_storage[15] : state.resources[15];
                default:
                    return GamePlayerManagerAPI.Instance.GetGoodAmount(playerId, good);
            }
        }

        private static ImageSource GetGoodImage(eGoods good)
        {
            return MainViewModel.Instance.getSmallGoodsIcon((int)good);
        }

        private static string GetMainViewModelString(string propertyName)
        {
            object viewModel = MainViewModel.Instance;
            PropertyInfo property = viewModel.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            object value = property?.GetValue(viewModel, null);
            return value?.ToString() ?? "";
        }

        internal static string GetLocalizedBuildingName(BuildingCostDefinition definition)
        {
            string translationKey = GetBuildingNameTranslationKey(definition);
            if (TryGetLocalizedGameText(translationKey, out string localizedName))
                return localizedName;

            return definition.DisplayName;
        }

        private static bool TryGetBuildMenuTranslationKey(BuildingCostDefinition definition, out string translationKey)
        {
            foreach (eStructs structure in definition.Structures)
            {
                if (BuildMenuTranslationKeys.TryGetValue(structure, out translationKey))
                    return true;
            }

            translationKey = null;
            return false;
        }

        private static readonly Dictionary<eStructs, string> BuildMenuTranslationKeys = new Dictionary<eStructs, string>
        {
            { eStructs.STRUCT_WOODCUTTERS_HUT, "TEXT_BUBBLE_HELP_TEXT_043" },
            { eStructs.STRUCT_HUNTERS_HUT, "TEXT_BUBBLE_HELP_TEXT_063" },
            { eStructs.STRUCT_OXEN_BASE, "TEXT_BUBBLE_HELP_TEXT_051" },
            { eStructs.STRUCT_QUARRY, "TEXT_BUBBLE_HELP_TEXT_042" },
            { eStructs.STRUCT_IRON_MINE, "TEXT_BUBBLE_HELP_TEXT_044" },
            { eStructs.STRUCT_PITCH_DIGGER, "TEXT_BUBBLE_HELP_TEXT_045" },
            { eStructs.STRUCT_WHEATFARM, "TEXT_BUBBLE_HELP_TEXT_059" },
            { eStructs.STRUCT_HOPSFARM, "TEXT_BUBBLE_HELP_TEXT_061" },
            { eStructs.STRUCT_APPLEFARM, "TEXT_BUBBLE_HELP_TEXT_060" },
            { eStructs.STRUCT_CATTLEFARM, "TEXT_BUBBLE_HELP_TEXT_062" },
            { eStructs.STRUCT_MILL, "TEXT_BUBBLE_HELP_TEXT_049" },
            { eStructs.STRUCT_BAKERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_057" },
            { eStructs.STRUCT_BREWERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_058" },
            { eStructs.STRUCT_HOVEL, "TEXT_BUBBLE_HELP_TEXT_040" },
            { eStructs.STRUCT_GRANARY, "TEXT_BUBBLE_HELP_TEXT_047" },
            { eStructs.STRUCT_GOODS_YARD, "TEXT_BUBBLE_HELP_TEXT_046" },
            { eStructs.STRUCT_ARMOURY, "TEXT_BUBBLE_HELP_TEXT_009" },
            { eStructs.STRUCT_TRADEPOST, "TEXT_BUBBLE_HELP_TEXT_050" },
            { eStructs.STRUCT_INN, "TEXT_BUBBLE_HELP_TEXT_065" },
            { eStructs.STRUCT_HEALER, "TEXT_BUBBLE_HELP_TEXT_066" },
            { eStructs.STRUCT_FLETCHERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_055" },
            { eStructs.STRUCT_POLETURNERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_056" },
            { eStructs.STRUCT_BLACKSMITHS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_052" },
            { eStructs.STRUCT_ARMOURERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_053" },
            { eStructs.STRUCT_TANNERS_WORKSHOP, "TEXT_BUBBLE_HELP_TEXT_054" },
            { eStructs.STRUCT_STABLES, "TEXT_BUBBLE_HELP_TEXT_012" },
            { eStructs.STRUCT_BARRACKS_WOOD, "TEXT_BUBBLE_HELP_TEXT_011" },
            { eStructs.STRUCT_BARRACKS_STONE, "TEXT_BUBBLE_HELP_TEXT_010" },
            { eStructs.STRUCT_ENGINEERS_GUILD, "TEXT_BUBBLE_HELP_TEXT_067" },
            { eStructs.STRUCT_TUNNELLERS_GUILD, "TEXT_BUBBLE_HELP_TEXT_068" },
            { eStructs.STRUCT_OIL_SMELTER, "TEXT_BUBBLE_HELP_TEXT_140" },
            { eStructs.STRUCT_WELL, "TEXT_BUBBLE_HELP_TEXT_048" },
            { eStructs.STRUCT_WATERPOT, "TEXT_IN_WATERPOT_001" },
            { eStructs.STRUCT_CHURCH1, "TEXT_BUBBLE_HELP_TEXT_102" },
            { eStructs.STRUCT_CHURCH2, "TEXT_BUBBLE_HELP_TEXT_103" },
            { eStructs.STRUCT_CHURCH3, "TEXT_BUBBLE_HELP_TEXT_104" },
            { eStructs.STRUCT_TOWER1, "TEXT_BUBBLE_HELP_TEXT_022" },
            { eStructs.STRUCT_TOWER2, "TEXT_BUBBLE_HELP_TEXT_023" },
            { eStructs.STRUCT_TOWER3, "TEXT_BUBBLE_HELP_TEXT_024" },
            { eStructs.STRUCT_TOWER4, "TEXT_BUBBLE_HELP_TEXT_025" },
            { eStructs.STRUCT_TOWER5, "TEXT_BUBBLE_HELP_TEXT_026" },
            { eStructs.STRUCT_GATE_STONE2A, "TEXT_BUBBLE_HELP_TEXT_092" },
            { eStructs.STRUCT_GATE_STONE2B, "TEXT_BUBBLE_HELP_TEXT_092" },
            { eStructs.STRUCT_GATE_STONE1A, "TEXT_BUBBLE_HELP_TEXT_030" },
            { eStructs.STRUCT_GATE_STONE1B, "TEXT_BUBBLE_HELP_TEXT_030" },
            { eStructs.STRUCT_GATE_WOOD1A, "TEXT_BUBBLE_HELP_TEXT_029" },
            { eStructs.STRUCT_GATE_WOOD1B, "TEXT_BUBBLE_HELP_TEXT_029" },
            { eStructs.STRUCT_GATE_WOOD1C, "TEXT_BUBBLE_HELP_TEXT_029" },
            { eStructs.STRUCT_GATE_WOOD1D, "TEXT_BUBBLE_HELP_TEXT_029" },
            { eStructs.STRUCT_DRAWBRIDGE, "TEXT_BUBBLE_HELP_TEXT_031" },
            { eStructs.STRUCT_KILLING_PIT, "TEXT_BUBBLE_HELP_TEXT_018" },
            { eStructs.STRUCT_BRAZIER, "TEXT_BUBBLE_HELP_TEXT_017" },
            { eStructs.STRUCT_MANGONEL, "TEXT_BUBBLE_HELP_TEXT_134" },
            { eStructs.STRUCT_BALLISTA, "TEXT_BUBBLE_HELP_TEXT_198" },
            { eStructs.STRUCT_MAYPOLE, "TEXT_BUBBLE_HELP_TEXT_073" },
            { eStructs.STRUCT_GALLOWS, "TEXT_BUBBLE_HELP_TEXT_072" },
            { eStructs.STRUCT_STOCKS, "TEXT_BUBBLE_HELP_TEXT_076" },
            { eStructs.STRUCT_GARDEN, "TEXT_BUBBLE_HELP_TEXT_105" },
            { eStructs.STRUCT_CESS_PIT, "TEXT_BUBBLE_HELP_TEXT_272" },
            { eStructs.STRUCT_BURNING_STAKE, "TEXT_BUBBLE_HELP_TEXT_273" },
            { eStructs.STRUCT_GIBBET, "TEXT_BUBBLE_HELP_TEXT_274" },
            { eStructs.STRUCT_DUNGEON, "TEXT_BUBBLE_HELP_TEXT_275" },
            { eStructs.STRUCT_RACK_STRETCHING, "TEXT_BUBBLE_HELP_TEXT_276" },
            { eStructs.STRUCT_CHOPPING_BLOCK, "TEXT_BUBBLE_HELP_TEXT_278" },
            { eStructs.STRUCT_DUNKING_STOOL, "TEXT_BUBBLE_HELP_TEXT_279" },
            { eStructs.STRUCT_DOG_CAGE, "TEXT_BUBBLE_HELP_TEXT_280" },
            { eStructs.STRUCT_STATUE, "TEXT_BUBBLE_HELP_TEXT_281" },
            { eStructs.STRUCT_SHRINE, "TEXT_BUBBLE_HELP_TEXT_282" },
            { eStructs.STRUCT_DANCING_BEAR, "TEXT_BUBBLE_HELP_TEXT_284" },
            { eStructs.STRUCT_POND, "TEXT_BUBBLE_HELP_TEXT_285" },
            { eStructs.STRUCT_OUTPOST_BEDOUIN, "TEXT_BUBBLE_HELP_TEXT_041" },
            { eStructs.STRUCT_BEDOUIN_STOCKADE, "TEXT_BUBBLE_HELP_TEXT_349" },
        };

        private static bool TryGetLocalizedGameText(string translationKey, out string localizedName)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GameTranslateAPI lookup failed: " + translationKey + " " + ex.Message);
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

        private static string GetBuildingNameTranslationKey(BuildingCostDefinition definition)
        {
            if (TryGetBuildMenuTranslationKey(definition, out string translationKey))
                return translationKey;

            switch (definition.Mapper)
            {
                case eMappers.MAPPER_HOVEL:
                    return "TEXT_IN_HOUSE_001";
                case eMappers.MAPPER_HEALER:
                    return "TEXT_IN_HEALERS_001";
                case eMappers.MAPPER_BARRACKS_WOOD:
                case eMappers.MAPPER_BARRACKS_STONE:
                    return "TEXT_IN_BARRACKS_001";
                case eMappers.MAPPER_CHURCH1:
                    return "TEXT_IN_CHURCH_001";
                case eMappers.MAPPER_CHURCH2:
                    return "TEXT_IN_CHURCH_004";
                case eMappers.MAPPER_CHURCH3:
                    return "TEXT_IN_CHURCH_005";
                case eMappers.MAPPER_TOWER1:
                case eMappers.MAPPER_TOWER2:
                case eMappers.MAPPER_TOWER3:
                case eMappers.MAPPER_TOWER4:
                case eMappers.MAPPER_TOWER5:
                    return "TEXT_IN_TOWER_001";
                case eMappers.MAPPER_MANGONEL:
                    return "TEXT_BUBBLE_HELP_TEXT_134";
                case eMappers.MAPPER_BALLISTA:
                    return "TEXT_BUBBLE_HELP_TEXT_198";
                case eMappers.MAPPER_GATE_MAIN:
                case eMappers.MAPPER_GATE_INNER:
                case eMappers.MAPPER_GATE_WOOD:
                    return "TEXT_IN_GATEHOUSE_001";
                case eMappers.MAPPER_GATE_POSTERN:
                    return "TEXT_IN_POSTERN_GATE_001";
                case eMappers.MAPPER_RACK_STRETCHING:
                    return "TEXT_IN_STRETCHING_RACK_001";
                case eMappers.MAPPER_RACK_FLOGGING:
                    return "TEXT_IN_FLOGGING_RACK_001";
                case eMappers.MAPPER_BEE_HIVE:
                    return "TEXT_IN_BEEHIVE_001";
                case eMappers.MAPPER_BRAZIER:
                    return "TEXT_BUBBLE_HELP_TEXT_017";
                case eMappers.MAPPER_OUTPOST_BEDOUIN:
                    return "TEXT_IN_OUTPOST_001";
                case eMappers.MAPPER_BEDOUIN_STOCKADE:
                    return "TEXT_IN_OUTPOST_010";
            }

            string structureName = definition.Structures[0].ToString();
            const string structurePrefix = "STRUCT_";
            if (structureName.StartsWith(structurePrefix, StringComparison.Ordinal))
                structureName = structureName.Substring(structurePrefix.Length);

            return "TEXT_IN_" + structureName + "_001";
        }

        internal static Dictionary<eMappers, BuildingCostDefinition> CreateBuildingCostDefinitions()
        {
            Dictionary<eMappers, BuildingCostDefinition> definitions = new Dictionary<eMappers, BuildingCostDefinition>();

            AddBuildingDefinition(definitions, "MAPPER_WOODSMAN", "woodcutters", new[] { "STRUCT_WOODCUTTERS_HUT" });
            AddBuildingDefinition(definitions, "MAPPER_HUNTER", "hunters", new[] { "STRUCT_HUNTERS_HUT" });
            AddBuildingDefinition(definitions, "MAPPER_OXENBASE", "ox tethers", new[] { "STRUCT_OXEN_BASE" });
            AddBuildingDefinition(definitions, "MAPPER_QUARRY", "quarries", new[] { "STRUCT_QUARRY" });
            AddBuildingDefinition(definitions, "MAPPER_IRON_MINE", "iron mines", new[] { "STRUCT_IRON_MINE" });
            AddBuildingDefinition(definitions, "MAPPER_PITCH_WORKINGS", "pitch rigs", new[] { "STRUCT_PITCH_DIGGER" });
            AddBuildingDefinition(definitions, "MAPPER_WHEATFARM", "wheat farms", new[] { "STRUCT_WHEATFARM" });
            AddBuildingDefinition(definitions, "MAPPER_HOPSFARM", "hop farms", new[] { "STRUCT_HOPSFARM" });
            AddBuildingDefinition(definitions, "MAPPER_APPLEFARM", "apple orchards", new[] { "STRUCT_APPLEFARM" });
            AddBuildingDefinition(definitions, "MAPPER_CATTLEFARM", "dairy farms", new[] { "STRUCT_CATTLEFARM" });
            AddBuildingDefinition(definitions, "MAPPER_MILL", "mills", new[] { "STRUCT_MILL" });
            AddBuildingDefinition(definitions, "MAPPER_BAKER", "bakeries", new[] { "STRUCT_BAKERS_WORKSHOP" });
            AddBuildingDefinition(definitions, "MAPPER_BREWER", "breweries", new[] { "STRUCT_BREWERS_WORKSHOP" });
            AddBuildingDefinition(definitions, "MAPPER_HOVEL", "hovels", new[] { "STRUCT_HOVEL" });
            AddBuildingDefinition(definitions, "MAPPER_GRANARY", "granaries", new[] { "STRUCT_GRANARY" });
            AddBuildingDefinition(definitions, "MAPPER_STORES", "stockpiles", new[] { "STRUCT_GOODS_YARD" });
            AddBuildingDefinition(definitions, "MAPPER_ARMOURY", "armouries", new[] { "STRUCT_ARMOURY" });
            AddBuildingDefinition(definitions, "MAPPER_TRADEPOST", "marketplaces", new[] { "STRUCT_TRADEPOST" });
            AddBuildingDefinition(definitions, "MAPPER_INN", "inns", new[] { "STRUCT_INN" });
            AddBuildingDefinition(definitions, "MAPPER_HEALER", "apothecaries", new[] { "STRUCT_HEALER" });
            AddBuildingDefinition(definitions, "MAPPER_FLETCHER", "fletchers", new[] { "STRUCT_FLETCHERS_WORKSHOP" });
            AddBuildingDefinition(definitions, "MAPPER_POLETURNER", "poleturners", new[] { "STRUCT_POLETURNERS_WORKSHOP" });
            AddBuildingDefinition(definitions, "MAPPER_BLACKSMITH", "blacksmiths", new[] { "STRUCT_BLACKSMITHS_WORKSHOP" });
            AddBuildingDefinition(definitions, "MAPPER_ARMOURER", "armourers", new[] { "STRUCT_ARMOURERS_WORKSHOP" });
            AddBuildingDefinition(definitions, "MAPPER_TANNER", "tanners", new[] { "STRUCT_TANNERS_WORKSHOP" });
            AddBuildingDefinition(definitions, "MAPPER_STABLES", "stables", new[] { "STRUCT_STABLES" });
            AddBuildingDefinition(definitions, "MAPPER_BARRACKS_WOOD", "wooden barracks", new[] { "STRUCT_BARRACKS_WOOD" });
            AddBuildingDefinition(definitions, "MAPPER_BARRACKS_STONE", "stone barracks", new[] { "STRUCT_BARRACKS_STONE" });
            AddBuildingDefinition(definitions, "MAPPER_ENGINEERS_GUILD", "engineers guilds", new[] { "STRUCT_ENGINEERS_GUILD" });
            AddBuildingDefinition(definitions, "MAPPER_TUNNELERS_GUILD", "tunnelers guilds", new[] { "STRUCT_TUNNELLERS_GUILD" });
            AddBuildingDefinition(definitions, "MAPPER_OIL_SMELTER", "oil smelters", new[] { "STRUCT_OIL_SMELTER" });
            AddBuildingDefinition(definitions, "MAPPER_WELL", "wells", new[] { "STRUCT_WELL" });
            AddBuildingDefinition(definitions, "MAPPER_WATERPOT", "water pots", new[] { "STRUCT_WATERPOT" });
            AddBuildingDefinition(definitions, "MAPPER_CHURCH1", "chapels", new[] { "STRUCT_CHURCH1" });
            AddBuildingDefinition(definitions, "MAPPER_CHURCH2", "churches", new[] { "STRUCT_CHURCH2" });
            AddBuildingDefinition(definitions, "MAPPER_CHURCH3", "cathedrals", new[] { "STRUCT_CHURCH3" });
            AddBuildingDefinition(definitions, "MAPPER_TOWER1", "lookout towers", new[] { "STRUCT_TOWER1" });
            AddBuildingDefinition(definitions, "MAPPER_TOWER2", "perimeter turrets", new[] { "STRUCT_TOWER2" });
            AddBuildingDefinition(definitions, "MAPPER_TOWER3", "defence turrets", new[] { "STRUCT_TOWER3" });
            AddBuildingDefinition(definitions, "MAPPER_TOWER4", "square towers", new[] { "STRUCT_TOWER4" });
            AddBuildingDefinition(definitions, "MAPPER_TOWER5", "round towers", new[] { "STRUCT_TOWER5" });
            AddBuildingDefinition(definitions, "MAPPER_GATE_MAIN", "large stone gatehouses",
                new[] { "STRUCT_GATE_MAIN", "STRUCT_GATE_STONE2A", "STRUCT_GATE_STONE2B" },
                new[] { "MAPPER_GATE_STONE2A", "MAPPER_GATE_STONE2B" });
            AddBuildingDefinition(definitions, "MAPPER_GATE_INNER", "small stone gatehouses",
                new[] { "STRUCT_GATE_INNER", "STRUCT_GATE_STONE1A", "STRUCT_GATE_STONE1B" },
                new[] { "MAPPER_GATE_STONE1A", "MAPPER_GATE_STONE1B" });
            AddBuildingDefinition(definitions, "MAPPER_GATE_WOOD", "wooden gatehouses",
                new[] { "STRUCT_GATE_WOOD", "STRUCT_GATE_WOOD1A", "STRUCT_GATE_WOOD1B", "STRUCT_GATE_WOOD1C", "STRUCT_GATE_WOOD1D" },
                new[] { "MAPPER_GATE_WOOD1A", "MAPPER_GATE_WOOD1B", "MAPPER_GATE_WOOD1C", "MAPPER_GATE_WOOD1D" });
            AddBuildingDefinition(definitions, "MAPPER_GATEHOUSE", "gatehouses", new[] { "STRUCT_GATEHOUSE" });
            AddBuildingDefinition(definitions, "MAPPER_GATE_POSTERN", "postern gates", new[] { "STRUCT_GATE_POSTERN" });
            AddBuildingDefinition(definitions, "MAPPER_DRAWBRIDGE", "drawbridges", new[] { "STRUCT_DRAWBRIDGE" });
            AddBuildingDefinition(definitions, "MAPPER_KILLING_PIT", "killing pits", new[] { "STRUCT_KILLING_PIT" });
            AddBuildingDefinition(definitions, "MAPPER_BRAZIER", "braziers", new[] { "STRUCT_BRAZIER" });
            AddBuildingDefinition(definitions, "MAPPER_MANGONEL", "tower mangonels", new[] { "STRUCT_MANGONEL" });
            AddBuildingDefinition(definitions, "MAPPER_BALLISTA", "tower ballistae", new[] { "STRUCT_BALLISTA" });
            AddBuildingDefinition(definitions, "MAPPER_MAYPOLE", "maypoles", new[] { "STRUCT_MAYPOLE" });
            AddBuildingDefinition(definitions, "MAPPER_GALLOWS", "gallows", new[] { "STRUCT_GALLOWS" });
            AddBuildingDefinition(definitions, "MAPPER_STOCKS", "stocks", new[] { "STRUCT_STOCKS" });
            AddBuildingDefinition(definitions, "MAPPER_GARDEN1", "gardens", new[] { "STRUCT_GARDEN" }, CreateNumberedNames("MAPPER_GARDEN", 2, 12));
            AddBuildingDefinition(definitions, "MAPPER_CESS_PIT1", "cesspits", new[] { "STRUCT_CESS_PIT" }, CreateNumberedNames("MAPPER_CESS_PIT", 2, 4));
            AddBuildingDefinition(definitions, "MAPPER_BURNING_STAKE", "burning stakes", new[] { "STRUCT_BURNING_STAKE" });
            AddBuildingDefinition(definitions, "MAPPER_GIBBET", "gibbets", new[] { "STRUCT_GIBBET" });
            AddBuildingDefinition(definitions, "MAPPER_DUNGEON", "dungeons", new[] { "STRUCT_DUNGEON" });
            AddBuildingDefinition(definitions, "MAPPER_RACK_STRETCHING", "stretching racks", new[] { "STRUCT_RACK_STRETCHING" });
            AddBuildingDefinition(definitions, "MAPPER_RACK_FLOGGING", "flogging racks", new[] { "STRUCT_RACK_FLOGGING" });
            AddBuildingDefinition(definitions, "MAPPER_CHOPPING_BLOCK", "chopping blocks", new[] { "STRUCT_CHOPPING_BLOCK" });
            AddBuildingDefinition(definitions, "MAPPER_DUNKING_STOOL", "dunking stools", new[] { "STRUCT_DUNKING_STOOL" });
            AddBuildingDefinition(definitions, "MAPPER_DOG_CAGE", "dog cages", new[] { "STRUCT_DOG_CAGE" });
            AddBuildingDefinition(definitions, "MAPPER_STATUE1", "statues", new[] { "STRUCT_STATUE" }, CreateNumberedNames("MAPPER_STATUE", 2, 5));
            AddBuildingDefinition(definitions, "MAPPER_SHRINE1", "shrines", new[] { "STRUCT_SHRINE" }, CreateNumberedNames("MAPPER_SHRINE", 2, 5));
            AddBuildingDefinition(definitions, "MAPPER_BEE_HIVE", "bee hives", new[] { "STRUCT_BEE_HIVE" });
            AddBuildingDefinition(definitions, "MAPPER_DANCING_BEAR", "dancing bears", new[] { "STRUCT_DANCING_BEAR" });
            AddBuildingDefinition(definitions, "MAPPER_POND1", "ponds", new[] { "STRUCT_POND" }, CreateNumberedNames("MAPPER_POND", 2, 4));
            AddBuildingDefinition(definitions, "MAPPER_BEAR_CAVE", "bear caves", new[] { "STRUCT_BEAR_CAVE" });
            AddBuildingDefinition(definitions, "MAPPER_OUTPOST_BEDOUIN", "Bedouin outposts", new[] { "STRUCT_OUTPOST_BEDOUIN" });
            AddBuildingDefinition(definitions, "MAPPER_BEDOUIN_STOCKADE", "Bedouin stockades", new[] { "STRUCT_BEDOUIN_STOCKADE" });

            return definitions;
        }

        private static void AddBuildingDefinition(
            Dictionary<eMappers, BuildingCostDefinition> definitions,
            string mapperName,
            string displayName,
            string[] structureNames,
            string[] aliasNames = null)
        {
            if (!Enum.TryParse(mapperName, out eMappers mapper))
                throw new InvalidOperationException("Unknown building cost mapper: " + mapperName);

            HashSet<eStructs> structures = new HashSet<eStructs>();
            foreach (string structureName in structureNames)
            {
                if (!Enum.TryParse(structureName, out eStructs structure))
                    throw new InvalidOperationException("Unknown building cost structure: " + structureName + " for " + mapperName);

                structures.Add(structure);
            }

            eStructs[] structureArray = new eStructs[structures.Count];
            structures.CopyTo(structureArray);
            BuildingCostDefinition definition = new BuildingCostDefinition(mapper, displayName, structureArray);
            definitions[mapper] = definition;
            if (aliasNames == null)
                return;

            foreach (string aliasName in aliasNames)
            {
                if (!Enum.TryParse(aliasName, out eMappers alias))
                    throw new InvalidOperationException("Unknown building cost mapper alias: " + aliasName + " for " + mapperName);

                definitions[alias] = definition;
            }
        }

        private static string[] CreateNumberedNames(string prefix, int first, int last)
        {
            string[] names = new string[last - first + 1];
            for (int number = first; number <= last; number++)
                names[number - first] = prefix + number;
            return names;
        }

        private void LogDebug(params object[] parts)
        {
            Shared.DebugLogHelper.LogDebug(log, parts);
        }
    }
}
