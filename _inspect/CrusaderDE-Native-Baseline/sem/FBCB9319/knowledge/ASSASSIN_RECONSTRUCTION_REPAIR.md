# Assassin route reconstruction failure and full-map repair [Scope: Vanilla + mod integration]

Native hash `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (installed DLL, 2026-10-20).
Evidence: disassembly of the listed functions plus a runtime replay of the descent on the live field
(BugfixesAndQoL DEVDIAG, 8-lord save) and an executed copy of E1640 in an isolated fixture.

## Flow (confirmed)

- `0xF4930` (per-unit route builder): when path manager `+0x88 != 0` (Assassin profile) the field comes
  from `0xD9C40` (or `0xDA590`/`0xDAAC0` on the `+0x80` branch); `0xF4B5A` then calls
  `0xE1640(manager, startX, startY, targetX, targetY, mode)` with mode 3. Otherwise mode is
  `(+0x84 != 0) + 1`. `0xE32B0` uses mode 0; F4930's second call (when `+0x94 != 0`) mode 1.
- `0xE1640` descends from the target tile to the start tile. A neighbor qualifies when its stamp
  equals the target's stamp and its distance is in `[best - 2, best)`. Mode 3 (`0xE19BB`..`0xE1A12`):
  ordinary edge = `conn[current] & mask[dir]` (`0xE19C0`); otherwise only a cardinal climb with
  building-ID rules (`0xE19D8`, `0xE19F9`) and a wall bit (`0x100`) on either tile.
- No qualifying neighbor: `++manager+0x78`, then `0xDAA50` (D86F0 over all 320800 tiles, ~9–10 ms),
  `0xE49D0(manager, 1)`, `0xC3E50`, `0xDE6A0`; about 19 ms per failure. The output stays empty.
- `0xD9C40` expands u -> v by `conn[u] & mask[dir]` (`0xD9EA6`): the start-side tile's bit. The
  physical step `0xDCE60` accepts either endpoint's bit (see ASSASSIN_DIRECT_GATEHOUSE_CLIMB.md).

## Inconsistency (confirmed by runtime replay)

Across a one-way ordinary connection the field contains the edge, but mode 3 rejects it because it
tests the target-side tile's bit back toward the start. Observed case: Assassins standing in their own
gatehouse (building with tile flag `0x10000000`) and leaving over its drawbridge; the drawbridge tile
lacks the bit back toward the gatehouse. All 63 in-game failures of one run had exactly this shape
(drawbridge buildings 382/675/831), every member of an AI Assassin group failing in the same tick.

Counts per 60 s on `test_8lordsmiddle` (PerfFixTest native counter `manager+0x78`):
Script Extender only 10; BugfixesAndQoL with direct gatehouse climbing 112 (the AI then sends
Assassin groups out of their gatehouse far more often); same with the APIShared rule below 3.

## Correction (APIShared `AssassinPathAPI`, site 0xE19C0/14) [mod integration]

While policy flag 1 (weighted reconstruction) or 2 (direct gatehouse climbing) is set, mode 3 also
accepts `conn[neighbor] & mask[(dir + 4) & 7]`, i.e. exactly the edge D9C40 used. Stamp/distance checks,
climb rules, other modes and the repair path are unchanged. Executed fixture: Vanilla fails with the
four repair calls; with the rule the route is reconstructed (packed directions verified); a step
without either bit still fails; mode 1 is unaffected.
