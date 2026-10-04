# Virtual closed drawbridge decision design

Status: design only; no behavior policy is published by this milestone.

## Proven decision and route

Frozen source: `_inspect/EnemyGateBuildingContextAudit/bridge-232200.log`, SHA256
`5AEF0B8E216C0E42E2E5AE63EB26A34AEC440E7762DFE221E7F0CC37CDCAF9D8`.
Player8 decision28 (root1303271) consumes CF360 mode0, keep components103/1,
native=effective103, return1, phase5->6. Later root1336380 retains the exact
completed plan; four group commands dispatch25 unit commands. Their52 logged
route observations reference this completed state. Unit1143/g2425970,
command1336382/group1336381, stores path523 from546/494 to588/384; steps64..66
cross bridge703/g2432893 at603/477->605/473 without its candidate parent footprint.
Group start/target components are1/1 and no E2610 call occurs. After raising,
decision31 consumes modes0/1 with107/31, native=effective0 and phase6->5.
This establishes retained-state consumption, not a counterfactual causal test or
executed movement. The session contains no observed deck position transition.

## Representation choice

Use an immutable, player-specific **virtual split-component graph**, built from
copied tile topology. Do not remove only a macro connection: the deck merges
terrain inside native component1, so the existing macro graph cannot express
its removal. Do not change live flags, PCL, edge masks, component counters,
visitation tables, planner grids or stored unit paths.

The checked E49D0 body floods the packed tile space using all eight native edge
bits and row-dependent packed neighbor offsets. It temporarily closes gate
passages during component construction, then restores them and reconstructs
connection endpoints. Its seed predicate includes surface mask0x4A5014B1 and
the special107160 case. Therefore a generic rectangular800x800 flood or an
unqualified flood over every tile is not a faithful replacement.

Required input publication:

- Actual packed coordinate conversion/bounds, PCL grid, edge-mask grid, surface
  seed eligibility and completed topology revision. Capacity is320800 packed
  elements; coordinate keys used by route diagnostics are not packed tile IDs.
- Exact15-cell deck geometry from2D1A30+orientation/2*100, Building-ID/Global-ID,
  alive state, ownership/capture/alliance policy, and validated gate association.
- Macro records with type, active/closed state and all endpoint tiles/components;
  every endpoint must be remapped to its virtual subcomponent. Preserve E2610's
  mode0/1 eligibility, third endpoint and ownership/capture behavior.
- Existing Gate route-policy snapshot, with player and IsCurrent checked before
  use. Snapshot invalidation must invalidate the virtual graph too. Do not claim
  the existing single-provider registration from the Bridge diagnostic mod.

Within each affected native component, derive virtual subcomponents using the
audited flood rules with enemy deck cells excluded and directed transitions into
and out of those cells removed. Preserve unaffected components and allowed
macro transitions. Resolve a reachability query by actual endpoint **tiles**,
not just their old component numbers. An alternate unblocked route preserves
acceptance even if a previously stored path used the bridge.

Policy must use Gate's IsUnrelatedGateCombination contract: owner/owner ally and
eligible capturer/capturer ally remain permitted; uncaptured unrelated owners
are enemies. NativeGateAccessSnapshot's PreserveUncaptured is a separate native
capturer-hook case, not permission to traverse an enemy bridge. Parent ID0 is
not a valid native association. A unique adjacent same-owner footprint remains
a hypothesis; ambiguous or unvalidated authorization yields Unknown, preserving
Vanilla. Standalone bridges are outside this feature until their association is
established.

## Decisions, ownership and backend

CF360/CF400 are suitable **early boolean consumers** for keep-to-keep access:
they accept equal nonzero PCL without E2610, otherwise query mode0/1. They have
no other writes. Preserve the inactive-player early acceptance and native
rejection; a positive result can be rejected only after a complete, current
virtual query proves NoRoute. Unknown and Reachable preserve the original.
The existing BridgeNativeHooks owns both entries and calls each original once.
Any later experimental policy must compose inside that owner, not add a detour.

This alone does not cover all retained phase6 orders or tactical target choice.
3B450/3C150 consume retained player state;11B520 bypasses region queries for
equal PCL and uses117C70's mode otherwise. Early target/formation selection must
consult actual start/target tiles before command acceptance, through the existing
mainmod/Gate owners. A new path-builder failure after choosing a target is not
the requested fix. No late-only filter is proposed as sufficient.

Current native SHA256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The renewed122-function audit and27-function stored-path audit cover the complete
physical/planning/consumer paths. Existing32 NativeX64 Absolute spans and incoming
edges are checked by bridge-decision-audit.py and NativeDecisionTests. CF360/400
use L3(pointer,int,int),14 displaced bytes
`4883EC384C63D24D69CA3C580000`, continuation entry+14. NativeX64 backend SHA256
`0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0`.
No Context-hook/flag contract is substituted. SE owns E2610/Assassin196870;
Gate/mainmod own relevant movement/search/tactical consumers. Fixes'2C5E1 internal
component-count patch is disjoint from the existing2C5A0 entry. Relocated component
capacities must come from the installed API, not stock hardcoded table sizes.

## Publication, cost and unresolved prerequisites

Cache keys include session, completed native topology revision, bridge identities,
geometry and physical state, Gate policy publication, ownership/capture/alliance
generation, query mode and endpoint tiles. Spawn/delete/ID reuse, physical bridge
changes, terrain changes, map reload and policy changes invalidate dependent data.
Until a rebuild and coherent input publication complete, return Unknown. Never
recompute the map during every native access check. Preallocate graph storage;
publish immutable completed generations, and abandon incomplete/changed input.
No additional Vanilla search is permitted; future managed graph work must be
budgeted separately and measured before activation.

Blocking prerequisites for an experimental fix:

1. Save-identical raised->lowered->raised run with Gate/Fixes and no own moat work,
   fresh access decisions and bounded delivery complete in each session.
2. Verify that deck exclusion reproduces closed-state boundary transitions and
   seed eligibility, including diagonals, elevated terrain and107160 exceptions.
3. Validate macro endpoint remapping, both modes, alternative routes and special
   unit permissions against Vanilla; preserve authoritative positive special
   permissions supplied by SE. A tile graph is not automatically a unit profile.
4. Resolve the parent authorization for bridge703 (native parent0) independently
   of the geometric candidate, and establish the owner-compatible integration
   contract for early target/formation decisions. No unreviewed new hook address
   or public interface is specified by this design.

This milestone keeps diagnosis passive. Tests must distinguish Reachable,
NoRoute and Unknown; incomplete data must never become a negative decision.
