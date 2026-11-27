# Trapped Unit Rescue Test: native contracts

**Production migration,27 November2026:** The replaced prototype runtime sources have been retired. Current code and ownership are in [BQoL/src/TrappedUnitRescue](../../BugfixesAndQoL/src/TrappedUnitRescue/UpdateToNewDLL.md); that production contract takes precedence for changed ownership, activation, scanner gating and save identity. The detailed address/layout audit below is retained as historical evidence and the inherited inventory. Historical prototype `src/` references refer to their versioned source, not a second current runtime.

Reference: canonical installed CrusaderDE.dll SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
These prototype contracts were inspected on 2026-11-15. Resolve future baselines through CURRENT.json. Unknown native images disable this test runtime; no semantic fallback is claimed for unit/grid layouts.

## Civilian removal instead of job continuation (27.11.2026)

The same canonical hash binds the existing grid/occupancy and unit-word readers used by CivilianRemovalPolicy. Life native6E4/public88, type6E6/public8A, owner6EE/public92 and death8F8/public29C are additionally layout-checked before runtime publication. Ordinary movement/hidden/portal/moat words retain the offsets documented below. The explicit24-type list is Peasant plus23 human economic roles derived from B6340/2E4E20; Ox is not an employed Peasant, and paid Monk/Engineer/BedouinHealer are excluded. Current conservative admission excludes AI>=108, nonliving, death-marked and hidden/special-mode units; completed worker922 need not be zero.

The only new write path calls the public Extender DeleteUnitSafe after final original-life, causal surface/old-exit, live physical-exit and occupancy checks. It is enabled solely inside the shared SimulationTickEvents recovery scope after outer mutation completion. It sets Life3;182B00 later invokes1869C0 and normal cleanup, without job/cargo/refund/recruitment replay. This introduces no new native call target or hook. An unchanged ordinary Current with newly unsafe acceptedNext is bounded observation, not immediate deletion; queue Observe preserves TTL/visit budgets without consuming a native-attempt allowance. Count RemovalRequested separately from completed native removal, which the actual game fixture must observe.

Evidence: current Baseline RECRUIT_TRANSFORMATION.md and vanilla/units.md; shared SE/APIShared tick and DeleteSafe bodies. Pending schema5 carries an explicit removal action plus original Current/Next-exit evidence without Job/Command/vegetation/bank fields; schema4 remains historical and is never reinterpreted as removal authority. Worker removal does not enter the old Command-P7 geometry join. These changes do not yet retire all old mixed worker/command runtime or establish gameplay acceptance.

## Native relocation and observations

