# Fog of War correctness issues

Reviewed commit: `3f6399511e8872c78a073978c91ca8781dbeecc6`

## Incorrect native unit-owner offset

`NativeUnitVisionReader` reads the owner at its manager-relative offset `0xA28`, which resolves to `GameUnit + 0x3CC`. The confirmed owner field is `GameUnit + 0x92`, corresponding to manager-relative offset `0x6EE` in the reader's addressing scheme. Please use the confirmed owner offset.

**In-game impact:** Friendly, allied, and enemy units can be assigned to the wrong player, breaking unit vision sources, radar classification, and hidden-unit selection filtering.

## Buildings created through the Script Extender can be missed

After a player has been marked as seeded by the initial keep-area scan, buildings created through [`GameBuildingManagerAPI.CreatePrefab`](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/5b4d48e732e9b6e2e93c135f0b28ce5b9d8bcd33/src/SHCDESE.BepInEx/API/GameBuildingManagerAPI.cs#L446) can be missed. This API calls the native building path directly and therefore bypasses the mod's `EngineInterface.PlaceMapperItem` patch; the later reconciliation only checks already tracked building IDs. Please add a registration or reconciliation path for buildings created through this API, for example by consuming the Script Extender's building-spawn event.

**In-game impact:** Such buildings can permanently fail to provide vision, especially when they are created outside the initial keep scan radius.
