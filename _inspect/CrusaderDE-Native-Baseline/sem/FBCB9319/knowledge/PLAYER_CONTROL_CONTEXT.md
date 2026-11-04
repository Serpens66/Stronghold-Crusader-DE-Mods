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

The authenticated Chore channel reads only a phase-0 opcode-106 slot, requires matching
source/execution IDs in 1..8, and rechecks phase, slot, opcode, source, tick and command
identity. A process-rooted managed scope around SE's native-only blob dispatcher binds the
exact packet ID/body to that pending slot before deserialization. It validates the four-byte
native length prefix, payload size and completed transfer cursor. Nested calls restore the
previous scope; an invalid or unrelated nested call cannot inherit its issuer. Typed delivery
rechecks the captured native tuple after consumer validation. Steam/worker events without this
exact thread scope are rejected before native reads, and activation and payload/authority
predicates run only after this verification. The Script Extender's opcode-106 handler stays in
place. Hash-bound layout checks do not prove grant validity, compatible peers or stable object
ownership.

SE's public `SendPacketToAllEx2` can serialize again and fall back to Steam. The authenticated
channel therefore binds the installed internal raw submission method, validates its loaded call
contract and uses that method directly with one captured serialization. The engine monitor,
pending-byte staging lifetime and native queue binding are checked. A native view differing
from the real local human identity rejects this submission; no source field is rewritten to
compensate. The loaded IL must use the actual engine monitor for both Enter and Exit, enclosing
the queue call in the same try/finally. Its mutable monitor/queue bindings are rechecked before
submission; a later replacement rejects the send.

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

## Unfinished control backend [mod integration]

Rights-gated control of individual native commands, several owners at once and multiplayer
control are not part of APIShared's main line; their unfinished backend and its private fixtures
(single-owner admission, native-queue sidecar, handler bridge, local control coordinator) are kept
on the APIShared branch `wip/player-control`. Before such a backend may retire control, it needs
a native queue reset/drain proof: removing managed sidecars does not remove queued native
slots, and a zero-demand handler bridge jumps to the original handler. A diagnostic fixture
showed that backend thread-suspending defaults differ from the production startup adaptation;
only the non-suspending startup options were validated.

## Second spectator-input boundary and Vanilla deselection (2026-11-02) [Vanilla facts]

Rechecked against the canonical native SHA-256 above. Inside the input dispatcher
`0x8C5F0` (called only from `DLL_RunTick` and recursively), the branch at
`0x8D0D0..0x8D109` requires stack flags `[rsp+0xD8]==0`, `[rsp+0xD0]==0`, BL==0 and
ESI==1, loads the input state `[0x1860AD56C]` into EBX, calls `0x897F0` and continues
only if that previous state was nonzero. It then loads EBX=[view] (`0x8D10B`), compares
the spectator flag `0x3666080` (`0x8D115`), executes CMOVNE EAX,EDI (`0x8D128`, EDI=0
from `0x8D0C8`), XOR R9D,R9D (`0x8D12B`) and MOV [view],EAX (`0x8D12E`), recursively
calls `0x8C5F0` with mode 3 (`0x8D15B`) and restores EBX (`0x8D16D`). Spectator map
input is therefore suppressed at this nested re-dispatch as well as in `DLL_RunTick`.
No direct branch in the dispatcher targets `0x8D129..0x8D133`, and a byte-level scan
of `.text` found no rel8/rel32 branch or call into that span or `0x868F7..0x86903`.

`DLL_GameAction` `CloseTroopsPanel` (1040, `0x410`) calls `0x897F0`: it resets app
modes `0x3D`/`0x3E`, and with a nonzero local marked count (`unit manager +0x20`)
calls `0x19A5B0` (opcode 15 via the local queue `0x23990`, whose source is the
current native view) and `0x82F0`; it then clears local marking (`0x186C50`) and the
input state. `EditorDirector` uses the same action for right-click deselection. The
managed wrapper has no branch for its zero result. Whether the opcode-15 handler
releases every group reservation (`+0x2A`) is not established statically.

The spectator flag has further readers (`0x41D60`, `0x6E620`, `0xB5690`, `0x1D160`,
`0x65830`, `0x94350`, `0x884D0`, `0x38A0`, `0x109EF0`, GameAction `0x40C`). For example
`0x1D160` tests `human[view]==0 && !spectator`; clearing the flag to grant input would
change these unrelated semantics. Managed placement/demolition
(`EditorDirector` -> `PlaceMapperItem`/`DeleteBuilding`) passes
`EditorDirector.ActivePlayerID`, which `DLL_MapAction` writes temporarily as the view.

## Player-view identity and spectator input control [mod integration]

