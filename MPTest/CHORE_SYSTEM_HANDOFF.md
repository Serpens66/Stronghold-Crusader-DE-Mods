# Investigation Handoff: SHCDE Multiplayer Chore System

> **Status update 2026-07-28:** The native investigation requested by this handoff has now been
> completed to the point where the packet header, pending-slot model, three handler modes, dynamic
> scheduling and the host command-ID barrier are understood. The detailed results and a proposed
> Script Extender API are documented in `CHORE_SYSTEM_NATIVE_ANALYSIS.md`.
>
> Statements below saying that the native functions or inner Chore format have not yet been traced
> describe the starting state of the investigation and are superseded by that new document.

## Task for the new chat

Continue investigating the native multiplayer Chore system of Stronghold Crusader Definitive Edition. The immediate goal is to determine whether a BepInEx/Script Extender mod can enqueue a custom deterministic action into the same lockstep command stream used by ordinary game actions.

The motivating use case is the `MPTest` mod: pressing a custom UI button should spawn a swordsman on a valid tile beside the selected woodcutter hut. Calling `GameUnitManagerAPI.CreateUnitLocal(...)` independently on every peer has caused multiplayer resynchronizations, even when a custom packet and Script Extender timer were used.

Please distinguish confirmed facts from inferences. Do not assume that using the same Steam transport is equivalent to participating in the native Chore/lockstep queue.

## Confirmed managed-side pipeline

### 1. UI actions enter the native engine through `GameAction`

Normal UI actions call:

    EngineInterface.GameAction(...)

This eventually invokes the native function:

    DLL_GameAction(int action, int structureID, int value, int value2)

For example, troop recruitment in `CrusaderDE.MainViewModel` calls:

    EngineInterface.GameAction(
        Enums.GameActionCommand.MakeTroop,
        amount,
        (int)chimpEnum);

The managed variable is called `structureID`, but for `MakeTroop` it contains the amount:

- Normal click: `1`
- Shift: `5`
- Ctrl: `1000`

The native `MakeTroop` action therefore does not accept an arbitrary spawn position or source woodcutter. The engine determines the recruitment building, position, costs, equipment and other effects.

### 2. The native simulation produces a Chore buffer during a tick

`EngineInterface.run(...)` calls:

    DLL_RunTick(..., byte* choreBuffer, ..., bool mpFrameSkip, ...)

The supplied buffer is `MemoryBuffers.MemBuffer.MPChores`.

After the simulation result has been processed, `Director` calls:

    Platform_Multiplayer.Instance.SendChores(
        nextBufferToRender.MPChores);

This strongly indicates the following sequence:

1. A local action is passed to the native engine.
2. The native simulation processes or queues it.
3. `DLL_RunTick` writes multiplayer commands into the Chore buffer.
4. The managed multiplayer layer distributes those Chores.

The precise internal connection between `DLL_GameAction` and the ChoreManager has not yet been reverse-engineered in `CrusaderDE.dll`. That part remains an evidence-based inference rather than a fully traced native call graph.

### 3. Outer Chore-buffer record format

`Platform_Multiplayer.SendChores(byte[] choreBuffer)` parses the buffer as a sequence of records.

For each record:

- Offset `+0`: signed 32-bit Chore payload length
- Offset `+4`: target player ID
  - `0` means broadcast to all other human players
  - A non-zero value targets one player
- Offset `+5`: first byte of the actual Chore payload
- Total outer record size: `payloadLength + 5`

A negative payload length terminates the buffer.

Conceptually:

    [int32 payloadLength]
    [byte targetPlayer]
    [byte[payloadLength] chorePayload]

The first Chore payload byte is inspected by managed code. Known examples include:

- `54`: resynchronization starts
- `67`: resynchronization ends
- `39`: associated with saving/resynchronization state

The complete inner Chore payload format and action opcodes remain undocumented.

### 4. Native Chores use multiplayer packet type 1

`SendChores(...)` eventually calls:

    SendGamePacketToAll(choreBuffer, payloadLength, payloadOffset);

`SendGamePacketToAll(...)` constructs an `MPData` packet with:

    packetType = 1;

This is the special packet type used for native game/Chore data.

Packets are sent through Steam Networking Messages on channel 2. The managed code and Script Extender documentation describe this in-game transport as reliable.

