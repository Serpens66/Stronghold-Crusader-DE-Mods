# Fixes 1.24 compatibility and evidence policy

Audit date: 2026-10-06. External source: clean v1.24.0, commit fcf32589586e874f9ea56a885c48ef33d8b10a08. Installed Fixes was 1.23 during the initial analysis; implementation preflight now observes 1.24.0.0. Installed SHCDESE is 2.13.0.0; canonical fork commit 85ab962b342c18f663da830570884a25b85116d0. Native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2; actual managed Assembly-CSharp SHA-256 BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789. This note is feature-scoped and does not certify unrelated baseline records or replace their provenance.

## User decision: retain integration notes, stop evidence work

On 2026-10-06 the user cancelled further evidence for the popularity activation dependency and unreachable siege direction logic. Both testmods, their shared observer and their offline project have been removed. Historical source probes and build logs remain analysis artifacts. Do not recreate the tests or prepare author reports for these findings without a new request. Neither finding is classified as a demonstrated gamebreaking bug. This decision supersedes the former test/evidence workflow below.

For future implementations: explicitly supply the Bad Thing attempt count when our own generated settings request a popularity threshold (100 retains Vanilla frequency); do not silently change foreign settings. The limit alone is ineffective in the audited implementation. The requirement is not stated in the README, which allows selecting only desired keys. Popularity5000/9000 means displayed50/90, not out-of-range input. For siege integration, only the extra Fixes5x5 gap check is operational; do not rely on the unreachable direction block to prevent forward placement or guarantee engineer access. Preserve the distinction between code behavior and unproven gameplay consequences. Recheck these implementation contracts after Fixes updates.


## Confirmed compatibility changes in our source

- ExtendedData preference conversion reflects all public readable/writable properties. Missing current properties keep constructor/property initializers, recursively including initialized nested objects. Unknown properties, unsupported types and lossy conversions fail with a diagnostic. Supplied values are compared structurally, with exact decimal-text normalization for JSON numbers; arbitrary representable modder values are not clamped. Capture retains a complete round-trip check. Original save/snapshot and package checksums are verified before new canonical payloads are published.
- Snapshot protocol 3 and Trail requirements 2 carry nullable LordType. Custom type is -1; Extended types are configuration indexes. Legacy formats remain readable; missing identities are resolved only through a unique configuration name/checksum match. Runtime ambiguity fails closed instead of guessing from an alias.
- Fixes storage adapter validates actual reflection contracts: public Plugin.CustomLordPreferences dictionary keyed by case-insensitive Custom name; private static Preferences.ExtendedLords dictionary keyed by configuration LordType+1. Original objects and absence are restored to the correct store. Map `_slots[9]` are observed but never written; their precedence remains authoritative. Verification explicitly describes lord defaults and separately reports map override presence.
- Gatehouse snapshot verification keeps the pristine full handler hash and full human-block bytes. Live checks and instruction invariants only own B7C32..B7C38, before Fixes Farmer hook B7C39. The permanent decision hook and its complete displacement validation remain intact.
- AIAttackTest has a Fixes soft dependency and independent recruit/lord hook transactions. An enabled or unknown Fixes defensive-recruitment owner skips only the recruit capability. Unpublished failures roll back only their candidate; published hooks are never undone, and settings use data flags. The ordinary AIC tests remain independent.

## External findings: proof level matters

### Popularity preference without attempts

`FixesMapEvents` activates the bad-thing override only if CustomBuildAttemptsRequiredForBadThings is supplied. Min popularity 9000 alone therefore leaves the override disabled/default minimum 5000. The exact source handler was exercised in memory: minimum-only gave disabled/5000; adding attempts100 gave enabled/9000. Native 0x41280 has a reachable AIV-building caller path through 0x51790/0x52270 and gates bad mappers176/177/301..311 on popularity5000 and attempt count100. Static logic and handler behavior are proven; natural in-game building effect/control are not yet recorded here.

Realistic integration: our preset/merge/export code independently changes only the documented minimum; a naturally building AI may then construct Bad Things below the requested limit. Preserve independently supplied values, log this known coupling and ensure our own generated presets include the intended complete pair. Do not silently rewrite foreign preferences. Former diagnostic design compared A minimum9000 vs B minimum9000/attempts100 with identical AIC/AIV; cancelled by user.

### Siege direction guard

The unchanged callback returns1 when IsPlayerIdValid(playerId) is true; the later target guard also returns1 for a valid target. Therefore valid normal players cannot reach the new directional restriction. The working 5x5 structure-gap check still runs first. This exact callback was exercised with API fixtures. No engineer/path gameplay failure has yet been proven.

Realistic integration: new siege staging/extra machine functions in our mods assume this guard prevents forward tents blocking earlier crews, or use a geometry that passes Vanilla and the working gap check but depends on direction. That protection must not be assumed. Only actual surviving engineers becoming unable to complete because of that placement, with a matched natural control, merits an author report.

### Dormant PCL hook risks: withdrawn as active-game reports

At 0x10DD50 the installed inline backend overwrites through 0x10DD61; original branches target interior0x10DD5A. At 0x11488A it overwrites through0x114898; branches target interior0x114895. The latter emitted buffer-address setup also replaces the live RAX neighbor-array cursor; the small-component continuation can advance the wrong buffer pointer. Full relevant functions and incoming branches were audited against the installed RedBird implementation (minimum14, instruction-rounded displacement).

