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

## Consequences in MoatMove anchor mode [mod integration]

- Ground-only profiles of the anchor core use the ground directions (FastTraversalCache bits 8..15). These are BugfixesAndQoL's edge rules without the moat-mode height correction, which DAFD0's rules require only for moat routes. Before this change, keep, tower and wall tiles had no ground edges (`pf-iter-7`: rule `height` with native byte `0xFF`), and 4,974 units per run fell back to Vanilla's builder.
- The ground rules correspond to `0xDA590`. `0xDAAC0`'s extra tile test is not applied. Published routes are a subset of `0xDA590`'s graph and each is audited live.
- A chunked watch compares `0x51890D0` with a copy (40,000 tiles per simulation tick) and marks changed tiles like a tile report.
