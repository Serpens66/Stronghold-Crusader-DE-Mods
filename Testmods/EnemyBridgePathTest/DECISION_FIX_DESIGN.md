# Virtual closed drawbridge decision design

Status: passive virtual shadow installed; no behavior policy is published.

## Stable execution evidence (2026-10-06)

Frozen `bridge-20261006-133913.log`, full SHA256
`26AD1EC083B8A65835E77C012ADC5E2E463DEE64AF35DC0D7797AF282C79C4FD`,
covers13:39:13.395..13:41:06.464:2118706 balanced native calls,36923 paired
commands, no capture/ID/queue/reference failures, complete file and delivery.
Bridge703/g2432893 lowers once at13:39:15.273; no raise follows. Its15 deck moat
IDs stay unchanged. Player8 first target becomes225464 after CF360 accepts102/1;
decision12 at root679188 consumes phase4->6 with native/effective102. Retained
root710831 uses four group commands/25 unit commands and83 linked path observations.
Group start/target PCL is1/1 and no region query occurs. Twenty player8 units
actually enter and exit the side deck. In all,91 units (players3:24,4:23,5:1,
7:23,8:20) generate364 adjacent movement observations.535 route observations
include511 linked to retained decisions; these are not535 unique commands.
The user's closed-gate observation is separate from the geometrical parent link.

The raised initial physical definition7 and settled lowered definition31 have
the same49 sampled tile IDs/moat IDs. Removing transitions incident to the15
deck cells reproduces every observed raised direction byte, including diagonals.
`bridge-topology-regression.py` proves this local boundary only. The complete
packed map is not present in the trace, so it cannot prove keep-to-keep virtual
NoRoute or the absence of alternative routes. The same-save raised counterrun
is now secured and paired below; another general counterrun is unnecessary.

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
and out of those cells removed. Validate the copied neighborhood against the
raised direction rules, not only the deck cells. Preserve unaffected components and allowed
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

1. Counterrun evidence is secured: save-identical settled player8 planning differs
   as documented below. Capture is complete; the raised run's pending67 rows and
   torn final row limit delivery, not its earlier completed decision chains.
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

## Completed virtual-topology audit and limits

`bridge-virtual-audit.py` retains42 complete hash-selected function bodies,
installed instruction decodings, full-body byte hashes and incoming references.
It complements the122-function audit and32 unchanged hook contracts; no new
native entry is installed. Physical mapper, moat updates, 3x3 direction refresh,
temporary gate closure, component flooding, macro query and early/later decision
consumers are covered as complete functions.

-107160 reads the tile's signed-short special-record ID, then record kind at
 stride0x9C+0x6A. Only nonzero record ID, signed kind>4 and kind!=15 permits the
 seed exception. Callers consume AL: the full64-bit return can contain stale
 upper bits and must not be tested as a managed Boolean/nonzero64-bit value.
-E49D0 scans320800 packed tiles in native order, admits the surface predicate
 or bit12/special exception, and marks reachable neighbors through eight outgoing
 bits. It is not a rectangular flood, nor automatically a strongly-connected
 component algorithm. Seed eligibility differs from transition eligibility.
-D86F0 depends on surface flags, effective heights, structure occupancy/height,
 water/ramp state, special records and diagonal corner rules.725E0 recalculates
 all footprint cells plus their3x3 neighborhoods. A cut matching this fixture
 does not establish all elevated/structure configurations.
-E2610 mode0 excludes connection type1; mode1 allows it, while type!=1 is excluded
 for mode2. Eligibility also needs active record, nonzero active/open field,
 player0 or matching native alliance group or captured-building state. Preserve
 actual record semantics; do not rename type1 as a bridge without evidence.
 Its first search excludes closed records; its second search includes them and
 sets the native structure-required flag at60AD6F8. A positive second-pass result
 is not ordinary free passage. Later structure/exit consumers must remain intact.
 Each record connects up to three component endpoints; all actual endpoint tiles
 need remapping. A whole-record removal or only two endpoint IDs is insufficient.
-The creation/footprint/exit functions B47E0/739C0/6CDD0 do not establish bridge703's
 parent authorization. GameBuilding.r_GatehouseId is still0, and searches for
 its canonical absolute/manager-relative access found no supported parent writer
 in this reviewed chain. This is an unresolved contract, not proof that Vanilla
 never has such a link. Do not promote same-owner adjacency to permission.
-A5E20's bridge animation uses6C3B0 to test unit presence in a native orientation
 mapper and trigger automatic raising; it does not obtain a gate owner. Its
 view-dependent rendering calls B9330 for type45 nearby in selected orientations.
 B9330 walks C0270's perimeter offsets and accepts the first alive-state2/type
 match, but ignores its player argument and writes no parent field. This is a
 stronger native rendering-context candidate, still no capture/access authority.
 No additional native perimeter search is invoked by the diagnostic mod.
