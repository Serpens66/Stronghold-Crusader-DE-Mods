# Native market price query contract

Audited 2026-10-10 against CURRENT native SHA-256
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Installed RedBird.Backends.NativeX64 SHA-256:
0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0.

- Buy: RVA CEB10, 31 bytes, full function SHA-256
  5B45784D8B227D4BEB1AA822E6B12523BD9A0825EFA17764909A037E613C6C6A.
- Sell: RVA CEB90, 31 bytes, full function SHA-256
  D428FAE5C2A3BED0B48195B2661F56550E5B53F6E8EE9A603FADA56DAEE8F670.
- ABI: int(void* manager, int playerId, int good, int amount), RCX/EDX/R8D/R9D,
  signed EAX result. The helper does not read playerId. Good is sign-extended into
  RAX, indexing an eight-byte price pair at manager+1817B8 (buy) or +1817BC (sell).
  It computes signed (basePrice / 5) * amount, truncating division before unchecked
  Int32 multiplication. No native side effects occur inside either helper.

Complete audited callers:

| RVA | Meaning and consequence |
| --- | --- |
| 29650 | Buy execution: goods addition succeeds before gold/stat deduction and bookkeeping. |
| 3EC90 | AI planned-goods affordability: the same buy price must agree with 29650 execution. |
| 29700 | Sale execution: gold/stat update, goods removal and bookkeeping. |
| D78C0 | Ally goods transfer/request path: sell price feeds sender/receiver statistics. |
| D7AD0 | Ally delivery path: gold handled directly; other goods use sell valuation and update both statistics. |

These are native price queries, not general player-trade events or transaction
acknowledgements. Replacing a price does not cancel the surrounding caller. In
particular, replacing/vetoing with zero changes valuation/cost rather than aborting
a purchase. Consumer policies must remain deterministic and consistent across
planning, execution and ally valuation on every multiplayer peer.

Current semantic xrefs contain zero incoming targets into either complete helper
interior. The two functions have one straight-line block with a final return and no
internal branches. Exact full bytes are checked against the installed file and live
entry. APIShared 0.6 owns both helpers, and consumer AI classification stays private.

Only the installed NativeX64 NativeDetour Indirect schema is accepted. It requires
six patch bytes, extends to ten bytes (MOVSXD 3 + table MOV 7), and resumes at +10,
the five-byte MOV EAX,66666667. FollowJumps=false; no alternate schema is attempted.
After commit and before publication, validate Scheme, DisplacedByteCount,
TargetAddress, ChainDepth, FF25 patch, NOP padding, pointer slot, hook entry,
relocated bytes, trampoline jump to +10 and untouched remaining helper bytes.
Private executable fixtures use the productive backend and validator, invoke the
actual native ABI over signed/overflow cases and exercise changed-continuation
rejection. They are not X64InlineHook probes. Published runtime hooks and static
roots remain for the process; only an unpublished failed transaction can roll back.

The Script Extender 2.14.1 and canonical Fixes v1.26.3 source searches found no owner
of these two helper entries. Occupied entries or unknown native hashes fail closed.
Evidence and caller exports are retained in .inspect/APISharedHookAudit. This is a
static/backend contract audit; no interactive host/client gameplay run is asserted.