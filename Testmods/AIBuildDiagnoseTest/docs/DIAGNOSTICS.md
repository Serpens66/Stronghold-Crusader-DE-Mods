# General AI construction diagnostics

AIBuildDiagnoseTest observes new and loaded games for all AI player slots,
including custom Lords. Editor and replay sessions are excluded. It records
AIV steps, resource/site searches, routes, construction events, positive-ID spawns,
walls and farm/path-grid evidence. Woodsman and orchard diagnostics remain more
detailed than other building types. No complete automatic cause detector is claimed.
Economy-grid overview snapshots follow the shared native last-search origin;
their logs name the sampled cell and do not attribute that shared state to a Lord.
Per-player origins and explicitly captured candidate tiles remain separate evidence.

The mod never places/deletes buildings, changes search data or suppresses Fixes
settings by itself. Legacy probe settings in its old configuration are ignored.
CanarariWoodTest is an optional companion requiring this mod; its settings belong
to its own new configuration and default to false.

## Ownership and lifetime

The diagnostic owns scheduler, route and general-site observation hooks and the
single BugfixesAndQoL observation sink. APIShared supplies mission lifecycle and
AIV build-step observation. Existing native validation and permanent hook lifetime
remain unchanged. Missing capabilities are logged independently.

The assembly-private companion bridge accepts only CanarariWoodTest_Serp. It
forwards records synchronously on the native caller thread after general capture,
with independent exception handling. Its optional gate and scoped overlay are
inert without companion registration. Only the companion performs mutations.
An overlay restoration callback runs after the single original native call,
including failure paths. Neither mod unhooks at startup cleanup or map changes.

Runtime objects and subscriptions are process-rooted; session state resets for
each mission. GameTimeManagerAPI.OnTick provides post-startup execution. Logs
include session, player and attempt identities; unknown attribution, missing
observations and budget omissions are not evidence of successful construction.

## Verification

The build driver selects APIShared main or development explicitly, checks source
boundaries and native contracts, runs Diagnostics.Tests and builds/installs the
plugin. The tests compile production observation, bridge and companion sources;
native backend probes use private copied code, never the installed process.

Game acceptance remains separate: general-only runs, multiple Lords, new/save
sessions, consecutive maps and opt-in disposable-copy experiments must be checked
in game. Source/build tests do not establish that these game runs occurred.

## Economy, decision and removal records

`AI_ECONOMY_*` records extend the same BepInEx log, without additional output files.
Every event includes observed wall-clock time, session, simulation tick, player and
operation identity. Native capture only queues immutable strings; the persistent
render publisher drains them, with a queue-only flush at real process exit.

PLAYER/AIC/STOCK/BUILDINGS establish the loaded/new state for every AI, then log
changes. AIC records contain the effective native profile index and custom Lord name,
separately from the Lord enum. SETTINGS reports current protection settings and hook
availability, not proof of their historical effect. FIXES_PURCHASE_STATE reads applied
Wood/Iron/Ale override arrays when the installed reflection contract is available.
No defaults or preference pairs are repaired. Unknown native fields retain raw names.

DECISION_INPUT/RESULT distinguish eligibility, the actual resource gate,
purchase selection and recruitment. COSTS reports active costs and
available materials. PRICE_QUERY and PURCHASE_CONDITION are price/affordability
evidence only. TRANSACTION_BEGIN/END reports measured gold/stock before and after.
BALANCE reconciles actual gold change, attributed exclusive operation deltas and the
remaining unattributed difference. Tax Pre events are not booked; the native all-player
settlement's measured net change is booked separately. Nested resource notifications
are evidence, never a second transaction. A unit spawn is not a recruitment purchase.

The installed BugfixesAndQoL (at least the info.json minimum 1.0.192, with the same
9-member observation sink and no economy publisher) remains unchanged. SETTINGS reads
its current hook settings, support flags and published-hook status through its
verified managed contract. It does not expose per-decision protection observations:
original/effective emergency/hovel/accessibility decisions, gate classification and
the hovel-removal context are explicitly unavailable. No competing hook is installed.
DELETE_BEGIN/END tracks the API call; a marked
slot is not removed. REMOVAL_CONFIRMED requires observed zeroing or identity change.
INVENTORY_REMOVAL catches differences between complete snapshots with unknown cause.
Native deletion-call context is separate evidence; no proximity/time heuristic
converts it into universal causal proof. Native building-group IDs remain in identities.

SUMMARY runs every 500 ticks and at session end. Legacy detail gets 56 MiB and the
new categories reserve 8 MiB of the 64-MiB session budget; final bounded accounting
and initialization/error notices remain visible outside normal detail allocation.
BUDGET names omitted categories, COVERAGE names missing hooks and callback failures.
State/repetition tables retain at most 8192 keys and explicitly count further omissions.
Missing Post events are reported and their open contexts abandoned at the next tick,
without booking or reusing those frames. Editor and replay sessions remain inactive.

When SpectatorEditorBuildTest is installed, its existing resource hook is the sole
owner. Version 0.1.2 exposes its own protected private begin/end observer connection;
nativeOriginalEvaluated=false identifies its override and never proves native
eligibility. An old or failed owner reports unavailable coverage. Summary call counts
include zeroes; those require available coverage before interpreting a missing call.

Builds and regression compilation reference the installed Bugfix DLL under
`BepInEx/plugins/SerpsMods_Serp/Mods/BugfixesAndQoL_Serp`, otherwise under
`BepInEx/plugins/BugfixesAndQoL_Serp` (override: `/p:BugfixesDir=...`), never the
developing workspace package. No Bugfix build/install is performed. APIShared remains pinned to main 0.9.2.