-Gate's IsUnrelatedGateCombination permits owner allies and valid capturer allies;
 unrelated uncaptured ownership is blocked. Invalid roles retain its fail-open
 contract. NativeGateAccessSnapshot.PreserveUncaptured is a different contract.

The previous Extender audit used2.12.0/commitf8d51730fcb54b25af43d3c9348d57db058e077f; the current update is documented below.
The public unit-type count is89; native profile capacity/permission stride is90.
The observed89/534 spans are canonical matching prefixes, not complete coverage.
The per-class getter offsets classes2..6 by1..5 entries too early. Preserve the
existing Gate flat-prefix/native-stride validation and do not use that getter
for the virtual policy. The English author report is retained; fork unchanged.

No behavior hook is selected beyond the audited early boolean-consumer design.
The later target/formation integration must be settled with its existing owner,
after global virtual topology and authorization have been validated. These open
inputs are represented as Unknown, never inferred NoRoute. No public API changes.

## Passive transport revision

Native observations now use fixed26-value `native-frame` rows: op,parent,
six args,scopeSession,state,seven entry stamp fields,completed,return(or v),
region count,six retained-plan fields. Every old entry/exit field is reconstructed
by the analyzer. Command contexts compare five numeric fields before enqueue:
parent,priorPlan,priorPhysical,priorTopology,state. Equal contexts reference one
definition; IDs remain monotonic across reloads. The bounded publisher and
session-delivered marker are unchanged. Captures remain synchronous; formatting
native values occurs only in WriteRecord. No return, target, path or flag changes.

Validation completed2026-10-06:37738 Bridge assertions,3830 Gate assertions,
frozen stable/older-chain reconstruction and49-cell boundary regression passed.
The2118706-call/36923-command fixture's unchanged hot population outputs104600
bytes including a95-byte-per-record logger-prefix allowance, normalized over
113.069s (about55505 bytes/min). It excludes changed decision bursts and is not
a measured game quiet-run result. The initially stricter100KB absolute assertion
was replaced by the specified time-normalized1MB/min criterion before installation.
The final elevated driver completes with zero warnings/errors; installed and
local DLL hashes match15D8D4A76304F1C62183A43EE74EBB9732603E4207131733763EE2A8C86166A7.
Both driver transcripts remain retained. Runtime JSON/lifecycle/public members,
permanent-hook workspace checks and CRLF passed. No new game/Unity members, native
hooks or searches were introduced. Existing members are validated by verify.ps1.

## 2026-10-06 raised counterrun and shadow implementation

Frozen bridge-20261006-155247.log has9941535 bytes and SHA256
D0DE38D7BEA5C0FDD41CDCE44D8557106598236930805038F0CCA5FC06098CBC.
The exact byte boundary matters: a new BepInEx header was appended directly to
the torn final Bridge row. Cutting by whole lines loses that fragment and falsely
reports file completeness. Capture is complete:2500812 balanced native calls,
40729 paired commands, no capture/ID/queue/reference failures. At session end53
critical+14 background rows remain; no delivery marker exists. Both delivery and
Bridge file completeness are false, while complete earlier chains remain valid.

Player8 starts from the same entry plan4/0/1/224222/543/489. Lowered uses
D95E0(1,1,1), target225464 and4->6; raised usesD95E0(1,0,1), target232127,
negative modes0/1 and4->5, then remains5. No stored deck route or actual crossing
was captured in the raised run. Early transient openings and the player5 positive
5->6 decision are excluded from the settled player8 comparison. This completes
the requested counterrun; another general raised replay is unnecessary.

Installed SE2.13.0, local85ab962b342c18f663da830570884a25b85116d0, current Native
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2, unchanged
NativeX64 backend0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0.
The122-function and42-function audits/32 entries have been renewed. The89/90,
534/540 warning remains; no completeness or new permission claim follows from
this update. Current byte gate-state fields are reconstructed as low|high<<8.

VirtualBridgeMap owns copied packed components, edges, coordinates, flags and
signed107160 special-record inputs. The read-only core follows directed edges,
removes candidate deck vertices, keeps actual macro A/B endpoints and preserves
the two E2610 passes. Important correction: the first-pass comparison at context
+record*0x204+0x2028 is record class+4, not an independent closed flag; class1 is
excluded in mode0 and is a structure-required second-pass connection in mode1.
No live native structure-required flag or visit table is modified.

