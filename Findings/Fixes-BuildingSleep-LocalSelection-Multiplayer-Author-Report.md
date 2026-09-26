# Fixes 1.19.1: building sleep uses each peer's local selection

At `src/shcde-fixes/Detours/BuildingDetours.cs:250-373`, Fixes chooses which stored goods to preserve from `CurrentlySelectedBuildingIdVA`. This patches native `CrusaderDE.dll` at RVA `0xC7F3E`. The installed Fixes 1.19.1.0 DLL contains this path.

Vanilla `ToggleSleep` (`GameAction` 1003) sends Chore 43 with a **two-byte building type**, not the selected building ID. Its handler toggles the sender's buildings of that type and reaches the storage reset at RVA `0xC7E90`. A receiving peer's local selected building can differ. If player 1 selects an iron mine containing iron while player 2 has never selected a building, Fixes preserves `r_IronIngotsAmount` (building offset `0x138`) on player 1's machine but clears it on player 2's. Vanilla's building checksum (`0x22020`) includes that field.

**Reproduce:** With `ProductionBuildingsDontLoseStorageOnSleepFix` enabled, fill an iron mine's local storage, select it only on player 1's computer, then toggle its sleep state. Compare `r_IronIngotsAmount` on both peers. This is a static proof from the current native command, reset and checksum paths; a dedicated runtime trace has not yet been captured. The preservation branch should use the building being processed or the synchronized building type.
