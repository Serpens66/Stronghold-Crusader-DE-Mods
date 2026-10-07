# Assassin direct gatehouse climb: native audit

Audit date: 2026-10-07. Installed CrusaderDE.dll SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
CURRENT.json and DATABASE_INFO.json identify this same binary. Complete function
pseudocode, caller/callee lists and disassembly from the installed binary are
preserved in `_inspect/AssassinGateClimb/0x<RVA>.txt`.

## Confirmed static result

Vanilla's physical Assassin climb implementation supports gatehouse roof tiles.
Its special path search and path reconstruction nevertheless reject a climb
edge when either endpoint has a nonzero building ID. Gatehouse roofs have both
the wall flag and a building ID. Thus a freestanding gatehouse has no direct
Assassin climb edge in the vanilla special path graph. An ordinary movement
connection through an adjoining wall can bypass this restriction. This explains
why wall-connected access can work without proving any particular rare observed
climb was direct. No controlled game run has yet established that observation.

Function names classified as candidate in the semantic database remain candidate
names. The conditions below are confirmed static instructions for this hash;
runtime behavior of the proposed change remains untested.

## Gatehouse tile construction and topology

- Building dispatcher 0x6D580 selects 0x74240 for type 45 and 0x74080 for type 46.
  Both constructors enumerate the footprint through 0x69850, raise the tile
  height by 90, set tile property IsWall (0x100), assign the owner, and write the
  allocated 1-based building ID into the building grid. They initialize building
  path topology through 0xD8510 and update tiles through 0x725E0.
- Grid building IDs reside at RVA 0x4B6AA50, wall/property grid at 0x48F71B0,
  height grid at 0x4DDD350 and movement direction grid at 0x51890D0. These are
  separate grids. Removing an IsBuilding flag does not remove a building ID.
- Load/topology flow 0x71670 -> 0xC8380 -> 0xB9510 preserves IsWall on the complete
  type 45/46 footprint and clears IsBuilding (0x400). It does not clear roof
  building IDs. Type 47 follows a separate high-wall branch and is outside scope.
- 0xC5040 and 0x725E0 call 0xD8CE0 to update gate passage direction bits according
  to gate orientation/state, adjacent surface legality and height difference.
  These ordinary movement connections must remain authoritative.

## Human target recognition and synchronized movement

- Input flow DLL_TroopSelection / DLL_RunTick -> 0x8C5F0, with target resolution
  0x79B90 -> 0x90830, distinguishes buildings from ground and walls.
- In 0x8C5F0, selected-Assassin predicate 0x196870 and the building-type switch
  already permit gate types 45/46 to enter the movement branch when the
  Assassin reachability query 0xE2CA0 succeeds. An unsuccessful query leaves
  building attack handling available. This is a reachability restriction, not
  absence of all gatehouse command handling.
- 0xB70C0 probes perimeter attack reachability; 0xB72C0 is a separate wall attack
  probe. They must not be replaced with unconditional climb acceptance.
- Ground move staging 0x195E30 emits Chore 17. Its consumer 0x10AE0 -> 0x196100
  -> 0x11B520 issues group movement. 0x199C30 handles separate command/sound
  bookkeeping and is not the movement consumer. Input release must not be
  replaced by an unsynchronized second command.
- 0xE2CA0 may return immediately for a shared path component or consult its pair
  cache; its forced query path uses 0xD9C40. Cursor and group queries therefore
  need the same climb-edge eligibility as actual route creation.

## AI and route generation

- AI tribe processing 0x117520 -> 0x10AA20, case 0x41A, already calls 0x1140C0.
  This gate-target action queries 0xEAB80 at increasing search budgets, resets
  tribe members through command 3 in 0x11E960 and moves the tribe to gatehouse
  coordinates through 0x11B520. Target search already recognizes live, nonburning
  enemy gatehouses of types 45/46; it is not proof of a realizable climb route.
- 0x117820 identifies an Assassin-only tribe. 0x11B520 then builds a reverse
  Assassin distance field through 0xD9C40 before distributing per-unit movement
  through 0x196280. Mixed groups use ordinary path constraints.