Runtime capture occurs only in the existing successful E49D0 post, with matching
pre/post dirty/revision and identity checks. It is throttled to at most one capture
per second. Spawn/delete, physical change, capture, ownership/identity/alliance
change, incomplete publication and session replacement invalidate inputs. Queries
compare keep access, completed target choice and formation endpoints. Group/target
mode hypotheses are explicitly marked; they are not observed native group modes.
CF's actual macro query order is target-component to attacker-component, while
the shadow evaluates movement from attacker tile to target tile. Pure managed
jobs run in64-node steps under an intended1ms render budget, separately from the
existing64-record/2ms output budget. Queues are bounded32; rejected and cancelled
jobs and interval capture/compute costs are explicit. No Vanilla search is added.

This is a geometric shadow, not a validated exact replacement for E49D0. Seed
predicate data are copied and tested, but complete closed-state relabelling and
all orientation/height exceptions have not yet been compared on a full map.
ComponentC has no public exact tile field: it can use an unchanged component
anchor, but an affected third component makes a negative result Unknown. Invalid
subject globals do likewise. Geometric NoRoute remains conditional. Cut policies,
unresolved parent authorization, missing Gate snapshot and unobserved group/unit
mode permissions yield policy Unknown. No behavioral result is installed.

The pure core covers alternative routes, directed asymmetry, diagonal packed
coordinates, class/mode/access eligibility, second-pass structure status, exact
third endpoints, unknown inputs and signed seed exceptions. Live full-map parity
will be assessed from shadow output; synthetic grid tests are not that evidence.

Native output uses bounded16-row packets with each original37-field row envelope
and numeric zero runs. Definitions and per-operation metadata remain recoverable.
Both frozen logs round-trip with identical native fields, decision/route/movement
chains and completeness: prefix-inclusive bytes4765869->4339452 (lowered),
7052551->5974835 (raised). Active-run volume still exceeds1MB/min. The separate
unchanged production fixture falls to53139 bytes over113.069s (about0.028MB/min).
This is a fixture measurement, not proof of quiet in-game runtime cost.

## Shadow validation and installation (2026-10-06 16:53)

The final elevated build.bat ran once and completed with zero warnings/errors.
Local and installed EnemyBridgePathTest.dll SHA256 both equal
3353B181AF4D28991637E9829AA6A7FCFEA1BED3469DA1D4F05E6FBDF36C61B6.
The retained transcript is _inspect/EnemyGateBuildingContextAudit/bridge-shadow-build.log.
Bridge tests passed37885 assertions; the Gate compatibility suite passed3840.
Analyzer completeness, both frozen counterruns, lossless native batches, local
boundary and previous decision/route regressions passed. Installed public view,
layout, lifecycle, JSON, CRLF and workspace permanent-hook checks passed.

Every queued shadow query has a unique definition, including two modes of one
operation. Repeated observations reference it; session cancellation lists its
pending definitions. Calculation exceptions deliver Unknown. The analyzer reports
shadow inputs/results/cancelled/pending independently of Bridge delivery and
validates result/reference definitions. No behavior decision has changed.

Remaining acceptance is explicit: no full-map in-game shadow result has yet been
measured. Geometric deck-cut results are hypotheses; a cut policy remains Unknown
until complete closed-state topology, macro endpoint and authorization parity is
proven. The evidence establishes the planning difference but does not yet make
this geometric model an authoritative replacement. Active trace volume remains
above1MB/min; the lower background fixture result does not claim otherwise.
The next focused run can reuse the known save to inspect virtual-shadow output;
another broad raised baseline comparison is unnecessary.
## 2026-10-06 native installation failure and repair

Frozen bridge-20261006-202236.log:59587638 bytes, SHA256
2265CFEA8D18ED975FAC1C74B4245E4D01B08ADD2060BBEC75BA7C899C41332D.
The20:22:36.457..20:24:41.505 session delivered37258 matching command pairs
and a complete Bridge delivery marker. The final non-Bridge SE line is torn.
There are no Native hooks/calls, coherent captures or shadow queries; balanced0/0
and event captureComplete do not establish Native coverage. Bridge703 lowers,
then all15 deck fields belong to PCL1. Bridge trace704197 bytes is not a valid
full Native diagnostic volume benchmark. AI Build Diagnose Test contributes
55959965 bytes to the59587638-byte file. Duplicate plugins were removed by the
user; only the canonical Bridge DLL remains in the plugin search path.

The copied live-body replay with actual V2 and installed NativeX64 reproduces
attack-phases displacement57 instead of15. Fixes enabled its two inline patches
at3C30F (18 displaced bytes) and3C3D9 (36 bytes). The first stub pointer in the
failed run was0x00007FFDC7716490. Its inline bytes90 64 71 C7 FD 7F 00 00 include
a spurious FS-prefixed JNO when scanned as code: branch from function+0x36 to
function entry. The backend widens its prologue to include this data branch.
This is an ASLR-dependent parsing artifact, not a genuine incoming Vanilla edge.
The canonical full122-function/32-entry and42-function topology audits were
renewed against the unchanged Native and backend hashes before this repair.

