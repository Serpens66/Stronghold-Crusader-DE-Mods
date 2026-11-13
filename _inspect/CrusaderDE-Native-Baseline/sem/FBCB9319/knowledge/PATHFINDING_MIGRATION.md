# Pathfinding migration and acceptance knowledge [Scope: Mod work, not Vanilla]

Preserved 2026-11-13 from `Pathfinding/MIGRATION.md`, root-repository Git blob
`5cd5a2a5d2b44fc17a973986c3a2d5af0c0edc62`, at HEAD
`1d10d7ddf56eb02eefad502b8051c3ed40f6358c`. The original document remains readable
with `git show <blob>`. This is a compact integration/history record, not Vanilla
evidence or a new game acceptance. Current tasks live in
[Pathfinding/src/TODO.md](../../../../../Pathfinding/src/TODO.md).

## Ownership and compatibility

- Pathfinding owns unit commands, the route engine, gate routing, Assassin paths,
  formations, Shift queue and troop movement formerly in BFQoL/MoatMove. BFQoL
  retains gate distance-origin/target-marker features and captured-gate protection.
  The source/blob map is `Pathfinding/migration/source-map.json`; preserved MoatMove
  development knowledge is `Pathfinding/migration/moatmove-notes.md`. Retired sources
  remain in Git, 175 hash-verified historical evidence files in `.work/a/moat-retired/001`.
- Keep save identifiers `BugfixesAndQoL-AssassinClimb` and
  `BugfixesAndQoL-TroopMovementSync` despite the source move. BFQoL retains only its
  own records. The intentional `FeatureSaveStates.cs` drift from the original
  source map must not be mistaken for a failed migration.
- Import legacy settings without materializing defaults before the migration
  journal is safely persisted; explicit new settings survive temporarily unreadable
  old storage. Retired BFQoL preset properties remain readable. Existing importer
  tests exercise the actual bootstrap path, not a separate model.
- Pathfinding loads after BFQoL and Fixes through soft dependencies. BFQoL before
  1.0.215 still owns duplicate features; startup reports
  `PATHFINDING_OUTDATED_BUGFIXES`. MoatMove remains incompatible. The 2026-11-07
  coexistence audit found no overlapping spans: `0x119F90` is a BFQoL inline hook
  but only a Pathfinding call; `0x11FFA7`/`0x123090` distinguish call site and entry;
  `0x59210`, `0x639C0`, `0x69470` distinguish BFQoL interior hooks and Pathfinding
  entries. EngineInterface.run keeps the shared bridge first. These are dated mod
  integration observations for the native hash below, not future overlap guarantees.
- Set the SimulationRunScope marker on every EngineInterface.run path independent
  of feature settings. Gate topology fingerprints are functional state even outside
  DEVDIAG; cached answers belong to one audited query pass, not merely one Extender
  time value. See [GATEHOUSE_QUERY_PASS.md](GATEHOUSE_QUERY_PASS.md).
- Assassin/formation network handlers use APIShared native sender authentication
  plus receiver-side player/live-tribe ownership validation. Preserve packet layout
  and registration order; formation sends remain synchronous on the simulation path.
  A Unity-only send method is not interchangeable there.
- NativeSiteRegistry entries for Pathfinding were an optional diagnostic addition,
  not an unfinished gameplay fix. Local duplicated helpers were optional cleanup.
  Retained TEMP_GATE diagnostics are DEVDIAG-only update aids, not mandatory removal.

## Dated acceptance and its limits

All native observations cited by the original migration use SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The installed DLL was rehashed against CURRENT.json and DATABASE_INFO.json during
this preservation. Native fire/plague/movement contracts already live in
[FIRE_EFFECTS.md](FIRE_EFFECTS.md) and [PLAGUE_HEIGHT.md](PLAGUE_HEIGHT.md);
load/placement/AI110 contracts in [UNIT_LOAD_PLACEMENT_CONTRACTS.md](UNIT_LOAD_PLACEMENT_CONTRACTS.md).
Older documents' MoatMove source paths are historical; current implementation is
`Pathfinding/src/Engine`, integration `Pathfinding/src/UnitCommands`, native core
`Pathfinding/native/PathfindingAnchor.cpp`, update contracts `Pathfinding/UpdateToNewDLL.md`.

