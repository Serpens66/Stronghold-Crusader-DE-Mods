# Material demand audit for an all-goods correction

Read-only native/source audit, 2026-11-22. This document records confirmed
Vanilla contracts separately from implementation options. No game run, runtime
change, installation or build forms part of this audit.

## Identity and evidence

The installed canonical CrusaderDE.dll SHA-256 was recomputed and agrees with
CURRENT.json and its selected database manifest:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Semantic function names remain candidate labels; the field accesses and branches
below are confirmed against the selected complete decompiler bodies. Entry bytes
were independently decoded from the installed PE with Capstone. The selected
native xref export has no reference entering the interior of any proposed
six-byte-Indirect entry displacement below. This does not authorize installation
over an existing owner or replace the actual-backend execution test.

All source-line references below are into
`_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/exports/semantic-decompiled-functions.c`.
Related existing knowledge: `AI_ECONOMY_OBSERVATION.md`,
`AI_RESOURCE_RESERVE.md`, `AI_SELL_THRESHOLDS.md`,
`CONSTRUCTION_COST_TABLES.md` in that baseline's `knowledge` directory.

## Confirmed writers and terminal behavior

| Producer | Native inputs and exact demand semantics | Context needed for a later current-demand check |
| --- | --- | --- |
| CC420, line118375 | `(playerManager, mapper, playerId, showMessage) -> int`; mapper is mapped by C77C0 to a cost row. AI eligibility is multiplayer-active, human marker -1, CPU-enabled. Free-building/world-kind gates and B9710 exceptions bypass ordinary checking. Insufficient iron is tested first, then pitch, then gold. Wood/stone are reached only after those earlier tests pass. Each reached demand raises the cell with `max(existing, deficit)`, wood using `cost-stock+2`. Corresponding suppress-sell-all timer is set to8 on the audited paths. Sufficient resources return success without clearing old demand. | The live cost row and relevant original mapper; demands from other material users must also survive. A successful cheaper check cannot cancel a more expensive outstanding check. CC420 does not receive coordinates or a unique construction-intent identity. |
| 2AA20, line25121 | `(aiManager, playerId) -> void`; active AIC/raw3974 gate. Stock<1 increments scarcity probes, stock>=1 resets them; corresponding enable fields can suppress probes. With custom-purchase-enable gate, probe>36 emits wood5/iron2/flour2, probe>72 emits hops2, only if pending==0. These are fixed batches from a zero-stock condition, not additive tickets. | Enabled probe, current stock and AI state are available, but a later nonzero cell may also belong to building/food/emergency/Fixes sources. |
| 2EAC0, line27970 | `(aiManager, playerId) -> void`; active AIC/raw3974, gold>29, valid engineer-guild/smelter IDs and types, requested engineer/group count. If smelter-local pitch<1 and global pitch<1, assigns pitch4 with no pending-zero guard. Otherwise the same routine may create/assign the smelter engineer. No cancellation of pitch demand occurs. | Original smelter ID plus stable identity/type/owner and its local supply; availability of the actual required engineer/group. Global stock alone is insufficient after local delivery or smelter removal. |
| 2F080, line28151 | `(aiManager, playerId, fullUpdateByte) -> void`; stance computation also executes in its partial mode. Only full mode reaches the response path. An active response counter at player38EC and successful2B490 predicate assign wood20, then response-specific demolition through2D810 and counter progression. It is a fixed batch, not a20-stock target. | The response state and2B490's live prerequisites, including building/resource/wealth conditions. The producer itself changes stance, PRNG and demolition state; it cannot safely be rerun merely to ask for fresh demand. |
| 51790, line46352 | `(aivState, playerId, frameIndex, restrictedMode, freeOrForcedByte) -> int`; particular wall-like subtype, previous-build state3 and expired retry timer, then stone<1. Emits stone5 only if pending<=0; if stone>=1 proceeds to placement loop. Earlier ordinary-building paths can call CC420. Success and retry-state/timer updates do not clear pending. | Original AIV slot/frame/type/state/timer and whether its actual construction remains outstanding. Stone can disappear into successful construction while stale pending survives. |
| D5710, line124655 | `(playerId, unitId) -> void`; if stone>=10, removes10 via B7D80 and adds20 to the unit's local ammunition counter. If stone<10 and CPU eligibility holds, assigns stone10. No pending-zero guard and no cancellation after successful refill. | Original unit's stable identity, owner/type/alive state and local ammunition requirement. This producer deliberately requests a full10 even at stock1..9; changing that to10-stock is a separate batch-policy change, not proof of stale-demand repair. |

