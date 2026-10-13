# EnemyBridgePathTest native update contract

Version 0.1.0 is diagnosis-only. It installs 30 permanent passive NativeX64 function detours, and publishes no path policy. Hard dependencies are Script Extender, APIShared and BugfixesAndQoL. It does not depend on EnemyGatePathfindingTest.

Reference native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Reference SE SHA-256: `DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF`, commit `f8d51730fcb54b25af43d3c9348d57db058e077f`, tree `657af449e1397c58e6c5ec054977d83198b68e66`.

## Owning features and derivation

| Feature | Reference native chain | Derivation / update action |
|---|---|---|
| Hypothetical closure cells | 0x739C0, 0x69850, 0xA5E20, 0x645C0, 0x64460, mapper 0x2D1A30 | Confirm ordered 5×5 occupancy, orientation/2 stride 100, 15 nonzero cells in four actual fixtures |
| Record prerequisite / physical flag | 0x69560, moat index layer +0x1EA23F0, 0x725A0 | Public GetMoatWorkTaskIndexLayer, nonzero index prerequisite; no private direct pointer to the layer |
| Native surface/region consequence | 0x725E0, 0xD90D0, 0xD86F0, 0xE3B90 | Confirm surface mask 0x4A5014B1 and physical raised flag 0x40000000; no modification |
| Same PCL / ordinary group | 0xE2610, 0x11B520, 0x117820, 0x117C70 | Recheck equal-PCL early return / skipped call, Assassin bypass and group mode; label absence separately |
| Work assignment | 0x11E960, 0xE7F60, 0x11B520 | Recheck command-7 selector success/early return/zero-PCL bypass, command-6 fallthrough and emitted command/context fields |
| Existing searches | mainmod-owned F4930, DBC60, DA020, 123090, 195E30, E2610 and D9C40 | Passive observer forwards existing results; exact native call count/result unchanged; hook absence is missing coverage |

Physical raise/lower, topology and early-decision functions are now passive executable targets listed completely in src/BridgeNativeDefinition.cs. The pure closure predicate is hash-bound; there is no semantic pattern fallback for ordered occupancy and the mapper. On native mismatch the diagnostic runtime stays inactive and logs an error. Unknown shapes, IDs, associations and reread mismatches remain unknown; they never create an active policy.

## Installed field / ID validation

`_inspect/EnemyGateBuildingContextAudit/verify-bridge-split.ps1` checks the complete public member list and signatures against installed SHCDESE.dll. Unit issued command/context X/Y/task offsets are 0x398/0x3E4/0x3E6/0x3B4. Bridge ordered occupancy begins at GameBuilding +0x1C8; grid +0xF8, orientation +0x102. Game IDs are 1-based; spans are 0-based. GetTileId has no coordinate guard, so coordinates are checked before row lookup. PCL accesses are bounded by the installed Span length.

Unity calls are public Application.onBeforeRender and Time.frameCount from installed Unity assemblies. No private or publicized Assembly-CSharp access is introduced. State/output objects and event registrations are statically rooted. Central APIShared session lifecycle resets logical state; no normal teardown unregisters hooks or observers. A post-startup render marker confirms the runtime survives component cleanup.

For a new Extender/native version, first update the full feature audit, installed enum/member/layout checks and compatibility with local Fixes/RedBird. Do not infer live validity from a successful build. README and historical evidence remain unchanged.

## Passive native decision probes (2026-10-04)

Before any native/Extender/RedBird update, regenerate the full-hash feature evidence and contract manifest with `_inspect/EnemyGateBuildingContextAudit/bridge-decision-audit.py`. Reconcile all 30 function ABIs, complete displaced blocks, incoming edges, continuation and full-function scan size with the installed DLL. Do not substitute X64InlineHook tests for NativeDetour tests. Installed backend SHA-256 is `0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0`.

Only Absolute is accepted: FF25 with zero displacement, embedded hook entry pointer, zero PointerSlot and NOP padding. Copied-function tests prepare/enable/validate the actual installed backend; a native executable fixture exercises the production wrapper. Fixture cleanup applies exclusively to unpublished independent test memory, never game hooks. Production delegates/hook objects remain rooted until process termination.

New native reads cover planning globals, candidate buffers, tribe manager fields and bridge direction/plan fields; they are hash-bound and module-bounded, not managed private-member accesses. Installed OnTick must remain public Action<int>. Its long-lived publisher supplies tick breadcrumbs; Application.onBeforeRender drains captured records and confirms survival after startup cleanup. Exact addresses/ABIs and owned-hook overlap analysis are in BRIDGE_DECISION_AUDIT.md.

