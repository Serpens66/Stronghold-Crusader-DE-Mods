# Completed stock publication and missed fulfillment

Audit dated2026-11-22, canonical installed native SHA-256 verified against CURRENT:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
This is a static native/source audit, not acceptance of a new in-game hook.
Function names without curated symbols remain candidates; machine accesses,
branch targets and argument/stock flow below were independently checked.

## Why a purchase-time snapshot is insufficient

Example: wood stock10, captured pending12 including the construction padding,
package target22. Another supply raises physical stock to22, then construction
uses20, leaving2 before the next market phase. A market-only snapshot misses the
completed package and may buy the old12 again. A completed-stock publication
observer can mark the owned package fulfilled at22 before consumption resumes.
This records supplied packages; it does not rerun or cancel construction intent.

## Coverage and method

Used CURRENT.json and its selected database manifest, full bodies of the storage
add/capacity/distribution/recount/removal routines,39 direct non-gold TotalGoods
global-reference users, selected High-P-code write/address facts, and a second
machine-operand scan of the current PE's function ranges (436086 decoded
instructions). Direct/manager-relative offsets and computed-pointer aliases were
crosschecked; a displacement-only scan would miss B6100 and C95D0. Scan coverage
alone is not the proof: the complete relevant producer bodies and their terminal
calls below close the observed normal routes. Session initialization/load copies
and unknown mod direct-pointer writes remain separate from ordinary native supply.

TotalGoods are signed Int32 observations despite unsigned SDK field declarations.
Direct base379B2A0 and player-manager-relative12F090 describe the same array;
player stride583C, goods0..24. Gold is good15. Positive stock publication must be
read after the full recount, not during its zeroing/partial sums.

All line references below are into
`_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/exports/semantic-decompiled-functions.c`.

## Complete identified normal TotalGoods writer set

| Function | Role and sign/terminal contract | Publication route |
| --- | --- | --- |
| AED70, line100367 | Incoming-resource delivery into stockpile. Eight direct increment sites for goods2,3,4,6,7,9,14,16, balanced by incoming-amount decreases and local-storage increases. | Any committed batch calls C1CE0 before returning; its capacity checks do not consume goods. |
| AF210, line100533 | Incoming equipment delivery into armoury for goods17..24. Local storage is incremented; good24's intermediate TotalGoods assignment is1, not a reliable final total. | A committed batch calls C1CE0 before presentation continues. Final recount supplies the actual total, including existing goods. |
| B23F0, line102432 | Incoming food delivery for goods10..13 into granary; adds one to local/global stock and decrements incoming amount. | A committed batch calls C1CE0 before later granary presentation/accounting. |
| B58B0, line104270 | Public/native add-resource operation. C5AD0 checks capacity; BFD30 maps the good to storage kind. Positive ordinary goods are distributed via B5E90/B6000/B6100. A direct TotalGoods increment exists only for kind40. | All traded non-gold goods use B6100/C1CE0. The direct kind40 branch is Gold15 only; it is not an uncovered traded-goods bypass. |
| B6100, line104540 | Seven-argument local-storage update. Checks building global identity, resulting signed local amount in0..capacity, and bAdd. For committed storage kinds10/11/19 updates TotalGoods by signed amount; then presentation and C1CE0. A capacity0 local reset is a distinct branch without an immediate TotalGoods increase. | Every committed ordinary positive storage delivery reaches C1CE0 before returning. Negative adjustments also reach it; no positive commit bypasses it. |
| B7D80 | Shared subtract-resource primitive. Ordinary physical paths subtract existing local/global resources and recount. No-storage fallback sets total0. Gold and native world-kind6/direct-account branches are separate. Negative input can reverse ordinary arithmetic but still reaches the same physical recount. | No ordinary positive non-gold bypass; later C1CE0 publishes the resulting physical total. |
| B8460, line105942 | Storage subtraction during terminal building deletion, called from B8310. Subtracts/clamps the storage building's contribution for all goods, then C1CE0. | A loss route, not a new supply source; final C1CE0 is observed. |
| C95D0, line116062 | Explicit storage emptying, called from80770. Subtracts/clamps global contributions, zeros local goods and calls C1CE0. Its TotalGoods pointers are computed aliases. | A loss route; final C1CE0 is observed. |
| C1CE0, line111146 | Rebuilds one player's TotalGoods from all alive2 storage buildings of owner==player. Storage types10/11/19 determine the appropriate ranges. Saves/restores gold. No CALL instructions; no consumption within the recount. | Own final marker C1F4A, before returning to any caller. |
| B97B0, line106683 | Scheduled all-eight-player recount, called from57330. Preserves gold, clears complete player totals and sums alive2 storage types10/11/19. This closes raw-local-storage changes that become visible through periodic recount. | Own final marker B9A30, after all players' final sums/gold restores, before security-cookie call and return. |

