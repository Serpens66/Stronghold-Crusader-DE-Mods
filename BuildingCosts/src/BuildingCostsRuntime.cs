using CrusaderDE;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BuildingCosts
{
    internal sealed partial class BuildingCostsRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BuildingCostsLobbyViewModel settings;
        private Hook buildStructureHook;
        private Hook updateRolloverHook;
        private BuildStructureDelegate buildStructureTrampoline;
        private UpdateRolloverDelegate updateRolloverTrampoline;
        private FieldInfo hoverStructField;
        private FieldInfo selectedStructField;
        private bool settingsPropertyChangedSubscribed;
        private bool libraryInitialized;

        private delegate long BuildStructureDelegate(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers mv, int buildingScaleUnk, int a7, byte bIsFree);
        private delegate void UpdateRolloverDelegate(HUD_Main self);

        public BuildingCostsRuntime(ManualLogSource log, BuildingCostsLobbyViewModel settings)
        {
            this.log = log;
            this.settings = settings;
        }

        public void SubscribeHooks()
        {
            if (buildStructureHook != null)
                return;

            MethodInfo target = typeof(SHCDESE.Detours.BulkBuildingDetours).GetMethod(
                "c_game_player_build_structure_hook_impl",
                BindingFlags.Public | BindingFlags.Static);

            if (target == null)
                throw new MissingMethodException("SHCDESE.Detours.BulkBuildingDetours", "c_game_player_build_structure_hook_impl");

            buildStructureHook = new Hook(target, new BuildStructureDelegate(BuildStructureHookImpl));
            buildStructureTrampoline = buildStructureHook.GenerateTrampoline<BuildStructureDelegate>();
            log.LogInfo("Building build-structure hook installed.");

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
            log.LogInfo("HUD_Main.UpdateRollover hook installed.");
        }

        public void Dispose()
        {
            updateRolloverHook?.Dispose();
            updateRolloverHook = null;
            updateRolloverTrampoline = null;
            buildStructureHook?.Dispose();
            buildStructureHook = null;
            buildStructureTrampoline = null;
            UnsubscribeSettingsChanges();
            ClearConfiguredModdedGoodCosts();
        }

        private long BuildStructureHookImpl(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers mv, int buildingScaleUnk, int a7, byte bIsFree)
        {
            bool isFree = bIsFree != 0;
            eStructs building = mv.ConvertToEStructs();
            bool hasModdedGoodCosts = !isFree &&
                building != eStructs.STRUCT_NULL &&
                BuildingCostsAPI.HasModdedGoodCosts(building, playerId);
            bool hasMissingModdedGoodCosts = hasModdedGoodCosts &&
                building != eStructs.STRUCT_NULL &&
                !BuildingCostsAPI.HasEffectiveGoodCostsAvailable(building, playerId);

            if (hasMissingModdedGoodCosts)
            {
                log.LogDebug($"Blocked build because modded costs are missing: playerId={playerId}, mapper={mv}, building={building}");
                if (playerId == GamePlayerManagerAPI.Instance.GetLocalPlayerId() &&
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId) &&
                    BuildingCostsAPI.HasMissingNonVanillaEffectiveGoodCosts(building, playerId))
                {
                    BuildingCostsPlugin.BuildingCostMissingNotificationViewModel.ShowMissingBuildingMaterials();
                }

                return 0;
            }

            long originalResult = buildStructureTrampoline(
                pTileManager,
                playerId,
                tileX,
                tileY,
                mv,
                buildingScaleUnk,
                a7,
                hasModdedGoodCosts ? (byte)1 : bIsFree);

            if (hasModdedGoodCosts &&
                originalResult != 0 &&
                building != eStructs.STRUCT_NULL)
            {
                BuildingCostsAPI.RemoveEffectiveGoodCosts(building, playerId);
            }

            return originalResult;
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
                BuildingCostsPlugin.BuildingCostTooltipViewModel.SetPlacement(
                    MainViewModel.Instance.RolloverBuilding_TooltipVis,
                    MainViewModel.Instance.RolloverBuilding_TooltipVisNot);

                int hoverStruct = (int)hoverStructField.GetValue(hud);
                int selectedStruct = (int)selectedStructField.GetValue(hud);
                int tooltipStruct = hoverStruct != 0 ? hoverStruct : selectedStruct;
                if (tooltipStruct <= 0)
                {
                    BuildingCostsPlugin.BuildingCostTooltipViewModel.Clear();
                    return;
                }

                eStructs building = ResolveTooltipBuilding(tooltipStruct);
                if (building == eStructs.STRUCT_NULL)
                {
                    BuildingCostsPlugin.BuildingCostTooltipViewModel.Clear();
                    return;
                }

                int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                BuildingGoodCost[] costs = BuildingCostsAPI.GetAllGoodCostsForTooltip(building, localPlayerId);
                if (costs.Length == 0 || !BuildingCostsAPI.HasModdedGoodCosts(building, localPlayerId))
                {
                    BuildingCostsPlugin.BuildingCostTooltipViewModel.Clear();
                    return;
                }

                List<BuildingCostTooltipEntry> entries = new List<BuildingCostTooltipEntry>(costs.Length);
                foreach (BuildingGoodCost cost in costs)
                {
                    entries.Add(new BuildingCostTooltipEntry
                    {
                        AmountRequired = $"   {cost.Amount} ",
                        AmountAvailable = $"({GetAvailableAmount(localPlayerId, cost.Good)})",
                        Image = GetGoodImage(cost.Good)
                    });
                }

                BuildingCostsPlugin.BuildingCostTooltipViewModel.SetCosts(entries);
            }
            catch (Exception ex)
            {
                log.LogError($"Error updating building cost tooltip: {ex}");
            }
        }

        private static eStructs ResolveTooltipBuilding(int tooltipStruct)
        {
            eStructs direct = (eStructs)tooltipStruct;
            if (BuildingCostsAPI.HasModdedGoodCosts(direct))
                return direct;

            eMappers mapper = (eMappers)tooltipStruct;
            eStructs mapped = mapper.ConvertToEStructs();
            return mapped != eStructs.STRUCT_NULL ? mapped : direct;
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
    }
}
