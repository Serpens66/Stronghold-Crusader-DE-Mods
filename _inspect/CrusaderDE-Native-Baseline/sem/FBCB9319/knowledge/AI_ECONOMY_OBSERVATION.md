# AI economy observation boundaries [Scope: Vanilla + mod integration]

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Static audit dated 2026-10-16; runtime acceptance is outstanding.

The complete function at RVA 0xCCBB0 takes one player-manager pointer and iterates
player IDs 1..8. It accumulates through 0xCB9A0 and 0xCBCD0, then at settlement
normalizes by ten, credits income, deducts expenditure, applies a zero floor and
resets accumulators. Installed Extender tax events at 0xCCC82 and its expenditure
site are Pre notifications; they do not independently prove a completed booking.
An all-player before/after observation measures the actual net gold change without
invoking the native function again. Confidence: high for the complete data flow;
unconfirmed internal function names remain candidates.

AI controller 0x3F270 requires the player's market and alternates sales with purchase
processing 0x3EC90. The latter reads pending per-goods requests and emergency/
popularity gates before asking the shared price helper and attempting 0x29650.
Purchase changes gold only after stock-add succeeds; 0x29700 handles sales separately.
Price helper results alone never prove a purchase, sale or ally transfer.

Accessibility sweep 0xC8F50 propagates state 3 to a shared nonzero building-group
dword at GameBuilding+0x2A8 (currently N0000178A). This address derives from the
alive-state pointer +0x1D8 in the actual loop, not the field's placeholder name.
Marking and low-level delete entry are distinct from eventual slot zeroing/reuse.
Unavailable object lookup is not removal evidence. Prior classification and the
actual deletion-call context must remain distinct from an inferred causal history.

The diagnostic implementation and per-entry ABI/span/update ledger are maintained in
Testmods/AIBuildDiagnoseTest/UpdateToNewDLL.md. Machine entry evidence is retained in
_inspect/AIEconomyDiagnostic-20261016/native-entry-audit.json. Existing APIShared price
helpers and BugfixesAndQoL protection entries retain exclusive hook ownership.

The user retains the installed BugfixesAndQoL 1.0.192 unchanged. Its nine-member
IAiBuildDiagnosticSink contract and InternalsVisibleTo(AIBuildDiagnoseTest) are
verified against that actual assembly. It has no added per-decision economy
publisher. The native facts above do not imply those protection decisions or the
hovel-call context are observed: the diagnostic reports that coverage unavailable.
Only current settings/support/published-hook status are reflected; none proves a
specific applied decision or historical protection. Spectator 0.1.2 supplies its
own independent protected resource-check observer seam, without a new Bugfix API.

## Pending-purchase producers and priority, 2026-11-22

Rechecked against the same current native hash. Sources: complete functions in
`exports/semantic-decompiled-functions.c`, CURRENT-selected SQLite
`function_data_references`/xrefs for RVA379D840..379D8A0, and the manager-relative
CC420 accesses. The following table covers the confirmed demand writers in
that feature path; initialization/load copies and external mod/API writes are
separate. Indexed aliases cannot be excluded merely by a direct-xref search.

Pending goods are signed32 cells, player-record offset2A70, stride583C,
good index0..24. Direct global base is RVA379D840; manager-relative base is
offset131630. These are quantities, not a proven chronological ticket list.