These ten functions comprise the identified non-gold writer set. The high-level
normal-supply conclusion is: every identified non-gold increase is published at
completed C1CE0 or completed B97B0 before its caller can perform subsequent
consumption. No identified normal non-gold restore/increase bypass remains.
This is not a claim to intercept arbitrary external writes or loaded bulk state.

### Distribution and capacity closure

C5AD0 is a capacity query: it scans matching alive storage, returning capacity,
or1000000 for kind40. It does not itself add or remove goods. B5E90 distributes
equipment using C6EA0 capacity checks and committed B6100 calls; B6000 distributes
food using BD470 capacity checks and committed B6100 calls. B58B0 distributes
stockpile goods through B6100 as well. Failed/no-op adds are not assumed successful
from their requested quantities; only final physical totals can fulfill a package.

The actual25-entry BFD30 storage-kind table at2E76D0 was read from the installed PE:
`0,10,10,10,10,10,10,10,10,10,19,19,19,19,10,40,10,11,11,11,11,11,11,11,11`.
Thus Gold15 is the sole40 entry. Every good in the native AI purchase-priority
table maps to ordinary10/11/19 storage, except trailing Null0 slots.

### Why subtraction hooks alone are incomplete

B7D80 covers construction/recruitment/general removal, but B6100 signed local
adjustments, B8460 terminal storage deletion, C95D0 clearing and raw-local changes
followed by recount can reduce totals elsewhere. Hooking only B7D80 cannot prove
that a package never reached its goal before another consumption route.
Conversely, observing the two final publication boundaries captures normal supply
before any subsequent removal regardless of which loss path is later used.

PRE-C1CE0 alone is insufficient: it can see old or intermediate totals, notably
AF210's metal-armour intermediate assignment1 before the complete physical sum.
Intermediate zeroing/sums within either recount are never fulfillment evidence.

## Minimal final-publication hooks

### One-player C1CE0

ABI: `void(BuildingManager* manager, int playerId)` in RCX/EDX at entry.
The6-byte Indirect entry displacement is10 bytes:
`48 89 5C 24 08 48 89 6C 24 10` (two stack-save MOVs).
No selected xref enters its interior. A post-original entry wrapper would execute
before its native caller resumes, but a lower-level final marker avoids an extra
managed wrapper/original call and its entry ownership question.

Chosen final marker C1F4A..C1F52,8 bytes:
`42 89 AC 36 CC F0 12 00`, `MOV [RSI+R14+12F0CC],EBP`.
This restores gold after all non-gold sums. RSI remains playerId*583C and R14
the player-manager base. At the marker, the corresponding non-gold totals are
already final and stable. Gold preservation is left to the original instruction.
Every function path joins C1F40, executes two register restores and reaches this
marker; the no-buildings branch is included. There are no direct/xref targets
strictly inside this8-byte single instruction. It has no RIP/RSP addressing or
branch. The callback must preserve its operands and execute the MOV exactly once.

