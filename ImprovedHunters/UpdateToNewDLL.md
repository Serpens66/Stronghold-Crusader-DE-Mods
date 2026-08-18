# Updating Improved Hunters for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24651686`
- DLL size: `3450880` bytes
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

On the audited hash, the mod uses each reference RVA directly and validates its
local semantic pattern without scanning the DLL. On a changed hash, each native
feature performs an executable-section-limited unique pattern search and then
validates its derived branches, tables and operands. An absent, ambiguous or
semantically inconsistent match disables only the affected feature. Raw unit
fields still require an in-game smoke test after a game update.

Version `1.1.67` changes no RVA, pattern, or native hook span. Production wiring
is now separated by feature: initial target selection and PCL filtering, active
pathfinding/visibility recovery, and correlated projectile damage recovery have
independent host gates. Chicken patches and granary management jointly follow
only `HuntChicken`; corpse timers and camel compatibility follow their respective
animal opt-ins. An update audit must therefore also confirm that every switch
mutates only its own callback path and that disabled features leave native
registers, flags, and patch bytes unchanged.

The temporary `HunterNativeVisibilityProbe`,
`HunterTargetSearchFallbackDiagnostic`, and
`HunterVanillaPathContinuationDiagnostic` are deliberately stricter: they are
enabled only on the exact audited hash and never perform a changed-hash pattern
fallback. A changed DLL therefore disables only these diagnostic paths.

## Native address map

| Source pattern | Reference RVA | Use / offset |
| --- | ---: | --- |
| `CamelDespawnTickTimePattern` | `0x158468` | signed immediate at `+13` |
| `ChickenDespawnTickTimePattern` | `0x163415` | signed immediate at `+13` |
| `TargetSelectionTypeDispatchPattern` | `0x18F262` | automatic target type dispatch |
| `ManualAttackCommandPattern` | `0x18EAE6` | explicit `AttackUnit` command path |
| `ManualAttackTargetAssignmentPattern` | `0x18ED46` | explicit target assignment before automatic dispatch |
| `ManualAttackDecisionSequencePattern` | `0x18EB98` | explicit `AttackUnit` compatibility decision; hook at `+0x2B` (`0x18EBC3`) |
| `ComparisonSequencePattern` | `0xD2AB4` | granary chicken target comparison hook at `+11` (`0xD2ABF`) |
| `HunterQueryCandidateLoopPattern` | `0x18AF70` | temporary Script Extender issue-123 actor capture |
| Native Hunter visibility wrapper | `0xA06F0` | behavior-neutral direct probe; seven arguments including context |
| Native Hunter visibility core | `0x9E350` | wrapper calls forward first and returns immediately when positive; reverse is called only after forward returns zero; `1.1.52` validates the entry and may call both directions explicitly |
| Shared obstacle-height helper | `0x6B990` | reads tile flags, building identity/type and effective obstacle height |
| Building-height type switch | `0x6B9F8` | dispatches building types `7..78` |
| Building-height dispatch targets | `0x6BAB4` | entries `0..3`; entry `3` is the normal fixed-height case |
| Building-type dispatch bytes | `0x6BAC4` | type `7`/Hunter's Hut is the first byte; Vanilla `0`, patched `3` |
| Building blocker-height table | `0x2E7C60` | type `7` already has normal blocker height `40` |
| Hunter query visibility call | `0x18B052` | must resolve to wrapper `0xA06F0` |
| Hunter direct-order visibility call | `0x18ED1A` | must resolve to wrapper `0xA06F0` |
| Unit-function table | `0x320CB0` | Hunter entry is index `6` at `+0x30` |
| Native `HunterUpdate` | `0x12FC20` | state dispatch and Vanilla Hunter movement/order transitions |
| Hunter distance helper | `0x79C0` | exact state-1 range result retained in `EDI` |
| State-0 query handoff | `0x12FD67` | query return hook at `0x12FD89`; query callee `0x18AF00` |
| State-0 `MoveHere` result | `0x12FE2A` | immediately after call to `0x196230` |
| State-1 near-target refresh | `0x130019` / `0x130022` / `0x12FF2E` | safe compare hook and Vanilla actor load; `1.1.66` reuses the exact 14-byte query-result span only for a guarded stale-moving-target State-0 replan |
| State-1 distance-28 compare | `0x1300EA` | since `1.1.56` the authoritative snapshot-based attack/continuation decision; sequence begins at `0x1300D2` |
| State-1 direct-attack result | `0x130149` | observation-only `test eax,eax` after call to `0x18E950` |
| Projectile spawn entry | `0x9B2B0` | Script Extender signature target; creates a live projectile and is not used for LOS preflight |
| Projectile manager tick | `0x9F960` | iterates live projectiles before their type-specific update |
| Common projectile flight step | `0x9EF20` | mutates live projectile motion and calls collision, height and orientation helpers |
| Projectile collision/update routine | `0x9C730` | large stateful collision and outcome path; unsafe as a synthetic Hunter LOS predicate |
| Archer-arrow type handler | `0x98EE0` | type-table handler after common flight processing; not the physical blocker predicate |
| Projectile type-function table | `0x2D99C0` | arrow type `1` resolves to RVA `0x98EE0` |

The source constants contain the complete wildcard patterns.

The automatic target selector starts at RVA `0x18E950`. A valid explicit
`TribeAICommand.AttackUnit` is checked at RVA `0x18EAE6` and assigns its target
at RVA `0x18ED46`, before the automatic candidate type dispatch.

Non-Hunters in that explicit branch call Vanilla's unit compatibility function
at RVA `0x186750`. On the audited DLL it returns `1` for chicken type `62`, and
the `test eax,eax` at RVA `0x18EBC3` therefore rejects the otherwise valid
manual order. Hunters bypass this call at RVA `0x18EB9C`. Improved Hunters hooks
only the explicit branch's test instruction and changes the well-known boolean
result from `1` to `0` when the target resolves to the same live chicken and the
attacker resolves to a live, supported ranged unit. The explicit classification
contains regular projectile attackers: archers, crossbowmen, the debug archer,
catapult, trebuchet, mangonel, both ballista types, Arabian bowmen, slingers,
horse archers and fire throwers, plus Bedouin ambushers, skirmishers and heavy
camels. Hunters bypass the compatibility call in Vanilla. Melee and support
units retain Vanilla's rejection instead of accepting an order their later
combat path cannot complete. Vanilla has already validated command, target
slot/global ID and alive state; its existing range and line-of-sight checks then
run before the regular target assignment continues. The callback is inactive
unless the mod, chicken hunting and the independent automatic-target patch are
all active.

The managed context callback must preserve `X64SmartCPUContextRegs.Volatile` in
addition to `R14` and `R15`. Vanilla reloads the unit-manager pointer into `R8`
at RVA `0x18EBB4` and dereferences it immediately after the accepted branch at
RVA `0x18EBD2`. Version 1.1.28 captured only `RAX`, `R14` and `R15`; the managed
call was therefore allowed to clobber live `R8`, causing a native CTD after the
callback had logged its successful `1 -> 0` decision. Version 1.1.29 captures
all Windows-x64 volatile registers, restores the callback's intentional `RAX`
change and returns every other live volatile register unchanged.

The dispatch target table is at RVA `0x18F9C4`, and the type-to-dispatch-index
table is at RVA `0x18F9E0`. Chicken type `62` has table index `62 - 44 = 18`,
so its byte is at RVA `0x18F9F2`. Vanilla value `6` routes to the general
acceptance case at RVA `0x18F3A6`. Improved Hunters temporarily writes value
`0`, which routes to RVA `0x18F28E`; that case accepts only attacker type `6`
(hunter) and otherwise rejects the candidate at RVA `0x18F754`.

Vanilla's per-player granary chicken update starts at RVA `0xD29E0`. Its
food/population-derived target is in `eax` when RVA `0xD2ABF` compares it with
the current native player chicken count at `[rdi+0x2048]`. The following signed
`jle` skips spawning at RVA `0xD2BA7`; otherwise Vanilla reaches the existing
granary spawn event at RVA `0xD2B4C` and the unit creation call at RVA `0xD2B5C`.
Improved Hunters hooks only the comparison and normalizes `eax` to `INT_MAX`
below the configured tracked limit or to `0` at/above it. Thus exactly the next
Vanilla spawn is permitted or denied while Vanilla retains its keep, granary,
mode, position and timing checks.

The comparison callback identifies the source player through `rbx` (validated
range `1..8`). Runtime tracking stores source player, one-based unit slot and
`GameUnit.r_GlobalId`; both `AliveState.NeedsInit` and live, healthy
`AliveState.IsAlive` chickens count. Slot/global mismatches, dead/corpse units
and deletion states are removed. Loaded neutral chickens are assigned to the
nearest active granary using Chebyshev distance, then building ID and player ID
as deterministic tie-breakers. Relevant fields are unit type `+0x8A`, owner
`+0x92`, global ID `+0x94`, tile `+0xC0/+0xC2`, corpse flag `+0x29C`, and
building alive/type/owner/global/tile fields in the publicized `GameBuilding`
layout.

The Script Extender versions through the locally inspected 1.41.0 can report
the caller's saved `RBX` value instead of the native Hunter ID in
`OnUnitHunterQueryTarget`; see upstream work item 123. Improved Hunters installs
a temporary read-only context hook at the candidate-loop anchor `0x18AF70`.
At this point `R13 = UnitManager + hunterId * 0x490`, `R14 = UnitManager` and
`ESI` is the one-based candidate ID. The mod reconstructs and validates the
Hunter ID from the exact divisible delta and consumes it only for the matching
public query candidate on the same thread. If capture and the reported ID are
both invalid, the callback leaves Vanilla's target decision unchanged. Remove
this workaround once the minimum supported Script Extender fixes work item 123.

### Temporary native Hunter visibility probe

`HunterNativeVisibilityProbe.cs` calls, but does not hook or patch, Vanilla's
visibility wrapper at `0xA06F0`. On the audited hash it validates the wrapper
entry, both core-call sequences, both relative core targets (`0x9E350`), and the
query/direct-order callsites plus their relative targets before creating the
delegate. Any mismatch disables the probe without changing Hunter behavior.