## Lean managed capture contracts

The original25 hooks remain permanently rooted; five disjoint player-planning entry probes extend the same audited backend/ABI family. Revalidate public BuildingR3EventHooks.OnBuildingSpawn/OnBuildingDelete and public manager IsValidId(int)->bool after updates. Spawn/delete publishers only invalidate the bridge index; all subscriptions remain rooted. The diagnostic index uses1-based IDs and bounds-checks their0-based span slots, without TryGetBuildingById on an unknown parent. Refresh is event-driven with a one-second backstop at capture time.

Trace scope inputs use fixed scalar arguments. Quiet ordinary-attack/topology callbacks count and compare without full bridge capture or params-array/scope allocation. Offline tests link the real trace rather than a replacement trace class. Queue budgets are64 records/2ms per render,4096 critical plus4096 background retained records and no unbounded end-of-map drain. Exact repeat counts are not missing coverage; overflow/capture failure is.

## Player attack planning and focused capture

New owning feature: passive player attack decision context. Entries3C2E0/2C480/3BD50 useV2;2D250/2C5A0 useV3. All RVAs, exact displaced bytes, complete function sizes and continuations are in BridgeNativeDefinition.cs and bridge-decision-contracts.json. Reference hash is the full Native hash above; search domain is the installed native executable .text and its full-hash semantic records. The entry prologues alone are not unique patterns. Layout reads remain hash-bound and module-bounded; no generic-prologue scan or unchecked old-RVA fallback is allowed. A changed live entry fails installation with a named error, and existing public passive observer coverage remains. On a changed native layout re-audit the entire feature before enabling probes.

New data contracts: countdown37ED4CC; player origin379AE2C/stride583C, lord379D0D0, phase379D974, targetPlayer379D9A8, targetTile379D968, targetX/Y379D970/379D96C and keepTile379AFB0. Group short IDs379F670 and GlobalIDs379F8C8 cover proven slots0..199. Target seed/mode2E97EA8/2E97EAC and signed-short distance5759230 + player*320800*2 + tile*2. Candidate headers include raw +4 availability and raw16-byte rows. These fixed addresses derive from retained complete writer/consumer bodies, not managed private fields. No independent semantic layout fallback exists; a hash mismatch deactivates dependent diagnosis.

Fixes already owns2C5E1..2C5EF internally; the17-byte2C5A0 entry is disjoint. Preserve its effective component count. Existing movement/region/tactical hook owners remain authoritative. New role claims/field maps and remaining runtime uncertainty are in BRIDGE_DECISION_AUDIT.md; verify installed UnmanagedVector2<ushort>.X/Y public fields when refreshing cached geometry.

Capture relevance: changed group orders, changed near-bridge unit movement and orders in detailed native decisions. All other commands/results remain exact numeric background counts; unresolved contexts retain bounded raw examples. This does not prove outside-bridge movement is unrelated. Region totals include detailed records. Physical/topology/plan markers and per-player chronological prior-plan links never imply a proven route. Queue deliveryPending is distinct from captured traceComplete; wait for pending output before closing a comparison.

## Keep-access and reservation additions (2026-10-04)

Owner: passive bridge decision diagnosis. Reference hash is the full NativeHash above. New function entriesCF360/CF400:152 bytes, Absolute span14, L3(playerManager,attacker,target), prologue4883EC384C63D24D69CA3C580000; query modes0/1 respectively. Full enclosing bodies, all retained callers, continuation and incoming edges are in bridge-decision-evidence.txt/contracts.json. Installed backend must match BackendHash; live bytes and displaced size are checked before and after publication. No owner overlap found in installed-source SE/Gate/mainmod/Fixes. No cross-hash or changed-entry fallback is safe for this diagnostic because raw data layouts are hash-bound; log initialization error and retain independent passive coverage.

New reads: passed player manager +player*583C+12EC54(active raw),+12EDA0(keep tile); bounded signed PCL at50EC690+tile*2. Native canonical player manager3366C210 derives379AE64/379AFB0; derive from the actual argument rather than trusting this constant. Target-region global2E97D14 belongs to the target player. Task assignment uses public GameUnit.r_AI_ContextTargetBuildingTileId at3A4, matching67E8E00+unitId*490; AIState at2BC is separate from AI_LastIssuedTribeCommand at398; full122B40/122800 writes and native Unit manager's pre-array sentinel establish the mapping; Game-ID is1-based. Capacity/API/GlobalID checks precede access; module bounds remain checked. Public unit members are unchanged and validated against installed SHCDESE. Re-audit these reads, signedness and table reservation layout after every native update. No private Unity member or publicized assembly is introduced.

