# Actual owner component regression

The explicit `market-save-roundtrip` scenario now checks these contracts before
writing its run-owned save. These are component probes in a paused local game,
not a substitute for an actual multiplayer repair.

- `FIX_ACTIVE` requires the real owner property plus `corrective`, `sessionActive`
  and both native gates to equal one. Technical hook readiness alone cannot pass.
- `CAPTURE_PURITY_PASS` invokes the compiled production `CaptureLedger` callback
  through a real APIShared save writer. A temporary invalid private policy proof
  must omit only the detached row, preserve the live request exactly and leave all
  native pending cells unchanged. The original genuine row is restored afterwards.
- Ordered public resync registrations surround the market owner at orders -101
  and -99. Under the actual engine monitor, the isolated component probe dispatches
  the real shared registry, without executing native Chore54/67. It checks ignored
  PACK/SIZE phases, owner request clearing, suspended native gates, repair-time
  capture refusal, completion scratch clearing, reactivation and native pending
  preservation within each callback boundary.
- `OWNER_REPAIR_COMPONENT_PASS` explicitly states `nativeChoresExecuted=False`
  and `dataTransportExecuted=False`. Dispatch is refused if an unrelated resync
  participant is registered, so the probe cannot reset another mod's private state.
- `OWNER_NEW_SOURCE_PASS` then calls the genuine native recruitment producer.
  After the reset, an unowned pending value must not manufacture provenance; only
  a new native source may reopen an original package with zero credit. One actual
  delivery and immediate consumption must again leave four pending and one credit.
  This genuinely reopened row is used by the subsequent save/load checks.

The acceptance mod also registers one ordinary generic `MultiplayerResyncData`
record under its own GUID. Local registration/readiness is evidence that the APIs
coexist, not evidence that data was transported. A real paired repair still has
to prove the native completion gate and the record transport across machines.

No pending cells, native recruitment counters or native repair state are written
by these fixture probes. Negative-case proof changes and repair scratch affect
only the test world's private managed owner state and have explicit cleanup.

## Existing workload scenarios

`market-fresh-fixed` now asserts the same production `CorrectionActive` property
at its existing health checkpoints and emits `AI_MARKET_FIX_ACTIVE` once. The
intentional final output drain is excluded because it first disables the owner.
`market-backlog-fixed` proves and logs all gates immediately after its real gift;
its earlier Vanilla comparison phase deliberately remains correction-free.
The observer-only `market-fresh-smoke` scenario remains observer-only.

These assertions do not add a scenario or change the procurement policy. They
prevent a technically ready, logically disabled fix from satisfying a fixed-run
acceptance check.