The validated native signature is
`int(context, startX, startY, startHeight, endX, endY, endHeight)`. Hunter input
uses `GameUnit +0xB2/+0xB4` and `+0xB6 + signed(+0xB8) + 30`; prey input uses the
same fields with height bias `+26`. The wrapper writes only `context +0xC`, so
the test supplies a zero-initialized private 16-byte context surrounded by
canaries. Any change outside `+0xC` disables the probe immediately. The native
global context is never passed.

Candidates are captured from the public Hunter query event and invoked later
only on the same managed thread in the established 100-ms native scan. The log
separates Manhattan query phases (`>20`, fallback `>5`, and the `>=54` early-LOS
bypass), the query acceptance range `1..432`, and the later direct-order rule
`>0`. Calls and logs are bounded; no target result, unit field, reservation,
movement order, or native branch is changed.

### Hunter's Hut visibility dispatch correction

`HunterHutVisibilityPatch.cs` corrects the exception in the shared obstacle-
height helper rather than implementing a second projectile-line model. When a
tile has the relevant building flag, helper `0x6B990` resolves the building ID
and reads its type at native `GameBuilding +0x12E`. The switch at `0x6B9F8`
maps types `7..78` through the byte table at `0x6BAC4`. Type `7` is
`STRUCT_HUNTERS_HUT`.

Vanilla byte `0` sends that type to `0x6BA3D`. With the obstacle-aware mode
used by visibility core `0x9E350`, this case skips the building contribution
and returns only terrain height. Dispatch byte `3` instead enters the normal
case at `0x6BA41`, which reads the type-dependent value at `0x2E7C60`; the
existing type-7 value is `40`, equal to the Woodcutter's Hut blocker height.
For the other helper mode, the old special case already fell through to this
normal value, so the effective behavior change is limited to obstacle-aware
Hunter's Hut queries.

On the audited hash the patch validates helper, switch, both tables, dispatch
targets, original byte and height value before writing exactly RVA `0x6BAC4`
from `0` to `3`. A changed hash requires a unique executable-section match for
the type-switch pattern and then re-derives and validates every table address
and target. Conflicting runtime values are never overwritten. Disabling the
mod or Improved Pathfinding restores the owned byte. The correction remains
disabled in real multiplayer and the map editor pending the Script Extender
`1.50.0` synchronization Chore.

### Native PCL reachability precheck calibration