Only attack-phases now uses scanLimit15. Its full baseline function has no incoming
interior edges into this straight-line prefix. The trampoline resumes at3C2EF.
Before preparation the entire1753-byte live body must match the immutable load-time
snapshot except the two audited Fixes windows, each exactlyFF25/zero displacement,
a nonzero eight-byte pointer and NOP tail. Unknown modifications fail closed.
This does not relax scheme, displaced count, target, pointer-slot, entry or
trampoline checks. The installed backend test also validates the bounded hook
before/after Enable on a private copy. No game function or Fixes hook is executed
by the reproduction; existing V2/V3 original-call fixtures retain ABI/return tests.

Preparation failure releases only unpublished candidates after checking both the
publicationStarted guard and every hook's uninstalled state. Enable starts the
permanent publication boundary; no failure beyond it rolls back code. Detailed
contract errors report expected/actual fields and phase. Startup logs the loaded
managed DLL path/hash, and the first durable render publisher emits readiness.
Native counters for the current map reset independently of process totals, so
old-map calls cannot establish new-map coverage. The analyzer separates event
capture, Native installation/calls, coherent shadow capture/results and delivery.
Unknown shadow results remain explicit; this repair activates no behavior policy.

Prebuild checks:37895 Bridge assertions,3840 Gate assertions, frozen unavailable,
positive/negative planning, route, topology and lossless transport regressions,
installed snapshot/public members, JSON/lifecycle, permanent hooks and CRLF pass.

Installation20:43:55: the single elevated build.bat completed with zero warnings/errors.
Installed and local DLL SHA256 both 3F24297AFC201783DCC775DBCCBE18B9BE4EF31AA6323C60EBAAE54F18D9E651.
Transcript: _inspect/EnemyGateBuildingContextAudit/bridge-native-repair-build.log.
A new game start is still required to confirm installation in the real process
and obtain the first coherent shadow comparison. Version and README are unchanged.


## 2026-10-06: controlled shadow inputs and offline artifacts

Frozen process: bridge-20261006-204752.log, 4,924,654 bytes, SHA256
463A8978B48BFE188947EA2481432B1938B20B366A75A887280BAC56DAA4C1C3.
It is selected by the BepInEx process boundary, not by the newest appended process.
The regression retains 1,136,667 completed Native calls, 23,246 paired commands,
31 coherent captures and 171 shadow results (74 Reachable, 13 NoRoute, 84 Unknown).
All 171 effective policies remain Unknown. Player8 has 25 units with observed
entry and exit. Query676422/definition96 returns geometric NoRoute after cutting
45 tiles across three bridges, whereas CF returns positive. This is not a validated
negative: query direction differed and no uncut control existed. Capture is complete;
90 queued records and the missing delivery marker make delivery incomplete.

The full122-function decision audit and42-function topology audit were renewed
against the entire installed Native hash. E2610 is a component query: equality
returns the component (including zero), otherwise it expands from destination,
first excluding class1, then with class1. Mode0 excludes class1 entirely; mode2
excludes other classes. Eligibility is active==1, enabled!=0 and native team access
or nonzero building capturer. CF360/CF400 pass target keep component as current,
attacker keep component as destination, modes0/1. The copied component control
reproduces boolean reachability only, not the exact next-component return or
native counters/structure flags. Raw component control and Gate-filtered directed
tile geometry are separate evidence bases. Gate ownership/capture restrictions
remain in the geometric adapter; Gate policy publication is frozen per request.
No new native entry, incoming edge, ABI, backend or hook owner is introduced.

Each request runs six controlled geometric variants on one immutable capture:
uncut/only703/all-hostile in attacker-to-target and target-to-attacker directions.
CF comparison uses its observed mode and direction. Group modes remain hypotheses.
A CF cut interpretation is blocked unless the raw macro boolean and the uncut
native-direction tile result both match the observed CF boolean; a direction's
uncut path must also be reachable. This is conditional diagnostic parity, not
permission to change AI behavior. Unresolved actual endpoints, notably componentC,
never obtain a physical endpoint from an arbitrary PCL anchor. A positive known
alternative remains a witness; unresolved transitions prevent negative proof.
Parent geometry and the Gate permission-table coverage warning remain unresolved.

