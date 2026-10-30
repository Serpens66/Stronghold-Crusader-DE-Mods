# Ground route builders F4930, DAAC0 and DA590 [Scope: Vanilla + mod integration]

Native DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (installed DLL, matches `CURRENT.json`). RVAs are for this binary only. Sources: semantic-baseline pseudocode of the three functions (2026-10-29) and runtime samples from the MoatMove anchor diagnosis on `test_stresstest4.sav` (runs `pf-iter-7` and `pf-iter-10`, 2026-10-29). Function names are candidate-level; the control flow below was read from the complete pseudocode.

## 0xF4930: per-unit route builder

- Called from `0x196280` (per-unit route of a group move) and `0x18E1E0`. Parameters: the path manager, a movement class and a flag (`param_3`).
- Region gate: the path regions of start and target are read from `0x50EC690`, indexed by tile id (`0x402FF2C[y*3] + x`). If they differ and both manager mode words `+0x84` and `+0x88` are zero, the builder searches only when `0xE2F60` reports the two regions as connected. Otherwise it returns 0 at once (cheap failure).
- Ground mode (`+0x80`, `+0x84` and `+0x88` all zero):
  1. with `param_3 == 0`, `0xF32B0` (direct attempt);
  2. on failure, `0xDAAC0` with budget `2·(d+4)²`, where `d` comes from `0x79C0` (written to `0x34A9F5C`);
  3. on failure, `0xDA590` with budget 400,000.
- `+0x80 != 0` goes directly to `0xDA590`. Moat mode (`+0x84`) uses `0xDAFD0` with 400,000. Assassin mode (`+0x88`) uses `0xD9C40`.
- After a found route `0xE1640` builds the final route (mode argument 1, 2 or 3). Optional `0xDB650` refinement follows when `+0x94 != 0`. The result is stored at `+0x155F68`; failure returns 0 and leaves `+0x7C = 0`.

## 0xDAAC0 and 0xDA590: breadth-first ground searches

- Both functions are FIFO queues over tile ids, not Dijkstra. Straight steps record distance +1 and diagonal steps +2, but the queue order is breadth-first. A search ends when the target is dequeued or the budget (`param_6` dequeues) runs out.
- Edge rule of `0xDA590`: direction bit `d` of the byte `0x51890D0[tile]` (bit 0 = north, clockwise; bits 0x40/0x04 west/east, 0x01/0x80/0x02 north row, 0x10/0x20/0x08 south row). There is no height, moat or gate check in the search itself.
- `0xDAAC0` applies the same rule plus a per-neighbour byte test: the neighbour tile must have `0x51D75F0[tile] == 0`. Its meaning is not yet identified.
- Both return 0 for a start outside 0..799 or with `0x3A11EA4[y*800+x] == 0`. An invalid target caps the budget at 500.
- Search stamps: generation word `manager+4` (wraps at 32,000 with a clear of `0x9CA40` bytes), visit stamps `0x52C2550`, distances `0x5225B10`, queue at `manager+0x155F6C`.

## Direction byte changes without a tile report

- `0x51890D0` is referenced by more than 40 native functions (most often `0xD8CE0`, `0xE0530`, `0xE6330`). Observation `pf-iter-10`: next to a castle gate (tile 153,501, buildings 67/37) the byte changed from `0xF1` to `0xF5` without a call of the tile update `0xD90D0`. The writer is not identified.

## 0xDAA50 and 0xD86F0: direction byte rebuild

- `0xDAA50(manager)` clears `manager+0x155F34` and calls `0xD86F0(manager, *(short*)(0x3AAE2A4 + 2*i), i)` for `i` from 0 to `0x4E51F` (320,800 entries). It then sets `0x67E6424 = 1` and calls `0xE04B0(manager)` when `manager+0x155F34 > 0`.
- Callers: `0x5BC70`, `0x71670`, `0x87460`, `0x87D10`, `0xE1110` and `0xE1640`. `0xE1640` is the route finishing step of `0xF4930`.
- `0xD86F0` first stores 0 into the tile's direction byte `0x51890D0[tile]`. It derives the new byte from the tile tables `0x4DDD350`, `0x48F71B0`, `0x4B6AA50` (building, via `c_game_building_get_type_height_offset`) and `0x5001730`, plus the neighbours' entries, and stores the final byte at the end.
- In some branches it appends the tile to the manager list at `+0x1CAB4` and increments `+0x155F34`. It writes no other table.
- Consequence: a call of `0xDAA50` changes only direction bytes (and the follow-up of `0xE04B0`, not audited here). It does not change height, building or moat data.

## Consequences in MoatMove anchor mode [mod integration]

