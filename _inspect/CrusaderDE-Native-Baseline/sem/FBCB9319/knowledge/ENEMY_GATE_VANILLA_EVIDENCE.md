# Gatehouse policy: native baseline and evidence limits (2026-10-13)

Native SHA256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The completed feature audit was reconfirmed against the installed image: 60 complete search/climb bodies, 16,960 instruction ranges, 21 planning/output bodies, plus the previously inspected topology, strategic planning and Maceman update bodies. Evidence remains in EnemyGateBuildingContextAudit/native-assassin-publication-evidence.txt, bridge-virtual-evidence.txt, bridge-decision-evidence.txt and macemen-native-update-evidence.txt. No new native addresses or hooks are introduced.

## Confirmed behavior

- E49D0 constructs components with gates temporarily closed through C4BF0(1), then restores them with C4BF0(0). D8CE0 updates gate boundary directions. This is not a physical drawbridge closure.
- E2610 and E2F60 return early for identical valid components. Their ordinary cross-component traversal excludes hostile uncaptured gate records independently of visible opening. They allow a nonzero capturer without requiring that capturer to be the querying player or ally.
- 11B520 uses the ordinary component check for appropriate groups; equal components and pure Assassin groups have distinct paths. F4930 likewise has equal-component and special search paths. These exceptions do not establish that an arbitrary ordinary attack group will plan a ground passage through a hostile gate.
- 11A980 can choose an enemy building via EDF30/C2280 and issue38 (building approach). 30620 can choose an enemy building before successful final reachability. An enemy target behind a gate, or a gate target itself, does not prove a through-gate movement order.
- DA020's query player and the movement player can differ by audited caller role. The existing verified movement scope remains unchanged.

## Policy and historical interpretation

The mod retains its player-specific gatehouse boundary masks, early capturer comparison, own/allied/captured access rules and permitted Assassin climbing. Its extra restrictions must be demonstrated per actual search path. Bridges remain outside this policy.

The October1 capturer-adapter correction repaired our own flags transport. A successful game run afterwards proves a regression outcome; it alone does not prove a change from unmodified Vanilla. Older aiDetour/vanillaDetour counters meant a successful query with rejected edges, not an observed counterfactual detour. Older PASS observation verdicts certified activity only. Historical logs remain unchanged.

## Diagnostic contract

Observation is OBSERVED; PASS/FAIL is reserved for checked integrity or explicit functional invariants. Negative searches with/without rejected edges and incomplete calls are separate. Local native comparison preservation/change is measurable, but an original-call result under active hooks is not pristine Vanilla.

Synchronous order evidence distinguishes early comparison changes from individual-route filtering. Equal PCLs are labeled a shortcut only when actually seen in the region call. No region callback means unobserved: it does not identify a skipped caller branch or cache. Packed output with unknown movement stays unproven. No extra searches, native hooks, artificial orders or retained unit histories are added.
