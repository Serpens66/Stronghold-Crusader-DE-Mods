# Game session: missions, editor, saves, lobby, Trails, Lord slots, peace time and defeat [Scope: Vanilla]

Native hash FBCB9319 (full hash in ../../../../CURRENT.json). Summary state 2026-11-27. Check the installed DLL hash per CURRENT.md before using any address. Evidence links point to the audit documents; they stay authoritative.

Managed facts refer to `Assembly-CSharp.dll` SHA-256 BC8B6A39... (full value in the audit documents). Binding working rules for code reacting to mission start/end, saves, editor and timeline: [AGENTS/mission-lifecycle.md](../../../../../../AGENTS/mission-lifecycle.md) (not repeated here).

## Overview

- A game session is entered through managed roots (EditorDirector, FRONT_StandaloneMission, FRONT_Multiplayer, HUD_Tutorial, Platform_Multiplayer) that call a small set of confirmed native exports: preinitializers (`DLL_PreInitMap_Multiplayer` 0x86270, `DLL_PreInitMap_Editor` 0x85E90), the map loader `DLL_LoadMapToPlay` 0x84AC0 and the save loader `DLL_LoadSaveGame` 0x853F0.
- `DLL_LoadMapToPlay` dispatches to per-family initializers: campaign 0x87C40, ordinary mission 0x8AE80, skirmish/custom/Coop/MP-save 0x94350, trail routing 0x28960, tutorial 0x100530. Reset 0x87D10 and unload 0x5BC70 are nested repeatedly inside these paths.
- Native mode global 0x8574B90 (0 singleplayer, 1 network multiplayer, 99 skirmish/local Coop preparation) gates many gameplay features, including peace time and No Dogs.
- Multiplayer start: lobby -> `StartGame`/`StartSave` -> preinit -> load -> managed `postLoading` -> seed/ack handshake (packets 2/3/9/4) -> `DLL_RemapPlayers` 0x86580 -> `DLL_StartMultiplayerGameSynced` 0x878B0 -> `Director.startSimThread`. Every tick runs `DLL_RunTick` 0x86680 -> simulation update 0xCDE60.
- Saves are written only by common writer 0x52B0 (callers: `DLL_SaveSaveGame` 0x86F80, synchronized Chore 39 handler 0x13030, `DLL_CreateTrailMission` 0x816B0).
- Player slots are one-based (1..8); a map-slot permutation (0x89870) and remap wrapper (0xCFE00) reassign map positions to lobby players. Lords are spawned by 0xC23C0 and eliminated through the Lord state machine 0x15A950 and player removal 0xCD190.
- Lobby, Coop Trail, Trail Maker, save requester, chat and confirmation popups are managed (FRONT_Multiplayer, Platform_Multiplayer, FRONT_ManageTrail, HUD_LoadSaveRequester, HUD_ConfirmationPopup) and need no native address.

## Key functions