Complete direct caller functions: AED70,AF210,B23F0,B58B0,B6100,B7D80,B8460,
C95D0,F82B0. B58B0 has two call sites. C1CE0 contains no internal CALL or
implicit goods consumer. Its last non-gold add is C1F1E, followed by the loop
continuation or shared completion, never another consumer before the marker.

### All-player B97B0

ABI at entry: `void(BuildingManager* manager)` in RCX. Its entry starts with a
stack allocation followed by a RIP-relative security-cookie load, so the simple
non-RIP Indirect-entry verifier is not suitable without additional relocation work.

Chosen final marker B9A30..B9A38,8 bytes:
`48 8B 4C 24 28 48 33 CC`, `MOV RCX,[RSP+28]; XOR RCX,RSP`.
All eight players' non-gold totals and restored gold are final at this point.
The remaining native operations are security-cookie validation and return; no
goods mutation or consumer remains. No selected xref targets the interior of
this span. The first instruction is RSP-relative: the relay must restore EXACTLY
the original stack pointer before executing the trampoline. The original XOR
must still produce its normal RCX/flags for the subsequent security-cookie call.

Do not use B99D6 as a supposedly universal marker: the no-buildings branch at
B983A jumps to B99E0 and bypasses it. That branch still reaches B9A30.

### Ownership and existing events

Current local SE/Fixes/APIShared/BugfixesAndQoL/Shared source searches by these
RVAs and recount roles found no owner of C1CE0/C1F4A/B97B0/B9A30. In contrast,
B58B0 and B6100 entries are owned by the Script Extender and must not be taken.
Actual live spans remain authoritative: installation must compare exact bytes,
claim the sites and fail closed on any unexpected owner; mission rechecks must
detect later overwrites without repatching published hooks.

Existing public events were checked first. SE's player-resource and goodsyard
Pre/Post wrappers allocate event arguments; their Post event metadata preserves
original requested arguments rather than necessarily the modified actual Pre
arguments. Requested amount/return flags alone do not prove final physical supply.
APIShared ProductionGoodsReceived reports genuine completed production but excludes
gifts/refunds/market supply, so it cannot alone close this all-source question.
The two final native boundaries cover those normal physical routes without a
second set of per-delivery managed receipt allocations.

## Own AI purchases and attribution

3EC90's complete native callees are price CEB10, actual buy29650, and ally-request
300F0. The latter updates request cells;3F7F0 checks Lord presence187E60 and
emits presentation103AA0, not an immediate transfer.29650 adds only the selected
good through B58B0, then books gold/statistics/presentation on success. There is
no other Vanilla supply or goods consumption inside that decision invocation.
3EC90 clears the purchased pending cell after the successful buy returns.

Therefore the fix must exclude receipts inside its active original3EC90 scope
from its avoided-purchase/fulfilled-package counters; those are the AI's own normal
purchases, not a prevented unnecessary purchase. AfterDecision observes native
clearing and final stock instead. External/manual purchases or normal production/
gifts outside that scope remain valid physical supply. Foreign reentrant code
inside a market callback is not established by this Vanilla audit.

The ordinary publication callback can inspect only active owned goals for the
affected player, and the global marker can inspect active goals across eight
players. AIC hashing/identity validation is needed only for a candidate transition;
there is no reason to hash every AIC on every normal delivery. Bounded numeric
state and a native no-demand gate can keep unused observer overhead small.

## Remaining boundaries

- Save/load and initial bulk copies are lifecycle transitions, not receipts;
  discard old provenance rather than inferring ownership from a loaded number.
- Unknown mods may write native pointers directly without invoking either recount.
  Such arbitrary code is outside the registered native-flow guarantee.
- Native pre/post publication and code preservation need executable backend tests;
  static byte/xref audit is not performance or real-game acceptance.
- Full in-game reproduction must include supply reaching the complete package
  target and immediate construction/refill consumption before the next market
  decision, plus own AI buys that must not inflate avoided-purchase counters.
