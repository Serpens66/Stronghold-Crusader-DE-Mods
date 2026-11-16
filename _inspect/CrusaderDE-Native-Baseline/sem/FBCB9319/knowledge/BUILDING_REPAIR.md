# Building repair contract (CrusaderDE.dll FBCB9319…F2831E2) [Scope: Vanilla + mod integration]

The installed `CrusaderDE.dll` has SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

- `FUN_1800B6F70` (RVA `0xB6F70`) calculates repair wood and stone only. For each positive construction cost it uses `max(1, floor((maxHP-currentHP)*baseCost/maxHP))`; full health yields zero. The manager's output fields are at offsets `0x31B824` (wood) and `0x31B828` (stone).
- `FUN_1800D51A0` (RVA `0xD51A0`) supplies the HUD's repair costs and permission, including the native proximity check. The audited entry begins `48 89 5C 24 08 57 48 83 EC 30 48 63 1D E3 31 71 06`. `FUN_18019D960` calls it for Vanilla tower and gatehouse HUD modes.
- `FUN_1800D4EE0` (RVA `0xD4EE0`) handles the RepairBuilding action and checks only wood and stone before queuing chore `0x44` (68).
- `FUN_1800D5030` (RVA `0xD5030`) executes the chore. It validates the building global ID, checks and subtracts the queued wood and stone, then restores health to the maximum. Gold, iron and pitch are absent from this path.
- The installed Script Extender exposes `BuildingR3EventHooks.OnBuildingRepair` around the `0xD5030` repair execution: Pre runs before the original and Post only after the original was called. It exposes player ID, one-based building ID, global ID, wood and stone costs, and `SkipOriginalFunction`.

APIShared's optional repair capability checks the full installed hash and the entry bytes before calling `0xD51A0` under `EngineInterface.threadLock`. Its event handler checks extra resources in Pre and charges them in Post only after a verified damaged-to-full-health transition for the same building global ID. This supplement is tied to the above native and Script Extender contracts and requires re-audit on either update.

## Damage eligibility versus repair permission [Vanilla facts]

Audit 2026-11-15 against the same native hash: the signed Int32 table at RVA
`0x2E6710` is indexed directly by structure type. Both direct damage `0x7EB00`
and area damage `0x7DF60` reject zero entries before subtracting building HP.
Target eligibility helpers `0xC2190`/`0xC2280` also read it, with separate tower
and gatehouse branches. Positive maximum health alone is not damage eligibility:
`STRUCT_GOODS_YARD` (10) defaults to 500 HP but has a zero entry; market type 26
has a nonzero entry. `0xD51A0` computes repair permission through costs and
proximity and is not a replacement for this permanent type eligibility check.

Managed `MainViewModel.setUpInbuilding` shows `RefTradepostPanel` for
`Enums.InBuildingModes.INSIDE_TRADEPOST` (25). Price, food, bulk, arms and trade
panels use modes 53..57; manual and automatic trade are children of mode 57.
`EditorDirector.directSetAppSubMode` writes the public managed
`GameData.app_sub_mode` before switching the panel and updating native mode;
this effective UI mode can precede the immutable repair snapshot during pause.

## Optional repair HUD visibility [mod integration]

ExtraFeatures 1.0.137 reads the live eligibility table through its existing
full-hash gate, suppresses its optional button for zero entries, and allows a
selected market's button only in the overview. It reuses its hidden/disabled
hover cleanup and leaves APIShared repair quotes and execution unchanged.
Production visibility tests run against the mapped installed image, including
live table overrides, invalid bounds and all market panel enum values.