- Ground-only profiles of the anchor core use the ground directions (FastTraversalCache bits 8..15). These are BugfixesAndQoL's edge rules without the moat-mode height correction, which DAFD0's rules require only for moat routes. Before this change, keep, tower and wall tiles had no ground edges (`pf-iter-7`: rule `height` with native byte `0xFF`), and 4,974 units per run fell back to Vanilla's builder.
- The ground rules correspond to `0xDA590`. `0xDAAC0`'s extra tile test is not applied. Published routes are a subset of `0xDA590`'s graph and each is audited live.
- A chunked watch compares `0x51890D0` with a copy (40,000 tiles per simulation tick) and marks changed tiles like a tile report.
- Once the watch has recorded every byte, BugfixesAndQoL's `0xDAA50` observer no longer forces a full mask resweep. It reports "direction bytes only" (`InvalidateFastMoatDirections`), and the watch picks up the changes within one round.

## 0xF4930 failure path and the state of a failed ground search (audit 30.10.2026)

Source: complete pseudocode of the functions named below in the semantic export (confirmed), instruction bytes of the installed DLL.

- Order in ground mode (`+0x84 == 0`, `+0x88 == 0`) after the region gate:
  - `+0x7C = 1`, `+0x155F68 = 0`.
  - The straight attempt: `0xF32B0` when `param_3 == 0` (pathfinding profile "Large"), otherwise `0xF3060`. Both walk greedily toward the target and need the direction bit of `0x51890D0` for every step. `0xF32B0` also stops on some building tiles. Both increment the generation `+0x04`, stamp the walked tiles in `0x52C2550` and write the steps into the route buffer `*(+0x155F60)` from offset `+0x155F68`; `0x196280` clears that unit buffer (1000 bytes) before the call.
  - Success: `+0xA8 += 1`, return `+0x155F68`.
  - Failure: `+0xAC += 1`, `+0x7C = 0`, `0x79C0(&0x34A9F50, sx, sy, tx, ty)`, `+0x155F68 = 0`; with `+0x80 == 0` first `0xDAAC0` (budget 2·(d+4)²), on its failure `0xDA590` (400,000); with `+0x80 != 0` directly `0xDA590`. Both failing: return 0. Success of either: `0xE1640` (mode 1 or 2), optional `0xDB650` refinement.
- Region gate (`0xF49C8`): start and target in different regions search only when the target region is non-zero and `0xE2F60(manager, player, startRegion, targetRegion)` returns non-zero; otherwise F4930 returns 0 without any write. `0xE2F60` increments `manager+0xC8` and searches the region graph in local buffers only.
- `+0x80` is the global `0x60AD6E0`; `0x196280` sets it when the unit field `+0x8FC` is 0 or `+0x6E8` is non-zero, `0x11B520` sets it per member before `0x196280`.
- `0xDA590` and `0xDAAC0` are FIFO breadth-first searches whose edge rule depends only on the direction bits of the dequeued tile (`0xDAAC0` additionally needs `0x51D75F0[neighbour] == 0`). The queue (`+0x155F6C`, rows at `+0x28F3EC`) holds 320,800 entries, so a failure with a valid target and budget 400,000 always exhausts the start's forward component. Written state: generation `+0x04` (cleared at > 32,000), stamps `0x52C2550`, distances `0x5225B10`, queue, continuation `+0x155F3C`/`+0x155F44`/`+0x155F58`.
- Readers of that state: 89 functions use the stamps or distances; the 18 that do not increment the generation themselves either clear the arrays (`0x15200`, `0x5BC70`, `0x71670`, `0xE6250`, `memset 0x7490`) or read a search their own caller chain started just before (`0x11B520` → `0x124740`/`0xE0970`/`0xE0AC0`/`0xE1D30`; `0x1243D0` → `0xE1C30`; `0x3BC90` → `0xEA620`; `0x3C150` → `0xF2CB0`; `0x1263D0`/`0x1264D0`/`0x1265B0`/`0xCEFC0`/`0xCF020`/`0xCF080`/`0xCF110`/`0x3BB80`/`0x3BD50` → `0xE3590` via `0xE8C20`/`0xE9500`/`0xE9610`/`0xE9720`/`0xEA3D0`/`0xEA4E0`; `0xE1640`/`0xE1110` after a found route; `0x123090`/`0x1232E0`/`0xE4FC0` search first). Only `0x11B520` resumes `0xDA590` (`param_7 = 1`), and only its own field search.
- `0x34A9F50` is a scratch object: all 61 readers of `+0x04..+0x0C` and the helpers receiving it (`0x77C0`, `0x7600`, `0x7A80`) write before reading; `0x78E0`, `0x7890` and `0x9E00` ignore it.
- After a failed build `0x196280` writes only unit fields (`+0x92A = 0`, type 0x52: `+0xA7E = 100`) and calls `0x199CD0`, which reads no search state; `0x11B520` then calls `0x123460`, which reads no stamps.

## Failed ground builds in MoatMove anchor mode [mod integration]