### 5. Received native Chores return to the native engine

In `Platform_Multiplayer.processMessage(...)`, packet type `1` is handled with:

    EngineInterface.ReceiveChore(
        fromMember.playerID,
        data.data,
        data.dataLength);

That wrapper calls:

    DLL_ReceiveChore(playerID, data, dataLength);

Received native game commands therefore do not execute through a managed custom-packet callback. They are fed directly into the native simulation/Chore system.

This is the important distinction from Script Extender custom packets.

## Evidence of native multiplayer timing control

The play-state data returned by `DLL_RunTick` contains:

- `MP_Ahead_By`
- `MP_Behind_By`
- `SkipFrame`
- `pingtimes[8]`

When `SkipFrame > 0`, `EngineInterface.run(...)` calls:

    Director.instance.MPSkipFrame(
        freeBuffer.gameState.SkipFrame);

`Director.MPSkipFrame(...)` updates the number of multiplayer frames to skip.

The in-game ping display reads:

    GameData.Instance.lastGameState.pingtimes[playerIndex]

and displays those values as milliseconds.

This confirms that the base game tracks network latency and simulation-frame distance internally. It also controls frame advancement to keep peers aligned. The exact lockstep algorithm, especially when it waits, skips or stalls for missing Chores, has not yet been reverse-engineered.

This also corrects an earlier assumption: the Script Extender has no dedicated public ping API, but the managed base-game state exposes native ping values through `GameData.Instance.lastGameState.pingtimes`.

## Script Extender custom packets are not native Chores

The Script Extender's `GameNetworkAPI` dynamically assigns custom packet IDs starting at:

    CustomNetworkPacketType.CustomPacketStart + 100

A custom packet is sent as an `MPData` packet whose `packetType` is that custom ID.

Incoming custom packets are intercepted and dispatched through:

    GameNetworkAPI.HandleRawPacket(...)

They are then delivered to managed R3 packet handlers.

Consequently, a custom packet:

- Uses the same underlying Steam transport.
- Does not use `packetType = 1`.
- Is not passed to `DLL_ReceiveChore(...)`.
- Is not automatically part of the native simulation command queue.
- Does not automatically receive the native Chore system's frame-waiting guarantees.

This explains why "send a custom packet and run the same managed function on every peer" can still execute on different simulation ticks.

## ChoreManager exposure in the current Script Extender

The current local Script Extender finds the native ChoreManager address:

    GameGlobalsManager.ChoreManagerVA

`GamePlayerManagerAPI` stores this address in:

    private IntPtr _choreManager;

It also exposes a `ChoreManagerOptionsInternal` wrapper, but that wrapper only accesses advanced game-option fields such as improved units, uncapped peasants and similar options.

No API for the following was found in the current local source:

- Enqueuing a native Chore
- Registering a custom Chore opcode
- Adding custom payload data to a simulation frame
- Executing a managed callback as part of a received native Chore
- Making the native simulation wait for a Script Extender custom packet

This conclusion came from searches across the Script Extender's `API`, `EventAPI`, `Detours`, documentation and reverse-engineering directories.

## Relevance to arbitrary unit spawning

`GameUnitManagerAPI.CreateUnitLocal(...)` directly calls native unit-creation functionality. It changes local game state but does not appear to create a native player command or Chore.

The existing native `GameActionCommand.MakeTroop` cannot replace it because it represents normal recruitment and does not allow specifying an arbitrary tile beside a woodcutter hut.

Other exposed `GameActionCommand` values also do not provide a suitable arbitrary unit-spawn command.

Using the native Chore system for `MPTest` would therefore probably require one of these:

1. A Script Extender API for custom deterministic Chores.
2. A native hook that reserves and handles a custom Chore opcode.
3. Discovery of an existing native Chore/action that can represent an arbitrary unit spawn.
4. A carefully integrated side channel whose input becomes part of native frame progression rather than merely using the same network transport.

Simply sending arbitrary bytes as packet type `1` is unsafe because the native Chore parser expects a specific undocumented format and known opcodes.

## Current MPTest synchronization approach

The current `MPTest` implementation does not use native Chores.

It:

1. Builds a `WoodcutterSwordsmanSpawnPacket`.
2. Chooses a future absolute map tick.
3. Schedules a Script Extender `TimerEngine` action locally.
4. Sends the custom packet to other players.
5. Each receiving peer schedules the same spawn for the transmitted map tick.