| RVA | Name/role | Notes | Confidence | Evidence |
|---|---|---|---|---|
| 0x84AC0 | `DLL_LoadMapToPlay` | Dispatches campaign/mission/skirmish/trail/tutorial; can normalize negative inner results to errorCode=1; passes save byte to 0x94350 | confirmed export | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md) |
| 0x87C40 | Campaign initializer | Errors -11/-21; Extender OnStartMap target | candidate name, pattern-confirmed target | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md) |
| 0x8AE80 | Ordinary (non-skirmish) mission initializer | Reader failure returns 1 (not success) | candidate | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md) |
| 0x94350 | Skirmish/custom/Coop initializer, also MP save branch | Success 0xFFFFFFFF, reader failure 0; save branch skips castle/unit/resource init; slot remap via 0x89870/0xCFE00; copies NoDogs setup to active at 0x96D79/0x96D7F | candidate name, flow inspected | [LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md), [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md) |
| 0x28960 | Trail routing | Reached from 0x84AC0 | candidate | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md) |
| 0x100530 | Tutorial initializer / scenario reconstruction | Separate route, need not emit OnStartMap; uses fixed 0x960 peace-time conversion | candidate | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md), [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| 0x853F0 | `DLL_LoadSaveGame` | Args UTF-16 bytes, Int32 byte length, out struct, editor flag; success return 1 and errorCode=1, failure 0; restores state without new-game startup | confirmed export | [SAVEGAME_MISSING_MAP_METADATA](../SAVEGAME_MISSING_MAP_METADATA.md), [EDITOR_MAP_LIFECYCLE](../EDITOR_MAP_LIFECYCLE.md) |
| 0x87D10 | Reset | Nested repeatedly; calls 0x5BC70 | candidate | [EDITOR_MAP_LIFECYCLE](../EDITOR_MAP_LIFECYCLE.md) |
| 0x5BC70 | Unload | Extender unload pattern target; repeated calls are not repeated sessions | candidate name, pattern-confirmed | [EDITOR_MAP_LIFECYCLE](../EDITOR_MAP_LIFECYCLE.md) |
| 0x87D0 | Archive reader | Returns 0 on invalid input; matches save section IDs against table 0x336B20 | candidate | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md), [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md) |
| 0x86270 | `DLL_PreInitMap_Multiplayer` | (byte skirmish, Int32 restart len, byte* restart, Int32 coop trail, Int32 mission, byte test/custom/extreme); resets via 0x87D10/0x8ADC0; sets mode 99 (skirmish) or 1 | confirmed export | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md), [COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md) |
| 0x8ADC0 | Reset helper in MP preinit | | candidate | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md) |
| 0x85E90 | `DLL_PreInitMap_Editor` | (int mapSize, int mapType, byte siegeThat, byte multiplayerMap, byte* retData); always errorCode=1 | confirmed export | [EDITOR_MAP_LIFECYCLE](../EDITOR_MAP_LIFECYCLE.md) |
| 0x86680 | `DLL_RunTick` | Decrements start countdown 0x8EBAA0 near entry; calls autosave scheduler 0x1F6E0 before managed pause branch; reaches 0xCDE60 | confirmed export | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md), [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x87880 | `DLL_StartMultiplayerGame` | Keeps saved random seed when fromSave; 0x8600 rebuilds random table | confirmed export | [LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md) |
| 0x878B0 | `DLL_StartMultiplayerGameSynced` | Writes 2 to countdown 0x8EBAA0, records tick at 0x366A12C | confirmed export | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md) |
| 0x86580 | `DLL_RemapPlayers` | Int32[9] + saved one-based local player; rebuilds human presence; absent non-AI slots -> 0xCD190 | confirmed export | [LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md) |
| 0x864C0 | `DLL_RegisterMultiplayerUser` | Human registration | confirmed export | [LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md) |
| 0x86550 | `DLL_RegisterSkirmishUser` | Writes Int32 nickname subtype to 0x8574C68 + playerID*4 | confirmed export | [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md) |
| 0x87350 | `DLL_SetExtendedLordConfig` | Decodes config into AIC record playerID+29 (stride 0x5E4), keeps original type at +4, then sets lineup type playerID+29; copy through 0x7140 and 0x1A8EE0; `opponent_type` stays available | confirmed export | [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md), [FIXES_COMPATIBILITY](../FIXES_COMPATIBILITY.md) |
| 0x81050 | `DLL_ApplyMultiplayerSetupData` | Tail-jumps to 0x26370 (setup copy) | confirmed export | [MODSETTINGS_ACTIVATION](../MODSETTINGS_ACTIVATION.md) |
| 0x26370 | Setup copy helper | Transfer DWORD 6 autosave -> +0x279F98; 7 peace minutes; 10..17 keep assignments (signed bytes); 0x16/0x17 advanced flags; 0x1B Improved Spearmen; offset 24 NoDogs -> +0x279FB8 (writes 0x263D4/0x263D7) | candidate name, writes confirmed | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md), [COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md) |
| 0x84280 / 0x20420 | `DLL_GetMultiplayerSetupData` / reverse copy | Reads NoDogs setup field back to transfer offset 24 | confirmed export / candidate | [MODSETTINGS_ACTIVATION](../MODSETTINGS_ACTIVATION.md) |
| 0x84600 / 0x286C0 | `DLL_GetTrailMissionInfo` / helper | Built-in non-Coop customization comparison; does not touch managed presentation | confirmed export / candidate | [COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md) |
| 0x81870 | `DLL_GameAction` | Writes native pause DWORD 0x366A0F8 for Game_Paused; action 6 called by RunTick after countdown | confirmed export | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x86F80 | `DLL_SaveSaveGame` | Filename via 0xA850, stores screen centres, calls 0x52B0; `mapSave` flag | confirmed export | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x816B0 | `DLL_CreateTrailMission` | Template route to 0x52B0 (third arg 0) | confirmed export | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x52B0 | Common save writer | Only writer; ignores write/close results after open, returns 1 even for short file; append boundary 0x5A4C | candidate name, flow confirmed | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x13030 | Chore 39 (0x27) synchronized save handler | 1102 bytes; phase 1 serializes, phase 0 writes; ignores writer result | candidate name, flow confirmed | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x1F6E0 | Autosave scheduler | timeGetTime interval check; no native-pause check; queues Chore 39 via 0x23990 | candidate | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x23990 | Chore enqueue | Assigns future tick, serializes for transport | candidate | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x21EE0 / 0x1F7E0 | Pending-slot selector / phase-0 Chore invoker | Execute Chores when scheduled tick reached | candidate | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x87940 | `DLL_TriggerMPSave` | Manual MP save, Chore 39 flag 0 | confirmed export | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x876F0 / 0x26BE0 | `DLL_SetPath` / path store | MP autosave directory | confirmed export / candidate | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x876D0 | Camera rotation/centre request | Only records request and pending flag | confirmed export | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| 0x14C00 | Save/setup serialization | Transfers NoDogs setup field both ways | candidate | [MODSETTINGS_ACTIVATION](../MODSETTINGS_ACTIVATION.md) |
| 0x19D960 | PlayState producer | Option byte +0x7DF bit 8 = NoDogs; nickname subtypes slots 1..8 as int16 | candidate | [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md), [MODSETTINGS_ACTIVATION](../MODSETTINGS_ACTIVATION.md) |
| 0xCDE60 | Main simulation update | Increments tick only when native pause DWORD 0; calls 0x1D160, start troops 0x119050 (call 0xCE2C3), peace display 0xCA870 (gate 0xCE304) | structurally verified | [PLAYER_DEFEAT](../PLAYER_DEFEAT.md), [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| 0xD1AD0 | Checksum scheduler; contains public pre-tick boundary | Runs before building, player-result, AI, unit passes | candidate | [PLAYER_DEFEAT](../PLAYER_DEFEAT.md), [SURRENDER_SELECTION](../SURRENDER_SELECTION.md) |
| 0x1D160 | Win/loss writer | Normal runtime writer of `r_WinLossState`; periodic Lord ID/global validation | candidate | [PLAYER_DEFEAT](../PLAYER_DEFEAT.md) |
| 0x57330 | Player/Lord processing before AI scheduling | Checks paused/result fields | candidate | [PLAYER_DEFEAT](../PLAYER_DEFEAT.md) |
| 0x15A950 | Lord state machine | Terminal state 0x6E; counter 33 -> remove Lord, call 0xCD190 | confirmed-static | [PLAYER_DEFEAT](../PLAYER_DEFEAT.md) |
| 0xCD190 | Player removal | Buildings 0xC3F10/0xC4290; units AliveState 2->3 via 0xA1280; walls/tiles/masks 0x71E90/0x71E30/0x62EB0/0x516D0; no AI conversion | candidate name, effects confirmed | [LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md), [PLAYER_DEFEAT](../PLAYER_DEFEAT.md) |
| 0xC3F10 | Owned-building traversal | Calls 0xC4290 for every nonempty owned building incl. keep and granary (call 0xC3F71) | confirmed-static | [PLAYER_DEFEAT](../PLAYER_DEFEAT.md) |
| 0xC4290 | Building mark/replacement | Writes life state 3 (MarkedForDeletion) immediately; slot cleanup later via 0xC60F0 -> 0xB8310 | confirmed-static | [PLAYER_DEFEAT](../PLAYER_DEFEAT.md) |
| 0x22020 | Checksum builder | Every 200 ticks; 14 categories; units 1..9999 | candidate name, flow checked | [SURRENDER_SELECTION](../SURRENDER_SELECTION.md) |
| 0x78E0 | Per-unit hash | 0x404 bytes from +0x88, path plan, 3 scalars; masks +0x418 (8 bytes) | candidate | [SURRENDER_SELECTION](../SURRENDER_SELECTION.md) |
| 0x27960 / 0x26D30 / 0x27C50 | Aggregate compare (opcode 101) / per-unit compare (opcode 56) / resync (opcode 54) | | candidate | [SURRENDER_SELECTION](../SURRENDER_SELECTION.md) |
| 0x19A5D0 | Unit deselection | Clears `r_UnitSelected`, decrements selected count | candidate | [SURRENDER_SELECTION](../SURRENDER_SELECTION.md) |
| 0x1869C0 | Final unit deletion | Calls 0x19A5D0, zeroes record and path plan | candidate | [SURRENDER_SELECTION](../SURRENDER_SELECTION.md) |
| 0x89870 | Keep/map-slot lookup | Eight signed-byte permutation; returns one-based keep whose assignment equals playerId-1, or 0 | candidate | [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md), [COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md) |
| 0xCFE00 | Player-slot reassignment wrapper | Calls 0x198FB0 then 0xC6810 | candidate | [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md) |
| 0x198FB0 | Unit owner swap | Types 0x37/0x38/0x39 get state 0x6E instead of transfer | candidate | [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md) |
| 0xC6810 | Player-resource field swap | Fixed list incl. Keep/start fields; not Lord IDs +0x21F8/+0x21FC | structural | [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md) |
| 0xCEF00 | Periodic player initializer | Calls 0xC23C0 for players 1..8 | candidate | [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md) |
| 0xC23C0 | Lord spawn/validation | Nonzero `r_LordUnitId` skips spawn without validation | structural | [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md) |
| 0x17FEF0 | Unit spawn | (owner, colour, coords, height, type); owner -> +0x92, colour -> +0x0C | confirmed (spawn contract) | [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md), [NativeEventNotes](../../../../../../Findings/NativeEventNotes.md) |
| 0x196F80 | Chore reset for new unit | | candidate | [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md) |
| 0x103AA0 | Lord speech media wrapper | Reads original type from AIC +4; 1..29 per-Lord tables, else generic | candidate name, dataflow confirmed | [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md) |
| 0x103290 | Message enqueue | Immediate via 0xFEE50 or 10-entry queue (full -> dropped) | candidate name, instructions confirmed | [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md) |
| 0xFEE50 | Message presentation | Text, optional audio/video via emitter 0x1A2850 | candidate | [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md) |
| 0x1A2850 | Render-record emitter | Record kinds 20/18/22/21 | candidate | [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md) |
| 0xCA900 | Peace-time initializer | end = minutes*StartingGameSpeed*60 + start (0xCA904 imul, 0xCA91E *60) | confirmed-static | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| 0xCA870 | Peace-time display/expiry | OST_PEACETIMER; clears flag when tick > end; event/sound 0x123 | confirmed-static | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| 0xD1E50 | Alternative Freebuild/scenario timer update | Only for special mode/submode/enable combination (0xCE2DA-0xCE2F3) | static | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| 0x119050 | Starting-troop dispatcher | 2459 bytes; mode-1 peace exit at 0x119110 | confirmed-static | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| 0x1867A0 | Target exclusion predicate (533 bytes to 0x1869B5) | RCX unit manager, EDX attacker, R8D target, R9B filter; nonzero = exclude | static | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| 0x156DC0 | Lion/hyena update | Via table 0x321CB0; reads tribe ID 0x156DF9, herd flag 0x156E56 | candidate name | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| 0x188A20 / 0x18E9A0 / 0x187230 / 0x195170 | Target admission / retained-target evaluation / contact melee / extra contact path | Consumers of 0x1867A0 | candidate | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| 0x199110 | Unit damage | Subtracts HP, clamps 0, publishes hit/death; other callers 0x198ED0 (self/environment), 0x1ABF0 (Lord resolution) | candidate | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| 0x2AE40 | AI military scheduler | Reads peace flag regardless of mode | candidate | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| 0x84A20 -> 0xFFC70 | `DLL_IsMapperAvailable` -> mapper availability (889 bytes) | Manager 0x32C7730; entries +0x22C + mapper*2; dog-cage branch 0xFFD87 | confirmed export / candidate | [MODSETTINGS_ACTIVATION](../MODSETTINGS_ACTIVATION.md) |
| 0x85BD0 | `DLL_MapAction` | Editor moat actions 106/107 -> internal 45/44 | confirmed export | [EDITOR_REGION_REFRESH](../EDITOR_REGION_REFRESH.md) |
| 0x6FE90 | Generic terrain writer | Writes flags/height/moat; calls edge updater 0xD90D0 (at 0x7094D); does not set path dirty | candidate | [EDITOR_REGION_REFRESH](../EDITOR_REGION_REFRESH.md) |
| 0xE49D0 | Path-region rebuild | (pathContext*, int force) -> 0/1 | confirmed-static | [EDITOR_REGION_REFRESH](../EDITOR_REGION_REFRESH.md) |
| 0xE2610 | Region query | Equal nonzero PCLs succeed immediately | confirmed | [EDITOR_REGION_REFRESH](../EDITOR_REGION_REFRESH.md) |
| 0x8C5F0 | Cursor dispatcher | Called from RunTick after `preDLLCallActions` | candidate | [EDITOR_REGION_REFRESH](../EDITOR_REGION_REFRESH.md) |
| 0x1A49C0 / 0x1A49F0 | Game crash-handler registration / handler | | candidate | [UU_IMGUI_SHUTDOWN](../UU_IMGUI_SHUTDOWN.md) |
| 0x1E3734 -> 0x1E3544 | Process exit wrappers | | candidate | [UU_IMGUI_SHUTDOWN](../UU_IMGUI_SHUTDOWN.md) |

## Data and fields

| Record/manager | Offset | Meaning | Type/sign/unit | Confidence | Evidence |
|---|---|---|---|---|---|
| Global | 0x3665F58 | Current simulation tick | int, ticks | confirmed-static | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| Global | 0x38722DC | Peace time active flag (36 audited references) | int | confirmed-static | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| Global | 0x38722E0 / 0x38722E8 | Peace time end tick / start tick | int, ticks | confirmed-static | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| Global | 0x8574B90 | Native mode: 0 SP, 1 network MP, 99 skirmish/local Coop | int | confirmed-static | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| Global (Chore options) | 0x87ECA00 | `StartingGameSpeed` (MP default 40, SP start e.g. 300) | int | confirmed-static | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| Global | 0x366A090 / 0x366A094 | Coop trail (native 1..4) / mission (zero-based) from MP preinit | int | confirmed | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md) |
| Global | 0x8EBAA0 | Synchronized start countdown (2 after sync start) | int | confirmed-static | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md) |
| Global | 0x366A12C | Native tick recorded at synchronized start | int | confirmed-static | [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md) |
| Global | 0x366A0F8 | Native pause DWORD (Game_Paused) | DWORD | confirmed-static | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| Network-state instance | 0x8574320 | Base; setup fields at +0x279F98 (autosave) and +0x279FB8 (NoDogs) | struct | confirmed-static | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| Global | 0x87EE2B8 | MP autosave interval | DWORD, minutes | confirmed-static | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| Global | 0x87EE2D8 | NoDogs setup value (Extender `ChoreManagerOptionsInternal.NoDogs` offset 2596792) | int | confirmed-static | [MODSETTINGS_ACTIVATION](../MODSETTINGS_ACTIVATION.md) |
| Global | 0x37EF964 | NoDogs active value (copied at init) | int | confirmed-static | [MODSETTINGS_ACTIVATION](../MODSETTINGS_ACTIVATION.md) |
| Global | 0x87EE2F0 / 0x87EE2F4 | Advanced-options flag MP / skirmish (mode 99) | int | confirmed-static | [COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md) |
| Global | 0x87EE304 | Improved Spearmen rule | int | confirmed-static | [COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md) |
| Global | 0x8574C68 + playerID*4 | Nickname subtypes (9 ints incl. slot 0); save section 1101 | int32, 0..7 valid | confirmed-static | [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md) |
| PE table | 0x336B20 | Save-descriptor table; row 124: ptr 0x188574C68, skip 0, len 0x24, compressed, section 1101 | table | confirmed-static | [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md) |
| Global | 0x3A11E58 / 0x3A11E5C | Saved screenCentreX/Y | int | confirmed-static | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| Global | 0x38722A0 / 0x38722A4 | Saved realScreenCentreX/Y (distinct quantity) | int | confirmed-static | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| Global | 0x32C7730 | Mapper manager; availability shorts at +0x22C + mapper*2 | int16 signed | confirmed-static | [MODSETTINGS_ACTIVATION](../MODSETTINGS_ACTIVATION.md) |
| PE tables | 0x316450 / 0x318340 | Lord speech / video pointer tables, 30 slots per category, index category*30 + originalSpeechLord (slot 0 generic) | pointer table | confirmed-static | [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md) |
| AIC record | stride 0x5E4, +4 | Original lineup/speech type; generic slot playerID+29 | int | confirmed-static | [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md) |
| GamePlayerResources | 0x21F8 / 0x21FC | `r_LordUnitId` / `r_LordUnitGlobalId` | int | confirmed | [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md), [PLAYER_DEFEAT](../PLAYER_DEFEAT.md) |
| GamePlayerResources | 0x2210 / 0x2244 | `r_IsPaused` / `r_WinLossState` | int | confirmed | [PLAYER_DEFEAT](../PLAYER_DEFEAT.md) |
| Map file | section 2022 / 3015 | Nine 22,588-byte player records / 10,000 1,168-byte unit records (HD used 1022/1015) | file sections | confirmed (decompressed maps) | [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md) |
| Unit record (0x490 stride, one-based) | +0x88 | `r_AliveState`; start of hashed 0x404-byte interval | | confirmed | [SURRENDER_SELECTION](../SURRENDER_SELECTION.md) |
| Unit record | +0xAE | `r_UnitSelected` (hashed, not masked) | byte | confirmed | [SURRENDER_SELECTION](../SURRENDER_SELECTION.md) |
| Unit record | +0x418 | Eight bytes masked during checksum | | confirmed-static | [SURRENDER_SELECTION](../SURRENDER_SELECTION.md) |
| Unit record (API struct) | +0x8A / +0x92 / +0x94 / +0x0C | Type (`r_UnitChimp`) / owner (`r_ControllableForPlayerId`) / global ID / sprite colour | | confirmed | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md), [NativeEventNotes](../../../../../../Findings/NativeEventNotes.md) |
| Unit record (raw, manager + id*0x490, LastOrderedUnit at +0x65C) | +0x6E6 / +0x6EE / +0x984 | Type / owner byte / classification byte (low byte of unnamed API +0x328) | byte | confirmed | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| GameUnitManager | +0x24 | First `r_SelectedChimpsCount[playerId]` element (native `manager[playerId+9]`); Extender comment 0x28 is wrong | int | confirmed (Marshal.OffsetOf) | [SURRENDER_SELECTION](../SURRENDER_SELECTION.md) |
| Building manager | manager + ID*0x32C + 0x12E | Building type (API record starts at +0x5C, type +0xD2) | word | confirmed-static | [FIXES_COMPATIBILITY](../FIXES_COMPATIBILITY.md) |
| Global | 0x3A11EA4 | Active tile byte map used by special-building registration | byte | confirmed-static | [FIXES_COMPATIBILITY](../FIXES_COMPATIBILITY.md) |
| Path context | +0x6C / +0x74 | Component-grid dirty flag / rebuild generation | int | confirmed-static | [EDITOR_REGION_REFRESH](../EDITOR_REGION_REFRESH.md) |
| Global | 0x67E6424 | Building/render notification written by edge updater (not path dirty) | | static | [EDITOR_REGION_REFRESH](../EDITOR_REGION_REFRESH.md) |
| Tribe | +0x608 | Herd attack flag (lion update) | | static | [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md) |
| Save preamble | block 6, payload +8 | MP autosave interval at save time | DWORD | confirmed-static | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |
| Save file | after native payload | 4036-byte section directory incl. tag; word after tag = native payload size; Extender ZIP may follow | | confirmed-static | [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md) |