- `Testmods/MoatMove/src/GroundFailureProofs.cs` replays the failure path for members of one group move whose start lies in a closed failure set (BugfixesAndQoL's `CursorGroundRejectProof`, checked against the current direction bytes and the gate policy) and whose target lies outside it: region gate and straight attempt run natively, `0xDAAC0`/`0xDA590` are skipped. Validated bytes and fields: `Testmods/MoatMove/UpdateToNewDLL.md`.
- DEVDIAG verification on `test_stresstest4.sav` (pf-known-8): 113 of 113 predicted failures matched Vanilla's result and terminal fields.

## 0xF4930 moat mode and 0xDAFD0 (audit 30.10.2026)

Source: pseudocode of `0xF4930`, `0xDAFD0`, `0x107160`, `0xC07C0` and `0xE1640` in the semantic export and the disassembly of the installed DLL (FBCB9319).

- Moat mode (`+0x84 != 0`, `+0x80 == 0`, `+0x88 == 0`): the region gate is skipped (mode word non-zero) and there is no straight attempt. `+0x7C = 1`, `+0x155F68 = 0`, `+0xAC += 1` (`0xF4A2B`), `+0x7C = 0`, `0x79C0(&0x34A9F50, sx, sy, tx, ty)`, `+0x155F68 = 0`, then `0xDAAC0(manager, sx, sy, tx, ty, 2*(d+4)^2, 0)` (`0xF4AF5`, `d` = `0x34A9F5C`) and, if it fails, `0xDAFD0(manager, sx, sy, tx, ty, 400000, 0)` (`0xF4B50`). Both fail: return 0 (`0xF4A85`). A route: `0xE1640(..., mode 2, 0)`, then with `param_3 == 0` and `+0x94 != 0` the `0xDB650` second pass.
- `0xDAFD0` returns 0 without a search when the start or target is off the map or `0x3A11EA4[y*800+x] == 0`. Otherwise (`param_7 == 0`) it increments the generation `manager+4` (wrap above 32,000 with a clear of `0x9CA40` bytes), stamps accepted tiles in `0x52C2550` and their distances in `0x5225B10`, and dequeues FIFO from the ring `manager+0x155F6C` (head `+0x155F3C`, tail `+0x155F44`, both wrapping at `0x4E520`).
- The budget compare (`0xDB164`) tests the ring head index against `param_6`. The head wraps below 320,800, so the 400,000 of F4930 never stops the search: a failure always exhausts the start's component, and the tiles stamped with the final generation are exactly that component.
- Edge rule, from the dequeued tile `v` to neighbour `n = v + 0x405EDB0[y*8 + d]` (y = row of `v`): an unvisited `n`; then either `v`'s direction bit (`0x312620[d] & 0x51890D0[v]`, a load APIShared's gate filter hooks at `0xDB242`) with a height check, or flag rules (`0x48F71B0` bits 8, 30, `0x40000800`, `0xA5014B1`, bit 12 with `0x107160`) that also read `v`'s side tiles `v + 0x405EDB0[y*8 + d - 1]` and `[y*8 + (d+1) mod 8]`. The height check compares `0x4DDD350` of `v` and `n` within 0x10; with flag `0x10000000` on `n` the building height offset `0xC07C0(type)` is added, and building types 0x2D/0x2E (word at `0x64CCCDE + b*0x32C`, `b` from `0x4B6AA50`) shift by 0x5A.
- No player input. `0x107160(0x32DE440, tile)`: index `0x4ACE010[tile]` (short); non-zero: true for the word at `0x32DE440 + index*0x9C + 0x6A` when it is at least 5 and not 0xF.
- `0xC07C0(manager 0x64CCBB0, building)` depends only on the building's type word (`+0x12E` at stride `0x32C`, the same word as `0x64CCCDE + b*0x32C`).
- `0xDAAC0`'s edges are not a subset of `0xDAFD0`'s: it follows direction bits without the height check (only `0x51D75F0[n] == 0`).
- `0xE1640` rebuilds all direction bytes (`0xDAA50`) only when its route reconstruction finds no step back; it then increments `manager+0x78`.
- Observation `pf-iter-66` (`test_stresstest4.sav`, after a spawn of 4,800 units): 128 moat-mode builds in one 2.5-s window, 109 failed, 281 of 282 ms. Vanilla alone shows the same bursts (`pf-se-1`, sampled inside `0xDAFD0`).

## Failed moat-mode builds in MoatMove anchor mode [mod integration]

- `Testmods/MoatMove/src/MoatFailureProofs.cs` keeps the stamped component of a failed moat-mode build (per player and gate policy) and a hash of the edge inputs above on every component tile with a neighbour outside it plus all their neighbours. A later moat-mode request with its start inside and its target outside replays F4930 up to `0xDAAC0` and skips `0xDAFD0`; a `0xDAAC0` route restores `+0xAC` and lets Vanilla's builder run. The hash is checked on every use; a mismatch drops the proof.