| Writer RVA | Trigger / goods | Write contract |
| --- | --- | --- |
| 2A420 | Active AIC; fruit13 below buy_apples_at (threshold>=0); cheese11/bread10/wheat9/hops3 below their corresponding positive AIC thresholds | Assign AIC.buy_food_amount to that cell, even when an older amount is nonzero; no += |
| 2AA20 | raw3974!=0, sustained zero-stock probes and probe-enable gates; wood2/iron6/flour16 after counter>36; hops3 after counter>72 | Only when corresponding pending==0: assign wood5, iron2, flour2, hops2 |
| 2EAC0 | Existing eligible engineer/smelter path, active AIC/raw3974, gold>29; smelter local supply<1 and player's pitch7<1 | Assign pitch7=4, no pending-zero guard |
| 2F080 | Emergency-response branch: player38EC>0 and successful 2B490 predicate, after stance/gold/unit context processing | Assign wood2=20, no pending-zero guard |
| CC420 | Actual building resource check, AI eligibility gates; insufficient iron6/pitch7/wood2/stone4; earlier resource/gold checks determine reached branch | Raise pending to missing deficit only if larger than its current value; wood deficit includes +2. Does not append a ticket. Sets respective suppress-sell-all timer8 on the audited branches |
| 40230 / 40430 | Failed bodyguard/economy-protection recruitment, failure reason2 and positive buy_weapons | Assign buy_weapons to reported missing-good cell; no += |
| 40740 | Failed combat recruitment with missing resource | Assign AIC buy_weapons, or5 for the audited LastStand branch, to missing-good cell; no += |
| 51790 | AIV construction subtype branch, retry timer expired and stone4<1 | Assign stone4=5 only if existing pending<=0 |
| D5710 | Stone-consumption/top-up path, current stone4<10 and native AI eligibility gates | Assign stone4=10; otherwise when stock>=10 remove10 stone and increase the unit's local counter by20 |
| 3EC90 | Successful29650 return for selected good | Clear that cell to0 after the buy function returns; this is consumption, not a demand producer |

For2AA20, zero probe-enable fields2B88/2B8C/2B90/2B94 suppress the respective
wood/iron/flour/hops zero-stock counters. Counter thresholds count this probe's
updates, not raw map ticks. Earlier narrow descriptions mentioning only wood
and iron were incomplete: flour and hops have confirmed small-order writers.
No equivalent fixed ale order is established by this function. The food/hops
AIC writer can overwrite an older amount; other listed assignments can also
replace or lower a cell. Repeated assignment alone does not accumulate trades.
Fixes' missing-horse validation and custom purchase routines remain separate
integration behavior; do not attribute their writes to an unmodified producer.

Complete audited purchase priority table (RVA2CED30), in order:
10,11,12,13,9,2,4,6,7,3,14,16,17,18,19,20,21,22,23,24,0,0,0,0,0.
3EC90 scans from the beginning on every call and returns after its first
successful purchase. LastStand>0 skips the first12 table positions. Otherwise
purchase gate is: first-four food positions with popularity<vlow_popularity,
or LastStand>0, or raw3974!=0; market access, pending and affordability still
apply. Gold equal to the quoted cost suffices. A failed affordable stock-add
does not itself prove a completed transaction.

This permits a specific starvation hypothesis without duplicated orders: a
small earlier-priority good can repeatedly become deficient and be refilled
before a later weapon cell is processed. A controlled test can alter physical
stock through SE.GetGoodAmount/RemoveGood and custom AIC thresholds/batch size,
while leaving every pending cell untouched. Example: buy_bread_at=1,
buy_cheese_at=1,buy_apples_at=1,buy_food_amount=1, enough gold/market/storage,
normal popularity and a confirmed raw3974 gate. Remove one available unit of
each selected good per completed buy phase and log the actual physical loss.
Control with the same consumption disabled, or the same consumption rate and
larger batch. A naturally created later weapon request should be delayed only
under sustained earlier demand, then complete after consumption stops.
That would establish priority starvation; it would not yet establish the
reported repeated-order overpurchase/sale defect or authorize a particular fix.

## Pending demand cancellation/reconciliation audit, 2026-11-22

The current cell is not necessarily the current need. Complete3EC90 reads the
cell at processing time and forwards that signed quantity unchanged to the price
helper and29650. A producer that replaced4 with2 while waiting therefore causes
2 to be processed, not an immutable older4. However, neither3EC90 nor29650
recomputes the construction/recruitment/food requirement from current stock.
29650 validates signed quantity, prices and storage-add success before charging;
it does not cap by fresh AI demand. Successful3EC90 then clears that cell.

Cancellation counterexamples checked against the complete producer bodies:

- 2A420: when stock now meets the configured food threshold, the corresponding
  write is skipped; no else-branch clears an already queued purchase.
- CC420: sufficient current building resources return success without cancelling
  a previously raised deficit. Deficit writes only raise existing quantities.
- 40230/40430/40740: successful recruitment and fulfilled target branches have
  no corresponding pending cancellation. A later failure can assign another
  missing-good request while an older good's cell remains.
