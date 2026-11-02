# Long simulation ticks from AI group movement (FBCB9319) [Scope: Vanilla + mod integration]

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

## Managed measurement boundary clarified, 2026-10-22

Installed `Assembly-CSharp.dll` SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789` was inspected
using its actual method IL. `EngineInterface.run(bool)` calls
`MemoryBuffers.getFreeBuffer(true)` before entering `threadLock`; a missing buffer
returns zero without calling `DLL_RunTick`. Thus completed managed run calls are
not a native-call count or proof of advanced simulation ticks. The bool argument
must be retained as context rather than interpreted as evidence of advancement.

The single Monitor scope contains `preDLLCallActions`, native argument preparation,
`DLL_RunTick`, deserialization, `CopyPlayStateStruct`, and `returnBuffer`, then releases
the monitor in a finally block. `GetMeritData` and `SetAchData` use the same static
lock. Managed Monitor waiting, pre-native work, the inclusive native call (including
SE/mod callbacks), and post-native publication must be measured separately. This
clarifies managed observation boundaries; it does not add a native address or
attribute every future native-time spike to the AI movement path observed above.

## Periodic AI order spikes with 10,000 units, 2026-11-02 [mod integration]

Evidence: a manual run on `test_stresstest5.sav` (800x800, 8 AI lords, 9,988 alive units) with MoatMove (anchor mode, DEVDIAG build), BugfixesAndQoL and PerfProbe (sampler with `SimOutsideRun`). It covers 52 s of play after load.

- **Cadence:** Every 200 game ticks, one `EngineInterface.run` took long; the map ticks were 121907, 122107, 122307, and so on. Rendering, camera and hover continued (`framesDuring` 818-1084), so only the simulation stopped. Mods logged AI group orders with up to 593 members in the same windows (`anchor-move-unreached tribe=... members=593`). This matches the per-AI scheduler `0x2AE40` issuing group orders through `0x11B520`.
- **While MoatMove's anchor masks were still being built:** The masks were built at 4,096 nodes per tick, about 28 s for 8 players. In this phase, group orders fell back to one Vanilla search per member.
  - The three ticks took 3,651, 2,746 and 2,943 ms, with `maskPendingMoves` at 33, 11 and 42.
  - Sim-thread samples: about 62 % self in `0xDA5EF`/`0xDAB1D` under BugfixesAndQoL's wrapper of the `0xF4930` original. About 25 % were in BugfixesAndQoL's `edgeHooks[index]` stubs at RVAs `0xDA783` and `0xDACB2` inside that search loop, mapped by stub address and size from the SE log. So in this configuration a fallback order costs several times the pure Vanilla figure above, and the edge hooks are a notable share of it.
- **After all masks were ready:** about 240-290 ms per 200 ticks, mostly MoatMove's own anchor work for the AI orders.
  - Sampled: `mma_query`, managed `LiveEdge`, `TryAuditFallbackPathCore`, BugfixesAndQoL `TryGetTraversalEdge`, `HashSet<int>.Contains` and native `0x119F90`.
  - DEVDIAG and the 1 kHz sampler inflate this, so a Release measurement is still pending.
- **Consequence in MoatMove (same day):** The map's first player scan now completes all requested masks in one tick, during loading. Later requests keep the per-tick budget. Uncertain: the remaining Release cost per 200 ticks, and the edge-hook share on runs without MoatMove's anchor mode.