- Live callable `0x19BA40`: Winapi void(unit manager, one-based ID, uint tile X, uint tile Y, ushort ground height). Canonical entry bytes, typed layouts and complete image hash are checked. Original routine unlinks/relinks tile occupancy and normalizes Current/Next XY and fine position; inactive packed Next73C is not rewritten (full midpoint DWORD740 is cleared, substep750 becomes8). It does not guarantee job continuation or normalize special AI states.
- Live callable `0x196280`: native single-unit move pipeline, including existing Script Extender/Pathfinding hooks. Result is inspected; failed experimental reissue is a failure, not a successful rescue-with-continuation.
- `0x19BA40` may already be owned by BugfixesAndQoL's `Lord.HomeTeleportGuard`. Accept only that registered, byte-validated live entry; never install a competing hook or bypass the owner's restrictions.
- Read-only image-relative grids: flags `0x48F71B0`, outgoing directions `0x51890D0`, building IDs `0x4B6AA50`, occupancy heads `0x4C559B0`, terrain heights `0x4DDD350`. Packed tile capacity 320800, map mask via the public Extender API.
- Native unit stride `0x490`, typed GameUnit bias `0x65C`; field anchors: next occupancy `0x8FA`, mounting `0x9B0`, portal `0x8EC`, transition `0x8F6`, crew IDs `0x96C`, crew globals `0x974`, crew count `0xA0C`, stored moat permission `0x9C8`. Source chains are validated before native unlink.
- Movement class at native `0x6EC` (typed `N00000573`, offset `0x90`) is passed by `0x1855A0` to `0xDCE60`. Mode 1 bypasses ordinary direction checks; other nonzero modes also lack this prototype's ordinary-exit proof and are deferred. The typed offset is checked at initialization and production state classification is exercised with actual `GameUnit` records offline. Raised connected exits remain explicitly unsupported pending the complete per-unit surface predicate.
- The stored path word at native `0x9C8` (typed offset `0x36C`) becomes `0xDCE60` argument seven. A nonzero value permits entry into a completed-moat neighbor even without ordinary directional links, independently of the source tile's moat flag. Such records remain unsupported by the ordinary-exit proof even after a non-moat construction changes their source tile. The production classifier fixture covers zero/nonzero permissions for stationary and routed soldiers and Assassins; clearing that permission retains ordinary behavior.
- Siege refresh inside `0x182B00` at `0x1839C0` reads only the first count-at-`0xA0C` crew slots. Rescue therefore requires a nonzero-ID prefix of exactly that count (0..4), empty trailing ID slots, and the separately validated unique live identities/bindings. Empty ID slots may retain stale global values. Matching the number of nonzero IDs alone does not validate the native prefix; the production shape helper and its malformed-prefix cases are tested offline.
- Native `0xA9E` (typed `N000000DB`, `0x442`) is copied from the per-type table `0x3226B0` by `0x19A240`/`0x19C130` and read by group-order admission. It is not a generic locomotion flag: actual Apple Farmer and Quarry Ox entries are zero, while their own type handlers issue ordinary moves. The prototype admits those two independently audited civilian types despite zero; other zero-table types remain unclassified rather than assumed immobile or movable. Production classifier cases retain the stationary-engine exclusions and cover the civilian zero-field cases.
- State preservation is limited to audited ordinary roots: nonzero group-order-field units (including admitted mobile siege roots) require AI state0/1/101. Apple farmers require state2/5/7/8 AND active route flags2/4; Quarry Ox requires state1 AND flags2/4. Other work/combat/animation states are unsupported. In particular, `0x135E30` state3 advances the picking cycle after animation without a new tree-distance test; `0x19BA40` does not normalize that job state. Preserving a stationary picker while moving it from distance2 to3 could therefore manufacture remote picking. The classifier rejects that concrete unsafe case instead of inferring safety from its small state number. Offline production-classifier tests cover the complete0..107 civilian state range and explicit unproven military work/combat states. This restriction is a prototype admission policy, not a claim that unsupported Vanilla states are immobile.
- Even an admitted active Apple/Ox segment is rejected BEFORE native relocation unless the explicit caller requests reissue (`Complete(true)` / `TryRescue(..., resume:true)`). Clearing that route with `0x19BA40` while leaving its travel AI state intact must not be reported as safe stationary relocation. The caller's explicit continuation request does not guarantee success: a failed native reissue remains `ResumeFailed`, with no invented idle/work fallback.
- A failed `0x196280` publication does not preserve an old valid route: before its builder call it clears the unit's1000-byte path slot; failure can retain previous flags, length and `0x9C8` while route bytes are cleared or partially rewritten. Synchronous survival under the Fixes worker guard therefore proves only that guard. It does not establish moat escape or natural worker continuation; those require subsequent actual movement and original-identity checks. Native selector1 is not a fresh-search fallback (`0xE32B0` reconstructs existing search state only).
- Spearman has one additional type-specific stationary idle: AI2 with flags0. The full creation path is public `CreateUnitLocal` -> `CreateUnitWorld` -> native `0x17FEF0` -> type setup `0x19A240`; the regular `0x182B00` dispatcher selects handler `0x143400` through table `0x321CB0` entry24. That handler's state0 selects2 when no `0x18E760` idle travel target is found. State2 reads idle animation tables `0x32EEE8/0x32EF20`, returns to0 at completion, and calls `0x181890`, which derives any unstack/terrain recovery from the current position. It has no harvesting or attack-effect continuation. The exception is NOT generalized to other types or active route flags. Assassin's handler `0x16CD70` instead selects idle1 from0. Neither handler's state107 (`0x6B`) is a general idle: those branches orient to stored target coordinates and run attack/interact animation paths. Unsupported-result diagnostics include actual AI state, flags and substep so an unfamiliar state remains evidence rather than a guessed admission.

## Dedicated causal Apple moat receipt (2026-11-17)

`CaptureAppleMoatMutation` is a fixture-only, synchronous receipt. The caller
captures it before the real completed-moat brush, attempts the actual live
`196280` publication to the original job destination, and consumes it inside
the same simulation-monitored `Fixture.Execute` action. There is no yield,
tick or mission transition between those operations. This is not an automatic
moat-completion observer or a general delayed-recovery API.

Admission is restricted to Apple travel states5/7, route flags2/4, substep8,
movement class0 and stored moat permission0, with matching current/next tiles
and a distinct recorded job destination. The source must be ordinary before
the writer, newly completed moat afterward, and have no outgoing/reverse
physical edge afterward. Employee identity, health, owner, tribe, workplace,
home/store identities, cargo, selected tree and job counters must still match.
A successful original publication never invokes fallback. The generic
`TryRescue` completed-moat rejection remains unchanged.

For this specific non-Assassin/class0/marker0 combination, `DCE60` has no
special moat/climb escape after all eight ordinary edges fail. The receipt
can therefore choose only a free ordinary bank from its pre-writer reachable
field, call the validated live `19BA40` helper and immediately republish the
ORIGINAL requested job destination through live `196280`. It does not write
AI state, path bytes or moat permission manually, and never substitutes the
local bank as the job destination. The short existing Fixes gate covers only
this explicit synchronous fixture action; it ends before simulation resumes.

