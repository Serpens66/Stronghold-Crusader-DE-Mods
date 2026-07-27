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

## Timer-based implementation under test

The button and network callbacks now only validate and schedule the request. `TryApplySpawn` runs from a non-savable `0 ms` Script Extender `TimerEngine` action:

    ScheduleSpawn(
        packet,
        networked ? "local-multiplayer-timer" : "singleplayer-timer",
        () => CompleteLocalSpawn(packet, networked));

The timer executes during the native simulation update window. After a successful local timed spawn, the packet is broadcast. A receiving client schedules the same operation through its own timer instead of changing game state directly from the packet handler.

Relevant code:

- [Local scheduling](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/MPTestRuntime.cs#L197-L205)
- [Packet validation and remote scheduling](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/MPTestRuntime.cs#L211-L257)
- [Timer scheduling and execution](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/MPTestRuntime.cs#L261-L310)
- [Actual unit creation](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/MPTestRuntime.cs#L319-L361)
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

## Working theory

The packet is received exactly once and the unit ID, global ID, owner, type, position, height, health, and other known fields match on both machines.

The likely problem is the execution context:

- A Unity button callback or network callback can run outside the short native window in which game-state changes are safe.
- The previous direct calls sometimes produced different unknown native `GameUnit` fields.
- The second test spawn happened to produce identical structures and did not appear to cause a resync.

The timer-based implementation moves all calls to `CreateUnitLocal` into the Script Extender's deterministic simulation-tick callback. The existing timing and full-structure diagnostics remain enabled so the result can be verified in new host and client logs.
