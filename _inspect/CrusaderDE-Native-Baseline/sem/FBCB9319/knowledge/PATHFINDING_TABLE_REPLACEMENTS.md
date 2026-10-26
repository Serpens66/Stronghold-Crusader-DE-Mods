# Opt-in pathfinding table replacement audit, 2026-10-14 [Scope: Mod work, not Vanilla]

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Recomputed from installed CrusaderDE.dll before implementation; it matches
CURRENT.json and the feature evidence. Installed SE: 2.14.1.0; canonical source
commit 5908e1f12437deb7ef5f5b1dc2d64e301178fba3, tree
c238441ea12db35ee36c5c5668d3c8fce0897295.

## Confirmed native and SE contracts

Full direct profile-reader bodies and disassembly remain in
`_inspect/EnemyGateBuildingContextAudit/pathfinding-globals-evidence.txt`:
0x18E1E0, 0x196280 and 0x198ED0 read Int32 profiles from 0x322540 using the
unit's signed 16-bit type. Movement readers branch on profile zero for surface
eligibility/search; their failure paths retain normal failure/state clearing.
The third reader branches on profile zero during an active unit transition.
Profile storage capacity is 90 Int32s.

Permission reader 0x181E00 has no callees or branches. It reads signed type at
manager + gameId*0x490 + 0x6E6, then returns the Int32 at class-zero base
0x32BC48 indexed by class*0x5A + type. Classes 1..6 begin at 0x32BDB0 and
occupy 540 Int32s, with 0x168-byte rows. Symbol names in the semantic database
remain candidate-level; these machine address calculations are confirmed.

Independent installed-file PE reads confirm class 2 / archer (type 22): native
RVA 0x32BF70 contains 1; SE's stride 89 selects 0x32BF6C, containing 0.
Installed SE decompilation and canonical source agree on 89/534 span lengths
and erroneous (class-1)*89 row addressing, inherited by Can/Set. Enum values
0..88 remain valid; storage slot 89 is not a newly valid unit type.

## Additive correction and validation

APIShared/src/ScriptExtenderReplacements contains the opt-in replacement and
its standalone documentation. It validates hash, complete in-module ranges,
loaded enum metadata and public SE-resolved addresses before publishing immutable
process-lifetime state. Initialization neither compares mutable canonical values
nor writes tables. It installs no hooks and changes no existing SE callers.
BugfixesAndQoL's startup comparison consumes these complete spans and retains
the canonical-value comparison plus explicit unavailable-service diagnosis.

The canonical Fixes source's shield option uses class 1, whose row origin was
already correct. Fixes and SE sources remain unchanged; foreign live values
are preserved. No competing hook or private game member is used.

The first canonical APIShared build passed 305 tests: 164 Core, 120 runtime
and 21 preset tests. Seven new tests cover the real replacement against owned
unmanaged buffers, all 534 valid class/type pairs, neighboring writes, all storage
slots, foreign live values, invalid arguments, unavailable states and public
SE signature parity. The migration example compiled. Test-only constant
assertions were subsequently improved to inspect loaded metadata. Final build
and consumer evidence is retained under `.inspect/PathfindingReplacement/`.

The final APIShared driver again passed all 305 tests, compiled the public
example and installed successfully. BugfixesAndQoL's driver also completed
successfully, including 157367 integrated gate-routing assertions. Installed
DLL hashes match the prepared packages. A fresh Steam launch at 14:48 verified
the real SE_PATHFINDING_REPLACEMENT availability marker with profiles=90,
permissions=540, rowStride=90 and validUnitTypes=0..88; captured evidence is in
`.inspect/PathfindingReplacement/startup-evidence.txt`.

The existing Workshop updater then staged APIShared and SerpsMods and restarted
the game. It replaced the installed development APIShared DLL with published
SHA-256 8AC07D530411F93DDB443FD15C45708060C0CE31F855FAD0CA899E5A028E59DB;
the tested local development package remains SHA-256
23289CE8531A88B3F735D4D49C71E82D911EEAE9CA507B8FA33C662044110238.
Do not attribute the subsequent session to the replacement. Reinstallation and
the existing Workshop update flow must be accounted for before map acceptance.

This does not prove in-game path-search outcomes. The user explicitly deferred
map acceptance. A consumer comparison with profileLength=90/90 and
permissionLength=540/540 after startup cleanup remains pending.

## Follow-up code review, 2026-10-14

Native hash/provenance and the complete feature audit remain unchanged. Added
initialization-only validation of loaded enum underlying widths and all six
connection-class values. A new metadata regression rejects changed widths,
changed intermediate class IDs, profile values and unit count without native
access. All eight replacement tests passed; the full run passed 167 Core,
121 runtime and 21 preset tests (309 total). No further current-layout address
or bounds defect was found. The active consumer remains migrated; legacy gate
testmod code is inactive and must not be loaded with the integrated feature.
The bridge diagnostic's correct 90/540 copied snapshots are not live-view
consumers and retain their independent snapshot contract.

The final input-consistency check then rejected packaging because parallel
Workshop sources changed during the build. The user confirmed that work is
still active; further builds/installation are deferred. This review therefore
does not attest a newly installed or game-tested build. Results are retained
in `.inspect/PathfindingReplacement/review-results.txt`. The manual acceptance
guide now specifies declining the SE staged-update/restart dialog, enabling
DetailedDiagnostics and checking both initialization and complete comparison.

## Build continuation after parallel work completed

On 2026-10-14 the current APIShared source/package proof passed and the installed
DLL matched the local package (SHA-256
13699714D6F426E312622DF3DB4493BDC6F15C6B085C2466050383D3C7138A47).
The existing APIShared build was therefore reused. BugfixesAndQoL's canonical
build driver then completed tests, build and installation successfully, including
157367 integrated gate-routing assertions. Local and installed DLL hashes were
verified equal for both mods; the legacy standalone gate test remains inactive.
Evidence is in `.inspect/PathfindingReplacement/review-results.txt` and
`review-consumer-build.log` beside it. No game was started; manual map acceptance
is still pending. Versions remain unchanged during testing.

## Live map acceptance, 2026-10-14

The user confirmed a running map. The latest BepInEx startup section contains
the replacement availability marker at 15:33:13.200 and the integrated consumer
comparison at 15:33:42.868, after chainloader startup cleanup. Gate routing
reported hostFix=True and DetailedDiagnostics=True. Coverage is 90/90 profiles
and 540/540 permissions, both mismatch counters and invalidValues are zero.
Actual and expected fingerprints are both 0xC63EAAE3BB01A032; profiles Large/Default
are 13/77 and class allowed totals are 4/16/89/89/89/83. The old coverage warning,
unavailable warning, access/type/method exceptions and APIShared/QoL Error/Fatal
lines are absent in this session. Live initialization and the migrated consumer
read path are accepted. This does not add a new claim about gameplay outcomes
or live setter exercise; exact writes remain covered by isolated tests. Evidence:
`.inspect/PathfindingReplacement/map-acceptance.txt`. No code/build/version change
was made during the running game.