Native `19BA40` normalizes movement and occupancy without changing the Apple
employee/cargo fields. Apple handler `135E30` states5/7 defer their job
transitions through `188340`; a positive native route publication therefore
remains necessary before any subsequent tick. The receipt rechecks job and
original-goal preservation after reissue. It is single-use and does not promise
rollback: relocation failure, identity mismatch or rejected destination reissue
is an explicit failed fixture (`ResumeFailed` where applicable), including when
the local relocation already occurred. Subsequent actual original-goal arrival
with the original employee identity is still a mandatory game assertion.

Run068 observed an unchanged path before/after the brush, then1000 zero path
bytes after rejected native publication while AI5, flags2, length9, substep8
and marker0 remained. That run contained external mods; it verifies the unsafe
fixture assumption about retaining a valid old route, not the cause of the
builder's failure or Vanilla-only gameplay. No success is claimed here for the
new receipt, future natural moat completion, saves/editor coverage or MP.

## Apple observer: native index versus public vegetation ID

The unit field at native `0x9A0` (typed `r_TargetVegetationId`, `0x344`) stores the native vegetation ARRAY INDEX returned unchanged by `0x108040`; its name alone does not establish the public SDK ID domain. In the canonical image, `0x1080EE` multiplies that index by `0x9C`, `0x1080F5` reads X at manager `+0x86`, and `0x108103` reads Y at `+0x88`. The array begins at manager `+0x18`; the record's typed X/Y are `+0x6E/+0x70`. `0x135E30` separately reads selected-tree growth at manager `+0xA4 + index*0x9C` and copies the selector result unchanged to unit `+0x9A0`.

The inspected public `TryGetVegetationById` resolves `VegetationArray[id-1]`; its RedBird array constructor preserves the supplied base. The fixture must therefore translate its native index to SDK ID `index+1`, with native zero meaning no selected tree and the index bounded strictly below the public span length. Both capture and later identity validation use this same adapter. This corrects the fixture's domain mix; it does not declare the public getter's one-based contract erroneous. Recheck the native selector, caller, manager layout and installed public implementation together after updates. The remote-picking guard still requires distance at most2 from the actual previously selected tree.

## General AI drawbridge pilot

The existing default-off experimental setting additionally admits a narrowly bounded
observer for actual AI-AIV drawbridge construction in known local missions (editor
missions are allowed; real multiplayer and conflicting customized origins are not).
It validates AIV manager/player/village/frame ownership and the single packed map
anchor, captures bounded current occupancy before the original call, then consumes
a single-use context/session-bound completion only after Vanilla completed. No
native pointer is retained in the snapshot. The original AIV/Fixes actions are not
suppressed. Only the pilot's own synchronous relocation/reissue opens the existing
logical guard, which closes in finally.

Admission currently excludes civilian jobs and siege/crew roots, and requires the
existing audited controllable-soldier/cavalry policy and before/after identity proof.
The observer does not claim human/editor placement, moat completion, arbitrary
construction, old-save trap discovery or multiplayer support. Existing fixture
scenarios keep this observer disabled; only its explicit AIV scenario opts in.
Offline receipt tests and source review are not an in-game acceptance result.

## Optional Fixes suppression

Audited Fixes source: 1.30.0, commit `0870146` (2026-11-17 observation); installed binary SHA-256 `5CB9272C9D06E2784B15208DD0BE85BFEE381D8C7C7E9D9D2D389115E389C388` is evidence, not a runtime allowlist. Compatibility is checked against the relevant managed method/helper contracts and the complete installed worker stub. Unrelated additions and assembly version/MVID changes do not invalidate these contracts. The loaded module must still match the file being inspected. Unknown relevant changes disable our replacement before publishing hooks; this is not a promise of compatibility with arbitrary future behavior.

- Managed `Fixes.Events.FixesAIEvents.OnAIBuildWall`: permanent SafeIlHook guards Pre only. Post custom-height behavior remains original. No external config edits or unsubscription.
- Own native span `[0x196342,0x196352)`, exactly 16 bytes `498BCF410FBFC489442470E8EE040000`. Validate the full enclosing native function, exact instructions, incoming direct branches, actual backend length and live bytes. Static whole-baseline xrefs contain no interior entry; no indirect control flow is accepted in the enclosing function.
- Relocated original call reaches live `0x196840`; preserve EAX. Only active mode forces ZF=1 after that call. This skips the adjacent Fixes worker-deletion body, whose original replay restores flags before Vanilla's CMOV. Inactive mode preserves original flags/registers/stack exactly.
- Adjacent Fixes span `[0x196352,0x196360)` is 14 bytes and is not modified. The subsequent MOVSX at `0x196360` and CMOV at `0x196364` remain live. Re-emit and byte-compare the complete audited stub, displaced instructions and continuation before publishing our guard. Register exclusive ownership of our disjoint span and retain hook/resources until process exit. The native fixture installs both actual adjacent hooks to verify these boundaries.
- The existing gatehouse-worker solution is outside this prototype replacement and remains untouched.
- Fixes mounted keep recovery remains active with its original option, delayed emitter and native ownership; the separate runtime scanner-capture correction below replaces only the known mutable capture. The original 1.30.0 native contract follows. It hooks the four rider update entries (`0x1493E0`, `0x16E440`, `0x1731F0`, `0x17A590`), displacing 15 bytes each, and is intended to call native traversal relocation `0x187110`. It does not use our `0x19BA40` relocation or our worker span. Native mounting/hidden field `0x9B0` is not the rider type classification. The checked Extender range hooks are inside the update bodies; optional third-party AI-state entry trackers need separate review.
- Own worker publication uses `Shared.NativePatchGuard` before backend decoding and immediately before Enable, then watches the verified published bytes. Each mission/editor start rechecks them. A detected later overwrite disables our replacement logically; no published code is restored or rewritten.

