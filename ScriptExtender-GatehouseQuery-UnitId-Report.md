# GatehouseQueryEventArgs.UnitId is zero-based

## Affected code

`BulkBuildingDetours.c_game_building_gatehouse_query_hook` in Script Extender
1.42.0 source and the byte-identical locally built and installed assembly compute
the event value as:

    int unitId = unitApi.GetUnitArray().GetIndexByOffset(unitOffset);

The audited Script Extender source is tag `v1.42.0`, commit
`171d68e155a8f98c5f8c4ee154d9af154c9a2443`. The local build, packaged copy,
and installed `SHCDESE.dll` all have SHA-256
`30A0719E3B2385952D8ADDE84DDBD5F069B0DE94D42B0794BBB6CDE504708AD5`.
Decompiling that installed assembly confirms the same call sequence without an
intervening `+1`.

`SimpleNativeArray.GetIndexByOffset` returns a zero-based array index, while
`GameUnitManagerAPI.TryGetUnitById` and the public `UnitId` contract require a
one-based game ID.

## Native evidence

For `CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`,
the unique gatehouse-query hook is at RVA `0xB7B4B` inside
`0xB73D0..0xB7CE5`. Immediately before the hook, RVA `0xB7B30` reads a
zero-based unit slot from the gatehouse candidate list and RVA `0xB7B34`
multiplies it by the `GameUnit` size `0x490`. The resulting byte offset in RDX
is used directly against the first unit record. It is therefore a span offset,
not an already one-based game ID.

The current hook consequently evaluates `TryGetUnitById(unitId)` against the
previous unit slot, and slot index zero becomes invalid ID zero.

## Proposed fix

Convert exactly once when leaving the native array-offset boundary:

    int unitId = unitApi.GetUnitArray().GetIndexByOffset(unitOffset) + 1;

The corrected one-based value should be used both for the hook's Vanilla logic
emulation and for `GatehouseQueryEventArgs.UnitId`. A regression test should
cover native offset `0 -> UnitId 1` and the final valid array slot.
