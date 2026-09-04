# Engineer duplication investigation

## Scope and symptom

The reported Vanilla defect occurs rarely after engineers finish constructing a siege engine in a siege tent. The resulting device is fully crewed and usable, but the original engineers can remain as idle free units. Besides catapult and trebuchet reports, the user reports video evidence of the same duplication on an Arab ballista, which uses two engineers.

This standalone test mod exists to identify and validate the native handoff before any fix is integrated into `BugfixesAndQoL`.

## Native baseline

- Canonical installed `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- All RVAs and byte contracts below were checked against `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` and the matching `FBCB9319` semantic baseline.
- Addresses must be treated as invalid for any other native hash. The mod requires the current hash and otherwise remains inactive.

## Confirmed native semantics

The baseline contains three state functions with explicit siege-engine crew logic:

- `FUN_1801520D0`, RVA `0x1520D0`: catapult state function.
- `FUN_1801535F0`, RVA `0x1535F0`: trebuchet state function.
- `FUN_180171C50`, RVA `0x171C50`: Arab-ballista state function, selected by unit-handler table entry `0x4D`.

The catapult and trebuchet state-6 branches validate an existing crew, apply a 16-phase tick throttle, search engineers and then write the crew IDs/global IDs. Catapult and Arab ballista require two engineers; the trebuchet requires three. All three handlers contain an existing-crew recovery block that clears the work field, writes packed engineer state `0x0005006D`, writes visual transition value `0x0200`, and clears the transition counter in that exact order.

The exact canonical recovery blocks begin at RVA `0x15249A` (catapult), `0x1539DB` (trebuchet), and `0x172156` (Arab ballista). These byte sequences and the type-to-handler table entries are covered by static tests. An earlier tentative association of RVA `0x1547F0` with the Arab ballista was wrong; that address belongs to another two-engineer handler and must not be used to identify type `0x4D`.

This proves what these functions do. It does not by itself prove that normal human siege-tent completion executes these branches.

## Previously negative runtime paths invalidated by lifecycle teardown

All negative runtime conclusions in this section must be retested. Their static semantics and hook byte contracts remain useful, but their zero-callback results are invalid because normal SHCDE startup disposed the runtime and uninstalled the hooks before the tested map activity.

### Siege-tent unit function at RVA `0x158690`

The first implementation hooked `FUN_180158690`, whose decompiled behavior marks a siege-tent unit for conversion and stores a pending catapult/trebuchet type and state 6.

The first attempted function detour at RVA `0x158690` failed activation and produced no runtime evidence. Later builds successfully installed a six-byte observation hook at the function entry and an observation hook at the completion tail at RVA `0x158762`, but no callbacks appeared during normal construction. Those later zero-callback results are invalid because the hooks were removed during startup cleanup. The function must be observed again with lifecycle-safe hooks.

### Pending unit converter at RVA `0x195D10`

The second implementation hooked `FUN_180195D10`. Its baseline semantics clearly convert a pending unit slot by copying pending type/state into active fields.

Runtime result on 2026-09-03: a catapult tent and trebuchet tent were created and removed normally, while the log contained zero `SIEGE_CONVERSION_CALLBACK`, commit, rejection or validation markers. This does not prove that normal construction bypasses the converter because the five-byte entry hook had already been uninstalled by startup `OnDestroy()`.

### Catapult and trebuchet state-6 interiors

The third implementation hooked the exact pre-throttle reads inside the proven crew-search branches:

- Catapult RVA `0x1524FA`, 7-byte instruction `8B 84 2B DC 09 00 00`.
- Trebuchet RVA `0x153A78`, 8-byte instruction `42 8B 84 1B DC 09 00 00`.

Both hooks were installed successfully and included a fail-closed check that the displaced phase-seed read in `RAX` matched the device field. Every execution would necessarily have emitted either `SIEGE_STATE6_CALLBACK` or a correction-disabled error.

Runtime result on 2026-09-03 at 21:36: catapult tent building 146 and trebuchet tent building 147 completed and were removed. The log contained zero state-6 callbacks and zero correction-disabled messages. This result is also invalid as route evidence because both hooks had already been uninstalled by startup `OnDestroy()`.

## What has not been proven

- No attempted hook has yet observed the actual building-to-unit handoff used by normal player construction.
- No runtime log has yet shown a correction commit or validation pass.
- The rare duplication defect has not been reproduced under instrumentation.
- Static similarity, decompiled behavior and a plausible call graph are insufficient evidence that a function participates in the live construction route.

## Current diagnostic direction

The next stage must discover the active route rather than choose another semantic candidate. It should observe the central unit dispatch boundary and record the current one-based unit ID, type, state, global ID and actual native handler target for siege-related units. The diagnostic must not modify game state. Once a normal catapult and trebuchet build produce concrete handler addresses and state transitions, only that runtime-confirmed path may be considered for a corrective hook.

## Dispatcher evidence and next instrumentation

The canonical baseline now establishes the dispatch chain used during the central unit update:

- `FUN_180182B00`, RVA `0x182B00`, iterates active unit slots.
- RVA `0x184103` loads the unit type from the current slot.
- RVA `0x18410C` performs the indirect call through the table at RVA `0x321CB0`.
- Table entry `0x27` at RVA `0x321DE8` points to the catapult handler at RVA `0x1520D0`.
- Table entry `0x28` at RVA `0x321DF0` points to the trebuchet handler at RVA `0x1535F0`.

The fourth diagnostic build hooks only the first complete five-byte instruction at each of these two handler entries. At runtime it additionally validates the relocated handler-table pointers before installing either hook. The callbacks are intended to record the dispatcher context ID and all relevant device identity, state, pending-conversion and crew fields. A once-per-game-tick observer is intended to record siege-slot and associated-engineer transitions so that the state immediately before and after handoff can be correlated.

This stage is intentionally observation-only. The former state-6 correction code and all native cleanup calls have been removed from the active runtime, so this build cannot alter engineers, crew slots or siege-engine readiness.

## Fourth diagnostic build and runtime result

The observation-only build retained mod version `0.1.0`. It compiled with zero warnings and zero errors, passed 464 static assertions and was installed successfully. The built and installed DLLs were byte-identical with SHA-256 `773CA5706B3BE110F8F79F1D9C3BFE88660D825B86E358CE9320B29A0FBB0D5F`.

The game session beginning on 2026-09-03 at 22:02 produced the following confirmed initialization evidence:

- `CrusaderDE.dll` again matched the canonical SHA-256.
- Both handler signatures resolved at their expected RVAs.
- `SIEGE_ROUTE_DIAGNOSTIC_INSTALLED` was emitted once.
- The relocated runtime table entry for type `0x27` pointed to RVA `0x1520D0`.
- The relocated runtime table entry for type `0x28` pointed to RVA `0x1535F0`.
- Both five-byte entry hooks reported active during initialization.
- The diagnostic explicitly reported `correctionActive=false`.

The same session contains independent building-cache evidence for a catapult tent and trebuchet tent:

- Catapult tent building 58 was created at 22:04:29 and deleted at 22:04:36.
- Trebuchet tent building 59 was created at 22:04:30 and deleted at 22:04:40.
- `CHIMP_TYPE_CATAPULT` and `CHIMP_TYPE_TREBUCHET` occurred in `ActiveSiegeTentCache` messages. These messages describe the intended unit type associated with each building-side tent; they do not prove that a resulting unit slot was observed.

Despite those completed tent lifecycles, the following marker counts were all zero:

- `SIEGE_DIAGNOSTIC_SESSION`
- `SIEGE_HANDLER_ENTRY`
- `SIEGE_SLOT_TRANSITION`
- `SIEGE_ENGINEER_TRANSITION`
- `SIEGE_ROUTE_DIAGNOSTIC_DISABLED`

There were no Engineer Siege Fix exceptions or explicit diagnostic failures. The game continued normally until the session ended at approximately 22:06:49.

### Confirmed lifecycle cause of the missing markers

The fourth run does not disprove the central dispatcher or either handler. The independent Unity polling path also failed to emit even its first session marker, so two intended observation mechanisms remained silent at once. Without a functioning control observation, zero handler callbacks are not sufficient evidence that the handler functions were not executed.

The new workspace lifecycle contract and its referenced Script Extender investigation establish the primary cause: SHCDE destroys Unity components created during early `BaseUnityPlugin.Awake()` as part of normal startup. It calls the plugin's `OnDestroy()` at that time even though the process and modded game are continuing.

The fourth diagnostic plugin performed all three invalid lifecycle operations:

1. It stored the runtime only in an instance field of the short-lived plugin component.
2. It depended on that component's `Update()` method for polling.
3. Its startup `OnDestroy()` unsubscribed `CrusaderLibrary.LibraryLoaded`, called `runtime.Dispose()`, unloaded both native hooks and cleared the runtime reference.

This exactly explains the observed sequence: the handler signatures and hooks were installed early enough to emit `SIEGE_ROUTE_DIAGNOSTIC_INSTALLED`, then normal SHCDE startup cleanup removed them before the map and siege-tent tests. It also explains why neither native callbacks nor the Unity polling marker appeared and why no diagnostic exception was logged.

The raw global at RVA `0x37ED4D0` was also an unvalidated tick source in this runtime. Although it is no longer needed to explain the complete failure, relying on it and returning before re-reading the unit manager was an additional avoidable weakness.

The handler-entry hooks were installed before the later-loaded official `Fixes` mod. Inspection of `Fixes` 1.9.1 found no hook at RVA `0x1520D0` or `0x1535F0`, so no direct overlap with these two hooks is currently known. Nevertheless, the next diagnostic should observe the proven indirect call itself at RVA `0x18410C`; this avoids relying solely on entry-hook execution and records the actual target selected for every relevant dispatch.

## Existing related fix in the official Fixes mod

The installed official `Fixes` mod version 1.9.1 contains a setting named `HarassingTentIdleEngineersFix`. Its own description explicitly says that it attempts to fix engineers duplicating and becoming idle when mounting a harassment siege-engine tent.

The implementation is limited to the AI harassment deployment path. It hooks `c_game_ai_deploy_harassing_siege_engine_tents` and delays reissuing the targeted tribe command through `GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(...)`. It does not atomically transfer engineer identities and does not hook the catapult or trebuchet state functions.

Consequences:

- This is strong independent evidence that command timing can cause an idle-engineer duplication variant.
- It specifically concerns AI harassment tents and does not establish the cause of normal human construction.
- Because the option is enabled by default in the installed Fixes mod, it may mask or reduce reproduction of the AI-specific variant during tests.
- It does not explain why all fourth-build observation markers were absent and it does not conflict directly with the two handler-entry hooks.

## Lifecycle-safe fifth diagnostic build

The corrected build remains observation-only and changes the runtime lifetime as follows:

1. The runtime and logger are held through static process-wide references independent of the destroyed Unity component.
2. `CrusaderLibrary.LibraryLoaded` uses a static handler and is not removed by startup `OnDestroy()`.
3. The plugin has no `Update()` and no `OnDestroy()` teardown.
4. Simulation polling is driven by the established `GameTimeManagerAPI.Instance.OnTick` publisher.
5. The first three tick callbacks emit `SIEGE_TICK_HEARTBEAT` before any unit-manager guard, proving that the runtime survived startup cleanup.
6. The raw `GameTickRva` and `lastPollTick` guard are removed.
7. The existing handler-entry hooks and slot/engineer observation remain read-only.

If this lifecycle-safe run still produces tick heartbeats but no handler entries, the next native step is to hook the proven indirect dispatcher call at RVA `0x18410C` with a fully documented register, stack, flags and displaced-instruction contract. No corrective writes may be restored until both catapult and trebuchet construction produce a complete runtime-confirmed handoff sequence.

## Combined lifecycle-safe retest

Because every earlier zero-callback result was affected by the same lifecycle teardown, all previous candidates are enabled simultaneously in the next build. The seven observation points are non-overlapping and remain read-only:

1. Siege-tent unit tick entry at RVA `0x158690`, six-byte span covering `push rbx` and `sub rsp,0x30`.
2. Siege-tent unit completion tail at RVA `0x158762`, eleven-byte pending-field clear before the epilogue.
3. Pending unit converter entry at RVA `0x195D10`, five-byte RBX save.
4. Catapult state-6 phase-seed read at RVA `0x1524FA`, seven-byte instruction.
5. Trebuchet state-6 phase-seed read at RVA `0x153A78`, eight-byte instruction.
6. Catapult handler entry at RVA `0x1520D0`, five-byte RBX save.
7. Trebuchet handler entry at RVA `0x1535F0`, five-byte RBX save.

Every hook executes its complete displaced Vanilla instruction before the callback, preserves all registers through `X64SmartCPUContextRegs.All` and only reads state for logging. The converter obtains its manager and one-based unit ID from its native `RCX`/`RDX` arguments; the other candidates use the central current-context unit ID. Each path has a distinct marker, and unchanged repeated snapshots are suppressed to keep one test run readable.

One normal test run with at least one completed catapult and one completed trebuchet can therefore retest every previous candidate. A valid run must first contain `SIEGE_TICK_HEARTBEAT`, proving survival beyond startup cleanup, followed by whichever of the seven candidate markers actually participate in the construction route.

## Crash in the combined-hook diagnostic and safe replacement

The combined lifecycle-safe run on 2026-09-03 conclusively proved that the lifecycle correction works, but it also exposed that the seven-hook diagnostic itself is unsafe:

- `SIEGE_ROUTE_DIAGNOSTIC_INSTALLED` reported all seven hooks active.
- `SIEGE_TICK_HEARTBEAT` appeared from tick 1 and `SIEGE_DIAGNOSTIC_SESSION` followed, proving that the runtime survived SHCDE's startup cleanup.
- The converter-entry hook at RVA `0x195D10` executed 72 times. Every recorded call was an ordinary global pending-unit conversion; this confirms that the function is not siege-specific.
- None of the six siege-path markers appeared: no handler entry, state-6 search, siege-tent entry or siege-tent completion marker was written.
- After catapult, trebuchet, siege-tower, Arab-ballista and portable-shield tents had been created, the process terminated abruptly at approximately 23:23:29. The BepInEx start line of the subsequent process was appended to the unfinished final log line, providing direct evidence of an abnormal termination and restart.
- No managed exception, Engineer Siege Fix error, Windows Application Error event or crash dump was available.
- The test mod was absent from the subsequent process and that process continued running, which is consistent with the user's isolation of this mod after the crash.

The native baseline still confirms the semantic targets: RVA `0x1520D0` is the catapult handler containing the two-engineer state-6 search, RVA `0x1535F0` is its trebuchet counterpart, and RVA `0x195D10` is the general pending-type converter. Thus the function identification was not disproved. The failure occurs before the first siege-specific managed marker and is therefore compatible with a native context-hook stub or its managed transition failing on first execution. The exact one of the six unobserved hooks cannot be identified from this run alone. The catapult/trebuchet entry hooks are the leading suspects because they first become reachable when the resulting device begins dispatch and their callbacks run at a native function entry, but this remains a bounded inference rather than a confirmed crash address.

The next build removes all native observation hooks, hook transactions and native callback contexts. It uses only the already runtime-proven `GameTimeManagerAPI.OnTick` subscription and direct read-only unit snapshots. It tracks every engineer instead of filtering on the still-unconfirmed command and target offsets, and tracks all engineer-built siege unit types rather than only catapult, trebuchet and the tentative siege-tent unit type. This preserves one-run coverage of resulting device state, crew slots, original engineer identity and survival while no longer altering any instruction in the construction, conversion or handler paths.

The former seven-hook run must not be repeated. A crash-free completion with the safe build will isolate the crash to the removed instrumentation set; it will not by itself distinguish which former hook was responsible. Any future instruction-level diagnosis must use either a native crash address or a separately audited mechanism that does not call managed code from these sensitive transitions.

## First successful hook-free runtime trace

The run beginning on 2026-09-03 at 23:40 used the safe build with `activeObservationHooks=0` and completed without a crash. It provides the first valid runtime trace of normal siege-engine handoff:

- The first three `SIEGE_TICK_HEARTBEAT` markers and `SIEGE_DIAGNOSTIC_SESSION` prove that the lifecycle-safe runtime and tick polling remained active on the map.
- No `SIEGE_ROUTE_DIAGNOSTIC_DISABLED`, managed exception or log-limit marker occurred.
- A catapult tent was created and deleted. At tick 1505, catapult unit 192/global 7332401 appeared with `crewCount=2`, crew IDs `[95,104]` and matching crew globals `[7331480,7331540]`. The same two engineer identities changed from state `0x00070007` to `0x00070005` in that tick. The device changed from alive state 1 to 2 at tick 1506 without changing its identity or crew references.
- An Arab ballista appeared at tick 1564 as unit 194/global 7332441 with two matching crew identities, `[106/7331586,110/7331636]`, and became alive state 2 at tick 1565.
- A portable shield appeared at tick 1687 as unit 198/global 7332560 with one matching crew identity, `126/7331685`, and became alive state 2 at tick 1688.
- A trebuchet tent was deleted at tick 1879. In the same tick, trebuchet unit 205/global 7332618 appeared with `crewCount=3`, crew IDs `[128,130,134]` and matching globals `[7331729,7331740,7331744]`. All three engineer identities simultaneously changed from state `0x00070007` to `0x00070005`. The device became alive state 2 at tick 1880 with unchanged identity and crew data.

This confirms that the hook-free snapshots are useful and that removing the native diagnostic hooks eliminated the completion crash. It does not prove which individual hook caused the crash, only that the failure was inside the removed instrumentation set rather than required for normal Vanilla completion.

### Corrected native invariants

The trace disproves two earlier assumptions that must not be used by the eventual fix:

1. Successfully assigned engineers do not immediately disappear from the native live-unit array. During the observed successful handoffs they remain `alive=2` and type `0x1E`, while their packed state changes from `0x00070007` to `0x00070005`. A later lack of further transition does not make them idle; the visible and controllable/free status must be derived from the correct state semantics rather than `alive` alone.
2. The fields currently logged as `command` and `target` were zero for every engineer participating in these successful human-player handoffs. A human association rule requiring command `0x10` plus the resulting device ID would therefore reject proven valid Vanilla crew. Those offsets may represent a different phase, may be cleared before the first post-tick snapshot, or may not be the correct fields for this route.

The device-side crew ID/global-ID pairs are now the strongest observed identity relation. The type-specific data at the same offsets inside engineer records is a union and must not be interpreted as engineer crew arrays; its short `1 -> 2 -> 0` sequence after handoff is only raw transition evidence until its semantics are confirmed from the engineer state machine.

## Automatic verdict and safe fault-model validation

The next diagnostic build converts the confirmed trace into an automatic, bounded verdict for catapults, trebuchets and Arab ballistas. It installs no native hook:

1. A tracker starts when a catapult, trebuchet or Arab ballista first appears with its device-side crew ID/global-ID fields.
2. Device unit ID, device global ID, required crew count and every ordered crew ID/global-ID pair are frozen as the expected handoff identity.
3. On every `GameTimeManagerAPI.OnTick`, the tracker rejects a missing/reused device slot, changed or duplicate crew identity, missing/reused engineer slot, wrong owner/type/alive state, or a referenced engineer whose packed main state is not the observed bound state 5.
4. A ready device with any such violation emits `SIEGE_HANDOFF_FAILED` immediately. A stable ready device emits `SIEGE_HANDOFF_PASSED` after 256 ticks. A removed or reused device identity emits `SIEGE_HANDOFF_INCONCLUSIVE` rather than a false failure.
5. Each real tracker also runs the same pure verdict policy against two shadow inputs: the valid bound-crew model must pass at the timeout, while a copy with one referenced engineer modeled as idle must fail. The result is logged as `SIEGE_HANDOFF_DETECTOR_SELF_TEST` with `faultInjection=shadow-only,no-game-state-write`.

The original shadow injection deliberately did not modify the game. It proved that the verdict policy distinguishes the confirmed normal invariant from the reported duplicate/idle failure model, but it could not prove the memory-write recovery path.

## Active fail-closed recovery and controlled live proof

The current build adds a bounded correction and a controlled live postcondition test for all three confirmed types. It still uses only the lifecycle-safe `GameTimeManagerAPI.OnTick` observer and installs zero native hooks.

For a naturally occurring defect, recovery is considered only after the full 256-tick observation window. The device must still have the same unit/global identity, live state, type and owner, and exactly the frozen unique crew ID/global-ID pairs. Every repair target must be the exact live engineer identity referenced by the device, owned by the same player, in main state 0, and within 32 world-coordinate units on both axes. All device and engineer reads are repeated before the first write. Any mismatch causes no write.

The recovery reproduces only the four writes in Vanilla's existing-crew block, in native order: work `0`, packed state `0x0005006D`, visual `0x0200`, counter `0`. It does not call a native helper and does not synthesize crew IDs. Afterward the exact device and crew identities must progress through recovery main state `0x6D` to the observed bound main state `5` and remain stable for a further 256 ticks. Failures are explicit and bounded.

Because the natural bug is rare, the first normally passed device of each supported type receives one controlled live proof. All referenced bound engineers are changed together to the reported idle postcondition: two for catapult and Arab ballista, and three for trebuchet. The normal detector must classify the complete idle crew as failure, and the same recovery path repairs the complete crew within the same tick callback before Vanilla can process an idle frame. Every original four-field snapshot is restored if any precondition or write verification fails. Successful evidence is the ordered marker chain `SIEGE_HANDOFF_PASSED`, `SIEGE_CONTROLLED_FAULT_INJECTED`, one `SIEGE_REPAIR_APPLIED` per crew member, `SIEGE_REPAIR_REBOUND`, and `SIEGE_REPAIR_VERIFICATION_PASSED` for type `0x27`, `0x28`, and `0x4D`.

The full-crew fault model reflects the user's additional video evidence: ten observed fire/Arab ballistas each produced two additional engineers. The previous live proof recovered only one injected engineer per type and was therefore positive but incomplete evidence for this exact symptom. The full-crew proof supersedes it. General transition deduplication also ignores continuously changing work and visual counters now; those fields remain present in targeted recovery evidence but no longer exhaust the diagnostic log limits during ordinary animation.

### Temporary visible Arab-ballista reproduction mode

The next test build intentionally withholds the mod recovery only for the first normally completed Arab ballista (`0x4D`). After the normal handoff passes its 256-tick identity and bound-state check, both referenced engineers are changed together to packed state `0x00000000`. The tracker emits `SIEGE_ARAB_BALLISTA_FAULT_LEFT_IDLE`, finalizes that device, and neither calls the recovery path nor restores the injected states. This deliberately exposes the artificial fault to subsequent Vanilla ticks so the user can verify whether it produces the reported visible result: a still-crewed usable fire ballista plus two selectable idle engineers.

Catapult and trebuchet retain the same-tick complete-crew recovery proof. This Arab-ballista mode is destructive diagnostic instrumentation, not a release fix, and must be removed immediately after the visual reproduction result has been recorded. If Vanilla changes the two engineers back without mod intervention, that is equally important negative evidence against packed state zero alone being sufficient.

This proves that the detector and actual game-memory correction work against the documented failed postcondition without waiting for the rare timing defect. It does not reproduce or prove the exact original timing race. Other siege types remain read-only snapshot observations because their construction and crew contracts have not been established as equivalent.

## Acceptance criteria for a future fix

A fix is not considered validated until logs from normal catapult, trebuchet and Arab-ballista construction show:

1. The relevant callback is reached for both device types.
2. The device ID/global ID remains stable across the handoff.
3. Exactly two or three unique engineer ID/global-ID pairs are committed.
4. The device becomes ready with matching crew slots.
5. Every referenced engineer reaches the confirmed bound state without becoming a visible, controllable or idle free engineer; remaining `alive=2` in the native slot is normal and must not be treated as duplication by itself.
6. Device crew IDs and global IDs continue to match the same engineer identities, with no reused or stale slot accepted.
7. No failure, inconclusive result or correction-disabled marker occurs.
8. For each of types `0x27`, `0x28`, and `0x4D`, the controlled live-proof marker chain completes through `SIEGE_REPAIR_VERIFICATION_PASSED` without exposing an idle engineer to a Vanilla tick.
