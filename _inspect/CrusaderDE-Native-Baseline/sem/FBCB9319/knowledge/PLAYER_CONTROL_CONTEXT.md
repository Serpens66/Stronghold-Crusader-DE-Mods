# Player control, Chore issuer and view-dependent enqueue [Scope: Vanilla + mod integration]

Audited 2026-11-01 against canonical native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
and managed SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Internal function names remain candidate labels. Their specific instructions and
dataflows below were checked against this image; they are not exported APIs.

## Received issuer versus local enqueue viewpoint

The managed `Platform_Multiplayer` receives Steam channel-2 messages, resolves
`m_identityPeer` through `findMember`, and passes the resulting roster member to
`processMessage`. Its packet-type-1 branch calls
`EngineInterface.ReceiveChore(fromMember.playerID, data.data, data.dataLength)`.
The source player is not taken from the inner Chore's claimed command identity.
Managed save/load roster remapping precedes this call.

Native receive scheduling at `0x23F10` stores that independently supplied source
at pending slot `+4`. Due execution at `0x1F7E0` loads the slot source through
`0x1F9A0` and stores it at ChoreManager `+0x36FA4C` before the handler call.
**In this image `0x1F9A0` is `8B C2 C3` (`mov eax,edx; ret`): it performs no
additional remapping.** A disagreement between these two values is not a normal
remapping case. Both must agree for the verified execution context.

ChoreManager's module RVA is `0x8574320`. Relevant fields are current slot
`+0x84CC8`, handler phase `+0x84CCC`, pending array `+0xB0BF8` with stride
`0x500`, and executing issuer `+0x36FA4C`. A slot contains scheduled tick `+0`,
source `+4`, opcode byte `+8`, state byte `+9`, command identity `+12`, barrier
byte `+16`, and parameters beginning at `+17`. See the existing
[Chore dossier](CHORE_SYSTEM.md) for queue/scheduler/barrier routes and limitations.

`0x20950` advances the queue cursor at manager `+0x370BF4`, wrapping over 500
slots. It accepts state zero or a signed-byte state >=10. If all 500 candidates
are active it nevertheless returns the last active slot; `0x23990` then clears
and replaces it. The new additive sender checks capacity under the same engine
monitor instead of interpreting a void queue call as a safe capacity decision.
That is a local preflight, not an all-peer reservation or network flow-control
proof. A future control protocol must limit total in-flight native packets too.

Due-list builder `0x21EE0` collects active slots with scheduled tick <= current
native tick, then sorts them by signed command identity before `0x1F7E0` executes
them. This is not sorting by local slot number or by the initial scheduled tick.

The local delay at manager `+0x370BFC` must not be assumed zero merely from
network mode. Initializers `0x211A0` and `0x24220` seed it to 35. The completed-run
maintenance `0x1CD20` sets it to zero for native modes 0 and 99; it is called late
in RunTick, after the input and due-command phases. Native `0x24E80` also updates
the local reference tick at `+0x370BA8` in those modes. Network-only paths in
`0x27DE0`/`0x27F30` adjust the delay dynamically. Local selection/order integration
therefore still needs a confirmed initialized input/queue context; mode alone is
not a proof of equal scheduling for two commands straddling initialization.

In contrast, local queue `0x23990` takes its source from manager `+0x36FA50`
(module RVA `0x88E3D70`), the same native value written by `DLL_SetEditorPlayer`
at `0x872E0`. It stores that value into slot `+4` and uses it in the command-ID
formula. Changing the native view and then queuing a normal command can therefore
change the claimed local network source. A report viewpoint is not authorization.

## Unit selection is more than a highlighted object

The input dispatcher `0x8C5F0` uses a current selected tribe and several local
and synchronized selection stages. `0x191130` builds a 1250-byte unit mask and
reserves a tribe through `0x11D1B0` before queueing opcode 16. Reservation marks
tribe header `+0x2A`; the multiplayer branch partitions candidates by player ID
and uses an expiry value. These are real state changes that must be considered
before redirecting input; capturing only the eventual move Chore is insufficient.

`0x195E30` stages tribe, destination and command/context flags and queues opcode
17 **before** its subsequent presentation/path/sound checks. The opcode-17
handler `0x10AE0` transfers eight bytes and dispatches `0x196100`. A tribe slot
alone does not provide a stable list of affected unit identities or permission to
control its members.