## Mounted keep regression contract

`rescue-mountedkeep-off/on` tests the installed Fixes recovery with our own setting off/on, without changing foreign settings. The scenario requires actual installed mounted recovery hooks. Its matrix covers four rider types, three keep types, four campground rotations, and stationary versus committed movement. It checks identity, health, tribe, destination and occupancy; a blocked campground exit must not cause an invalid relocation. Local rescue and the following natural Fixes update are checked separately to detect a second teleport. Missing prerequisites or unestablished movement are failures, not accepted coverage. Existing bridge, group and manned-siege scenarios remain separate controls. Moat-worker physical egress and loaded-Ox delivery remain open.

The mounted approach is staged by the audited native relocation only after a real
idle boundary, before the tested build, with unchanged identity/health/tribe/AI.
Captured pre-midpoint cases require Current occupancy1 and Next occupancy0. A
separate natural-approach handoff observation checks old occupancy0/new1. A distinct
occupied native6B8 auxiliary entry is checked if observed, but is not an established
ordinary-riding state; zero observed positive cases are logged `not-executed`, not
claimed as cleanup coverage. See the dated ordinary occupancy audit in the baseline.

Canonical keep construction `0x6D580 -> 0xC77C0 -> 0x74DA0` passes argument7 as direction0/2/4/6 to both the root and campground (`GameBuilding` typed+0x92); the sprite variant is a separate allocator argument. Fresh KEEP1 footprint receives IsBuilding0x400, whereas KEEP2/3 receive IsElevated0x10000000. Door children lie outside those footprints. Allocator `0xB47E0`, player registration `0xD0630`, initialization `0xC60F0`, rendering, keep handlers and path/dirty helpers do not promote that fresh KEEP1 footprint to an elevated roof. Thus KEEP1 is a no-roof negative case, not a claimed positive Fixes recovery. Blocked-exit recovery uses KEEP2. Compound campground offsets reach -12..+18 relative to the root origin, so fixture selection verifies a free radius20 square. The actual campground reference, rotation, flags and identities are checked after each native prefab creation; no roof or unit state is fabricated.

Fixes also owns an intermediate detour at `0x74DA0`: when custom player rotations are installed it replaces the incoming direction for human players with `PlayerKeepRotationData[playerId]`. The matrix therefore selects an actual AI owner, for which the same live wrapper preserves its input. No detour is bypassed and no foreign setting is changed. The initial human-owner direction assertion exposed this transform; it was a fixture prerequisite failure, not a proved native layout or rider-recovery failure.

## Confirmed Fixes 1.30 emitter defect (2026-11-17)

**Historical decision (2026-11-17): initially deferred pending an upstream correction. A subsequent explicit user instruction authorizes the isolated temporary correction of disposable test-layout copies described below. Mounted recovery remains enabled. This supersedes the earlier no-workaround decision without changing the historical failure evidence. A version bump or changelog alone still does not prove an upstream correction.**

Actual published mounted recovery stubs call `0xE2610`, not `0x187110`, with the installed Fixes configuration. `DataScanner.Scan` mutates and returns the same scanner. `InstallMountedKeepRecovery` captures that object; its deferred generator reads `CurrentAddress` only during Commit, after the shared scanner has been changed by the later Pathing scan. `0x187110` has no standing/terrain refusal: with prepared native738/73A it unconditionally updates71C/71E and72C. The observed correct Next coordinates with unchanged Current coordinates are therefore not a valid recovery. Runs046/047 reproduce the failure with own rescue off/on;047 records the published CALL target. These historical runs used the unmodified foreign feature; it remains active in subsequent testing.

### Historical copied-layout correction (removed 2026-11-26)

The following describes the earlier test setup, not the current runner. Historical archives and commands retain that meaning.

The correction captures the numeric address immediately after the scan and stores that UInt64 in the deferred generator closure. The authorized implementation is an offline Cecil CLI under `TemporaryFixes`, not a runtime detour: `Run-Tests.ps1` first copies installed dependencies into its allocated workspace `.work` layout, invokes `--patch-copy` on that copied `fixes.dll`, then passes the layout to the official transactional runner. The original installed package and external source clone remain unchanged. The runner temporarily installs the corrected copy and restores the original after each scenario. No published native code is repatched.

