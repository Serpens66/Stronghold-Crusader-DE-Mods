using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace BuildingCosts
{
    internal sealed partial class BuildingCostsRuntime
    {
        private readonly HashSet<ConfiguredModdedGoodCostKey> configuredModdedGoodCosts = new HashSet<ConfiguredModdedGoodCostKey>();

        public void InitializeAfterLibraryLoaded()
        {
            if (libraryInitialized)
                return;

            SubscribeSettingsChanges();
            SubscribeMapReloadCostApplication();
            ApplyConfiguredModdedBuildingCosts();
            libraryInitialized = true;
        }

        private void SubscribeSettingsChanges()
        {
            if (settingsPropertyChangedSubscribed)
                return;

            settings.SettingChanged += OnSettingChanged;
            settingsPropertyChangedSubscribed = true;
        }

        private void UnsubscribeSettingsChanges()
        {
            if (!settingsPropertyChangedSubscribed)
                return;

            settings.SettingChanged -= OnSettingChanged;
            settingsPropertyChangedSubscribed = false;
        }

        private void SubscribeMapReloadCostApplication()
        {
            MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => ApplyConfiguredModdedBuildingCosts());

            MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => ApplyConfiguredModdedBuildingCosts());
        }

        private void OnSettingChanged(string propertyName)
        {
            log.LogInfo("BuildingCosts settings changed: " + propertyName);

            if (propertyName == nameof(BuildingCostsLobbyViewModel.BuildingCosts))
                ApplyConfiguredModdedBuildingCosts();
        }

        private void ApplyConfiguredModdedBuildingCosts()
        {
            ClearConfiguredModdedGoodCosts();

            Dictionary<string, BuildingCostsLobbyViewModel.BuildingCostValues> parsedCosts =
                BuildingCostsLobbyViewModel.ParseSerializedCosts(settings.BuildingCosts);

            foreach (KeyValuePair<string, BuildingCostsLobbyViewModel.BuildingCostValues> entry in parsedCosts)
            {
                if (!Enum.TryParse(entry.Key, out eMappers mapper))
                {
                    log.LogWarning("BuildingCosts setting uses unknown mapper: " + entry.Key);
                    continue;
                }

                eStructs building = ResolveConfiguredBuilding(mapper);
                if (building == eStructs.STRUCT_NULL)
                {
                    log.LogWarning("BuildingCosts setting cannot resolve mapper to building: " + mapper);
                    continue;
                }

                ApplyConfiguredModdedBuildingCost(building, entry.Value);
            }
        }

        private static eStructs ResolveConfiguredBuilding(eMappers mapper)
        {
            if (BuildingCostDefinitions.TryGet(mapper, out BuildingCostDefinition definition))
                return definition.Structure;

            return mapper.ConvertToEStructs();
        }

        private void ApplyConfiguredModdedBuildingCost(eStructs building, BuildingCostsLobbyViewModel.BuildingCostValues values)
        {
            if (values.Wood >= 0)
                ApplyConfiguredModdedGoodCost(building, eGoods.STORED_WOOD_PLANKS, values.Wood);

            if (values.Stone >= 0)
                ApplyConfiguredModdedGoodCost(building, eGoods.STORED_STONE_BLOCKS, values.Stone);

            if (values.Iron >= 0)
                ApplyConfiguredModdedGoodCost(building, eGoods.STORED_IRON_INGOTS, values.Iron);

            if (values.Pitch >= 0)
                ApplyConfiguredModdedGoodCost(building, eGoods.STORED_PITCH_RAW, values.Pitch);

            if (values.Gold >= 0)
                ApplyConfiguredModdedGoodCost(building, eGoods.STORED_GOLD, values.Gold);

            if (values.Ale >= 0)
                ApplyConfiguredModdedGoodCost(building, eGoods.STORED_FOOD_ALE, values.Ale);
        }

        private void ApplyConfiguredModdedGoodCost(eStructs building, eGoods good, int amount)
        {
            BuildingCostsAPI.SetGoodCost(building, good, amount);
            configuredModdedGoodCosts.Add(new ConfiguredModdedGoodCostKey(building, good));
        }

        private void ClearConfiguredModdedGoodCosts()
        {
            foreach (ConfiguredModdedGoodCostKey cost in configuredModdedGoodCosts)
                BuildingCostsAPI.ClearModdedGoodCost(cost.Building, cost.Good);

            configuredModdedGoodCosts.Clear();
        }

        private readonly struct ConfiguredModdedGoodCostKey : IEquatable<ConfiguredModdedGoodCostKey>
        {
            public readonly eStructs Building;
            public readonly eGoods Good;

            public ConfiguredModdedGoodCostKey(eStructs building, eGoods good)
            {
                Building = building;
                Good = good;
            }

            public bool Equals(ConfiguredModdedGoodCostKey other)
            {
                return Building == other.Building && Good == other.Good;
            }

            public override bool Equals(object obj)
            {
                return obj is ConfiguredModdedGoodCostKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Building * 397) ^ (int)Good;
                }
            }
        }
    }
}
