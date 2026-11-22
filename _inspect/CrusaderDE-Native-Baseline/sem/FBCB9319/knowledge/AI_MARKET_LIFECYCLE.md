# AI market entries, repair boundaries and integration evidence [Scope: Vanilla + mod integration]

Verified 2026-11-22 against CURRENT's canonical installed native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Current function records must be selected by this binary hash: an RVA-only query
can also return historical functions with another name at the same address.
This focused note adds lifecycle facts; it does not replace the demand-writer,
stale-request or negative-sale audits in the adjacent knowledge documents.

## Unchanged native trade origins and entry contracts

All four are Windows-x64 entries, not call sites. Root preserves the original
first argument: it is the AIC/profile root in the AI decision/recruitment path;
the transaction bodies do not dereference that argument. Player and good/amount
are signed32 where shown. Shared ownership does not relocate their origins.

| RVA / displaced span | Native ABI | Exact displaced bytes / continuation |
| --- | --- | --- |
| 3EC90 /6 | void(root, int player), purchase-decision consumer | 40 56 41 54 41 55; continue3EC96 |
| 40740 /12 | void(root, int player), combat recruitment | 40 55 56 41 56 48 81 EC 60 01 00 00; continue4074C |
| 29650 /10 | int(root, int player, int good, int signedAmount), buy | 48 89 5C 24 08 48 89 6C 24 10; continue2965A |
| 29700 /9 | void(root, int player, int good, int signedAmount), sell | 45 85 C9 0F 88 96 00 00 00; continue29709; negative-amount JS target2979F |

3EC90 reads the current stored Pending quantity, quotes its cost, calls29650
and clears that cell only after success. 29650 does not recompute demand from
current stock; physical stock-add success precedes charging gold. 29700 has its
separate signed-amount and accounting/removal path. Price queries, a phase change
or a gold/stock tick coincidence alone do not prove a completed transaction.
40740's recruitment and failure branch can generate requests; its function entry
is not a distinct successful-booking event. See AI_ECONOMY_OBSERVATION.md and
AI_SELL_THRESHOLDS.md for those complete flows and their limits.

## Actual common multiplayer repair boundaries

Transfer phase is the signed32 global at RVA85F8FEC. Phase0 is execution,
phase1 packing and phase2 size measurement. Capture phase at handler ENTRY:
phase1 handlers can change that global themselves before returning.

| Handler | Contract | Relevant native effect |
| --- | --- | --- |
| Chore54 at15170 /10 | void(), bytes48 83 EC 28 8B 05 72 3E 5E 08; continue1517A | Phase0 sets repair-state85750CC to10 and starts repair bookkeeping |
| Chore67 at16E40 /6 | void(), bytes48 89 7C 24 18 55; continue16E46 | Phase0 resets repair state and completes native repair cleanup |

The15170 entry contains a RIP-relative EAX load whose operand resolves to
85F8FEC:1517A + signed displacement085E3E72. A trampoline must preserve the
operand's absolute address, the stack adjustment and the original continuation.
16E40's ordinary displaced instructions preserve its stack/register entry frame.

Executed54 permits a PRE notification before its original bookkeeping/checksum
path. Executed67 permits a POST notification after its original fully returns.
67 PACK phase1 sets transfer phase to0 then returns, so a post-return-only phase
test would falsely publish completion during packing. PACK/SIZE must emit
neither boundary. UI repair panels and local timers are not substitutes.

The native run scheduler CDE60 distinguishes host/client repair progress through
host flag8574CC0 and repair-state85750CC. Host state10 dispatches27960, state1
dispatches26D30, state2 dispatches251F0, and the later acknowledgement/completion
stage dispatches28160. Client stages send their corresponding repair/control
messages rather than executing the host state machine. In28160 the host checks
the participating players' completion acknowledgements; on completion it queues
Chore67 via23990. Its timeout paths retain native disconnect/acknowledgement
handling; they are not a common successful-completion notification.

Player-record transfer Chore60 at15B90 is particularly unsuitable as an all-peer
metadata-reset boundary: phase0 unpacks a0x583C player record only on the client
(host flag0). Packing serializes on the sender; sizing is a third mode. Local
return from this record transfer therefore does not establish that both peers
have passed a common completion boundary.

