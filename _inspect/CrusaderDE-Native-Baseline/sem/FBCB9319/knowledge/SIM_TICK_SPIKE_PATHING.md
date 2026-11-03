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

### Follow-up the same day: tribe member lookup and Assassin armies [mod integration]

Release runs on the same save at game speed 215: `ai-lag-base-1`, `ai-lag-se-1` (Script Extender + APIShared + PerfProbe only) and `ai-lag-fix-1..4`, logs under `Testmods/MoatMove/.local/runs`.

- **Vanilla alone stalls on this save too:** 24 runs over 100 ms, the longest 1.75 s.
  - Samples point to Assassin BFS `0xD9C40` and the ground searches `0xDA5EF`/`0xDAB1D`.
  - In several ticks `0x119F90` takes 46-71 %. That is the ordinal member lookup: it scans the 625 member words from the start on every call, and `0x11B520` walks all members through it about six times per order.
- **`0x119F90` contract (pseudocode and disassembly, high confidence):**
  - Register use: `rcx` tribe manager, `edx` tribe id, `r8d` ordinal.
  - Returns 0 unless `(short)tribe+0x40 == 2` and `0 <= i < (short)tribe+0x5C`. Otherwise it returns `word*16+bit` of the i-th set bit, or 0 when there are fewer set bits.
  - No call and no RIP-relative operand: the body is position independent. Writes only `rax, rcx, rdx, r9, r10, r11`; `rbx` is restored.
  - BugfixesAndQoL V14 now answers it exactly with a 64-bit word skip. Group order ticks with all mods went from 250-280 to 200-235 ms.
- **Assassin armies:**
  - An AI order to 1,148 Assassins (one tick, `vanillaCalls=1148`) costs one fresh `0xD9C40` per member in `0xF4930`, about 2.4 ms each. The group field built by `0x11B520` is never reused per member.
  - On a zero result `0xF4930` returns immediately (`LAB_1800f4a85`, no `0xE1640`).
  - **`+0x94` is a per-route output of `0xE1640` (high confidence, pseudocode):** `0xE1640` sets `DAT_1860AD6F4` (context `+0x94`) to 0 and counts the route tiles with flags `0x10000100`, without flag 2 and without a building whose type entry is non-zero. `0xF4930` then runs `0xDB650` (or `0xDA590`) and `0xE1640` mode 1 when it is non-zero, which recounts it. When `0xD9C40` finds no route, `+0x94` keeps the value of the previous `0xE1640` call.
  - Consequence: skipping `0xD9C40` for a found route is not behavior-preserving. BugfixesAndQoL's single-unit publication accepts a staged weighted route only while `+0x94 == 0` after `0xF4930`, so a skipped search would decide with a stale value. A trial that skipped it (0.9-1.4 s instead of 3.7-4.3 s per wave) was withdrawn the same day. A MoatMove trial that skipped its ground query for Assassin builds across regions (only measured as always unreachable, never proven) was withdrawn as well.
  - Remaining cost of such a wave with BugfixesAndQoL: one `0xD9C40` per member plus the weighted A* (`bfq_assassin_search`). In `ai-share-dd-1` every member had its own target and no start/target pair repeated (92 % of targets in one 8×8 cell, 78 % repeated starts); one weighted search expanded ~41,000 nodes.
- **Path manager `+0xBC`:** `0xD9C40` increments it on every call. A scan of all executable sections found no RIP-relative access to `0x1860AD71C` and no manager-relative reader near manager loads. It looks like a statistic (medium confidence).

### Assassin group moves with group fields (2026-11-03) [mod integration]

- **Assassin group move members (`0x11B520`, confirmed by pseudocode):** the Assassin branch builds its own `0xD9C40` flood from the move target (target `-1`, budget raised in steps of 5000), assigns formation slots from it (`0xE0970`/`0xE0AC0`/`0xE1D30`), sets context `+0x88` and calls `0x196280` per member with the member's slot. Every member's `0xF4930` then runs a fresh `0xD9C40` to its slot; the group flood is not reused.
- **Call counts (PerfProbe `assassinSearches` = `+0xBC`):** SE only 1,023 in 90 s; SE + Fixes + BugfixesAndQoL + MoatMove 4,450 in 60 s; all mods 7,156 in 90 s. With MoatMove the AI reaches the siege phase sooner and re-issues `move/NoChange` to ~1,100 Assassins every 6-8 s. Each fresh `0xD9C40` expanded ~208,000 tiles before reaching the slot.
- **BugfixesAndQoL group fields:** inside such a move command the member's `0xF4930` call is replaced by a route from one native backward field per command (exact descent towards the move target, then A* to the slot) and published through the single-unit frame without Vanilla's builder. `+0x94` is then not tested: its only writers are `0xE1110`/`0xE1640`, so before or without them it holds an earlier route's value. The frame used to test it already at `0xD9C40` time, which left ~78 % of the Assassins of a wave without a weighted route.
- **Effect (`grp-rel-1`, 120 s release):** stall time 59 → 18.4 s, longest tick gap 4.2 → 0.45 s. The remaining `0xD9C40` calls are mostly `0x11B520`'s own floods.