## Behavior

### Mission load and initializer routing

- Successful native initializers return low 32 bits 0xFFFFFFFF; campaign errors -11/-21, ordinary reader failure 1 and skirmish reader failure 0 are not success. A failed inner initializer vetoes an otherwise successful outer result. [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md))
- `DLL_LoadSaveGame` success is return 1 plus errorCode=1; it restores state without new-game startup. MP restoration also enters 0x94350 with the save byte set, so an OnStartMap callback does not imply a new game. [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md))
- Local saves, including local Coop, use `DLL_LoadSaveGame`; MP saves use `DLL_PreInitMap_Multiplayer` + `DLL_LoadMapToPlay` (save byte) -> 0x94350 save branch, which returns 0xFFFFFFFF after restoring, zero on invalid archive. [confirmed, static] ([SAVEGAME_MODSETTINGS_LOAD](../SAVEGAME_MODSETTINGS_LOAD.md))
- `LoadMapReturnData.siege_or_invasion`: 0 Siege, 1 Invasion, 2 Economy, 3 FreeBuild; `GameData.Init` converts to `Enums.GameModes` (numeric values differ). `coopMissionID`, `skirmishTrailLevel`, campaign `mission_level` stay zero-based; player IDs one-based. [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md))
- Coop: frontend selection 21..24 -> native trail 1..4; UI one-based mission is decremented once in `CoopMissionChanged`; StartGame/StartSkirmishGame pass it unchanged. Provider trail IDs are 0..3. Save retData, not the lobby mission, is authoritative for a loaded mission. The shared skirmish entry alone cannot distinguish local Coop from custom games; the preinit arguments can. [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md), [SAVEGAME_MODSETTINGS_LOAD](../SAVEGAME_MODSETTINGS_LOAD.md))
- Lower `LoadMapFile` exports without an interactive root create no mission (e.g. `SaveCustomTrailMap`). [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md))

