# CrusaderDE Chore system knowledge

## Scope and confidence

This document is the self-contained human-readable companion to the structured Chore knowledge records in this directory. It describes native facts, runtime observations, and still-open interpretations. Addresses are RVAs relative to image base `0x180000000` and are valid only for the explicitly named binary hash.

Confidence is deliberately split by fact. `confirmed-runtime` means that a focused runtime trace observed the behavior. `confirmed-static` means that complete native data flow, an export, or an exact binary contract establishes it. `probable` is a strong semantic reconstruction. `candidate` is a lead that still needs validation. Historical HD names never establish DE semantics by themselves.

The historical analysis and accepted two-peer runtime trace used:

- Native SHA-256 `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`
- File size `3,446,784` bytes
- PDB reference `D:\Jenkins\.jenkins\workspace\CrusaderDE\CDE-DLL-STABLE\CrusaderDEDLL\Source\ff_gfx_manager\Release\Crusader.pdb`
- PDB GUID `{C1E26511-89D7-4315-843A-C3DF84430ECC}`, age `7`

The current canonical baseline and installed game use:

- Steam build ID `24816905`
- Native SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- File size `3,451,392` bytes

## Managed and network path

Managed UI actions normally call `EngineInterface.GameAction(...)` while holding `EngineInterface.threadLock`. Native `DLL_GameAction` stores opcode-specific parameters and queues a native Chore. `DLL_RunTick` emits outer Chore records through the managed multiplayer layer. Native Chores use multiplayer packet type 1 on Steam channel 2. Received data is passed through `EngineInterface.ReceiveChore` to `DLL_ReceiveChore`, appended to the native incoming FIFO, parsed, and converted into a remote pending slot.

The sender does not receive its own network record. Its pending slot is created during local enqueue. Receivers create their pending slots from the incoming record. At the scheduled tick, both paths call the same opcode handler in execute mode.

Historical Script Extender custom Steam packets were reliable transport but did not create a native pending slot, command ID, scheduled tick, or SyncEvent barrier membership. The later Script Extender Chore transport reserves opcode 106 and routes its payload through the native queue.

## Wire formats

### Outer managed/native record

Each outgoing or incoming record is:

| Offset | Size | Meaning |
|---:|---:|---|
| `0x00` | 4 | Signed payload length |
| `0x04` | 1 | Outgoing target player ID or incoming sender player ID |
| `0x05` | variable | Payload |

Outgoing target `0` means normal broadcast. The outgoing buffer is terminated by a negative payload length. A record consumes `payloadLength + 5` bytes. `DLL_ReceiveChore` reconstructs the same shape with the separately supplied sender ID. The observed incoming FIFO capacity is approximately one million bytes; overflow drops a record in the inspected path.

### Normal inner Chore

| Offset | Size | Meaning |
|---:|---:|---|
| `0x00` | 1 | Opcode |
| `0x01` | 3 | Scheduled tick, little-endian unsigned 24-bit |
| `0x04` | 4 | Command ID, little-endian signed 32-bit |
| `0x08` | variable | Opcode-specific payload |

The 24-bit tick is zero-extended on receive. Its theoretical wrap is `16,777,216` ticks, approximately 116.5 hours at 40 ticks per second; such a match was not tested.

Opcodes `0`, `1`, `125`, `126`, and `127` use special timing, synchronization, compression, or ping paths. The normal handler table is valid through opcode `120`. Bytes corresponding to `121` through `124` are not reliable normal handler pointers.

## Pending slots and handler ABI

A normal pending slot is `0x500` bytes. The inspected queue has 500 slot candidates.

| Slot offset | Meaning |
|---:|---|
| `0x00` | Scheduled tick |
| `0x04` | Sender/player ID |
| `0x08` | Opcode |
| `0x09` | State; `1` denotes a new pending command |
| `0x0C` | Command ID |
| `0x10` | Already included in a host barrier |
| `0x11` | Opcode-specific parameters |