`PlayerViewIdentityRuntime` owns one process-wide `PlayerViewIdentity` core for positive-player
views; the common Save writer guard (`PlayerViewSaveScope`, Indirect/10 at `0x52B0`, installed
during startup), the raw Chore submission and the local-ID correction share it. Immutable
owner/world/participant/token leases prevent role adoption and stale cleanup; only the
lifecycle's world invalidation retires ended worlds. Eliminated-participant views require this
protection for foreign views. Fixtures verify nested writers, failure restoration, world
replacement during view reads/writes and allocation-free dormant calls.

Spectator input control installs both input boundaries (`[0x868F6,0x86900)` Indirect/10 and
`[0x8D128,0x8D12E)` Indirect/6, the latter followed by the unchanged store) for this hash only,
on the first consumer registration (none without a consumer). A clear spectator comparison (ZF
set) always follows the original block. `LocalInputControl` owns the actor cell;
`PlayerPerspectiveAPI.TrySetSpectatorControl` deselects as the old view, confirms the new view
and only then publishes the actor, all under the engine monitor that serializes `DLL_RunTick`.
World invalidation resets the actor without commands. This is full Vanilla input of one player
for original offline spectators. Spectator (the consumer) uses it for original spectators;
BugfixesAndQoL shows the Vanilla troop HUD for the controlled player's units. Executable
fixtures cover both stub forms and the 108 relocation combinations of the RunTick bridge.

## Audit limits [mod integration]

No live-game or real host/client acceptance is claimed. Individual native command validation,
mixed-owner control and multiplayer control are not established (see the unfinished backend
above). Whether Vanilla's opcode-15 deselection releases every group reservation is unproven.

## Human registration and controlled AI players (2026-11-02) [Vanilla facts]

`0x8574BCC` is the per-player human registration (written by load `0x853F0`, kick
`0x84A30`, start `0x8AE80` and, in editor mode only, `DLL_SetEditorPlayer`). The input
dispatcher `0x8C5F0`, selection `0x191130`, order staging `0x195E30`, opcode-17 handler
`0x196100` and deselection `0x897F0` do not read it. `0x8AA20` admits building interaction
when the owner's team equals the view's team (or for a non-human AI owner); `0xCC420` has an
AI branch in placement validation that reflects Vanilla's own rules for that owner. Managed
player-ID arguments outside the native view appear only in `PlaceMapperItem`/`DeleteBuilding`
(`EditorDirector.ActivePlayerID`), the ally panel (`GameData.playerID`), insults, chat and merit.
Static evidence; controlling an AI player in a live mission still needs in-game confirmation.

## Multiplayer progress and frame skipping (2026-11-03) [Vanilla facts]

The following chain was checked against the canonical native hash above, including
4,589 decoded instruction byte comparisons across the relevant seventeen functions.
Internal names remain candidate labels; the stated loads, branches and offsets are
instruction-backed facts for this image. Manager base `0x8574320` is shared by the
Chore scheduler and multiplayer progress state.

`DLL_RegisterMultiplayerUser` (`0x864C0`) records a local human's declared ID at
manager `+0x8A8` and sets host flag `+0x9A0` when that local ID is 1. It separately
writes native view `+0x36FA50` (module RVA `0x88E3D70`). Changing the view with
`DLL_SetEditorPlayer` (`0x872E0`) does not change the declared ID or host flag in
multiplayer mode. That setter changes the human registration array only in editor
game mode 1; positive multiplayer view changes write only the native view cell.

The ordinary heartbeat is not the Script Extender's custom Chore sender. Late in
`DLL_RunTick` (`0x86680`), `0x1CD20` sends it through `0x27F30`, normally after at
least 100 ms (with 20 ms added for each participant beyond two). The paused/start
maintenance route `0x24FD0` also calls `0x27F30`, after 179 ms, for non-hosts.
`0x27F30` exits for network modes 0 and 99. Its packet type is a byte argument;
it has no explicit local-player argument.

Heartbeat `0x27F30` writes current native tick into both
`manager+0x14D030+player*4` and `manager+0x14D054+player*4`, and `timeGetTime()`
into `manager+0x14D078+player*4`. Each of the three player loads independently reads
`manager+0x36FA50`, the presentation view. The writes occur only when connection
pause field `+0xEE4` (RVA `0x8575204`) is less than 1. This field is written by
`DLL_ConnectionPause` (`0x81360`) and reset by `0x87D10`; it is not the spectator
flag at RVA `0x3666080`. Connection/save pause also changes the heartbeat's lag byte
to `0x7F`. The native payload is five bytes: packet type, the low three tick bytes
copied by `0x7130`, and the lag byte. `0x19D3C0` appends its length, destination
and bytes to the native-to-managed outgoing buffer.