### Managed completion, screens and retirement

- Interactive roots: EditorDirector creation/editor load/campaign/trail/custom trail/save, FRONT_StandaloneMission.StartMap, FRONT_Multiplayer.StartSkirmishGame/RestartSkirmishGame, HUD_Tutorial.StartTutorial, Platform_Multiplayer.StartGame/StartSave. Nested custom-trail/skirmish calls belong to one attempt. [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md))
- `postLoading` returns after GameData/GameMap/UI initialization; the enclosing root still performs player assignment and colour mapping. Clients waiting for the seed complete in the main-thread `processMessage` packet-2 path; simulation starts later (packet 4). [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md))
- `MainViewModel.GoToScreen` and `FatControler.NewScene` change actual screen state; Options bypass scene replacement. HUD win/lose presentation is not departure. `IsMapEditorMode` can survive departure. [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md), [EDITOR_MAP_LIFECYCLE](../EDITOR_MAP_LIFECYCLE.md))
- Normal exit: `MainViewModel.ButtonExit -> ExitApplication -> FatControler.ExitApp` (public instance void) `-> Application.Quit`; it reaches the same shutdown path as Alt+F4 WM_CLOSE. [confirmed, static] ([UU_IMGUI_SHUTDOWN](../UU_IMGUI_SHUTDOWN.md))

### First PlayState and simulation start

- `EngineInterface.run` calls `DLL_RunTick`, then copies a newly allocated PlayState (`CopyPlayStateStruct`); `Director.Update` consumes it only while not locally paused and simulation running; `GameMap.processTestMap` calls `GameData.setGameState` (replaces `lastGameState`) and then the GUI check. A GUI veto/exception does not undo the setter. [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md))
- `GameMap.createMap` resets buffer flags; `EditorDirector.loadSaveGame`/`postLoading` clears `lastGameState` to null. A managed Start is not proof that a first PlayState was published. [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md))
- Host `Platform_Multiplayer.monitorHostGameStart` may wait up to 60 s for human acknowledgements before `Director.startSimThread` and `StartMultiplayerGameSynced`. [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md))
- Local skirmish briefing opens from a post-update callback after the first consumed buffer and can pause further consumption. Local Coop does not schedule the briefing (only `!coopGame` schedules `ButtonGotoBriefing`). [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md), [COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md))
- `DLL_RunTick` decrements a positive start countdown before pause and MP chore gates; on the second entry it calls `DLL_GameAction(6,-1,-1,-1)`. An unchanged countdown 2 after sync start places a stall before that path. Managed `Director` counts run attempts, which can advance while native time does not. [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md))
- `EditorDirector.postLoading` resets zoom and requests forced rotation; `processTestMap` applies `SetCameraFromGameState`, finishes pending rotation and reapplies the camera before publishing `lastGameState`. `CameraControls2D.Update` can still apply `boundsFixCamera` on a later frame. [confirmed, static] ([SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md))

### Multiplayer handshake, saves and slot remapping

- Packets (Vanilla `processMessage`): 2 initial seed then deferred postLoading/ack, 3 host acknowledgement (not a modsettings fingerprint), 9 client old/new saved-slot mapping, 4 final mapping and synchronized start. [confirmed, static] ([LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md), [SAVEGAME_MODSETTINGS_LOAD](../SAVEGAME_MODSETTINGS_LOAD.md))
- `FRONT_Multiplayer.Update` returns early once `MPGameLoading`; before that, the ready branch calls `ReceiveGameMessages` (returns without reading channel 2 when no connected remote members) then `RefreshLobbyList` -> `receiveLobbyMessages`, then StartGame/StartSave. [confirmed, static] ([LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md))
- `HostLoadGame` clears remapping indices 1..8 to -1 and writes the filename to Steam lobby data; Update resolves it and calls `StartSave`; missing filenames keep Vanilla's error path. Host waits only for actual remote human gameMembers. [confirmed, static] ([LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md))
- `StartSave` registers humans in sequential slots 1..N, loads with save byte, records saved local player ID; AI descriptors are restored separately. Final remap happens later: host in `monitorHostGameStart`, client in packet-4 branch, both before `startSimThread`, via `remapMPGameMembers -> EngineInterface.RemapPlayers`. Occupied human destinations keep their loaded Lord identities; managed-ready roster [1,2] can still become [1,8]. [confirmed, static] ([MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md))
- `DLL_RemapPlayers`: every slot absent from the human-presence map and without saved AI registration enters 0xCD190 (deferred removal of buildings, units, walls; no AI conversion). Vanilla therefore does not reject a save with fewer humans than saved, including a solo host. [confirmed, static] ([LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md))
- Save CRC exchange: `SendSaveCRCs(bool coop)` resets every MP header's counter and sends only one category (coopTrailID>0 vs <=0) on channel 21 (filename + checksum XOR save time); clients reply after matching; rows show when `retrieveCRCChecks == CountHumanPlayers()-1` (zero remote humans needs zero). Two category calls are not combined. `GetMPSaves(false)` returns all MP saves. `StartSave` derives Coop refresh from `map.coopTrailID>0`. Not a full save-content validation. [confirmed, static] ([LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md))
- Whole-file equality between peers is not established; Vanilla matches names plus checksum/time. [confirmed, static] ([SAVEGAME_MODSETTINGS_LOAD](../SAVEGAME_MODSETTINGS_LOAD.md))

### Save writing

- Routes: export 0x86F80 (ordinary save/map export), Chore 39 handler 0x13030 (synchronized MP save: phase 1 serializes coordinates, autosave flag, wide filename; phase 0 picks `autosave.msv`, `autosave%d.msv` or supplied name and calls 0x52B0), export 0x816B0 (trail template). The third writer argument 0 does not classify gameplay saves. [confirmed, static] ([SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md))
- 0x13030 entry: `mov [rsp+8],rbx` (5) + `push rdi` (1); 0x13036 `mov eax,0x1D40` then chkstk 0x1A6990; no branch targets the interior. [confirmed, disassembly] ([SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md))
- Writer ignores WriteFile (CRT chain 0x1DDB94 -> 0x1DDC2C -> 0x1DDD4C) and close results after open and returns 1 even for short files; reaching 0x5A4C is not proof of a complete file. Chore 39 discards the writer result and advances the handshake. [confirmed, static] ([SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md))
- Autosave: `DLL_ApplyMultiplayerSetupData` stores the interval; 0x1F6E0 (called by RunTick before the managed pause branch, no native-pause check) compares unsigned `timeGetTime()` elapsed ms with interval*60,000, restamps and queues Chore 39 flag 1 (manual `DLL_TriggerMPSave` flag 0). The saving notification is shown on Chore send/receive, before phase 0. While paused (DWORD 0x366A0F8 set) the tick does not advance, so an announced save stays queued until after unpause. [confirmed, static] ([SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md))
- Managed `EngineInterface.SaveSaveGame` calls `Director.SafeToSave(wait:true)` (up to 100 x 10 ms) and returns false without opening the file if it gives up; false also covers native failure. [confirmed, static] ([SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md))
- Managed `SaveSaveGameOrMap` passes Screen.width/64/zoom, Screen.height/16/zoom to `getScreenXYForSaveCentring` (quantized 3-unit, 32/16-px steps); `getRotationCentre` uses `CalcMapTileFromMousePos`. 0x86F80 stores screen centres separately from real screen centres; 0x876D0 return does not prove an applied camera. [confirmed, static] ([SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md))
- `MultiplayerSetupData.FromString`: first field is negative version or (legacy) fairness; starting speed and autosave are +1 and +6 fields after fairness; `ToString` writes a version prefix. Setup dialog: `MPTEMPsetupData` from `MPsetupData.ToString()`; with coopGame && !singlePlayerCoop `Settings_Autosave` cycles `MPsetupData` directly but `ApplySettings` reimports TEMP (old interval); Coop options use `CloseCoopOptions` -> `UpdateHostInfo`. [confirmed, static] ([SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md))

### Save metadata, restart data and missing maps