Every successful E49D0 rebuild now attempts a coherent snapshot without the
one-second throttle. Dirty/revision/identity checks remain. Coordinate arrays are
shared only after comparing all800 native row starts and all320,800 packed row
coordinates; X is exactly tile-rowStart[Y]. Other live topology grids are copied.
The map adopts only these already-owned immutable arrays, avoiding a second copy.
A valid input/policy is retained for its historical request even if publication
changes later. Missing input at the decision, dirty topology, changed identities/
roles/alliances and a policy already stale at capture have distinct markers.
Map end cancels pending calculations explicitly; it does not discard file output.

At most two schema1 binary input artifacts are selected per map: the first valid
player8 keep check and a player8 group proven relevant by stored deck-route evidence.
The latter retains its original Pre input, group identity, planning root, decision
and phase where actually linked; a coalesced source query is separately identified.
The files contain copied grids, signed special records, all consumed connection
members including raw third components, building/unit globals, owner/capturer and
alliance inputs, bridge identities/decks/parent authorization, query parameters and
the complete immutable player-specific Gate direction mask. Unknown input stays
explicit. No path search or native function is called by the producer or replay.

The permanent render publisher writes at most one64-KiB block per render and aims
at2ms including lazy encoding/hashing and IO. This is a soft time target, not a hard
filesystem latency guarantee. Files live under BepInEx/diagnostics/EnemyBridgePathTest/
<UTC capture instance>/, outside the plugin directory removed by build.bat.
Only after footer/payload SHA256, close and rename from .partial to .bin is the full
file hash/completion emitted. Session-delivered waits for pending artifacts. Failed
artifact status and log/file delivery are evaluated independently. Abrupt process
exit cannot finish either queue. Do not analyze .partial files.

Offline validation: _inspect/EnemyGateBuildingContextAudit/bridge-artifact-analysis.py
<absolute .bin path> --replay. It validates schema/extents/footer/hash and invokes
the PolicyTests --replay mode, which reconstructs inputs and uses production
Prepare/Pump rather than a separate graph implementation. Versioned inputs permit
further core tests without restarting the game. Completion records alone do not
prove that an artifact exists intact; the offline decoder verifies its bytes.

Player8 strategic references remain explicit; other repeated shadow references are
batched with exact repeat counts and explicitly summarized intermediate parents.
All Native/event counters and existing relevant route chains remain. The64-record/
2ms log drain and bounded queue remain. Existing quiet-volume regressions are not
a live-volume claim for this new full-shadow build; the previous active trace was
3,276,971 bytes in47.271s. Live capture/compute/output cost and volume still require
the next targeted run. No behavior policy, public API, SE fork, version or README
is changed. Test next with a permanently lowered bridge and closed gatehouse, then
end the map and await both artifact completion and session-delivered before exit.


Build/installation 2026-10-06 21:30:20: the prescribed elevated build.bat ran once successfully, with zero warnings/errors. Bridge assertions: 38012; Gate assertions: 3840; all seven frozen log/transport/topology regressions passed. Installed and local DLL SHA256: 3CBEC1AC3B0FDBF6542B67FEC3DD773D4D2BAEE821CD84C3E99308E2674E44F5. Native SHA256 was rechecked unchanged after installation. Build transcript: _inspect/EnemyGateBuildingContextAudit/bridge-shadow-controls-build.log. Binary fixture integrity and replay were independently checked by the Python decoder and production C# adapter. The real uncut/cut comparison and live costs remain pending the next targeted game run.

## 2026-10-07: real continuous planning and conditional raised replay

Frozen regression: `_inspect/EnemyGateBuildingContextAudit/real-plan-20261007-151016`.
The full archived Log_264 SHA256 is 598639F9EB2DBB7FEB7D9705AE2CFA392BE447556A1E277404B3623F5991D3CB.
Planning SHA256 48EB96C1902815BC062B4C87777679A49FFCF6B38997AEA3F72FE5BCA373A552;
linked group SHA256 6E0437D565FCD4CA77D4F1CD1A56DA04F465EE3103D46FD3958893390C0BB461.
Later LogOutput processes are excluded. Original binary/log files are byte-exact;
manifest and productive replay reports retain full hashes and source paths.

Full installed native audit remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
122 decision/path bodies,42 topology bodies,30 private reference bodies plus2
native table readers. Installed members/backend, runtime JSON/lifecycle and
permanent publisher/hook checks passed. Runtime work remains rooted by static
Application.onBeforeRender and permanent native/event owners after startup cleanup.
No new hook, public API, game-member access, JSON dependency or executable mutation.

The productive baseline now starts at caller-pre, validates CF360-derived R8,
its mode0 scratch controls C0/C4 and target-bank selection, clears seed state as
2D250 does, then carries its calculated Seed output through Distance and caller
completion. It never reloads recorded seed-post/distance-pre as working state.
Full field/queue/visit/controls/candidate-column comparisons remain. Tests separate
shared immutable section references before corruption checks, so mutating one
input cannot accidentally mutate its expected Post evidence too. Private capture
fixtures were corrected to put 2D250's bank write BEFORE D95E0, matching native order.

