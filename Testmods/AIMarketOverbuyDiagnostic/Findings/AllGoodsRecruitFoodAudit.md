# Food and recruitment purchase audit, 2026-11-22

Read-only static audit; no implementation or new game acceptance. Installed
CrusaderDE.dll SHA256 was verified against CURRENT.json:
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Function names remain candidate labels where not curated; the statements below
refer to their complete exported bodies and explicit field accesses.

## Sources

Workspace-relative native export:
`_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/exports/semantic-decompiled-functions.c`.
Relevant starting lines: 2A420:24869, 2AE40:25282, 2CC10:26668,
2CE40:26758, 2D120:26870, 3EC90:35980, 3EE10:36031,
40230:37017, 40430:37079, 40740:37182, 618D0:56036,
1819D0:218140, 190CA0:227673.

Related baseline knowledge: `knowledge/AI_ECONOMY_OBSERVATION.md`,
`knowledge/AI_SELL_THRESHOLDS.md`, `knowledge/FIXES_COMPATIBILITY.md`
under the same semantic directory. Historical knowledge identities do not replace
checking the currently installed assemblies or current external source.

## Confirmed quantity and execution contracts

- Scheduler 2AE40 invokes 40430, 40230 and 40740 before the selected player's
  market controller. Food producer 2A420 belongs to its separate scheduled branch.
- 2A420 assigns fixed AIC.buy_food_amount for fruit, cheese, bread, wheat and hops
  when stock is below the relevant AIC threshold. Fruit permits threshold >=0;
  the other four require threshold >0. These assignments replace an existing
  cell, including with the same value; no append or accumulation occurs. A
  satisfied threshold skips the write and does not cancel an existing request.
- 40230 and 40430 assign fixed positive buy_weapons when recruitment failed with
  failure reason2. 40740 assigns the same batch, or5 in its LastStand branch.
  Successful recruitment and completed-role exits do not cancel old requests.
- 190CA0 reads the live configurable unit-cost table and checks its first three
  goods separately for stock <1. Duplicate good IDs are NOT summed by validation;
  success subsequently removes1 for each slot. Therefore a hypothetical claim
  that three identical slots require stock3 to pass validation is false. The
  fourth cost slot is the horse requirement. Native1819D0 has no equivalent
  ordinary-goods failure path in its complete body.
- Unit costs can contain other goods, so equipment17..24 is not a complete
  recruitment-demand domain. Extender source references:
  `shcde-script-extender/src/SHCDESE.BepInEx/API/GameUnitManagerAPI.cs:915`
  and `Interop/UnitGoodCost.cs`.
- 3EC90 reads the pending signed quantity and passes it unchanged to pricing and
  29650. Success clears that cell. It performs at most one successful purchase.
  3EE10 calculates sale eligibility/quantity from live stock and reserves;
  ordinary equipment reserves include pending. No immutable chronological
  duplicate-order list was found in this path.

## Correction choices are different policies

Preserving normal fixed batches suggests recording a confirmed request's
initial stock plus its batch as an absolute target, then reconciling an owned
quantity against that stable target and current stock. Construction requests
instead encode a deficit and require their own source-derived target. A generic
pending-minus-current-stock formula is invalid.

This does not by itself prove when the underlying demand is cancelled. AIC
replacement, disabled purchasing and completion of the relevant recruitment
role require separate source/context validation. Multiple producers can retain
claims for one good; discarding one must not erase another valid claim.

Cancelling an entire recruitment batch as soon as stock >=1 follows the live
missing-good predicate, but changes batch prefetching. Limiting food to
threshold-minus-stock changes ordinary purchase batches too. Neither policy is
implicitly authorized by the native code. Subsequent user choice: preserve the
package, so a pending package5 with supply1 has remaining quantity4. No general
implemented or accepted fix is established by this audit alone.

One concrete weakness of monotone-only correction: request5 at stock0, supply3,
reduce pending to2, then consume those3 before purchase. The stable target again
has deficit5, but min(currentPending, deficit) remains2. A truly current quantity
needs upward reconciliation while ownership remains proven, or another explicit
batch policy. Increasing an unknown or externally replaced request is unsafe.

## Provenance and efficiency requirements

Before/after pending differences miss same-value renewals. Reading global
recruitment failure/missing-good values only after a producer returns is also
insufficient: early returns preserve old globals. Actual write-site evidence or
an original-call scope that captures an actual recruitment attempt and confirms
its subsequent write is needed.

Function-entry scopes around all three recruitment producers plus observation
of their actual190CA0 attempt are a feasible direction, subject to a new exact
hook-span/ABI/ownership audit. Record actual selected unit, role, live cost
signature, failure/result and request context. Do not infer selection from a
changed pending value alone.

