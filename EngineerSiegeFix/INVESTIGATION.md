# Engineer duplication investigation

## Scope and symptom

The reported Vanilla defect occurs rarely after engineers finish constructing a siege engine in a siege tent. The resulting catapult or trebuchet is fully crewed and usable, but two of the original engineers can remain as idle free units.

This standalone test mod exists to identify and validate the native handoff before any fix is integrated into `BugfixesAndQoL`.

## Native baseline

- Canonical installed `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- All RVAs and byte contracts below were checked against `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` and the matching `FBCB9319` semantic baseline.
- Addresses must be treated as invalid for any other native hash. The mod requires the current hash and otherwise remains inactive.

## Confirmed native semantics

The baseline contains two large state functions with explicit siege-engine crew logic:

- `FUN_1801520D0`, RVA `0x1520D0`: catapult state function.
- `FUN_1801535F0`, RVA `0x1535F0`: trebuchet state function.

Their state-6 branches validate an existing crew, apply a 16-phase tick throttle, search engineers and then write the crew IDs/global IDs. The catapult branch requires two engineers and the trebuchet branch requires three. Both write engineer consume state `0x0005006D`, fade value `0x0200`, command `3`, clear selection/group membership and finally set the device state to ready.

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

## Acceptance criteria for a future fix

A fix is not considered validated until logs from normal catapult and trebuchet construction show:

1. The relevant callback is reached for both device types.
2. The device ID/global ID remains stable across the handoff.
3. Exactly two or three unique engineer ID/global-ID pairs are committed.
4. The device becomes ready with matching crew slots.
5. All original engineers leave the live unit population within the bounded validation interval.
6. No failure, inconclusive result or correction-disabled marker occurs.
