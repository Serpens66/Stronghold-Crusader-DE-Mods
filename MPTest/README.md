# MPTest Native Chore Probe

## Purpose

The multiplayer button no longer creates a unit. It schedules a fixed 16-byte no-op payload through
the native Chore queue with opcode `111`. This is the safety gate before implementing a reusable
Script Extender Chore API or moving the swordsman spawn into a native synchronized callback.

Singleplayer keeps the previous woodcutter swordsman action.

The native integration is enabled only for this exact game DLL:

    SHA-256:
    17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4

The runtime also validates the original opcode-111 handler pointer and bytes, the handler table's
memory protection, and the prologues of the native enqueue and field-copy functions. A failed check
leaves the handler table untouched and hides the multiplayer button.

## Probe flow

1. Select a locally owned woodcutter hut.
2. Press the existing action button.
3. In multiplayer the callback stages a diagnostic payload and calls the native
   `QueueLocalChore(ChoreManager, 111)` function while holding `EngineInterface.threadLock`.
4. Handler mode 1 serializes the payload into the local pending slot.
5. The original game distributes the Chore and includes its command ID in a host SyncEvent.
6. Handler mode 0 logs execution on every peer and intentionally performs no mutation.

`Platform_Multiplayer.SendChores` and `EngineInterface.ReceiveChore` are observed without changing
their buffers. Logs contain source/request IDs, native command ID, scheduled tick, SyncEvent
membership, and actual execution tick with millisecond wall-clock timestamps.

## Comprehensive two-peer test

MPTest 1.2.0 creates these settings on both peers:

    ComprehensiveBarrierTestEnabled = true
    BarrierTestIncomingDelayMs = 500
    CommandsPerClick = 5

Both peers use the same DLL and configuration. The delay still affects only incoming opcode-111
Chores on the non-host client. The host never delays its incoming Chores. One multiplayer button
click queues five consecutive no-op commands, allowing batching and ordering to be tested with few
manual actions.

Use this sequence for one comprehensive game start:

1. Install the identical `MPTest.dll` and Script Extender build on both peers.
2. Start one fresh two-human multiplayer game.
3. On the host, select an owned woodcutter hut and click the MPTest button once.
4. Wait about two seconds.
5. On the client, select an owned woodcutter hut and click once.
6. Wait about two seconds.
7. For the overlap stress test, click once on both peers at nearly the same time.
8. Let the match continue for at least five seconds, then copy both logs.

This produces at least 20 commands: two isolated five-command batches and two overlapping
five-command batches. No unit or other game-state mutation is performed.

Compare the logs with:

       powershell -ExecutionPolicy Bypass -File .\Compare-ChoreProbeLogs.ps1 `
         -HostLog .\LogOutput.log `
         -ClientLog .\LogOutputC.log `
         -Comprehensive

The comparer automatically selects the latest map containing probe executions, even when smoke and
two-peer tests share one process log. Comprehensive mode requires at least ten requests and proves:

- one enqueue, mode-1 serialization, remote mode-2 size query, and mode-0 execution per request;
- identical source/request IDs, native command IDs, scheduled ticks, and execution ticks;
- matching outgoing host and incoming client SyncEvents;
- at least one SyncEvent containing multiple probe command IDs;
- a real client hold of at least approximately 500 ms;
- receipt of the matching SyncEvent while the Chore is still held;
- repeated engine-run calls without crossing the target barrier tick;
- reinjection before the exactly-once execution on the original target tick;
- no resync, forced run, duplicate execution, malformed payload, legacy spawn path, or mutation.

The delay path uses a persistent `EngineInterface.run` detour. It does not sleep the simulation or
network thread. Set `ComprehensiveBarrierTestEnabled = false` after the diagnostic phase.

Do not proceed to state-changing Chores if the comparer fails any invariant.

## Comparer self-test

The positive fixture contains one delayed host command and one non-delayed local client command.
The negative run deliberately requires too many requests and must return exit code 1:

       powershell -ExecutionPolicy Bypass -File .\Test-ChoreProbeComparer.ps1