The CLI pins normalized complete `InstallMountedKeepRecovery` and deferred-generator IL, including relevant operands and exception regions. It changes only the captured field from DataScanner to UInt64, inserts the immediate CurrentAddress read and removes the deferred read semantically. The recovery emitter and MVID remain unchanged. The serialized candidate is revalidated before atomic fixture-file replacement; paths outside workspace `.work` and reparse points are refused. No whole-assembly hash/version gate is used. A matching known corrected capture is an explicit skip (also accepting no inert NOP and the name `relocateAddress`); no installer is explicitly not applicable. Unknown relevant IL, including an unreviewed different future fix, is rejected before changing the fixture.

The separately authorized temporary package correction applies to both experimental-off and experimental-on controls. Existing experimental settings still gate only our rescue and its narrow suppression scope. No foreign setting or additional modsetting is introduced. Current offline tests cover correction/idempotence/serialization, unchanged native emitter, unrelated updates, unknown-contract rejection without mutation, and original-file preservation. They do not establish that the in-game matrix passes. Corrected-layout evidence must be identified separately from unmodified upstream evidence.

Removal is confined to the `TemporaryFixes` directory, its build/self-test calls in `Build.ps1`, and its copied-layout invocation in `Run-Tests.ps1`. There was no persistent preloader or runtime companion to uninstall. That CLI/project/guide and the runner/build invocations have now been removed; current implementation: [Audits/FIXES-MOUNTED-RUNTIME.md](Audits/FIXES-MOUNTED-RUNTIME.md).

## Current runtime scanner island (2026-11-26)

The normal consumer DLL declares the generic early provider. Before Fixes activation, it uses the existing shared SafeIlHook API to edit only the verified InstallMountedKeepRecovery producer. Immediately before its original stfld, the one original Scan result is replaced with an independent `context.CreateScanner().At(capturedAddress)`. Original field type, delayed closure, emitter, native hooks and foreign configuration remain unchanged. The four removable `src/FixesMountedScanner*.cs` files own this island. No new patcher payload, DLL-file correction, global scanner mutation or game-native hook is added. General rescue OFF/ON does not disable this external recovery compatibility correction; a future BQoL master policy remains a separate decision.

On updates recheck exact used method signatures, complete installer/generator/selection-predicate/emitter IL, emitter RVA data, every module-wide access to the captured field, and relevant scanner/context helper bodies against actual loaded methods/modules. Unused new overloads and unrelated version/method changes remain accepted. Unknown relevant managed contracts warn and stay untouched. A known scalar-fixed capture is skipped; absence of the old installer is NotApplicable, not proof of a working replacement.

Before the known installer publishes its descriptors, require the actual x64 loaded CrusaderDE module, canonical SHA256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, exact base+187110 address, image bounds and readable executable committed allocation. Compare the entire 227-byte audited function in both the load snapshot and live memory; function SHA256 `C2EB3DF1DC97F5AF92F0350C00E8484804EE6D553E4529ECF36842DD622928CE`. Preserve its original two-argument emitted ABI and audited unlink/position/relink chain. A technical native failure returns only from this known void installer before AddInline and reports Error. No published hook is undone; validation does not promise protection against arbitrary subsequent foreign writes.

The ordinary build runs `tests/MountedScannerRuntimeFixture.cs` against all four active source files and the explicit selected SDK plugin/bootstrap pair; actual loaded bytes must match that pair. Its 66 offline checks include the real DMD prefix, four original deferred generator executions, full SafeIlHook precompile/publication and negative native/managed/update cases. Scalar vectors and serialization remain in memory only; offline tests execute no generated game-native code. The removed CLI/EXE/DLL-writing path has no fallback.

