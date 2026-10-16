# AI economy observation boundaries

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