- B58B0/C5AD0/B5E90/B6000/B6100/C1CE0/B7D80: complete add/production-storage/
  recount/removal paths update physical/local storage and TotalGoods; no pending
  reconciliation was found. C1CE0 resets/sums TotalGoods379B2A0..379B300 while
  preserving gold379B2DC; Pending379D840 is a separate area.

Thus other supply can satisfy a requirement without automatically invalidating
its recorded request. This establishes a stale-demand-capable native path, not
yet the size/frequency of avoidable losses in an actual match. An individual
good can also remain legitimately wanted after one successful recruitment;
target completion must be observed rather than inferred from stock movement.

A focused physical-supply fixture can hold a naturally generated request behind
earlier food purchases, supply its goods through normal storage addition, allow
the relevant need to be fulfilled, then release the earlier demand. Record both
the original request and actual fulfillment before checking whether3EC90 still
purchases the unchanged quantity. Do not write Pending in this fixture. A
Vanilla-compatible candidate profile is Baldwin buy5/sell5/minimum2: if one
limited required troop consumes one of five supplied weapons while queued5
remains, stock4 plus a later stale buy5 becomes9, above the ordinary reserve7;
an ensuing ordinary sale4 would leave5. This is an unexecuted reproduction
prediction, not a reported observed outcome.

A generic pending-minus-current-stock replacement is not justified: CC420's
quantity is a deficit relative to the stock at request generation, rather than
an absolute target. Correct reconciliation requires the originating demand
semantics or sufficient provenance. No fix is established by this static audit.

### Controlled runtime stale-demand reproduction, 2026-11-22

The separate testmod's baseline run1bbae8516d624cb6ac1432b3bc250d4e reproduces
the stale path with a custom profile using normal weapon batch/sale/minimum
5/5/2 and recurring small food requests, sufficient gold and physical supplies.
No Pending cells are written by the fixture; correction remains disabled.
Native40740 emits a zero-stock leather23 request5 at tick221044/sequence1041.
At221051 the physical Gift adds10 leather, leaving gold and Pending5 unchanged.
At222094 the native buy still purchases5 although stock is7, producing12 and
debit125. At222144 the native sale removes6 from stock11, leaves5 and credits72.
Complete detail flags0; normal popularity/LastStand/sell_all conditions explain
the sale. This is a controlled native-behavior reproduction, not a measured
natural-match frequency or proof of chronological duplicate tickets.

Archive `.work/a/market-backlog/002/1bbae8516d62/LogOutput.log`, lines2290/2319;
complete source/transaction chain and limitations in
`.work/a/market/001/stale-baseline-assessment.md`. Runner PASS/restoration
verified;960 native observations,104 buys/100 sales, zero capture-health errors.
Intervening net stock decreases are observed, not individual recruit counts.
Economic and unchanged-behavior acceptance of a proposed correction require
the separate matched fixed run; this baseline does not establish a release fix.

### Narrow corrected-run acceptance, 2026-11-22

Run5f535a4b2e4f4c37b6d5d956816e59fc subsequently accepts the test-only
provenance-bound equipment correction against the same source221044/1041,
player2/good23/AIC32/zero-stock batch5. Physical Gift10 is unchanged. After
delivery the policy reduces that owned Pending5 to0 at stock10, while original
3EC90 still executes once. The next actual purchase of this good occurs only
at stock0, taking the normal full batch5; baseline instead bought5 at stock7.
The instrumented excess-purchase count changes5->0 and the same-good ordinary
zero-stock purchase remains enabled. No claim is made about other writers or
raw-material deficit semantics.

Archive `.work/a/market-backlog-fixed/003/5f535a4b2e4f/LogOutput.log`,
lines2291/2297/2419/2431;958 native callbacks,102 buys,100 sales, no capture-health
errors, final output drained, runner PASS/restoration verified. Full paired
proof and limitations are retained in the assessment referenced above. Two
earlier fixed starts blocked before mission entry by a Workshop prompt are
excluded. This is controlled in-game test acceptance, not a general release
or a natural-frequency/performance guarantee.

## Exact pending STORE sites and completed stock publication, 2026-11-22 [Vanilla facts]

Static extension on the same CURRENT-selected native hash, independently checked
against the installed PE. Historical game proofs above are unchanged. This
extension does not establish in-game acceptance of an all-goods correction.

