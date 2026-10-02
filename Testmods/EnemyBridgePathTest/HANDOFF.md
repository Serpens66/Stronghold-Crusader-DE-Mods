# EnemyBridgePathTest — handoff (2026-10-02)

## Goal and current boundary

Make an enemy drawbridge look physically raised to AI planning even when a lateral deck crossing does not pass through its gatehouse. First find the earliest different Vanilla decision. This build is **read-only**: its closure mask is hypothetical and is never registered as the gate policy. No new native hooks. It co-loads with EnemyGatePathfindingTest and works with APIShared/BugfixesAndQoL without that testmod. Later gate integration must preserve the independent observer contract.

## Reproduction and historical logs

Use `test_gates.sav`. Demolish/rebuild a gate next to moat so the lowered deck can be crossed sideways around the gatehouse. Finish the layout before the attack begins. Compare bridge down versus physically raised in separate save epochs; record unit behavior as well as logs.

Historical first shifted example: gate 578 / bridge 539, portals PCL 1 on both sides. Gate/bridge association was present; the old center seam missed lateral crossing. A later experimental 15-cell active mask was built at 22:46:31. Epoch 22:50:55.459–22:53:11.085 built gate 834/global 2433338 at 22:51:19 and bridge 839/global 2433362 at 22:51:21. It counted 24,098 AI searches and 5,712 NoRoute; 4,805 command-3 MACEMAN failures. Failures continued after publication. Integrity passed. The user saw initial approaches, then no further approaches and no moat filling. These are historical experimental-mask results, not results of this diagnostic build.

## Confirmed native contract

Native SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; SE SHA-256 `DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF`. Source commit `f8d51730fcb54b25af43d3c9348d57db058e077f`, tree `657af449e1397c58e6c5ec054977d83198b68e66`. Installed RedBird 1.5.0.0. Verify identities again before changing native behavior.

- Creation `0x739C0` stores occupied IDs in mapper order; `0x69850` produces coordinates. Game IDs are 1-based; spans are 0-based.
- Animation `0xA5E20` calls raise `0x645C0` / lower `0x64460`. Mapper `0x2D1A30 + orientation/2 * 100` contains 25 Int32 entries; 15 are nonzero. Orientations 0/4 select middle three rows, 2/6 middle three columns.
- Raising requires a nonzero moat-record index (`0x69560`, tile manager `+0x1EA23F0`); selected cells receive `0x40000000` and record state 2 through `0x725A0`. Lowering removes the flag and restores state 0. `0x725E0 → 0xD90D0 → 0xD86F0` updates surface edges; `0xE3B90` invalidates regions. Blocking mask `0x4A5014B1` includes that flag.
- Drawbridge type is `eStructs.STRUCT_DRAWBRIDGE`, 49. `0xE0770` is stairs (type 0x45), not a bridge graph builder.
- `0xE2610` accepts equal PCLs before gate comparisons. Ordinary group `0x11B520` can skip that call entirely for equal PCLs. Assassin-only groups also bypass ordinary region checks (`0x117820`). A missing observed region call is not an observed E2610 acceptance.
- `0x11E960`, command 7 (`TribeAICommand.Unknown7`), uses `0xE7F60` when leader PCL is nonzero. Zero selector result returns early; a positive result selects work-access coordinates and falls through command 6. Both enter nested `0x11B520` and write the issued tribe command/context coordinates for eligible units. The event captures this path without a new callsite hook. Leader PCL zero skips the selector. Missing/invalid leader context remains unclassified.
- Installed public unit fields: issued command `+0x398`, context X/Y `+0x3E4/+0x3E6`, moat task index `+0x3B4`. Task presence is not proof of a new assignment or of executed work.

Full retained feature evidence: `_inspect/EnemyGateBuildingContextAudit/bridge-native-evidence.txt`; baseline `knowledge/ENEMY_GATE_DRAWBRIDGE_POLICY.md`. Gate adapters and mainmod hooks have their existing owners; no hook address changes in this split.

## Code entry points