Relevant ChoreManager offsets are current-slot index `+0x84CC8`, handler mode `+0x84CCC`, payload size `+0x84CD4`, field cursor `+0x370BF8`, and pending slots `+0xB0BF8`.

Handlers take no explicit parameters and read the active context through ChoreManager state:

- Mode `0`: deserialize/use slot fields and execute the simulation effect.
- Mode `1`: serialize the locally staged values into the slot/outgoing record.
- Mode `2`: publish the expected receive payload size.

The field-copy helper moves one field in the direction selected by the mode and advances the cursor. Mode 2 makes fixed payload sizes the safe baseline contract. A bounded envelope can carry a logical length, but truly variable native payload sizing needs additional receive-dispatch handling.

## Scheduling, identity, and barrier

Observed command IDs follow `commandId = playerId * 100000000 + perPlayerCounter`.

Initial scheduling is dynamic, approximately `max(currentTick, trackedReferenceTick) + currentDynamicCommandDelay`. A special path uses `currentTick + syncPeriod * 50`; a period of four produces 200 ticks. Runtime tracing established that the first scheduled tick is provisional. Before execute mode, the host SyncEvent can move the pending slot to its final target tick. Source player, request ID, and command ID remain stable correlation identities; changing scheduled ticks are not identity conflicts.

The host normally emits opcode-120 SyncEvents approximately every four ticks. It scans pending slots, marks commands not already assigned to a barrier, and includes their command IDs. The payload begins with target tick, command-ID count, and sequence number followed by the IDs. Static analysis indicates a maximum of 25 IDs; runtime testing confirmed batches of five.

At the target tick, a client checks whether every listed command ID exists locally. A missing command stalls simulation advancement. After approximately three seconds, the native path logs `SyncEvent - Forced run` and permits progress. This fallback was found statically but was not triggered in the accepted runtime test.

## Native function map

| Historical RVA | Current RVA | Working meaning |
|---:|---:|---|
| `0x23960` | `0x23990` | Queue local Chore |
| `0x237D0` | `0x23800` | Build normal inner Chore |
| `0x23E40` | `0x23E70` | Append received outer record |
| `0x23C00` | `0x23C30` | Pop incoming record |
| `0x235F0` | `0x23620` | Parse and dispatch incoming payload |
| `0x23EE0` | `0x23F10` | Schedule received normal command |
| `0x19C370` | `0x19D3C0` | Write outgoing outer record |
| `0x1F5C0` | `0x1F5F0` | Copy/serialize one Chore field |
| `0x1F7B0` | `0x1F7E0` | Execute due pending Chores |
| `0x1BCC0` | `0x1BCF0` | Sync barrier / can advance frame |
| `0x1ADE0` | `0x1AE10` | Opcode-120 SyncEvent handler |
| `0x27DB0` | `0x27DE0` | Adjust dynamic command delay |
| `0x24E50` | `0x24E80` | Frame lag/skip calculation |
| `0x1CCF0` | `0x1CD20` | Additional ping/lag management |
| `0x1F6B0` | `0x1F6E0` | Queue synchronized autosave |
| `0x127A0` | `0x127A0` | Opcode-31 MakeTroop handler |
| `0x80AE0` | `0x81870` | Export `DLL_GameAction` |
| `0x856F0` | `0x86480` | Export `DLL_ReceiveChore` |
| `0x858F0` | `0x86680` | Export `DLL_RunTick` |

All listed internal function pairs except separately marked gaps have confirmed one-to-one version identity through unique raw or normalized instruction hashes and CFG. This establishes identity across builds, not automatically unchanged semantics.

For the current build, the handler table is at `0x2C7A30`; opcode 111's entry is `0x2C7DA8`. The entry still points to RVA `0xFC30`, whose expected bytes begin `C2 00 00` followed by padding. The queue entry is derived from the unique interior pattern at `0x239AE` minus `0x1E`, because a prior startup hook may replace its prologue. The field-copy entry remains `0x1F5F0`.

