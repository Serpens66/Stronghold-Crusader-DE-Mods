# AI hunter economy: contracts and prior test evidence

## Ownership and policy

Developer documentation for the migration into Improved Hunters on 2026-11-21.
The production owner is `src/AIHunterEconomyFeature.cs`; helpers live in
`src/AIHunters/`. The three independent host settings are `ControlAivHunters`,
`AutoBuildHunters`, and `RetireEmptyHunters`, under Improved Hunters' main gate.
There is no runtime dependency on the former standalone testmod or its private
reflection bridge. Current settings, corpse admission and chicken-service readiness
are read directly from the Improved Hunters runtime and public APIShared catalog.

One existing AIV hut counts as the baseline hut. Automatic supplementation creates
only a missing baseline hut, never additional huts. Chicken-only food permits one
hut; additional AIV huts require other huntable prey. The baseline is nearest to
the first own granary by squared tile distance, with the lowest global building
ID breaking ties; without a valid granary use the lowest global ID.

Chicken renewal can retain or permit that baseline between spawns when owned-chicken
hunting is enabled, `ChickensPerPlayer > 0`, the registered population handle is
enabled, the current catalog allows own-player chicken prey, and an own living
bakery, mill or granary exists. Editor mode does not assume renewal. This is
potential supply: native access checks and other population providers can still
prevent immediate spawning. It never creates prey or supplies resources itself.

Prey is map-wide, including registered raw unit types outside the Extender enum.
Own-player prey requires the registration's explicit permission. Only currently
usable free corpses count; reservations and local Hunter preferences do not exhaust
prey. Query failure suspends this mission's decisions rather than reporting absence.
AIV placement gating leaves humans alone and does not move the scheduled check.

## Lifecycle, scheduling and work limits

The static Improved Hunters runtime roots this controller, its public event
subscriptions, native delegates and builder for the process lifetime. Startup
`OnDestroy` does not dispose them. Mission lifecycle notifications reset all
mission-relative work and settings. Simulation work is invoked by the existing
Improved Hunters `SimulationTickEvents` callback; no additional tick/render polling
is introduced. Compact output is queued to owner-bound UI frame work for logging.

The first scheduled check is 200 map ticks after the first mission tick, then
4800 ticks between cycle starts. Reset also applies after loading a save with a
large restored tick. Pause stops the clock; roughly five seconds/two minutes at
speed 40 is a simulation-time description, not a wall-clock timer.

Performance contracts:

- One shared building traversal and one shared unit traversal collect all AI owners
  per cycle. They collect hut identities, possible spawn sites, prey masks and busy
  linked Hunters together. No full-world scan per player.
- Subsequent counts revalidate the compact hut index by one-based ID/global ID,
  type, owner and life state. Spawn events add new huts/spawn buildings to that index.
- At most one retirement attempt or one player's bounded placement search runs per
  tick. A build search tests at most 256 candidates and 64 native placement attempts.
- The search is deterministic, radius 32 in steps of four around the first granary.
  Its entire 3x3 footprint must be free, inside the map, on the keep-door component,
  and outside all captured AIV reservations. This conservative component condition
  can overlook valid reachable sites. No world path search or forced cleanup.
- One 67x67 boolean reservation buffer is reused, cleared and reanchored per eligible
  search. Mapper scales are cached at builder initialization, not reflected during
  simulation. No disk AIV decode or persistent geometry cache.
- If the captured AIV reservations overlap every footprint in the entire search
  raster (all 288 candidates, including those beyond the placement budget), suppress
  further automatic searches for that player until mission reset. Current occupancy,
  pathing, missing resources or an unavailable plan cannot set this exclusion. The
  exclusion is mission-local and recomputed after a save load; no persistent state.
- Retirement retains fresh whole-unit drain checks immediately before refund and
  after arbitrary refund subscribers. The busy-hut snapshot only skips known busy
  candidates; it never replaces these authoritative checks.

## Native contract