The canonical Script Extender exposes
`GamePlayerManagerAPI.GetNextReachablePCLToDestinationForPlayer`. It forwards
the pathfinding context, player ID, target PCL, source PCL and an undocumented
mode to native function RVA `0xE2610` on Steam build `24651686`, SHA-256
`33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.
The audited entry signature is `40 55 41 54 41 55 41 56 48 8D AC 24` and occurs
exactly once in that DLL.

The native function returns the target PCL immediately when source and target
match, returns `0` when either input PCL is zero or no permitted PCL connection
exists, and otherwise returns the next connected PCL toward the destination.
Its traversal accounts for player/alliance-dependent gate connections. This is
a bounded connectivity query rather than the Script Extender's unbounded
managed A*.

`c_game_unit_issueorder_movehere` at RVA `0x196230` calls this function at RVA
`0x1964D3`. It passes public `GameUnit +0x92`
(`r_ControllableForPlayerId`) as the player, target/source PCL in `R8D/R9D`,
and public `GameUnit +0x35C` (`N000001CA`) as the fifth mode. The return is
stored into the unit's path-connection field and a zero value branches directly
to MoveHere's failure path. Therefore zero is suitable as a conservative hard
pre-filter; a nonzero result must still leave Vanilla's detailed path creation
authoritative.

Version `1.1.43` adds the separately removable
`HunterPclReachabilityDiagnostic.cs`. Before ranking, it records source/target
PCL and compares mode `0`, mode `2`, and the Hunter's live `+0x35C` value. The
existing exact state-0 MoveHere result hook correlates its return with the stored
probe only when Hunter/prey identities, player, PCL inputs, mode, and a
three-second time bound still match. The MoveHere hook does not invoke the PCL
query again. The diagnostic neither filters candidates nor writes targets,
orders, path fields, reservations, AI state, or hook registers.

The `1.1.43` in-game calibration recorded `53` candidate probes and `10` exact
MoveHere correlations. All four nonzero PCL results matched MoveHere return `1`;
all six zero PCL results matched MoveHere return `0`. Every correlation retained
identical player, mode and source/target PCL inputs within the three-second
bound, with zero disagreements. The live Hunter mode was consistently `0`, and
mode `0`, mode `2`, and the live-mode result agreed in every tested wall case.
The first warm-up probe took `170 us`; the average across all probes was
`4.43 us`, with normal subsequent calls generally `1..3 us`. No PCL callback
error or visible search delay occurred.

Version `1.1.44` promotes the validated query into
`HunterPclReachability.cs`. A zero result removes the candidate before heuristic
ranking and is checked independently again at the concrete public target-query
handoff, so an empty ranking cannot re-enable disconnected prey through the
generic fallback. Positive or unavailable results remain eligible and Vanilla
still owns detailed path creation. Results are cached per Hunter/global-prey
identity and exact player/mode/source-PCL/target-PCL input for at most one
second. Changed PCL inputs bypass the cache immediately; unchanged gate state is
therefore rechecked after at most one second. Expired identities are pruned
periodically.

The audited stale-target path in `HunterUpdate` state `1` first calls the order
reset helper at RVA `0x12FEF6` (target RVA `0x193A20`). It compares the current
target-slot global ID with the Hunter's stored target global ID at RVA
`0x12FF1F`. A mismatch calls Vanilla's Hunter target query at RVA `0x12FF2E`
(target RVA `0x18AF00`): a nonzero result writes state `0` at RVA `0x12FF45`;
a zero result writes state `6` at RVA `0x12FF5F` and follows the native timer and
return path at RVAs `0x12FF73` and `0x12FF7C`.

Version `1.1.45` uses the existing persistent 100-ms native scan to recheck each
live state-`1` target through `HunterPclReachability`. On a confirmed PCL zero it
sets only the public Hunter target-global field at `GameUnit +0x39C` to zero and
releases the prey reservation through the existing identity guard. The next
`HunterUpdate` therefore enters the audited stale-identity branch and owns order
reset, target search and any replacement movement. The scan does not write AI
state or path/order fields and does not issue a move. Query errors remain
fail-open.

### Native-unreachable prey rejection lifetime

Version `1.1.42` used the immediate state-0 `MoveHere` result as the only
reachability authority for an exact Hunter/global-prey pair and retained zero
for five minutes. Version `1.1.44` supersedes that delayed discovery: known PCL
disconnections never receive a MoveHere order and are rechecked through the
one-second connectivity cache. A rare positive-PCL candidate whose detailed
MoveHere still returns zero now receives only the generic 30-second abort
cooldown. No AI state, voice line or movement order is issued by the PCL filter.

Future updates must revalidate that the PCL zero result remains a conservative
MoveHere rejection, that the concrete query event still observes every Vanilla
candidate, and that a complete set of PCL-rejected candidates reaches Vanilla's
normal no-target progression.

## Required update audit

1. Require one semantic match for both despawn patterns and verify that operand 1 at
   pattern offset `13` remains the signed 16-bit despawn duration.
2. Revalidate both automatic target dispatch tables, the chicken entry, the
   hunter-only acceptance/rejection branches and that explicit `AttackUnit`
   target assignment still occurs before automatic candidate dispatch.
3. Revalidate the explicit `AttackUnit` decision sequence at `0x18EB98`, its
   compatibility call target, Hunter bypass target, `R14` target ID, `R15D`
   attacker ID and that forcing `EAX` from `1` to `0` at `0x18EBC3` still
   reaches Vanilla's existing target assignment without bypassing its earlier
   identity, alive-state, range or line-of-sight checks.
4. Revalidate the Script Extender unit array and raw fields `+0x88`, `+0x92`,
   `+0x94`, `+0xC0`, `+0xC2`, `+0x29C`, `+0x2BC`, `+0x2C4`, `+0x370`,
   `+0x39A`, `+0x39C` and `+0x448`.
5. Confirm hunter/prey states, corpse flag, death timer, reservations, target
   IDs, coordinates, camel health and visual health refresh behavior.
6. Test automatic ranged rejection, explicit ranged `AttackUnit`, Hunter
   retargeting, stalled/blocked projectile compensation, resulting `0x6E` corpse
   pickup, line-of-sight recovery movement, corpse cleanup, camel health and
   chicken neutralization on fresh and loaded maps.
7. Revalidate the granary chicken function at `0xD29E0`, the comparison
   sequence at `0xD2AB4`, hook instruction at `0xD2ABF`, signed `jle` target
   `0xD2BA7`, spawn event path `0xD2B4C`, `rbx` player identity and native
   count field `[rdi+0x2048]`.
8. Update all reference RVAs and the dispatch-table map before approving
   the new shared hash.
9. Revalidate the Hunter query candidate-loop signature, `R13`/`R14` slot
   formula, `ESI` candidate ID and callback ordering before the public Extender
   event. Check whether upstream work item 123 is fixed and remove the temporary
   workaround when it is no longer needed.
10. Revalidate visibility wrapper `0xA06F0`, core `0x9E350`, query call
    `0x18B052`, direct-order call `0x18ED1A`, the seven-argument ABI, both core
    relative targets, context write boundary `+0xC`, world/height field
    construction, and return thresholds. Keep the diagnostic fail-closed until
    all control cases have been repeated in game.
11. Revalidate height helper `0x6B990`, type switch `0x6B9F8`, dispatch target
    table `0x6BAB4`, type table `0x6BAC4`, the Hunter's Hut mapping `7 -> 0`,
    special/normal targets `0x6BA3D` and `0x6BA41`, and blocker-height table
    `0x2E7C60` with type-7 value `40`. Confirm the patch still changes only the
    first dispatch byte to `3` and restores it to `0`.
12. Revalidate PCL reachability RVA `0xE2610`, its unique entry signature,
    zero/nonzero return semantics, player-aware gate traversal, and the call at
    MoveHere RVA `0x1964D3`. Confirm the caller still passes public unit fields
    `+0x92` and `+0x35C`, target/source PCL in `R8D/R9D`, and branches to failure
    on zero before enabling the production pre-filter.

## Temporary Hunter visibility diagnostic

Version 1.1.26 keeps the visibility investigation in the removable
`HunterVisibilityDiagnostic.cs` file. It installs no native hook and changes no
branch result or unit state. Version 1.1.25 briefly used behavior-neutral hooks
at RVAs `0x18EE14` and `0x130171`; the first real blocked-order test ended in a
native CTD before the hook's first confirmation marker. Those hooks and their
patterns were therefore removed completely rather than retained as fallbacks.

The diagnostic now correlates the public Hunter-query event, targets observed
by the existing 100-ms native unit scan and successful projectile-spawn events.
When that safe scan observes a Hunter in AI state `6`, it resolves the current,
recently assigned or recently accepted chicken by unit slot plus global ID. The
bounded log records movement at most every two seconds while positions remain
unchanged. It includes unit tile and world positions, elevation and look-at
coordinates, Hunter path/order fields, matching-projectile age, the straight
tile line's terrain-height range, and each building ID, type, owner, footprint
and occupied-line-tile count. The existing projectile-spawn event emits the same
line context for successful attack paths, providing a behavior-neutral control.

The diagnostic reads the already documented `GameUnit` layout fields at
`+0x88`, `+0x8A`, `+0x94`, `+0xC0/+0xC2`, `+0xF2`, `+0xF4`, `+0x2BC`,
`+0x398`, `+0x39A`, `+0x39C`, `+0x3FE` and `+0x448`. Because it adds no native
address, a future DLL update requires only the normal structure-layout audit.

## Hunter blocked-shot and line-of-sight recovery

Version 1.1.27 removes the one-second `KillUnit` compensation. That API enters
the melee-death path and produced animal state `0x6F`, which the Hunter did not
subsequently collect. A pending shot now stores Hunter, prey and projectile slot
plus global IDs. While that exact `ArcherArrow` is still alive, the runtime calls
the Script Extender's `GameUnitManagerAPI.DamageUnitRanged(victim, projectile)`
only after the arrow has stopped moving for 300 ms, is within 32 world units of
the target, or reaches its public projectile-delete pre-event. This re-enters
Vanilla's ranged damage/death path and is limited to three attempts. An unresolved
intent expires after five seconds without a synthetic kill. The validation also
requires the live Hunter source, live configured prey, matching projectile source
and target, owner/color-independent prey eligibility, and projectile source-player
consistency.

Pre-shot visibility recovery is isolated in `HunterLineOfSightRecovery.cs` and
currently remains fail-closed: it reports unavailable, performs no path search,
and issues no movement. The former implementation synchronously called the
Script Extender's managed `GameTileManagerAPI.FindPath`. An in-game case with an
unreachable destination showed that this unbounded managed A* can monopolize the
game thread. All such calls were therefore removed from both recovery and target
ranking. Target and granary costs temporarily use a bounded Chebyshev heuristic;
the heuristic is not reachability proof and must not be documented as one.

For Steam build `24651686`, native `c_game_unit_issueorder_movehere` is at RVA
`0x196230`, has size `0x50D`, and returns `0` through its rejection/cleanup path.
After a positive native path length it writes public `GameUnit` path fields
`+0xF2/+0xF6/+0xF8`, sets path state `2`, and returns `1`. Its native
manager-relative offsets are `+0x65C` higher because the native slot base is
`0x65C` bytes before the Script Extender's public `GameUnit` pointer. The
routine mutates order and target fields before path acceptance, so neither it
nor `MoveToTile` is a behavior-neutral reachability query.

The standard path helper at RVA `0xF4930` has size `0x315`. It mutates a large
scratch object (including offsets through at least `+0x155F68`) and is invoked
with the global scratch context at `imageBase + 0x60AC660`. It must not be called
directly with a guessed private context. Any future update must revalidate both
RVAs, function extents, the `0x65C` base adjustment, return branches and the
mapped public path fields before enabling native movement diagnostics or
recovery.

`HunterNativeMoveDiagnostic.cs` was the temporary behavior-changing calibration
for these findings. In non-editor singleplayer only, two rapid target aborts
without a recent Hunter projectile may issue one `MoveToTile` order to the live
prey's occupied tile. It correlates the synchronous public `OnUnitMoveHere`
Pre/Post events, requires exact return value `1` for acceptance, and observes
public fields `+0xF2/+0xF4/+0xF6/+0xF8` plus position progress. It is bounded to
six attempts per map with cooldown and hard timeouts, suppresses target/idle
requery only while an accepted diagnostic move is active, and must remain a
separately removable diagnostic rather than becoming an undocumented fallback.
It was fully removed in version `1.1.35` after completing the calibration and
must not be restored as a production reachability test.

The `1.1.31` calibration accepted the reachable wall-case destination with
return `1`, path state `2` and path size `38`, but Vanilla replaced it within
`100 ms` by a one-step path to the Hunter's current tile. No second abort burst
occurred for the later fully enclosed state-7 target. Version `1.1.32` therefore
also performs one bounded attempt after the same live target remains in Hunter
AI state `7` for two seconds without a projectile. It logs the AI/context target
fields and all subsequent public `OnUnitMoveHere` events for that Hunter; it
still writes no AI state and never retries the same stable Hunter/target global
identity.

The `1.1.32` calibration then rejected the fully enclosed control destination
with exact return `0` for Hunter `1/358`, prey `8/285`, and distance `19`, without
a freeze or diagnostic exception. Taken together, the two ingame cases calibrate
exact return `1` as an accepted native foot route and `0` as rejection for the
tested unreachable destination. The preceding reachable wall phase remained in
AI state `6` and generated repeated debounced searches but no abort or stable
state-7 trigger. Version `1.1.33` therefore adds one earlier, still bounded
diagnostic attempt after three search starts in four seconds when state `6`
retains a live enabled context target. It uses that current native context rather
than an old aborted target, retains the same singleplayer and identity gates,
and still neither invokes the private helper nor writes an AI state.

The `1.1.33` ingame run accepted two state-6 destinations with return `1`. The
first produced a 43-node path and was still progressing at node `20` after the
old eight-second observation timeout; the deer had moved away from the original
destination before the Hunter arrived. The second produced a 15-node path, but
only `10 ms` later Vanilla issued a successful `MoveHere` back to `396,350` and
replaced it with a one-node path. There were no ImprovedHunters errors, freezes,
or Hunter projectiles; the session reached 476 queries and 35 debounced search
starts.

The audited unit-function table is at RVA `0x320CB0`. Its Hunter entry (index
`6`) points to `HunterUpdate` at RVA `0x12FC20`, size `0x17B2`. The initial
acquisition path calls the target query at `0x12FD6C`, calls `MoveHere` at
`0x12FE25`, and writes AI state `1` at `0x12FE45` only after a positive return.
Before that call it stores target slot/global identity at `0x12FDD7/0x12FDEF`,
the worker-target global ID at `0x12FDFF`, and prey reservation `2` at
`0x12FE13`. The state-1 branch from `0x12FEE8` validates that live identity and
continues processing the target's current coordinates while the movement engine
advances the accepted path.

The state-6 branch begins at `0x130C7D`. When manager-relative flag `+0x9CC` is
set, it increments `+0x920` to 20 and, after its helper gates, passes stored
coordinates `+0x992/+0x994` to `MoveHere` at `0x130D1A`; successful issuance
clears `+0x9CC`. With the validated `0x65C` manager/public adjustment these map
to public offsets `+0x370`, `+0x2C4`, and `+0x336/+0x338`. This is the exact
return-to-hut writer observed in the second `1.1.33` attempt. The inspected
state-7 branch from `0x130DBB` has no direct `MoveHere` call.

Version `1.1.34` therefore extended only the removable singleplayer diagnostic:
after exact `MoveHere` return `1`, original state `6`, unchanged context
slot/global identity, and reservation `0` or `2`, it readback-guards the normal
worker-target global ID, prey reservation `2`, and AI-state `1` continuation.
The mutation is additionally gated by the audited reference DLL hash and rolls
all three values back if any readback differs. It then observes moving
prey, AI transitions, order changes, and projectile spawn for up to 30 seconds,
keeps only the same global prey identity query-eligible, and releases the
attempt identity for a bounded retry if Vanilla returns to state `0` or `6`.
Future DLL audits must revalidate the table entry, function bounds, these state
dispatch comparisons and call/return-dependent state transition before using
them for recovery rather than diagnosis.

The `1.1.34` ingame run rejected that isolated continuation. The first two
regressive approaches did not apply state `1` at all: the 30-second observation
still overrode every target query as invalid, so the Hunter followed stale prey
tiles and ignored a deer that became visible. Later attempts passed all
readback guards and changed state `6 -> 1`, but Vanilla returned to state `6`
after about `1.7 s` and `100 ms` respectively and issued its hut-return move.
Six accepted diagnostic moves produced no Hunter projectile. This proves that
target identity, worker-target global ID, reservation `2`, and AI state `1` do
not reconstruct the complete acquisition context. Version `1.1.35` removes the
diagnostic source and every query, move, reservation, and AI-state integration;
only behavior-neutral visibility/state diagnostics remain. Future recovery
work must identify the missing native caller context or a safer native control
point before issuing movement.

A subsequent bounded audit refined the cause. In state `1`, RVA `0x130110`
checks public auxiliary path field `+0xF4`. If it is zero, execution immediately
reaches the direct attack order call at `0x13013D` (`0x18E950`). A failed line-
of-sight result then reaches `0x130171`, which writes Hunter state `6`, timer
`20`, and the hut-return flag. If the auxiliary field differs and path state
`+0xF2` is `2`, the update exits without that abort. The out-of-band
state write therefore raced ahead of Vanilla movement scheduling; copying more
fields is not an acceptable fix.

Version `1.1.36` implements the next removable diagnostic at the complete
target-search return inside `HunterUpdate`. `EDX=EBX` passes the Hunter ID to
the call at RVA `0x12FD6C`, and the returned prey slot remains in `EAX` until it
is copied to `R8` at `0x12FD89`. A start hook at RVA `0x12FD67` arms only this
state-0 invocation; candidate events from other calls cannot leak into it. The
return hook changes only `EAX == 0`, and only to a live, unreserved, same-global-
ID candidate already accepted by the existing runtime policy during that exact
query. Vanilla then performs its target/global, worker-target, reservation,
`MoveHere`, and state-1 sequence in the original update context. Direct
movement and later AI-state writes remain forbidden.

The audited state-0 failure path writes target slot/global at
`0x12FDD7/0x12FDEF`, worker-target global at `0x12FDFF`, and prey reservation
`2` at `0x12FE13` before calling `MoveHere` at `0x12FE25`. Return `0` jumps to
the state-7 writer without undoing those values. The state-7 branch beginning
at `0x130DBB` likewise contains no target/reservation cleanup. A third hook at
RVA `0x12FE2A` therefore observes only the immediately preceding state-0
`MoveHere`. Positive results are untouched. On zero it applies a 30-second
Hunter/global-prey cooldown, clears the matching target identity and worker
target, and releases reservation `2` only after slot/global validation and a
scan proving that no other live Hunter targets the same identity. Readback and
the cleanup invariant are logged. State `7` and all later transitions remain
Vanilla.

The three hook windows are hash-bound to Steam build `24651686`, SHA-256
`33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.
Their reference RVAs are `0x12FD67`, `0x12FD89`, and `0x12FE2A`; callees must
still resolve to query RVA `0x18AF00` and `MoveHere` RVA `0x196230`. Any hash or
byte/call-chain mismatch disables only this diagnostic. It is additionally
fail-closed in real multiplayer and the map editor pending the Script Extender
`1.50.0` multiplayer Chore.