- `EditorDirector.loadSaveGame` clears restart state, calls 0x853F0, then reads the header with `loadRestartInfo=true`. `GetFileInfoFromFileName` routes restart versions 50..99 to `RestartSkirmishMapInfo.decode`, which discards the whole decoded object (two null paths) if the original map header is unavailable (except customTrailMission callers); custom trails try the Maker map first and can throw KeyNotFoundException. [confirmed, static] ([SAVEGAME_MISSING_MAP_METADATA](../SAVEGAME_MISSING_MAP_METADATA.md))
- Original map source byte: 0 user, 1 built-in, 2 Workshop; `GetHeaderFromFileNameForSkirmishRestart` searches only that catalog, case-insensitive. Without restart data, `getAIFace` falls back to GameSprites[738] and `getComputerName` returns empty. User-confirmed: installing the original Workshop map repairs both. `encode` dereferences selectedHeader three times; `RestartSkirmishGame` loads `selectedMPHeader.filePath`; `doOpen` clears `selectedMPHeader`. [confirmed, static + user-confirmed] ([SAVEGAME_MISSING_MAP_METADATA](../SAVEGAME_MISSING_MAP_METADATA.md))
- Trail Maker `FRONT_ManageTrail.DoImport` -> Init -> `SelectMission` dereferences `selectedHeader.display_filename` without null check (IL 0x01CB..0x01DB); Maker Load reads folderType 3, Custom Trail contract is folderType 4; `GetFileInfoFromFileName` selects map vs campaign parsing by `.map` extension; `populateMapList` selects headers by reference identity. [confirmed, static] ([SAVEGAME_MISSING_MAP_METADATA](../SAVEGAME_MISSING_MAP_METADATA.md))
- `RestartMPInfo` encodes `LordNames` and `LordType`; the MP writer only updates names, so the type array is not a reliable AIC identity. `StartGame` creates one RestartMPInfo; ordinary MP (coopTrailID 0) copies lobby AIV names/images, Coop skips it; both encode before native preinit. [confirmed, static] ([SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md), [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md))
- `CustomLordConfig.encode/decode` drops source path and workshop flag. `MPAIVInfo.Init` sets lordName; standard Lords use empty names; `LordUser` sets builtInLord=false without a name. Empty name + builtInLord=false + valid standard type is legitimate. [confirmed, static] ([TRAIL_CUSTOM_AIC_EXPORT](../TRAIL_CUSTOM_AIC_EXPORT.md))

### Save requester and confirmation popups

- `HUD_LoadSaveRequester.OpenLoadSaveRequester` opens the visible instance; `ButtonClicked(1, ...)` hides all three surfaces before `RunOKAction` invokes OKAction; private `RunOKAction` then updates remembered names and `GameData.currentFileName`. Editor overwrite defers RunOKAction to its popup; `CloseRequester` invokes CancelAction. [confirmed, static] ([SAVEGAME_MODSETTINGS_LOAD](../SAVEGAME_MODSETTINGS_LOAD.md))
- `HUD_ConfirmationPopup.ShowConfirmation`: MPConf=false -> `Show_HUD_Confirmation`, true -> `Show_HUD_ConfirmationMP` (the overlay bound in FRONT_Multiplayer.xaml). `ShowConfirmationOKMessage` enables only the ordinary overlay (fourth optional arg is Sands, not MPConf). `ConfirmationClicked` clears visibility before running the action. [confirmed, static] ([TRAIL_CUSTOM_AIC_EXPORT](../TRAIL_CUSTOM_AIC_EXPORT.md), [LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md), [TRAIL_IMPORT_DELETION](../TRAIL_IMPORT_DELETION.md))

### Lobby frontend

- Load button: `RefLoadButton.IsEnabled = (flag5 && !flag6) | skirmishGame` (IL 0x17D8, line 7686); flag5 = member-count/capacity, local and all-human ready; flag6 = `CountAIPlayers()>0`. A host with AI cannot load; one human alone fails the member-count gate. Coop-trail page buttons (7687-7690) use flag4 plus local-Coop/invite exceptions; all issue `ButtonClicked("Load")`, where coopGame selects Coop requester and `SendSaveCRCs(true)`. [confirmed, static] ([LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md))
- `ShowLobbyScreen` hides `RefLoadButton`; `ShowSetupScreen` restores it only for MP host, not skirmishGame; no other writer restores it. `FatControler.Update` calls `FRONTMultiplayer.Update` while `Show_MultiplayerSetup`. [confirmed, static] ([LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md))
- `customCoopGame` (public static) is set for MPGameType 2/3 and lobby type "1"; reset only in `doOpen` fromNew block and `ShowLobbyScreen`; `ShowSetupScreen` with type "0" does not clear it. `UpdateHostInfo` then publishes type=1/maxPlayers=2 whenever it is true and a header is selected. [confirmed, static] ([LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md))
- Standard lobby: host `coopTrailID=-1`, Steam `cooptrail=-1`; client keeps default 0 because `coopTrailGameSetup("-1")` returns before assigning. Host `MPLobby.AIPlayers` is never written (Steam initial `aiplayers=0`; later `CountAIPlayers()` = members with ID < 1000); clients read the advertised value. Live roster converges via `setHostMemberOrder`. `ButtonClicked("Play")` publishes setup, then `HostStartGame` publishes `GO!` with the AIV checksum. [confirmed, static] ([COOP_PACKAGE_MAP_READINESS](../COOP_PACKAGE_MAP_READINESS.md))
- Map validity: client checks `MPMapChecked`/`MPLastMapName`, calls `GetHeaderFromFileNameMP(name, crc)` over BuiltInMP/WorkshopMP/UserMP only; mismatch leaves `MPMapValid=false` and clears unlocked readiness. `ButtonClicked("Ready")` acts only when `MPMapValid || isHost`, silently otherwise. Header CRC is uint, lobby query int. `GetFileInfoFromFileName` does not register headers; returned `fileName` is the basename. [confirmed, static] ([COOP_PACKAGE_MAP_READINESS](../COOP_PACKAGE_MAP_READINESS.md))
- Chat: `HandleIncomingMessage` decodes UTF-8; `ChatHandle` resolves name and colour ID (-1 unknown). `receivedLobbyChat` skips muted ordinary messages (systemMessage bypasses), truncates to 300 chars, strips LF, keeps 30 entries; `addSystemLobbyChat` trims at 100. `refreshLobbyChat` redraws all five displays, rebuilds inline rows (shield 14x14, name, LineBreak, message font 12, NewLine) and advances its timestamp by 500 ms. [confirmed, static] ([LOBBY_CHAT_LAYOUT](../LOBBY_CHAT_LAYOUT.md), [COOP_PACKAGE_MAP_READINESS](../COOP_PACKAGE_MAP_READINESS.md))

### Coop Trails and Trail Maker

- `CoopMissionChanged(int,int,bool)` (zero-based Trail, one-based mission) builds roster/setup, places AI partner in slot 2 with full keep order (honouring `coopOrderSwapped`), forces co-op team; does not set `teamShield`. `PopulateTeamsPanel` resets/assigns shields; `CreateTeamShields` returns while coopGame. `getKeepTeamShield(zeroBasedKeepIndex)` feeds the radar. [confirmed, static] ([COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md))
- `updateSteamIDMappings` removes absent identities, then `add_player_to_keep_locations` assigns a random free keep (`System.Random.Next`) to players without one. [confirmed, static] ([COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md))
- `UpdateHostInfo(bool)` submits setup immediately even with delayed=true. Speed slider handler ignores changes while `!panelLoaded || !panelActive`; `doOpen` keeps panelActive false during setup. `ShowSetupScreen` does not project setup into captions; `SetupSkirmishModeSettings` and `PopulateMapDetailsPanel` (overwrites `StandaloneMissionText`) do. Online Customize: `ApplySettings` forces peace minutes 0 if `Show_MPPeacetime` is false. [confirmed, static] ([COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md))
- Advanced options: Trail Maker writes `advanced_skirmish_options`, MP dialog `advanced_options`; 0x26370 copies rules if either flag is set, else clears Improved Spearmen. Spearman running (branch 0x143BD9 in 0x143400) and projectile damage 0x192750 read 0x87EE2F4 in mode 99 and 0x87EE2F0 otherwise. [confirmed, static] ([COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md))
- Frontend: `Skirmish` main-menu command shows `Show_Frontend_Skirmish` (FrontendMenus.xaml Grid line 2632) containing `CoopMenu_Button` (`Coops`); `Coop` selects page 21 if `SteamUser.BLoggedOn()`. [confirmed, static] ([COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md))
- Local Coop restart: `importMembers` serializes -1 only for `SkirmishHumanMember`; Steam humans get `(int)SteamID`. `MPLobby(RestartSkirmishMapInfo)` does not restore coopTrailGame/ID/selected mission; `StartSkirmishGame` treats `SkirmishHumanMember || (coopGame && mappedPlayerId == 1)` as human. `CurrentSelectedTrail` public static, `selectedCoopMissionID` private, lobby `coopSelectedMission` one-based. [confirmed, static + runtime] ([COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md))
- Trail import list: `ButtonClicked("Import")` fills `importRows` from `GetCustomTrails(true)` with exact name in Text1; selection handler never disables on null; `GetCustomTrails(true)` filters `workshop`, not `workshopUploadInfoAvailable`; `RescanCustomTrailsFolder` rebuilds local entries without FullPath; Vanilla scans direct child folders and top-level `*.trail`; backup checkbox backs up TrailMaker, not CustomTrails. [confirmed, static] ([TRAIL_IMPORT_DELETION](../TRAIL_IMPORT_DELETION.md))
- `BuildExtendedLordDirectory` assigns types by directory name (`ConfigSettings.extendedLordPaths` zero-based); Workshop scan appends standard configs to the same collection (names/types/checksums can coincide). `BuildCustomLordDirectory` admits Lords with nonempty AIV and config lists; info.json/lordmeta.json are not admission conditions; `GetCustomLords(bool)` is public. [confirmed, static] ([TRAIL_CUSTOM_AIC_EXPORT](../TRAIL_CUSTOM_AIC_EXPORT.md), [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md))