Do not conflate the unit's typed `r_UnitHover` at `+0x30`, used for local marking,
with `r_UnitSelected` at `+0xAE`, used by synchronized selection. The latter is
compared to a player ID, not merely a Boolean. Native unselect receiver
`0x19A5D0` clears it for the executing owner's units. Native local marking clear
`0x186C50` clears the former. These are distinct selection lifetimes; neither is
the ownership field at typed `+0x92`. See also the
[four-byte wire/typed-record distinction](DESYNC_CAPTURE.md).

For this image, `0x11D1B0` writes reservation value 1 in local network mode 0;
the other branch writes the low 16 bits of current native tick plus 400.
`0x119CE0` creates the requested group by clearing its full record, assigning
owner/state and a new global identity. A rejected selection must not simply clear
the reservation of whatever group later occupies the same slot. Its captured
world, group identity, inactive state and reservation still need to match.

Opcode 59's handler `0x15A50` temporarily zeros the reservation at group-manager
`+0x2A + groupId * 0x688` while copying a complete group, then restores its local
value. This establishes that the ordinary full-group synchronization excludes
that local reservation. It does not establish that every save/load route clears
or repairs outstanding reservations.

## RunTick input boundary

Additional current-image displacement check on 2026-11-02: within the full
`DLL_RunTick` unwind range `[0x86680,0x86F72)`, the input boundary at
`[0x868F6,0x86900)` is CMOVNE EAX,R12D (4 bytes), then MOV [RIP+disp32],EAX
(6 bytes). The store resolves to native viewpoint RVA `0x88E3D70`. R12D is set
to zero at `0x868D6`, and EAX gets EBX at `0x868E3`. Flags come from the
spectator comparison at `0x868DC`; intervening MOV/MOVZX instructions leave
them intact. The unchanged +10 continuation is MOVZX R8D,R14B. The complete
function's decoded direct branches/calls and baseline xrefs have no target inside
the ten-byte span. This instruction/relocation check does not prove control rights
or the safety of all downstream commands.

## Pending queue and save restoration

Additional current-image audit on 2026-11-02: the section-based writer's table
at `0x336B20` has no source range overlapping the 500 pending slots at module
RVA `[0x8624F18, 0x86C1318)`. Group records are different: table rows 58 and 59
start at `0x7CC6748` and cover respectively 1500 and 4500 records of 0x688 bytes,
including their reservation field.

`DLL_LoadSaveGame` at `0x853F0` calls unload `0x5BC70` and reset `0x87D10`
before reader `0x87D0`. Reset calls `0x211A0`, which calls `0x241B0` to zero
all 640000 bytes of the pending-slot array and reset its cursor. After a successful
load, `0x853F0` separately calls `0x186C50` and `0x19A500` to clear local marking
and synchronized selection. Thus pending native commands do not survive this
section-table/load-reset path; per-command control sidecars must be retired,
not restored as another persistent command queue. This is static evidence for
these routes, not live save acceptance or proof of reservation normalization.

## APIShared adapter [mod integration]

The additive authenticated Chore channel reads only a phase-0 opcode-106 slot,
requires matching source/execution IDs in 1..8, and rechecks phase, slot, opcode,
source, tick and command identity. A process-rooted managed scope around SE's
native-only blob dispatcher binds the exact packet ID/body to that pending slot
before deserialization. It validates the four-byte native length prefix, payload
size and completed transfer cursor. Nested calls restore the previous scope;
an invalid or unrelated nested call cannot inherit its issuer. Typed delivery
rechecks the captured native tuple after consumer validation. Steam/worker events
without this exact thread scope are rejected before native reads. Activation and
payload/authority predicates run only after this verification, so a rejected
Steam/worker event cannot invoke consumer game-state reads. This prevents
a reentrant observer from attributing an older packet to a newer native context.
The implementation does not replace the Script Extender's
opcode-106 handler or change old channels. Hash-bound layout checks do not prove
grant validity, compatible peers or stable object ownership.

SE's public `SendPacketToAllEx2` can serialize again and fall back to Steam.
The stronger channel therefore binds the installed internal raw submission
method, validates its loaded call contract and uses that method directly with one
captured serialization. The engine monitor, pending-byte staging lifetime and
native queue binding are checked. A native view differing from the real local
human identity rejects this submission; no source field is rewritten to compensate.
The loaded IL must use the actual engine monitor for both Enter and Exit, enclosing
the queue call in the same try/finally. Its mutable monitor/queue bindings are
rechecked before submission; a later replacement rejects the send.
This reader/adapter work is not a completed foreign-control backend.