## 2026-10-04: assignment repair and numeric capture

The20:35:52.943..20:37:31.132 session has1559559 balanced native calls and32410 command pairs, zero queue overflows and zero ID0 lookups. Three capture failures abort consumer-return assignment evidence. The old raw assignment RVA contained an extra leading6; the correct native RVA is67E8E00. Canonical Unit manager67E8400 + sentinel-array offset65C + Game-ID*490 + public task offset3A4 exactly reproduces the installed122B40 write. Unit IDs remain1-based. AIState at2BC maps to native67E8D18; its65/67 assignments are distinct from AI_LastIssuedTribeCommand at398/native67E8DF4. Installed SHCDESE fields are public UInt32/UInt16/UInt16. Runtime now uses these public fields, verifies GlobalID and isolates each selected unit's capture failure. No new native address or hook is introduced.

At20:37:12.687, player8's unequal keep PCLs109/1 invoke mode0 E2610(targetKeep,attackerKeep): native=effective=109; CF360 accepts and enclosing3C2E0 phase5 becomes6. At20:37:18.959, keep PCLs116/31 invoke modes0 and1: native=effective=0; CF360/CF400 reject and enclosing phase6 becomes5. All63 emitted keep-access observations use unequal-region query branches. This proves the consumed access branch/phase difference, not a route through a particular bridge, and does not test equal-PCL bypass. There is one map session, so no save-identical reload experiment. All26 reservation-linked movement events refer to tasks/approaches outside observed bridge footprints/rings. Bridge703/720/828 footprint moat values are unchanged; four player5 work orders are outside bridge proximity. Missing own work markers do not prove absence.

Trace has6863 records/3546622 bytes including prefixes in98.189seconds. Measured full capture time5856.022ms versus538.476ms in the preceding run; text formatting followed by compiled-regex reparsing was present on every capture. All examined definitions resolve, but final summary reports167 queued records with6799 already delivered; only64 subsequent records appear, leaving104 undelivered. Capture and delivery completeness must both be checked.

Production captures now read reusable numeric arrays, compare numeric keys and format only new physical/planning/state definitions. Immutable dictionary keys are copied only for new definitions. Physical cells use columns tile/pcl/moat/flags/direction (flags hex; directions decimal), first grid*grid rows are occupied fields and remaining rows the immediate coordinate ring described by bounds. Invalid tile raw values remain visible without dereference. Planning columns remain tile/seed/field/distance16; missing distance is not-captured. Definition captureClock is original definition observation; referring operation Clock is its current synchronous observation. Separate interval read/compare/format costs and definition counts are emitted. These timers cover bridge fragments and candidate observations, not all wrapper cost; interval outputBytes excludes logger prefixes.

Unchanged candidate-table references are batched32 per record, preserving parent/rva/slot/definition/plan/phase/count. Exact repetitions coalesce before enqueueing. New planning provenance and reservation changes still produce evidence. Native entry contracts, original-once forwarding, public APIs and owners remain unchanged. Session-end is one immediate bounded record with captureComplete/deliveryComplete and critical/background pending; queued records continue through the existing process-rooted render publisher at64 records/2ms target. Immediate process exit can still leave output pending and must never be described as complete.

Copied gatehouse selection extension,2026-10-13 (offline only):
- Feature: EAB80 selection inside3B8D0->1140C0. Full native body649 bytes,
  SHA256069B44D62A1D36DF2B2E749A6AE7F5F3454BC9FB22EA81F5579A26D5D6D384B2,
  reference nativeFBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
- Runtime never calls/hook-installs EAB80. Private reference verifies complete
  body hash; copied kernel uses recorded grids/direction tables/building data.
- NativeBuilding record manager64CCBB0+id*32C; type12E, alive12C, owner132,
  global134, fire31A.1140C0 order coordinates are2BC/2BE, not1BC/1BE.
- Root60AD660 statisticsA0, generation4, queue155F38/155F6C/28F3EC;
  full source inputs already captured. Unknown extents or owner IDs abort.
- Original EAB80 permits building-grid neighbors independently of directions;
  raised physical edges alone do not exclude a gatehouse candidate.
