# Chores, multiplayer synchronization, desync, player control and spectating [Scope: Vanilla]

Native hash FBCB9319 (full hash in ../../../../CURRENT.json). Summary state 2026-11-27. Check the installed DLL hash per CURRENT.md before using any address. Evidence links point to the audit documents; they stay authoritative.

Managed facts refer to `Assembly-CSharp.dll` SHA-256 `BC8B6A39...` (full hash in [PLAYER_CONTROL_CONTEXT.md](../PLAYER_CONTROL_CONTEXT.md)). Historical runtime Chore traces were taken on the older native hash `17F8DD4A...` (Steam build before 24816905); their function identities were mapped one-to-one to FBCB9319, semantics were not automatically re-proven ([CHORE_SYSTEM.md](../CHORE_SYSTEM.md#native-function-map)). Binding working rules for MP/desync work are in [multiplayer-network.md](../../../../../../AGENTS/multiplayer-network.md) and [desync-diagnosis.md](../../../../../../AGENTS/desync-diagnosis.md) (not copied here).

Link abbreviations used below: CHORE = [CHORE_SYSTEM.md](../CHORE_SYSTEM.md), PCC = [PLAYER_CONTROL_CONTEXT.md](../PLAYER_CONTROL_CONTEXT.md), DESYNC = [DESYNC_CAPTURE.md](../DESYNC_CAPTURE.md), GA = [GAMEACTION_OBSERVER_CLASSES.md](../GAMEACTION_OBSERVER_CLASSES.md), SBP = [SPECTATOR_BUILD_PLACEMENT.md](../SPECTATOR_BUILD_PLACEMENT.md), SR = [SPECTATOR_REPORTS.md](../SPECTATOR_REPORTS.md), STT = [SPECTATOR_TEST_TOOLS.md](../SPECTATOR_TEST_TOOLS.md), QT = [Findings/QueueTest.md](../../../../../../Findings/QueueTest.md) (secondary source outside the baseline).

## Overview

- Every synchronized player command is a native **Chore**: managed UI calls `EngineInterface.GameAction(...)` under `EngineInterface.threadLock` (private static `System.Object`), native `DLL_GameAction` (`0x81870`) stages opcode parameters in global staging fields and calls the local queue `0x23990`, which serializes the command (handler mode 1) into a 0x500-byte pending slot and the outgoing buffer.
- `DLL_RunTick` (`0x86680`) emits the outgoing buffer to managed `Platform_Multiplayer.SendChores` (Steam channel 2, packet type 1); receivers call `EngineInterface.ReceiveChore(fromMember.playerID, ...)` -> `DLL_ReceiveChore` (`0x86480`) -> incoming FIFO -> parse/schedule into a remote pending slot.
- At the scheduled tick both sender and receivers execute the same opcode handler in mode 0 (the only state-changing mode); due commands are sorted by signed command ID. The host emits opcode-120 SyncEvents (barrier manifests) about every 4 ticks; a client stalls until all listed command IDs exist locally (3 s forced-run fallback).
- One manager base `0x8574320` (ChoreManager) holds the pending slots, handler mode, issuer, native view, heartbeat/progress tables and skip state.
- **Native view** (`0x88E3D70` = manager `+0x36FA50`) is the local presentation/player viewpoint written by `DLL_SetEditorPlayer` (`0x872E0`). It is also the network source of every locally queued Chore, the save's local identity (section row 103) and the index for heartbeat progress. It is not an authorization or a role.
- **Spectator flag** (`0x3666080`, process-local) set by GameAction `SpectatorMode` (`0x431`) suppresses map input in `DLL_RunTick` and in the nested input re-dispatch by temporarily writing view 0; reports keep following the selected view.
- Unit/group selection uses a local marking stage, a synchronized selection (Chore 16/15) and a per-group reservation; the input dispatcher `0x8C5F0` drives selection, orders and power targeting.
- The unit checksum (desync detection) hashes unit wire records and paths; Chore 56 transfers a whole unit record for resync.

Machine-readable Chore data (link, not copied): [chore-opcodes.jsonl](../chore-opcodes.jsonl), [chore-contracts.jsonl](../chore-contracts.jsonl), [chore-observations.jsonl](../chore-observations.jsonl), [chore-evidence.jsonl](../chore-evidence.jsonl), and the `fn-chore-*` records in [function-claims.jsonl](../function-claims.jsonl). Query with `_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1 chore opcode <n>`, `query.ps1 chore gaps` (also `chore list|function|contract|evidence`).

## Key functions

### Chore core (queue, network, scheduler)

| RVA | Name/role | Notes | Confidence | Evidence |
|---|---|---|---|---|
| 0x81870 | `DLL_GameAction` export | Switch 1001..1073 (`0x3E9..0x431`); 2000..2026 via default to `0x80770`; `0x7DE` is a case without enum value. Historical RVA `0x80AE0`. | confirmed, export | GA, CHORE |
| 0x86480 | `DLL_ReceiveChore` export | Buffers peer ID + bytes via `0x23E70`. Historical `0x856F0`. | confirmed, export | CHORE, PCC |
| 0x86680 | `DLL_RunTick` export | Unwind range `[0x86680,0x86F72)`. Order: input `0x8C5F0`, due Chores `0x1F7E0`, then `0xCDE60`; late `0x1CD20`. Historical `0x858F0`. | confirmed, export | CHORE, PCC |
| 0x23990 | Queue local Chore | Source = native view `+0x36FA50`; stores it at slot `+4` and in command ID. Entry derived from unique interior pattern at `0x239AE` minus `0x1E` (prologue may be hooked by a startup hook). Historical `0x23960`. | identity confirmed, semantics probable | CHORE, PCC |
| 0x23800 | Build normal inner Chore | Historical `0x237D0`. | probable | CHORE |
| 0x23E70 | Append received outer record | ~1 MB FIFO; overflow drops a record. Historical `0x23E40`. | probable | CHORE |
| 0x23C30 | Pop incoming record | Returns the buffered peer ID with the packet. Historical `0x23C00`. | probable | CHORE, PCC |
| 0x23620 | Parse/dispatch incoming payload | Routes heartbeat types 0/1 to `0x27DE0`. Historical `0x235F0`. | probable | CHORE, PCC |
| 0x23F10 | Schedule received normal command | Stores independently supplied source at slot `+4`. Historical `0x23EE0`. | probable | CHORE, PCC |
| 0x19D3C0 | Write outgoing outer record | Appends length, destination, bytes to native-to-managed buffer. Historical `0x19C370`. | probable | CHORE, PCC |
| 0x1F5F0 | Copy/serialize one Chore field | Direction per mode; fifth arg 0 = pack, 1 = unpack; advances cursor. Historical `0x1F5C0`. | probable | CHORE, DESYNC |
| 0x1F7E0 | Execute due pending Chores | Loads slot source via `0x1F9A0`, stores to issuer `+0x36FA4C`, calls handler mode 0. Historical `0x1F7B0`. | probable | CHORE, PCC |
| 0x1F9A0 | Source identity helper | Bytes `8B C2 C3` (`mov eax,edx; ret`): no remapping. | confirmed, disassembly | PCC |
| 0x21EE0 | Due-list builder | Active slots with tick <= current tick, sorted by signed command ID (not slot number or initial tick). | confirmed, static | PCC |
| 0x20950 | Queue cursor advance | Cursor `+0x370BF4`, wraps 500 slots; accepts state 0 or signed state >= 10; if all active returns last active slot, which `0x23990` clears and replaces. | confirmed, static | PCC |
| 0x1BCF0 | Sync barrier / can-advance | Uses host flag, tick counter, sync-event queue, barrier list, 3 s forced run; does not index progress by view. Historical `0x1BCC0`. | probable | CHORE, PCC |
| 0x1AE10 | Opcode-120 SyncEvent handler | Historical `0x1ADE0`. | probable | CHORE |
| 0x1A4A50 | Logger stub | `C2 00 00` (`RET 0`): "SyncEvent - Forced run" is never written. | confirmed, disassembly | DESYNC |
| 0x27DE0 | Adjust dynamic command delay / heartbeat receive | Network-only. Historical `0x27DB0`. | probable | CHORE, PCC |
| 0x27F30 | Heartbeat send | Exits for net modes 0/99; packet type byte arg; no local-player arg; 5-byte payload. The three view loads (MOVSXD RCX,[RBX+0x36FA50], 7 bytes each) are at `[0x27FEB,0x27FF2)`, `[0x27FFF,0x28006)`, `[0x28019,0x28020)`; no control-flow entry inside. | confirmed, disassembly | PCC |
| 0x24E80 | Frame lag/skip calculation | Called before a real sim pass with (manager, nativeView); view loaded in RunTick by MOV EDX,[RIP+view] at `[0x869D2,0x869D8)` (no entry inside); resets skip count. Historical `0x24E50`. | confirmed, static | CHORE, PCC |
| 0x1F9B0 | Progress min/max | Min/max over registered humans 1..8 (manager `+0x8AC+slot*4 != -1`); local reference from `+0x14D030+player*4`. | confirmed, static | PCC |
| 0x1CD20 | Completed-run maintenance / ping-lag | Sets local delay 0 in modes 0/99; sends heartbeat after >=100 ms (+20 ms per participant beyond two). Historical `0x1CCF0`. | confirmed, static | CHORE, PCC |
| 0x24FD0 | Paused/start maintenance | Calls `0x27F30` after 179 ms for non-hosts. | confirmed, static | PCC |
| 0x7130 | Copy low three tick bytes | Used by heartbeat. | confirmed, static | PCC |
| 0x1F6E0 | Queue synchronized autosave | Historical `0x1F6B0`. | probable | CHORE |
| 0x211A0, 0x24220 | Chore manager initializers | Seed local delay `+0x370BFC` = 35. `0x211A0` calls `0x241B0`. | confirmed, static | PCC |
| 0x241B0 | Pending-array reset | Zeroes all 640000 bytes (500 x 0x500) and resets cursor. | confirmed, static | PCC |
| 0x864C0 | `DLL_RegisterMultiplayerUser` | Declared local ID at `+0x8A8`; host flag `+0x9A0` when ID == 1; also writes native view. | confirmed, static | PCC |
| 0x872E0 | `DLL_SetEditorPlayer` | Writes native view only; in editor game mode 1 also human registration. Does not change declared ID/host flag in MP. | confirmed, static | PCC, SR |
| 0x81360 | `DLL_ConnectionPause` | Writes connection pause `+0xEE4`. | confirmed, static | PCC |
| 0x87D10 | Reset | Resets `+0xEE4`; calls `0x211A0`. | confirmed, static | PCC |
| 0x19D960 | PlayState serializer | Copies report fields by view, spectator flag to `PlayState.spectatorMode` (`0xB12`), skip count to `+0x78C`, camera target, troop HUD fields. | confirmed, static (name candidate) | SR, PCC, SBP |

### Chore handlers (table `0x2C7A30`, 8-byte pointers by opcode, valid through 120)

| RVA | Opcode / role | Notes | Confidence | Evidence |
|---|---|---|---|---|
| 0x10AE0 | 17 MoveUnits | 470 bytes, end `0x10CB6`; 8-byte payload; dispatches `0x196100`. ABI `void(void)`. | handler confirmed, static | PCC, QT |
| 0x11B50 | 25 PlaceWall (delayed wall) | Uses decoded sender `0x88E3D6C`; reaches `0x6F230`, `0xC8CC0`, `0xB7D80`. | probable | SBP |
| 0x12090 | 28 (0x1C) building placement | Re-checks `0xCC420`, then `0x6D580` (arg 7 = variation from staging `0x86C133C`). | confirmed, static | SBP |
| 0x12400 | 29 DestroyBuilding | 7-byte payload. | handler confirmed | opcode catalog |
| 0x127A0 | 31 MakeTroop | 5-byte payload (counter-evidence: old enum said 2 x INT32). Leads `0xD5250 -> 0x190CA0 -> 0x117000`. | confirmed, static | CHORE, PCC |
| 0x12BF0 | 36 UnitOrder (target order) | 450 bytes, end `0x12DB2`; commands 0..38 (Stop 0x1F, Disband 0x1E, AttackUnit 4, AttackBuilding 9, ForceAttackBuilding 36). | confirmed, static | QT, PCC |
| 0x13030 | Synchronized save/autosave handler | Caller of common writer `0x52B0`. | confirmed, static | PCC |
| 0x13CA0 | 44 CreateAnimal | | handler confirmed | opcode catalog |
| 0x15170 | 54 | Calls unit checksum builder with argument 1. | confirmed, static | DESYNC |
| 0x15710 | 56 unit-record transfer | 0x490-byte unit wire record + 1000 path bytes; pack branch sets manager phase 1 -> 0 before `0x1F5F0`. LEA `0x157AE` -> `0x67E8A58`. | confirmed, static | DESYNC |
| 0x15A50 | 59 full group copy | Temporarily zeroes reservation `+0x2A + groupId*0x688`, then restores the local value. | confirmed, static | PCC |
| 0x176C0 | 71 Shift waypoint append ("ExtendRallyPoint") | 487 bytes, end `0x178A7`; only caller of `0x11C3A0`. | confirmed, static | QT |
| 0x1A240 | 109 AutoTradeSettings | | handler confirmed | opcode catalog |
| 0x1A6F0 | 113 AllyCommand | Serializes signed short kind + four signed int32; kind 0 -> `0xD7580`. | confirmed, static | SR, STT |
| 0x1ABF0 | 119 ActivateTacticalPower | Producer: input `0x8C5F0` mode 4 or GameAction ExtremePower via `0x105510`. | confirmed, static | GA, STT |
| 0xFC30 | 111 (table entry `0x2C7DA8`) | Trivial handler, bytes `C2 00 00`; no known producer (unused candidate). | confirmed, static | CHORE |

### Player control, selection and input

| RVA | Name/role | Notes | Confidence | Evidence |
|---|---|---|---|---|
| 0x8C5F0 | Input dispatcher | Called only from RunTick and recursively; args `(leftState, rightDown, shift, SH1RtsControls, rightUp, ctrl, alt)`. Nested spectator boundary `0x8D0D0..0x8D16D`. | confirmed, static | PCC, STT |
| 0x879A0 | `DLL_TroopSelection` export | Writes mouseState/rightDown/rightUp to `0x8EBA98`/`0x8EBA93`/`0x8EBA9C`. A conflicting name-only record at `0x86C00` has no body and is not authoritative. | confirmed, export | STT |
| 0x191130 | Selection builder | 1250-byte unit mask; reserves tribe via `0x11D1B0` (by native view); queues opcode 16. | confirmed, static | PCC |
| 0x191250 | Selection group creation | Creates a new group for selected units. | confirmed, static | PCC |
| 0x11D1B0 | Group reservation | Requires state and reservation clear; writes `+0x2A` = 1 in net mode 0, else low 16 bits of tick + 400. | confirmed, static | PCC |
| 0x119CE0 | Create requested group | Clears full record, assigns owner/state/new global ID. | confirmed, static | PCC |
| 0x119C00 | Create-group (allocation) | Tests state `+0x40` only, not reservation `+0x2A`; call at `0x11E17C`. | confirmed, static | PCC |
| 0x11D370 | Assign unit to group | Adds membership bit/count, rewrites tribe/leader/global refs; does not remove previous membership. | confirmed, static | PCC |
| 0x123EA0 | Remove unit from tribe | 312 bytes (end `0x123FD7`); ABI `void(manager, unitId, tribeId)`; only match of the Extender `UnassignUnit` pattern. | confirmed, static | QT, PCC |
| 0x195E30 | Order staging | Stages tribe, destination, flags; queues opcode 17 before presentation/path/sound checks; arg 5 -> patrol staging `0x86C1338`. | confirmed, static | PCC |
| 0x196100 | Chore-17 wrapper | Calls `0x11B520` with new-order 1; flags minus bit 7 = move type; bit 7 selects group `+0x56C` value. | confirmed, static | PCC, QT |
| 0x11B520 | Group move initializer | Returns 0 only before per-member loops (fields may already be written); initializes packed index/count `0x5DC/0x5DE`. | confirmed, static | PCC, QT |
| 0x11E960 | Target dispatcher | Case `0x1F` Stop (always `return 1`), `0x1E` Disband. | confirmed, static | PCC |
| 0x11C3A0 | Shift waypoint append helper | 71 bytes to `0x11C3E6`; writes point at tribe `+0x5B4+idx*4`, `idx+1` at `+0x5DE`, mode at `+0x582`. | confirmed, static | QT |
| 0x1178D0 | All-members-arrived predicate | Read-only; 1 when every living member arrived. | confirmed, static | QT |
| 0x1222A0 | Tribe overlay renderer | 1371 bytes; draws markers only when patrol mode `+0x582` != 0. | confirmed, static | QT |
| 0x417A0 | Overlay draw-submission helper | 232 bytes; max 250 records; dedups same category/sprite on same tile. | confirmed, static | QT |
| 0x19A5B0 | Queue deselect (opcode 15) | Uses local queue, source = view. | confirmed, static | PCC |
| 0x19A5D0 | Unselect receiver | Clears `r_UnitSelected` for executing owner's units. | confirmed, static | PCC |
| 0x19A500 | Clear synchronized selection | Called after load. | confirmed, static | PCC |
| 0x186C50 | Clear local marking | Clears `r_UnitHover`. | confirmed, static | PCC |
| 0x1904C0 | Local box marking | Marks only native-view units, excludes Lords. | confirmed, static | PCC |
| 0x191C90 | Lord finder | Finds a selectable Lord for the local view. | confirmed, static | PCC |
| 0x897F0 | Deselect / CloseTroopsPanel | Resets app modes `0x3D`/`0x3E`; if marked count > 0 calls `0x19A5B0` and `0x82F0`; then `0x186C50`. | confirmed, static | PCC, GA |
| 0x90510 | Troop order arming | Stop/Disband -> `0x199B50` (op 36); arms Attack Here. | confirmed, static | GA, PCC |
| 0x8BD60 | Mapper start | Via `DLL_StartMapAction` `0x87830`; camera call at `0x8BF96`; computed branch `0x8BE82` targets `0x8BFA0`. | confirmed, static | SBP |
| 0x117000 | Per-player recruit group | Reached by ordinary soldiers via `0x127A0 -> 0xD5250 -> 0x190CA0`, by special types and Arab troops via `0x1819D0`. Engineer 30, Tunneler 5, Ladderman 29 own records; Monk 37 separate; others share index 0; revalidated by global ID, recreated by `0x119C00`. | confirmed, static | PCC |
| 0x1261F0 | Group maintenance | Removes dead/foreign-tribe/other-state members via `0x123EA0`; clears empty groups via `0x118C70`. | confirmed, static | PCC |
| 0x182B00 | Unit loop | Precedes group maintenance; also counts eligible AI-role units. | confirmed, static | PCC, STT |

### Spectator, view, save and other

| RVA | Name/role | Notes | Confidence | Evidence |
|---|---|---|---|---|
| 0x85BD0 | `DLL_MapAction` | Saves view, writes `playerID` argument as view for the action, restores. Wall input via `0x8AAF0` -> `0x94120`. | confirmed, static | SBP |
| 0x90CD0 | Building click producer | Checks `0xCC420`, enqueues opcode `0x1C`. | confirmed, static | SBP |
| 0xCC420 | Placement validation | Entry `48 89 5C 24 20 44 89 44 24 18`; has AI-owner branch. | confirmed, disassembly | SBP, PCC |
| 0x93750 | Unit mapper | Reads temporary view; troops via `0x118CA0` -> `0x17FEF0`. | confirmed, static | SBP |
| 0x87220 | `DLL_SetAppMode` | Report mode 16 and submodes 71..79; no spectator rejection. | confirmed, static | SR |
| 0x853F0 | `DLL_LoadSaveGame` | Calls unload `0x5BC70`, reset `0x87D10`, reader `0x87D0`; then `0x186C50`, `0x19A500`; view 0 -> 1 in non-editor branch. | confirmed, export | PCC, SR |
| 0x87D0 | Save reader / reset path | Clears spectator flag. | candidate name | SR |
| 0x24B70 | Save reader part | Restores native view from save field `0x1D`; no spectator-flag writer. | candidate name | SR |
| 0x52B0 | Common save writer | Section table `0x336B20`; row 103 = native view. Entry: two 5-byte MOVs `[0x52B0,0x52BA)`; unwind end `0x5AD8`; SE append hook site later at `0x5A4C`. | confirmed, disassembly | PCC |
| 0x86F80 | Ordinary save export | Caller of `0x52B0`. | confirmed, static | PCC |
| 0x816B0 | Trail-template writer | Caller of `0x52B0`. | confirmed, static | PCC |
| 0xB8220 | Count live building types | Save pre-pass; no view read. | confirmed, static | PCC |
| 0x186040 | Count live unit records | Into global `0x3668E3C`; no view read. | confirmed, static | PCC |
| 0x84A30, 0x8AE80 | Kick, start | Write human registration `0x8574BCC`. | confirmed, static | PCC |
| 0x8AA20 | Building interaction admission | Owner team == view team, or non-human AI owner. | confirmed, static | PCC |
| 0x78E0 | Checksum word helper | Sign-extends bytes, rotate-left-1 between words, uint32 wrap. | confirmed, static | DESYNC |
| 0xD1AD0 | Regular scheduler | Calls checksum builder with argument 0. | confirmed, static | DESYNC |
| 0x97C70 | RunTick post-view UI step | App mode `0x10` sub-mode 2 -> `0x982A0` -> `0xD6DE0`. | confirmed, static (other sub-modes unaudited) | GA |
| 0x45F20 | Camera target producer | `49 63 C0 4C 8D 04 40 42 03 94 81 6C E1 61 00 E9 EC A2 00 00`; adds `[RCX+R8*4+0x61E16C]`, jumps `0x50220`. First two complete instructions `[0x45F20,0x45F27)` (3+4 bytes), no entry inside. | confirmed, disassembly | SBP |
| 0x50220 | Camera write | Pending target manager `0x3A11DC0` +0x7C/+0x80/+0x84, offsets +0x98/+0x9C; calls clamp `0x4B270` (+0xA0/+0xA4). | confirmed, static | SBP |
| 0xCC140 | First-family building lookup by view | Used by mapper camera branches. | confirmed, static | SBP |

### GameAction producers (enqueue as native view)

| RVA | GameAction (value) -> opcode | Evidence |
|---|---|---|
| 0xD6620 | GateHouseState 0x3E9 -> 45 | GA |
| 0xD4EE0 | RepairBuilding 0x3EA -> 68 | GA |
| 0xD6F00 | ToggleSleep 0x3EB -> 43 | GA |
| 0xD6DE0 | SetTaxRate 0x3EC (writes view `+ADE4`) -> 34 | GA |
| 0xD6790 | SetRationing 0x3ED -> 35 | GA |
| 0xD6590 | SetFoodEaten 0x3EE (opcode 35 food restriction variant, probable) | GA |
| 0xD6520 | DrawbridgeState 0x3F0 -> 37 | GA |
| 0x199B50 | Troops_Stop 0x3F2 / Disband 0x3F3 (via `0x90510`) -> 36 | GA |
| 0x11E860 | Troops_ChangeStance 0x3FC -> 70 | GA |
| 0xD66B0 | SetNextWeaponMade 0x3FF -> 33 (not in opcode catalog) | GA |
| 0xD7080 | BuyGoods 0x400 / SellGoods 0x401 -> 38 | GA |
| 0x191DC0 | AmmoRecharge 0x411 -> 86 | GA |
| 0xD7520 | Ally_* 0x425..0x42A -> 113 | GA, SR |
| 0x105510 | ExtremePower 0x42C -> 119 | GA |
| 0x1045F0 | FreeBuild_Event 0x407 (direct write, probable) | GA |
| 0x89F40 | SelectBuildingType 0x417 opens view's building panel, writes mirrors | GA |
| 0xD0300, 0xD0F80 | GroupTroops (KeyFunctions overload) control groups | GA |

## Data and fields

| Record/manager | Offset | Meaning | Type/sign/unit | Confidence | Evidence |
|---|---|---|---|---|---|
| ChoreManager (module RVA 0x8574320) | +0x8A8 | Declared local MP ID | int | confirmed, static | PCC |
| ChoreManager | +0x8AC + slot*4 | Registered-human progress slot (-1 = unregistered) | int | confirmed, static | PCC |
| ChoreManager | +0x9A0 | Host flag (local ID == 1) | byte/int | confirmed, static | PCC |
| ChoreManager | +0xDA8 | Early-return condition in `0x24E80` | | confirmed, static | PCC |
| ChoreManager | +0xEE4 (RVA 0x8575204) | Connection pause (heartbeat writes only when < 1; lag byte 0x7F when paused) | int | confirmed, static | PCC |
| ChoreManager | +0x84CC8 | Current slot index | int | probable | CHORE |
| ChoreManager | +0x84CCC (RVA 0x85F8FEC) | Handler mode: 0 execute, 1 serialize, 2 receive-size | int | confirmed-runtime (old hash) | CHORE, QT |
| ChoreManager | +0x84CD4 | Payload size | int | probable | CHORE |
| ChoreManager | +0xB0BF8 (RVA 0x8624F18..0x86C1318) | 500 pending slots x 0x500 | | confirmed, static | CHORE, PCC |
| ChoreManager | +0x14D030 / +0x14D054 + player*4 | Heartbeat tick progress (two tables) | int | confirmed, static | PCC |
| ChoreManager | +0x14D078 + player*4 | Heartbeat `timeGetTime()` | uint ms | confirmed, static | PCC |
| ChoreManager | +0x279F58 | Ahead-versus-delay condition flag | | confirmed, static | PCC |
| ChoreManager | +0x36FA4C (RVA 0x88E3D6C) | Executing issuer (decoded sender) | int | confirmed, static | PCC, SBP |
| ChoreManager | +0x36FA50 (RVA 0x88E3D70) | Native view = local enqueue source | int | confirmed, static | PCC |
| ChoreManager | +0x370BA8 | Local reference tick | int | confirmed, static | PCC |
| ChoreManager | +0x370BB4 / +0x370BB8 | Signed ahead / behind | int | confirmed, static | PCC |
| ChoreManager | +0x370BE8 | Last skip-demand timestamp | ms | confirmed, static | PCC |
| ChoreManager | +0x370BEC (RVA 0x88E4F0C) | Skip count (frame-skip demand) | int | confirmed, static | PCC |
| ChoreManager | +0x370BF4 | Queue cursor | int | confirmed, static | PCC |
| ChoreManager | +0x370BF8 | Field cursor | | probable | CHORE |
| ChoreManager | +0x370BFC | Local command delay (seed 35; 0 in modes 0/99) | ticks | confirmed, static | PCC |
| Pending slot (0x500) | +0x00 | Scheduled tick | int | confirmed, static | CHORE |
| Pending slot | +0x04 | Sender/source player | int | confirmed, static | CHORE |
| Pending slot | +0x08 | Opcode | byte | confirmed, static | CHORE |
| Pending slot | +0x09 | State (1 = new; 0 or signed >= 10 = reusable) | byte | confirmed, static | CHORE, PCC |
| Pending slot | +0x0C | Command ID | int32 | confirmed, static | CHORE |
| Pending slot | +0x10 | Already in host barrier | byte | confirmed, static | CHORE |
| Pending slot | +0x11 | Opcode parameters | bytes | confirmed, static | CHORE |
| Staging globals | 0x86C132C | Tribe ID (17/36), selected power (119) | int | confirmed, static | QT, STT |
| Staging globals | 0x86C1330 | Chore-36 command or Chore-17 tile X; power target tile (119) | int | confirmed, static | QT, STT |
| Staging globals | 0x86C1334 | Chore-17 Y; local power actor (119) | int | confirmed, static | QT, STT |
| Staging globals | 0x86C1338 | Chore-17 signed patrol mode (1 byte packed, sign-extended) | int | confirmed, static | QT |
| Staging globals | 0x86C133C | Chore-17 move type; Chore-28 arg five (placement variation) | int | confirmed, static | QT, SBP |
| Handler table | 0x2C7A30 | Handler pointers indexed by opcode (entry 111 at `0x2C7DA8`) | ptr[8] | confirmed, static | CHORE, QT |
| Globals | 0x3666080 | Spectator flag | byte | confirmed, static | GA, SR |
| Globals | 0x3665F28 | Editor flag (== 1 gates editor GameActions) | int | confirmed, static | GA |
| Globals | 0x8574BCC | Per-player human registration | int[] | confirmed, static | PCC |
| Globals | 0x67E8394 | Selected building | int | confirmed, static | GA |
| Globals | 0x67E8420 | Local selection (marked) count (= unit manager +0x20) | int | confirmed, static | GA, PCC |
| Unit manager | +0x24 + player*4 | Per-player selection count | int | confirmed, static | PCC |
| Unit manager | +0x658 / +0x65C | `EmptyUnitFillValue` (= Chore-56 transfer base `0x67E8A58`) / typed `GameUnitArray` | | confirmed, static | DESYNC |
| Globals | 0x7338278 + unitId*1000 | Unit path bytes (checksummed) | 1000 bytes | confirmed, static | DESYNC |
| Globals | 0x60AD56C | Input state read by nested spectator boundary | int | confirmed, static | PCC |
| Globals | 0x8EBA92 | RunTick `mpFrameSkip` input flag | byte | confirmed, static | PCC |
| Globals | 0x8EBA98 / 0x8EBA93 / 0x8EBA9C | Mouse state / rightDown / rightUp | | confirmed, static | STT |
| Globals | 0x8EBAC1 | Byte: input starts move flags at 0x81 instead of 1 | byte | confirmed, static | PCC |
| Globals | 0x3668E3C | Live unit-record count (save pre-pass) | int | confirmed, static | PCC |
| Per-player record (stride 0x583C, base 0x379ADxx + view*0x583C) | +ADE4 / +ADF0 | Tax rate / displayed tax (differ -> op 34 enqueue) | int | confirmed, static | GA |
| Per-player record | +ADF8, +ADE0, +ADE8, +AE04 | Mirror fields written by 0x403/0x417/`0x89F40` | | confirmed, static (relevance open) | GA |
| Player resources (stride 0x583C) | +0x384C | `r_AIAllyOrderMode` | int | confirmed, static | STT |
| Player resources | +0x3850 | `r_AIRequestedAttackTargetPlayerId` | int | confirmed, static | STT |
| Player resources | +0x38E4 | `r_AIAllyOrderRequesterPlayerId` | int | confirmed, static | STT |
| Player resources | +0x46C | Delivery-reserve (incoming goods) table base | | confirmed, static | STT |
| Player resources | +0x50C | `r_TotalGoodsGold` | signed int | confirmed, static | STT |
| Group/tribe record (stride 0x688; table rows 58/59 from 0x7CC6748, 1500 / 4500 records) | raw +0x2A | Local pending-selection reservation (typed `GameTribe*` begins here) | int16 | confirmed, static | PCC, QT |
| Group record | raw +0x40 | Group state | | confirmed, static | PCC |
| Group record | raw +0x56C | Value selected by move-flag bit 7 | | confirmed, static | PCC |
| Group record | raw +0x582 (typed +0x558) | Movement/patrol mode (-1 finite Shift chain, 0 done) | int16 | confirmed, static | QT |
| Group record | raw +0x5B4 + idx*4 | Waypoint coordinate pairs (10) | int16 x2 | confirmed, static | QT |
| Group record | raw +0x5DC / +0x5DE (typed 0x5B2/0x5B4) | Current index / stored point count | uint16 | confirmed, static | QT |
| Unit (typed GameUnit, 0x490; wire = typed + 4) | typed +0x30 | `r_UnitHover` (local marking) | | confirmed, static | PCC |
| Unit | typed +0x68 | Attack-target marker word (renderer clears) | int16 | confirmed, static | QT |
| Unit | typed +0x92 | Ownership | | confirmed, static | PCC |
| Unit | typed +0x94 (wire +0x98) | `r_GlobalId` | int | confirmed, static | DESYNC |
| Unit | typed +0xAE (wire +0xB2) | `r_UnitSelected` (compared with player ID) | | confirmed, static | PCC, DESYNC |
| Unit wire record | +0x34 | Preserved local word restored after Chore-56 copy | | confirmed, static | DESYNC |
| Unit wire record | +0x3C, +0x40, +0x44 | uint32 fields added to checksum | | confirmed, static | DESYNC |
| Unit wire record | +0x8C (RVA 0x67E8F74 for unit 1; LEA `0x220C2`) | Start of 0x404 hashed bytes | | confirmed, static | DESYNC |
| Unit wire record | +0x41C (RVA 0x67E9304; LEA `0x2209A`) | 8 bytes zeroed during hashing | | confirmed, static | DESYNC |
| Unit raw | +0x8FC, +0x9B0, +0x918, +0x708 | Marking filter: selection allowed != 0, zero, != 0x6D (disband), zero | | confirmed, static | PCC |
| Building (0x32C) | +0xC2 / +0xD8 | Attack-target marker word / Global ID | | confirmed, static | QT |
| Tile manager (0x405EDB0) | +0x204E70C (RVA 0x60AD4BC) | Placement variation result (qword 15 = automatic) | | confirmed, disassembly | SBP |
| Building manager slot | +0x15E | Persisted variation | int16 | confirmed, disassembly | SBP |
| Camera manager 0x3A11DC0 | +0x7C/+0x80/+0x84, +0x98/+0x9C, +0xA0/+0xA4 | Pending target, offsets, clamp outputs | | confirmed, static | SBP |
| PlayState return buffer | +0x554/+0x556/+0x558 | Pending camera target low words | | confirmed, static | SBP |
| PlayState return buffer | +0x78C (managed offset 1932) | `SkipFrame` int16 | int16 | confirmed, static | PCC |
| PlayState return buffer | 0xB12 | `spectatorMode` | byte | confirmed, static | SR |
| Projectile type 41 | +0xD4 / +0xD8 | Popup mask / popularity snapshot (/100) | | confirmed, static | SR |

## Behavior

### Wire formats and handler ABI
- Outer record: int32 payload length, 1 byte target (outgoing, 0 = broadcast) or sender (incoming), payload; record = length + 5; negative length terminates the outgoing buffer. [confirmed, static] ([CHORE](../CHORE_SYSTEM.md#wire-formats))
- Inner normal Chore: opcode byte, 3-byte little-endian unsigned tick (zero-extended; wrap after 16,777,216 ticks ~ 116.5 h at 40 tps, untested), int32 command ID, payload. [confirmed, static] (CHORE)
- Opcodes 0, 1, 125, 126, 127 take special timing/sync/compression/ping paths; normal handler dispatch is valid only through 120; table bytes for 121..124 are not handler pointers. [confirmed, static] (CHORE)
- Handlers take no arguments; they read implicit ChoreManager state. Mode 0 executes, mode 1 serializes staged values, mode 2 publishes the expected receive size. Fixed payload sizes are the safe contract; a variable size needs extra receive-dispatch handling. [confirmed-runtime on old hash] ([CHORE](../CHORE_SYSTEM.md#pending-slots-and-handler-abi))
- Pending execution, local pack, local immediate execution, remote measure and remote immediate execution all use this no-argument ABI. The local immediate dispatch has an incoming branch to `0x23B98`; predecessor block `0x23B85..0x23B98` ends there, so a 14-byte patch at `0x23B94` would span a branch target. [confirmed, disassembly] (DESYNC)
- The manager phase seen inside a field-helper call does not identify the originating dispatch (Chore 56 changes phase 1 -> 0 while packing). [confirmed, static] (DESYNC)

### Scheduling, identity and barrier
- `commandId = playerId * 100000000 + perPlayerCounter`. [confirmed-runtime, old hash] (CHORE)
- Initial tick ~ `max(currentTick, trackedReferenceTick) + dynamicDelay`; a special path uses `currentTick + syncPeriod*50` (200 for period 4). The first scheduled tick is provisional; the host SyncEvent can move it. Source, request and command ID are the stable identities. [confirmed-runtime, old hash] ([CHORE](../CHORE_SYSTEM.md#scheduling-identity-and-barrier))
- The sender does not receive its own record: its slot is created at local enqueue; receivers create slots from the record; both execute the same handler in mode 0 at the scheduled tick. [confirmed-runtime] (CHORE)
- Opcode 120 SyncEvent: host scans pending slots, marks unassigned ones (slot `+0x10`), payload target tick, count, sequence, IDs; static max 25 IDs, runtime saw 5. Clients stall at the target tick if a listed ID is missing; after ~3 s the forced-run path permits progress (not triggered in tests). [confirmed-runtime / static] (CHORE)
- "SyncEvent - Forced run" goes to logger stub `0x1A4A50` = `RET 0`; no log output exists in this image. [confirmed, disassembly] (DESYNC)
- Due commands are executed sorted by signed command ID. [confirmed, static] (PCC)
- Local delay `+0x370BFC` is seeded 35 and only zeroed late in RunTick (`0x1CD20`) in modes 0/99; network mode alone does not prove equal scheduling across initialization. [confirmed, static] ([PCC](../PLAYER_CONTROL_CONTEXT.md#received-issuer-versus-local-enqueue-viewpoint))
- If all 500 slots are active, the cursor returns the last active slot and the queue overwrites it (no capacity refusal). [confirmed, static] (PCC)
- Unmodified peers execute an unknown/custom opcode as no-op and desync; all peers need compatible opcode registrations. [confirmed, static] (CHORE)
- Not proven: save/load with pending custom Chores, native resync, repeated sessions/map changes, disconnect/host migration, sustained slot recycling, exact 25-ID limit, tick wrap, forced run. [open] ([CHORE](../CHORE_SYSTEM.md#safety-and-remaining-gaps))

### Issuer versus enqueue source
- Received issuer = Steam roster member resolved by managed `findMember` (`fromMember.playerID`), not a field of the inner Chore; managed save/load roster remapping happens before. [confirmed, static] (PCC)
- Execution copies slot `+4` through no-op `0x1F9A0` to issuer `+0x36FA4C`. [confirmed, disassembly] (PCC)
- Local enqueue takes its source from the native view; changing the view and queuing changes the claimed network source. A report viewpoint is not authorization. [confirmed, static] (PCC)
- `DLL_MapAction` temporarily writes its `playerID` argument as view; delayed handlers use the decoded sender, so a single-player human can submit a mapped action for an AI owner without switching view permanently. Managed `PlaceMapperItem`/`DeleteBuilding` pass `EditorDirector.ActivePlayerID`. [confirmed, static] (SBP, PCC)
- Managed player-ID arguments other than the native view appear only in `PlaceMapperItem`/`DeleteBuilding`, the ally panel (`GameData.playerID`), insults, chat and merit. [confirmed, static] (PCC)

### Heartbeat, progress and frame skipping
- Heartbeat payload: 5 bytes (type, low three tick bytes, lag byte); writes tick into both progress tables and `timeGetTime()` for the player loaded three times independently from the native view; writes only when connection pause < 1. [confirmed, static] ([PCC](../PLAYER_CONTROL_CONTEXT.md#multiplayer-progress-and-frame-skipping-2026-11-03-vanilla-facts))
- Receivers index progress by the authenticated peer ID; the sender's own local update uses its selected view. Selecting an AI view therefore advances the AI-indexed entry and leaves the sender's real human slot stale locally. [confirmed, static] (PCC)
- `0x24E80` uses the view from `0x869D2` as local reference; resets skip count; mode 0/99 and `+0xDA8` skip demand; negative behind returns first; ahead must exceed 4 with >= 50 ms elapsed. Host demand 1 for ahead 5..14, 2 for 15..99, 3 for >= 100; non-host 1 only for ahead > 8. Timestamp updated on the elapsed path even without demand. [confirmed, static] (PCC)
- `0x19D960` copies skip count to `+0x78C` only when incoming `mpFrameSkip` is zero. Managed `Director.MPSkipFrame` adds to `FrameToSkipMP` in MP only; `runSimTickTest` decrements after each call including the creating one, so demand 3 skips two subsequent calls. [confirmed, static] (PCC)
- The SyncEvent gate's local sim-skip flag is a separate decision from this demand. Managed `lowFPSCompensationLevel` (4 for speed <= 10, 3 <= 15, 2 <= 20, else 1; derived from requested speed, not FPS) is a third source of skipped calls. [confirmed, static] (PCC)

### Selection, groups and orders
- Selection is staged: local marking (`r_UnitHover`), group reservation (`+0x2A`), synchronized opcode 16 selection (`r_UnitSelected` = player ID); unselect receiver clears for the executing owner. Capturing only the final move Chore is insufficient. [confirmed, static] ([PCC](../PLAYER_CONTROL_CONTEXT.md#unit-selection-is-more-than-a-highlighted-object))
- Reservation is chosen by the native view; two PCs viewing the same player pick the same free slot and `0x119CE0` clears it on opcode-16 execution; sharing a view with an active owner is unsafe. [confirmed, static] (PCC)
- Opcode-59 full-group sync excludes the local reservation; no proof that every save/load route repairs reservations; whether opcode 15 releases every reservation is unproven. [confirmed / open] (PCC)
- `0x119C00` allocation ignores the reservation, so independent `Create` on every peer is not guaranteed reservation-safe or identical. Assignment `0x11D370` does not remove prior membership. [confirmed, static] (PCC)
- Order staging queues opcode 17 before its own feedback checks; a tribe slot is not a stable member list. [confirmed, static] (PCC)
- Input starts move flags at 1 (0x81 if byte `0x8EBAC1` set) and toggles bit 7 for a repeated click on the same tile within 500 ms. [confirmed, static] (PCC)
- Chore-17 move types from the sole producer: 0, 1, 0x81; bit 0x80 stripped on unpack; bit 0x40 unused by Vanilla. [confirmed, static] (QT)
- Chore-17 patrol mode (`0x86C1338`) is packed as one byte and sign-extended before `0x196100`/`0x11B520`. [confirmed, static] (QT, `Pathfinding/UpdateToNewDLL.md`)
- Finite Shift waypoint chain uses movement mode -1; Vanilla resets to 0 only after the final point; overlay draws markers only when mode != 0; point count is a fixed stored count, not remaining. [confirmed, static] (QT)
- RunTick order: input, due Chores, then `0xCDE60`; inside the sim, unit loop before group maintenance; corpses remain in a group bitmap only until maintenance. [confirmed, static] (PCC)
- Local box marking: all owners' IDs arrive via `DLL_TroopSelection`, but only native-view units pass (state 2, no death marker, raw `+0x8FC` != 0, `+0x9B0` == 0, `+0x918` != 0x6D, `+0x708` == 0), Lords (type 0x37) excluded. [confirmed, static] (PCC)
- Managed: `TroopSelection` takes screen pixels; order tile = `mouseOverX/Y` from `preDLLCallActions` (-1 over GUI unless `CurrentAction == 9`); `GameMap.grabTroopsOnScreen` has no owner test; `CurrentAction` 8 -> 9 on release, set to 0 before `TroopSelection` state 3. [confirmed, managed static] (PCC)
- `Settings_SH1RTSControls` (RunTick arg 26): true = left release orders, right press deselects via `0x897F0`; false = right release orders, left press without Shift deselects and reruns as release. [confirmed, static] (PCC)
- Troop HUD `troops_show_attack_here_and_type` from `0x19D960`: 1 (`0x1968B0`/`0x1968F0`), 2 (`0x196CB0`), 3 (types 0x27/0x28, ammo `0x40A`), 4 (`0x196A30`), 5 or 0 (`0x196AC0`). Type lists of the last two not resolved. [confirmed, static] (PCC)
- `DLL_GameAction` Patrol 0x3FB returns mapper 0x22, which the managed wrapper turns into `placeBuilding`. [confirmed, static] (PCC)
- Single observation (SP, 2026-11-05): an AI absorbed 150 spawned archers from a separate tribe into its own high-slot tribes within 300 ms; tribes built with `Create`/`UnassignUnit`/`AssignUnit` stayed intact for 51 s. Not a general AI rule. [in-game, one run] (PCC)

### Spectator input and view
- `DLL_RunTick` boundary `[0x868F6,0x86900)`: CMOVNE EAX,R12D (R12D = 0 at `0x868D6`, EAX = EBX at `0x868E3`, flags from `0x868DC`) + MOV to view; continuation MOVZX R8D,R14B; no branch target inside. [confirmed, disassembly] ([PCC](../PLAYER_CONTROL_CONTEXT.md#runtick-input-boundary))
- Nested boundary in `0x8C5F0`: branch `0x8D0D0..0x8D109` (stack flags, ESI==1), EBX = input state, calls `0x897F0`, loads view `0x8D10B`, spectator compare `0x8D115`, CMOVNE EAX,EDI at `0x8D128` (EDI = 0 from `0x8D0C8`), XOR R9D `0x8D12B`, store `0x8D12E`, recursive mode 3 `0x8D15B`, restore `0x8D16D`; no branch into `0x8D129..0x8D133` or `0x868F7..0x86903`. [confirmed, disassembly] (PCC)
- Further spectator-flag readers: `0x41D60`, `0x6E620`, `0xB5690`, `0x1D160` (tests `human[view]==0 && !spectator`), `0x65830`, `0x94350`, `0x884D0`, `0x38A0`, `0x109EF0`, GameAction Undo `0x40C`; clearing the flag would change those. [confirmed, static] (PCC)
- `SpectatorMode` 0x431 sets only the flag, returns 0; no managed role/UI change; an eliminated positive participant does not become a nonpositive spectator. [confirmed, export] (SR)
- Human registration `0x8574BCC` is not read by the input dispatcher, selection, order staging, opcode-17 wrapper or deselection. [confirmed, static] ([PCC](../PLAYER_CONTROL_CONTEXT.md#human-registration-and-controlled-ai-players-2026-11-02-vanilla-facts))
- Controlling an AI in a live mission is not yet confirmed in-game. [open] (PCC)

### GameAction classes under a foreign view
- Only Undo `0x40C` checks the spectator flag; every enqueue uses `0x23990`. Full class table (acts-as-view, arms cursor, direct writes, local presentation, editor/global, self-guarded) in [GA](../GAMEACTION_OBSERVER_CLASSES.md). [confirmed, static]
- Local presentation/camera/pause branches: `0x402`, `0x404`, `0x405`, `0x406`, `0x413`, `0x415`, `0x416`, `0x418`, `0x42B`, `0x431` (plus OpenDogCage 0x3F1 no-op, 0x40B, 0x419, 0x422, 0x423, 0x42D, 0x42F). CloseTroopsPanel `0x410` also deselects and is not pure presentation. [confirmed, static] (GA, SR)
- Autotrade staging 0x41B..0x41D writes globals until Apply 0x41E (op 109); `EngineInterface.SetAutoTrade` calls some directly; `setUpInbuilding` and `FatControler.NoesisGUIUpdateChecksInGame` send Apply/Pause automatically while the panel is visible. [confirmed, static] (GA)
- Editor-only GameActions (EditorWipeGoods, RespawnLord, WipeAnimals, HoldTime, FastGoodsFeedin, Balanced) need editor flag `0x3665F28 == 1`; SH1Cheats 0x414 only in SP modes 0/99. [confirmed, static] (GA)
- KeyFunctions overload (< 1001): MPPing `0xCA` enqueues op 0x66 as view; GroupTroops edits control groups. [confirmed, static] (GA)
- `MainViewModel.buildControlsFreeze` does not close open panels; `ButtonChangeRations`/`ButtonAdjustTax` call SetRationing/SetTaxRate directly without spectator check; tax uses the view's resources. [confirmed, static] ([SR](../SPECTATOR_REPORTS.md#eliminated-participants-and-retained-action-panels-2026-11-02-vanilla-facts))

### Reports and spectator saves
- RunTick restores the view after spectator input, then `0x19D960` fills PlayState by the selected view (resources, popularity, population, troops, religion). [confirmed, static] (SR)
- `MainViewModel.ButtonReports` reads `FreezeMainControls` once and forces Army (`num = 9`) when frozen; `FatControler` freezes build controls when `ActivePlayerID <= 0` and sets `PlayerNameText` from `Settings_UserName`; `PlayerNameText` notifies only on change. [confirmed, managed static] (SR)
- Food report has four `ButtonSetEdibleCommand` buttons sending `SetFoodEaten`; their `BTN_Building` style hides sprites when disabled (`IsHitTestVisible=false` avoids that). [confirmed, managed static] (SR)
- Spectator flag is cleared by load reset `0x87D0` and not restored; view restored from save field `0x1D`; non-editor load converts view 0 to 1 and writes human registration 1 for a CPU-less slot (synthetic human). `retData+0x3C` carries the one-based view. [confirmed, static] (SR)
- `createLoadedSkirmishMembers` creates 8 placeholder members without `skirmishAI`; only native PlayState computer/human arrays describe the roster, and a lone synthetic human is indistinguishable from a real one. [confirmed, managed static] (SR)
- Pending native Chores do not survive load: `0x853F0` -> `0x87D10` -> `0x211A0` -> `0x241B0` zeroes the slots; section table has no range over the slots; group records (with reservation) are saved. [confirmed, static] ([PCC](../PLAYER_CONTROL_CONTEXT.md#pending-queue-and-save-restoration))
- Save row 103 stores the native view; a human viewing another player saves that foreign view as local identity. [confirmed, static] ([PCC](../PLAYER_CONTROL_CONTEXT.md#saved-local-identity))

### Ally commands
- `HUD_AlliesPanel` lists use `GameData.playerID` (not updated by `SetEditorPlayer`); `ButtonClicked` sends 1061..1066; `DLL_GameAction` uses the native view as sender; order/cancel/confirm enqueue 113; goods branches check sender resources. Ally/enemy lists have 6 slots. [confirmed, static] (SR, STT)
- 113 handler branches: `0xD7580` (orders), `0xD78C0`, `0xD7AD0` (send goods), `0xD7450`, `0xD7360`, `0xD77D0`. [confirmed, static] (SR)
- `0xD7580(attackTarget, orderedAi, requester, order)` (587 bytes, one-based IDs): order 10 clears, 11 requests protection, else attack target; no alliance check; writes request state only on AI acceptance. [confirmed, static] (STT)
- Defend acceptance `0x2A500`: initialized AI, help-block flag zero, signed eligible-army count >= AIC `siege_troops_before_will_come_to_rescue`. Attack acceptance `0x41500` analogous. Accepted attack prepares via `0x2A580` (`0xC4080`, `0x2B5F0`, `0x304B0`, `0x30550`, clears siege state). [confirmed, static] ([STT](../SPECTATOR_TEST_TOOLS.md#defend-eligibility-2026-11-10-vanilla-facts))
- Help-block flag written by `0x2DFC0` (nearby hostile strength + margin vs own near-keep strength); `0x182B00` counts eligible roles; an allied AI with a HUD army can still decline. [confirmed, static] (STT)
- `0x2F670` clears requests whose requester Lord is invalid; protection handled by `0x3C2E0` -> `0x3D020`. [confirmed, static] (STT)
- Send-goods `0xD7AD0` moves physical goods sender -> recipient, updates stats, clears matching requests; gold capped by sender funds. Storage consumers `0xAED70`/`0xAF210` absorb incoming goods by capacity (not in editor). [confirmed, static] (STT)
- Whether a spectator-origin 113 packet stays synchronized on all peers is unverified. [open] (SR)

### Power targeting input
- Input mode 4 accepts a power on the DOWN edge: `rightDown` when SH1RtsControls false, `leftState == 1` when true (rightDown then cancels via `0x897F0`). Mode 5 transitions separately. Spawn powers 2, 3, 4, 6 have extra spatial tests; ArrowVolley 0 does not. Acceptance stages power/tile/actor at `0x86C132C/30/34`, enqueues opcode 119, resets mode 0. [confirmed, static] ([STT](../SPECTATOR_TEST_TOOLS.md#power-confirmation-input-2026-11-13-vanilla-facts))

### Building and wall placement
- Wall: `0x8AAF0` -> `0x94120`; preview `0x61210`; line limit by stone at `0xCF640`; delayed chore 25 via `0x11B50` -> `0x6F230`, `0xC8CC0`, `0xB7D80`. A click-time stone change cannot make construction free. [confirmed, static] (SBP)
- Building: `0x90CD0` checks `0xCC420`, enqueues 28; `0x12090` re-checks and calls `0x6D580` (terrain/collision validation retained). [confirmed, static] (SBP)
- Placement validator `0x77E60` writes qword 15 at `0x77EA5` to tile manager `+0x204E70C`; Stores 52, Granary 80, Armoury 81 resolve via selector table `0x79914` / target table `0x798F4` to `0x780D0`; later storage checks `0x7897F`, `0x789FF` can reject; neighbor helper `0xC72C0`; no other write through `0x798F3`. [confirmed, disassembly] (SBP)
- `0x6D580`: Granary/Armoury -> `0x75780` -> allocator `0xB47E0` (arg 9 variation; persisted at `0xB4AB5` to slot `+0x15E`); Stores -> `0x76E80` (four stockpiles). Renderer `0x6E620` compares variation 15 at `0x6EFAA`/`0x6EFAE` -> automatic branch `0x6F04A`. [confirmed, disassembly] (SBP)
- Mapper camera: `StartMapperItem` -> `DLL_StartMapAction` `0x87830` -> `0x8BD60`; first-family branches (0x34, 0x4D, 0x4F, 0x50, 0x51, 0x56/0x57, 0x58/0x59) call `0x45F20` at `0x8BF96` (return `0x8BF9B`); serializer resets pending XY; managed `GameData.SetCameraFromGameState` applies it later. [confirmed, disassembly] ([SBP](../SPECTATOR_BUILD_PLACEMENT.md#mapper-selection-camera-producer-2026-11-04-vanilla-facts))

### Desync checksum and resync records
- Unit checksum scans IDs 1..9999: 0x404 bytes from wire `+0x8C` with 8 bytes at wire `+0x41C` zeroed, the 1000-byte path, plus uint32 at wire `+0x3C/+0x40/+0x44`; masks restored. [confirmed, static] ([DESYNC](../DESYNC_CAPTURE.md#native-records-and-checksum-inputs))
- Chore-56 wire record = typed GameUnit minus 4 bytes; slot 0 is a sentinel; preserved word wire `+0x34`. Typed offsets (e.g. surrender audit) must not be applied to wire records. [confirmed, static] (DESYNC)
- `0xD1AD0` calls the checksum builder with 0, Chore 54 `0x15170` with 1; both can occur in the same tick. [confirmed, static] (DESYNC)

### Campfire popularity popup (spectator-visible)
- `0xB5690` creates projectile type 41 via `0x9B2B0` (updater run at `0x9BA06` through table `0x2DA9C0` before the value is set), writes popularity/100 at `0xB5845`. Updater `0x9AC20` (`0x9ACAC..0x9ACCF`): mask 130 < 40, 129 for 40..59, 128 >= 60. `0xA12C0` suppresses duplicates; `0xCAA70` schedules later snapshots. Packet builder `0x1A13C0` writes mask to short index 17, value to 18/19. Managed `addUpdateFly` creates icon children only on creation, so a stale red mask can persist. [confirmed, static] (SR)

### Extended Shift queue natives (from QueueTest)
- Draw helper ABI `void(drawManager, category, spriteId, layer, verticalOffset, tileId, flags)`; Vanilla PatrolOnce flags tuple `category=0xAC`, `layer=0x12`, `verticalOffset=-1`, `flags=0xA0022`, sprites `0x138..0x141` (flags) and `0x12E..0x136` (digits). [confirmed, static] (QT)
- Extender 2.2.0 `GameTribe` labels at `0x5B2/0x5B4` conflict with native flow; point count is a `ushort`. Extender 2.0.2 `UnassignUnit` forwarded arguments in the wrong order; fixed in 2.2.0. [confirmed, static] (QT)

## Cross-topic links

- Save routes and the writer `0x52B0`: [SAVE_WRITER_ROUTES.md](../SAVE_WRITER_ROUTES.md).
- Chore 43 and building sleep selection: [BUILDING_SLEEP_SELECTION.md](../BUILDING_SLEEP_SELECTION.md).
- Typed unit offsets for surrender: [SURRENDER_SELECTION.md](../SURRENDER_SELECTION.md).
- Group movement/pathing (`0x11B520`, tribes): [PATHFINDING_MIGRATION.md](../PATHFINDING_MIGRATION.md) and `Pathfinding/UpdateToNewDLL.md`.
- Mission start/load lifecycle: [MISSION_LIFECYCLE.md](../MISSION_LIFECYCLE.md).

## Open questions and contradictions

- Chore receive order (audited on the older DLL 33AA3345, not yet re-verified on FBCB9319): the remote-chore routine zeroes the whole 1260-byte (`0x4EC`) payload slot, sets handler phase 2 (`ChoreManager+0x84CCC`) and calls the handler, then copies the full outer payload with one memcpy, sets phase 0, resets the cursor and calls the handler again. Handlers must act only on phase 1 (pack) and 0 (unpack) and ignore phase 2. The field-copy helper (`0x1F5F0` on that build) checks only the temp-buffer ceiling `0x2BF20`, not the current chore's outer length `+0x84CD4`, so a receiver trusting an embedded inner length reads zero padding; validate `innerLength <= outerLength - 4`. [probable, static, old hash] ([UpstreamReport.md](../../../../../ScriptExtenderChoreAudit/UpstreamReport.md))

- CHORE says the forced-run path "logs"; DESYNC shows the logger is `RET 0` in this image: no log line can be expected.
- Opcode 71 is "ExtendRallyPoint" (candidate submodes rally/patrol) in the catalog; QT identifies its handler as the Shift-waypoint append into `0x11C3A0`. Both are consistent if rally/patrol and Shift waypoints share this path; not resolved in the catalog.
- Opcode 28 is a catalog candidate ("PlaceBuildingCategory2"); SBP traces it as the general building placement (`0x90CD0` -> `0x12090`).
- Opcodes 33 (SetNextWeaponMade), 54, 56, 59, 102 (MPPing) have audited producers/handlers in these documents but no catalog entry.
- GA routes Stop/Disband through `0x90510` -> `0x199B50`; PCC says `0x90510` arms Attack Here and Stop/Disband map to group commands 0x1F/0x1E. Consistent as different sub-branches of one function; not separately traced.
- Opcode 31 payload: 5 bytes observed vs. early enum 2 x INT32.
- Opcode 111: indirect producers not disproved; 42: no current-build handler match.
- Open from GA: tax via `0x97C70` under foreign view; sub-modes 4, 0x23, 0x2C, 0x3F, 0x46 unaudited; checksum relevance of mirror writes; `0x8BD60(0x42)` attack-here path (0x123 branch enqueues 86); real dog-cage export.
- Whether opcode 15 releases every reservation; whether `0x1142E0` uses `0x119D60` in MP for other reasons.
- Pending live checks for placement: zero-stock walls of all three mapper types, delayed order matching, stone remainder, well sprite variation.

## Touched by our mods

- APIShared: authenticated opcode-106 channel, participant view identity, spectator input boundaries, heartbeat/progress correction, mapper camera bridge, GameAction broker -> [../mods/network.md#apishared](../mods/network.md#apishared).
- Spectator: observer action policy, placement, reports, ally/test tools -> [../mods/network.md#spectator](../mods/network.md#spectator).
- BugfixesAndQoL: troop HUD for controlled units, master host switch -> [../mods/network.md#bugfixesandqol](../mods/network.md#bugfixesandqol).
- Pathfinding: extended Shift queue hooks on Chores 17/36/71 -> [../mods/network.md#pathfinding](../mods/network.md#pathfinding).
- Testmods DesyncTest, PlayerControlRelayTest, Chore probe -> [../mods/network.md#testmods](../mods/network.md#testmods).

## Sources

- [CHORE_SYSTEM.md](../CHORE_SYSTEM.md): Chore network path, wire formats, slots, scheduling/barrier, function map (historical/current), opcode cautions, two-peer runtime results (old hash), gaps.
- [DESYNC_CAPTURE.md](../DESYNC_CAPTURE.md): unit checksum inputs, Chore-56 wire record, forced-run logger stub, dispatch boundaries; DesyncTest note (mod).
- [GAMEACTION_OBSERVER_CLASSES.md](../GAMEACTION_OBSERVER_CLASSES.md): `DLL_GameAction` classes under a foreign view; Spectator command policy (mod).
- [PLAYER_CONTROL_CONTEXT.md](../PLAYER_CONTROL_CONTEXT.md): issuer vs view, selection/reservation, input boundaries, save identity, heartbeat/skip, groups, mouse input; APIShared/relay sections (mod).
- [SPECTATOR_BUILD_PLACEMENT.md](../SPECTATOR_BUILD_PLACEMENT.md): wall/building placement path, variation 15, mapper camera producer; Spectator/APIShared integration (mod).
- [SPECTATOR_REPORTS.md](../SPECTATOR_REPORTS.md): report data/mode flow, spectator save loading, ally panel, retained panels, campfire popup; Spectator integration (mod).
- [SPECTATOR_TEST_TOOLS.md](../SPECTATOR_TEST_TOOLS.md): ally order/acceptance contracts, goods/gold credit, defend eligibility, power confirmation input.
- [Findings/QueueTest.md](../../../../../../Findings/QueueTest.md): secondary source outside the baseline; Chore 17/36/71 handler contracts, staging globals, tribe waypoint fields, overlay renderer (feature now in Pathfinding).
- Structured: [chore-opcodes.jsonl](../chore-opcodes.jsonl), [chore-contracts.jsonl](../chore-contracts.jsonl), [chore-observations.jsonl](../chore-observations.jsonl), [chore-evidence.jsonl](../chore-evidence.jsonl), [function-claims.jsonl](../function-claims.jsonl).

### Opcode table (compact; authoritative data in chore-opcodes.jsonl)

| Op | Meaning | Status | Handler (FBCB9319) |
|---|---|---|---|
| 0, 1 | Timing control (special path) | probable | - |
| 4 | InitGame | confirmed-static | - |
| 5 | ResetPlayerList | confirmed-static | - |
| 15 | UnselectUnits (producer `0x19A5B0`) | candidate | - |
| 16 | SelectUnits (producer `0x191130`) | candidate | - |
| 17 | MoveUnits (8 bytes) | probable | 0x10AE0 |
| 20, 21, 23, 24 | Tile raise/property/min height/height | candidate | - |
| 25 | PlaceWall (24 bytes) | probable | 0x11B50 (SBP) |
| 26 | PlaceVegetation | candidate | - |
| 28 | Building placement ("PlaceBuildingCategory2") | candidate (SBP: traced) | 0x12090 |
| 29 | DestroyBuilding (7 bytes) | probable | 0x12400 |
| 31 | MakeTroop (5 bytes) | confirmed-static | 0x127A0 |
| 34 | TaxChange (4 bytes) | confirmed-static | - |
| 35 | Rations / food restriction (4 or 8 bytes) | probable | - |
| 36 | UnitOrder combined (15 bytes) | probable | 0x12BF0 |
| 37 | DrawBridgeState (12 bytes) | probable | - |
| 38 | Buy/Sell good (12 bytes) | probable | - |
| 39 / 40 | SaveGame / LoadGame (MP) | confirmed-static | - |
| 42 | PlaceSiegeTent | candidate (no current match) | old 0x138D0 |
| 43 | SetToggleSleep (2 bytes) | confirmed-static | 0x13C10 (producer 0xD6F00) |
| 44 | CreateAnimal (5 bytes) | probable | 0x13CA0 |
| 45 | GatehouseState (12 bytes) | confirmed-static | - |
| 68 | RepairBuilding (16 bytes) | confirmed-static | - |
| 69 | SpawnEntity (9 bytes) | probable | 0x172B0 |
| 70 | SetUnitStance (3 bytes) | probable | 0x175E0 |
| 71 | ExtendRallyPoint / Shift waypoint (8 bytes) | probable | 0x176C0 |
| 78 | DeselectUnit (2 bytes) | probable | 0x180A0 |
| 86 | AmmoRecharge (3 bytes) | confirmed-static | 0x18AE0 |
| 106 | Script Extender Chore (bounded blob) | confirmed-static | SE |
| 108 | TroopSelectionChanged | confirmed-static | - |
| 109 | AutoTradeSettings (7 bytes; HD name wrong) | probable | 0x1A240 |
| 111 | Unused candidate (no-op) | candidate | 0xFC30 |
| 113 | AllyCommand (18 bytes) | probable | 0x1A6F0 |
| 119 | ActivateTacticalPower (8 bytes) | confirmed-static | 0x1ABF0 |
| 120 | SyncManifest / SyncEvent | confirmed-runtime | 0x1AE10 |
| 125 | Compressed wrapper | probable | - |
| 126, 127 | Ping timing | probable | - |

Historical groups 46, 49, 54-67, 80-84, 87-101, 114-116 remain candidates (CHORE).