A bounded full .text reference scan plus data/unwind inspection found no demonstrated active caller path. 0x10DCB0 is reached through0x114680/0x40180, but no live entry to0x40180 was established; references to0x114760 in .rdata are unwind metadata, not a dispatcher. Static defects are conditional risks, not proven current gameplay bugs.

Realistic integration: an additional escape/unstuck feature calls those dormant routines through new RVA/pattern adapters, or a future native build makes them reachable. Relevant out-of-bounds/blocked-neighbor paths can then jump into overwritten bytes; a small-component search can advance the wrong cursor. Do not invoke them merely to manufacture author evidence. Audit the complete active caller chain before any adapter.

### Incomplete purchase pairs

BuyWoodAt without BuyWoodAmount can throw during map preferences, after setting part of the override; later player settings are then skipped. The R3 subscriber contains the error, so this is not proof of a game crash or complete map-load failure. With the corresponding feature array disabled, the null-conditional write skips the RHS and no exception occurs. Exact-source handler probes reproduced this distinction.

Realistic integration: our preset composer merges a threshold but omits its amount or deletes only one property. Our generated purchase configurations must contain complete pairs. Foreign values remain unchanged; issue a baseline/integration diagnostic rather than an author report for deliberately incomplete input.

## Native siege/search contract and remaining uncertainty

- 0x2B080 processes up to eight configured machines in one call; machine IDs39/40/58/59/60/77 map to crew2/3/4/4/1/2 and mappers190/191/192/193/194/358 through tables2C80B0/2C80C8/2C8098. It detaches the total required crew from its siege-engineer tribe first, then calls0x2C1B0 per machine.
- 0x2C1B0 scans unit game IDs1..<native total>, requiring alive2, native ushort owner==player, unit type30, low word at GameUnit+29C==0, role+426==10, tribe+2D4==0, AIstate+2BC!=8. GameUnit record size490; manager sentinel precedes the API's 0-based span. These layout offsets are checked before observer registration. A tick may miss the detachment/selection interval; absence of six simultaneous eligible samples is inconclusive.
- 0x1134D0 takes the chosen tribe leader coordinates into0xEA760 at maximum depth100, then200 if zero. It passes filter60 and tribe owner. Final checks use center minus1 in each coordinate, active-coordinate grid and origin logic mask4A5014B1 before0x6D580; later0x11E960 links the created tent.
- 0xEA760 uses FIFO/visited traversal, fixed direction masks and packed per-row neighbor deltas; the Y table at2D2E54 uses eight-byte stride (EA9A3/EAAA0), giving [-1,-1,0,1,1,1,0,-1]; starts at depth1 and only tests sites after current depth>4. The native filter reads signed Int16 **player tile metric**, selected via2EA70DC+player*177BC, at5759230+selector*320800*2. It is not PathConnectionGrid/PCL. Eight footprint neighbors exclude the center; reject logic mask4A7014B1 unless the tree helper allows its bit1000, reject bit4, structures and units, and require height range<12. Fixes adds its callback at that footprint check. After acceptance the same traversal enqueues unvisited neighbors. No RNG occurs in this audited search path.
- 0x107160 reads the tile organism index, then signed native vegetation state at manager+id*9C+6A. It allows state>4 except15. Record header1A and GameVegetation.AliveState offset50 explain that address. Observer capture preserves the predicate result rather than calling the routine.

Function names/role associations remain candidate-level where the semantic database has not confirmed symbols; byte-level control/data flows are checked against the canonical hash. Runtime snapshots capture actual tables and pre-build state. Search validation still needs the actual selected leader and proof the post-search capture equals the relevant pre-search state. A model result alone cannot prove accessibility, mounting success or causal attribution. No bit-exact save replay is promised.

## Non-reportable inputs and evidence gate

The author intentionally accepts modder-supplied values. No author report is generated solely for absent plausibility checks, unusual limits or incomplete input. Our own generated fixtures use ordinary representable values. Existing compatibility tests cover new properties, legacy envelopes, aliases, type identity, original restoration and map override preservation.

The removed observers were static/publisher rooted and passive; they are no longer installed or maintained. Author evidence requires unchanged Fixes1.24, an isolated mod set, fixture/save/settings/log provenance, concrete natural gameplay effect and a matched control. Missing conditions mean INCONCLUSIVE; absence alone does not mean NOT_REPRODUCED. At most three targeted runs precede reassessment; no candidate or failed model validation stops map experiments. No gameplay-confirmed external report currently exists in this implementation.

## Documentation contract checked on 2026-10-06

The canonical v1.24 README lines543-551 describes the three Good/Bad Thing properties individually and their Vanilla values, without stating that the Bad Thing attempt setting is required to activate the popularity setting. Line637 explicitly instructs modders to include only keys they want to override. The all-properties example supplies both, but is explicitly an example containing every key, not a minimal required-pair schema. Both properties are nullable and separately default-commented. Implementation requires attempts.HasValue; minimum alone is silently ignored. This is an undocumented activation dependency, not evidence that the author explicitly specified a required pair. Whether the author intends that dependency remains unresolved.

Siege evidence distinction: valid-player early return conclusively prevents execution of the directional block, independently of map geometry. This proves dead directional logic when the callback runs; it does not prove a tent is accepted that strands engineers. The README's siege-gap description promises larger gaps to prevent pathing problems, but does not separately document the directional rule. A narrow code report can describe unreachable logic without claiming a reproduced engineer/pathfinding defect. Under the user's gameplay-proof criterion, the latter still requires natural game evidence; the passive testmod is tooling, not logically necessary to establish the early-return defect itself.
