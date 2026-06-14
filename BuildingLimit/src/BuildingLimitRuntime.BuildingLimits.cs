using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.EventAPI.Units;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace BuildingLimit
{
    public sealed partial class BuildingLimitRuntime
    {
        private void OnBuildingPlacementValidation(BuildingPlacementValidationEventArgs args)
        {
            if (GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId))
                return;

            if (!activeBuildingLimitRules.TryGetValue(args.Mappers, out BuildingLimitRule rule))
            {
                if (IsSiegeTentPlacementMapper(args.Mappers))
                    return;

                LogDebug(
                    "Building placement validation has no active limit rule:",
                    "player", args.PlayerId,
                    "mapper", args.Mappers);
                return;
            }

            if (rule.Limit < 0)
            {
                LogDebug(
                    "Building placement validation limit is unlimited:",
                    "player", args.PlayerId,
                    "mapper", args.Mappers,
                    "limit", rule.Limit);
                return;
            }

            int aliveCount = CountAliveBuildings(args.PlayerId, rule.Definition);
            if (Shared.DebugLogHelper.IsDebugEnabled())
            {
                string structures = string.Join(",", Array.ConvertAll(rule.Definition.Structures, structure => structure.ToString()));
                LogDebug(
                    "Building placement validation count:",
                    "player", args.PlayerId,
                    "mapper", args.Mappers,
                    "definition", rule.Definition.Mapper,
                    "limit", rule.Limit,
                    "count", aliveCount,
                    "structures", structures);
            }

            if (aliveCount < rule.Limit)
            {
                LogDebug(
                    "Building placement validation below limit:",
                    "player", args.PlayerId,
                    "mapper", args.Mappers,
                    "limit", rule.Limit,
                    "count", aliveCount);
                return;
            }

            args.CustomValidationRules = true;
            args.ForceBlockPlacementState = true;
            LogDebug(
                "Building placement validation blocked by limit:",
                "player", args.PlayerId,
                "mapper", args.Mappers,
                "limit", rule.Limit,
                "count", aliveCount);
            ShowBuildingLimitMessageForLocalPlayer(args.PlayerId, rule.Definition, rule.DisplayLimit);
        }

        private void ShowBuildingLimitMessageForLocalPlayer(int playerId, BuildingLimitDefinition definition, int limit)
        {
            if (!IsLocalPlayer(playerId))
            {
                LogDebug(
                    "Building limit message skipped for non-local player:",
                    definition.Mapper,
                    "player", playerId);
                return;
            }

            ShowBuildingLimitMessage(
                definition.Mapper,
                SerpLocalization.Get(SerpLocalization.Max) + " " + limit + " " + GetLocalizedBuildingName(definition));
        }

        // This method is no longer used since we switched to using the active building cache for counting alive buildings, but it's kept here for reference in case we need to revert that change or want to compare results.
        // private int CountAliveBuildings(int playerId, BuildingLimitDefinition definition)
        // {
        //     int count = 0;
        //     foreach (eStructs structure in definition.Structures)
        //     {
        //         matchingBuildingIds.Clear();
        //         GameBuildingManagerAPI.Instance.GetAllBuildings(
        //             matchingBuildingIds,
        //             AliveState.IsAlive,
        //             structure,
        //             PlayerRelationship.Self,
        //             playerId);
        //         count += matchingBuildingIds.Count;
        //     }

        //     return count;
        // }

        private int CountAliveBuildings(int playerId, BuildingLimitDefinition definition)
        {
            int count = 0;
            foreach (eStructs structure in definition.Structures)
                count += activeBuildingCache.GetActiveBuildingCount(playerId, structure);

            return count;
        }

        private void UpdateBuildingLimitTooltip(HUD_Main hud)
        {
            try
            {
                int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                if (localPlayerId <= 0 ||
                    !GamePlayerManagerAPI.Instance.IsPlayerIdValid(localPlayerId) ||
                    GamePlayerManagerAPI.Instance.IsAIPlayer(localPlayerId))
                {
                    ClearBuildingLimitTooltip();
                    return;
                }

                int hoverStruct = (int)hoverStructField.GetValue(hud);
                int selectedStruct = (int)selectedStructField.GetValue(hud);
                int tooltipStruct = hoverStruct != 0 ? hoverStruct : selectedStruct;
                if (tooltipStruct <= 0 ||
                    !TryResolveActiveBuildingLimitRule(tooltipStruct, out BuildingLimitRule rule) ||
                    rule.Limit < 0)
                {
                    ClearBuildingLimitTooltip();
                    return;
                }

                int count = GetDisplayBuildingCount(rule, CountAliveBuildings(localPlayerId, rule.Definition));
                int limit = rule.DisplayLimit;
                if (!buildingLimitTooltipIsClear &&
                    tooltipStruct == lastTooltipStruct &&
                    localPlayerId == lastTooltipLocalPlayerId &&
                    count == lastTooltipCount &&
                    limit == lastTooltipLimit)
                {
                    return;
                }

                BuildingLimitTooltip.Show(count, rule.DisplayLimit);
                lastTooltipStruct = tooltipStruct;
                lastTooltipLocalPlayerId = localPlayerId;
                lastTooltipCount = count;
                lastTooltipLimit = limit;
                buildingLimitTooltipIsClear = false;
            }
            catch (Exception ex)
            {
                LogDebug("Error updating building limit tooltip:", ex.Message);
                ClearBuildingLimitTooltip();
                ResetBuildingLimitTooltipCache();
            }
        }

        private bool TryResolveActiveBuildingLimitRule(int tooltipStruct, out BuildingLimitRule rule)
        {
            eStructs direct = (eStructs)tooltipStruct;
            if (Enum.IsDefined(typeof(eStructs), direct) &&
                TryGetActiveBuildingLimitRuleByStructure(direct, out rule))
            {
                return true;
            }

            eMappers mapper = (eMappers)tooltipStruct;
            if (activeBuildingLimitRules.TryGetValue(mapper, out rule))
                return true;

            eStructs mapped = mapper.ConvertToEStructs();
            return TryGetActiveBuildingLimitRuleByStructure(mapped, out rule);
        }

        private bool TryGetActiveBuildingLimitRuleByStructure(eStructs structure, out BuildingLimitRule rule)
        {
            if (structure == eStructs.STRUCT_NULL)
            {
                rule = null;
                return false;
            }

            return activeBuildingLimitRulesByStructure.TryGetValue(structure, out rule);
        }

        private static int GetDisplayBuildingCount(BuildingLimitRule rule, int internalCount)
        {
            if (rule.Definition.Mapper != eMappers.MAPPER_STORES)
                return internalCount;

            return (internalCount + 3) / 4;
        }

        private void ShowBuildingLimitMessage(eMappers building, string message)
        {
            LogDebug("Building limit notification shown:", building, message);
            DisplayLimitNotification(message);
        }

        private static bool IsSiegeTentPlacementMapper(eMappers mapper)
        {
            switch (mapper)
            {
                case eMappers.MAPPER_CATAPULT:
                case eMappers.MAPPER_TREBUCHET:
                case eMappers.MAPPER_BATTERING_RAM:
                case eMappers.MAPPER_SIEGE_TOWER:
                case eMappers.MAPPER_PORTABLE_SHIELD:
                    return true;
                default:
                    return false;
            }
        }

        private void DisplayLimitNotification(string message)
        {
            BuildingLimitNotification.Show(message);
            CancelBuildingLimitMessageTimer();
            buildingLimitMessageTimerHandle = GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(
                BuildingLimitMessageDurationMilliseconds,
                OnBuildingLimitMessageTimerElapsed,
                null);
        }

        private void OnBuildingLimitMessageTimerElapsed()
        {
            buildingLimitMessageTimerHandle = null;
            BuildingLimitNotification.Hide();
        }

        private void HideBuildingLimitMessage()
        {
            CancelBuildingLimitMessageTimer();
            BuildingLimitNotification.Hide();
        }

        private void CancelBuildingLimitMessageTimer()
        {
            if (string.IsNullOrEmpty(buildingLimitMessageTimerHandle))
                return;

            try
            {
                GameTimeManagerAPI.Instance.GetTimerEngine().RemoveAction(buildingLimitMessageTimerHandle);
            }
            catch (Exception ex)
            {
                LogDebug("Could not cancel building limit message timer:", ex.Message);
            }

            buildingLimitMessageTimerHandle = null;
        }

        private void ApplyBuildingLimits()
        {
            activeBuildingLimitRules.Clear();
            activeBuildingLimitRulesByStructure.Clear();
            Dictionary<eMappers, int> parsedLimits = ParseEnumAmounts<eMappers>(settings.BuildingLimits);
            foreach (KeyValuePair<eMappers, int> entry in parsedLimits)
            {
                if (!BuildingLimitDefinitions.TryGetValue(entry.Key, out BuildingLimitDefinition definition) ||
                    definition.Mapper != entry.Key)
                {
                    LogDebug("Building limit mapper is not a supported limit key:", entry.Key);
                    continue;
                }

                int internalLimit = GetInternalBuildingLimit(definition, entry.Value);
                BuildingLimitRule rule = new BuildingLimitRule(definition, internalLimit, entry.Value);
                foreach (KeyValuePair<eMappers, BuildingLimitDefinition> mappedDefinition in BuildingLimitDefinitions)
                {
                    if (mappedDefinition.Value.Mapper == definition.Mapper)
                        activeBuildingLimitRules[mappedDefinition.Key] = rule;
                }

                foreach (eStructs structure in definition.Structures)
                    activeBuildingLimitRulesByStructure[structure] = rule;

                if (entry.Value >= 0)
                {
                    if (Shared.DebugLogHelper.IsDebugEnabled())
                    {
                        string structures = string.Join(",", Array.ConvertAll(definition.Structures, structure => structure.ToString()));
                        LogDebug(
                            "Active building limit:",
                            entry.Key,
                            "=",
                            entry.Value,
                            "internal",
                            internalLimit,
                            "structures",
                            structures);
                    }
                }
            }

            LogDebug("Applied active building limit rules:", activeBuildingLimitRules.Count);
            ResetBuildingLimitTooltipCache();
        }

        private static int GetInternalBuildingLimit(BuildingLimitDefinition definition, int configuredLimit)
        {
            if (configuredLimit <= 0 || definition.Mapper != eMappers.MAPPER_STORES)
                return configuredLimit;

            return configuredLimit * 4;
        }

        internal static Dictionary<eMappers, BuildingLimitDefinition> CreateBuildingLimitDefinitions()
        {
            Dictionary<eMappers, BuildingLimitDefinition> definitions = new Dictionary<eMappers, BuildingLimitDefinition>();

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
            Dictionary<eMappers, BuildingLimitDefinition> definitions,
            string mapperName,
            string displayName,
            string[] structureNames,
            string[] aliasNames = null)
        {
            if (!Enum.TryParse(mapperName, out eMappers mapper))
                throw new InvalidOperationException("Unknown building limit mapper: " + mapperName);

            HashSet<eStructs> structures = new HashSet<eStructs>();
            foreach (string structureName in structureNames)
            {
                if (!Enum.TryParse(structureName, out eStructs structure))
                    throw new InvalidOperationException("Unknown building limit structure: " + structureName + " for " + mapperName);

                structures.Add(structure);
            }

            if (structures.Count == 0)
                throw new InvalidOperationException("Building limit definition has no structures: " + mapperName);

            eStructs[] structureArray = new eStructs[structures.Count];
            structures.CopyTo(structureArray);
            BuildingLimitDefinition definition = new BuildingLimitDefinition(mapper, displayName, structureArray);
            definitions[mapper] = definition;
            if (aliasNames == null)
                return;

            foreach (string aliasName in aliasNames)
            {
                if (!Enum.TryParse(aliasName, out eMappers alias))
                    throw new InvalidOperationException("Unknown building limit mapper alias: " + aliasName + " for " + mapperName);

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
    }
}