The first `1.1.36` ingame run confirmed all three hooks and the singleplayer
gate, but did not inject a candidate: the first confirmed state-0 query already
returned Vanilla slot `12` while the staged hidden preference was slot `6`.
Across 31 debounced searches and 291 candidate events there were zero
`supplied`, zero correlated accepted moves, and zero correlated rejected moves.
The Hunter nevertheless cycled through multiple target identities in state `6`;
the behavior-neutral native probe repeatedly returned `0` for several deer,
and only deer that later approached into sight were attacked. The final deer
were unreachable, but a nonzero Vanilla target meant the injection-only move
correlation still did not observe their `MoveHere` result.

Version `1.1.37` therefore retains a validated Vanilla nonzero slot/global
identity until the same immediate state-0 `MoveHere` result at `0x12FE2A`.
Positive returns are logged and left byte-for-byte on Vanilla's path. Return
`0` registers the Hunter/global-prey pair in the runtime's existing 30-second
aborted-target cooldown, invalidates its cached ranking, and runs the same
guarded identity/reservation cleanup used for an injected fallback. This adds
no new RVA, pattern, movement call, or AI-state write. Future audits must keep
the invariant that the pending identity is created only at `0x12FD89` and
consumed once at the immediately following `0x12FE2A`.

The `1.1.37` ingame run recorded 71 accepted Vanilla state-0 `MoveHere`
results, one rejected result, and no injected fallback. The first Hunter also
followed a long obstacle-avoiding route, while fully unreachable prey left both
Hunters waiting beside their huts without a "No game" voice line. Waiting alone
does not prove that target searches have stopped, but the long route confirms
that Vanilla movement already handles the out-of-range case.

The state-1 branch explains this split. `HunterUpdate` calls the native distance
helper at RVA `0x79C0` and retains its exact result in `EDI`. Values above 28
select a distance-dependent movement/animation stage and exit, allowing the
accepted path to progress. At 28 or below, public auxiliary field
`GameUnit +0xF4` is checked at `0x130110`; a nonzero value together with path state `2` at
public `+0xF2` also exits. Otherwise RVA `0x13013D` calls direct attack function
`0x18E950`. Its zero result is tested at `0x130149` and leads to the state-6,
timer-20, return-to-hut writer at `0x130171`.

Version `1.1.38` adds a fourth, observation-only hook to the separately
removable `HunterTargetSearchFallbackDiagnostic.cs`. The audited sequence begins
at `0x13013D`, the hook is at offset `+0xC` (`0x130149`), and its relative call
must resolve to `0x18E950`. It captures volatile registers plus `RDI`, correlates
only a validated accepted state-0 move with the same live Hunter/target identity
for at most 60 seconds, and logs the exact native distance, attack result and
public path fields `+0xF2/+0xF4/+0xF6/+0xF8`. The callback changes no register,
target, reservation, movement order or AI state. Any hash, pattern, relative-
target, identity or state mismatch leaves behavior unchanged.

The `1.1.38` test recorded 41 direct state-1 attack results: 40 failures and
one Vanilla success. Every failure occurred at native distance 28 or below.
The longest correlated approach had already run for 44.383 seconds and reached
public path progress/length `+0xF6/+0xF8=59/61` when the blocked direct attack
failed at exactly distance 28 and sent the Hunter back to its hut. This also
corrects the earlier field interpretation: `+0xF6`, not `+0xF4`, is the
observed advancing path index. The tail of `MoveHere` at RVA `0x196230` writes
path state `+0xF2=2`, progress `+0xF6=0`, and length `+0xF8`; generic Vanilla
unit movement advances `+0xF6`. Reissuing `MoveHere` would reset that progress.

Version `1.1.39` adds the separately removable, exact-hash-only
`HunterVanillaPathContinuationDiagnostic`. Its full distance-stage pattern
starts at RVA `0x1300D2`; the hook validates `cmp edi,28`, its signed short
branch target, and runs before the compare at RVA `0x1300EA`. For a live
same-identity state-1 Hunter with path state `+0xF2=2`, remaining progress
`+0xF6 < +0xF8`, and a zero result from the validated native visibility probe,
the callback changes only `RDI` from the real value at or below 28 to 29. The
relocated original compare then selects Vanilla's existing distance-29 stage;
the mod issues no movement or order and writes no target, path, reservation, or
AI-state field. A positive visibility result leaves the real distance intact so
Vanilla can attack immediately. Version `1.1.39` released on path completion,
three seconds without progress, 60 seconds total, 1200 callbacks, or its
temporary per-map two-identity diagnostic bound, and remained fail-closed in
real multiplayer.

The `1.1.39` multi-Hunter run proved that the global identity bound was visible
behavior rather than a harmless logging restriction. Six Hunters received
accepted Vanilla paths, but later blocked pairs repeatedly reached failed
direct attacks with active path state while continuation slots were already
consumed. Version `1.1.40` removed both the permanent global identity set and
the redundant callback-count bound. It keeps one active attempt per Hunter,
allows unlimited sequential target identities over a map, and retained the
then-current 60-second continuous-near-range and three-second no-progress bounds. A bounded
stop suspends only the same Hunter/global-prey identity for five seconds; a
different identity or natural distance 29/30 clears that Hunter's stale state.
At that version, a callback gap of more than one second started a fresh bounded interval. Logging
is limited to attempt starts, actual `+0xF6` progress changes, releases, and
bounded stops so parallel Hunters do not exhaust the diagnostic budget during
the first few seconds.

Version `1.1.64` removes the 60-second total bound and callback-gap reset. The
same identity may continue for any total duration while its path progresses;
only the local three-second no-progress detector and five-second correction
cooldown remain.

The subsequent `1.1.40` in-game repeat confirmed the state separation: every
simultaneously deployed Hunter continued its own blocked-visibility Vanilla
path successfully. The apparent position-dependent failure from `1.1.39` did
not recur and is attributed to the removed global diagnostic identity bound.

The `1.1.47` remaining-path-speed test exposed a separate earlier Vanilla
refresh. Two accepted 68-step paths ended at progress `55` and `51` without
reaching the direct-attack result hook. Each transition immediately entered a
new target search for the same live identity `17/319`; PCL and cooldown passed,
but the mod's unreserved-prey ranking reported `best=none`, no replacement
`MoveHere` followed, and the Hunter visibly returned to its hut. A third,
26-step path succeeded after the moving deer shortened the approach.

Static reinspection shows that the second call to RVA `0x79C0` stores the
maximum absolute world-coordinate delta at scratch address `0x1834A8F5C`.
`HunterUpdate` compares it with `20` at RVA `0x130019`. The `<=20` fall-through
at `0x130022` jumps to the target query call at `0x12FF2E`, before both speed
hooks. A zero result tested at `0x12FF33` reaches the state-6/timer-20/hut-return
writer at `0x12FF53`. The other path into the same query follows a target-global
identity mismatch and must not share this recovery.

The first version `1.1.48` attempt validates a unique near-refresh branch
sequence at `0x130019`, hooks its apparent exclusive fall-through instruction
at `0x130022`, and separately hooks the query result at `0x12FF33`. Only the
same live Hunter/target identity
with corpse flag zero, Vanilla reservation exactly `2`, accepted owner/PCL/
cooldown event policy, and no other live Hunter targeting that identity may
replace query result zero with its existing target slot. A two-second bounded
handoff pre-stages the same candidate for the immediately following audited
state-0 query at `0x12FD67`; its existing return and `MoveHere` result hooks then
leave target, reservation, path, movement and AI-state writes to Vanilla.
Changed identity, reservation `0` or foreign reservation, missing event
acceptance, real multiplayer and every validation error remain behavior-neutral.

The first in-game run rejects the `0x130022` hook even though installation
succeeds. The log ends natively when the new Hunter starts moving, before any
managed callback confirmation or exception. `X64InlineHook` uses a 14-byte
absolute indirect jump and extends the overwritten range to whole
instructions. From `0x130022` it therefore consumes 18 bytes:

- six-byte `mov edx,[0x18092F2C4]` at `0x130022`;
- five-byte `jmp 0x12FF2B` at `0x130028`;
- seven-byte `movsxd rbx,[0x18092F2C4]` at `0x13002D`.

The preceding original `jg 0x13002D` at `0x130020` remains live and targets
offset `+0x0B` inside `[0x130022,0x130034)`. That address lies inside the
eight-byte destination literal of the installed absolute jump. A world delta
greater than `20` consequently branches into data and crashes the process.
This is an inbound-branch/overwrite-span defect, not a managed validation or
register-context failure.