## Saved local identity

The common native writer `0x52B0` uses the section table at RVA `0x336B20`.
Row 103 stores four bytes from module RVA `0x88E3D70`, the native viewpoint.
An active human looking through another player's view must therefore not save
that foreign viewpoint as the original local identity. The three current callers
are the ordinary export `0x86F80`, synchronized save/autosave handler `0x13030`
and Trail-template writer `0x816B0`; see [save routes](SAVE_WRITER_ROUTES.md).

The writer's entry saves RBX and RSI in two five-byte MOV instructions. An
Indirect/6 detour displaces ten complete bytes `[0x52B0,0x52BA)` and resumes at
`push rbp`. The complete PE unwind range ends at `0x5AD8`; current-image direct
branches/calls and xrefs have no targets inside the displaced span. The Script
Extender's append hook remains later in the original writer at `0x5A4C`.

The two initial data-preparation callees do not read the viewpoint: `0xB8220`
counts matching live building types; `0x186040` counts matching live unit records
into global RVA `0x3668E3C`. Neither rewrites unit/building owners. Changing the
view cell is not a change of `EditorDirector.ActivePlayerID` or a role grant.

## Local isolation and identity-guard fixtures [mod integration]

Single-owner singleplayer admission has its own registrations, grants, bounded
pending-command tickets and execution gate. It does not construct or consult
the multiplayer agreement, fragments, commit barrier or mixed-owner provider.
The backend router has separate failure domains and no cross-mode fallback.
The local native-queue sidecar is separately fixture-tested. It binds world,
manager, slot, command identity, source owner, opcode and copied native body;
the scheduled tick is deliberately not an identity component because Vanilla
may reschedule it. Consumed, rejected and revoked entries retain tombstones
until actual slot reuse or world retirement. Exceptions cannot make a queued
foreign command fall through to the ordinary untracked path on a retry. Native
producer/handler wiring and real reservation release remain pending.
Loss of the confirmed singleplayer world retires the local grants and pending
tickets for that session permanently. A later successful mode read cannot revive
them; another confirmed lifecycle start is required. A throwing mode adapter also
retires them before propagating its error to the backend's isolation boundary.
A stale mode callback that reentrantly replaces the session cannot retire that
replacement. The queue checks local session health before returning NotTracked,
including after a reservation cleanup callback; a missing sidecar is not a reason
to follow Vanilla after the local world was lost or queue cleanup failed.

The no-argument normal-handler ABI also permits a dormant native guard which tails
the complete original handler without a managed call or game lookup. Its isolated
machine fixture checks both tail destinations, one invocation, opcode delivery,
return value, stack, an ABI-preserved register and zero dormant allocations. The
primitive publishes no native handler-table entry and grants no control itself.

The optional active-human save guard is also independent of original-spectator
control. Fixtures verify nested writers, failure restoration, release during
an append callback, world replacement, retained identity after failed view
restoration and allocation-free dormant calls on the .NET Framework test host.
The writer adapter and actual NativeX64 Indirect/10 backend are tested against a
private ABI fixture. They are not yet wired into a published control capability
or a running game. Installation is startup-only; no late hook is permitted.
The guard also rejects world replacement during initial view reads/writes, avoids
rollback into the new world even after a throwing setter, and retains the actual
completed writer result if a later world check throws. The private native-world
identity in the central lifecycle remains stable through metadata checkpoints but
is retired before load/unload observers, independently of their reentrant order.

The input bridge additionally has a real NativeX64 Indirect/10 relocation fixture:
108 combinations execute the conditional move/RIP store through the production
intermediary and its original trampoline. Wrong actor/view/continuation validation
is rejected; no managed input callback runs. This is private executable memory,
not an installed control hook or an in-game permission/selection acceptance test.

A diagnostic fixture initially used the backend's thread-suspending defaults,
instead of the production startup adaptation. Combined fixtures then terminated
in CLR memory access after both native bodies and cleanup had returned. With
the actual non-suspending startup options, the native pair and combined control
suite pass, including forced collection while the writer hook remains rooted.
This comparison does not establish a general CLR/RedBird root cause or a live-game
acceptance result. No published game hook was modified by these fixtures.

## Audit limits [mod integration]

The full mixed-owner input/command backend, individual native command validation,
view-lease integration and live save identity protection, Spectator consumption, and genuine peer
acceptance are not established by these findings. They need separate completed
audits and tests before foreign control may be exposed. No live-game or real
host/client acceptance is claimed here.