Reference `CrusaderDE.dll` SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The complete feature audit was performed for the testmod on 2026-11-15, with AIV
geometry added on 2026-11-19. This migration changes ownership and scheduling,
not native function semantics. Static evidence does not imply game/MP acceptance.
The [selected baseline](../../_inspect/CrusaderDE-Native-Baseline/CURRENT.md) and
[prepared-plan geometry](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_BUILD_RANGE.md)
retain the independent Vanilla evidence.

### Loaded AIV geometry

`0x55320` imports, `0x56670` rotates, `0x53D00` materializes the per-village
positions, `0x539B0` schedules and `0x51790` executes them. Resolve the owning
village through public `GameAIVManagerAPI` pointer/span APIs; do not assume player
ID equals village slot, copy the large `AivSystem`, read another on-disk variant,
or reuse the shared temporary rasters. Selection must be Usable/Perfect with native
rotation 0/2/4/6. `MaximumBuildStep` is inclusive: capture all populated records,
including built, abandoned and not-yet-unlocked sites. Unknown/malformed plans
prevent only automatic supplementation.

Ordinary anchors already represent final world minimum corners. Installed SE
`BuildingScales` was checked against all 440 named enum entries in native
`0x6A190`, selector `0x6A250` and RVA table `0x6A214`. Defined special commands
and zero-size markers clamp to one tile; unknown mappers prevent capture.
Grouped walls/crenels/woodwalls/pitch/moat contain ordered-buffer indices and
counts; resolve each absolute tile as 1x1, including moat (ordinary scale 2 does
not apply). Mapper 99 is pitch ditch; 98 is ungrouped killing pit. Validate ranges
against the populated ordered count and packed IDs against the public path grid.
The public accessor follows relocated Fixes buffers.

Additional component offsets are from tables `0x2D2E90` and `0x2D2EF0`, consumed by
`0x53D00`/`0x51790`; `0x72E10` confirms actual four-record barracks construction.

| Rotation | Three extra barracks/stockade 5x5 origins | Guild extra 5x5 | Oil-smelter extra 4x4 |
| --- | --- | --- | --- |
| 0 / South | (5,0), (0,5), (5,5) | (0,5) | (0,4) |
| 2 / East | (0,-5), (5,0), (5,-5) | (5,0) | (4,0) |
| 4 / North | (-5,0), (0,-5), (-5,-5) | (0,-5) | (0,-4) |
| 6 / West | (0,5), (-5,0), (-5,5) | (-5,0) | (-4,0) |

Stockade/wooden barracks/stone barracks use all three extras plus the base 5x5.
Engineers'/tunnelers' guilds use only the second extra plus the base 5x5.
Oil smelters use the listed extra plus base 4x4. Never rotate the base origin again.

### Construction and live-call ownership

Placement gating uses the public validation Post event. Automatic construction
uses ordinary public `CreatePrefab`, current APIShared construction costs and an
explicit affordability precheck (native construction itself debits costs without
that separate AI admission check). Synchronous building-spawn Post observation
captures the one-based ID; verify type/owner/global ID/origin and accept NeedsInit
or IsAlive. The native constructor is void: do not interpret its managed wrapper's
return as a success ID. An unblocked call without a confirmed spawn ends that
search to avoid duplicate construction/costs. No private construction hook is owned.

Retirement calls live entries so existing SE refund hooks and other consumers run.
It installs no hook and writes no executable bytes. Register three one-byte
`NativeSiteMode.CallOnly` entries under
`ImprovedHunters_Serp/ai-hunter-retirement-calls`. Use the caller's shared native
identity result; never hash the native file again for this backend. Original code
checks use `CrusaderLibraryLoadContext.Memory`, never a copied live probe.