Real uncut replay matches the observed fields and 2C5A0 projection. Native count0
is kept separate from effective Fixes count132415. The copied PCL histogram and
zero-cut rebuilt count both confirm132415. The canonical Fixes1.25.0 owner at
2C5E1 reads its extended table; it is not replaced. Supported virtual labels remain
bounded to999; unsupported extended labels return unknown rather than being guessed.

Topology states are now explicit: captured physical directions; physical raised
overlay; E49D0's C4BF0(1) closure for the global flood; restored physical publication;
then an independent temporary closure for planning. The captured physical map
supplies special107160 data. Active macro-subject globals and coupling identities
are validated; A/B use native offsets36/48 and are mapped through concrete tiles.
Unknown C preserves independently known A/B witnesses, but cannot prove a negative.
Coupling comes from APIShared; bridge/gate owner, allied owner, capturer and allied
capturer permission remain separate. No native/default permission table is invented.
Native D9190 permission semantics are explicitly not effective Gate unit permission.

Controls on the same historical inputs:
- Original: CF Reachable, seedShortLimit=True, origin103/target1, seed/distance changes0/0.
- Global rebuild WITHOUT deck cut: CF Reachable; seed/distance changes0/0 and count132415.
  However one tile is added and one removed. The first removed tile123127 (326,350)
  has flags0000B000, old/new PCL1/0. No split or merge is observed. Therefore Dirty1
  is not silently promoted to a current, complete negative topology publication.
- Only703 (15 fields): CF NoRoute, R8 True->False, origin105/target31;
  4000 changed seed fields,34878 changed distance fields; observed-target count116982.
- All unallowed confirmed coupled bridges (45 fields): same first R8 change and
  same field-change counts; origin107/target31, observed-target count116964.

These are conditional calculations on captured physics, NOT live policy results.
Revision43, Dirty1, planning clock788991761, physical capture789011290 and query
observation789073802 are retained. Group input independently retains native
revision46/Dirty0 and its actual capture timing. Legacy artifacts missing validity
metadata remain readable but their cut assessment is explicitly blocked.

Important integration limit: this real 2D250 uses mode0/candidateDistance0. D9190
preserves the434 prior candidate entries, including both table columns. 2C5A0
projects the stored selection225464; it does not select a new target. Therefore
reports do NOT call the unchanged candidate table a fresh candidate choice or
invent a new virtual target/phase. First changed planning parameter is proven;
a fully replayed subsequent target consumer and pending update semantics remain
release gates. A future decision patch must use the same valid virtual publication
for early fields, castle checks and late formation/equal-PCL decisions. Unknown
preserves Vanilla; successful alternatives stay allowed. No late movement block.

Runtime diagnosis: changing command values are fixed31-column numeric records;
only constant kind/id/global/type identities are defined. Identity comparisons are
numeric before formatting. Original envelopes, parent/event/context, full64 returns,
retained arguments/a6, positions/PCL, search counters, promotion and timing survive.
Duplicated Post aggregate details point to the command envelope and matching Pre.
The reader handles both old12-column frozen traces and new31-column transport;
missing identities/torn rows are rejected. Full-row delivery remains64/target2ms.

Fresh or changed strategic keep decisions are prioritized. Unchanged keeps and
selected-target hypotheses are counted separately without duplicate six-variant
searches. In production, full group comparisons focus on the retained planning
artifact's exact decision root. Other group calculations are marked skipped,
without dropping their route/command evidence. Queue rejection, cancellation,
intentional skips, completed calculation and file delivery remain separate facts.
Groups/alternative modes remain hypotheses; native castle direction stays target
->attacker. Historical immutable inputs are never replaced by a newer observation.
Artifact output stays one64KiB block/render; session-delivered remains unchanged.

Frozen population:1837071 native completions,36747 command pairs,105 topology
copies,239 rejected computations and10 cancellations. Full capture/delivery is not
full calculation coverage. Command-only transport replay reduces Bridge bytes
including prefixes7894389->6453084 (18.3percent):6420 detailed observations through
352 identity definitions. This is OFFLINE serialization, not an in-game timing or
under1MB/min acceptance. Existing unchanged-population volume fixtures pass;
active burst volume and game compute cost remain open measurements.

Behavior fix remains disabled. APIShared stays0.4.10; all versions, README and
Script-Extender fork remain unchanged. No new game start was performed or requested.

## 2026-10-07: bounded consumer acquisition and historical flood reference