## Known opcode interpretations

The structured opcode catalog is authoritative for machine queries. Important interpretation cautions are:

- `31` MakeTroop has a statically observed five-byte native payload even though an early enum comment described two `INT32` values. The disagreement is retained as counter-evidence.
- `35` covers both ration changes and food restriction and therefore has mode-dependent payload meaning.
- `36` is a combined unit-order operation; stop, disband, attack-here, and cow-related submodes are not fully separated.
- `109` is AutoTrade settings in DE, despite historical HD use as a skirmish-screen command.
- `113` has a confirmed ally-command family but four mode-dependent values remain incompletely named.
- `119` is reached from managed `GameActionCommand.ExtremePower`; its eight payload bytes are not field-mapped.
- `120` is synchronization/barrier metadata and carries no gameplay mutation itself.
- `111` had no direct static producer in the historical build and targeted a trivial handler. Indirect generation was not disproved, so it is only a hash-bound unused candidate.
- Historical opcode groups `46`, `49`, `54-67`, `80-84`, `87-101`, and `114-116` remain candidates until their DE producers and handlers are validated.

## Accepted runtime evidence

The accepted no-op probe used opcode 111 and a fixed 16-byte payload containing magic `SCH1`, protocol version 1, source player ID, zero flags, per-source request ID, and sentinel `0xC011AB1E`. It held `EngineInterface.threadLock`, staged one payload, called the original queue, and made no simulation mutation in mode 0.

Before the comprehensive delay test, a one-human smoke test executed two host requests exactly once. A normal two-peer test executed 11 requests (six host-originated and five client-originated) with identical command IDs and final ticks on both peers.

The comprehensive test used a 500 ms incoming delay on the non-host peer and five commands per click:

| Measurement | Result |
|---|---:|
| Logical requests | 30 |
| Host-originated / client-originated | 15 / 15 |
| Mode-0 executions per peer | 30 |
| Relevant host SyncEvents | 6 |
| IDs in each relevant SyncEvent | 5 |
| Held / barrier-observed / released / reinjected | 15 / 15 / 15 / 15 |
| Minimum / maximum / average hold | 502 / 524 / 509.2 ms |
| Minimum repeated barrier-wait run calls | 15 |
| Maximum tick beyond barrier before reinjection | 0 |
| Commands crossing the barrier early | 0 |
| Errors | 0 |

Every request had one origin enqueue, one mode-1 serialization, one remote mode-2 sizing call, one mode-0 execution per peer, identical command ID/final scheduled tick/actual tick, and `mutation=none`. Every delayed remote command followed incoming observation, hold, matching SyncEvent, repeated barrier wait, release, reinjection, and execution at the original final target tick. No forced run, resync, access violation, callback failure, malformed payload, correlation collision, duplicate execution, tick mismatch, legacy custom-packet path, or state mutation occurred.

## Safety and remaining gaps

- A native integration must fail closed on unknown hashes, unexpected handler pointers/bytes, non-unique patterns, or invalid memory bounds.
- Managed exceptions must never cross native callbacks. Delegates, hooks, and handler pointers must remain rooted for process lifetime and must not be removed from SHCDE's startup-time plugin `OnDestroy()`.
- Only handler mode 0 may apply synchronized gameplay mutation. UI and capability packets may enqueue or negotiate but must not apply the effect themselves.
- All peers require compatible registrations and payload semantics; an unmodified peer would execute a custom opcode as a no-op and desynchronize.
- Still unproven: save/load with pending custom Chores, native resync, repeated sessions and map changes, disconnect/host migration, sustained slot recycling, exact 25-ID limit, 24-bit tick wrap, indirect opcode-111 producers, and deliberate forced-run behavior.
- Opcode 42's historical handler did not receive a safe current-build version match and remains historical/candidate until located and revalidated.

