# Building repair contract (CrusaderDE.dll FBCB9319…F2831E2)

The installed `CrusaderDE.dll` has SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

- `FUN_1800B6F70` (RVA `0xB6F70`) calculates repair wood and stone only. For each positive construction cost it uses `max(1, floor((maxHP-currentHP)*baseCost/maxHP))`; full health yields zero. The manager's output fields are at offsets `0x31B824` (wood) and `0x31B828` (stone).
- `FUN_1800D51A0` (RVA `0xD51A0`) supplies the HUD's repair costs and permission, including the native proximity check. The audited entry begins `48 89 5C 24 08 57 48 83 EC 30 48 63 1D E3 31 71 06`. `FUN_18019D960` calls it for Vanilla tower and gatehouse HUD modes.
- `FUN_1800D4EE0` (RVA `0xD4EE0`) handles the RepairBuilding action and checks only wood and stone before queuing chore `0x44` (68).
- `FUN_1800D5030` (RVA `0xD5030`) executes the chore. It validates the building global ID, checks and subtracts the queued wood and stone, then restores health to the maximum. Gold, iron and pitch are absent from this path.
- The installed Script Extender exposes `BuildingR3EventHooks.OnBuildingRepair` around the `0xD5030` repair execution: Pre runs before the original and Post only after the original was called. It exposes player ID, one-based building ID, global ID, wood and stone costs, and `SkipOriginalFunction`.

APIShared's optional repair capability checks the full installed hash and the entry bytes before calling `0xD51A0` under `EngineInterface.threadLock`. Its event handler checks extra resources in Pre and charges them in Post only after a verified damaged-to-full-health transition for the same building global ID. This supplement is tied to the above native and Script Extender contracts and requires re-audit on either update.
