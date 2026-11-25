# Market fix separation and SP acceptance, 25 November 2026

Scope: AIMarketOverbuyDiagnostic 0.1.0; existing APIShared 0.73.3 package, unchanged.
Normal runtime sources: src/Fix. Optional development capture, trace, timing and
legacy diagnostic scenarios: src/Diagnostics, compiled only with /devdiag.
The current external acceptance scenario and its AIC fixture live in AIMarketAcceptance.
No integration into a main mod and no dedicated multiplayer Rest4 test in this change.

The fix retains its existing source-specific policies, native spans, save identity
and resync contract. Private request provenance must belong to the active session
before merging a new source, modifying a native amount, or serializing a row.
Mismatches relinquish private ownership; they do not alter native pending amounts.
Repair start and completion still clear the ledger on both peers, without retaining
credits or imposing a cap on unknown orders. The accepted post-repair boundary remains.

Normal initialization and mission integrity checks are independent of diagnostics.
Core failures disable correction and report the first cause through APIShared UI
dispatch. Optional preparation and capture failures cannot prevent core installation.
The normal package has no diagnostic overview, event ring, timing histogram, worker
or test scenario; Verify-Package.ps1 checks both positive core inclusion and the
normal/development assembly boundary. Version and published identities stay unchanged.

Checks passed: scoped runtime/JSON/lifecycle/native-publication guardrails; existing
observation reference/performance suite; demand-target and save/lifecycle behavior
suite; actual installed native-backend ABI, register and relay fixtures. Both
/devdiag and normal builds passed their compiled package boundary checks.
The normal package was used for the game run, not the development package.

SP run: 488c98415a814e908fb6dde369781943, 25 November 2026 00:40 Europe/Berlin.
One funded custom AI with a real native recruitment-produced five-item request.
Player2, good22: external supply1, consumption1 -> pending4/credit1.
Two ordinary save reloads retained the raw pre-first-fold state pending4/credit1.
Final supply4 -> pending0/stock4; consumption4 -> pending0/stock0; gold unchanged.
The next native purchase decision made zero buy attempts for that good.
Existing local owner callback and pure-save-capture component checks also passed;
these are explicitly not a two-peer resync/transport proof.
No Error/Fatal/MOD_FAILURE appeared in the test log.

Evidence: .work/a/market-core-split/001/488c98415a81/LogOutput.log and desktop.json;
normal-build.log, devdiag-build.log, acceptance-build.log and runner.log in parent.
Runner PASS: original plugins/settings/saves restored and verified. The actual game
window stayed on the nonactivated test desktop, input desktop Default before/after.
Temporary copied plugin layout removed. SDK sources/packages were not rebuilt.
