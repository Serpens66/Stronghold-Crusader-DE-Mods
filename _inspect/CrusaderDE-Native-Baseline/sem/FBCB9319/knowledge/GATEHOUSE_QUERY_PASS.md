# Automatic gate closing: query pass per engine run [Scope: Vanilla]

Audit observation: 2026-10-23. Native canonical installed DLL SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
(Script Extender 2.14.3 installed). RVAs are evidence for this image only; no new hook
contract. Reproducible disassembly helper: `.work/a/gate-loop/001/dis.py` (capstone,
`.pdata`, direct-call and pointer scans of the installed DLL).

## Call chain (confirmed by direct calls and the only table reference)

- `DLL_RunTick` export `0x86680` calls the game tick `0xCDE60` exactly once (`0x869EF`);
  no later branch in `0x86680` returns before that call, and no pointer or jump references
  `0xCDE60`. Other direct callers: `DLL_SetMapEditorParam` `0x87460` and the map
  initialization `0x71670` (calls `0xCDE60` once, then the PCL rebuild `0xE49D0`), which is
  reached from `DLL_LoadSaveGame` and two start paths (`0x94350` calls it twice in a row).
- `0xCDE60`: the date/time update `0xD1AD0` is conditional (`0xCE003..0xCE021`); the
  Extender's tick context hook sits inside it (`0xD1D4F`, pattern `FF 87 ?? ?? ?? ?? 33 DB`).
  The building dispatcher `0xC60F0` (`0xCE1B4`) is reached independently of that branch.
  Hence the Extender tick is not a reliable identity for a building pass.
- `0xC60F0`: loops `gTempBuildingIndex` (`0x8F9BE4`) once over all buildings and calls the
  per-type update from the table at `0x2DEAE0` (single code reference `0xC64AE`). The three
  gatehouse sizes `0xA5320`, `0xA57F0`, `0xA5C80` (table slots `0x2DEC48/50/58`) each call
  `0xB73D0` once; `0xB73D0` has no other caller.

## Gatehouse handler `0xB73D0` (function `0xB73D0..0xB7CE5`)

- The unit loop (`0xB7B30..0xB7BEE`) runs only when
  `(tick 0x3A0FA28 % 10) == ((building+0x64CCD10 + 5) % 10)`; this is a different counter
  from the Extender's `gCurrentGameTick` (`0x3665F58`).
- It walks the list of the building owner's player record (count at `+0x379CE58`, stride
  `0x583C`; unit IDs at `0x3809EBB + player*0x4E20` as words, global IDs at
  `0x3835DF3 + player*0x9C40` as dwords), skips entries whose global ID no longer matches,
  and at `0xB7B4B` performs the alive/type/controllable test that the Extender replaces with
  `BuildingR3EventHooks.OnGatehouseQuery`. Without a subscriber's veto the unit's
  Chebyshev distance to the gate in fine coordinates decides (`< 200`, or `< 140` with the
  flag computed at `0xB7AB2`).
- The first unit within range sets the close timer (`0x4B0`/`0x64`), may set state `0x0A`
  and call `0xC5300`, and leaves the loop. Iterations without that result write nothing.

## Field identities (Script Extender 2.14.3 structures)

Derived from the logged `_buildingArray` (slot 1 at RVA `0x64CCF38`, stride `0x32C`) and unit
array: gate record `+0xD6` = `GameBuilding.r_PlayerIdOwner` (cross-check of the base),
`+0xEE/+0xF0` = `r_TilePositionXBegin/YBegin` (read as signed words, multiplied by 8);
unit `+0x88/+0x8A` = `r_AliveState/r_UnitChimp`, `+0xB2/+0xB4` =
`r_CurrentWorldPositionX/Y` (signed words). The native third candidate test reads unit
`+0x2A0` (`r_SelectionAllowed`), whereas the Extender's emulated default uses
`r_ControllableForPlayerId`; consumers follow the Extender's published default.

Closing condition after the query: `max(|8*X - unitX|, |8*Y - unitY|) < 200`, or `< 140`
with the flag from `0xB7AB2`. A query for a unit at distance 200 or more can never close
the gate, whatever its answer.

## Consequence for consumers

Inside one managed `EngineInterface.run` (which calls `DLL_RunTick` once), every gatehouse
has at most one query pass, and nothing native between two queries of a pass changes
buildings, path connection records, the structure layer or the path component grid.
BugfixesAndQoL relies on this to take the gatehouse lookup (the Extender's
`TryGetPathConnectionRecordByBuildingId` scans all 199 records, ~0x200 bytes apart) and its
drawbridge collection once per pass (`SimulationRunScope`, `ReachableEnemyGatehouseRuntime`).
Queries outside a marked run (map initialization, editor) are evaluated freshly. Units outside
the closing range keep the candidate decision without a reachability evaluation. Remaining
assumption: another `OnGatehouseQuery` subscriber does not mutate these structures inside
its callback; the DEVDIAG build re-evaluates every reused result and counts disagreements
(`passMismatches`).

Measured cost on `test_stresstest4.sav` (8 AI, largest map, speed 300, 2026-10-23):
~4000 queries per gate pass; before the change the reachability callback took
0.9 ms per simulation tick (DEVDIAG) and slowed the following native phase `0xF5A40`
by ~0.3–0.5 ms through cache eviction.