The current fixed multiplayer lead is eight simulation ticks. At 40 ticks per second, this is 200 ms of game time.

This approach is outside the native Chore queue. The fixed delay is only a heuristic and cannot provide the same guarantee as native lockstep processing. A packet that arrives after the target tick can still cause one peer to execute while another rejects the operation.

## Most important open questions

The next investigation should answer:

1. What does native `DLL_GameAction(...)` do internally?
2. Where does it write commands into the ChoreManager?
3. What is the full inner Chore payload format?
4. Does each Chore contain a simulation-frame number or sequence number?
5. How does `DLL_ReceiveChore(...)` store received commands?
6. Under which conditions does the engine wait or skip frames?
7. Can a currently unused Chore opcode safely be intercepted?
8. Is there an existing editor, scenario-event or cheat Chore for spawning a unit at arbitrary coordinates?
9. Can the Script Extender expose something like:

       EnqueueCustomChore(commandId, payload)

   with the same frame synchronization guarantees as native player actions?
10. How is the local copy executed? Native Chores are only sent to other players, so a custom system must ensure that the sender and receivers execute the operation through equivalent simulation paths.
11. How do player disconnects, resynchronization and saved multiplayer games interact with pending Chores?

## Suggested native investigation

Start with the native exports or wrappers for:

- `DLL_GameAction`
- `DLL_RunTick`
- `DLL_ReceiveChore`

Trace all accesses to the address identified by `GameGlobalsManager.ChoreManagerVA`.

Useful goals:

1. Find the native function that serializes a local action into a Chore.
2. Identify the function that parses the first opcode byte of a received Chore.
3. Determine how the engine associates Chores with simulation frames.
4. Identify where `MP_Ahead_By`, `MP_Behind_By`, `SkipFrame` and `pingtimes` are written.
5. Capture and compare Chore payloads produced by simple actions such as changing taxes, buying goods and recruiting one troop.
6. Determine whether an existing no-op or extensible Chore type can carry a mod command without changing unrelated game state.

Do not modify the Script Extender or native binary until the format and frame semantics are understood.

## Sources

### Decompiled managed game assembly

Assembly:

`D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Meine Mods\shcde-script-extender\deps\Assembly-CSharp-publicized.dll`

Types inspected with `ilspycmd`:

- `EngineInterface`
- `Platform_Multiplayer`
- `Director`
- `CrusaderDE.MainViewModel`
- `OnScreenText`
- `Enums`

Useful reproduction commands:

    ilspycmd -t EngineInterface Assembly-CSharp-publicized.dll
    ilspycmd -t Platform_Multiplayer Assembly-CSharp-publicized.dll
    ilspycmd -t Director Assembly-CSharp-publicized.dll
    ilspycmd -t CrusaderDE.MainViewModel Assembly-CSharp-publicized.dll
    ilspycmd -t OnScreenText Assembly-CSharp-publicized.dll
    ilspycmd -t Enums Assembly-CSharp-publicized.dll

Installed `ilspycmd` path used during the investigation:

`C:\Users\Serpens66\.dotnet\tools\ilspycmd.exe`

### Script Extender source

`D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Meine Mods\shcde-script-extender\src\SHCDESE.BepInEx\API\GameNetworkAPI.cs`

- Custom packet registry: approximately lines 70-108
- Managed custom-packet dispatch: approximately line 198
- Packet send methods: approximately lines 371-428
- Player and host helpers: approximately lines 564-671

`D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Meine Mods\shcde-script-extender\src\SHCDESE.BepInEx\GameGlobals\GameGlobalsManager.cs`

- Locates `ChoreManagerVA` at approximately lines 1855-1863.

`D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Meine Mods\shcde-script-extender\src\SHCDESE.BepInEx\API\GamePlayerManagerAPI.cs`

- Stores the ChoreManager pointer and initializes `ChoreManagerOptionsInternal` at approximately lines 157-161.

### Native binary not yet fully investigated

The native implementation is in:

`D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Meine Mods\x86_64\CrusaderDE.dll`

The P/Invoke declarations were found in the managed assembly, but the implementations of `DLL_GameAction`, `DLL_RunTick` and `DLL_ReceiveChore` have not yet been traced inside this native binary.