### Player slots and Lord spawn

- 0x94350 maps each occupied lobby player to its map slot via 0x89870 and calls 0xCFE00(old slot, new slot); the remap accepts human and AI representations (not AI-specific). 0x89870 also supplies keep coordinates for fixed/automatic AIV selection and the no-fit fallback. [candidate roles, static] ([LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md), [COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md))
- 0xC6810 swaps Keep/start fields but not Lord IDs; 0xC23C0 trusts any nonzero `r_LordUnitId` and skips spawn. When zero, it spawns type 0x37 at the Keep position via 0x17FEF0, writes unit/global IDs, resets chores (0x196F80) and advances NeedsInit -> active. [structural, static] ([LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md))
- Vanilla defect: built-in map *The Ford Across the River* stores stale Lord identities in unused player records 3..8 (records 7/8 = 4/517 and 8/534; referenced unit slots all zero). Remap into those records leaves tombstones; player gets no Lord. Bow Ridge, Inches Apart, Its just not fair, Lionheart, Crater Lake serialize 0/0 and are unaffected. Reproduced in CustomGame, CustomTrail and two-peer MP (player 8 tombstone owner 0, CHIMP_TYPE_NULL, AliveState None, global 0, health 0). [confirmed, runtime + map decompression] ([LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md))

### Defeat, elimination and surrender

- 0x1D160 is the normal runtime `r_WinLossState` writer and periodically validates Lord ID/global identity; it does not treat the start of the death animation as invalid identity. Save loading writes the field separately (baseline, not a transition). [confirmed, static] ([PLAYER_DEFEAT](../PLAYER_DEFEAT.md))
- Lord terminal state 0x6E: counter reaches 33, then the same invocation marks the Lord for removal and calls 0xCD190 -> 0xC3F10 -> 0xC4290, which immediately writes building life state 3 for all owned buildings incl. keep/granary (calls at 0xCD1AD and 0xC3F71). No later pre-tick intervenes. A script writing WinLoss.Loss alone does not run this cleanup. Raw IsAIPlayer is not an alive-player predicate. [confirmed, static/disassembly] ([PLAYER_DEFEAT](../PLAYER_DEFEAT.md))
- Checksum: every 200 map ticks 0x22020 aggregates 14 categories; bucket 1 hashes units 1..9999 (0x404 bytes from +0x88, 1000-byte packed path plan, 3 scalars, +0x418 masked). `r_UnitSelected` (+0xAE) is hashed. Mismatch: opcode 101, per-unit detail opcode 56 (first offending unit), resync opcode 54. [confirmed, static; other categories not exhaustively attributed] ([SURRENDER_SELECTION](../SURRENDER_SELECTION.md))
- Surrender desync cause: the surrendering Lord's `r_UnitSelected` differed between peers (host/client 0/2 and 1/0 in two traces, Vanilla detail opcode named that unit). Strong sufficient explanation, not proof of no other differing input. [probable, runtime] ([SURRENDER_SELECTION](../SURRENDER_SELECTION.md))
- 0x19A5D0 clears selection and decrements `r_SelectedChimpsCount[playerId]`; 0x1869C0 calls it before zeroing the record. Extender `KillUnit` instead sets HP 1 and applies max melee damage, so a dead Lord can stay in its slot through the next checksum. [confirmed, static] ([SURRENDER_SELECTION](../SURRENDER_SELECTION.md))

### Peace time

- 0xCA900 stores start = current tick and end = minutes * `StartingGameSpeed` (0x87ECA00, `imul edx,[0x87ECA00]` at 0xCA904, bytes `0F AF 15 F5 20 72 08`; no direct branch/call target inside 0xCA905-0xCA90A) * 60 (0xCA91E) + start: 0x960 ticks/min at MP speed 40, 18,000 at SP speed 300. 0x100530 uses fixed 0x960. [confirmed, disassembly] ([VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md))
- 0xCA870 reads start/end/current at 0xCA87D/0xCA889/0xCA89C, publishes OST_PEACETIMER, clears the flag when tick > end, removes the OST and emits event/sound 0x123. Its only caller (0xCE309) is skipped by `je` 0xCE304 (`74 08`) only in mode 0 (mode read 0xCE2FD). [confirmed, disassembly] ([VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md))
- Active-flag references (36): 15D63, 15FD4, 22561, 2AEA4, 2AF1C, 88234, 8A14A, 8D368, 8D7C6, 8E04C, 8E0E0, 8E601, 8EA43, 8EC88, ABC9D, C70BC, CA874, CA8CD, CA917, CA92C, CDD6F, D1E95, D1F42, D2935, 10073D, 100820, 105534, 119110, 124B98, 1256A7, 1259D7, 156E8D, 17F3CE, 1868A0, 186964, 186991. Mode-independent (14): 15D63, 15FD4, 22561, 2AEA4, 2AF1C, CA874, CA8CD, CA917, CA92C, D1E95, D1F42, D2935, 10073D, 100820 (save/load 0x15C60, checksum, AI scheduler 0x2AE40, timer). [confirmed, static] ([VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md))
- The 22 gameplay consumers have mode-0/99 bypasses in 0x89F40, 0x8C5F0, 0xABB90, 0xC70B0, 0xCDB20, 0x105510, 0x124B90, 0x1256A0, 0x1259D0, 0x156DC0, 0x17F2F0, 0x1867A0 and 0x119050: Vanilla peace time protects only network MP (mode 1). [confirmed, static] ([VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md))
- Mode-bypass instruction sites (used by the patch contract): NOP sites 8A148, 8D366, 8D7C4, 8E05E, 8E0DE, 8E5FF, 8EA59, 8EC9E, C70BA, 186976, 18697A; conditional selections 8E062, 8EA5D, 8ECA2; MP-network-flag skips ABC88/ABC8E -> ABC9D, 105520/105525 -> 105534; taken exits CDD85 -> CDDCC, 124BB4 -> 124E56, 1256C3 -> 1259A7, 1259F3 -> 125CEB, 156EAB -> 156F4A, 17F3E4 -> 17F40D, 1868B6 -> 18688E; peace-only mode read 18699A (`8B 05 F0 E1 3E 08`); 1869A3 and shared JNE 1869A7 (also reached from 1868DE after classification compare 1868D6). Body spans used by patches: 0x1868B2..0x1868B8, 0x186976..0x18697C, 0x18699A..0x1869A0 (no interior targets in the six-byte mode read). [confirmed, disassembly] ([VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md))
- Earlier claim that 1869A7 is peace-only was corrected (2026-10-07): it is shared with the lion/hyena classification path; making it JMP excludes valid wildlife-vs-troop contacts even without peace. [confirmed, static] ([VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md))
- Starting troops: dispatcher 0x119050 (called at 0xCE2C3 before 0xCA870) exits in mode 1 while the flag is set (0x119110), so it stays blocked on the expiry tick and resumes next tick. Entry 0x119050-0x119060: six instructions `40 53 56 57 41 54 48 83 EC 68 8B 05 C8 CE 54 03` (lengths 2,1,1,2,4,6); no interior branch target 0x119051-0x11905F. [confirmed, disassembly] ([VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md))
- Predicate entry 0x1867A0: `48 89 5C 24 08 / 48 63 C2 / 48 8B D9 / 4C 69 D8 90 04 00 00` (5,3,3,7), continuation 0x1867B2 `4C 03 D9`; no interior targets 0x1867A1..0x1867B1; the first two instructions end at 0x1867A8, the next complete instruction at 0x1867B4. In older image 1e6d4c2e the predicate started at 0x186700; in FBCB9319 that address lies in a jump table. Callers pass mode 0 at 0x18795C (melee) and 0x188ACE (candidate validation), mode 1 at 0x18EBFF (ranged retained target); Hunter branch bypasses the call. 0x195170 revalidates via 0x188A20 before damage 0x199110. [confirmed, disassembly] ([VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md))
- Wildlife: lion and hyena entries in table 0x321CB0 both resolve to 0x156DC0; species IDs lion 45, hyena 87, crocodile 88. Armed herd target range 200; unarmed 25 after 10 counter increments, 50 after 3000. An Extender-spawned lone lion lacks herd state. [confirmed, static + in-game] ([VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md))
- 0xD1E50 (special mode/submode combination) is not a substitute for 0xCA870 in SP custom games. [confirmed, static] ([VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md))

