# Unit line of sight and building sight heights [Scope: Vanilla]

Native build: `CrusaderDE.dll` SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, image base `0x180000000`. Static audit of 2026-11-14 (disassembly of the installed image plus semantic decompiler); executed on a private image copy with synthetic tile data by APIShared's `tests/WorkerProductionBonus.NativeTests/SightDispatchTests.cs`. No in-game trace yet. Function names stay `candidate`; the semantics below are `probable` unless marked.

## Wrapper `0xA06F0` (156 bytes, confirmed by full disassembly)

`int wrapper(ctx, sx, sy, sh, ex, ey, eh)`: calls the core `0x9E350(ctx, sx, sy, sh, ex, ey, eh, 0)`; a non-zero result is returned at once. Only a zero result triggers the reverse call `0x9E350(ctx, ex, ey, eh, sx, sy, sh, 0)`, whose result is returned. Both calls pass `0` as eighth argument. 16 callers, among them the Hunter target search `0x18AF50` (call `0x18B0A2`), the ranged attack order `0x18E9A0` (call `0x18ED6A`), the Bedouin healer `0x18DE10` and `HunterUpdate` `0x12FC70`.

## Core `0x9E350` (2681 bytes)

- Arguments: `ctx` (rcx), start X/Y/height, end X/Y/height (world units, 8 per tile; heights in sight units), eighth argument on the stack (`[rsp+0xE8]` after the prologue). A non-zero eighth argument skips every obstacle test (`(step & 1) != 0 && arg8 == 0` guards each tile check), so the trace only counts steps.
- Memory writes: only `[ctx+0xC]` (eight stores: `0x9E4D3`, `0x9E531`, `0x9E684`, `0x9E6D2`, `0x9E93A`, `0x9E9BB`, `0x9EC17`, `0x9EC9C`); no other store besides its own stack. Reads: the tile manager `0x405EDB0` (flags `+0x898400` as uint per tile, building id `+0xB0BCA0` as short, height `+0xD7E5A0` as byte), the row lookup table `0x402FF2C` (stride 12, the same table as the Script Extender's `GetTileId`).
- Tile coordinates are `x / 8` and `y / 8` (truncating, `cdq; and edx,7; add; sar 3`), without bounds checks.
- Start height gets `+10` when the start tile flag `0x10000000` is set. The value written to `ctx+0xC` starts as `0` (start not above end `+0x36`), `100` (start higher) or `0x1C`; half of it is an initial number of steps without obstacle checks.
- The trace walks one world unit per step along the major axis (Bresenham). On odd steps it looks at the tile; a tile without flags `0x400300` ends the "own structure" phase. When the tile changes and (flag bit 7 is set, or the tile's building differs from the start tile's building and the own-structure phase has ended or the tile has no flags `0x400200`), it asks the obstacle-height helper `0x6B990(tiles, tile, 1)`; if the line height interpolated between start and end is below that height, it returns `0`.
- Results: `0` blocked (also when the end is reached after the first step, because the step counter is still 0), otherwise the step count, at most `1000` (`999 < steps` returns), and `100000` for identical start and end X/Y. Never negative.

## Readers of the result

- Hunter target search `0x18AF50`: viewer = Hunter raw `+0x70E/+0x710` (X/Y), height = raw `+0x712 + +0x714 + 0x1E` (`add ecx,0x1E` at `0x18B070`); target = same fields `+ 0x1A` (`0x18B052`); visible when `result - 1 <= 0x1AF` unsigned (`0x18B0A7`), i.e. `1..0x1B0`. Raw record origin `0x65C`: `+0x70E` is `GameUnit +0xB2` (`r_CurrentWorldPositionX`), `+0x712` is `+0xB6` (`r_HeightElevation`), `+0x714` is `+0xB8` (`N0000006A`); all read with `movsx`.
- Ranged attack order `0x18E9A0`: viewer offset stored as `0x1E` at `0x18E9DE` and raised to `0x2D` or `0x3C` for two raw unit-type cases of its type switch (raw type word `+0x6E6`; types `74` and `83`); target offset `0x1A` (`0x18ED3C`); visible when `result > 0` (`test eax,eax; jle` at `0x18ED71`).

## Obstacle-height helper `0x6B990` (290 bytes, confirmed by full disassembly)

`int height(tiles, tile, mode)`: terrain byte `+0xD7E5A0` plus a contribution. Flag bit 28: `c_game_building_get_type_height_offset` (`0xC07C0`). Flag bit 10 (building): type word of the building record (`0x64CCBB0 + id * 0x32C + 0x12E`), types `7..78` dispatch through the byte table `0x6BAC4` to the case table `0x6BAB4` (`add rcx,r8; jmp rcx`):

| Case | Target | Types | Contribution |
|---|---|---|---|
| 0 | `0x6BA3D` | 7 (`STRUCT_HUNTERS_HUT`) only | `mode != 0`: none; `mode == 0`: falls through to case 3 |
| 1 | `0x6BA30` | 40-44 | type height offset |
| 2 | `0x6BA22` | 45, 46, 61, 74-78 | type height offset `+10` |
| 3 | `0x6BA41` | all others | building-height table `0x2E8C60[type]` (`.data`; type 7 holds `40`, the Woodcutter's hut type 3 also `40`) |

Types outside `7..78` go straight to case 3. Without a building, flag bit 7 without bit 8 and a value above `3999` in the tile grid `+0xA6F260` add `50` or `70` (by the word at `0x3357610 + value * 32`: `>= 13` gives 70, `>= 9` gives 50).

Callers: only the core `0x9E350` (six calls, all with `mode = 1`) and the projectile routine `0x9C730` (calls `0x9CD70` and `0x9CDD5`, both `xor r8d,r8d`). So case 0 exempts the Hunter's hut from sight only; arrows already collide with its table height. No direct branch in `.text` targets the interior of `[0x6BA0C, 0x6BA1D)`; RCX is free before `mov ecx,[r8+rax*4+0x6BAB4]` and the flags are dead until `add rcx,r8`.

## Uncertainties

- Flag meanings (`0x400300`, `0x400200`, bit 7, bit 28) are inferred from the helper only.
- Two of the six core-to-helper calls load `r8d` from `eax`; the decompiler resolves it to `1`, not traced instruction by instruction.
- The viewer offsets `45`/`60` of `0x18E9A0` are tied to raw type values whose unit names were not verified.