The following22 actual pending-store RVAs were verified with their complete
containing functions and incoming-reference checks. Each is a single ordinary
MOV of6..12 bytes, without RIP-relative addressing or a known interior target;
that is not permission to overwrite a larger14-byte inline span.
These are demand-producer stores;3EC90's success clear and initialization/load
copies are separate from this22-site set.

| Producer | Actual STORE RVAs | Assigned goods / quantity |
| --- | --- | --- |
| Food2A420 | 2A464,2A488,2A4AC,2A4D0,2A4F4 | fruit13,cheese11,bread10,wheat9,hops3; AIC batch |
| Bodyguard40230 / economy40430 | 403A7,40597 | dynamically reported missing good; buy_weapons |
| Combat40740 | 40D3C,40D62 | dynamic missing good; LastStand5 / ordinary AIC batch |
| ConstructionCC420 | CC555,CC5C1,CC657,CC692,CC6E4 | iron6,pitch7,wood2,wood2,stone4; raised deficits, wood+2 |
| Scarcity2AA20 | 2ADB5,2ADD7,2ADF9,2AE1B | wood5,iron2,flour2,hops2, when corresponding cell is zero |
| Smelter2EAC0 / emergency2F080 | 2EB9C,2F56E | pitch4 / wood20 |
| AIV retry51790 / ammunitionD5710 | 51ADB,D5791 | stone5 / stone10 |

No chronological immutable FIFO tickets were found: these stores replace or
raise one signed quantity per player/good.3EC90 reads that current cell, buys at
most one successful good in table priority, then clears the selected cell.
Repeated assignments may write exactly the old numeric value. Before/after
numeric differences therefore cannot prove the absence of a renewal. Global
recruitment failure/missing-good fields also survive producer early returns;
their post-call values alone do not prove a current request was emitted.
Configurable unit costs mean recruitment requests are not limited to equipment.

A request can be fully supplied and then consumed before the next purchase
snapshot. Example: stock10,pending12,target22; supply12 reaches22, construction
uses20, later market sees2. Fulfillment must be observed before subsequent
consumption, rather than inferred solely from the final market-time stock.

The identified normal non-gold TotalGoods writer set is AED70,AF210,B23F0,
B58B0,B6100,B7D80,B8460,B97B0,C1CE0,C95D0. Incoming-store deliveries and
committed local-storage additions reach C1CE0; B8460/C95D0 are storage-loss
routes, and B7D80 ordinary physical subtraction also recounts. B97B0 is the
separate all-eight-player recount and closes direct local-storage changes.
Computed-pointer aliases in B6100/C95D0 were explicitly included. Every identified
normal non-gold increase is published by completed C1CE0 or completed B97B0
before its native caller can perform subsequent consumption. Unknown external
pointer writes and save/load bulk copies are separate boundaries, not receipts.

BFD30's actual25-entry storage-kind table at2E76D0 is
`0,10,10,10,10,10,10,10,10,10,19,19,19,19,10,40,10,11,11,11,11,11,11,11,11`.
Kind40 is Gold15 only. Thus B58B0's direct non-recount kind40 increment does not
bypass receipt publication for any ordinary AI purchase good.

Two final-publication markers avoid transient zeroing/partial sums:

- C1CE0 ABI is void(BuildingManager*,int playerId). At C1F4A..C1F52,
  `42 89 AC 36 CC F0 12 00` is MOV[RSI+R14+12F0CC],EBP restoring gold AFTER
  all non-gold sums. RSI=playerId*583C,R14=player-manager. All paths reach it,
  including no buildings. The8-byte single instruction has no known interior
  target and no RIP/RSP addressing. C1CE0 contains no CALL or goods consumer.
- B97B0 ABI is void(BuildingManager*). At B9A30..B9A38,
  `48 8B 4C 24 28 48 33 CC` is MOV RCX,[RSP+28]; XOR RCX,RSP, after all eight
  final sums and gold restores. All paths reach it; the no-buildings branch
  bypasses B99D6, so that earlier location is not universal. No known interior
  target enters the8-byte span. A relay MUST restore the exact original RSP
  before replaying its stack-relative MOV and preserve the XOR/security-cookie
  continuation. No goods mutation follows before return.