### No Dogs mapper gate

- 0xFFC70 dog-cage branch (`MAPPER_DOG_CAGE` 0x138): 0xFFD87 compare mapper; 0xFFD8F/0xFFD92 mode 0 bypasses; 0xFFD94 compare 99; 0xFFD98 `74 31` mode 99 bypasses to 0xFFDCB; 0xFFD9A compare active NoDogs 0x37EF964; 0xFFDA1 zero continues; 0xFFDA3 nonzero returns unavailable. So Vanilla NoDogs applies only in mode 1. [confirmed, disassembly] ([MODSETTINGS_ACTIVATION](../MODSETTINGS_ACTIVATION.md))
- Callers of 0xFFC70: export 0x84A20, 0xC2030 (mappers 60/61, not dog branch), producer 0x19D960. 0xD6B40 indexed reference is a table access, not a NoDogs read. Setup flow: `MultiplayerSetupData.no_dogs` -> `setMPSetup` -> `setMultiplayerStartingData` -> 0x81050 -> 0x26370. [confirmed, static] ([MODSETTINGS_ACTIVATION](../MODSETTINGS_ACTIVATION.md))

### Lord identity, names and media

- `MPAIVInfo` keeps `lordConfig` separate from `lordName`; user AIC sets builtInLord=false without requiring a name; `setCustomLordConfig` imports it in both start paths. The configuration's `lordType` names the Extended Lord; the lobby slot's `lordType` may describe a different role. [confirmed, static] ([LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md), [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md))
- 0x103AA0: original types 1..29 -> per-Lord tables; larger -> generic player-qualified media; out-of-range or nonpositive -> nothing. Each active branch first enqueues a preliminary category-0 announcement (empty video; Wazir `general_message33.wav`), then the speech. Built-in text: `TEXT_SKIRMISH_SPEECH` index (originalSpeechLord-1)*34 + category, categories 1..33. [confirmed, static] ([LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md))
- Allied messenger media: Rat, Snake, Pig, Wolf goods request = `all_req_01.wav` + `bad_soldier_taunt.webm`; generic allied categories use the `all_*` family with `bad_soldier_taunt.webm`/`bad_soldier_nevous.webm`; Rat and Pig have own DieAlly media; Wolf KillNpc is generic but not allied. Taunt1: Rat `rt_taunt_01.wav`/`rt_taunt2.webm`, Snake `sn_taunt_01.wav`/`sn_taunt1.webm`, Pig `pg_taunt_03.wav`/`pg_taunt1.webm`, Wolf `wf_taunt_01.wav`/`wf_taunt1.webm`. All 29x33 slots have nonempty references. [confirmed, static] ([LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md))
- Presentation group: 0xFEE50 emits kinds 20 (text; section/index in shorts [11]/[12]), 18 (speech channel 1, gain 100), 22 (controls), 21 (video); shorts [13]/[14] = audio/video present (not speaker identity). `processTestMap` case 21: [3]<0 stop, 0 start, >0 loop; [11]>0 wait for speech. One `speechFileName`/`binkFileName` per PlayState. `OnScreenText` reuses one OST object per slot. [confirmed, static] ([LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md))
- Names: save stores nickname subtypes, not strings. `OnScreenText.getComputerName(oneBasedType, zeroBasedSubtype)` uses `GetCustomLordText(name, 34+subtype)` or `Translate.lookUpText(TEXT_CUSTOMISATION, 23+subtype)`. `MPLobbyMember.GetName` takes zero-based type; cases 29..37 resolve only the suffix (`SK_X1 - 1`). [confirmed, static] ([LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md))

### Editor

- `DLL_PreInitMap_Editor` initializes a blank map; `newMapEditor` passes siegeThat=false, increments mapRotation; `createNewMap` initializes GameData/GameMap/UI/simulation and player 1. `EngineInterface.LoadMapFile` calls the editor preinit with (160,0,false,false), then 0x853F0; `loadMapIntoEditor` continues only for errorCode 1. Extender OnStartMap targets are not reachable from the editor exports; OnLoadSave Post fires before managed editor init. `InitNewScene(MapEditor)` sets `IsMapEditorMode` and goes to ActualMainGame. [confirmed, static] ([EDITOR_MAP_LIFECYCLE](../EDITOR_MAP_LIFECYCLE.md))
- Moat: input handler 0x90CD0 uses width 3 (5 with Shift), queues Chore 21 (property 0x40000000, op 2 place / 3 remove); handler 0x111D0 calls 0x6FE90; landscape dispatcher 0x92FF0 uses the same handler. Terrain clearing 0x7AE50 and the 3x3 updater 0xD90D0 do not set path dirty; vegetation 0x6E2C0, editor deletion vegetation branch 0x62AB0, digging 0x639C0, filling 0x69470, building placement 0x6D580 do. Height tools 0x6FA10, 0x70DF0, 0x6C0F0, 0x64700 have separate guards (not asserted to fail). [confirmed, static] ([EDITOR_REGION_REFRESH](../EDITOR_REGION_REFRESH.md))
- 0xE49D0: force!=0 sets dirty=1 and resets countdown; force=0 can skip on countdown or dirty=0; a completed flood increments +0x74, clears dirty, republishes UInt16 PCL grid and component counts. Hence unmarked water/moat edits stay stale until something else sets dirty (e.g. wall placement). [confirmed, static + fixture] ([EDITOR_REGION_REFRESH](../EDITOR_REGION_REFRESH.md))
- `EditorDirector.preDLLCallActions(ref int, ref int)` runs once inside `EngineInterface.run`'s threadLock before `DLL_RunTick`, also while paused. `EditorDirector.Update` reads physical mouse; held painting and release placement happen in its preparation; the native Chore can complete later. `KeyManager.Update` filters Noesis capture before Extender key events. [confirmed, static] ([EDITOR_REGION_REFRESH](../EDITOR_REGION_REFRESH.md))

### Related Vanilla facts from external-component reviews

- Bad Things 0x41280 (via AIV path 0x51790/0x52270) gates mappers 176/177/301..311 on popularity 5000 (displayed 50, scale x100) and attempt count 100. [static] ([FIXES_COMPATIBILITY](../FIXES_COMPATIBILITY.md))
- Siege: 0x2B080 handles up to 8 machines (IDs 39/40/58/59/60/77 -> crew 2/3/4/4/1/2, mappers 190/191/192/193/194/358 via tables 2C80B0/2C80C8/2C8098), detaches crew then calls 0x2C1B0 (engineer filter: alive 2, owner, type 30, GameUnit+29C low word 0, role +426==10, tribe +2D4==0, AIstate +2BC!=8). 0x1134D0 searches tent sites with 0xEA760 (depth 100 then 200, filter 60; masks 4A5014B1/4A7014B1; Y table 2D2E54 stride 8 at EA9A3/EAAA0; signed Int16 metric at 5759230 + selector*0x320800*2, selector via 2EA70DC+player*177BC, not PCL; sites tested after depth > 4; height range < 12; no RNG); then 0x6D580 and 0x11E960 links the tent. [candidate roles, static] ([FIXES_COMPATIBILITY](../FIXES_COMPATIBILITY.md))
- 0x107160 allows vegetation state > 4 except 15 (manager+id*0x9C+0x6A). PCL count load 0x10F1B3 in 0x10F150 (callers 0x10C1C0, 0x10D9F0, 0x1102D0) stored at 0x10F1BA to global 0x2E9CA14; 0x10DCB0 reached via 0x40180 -> 0x114680; 0x114760 refs are unwind data only. [static] ([FIXES_COMPATIBILITY](../FIXES_COMPATIBILITY.md))
- Call site 0x2EFCB stores EAX to [RSP+0x20] before order 15; native args 5/6 are at +0x20/+0x28 there. Order 15 tent branch stores building global into Engineer+0x6F8 and reuses param 5 for crew. [confirmed, disassembly] ([FIXES_COMPATIBILITY](../FIXES_COMPATIBILITY.md))
- Special-building registration 0xD0630..0xD097B (from construction 0xB47E0, gate assembly 0x74DA0; replacement 0xC4290): args manager, building ID, player, category; checks coordinates <= 799 and active tile byte 0x3A11EA4 (CMP 9 + JZ 2 + IMUL 7 at 0xD08E8..0xD08FA, failure branch 0xD095F); success writes ID, coordinates, packed tile, region. [confirmed, disassembly] ([FIXES_COMPATIBILITY](../FIXES_COMPATIBILITY.md))

