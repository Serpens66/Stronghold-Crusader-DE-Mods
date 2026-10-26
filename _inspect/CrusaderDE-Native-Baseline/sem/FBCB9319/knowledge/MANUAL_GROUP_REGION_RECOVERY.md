# Dry mixed-group movement recovery

Historical implementation: superseded on 2026-10-26 by
[pre-mutation group admission](MANUAL_GROUP_ADMISSION.md). The dated findings and
original proof limitations below are retained as historical evidence.

Observed 2026-10-25 against installed native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Names other than E2610 retain candidate confidence; the following control/data-flow
findings are confirmed-static, not game acceptance.

## Native flow

The input/Chore chain is in MOVE_COMMAND_RELEASE.md. Group dispatcher 11B520 reads
the one-based leader ID (tribe record +5A, stride 688), its current PCL and the
target PCL. Ordinary cross-region movement obtains group mode from 117C70 and
calls E2610(player, source, destination, mode). Zero leads to E9D90/E9FF0
structure-exit checks; failure returns zero before per-member moves. A dry leader
enclosed by moat can block members already in the target ground component. The
cursor's 18E1E0 witness is independent and cannot seed simulation ownership.

Native fallback 118E00 writes group waypoint/patrol fields and enumerates members
through 119F90. It retains its own native life/kill/state/capability predicates
before calling 196280. It is not equivalent to issuing terminal moves directly
to captured units. 196280 normalizes its start, checks member regions/portals and
only then constructs the actual path. Positive macro reachability is not movement
permission; FORMATION_MOVEMENT_PERMISSION.md still applies.

## Macro query write closure

Full E2610 body: RVA E2610, 1589 bytes, SHA-256
`7F875ED003B5E0A047F394632F0F2F952D4364DCC9DD8C9A62D179441B39A0ED`.
All 347 instructions were decoded directly from the installed PE. The only call
is the security-cookie checker. Apparent r11 writes at E271D/E2725 address the
local stack array established by `lea r11,[rbp+230]` at E26B9.

The entire non-stack write set is three int32 values in manager RVA 60AD660:
+C4 counter (E2660), +C0 search state (E2747/E297E/E2996), +98 fallback state
(RIP writes E298A/E29A5/E2C29 all target 60AD6F8). All other stores are stack
locals. The query does not touch tile distance/stamp grids, packed paths or
terrain. It traverses connection records using exact player/diplomacy/building
and connection-mode inputs. Equal positive regions need no query. Mode is the
native signed value, not an unconditional default ladder mode.

The constructor's full hash gate binds this closure. Recovery also validates
representative writer bytes and the existing GameUnit mode offset. No semantic
fallback is claimed on unknown DLLs. Query-only calls restore +98/+C0/+C4 in
finally, including exceptions; real simulation queries keep their side effects.

## Consumer ordering and limits

Extender BulkTribeDetours raises Post after 11B520 and returns its ReturnValue.
Post has original inputs; a retained Pre object has final effective inputs.
Recovery rejects changed/vetoed inputs and requires matching effective early
rejection, zero group result and no previous member movement event. It is limited
to human dry-ground commands, unchanged leader identity/location, map/tick/revision
and command sequence. A later nested order invalidates the older recovery. Attack,
work, moat-start, special and planner scopes cannot provide this proof.

Recovery precedes frame clearing, queue Post and formation Post. A local scan
first seeks a same-target-region member; otherwise each distinct (player, source,
destination, signed mode) is queried once, stopping at the first witness. There is
no cursor cache, full probe, synthetic Chore, membership mutation, new hook,
new moat-placement batch or terminal-move loop. ObserveCommonGroupMove preserves
the existing native/formation integration. Native refusals remain authoritative;
a failed/throwing replacement is never retried.

Evidence: `.work/a/manual-region/001`. FriendlyMoatMovement.Tests executes actual
production recovery/observation with native boundaries doubled, checks the full
installed function fingerprint and guards Post integration timing. 100/200/550
members require zero full probes, zero extra graph queries on successful originals
or same-region witnesses, and at most one per distinct queried key. Actual game
latency, editor behavior and real host/client acceptance remain separate.