- 0x196280 -> 0xF4930 creates/reconstructs a per-unit route. 0xD9C40 callers also
  include 0xE2CA0 and 0x123090. Negative target coordinates request a flood field;
  the flood's zero return does not necessarily mean no useful field was built.
- 0xD9C40 accepts an ordinary direction-bit edge first. Its extra climb branch
  requires a cardinal direction, permitted target surface (mask 0x4A5014B1 or
  the vanilla vegetation exception 0x107160), zero source AND target building
  IDs, and IsWall at one or both endpoints. This is the direct gatehouse blocker.
- 0xE1640 reconstruction mode 3 repeats cardinal/zero-ID/wall constraints when
  ordinary direction bits do not connect the pair. Relaxing the search alone
  cannot produce a consistent usable path. Other reconstruction modes, ladder
  logic, search limits, stamp ownership and packed path semantics remain intact.

## Physical step, animation and completion

- Simulation 0x182B00 -> 0x1855A0 consumes low-first packed direction nibbles.
  Existing Assassin states 126..129 suspend ordinary movement stepping.
- For a new step, 0x1855A0 calls 0xDCE60 then 0xDCD60. The Assassin branch of
  0xDCE60 accepts ordinary direction connections first, otherwise validates the
  target surface and IsWall. Unlike the path search, it has no zero-building-ID
  requirement. Unit type 73 enters ascending state 126 or descending state 128;
  height difference, facing and animation/progress fields are initialized there.
  Other movement profiles and ladder branch are separate and outside scope.
- Assassin state dispatcher 0x16CD70 progresses 126 -> 127 and 128 -> 129,
  updates climb animation/progress, then resumes ordinary state 101.
  0x180A80 finalizes next-tile coordinates and actual height with ordinary
  occupancy remove/add helpers. 0x188340 is the normal movement-arrival test.
- Ordinary gate interaction/capture and combat remain vanilla. Merely setting
  movement or attack context is not evidence that states 126/127 occurred.
  A runtime test must observe command, route, physical climb state and arrival.

## Workspace and Fixes compatibility

- BugfixesAndQoL AssassinPathfindingRuntime owns the 0xD9C40 function replacement.
  AssassinPathReconstructionPatch owns the 0xE19D8 / 0xE19F9 reconstruction guards.
  Its current reservation policy permits nonzero IDs only when the endpoint's
  ordinary movement mask is nonzero. It is not a gatehouse-specific contract.
- AssassinSelectionAdapters own selection call adapters at 0x8D724, 0x8E2B8,
  0x8E550, 0x8F325, 0xB7161 and 0xB7321. The Script Extender owns predicate
  0x196870. Positive original results remain authoritative; competing hooks at
  any of these owned addresses are unacceptable.
- AssassinClimbRuntime and AssassinClimbCancellationRuntime govern existing
  player climb settings and stop/cancel semantics. A gatehouse experiment must
  respect those settings and cannot reset active climb states independently.
- Canonical external Fixes source was checked first. SmarterSiegeAssassins hooks
  gate target filtering at 0xEAD8C and departure at 0xEACC3; the optional non-siege
  target filter and capture handler affect later AI targeting. Fixes preserves
  live/type/fire eligibility and does not remove the D9C40 zero-ID climb rule.
  A testmod must preserve Fixes' captured-gate/ally decisions, not replace its
  complete gate selection action.

## Shared implementation decision (2026-10-07)

The user authorized central ownership in APIShared. AssassinPathAPI owns the
D9C40 detour and all four building-ID guard adapters. BugfixesAndQoL registers
its weighted builder and consumes the same gate endpoint rule. The experiment
does not replace human command dispatch or Fixes AI target selection. Physical
climbing remains Vanilla. Runtime gameplay acceptance remains pending.

Audited spans: D9C40 NativeX64 Indirect-only / 10 bytes; X64InlineHook D9F0C /16,
D9F1C /15, E19D8 /18, E19F9 /23. Complete containing functions and cross-function
direct edges were checked for interior incoming branches. Source-linked native
tests execute the productive assembler, decode it completely, validate actual
installed RedBird displacement and detour pointer-slot contracts, and exercise
active/inactive gate eligibility. See `_inspect/AssassinGateClimb`.

## First human acceptance (2026-10-07)