- `src/BridgeSnapshot.cs`: deferred building/tile snapshot, parent gate identity/link source, role labels, raw flags/PCLs and hypothetical incident edges.
- `src/DrawbridgeClosurePolicy.cs`, `GateEdgeOwnership.cs`: migrated pure experiment; never published as active policy.
- `src/BridgeDiagnostics.cs`: short-lived Pre/Post command contexts, nested work access, synchronous before/after unit fields, existing search outcomes and Assassin route inspection.
- `APIShared/src/EnemyBridgeDiagnosticBridge.cs`: independent passive registration, exception-isolated paired search and region forwarding.
- Mainmod `MovementPathPublication.cs`, `MovementSearchContext.cs`, `FriendlyMoatMovementRuntime.cs`, `AssassinPathfindingRuntime.cs`: reports from existing detours. No observer means no added context, state capture or route inspection.
- `verify.ps1`, `tests/Program.cs`, `tests/DrawbridgeClosureTests.cs`, `build.bat`.

## Reading the new diagnosis

Ten-second active aggregates retain exact event counts, first/last concrete values and target changes. State definitions are output before references; no 32-event/pattern limit. `gate=` in the shared aggregate denotes the bridge ID in this mod, not a selected gate. Bridge/PCL states are candidates or snapshot facts, not proof of a chosen bridge. Unknown identity, association and player roles are explicit.

`same-pcl-with-no-region-call` separates the group bypass case from `region-query-executed`. It describes observed calls in the whole short-lived context, not a disassembly trace of an individual branch. `work-access` records the nested selected-access movement; `work-unit-fields` compares command and context, preserving task values separately. Actual work remains a game observation.

## Remaining gaps and next comparison

Region/builder/Assassin hooks can be absent depending on mainmod feature installation. No callback is reported as missing coverage, never as a negative search. The final physical bridge animation and the earliest AI planner acceptance still need paired evidence. A context with no region callback can also take an Assassin/other branch; do not infer a precise branch from absence alone. Snapshot reads are deferred, not an atomic view of all simulation memory. Unexpected identity/shape must remain unknown.

1. Bridge below/raised with already finished layout, same save and attack setup.
2. Compare the first group decision, executed region calls, later route and work-access entry.
3. Normal soldiers and Assassins with improvement on/off; genuine side access and own/captured bridge.
4. Only then select a player-/bridge-specific intervention at the first false acceptance. Do not block whole PCLs, change global tile flags or synthesize alternative orders.

Event coverage: Script Extender emits no Post after SkipOriginalFunction. Retained Pre args are checked for suppression before nested observations and at deferred cleanup; unsuppressed missing Post is an error. Event subscribers can alter arguments or issue nested replacements. Therefore work-access is an observed nested movement, never direct proof of E7F60 success.

## 2026-10-03: completed technical verification

All four affected build.bat drivers completed successfully (APIShared, BugfixesAndQoL, EnemyGatePathfindingTest, EnemyBridgePathTest). Local and installed DLL SHA-256 values match. Existing versions and README files are unchanged; the new bridge mod is 0.1.0. Gate: 3,830 policy assertions and real installed-RedBird adapter execution. Bridge: 4,849 geometry/observer/aggregate assertions, including independent registrations, native-call counts, observer failures, nesting, map reset and over-32 state coverage. Mainmod: 15,494 weighted Assassin assertions; 269,682 unit-plan assertions, 6,480 building-distance checks, 18,262 independent search assertions and 1,469,340 cursor comparisons. Full gate/bridge source compilation was checked against installed assemblies before building.

JSON/lifecycle, enduring callback ownership, installed public members/layout, permanent hook mutation, XAML and CRLF checks passed. All builds had zero errors; both testmod runtime builds had zero warnings. BugfixesAndQoL retained an MSB3277 Mono.Cecil reference-version warning; test dependencies also report framework/reference warnings. No new runtime JSON dependency was introduced.

Final review corrected occupied-array capacity to 36 cells and records ordered raw footprint values. Bridge contexts distinguish suppressed events (SE has no Post in that case) from unsuppressed missing Post; retained mutable Pre arguments and original-input Post arguments are identified separately. Nested work movement is observed evidence, not a direct E7F60 return or proof of execution. Assassin diagnostic failures are counted without changing results. No extra native hooks, active bridge masks or persistent unit monitoring were added. Pure-gate in-game regression remains required before integration.