The replacement design must remove the `0x130022` hook. Hooking the compare at
`0x130019` is structurally safe for the audited DLL: the minimum 14-byte hook
decodes 15 bytes ending at `0x130028`, so the relocated `jg` still targets the
untouched instruction at `0x13002D`. A pre-instruction callback can read the
same scratch maximum, stage a refresh only for `<=20`, and then let the
relocated compare, conditional branch and near-path load run unchanged.

The full inbound-branch audit also rejects retaining the former result hook at
`0x12FF33`: its 14-byte minimum extends to an 18-byte overwrite span ending at
`0x12FF45`, while an original jump at `0x13058B` targets `0x12FF3E` inside that
span. The safe result design therefore hooks the exact 14-byte
`[0x12FF2E,0x12FF3C)` sequence containing `call 0x18AF00`, `test eax,eax` and
the current-Hunter `movsxd` load. Its callback executes after those relocated
instructions and before the untouched `je 0x12FF53`. On a fully validated own
reservation it clears only ZF, selecting Vanilla's state-0 writer; it preserves
RAX and leaves the queued state-0 continuation to perform the existing guarded
target handoff. The original target `0x12FF3E` remains outside the hook.

Version `1.1.49` implements that replacement. It requests the exact decoded
15-byte span `[0x130019,0x130028)`, captures context before the relocated
instructions, and stages nothing unless the untouched scratch value at RVA
`0x34A8F5C` is in the native near range `0..20`. The actor ID is read from RVA
`0x92F2C4`, matching the value Vanilla itself loads into `EDX` at `0x130022`,
rather than relying on `RBX`. Before the transaction commits, Iced must decode
the audited `cmp`/`jg`/`mov` lengths, the far target `0x13002D`, both
RIP-relative globals and the `jmp 0x12FF2B` at the first byte after the hook.
The combined query/result hook must independently decode as the 14-byte span
`[0x12FF2E,0x12FF3C)`, validate its query target `0x18AF00`, and leave both the
following `je 0x12FF53` and the original inbound target `0x12FF3E` outside. A complete
linear decode of HunterUpdate `[0x12FC20,0x1313D2)` additionally rejects every
direct call or branch from outside into the strict interior of either hook
span. Any mismatch fails closed before a native patch is installed.

Future DLL audits must revalidate the exact sequences at `0x12FF07`, `0x130019`
and `0x1300D2`; the near-refresh path at `0x130022` still targeting the query
call at `0x12FF2E`; the safe hook span `[0x130019,0x130028)` leaving branch
target `0x13002D` untouched; compare and short-branch bytes at `0x1300EA`; the
distance helper scratch maximum; the result remaining in `EDI`; the semantics
of the `>28` stage-8 writer; and the `MoveHere` path-field writes. The result
span `[0x12FF2E,0x12FF3C)` was not hooked from `1.1.51` through `1.1.65`;
`1.1.66` reuses it only under the stricter moving-target guard documented below.
A matching instruction pattern alone does not validate reservation ownership or
the raw `GameUnit` offsets.

### Remaining-path Hunter speed stages in 1.1.46/1.1.47

