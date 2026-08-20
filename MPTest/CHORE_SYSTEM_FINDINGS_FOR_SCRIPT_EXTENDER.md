# Stronghold Crusader Definitive Edition Chore System

## Native findings, dynamic validation, and a proposed Script Extender API

Date: 2026-07-28

> **Current integration status:** The proposed integration was implemented in Script Extender
> 1.41.0 as the managed Chore-106 transport. Claims below that the extender currently lacks custom
> Chore registration describe only the compared historical commit.

Status:

- Native Chore format and scheduling path: sufficiently understood for a guarded prototype.
- Stateless custom Chore prototype using opcode `111`: implemented and dynamically validated.
- Native lockstep barrier behavior under an artificial 500 ms delay: dynamically proven.
- Reusable Script Extender `GameChoreAPI`: proposed, but not yet implemented.
- State-changing custom Chores: deliberately not attempted yet.

This document is intended as a technical handoff to the author of
[shcde-script-extender](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender).
It consolidates the managed analysis, native reverse engineering, prototype implementation, and
two-peer runtime evidence gathered so far.

## Executive summary

SHCDE uses a native deterministic lockstep command system called the Chore system. A normal Chore
receives:

- a pending-command slot;
- a native command ID;
- a scheduled simulation tick;
- serialization and deserialization through an opcode-specific handler; and
- inclusion in host-generated `SyncEvent` barriers.

The host periodically sends opcode `120` SyncEvents containing concrete command IDs. A client is not
allowed to advance past the corresponding target tick until every listed command ID exists in its
local pending queue. Static analysis also found a native three-second `SyncEvent - Forced run`
fallback.

This is fundamentally different from a Script Extender custom packet. A custom packet may use the
same reliable Steam transport, but it does not receive a native pending slot, command ID, scheduled
tick, or barrier membership.

For one exact `CrusaderDE.dll` build, opcode `111` has no static producer and its dispatch-table
entry points to a trivial `ret 0` handler. A strictly version-gated MPTest prototype replaced only
that table entry, queued fixed 16-byte no-op Chores through the original native enqueue function,
and performed no state mutation.

The final two-peer test executed 30 custom Chores:

- 15 originated on the host;
- 15 originated on the client;
- all 30 executed exactly once on both peers;
- all command IDs and final scheduled/actual ticks matched;
- six host SyncEvents each contained five prototype command IDs;
- all 15 remote host commands were held for 502-524 ms on the client;
- every corresponding SyncEvent arrived while its command was still held;
- the client repeatedly entered `EngineInterface.run` but never crossed the barrier tick;
- after reinjection, every held command still executed on the original barrier tick; and
- neither process logged an error, resync, forced run, duplicate execution, malformed payload, or
  legacy state-changing path.

This dynamically confirms that the original native enqueue path is a viable integration point for a
future guarded Script Extender API.

## Evidence terminology

This report uses these categories:

- **Confirmed statically:** directly reconstructed from native or managed code.
- **Confirmed dynamically:** observed on two real multiplayer peers.
- **Inferred:** a semantic name or behavior derived from use, without symbols or a dedicated runtime
  experiment.
- **Proposed:** API design that has not yet been implemented.

## Investigated game build

All native addresses and conclusions in this report apply only to:

| Property | Value |
|---|---|
| File | `x86_64/CrusaderDE.dll` |
| File size | `3,446,784` bytes |
| SHA-256 | `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4` |
| PE image base | `0x180000000` |
| PDB path embedded in the binary | `D:\Jenkins\.jenkins\workspace\CrusaderDE\CDE-DLL-STABLE\CrusaderDEDLL\Source\ff_gfx_manager\Release\Crusader.pdb` |
| PDB GUID / age | `{C1E26511-89D7-4315-843A-C3DF84430ECC}`, age `7` |

Addresses below are RVAs relative to the loaded module base. They must not be treated as stable
across game builds.

Static analysis used portable Cutter 2.4.1, Rizin 0.8.1, and Cutter's bundled Ghidra decompiler.
Nothing was installed system-wide or copied into the game directory for the analysis.

## Managed-side data path

### Local actions

Normal UI actions enter the native simulation through:

    EngineInterface.GameAction(...)
        -> DLL_GameAction(...)