- 30E90 stored target64C/650 is independent of this coordinate-only selection.
- Fixes owns interior EAB80 patches (EAD8C plus owner-check window). This step
  adds no competing hook, and unpatched private results are Vanilla evidence,
  not a complete reproduction of every effective Fixes option.
- Unknown hash/layout/data remains Unknown; no PE table defaults for live values.

## 2026-10-13: copied structure selection and own command entry

Native reference remains full SHA256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
EDF30:936 bytes,14DA7DA795A0026B125A2BAB880519E5EAF850B131335AFFB123E656CAB1B622.
C2280:81 bytes,416BCC215DBA09A94F39A848E4AB92DBAA33C44A680CEE7BF9AC73E0BA77ABDB.
DA020:1392 bytes,3914CE45FCD16723C8EAABA84C5929D14115B769BFEC4EAFF26C0EBA17528DD7.
C0270:590 bytes,8C851D48BC5579727AD53C1C1CC3A835E95C57CE8E68DBFD8B23C43BDBFEF97F.
117820:164 bytes,B51D84864B4FEAED91FE2A641DEA28BA645FE8743C6E69017AE1CFB4D4E56EE2.
Full private bodies use existing119F90,7490 and E2610 reference contracts.
No runtime call/entry hook is added for these functions. Exact copied memory
alone is used. Uncopied reads throw Unknown, never native DLL/default substitution.

EDF30 uses four direction indices0/2/4/6, not the eight-direction EAB80 search.
It can select a building despite absent direction connections. C2280 is a
building-class predicate, not proof of a reachable legal melee attack position.
DA020 walks C0270 rings in native order; secondary invalid coordinates do NOT
advance the ring ordinal. Heights, signed values, own native macro checks,
contact tile and candidate capacity are preserved. Capacity-truncated candidate
sets cannot prove total NoRoute. Uncopied E2CA0 assassin fallback stays Unknown.

Required copied ring tables:2E3FA0..2E4B00 (2912 bytes),2E8FD8 (14 int counts),
3123B0 (four XY int pairs),2E5360 (336 int height classes). Transient offset
scratch67E8380/4 is written by C0270 before use. Building record148 is signed
height,14A/14C origin,150 packed tile,154 grid size,134 GlobalId. Tribes use
native688 stride; member slots and leaders use native490 unit stride with
fields starting beyond the manager header. Source controller word6EE is not
interchangeable with the tribe owner integer2C. No new direct game members.
These writable table values are captured at own consumer/command Pre, never
substituted from another historical artifact or an installed DLL initializer.
All direct/derived addresses remain hash-bound, module-extent-checked; unknown
native hash or installation contract disables these dependent observations.
There is no speculative cross-hash layout fallback.

