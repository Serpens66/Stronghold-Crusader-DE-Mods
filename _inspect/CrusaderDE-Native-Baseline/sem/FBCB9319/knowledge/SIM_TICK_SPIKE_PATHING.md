# Long simulation ticks from AI group movement (FBCB9319)

Native DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. The addresses below are RVAs for this binary only.

Evidence: PerfProbe DEVDIAG stack sampler (`Testmods/PerfProbe/src/PerfProbeSimSampler.DevDiag.cs`). It suspends the simulation thread about once per millisecond during an `EngineInterface.run` that has run for 80 ms or longer, then unwinds the thread with the x64 unwind tables. Frames are reduced to `RUNTIME_FUNCTION.BeginAddress`, so a value can be the start of a chained fragment rather than a whole function. Observed runs on 2026-10-20 with `test_dustyFPS2.sav` (runs R2 06:33:07 and F1 06:50:27): 390 and 388 samples in single ticks of 483 and 480 ms. No hook was involved.

- 373 of 390 samples share the outer chain `0x86680` (`DLL_RunTick`) > `0xCE099` > `0x5743D` > `0x2AE40` > `0x3C36B` > `0x3D70B` > `0x11B520` > `0x196280` > `0xF4930`.
  - `0x2AE40` is the per-AI-player scheduler (see `RAID_RETARGET.md`, `FIXES_WHEAT_STARTUP.md`).
  - `0x11B520` is the tribe coordinate movement order (see `ASSASSIN_DIRECT_GATEHOUSE_CLIMB.md`).
  - `0x196280` → `0xF4930` builds the per-unit route; `0xF4930` uses search budgets up to 400000.
- Leaf distribution: `0xD86F0` (47 %), `0xDB650`, `0xDA5EF`, `0xE4A1A`, `0xD9C40`, `0xDAA50`. These are pathfinding internals called from `0xF4930`; `0xD90D0` is the confirmed `c_game_update_pathfinding_for_tile_and_neighbors3x3`.
- The cost of such a tick is therefore one AI group order: one route search per unit, each potentially up to the search budget. The renderer frame loop is not involved: it accounts for 3 of 390 samples (`0x86680` > `0x41D60`). Rendering continues during such a tick (`framesDuring` 86–91). The main thread only blocks if it waits for `EngineInterface.threadLock`.
- Timing is driven by AI scheduling, not by zoom or load. Earlier runs on another save showed these ticks every ~25 s. In the automated runs it occurred once, about 4 s after unpausing.
- Uncertain: the exact AI military routine around `0x3C36B`/`0x3D70B` (sampled addresses are fragment starts), the number of units per order, and whether individual searches exhaust their budget.