18:32:20-18:36:50 editor run with both testmods, weighted improvement and Fixes 1.25.1: seven completed ascents and two descent sequences; user confirms commands worked. Cursor, formation and physical climb code remain unchanged in the subsequent gate-safety step. This confirms the human experiment, not yet all AI, switches or mainmod regressions. See ENEMY_GATE_ASSASSINS.md for the shared classifier and conservative distance-field guard.
## Coordinated review and installation completed (2026-10-07)

After the parallel APIShared work finished, the installed APIShared was verified current (package/install SHA256 equality and new public contract present); no redundant APIShared build was needed. The final review also required a weighted diagnostic climb endpoint to match the exact gate responsible for the blocked edge, rather than a neighboring gate. Actual diagnostic runtime tests now pass 46 assertions. The main movement test fixture was adapted to the parallel UnitAccess.IsReallyAlive addition (enum alias and low-word death-marker field); production UnitAccess was preserved.

All pre-build Native/Interop, compatibility, JSON, Lifecycle, permanent-hook, XAML and CRLF checks passed. Independent Assassin tests: 15,862 assertions; Gate tests: 9,900; installed RedBird Assassin execution tests: 1,396. The full BugfixesAndQoL build driver and all its regression suites completed successfully, with zero errors and the existing MSB3277 assembly-binding warning. EnemyGatePathfindingTest build.bat completed with zero warnings/errors. Both drivers installed successfully. Installed/local package hashes match for APIShared, BugfixesAndQoL and EnemyGatePathfindingTest; evidence: _inspect/EnemyGateBuildingContextAudit/transition-installed-hashes.json. Successful build logs: transition-main-build-complete.log and transition-gate-build-complete.log in the same directory. Earlier failed-attempt logs remain historical evidence.

AssassinGatehouseClimbTest is unchanged and needs no rebuild. Versions and README files remain unchanged. No new native hooks or changes to cursor, formation or physical climb execution were introduced. In-game acceptance remains pending: Assassin commands to the roof and behind open/closed enemy gates, ordinary soldiers denied the ground passage, improvement enabled/disabled, and AI behavior at the same setup. Bridges remain outside this acceptance. Native-only historical mask overlaps are not retroactively marked resolved.

## Exact weighted single-unit route publication (2026-10-07)

Native reference hash: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
E1640 reconstructs from step distances, with a mutable best distance inside its direction loop.
For target-first nodes T(1,1,d4),R(2,1,d3),P(2,2,d2),S(1,2,d1), it can accept R at direction 2,
then S at direction 4 against the already reduced distance 3. This replaces the cheap
S->P->R->T entrance route with a costly S->T climb. Weighted parents and tick costs
are not represented by the published unit-step distances. This is a confirmed static
counterexample, not yet causal proof of the user's particular gameplay run.

APIShared's existing F4930 owner now opens a synchronous, thread-local single-unit
handoff. Bugfixes stages a copied exact low-nibble-first forward direction sequence;
F4930 still executes once, then its checked output buffer receives the prepared bytes
before the 196280 consumer latches length/cursor/state. Successful staging retains the
original D9C40 field/result until the outer builder publishes. Failure inside a unit
frame preserves native data. Flood/continuation calls never stage; standalone queries
retain the checked stamp/distance publication path. Nested builders shadow all outer
frames, including unqualified/manual-probe frames. Delegates live only in the synchronous
frame, whose Leave restores its predecessor in finally; native hook roots remain permanent.

Existing hash-bound data contracts (no additional hook, fixed RVA, AOB or executable write):
- Path manager at module+60AD660: source int32 +8/+C, destination int32 +10/+14;
  Assassin flag int32 +88, moat flag +84, later alternate-builder flag +94.
  Exact publication requires Assassin enabled and the other two flags zero.
- F4930/E1640/E4E90/196280 prove output pointer +155F60, direction-count int32 +155F68.
- Unit manager module+67E8400: buffer +B4FE78 + one-based unitId*1000, capacity
  1000 bytes/2000 directions. Buffer arithmetic derives the ID and checks it against
  UnitAccess, type, true life, Global-ID, control player and native movement start.