Own structure command snapshots use the existing11E960/11B520 event owners.
Explicit command38 contains building ID and GlobalId. Exact gate order XY on a
coordinate command is ONLY a candidate, not an observed EAB80 return. Current
APIShared coupling identities must agree with native globals for both bridge
and gate. Two bounded follow-entry attempts; only one plan and one follow file
may be published. Existing schema2 is extended additively with structure/*.
Original argument mutation, suppressed call, nested capture, map or ID change
rejects own-command proof. Copied Pre inputs and whole-command Post outputs
are separate; Post approach rows may already have been ranked/consumed.
A dirty shadow publication cannot block the own command input. A separately
copied topology must match its grids, Dirty and native generation exactly.
No later path-observation snapshot is presented as command-entry data.

Building damage is observed through the existing SDK OnBuildingTileTakeDamage.
Installed public event/args were decompiled and checked. Native7EB00 body and
28 complete callers were inspected; full body SHA256
DAEEF14006DC10549E09C1CC498A35B12E292F1CAE8D8737449F409BA198D424.
Owner remains Script Extender. Building signed HP word168 and GlobalId134
are read only. Param9/Unknown4 can supply the native unit slot, validated before
raw reading and correlated only to a matching stored route identity. Negative
source parameter values can represent native unit classes; they are preserved
as raw values and never sent to a player-resource API. The shared damage path
is NOT exclusive melee. SDK Post ReturnValue remains its default0 and is not
used; Post Damage is the original input, not necessarily the effective edited
Pre argument. Nested/reused identities do not attribute HP loss to one unit.
Samples are bounded16/map; event counts remain separate from Bridge detours.
Static event subscription survives plugin startup cleanup. No new executable
hook, public API, version, README or external mod change.

Own command inputs also contain full current unit/tribe/player managers at Pre and whole-command Post. These are copied at the event itself; military-entry managers are not reused. Pre/Post costs are separate from native time. Source arrays remain bounded by the already audited10000-unit/4500-tribe capacities and the existing two-file publisher.

## 2026-10-13: gate damage and retained bridge-loss evidence

Complete hash-bound decision audit122 plus63 full damage/deletion/attack bodies,
including installed instruction bodies, are retained in
_inspect/EnemyGateBuildingContextAudit/bridge-loss-full-evidence.txt and
bridge-loss-contracts.json. Native SHA256 remains
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Fixes canonical local source is v1.26.4; existing owners remain unchanged.

7EB00 can mark up to two coupled class49 bridges via B9330/C4290 when its
parameter8 is1, before checking whether a class45/46 gatehouse has died. C4290
marks alive state3; C60F0 later consumes state3 through B8310. B8310 calls61FC0,
which dispatches class49 to622D0. Native class49 removal-table value at2E7190
is1. The resulting keep-moat word120 is1:622D0 keeps the moat flag instead of
calling61E70 to clear the moat record. This is a damage/deletion distinction,
not evidence that a separate bridge attack was ordered. Damage SDK arguments
do not expose every native branch flag or the actual return value.

The full log snapshot contains a later separate short process as well. The
explicitly selected historical process (7312 paired commands,359190 completed
native calls) is frozen under historical-20261013-152715. Original full-file
SHA25647F1F1CF57FBA9F72C5AB2A5A0FC325AD9AA7BAC8E865E2790FC876A1ED7D46C;
selected UTF8 process-section SHA256
7FE42449AF3164AA0FE835BB9CDB96C92FBCECB8454BCAA98BF5FC79CDF0C463.
Gate163 lost6HP; bridge494 left the active index; the next completed rebuild
followed. Old logs did not retain the removed footprint, so persistent moat
state is native/user evidence rather than a complete old-log field proof.

Runtime now allows relevant own military entry phases4/5, still two attempts.
Existing unit-command events can copy own unit identity and context target tile;
existing movement Post can supply an explicitly ongoing-state snapshot. These
are not selector results, fresh decisions, or proof of melee execution. Missing
native context targets can use only a remaining, identity-matched deck path as
a geometrical coupling candidate. Consumed path sections never qualify.
Unit/tribe GlobalIds are distinct. Native copied slot window has header65C:
unit Global offset65C+94=6F0; context building tile65C+3A4=A00.
Own unit event inputs, Unknown stack parameter and suppression are preserved.

BridgeLossHistory holds at most128 historical bridge/gate/footprint identities
per session, separate from live topology and permissions. Reuse/coupling changes
do not overwrite old evidence. Fields and state are recorded at relevant index,
damage and completed-rebuild observations. Unchanged field definitions are
referenced; generation/dirty changes alone do not generate duplicate state.
PCL50EC690, flags48F71B0, directions51890D0 and moat-task layer5F011A0 are read
within320800 entries. Moat-task5F011A0 derives from tile root405EDB0+1EA23F0;
it is not the organism layer485A770 used by69560. Historic evidence is never
used to grant or deny live reachability. Scalar snapshots are non-atomic and
cannot authorize negative decisions. Capacity overflow explicitly loses
coverage. Later gate damage remains relevant after its last bridge disappears.

Importer retains legacy artifacts and validates optional unitIdentity4/own
observation origin. Zero roots never constitute an exact causal link. Weak
ongoing-path snapshots are deferred, with one bounded final fallback; stronger
own input can consume the remaining attempt. Two delivered artifact limits,
64 records/64KiB blocks and the durable session-delivered marker remain.

Behavior fix remains disabled. Copied DA020/EDF30 and conservative melee-access
alternatives remain offline-only; full real own unit-command/formations and
effective permission integration still require actual input replay. The resource
errors have no proven Bridge caller and are not suppressed or replaced.

Next targeted capture: same gate-only-access layout; permanently lowered bridge,
closed gatehouse, Gate/Fixes/manual feature active, no own moat work. Observe a
fresh military decision and allow gate hit/bridge loss. Require planning artifact,
own unit-command or explicitly ongoing-state follow input, bridge-loss field/state
records, later gate damage if it occurs, completed topology and both artifact/
session delivery markers. It is a capture run, not behavior-fix acceptance.
