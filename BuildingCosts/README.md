# BuildingCosts

BepInEx mod that exposes an API for replacing building good costs.

The native vanilla cost tables are not changed by the public API. Instead, the mod
checks configured costs before building, builds through the original function as
free when a matching override exists, and then removes the effective modded costs
itself. This allows different costs for humans and AI.

Other C# mods can reference `BuildingCosts.dll`, add `[BepInDependency("BuildingCosts")]`, and call:

```csharp
using BuildingCosts;
using SHCDESE.Interop;

// Replace the human cost for this building: vanilla wood is explicitly disabled,
// stone is set to 6, and AI keeps the vanilla cost because it has no AI override.
BuildingCostsAPI.Building_SetGoodCost(
    eStructs.STRUCT_HOVEL,
    eGoods.STORED_WOOD_PLANKS,
    0,
    BuildingGoodCostTarget.Human);

BuildingCostsAPI.Building_SetGoodCost(
    eStructs.STRUCT_HOVEL,
    eGoods.STORED_STONE_BLOCKS,
    6,
    BuildingGoodCostTarget.Human);

int moddedStoneCost = BuildingCostsAPI.Building_GetModdedGoodCost(
    eStructs.STRUCT_HOVEL,
    eGoods.STORED_STONE_BLOCKS,
    BuildingGoodCostTarget.Human);

BuildingCostsAPI.Building_ClearModdedGoodCost(
    eStructs.STRUCT_HOVEL,
    eGoods.STORED_WOOD_PLANKS,
    BuildingGoodCostTarget.Human);
```

`Building_SetGoodCost` replaces the cost for one good on one building and target.
The overload without a target applies to `BuildingGoodCostTarget.All`. A value of
`0` is a valid override and means that this good is explicitly not required. Use
`Building_ClearModdedGoodCost` to remove an override and return that good to
vanilla behavior.

`Building_GetModdedGoodCost` and `Building_GetModdedGoodCosts` return only
explicitly configured modded override values. They do not return the effective
vanilla-plus-overrides cost list.

The ScriptExtenderUI options set modded overrides for wood, stone, iron, pitch,
gold, and ale. A value of `-1` leaves vanilla behavior unchanged. Values from
`0` to `1000` set an override for that good on the selected building.
