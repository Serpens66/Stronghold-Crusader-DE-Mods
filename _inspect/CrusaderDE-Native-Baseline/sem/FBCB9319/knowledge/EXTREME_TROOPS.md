# Extreme Troops: unit and projectile pools, trails and outposts [Scope: Vanilla + mod integration]

Static audit, 2026-11-22. Installed native SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, managed SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`. Both match CURRENT and the published semantic identities. Addresses below are RVAs (preferred VA = RVA + `0x180000000`). Confirmed-static claims concern specific data and branches. Unnamed functions retain their existing baseline confidence.

## Setup, persistence and bounds

| Native value | Ordinary | Extreme Troops |
|---|---:|---:|
| Unit allocation exclusive bound, signed dword RVA `0x3668E34` | 3000 | 10000 |
| Projectile and effect allocation exclusive bound, signed dword RVA `0x3668E38` | 3000 | 6000 |

- Skirmish/lobby setup encodes `MultiplayerSetupData.extreme_troops` in its settings string. `EngineInterface` copies it to the native transfer struct as a signed 32-bit field at `+0x48`. Export `DLL_ApplyMultiplayerSetupData` at `0x81050` forwards it to `0x26370`. Zero selects 3000/3000, any nonzero selects 10000/6000. Reverse export `0x84280 → 0x20420` reconstructs the field from the unit bound > 3000. The frontend consumes it for lobby icons and the optional Extreme warning. Extreme Powers has its own neighboring transfer field at `+0x4C`.
- Free Build sends a separate Boolean from `FRONT_StandaloneMission` and its restart record. `DLL_PreInitMap_JustBuild` at `0x86120` stores argument 13 (low byte, zero-extended) at RVA `0x87EE2E0`. `0x8AE80` upgrades both bounds when that dword is nonzero. `0x87D10` resets the pair and the Free Build dword. `0x94350` captures the preexisting unit-bound predicate, later resets the pair and reapplies it, with its own built-in trail branches.
- Save writer/reader `0x52B0/0x87D0` and matching state transfer `0x15C60` serialize the pair as two dwords. The archive reader defaults both to 3000 and contains a separate legacy-record branch that sets 10000/6000 and the legacy-format flag at `0x3668E30`. That format flag has readers of its own and is not set by toggling Extreme Troops in the lobby. `0x22020` includes both bounds in the native checksum. A live change to either bound can affect state compatibility and allocator assumptions.
- Unit allocator `0x17FEF0` scans IDs from 1 through bound minus 1 (2999/9999 slots), and returns zero on exhaustion. The snapshot writer `0x19D960` sends bound minus 1 to the managed side. Per-player quotas at `0x37EF950/0x37EF954` derive separately from the unit bound via `0xCBD90`. With native mode 0 or zero qualifying slots both quotas equal the bound. Otherwise both start at integer `(bound - 100) / qualifyingSlots - 40`. In the built-in trail/skirmish branch (mission mode 3, trail flag 1, network mode 99, more than two qualifying slots), the second quota doubles up to half the bound only if the bound differs from 10000. See [unit load and placement](UNIT_LOAD_PLACEMENT_CONTRACTS.md) for quota consumers, allocation failure and separate group limits.

## Flying projectiles and their trails

The mode raises the shared native pool limit and suppresses secondary projectile effects at creation. The relevant path lives in the simulation tick.

1. Main projectile creation at `0x9B2B0` uses manager RVA `0x2F7A680`, record stride `0xE8`, state word at manager + `id*0xE8+0x3C`, and scans against the projectile/effect bound. A few special types use dedicated low slots. At exhaustion some types try to reclaim eligible records before returning zero. Machine reads at `0x9B36A` and `0x9B40C` are allocation limits. The bulk update at `0x9F960` walks the used range in manager +4.
2. Initializer `0xA1DB0` copies the afterimage kind from the dword table at `0x2DB040` to a word at record +`0xBA`. Default kind 3 occurs on launch types `01,02,03,04,07,08,14,17,18,19,21,22,24,25` (hex). These include ordinary archer arrows, crossbow bolts, siege rocks, ballista bolts, slinger stones, thrown cattle and other projectiles. The creation path remaps some launch types after initialization and can force afterimage kind 1 at `0x9B9D6`, including subtype branches such as the Arabian ballista launch type `0x25`.
3. The ordinary flying branch in `0x9F960` checks a live/moving-state path, nonzero trajectory value at `+0x8C`, nonzero afterimage kind and zero stop word at `+0x80`. Kind 3 calls `0x9BFC0` at `0x9FED0`, passing type 6, the current projectile coordinates/elevation and its sprite data. Other nonzero kinds call `0x9BE80` at `0x9FEAE`, passing type 5 and the afterimage kind as the sixth argument. The type numbers correspond to Extender vocabulary `AfterImage` and `Steam1`.
4. At the larger pool bound, `0x9BFC0` returns zero before allocation for kind 3, and `0x9BE80` returns zero unless the kind is 1. At the ordinary bound, both reach the regular allocation checks. Success claims an independent slot, increments shared creation serial `0x3665F84`, uses state 5 then 6, links the effect via `0x9C730`, and initializes its position/sprite state. The type-5/6 dispatch targets `0x99110/0x99170` update the short-lived effect and eventually mark state 3 for removal by `0x9F2D0`. The renderer at `0x41D60` consumes the tile-linked records and publishes sprite submissions via `0x1A13C0`.
5. In both cases the caller continues with its own projectile, including main flight update `0x9EF20` when the stop word is zero. Neither return value is consumed. The mode does not directly choose a new arrow graphic, ballistic table, hit rule or damage value in this path. The larger pool can affect whether shots can be allocated under saturation. No image capture or game comparison was made to identify the exact pixel appearance of the missing trail.

### Fire arrows and the appearance of the trail

The European archer update at `0x13F540` checks the brazier predicate `0xA0310` before launching. A normal arrow uses launch type `0x18` for a tile target or type 1 for a unit target. An ignited arrow uses `0x5C` or `0x5B` respectively. The projectile creator `0x9B2B0` normalizes both ignited variants to archer arrow type 1, gives them sprite file `0x91` (`GM_ANIM_MISSILE_FIRE`, loaded as `body_missile_fire`), and overrides their afterimage kind to 1 at `0x9B9D6`. Kind 1 emits a type-5 effect using file 125 (`GM_BODY_STEAM`, loaded as `body_steam`). `0x9BE80` permits kind 1 at both pool bounds, so both the flame on the arrow and its gray smoke remain in Extreme Troops. The testmod identifies these ignited arrows by their sprite file and leaves their existing presentation alone.

For an ordinary kind-3 arrow or bolt, the separate type-6 record keeps the source projectile's current sprite file and frame at the position where it was spawned. Its own age drives transparency, published to `GameMap.addUpdateFly`. The Standard effect is a fading copy of the projectile sprite at a previous flight position. The player's paired Vanilla fire-arrow captures on 2026-11-22 show ignited projectiles and gray smoke in both configurations. The player's later comparison of regular arrows shows the visible sprite copies in Standard.

## Other direct simulation effects

- **Outpost troop groups.** The shared type 2/106/107 handler at `0xABB90` tests unit bound > 3000 and signed native trail type (RVA `0x3669048`) < 11. When both tests pass, `0xAC4F4` selects a lower floor for the group-wait threshold (100 vs. 600). The base is `2000 - signed group acceleration`, with a prior floor of 400 for native modes other than 0 and 99. At group completion `0xACD91` adds 100 to group acceleration instead of 33. These are group timing changes. The individual spawn acceleration increment at `0xACDC3` stays +4. This predicate also matches ordinary non-trail contexts with native trail type 0. See the [outpost timing audit](OUTPOSTS.md#macemen-timing-audit-closure-2026-09-17).
- **Internal per-player counter.** The periodic updater `0xCDB20` (called from `0xCDE60`) increments a signed dword at manager + `0x137DD0 + player*0x583C` up to 7000, with existing gates for editor and session/network state. Under the ordinary unit bound, `0xCDD97..0xCDDC5` additionally requires signed simulation tick % 3 == 2. Extreme Troops bypasses that modulo gate and increments on each eligible update. The gameplay meaning and downstream consumers of this field remain unconfirmed.

## Evidence inventory

Cross-checked the installed PE by decoding direct RIP-relative references in executable code and following the verified setup, save/restore, native update and rendering paths. The targeted scan matters because the semantic pseudocode omits the two outpost reads and both main projectile allocator reads. The reference instruction inventories below allow a repeat scan and comparison without trusting pseudocode completeness.

- `0x3668E34`: `593B,8D6E,9140,955A,15D7D,15FC2,20873,2257B,26B7E,26B94,87D3F,8B0FF,948A9,96D91,96E09,AC4F4,ACD91,CBD9C,CBDA8,CBDF3,CBE37,CBE4C,CBE6A,CDD97,17FEF8,1A0FB4`.
- `0x3668E38`: `594F,8D78,9151,9564,15D97,15FF2,2256F,26B88,26B9E,87D56,8B109,96D9B,96DFF,9B36A,9B40C,9BE8F,9BFCF`.
- The inventory covers direct code references to the bound pair, including save/transfer address passing and the 64-bit default store at `0x8D78`. It is not a proof that arbitrarily indirect consumers or emergent load-dependent effects are absent.

## Mod options [mod integration]

- A client-side option for the appearance of flying shots can emit short-lived visual trails from the render data of surviving projectiles, retaining Extreme Troops allocation and the simulation's normal effect records. Use stable render identity and positions, with a bounded render budget. Counter-test normal versus Extreme and duplicate trails for kind-1 variants. Actual renderer timing, appearance and high-density cost would need validation.
- Restoring the native trail producers is also feasible. Keep the projectile bound at 6000 and change the suppression decision locally at the two producers. Their normal path allocates in the same pool as gameplay projectiles and changes the shared creation serial and tile lists, so treat that implementation as simulation-affecting. In multiplayer use the same policy on all peers. In particular, a request to change only visuals does not justify lowering the global bound to 3000 to re-enable trails.
- A different player quota or outpost group timing can be changed at its specific consumer. Raising the physical unit/projectile bounds beyond the audited Vanilla maxima needs a separate allocation, consumer, archive and transfer audit. The published setup bit reports whether the unit bound exceeds 3000 and cannot round-trip an arbitrary custom bound through that field.
- No mod, live patch, hook span, game run or multiplayer acceptance is claimed by this audit.

### Unity flight trail prototype (2026-11-22) [mod integration]

CastlePlanner's [ground-marker renderer](../../../../../CastlePlanner/src/BlueprintRenderer.cs) demonstrates a Unity mesh per depth row using the formula `-20000 + row * 49 + 1`. Projectile sort order is a different position in that row. On the confirmed managed assembly, `Director.Update → GameMap.processTestMap` consumes kind-3 records. An update forwards the render ID, projectile kind, signed height and sprite fields to `GameMap.addUpdateFly`; zero sprite file emits a deletion. The public ID deletion forwards to `deleteFly(Fly,bool)`, which also receives the map clear path. The existing `Fly` and its sprite renderer own the projected coordinates, sorting layer and row order (`-20000 + row * 49 + 15`). Render ID `slot + 10000` is a shadow variant. Original launch subtype `0x5B` becomes an archer arrow with kind-1 trail and sprite file `0x91` in the native creator (`0x9B3D4`, `0x9B821`, `0x9B9D6`).

The [ProjectileTrailsTest](../../../../../Testmods/ProjectileTrailsTest) prototype samples the existing body sprite after managed publication. It uses its sorting layer and full order as the batch key and adds a local transparent segment from two observed positions. All groups share one material and each group has a reusable mesh. Trail geometry lives in Unity and uses zero native slots. Visual occlusion and performance acceptance require in-game comparisons.

Hidden-desktop tests on 2026-11-22 confirmed geometry submission in all four rotations, persistence during pause, and zero mod segments after a same-process switch to a Standard Troops save. A separate group of 24 spawned archers and eight crossbowmen produced 24 source-matched segments at the paused capture. Two alternating stress pairs measured roughly 55–58 additional draw calls per frame at up to 1,024 simultaneous segments in the original line renderer. Individual wall, tower and gate overlaps and in-game allocation bytes remain unverified. See the [test results](../../../../../Testmods/ProjectileTrailsTest/TESTING.md#first-prototype-desktop-test-on-2026-11-22).

The player's screenshots showed that the original line was hard to discern even when the renderer reported geometry. Testmod 0.1.1 stores the actual Sprite geometry, texture and basis from each of up to three preceding observations and draws fading copies locally. Batches share materials by texture and preserve each historical sprite's sorting order. The 1,024-copy and 64-batch budgets give first priority to each shot's newest copy.

## Afterimage cadence and lifetime

- In the projectile sweep at `0x9F960`, the kind-3 call at `0x9FED0` passes signed fine X, fine Y and height from record `+0x4C/+0x4E/+0x50`, file from `+0x1A` and frame from `+0x14`. It runs before the same projectile's movement at `0x9EF20`. The branch has no periodic emission throttle. The tick modulo ten near the sweep entry at `0x9F970` resets the allocation cursor to 25 and does not skip emissions.
- For regular arrow and crossbow sprites, initializer `0xA1F60` uses an interval of 3 at `+0x24` and a terminal age of 3. The cow sprite file `0x87` uses an interval of 1. The allocator clears both timer accumulator `+0x26` and age `+0x28` and immediately dispatches the type-6 updater `0x99170`. At ages 0, 1 and 2, it stores truncating integer transparency `age * 32 / 3` (0, 10, 21). The managed SpriteRenderer applies `(32 - transparency) / 32`, resulting in 100%, 68.75% and 34.375% opacity.
- Each eligible visit to a type-6 slot advances its timer. If the allocation lands above the source slot, it can get its first increment in the same sweep. Otherwise it starts incrementing in the next sweep. On reaching age 3, `0x99170` sets state 3 and retains the last transparency. The next visit removes the record through `0x9F2D0`. The static shorter/longer traces span 10/11 slots in time including the birth step and the terminal state. Tile clipping, render visibility and the allocation pool can reduce the number actually seen.
- Tile linkage `0x9C730` uses fine coordinates divided by 8. Subtile projection `0xA01D0` indexes the two signed words in the table at `0x2DB520` and `0x2DB524` by the low three bits of each coordinate, mirroring X at rotations 2 and 4 and Y at rotations 4 and 6. The ordinary file-34 sprite branch in `0x41D60` publishes its stored height and subtile offsets and layer delay 1 to `0x1A13C0`. Managed `GameMap.mapGameTileToTilemapCoord`, `getSpritePosVector` and `getRow(x, y, 1)` then determine world position and order `-20000 + 49 * row + 15`. Native publication skips a zero frame.

### Native observer span [mod integration]

The installed image has one direct caller of `0x9BFC0`, `0x9FED0`, and no direct branch into the displaced bytes. The RedBird inline observer uses the complete 15-byte entry span (instruction lengths 5/5/1/4), followed by the untouched bound load at `0x9BFCF`. The capture reads the original Win64 arguments and the source slot indicated by the sweep. The observer leaves the return value, native pool and projectile writes to Vanilla. Activation and rotation/session invalidation use the mod's own memory.

### test_green runtime corroboration (2026-11-22)

On the installed build, a controlled arrow launched near the east keep on
`test_green` produced a peak of ten active Vanilla AfterImage SpriteRenderers,
at native transparency 0, 10 and 21. For example, a copy first submitted at
tick N changed transparency at N+3 and N+6. The unchanged pose was independently
observed through the managed publication records at each change. This confirms
the shorter alpha cadence in the ordinary ascending-slot case. The Unity
prototype recorded the same 15 visual emission records and 36 checked
opacities in the matched Extreme Troops run. Drawing through every obstruction
and the less common later-age case caused by allocating before the current slot
were outside the isolated capture.

The test details and log pointers are in
[ProjectileTrailsTest TESTING](../../../../../Testmods/ProjectileTrailsTest/TESTING.md#simulation-tick-afterimages-on-test_green).

The projectile sweep's cursor-reset counter at `0x3A0FA28` cycles with the
calendar. In a single continuous mass test the sampled value moved from 157
back to 45 without a map change. Use the monotonic map tick at `0x3665F58` to
measure time since emission. The installed image increments that Int32 at
`0xCE03B` before the projectile sweep call at `0xCE245`. It is also the address
resolved by the installed Extender for `GetElapsedMapTicks()`. See
[periodic simulation clock](PERIODIC_GOLD.md) and
[gatehouse query pass](GATEHOUSE_QUERY_PASS.md) for the two distinct clocks.

### GPU renderer runtime check (2026-11-23) [mod integration]

On the same managed and native hashes, the test mod reproduced the ten-copy
paused arrow on `test_green` with 15 matching position, sprite and depth
records and 36 matching opacity samples. The GPU renderer keeps the same
texture and sorting keys. It reuses sprite geometry and streams one
24-byte record per visible copy. The local display continues to consume zero
native slots. The synthetic game fixture submitted 50,000 visible copies in
96 groups. An independent Unity Player pixel test verified 50,000 distinct
positions using the same shader bundle. See the
[GPU game test and timings](../../../../../Testmods/ProjectileTrailsTest/TESTING.md#gpu-renderer).
These observations do not establish why the developers chose to suppress native
trail generation in Extreme Troops.