For example, normal troop recruitment uses `GameActionCommand.MakeTroop`. That command does not
accept an arbitrary unit position or woodcutter-hut source, so it cannot represent the original
MPTest use case.

`EngineInterface.GameAction` holds `EngineInterface.threadLock` while calling native code.

### Simulation output

`EngineInterface.run(...)` calls `DLL_RunTick(...)` with
`MemoryBuffers.MemBuffer.MPChores`. After the simulation result is processed,
`Platform_Multiplayer.SendChores(...)` distributes the generated Chore buffer.

### Network transport

Native game Chores use `MPData.packetType = 1` and Steam Networking Messages channel 2. Incoming
packet-type-1 data is passed to:

    EngineInterface.ReceiveChore(playerId, data, dataLength)
        -> DLL_ReceiveChore(...)

The managed wrapper holds the same `EngineInterface.threadLock`.

### Why historical Steam-only Script Extender custom packets are not Chores

Script Extender custom packets:

- use dynamically registered custom packet types;
- are dispatched through `GameNetworkAPI.HandleRawPacket(...)`;
- do not use packet type `1`;
- are not passed to `DLL_ReceiveChore(...)`;
- do not create native pending-command slots; and
- are not automatically included in native host barriers.

Reliable delivery alone is therefore insufficient for deterministic simulation-tick execution.

## Reconstructed native Chore path