The completed Log264 plan lacks the inputs of2C480's downstream consumers.
No new virtual target or full phase replay is claimed. Historical replay explicitly
returns Unknown for missing consumer inputs; captured434 D9190 mode0 candidates
remain inherited. Recorded Post fields are never substituted for full computation.

The private reference now retains41 hash-checked complete bodies (including3C2E0,
2C480,10DF60 and115B10), plus2 readers. Unsupported outbound branches trap; full
3C2E0/2C480 execution is NOT validated merely by including their bodies. Executed
native comparisons validate1126B0's one-based unit target selection and all five
availability consumers:112370/1123E0/112200/112450/112190, signed5999 boundary and
both reservation modes. Productive byte-copy kernels agree on complete buffers.

A hash-bound replay of the original real plan supplies the separately validated
native temporary gate-closure input to E49D0's full component flood. In this narrow
reference the building update list is empty; it does NOT claim replay of live
C5040 building updates. Entire320800 component grid,1000 field-count entries,
component ceiling and total agree with the productive flood. Changes versus the
old Dirty1 publication are exactly:
- tile123127,326/350,flags0000B000,edges0: oldPCL1 ->0.
- tile290885,414/627,flags00008000,edges249: oldPCL0 ->1.
The former cannot seed the ordinary flood and has no incoming flood inclusion;
the latter is included by the copied directions. These differences show disagreement between captured labels and the copied
physical rebuild; they do not authorize a negative live decision. Dirty/revision and
old versus rebuilt labels stay distinct. Policy remains Unknown.

Acquisition uses existing3C2E0,2C480,10DF60 and115B10 observers, no new hooks.
Only the selected player's own phase4 military entry and planning-root consumer
are captured. Boundaries carry session,root,op,parent,actual players,clock,Dirty and
native revision. Capture2C480 Pre/Post and builder/weight Pre/Post; retain own
military exit. Required child stages are checked before artifact publication.

Concrete new inputs at2C480 entry, not replaceable from Log264:
- unit occupancy4C559B0 (320800 ushort),height4DDD350/base-height4E2B870 and
  terrain owner4E79D90 (320800 bytes each),selection mask53AD4B0 and seed field;
- unit manager67E8400: bounded next-ID1..10000,header65C and490-byte records,
  including reserved storage without public ID0 lookups;
- tribe manager7CC6720:2A-byte prefix and4500 raw688-byte slots;
- candidate working span2E76F10..2F91E24,derived from the last eight-player
  candidate table extent (11AF14 bytes),and nine player records starting379AE00;
- actual10DF60 cost branch byte8574B90; current consumer flags/components/edges,
  building-ID grid and bounded live building records.
All are copied immutable byte storage; no large interop structs constructed.
Unchanged definitions reuse exact own bytes. Additive consumer capture version1
uses the existing schema2 artifact; old schemas1/2 remain readable with unknown
consumer coverage. Import checks complete stages,frame association and capacities.
Local unit/availability handoff replay is labelled partial: it is not a computed
candidate-builder/weight or full military counterfactual.

Limits remain two capture attempts,one planning/one linked group artifact,64 log
records and one64KiB artifact block per render with target2ms. Existing tests with
dummy native pointers do not dereference them; actual capture is tested on the
private native image,including file write/import and mismatched-frame rejection.
APIShared0.4.10,versions,README,public APIs,SE fork and permanent32 owners unchanged.

Next acquisition: same prepared save,bridge permanently DOWN and gate CLOSED,
Gate/Fixes/manual bridge feature active,no own ongoing moat work. Capture first
fresh player8 phase4 planning and linked bridge-relevant group. Wait after map end
for session-delivered and both complete artifact/hash markers. Look for
planning-consumer-captured and planning-consumer-coverage with hasConsumer=True
and hasMilitaryEntry=True. No raised counterrun requested. Fix remains disabled.


Final review/build 2026-10-07: the second review added explicit consumer building
count/record and Post player-buffer extent checks. Native capture/write/import
fixtures Dirty0/1 pass including child-frame corruption rejection. Full historical
consumer replay remains Unknown; a successful local handoff is not full-chain proof.
Final prescribed elevated build.bat /nopause completed once, after preflight and
review:110 native differential cases/10095 checks,144 raising cases,38104 Bridge
assertions; existing Gate regression9894 assertions and frozen-log regressions pass.
Only existing Shared CS0649 warning; zero build errors. Package and installed DLL
SHA256 both68CE104F5FCBBCC833B158B3A4C766525034324AE22C39E8674550311856570C.
Installed APIShared0.4.10 unchanged. Behavior fix disabled. No version,README,
public API,SE fork or competing hook changes. Runtime/game cost of the new bounded
consumer acquisition still requires the targeted run; synthetic copy timings are
not game-cost measurements. Build report:consumer-final-build-20261007.log.


