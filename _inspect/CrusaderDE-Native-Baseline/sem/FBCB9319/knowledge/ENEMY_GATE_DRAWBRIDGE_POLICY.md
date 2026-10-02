# Enemy gate policy: lateral drawbridge access (2026-10-02)

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Installed SE SHA-256: `DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF`.
SE provenance remains commit `f8d51730fcb54b25af43d3c9348d57db058e077f`, tree `657af449e1397c58e6c5ec054977d83198b68e66`.

## Confirmed native contract

- Drawbridge type is 49 (`0x31`). Do not confuse stair handler `E0770` (type `0x45`) or gate handler `D8CE0` with a bridge-specific graph builder.
- Creation `739C0` stores occupied tile IDs in the same iteration order used by the 25-cell mapper. `69850` supplies each footprint coordinate; occupancy index zero remains a missing cell, never an inferred neighboring ID.
- Animation `A5E20` invokes raising `645C0` and lowering `64460`. Raising selects nonzero cells in the orientation/2 mapper at `2D1A30`, and only cells with a nonzero moat-task index (`69560`, tile-manager `+1EA23F0`) receive tile flag `0x40000000` and record state 2 (`725A0`). Lowering clears the flag and restores state 0. Physical state is not modified by this policy.
- The actual installed mapper contains 15 nonzero cells: middle three rows for orientations 0/4, middle three columns for 2/6. It does not close all 25 footprint cells or a surrounding rectangle.
- `725E0` refreshes the footprint through `D90D0` and `D86F0`; the ordinary surface branch tests blocking mask `0x4A5014B1`, which includes `0x40000000`. Raised cells cannot supply ordinary surface transitions, and neighboring transitions into them are rejected. Bridge type `0x31` does not take the specialized gate/stair connection rebuild cases. Region invalidation follows `E3B90`.
- `E2610` can accept equal valid PCLs before the capturer comparisons. A complete late edge mask alone does not prove an early AI planning correction.

Complete feature pseudocode/caller evidence and four mapper fixtures can be reproduced by `_inspect/EnemyGateBuildingContextAudit/bridge-native-audit.py`; retained output is `bridge-native-evidence.txt`. The audited movement and Assassin paths retain their existing contracts and hook owners.

## Observed failure and correction boundary

First `test_gates.sav` epoch 22:17:41–22:18:50: user demolished gate 819 and rebuilt gate 578/global 2433141; new bridge 539/global 2433175 was uniquely footprint-adjacent, with raw gatehouse link 0. Gate portals both belonged to PCL 1. The old center seam did not prevent a side crossing of part of the deck. The epoch counted 10,602 AI builders, zero recorded NoRoute and passed integrity; these counters do not prove which route was planned or used.

The policy now isolates the exact native closure cells and their eight directed incident transitions. It uses ordered occupied IDs and the installed public moat index API, independently of the parent gate passage axis. Cell/record changes contribute to publication identity; animation flags do not. Unknown footprint/orientation/identity retains the open path. Parent gate owner/alliance/capture rules remain authoritative. Both native filters and the optional weighted Assassin snapshot consume the same mask.

Diagnostics retain separate gate/bridge identities and closure-cell marks. `bridge-reachability` definitions explicitly describe PCL candidates, not proof of a selected bridge. `region-pair` separates equal-PCL input from region traversal. `route-after-reachability` connects the later builder result with preceding acceptances in the short-lived order context; it does not assert that one specific earlier query caused the route. No extra search, native hook, public interface or persistent unit tracking was added.

## Remaining game acceptance

Compare the shifted layout with the enemy bridge down and raised. Success requires the early order choice to treat the foreign bridge as closed, with normal Vanilla alternatives. Also test real access beside the bridge, own/captured access and the weighted Assassin option. If only individual routes are rejected after an early same-PCL acceptance, this build is not a complete planning fix; audit the first false acceptance before choosing a separate intervention. Global PCL, gate, bridge and map state remain unchanged.

Build/install completed via the testmod build.bat at 22:46:31 on 2026-10-02: 8,534 policy assertions including installed-RedBird machine execution; 15,494 Assassin A*/Dijkstra regression assertions. Runtime build: zero warnings/errors. JSON/lifecycle, workspace hook-mutation, XAML and CRLF checks passed. Installed testmod matches the local package (SHA-256 8EB5F9B148589AACA348E62F277842AA18633E286AD087290EE41836755FA150). APIShared and BugfixesAndQoL installed hashes remain unchanged. Game acceptance of the lateral bridge and early same-PCL planning remains pending.

## 2026-10-03: independent bridge diagnosis and gatehouse boundary

All bridge edge masks (including the old center seam) have been removed from EnemyGatePathfindingTest. Gate identity/axis linkage is retained. EnemyBridgePathTest 0.1.0 is read-only and independently registered through APIShared; mainmod-owned hooks emit existing results only when an observer is registered. No new native hooks or active bridge policy. The 22:46 experimental mask and its 5,712 NoRoute result are historical, not the current gate policy. Native/SE identities remain confirmed. Pure-gate game acceptance remains pending; see Testmods/EnemyGatePathfindingTest/ACCEPTANCE.md and Testmods/EnemyBridgePathTest/HANDOFF.md. Work commands use existing nested MoveHere and synchronous before/after fields; no task-index or return value is interpreted as proof of work execution.

## 2026-10-03: completed technical verification

All four affected build.bat drivers completed successfully (APIShared, BugfixesAndQoL, EnemyGatePathfindingTest, EnemyBridgePathTest). Local and installed DLL SHA-256 values match. Existing versions and README files are unchanged; the new bridge mod is 0.1.0. Gate: 3,830 policy assertions and real installed-RedBird adapter execution. Bridge: 4,849 geometry/observer/aggregate assertions, including independent registrations, native-call counts, observer failures, nesting, map reset and over-32 state coverage. Mainmod: 15,494 weighted Assassin assertions; 269,682 unit-plan assertions, 6,480 building-distance checks, 18,262 independent search assertions and 1,469,340 cursor comparisons. Full gate/bridge source compilation was checked against installed assemblies before building.

JSON/lifecycle, enduring callback ownership, installed public members/layout, permanent hook mutation, XAML and CRLF checks passed. All builds had zero errors; both testmod runtime builds had zero warnings. BugfixesAndQoL retained an MSB3277 Mono.Cecil reference-version warning; test dependencies also report framework/reference warnings. No new runtime JSON dependency was introduced.

Final review corrected occupied-array capacity to 36 cells and records ordered raw footprint values. Bridge contexts distinguish suppressed events (SE has no Post in that case) from unsuppressed missing Post; retained mutable Pre arguments and original-input Post arguments are identified separately. Nested work movement is observed evidence, not a direct E7F60 return or proof of execution. Assassin diagnostic failures are counted without changing results. No extra native hooks, active bridge masks or persistent unit monitoring were added. Pure-gate in-game regression remains required before integration.