- Existing grid bases: connections 51890D0, direction masks 312620, row table 402FF2C,
  heights 4DDD350, surfaces 48F71B0, building IDs 4B6AA50. Physical DCE60 accepts
  source-forward OR destination-reverse before climbing. E1640 tests its own
  target-to-predecessor connection, so reconstruction capability is checked separately.

All addresses derive from the canonical installed-hash disassembly and the existing
shared command layout validator. Fixed layouts have no independent semantic fallback;
search scope is the supported installed module only. Unknown hashes retain the existing
fail-closed feature initialization. F4930 hook resolution/backend remain unchanged;
its existing section-bounded function signature fallback and displacement checks apply.
On native updates re-audit field layout, buffer ownership, nibble reversal, movement-start
selection, alternate builders and the terminal 196280 consumer before permitting publication.
No new Assembly-CSharp access. Fixes gate targeting and Script Extender selection ownership
remain unchanged; the existing soft dependency loads the mainmod after Fixes.

Regression sources compile the productive handoff, encoder, and actual shared builder/
publication methods. Tests cover the expensive reconstruction shortcut, exact buffer bytes,
length, ID reuse, changed policy/pointer, nested frames and native exceptions. Physical
walking/climbing and open owned/captured gate roof acceptance still require an in-game run.
README files and versions remain unchanged during that acceptance.

Validation completed 2026-10-07:
- 15,890 Assassin A*/Dijkstra, physical-transition and exact-publication assertions passed.
- 1,396 source-linked Assassin checks passed on the installed NativeX64 backend.
- Actual F4930 wrapper/publication methods compiled in an isolated memory fixture:
  exact entrance bytes/count, changed identity/policy/buffer and native exceptions passed.
- Shared source/API visibility, JSON, lifecycle, permanent-hook, XAML, CRLF, UnitAccess
  and Fixes compatibility preflights passed; existing native command/moat suites passed.
- APIShared public API regression now permits only the audited context parameter of
  TryStageWeightedRoute in addition to the existing native builder bridge exceptions.
- Elevated direct build.bat drivers completed for APIShared and BugfixesAndQoL.
  The first APIShared driver stopped in its public API test before runtime build;
  the corrected test passed before the successful driver retry.
- Installed standalone APIShared SHA-256:
  A1B08496BEE16EDCACB66B657BD342168F6B74B7F0508BF7F0ABC02F964A2DB1.
- Installed standalone BugfixesAndQoL SHA-256:
  1041E2AFFD6B90C2E3A33774B02E58F9F5EE64F7F4B1162CB2AB7C0FFF1799DE.
  Both match their local package DLLs. Build logs: _inspect/AssassinGateClimb/
  exact-route-APIShared-build-retry.log and exact-route-BugfixesAndQoL-build.log.
- No in-game acceptance claimed. Test roof orders on both gate sizes, owned/captured
  open gates and different approach sides, with Improved Pathfinding on/off.

## Assassin request-index life filter, 2026-10-08

The weighted request index now uses APIShared.UnitAccess.IsReallyAlive rather than AliveState alone. The confirmed low-word death marker at GameUnit+0x29C excludes death-animation/corpse records that remain IsAlive; the unrelated upper word is ignored. Such records previously could create false player/control-player ambiguity or inflate the slowest movement delay when sharing a coordinate with a live Assassin. No native hook, path-edge eligibility or route-publication contract changes.

Regression tests compile and execute the actual BuildRequestIndex, its request record and the actual reference-view life predicate. Cases cover live units mixed with foreign/same-player corpses, corpse-only coordinates, deleted/empty slots, nonzero upper word with zero death marker, preserved slowest live speed and preserved ambiguity between living players. The Assassin suite passes 15,892 assertions. This is automated evidence; no new gameplay acceptance is claimed.

## Weighted-route validation and request cost, 2026-10-08

The complete feature audit above still applies to the unchanged installed native hash.
Search/physical walking accepts the source forward connection OR the destination
reverse connection; E1640's reconstruction connectivity is checked separately.
F4930 capture and terminal 196280 consumption remain under their existing ownership.
Script Extender v2.13.1 retains its selection override, Fixes v1.25.1 its targeting
decisions; no hook sites/backends, movement costs, neighbor order, heap ordering or
search budgets changed. Published hooks remain permanently rooted and installed.

