# Buildings, walls, gatehouses, drawbridges and construction [Scope: Mod work, not Vanilla]

What our mods, testmods and the external components do in this game system. Summary state 2026-11-27, native hash FBCB9319 (full hash in ../../../../CURRENT.json). Vanilla facts and addresses are explained in [vanilla/fortifications.md](../vanilla/fortifications.md); the audit documents in the parent directory stay authoritative. Each mod's full site/register contract is in its `UpdateToNewDLL.md`.

Common rules seen across all features here: hooks are permanent and logically activated (never unpatched); every native feature is bound to the audited hash with whole-function/span byte checks and refuses foreign patches; RedBird X64InlineHook uses a 14-byte minimum rounded to whole instructions, so actual displaced lengths are pinned per site.

## BugfixesAndQoL

| Feature (source folder) | What it changes | Vanilla touch points | Setting / scope | Status |
|---|---|---|---|---|
| Drawbridge destruction fix (`src/DrawbridgeDestructionFix`) | Adds a positive-HP guard before the early gate→bridge removal for direct attacks on the two stone gates; lethal, area, bulldoze paths unchanged | Hook at `0x7EE6B`, displaces 4+6+5 = 15 bytes, continues `0x7EE7A` or skips to `0x7EF87` ([vanilla](../vanilla/fortifications.md#damage-and-repair)) | Default-true `[SyncHostOnly]`, permanent logical activation | Editor acceptance of the testmod 2026-10-13 (668 paired calls, 665 protected, both gate sizes); main mod user-confirmed 2026-10-13, version 1.0.188. Projectile, two-bridge and MP coverage not separately shown. ([DRAWBRIDGE_DESTRUCTION.md](../DRAWBRIDGE_DESTRUCTION.md)) |
| Gatehouse living capture (`src/GatehouseLivingCapture`) | Capture ignores death-marked units (IsAlive AND marker 0, `APIShared.UnitAccess.IsReallyAlive`) | Span `0xB7540..0xB7552` (CDQE/IMUL/CMP 2/7/9, 18 bytes), native `JE 0xB75B7` retained; RAX unit ID, RDI slot displacement, R13 module, R14W zero; preserve all GPRs and XMM0..5 | `EnableGatehouseLivingCaptureFix`, default on, host, gated by EnableMod | Testmod gameplay 2026-10-07 in editor with Fixes 1.25.1 (exclusion observed); integrated 1.0.176 with 648 machine cases. Main-mod gameplay and real MP pending. ([GATEHOUSE_LIVING_CAPTURE.md](../GATEHOUSE_LIVING_CAPTURE.md)) |
| Building pause correction (`src/Buildings`) | Normal pause/resume and the button state use the per-type table plus the selected building's override instead of the incomplete UI field (Water Pot, cathedral) | Managed only: reads `r_BuildingSleep`; keeps Chore 43 chain; never edits the PlayState snapshot | Feature-gated | Static contract; in-game/MP repeated toggles, save/load pending. ([BUILDING_PAUSE_UI.md](../BUILDING_PAUSE_UI.md)) |
| Occupied-wall demolition (`src/AreaDemolition`) | Lets a wall with units on it be demolished | `0x79F84..0x79F98` (6/8/6 bytes; continuations `0x79F98`, `0x7A044`, `0x7A058`), `0x63735..0x63758` (4/8/8/9/6; continues `0x63758` or `0x63842`); Fixes' hook `0x7A047` stays on the Vanilla route | Production option; MP needs both sites on every peer (simulation agreement), host setting frozen per mission | 2026-11-21 passed single-click tests with and without Fixes 1.31.0; units keep position/health, farmers finish deliveries. ([AREA_DEMOLITION.md](../AREA_DEMOLITION.md), [contract](../../../../../../BugfixesAndQoL/UpdateToNewDLL.md#occupied-wall-demolition)) |
| Area demolition / construction (`src/AreaDemolition`, `src/AreaConstruction`) | Area bulldoze and area build from the former testmods; authenticated APIShared batches instead of per-target Chores 29/41 | Calls live helper entries under the engine monitor, restores cursor/flattened/actor/neighbour scratch; storage adjacency via SE `0xC72C0` Post event for prospective hover only | Default-on local; packets independent of UI settings | Area demolition: private-memory tests only; ingame acceptance deferred by the user 2026-11-11. Construction: editor run placed contiguous storage (12/3/3 records). ([AREA_DEMOLITION.md](../AREA_DEMOLITION.md), [FARM_BUILD_PREVIEW.md](../FARM_BUILD_PREVIEW.md)) |
| Crenels (`src/Crenels`) | "Keep crenels crossable": every managed intact crenel follows map parity (high when x+y odd); diagonal-only crenels alternate by x; low non-straight pieces are drawn with the straight sprite so `0x65EF0` never raises them | Detour on `0x5C2D0` via `Shared/Runtime/Native/AuditedEntryDetour` (NativeX64 Indirect, 10 bytes `mov [rsp+8],rbx; mov [rsp+10h],rsi`, exclusive registry claim, `NativePatchGuard`); SE `OnWallBulldoze`, `OnBuildingTileTakeDamage`, `OnBuildWall`, `OnAIBuildWall` | Host option, default on; three align-existing options default off; frozen per session | Editor and skirmish acceptance 2026-11-22..25 (enterability, ladders, towers, diagonals, local redraw); visual check by user 2026-11-25. See details below. ([CRENELATED_WALLS.md](../CRENELATED_WALLS.md)) |
| Wall deposits (`src/WallDeposits`) | Restores stone/iron bits lost under walls when the wall disappears | 13 sites: `0x6F55D`, `0x6CB85`, `0x63775`, `0x71F0D`, `0x71FF3`, `0x7E15A`, `0x7ED20`, `0xED990`, `0x6D711`, `0x5D78A`, `0x638D3`, `0x63F25`, `0x6D4B1`; outside Fixes `0x6CCC8` and the moat hook ending at `0x5D783` | All modes; MP only after APIShared admits the capability for all peers | Replaced the standalone Deposit Restoration test mod in 1.0.240 (archive id `DepositRestorationTest_Serp`, entry `covered-deposits`, schema 1 kept). 0.1.0 accepted locally; MP shared save load preserved deposits (user); live MP repair acceptance pending. ([WALL_DEPOSIT_RESTORATION.md](../WALL_DEPOSIT_RESTORATION.md)) |
| Moat terrain (`src/MoatTerrain`) | Restores the original soil byte when a moat is filled or a bridge removed | Private stubs on disjoint soil/capture spans (`MoatTerrain/NativeDefinition.cs`); bridge origin captured before `0x6D8DF` cleanup | Map-scoped ushort buffer (planned, dug, bridge, transient); save `BugfixesAndQoL.MoatTerrain` schema 1 via `VersionedSaveData`; uses `EngineInterface` threadLock by reflection | Static + fixtures; game visuals, farm placement, MP untested. ([MOAT_TERRAIN_RESTORATION.md](../MOAT_TERRAIN_RESTORATION.md)) |
| Moat land bit in MP | Recomputes IsLand on completed moats with a fixed reference neighbour so view rotation cannot desync | Span `[0x7C8EE, 0x7C8FD)` in `0x7C0A0` | — | Fixture-tested; Steam MP comparison separate. Origin: resync category 6 captures 2026-11-19 ([MOAT_TERRAIN_RESTORATION.md](../MOAT_TERRAIN_RESTORATION.md), [contract](../../../../../../BugfixesAndQoL/UpdateToNewDLL.md#multiplayer-moat-land-bit-at-the-terrain-loop-tail)) |
| Pitch/moat lines (`src/PitchMoatLines`) | Whole-line packet 106 calls the live `0x6FE90` and reproduces Chore 21's path invalidation | Keeps SE `BulkMapEditorDetours` and MoatTerrain interior hooks; planned-height acceptance stays with the live gate (ExtraFeatures bypasses `0x5CC1E`/`0x704CC`) | — | Static + call-ABI fixture. ([MOAT_TERRAIN_RESTORATION.md](../MOAT_TERRAIN_RESTORATION.md)) |
| Pitch/stairs variant B (`src/PitchStairs`) | Pitch keeps ground height; fourteen sites incl. producers, terminal markers and fire-only virtual height | Inventory of `0x59380`, `0x804C0`, `0x6CCED`, `0x6F6B6`, renderer compensation, fire readers | `EnablePitchStairsFix`, default true `[SyncHostOnly]`, HostSiege / Fixes?; native word chosen at first BeforeLoad per session | 11361 offline checks (testmod); live runs 2026-11-02 had eleven sites; integrated fourteen-site game/MP acceptance pending. ([PITCH_STAIRS_HEIGHT.md](../PITCH_STAIRS_HEIGHT.md), [PITCHSTAIRS.md](../../../../../../BugfixesAndQoL/docs/PITCHSTAIRS.md)) |
| Improved building preview (`src/BuildPreview`) | Red ground marking for farm/resource coverage, counts and tooltip | Indirect detour at `0x5DD70` (6 bytes, resumes `0x5DD76`); APIShared Ground contribution and main-thread observers | Local, default on, generation-bound snapshots | User accepted marking/counts 2026-10-13 (1.0.189); map-edge transient skip 2026-10-31, game/editor acceptance pending. ([FARM_BUILD_PREVIEW.md](../FARM_BUILD_PREVIEW.md)) |
| Peace-time civilian proximity | Civilian-only limit 3 during actual native active peace, through the APIShared broker | `0xEE640` via SE event (no own hook) | Host preference | Isolated tests only. ([ENEMY_BUILD_PROXIMITY.md](../ENEMY_BUILD_PROXIMITY.md)) |

## Pathfinding

- **Enemy gate reachability** (`Pathfinding/src/GateRouting/ReachableEnemyGatehouseRuntime.cs`, `src/Core/Common/SimulationRunScope.cs`; written by the audit as BugfixesAndQoL, the code now lives in the Pathfinding mod, checked 2026-11-27): `OnGatehouseQuery` subscriber rejects inaccessible enemies. Gate lookup and drawbridge collection are cached once per query pass, relying on one pass per gate per `EngineInterface.run`; queries outside a marked run (map init, editor) are evaluated freshly; units outside closing range keep the candidate decision without reachability. Assumption: no other `OnGatehouseQuery` subscriber mutates buildings/path records in its callback; the DEVDIAG build counts `passMismatches`. Before caching the callback cost 0.9 ms per tick and slowed `0xF5A40` by ~0.3–0.5 ms on `test_stresstest4.sav` (2026-10-23). ([GATEHOUSE_QUERY_PASS.md](../GATEHOUSE_QUERY_PASS.md))

### Crenels: contract for a reimplementation

- Callback calls the original once, keeps every positive answer, answers 1 only for a published low, intact, structure-free managed crenel whose owner and row match, on the axis with more live crenel neighbours (tie: direction 0). The assignment holds owner and row only (no recorded neighbourhood) because `0x65830` also runs on local-only full redraws; answers must come from synchronized state. Rendering never writes game data.
- Redraws of managed tiles are recorded and normalized at the next `OnTick` when their shape signature changed (raising with path refresh). `OnWallBulldoze` and `OnBuildingTileTakeDamage` normalize neighbours inside the event; an event-less splash raise is corrected one tick later. Limit: removals that never redraw neighbours are not seen.
- Pre-built AI castles (`0x6CB20` before `Started`) are adopted at `Started`, except editor and save sessions; later AI walls per call. Align-existing options run one full-map scan at `Started` after saved tiles are restored.
- Save: APIShared `VersionedSaveData` `BugfixesAndQoL.Crenels` (saves and maps, simulation-relevant), tile identities only. MP: optional `SimulationAgreement` feature, resync record `crenel-checkerboard` (one owner nibble per packed tile); host/client state hashes matched incl. map start, save load and a `MakeDesync` resync (2026-11-25); the record alone surviving a resync was not tested separately.
- Performance (testmod, same algorithm): 18,000 managed crenels on 800×800 under catapult fire: ≤ 1.4 ms per tick, ~94k hook calls ≤ 0.31 ms, 100-crenel build 14–26 ms. Save restore cost not measured.
- Not changed: the X shape and the zigzag cap of diagonal rows without plain walls.
- Superseded designs (history only): 2x2/blockade admission with `0xCF640` quota read, preview validator hook at `0x7A3B0` (18-byte Absolute span, continuation `0x7A3C2`), corner-anchored runs, Absolute/15 detour with 237-byte body compare. Testmods `CrenelatedWallTest` and `CrenelatedWallAcceptance` were deleted 2026-11-25.

## ExtraFeatures

| Feature | What it changes | Vanilla touch points | Status |
|---|---|---|---|
| Elevated-moat drawbridges | Bridge tiles on raised terrain use `Default - 8` (Vanilla 0 for Default ≤ 12); all rigid parts and units use building height H; low bridges stay Vanilla | Six hooks in the atomic 20-hook Elevated-Moat transaction: creation `0x73B35` (17), lowering `0x64546` (15), special renderer `0x45820` (19), animated `0x43BA2` (17), static `0x44EDD` (16), unit `0x18511C` (19). Special/animated shift `-(H-8)`, static `shadow - H`, unit `+0x714 = H - +0x712`, all only for H > 12 | Built 1.0.97, 7562 assertions, in-game confirmed 2026-09-17 (20/20 installed). ([DRAWBRIDGES.md](../DRAWBRIDGES.md), [Findings/Drawbridges.md](../../../../../../Findings/Drawbridges.md)) |
| Elevated human moats | Hooks bypass the moat height gates `0x5CC1E` and `0x704CC` | Interacts with BFQoL pitch/moat lines | ([MOAT_TERRAIN_RESTORATION.md](../MOAT_TERRAIN_RESTORATION.md)) |
| Enemy-proximity categories | Separate civilian and military radii for ordinary human 15/30 calls and classified AI repairs/rebuilds; `-1` keeps the incoming radius | SE `OnBuildingAllowRepairInProximity` on `0xEE640` (Pre skip with result); private copy of the full pristine body, only the CMP at `0xEE748` changed, seventh argument = civilian squared radius; one unpublished 4096-byte RedBird stub | Moved into APIShared `Buildings/EnemyProximity` 2026-11-02; ExtraFeatures contributes scoped overrides and the AI rebuild-delay veto. Settings migration via detached snapshot. No game/MP acceptance claimed. ([ENEMY_BUILD_PROXIMITY.md](../ENEMY_BUILD_PROXIMITY.md)) |
| Optional repair HUD button | Hidden for zero-eligibility types; market only in the overview | Reads `0x2E6710` under the full-hash gate | 1.0.137, production tests against the mapped image. ([BUILDING_REPAIR.md](../BUILDING_REPAIR.md)) |
| Manual gate/bridge preferences | Uses typed `GameBuilding` fields, never gate timer aliases on a bridge; actual spans owned by APIShared | — | ([GATE_BRIDGE_AUTOMATION.md](../GATE_BRIDGE_AUTOMATION.md)) |

## APIShared

- **Repair extras:** optional capability calls `0xD51A0` under `EngineInterface.threadLock` after full-hash and entry-byte checks; charges extra resources in SE `OnBuildingRepair` Post only after a verified damaged→full transition of the same global ID. ([BUILDING_REPAIR.md](../BUILDING_REPAIR.md))
- **Gate and bridge automation:** owns the configured distances/delays and the optional centered origin (`begin*8 + gridSize*4`, conservative published distance check); native decisions keep their thresholds. Manual-control spans: `0xB7A4E` (17 → `0xB7A71`), `0xB7BF4` (15 → `0xB7CB4`), `0xC53D5` (18 → `0xC54E8`), `0xC54A0` (16 → `0xC54D8`); direct-close timer spans `0xD5835..0xD5844` (15 → `0xD584D`) and `0xB79E2..0xB79F4` (18 → `0xB7A05`). Routes manual gates before Fixes' `0xB7C39`. Basic mixed-mode operation confirmed by user; full matrix (four preferences, two bridges, blocked raise, save/restore, real MP) and direct-close delay acceptance pending. Tests: `_inspect/APISharedTests/GateBridgeAutomationTests.cs`, `_inspect/GateBridgeAutomation/Verify.ps1`. ([GATE_BRIDGE_AUTOMATION.md](../GATE_BRIDGE_AUTOMATION.md), [GATEHOUSE_QUERY_PASS.md](../GATEHOUSE_QUERY_PASS.md))
- **Enemy proximity broker** (`Buildings/EnemyProximity`): no default policy, no native hook; read-only peace-state source. Native fixture now in `tests/APISharedTests`. ([ENEMY_BUILD_PROXIMITY.md](../ENEMY_BUILD_PROXIMITY.md))
- **Construction receipts and refunds:** SE delete/bulldoze callbacks bound to the actual AOB targets (`0xC4290` mark, `0xB8310` terminal). A shape-checked SafeIlHook replaces SE's invalid stack read at `0xC487A` with `RefundContextBuildingId`. Two startup CALL retargets at `0xC4793`/`0xC496C` through 14-byte FF25 relays isolate the refund gold call from nested gifts. Undo opcode 75 decoded as two Int16 fields; identity rechecked after reading. Local game acceptance 2026-11-15 in FreeBuild (Brazier, Ballista, Mangonel refunds); real host/client pending. ([CONSTRUCTION_COST_TABLES.md](../CONSTRUCTION_COST_TABLES.md))
- **Decoration removal:** owns an Indirect/7 detour on `0x9F410` (`Buildings.ConstructionDecorationRemoval`); further use must go through that owner. ([CRENELATED_WALLS.md](../CRENELATED_WALLS.md))
- **Mission lifecycle:** EditorCreated/EditorLoaded publication used by diagnostics (fixed the drawbridge testmod's missing editor captures). ([DRAWBRIDGE_DESTRUCTION.md](../DRAWBRIDGE_DESTRUCTION.md))
- **Wall deposits in MP repair:** transfers the host's private buffer snapshot per executed repair (Chores 54/67/65 boundaries). ([WALL_DEPOSIT_RESTORATION.md](../WALL_DEPOSIT_RESTORATION.md))

## CastlePlanner

- **Drawbridge blueprint direction:** uses planar camera-transformed cell centers (no terrain/flattened height); `DrawbridgeFront` PNG = BottomRight, `DrawbridgeRear` = TopLeft, mirrored for the other sides. Released as 0.8.37 after user confirmation 2026-10-07; 128 gate/castle/camera fixtures in `Helpers/AIVParser/AIVParser.Tests`, preflight `_inspect/Test-CastlePlannerDrawbridgePreflight.ps1`. ([DRAWBRIDGES.md](../DRAWBRIDGES.md))
- **Keep direction:** commit `2cd3fddf` (first shipped DLL 0.8.47) read `r_SpriteVariationIndex` as direction and aborted East castles; corrected reader uses record `+0x92`. Nine local runs 2026-11-15 (all rotations, no Fixes, save/reload, editor) confirmed. ([KEEP_DIRECTION.md](../KEEP_DIRECTION.md))

## Testmods

| Testmod | Purpose | Status |
|---|---|---|
| AreaDemolitionTest | Prototype of area bulldoze; bounds outstanding batches by all-simulator acknowledgements | Integrated into BFQoL; `Testmods/AreaDemolitionTest/UpdateToNewDLL.md` keeps bindings. ([AREA_DEMOLITION.md](../AREA_DEMOLITION.md)) |
| AreaConstructionTest | Prospective storage adjacency hit via SE `0xC72C0` Post | Editor integration run passed; folded into BFQoL. ([FARM_BUILD_PREVIEW.md](../FARM_BUILD_PREVIEW.md)) |
| CrenelatedWallTest / CrenelatedWallAcceptance | Crenel design exploration and in-game scenarios | Deleted 2026-11-25; facts kept in [CRENELATED_WALLS.md](../CRENELATED_WALLS.md). `CrenelSiegeAcceptance` exists in `Testmods/`. |
| Drawbridge destruction testmod 0.1.0 | Prototype of the HP guard | Retired; evidence under `_inspect/DrawbridgeDestructionIntegration`. ([DRAWBRIDGE_DESTRUCTION.md](../DRAWBRIDGE_DESTRUCTION.md)) |
| GatehouseLivingCaptureTest | Prototype capture filter | Retired after integration; tests in `BugfixesAndQoL/tests/GatehouseLivingCapture.Tests`. ([GATEHOUSE_LIVING_CAPTURE.md](../GATEHOUSE_LIVING_CAPTURE.md)) |
| PitchStairsTest | Variant B prototype | Migrated to BFQoL. ([PITCH_STAIRS_HEIGHT.md](../PITCH_STAIRS_HEIGHT.md)) |
| Deposit Restoration test mod | Prototype of wall deposits | Replaced by BFQoL 1.0.240. ([WALL_DEPOSIT_RESTORATION.md](../WALL_DEPOSIT_RESTORATION.md)) |
| Campground ground-preservation probe | Suppressed original-ground writes at `0x6901D` (348 per sample) to keep the Keep campground ground | Runtime samples 2026-09-26 only. ([KEEP_CAMPGROUND_GROUND.md](../KEEP_CAMPGROUND_GROUND.md)) |
| FreeBuild Undo probe (game probe 462794c61769) | Disproved "FreeBuild never supports this Undo" | Led to the corrected decoder. ([CONSTRUCTION_COST_TABLES.md](../CONSTRUCTION_COST_TABLES.md)) |

## External components

- **Script Extender** (2.14.1 hash E5D78FDF…, 2.14.3 installed for the gate audit):
  - Events: `OnBuildingRepair` (around `0xD5030`; Post only after Original), `OnBuildWall` (Pre inputs mutable, ignores Skip, Post has no success result), `OnAIBuildWall` (exactly `0x6CB20`), `OnWallBulldoze` (`0x63700` entry detour), `OnBuildingTileTakeDamage` (`0x7EB00`), `OnGatehouseQuery` (replaces the `0xB7B4B` test; emulated default uses `r_ControllableForPlayerId` while native reads `+0x2A0`; scoring/capture hooks at `0xB757A`/`0xB790B`), `OnBuildingAllowRepairInProximity` (`0xEE640`), `OnBuildingSpawn`/`OnBuildStructure`, `MapEditorR3EventHooks` brush events, adjacency detour on `0xC72C0`, `BulkMapEditorDetours`.
  - Pitfalls: delete/bulldoze names in SE comments are swapped relative to their AOB targets; the `0xC487A` material interceptor reads the building ID from the caller stack (`29 * IntPtr.Size`) and breaks when managed wrappers change the caller; wall cost multipliers are not saved; owner getter returns the raw byte; the debug mirror tool calls originals before Pre; `CurrentlySelectedBuildingIdVA` = `0x67E8394`; SE may mark MP saves as non-save archive loads.
  - Interop fields: `r_AliveState` Int16 `+0x88`, `r_IsKilledByProjectile` UInt16 `+0x29C` (a death marker for all causes), `r_ControllableForPlayerId` UInt16 `+0x92`, `r_SpawnedForPlayerIndex` `+0x7C`, unit size `0x490` (2.14.0 split of `N0000019A`).
  - Sources: [GATEHOUSE_LIVING_CAPTURE.md](../GATEHOUSE_LIVING_CAPTURE.md), [CRENELATED_WALLS.md](../CRENELATED_WALLS.md), [CONSTRUCTION_COST_TABLES.md](../CONSTRUCTION_COST_TABLES.md), [GATEHOUSE_QUERY_PASS.md](../GATEHOUSE_QUERY_PASS.md).
- **Fixes:**
  - 1.19.1 patches the sleep reset at `0xC7F3E` and selects preservation by the locally selected building (`0x67E8394`); Chore 43 carries only the type, so peers can diverge: runtime 2026-09-27 showed iron 1 on host and 0 on client after the same toggle, with a user-reported desync. ([BUILDING_SLEEP_SELECTION.md](../BUILDING_SLEEP_SELECTION.md))
  - Owns `0xB7C39` (farmer recovery in the gate handler), `0x6CCC8` (AI wall/stair), `0x7A047` (moat demolition), `0x13880` `EB 11` (skips low-wall refund halving), a low-wall-only quota adjustment, and can detour the Keep constructor argument. ([GATE_BRIDGE_AUTOMATION.md](../GATE_BRIDGE_AUTOMATION.md), [AREA_DEMOLITION.md](../AREA_DEMOLITION.md), [KEEP_DIRECTION.md](../KEEP_DIRECTION.md))
  - Compatibility verified: occupied-wall demolition with 1.31.0, gate capture with 1.25.1, Keep direction with and without Fixes.
- **RedBird:** X64 1.1.0 / Iced 1.21.0 (drawbridges), RedBird 1.5.0.0 (capture, preview), NativeX64 hash 0843DD4C…; `HookSize` is a minimum; at most one bound label per generated instruction; default Commit activates patches and Dispose removes owned hooks, so owners prepare without activation. ([DRAWBRIDGES.md](../DRAWBRIDGES.md), [MOAT_TERRAIN_RESTORATION.md](../MOAT_TERRAIN_RESTORATION.md), [Findings/Drawbridges.md](../../../../../../Findings/Drawbridges.md))

## Open release gates

- Real multiplayer acceptance: drawbridge HP guard, gate capture, gate/bridge automation matrix, crenel resync record alone, wall deposits repair, pitch/stairs variant B, moat terrain, moat land bit, construction refund receipts, building pause.
- In-game: area demolition (deferred by the user 2026-11-11), moat terrain visuals and farm placement, preview map-edge recovery, pitch/stairs fourteen-site integration, building pause repeated toggles and save/load.

## Construction input idle monitor, 2026-11-28 [mod integration]

`AreaConstruction.PlacementInput.AfterInput` is a process-rooted postfix of `EditorDirector.Update`. Its original unconditional `EngineThreadLock.Gate` acquisition could wait even without a construction gesture. Continuous main-thread samples of four 75–106 ms gaps placed 94.7–97.2% of inner mod frames in this method, with `mono_monitor_enter_v4_internal` present in the full window. The gate is the actual `EngineInterface.threadLock`; the older 88FC artifact demonstrably blocks an otherwise idle callback while a helper holds it (explicit ~750 ms negative control).

The guarded idle path requires no captured gesture, drag, queued sites, storage order, quarry order or unapplied cancellation epoch, and hides the preview on Unity before returning. Primary/HandleButton and AfterInput create local work only on Unity; QuarryTick, lifecycle and cancellation paths can consume or clear it. The active/cancellation paths retain the existing monitor. Local Unity construction input, rather than a player's AI flag, is the authority: manual editor placement into an AI slot remains possible. Non-UI Primary/cursor calls and irrelevant local inputs pass through unchanged. Normal AI building has no placement-feedback or storage-neighbor probe scope. Authenticated replay of an already confirmed human quarry order remains valid on the simulation thread; the independent host storage-limit rule is unchanged.

Normal artifact035A passed seven actual input/monitor cases and the original nineteen relocation/rescue cases in one hidden-desktop transaction (`ca570a3b91a0488cb513c5967ab55a1c`). The idle callback returns while the helper still owns the monitor; cancellation still waits and applies its epoch. These forced monitor tests are not performance timing or active OS mouse-drag acceptance. Evidence and final normal ON/OFF qualification: [BQoL rescue acceptance](../../../../../../BugfixesAndQoL/docs/TrappedUnitRescue.Acceptance.md), `.work/a/area-idle-gate/001/result-composite.json`. No new native hook or native geometry policy follows from this managed idle correction.


## Wall construction prices and demolition refunds, 2026-11-28

Static comparison against the installed SHCDESE 2.14.6 assembly, local
Extender commit 81a7a945c47c2c5dcc347cd1ad3661a54be02429 (tree
3215dd016cdca0ae7c3a89001f1116ba4d06d0ee), and the local Fixes source.
The database manifest captures an older Extender snapshot. See the
[Vanilla audit](../WALL_COSTS_REFUNDS.md) for the canonical native paths.

- `GameBuildingManagerAPI.SetHighWallCostMultiplier` and
  `SetLowWallCostMultiplier` already alter quarter-stone pricing. Defaults
  0.5 and 0.25 match Vanilla; native-facing getters truncate value*4.
  The AOB in `BulkBuildingDetours` resolves to `0xC8D1C`, where the
  Extender replaces the weighted tile sum. Human walls, crenels and stairs
  use the same high multiplier. AIV uses the high counter even for low
  walls, and the free AIV stair variants bypass pricing entirely.
- The Extender's placement-cost AOB resolves only to `0x941AB`. It takes
  the existing Vanilla quota Q and computes
  `floor(floor(4*Q / quartersPerTile) / (low ? 4 : 2))` for positive prices.
  If quartersPerTile is zero it keeps Q. It does not alter tool selection
  `0x8BD60` or the separate commit quota at `0x6F2C2`. Example without
  Fixes: five stone and zero remainder, high price 1.0, gives Vanilla Q=10.
  The Extender returns five for the drag quota. After a queued five-tile
  order reaches execution with two stone left, Vanilla commit Q=4 and the
  strict comparison can admit all five. The debit requests five and the
  storage helper can only remove the two present. This mismatch follows
  statically from the current paths. It has no game acceptance.
- Fixes `ProperLowWallCostFix` adds its own low-wall doubling at execution
  `0x6F2C2`, Chore adapter `0x11D4A` and the preview cap `0x6136F`.
  The actual execution mapper is in SI at `0x6F2C2`; the Fixes emitter
  compares R11 there. The source establishes patch ownership and intent,
  without establishing effective live limits for arbitrary prices. Fixes'
  `EnableLowWallRefundFix` changes the branch at `0x13880` from JG to
  JMP, skipping the halving for every tile. At 50 this gives a quarter
  per tile, equal to the default low-wall price. Both options were enabled
  in the local config snapshot.
- `OnBuildingRefund(Pre)` is invoked for sentinel -1. The Extender forwards
  the mutable Percentage and supports SkipOriginalFunction there. It
  supplies player and sentinel, without a tile. The per-material interceptor
  at `0xC487A` is bypassed on that route. `OnWallBulldoze(Pre)` supplies a
  tile but runs via `0x63700` after the refund and its wrapper calls the
  original irrespective of SkipOriginalFunction.
- BuildingCosts uses ordinary cost-table targets and omits wall rollover
  structures. ExtraFeatures excludes nonpositive IDs in
  `NormalizeRefundPercentage`, and APIShared's normal receipt policy
  requires a positive owned building ID. The ExtraFeatures source comment
  that the fourth argument is not a percentage is incorrect for the wall
  sentinel. Sending arbitrary values through the existing event cannot
  overcome the native refund buckets.
- A complete manual price setting needs a single mapper-aware price and
  compatible affordability/charge behavior at selection, preview, queued
  maximum and execution. A tile-specific refund percentage needs tile/Chore
  context before the native credit and finer per-player fixed-point
  accumulation. A policy based on actual purchase price also needs price
  provenance for free, preplaced, AI and repaired/replaced tiles. Decide
  explicitly what applies to each, and preserve the separately accounted
  Undo route. Any synchronized gameplay implementation needs matching
  rules and remainder state across peers, save/load and resync. These are
  design constraints from the static audit, with no new implementation or
  runtime verification.
