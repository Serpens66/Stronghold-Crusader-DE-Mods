# Minimap (radar) pass 0xF5A40 (FBCB9319)

Native DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (verified against the installed DLL on 2026-10-23). The addresses below are RVAs/VAs for this binary only.

Evidence:
- semantic pseudocode `exports/semantic-decompiled-functions.c`;
- Rizin disassembly of the call site and of the whole function (`pdr`, 0x1800F5A40–0x1800F6DDD);
- managed decompilation of `Assembly-CSharp` `BC8B6A39…`;
- PerfProbe thread sampler (2026-10-23, 63 633 sim-thread samples, `0xF5A40` self share 42.1 %).

## Call

- `DLL_RunTick` (`0x86680`, confirmed) calls `0xF5A40` at `0x86B9B` unconditionally on every tick, including paused ticks (the call is after the `paused` block).
- Arguments:
  - `rcx` = `&0x183496220` (radar object);
  - `rdx` = `retData`;
  - `r8` = `radarMap` (managed `MemoryBuffers.MemBuffer.radarMap`, pinned in `EngineInterface.run`);
  - `r9d` = `screenCentrePosX`;
  - stack: `screenCentrePosY`, `screenTilesWide`, `screenTilesHigh`, `radarMapWidth`, `radarMapHeight`, `radarZoom`, `0`.
  - Ghidra shows 9 parameters; Rizin reads stack arguments up to `arg_48h`.
- Trust level: confirmed (call site and argument names from the `DLL_RunTick` export signature).

## Work

- An 800-row tile walk with rotation variants selected by `0x1860AD40C` (0/2/4/6).
- Per minimap pixel inside the radar window it reads:
  - the tile index (`0x183B4ACE4`);
  - the unit on the tile (`0x184C559B0` → unit record `0x1867E8xxx`, stride `0x490`/`0x248`);
  - the diplomacy table `0x1837EDF3C` (colour against the local player `0x1888E3D70`);
  - tile flag layers (`0x1848F71B0`, `0x184A312B0`, `0x18485A770`, `0x184DDD350`, `0x184EC82B0`, `0x184F64CF0`);
  - the colour tables `0x1803126xx`/`0x1803127xx`/`0x180312880`.
- Afterwards it draws the camera frame (`0xFFFFFFFF`) with a thickness depending on the radar width (`+0x44` < 0x100, < 0x1F0, else).
- No callees.

## Writes (complete: 27 non-stack stores)

- 26 stores through `r12` = `radarMap`: ARGB pixels (two horizontal pixels per tile) and the frame.
- 1 store at `0x1800F64B7` into `*(radar+0x70)[row*width+col]`: the tile index per minimap pixel (click lookup).
- No store through `retData` and none to absolute globals.

## Readers

- `radarMap`: managed only. `Director.Update` → `GameMap.processTestMap(…, radarMap)` → `ApplyRadarMap` (texture upload). The save radar uses a separate buffer (`0x183496298` via `0xF6FB0`).
- `radar+0x70` table: allocated by `0xF6F80` (`DLL_GameAction` 0x406 = `ResizeRadarMapBuffer`, 1030; `c_game_malloc(0x271000)`).
  - Read only by `0xF6F20` (`DLL_GameAction` 0x404 = `RadarClicked`, 1028, from `FatControler`).
  - On a non-zero entry `0xF6F20` calls `0x50220` on the view object `0x183A11DC0` (camera jump to that tile) and sets `0x183A11E48 = 1`.
- Other methods of the radar object (`0xF4C50`, `0xF4D40`, `0xF4DF0`, `0xF6FB0`, `0xF7210`) do not access `+0x70`.

## Classification

- Presentation and local camera input only. Nothing the simulation reads, so skipping or delaying it does not change simulation state or MP synchronisation.
- Output depends on every tick (unit positions, ownership colours, camera), so exact reuse is impossible.
- Paused ticks still cost 5.1–6.5 ms against 6.6–6.9 ms running (managed `EngineInterface.run` timing, 40 ticks/s, `test_dustyFPS2.sav`). This is consistent with presentation passes (`0xF5A40`, `0x41D60`) dominating the tick.
- Uncertain:
  - the exact meaning of `radar+0x48` (mode value read at entry) and of the frame-thickness thresholds;
  - per-map cost scaling (measured only on the maps above).