| Historical evidence | Preserved result and boundary |
| --- | --- |
| `.work/a/pf-finish/001`, `final-builds.json`, `final-ui-acceptance.json`, `final-restoration.json` (2026-11-12) | Normal/DEVDIAG builds 1187/1188 and related BFQoL/Spectator checks passed. Final SP-pathfinding alone and with BFQoL/Fixes each 43/43, control 27/27; commands, queue, saves and editor exercised. Restore/file hashes checked; no global inputs. Earlier UI runs do not supersede these final runs. |
| Same directory, `performance-results.json`, `final-performance-review.md` | Eight alternating 120 s runs on test_stresstest5, speed 250: Pathfinding vs APIShared alone +6.7–7.2% FPS, +4.7–6.3% ticks/s; native F4930 work reduced, reconstruction failures zero. p95 and >50 ms frames did not uniformly improve. Scenario-specific net effect under a sampler, not universal FPS or isolated mod overhead. |
| Initial `pf-perf` vs corrected `pf-perf2` | Initial PerfProbe phase timing replaced Monitor calls needed by BFQoL's line boundary. Corrected series used RunPhaseTiming=false, Extras.EngineCalls=false and Sampler.SimOutsideRun=true. Do not combine series or attribute the diagnostic interference to BFQoL gameplay code. |
| `.work/a/pf-opt/001`, results/watch-results/wall-results/final-runs/final-verification JSON (2026-11-12) | Direction-watcher simplification retained aliases, invalidations, budgets and exception state. Isolated Mono medians improved; builds 1190–1192 passed. Open/wall 5000-unit runs verified identities, paths and motion over 600 ticks, including standalone and BFQoL. They did not prove all-unit arrival or improved total command throughput. Test automation stays in PathfindingLoadTest. |
| `.work/a/pf-startup/001`, fix-test-result/post-fix-verification JSON | Former TryGetBuildingById(0/3999) startup message traced to BFQoL demolition event ID 0. BFQoL 1.0.222 rejects nonpositive IDs before lookup; build 1194 and joint fresh-map retest passed. Pathfinding/native hooks were unchanged. This issue is resolved, not a remaining Pathfinding bug. |
| `.work/a/access/001`, build-map-joint/joint-installation-result/joint-final-verification JSON | Shared BuildingAccess/IndexBounds adoption retained consumer identity/owner/liveness checks. Builds 1214–1224 and Pathfinding DEVDIAG 1225 passed; stale PreserveNewest info.json packaging was reproduced and fixed. Pathfinding then 0.1.4/minimum APIShared 0.49.0; joint installed SDK 0.50.0. Joint map start and restoration checks passed. These versions are historical observations, not current dependency pins. |

The original document also records earlier 2026-11-07 smoke/performance runs under
`.work/a/pathfinding-build/runs`; their different MoatMove options and packages do
not establish an exact before/after benchmark. Release/Workshop registration and
pack validation were completed, not authorization to publish. Historical complete
logs/build records remain at their original paths and are not rewritten here.

## Remaining work and later review

- Real two-computer multiplayer and manual visual acceptance remain release
  requirements. SP fixtures, automated UI steps and source audits do not replace them.
- A historical healthy-spearman AI110 dissolution on the Moat test map remains
  causally unassigned; native explanation of the AI110 terminal path does not identify
  its trigger. It is distinct from the fixed BFQoL startup message.
- The 2026-11-13 review found a static solo+escape restore ownership error:
  CaptureMovementCases can serialize both cases for one soldier; after restoring
  the escape, rejecting/ending the solo case rejoins its group too early. Independent
  source review confirmed the path; no new live reproduction or correction here.
  TODO B1 contains locations, counterargument checks and regression acceptance.
- Fire/plague avoidance remains explicitly release-gated off in EngineOptions.Capture
  with hidden settings/search sections. Engine fixtures pass on 2026-11-13 but do not
  compile the real FireRouteRules/PlagueRouteRules scanners. Full effect, save,
  height-transition, load, multiplayer and visual acceptance is still required.
- Commit/publication require an explicit user instruction. Parallel BFQoL
  PlacementAcceptance and menu/autosave work is outside this Pathfinding backlog.

## Offline implementation follow-up, 2026-11-13 [Mod work, not Vanilla]

This later observation supersedes the earlier **current-state** statements about
unfixed B1 and missing scanner compilation; the historical review above remains
unchanged. Working-tree changes on the same native hash preserve nested solo/escape
ownership on load, including ownership transfer when the solo watch is disabled.
Actual production regressions cover fire/plague and outward/safe/return phases.
The 0x11B520 admission audit now guards against stale 101/122 success for excluded
members; installed managed field types/offsets were independently checked. These
are mod corrections, not changes to Vanilla's admission contract.

Own fire/plague route waits now have separate host-synchronised indefinite options,
both default false; APIShared MaximumWaitTicks=0 and return deadlines retain their
meaning. Deterministic native-tick rotation prevents the reproduced high-ID probe
starvation under sustained limited remaining budget, with the same order after
save/load. Active automatic stop/flee control remains human-soldier-only; AI cost
routing within existing orders is retained pending a separate AI state/game audit.
Supported Pathfinding move/queue paths reject forbidden targets while preserving
the previous order and queue. Extender skipped Pre events have no Post: the later
anchor subscriber must not observe that order or open an unmatched frame.

Production-code fixtures passed 215580 runtime assertions plus 1273 scanner,
546 settings, 30 wait and 9 integrated veto-subscriber checks; Friendly movement
adds 18 boundary checks. Scanner fixtures replace game boundaries and do not
prove native binding or real callback execution. Native plague/backend checks
passed 1206 assertions. G1/G2 assembler candidates passed 7,095,335 comparisons
but were slower with actual installed-backend jump/gate costs, so no production
enemy-scan optimisation was integrated. Other isolated costs are not current
in-game frequency or FPS evidence.

Durable report: [OfflineImplementation](../../../../../Pathfinding/Findings/OfflineImplementation.md),
details in sibling OfflineHazards/OfflineOptimization documents; dated raw/compact
evidence under `.work/a/pf-offline/001` (B1/Q3 before/after, final Engine log,
enemy-scan.json). Normal/DEVDIAG/SDK build acceptance is recorded separately by
the coordinating run. Real height transitions, travelled routes, visual/editor
acceptance and two-computer multiplayer remain open. The historical healthy
AI110 case remains causally unresolved after re-reading pf-opt/001's native audit
and pf-startup/001's fix result: full HP and life=3 without previous route/flags
do not identify the trigger. Fire/plague runtime and UI release gates remain shut.