Never rerun native selection to refresh demand: 2CE40 rebuilds siege scratch
flags and consumes RNG; 2D120 changes its siege roster cursor; 40740 changes
defender/harasser cursors and may recruit2/3/4 units. Moat helper618D0 scans
63,999 records. Use captured original context and inexpensive proven predicates
at purchase decisions, not another native decision pass or that full scan.

## Fixes integration and goods coverage

Canonical external source:
`D:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Fremde Mods/shcde-fixes-main/src/shcde-fixes/Detours/AIDetours.cs`.
Missing-horse hooks (lines approximately254..530) are inside40230/40430/40740;
function entries are separate. The ordinary successful unit-transition hook
inside190CA0 is owned by Extender BulkUnitDetours.cs:768, so an entry candidate
still requires precise span and live-byte checks. Avoid3F270's known Fixes sites.

Fixes custom wood/iron/ale logic (AIDetours.cs:1620..1700) patches the hops tail
inside2A420. Its flags and threshold/amount arrays produce independent requests.
An after-original2A420 snapshot contains these foreign writes too. Do not label
them Vanilla food; their active flags/preferences and ownership are separate.

Confirmed native producers cover goods2,3,4,6,7,9,10,11,13,16 and equipment17..24.
The buy-priority table also contains meat12 and ale14; no dedicated Vanilla meat
producer was established, while Fixes adds ale. Configurable recruitment can
introduce additional priorited goods. Null, raw logs1, hides5, refined pitch8
and gold15 are not ordinary entries in that market-priority table. Unknown
external writers need their own contract and must remain untouched until then.

The material-source audit and integrated tests remain separate prerequisites
for any claim that a correction covers all reachable goods and false amounts.

## Exact store-only relay candidates

Decoded afresh from the verified installed DLL with Capstone. The complete
surrounding functions' direct calls/jumps and all CURRENT SQLite xrefs were
checked: no target lies strictly inside any of these spans. Each span consists
of one ordinary non-RIP-relative MOV. These are candidates for an Indirect6
detour to a custom native register-preserving relay; they are NOT safe14-byte
inline spans. Actual installed-backend tests, live-byte checks and relay ABI/
register/flags/SIMD/stack execution tests remain required before publication.

| Source | Store RVA | Length | Complete bytes |
| --- | --- | --- | --- |
| Fruit | 2A464 | 8 | 43 89 84 08 64 16 13 00 |
| Cheese | 2A488 | 8 | 43 89 84 08 5C 16 13 00 |
| Bread | 2A4AC | 8 | 43 89 84 08 58 16 13 00 |
| Wheat | 2A4D0 | 8 | 43 89 84 08 54 16 13 00 |
| Hops | 2A4F4 | 8 | 43 89 84 08 3C 16 13 00 |
| Bodyguard | 403A7 | 8 | 41 89 84 95 30 16 13 00 |
| Economy protection | 40597 | 8 | 41 89 84 95 30 16 13 00 |
| Combat LastStand | 40D3C | 11 | C7 84 82 40 D8 79 03 05 00 00 00 |
| Combat normal | 40D62 | 7 | 89 84 8A 40 D8 79 03 |

Register map immediately before each store:

- Food: EAX=batch; R8=playerId*583C; R9=player-manager;
  RCX=lord-manager root; RDX=nativeAicId*5E4. Good is fixed per site.
  Store destination is R8+R9+131630+good*4.
- Bodyguard/economy: EAX=batch; RCX=good; RDX=playerId*160F+good;
  R15=playerId; EBX=actual selected unit type; R13=player-manager.
  Store destination is R13+RDX*4+131630.
- Combat LastStand: RAX=playerId*160F+good; RDX=module base;
  RBX=playerId*160F; R14=playerId; EDI=actual selected unit; amount is5.
  Derive good=RAX-RBX. Destination RDX+RAX*4+379D840.
- Combat normal: RCX=playerId*160F+good; RDX=module base;
  RBX=playerId*160F; R14=playerId; EDI=actual selected unit; EAX=batch.
  Derive good=RCX-RBX. Destination RDX+RCX*4+379D840.

Observing before the store preserves original pending and current stock, catches
same-value renewals, and excludes all early-return paths. After the observer,
the original trampoline must execute this store exactly once and jump to its
following instruction without changing live registers, flags or floating state.
Do not use a managed delegate with an invented function ABI at a store site.
Food hops store2A4F4 is separate from Fixes' tail hook beginning2A4D8; still
validate the actual installed hook's span and adjacent continuation live.

## Writer-relay offline execution, 2026-11-22