The source batch constants alone do not establish an absolute-stock target. In
particular, initial stone9 and D5710 Pending10 are a valid fixed-batch emission;
claiming that9 of those10 were already stale would change Vanilla's batching.

## Closed material data/control flow

CC420's complete direct caller set is12090,138D0,50D80,50E00,50F90,51190,
51270,51540,51790,52270,55270,59730,8BD60,90CD0. These cover executed placement
chores, economy placement attempts, multiple AIV frames and human presentation/
input checks. They are not a single globally replaceable building request.

Relevant entry lines:12090=11446,138D0=12115,50D80=45964,50E00=45987,
50F90=46041,51190=46101,51270=46128,51540=46217,51790=46352,
52270=46703,55270=48886,59730=51395,8BD60=81935,90CD0=84287.
55270 traverses several not-yet-built AIV frames and returns on the first
failed resource check;52270 also scans multiple possible frames. A later
CC420 call can refer to a different requirement without replacing an older cell.

C77C0 aliases mapper/menu values to real native building-cost rows; native row
zero is67E5B8C with110 five-Int32 rows (wood,stone,iron,pitch,gold). CC420's
resource-manager base/offset form addresses the same per-player pending cells
as the direct-global writers: Pending379D840, stride583C, good index0..24.
The successful construction charge routine C8A90 (line115625) removes the actual
resources via B7D80, including free-build/alternate/pitch grouping branches,
but does not reconcile pending quantities.

2AE40's scheduler invokes2EAC0 and2F080 in the corresponding player phases;
2AA20 is reached separately from57330. The emergency predicate2B490
(line25520) considers live building/resource/wealth conditions; demolition2D810
(line27166) refunds through C4480 then marks buildings through C4290.
Those side effects must not be duplicated by a revalidation pass.

D5710's callers are18AE0 (refill chore),1520D0,1535F0 and18FC00. The latter
three request automatic refill when local ammunition is exhausted. Their source
entries are lines193512,194201,227180. The alternate group refill D55B0
(line124610) can refill the same unit using available stone, including partial
refills, without clearing the AI purchase cell. Therefore an originating ammo
need can be fulfilled even when global stone is subsequently zero.

The terminal pending consumer3EC90 forwards the currently stored amount to29650
and clears the cell only after successful purchase. It does not reconsider the
above originating contexts. Stock addition, production, recount and removal are
separate from pending cancellation (complete existing baseline audit). Live
sales compute their quantities separately; no chronological immutable trade
tickets were found.

## Concrete counterexamples and attempted falsification

- Initial wood12, construction cost20: CC420 emits10 including padding2.
  `max(0,pending-currentStock)` wrongly cancels even with no supply change.
  B7D80/C8A90 and the successful CC420 branch contain no compensating rewrite.
- A fixed goal22 for the same wood demand still buys2 when stock reaches20
  and the building is already affordable. Exact material logic is piecewise:
  stock>=cost means no shortage; otherwise cost-stock+2 for wood. The padding
  cannot be treated as a necessary resource after the actual shortage disappears.
- An expensive AIV/economy check and a cheaper subsequent check share a cell.
  Replacing the expensive target with the latest target, or globally clearing on
  one success, can underfund the expensive requirement. The max-only native
  updates and multiple-frame callers refute the single-live-intent assumption.
- A delivered batch can satisfy a construction or refill, then be consumed.
  At purchase time stock may again be0 while old pending is still positive.
  Comparing only current stock with an old absolute goal cannot distinguish this
  completed intent from a still-live requirement after unrelated consumption.
- Repeated producer assignments can have exactly the same numeric value. A
  before/after cell comparison cannot detect those writes or identify ownership.
  Fixes custom wood/iron/ale assignments and public SE setters add foreign writers.
- A save preserves cells but not a testmod's in-memory provenance. A loaded
  positive cell cannot safely be attributed to the numerically matching writer.

## Entry ABI, ownership and integration limits

Windows x64 argument order is RCX,RDX,R8,R9 and subsequent stack arguments.
The signatures in the writer table follow complete parameter use; small-byte
arguments must remain byte-sized. Independently decoded entry spans for the
six-byte Indirect scheme are:

