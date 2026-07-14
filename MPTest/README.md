# MPTest Multiplayer Spawn Desync

## Current implementation

When the button is clicked, the initiating player spawns the swordsman locally and then broadcasts a custom packet:

    if (!TryApplySpawn(packet, "local-multiplayer-click"))
        return;

    GameNetworkAPI.SendPacketToAll(packet, packetHook.GetPacketId(), true);

The receiving client handles the packet and performs the same spawn:

    if (!TryApplySpawn(packet, "remote-multiplayer-packet"))
        return;

Both paths eventually call `GameUnitManagerAPI.CreateUnitLocal(...)`.

Relevant code:

- [Local spawn and packet broadcast](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/MPTestRuntime.cs#L197-L207)
- [Packet handler](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/MPTestRuntime.cs#L217-L251)
- [Actual unit creation](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/MPTestRuntime.cs#L258-L300)
- [Diagnostic snapshot and full `GameUnit` hash](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/MPTest/src/UnitSpawnDiagnostics.cs#L19-L91)

## Observed behaviour

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

## Conclusion

The packet is received exactly once and the unit ID, global ID, owner, type, position, height, health, and other known fields match on both machines.

The likely problem is the execution time:

- The host calls `CreateUnitLocal` immediately.
- The client calls it later when the packet arrives.
- If both calls happen during the same simulation tick, the result is identical.
- If the client is one tick later, unknown native `GameUnit` fields differ and the game detects a desync.

Therefore, “spawn locally and let clients follow as soon as possible” does not reliably guarantee deterministic execution. A likely solution is to send a future simulation/map tick in the packet and let every participant, including the host, execute the spawn at exactly that tick.