Implemented reusable relay in `src/MarketDemandWriterHooks.DevDiag.cs`, with
native gating, saved15 GPRs/flags and aligned FXSAVE64/FXRSTOR64 across the managed
observer. A private original-trampoline slot is initialized before publication;
published relays/hooks/delegates remain statically rooted and are never undone.
The observer owner must contain all exceptions and keep the native gate dormant
until the complete source set is prepared and published.

`tests/TradeEntries/WriterRelayTests.cs` links the actual helper and authoritative
`MarketDemandSites.All` table. It independently decodes all22 store mappings
with Iced, validates the real installed DLL bytes, then exercises the installed
RedBird Indirect backend on guarded private copies. Checks cover dormant/active
gates, both incoming stack alignments, all15 GPRs, arithmetic flags, all16 XMM
registers, actual stores and single continuations, repeated same-value writes,
both branches of the separate2A4EB conditional boundary, and the actual2A42A
pre-Fixes IMUL boundary with its original R8 product and CF/OF results. The
2A4EB boundary is retained as a generic branch-preservation test; it is not a
complete observer for custom Fixes purchases when the hops threshold is <=0.
The fixtures do not call full game functions on fake state.

Commands: `dotnet build tests/TradeEntries/TradeEntries.Tests.csproj -c Release`
from the mod directory, then execute the resulting net481 EXE directly.

Recount-marker fixtures additionally execute C1F4A's nongold store and B9A30's
stack-relative MOV/XOR before the security-cookie call. Both incoming stack
alignments verify a known original-RSP+28h sentinel, the exact RCX XOR result
and its defined status flags, with the observer receiving the pre-replay frame.
The stack fixture reserves separate scratch rather than corrupting caller saves.
Final run completed with exit 0 and 3764 successful checks. The build had one existing
unused-field warning for DebugLogHelper.PersistentHashDirectory. Own edited
text files were normalized and verified with zero lone LF bytes. These are
native-fixture results, not Unity Mono or in-game acceptance of the integrated
quantity policy.

### Unity Mono native-destination correction

The subsequent hidden-game smoke correctly failed closed because Unity Mono's
Marshal.GetDelegateForFunctionPointer/GetFunctionPointerForDelegate round trip
produced a managed wrapper rather than the requested native relay address.
Evidence: `.work/a/market-smoke/003/fc0649628186/LogOutput.log`, line 1484.
Executing that wrapper at a mid-function store would have used an invalid ABI;
the address guard prevented publication.

Installed RedBird's public `DetourRequest<T>.IntermediaryFactory` contract provides
the supported solution. `Create(in DetourIntermediaryContext)` receives the native
trampoline and returns `DetourIntermediaryStubs(nativeRelay, trampoline)`. The
installed backend uses those two destinations for the patch and original entry,
respectively, rather than the otherwise required callback delegate address.

Added `Shared.AuditedEntryDetour.PrepareNativeTarget` with the same pristine/live
bytes, span, trampoline and publication checks as existing Prepare. It additionally
checks architecture, target identity, explicit raw hook destination and direct
original-trampoline destination before publication. Existing managed Prepare API
and behavior are unchanged. Writer relays now supply an unreachable managed
placeholder and never convert a native relay to a delegate and back. A missing
native address fails closed instead of selecting that placeholder.

The complete TradeEntries fixture executes the new API through all relays and
checks explicit native-versus-placeholder addresses plus pristine/foreign-patch
rejection. Shared's existing AuditedEntryDetour fixture also passes (43 checks).
Unity Mono in-game acceptance of this replacement remains separate.

### Immediate correction after a confirmed source store

The later source-intent implementation uses `PrepareSource` only for the22
audited single dword MOV stores. It calls the owner before the native assignment,
with the old live pending quantity still intact. After restoring all registers,
flags, FX state and the exact original RSP, it executes the audited MOV inline
once. If the separate correction gate is active, a second callback with index+256
then sees the emitted quantity and may correct that pending cell immediately.
Finally the relay restores native state again and jumps directly to the native
continuation. It must not execute the original trampoline after correcting the
cell, because that would overwrite the correction with the original batch.

With the writer gate inactive the original trampoline runs once without either
callback. With only the writer gate active, the pre callback and native store run
once, followed by the native continuation. Original and continuation pointer slots
are initialized and read back before publication, never rewritten afterward.
The specialized emitter rejects multiple instructions, loads, RIP-relative or
RSP-dependent operands and other opcodes. Other markers retain the pre-only relay.

Offline fixtures execute both relay forms on all22 actual source instructions,
including dormant, baseline, corrective and pre-callback-deactivates-correction
states at both stack alignments. They check old-cell pre-observation, emitted-cell
post-observation, a test correction Q-1 surviving continuation, repeated writes,
one continuation, and complete GPR/flags/XMM preservation. These test relay
ordering and preservation; source credit and ownership policy remain the owner's
separate contract and require integrated and in-game tests.