E1640 mutates its comparison distance within the eight-direction loop. For the
target-first route T=11(d4), R=12(d3), P=22(d2), S=21(d1), width 10, it first
accepts R and reduces the comparison from 4 to 3. S can then pass `distance >=
comparison-2 && distance < comparison`, even though S lies three route steps behind
T. Testing only one/two earlier steps misses this candidate. The conservative field
validator now examines every adjacent node with smaller stamped distance: at most
eight neighbor probes per route node, no pairwise route scan. The regression rejects
forbidden S->T before any replacement field is written. This proves the native
reconstruction counterexample, not a new observed gameplay failure.

The synchronous F4930 frame optionally provides the already bound unit's full control
word and current speed. The profile proof uses manager/buffer assignment, one-based
unit ID, live/type/Global-ID, actual native movement start, start/target, current full
control word and speed, path flags/counters and capacity. Moving units use the native
next-tile start when appropriate. Unknown/invalid/nested-shadow contexts provide no
profile. Ownership is restricted to the registered weighted builder. Gate-mask
queries still require the complete control word in 1..8; without a mask the existing
low-byte behavior is retained. Unbound queries retain the existing command index.
Both added unit fields are public in the installed genuine SHCDESE metadata:
`r_CurrentSpeed : UInt16`, `N00000569 : Byte`. No new Assembly-CSharp member is used.

Each A* resolves its suffix dictionary once. Exact staging rejects unqualified
contexts before route/encoding/delegate allocations; C# delegate captures are in a
separate qualified method to avoid method-entry closure allocation. A command's
immutable cached route retains its packed bytes and bound validator delegate.
The delegate rechecks current map epoch, settings, climbing, gate policy, physical
transitions, costs and direct-gate state every time; no validation result is cached.
APIShared still clones bytes at its public boundary and performs all final unit,
buffer, context and route checks before writing. Direction encoding and cached-route
validation share a static lookup with explicit invalid/row-wrap rejection.

Final validation and packed publication are now included in command publication and
total-request phase timing. The per-frame completion callback preserves nested
isolation, reports publication failures and cannot change the result if measurement
code throws. No per-unit log or cross-command route cache was added.

Production-method correctness, load measurements, methodology and limitations are
documented in `_inspect/AssassinPerf/RESULTS.md`, with raw CSVs and retained preflight
logs in that directory. The repaired EnemyGatePathfindingTest source references point
at APIShared's UnitCommandPathRuntime; its complete suite passes 9,900 assertions.
Automated evidence covers Dijkstra agreement, cache bounds, cheap entrance/faster
climb/disabled climb, moving units, profile changes/ID reuse/invalid full player,
nested frames, fallback and current installed NativeX64 contracts. Offline timings
exclude real native Vanilla work and do not establish game performance. User reports
that the earlier roof fix worked; this optimization still needs game acceptance on
owned/captured open gates, both gate sizes and approach sides. README/version unchanged.

Final comparison against Git f57dfdb02 and 8fe105a11 checks unchanged native fallback,
physical/cost/cache validation, heap, native field publication and F4930 wrapper
contracts as normalized C# syntax. The A* body differs only in the once-per-search
suffix lookup. Accumulated command node/heap counters are now 64-bit: 10,000 searches
over sufficiently large reachable regions could overflow the previous diagnostic
Int32 sums. Per-search counters, budgets and route selection stay unchanged; the
overflow regression exercises the actual accumulator methods.

### Performance diagnostics default off (2026-10-08 follow-up)

BugfixesAndQoL now uses the compile-time `PerformanceDiagnosticsEnabled=false`
switch independently of gameplay and testmod policy observers. Disabled active
path methods contain no clock/statistics calls in the compiled IL. Exact-route
staging does not create a measurement callback; APIShared clocks publication only
when such a callback was supplied. Native contracts, original builder invocation,
search budget, route edge counts used by cache validation and live final checks
remain unchanged. Both switch states pass the source-linked Dijkstra, publication
and prior Git contract regressions (16,229 assertions). Detailed measurements and
limitations are recorded in `_inspect/AssassinPerf/RESULTS.md` and the diagnostic
switch CSV. No new game acceptance or measured native Vanilla timing is claimed.