- Known foreign patch sites on Vanilla code (ownership and conditions in [../mods/session.md#external-components](../mods/session.md#external-components)): Extender ranged hook 0x18EB20; Fixes tunnel target 0x187D69, PCL count 0x10F1B3, ladder hooks 0x111C00 and 0x6A5D0, expanded defensive positions 0x53D00, farmland [0x58B12,0x58B2E) (our WoodSiteGuard [0x58B26,0x58B2E)), farmer 0xB7C39 (our gatehouse checks own 0xB7C32..0xB7C38), 0x55D43, 0x2BC40, 0x2FE20, 0x3E850, 0x10E26E, 0x11F0E5, 0x11BB38, trade post 0xD08E8. Separate nearby sites: APIShared prices 0xCEB10/0xCEB90, AI sells 0x3F1F3/0x29723. [static] ([FIXES_COMPATIBILITY](../FIXES_COMPATIBILITY.md), [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md))

### Scenario events and timeline (secondary source)

- `GameTimeManagerAPI.OnTick` runs in a native pre-tick before time/date processing; mutating timeline vectors there crashed map start. TimerEngine actions created in OnStartMap(Post) can be removed by the later `ReadTimerDataFromArchive`. [runtime] ([NativeEventNotes](../../../../../../Findings/NativeEventNotes.md))
- FreeBuild events: wildlife handler 0x11E150 (action 222 rabbits 14..21, limit 160, 1200-tick state; action 221 lions, activation 0x10000), predicate 0x1177A0, spawner 0x123AC0; case 148 at 0x104E13 reads signpost X/Y at slot +0x40/+0x44 (stride 0x10); case 146 writes local-player event status 16 at 0x104CBA; message handler 0x1031B0; marriage UI SFX 207 via 0x2940 (-> 0x1A4190, position 64, volume 100) at 0x104A53 and timeline 0xFA771. Farm-event prerequisite 0xB8D50; effects 0xC3130, 0xC2E30, 0xC2C30, mad cattle 0x194C40 + 0xC6090, granary theft 0xC5F70. Tribe command dispatcher 0x11E910; command 36 validates targets with unit stride at 0x11F106. [static/runtime, secondary] ([NativeEventNotes](../../../../../../Findings/NativeEventNotes.md))

## Cross-topic links

- Chore opcodes, checksum categories, desync and resync: [network.md](network.md).
- Unit records, damage, Lord unit states, siege crews (`SIEGE_ENGINEER_CREW_TIMEOUT.md`): [units.md](units.md).
- Path regions/PCL and the 0xE49D0/0xE2610 rebuild/query: [pathing.md](pathing.md).
- Notification queue and presentation lifecycle (`NOTIFICATION_LAST_FRAME.md`): [presentation.md](presentation.md).
- AIV lobby selection and AI military scheduling: [ai.md](ai.md).
- Manager globals and Extender interop offsets: [memory.md](memory.md).

## Open questions and contradictions

- Initializer names 0x87C40/0x8AE80/0x94350 and reset/unload/reader roles remain candidate-level.
- Surrender checksum: path plan and three extra scalars were not captured; other checksum categories and all resync triggers are not attributed.
- Exact lost handshake packet in the failed repeated MP load is unknown (loss window is in the Extender channel-2 drain, see mods file).
- 2026-11-20 host stall at native tick 1, countdown 2: root cause not established in the audit.
- Peace time: runtime validation of countdown expiry, damage/targeting, AI military resumption and start-troop cadence in modes 0, 1, 99 is still open; no conclusion transfers to another hash.
- Natural-gameplay consequences of Fixes' distanced-siege direction guard and PCL flow were not demonstrated.
- Extender comment says selected-count array at +0x28; installed assembly says +0x24 (assembly is authoritative).

## Touched by our mods

- APIShared: mission lifecycle, PlayState readiness, defeat observation, save metadata/requester ownership, lobby map/readiness, Lord media and messages, wildlife target entry 0x1867A0, UU-ImGUI shutdown. See [../mods/session.md#apishared](../mods/session.md#apishared).
- BugfixesAndQoL: peace-time mode gates and 0xCA904 factor, wildlife guard, No Dogs 0xFFD98, solo MP save loading, Skirmish load button, lobby chat layout, editor region refresh, Trail deletion, missing-map metadata, Coop partner keep. See [../mods/session.md#bugfixesandqol](../mods/session.md#bugfixesandqol).
- ExtendedData: Coop package map lookup, Trail export/Lord packages, Fixes preference capture, legacy saved presentation. See [../mods/session.md#extendeddata](../mods/session.md#extendeddata).
- Lord tombstone correction, Surrender selection correction and testmods: [../mods/session.md#other-mods-and-testmods](../mods/session.md#other-mods-and-testmods).
- Script Extender, Fixes, UU-ImGUI: [../mods/session.md#external-components](../mods/session.md#external-components).

## Sources

- [MISSION_LIFECYCLE](../MISSION_LIFECYCLE.md): initializer routing, return codes, managed roots, PlayState readiness, sync start countdown, MP-save remap timing; plus APIShared lifecycle contract.
- [EDITOR_MAP_LIFECYCLE](../EDITOR_MAP_LIFECYCLE.md): editor preinit/load exports and managed editor session; historical APIShared editor contract.
- [EDITOR_REGION_REFRESH](../EDITOR_REGION_REFRESH.md): editor terrain writers, path dirty flag, 0xE49D0 rebuild; BugfixesAndQoL refresh.
- [LOBBY_SAVE_LOADING](../LOBBY_SAVE_LOADING.md): load button gates, MP save start/remap/removal, handshake packets, customCoopGame, save CRC categories; BugfixesAndQoL/MultiplayerRestartTest.
- [SAVEGAME_MODSETTINGS_LOAD](../SAVEGAME_MODSETTINGS_LOAD.md): MP vs local save routes, requester timing; APIShared ownership.
- [SAVEGAME_MISSING_MAP_METADATA](../SAVEGAME_MISSING_MAP_METADATA.md): restart metadata and original-map dependency; SavegameMissingMapFixTest and Trail Maker consumer.
- [SAVE_WRITER_ROUTES](../SAVE_WRITER_ROUTES.md): writer routes, Chore 39, autosave, pause, camera serialization, setup strings (Vanilla).
- [COOP_PACKAGE_MAP_READINESS](../COOP_PACKAGE_MAP_READINESS.md): Ready/map validity, chat boundary, AI count, inactive Coop fields; APIShared/ExtendedData.
- [COOP_TRAIL_CUSTOMIZE_PRESENTATION](../COOP_TRAIL_CUSTOMIZE_PRESENTATION.md): Coop setup presentation, keep assignment, advanced options, frontend, local Coop restart.
- [LOBBY_CHAT_LAYOUT](../LOBBY_CHAT_LAYOUT.md): chat receive/render; BugfixesAndQoL compact layout.
- [TRAIL_CUSTOM_AIC_EXPORT](../TRAIL_CUSTOM_AIC_EXPORT.md): AIC/Lord identity in restart data, confirmation overlays; ExtendedData export.
- [TRAIL_IMPORT_DELETION](../TRAIL_IMPORT_DELETION.md): Trail import list; BugfixesAndQoL deletion.
- [LORD_SLOT_REMAP](../LORD_SLOT_REMAP.md): slot remap, Lord spawn, Ford tombstone defect and correction.
- [LORD_MEDIA_LOOKUP](../LORD_MEDIA_LOOKUP.md): AIC slots, speech/video tables, nickname subtypes, presentation records; APIShared/ExtendedData.
- [MODSETTINGS_ACTIVATION](../MODSETTINGS_ACTIVATION.md): No Dogs gate and dispatch feasibility.
- [VANILLA_PEACE_TIME](../VANILLA_PEACE_TIME.md): peace timer, mode gates, start troops, wildlife predicate; BugfixesAndQoL/APIShared/Diplomatie.
- [PLAYER_DEFEAT](../PLAYER_DEFEAT.md): win/loss writer, Lord elimination; APIShared observation.
- [SURRENDER_SELECTION](../SURRENDER_SELECTION.md): checksum builder and Surrender selection desync (Vanilla).
- [FIXES_COMPATIBILITY](../FIXES_COMPATIBILITY.md): external component (Fixes); Vanilla facts on Bad Things, siege tents, PCL, trade-post registration.
- [UU_IMGUI_SHUTDOWN](../UU_IMGUI_SHUTDOWN.md): external component (UU-ImGUI); Vanilla exit path and crash handler RVAs.
- Secondary: [NativeEventNotes](../../../../../../Findings/NativeEventNotes.md) (RandomEvents notes, German).
