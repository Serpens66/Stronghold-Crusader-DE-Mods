# Building pause state and menu coverage [Scope: Vanilla + mod integration]

Static audit, 2026-11-15. Installed CrusaderDE.dll SHA-256 checked against CURRENT.json:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Native roles below are direct data-flow reconstructions; descriptive names do not upgrade candidate symbols to an official ABI.

## Vanilla facts

Building selection at RVA `0x89F40` maps `STRUCT_WATERPOT` (70) to panel 40 and
`STRUCT_CHURCH3` (38, cathedral) to panel 96. `HUD_Buildings.IsWorkerBuilding`
and `GetBuildingSleepable` expose the WorkerPanel's two pause buttons for both.
The full regular pauseable type set is
`3,4,5,6,7,12,13,14,15,16,17,18,20,22,23,27,30,31,32,33,34,36,37,38,70`.
Of these 25 types, exactly Water Pot and cathedral map to panels absent from
the sleep-status predicate at RVA `0xD5140`; the other 23 map to accepted panels.
The predicate's accepted panel values are
`3,6,7,8,9,10,13,14,15,16,17,18,19,20,22,26,29,30,31,32,33,35`.

RVA `0x19D960` writes `sleep_allowed` and `building_type_sleeping` only when
`0xD5140` succeeds. The latter is therefore not an authoritative per-type status
for every building whose managed menu exposes a pause button. The managed
`EngineInterface` copy retains this field unchanged. `FatControler.addChimpActions`
reads it once and uses it for the pause-button visibility, pause text and early return.

The actual table is `module+0x379CF64+playerId*0x583C+buildingType`, with one-based
player IDs and 91 byte entries per player. Native `0xD5980` toggles a byte by
zero/nonzero, then invokes `0xC7E90`, which updates alive buildings matching owner
and type and resets worker references and local goods. `0xC7D50` periodically
synchronizes alive building sleep bytes against the same table, with dependent
resets on disagreement; types outside the table are not indexed.

The public Extender view resolves the same address:
`playerManager+0x1343FC+(playerId-1)*0x583C+0x2194+buildingType`,
where the manager is `module+0x366C210`. Thus the per-type state can be read through
`TryGetPlayerResourcesById` and `GamePlayerResources.r_BuildingSleep`, without a new
raw-address consumer. The installed SHCDESE.dll and local source both expose this
fixed byte[91] field and enum values WELL=27, CHURCH3=38 and WATERPOT=70.

The command chain remains `ButtonToggleZZZMode` -> `DLL_GameAction` (RVA `0x81870`,
command 1003) -> `0xD6F00` -> Chore43 (`0x13C10`) -> `0xD5980` -> `0xC7E90`.
Chore43 carries the two-byte building type and uses the Chore sender as owner.
In tutorial mode4 the producer first checks `0xF7590`; rejection calls `0xF80C0`
and queues no toggle. Existing Vanilla command dispatch preserves that guard.
For historical selection-dependent Fixes storage behavior see the separately
pinned `BUILDING_SLEEP_SELECTION.md`; its evidence bytes are unchanged.

## BugfixesAndQoL correction [mod integration]

Both normal-click paths previously compared the desired state with the incomplete
UI field. With Water Pots globally asleep and a stale UI value0, a normal resume
skipped the native toggle; the subsequent synchronizer reapplied the sleeping type
state. The installed mod's decompiled methods confirmed both occurrences.

Read the owner/type table before clearing overrides or queueing any action. Use the
selected building's individual override (otherwise its sleep byte) to derive the
desired state. Preserve existing Chore ordering, native worker resets and goods
handling. Failed table resolution makes no state changes.

Replace the UI field read in addChimpActions with the effective selected-building
state, logically gated by the feature. Keep Vanilla's button/text branches intact
and never modify the shared PlayState snapshot. Disabled/unresolved paths retain
the original value. The patch is managed only and adds no native site.

These are static findings and implementation contracts, not in-game or real
multiplayer acceptance. Confirm repeated toggles, selection changes, overrides,
save/load and worker recovery separately in those environments.