| RVA | Complete displaced bytes | Length | Existing ownership seen in current sources |
| --- | --- | --- | --- |
| CC420 | 48 89 5C 24 20 44 89 44 24 18 |10| Spectator entry; PitchMoatLines also has call-only access |
| 2AA20 | 40 53 56 48 83 EC 38 |7| BFQoL AIEconomyProtectionHook entry |
| 2EAC0 | 40 57 41 54 41 56 |6| No exact entry ownership found in searched SE/Fixes/APIShared/production sources |
| 2F080 | 44 88 44 24 18 53 |6| No exact entry ownership found in searched sources |
| 51790 | 40 53 55 56 57 41 54 |7| APIShared AivBuildStep capability |
| D5710 | 48 89 5C 24 08 57 |6| No exact entry ownership found in searched sources |

Spectator's AvailabilityObservation is a mod-private observation seam with a
per-observed-call closure. APIShared AivBuildStep observers allocate context/
invocation/list state when used. Neither is automatically an allocation-free
permanent tracker. BFQoL2AA20 runs its original then clears sleep requests;
its entry cannot be taken by an independent market detour. Fixes' purchasing
extension is inside2A420 and writes wood/iron/ale cells; it must not be treated
as a Vanilla demand merely because those cells also have Vanilla writers.

## Design conclusions, not an implemented correction

Supply reconciliation and exact current-intent recomputation are different
contracts. Capturing original stock+deficit, retaining a fixed target over
reductions, and preserving max concurrent goals can repair a proven delayed
supply case. It cannot establish that an old building/smelter/emergency/AIV/
ammunition intent is still wanted, or that a numerically identical foreign
assignment has not replaced it. Nor may a fixed batch automatically become an
absolute inventory target.

An exact all-intent correction needs origin identity and completion/cancellation
information for each relevant producer, or a new authoritative per-player demand
calculation reproducing all current construction, economy, recruitment, food,
scarcity, smelter, emergency and ammunition gates. No such pure native query is
established. Calling the existing producers as a dry run is unsafe because they
mutate counters, PRNG, production, recruitment and demolition.

Without that additional contract, unknown loaded/foreign demand must remain
unchanged and the correction's claimed scope must be supply reconciliation,
not all incorrect quantities or all canceled intents. This limitation is an
information-loss finding, not an ingame-frequency claim or an authorization to
change purchasing policy.

## Accepted narrower contract and store-only instrumentation map

Subsequent coordinating-task decision: preserve the original package quantity,
credit later supply (request5, later supply1 means remaining4), and leave unknown
save-loaded/foreign demands untouched. This is bounded supply reconciliation;
the earlier exact-intent limitations remain. It does not claim canceled-intent
reconstruction or change the stone-at-stock9 fixed10 package into a1 purchase.

The following13 individual MOV instructions were decoded from the installed PE.
Every single-instruction span is at least6 bytes. The CURRENT-selected xrefs
contain no incoming target strictly inside any listed single-instruction span;
all13 instructions have ordinary fallthrough, no branch and no RIP/RSP-relative
operand. The whole containing functions were decoded for branch-target checks.
The actual RedBird Indirect backend must still prove that it displaces exactly
the specified single instruction, and live ownership/byte checks remain mandatory.
An arbitrary mid-function native stub must not be treated as a function with the
producer's entry ABI: live registers, flags, stack and SIMD state need preservation.

Here P means the effective pending-cell address, S is signed Int32 at P-25A0,
and Q is the signed Int32 being written. The displacement25A0 is independently
confirmed by global379D840-379B2A0 and CC420's relative131638-12F098.
An emission's package baseline is the original S+Q, not Q-S. For safe indexing,
validate P against actual Pending base + player*583C + good*4 and player1..8;
do not infer a player from an arbitrary single register's numeric range.

