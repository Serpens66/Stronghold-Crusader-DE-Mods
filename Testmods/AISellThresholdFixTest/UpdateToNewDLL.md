# AI sell threshold native contract

Reference native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Audit date: 2026-10-22. Discover the current baseline through workspace
`_inspect/CrusaderDE-Native-Baseline/CURRENT.json` and its database manifest.

## Closed feature flow

Vanilla CustomisationFileManager parses `.lordjson` into NewAIC signed Int32 fields.
EngineInterface.CreateAILordConfigData copies them unchanged. setCustomLordConfig
serializes the native 0x5E4 profile and DLL_SetExtendedLordConfig (0x87350) copies
it without threshold validation. Effective profile selection is read by the AI
scheduler (0x2AE40), market/phase controller (0x3F270) and sales handler (0x3EE10).
The controller requires a market and alternates purchase and sale processing.
The decompiler presents the tail-shared sale body inside the controller as well;
machine instructions prove that it branches to 0x3EE10, rather than duplicating sales.

The 20 pairs at 0x2CEC90 map goods to eight threshold categories: wood, food,
stone, weapons, ale, resources (including category zero's default), flour, pitch.
Category fields are profile offsets A8/7C/AC/B8/BC/B0/B4/C0. Trade minimum C4 is
added; low popularity, emergency state/gold, pending purchase demand, sell_all
and cooldown branches alter the effective reserve and the final requested amount.
All converge at 0x3F1F3 and the signed stock > reserve comparison at 0x3F202.
Only the first qualifying good is offered per sale phase.

0x29700 rejects negative requested quantities, but accepts zero and overselling.
0xCEB90 computes `(configuredPrice / 5) * quantity`. Three native bookings at
0x2974D/0x29758/0x2975F credit that amount BEFORE 0xB7D80 removes physical goods.
0xB7D80 selects storage types via 0xBFD30/table 0x2E76D0, consumes eligible alive
owned storage, and recounts totals through 0xC1CE0. Empty/exhausted storage never
reverses the prior credit. It has separate editor/direct-account branches; the
sale guard operates before these alternatives. 0xCB050 may emit sale presentation
through 0xB5690; it does not reverse or cap the transaction.

Function names without curated symbols remain candidates; the register, branch,
signedness and booking-order findings are confirmed machine/data-flow observations.

## Hook spans and machine state

| Feature | Span (end exclusive) | Instructions | Continuation |
| --- | --- | --- | --- |
| Effective reserve floor | 3F1F3..3F202, 15 bytes | LEA RAX,playerManager (7); MOV R9D,[RAX+RBP+12F090] (8) | original signed CMP/JG |
| Sale quantity cap | 29723..29732, 15 bytes | MOV EDX,EDI (2); LEA RBP,playerManager (7); MOV ESI,R8D (3); MOV RCX,RBP (3) | price CALL 0xCEB90 |

The installed RedBird.X64 backend uses max(requested,14), rounded to complete
instructions. Both actual displacement lengths must equal 15. All baseline xrefs
and whole-function direct edges were checked: neither span has an interior entry.
Two branch-free copies of the original block use IP=0 to avoid duplicate encoder
instruction identities; their absolute RIP targets remain intact.

Decision: EDX is the final signed reserve; only a negative EDX changes to zero.
RAX/R9D retain the displaced block's outputs, and original flags are preserved
until the native CMP replaces them. A skipped empty good falls through to the
existing loop increment and can no longer starve later goods with stock.

Sale: the complete prologue has run. RDI is the sign-extended 1-based player
(1..8), R8D the native goods index, EBX and R9D the requested quantity. Invalid
goods, null/gold pseudo-goods and nonpositive stock cannot be sold. Signed stock
is read using Vanilla's comparison interpretation, although Interop exposes most
totals as UInt32. Clamp EBX and R9D consistently. RAX/RCX/flags are saved across
the guard; XMM is untouched. Zero quantity branches to 0x2978B with the native
stack intact, which restores RBP/RBX/RSI/RDI and returns. No price, booking,
subtraction or presentation occurs on that path.

## Data roots, resolution and ownership

All constants are machine contracts for this native identity:

- Player manager 0x366C210; 1-based player stride 0x583C.
- Total goods 0x379B2A0 + player*0x583C + nativeGoodsIndex*4.
- Gold 0x379B2DC + player*0x583C; sales totals 0x87EED98 + player*4
  and 0x379AE24 + player*0x583C.
- GamePlayerResources offsets 4D0 (null base), 508 (ale), 50C (gold), size 583C.

Runtime checks the canonical loaded DLL file hash, actual Interop layout and
backend hashes, full displaced bytes, subsequent comparison/call and sale epilogue.
Reference-RVA lookup is followed, if it fails, by a unique exact semantic-block
search restricted to executable mapped PE sections. There is no independently
audited layout/target migration fallback: a moved duplicate or unknown hash is
rejected. A successful snapshot never overrides conflicting live bytes.

Fixes' wheat hook at 3F0C9..3F0DC, APIShared's sell-price entry at CEB90 and
AIBuildDiagnoseTest's sale entry 29700..29709 do not overlap. The latter's current
31-byte entry fingerprint ends before 29723. Neither external source tree changes.
An unknown live owner of either new span disables this complete optional feature.

All stubs prepare inactive, commit together, and validate their live FF25 pointer,
padding and displacement before publication. Only an unpublished initialization
candidate can roll back. Published code and delegates remain rooted until process
exit. No settings or map transitions mutate executable memory. No JSON parser,
publicized game assembly, APIShared or other workspace runtime dependency exists.

## Verification and update checklist

`build.bat /nopause` checks the current working sources, runs focused preflight,
audits canonical PE bytes/control flow/xrefs, runs the native harness in normal
and DEVDIAG variants, builds .NET Framework 4.8.1, then installs three files with
SHA-256 equality checks. `/noinstall` and `/verifyonly` are supported. Only an
explicit `/devdiag` enables development instrumentation; inherited flags are cleared.

The harness runs original copied Vanilla functions 3EE10,29700,CEB90,B7D80,BFD30,
C1CE0,CB050 and original category/storage tables. Only visual rendering endpoints
C05B0/6E620 are no-op fixture boundaries. It first reproduces phantom credit and
then applies the actual installed RedBird backend and production generators to
private memory. It covers all 20 goods/eight categories, threshold/minimum signed
extremes, zero/partial stocks, low popularity, emergency, demand/sell_all, repeated
empty phases, players 1/8, valid-sale parity, stack/register/flags/SIMD preservation.
Fixture teardown never affects a running game or published game hooks.

On updates, re-audit the full input/decision/booking/removal chain and all branches,
verify actual new DLL and backend identities, incoming edges and owners, layouts,
enums and machine register contracts. Update expected bytes only with new evidence.
The old database/source snapshot's freshness warning is not permission to change
the hash-bound machine contract. Runtime acceptance remains separate from tests.

Manual acceptance: negative ale threshold with empty stock yields no gold; stocked
ale sells only what exists; an empty earlier category does not block later goods;
normal thresholds preserve sales. Confirm the post-cleanup OnTick marker, repeat
after save loading/map replacement and in editor. Real multiplayer requires the
same plugin on every peer and host/client logs; no multiplayer result is inferred
from the private harness. Development-only result logs observe actual resource
subtraction and gold delta, with bounded reports and a removal-balance invariant.