## Validation, 2026-10-16

25 regressions pass against the actual retained Bugfix 1.0.192 assembly, including
the existing companion tests, copied native-entry backend tests, exclusive booking,
slot/global identity, session boundaries and protected owner callbacks. These are
source/backend checks, not game acceptance. Shared boundaries, dependencies,
lifecycle, native contracts, permanent hooks and CRLF checks pass.

Both build drivers completed with `/nopause /apishared:main` (main c7b210f00703,
APIShared 0.9.2). Installed DLLs match their build outputs: AIBuildDiagnoseTest 0.2.0
and SpectatorEditorBuildTest 0.1.2. Existing reference/source-link compiler warnings
remain; compilation has no errors. APIShared's installation/checkout are unchanged,
and all 52 files of the retained installed Bugfix package match their protected hashes.
Evidence is under `_inspect/AIEconomyDiagnostic-20261016/`: `build-diagnostic.log`,
`build-spectator.log`, `installed-verification.json` and `installed-bugfix-protected.json`.

Game acceptance is still pending: different standard/custom Lords, new and loaded
games, repeated sessions, tree/market/resource variations, controlled removal cases
and `test_dusty.sav`. No historical cause or successful in-game hook execution is
claimed from these build/backend results. Per-decision Bugfix protection coverage
remains explicitly unavailable with the user-retained old DLL.

## Protection snapshot correction, 0.2.1

The diagnostic reads managed plugin references after Unity startup cleanup, without
using Unity object equality. Missing registration, managed reference, runtime and
protection hook are distinguished. Each unavailable member is isolated so other
settings and hook statuses remain visible; configuration-only fallback is labeled.
Expected absent protection-decision publishing is a Warning with the actual loaded
owner version. Failed capabilities and capture failures remain Errors. This does
not add per-decision protection coverage or prove historical protection effects.
The early Extender dependency warning remains outside this mod's control.

30 regressions passed, including five protection-snapshot/log-severity tests and
the retained native backend and companion tests, against APIShared main 0.9.2.
These are source/backend checks; game acceptance after this correction is pending.
Evidence: _inspect/AIBuildDiagnoseFix-20261016/.

The 0.2.1 build driver completed with /nopause /apishared:main (c7b210f00703,
APIShared 0.9.2), 30 tests passed, 0 build errors. Existing compiler warnings remain.
Installed DLL, PDB and metadata match the build output. All 68 protected installed
APIShared/Bugfix files and the APIShared checkout HEAD/status remain unchanged.

## Release Fixes compatibility: controller observation removed (source only)

The trade-controller detour at 0x3F270 and its exclusive machine contract are removed
from future installation. Installed Release Fixes 1.26.4 has a wheat-sale stack-slot
error in its deeper inline hook; this diagnostic avoids exposing it through a
managed controller call. No register adjustment, replacement hook or dependency on
a development APIShared/Bugfix build is introduced. Existing published hooks are
never undone; this source change takes effect only after a later authorized build
and a fresh game process. The currently installed diagnostic DLL is unchanged.

COVERAGE explicitly reports trade-controller unavailable as an expected compatibility
Warning. SUMMARY omits its call counter entirely. Missing controller records never
mean no trade was attempted. Market-gate decisions, buy/sell alternation and aborted
sale decisions remain unknown. Purchase selection (0x3EC90), actual purchases
(0x29650), actual sales (0x29700), price events and Extender events remain independent
partial observations; they do not restore complete controller coverage.

Only non-compiling static checks are authorized for this change. Regression sources
are prepared but not compiled or executed. The earlier 30-test/build evidence above
describes the preceding installed version, not acceptance of this compatibility edit.
A later build must target the currently installed Release dependencies, never
development DLLs. Game acceptance then requires at least three fresh processes loading
test_dusty.sav beyond tick 70 with the Fixes wheat setting retained, plus repeated
loads in one process. Retain logs/crashes; successful runs do not repair or certify
the unchanged Fixes hook for other callers. No version, build or install changes now.

Static outcome (2026-10-16): native-byte/call-graph, runtime JSON/lifecycle and
existing read-only checks reached the workspace-wide permanent-hook check without
a preceding failure. The full verify.ps1 then stopped on the parallel, out-of-scope
APIShared/src/Compatibility/FixesWheatMemory.cs VirtualProtect declaration. That
file is untouched here. The remaining mod-only economy/owner/compatibility guards
passed separately (compatibility-mod-checks.log); the full failed run is retained
in compatibility-static-verification.log. Diff, CRLF and PowerShell syntax passed.
All 136 installed plugin DLL/JSON hashes checked remained unchanged, including
the diagnostic package. Three new regression methods are prepared, not compiled
or run. Branch develop and version 0.2.1 are unchanged. No build/install occurred.

## Authorized compatibility build/install, 2026-10-16

The source-only restriction above describes the preceding work phase. Following
explicit build authorization, build.bat /nopause /apishared:main completed against
Release APIShared 0.9.2 (c7b210f00703, byte-identical SDK/installed DLL), installed
BugfixesAndQoL 1.0.192 and installed Extender references. All 33 tests and required
checks passed; 0 errors, 47 existing compiler warnings. Version remains 0.2.1.
The installed DLL/PDB/metadata match the package; Cecil inspection confirms no
controller RVA in the compiled installation or contract list. All 134 protected
installed dependency DLL/JSON hashes are unchanged. Detailed evidence is in
build-evidence/. Game acceptance across fresh processes is still pending.