The effective command flow is:

    Local action or custom enqueue
        |
        v
    QueueLocalChore(ChoreManager*, opcode)
        |-- allocates a local pending slot
        |-- assigns player ID, command ID, and a provisional future tick
        |-- calls the opcode handler in mode 1
        `-- writes an outgoing packet-type-1 Chore
                                      |
                                      v
                         DLL_ReceiveChore on remote peer
                                      |
                                      v
                          native incoming FIFO/parser
                                      |
                                      v
                         handler mode 2 size query
                                      |
                                      v
                            remote pending slot

    At the final scheduled tick:
        local pending slot and remote pending slot
            -> same opcode handler in mode 0
            -> deterministic simulation effect

The sender does not receive its own network packet. Its later execution comes from the pending slot
created locally by `QueueLocalChore`. Remote peers create their pending slots from the received
packet.

This is why directly constructing a packet-type-1 message is not a complete replacement for the
native enqueue function: it would not create the sender's local slot through the equivalent path.

## Chore formats

### Outer Chore-buffer record

Confirmed managed-side and native-side format:

| Offset | Size | Meaning |
|---:|---:|---|
| `0x00` | 4 | Signed payload length |
| `0x04` | 1 | Target player when sending; sender player when stored in the incoming FIFO |
| `0x05` | variable | Inner Chore payload |

A negative length terminates the outgoing buffer. The total record size is `payloadLength + 5`.
Target player `0` is the normal broadcast case.

### Inner normal Chore

| Offset | Size | Meaning |
|---:|---:|---|
| `0x00` | 1 | Opcode |
| `0x01` | 3 | Scheduled tick, little-endian `uint24` |
| `0x04` | 4 | Command ID, little-endian `int32` |
| `0x08` | variable | Opcode-specific fixed-size payload |

The 24-bit tick wraps after `16,777,216` ticks, approximately 116.5 hours at 40 ticks per second.
Wrap behavior has not been tested.

Opcodes `0`, `1`, `125`, `126`, and `127` use special paths. The ordinary handler table is safely
addressable only through opcode `120` in this build. Bytes corresponding to `121` through `124` are
not reliable function pointers and must not be treated as free slots.

### Opcode 120 SyncEvent

After the normal eight-byte inner header:

| Overall offset | Size | Meaning |
|---:|---:|---|
| `0x08` | 4 | Barrier target tick |
| `0x0C` | 4 | Number of command IDs |
| `0x10` | 4 | Barrier sequence |
| `0x14` | `count * 4` | Command-ID list |

The semantic names are inferred from static use and confirmed by runtime correlation.

## Pending-command layout

The normal pending slot size is `0x500` bytes. The investigated build contains 500 slot candidates.

| Slot offset | Meaning |
|---:|---|
| `0x00` | Scheduled tick |
| `0x04` | Sender/player ID |
| `0x08` | Opcode |
| `0x09` | State; `1` represents a newly pending command |
| `0x0C` | Command ID |
| `0x10` | Already included in a host barrier |
| `0x11` | Start of opcode-specific parameters |

Relevant ChoreManager-relative offsets:

| Offset | Meaning |
|---:|---|
| `+0x84CC8` | Current slot index |
| `+0x84CCC` | Current handler mode |
| `+0x84CD4` | Handler-published payload size |
| `+0x0B0BF8` | Pending-slot array |
| `+0x370BF8` | Current field-copy cursor |

The field names are inferred from their use. The layouts are confirmed sufficiently for the guarded
prototype but are not backed by debug symbols.

## Handler table and ABI

The normal handler table is at RVA `0x2C5A30`. It contains eight-byte function pointers and resides
in writable committed memory in the tested build.

Handlers take no explicit arguments. They read global context from the ChoreManager and inspect the
mode at `ChoreManager + 0x84CCC`.

| Mode | Confirmed meaning |
|---:|---|
| `0` | Execute: deserialize fields from the pending slot and apply the simulation operation |
| `1` | Local enqueue/send: serialize staged fields into the local slot |
| `2` | Receive sizing: publish the expected fixed payload size |

The helper at RVA `0x1F5C0`, named `CopyChoreField` in the prototype, copies one field between a
managed/native address and the current slot parameter area, then advances the cursor.

Effective prototype signature:

    void CopyChoreField(
        ChoreManager* manager,
        void* data,
        int size,
        int usePendingSlot,
        int deserialize);

The prototype used:

- mode 1: `CopyChoreField(manager, payload, size, 1, 0)`;
- mode 2: publish `size` only; and
- mode 0: `CopyChoreField(manager, payload, size, 1, 1)`.

The fixed-payload requirement is important. The normal receive path asks the handler for a size
before copying the payload into a pending slot. A first public API should therefore use a fixed-size
envelope, even if the envelope contains an internal actual-length field.

## Important native functions

| RVA | Working name / meaning |
|---:|---|
| `0x080AE0` | Export `DLL_GameAction` |
| `0x0856F0` | Export `DLL_ReceiveChore` |
| `0x0858F0` | Export `DLL_RunTick` |
| `0x023960` | `QueueLocalChore` |
| `0x0237D0` | Build a normal inner Chore |
| `0x023E40` | Append a received record to the incoming FIFO |
| `0x023C00` | Pop the next incoming record |
| `0x0235F0` | Parse and dispatch an incoming payload |
| `0x023EE0` | Schedule a received normal command |
| `0x19C370` | Write an outer outgoing record |
| `0x01F5C0` | Serialize/deserialize one Chore field |
| `0x01F7B0` | Execute due pending Chores |
| `0x01BCC0` | Host/client barrier check, working name `CanAdvanceSyncFrame` |
| `0x01ADE0` | Opcode `120` SyncEvent handler |
| `0x027DB0` | Adjust dynamic turn/command delay |
| `0x024E50` | Frame-lag/skip calculation |
| `0x01CCF0` | Additional ping/lag management |
| `0x01F6B0` | Create synchronized autosave Chore |
| `0x0127A0` | Opcode `31` / `MakeTroop` handler |
| `0x02C5A30` | Handler table |
| `0x08571310` | ChoreManager data base |

`QueueLocalChore` has the effective signature:

    void QueueLocalChore(ChoreManager* manager, byte opcode);

It allocates a slot, assigns the local player ID and command ID, calculates a provisional future
tick, invokes handler mode 1, and emits the outgoing Chore.

Observed command IDs follow:

    commandId = playerId * 100000000 + perPlayerCounter

## Scheduling and barrier behavior

### Dynamic scheduling

The initial scheduled tick is not a fixed constant. Static analysis shows it is based on the current
tick, a tracked reference tick, and a dynamically adjusted command delay.

Runtime instrumentation discovered an additional practical detail: the scheduled tick observed
when a command is first queued or received is provisional. Before mode-0 execution, the slot is
updated to the host's final SyncEvent target tick.

Therefore:

- source player ID, request ID, and command ID are stable correlation identities;
- the initial and final scheduled ticks may legitimately differ; and
- a diagnostic tool must not report that tick update as an identity conflict.

The final tick in every accepted test matched the SyncEvent target tick and the actual mode-0
execution tick on both peers.

### Native barrier

The host normally emits an opcode-120 SyncEvent approximately every four ticks. It scans pending
slots, marks commands not yet included in a barrier, and writes their command IDs into the event.

The exact maximum number of IDs per SyncEvent remains inferred; static analysis suggested
approximately 25. Dynamic testing confirmed batches of five.

On the client, the barrier check verifies that every listed command ID exists in its local pending
queue. If an ID is missing, the simulation does not advance beyond the barrier target tick.

Static analysis found a three-second fallback that logs `SyncEvent - Forced run` and permits
advancement despite a missing command. The accepted prototype tests did not trigger this fallback.

## Why opcode 111 was used

Choosing opcode `111` is effectively adding a custom opcode within the existing one-byte native
opcode namespace. Extending the protocol outside the existing table would require invasive changes
to serialization, parsing, validation, pending slots, barriers, save/resync paths, and possibly
replay handling, without providing a practical benefit.

Static call-site analysis found:

- 140 direct calls to `QueueLocalChore`;
- 83 distinct constant opcodes;
- no direct static producer for opcode `111`; and
- opcode `111`'s table entry pointing to RVA `0xFC30`.

The expected handler bytes at RVA `0xFC30` are:

    C2 00 00 CC CC CC CC CC CC CC CC CC CC CC CC CC

This is a `ret 0` followed by padding.

Opcode `111` is not universally unused. In the HD predecessor it represented `Skirmish Add AI`.
Indirect or data-driven generation in DE also cannot be ruled out solely through direct-call
analysis. The result is therefore valid only for the exact DLL hash above.

Other trivial-looking entries were rejected because several had historical meanings, and opcode
`116` is still statically produced in DE.

## Guarded MPTest prototype

MPTest 1.2.0 implemented a no-op custom Chore as the Stage-1 safety gate.

### Compatibility guard

The patch is enabled only after validating:

- 64-bit process;
- exact file size and SHA-256;
- handler-table entry at RVA `0x2C5A30 + 111 * 8`;
- exact original handler pointer `moduleBase + 0xFC30`;
- exact original handler bytes;
- expected `QueueLocalChore` prologue at RVA `0x23960`;
- expected `CopyChoreField` prologue at RVA `0x1F5C0`; and
- writable committed memory for the handler-table entry.

Any mismatch disables the patch and the multiplayer test button. There is no best-effort mode.

### Handler installation

The table entry is replaced with a rooted Win64 managed delegate pointer. The delegate, pointer,
detours, and runtime instance remain rooted for the process lifetime.

They are intentionally not removed in `BaseUnityPlugin.OnDestroy()`. In this SHCDE/BepInEx
environment, the BepInEx manager GameObject destroys plugin components immediately after chainloader
startup even though the process and registered functionality continue running.

All callback exceptions are caught before crossing the native boundary.

### Prototype payload

The prototype used a fixed 16-byte payload:

| Offset | Type | Value / meaning |
|---:|---|---|
| `0x00` | `uint32` | Magic `0x31484353` (`SCH1`) |
| `0x04` | `uint16` | Protocol version `1` |
| `0x06` | `byte` | Source player ID |
| `0x07` | `byte` | Flags, required to be `0` |
| `0x08` | `int32` | Per-source request ID |
| `0x0C` | `uint32` | Sentinel `0xC011AB1E` |

Mode behavior:

- mode 1 serializes the staged 16 bytes;
- mode 2 publishes payload size `16`; and
- mode 0 deserializes, validates, correlates, logs, and performs no mutation.

### Enqueue

The multiplayer UI callback:

1. obtains the local human player ID;
2. creates one or more diagnostic payloads;
3. holds `EngineInterface.threadLock`;
4. stages one payload;
5. calls `QueueLocalChore(ChoreManager, 111)`; and
6. clears staging in `finally`.

The old custom packet, custom scheduling timer, and `CreateUnitLocal` path are not used in
multiplayer. Singleplayer behavior remains separate.

### Observability

Read-only detours observe:

- `Platform_Multiplayer.SendChores`;
- `EngineInterface.ReceiveChore`; and
- `EngineInterface.run`, used only to reinject intentionally delayed diagnostic Chores.

Logged events include:

- enqueue and batch boundaries;
- all three handler modes;
- outgoing and incoming Chore edges;
- source/request identity;
- command ID;
- provisional and final scheduled ticks;
- actual execute tick and execution sequence;
- SyncEvent target, sequence, ID list, and matched prototype IDs;
- delay hold, barrier observation, release, reinjection, and real elapsed time;
- repeated engine-run calls while held;
- duplicate execution and tick-invariant failures;
- resync and forced-run danger signals; and
- millisecond wall-clock timestamps.

Buffers are never modified by the observation hooks. Original methods are called exactly once,
except that a deliberately held opcode-111 packet is copied and passed to the original
`ReceiveChore` later.

## Dynamic test evidence

### Initial smoke and normal two-peer tests

Before the delayed test:

- a one-human multiplayer smoke test executed two host requests exactly once;
- a normal two-peer test executed 11 requests, six host-originated and five client-originated;
- every request had identical command IDs and final ticks on both peers;
- every command ID appeared in the matching outgoing host SyncEvent; and
- no crash, resync, forced run, duplicate execution, or mutation occurred.

### Comprehensive 500 ms test

Both peers ran:

    MPTest 1.2.0
    ComprehensiveBarrierTestEnabled = true
    BarrierTestIncomingDelayMs = 500
    CommandsPerClick = 5

Only incoming opcode-111 Chores on the non-host client were held. SyncEvents passed to the native
code normally. No simulation or network thread used `Thread.Sleep`.

Results:

| Measurement | Result |
|---|---:|
| Total logical requests | 30 |
| Host-originated requests | 15 |
| Client-originated requests | 15 |
| Mode-0 executions in host log | 30 |
| Mode-0 executions in client log | 30 |
| Host prototype batches | 3 |
| Client prototype batches | 3 |
| Relevant host SyncEvents | 6 |
| Prototype command IDs per relevant SyncEvent | 5 |
| Client-held remote commands | 15 |
| Delay holds | 15 |
| Barrier-observed events | 15 |
| Releases | 15 |
| Successful reinjections | 15 |
| Minimum real hold | 502 ms |
| Maximum real hold | 524 ms |
| Average real hold | 509.2 ms |
| Minimum repeated barrier-wait run calls | 15 |
| Maximum observed tick minus barrier target | 0 |
| Commands that crossed the barrier before reinjection | 0 |
| Errors in either complete process log | 0 |

For all 30 requests:

- source player ID and request ID matched;
- native command ID matched;
- final scheduled tick matched;
- actual execute tick matched;
- actual execute tick equaled the final scheduled/SyncEvent target tick;
- execution occurred exactly once per peer;
- mode 1 occurred once on the origin;
- mode 2 occurred once on the remote receiver;
- mode 0 occurred once on each peer; and
- mutation was explicitly reported as `none`.

For every one of the 15 delayed remote commands:

    incoming command observed
        -> command copied and held
        -> matching SyncEvent received normally
        -> client repeatedly reached the native barrier without crossing it
        -> command reinjected after at least 500 ms
        -> command executed on the original target tick

Client-originated commands correctly did not pass through the client's incoming-delay path. Their
local sender slots were created directly by the original enqueue function.

No log contained:

- `SyncEvent - Forced run`;
- resync start or end;
- native access violation;
- callback failure;
- malformed record or payload;
- command/request correlation collision;
- duplicate mode-0 execution;
- scheduled/actual tick mismatch;
- legacy custom packet or unit-spawn path; or
- any MPTest error.

This is direct runtime evidence that the native barrier, not merely normal network speed, enforced
the shared execution tick.

## Proposed Script Extender API

The following design is proposed but not yet implemented.

### Ownership

The Script Extender should centrally reserve and patch one native opcode, initially `111` for the
known DLL hash. Individual mods should not patch the native handler table themselves.

Mods register logical command types identified by GUID inside one shared fixed-size native envelope.
This permits many mods to use one carefully guarded native integration point.

### Suggested public surface

    GameChoreAPI.IsSupported
    GameChoreAPI.CompatibilityState

    RegisterCustomChore(
        Guid commandType,
        ushort protocolVersion,
        ushort maxPayloadLength,
        CustomChoreHandler handler)

    TryEnqueueCustomChore(
        Guid commandType,
        ReadOnlySpan<byte> payload)

Suggested handler context:

    CustomChoreContext
        CommandType
        ProtocolVersion
        SourcePlayerId
        RequestId

Suggested enqueue results:

    Enqueued
    UnsupportedBuild
    NotRegistered
    InvalidPayload
    SessionUnverified
    SessionIncompatible

Registrations should be process-wide, immutable, and allowed only before the first multiplayer
compatibility handshake. Duplicate GUIDs or incompatible version/size registrations must fail.

### Proposed fixed 256-byte envelope

| Offset | Size | Meaning |
|---:|---:|---|
| `0x00` | 4 | Magic |
| `0x04` | 2 | Envelope version |
| `0x06` | 2 | Actual payload length |
| `0x08` | 16 | Logical command GUID |
| `0x18` | 2 | Command protocol version |
| `0x1A` | 2 | Reserved flags |
| `0x1C` | 1 | Source player ID |
| `0x1D` | 3 | Reserved/padding |
| `0x20` | 4 | Request ID |
| `0x24` | 220 | Payload, zero-filled after actual length |

Mode 2 always publishes `256`. Mode 1 serializes the complete zero-filled envelope. Mode 0 validates
all fields and synchronously invokes the registered handler.

The handler must be deterministic, must not retain pointers into the native slot, and must not throw
across the callback boundary.

### Multiplayer compatibility handshake

All human peers must have exactly compatible registrations before custom Chores can be enqueued.

A suitable handshake can still use a Script Extender custom packet because it negotiates
capabilities rather than applying simulation state.

Proposed behavior:

1. Reserve one internal Script Extender packet, for example `CustomPacketStart + 4`.
2. Use an explicit `IMessagePackFormatter` with stable numeric keys.
3. Host sends a manifest request through the existing lobby-info lifecycle.
4. Each client returns a sorted manifest of GUID, protocol version, and maximum payload size.
5. Host compares every human peer and distributes the result.
6. `TryEnqueueCustomChore` remains disabled until all manifests match exactly.
7. Missing or old peers remain `Unverified`; map start converts unresolved sessions to
   `Incompatible`.
8. Singleplayer does not require a handshake.

An unmodified peer would execute opcode `111` as a no-op while modified peers applied a state change.
Allowing custom Chores without the handshake would therefore guarantee a desync.

## Required lifecycle and safety rules

- Check the exact DLL hash and native signatures before changing the table.
- Never patch on a best-effort basis.
- Use the shared Script Extender registry to prevent two mods claiming the same native opcode.
- Hold `EngineInterface.threadLock` around staging and `QueueLocalChore`.
- Keep delegates, native pointers, detours, and registration state rooted for the process lifetime.
- Do not dispose process-lifetime hooks from `BaseUnityPlugin.OnDestroy()`.
- Catch every managed exception inside native callbacks.
- Keep mode-0 execution synchronous and deterministic.
- Never execute the gameplay effect directly from the UI callback or custom-packet handler.
- Use mode 0 as the only multiplayer state-changing path.
- Use fixed-size, bounded, zero-filled payloads.
- Disable enqueue until peer compatibility is proven.
- Include millisecond timestamps, source/request IDs, command IDs, scheduled ticks, actual ticks,
  handler modes, and barrier membership in diagnostic logging.

## What remains unproven

The following work is still required before calling the API production-ready:

1. Multiplayer save/load with pending custom Chores.
2. Native resync while custom Chores are registered or pending.
3. Map changes and repeated multiplayer sessions with the final API.
4. Disconnects, host departure, and host migration behavior.
5. At least 100 no-op API commands in one session.
6. Deliberately incompatible manifests and old/unmodified peers.
7. Slot recycling under sustained command volume.
8. The exact per-SyncEvent command-ID limit.
9. Behavior around the 24-bit tick wrap.
10. Investigation of any indirect or data-driven producer of opcode `111`.
11. A deliberate missing-command test of the native three-second forced-run fallback, if it can be
    performed safely.
12. A new signature/validation set for every future game DLL.

The current evidence is strong enough to proceed from the private no-op prototype to a centrally
owned Script Extender API. It is not yet sufficient to enable arbitrary state-changing handlers for
general users.

## Relevant implementation and test artifacts

Workspace-relative files:

- `CHORE_SYSTEM_HANDOFF.md`: original managed-side investigation handoff.
- `CHORE_SYSTEM_NATIVE_ANALYSIS.md`: detailed German native analysis.
- `MPTest/src/NativeChoreProbe.cs`: guarded opcode-111 prototype and instrumentation.
- `MPTest/src/MPTestRuntime.cs`: multiplayer button integration and five-command batches.
- `MPTest/src/MPTestPlugin.cs`: test-profile configuration and process-lifetime initialization.
- `MPTest/Compare-ChoreProbeLogs.ps1`: comprehensive two-peer log validator.
- `MPTest/Test-ChoreProbeComparer.ps1`: positive and negative comparer self-test.
- `MPTest/tests/fixtures/`: synthetic comprehensive host/client log fixtures.
- `MPTest/README.md`: runtime test procedure.

The accepted prototype DLL had:

    MPTest version: 1.2.0
    SHA-256:
    D60BE25BB89FE4FC1E68A790932B09D97F2714A0920ABFE4FDF8FFA8353958EC

The real runtime logs are local test artifacts and are not required to understand the implementation;
the exact acceptance measurements are reproduced above.

## Related projects and prior-art value

### OpenSHC and HD reverse-engineering notes

These provided the most useful semantic correspondence:

- [`GameSynchronyState.hpp`](https://github.com/sourcehold/OpenSHC/blob/1acd3d86810b060e04de694923151404fa7286f6/src/OpenSHC/Synchrony/GameSynchronyState.hpp)
- [`GameCommand.hpp`](https://github.com/sourcehold/OpenSHC/blob/1acd3d86810b060e04de694923151404fa7286f6/src/OpenSHC/Commands/GameCommand.hpp)
- [`GameCommandType.hpp`](https://github.com/sourcehold/OpenSHC/blob/1acd3d86810b060e04de694923151404fa7286f6/src/OpenSHC/Commands/GameCommandType.hpp)

HD-to-DE correspondence:

| HD function | DE RVA | DE working name |
|---|---:|---|
| `queueCommand` `0x00489100` | `0x023960` | `QueueLocalChore` |
| `scheduleReceivedCommand` `0x00480210` | `0x023EE0` | Schedule received command |
| `serializeOrDeserializeCommandParameter` `0x004805D0` | `0x01F5C0` | Chore field copy |
| `processWaitingCommands` `0x004892F0` | `0x01F7B0` | Execute due Chores |
| `updateTurnDelayFromSyncPacket` `0x00488010` | `0x027DB0` | Adjust dynamic delay |

The correspondence is based on matching layouts, mode transitions, cursor-copy behavior, scheduling,
and lag handling, not only similar call positions.

### shcde-fixes

Useful mainly as implementation prior art for:

- pattern scanning;
- native hooks and detours;
- managed callbacks through native stubs; and
- process-lifetime rooting.

Compared commit: `400a7c7c75332ff6fbddc854f3e0ce1fadce19b8`.

### Crusader DE Tweaker

Useful confirmation that `EngineInterface.GameAction` can be detoured while preserving the original
Vanilla path. It did not contain additional Chore internals.

Compared commit: `d8b16dca9633871dcb152df5dec4d0c594a02f66`.

### Stronghold Crusader DE AI Buff

Useful as a general deterministic multiplayer-mod example, but it did not access the Chore queue.

Compared commit: `c8a4a86f3c9845cf558d31582eb6f25566b72e95`.

### UCP3

Useful as general native-patching prior art for the HD predecessor, but OpenSHC and the local HD
reverse-engineering notes contained the more relevant command-queue information.

Compared commit: `02a7a6bc8ab956a91fc752e8c8ed215c149855e7`.

### Local Script Extender

The compared Script Extender already located the ChoreManager and provided established pattern-scanner,
hook, API, packet, and lobby lifecycle infrastructure. That historical commit lacked custom Chore
registration and enqueue functionality; version 1.41.0 and newer include both.

Compared commit: `9ddb419ca6a5f05d7c8f85a10ba0795c1193c318`.

## Bottom line

The original native enqueue function is the correct integration point.

The following alternatives are incomplete:

- directly calling `CreateUnitLocal` on every peer;
- sending a Script Extender custom packet with a fixed future tick;
- manually constructing packet-type-1 data without creating the sender's local pending slot; or
- feeding arbitrary bytes into `DLL_ReceiveChore`.

A safe next step is to move the proven no-op bridge into the Script Extender, reserve opcode `111`
centrally for the exact supported DLL build, add a fixed-envelope logical command registry and a
strict multiplayer manifest handshake, and repeat the accepted no-op tests through the public API
before enabling any gameplay mutation.
