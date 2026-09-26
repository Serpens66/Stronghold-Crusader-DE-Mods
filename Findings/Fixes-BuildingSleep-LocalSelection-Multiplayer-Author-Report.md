# Fixes 1.19.1: building sleep diverges in multiplayer

`ProductionBuildingsDontLoseStorageOnSleepFix` chooses which goods to preserve from each peer's local `CurrentlySelectedBuildingIdVA` (`BuildingDetours.cs:250-373`, native RVA `0xC7F3E`). Vanilla's synchronized `ToggleSleep` command carries only the building type (Chore 43), so peers can take different preservation branches.

**Reproduced on 27 September 2026:** The host selected an iron mine; the client selected a woodcutter's hut. Both logged `iron=1` in the same mine (`building=58`, `global=4124`) before the sleep toggle. The next tick and 20 ticks later, the host had `iron=1` and the client `iron=0`. The tester observed a desync; neither BepInEx log contains an explicit checksum/desync message.

Use the building being processed or the synchronized building type to choose the preservation branch.
