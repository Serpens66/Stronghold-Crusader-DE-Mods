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