| Purpose | RVA | Decimal length | SHA-256 of complete original function bytes |
| --- | --- | ---: | --- |
| AI retirement branch | `0x3B270` | 229 | `EFF1F3C1FB0BB922746F17F266D62EAA252E6FF7C64610EF50856EA2833AAD9D` |
| Coarse-cell avoidance | `0x55E10` | 109 | `84F5ED0A91B52B6FD62CB4B56F35A5EB320BAB9B4A1F18E9603B89C2208DB828` |
| Building refund | `0xC4480` | 1320 | `C1E4DC936740B9D35117AC1539BC0A07D48EF212F61CE57778DC2A82AA8445D7` |
| Removal/group marking | `0xC4290` | 257 | `713BF8FC70A1320F9EA7BB8B1CEBC82F0E52211C78A41D1DA3286EA5E24B0E56` |

The ranges contain no PE base relocations and must fit executable PE sections.
After validating the whole branch, derive `instructionRva + length + disp32`:

| Instruction | Original bytes | Length / operand offset | Derived reference |
| --- | --- | --- | --- |
| `0x3B27F` | `48 8D 35 2A 19 49 06` | 7 / 3 | building manager `0x64CCBB0` |
| `0x3B30E` | `48 8D 0D 5B EC 46 03` | 7 / 3 | AIV state `0x34A9F70` |
| `0x3B31E` | `E8 ED AA 01 00` | 5 / 1 | avoidance `0x55E10` |
| `0x3B331` | `E8 4A 91 08 00` | 5 / 1 | refund `0xC4480` |
| `0x3B33B` | `E8 50 8F 08 00` | 5 / 1 | removal `0xC4290` |

Data roots must fit readable/writable `.data` (`0xC0000040`). Before each removal,
the derived building manager must match the public manager pointer. Call order:

1. `0x55E10(aivState, originX, originY)`: temporary coarse-cell avoidance, not AIV
   progress reset. Cell is `x/5 * 160 + y/5`, stride `0x30`; removal-count byte
   `root + cell*stride + 0x5B847`, delay byte `+0x5B843`. Old count below 10 is
   incremented and delay becomes `3*oldCount+10`; otherwise delay becomes 10.
   Grid refresh ages the delay.
2. `0xC4480(buildingManager, oneBasedBuildingId, ownerPlayerId, 50)`: native
   integer construction-cost refund. Mode 1 gives no refund; mode 6 uses 100.
   Entry subscribers can modify it, so the argument is not an unconditional 50%.
3. `0xC4290(buildingManager, oneBasedBuildingId)`: marks alive state 3 and removal
   flag. Reject nonzero demolition groups at public building `+0x2A8` to avoid
   removing linked buildings. Recheck identity and drain after refund subscribers;
   partial failure permanently disables this backend to prevent repeat refunds.

Successful marking is not terminal cleanup. Normal `0xC60F0 -> 0xB8310` later
clears the record and releases workers. The original AI failure counter and
owner/control flags are not forged to invoke its ordinary retirement branch.

### Record and drain invariants

Public Building stride/size is `0x32C`, pointer `manager + id*0x32C + 0x5C`;
public Unit stride/size is `0x490`, pointer `manager + id*0x490 + 0x65C`.
Public spans begin at game ID 1: array index is `id-1`. Validate sizes and all
consumed offsets with Marshal before retirement becomes available.

| Record | Offsets | Contract |
| --- | --- | --- |
| Building | `0x26`, `0x2C`, `0x118`, `0x150` | cycle mode, progress, animation stage, hut meat: all zero |
| Building | `0xD0`, `0xD2`, `0xD6`, `0xD8` | alive state, type, owner, nonzero global ID |
| Building | `0xEE`, `0xF0` | origin X/Y |
| Building | `0x19E`, `0x1A8` | first worker ID and global ID: Hunter identity |
| Building | `0x2A8` | demolition group: zero |
| Unit | `0x88`, `0x8A`, `0x94` | alive state, type, global ID |
| Unit | `0x2BC`, `0x364`, `0x384` | AI state, linked hut global ID, carried goods |

`0xAE570` verifies the first worker pair. The second pair `+0x1A0/+0x1AC` is
the hound (type `0x34`), whose carry field is an animation counter. The first
worker must be alive, Hunter type, correctly linked by both identities, empty,
and in idle state 0/7/8. Every other extant linked Hunter is checked as well.
Reused or ambiguous slots wait; hound animation does not block an empty hut.

