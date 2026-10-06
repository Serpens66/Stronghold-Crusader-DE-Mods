# Drawbridge decision fix: integration contract, not activated

Native SHA256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

The captured keep op670225 and group op828296 now have a production no-cut witness
and no route after removing only703. This does not authorize a CF-only patch.

| Decision boundary | Actual inputs / output and side effects | Existing owner / required continuation |
| --- | --- | --- |
| CF360 / CF400 | Player-root pointer, attacker and target player; native Int64 boolean. Inactive keep branches return1. Nonzero equal PCL bypasses E2610. Otherwise native query is target -> attacker, mode0/1. | Bridge permanent L3 wrappers. Run original exactly once. Preserve inactive/zero/Unknown branches. A later authorized NoRoute can narrow a positive result before its caller consumes it. No new E2610 detour. |
| 2D250 -> D95E0 | Attack player selects stored target; CF360 supplies expansion boolean. Seed arguments are TARGET player, access boolean, extra observed argument1; target player is not requester. D95E0 temporarily closes/restores gates and mutates seed/queue grids. | Bridge permanent V3 / V4 wrappers. Keep attacker in the parent context; preserve native close/restore and all scratch side effects. A valid early fix must rebuild the seed flood on copied virtual transitions and reproduce its output contract, not just remove final target cells. |
| D9190 / 2C5A0 -> 2C480 | Distance builder consumes seeds, count/limit/region and actual attack-player output bank. Target-region globals and weights influence candidate formation. | Bridge permanent V5 / V3 / V2 wrappers. Preserve sentinel/cost/tie-break and candidate/reservation semantics. Reachable alternatives must retain valid candidate ranking. Copied snapshots currently do not include the whole decision-time distance/weight contract. |
| Phase6 -> 3B450 -> 11B520 | Stored attacker/target decision state supplies tribe and formation target. Leader Unit-ID is1-based. Equal start/target PCL bypasses region search; command then changes tribe/unit states, formation and plans. | 11B520 is the Script Extender tribe MoveHere detour/event publisher; no competing function hook. Pre event has SkipOriginalFunction and ReturnValue, Post follows original. A command-only veto is insufficient: planning must already exclude the invalid formation choice. Preserve callers' side effects and return contract. |
| Per-unit builders / next-tile checks | Unit-specific profiles, registered macro access and effective Gate masks remain authoritative. Existing stored paths may predate a new decision. | Existing Gate/SE search owners and event observation only. Do not install a second builder hook or block movement of a previously accepted path as the primary fix. |

Implementation prerequisite: prepare immutable per-player virtual topology and
endpoint mappings after completed rebuild. Cache by full content, identity, roles,
Gate policy and mode; same native PCL never substitutes for tile reachability.
Return Reachable for a fully known witness, NoRoute only for exhausted complete
admissible topology, otherwise Unknown and preserve Vanilla. No search per attack.
A bounded synchronous lookup may use previously completed cached answers; a queued
answer cannot retrospectively authorize rewriting a caller's decision.

For early seed/distance integration, a Boolean oracle alone cannot reproduce
Vanilla planning weights. Complete and verify the copied seed/distance output
contract before activating the already owned wrappers. This is an explicitly
unmet implementation prerequisite, not an invented approved hook point. Matching
CF results and the saved paths do not prove all candidate ranking semantics.

Coupling and permissions: use native spatial selection, not r_GatehouseId or AIV
frame order. Owner/ally/rightful-capturer behavior follows the Gate policy. Retain
alternative terrain and macro routes and first-pass/structure-required distinction.
Unsupported permissions, geometry, stale inputs or budget exhaustion remain Unknown.

Regression acceptance before activation: both original immutable fixtures; an
alternative route surviving only703; class3/4 C and unknown classes; equal native
regions; both directed query orders and modes; capture/role/identity/map changes;
actual original-once forwarding and candidate/formation side effects. Physical
raised-state equivalence and remaining Gate unit-table coverage are separate gates.


## Shared drawbridge coupling (2026-10-07)

At the user's explicit request, APIShared now exposes GatehouseDrawbridgeCoupling and GatehouseFootprintCandidate. BuildOrderedFootprintCandidates reproduces B9330 order; CollectFirstDistinctBuildingIds selects at most two different positive Game-IDs using the caller's live drawbridge predicate. Callers validate footprint/map bounds and identities. No ownership, access permission, parent ID from r_GatehouseId, cache or hook is inferred by this pure API. BugfixesAndQoL's existing approach policy delegates to this shared core; EnemyBridgePathTest calls it directly. Offline projects source-link the same API implementation. CastlePlanner's AIV geometry remains separate. This explicit request supersedes the earlier restriction on adding a public API for this helper; the behavior fix remains disabled.