Real Unity-Mono acceptance `149/40d2c8669dae` used unchanged original Fixes 1.31 in the Planner profile: frontend plus 26 mounted partitions OFF/ON, 104 verified direct CALL187110/return-to-original checks. See [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md#nachtrag-149-runtime-scannerkorrektur-ohne-fixes-dateikopie-bestanden) for exact physical assertions and restoration. No separately occupied third Aux tile, actual CastlePlanner UI rotation command or foreign optionfalse game test is claimed.

### Follow-up after a Fixes update

1. Inspect the original updated Fixes package and its relevant IL before crediting an upstream correction. Record binary identity and runtime provider classification. Test without the compatibility island, or prove the known scalar-fixed/not-applicable branch installed no capture hook; General rescue OFF is not that isolation. NotApplicable alone proves no working replacement. The intended function address must remain unaffected by later shared scans.
2. With default Fixes settings (including `EnableStuckOxGruntsFix`), inspect the actual published mounted hook calls. On the audited native image the intended target is `0x187110`, not `0xE2610`; if the native image changes, resolve and audit the target again rather than reusing these RVAs.
3. Repeat the mounted keep scenarios with our rescue off/on. Verify actual current position at the valid campground exit, identity, health, tribe and occupancy. Correct Next/target coordinates alone do not establish recovery. Include all four rider types, rotations, committed steps and the blocked-exit control; inspect actual built directions when CastlePlanner participates.
4. Mark the upstream issue resolved only after both the unassisted emitted call and gameplay behavior are verified. Distinguish original-package evidence from runtime-assisted evidence. Then remove the four compatibility files and their ordinary test wiring together; retain historical evidence. Unreached cases or fixture preparation failures remain open, not proof that upstream is broken or fixed. Record the tested version and evidence here.

CastlePlanner is another direction producer: its human-start preparation changes the native argument at its own start-call site and `FixesKeepRotationCompatibility` temporarily aligns/restores Fixes' rotation data. `KeepDirectionReader` verifies and reads typed92. These sites do not overlap our worker span; tests must inspect actual built directions. The `Planner` test layout includes the installed CastlePlanner package without editing its source or settings.

Required update checks: full DLL/layout identity; decode caller and both branches; external and local incoming edges; Script Extender, Fixes, APIShared and Pathfinding site ownership; live adjacency and stub equality; execute tests/WorkerGuardFixture on the real backend before game tests. Native fixture success is not gameplay or multiplayer acceptance.


## Pure mutation-envelope contracts (2026-11-25)

Before admitting `RescueMutationFootprints` on another native image, re-audit `69850/6A620/E3360`, the brush-count and offset tables, flags0x30 expansion semantics, cardinal neighbour order/row packing and exhaustion behavior. Recheck that every preliminary/nested writer preserves these expansion bits, or supply an audited overlay; pre-cleanup flags must not silently stand in for changed terrain. The planner never calls native scratch enumerators.

Recheck all23 creator-family dispatches and compound tables, farm preparation/plot variants, Keep effective direction plus root/camp brush footprints and goods subcreator, Quarry's eleven perimeter attempts including outward expansion beyond width13, and both AIV-cleanup passes plus complete linked removal closures. Unknown/missing contracts and exceeded fixed budgets must remain explicit rejection. The pure fixture is behavioral coverage of the planner, not installation, gameplay, multiplayer or complete recovery acceptance. AIV cleanup remains gated by its additional complete-closure/flags contract; unsupported square sizes remain visibly unhandled.


## Read-only native footprint adapter completion (2026-11-25)

The earlier AIV brush/Keep-replacement/size14 exclusions are now closed for the audited image. `NativeMutationFootprints` uses live scalar reads only; source manager bases are405EDB0 (tiles),64CCBB0 (buildings), or the qualified SDK AIV-system pointer. It owns no native patch or native function invocation. Recheck row/Y/availability tables402FF2C/3AAE2A4/3A11EA4, neighbour table405EDB0 plus Y-deltas2D2E54, Flags48F71B0 and BuildingLayer4B6AA50. All accesses use their audited strides, not full-grid tile IDs.

Recheck typed building base64CCC0C, stride32C, IDs1..3999, manager high-water+50, fieldsD0/D2/D6/D8/EE/F0/F8/1C8/2A8. Recheck C43A0/B8310 compound propagation, B8ED0's36-field farm lookup even for shorter plot families, B8E00 preparation lookup and terminal Apple cleanup11. Keep5D3A0 checks its own default anchors and all3999 Alive3 slots, so before-scratch alone is not its complete admission test. New Keep envelopes conservatively include all four downstream rotations.

For cleanup flag projection, verify every relevant61FC0/vegetation/refresh writer preserves exactly RealityEdge|MapBorder0x30. Do not project any other tile state as unchanged. Resolve the indirect effect handler in9B2B0: canonical2DA9C0[30]=9A6E0; read the actual code through9BA16 and its shared return instead of truncating at the dataset's size1778. The generic FEE50 spill-writer callgraph path is infeasible for B5C40's effect1E; re-audit this parameter restriction if any dependency changes.

61DB0/619C0 initialize only sizes0..13. Cleanup size10 nevertheless reads196 size14 records from mutable adjacent native data8EBA90..8ECCF0. Capture those exact live records once in the synchronous invocation, preserve valid duplicate/remote offsets, and reject invalid arithmetic/coordinates. Never replace this path with an invented14×14 rectangle or a static-file table. Ordinary squares1..13 retain their complete unique-square validation.

59730's high-ground alternative invokes scale1 KillingPit at the same tile; AIV-Moat envelopes include it without reproducing its guards. Test nonterminal639C0/69470 using signed byte wrap for all256 progress values, including Fill0 after Chore41. The proven early-return path performs three scalar reads and no footprint allocation/discovery. The source broker still observes all original invocations.

Recheck SDK mapper conversion against native C77C0 and scales against6A190 on SDK updates. The audited installed converter differs for legacy MAPPER_KEEP4/KEEP5 only: managedKEEP_FOUR/FIVE versus nativeSTRUCT_NULL and scale-1; the adapter declines these values. All440 declared values were checked offline. Fixed read/work/closure budgets remain explicit, invocation-local caches never survive a mutation, and successful pure tests remain distinct from gameplay, installation and multiplayer acceptance.


## Archer stationary animation proof (26.11.2026)

Recheck full13F540 branches7/8/11 and their entry through182B00 table321CB0[ARCHER22]=13F540. Verify the raw jump tables141CF4/141C98: AI1 is the141C7D return, not a normal idle branch. Close idle facing197BC0 and positional recovery181890, as well as the0/2/101 and4->5->6 combat owners and18E9A0 command/target/deferred-order branches. No new native entry or hook is installed.

The new Archer-only stationary schema records the existing common ranged99C/6F8, melee996/998 and context9F6/9F8/A00 fields plus native unit748/74A deferred goals (typed GameUnit bias65C). It requires empty target/deferred fields and command0 or MoveHerePosition3; all other stationary command schemas and active phase contracts retain their prior layout. Recheck producer/consumer semantics before changing any admission. Existing current-pose, native-original receipt, final-original source phase and refusal-fallback guards remain mandatory. See the dated Archer section in TRAPPED_UNIT_RESCUE_AUDIT.md; offline source fixtures and real game acceptance are separate evidence.


## Ordinary-step cadence fixture inventory (26.11.2026)

At audited native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2, recheck the complete182B00 caller gate and1855A0 inner loop before reusing the automatic command-Moat timing fixture. Native916 is a signed16 budget passed by182B00; signed16 A2A is added, then the0-based inclusive loop performs budget+1 iterations. Admission also depends on signed16 wait74C and9A2 plus signed32 DWORD elapsed9AC-9A8. The fixture reads these values only and predicts the next nonwrapping fresh-unit update; it must verify the actual following counters and physical pose before recovery.

Read inventory with GameUnit view bias65C: native74C/typedF0 signed16 wait;916/2BA signed16 budget;9A2/346 signed16 caller term;A2A/3CE signed16 extra budget;9A8/34C and9AC/350 DWORD counters;985/329 BYTE animation gate;9D6/37A signed16 animation phase;90C/2B0 direction;750/F4 substep;740/E4 complete DWORD midpoint marker.985 modes1 and2 have phase gates4..11 and5..9;984 is a different failed-step cleanup field. Recheck actual widths, signedness, all skip/early-return paths and the restoration of speculative param4=1 updates. Do not infer timing from335C98 alone.

Canonical335C98 direction thresholds are4,4,3,3,3,4,4,4. The observed native Spearman916=1/A2A=0 publishes odd substeps: fresh stepSS1, thenSS3, thenSS5. North direction0 commits at the internalSS4 iteration. The fixture therefore selectsSS1->SS3 before midpoint andSS3->SS5 after midpoint, guarded by the real caller due predicate and an actual pre-recovery read. Native counter/AI/pose writes and weaker recovery guards are not a valid fixture correction. These are static native contracts and test preconditions; game acceptance must still be demonstrated by the actual recorded pre-drain and original-call outcomes.

## Isolated /devdiag no-new-Jesters test world

Owner: only `Fixture.PerformanceNoJesters.DevDiag.cs` / `NoJestersState.DevDiag.cs`, profiles `rescue-diag-nojesters4000-on` and `rescue-perf-4000-nojesters-passive-on`. This is a deliberate artificial test world, not a rescue feature, gameplay setting, public API or change to existing performance profiles. Normal builds have no reader/setter/typed helper references. Canonical hash `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; no unknown-hash fallback and no hook. Full native/managed lifecycle evidence: `Audits/JESTER-PERFORMANCE-WORLD.md`.

- `37EDF82`: the two-byte Jester creation-enable WORD read by fullC23C0. Only one zero write in the exact own fresh NativeLoaded Skirmish; loaded own saves must already preserve0.
- `37EDF80`: adjacent Lady WORD, read-only before/after every operation; never use an overlapping DWORD write.
- `C29AB`: `66 39 2D D0 B5 72 03`, native `cmp word ptr [rip+disp],bp` targets37EDF82. Live exact-byte check rejects a foreign patch.
- `951C1`: `C7 05 B5 8D 75 03 00 00 01 00`, Skirmish initializer stores DWORD10000 at37EDF80. Live exact-byte check corroborates the separate words and defaultJester1.
- `379CFD8/379CFDC + playerId*583C`: original tracked Jester unitID/global. `playerId` is1..8, not a zero-based lobby/member/array index. All eight pairs must be wholly zero; any existing/stale/inconsistent pair rejects setup, without deleting or modifying it.
- `3665F28` signedInt32 game-mode3 and `8574B90` signedInt32 native network-mode99: necessary independent actual Skirmish checks, not interchangeable enums.
- Save directory `336B20`, entry64 at337120, stride24: pointer37ED180, flags0, length82474, type1023. Exact live pointer/size/type checks corroborate the source lifetime and serialized data extent. Flag lies at section+E02. Native52B0/87D0 serialize/restore it.

All addresses must lie in the selected actual CrusaderDE module, whose file SHA and base match the already initialized NativeRelocation image. VirtualQuery must prove the complete access span belongs to committed MEM_IMAGE allocation with readable protection; a write additionally requires data PAGE_READWRITE/PAGE_WRITECOPY. No VirtualProtect or executable write occurs. The sole state mutation is Marshal.WriteInt16 at the validated two-byte flag.

A missing/changed image, code anchor, section record, scope/path/run, native world, timing, monitor, mode, initial word or pair invariant fails before setup write. Later unexpected word/Lady/pair/HP/lifetime changes reject the test; never continuously reapply0. Fresh restoration requires the same still-qualified world and unchanged owned0 state. A loaded world is never changed back to a fresh world's original1; unload/replacement invalidates old ownership. Own controlled saves are removed by the existing transactional runner, and a subsequent ordinary fresh game uses Vanilla's own initializer.

Update procedure: reclose the full producer/read/write/reset/save/load/peace/victory and managed callback ordering, not just the two anchors. Unknown hash remains unsupported. New addresses or widths require a new reviewed audit; the exact original two-byte and eight-pair contracts must not be guessed from layout similarity.


## Natural civilian removal fixture: migration preconditions (27.11.2026)

Only `Fixture.CivilianRemoval.cs` reads the public `GamePlayerResources` view through `GamePlayerManagerAPI.TryGetPlayerResourcesById` (players1..8, array index playerId-1). No player writes, new callable RVAs or hooks. The already checked canonical hash above remains mandatory; actual installed SE public-field visibility and runtime layout guards are separate prerequisites.

SE resources start at player-manager+1343FC, stride583C. D2C40's manager+13446C is view+70; its preceding cumulative DWORD is view+6C. Historical public names are misleading: `r_PeasantSpawnProgressRequired` at6C is cumulative progress, `r_PeasantSpawnProgress` at70 is the signed increment. Read both; Hovel creation alone does not prove a spawned Peasant. Recheck complete D2C40 and its D1AD0 scheduling and CDB20 recount before changing the setup wait contract.

Resource offsets (no unit65C/building5C bias): UInt32 popularity60, cumulative6C, housing74, readyPeasants88, existingPeasants8C, KeepID94, KeepDoorID1D4, doorX1D8, doorY1DC, population2180, paused2210, WinLoss2244; Int32 increment70. Runtime setup checks the actual583C struct size and every consumed offset. Missing/changed fields, player bounds, Keep/door ownership, housing, positive progress or initialized simulation reject setup rather than inventing raw Peasants or changing gameplay admission.

The fixture waits for actual population progress, then takes one bounded setup census and follows captured lifetimes only. Farm replacement checks original buildingID/global and a genuinely new employee global after native cleanup/hiring. Those checks do not promise unconditional instantaneous replacement in the rescue feature.


## Auxiliary cleanup acceptance readers (27.11.2026)

Owner: `Fixture.AuxRemoval.cs`, controlled tests only, canonical native hash above. No new native callable target or hook. GameBuilding pointers use native-header bias5C. Employment IDs at1FA+2*slot and Globals at204+4*slot are separate from per-pass heartbeat214+2*slot; setup validates the matching actual typed offsets through Marshal. Miller slots0..2, Mine slots0/1 and Cow-farm slots1..3 are explicit audited domains. C60F0 clears214/216/218/21A after the building handler; the following unit pass may republish them. The first post-marker building pass may still consume the previous heartbeat. Do not require immediate synchronous zero or forbid that last natural production update.

Miner1 active214 exists only hiddenAI1, which this removal policy excludes: visibleR2 acceptance proves employment cleanup only. Visible Miner2 is restricted toAI1 before any native setup normalization; its193F10 physical-building check protects against remote extraction. Miller uses genuineAI3/8 movement and actual wheat input, without remote normalization; hidden21C is not claimed. Tanner uses genuineAI3 movement and reciprocal Cow4/5 reservation, without remote normalization or reissue. Read-only unit fields:9EE/9F0/9F6 are WORD,9F8 is DWORD, Cow timer920 is WORD, with the existing GameUnit bias65C. Cow5 may use its1001-update timeout before6/110/Life3; stale reference bytes alone are not an active reservation. Actual original HP and death8F8 remain checked. Full closed contracts: Baseline RECRUIT_TRANSFORMATION auxiliary-worker-bindings-and-ordinary-ai-hiring section and curated units/economy topics.

Public capacity reads for the natural Miller input run only in the existing shared simulation callback; the UI automation monitor alone does not establish SimulationMutationScope. Saved v5-removal acceptance uses the actual public MapArchive plus signature-checked selected SDK decoder read-only. This introduces no JSON serializer or new native save entry.