Full `0x12FC70` state audit is required before changing this predicate. States
1/9/10/11 hunt/approach corpses; 2 returns a carcass; 3/109 process it. State 0
with hut meat enters state 4 and subtracts six via `0xB6100`, good `0xC`.
Only a successful granary route changes state 4 to 5 and sets actual carried
amount via `0x18D940(hunterId,6,1)` at `0x1309FE`. State 4 can therefore own
meat while both visible meat fields are zero. States 4/5 wait even with yield 0.
Delivery uses `0xBF9F0`, `0xC90E0`, `0xC9080`, `0xBD470`, `0xB6100`; return
state 6, state 110, death and unknown states also wait. No timeout discards meat.
`0xB6240(...,2)` clears progress/stage but briefly leaves cycle mode 2 until
later building updates. Unit `+0x300` is productivity remainder, not inventory.
IH/APIShared yield changes do not invalidate this deferred-cargo protection.

### Native scheduling and update gates

The audited public tick publisher's `FF 87 ?? ?? ?? ?? 33 DB` uniquely matches
`0xD1D4F` in `0xD1AD0` (`[0xD1AD0,0xD1E4F)`). Its sole native caller
`0xCDE60` increments map tick, calls it, then performs building update `0xC60F0`,
AI/AIV `0x57330`, and unit passes `0x182B00`/`0x187B30`. Earlier helpers finish
before the callback; no HunterUpdate is suspended there.

A changed native hash disables retirement and geometry-dependent automatic
construction. There is no pattern fallback: finding these callees cannot prove
worker headers, state machine, compound tables, delivery failures or update order.
Re-audit that complete chain, PE ranges/relocations/anchors, record writers/readers,
all Hunter transitions, terminal cleanup, tick location and SE/Fixes/APIShared
live ownership together. Keep live refund events and CallOnly ownership. Then run
policy/native-resolution checks and separate map/save/editor/MP acceptance.

## Historical evidence retained from the standalone testmod

The following records are historical observations, not validation of the migrated
Improved Hunters binary. Their source documents at migration had these identities:

- `Testmods/AIHunterPreyTest/TESTING.md`: SHA-256 `E5215462BC2649464934F276010E5A190A5233518736F2DDF7D05BA61FFF9914`.
- `Testmods/AIHunterPreyTest/UpdateToNewDLL.md`: SHA-256 `48A96F999F593AB4C89288ECAB0004A32DB0631A6CD3FD9BAC8FE716B82B0E4A`.

| Recorded test date | Recorded result and limitation |
| --- | --- |
| 2026-11-15, 12:18 | Full package build without installation; 49 prey/drain checks and 88 focused preset checks plus runtime/dependency/ID/tooltip source checks. APIShared 0.54.0, IH 1.2.2; checked DLLs matched local outputs. Installation awaited closed game. No game launch or game/MP acceptance. |
| 2026-11-19, initial refresh | 76 prey/hut/drain and 88 preset checks passed against installed APIShared 0.57.0 with targeted MSBuild and disabled workspace build targets. Ordinary driver was then blocked by `Incomplete or mixed bootstrap pair`; no foreign package changes, installation or game test. This temporary package condition is not a current requirement. |
| 2026-11-19, AIV area protection | Ordinary build again succeeded; 1322 behavioral and 88 preset checks. Installed with game closed and SHA-256 compared for all four package files. DLL `2AEDE3B73A80C09652D7948BDDB6ED0900488435E3705EE4DA73B2367BE4C16F`. No in-game acceptance of AIV protection. |
| 2026-11-20, initial delay | 1326 behavioral and 88 preset checks passed; ordinary no-install build succeeded. Installation stopped because game was running. No in-game acceptance of initial delay. |