There is also a local abort path in DLL_ReceiveChore at86480: playerID==-1 and
length==-1 can clear85750CC and8575124 without executed Chore67. Clearing native
repair state is consequently not proof of common completion. A metadata owner
must not manufacture a successful local resume from that observation.

## Shared ownership and failure semantics [mod integration]

APIShared.Economy.AiMarketEvents owns the four trade entries as permanent
NativeX64 Indirect detours, FollowJumps=false. It validates audited load/live
spans, actual displacement, pointer slot/hook entry and trampoline semantics;
the sale's conditional branch target remains intact. Consumers subscribe to
that owner rather than installing competing market-entry hooks.

APIShared.Multiplayer.MultiplayerResyncEvents owns15170/10 and16E40/6. It uses
the saved entry phase for both notifications, publishes ordered critical
callbacks outside registry locks and runs native originals exactly once.
An agreed consumer can reset its own AI bookkeeping at both common boundaries
on all peers, leaving native Pending untouched. This is not transfer of a
multiplayer metadata sidecar or reconstruction from a client-only record.

An engine-run PRE/POST guard checks85750CC while an observed repair is active.
State0 without common executed67 is terminal: keep the logical fence and fault
the world; never emit synthetic completion. Consumer exceptions similarly
latch the terminal world guard, even without a SimulationAgreement receipt.
When a receipt exists its peers are notified as well. Initialization requires
the actual common lifecycle/admission owner, not merely an allocated engine
monitor: that owner's earlier lifecycle callback closes admission and retires
old run generations before resync world metadata is reset.

## Test evidence and acceptance boundaries [mod integration]

- APIShared/UpdateToNewDLL.md, "Multiplayer repair lifecycle boundaries", records
  the current hash, complete execution/PACK distinction, globals and update gates.
- APIShared/tests/APISharedTests/AiMarketEventsTests.cs executes the installed
  NativeX64 backend on private copies of all four entries, including both sale
  branches, and tests paired observation, original exceptions and dispatch.
- APIShared/tests/APISharedTests/MultiplayerResyncTests.cs tests entry-phase
  gating, PACK's misleading final phase, ordering, critical failures, local abort,
  world retirement and two independent ledger state machines. Its guard-page
  fixtures execute both actual backend detours, the relocated RIP load and their
  original continuations in private memory, without game startup probes.
- WorkerProductionWorldSafetyTests includes the actual terminal fault latch with
  no agreement receipt. NativeWorldRestoreEventsTests covers ordered raw restore
  participants before first AI folds; save restore is a separate lifecycle.
- Controlled single-player trade/Gift and save/reload acceptance exists. In combined
  run4d73582e76624d789ecf0636724503a7, the archived log under
  `.work/a/market-save/003/4d73582e7662` records native-source remaining4/credited1,
  two RAW_RESTORE_PASS checks before the first fold and two ROUNDTRIP_PASS checks.
  These prove that tested single-player file round trip, not live network repair.

Static audit, private executable fixtures and isolated two-ledger tests do NOT
establish actual two-peer multiplayer repair acceptance. No such live MP result
is claimed here. Unknown native hashes, foreign overlaps and changed layouts
still require a fresh audit; none is authorized by these tests.

## AIBuild emergency observation conflict [mod integration]

The first combined game exposed an integration conflict, not a Vanilla market
defect: `.work/a/market-save/001/7563c821acc4/LogOutput.log` line3180 reports
BugfixesAndQoL's late foreign-patch guard at emergency-demolition entry2D810.
AIBuild's old installer, loaded after its hard dependency, decoded/chained the
main owner's live six-byte FF25 instead of preserving that published entry.

AIBuild now checks each complete displaced live span before backend preparation
and again before Commit. Different bytes decline only that observation and
report owner-preserved unavailable coverage; pristine entries remain installable.
An unregistered writer is preserved too: registry attribution is supplementary.
The private regression verifies pristine acceptance and foreign FF25 preservation.
The canonical no-install AIBuild build passed52/52 tests on2026-11-22.

Combined reruns2 and3 show native-2D810 unavailable/owner-preserved and shared
market observation available, with no NATIVE_FOREIGN_PATCH_LATE. In run3 the
first coverage lines2301/2304 and later session summaries retain that explicit
gap. The registry owner text is unregistered/unknown, so that log alone does not
identify its publisher; the original main-mod guard and load-order/source audit
establish the conflicting owner. No main-mod protection is disabled by this fix.
