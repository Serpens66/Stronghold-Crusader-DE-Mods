# Native economy/construction/removal observation

Reference SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Manifest: workspace `_inspect/CrusaderDE-Native-Baseline/CURRENT.json`.
Machine evidence: `_inspect/AIEconomyDiagnostic-20261016/native-entry-audit.json`.
The audit script reads the manifest-selected generation's instruction exports and checks
all exported direct incoming targets against the whole-instruction entry spans.
Native function labels without curated claims remain candidates; argument/call/value
contracts below are based on the actual instructions, not their guessed names.

## Sole diagnostic entry ownership

All entries use the installed NativeX64 backend's Indirect schema only. Extended
entry fingerprints are in `EconomyHookContract.cs`. Validate those and decode the
minimum six bytes to the listed complete instruction boundary before publication.
Validate the real installed displacement, pointer slot, entry, padding and scheme.
Tests install the actual production backend on private copies and restore only those
unpublished test candidates. Published process hooks never uninstall or reapply.

If SpectatorEditorBuildTest_Serp is installed, its existing 0xCC420 entry wins and
this diagnostic does not install that entry at all. A soft dependency fixes startup
order. Spectator 0.1.2 publishes through its own private observation seam only
after its real patch passes validation. Its wrapper reports nullable return and
whether the native original was evaluated; override returns never become native
predicate evidence. No subscriber is inert; begin/end callback exceptions are
contained independently. Readiness is rechecked at session start; older/failed
owners produce an explicit gap. All independent entries/events remain active.

| RVA | Displacement | ABI and observation |
| --- | --- | --- |
| 0x2E1E0 | 10 | int(profile root, 1-based player); actual wood eligibility result |
| 0x2DBA0 | 10 | int(profile root, player); actual farm eligibility result |
| 0x3B360 | 10 | int(profile root, player); wood retirement, refund/delete inside original |
| 0xCC420 | 10 | int(player manager, mapper, player, feedback); actual resource gate, including demand writes |
| 0x3EC90 | 6 | void(profile root, player); request priority, emergency/popularity gates, affordability |
| 0x29650 | 10 | int(profile root, player, goods index, signed quantity); actual AI purchase |
| 0x29700 | 9 | void(profile root, player, goods index, signed quantity); actual AI sale; entry includes JS |
| 0x2D810 | 6 | void(profile root, player, category, byte special-case); strategic demolition/refund/delete |
| 0x40740 | 12 | void(profile root, player); AI recruitment, including failure/demand and assignment branches |
| 0xCCBB0 | 10 | void(player manager); all-player tax/donation accumulation and settlement, including zero floor |

The fallback searches executable PE sections for the unique complete entry pattern
on the same supported native hash. A moved/occupied entry is rejected: data layouts
and caller contracts have no independently proven migration fallback. Unknown hashes
disable only native decision capture; independent Extender observations remain active.
Each entry has independent availability and an original-only callback until validated.

## Closed relevant flow and evidence boundaries

Scheduler 0x539B0 calls the wood retirement/eligibility branch and the farm census/
selection branch (0x2DBA0 -> 0x2C9F0 -> 0x50D80). A rejected eligibility result is
distinct from no call. 0x50D80 and the other building callers enter 0xCC420 before
search/build; that function may modify purchase requests when resources are missing.
The observer never invokes census, resource, accessibility or search functions again.
Costs use Extender's explicit mapper-to-structure conversion, mirroring 0xC77C0,
and its active cost-table getters; observed costs alone are not an eligibility result.

0x3F270 requires the player's market and alternates purchase processing with sales.
It is deliberately NOT instrumented: the installed Release Fixes 1.26.4 wheat-sale
hook at 0x3F0C9 reads an incorrect caller stack slot, exposed by the managed
controller detour. The controller detour and its exclusive entry contract are removed
for the next process start; no live hook is undone, register repaired or replacement
instrumentation installed. This configuration does not depend on any APIShared fix.
The trade-controller coverage notice marks the market gate, alternation and aborted
sale decisions unknown; controller call counts are not emitted as false negative evidence.
0x3EC90 reads 25 request slots, applies its emergency/popularity/request gates, asks
0xCEB10 for the actual final price, then calls either the ally-request path 0x300F0
or purchase 0x29650. Purchase calls stock-add 0xB58B0 and only then deducts gold and
updates trade state 0xCB050. Sale 0x29700 credits gold, removes stock through 0xB7D80
and updates trade state. Price helpers 0xCEB10/0xCEB90 belong exclusively to existing
APIShared MarketPriceEvents; queries/ally valuations never imply trades.
0x40740's verified argument pair encloses only recruitment decisions, the actual
0x1819D0/0x190CA0 recruitment calls and subsequent assignment/demand handling. Existing
Fixes/Bugfixes inline hooks deeper in that function remain untouched.

Tax settlement 0xCCBB0 has one manager argument, not one player argument. It iterates
players 1..8, accumulates income/expenses through 0xCBCD0 and 0xCB9A0,
normalizes by ten on settlement, changes gold and floors at zero. A before/after
operation for each observed AI measures the actual net change. Existing Extender tax
notifications are Pre-only and stay separate from confirmed settlement accounting.

0x2F080's emergency gate already belongs to BugfixesAndQoL at 0x2F454. The user
requires an unchanged installed build (minimum 1.0.192); it has no per-decision publisher.
Do not patch that owner or infer protected outcomes from settings/absent calls.
Actual strategic deletion enters 0x2D810 with the player. Hovel deletion 0x3B1D0
also remains exclusively owned by that build; its decisions/context are unavailable.
Current settings and published-hook status are read through the verified managed
field/property contract only, with missing members reported as unavailable.
Native 0xC8F50 calls
0xC90E0, sets state 3 for accessibility returns 0/2, propagates by group and updates
the heatmap. Existing decision hook 0xC8FD7 is untouched; original/effective results
and classification are unavailable with the retained owner. Marking is not terminal
deletion. Group is the actual dword at
GameBuilding+0x2A8 (`N0000178A`), derived from state address +0x1D8 in the group loop.
Delete/bulldoze/damage/refund/pause/spawn use existing Extender events exclusively.
Delayed deletion watches and full inventory comparisons distinguish a surviving
marked slot, zeroed slot, reused global ID and unavailable lookup.

## Data roots and update checks

Player state stride is 0x583C; native slots are 1-based. `GamePlayerResources` is
0x583C, gold field +0x50C and goods base +0x4D0. Building stride is 0x32C, alive
state +0xD0, owner +0xD6, global ID +0xD8, group +0x2A8. Managed layout is validated
before capture. Goods index `Count` and negative pseudo-goods are not stock entries.

Raw per-player RVAs: 0x379D0D0 (effective profile), 0x379D838/0x379D83C,
0x379E630, 0x379E6BC/0x379E6C0, 0x379E744, 0x379CF50,
0x379D8EC/0x379D8F0 and 0x379D840 + goods*4 (25 request slots).
0x3505470 is captured as a raw global operand. Its `<1` comparison is the first
wood eligibility branch; no unverified semantic name is assigned to that field.
Profile data is 0x404C950 + (profileIndex-1)*0x5E4, checked against image bounds;
no guessed maximum of 64 profiles is imposed. Runtime AIC size 0x5E4 and each
selected public field's offset are taken from installed Interop. Lord enum and
effective profile index are logged separately. Never use a Lord enum as that index.

For an update re-audit complete functions/callers, all conditional/alternative exits,
incoming edges, relocated instructions and the installed backend. Refresh fingerprints,
offset contracts and machine evidence together. No stale RVA on an unknown image.