The old installation identity was `AIHunterPreyTest_Serp` (testmod 0.1.0, SE
minimum 2.14.0). Its optional IH settings/corpse reflection bridge and its separate
four-switch settings document are superseded by direct integration. Do not install
that old package together with the production owner. Historical standalone log
markers included `AI_HUNTER READY`, `SESSION`, `SCAN`, `BUILD_BLOCKED`, `AUTOMATIC`,
`AUTOMATIC_WAIT`, `RETIRED`, `RETIREMENT_UNAVAILABLE`, `RETIREMENT_DISABLED`,
`PREY_UNKNOWN`, and `FAILED`; those names alone do not prove native completion.

Production-source CLI coverage is in `tests/AIHunters/`; use the current Improved
Hunters build/verification entry points. Old standalone package instructions are
not retained as active commands. Production results must be recorded separately.

## Remaining game acceptance scenarios

Check all three switches independently and preserve human behavior:

1. No wildlife, admitted mod prey or renewal: no automatic hut, gated AIV order
   waits; disabling only AIV control restores the ordinary AIV decision.
2. Prey and a lord with no hunter frame: one supplement with normal costs and
   prerequisites; disabling automatic build prevents supplementation.
3. Only rabbits, camels, chickens or deer: current IH hunting admission controls
   the decision, including disabling the sole present species before another game.
4. Last prey hunted: retain the hut through carcass return, processing and delivery.
   Full/unreachable granary can defer indefinitely; remove only after drained.
5. Only usable free corpses: retain admission; consumed/fading corpses expire it.
6. New prey after shortage: permit rebuilding after a subsequent check. Repeat
   after save/load and map change; reset the 200-tick delay from the loaded clock.
7. Only chickens with multiple AIV orders: keep one baseline; other huts finish
   work before removal. Multiple orders within one interval must count fresh spawns.
8. Temporarily no chickens but renewal enabled: retain baseline. Disabled chicken
   hunting, target zero, missing service/spawn site, or editor without actual prey
   must not invent renewal. Other population providers need separate observation.
9. Future loaded AIV squares, walls/moats, rotated compounds and final build phases
   remain protected while adjacent free squares work. Include all four rotations,
   save/load and active-plan selection. Existing huts are not moved by this rule.
10. Verify continued simulation after startup cleanup and bounded cost under many
    huts/units. Compare disabled/enabled runs; builds and pure tests are insufficient
    proof of in-game performance. Multiplayer admission/synchronization is pending
    separate matching-peer acceptance.

## Final review: natural AI elimination (2026-11-21)

The proposed additional WinLoss/Lord-alive construction guard was reviewed and
rejected for the ordinary native Lord-death path. `IsAIPlayer` indeed only reads
the retained AI lineup; that alone is not an alive-player contract. However, the
existing builder also requires an owned IsAlive keep and IsAlive granary.

Full relevant paths were read on the reference hash: simulation `0xCDE60`,
player result update `0x1D160`, AI scheduler `0x57330`, Lord state machine
`0x15A950`, elimination `0xCD190`, building traversal `0xC3F10`, synchronous
marking `0xC4290` and normal building cleanup `0xC60F0 -> 0xB8310`.
The Lord's terminal state-0x6E pass reaches cleanup after its counter reaches 33.
It marks the Lord and, in the same synchronous pass, calls
`0xCD190 -> 0xC3F10 -> 0xC4290` for every nonempty owned building, including
keep and granary. Their life state is immediately MarkedForDeletion (3), before
a subsequent simulation tick can attempt automatic construction. Slot deletion
may be deferred; the builder's strict IsAlive checks already reject the records.
The earlier death animation is distinct from this completed elimination, and the
native AI's own paused/result gates remain in `0x57330`.

`0x1D160`'s periodic Lord-reference check compares stored ID/global identity,
not merely the start of a death animation. Retained AI lineup therefore does not
establish a post-elimination construction opportunity. Direct current-PE decoding
also confirmed calls `0xCD1AD -> 0xC3F10` and `0xC3F71 -> 0xC4290`.
No new guard or per-tick defeat observer was added from this candidate. This proof
covers ordinary native elimination; scripts or mods that only write WinLoss.Loss
without running native world cleanup are a separate, unconfirmed scenario.
No runtime source changed during this read-only countercheck.