Offline consumer closure review 2026-10-07 (17:15:58 real session)

Frozen inputs: real-consumer-20261007-171558/Log_267.log SHA256
71120296E01A098ED7BF45E8DE94A4D36D4C8DADC84238E79FF0051FF782BE2C;
planning.bin 3C5290A9081930B4E0B3D55A537217DA2CDD04A497034403585D29ED059DB7AB;
group.bin CDBB2B69FAEA1895A0FA963597A051D64E6D02ADB4F13E78301F4F142CB607D7.
Process section and sources retained in SHA256.json. No original bytes changed.

Full feature evidence now retains 58 hash-validated private reference bodies and
two readers, plus the existing decision/topology audits. Retaining a body is not
proof that its whole historical call chain has executed. The new productive
115B10 weight kernel and 193C80/193D90 helpers match all output bytes against the
private native reference for both 70 and150 limits. Eight synthetic weight cases
cover signed-distance/score boundaries, inactive targets and unit blockers.
Both real-input weight branches also match native:70 reproduces recorded output;
150 differs by100 bytes. Actual inputs3665F10/3665F28 were not recorded. Matching
Post values does not establish those inputs for a counterfactual.

The full2C480 builder/CF020/military3C2E0 consumer remains Unknown. The fullC5040
Dirty rebuild also remains Unknown:67E6424 building dirty/counter controls and
all3999 native updater slots are missing. Existing empty-updater flood comparisons
do not explain132473->132471 or authorize a negative Dirty decision.

Consumer capture version2 adds the exact opaque mode values at weight Pre/Post,
full updater slots/controls, packed coordinate validity, effective combat and
building-class tables, and consumer Pre/Post work controls/queues/coordinates/
visits/distances/targets. These are passive own-frame copies via the existing
hash/range-checked reader. No hooks, game writes or additional searches added.
Importer validates extents, stable mode inputs and frame/identity association.
Old schema1/2 and consumer-version1 artifacts remain readable; absent inputs are
explicitly Unknown. inputs-present is not full replay or fix authorization.

Private producer/write/import/replay cases pass Dirty0/1, including corrupt mode
and wrong-frame rejection. Native suite:118 differential cases/10229 checks,
144 raising cases. Bridge suite:38109 assertions, including3776002-call/66689-
command population and bounded quiet output. Costs measured offline, not in-game.

Constant stored-route contract fields now have one route-format definition per
session. Numeric observations, paths, bindings and full decision chains remain.
Frozen transport model preserves reconstruction, movement counts, definitions
and chains; saves470140 bytes from11282251, yielding10812111. This changing-run
model is not the under1MB/min quiet-operation proof or an in-game measurement.

Installed SE2.13.1/native/backend contracts remain valid. Canonical Fixes1.25.1
preserves2C5E1 counter hook;10F150 removes discarded-EAX push/pop, so later planning
must retain the effective owner distinction. Permissions remain separate from
spatial APIShared coupling; negative policy and behavior fix remain disabled.
No version, README or Script Extender fork changes by this work.

A parallel migration added APIShared.UnitAccess calls in Bridge source. Its source
is included in offline policy tests; installed APIShared currently lacks it.
User chose the other chat to finish and install APIShared first. No Bridge runtime
build/install may proceed until that dependency is available and validated.

Next necessary acquisition after verified Bridge installation: same save, bridge
permanently DOWN, gate CLOSED, Gate/Fixes/manual bridge feature active, no own
ongoing moat work. First fresh player8 phase4 planning and exact linked group;
wait after map end for session-delivered and both complete artifact/hash markers.
One bounded planning plus one group artifact, maximum two attempts. No raised
counterrun. Full candidate/Dirty replay and fix approval follow those actual inputs.

Final independent checks: all11 existing frozen-regression scripts pass, plus
new real-consumer hashes/closure/transport checks and producer v2 artifact tests.
Runtime verify.ps1 passes JSON/lifecycle/publisher/permanent-hook/real-member/CRLF
checks after preserving the parallel UnitAccess migration. Source inclusion in the
offline project is required because it does not reference the runtime APIShared.

The existing Gate test executable was also attempted from its own project folder:
its native RedBird capturer cases pass, but NormalCursorPreviewContractIsExact
fails at the reference-first/filtered-last DB650 source assertion. Gate source is
being changed in parallel; no Gate files were modified to hide this failure.
This run must not be reported as a successful current Gate regression.

No runtime build or installation executed in this work session. The prescribed
Bridge driver remains pending the user-selected APIShared installation by the
other chat; final installed signature/hash and current owner checks must run
before that build. Do not request the acquisition run until installation succeeds.
