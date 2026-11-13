# Long simulation ticks from AI group movement (FBCB9319) [Scope: Vanilla]

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

## Periodic AI group orders with 10,000 units, 2026-11-02 [Vanilla facts]

Evidence: a manual run on `test_stresstest5.sav` (800x800, 8 AI lords, 9,988 alive units) with PerfProbe's sampler (`SimOutsideRun`); MoatMove and BugfixesAndQoL were loaded. It covers 52 s of play after load.

- **Cadence:** Every 200 game ticks, one `EngineInterface.run` took long; the map ticks were 121907, 122107, 122307, and so on. Rendering, camera and hover continued (`framesDuring` 818-1084), so only the simulation stopped. AI group orders with up to 593 members fell into the same windows. This matches the per-AI scheduler `0x2AE40` issuing group orders through `0x11B520`.
- **Cost without a mod route:** such an order runs one `0xF4930` search per member; the sampled self time sits in `0xDA5EF`/`0xDAB1D`.
- Mod behaviour and the measured costs on this save: [VanillaOptimization.md, Gruppenbefehle großer Armeen](../../../../../VanillaOptimization.md#gruppenbefehle-großer-armeen-0203112026).

### Tribe member lookup and Assassin armies, 2026-11-02 [Vanilla facts]

Release runs on the same save at game speed 215 with Script Extender, APIShared and PerfProbe only (`ai-lag-se-1`; logs under `Testmods/MoatMove/.local/runs`).

- **Vanilla stalls on this save:** 24 runs over 100 ms, the longest 1.75 s.
  - Samples point to Assassin BFS `0xD9C40` and the ground searches `0xDA5EF`/`0xDAB1D`.
  - In several ticks `0x119F90` takes 46-71 %. That is the ordinal member lookup: it scans the 625 member words from the start on every call, and `0x11B520` walks all members through it about six times per order.
- **`0x119F90` contract (pseudocode and disassembly, high confidence):**
  - Register use: `rcx` tribe manager, `edx` tribe id, `r8d` ordinal.
  - Returns 0 unless `(short)tribe+0x40 == 2` and `0 <= i < (short)tribe+0x5C`. Otherwise it returns `word*16+bit` of the i-th set bit, or 0 when there are fewer set bits.
  - No call and no RIP-relative operand: the body is position independent. Writes only `rax, rcx, rdx, r9, r10, r11`; `rbx` is restored.
- **Assassin armies:**
  - An AI order to 1,148 Assassins costs one fresh `0xD9C40` per member in `0xF4930`, about 2.4 ms each. The group field built by `0x11B520` is never reused per member.
  - On a zero result `0xF4930` returns immediately (`LAB_1800f4a85`, no `0xE1640`).
  - **`+0x94` is a per-route output of `0xE1640` (high confidence, pseudocode):** `0xE1640` sets `DAT_1860AD6F4` (context `+0x94`) to 0 and counts the route tiles with flags `0x10000100`, without flag 2 and without a building whose type entry is non-zero. `0xF4930` then runs `0xDB650` (or `0xDA590`) and `0xE1640` mode 1 when it is non-zero, which recounts it. When `0xD9C40` finds no route, `+0x94` keeps the value of the previous `0xE1640` call.
  - Consequence: skipping `0xD9C40` for an already known route is not behavior-preserving for any decision that reads `+0x94` afterwards.
- **Path manager `+0xBC`:** `0xD9C40` increments it on every call. A scan of all executable sections found no RIP-relative access to `0x1860AD71C` and no manager-relative reader near manager loads. It looks like a statistic (medium confidence).
- Mod contract of the exact member lookup: [BugfixesAndQoL/UpdateToNewDLL.md, Tribe member lookup V14](../../../../../BugfixesAndQoL/UpdateToNewDLL.md#tribe-member-lookup-v14-2026-11-02).

### Assassin group moves, 2026-11-03 [Vanilla facts]

- **Assassin group move members (`0x11B520`, confirmed by pseudocode):** the Assassin branch builds its own `0xD9C40` flood from the move target (target `-1`, budget raised in steps of 5000), assigns formation slots from it (`0xE0970`/`0xE0AC0`/`0xE1D30`), sets context `+0x88` and calls `0x196280` per member with the member's slot. Every member's `0xF4930` then runs a fresh `0xD9C40` to its slot; the group flood is not reused.
- **Repeated orders:** `0x11B520` replans a member only when its formation slot or route state changed (`d34/d36 != b20/b22` or `b4e != 2`).
- **Cost per member:** each fresh `0xD9C40` expanded ~208,000 tiles before reaching the slot. With Script Extender only, PerfProbe counted 1,023 Assassin searches (`assassinSearches` = `+0xBC`) in 90 s. In the siege phase the AI re-issues `move/NoChange` to its ~1,100 Assassins every 6-8 s.
- **`+0x94` writers:** only `0xE1110`/`0xE1640` write it, so before or without them it holds an earlier route's value. Testing `+0x94` at `0xD9C40` time reads an earlier route's value (measured: ~78 % of a wave without weighted route).
- Mod contract of the group fields: [Pathfinding/UpdateToNewDLL.md, Group fields of Assassin group moves](../../../../../Pathfinding/UpdateToNewDLL.md#group-fields-of-assassin-group-moves-2026-11-03-contract-0x01000002).

## Enemy-distance scan kernel, 2026-11-13 [Vanilla facts]

This audit uses the same native hash stated above. The data/control flow below is
confirmed from the complete pseudocode and the 2,665 decoded bytes of RVA
`0x18C160`; its exported `FUN_18018c160` symbol remains unnamed. Evidence is
retained under `.work/a/stress90/001/native-full-0x*.json`,
`native-hot-0x18C160.json`, and `native-disasm-18C160.json` in the workspace.
Mod-installation measurements are separate evidence, not a Vanilla-only baseline.

- `DLL_RunTick` -> `0xCDE60` -> `0x182B00` updates units in ascending ID order.
  The ordinary `0x18C160(unitManager, unitId)` invocation is staggered using the
  unit mask at `unitManager + unitId*0x490 + 0xA94`, the native cadence global at RVA
  `0x37ED4D0`, and a per-unit cadence comparison value at the same
  slot-relative `+0x9DC` (read as 32 bits; its origin is not established here).
  `0x19A240` initializes the mask from the native
  type table at RVA `0x32BAD0`; common table entries are 63 or 127. This is not
  one unconditional full enemy scan per unit on every tick. The writer and
  increment contract of that cadence global are not established here; it must
  not be confused with the separately audited current-game-tick global.
- Military state handlers can additionally call `0x19A1F0`, which requests an
  immediate scan and publishes its distance and auxiliary output. The relative
  frequency of ordinary and forced scans is not established by this static audit.
- The scan reads a per-owner candidate ID array at RVA `0x38C19E8 + player*20000`,
  corresponding global IDs at `0x38ED908 + player*40000`, and a count at
  `0x379CE58 + player*0x583C`. Coordinates are read from the current unit slots.
  For each candidate it computes the integer distance approximation
  `max + (((min*2)/5)*min)/max`, with the original zero-distance branch and
  truncation order. The variable signed division is at `0x18C525` or
  `0x18C54C`, depending on the larger coordinate difference.
- Distance and nearest-distance accumulation occur before the candidate's
  global-ID and acquisition-range checks. Thus even a retained stale list entry
  can affect the distance result. Target acquisition has additional stance,
  order, cooldown, type, invisibility, load and weighted-score gates; defensive
  stance can use the stored home origin. An auxiliary minimum is published via
  `unitManager+0x1C` for the applicable unit-type branch.
- Acquiring a candidate can inspect eight neighboring cells through `0x6B910`,
  ask PCL reachability through `0xE2610`, choose an adjacent destination through
  `0x698E0`, and issue movement through `0x196280`. Failure/no-target branches
  also clear targets, update cooldowns or issue a return move. The distance
  return is capped at 32000; `0x182B00` publishes it at `GameUnit+0x2A2` for
  subsequent type-specific state handlers. Skipping acquisition alone does not
  make the nearest-distance result unused.
- `0x181340` builds each owner's list with alive/death, team, type and additional
  eligibility filters. `0xD1AD0` schedules player rebuild slots in a 200-step
  cycle (10..18, 60..68, 101..108, 160..168; slot 100 is excluded by a prior
  branch). Units move while lists persist.
  Initialization (`0xCF8D0`), load/legacy conversion (`0xD3380`, `0xD4820`,
  `0xD4970`, `0xD4A70`), transfer (`0x15C60`) and checksum (`0x22020`) paths
  also participate in the list's state contract.

Consequently, range-only enumeration, removing stale entries at read time,
lower scan cadence, or a spatial index updated only when the candidate list is
rebuilt are not proven result-identical replacements. They can change distance
outputs, reaction timing, target side effects or the live position observed.

## Enemy-scan cadence and integer contracts, 2026-11-13 [Vanilla facts]

Reference native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The installed canonical image was rehashed and matched CURRENT.json and the
record's binary_hash. The retained 2,665-byte `0x18C160` record has raw_hash
`53FF884138B941ECD72D0CAC182228680D3702B5D94B2A991DFFE2B8230C22D0`.
The statements below describe Vanilla state and arithmetic, not an implemented
optimization or an expected performance gain. They refine the open writer and
seed questions in the preceding audit without changing its historical text.

### Cadence writers and mask meaning

The cadence global at RVA `0x37ED4D0` is player manager RVA `0x366C210`
plus `0x1812C0`. `0xD1AD0` increments this DWORD at instruction `0xD1DAE`.
Its direct caller in the current semantic export is `0xCDE60`, which calls it
with that player manager after the `0x7E50`/`0x7EA0`, mode and pause-related
branch conditions, immediately after incrementing the separate global
`0x3665F58`. The later `0x182B00` unit update occurs under its own enclosing
conditions. These are conditional native paths, not a guarantee that every
managed run advances either counter. Reset `0xD1130` writes a zero QWORD at
manager+`0x1812C0` (instruction `0xD113C`); initialization `0xCF8D0` calls it.
This closes the earlier open question about these normal increment/reset paths;
it does not identify the counter with a separately named public game-tick API.

The native type table at RVA `0x32BAD0` supplies unit-slot `+0xA94` through
`0x19A240`. Its first 89 DWORD entries contain values 0, 63, 127, 191 and 255.
Entries for type IDs `0x4E` through `0x55` are 191 in this image. In the ordinary
scan branch `0x182B00` checks nonzero mask and
`(mask & cadence) == (mask & unitSeed)`; it does not use modulo mask+1.
Mask 191 (`0xBF`) tests the low six bits and bit 7 while ignoring bit 6.
Consequently, within a continuously incrementing 256-value counter cycle its
two matching residues have alternating gaps of 64 and 192 counter increments.
The mask's numerical value therefore does not imply a uniform 192-step period.
Forced scans through `0x19A1F0` remain a separate path.

### Seed provenance and score division

Spawn `0x17FEF0` sign-extends the WORD at VA `0x18856A6D2` into EAX at
`0x1800D2` and stores the DWORD into unit-slot `+0x9DC` at `0x1800D9`.
Normal pool initialization `0x8600` fills 20,000 WORDs from `0x1DECF4`;
that generator updates its 32-bit state by `state*0x343FD + 0x269EC3` and
returns `(state >> 16) & 0x7FFF`. `0x7530` copies a pool WORD into the source
at pool+2 and advances its pool index. Normal generated seed values are
therefore 0..32767 despite the signed load. The signed instruction alone is
not evidence that a negative seed is reachable. Save, transfer and other
writers have not been exhaustively closed for a universal seed-range contract.

In `0x18C160`, `0x18C69F` reads that seed as a DWORD. The addition at
`0x18C6AE` is 32-bit; the following signed remainder calculation preserves
truncation toward zero. `0x18C6CF` sign-extends its remainder before the live
DWORD read at `0x18C6D2` from RVA `0x3219C0 + remainder*4`.
The subsequent `IDIV` at `0x18C6DA` is distinct from the distance divisions
at `0x18C525` and `0x18C54C`. Only one of the two distance sites executes
for a nonzero-distance candidate; the score division is additionally guarded
by acquisition eligibility. Its divisor is chosen once before enumeration:
list counts 1..4 select 5, 5..14 select 2, and counts >=15 select 1.

Canonical file DWORDs at indices 0..9 are `0,50,100,150,200,250,300,350,400,450`.
The preceding nine DWORDs, addressed if the signed remainder is -9..-1, are
`-1,5,6,6,-1,6,7,7,-1`. This records the mapped arithmetic behavior for such
an input, not proof that a valid ordinary spawn reaches it. This entire region
is in writable `.data`; initialized bytes are not a runtime immutability proof.
The raw xref export identifies the read at `0x18C6D2`, but absence of another
direct xref does not rule out indirect writes or establish a cache lifetime.

### Distance arithmetic and observable ordering

Coordinates are sign-extended WORDs, so absolute component differences `m<=M`
are bounded by 65535. For M>0 the exact kernel is
`D = M + floor(floor(2*m/5)*m/M)`, with the original separate zero branch.
The maximum numerator is 1,717,934,490, below signed INT32_MAX; the quotient
is nonnegative. Thus `D>=M` holds under the actual integer arithmetic, and
when `floor(2*m/5)*m < M`, D equals M exactly. For m<=2 the numerator is zero.
These arithmetic facts do not by themselves make any candidate irrelevant.
The return minimum, optional auxiliary minimum and acquisition consume distance
under different conditions; acquisition range is inclusive `<=`. In the home
origin branch its distance is replaced with Chebyshev distance to that origin.

The approximation does not satisfy the triangle inequality: the distances
from (0,0) to (2,2) and from (2,2) to (4,4) are both 2, whereas the direct
distance from (0,0) to (4,4) is 5. It is also not Euclidean distance.
The caller's fine X/Y are captured before the function's possible preliminary
`0x196280` call. Candidate positions are then read during enumeration.
Weighted-score replacement uses strict `<`, so equal eligible scores retain
the earlier original list entry. The selected raw acquisition distance also
feeds the subsequent cooldown calculation. These are observable contracts
independent of the final nearest-distance cap of 32000.

### Exact source anchors

Paths below are relative to the workspace root. `S` denotes
`_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/exports/semantic-decompiled-functions.c`.

- S:119630-119635 and 119685: conditional `0xCDE60` counter/update calls; S:122048 reset, S:121089 its initialization caller, S:122493 increment. Canonical instructions `0xD113C` and `0xD1DAE` independently confirm write widths and offsets.
- S:219390-219403: mask/seed equality and ordinary scan dispatch; S:233967: mask-table initialization into unit+0xA94. Canonical table RVA `0x32BAD0` provides the quoted file values.
- S:217179-217180: spawn seed publication and pool advancement; S:5145-5150: pool WORD advancement; S:5946-5960: pool initialization; S:291849-291853: LCG and 15-bit result.
- S:224734-224735 and 224819: captured caller coordinates and preliminary move; S:224856-224862: exact distance expressions; S:224882-224907: auxiliary/home-origin/range branches; S:224912-224915: indexed score term; S:224976-225014: strict score selection and raw distance capture; S:225066-225073: cooldown; S:225088-225091: return cap.
- `.work/a/stress90/001/native-full-0x18C160.json` and `native-disasm-18C160.json`: full hash-bound function and all 567 decoded instructions, including the three IDIV sites and signed remainder/address construction.
- `_inspect/CrusaderDE-Native-Baseline/FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/sections.json`: writable .data mapping; the same directory's `xrefs.jsonl`:166230 identifies the score-table read. The quoted table and prefix values were independently read from the canonical PE's raw-backed .data at RVAs `0x32199C` through `0x3219E4`.