| Store RVA | Full original hex / length | Exact P and Q | Good / package meaning |
| --- | --- | --- | --- |
| CC555 | 44 89 A3 48 16 13 00 /7 | P=RBX+131648; Q=R12D | iron6; original cost-stock |
| CC5C1 | 44 89 AB 4C 16 13 00 /7 | P=RBX+13164C; Q=R13D | pitch7; original cost-stock |
| CC657 | 89 BB 38 16 13 00 /6 | P=RBX+131638; Q=EDI | wood2; original cost-stock+2, branch also lacks stone |
| CC692 | 89 BB 38 16 13 00 /6 | P=RBX+131638; Q=EDI | wood2; original cost-stock+2, sufficient-stone branch |
| CC6E4 | 89 B3 40 16 13 00 /6 | P=RBX+131640; Q=ESI | stone4; original cost-stock |
| 2ADB5 | 42 C7 84 33 38 16 13 00 05 00 00 00 /12 | P=RBX+R14+131638; Q=5 | scarcity wood2 |
| 2ADD7 | 42 C7 84 33 48 16 13 00 02 00 00 00 /12 | P=RBX+R14+131648; Q=2 | scarcity iron6 |
| 2ADF9 | 42 C7 84 33 70 16 13 00 02 00 00 00 /12 | P=RBX+R14+131670; Q=2 | scarcity flour16 |
| 2AE1B | 42 C7 84 33 3C 16 13 00 02 00 00 00 /12 | P=RBX+R14+13163C; Q=2 | scarcity hops3 |
| 2EB9C | 43 C7 84 20 5C D8 79 03 04 00 00 00 /12 | P=R8+R12+379D85C; Q=4 | smelter pitch7 |
| 2F56E | C7 84 28 38 16 13 00 14 00 00 00 /11 | P=RAX+RBP+131638; Q=20 | emergency wood2 |
| 51ADB | 42 C7 84 11 40 16 13 00 05 00 00 00 /12 | P=RCX+R10+131640; Q=5 | AIV retry stone4 |
| D5791 | 41 C7 84 38 50 D8 79 03 0A 00 00 00 /12 | P=R8+RDI+379D850; Q=10 | ammunition stone4 |

At all five CC420 writes, RBX is the player-manager base plus player stride.
R12D/R13D/EDI/ESI have already become deficits; capturing their original costs
would require S+Q (minus the known wood padding2), not reading those registers
as absolute costs. S+Q is sufficient for the accepted package-preserving scope.
For raw global forms, one addend is the image base and the other player stride;
using the full effective address avoids swapping them. At2F56E RAX was just
loaded with the player-manager global and RBP is the player stride. The scarcity
and AIV forms similarly use manager base plus player stride.

Do not use RedBird X64InlineHook with its14-byte minimum at every listed store.
The resulting spans have incoming interior branches at CC657 (targetCC65F),
CC6E4 (CC6EA),2ADB5 (2ADC1),2ADD7 (2ADE3),2ADF9 (2AE05),
2AE1B (2AE27),D5791 (D579D). A store-only6-byte-Indirect contract avoids
those interiors, provided the backend truly displaces only that single MOV.
CC555/CC5C1/CC692 and the remaining larger spans do not have that particular
interior-target defect, but still require the same live-state and backend checks.

## Fixes custom-source boundary correction

The optional Fixes custom wood/iron/ale source is injected at2A4D8. It executes
its three flag/stock/threshold checks before replaying the displaced19 bytes.
That replay includes `JLE2A4FC` when the native hops threshold is nonpositive.
Consequently2A4EB is NOT a complete post-source observer: the custom orders
have already been assigned even when that continuation is bypassed. A generic
machine test of the2A4EB conditional does not prove complete custom attribution.

The chosen guaranteed-future-write boundary is2A42A..2A431, the single
`IMUL R8,RAX,583C` (`4C 69 C0 3C 58 00 00`,7 bytes). RAX is the sign-extended
player ID. No selected xref enters its interior. The subsequent AIC-zero branch
exits directly; with a nonzero AIC all food-condition paths join2A4D8. The
complete221-byte Vanilla function contains no CALL or stock write, and the
recognized complete Fixes stub changes only pending cells and scratch registers.
An adapter at2A42A may therefore register the package implied by each current
enabled custom flag and signed `stock<threshold` predicate, including repeated
same-value assignments. It must ignore AIC-zero and nonpositive package amounts;
it must not require Pending already equals the future assigned amount.

`MarketFixesDemandSource.DevDiag.cs` verifies the actual published custom stub,
its live19-byte patch and live array addresses instead of trusting a Fixes version
or reflection names alone. Initialization checks the entire live2A420 body
against the load image except that recognized Fixes patch, before our own relays
are installed. The caller seals the expected body only after its own audited
relays have been published and validated; mission rechecks compare the sealed
body and custom stub. Unknown Fixes shapes fail closed. Source existence remains
conservative when reflection cannot establish whether custom writes are active.

The sole indexed native caller is2AE40, its direct call at2AF52 being
`E8 C9 F4 FF FF`. A full wrapper of that call is an alternative before/after
boundary, not the implementation selected here. No new call-site hook is implied.