Managed `Platform_Multiplayer.SendChores` forwards the outgoing payload using the
Steam peer connection. On reception, packet type 1 calls
`EngineInterface.ReceiveChore(fromMember.playerID, ...)`, under the engine monitor.
`DLL_ReceiveChore` (`0x86480`) buffers that peer ID through `0x23E70`; `0x23C30`
returns it with the next packet. Receive dispatcher `0x23620` routes heartbeat
types 0 and 1 through `0x27DE0`. Identity helper `0x1F9A0` preserves the received
peer ID. The receiver therefore updates the authenticated peer's progress index,
while the sender's own local progress update uses its selected native view.
This asymmetry is present even if custom Chore submission is separately protected.

Before a real simulation pass, `DLL_RunTick` calls `0x24E80(manager, nativeView)`.
The player argument is loaded at `0x869D2`; `0x24E80` retains EDX through its call
to `0x1F9B0`. That function finds minimum/maximum progress among registered humans
(slots 1 through 8 whose manager `+0x8AC+slot*4` is not -1), but chooses the local
reference from `manager+0x14D030+player*4`. It produces signed ahead/behind values
at `+0x370BB4`/`+0x370BB8` relative to that selected reference. Selecting an AI can
therefore advance the AI-indexed heartbeat while leaving the sender's real human
slot stale in its own local table. Other peers still receive heartbeats as the
real human. A selected view is not a substitute for the participating identity.

`0x24E80` resets skip count `+0x370BEC` (module RVA `0x88E4F0C`) to zero. Mode
0/99 and its `+0xDA8` early-return path do not calculate network skip demand. A
negative behind value returns before the ahead path. Positive ahead must exceed
4 and at least 50 ms must have elapsed since `+0x370BE8` before demand is set.
Hosts request 1 for ahead 5..14, 2 for 15..99, and 3 for at least 100. Non-hosts
request 1 only for ahead greater than 8. The timestamp is updated on the elapsed
path even when the non-host threshold does not request a skip. The additional
ahead-versus-delay condition can set manager `+0x279F58`; it does not rewrite the
selected player or host role.

Serializer `0x19D960` copies this count to return-buffer byte `+0x78C` only when
the incoming RunTick `mpFrameSkip` flag (global RVA `0x8EBA92`) is zero; otherwise
it writes zero. Managed `PlayStateReturnData.SkipFrame` is a signed 16-bit field
at offset 1932, copied unchanged into `PlayState`. `EngineInterface.run` passes a
positive result to `Director.MPSkipFrame`, which adds it to `FrameToSkipMP` only
in multiplayer. `Director.runSimTickTest` passes `FrameToSkipMP>0` into the next
`run`, and decrements positive demand after each call, including the call that
created it. Demand 3 thus gives two subsequent skipped calls per real pass.

The separate native sync-event gate `0x1BCF0` uses the host flag, tick counter,
sync-event queue, due-command barrier list and a 3-second forced-run fallback.
It does not index participant progress through the selected view. RunTick may set
its local simulation-skip flag when this gate declines; its original input flag
still controls the serializer above. These two skip decisions must not be merged.

Managed `lowFPSCompensationLevel` is a third source of incoming skipped calls.
`Director.SetEngineFrameRate` derives it from requested simulation speed: 4 for
at most 10, 3 for at most 15, 2 for at most 20, otherwise 1. The timer runs at
requested speed multiplied by this level; intermediate calls are marked skipped.
It is not driven by measured rendering FPS. A 40-call/s, two-thirds-skipped trace
alone cannot distinguish level 3 at approximately 13.33 requested speed from
native demand 3. Record the level and native ahead/behind/SkipFrame values when
that distinction matters; PerfProbe's existing render snapshots include the level.

## Positive participant view integration boundary [mod integration]

A narrow correction can substitute the protected positive participant index at
four loads without changing native presentation view: `[0x869D2,0x869D8)` (six
bytes, MOV EDX,[RIP+view]) and heartbeat `[0x27FEB,0x27FF2)`,
`[0x27FFF,0x28006)`, `[0x28019,0x28020)` (seven bytes each,
MOVSXD RCX,[RBX+0x36FA50]). Full enclosing-function control flow and known global
xrefs have no entry inside these single-instruction spans. Their continuations
are respectively MOV RCX,R14; MOV EAX,[tick]; MOV EAX,[tick]; and
MOV [RBX+RCX*4+0x14D078],EAX. Current local Extender, canonical Fixes and APIShared
sources have no competing hook at these sites.

The installed NativeX64 backend (SHA-256
`0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0`)
selects an Indirect patch's six-byte minimum and rounds up by whole instructions.
These four sites consequently need exactly 6/7/7/7 bytes; the separate context-hook
backend's fourteen-byte minimum must not be used here. A positive protected lease
must be unavailable until every site passes preparation and publication validation.
An inactive cell follows the complete original loads, including zero/negative
native spectator IDs. A failed partial publication must retain all executable
roots with that cell inactive. This describes the bounded integration contract;
it is not evidence of live host/client acceptance of a corrective build.
