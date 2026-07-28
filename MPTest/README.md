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

## Normal two-peer test

1. Install the identical `MPTest.dll` and Script Extender build on both peers.
2. Keep `DelayIncomingProbeMs = 0` in both `BepInEx/config/MPTest_Serp.cfg` files.
3. Start a fresh multiplayer game.
4. Trigger one request on the host, one on the client, then several alternating requests.
5. Close the game or copy both logs after the test.
6. Compare them:

       powershell -ExecutionPolicy Bypass -File .\Compare-ChoreProbeLogs.ps1 `
         -HostLog .\LogOutput.log `
         -ClientLog .\LogOutputC.log

The script fails unless each request executes exactly once on both peers with the same command ID,
scheduled tick, execution tick, and an outgoing host SyncEvent containing that command ID.

## Delayed barrier test

After the normal test passes, set the non-host client's configuration to:

    DelayIncomingProbeMs = 500

Leave the host at `0`, start a fresh match, and trigger at least one request on the host. The client
holds only the incoming opcode-111 packet; SyncEvents continue normally. A persistent
`EngineInterface.run` detour releases the packet later without sleeping or relying on the short-lived
BepInEx plugin component.

Compare the fresh logs with:

       powershell -ExecutionPolicy Bypass -File .\Compare-ChoreProbeLogs.ps1 `
         -HostLog .\LogOutput.log `
         -ClientLog .\LogOutputC.log `
         -RequireDelayProof

Do not proceed to state-changing Chores if the game reports a resync,
`SyncEvent - Forced run`, duplicate execution, malformed payload, or different execution ticks.