The canonical Steam build has SHA-256
`33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.
`HunterUpdate` starts at RVA `0x12FC20`; its helper at RVA `0x79C0` returns the
exact Manhattan tile distance `abs(dx) + abs(dy)` in `EAX`, retained in `EDI`.
The initial speed-stage sequence starts at RVA `0x13005C`, and its first compare
at RVA `0x130063` is `83 FF 28 7E 1B` (`cmp edi,40; jle 0x130083`). The complete
ladder writes `r_CurrentSpeed` `1/2/3/4/6/8/10` for distances
`>40/37..40/35..36/33..34/31..32/29..30/<=28`. Public field `+0x4` is written
as `1` on the far branch and `0x81` on the remaining branches. No speed or
animation write occurs before RVA `0x130063`.

Generic unit movement loads the `GameUnitManager` base at module RVA
`0x67E7400` at RVA `0x18576C`. At RVAs `0x18580F..0x185865` it applies the
manager-relative path-buffer offset `0xB4FE78`, yielding effective module RVA
`0x7337278`, and then indexes `unitId * 0x3E8 + pathIndex / 2`. The `MoveHere`
writer at RVAs `0x196554..0x19657E` independently uses the same manager-relative
address form. Even path indices use the low nibble and odd indices the high
nibble. Direction values `0..7` alternate cardinal and diagonal movement, so
the direct-Manhattan-compatible remaining cost is one per even direction and
two per odd direction. Public `GameUnit
+0xF6` is the `UInt16` path index and public `+0xF8` is the `UInt32` path length;
one unit path owns `0x3E8` bytes and at most 2000 nibbles. The movement code may
increment `+0xF6` immediately after loading a direction when the word at public
`+0x3F0` is zero, so the stored remainder can omit the currently traversed
segment. Version `1.1.46` deliberately applies no unvalidated in-flight
correction and therefore uses the decoded value as a conservative lower bound.

`HunterRemainingPathSpeedRecovery.cs` is separately removable and exact-hash-
only. It validates the sequence and short-branch target, hooks before the
relocated compare at `0x130063`, and captures volatile registers plus `RBX` and
`RDI`. For a live same-identity state-1 Hunter with path state `2`, stable
progress/length, at most 2000 valid direction nibbles, a configured prey type,
the singleplayer mode gate, and native visibility exactly zero, it replaces
only `RDI` with `max(nativeDistance, min(decodedRemainingCost, 41))` when this
selects a faster existing Vanilla stage. It writes no unit, speed, animation,
path, order, reservation, or AI-state field. Positive visibility, invalid
visibility results, decode errors, changed snapshots, option/mod disable,
map-editor and real multiplayer remain behavior-neutral. Attempts are bounded
per Hunter/global-target identity by 60 seconds and three seconds without path
progress, with a five-second same-identity retry cooldown.

The first `1.1.46` in-game test observed direct distance `10`, path
progress/length `0/72`, two `invalid-packed-path-direction` skips, zero Package-E
observations, zero register mutations, and no callback failures. The existing
Vanilla-path continuation still selected distance `29`, matching the Hunter's
slow movement from the beginning. Static reinspection showed that `1.1.46` had
mistaken the manager-relative displacement `0xB4FE78` for a module RVA and read
unrelated memory. Version `1.1.47` obtains the manager base from
`GameUnitManagerAPI.Instance.GetUnitManager().Pointer`, adds `0xB4FE78`, and on
the exact audited hash validates that the manager and effective buffer resolve
to module RVAs `0x67E7400` and `0x7337278`. Any mismatch disables only this
feature and preserves Vanilla behavior.

Future DLL audits must revalidate the DLL hash, RVAs `0x79C0`, `0x13005C`,
`0x130063`, `0x18576C`, `0x18580F..0x185865`, `0x196554..0x19657E`, manager RVA
`0x67E7400`, manager-relative offset `0xB4FE78`, and effective path-buffer RVA
`0x7337278`; the full speed ladder; the path-buffer stride and nibble parity;
the 2000-step bound; public offsets
`+0xF2/+0xF4/+0xF6/+0xF8/+0x3F0`; and the meaning of the path-index increment
guard. A matching speed-ladder signature does not validate the manager base,
manager-relative path-buffer displacement, or raw unit-field layout.

### Hunter movement-transition diagnostics in 1.1.50

The `1.1.49` game run showed a wrong sitting/waiting animation only after the
state-1 near refresh and during the distance-29 continuation special case. The
normal remaining-path speed stage retained the expected matching animation, so
speed and animation of that Vanilla stage must not be separated.

Version `1.1.50` installs no additional native hook. It adds read-only snapshots
to the already validated near-refresh entry/result, state-0 `MoveHere` result,
distance-29 continuation and native-visibility callbacks. Each snapshot records
the public animation frame, sprite-animation frame, animation timer, both speed
fields, direction, transform target and positions together with the audited raw
AI, target and path fields. Raw `GameUnit +0x4` is logged as
`locomotionControl`; its `1`/`0x81` state-1 writer semantics remain bound to the
canonical SHA-256 above and must be revalidated for a changed DLL. The
diagnostic writes none of these fields and does not alter hook spans, registers,
movement, orders or AI state.

### Coupled near-refresh continuation in 1.1.51

The `1.1.50` log identifies the sitting/waiting animation as repeated normal
order initialization, not as a mismatched Vanilla locomotion stage. At world
maximum distance `20`, the unchanged query returned a nonzero target, wrote
Hunter state `0`, and the next `MoveHere` repeatedly reset the path and animation
frame to `657`. A normal initial `MoveHere` uses the same frame briefly and then
transitions correctly; only the rapid refresh loop pins it visibly.

Targeted analysis of canonical `HunterUpdate [0x12FC20,0x1313D2)` finds only one
read of scratch RVA `0x34A8F5C`, the `cmp dword [...],20` at `0x130019`. Helper
RVA `0x79C0` overwrites that `+0xC` maximum-distance result before every compare.
The hook API exposes flags but no instruction pointer. Because the relocated
`cmp` overwrites callback flags, changing ZF or OF cannot safely choose the far
branch. A new hook at the fall-through is also forbidden by the documented
inbound target. Version `1.1.51` therefore reuses the validated 15-byte hook and,
only after all guards pass, changes this immediate comparison operand from its
current `0..20` value to sentinel `21`. The relocated original `cmp`/`jg` then
selects untouched target `0x13002D`. No speed, animation, path, order, movement
or AI-state field is written.

The compare callback prepares a one-use, generation- and identity-bound ticket
for world distances `0..28`. It requires state `1`, the same live reservation-2
prey, an active incomplete path, an exact positive active-target PCL snapshot and
native blocked-visibility classification. PCL is snapshot-only in the inline
callback; since version `1.1.55`, the existing persistent scan refreshes each
unchanged active target natively at most once per second outside `HunterUpdate`
and retains the observation for two seconds of read-only handoff. Identity,
player, mode or source/target-PCL changes bypass the interval immediately. This
separates the inline handoff from the general one-second target-selection cache
and removes its deterministic expiry race without increasing active native PCL
queries beyond approximately one per second and Hunter. The `0x1300EA` hook
consumes the ticket before it may select Vanilla distance `29`. Since version
`1.1.64`, total duration is unlimited while path progress changes; only the
three-second no-progress detector and five-second retry cooldown remain per
Hunter/target identity. Missing or stale active snapshot,
changed identity, visible target, unreachable PCL, invalid path and every error
leave both original branches unchanged.

From `1.1.51` through `1.1.65`, the exact 14-byte query-result hook at
`[0x12FF2E,0x12FF3C)` and its ZF override were removed. State 1 no longer queued
every own reservation through state 0 or created another `MoveHere`; failed
preparation deliberately released the decision to Vanilla. `1.1.66` does not
restore that repeated behavior: it reuses the span once per accepted path only
after the separate crossed-anchor geometry guard succeeds. Future DLL updates
must additionally verify that scratch
RVA `0x34A8F5C` remains single-use after the second `0x79C0` call, is overwritten
before the next Hunter comparison, and that callback placement remains before
the relocated 7+2+6-byte `cmp`/`jg`/`mov` span.

### Bidirectional near-range visibility decision in 1.1.52

An ingame diagonal-wall case showed that a positive wrapper result does not
guarantee a physically unobstructed arrow. Static analysis of wrapper RVA
`0xA06F0` confirms asymmetric control flow: it calls core RVA `0x9E350` from
Hunter to prey and returns that positive result immediately. It reverses all
end-point arguments and calls the same core a second time only when the forward
result is zero. The wrapper is shared by Hunter query RVA `0x18B052` and direct
order RVA `0x18ED1A`; therefore both Vanilla decisions inherit the same corner
or diagonal false-positive risk.

Version `1.1.52` validates the core entry bytes in addition to the two wrapper
call targets and constructs a second exact-hash delegate with the already
validated seven-argument ABI. This is not another hook. At native state-1
distance `0..28`, the existing wrapper remains the cheap first test. A zero
result already represents two failed internal directions, so no extra native
call is made. Only a positive wrapper result triggers two guarded direct core
calls, forward and reverse. Each receives a separate zeroed 16-byte private
context surrounded by the existing guard words. Any invalid result, context
guard change, identity change or invocation error fails closed before the
near-refresh mutation.

Two positive directional results select `HandoffToVanillaAttack`. At world
distance `<=20`, only scratch RVA `0x34A8F5C` changes to `21`, so the original
branch skips the destructive query; no continuation ticket is prepared and the
later untouched distance path reaches Vanilla's direct attack. Wrapper zero or
a directional disagreement selects `ContinueExistingPath`, prepares the
identity-bound ticket and permits the existing RVA `0x1300EA` distance-29
continuation. No speed, animation, movement, path, order or AI-state field is
written in either case.

The log records the wrapper plus explicitly named Hunter-to-prey and
prey-to-Hunter core results, which direction equals the wrapper, the final classification, and
`physicalArrowCollisionPreflight=False`. This comparison is deliberately a
cheap conservative experiment, not a claim of exact projectile collision.
The real common flight step at RVA `0x9EF20` invokes the large stateful collision
routine RVA `0x9C730` on a live projectile. Calling that path with fabricated
projectile or manager state from `HunterUpdate` is not a validated preflight and
must remain forbidden. If the diagonal-wall test still yields two positive core
directions before an arrow collision, locate a separate state-neutral collision
query instead of reusing `0x9C730`.

The `1.1.52` runtime log resolved the previously ambiguous call orientation for
the tested diagonal wall: wrapper results `18` and `16` matched the
prey-to-Hunter core direction, while the Hunter-to-prey direction returned
zero. The bidirectional requirement classified both as blocked, prepared and
consumed the continuation ticket, and prevented the known bad attack handoff.
There were no Improved Hunters callback failures or managed exceptions. A
successful `visible-attack-handoff` was not observed; two later Vanilla direct
attack observations returned zero.

Version `1.1.53` temporarily added `HunterDeerFreezeDiagnostic.cs` as test
scaffolding. Although it installed no new hook and wrote no unit field, it set
`SkipOriginalFunction` on the Script Extender's existing
`OnUnitMovement(Pre)` event for living deer. The runtime test disproved the
assumption that this was a movement-only suppression. From `21:28:37.759`, five
deer at the same origin had their original handler skipped. Target `17/319`
remained a live eligible unit with no subsequent `OnUnitDelete` before process
exit, but disappeared visually and the later Vanilla direct attack returned
zero. Four slots from the first affected herd (`55`, `15`, `59`, `4`) were
actually deleted shortly after their first skipped callbacks.

Targeted analysis of the canonical installed DLL with SHA-256
`33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`
locates the Script Extender signature
`48 63 C2 4C 69 C0 ?? ?? ?? ?? 41 83 BC 08` uniquely at RVA `0x1801E0`.
The function copies the next-tile coordinates and tile ID into the current-tile
fields and also removes/inserts the unit in global per-tile linked occupancy
lists. Skipping the whole trampoline can therefore desynchronize AI/path,
current tile, tile occupancy and Unity presentation even without a direct field
write by the mod. `OnUnitMovement.SkipOriginalFunction` must not be used as a
unit freeze.

Version `1.1.54` removes `HunterDeerFreezeDiagnostic.cs`, its project entry and
all runtime field, initialization, status, map-reset and disposal wiring. Deer
movement is fully Vanilla again. LOS calibration must use repeated natural
attempts and retain only runs where the same deer identity happens not to move
during the relevant approach window.

### Tile-distance attack decision and active visibility snapshot in 1.1.56

The `1.1.55` runtime test disproved the remaining one-use-ticket assumption.
The compare at RVA `0x130019` uses the maximum absolute delta of world
coordinates `+0x70E/+0x710`, while the later compare at RVA `0x1300EA` uses the
Manhattan distance of tile coordinates `+0x71C/+0x71E` retained in `EDI`.
Consequently, `EDI<=28` can enter direct attack RVA `0x13013D` in an update that
never passed through the world-distance preparation window. This is a metric
and coverage mismatch, not a PCL-cache expiry.

Version `1.1.56` installs no new native hook and does not change either decoded
overwrite span. The existing exact-hash hook at `0x1300EA` is now authoritative.
For `EDI<=28` it revalidates the same live Hunter/prey identity, state `1`, prey
reservation `2`, active incomplete path and a positive active-target PCL
snapshot. It then consumes an active visibility snapshot without making a
native visibility or PCL call inside the hook. A blocked wrapper result, a
directional disagreement or the bounded first-probe pending state changes only
`RDI` to `29`. A fresh result with both core directions positive leaves `RDI`
unchanged and hands control to Vanilla's direct attack. Missing, expired or
invalid snapshots, PCL zero, identity changes and all errors remain fail-open.

`HunterActiveTargetVisibilitySnapshot` runs from the existing approximately
100-ms persistent scan. For each unchanged active target it invokes the already
validated wrapper/core delegates no more than once per second and exposes the
result for at most two seconds. Its identity includes Hunter slot/global ID,
prey slot/global ID/type, controlling player, map generation, state, path state
and path length, and reservation. A changed identity or relevant state creates
an immediately due probe. Before that first probe, only a two-second
`visibility-pending` interval can select Vanilla distance `29`; a permanent
probe failure cannot extend this interval.

The `0x130019` callback consumes the same snapshot solely to bypass Vanilla's
destructive world-near target query. It no longer issues or authorizes a ticket
for `0x1300EA`. To cover the first possible attack update before the next scan,
the existing state-0 `MoveHere` result callback promotes a still-fresh positive
target-selection PCL cache entry into the separately readable active snapshot.
This promotion performs no native PCL call and is accepted only when current
player, mode, source PCL, target PCL and both stable identities still match the
cached inputs.

The existing static requirements remain unchanged for future DLLs: validate
the full hook spans and all inbound branch targets, the `EDI` tile-distance
semantics, state/target/reservation/path offsets, the direct-attack call/result
locations, and the wrapper/core ABI. The new snapshot mechanism is managed and
adds no RVA, but it depends on those exact native semantics. Version `1.1.56`
is pending the seven Package-E in-game scenarios and must not be treated as
accepted based on build success alone.

### Active visibility identity correction in 1.1.57

The `1.1.56` runtime log exposed a managed identity error rather than a changed
native layout. `GameUnit +0xF4` changed repeatedly while the same active path
advanced. The first implementation included that live locomotion substep in
`VisibilityInputs.Equals(...)` and `GetHashCode()`, replacing the tracker on
nearly every 100-ms scan despite the configured one-second probe interval.
Positive visibility results therefore frequently became
`new-identity-pending` before the authoritative `0x1300EA` callback could read
them.

Version `1.1.57` removes `+0xF4` only from stable equality and hashing. It keeps
the field in capture and diagnostics. Hunter/prey slots and global IDs, prey
type, player, map generation, AI state, path state and length, and reservation
remain identity-bound. Forward path progress remains observation-only; a
backwards progress jump still replaces the tracker as the existing path-restart
guard. No RVA, hook, native call, field offset, overwrite span or behavior write
changes in this version. Future updates must retain this distinction between
the live `+0xF4` locomotion value and validated stable path identity.

### Accepted MoveHere path generations in 1.1.58

The `1.1.57` runtime test showed that `GameUnit +0xF6` is not a monotonic path
generation either. The same Hunter/prey identity and path length repeatedly
returned to `new-identity-pending`; 72 of 84 measured same-group snapshot
intervals were shorter than 900 ms. A clear target was handed to Vanilla at
tile distance `28`, then returned to pending 91 ms later and remained visible
while the old path advanced to `19/20`. This was a managed tracker-boundary
error, not evidence of a new native offset or a hard reduced attack range.

Version `1.1.58` removes the raw progress-decrease replacement condition. The
already validated state-0 MoveHere result hook advances a managed per-Hunter
path generation only when Vanilla returns success. This callback already
promotes the matching positive PCL selection result, so it is the earliest
validated path-acceptance boundary and requires no new native hook or call.
Stable Hunter/prey/player/map/state/path-length/reservation identity checks
remain independent replacement conditions.

Queued visibility probes capture that generation. Before the native visibility
call, on probe failure and before snapshot commit, the code verifies both the
tracker generation and the latest accepted generation. An in-flight probe from
an older path therefore cannot populate a tracker for a newer accepted path,
even when Hunter, prey and path length are otherwise identical. The generation
is emitted as `visibilityPathGeneration` in world-refresh and tile-decision
logs. World and tile logging use separate throttle dictionaries so alternating
callbacks no longer overwrite each other's signatures and exhaust the shared
diagnostic budget.

No RVA, native signature, field offset, overwrite span, visibility/PCL call
site or behavior write changes in `1.1.58`. `+0xF4` and `+0xF6` remain captured
for diagnostics only. A future DLL update must preserve the successful
state-0 MoveHere result semantics before this managed generation bridge is
considered valid.

### Bounded tracker retention in 1.1.59

The `1.1.58` runtime test confirmed the explicit MoveHere generation but exposed
a second managed lifetime error. Five accepted paths produced 172
`new-tracker-pending` observations and 352 pending tile decisions. The cleanup
pass removed a tracker whenever one 100-ms scan could not capture the complete
state-1/path/reservation context, even though the authoritative inline hook
validated that same context again milliseconds later. A positive handoff could
therefore disappear before Vanilla reached its direct-attack update.

Version `1.1.59` retains a recently validated tracker for at most two seconds of
scan absence. Every complete scan capture and every complete inline-hook capture
refreshes that bound. A transient miss neither replaces the identity nor clears
the snapshot; a target, global ID, player, map, stable state, path-length,
reservation or accepted-MoveHere generation change still replaces the tracker
through the existing validation path. An invalid live context cannot consume a
retained snapshot because `TryGetObservation(...)` performs the complete native
input capture before entering the tracker lookup.

The first retained miss records the exact failed capture predicate, and a real
two-second expiry records absence and last-validation age. This change adds no
RVA, hook, native call, field offset or behavior write. Movement, attack range,
cooldown, path, animation, speed and AI state remain Vanilla-owned.

### Geometry and locomotion-gate diagnostics in 1.1.60

The `1.1.59` runtime log confirms bounded tracker retention but separates the
remaining late-shot symptom into two native stages. Every observed positive
snapshot released the distance override while public `GameUnit +0xF4` was
still nonzero. Every later direct attack at RVA `0x13013D` occurred only after
that field became zero, matching the audited comparisons at `0x130110` through
`0x130124`. Separately, some visually clear approaches kept wrapper RVA
`0xA06F0` at zero until short range. The former logs did not expose exact probe
geometry or the two direct core results for a wrapper-zero sample.

Version `1.1.60` does not change the authoritative wrapper classification,
hook span, register mutation or any `GameUnit` field. Each existing once-per-
second active-target probe captures the exact Hunter/prey tile coordinates,
world endpoints and height arguments used for that call. It also invokes the
already exact-hash-validated core delegate at RVA `0x9E350` in both directions
when the wrapper is zero as well as when it is positive. These two additional
bounded calls are diagnostic only; wrapper zero remains blocked. A disagreement
is logged as a warning and cannot authorize an attack.

The tile-decision marker now distinguishes distance-override release with a
ready `+0xF4 == 0` Vanilla gate from release deferred by nonzero `+0xF4`. A
future correction must first use these samples to decide between native
visibility semantics and snapshot freshness. Any eventual bypass of the
`0x130110` gate requires a new complete overwrite-span, inbound-target and
relocation audit and must enter the unchanged direct-attack sequence exactly
once; writing `+0xF4`, path state or AI state remains forbidden.

### Position-bound visibility refresh and attack-gate handoff in 1.1.61

The `1.1.60` runtime log resolves both open questions on the audited DLL. Free
targets produced a positive wrapper and two positive core directions at tile
distances `26`, `12` and `13`; subsequent direct attacks were delayed by about
`301 ms` and `1,213 ms` while the gate at `0x130110` remained deferred. Thus the
remaining free-target latency is downstream of native visibility. Conversely,
long blocked paths and both moving-prey attempts stopped receiving probes while
the persistent scan observed reservation `1`. Their last authoritative wrapper
zero then expired after exactly two seconds and the next direct attack returned
zero before state `6`. The shortened blocked path became visible before expiry
and attacked successfully. Every wrapper-zero sample also had both core results
zero, so wrapper classification remains unchanged.

Version `1.1.61` permits raw reservation `1` only in the behavior-neutral
persistent probe capture after validating the complete Hunter, prey, global ID,
player, map, state, active incomplete path and accepted-MoveHere-generation
context. Stable snapshot identity remains normalized to reservation `2`, and
strict inline readers still require raw reservation `2`. Near targets at a
Manhattan tile distance of at most `30` are sampled every `250 ms`; farther
targets retain the one-second interval. A visible result records both exact tile
positions and becomes pending after either position changes. A known blocked
result remains conservative after its two-second readable lifetime only while
the complete strict live context and same active path remain valid, preventing
expiry from authorizing the destructive failed direct-attack path.

The same exact reference hash remains mandatory:

- SHA-256:
  `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`
- HunterUpdate audited function: RVA `[0x12FC20,0x1313D2)`, `6066` bytes
- existing distance hook: RVA `0x1300EA`, decoded overwrite span
  `[0x1300EA,0x1300FD)` = `3+2+5+9` bytes
- distance hook internal `JLE`: RVA `0x1300ED` to `0x130102`
- new attack-gate hook: RVA `0x130110`, decoded overwrite span
  `[0x130110,0x130124)` = `9+2+9` bytes
- relocated gate-ready `JE`: RVA `0x130119` to unchanged attack setup at
  `0x13012A`
- following untouched path-state `JE`: RVA `0x130124` to `0x1313B2`, with
  fallthrough to `0x13012A`
- unchanged direct attack call: RVA `0x13013D` to `0x18E950`

The hook cannot safely begin at the later `JE 0x130124`: any detour large enough
for the callback would overwrite `0x13012A`, which is the target of the earlier
branch at `0x130119`. Starting at the basic-block boundary `0x130110` relocates
the complete first `CMP/JE` and second `CMP` before the callback. If Vanilla's
first comparison is ready, its relocated branch goes directly to original RVA
`0x13012A` without invoking the callback. Otherwise the callback observes the
second comparison's flags. Only a positive PCL snapshot plus a fresh (at most
`500 ms`), position-matched positive visibility snapshot may clear `RFLAGS.ZF`;
the untouched branch at `0x130124` then falls through into Vanilla's attack
setup exactly once. Every rejected or exceptional callback preserves the
original flag and branch. No unit or manager field is written.

Before transaction commit, Iced decodes every complete instruction in both
spans and the following gate branch, validates all expected targets, checks
non-overlap and scans the entire HunterUpdate range for direct conditional
branches, unconditional branches and calls whose targets enter either span's
interior. Any indirect branch anywhere in the audited function disables the
feature because it could conceal a jump-table entry. The targeted Rizin audit on
the canonical DLL reconstructed one `6066`-byte function with `207` basic blocks
and no interior Xref to the gate-span addresses; the disassembly contains the
expected two-entry fallthrough structure and no indirect jump-table dispatch.
Both hooks are added to one rollback-on-failure transaction, so partial
installation is impossible.

On a future DLL hash, do not pattern-scan or enable this handoff. Re-identify the
complete HunterUpdate function and direct attack sequence, repeat instruction,
incoming-target, function-entry, Xref and jump-table audits for both spans, then
update all RVAs and the reference hash together. A matching opcode prefix alone
is insufficient.

### State-10 post-shot continuation in 1.1.62 and resolver correction in 1.1.63

The `1.1.61` blocked-map run accepted the direct attack on target `17/319` at
`10:52:13.318`. The target remained alive, enabled, own-reserved, without a
cooldown and PCL-reachable. At `10:52:14.357`, State 10 invoked the target query,
but the managed ranking reported `allowed=True, fallback=True, best=none`; no
State-0 `MoveHere` followed and the Hunter reached State 6. The same identity
was selected normally again at `10:52:21.301`, excluding death, slot reuse,
target change and persistent unreachability.

Static reinspection of HunterUpdate establishes the exact primary path. State
10 calls the visibility wrapper at RVA `0x130384`. A nonpositive result at
`0x13038B`, a target-global mismatch at `0x1303AF`, or a reservation mismatch at
`0x1303BE` reaches the query call at RVA `0x1304D1`. Its result is tested at
`0x1304D6`; zero falls through the jump at `0x1304E5` to the shared writer at
RVA `0x12FF58`, which writes State `6`, timer `20` and the return-to-hut control
value. A secondary State-10 path calls the same query at RVA `0x130577`; its test
at `0x13057C` branches directly to `0x12FF58` on zero. Nonzero results from both
sites reach the existing State-0 writer at RVA `0x12FF3E`.

The query at RVA `0x18AF00` requires candidate reservation zero. The still-live
original target correctly retained the Hunter's own reservation `2`; therefore
neither the native query nor the mod's free-prey ranking could return it. The
event callback's fail-open `allowed=True` did not stage a fallback because
`TargetSelection.HasTarget` was false. Renewed visibility blocking thus
triggered the primary State-10 query, while the own-reservation mismatch between
that query and the continuation requirement caused the abort.

Version `1.1.62` intended to add two exact-hash-only result hooks:

- primary result RVA `0x1304D6`, decoded overwrite span
  `[0x1304D6,0x1304E5)` = `2+7+6` bytes (`TEST`, RIP-relative current-Hunter
  load, `JNE 0x12FF3E`);
- secondary result RVA `0x13057C`, decoded overwrite span
  `[0x13057C,0x13058B)` = `2+7+6` bytes (`TEST`, RIP-relative current-Hunter
  load, `JE 0x12FF58`).

The primary semantic resolver pattern is
`8B 1D ?? ?? ?? ?? 8B D3 49 8B CD E8 ?? ?? ?? ?? 85 C0 48 63 05 ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? E9 ?? ?? ?? ??`.
The secondary pattern is identical except for `0F 84` at the result branch.
Both calls must resolve to query RVA `0x18AF00`; both RIP-relative loads must
resolve to current-Hunter RVA `0x92F2C4`. Iced validates each full span and
scans the complete HunterUpdate range `[0x12FC20,0x1313D2)` for external direct
branches or calls into either span interior before the rollback-on-failure
transaction commits.

The `1.1.62` runtime test showed that this component failed closed before hook
installation. `ResolveRelativeTarget` expects the first byte of the four-byte
relative displacement and the RVA of the next instruction. The State-10 call
instruction starts at sequence offset `0x0B`, but its displacement starts at
offset `0x0C`; the next instruction starts at offset `0x10`. Version `1.1.62`
incorrectly supplied `0x0B`, so the `E8` opcode became the low displacement
byte and produced invalid targets `0x5BD2FBE` and `0x5BC8A64`. The semantic
target check rejected both, no transaction was created, and Hunter behavior
remained unchanged.

Version `1.1.63` supplies displacement offset `0x0C` and next-instruction
offset `0x10` for both calls. This resolves each target to audited query RVA
`0x18AF00`. The call instruction RVAs (`0x1304D1`, `0x130577`), result-hook
RVAs, decoded spans and all behavioral guards remain unchanged.

An accepted direct attack records Hunter and target slot/global identities.
At either State-10 result hook, continuation additionally requires the same
live Hunter in State 10, the same live enabled target, positive health, corpse
flag zero, own reservation exactly `2`, no other live Hunter targeting that
identity, no MoveHere cooldown and a nonzero PCL result. A known PCL zero leaves
Vanilla unchanged; a technical PCL failure is fail-open, consistent with the
selection filter.

If all guards pass, the callback changes only `RAX` from the query result to the
original target slot. The relocated `TEST` selects Vanilla's existing nonzero
branch, and Vanilla itself writes State 0. On the next State-0 query, the
existing fallback hook supplies the same own-reserved identity exactly once;
Vanilla then performs its normal target assignment, reservation write and
`MoveHere` call. The existing MoveHere-result callback remains authoritative and
applies its normal rejection cleanup and cooldown if path creation returns zero.
The mod writes no Hunter AI state, order, target field, path, speed or animation
field. Dead, invalid, disabled, foreign-targeted, cooled-down or PCL-disconnected
prey never enter this handoff.

#### Earlier State-9 and failed-attack recovery in 1.1.64/1.1.65

The `1.1.63` runtime test proved that the State-10 result hooks initialize and
execute, but also exposed that this handoff is unnecessarily late. After the
shot, HunterUpdate has already entered State 9 and subsequently prepares ESI
`10` before the native AI-state writer at RVA `0x13024A`; State 10 configures
the visible sit/wait animation before its target queries. A second independent
abort occurs when the direct-attack call at RVA `0x13013D` returns zero after a
completed stale path: the branch at `0x13014B` reaches RVA `0x130171`, prepares
timer/state/control values `20/6/1`, then writes State 6 at RVA `0x130191`.

Version `1.1.64` adds both earlier hooks to the same rollback-on-failure
transaction as the two State-10 query-result hooks:

- State-9 completion starts at RVA `0x13023C`, span
  `[0x13023C,0x130253)` = `7+7+9` bytes: RIP-relative current-Hunter load,
  unit-stride multiply and the native AI-state writer. Its exact-hash pattern is
  `48 63 05 ?? ?? ?? ?? 48 69 C8 90 04 00 00 66 42 89 B4 29 18 09 00 00`.
  The RIP-relative operand must resolve to current-Hunter RVA `0x92F2C4`.
  With a live identical target, remaining attempt budget and nonzero PCL, the
  callback changes only RSI from `10` to `0`; the relocated native writer then
  selects State 0 instead of State 10. The projectile-producing State-9 work
  has already completed, so no projectile-delete/end wait is part of this
  decision and the sit/wait animation state is never entered.
- Failed direct attack starts at RVA `0x130171`, span
  `[0x130171,0x130188)` = `7+6+5+5` bytes: unit-stride multiply plus preparation
  of R15/RAX/RSI as `20/6/1`. Its exact-hash pattern is
  `48 69 CA 90 04 00 00 41 BF 14 00 00 00 B8 06 00 00 00 BE 01 00 00 00`.
  The overwritten preparation executes before the callback. For the exactly
  correlated failed direct attack and the same live reachable target, the
  callback changes only those registers to `0/0/0`; Vanillas untouched writers
  at `0x130188`, `0x130191` and `0x13019A` write timer/state/control zero and the
  next Hunter update follows the regular State-0 query/MoveHere path.

Iced must decode the complete spans with the instruction lengths above. The
complete HunterUpdate range `[0x12FC20,0x1313D2)` is scanned for every direct
conditional branch, unconditional branch and call into either span interior.
Known predecessors target the failed-attack hook start `0x130171`, not its
interior. All four transaction spans are non-overlapping. The canonical DLL
hash remains
`33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`;
on a different hash these hooks stay disabled rather than pattern-scanning.

The old 60-second total-attempt bounds in blocked-path continuation and
remaining-path speed selection, plus the 60-second accepted-MoveHere
correlation lifetime, are removed. Total route duration is unlimited while
path progress continues. A three-second no-progress detector and five-second
retry cooldown remain local stall protection, not a hunt-duration limit.
Post-attack continuation has no wall-clock expiry and is instead bounded by
exact Hunter/prey identity, native AI state, live/corpse/reservation checks,
PCL reachability and at most three recovery-causing attacks per Hunter/target
identity. The third attempt leaves Vanilla's abandonment path unchanged and
registers the existing bounded target cooldown.

Version `1.1.64` failed closed before transaction creation because the original
State-9 semantic pattern extended beyond its audited 23-byte hook span and
misencoded the first following instruction. It expected `42 C7 84 ...`, while
the canonical DLL contains `42 89 AC 29 08 09 00 00` at RVA `0x130253`.
Consequently `hunterPostShotContinuationAvailable=False`, and neither new hook
nor either State-10 fallback hook ran during that test.

Version `1.1.65` uses exactly the two 23-byte patterns documented above. Pattern
validation no longer depends on a following instruction, but the Iced semantic
validators independently decode and require it: State-9 span end `0x130253`
must be the eight-byte `MOV [RCX+R13+0x908], EBP`; failed-attack span end
`0x130188` must be the nine-byte `MOV word [RCX+R13+0x920], R15W`. The validators
also require the audited `RAX/RCX/RDX/RSI/R15` operands, immediates
`0x490/20/6/1`, writer displacements `0x918/0x908/0x920`, exact span lengths,
shared current-Hunter global and no direct external branch target in either
span interior. This separates exact hook bytes from the explicit first-after-
span safety check and prevents the overlong-pattern failure from recurring.

Version `1.1.65` nevertheless failed closed because Iced decodes the immediate
operand of both audited `imul r64,r64,imm32` instructions as
`OpKind.Immediate32to64`. Version `1.1.66` requires that actual operand kind for
the State-9 and failed-attack stride multiplies. The three MOV immediates remain
`OpKind.Immediate32`; no accepted byte, instruction length, register,
displacement or branch target changed.

### Guarded stale-moving-target replan in 1.1.66

The exact-hash State-1 result hook is restored at RVA `0x12FF2E` with span
`[0x12FF2E,0x12FF3C)` = `5+2+7` bytes: call to Hunter query `0x18AF00`,
`test eax,eax`, and RIP-relative load of current Hunter ID from `0x92F2C4`.
The first untouched instruction must be the two-byte `je` at `0x12FF3C` with
target `0x12FF53`. Iced validates all instructions and the complete
HunterUpdate direct-branch audit rejects any external target inside the hook
span. Relocated instructions execute before the callback; therefore the loaded
Hunter ID is in RAX while ZF still represents the query result.

The hook normally leaves all registers and flags unchanged. A request exists
only when a successful prior Vanilla `MoveHere` recorded the same live
Hunter/prey slot and global identity, path state `2`, unchanged path length,
own reservation `2`, positive/fail-open PCL validation, a blocked (not pending)
visibility decision, target-anchor Chebyshev displacement of at least six,
opposing Hunter-to-live-target and Hunter-to-anchor vectors (`dot < 0`), and a
live-target Manhattan distance no larger than the obsolete-anchor distance.
The request is atomically limited to once per accepted path generation and at
most three successful moving-target replans in one chain.

For that request only, a zero query result clears ZF so Vanilla takes its
existing State-0 branch; a nonzero result already takes that branch unchanged.
The subsequent audited State-0 query-result hook supplies the same identity
once, and Vanilla owns target assignment and `MoveHere`. A successful
`MoveHere` captures a fresh anchor and generation. If the old path completes
before the near-query handoff, State 0 uses the same identity/PCL/reservation
guards as a path-complete fallback. This replan writes no target, reservation,
path, order, AI-state, speed or animation field, has no hunt-duration limit and
does not reset the independent post-shot attempt budget.

On a future DLL hash, keep this feature disabled. Re-identify both State-10
query calls and their exact zero/nonzero destinations, decode the actual detour
spans, audit every inbound direct and indirect entry, and reconfirm the native
query's reservation-zero filter plus the State-0 and MoveHere semantics before
updating the reference hash and RVAs.

Future Script Extender updates must revalidate the public ranged-damage,
projectile-delete and move-order semantics. A bounded native reachability path
must be identified and validated before pre-shot recovery is enabled again. The
state-1 diagnostic remains exact-hash-only together with the target-search
fallback; production features retain their established exact-hash RVA and
changed-hash unique-pattern strategy.

## Audit for Steam build 24651686

Both complete patterns match exactly once. Their signed 16-bit immediate still
starts at pattern offset `13`; the surrounding animal state remains `0x6E` and
the death-timer field remains `+0x986` in the native manager-relative form.
The automatic target pattern also matches exactly once. Its encoded table RVAs
remain `0x18F9E0` and `0x18F9C4`; chicken entry `0x18F9F2` is `6`, dispatch
entries `0` and `6` remain `0x18F28E` and `0x18F3A6`, and the hunter-only case
still rejects non-hunters at `0x18F754`. Explicit `AttackUnit` still assigns its
target before the automatic dispatch.
The Script Extender initialized the same `0x490`-byte unit records, and targeted
native accesses reconfirmed the raw field map used by the runtime. Fresh/load
map behavior remains a post-build game smoke test.

The granary update and comparison signature were also audited on this hash.
The semantic sequence at `0xD2AB4` is unique, the compare remains
`cmp eax,[rdi+0x2048]` at `0xD2ABF`, its signed `jle` still targets `0xD2BA7`,
and the existing Script Extender granary spawn hook remains on the fall-through
spawn path at `0xD2B4C`.