PRE-C1CE0 alone is insufficient: AF210's metal-armour delivery can temporarily
assign TotalGoods1 before final recount reconstructs existing+new storage.
Final signed Int32 values are the valid evidence. Current source ownership
searches found no owner of those two final markers; live exact-byte checks and
later mission rechecks remain mandatory. SE already owns B58B0/B6100 entries.
3EC90 supplies only its own selected purchase through29650; its other callees
priceCEB10 and ally-request300F0 do not immediately transfer/consume stock.

### Package-preserving correction scope [mod integration]

The selected testmod policy preserves an emitted package: request5 plus later
supply1 leaves4. It is not a Vanilla proof of current construction/recruitment
intent, and does not infer cancellation from completed roles or disappeared
buildings. Supply reaching a whole package target can close owned demand before
later consumption. Own purchases inside original3EC90 must be distinguished from
external supply, so ordinary native buys are not counted as avoided purchases.
Unknown saved/foreign demand remains unattributed. This broader integration has
no in-game acceptance in this static extension; the earlier narrow equipment
A/B proof does not establish its behavior or performance.

Detailed machine spans, register maps, counterexamples and source-flow evidence:
[material audit](../../../../../Testmods/AIMarketOverbuyDiagnostic/Findings/AllGoodsMaterialAudit.md),
[food/recruitment audit](../../../../../Testmods/AIMarketOverbuyDiagnostic/Findings/AllGoodsRecruitFoodAudit.md),
[completed-stock audit](../../../../../Testmods/AIMarketOverbuyDiagnostic/Findings/StockReceiptAudit.md).

### Automatic siege reload demand provenance (22.11.2026) [Vanilla facts]

The canonical FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2
image has five automatic CALL returns into D5710:153057/153156 in1520D0,
1542C6/1543C7 in1535F0, and18FD8A in18FC00. Each is dominated by signed
native ammunition-WORD<=0. Manual18AE0 instead tail-jumps18BB5 without that
guard. At pending storeD5791, originalRSP+38h contains the real caller return,
and RBX retains the one-based unit ID from MOVSXD RBX,EDX atD572B.
D5710 consumes atomic10 stone and adds20 ammo; its missing-stock branch writes
literal10 even at stock9. It is not a continuous general stockpile target and
not an AIC weapons/food batch. D55B0 can use9 stone to supply18 ammo manually;
a fractional remaining1-stone purchase cannot itself feed the automatic10-stone
consumer. Reading only the low SDK ammo byte is also invalid:GameUnit+35E is
a signed16-bit native value spanning both SDK byte fields.
Full call/branch, stack, identity and counterexample audit:
[automatic ammunition source](../../../../../Testmods/AIMarketOverbuyDiagnostic/Findings/AutoAmmoSourceAudit.md).

## Resource extraction can bypass zero caps on the first building [Vanilla facts]

Rechecked 2026-11-26 on the native hash above. The managed LORDJSON transfer and native
`DLL_SetExtendedLordConfig` at RVA `0x87350` preserve the extraction values. In the
active AIC the ratios at `0x50..0x5C` are in wood, stone, iron, pitch order.
The caps at `0x60..0x6C` are in stone, iron, wood, pitch order.

Native construction controller `0x539B0` dispatches through the following checks.
Each rejects a nonpositive ratio before the count-dependent branch. Each checks
the cap only when its current building count is positive, so a zero cap by itself
still permits one building.

| Resource | Check RVA | Construction RVA |
| --- | --- | --- |
| Wood | `0x2E1E0` | `0x51540` |
| Stone | `0x2E0D0` | `0x51270` |
| Iron | `0x2DC80` | `0x50E00` |
| Pitch | `0x2DF10` | `0x51190` |

The positive-ratio path still needs its resource, workforce and placement checks.
The stone predicate can admit the first quarry above 15 population even when the
population-per-quarry test fails. The separate ox tether check at `0x2DD30` uses
an existing quarry and resource demand before allowing construction at `0x50F90`.

In a DoNothing skirmish on 4spielerkarte, positive ratios with zero caps produced
one woodcutter, quarry and pitch digger. The tested start did not demonstrate iron
mine creation. Setting all four ratios to zero suppressed these buildings in the
follow-up. Improved Hunters was disabled in the follow-up because its automatic
hunter's hut follows a separate mod path.
