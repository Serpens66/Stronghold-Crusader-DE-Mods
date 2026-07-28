# MPTest Multiplayer Spawn Desync

## Previous implementation and problem

The initiating player previously spawned the swordsman directly from the Unity button callback and then broadcast a custom packet:

    if (!TryApplySpawn(packet, "local-multiplayer-click"))
        return;

    GameNetworkAPI.SendPacketToAll(packet, packetHook.GetPacketId(), true);

The receiving client also performed the spawn directly from the network callback:

    if (!TryApplySpawn(packet, "remote-multiplayer-packet"))
        return;

Both paths eventually call `GameUnitManagerAPI.CreateUnitLocal(...)`.

## Shared future-tick implementation under test

The multiplayer initiator selects one shared future map tick, schedules its local spawn for that tick, and broadcasts the packet before the spawn happens:

    ExecuteAtMapTick =
        GameTimeManagerAPI.Instance.GetElapsedMapTicks() + MultiplayerSpawnLeadTicks;

The receiving client calculates the remaining ticks from the packet's `ExecuteAtMapTick`. Both sides then use a non-savable Script Extender `TimerEngine` action and only apply the spawn if the callback executes at exactly the requested map tick.

The lead is eight simulation ticks: the largest observed packet-to-remote-execution difference was six ticks, so this adds a two-tick safety margin. At the normal 40 Hz simulation rate this is 200 ms of game time. Singleplayer does not use this multiplayer lead and retains the next `0 ms` timer callback.

Relevant code:

- [Target-tick selection, local scheduling, and packet broadcast](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/MPTestRuntime.cs#L190-L226)
- [Packet validation and remote scheduling](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/MPTestRuntime.cs#L246-L284)
- [Timer scheduling and exact-tick enforcement](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/MPTestRuntime.cs#L291-L358)
- [Actual unit creation](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/MPTestRuntime.cs#L385-L427)
- [Diagnostic snapshot and full `GameUnit` hash](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/UnitSpawnDiagnostics.cs#L19-L91)

## Observed behaviour before the timer change

The button was pressed three times. The game appeared to resync after the first and third spawn, but not after the second.

### First spawn — likely resync

    Host:   gameTimeUnits=7750000, elapsedMapTicks=311,
            structFnv1a64=0xD61D94F514791AE0

    Client: gameTimeUnits=7775000, elapsedMapTicks=312,
            structFnv1a64=0xFC570BF53EE77FDF

The client executed the spawn one simulation tick later, and the resulting native `GameUnit` structures differ.

### Second spawn — no observed resync

    Host:   gameTimeUnits=15350000, elapsedMapTicks=615,
            structFnv1a64=0xEC5F4039FFF89D5A

    Client: gameTimeUnits=15350000, elapsedMapTicks=615,
            structFnv1a64=0xEC5F4039FFF89D5A

Both sides executed the spawn at the same deterministic game time. The complete 1168-byte `GameUnit` structures are identical.

### Third spawn — likely resync

    Host:   gameTimeUnits=28425000, elapsedMapTicks=1138,
            structFnv1a64=0x95CB2518AF4C746C

    Client: gameTimeUnits=28450000, elapsedMapTicks=1139,
            structFnv1a64=0x6799A9320B894FD8

Again, the client executed the spawn one simulation tick later, producing a different native structure.

## Previous timer-only test result

The packet is received exactly once and the unit ID, global ID, owner, type, position, height, health, and other known fields match on both machines.

The timer-only test moved the mutation into the expected execution context, but it was not sufficient:

- A Unity button callback or network callback can run outside the short native window in which game-state changes are safe.
- The previous direct calls sometimes produced different unknown native `GameUnit` fields.
- The second test spawn happened to produce identical structures and did not appear to cause a resync.

The shared future-tick implementation additionally aligns the exact map tick on all participants. The existing timing and full-structure diagnostics remain enabled so the result can be verified in new host and client logs.
