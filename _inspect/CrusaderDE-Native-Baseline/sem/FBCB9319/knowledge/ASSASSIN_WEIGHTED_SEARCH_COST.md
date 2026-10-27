# Weighted Assassin search cost after loading (2026-10-23) [Scope: Vanilla + mod integration]

Native hash `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (installed DLL, verified 2026-10-23).
Evidence: decompiled `0xD9C40` (baseline database), BugfixesAndQoL DEVDIAG request counters and
per-search lines on `test_stresstest4.sav` (8 AI lords, largest map), runs L2BFQD–L5BFQD.

## Native builder `0xD9C40` (confirmed by pseudocode)

- Breadth-first search over the native queue (`manager+0x155F3C/+0x155F44`, ring of 0x4E520 entries).
- Returns 1 when the target tile is dequeued; 0 when the queue index reaches `param_6` (node limit),
  when the dequeued tile id exceeds 0x4E51F, or when the dequeued distance exceeds **800**
  (`*(short*)(0x5225B10 + 2*tile) > 800`). Vanilla therefore never accepts a route deeper than 800 steps.
- Edge u -> v: `conn[u] & mask[dir]` (`0x51890D0`, `0x312620`); otherwise only the even-direction
  climb fallback with `(flags[v] & 0x4A5014B1) == 0` (or special flag 0x1000 accepted by `0x107160`),
  building layer `0x4B6AA50` zero on **both** tiles, and the wall bit 0x100 on either tile.
- Callers: `0xE2CA0` (PCL-pair reachability, 400000 nodes), `0xF4930` (per-unit route), `0x11B520`
  (Assassin group order), `0x123090`.

## Observed load pattern (runtime evidence)

- In the first ~1.5 s after "mission ready", one AI Assassin group (player 5, tribe 4327, all units on
  tile 499,207) received ~100 `AttackBuilding` target commands. Vanilla's builder returned 0 for almost
  all of them (`native=0`): the start tile is a building/reserved climb endpoint that the native climb
  fallback rejects. BugfixesAndQoL's weighted search (reserved/direct-gatehouse endpoints allowed)
  found ~40 routes (13,000–43,000 expanded nodes, ~10 ms each).
- 15 targets were unreachable for the weighted search as well. Each search expanded exactly 243,362
  nodes (the whole start component) for ~70 ms: about 70 % of the ~1.5 s simulation stall.
- A reversed-edge search from those targets (same edge predicate) visited only 1–11 tiles; for every
  reachable target it visited ~244,000 tiles and met the start. No contradiction to the A* result.

## Consequences in BugfixesAndQoL (managed only, no native change) [mod integration]

- `ProvesTargetUnreachable`: bounded (512 tiles) reversed-edge proof before the A* and existence
  searches; skipped when Vanilla's builder already reached the target. Exactly the forward predicates,
  so a proof only replaces a search that would return "no route" anyway.
- Four-ary heap with packed (estimate, cost) key; identical pop order (unique insertion orders).
- Host settings split: `EnableImprovedAssassinPathfinding` (human players, climb button) and
  `EnableImprovedAssassinPathfindingAI` (AI lords), both on by default.
