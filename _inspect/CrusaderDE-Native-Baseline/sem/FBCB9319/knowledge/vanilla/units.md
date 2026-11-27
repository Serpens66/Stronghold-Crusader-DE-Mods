# Units, combat, siege engines, fire, disease and unit audio [Scope: Vanilla]

Native hash FBCB9319 (full hash in ../../../../CURRENT.json). Summary state 2026-11-27. Check the installed DLL hash per CURRENT.md before using any address. Evidence links point to the audit documents; they stay authoritative.

All addresses are RVAs (VA = RVA + `0x180000000`) unless written as VA. Most function names are generated `candidate` labels; the confidence column rates the described behavior, not the name. Managed facts refer to Assembly-CSharp SHA-256 `BC8B6A39...FD789`.

## Overview

- Units live in one static manager at `0x67E8400`, stride `0x490`, one-based IDs (0 reserved). Native record anchor = manager + id*0x490; the Script Extender `GameUnit` view starts at anchor + `0x65C`, so native offset - `0x65C` = view offset (e.g. native `+0x918` = view `+0x2BC`). Save section 3015 uses yet another origin (anchor - `0x658`).
- `DLL_RunTick` `0x86680` -> game tick `0xCDE60` -> unit dispatcher `0x182B00`, which handles life states (1 NeedsInit -> 2 alive, 3 delete via `0x1869C0`, 4 transform via `0x195D10`) and dispatches per type through the handler table `0x321CB0`.
- New units come from allocator `0x17FEF0` -> type initializer `0x19A240` (HP, Enemy HP modifier). Recruitment and fatal fire reuse the same slot through life state 4 (no new identity).
- Projectiles and short-lived effects (arrows, rocks, fire, plague clouds, afterimages, smoke) share one effect/projectile manager at `0x2F7A680` (stride `0xE8`), created by `0x9B2B0` and updated by the sweep `0x9F960`. Fire is effect type 9, plague clouds type 22 (`0x16`).
- Siege engines are units (types 39/40/41/58/59/60/61/77) built from tents and crewed by Engineers (type 30) through ID/global pairs.
- Line of sight is `0xA06F0` -> core `0x9E350` with obstacle heights from `0x6B990`.
- Unit speech goes through dispatcher `0x3BC0` and the shared filename buffer of SoundManager; positional SFX through wrappers `0x2960/0x2990/0x2940` into a 42-byte record stream consumed by managed `GameMap.processTestMap`.
- Extreme Troops raises the unit bound 3000 -> 10000 and the projectile/effect bound 3000 -> 6000 and suppresses most afterimage trails. Extreme Powers (mana-driven player powers) spawn, heal and damage units.

## Key functions

| RVA | Name/role | Notes | Confidence | Evidence |
|---|---|---|---|---|
| 0x86680 | `DLL_RunTick` export | Binds caller sound buffer (`0x1A45D0`), copies pending records (`0x1A3EE0`), runs simulation, returns count, resets through `0x19CE70` | confirmed export | [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md) |
| 0xCDE60 | Game tick | Calls pitch update, effect sweep, player maintenance; increments map tick `0x3665F58` at `0xCE03B` before effect sweep call at `0xCE245`; calls `0xCDB20` at `0xCE261` | confirmed | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md), [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0xD1AD0 | Periodic player scheduler | Calls `0x181340` for players 0..8; schedules migration outside modes 1/6, hiring `0xCEF00` at slot 95/200 | probable | [BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md), [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0x182B00 | Unit dispatcher / update loop | Life 1->2, life 3 -> `0x1869C0`, life 4 -> `0x195D10`; type table `0x321CB0`; engineer timeout increment `0x1832C7`; invalid-standing rescue call `0x18300F` | confirmed | [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md), [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0x1839C0 | Siege-unit crew pass | Clears engineer timeout when device, crew global and engineer state 5 match | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0x17FEF0 | Unit allocator | Scans IDs 1..bound-1, returns 0 on exhaustion; writes GlobalId `0x17FF74`, life 1 `0x17FF99`, calls `0x19A240` at `0x180000`, then `0x180230`->`0x1802A0`, `0x18BE50` | confirmed | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md), [KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md) |
| 0x19A240 | Unit type initializer | Writes type `+0x6E6` (at `0x19A27B`), default HP times owner Enemy HP modifier, dependent fields; callers `0x17FEF0`, `0x195D10`, `0x19C760` | confirmed static | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md), [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0x195D10 | Same-slot transition (life 4) | Restores life 2 (`0x195D3D`), queued AI state, calls `0x19A240` (`0x195DF6`) with `+0x922`; keeps GlobalId and creation tick | confirmed | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0x19C760 | Bulk unit reset | Accepts life 1/2, calls `0x19A240` at `0x19C7B3` | confirmed static | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0x197EF0 | New-game unit HP normalizer | Reads per-owner modifier | probable | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0x19C300 | Save-load unit normalizer | Called from `0x19A8C0`; skips HP stores for wounded records | probable | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0x19BF70 | Old-format unit migration | Not a universal load step | candidate | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0x1869C0 | Unit removal | `0x186C20` hover counter, `0x19A5D0` sync selection, `0x186AD0`->`0x186AF0` occupancy; clears 0x490 record and 1000-byte path | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md), [KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md) |
| 0x186D10 | Dismissal marker | Sets anchor `+0xA48` (MarkedForDeletion) | probable | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0x19D960 | Snapshot / PlayState writer | Sends bound-1 to managed side; emits selected one-based IDs (`0x19DEE0..0x19DF31`); copies 128-byte speech filename to struct `+0x600` and zeroes it | confirmed static | [KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md), [UNIT_VOICES.md](../UNIT_VOICES.md) |
| 0x87B20 | `DLL_TroopSelectionChanged` -> `0x19A7E0` | Requires existing local mark; cannot mark a new unit | confirmed export | [KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md) |
| 0x8C5F0 | Native input | Runs before `0xCDE60`; double-click type expansion `0x8F01B..0x8F084` | confirmed static | [KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md) |
| 0x1819D0 | Mercenary recruitment | Reserves live peasant, writes `+0x922/+0x924/+0x918=0x6D/+0x9CA=1/+0x986=0x0100`, returns same ID | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md), [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0x190CA0 | European recruitment | Same transformation contract | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0x117000 | Recruitment tribe/notification work | No successor slot | probable | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0x12A8F0 | Type-1 (peasant) handler | Runs 33-update `0x6D` transition, then life 2->4 | confirmed (runtime trace) | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0x190B30 | Worker employment (peasant pick) | Records employer `+0x990/+0x9C0`, home `+0x992/+0x994`, desired type `+0x922`, AI 109, fade `+0x986`=0x0100, work `+0x9CA`=1 | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0xB62D0 | Employment record | Building `+0x1FA+slot*2` / `+0x204+slot*4`; unit slot `+0x9EE`, employer `+0x9F0` | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0xB6340 | Peasant assignment | Mine: type 31 Miner1 slot 0, type 32 Miner2 slot 1 | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0xC9550 / 0xC98A0 | Employee validation / vacancy recount | Does not require final worker type, accepts life 4 / clears invalid pairs | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0xCEF00 / 0xB6890 / 0xC5990 | Hiring chain | Gated by player/state/mode (mode 6 excluded, mode 1 skips); collects vacancies (excl. types 8/9) with PCL route; orders by `+0x332` and `0xBFB50`/`0xB8DA0` priority | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0xD2C40 | Migration | Progress `>=4000` spawns type 1 via `0x17FEF0` at KeepDoor, resets to 2000 | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| 0xCDB20 | Player resource updater | Population recount; Extreme Powers mana regen block `0xCDD87..0xCDDCB` (max 7000) | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md), [EXTREME_TROOPS.md](../EXTREME_TROOPS.md), [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| 0xC23C0 | Player maintenance | Refreshes Lord home `+0x992/+0x994` from Keep; applies AI Lord HP percentage (gate accepts mode 1 and 99) | confirmed static | [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md), [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0x15A950 | Lord (type 0x37) handler | Home route failure at `0x15ABD1` -> `0x19BA40`, continuation `0x15ABD6`; one SFX source `0x15D2C1` | confirmed static | [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md) |
| 0x19BA40 | Unit relocation (teleport) | Removes occupancy `0x186AD0`, resets movement, writes tiles/fine coords/height/compact tile, adds occupancy `0x180230`, recomputes `0x18BE50` | confirmed static | [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md) |
| 0x196280 | Unit route builder | Stores route start `+0xC8`=`+0xC0`; returns 1 published/reached, 0 failure; never sets walking state | confirmed static | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x188340 | Path-completion predicate | Read-only arrival check | confirmed static | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x11B520 | Group move order | Writes AI 101 etc. per admitted member before `0x196280`; admission rules below | confirmed static | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x196100 | Chore-17 move handler | Sets tribe `+0x56C` fast flag (input bit 7), calls `0x11B520(tribe,x,y,0,1,1)` | confirmed static | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x11D370 / 0x123EA0 / 0x119C00 / 0x118660 / 0x1261F0 | Tribe assign / remove / create (slots 1..4499) / AI typed tribe / delete | Membership only; last member removal -> tribe life 3, deleted same tick; `0x11D3B0` increments group `+0x5C` | confirmed static | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md), [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| 0x11E960 | Group order executor | Action `0x11` dismount clears crew slots; order 15 engineer deployment (skips peasants, no `+0x9CA` check) | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0x14AED0 | Engineer (type 30) handler | Timeout compare 120 at `0x14C3E0`; tent arrival packed `0x0007006D`; recruitment transition `0x14D252`; parking states 101/105 | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0xBC6A0 | Guild parking lookup | Returns 0 at reached position | probable | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0x2ECD0 | AI harassment deployment | Reserve >=2, devices < AIC max, role-22 engineers < 20 | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0xA65A0 / 0xA8E40 / 0xA93C0 | Arab ballista / catapult / trebuchet tent builders | Full-crew writes main 5 at `0xA68EF`/`0xA916A`/`0xA97F6` without timeout reset; catapult can complete with partial/empty crew | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0x1520D0 | Catapult (39) handler | Ammo `+0x9BA` signed decrement `0x153111..0x15315C`; state-6 crew search | confirmed disassembly | [SIEGE_AMMUNITION_SIGNEDNESS.md](../SIEGE_AMMUNITION_SIGNEDNESS.md) |
| 0x1535F0 | Trebuchet (40) handler | Ammo branches `0x1541CE..0x1541E9`, `0x154375..0x154386` | confirmed disassembly | [SIEGE_AMMUNITION_SIGNEDNESS.md](../SIEGE_AMMUNITION_SIGNEDNESS.md) |
| 0x1547F0 / 0x15ECE0 / 0x160420 / 0x161220 / 0x161CB0 / 0x171C50 | Mangonel / siege tower / ram / shield / ballista / Arab ballista handlers | Death branches delete crew; tower deployment `0x15ECE0` | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0xD55B0 | Stone refill | Target 20, signed read (`0xD5649..0xD5658`) | confirmed disassembly | [SIEGE_AMMUNITION_SIGNEDNESS.md](../SIEGE_AMMUNITION_SIGNEDNESS.md) |
| 0x7EB00 | Siege tower unit/building damage | Signed Int32 product of unit max and building HP | probable | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0x192400 | Per-unit fire damage | Fatal branch `0x1925DB` for siege types -> pending state 0, life 4, switch `0x192612` -> pending type 2 (burning man) | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md#fatal-fire-damage-vanilla-facts) |
| 0x12B980 | Burning-man handler | Target of converted burning siege units | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0x192750 | Projectile damage to unit | Args: manager, unit ID, sentinel slot, radius class 0/1/2; Disease branch filters; always returns 0 for Disease | confirmed static | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| 0x9B2B0 | Projectile/effect constructor | 12 args; allocation bound reads `0x9B36A`/`0x9B40C`; reserved slots 1..24 for economy indicators (`0x9B336`); reclamation `0x9B403..0x9B498` | confirmed | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md), [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0xA1DB0 / 0xA1F60 | Projectile / afterimage initializers | Afterimage kind from `0x2DB040` to `+0xBA`; type-6 interval 3, terminal age 3 (cow file `0x87` interval 1) | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x9F960 | Effect/projectile sweep | Walks used range; state 1->2; kind-3 call `0x9FED0`, other kinds `0x9FEAE`; cursor reset to 25 at `0x9F970`; disease height reset `0x9FDB8..0x9FDD8` | confirmed | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md), [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| 0x9EF20 | Main projectile flight update | Runs when stop word `+0x80` is zero | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x9BFC0 | Type-6 AfterImage producer | Only caller `0x9FED0`; Extreme early return (epilogue `0x9C0E6`); continuation `0x9FED5` ignores result | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x9BE80 | Type-5 Steam/smoke producer | Extreme: returns 0 unless kind 1 | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x99110 / 0x99170 | Type-5 / type-6 effect updaters | Type 6: transparency `age*32/3`, state 3 at age 3 | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x9F2D0 | Effect removal | Frees state-3 records; called by reclamation | confirmed | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x9C730 | Tile linkage / disease registration | Fine coords /8; disease damage registration (center, 8 neighbours, 16 ring); calls `0x6B990` mode 0 at `0x9CD70`/`0x9CDD5` | confirmed static | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md), [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md) |
| 0xA01D0 | Subtile projection | Tables `0x2DB520`/`0x2DB524`, mirrored by rotation | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x41D60 | Map renderer (effects) | Publishes sprites via `0x1A13C0`; disease material 134 surface branch | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md), [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| 0xA2D80 / 0xA2E60 | Effect relink / purge of slots 1..0x18 in state 2 | Relinks nonzero-state records | confirmed (bytes of `0xA2E60`) | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0xA02D0 / 0xA0280 | Per-tile effect list search/link | Finds existing fire on tile | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x9C120 | `c_game_spawn_fire_internal` (SE name) | Spreads fire; skips building types 0x2D, 0x2E, 0x4A..0x4E; ignites others via `0xC4D30` | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x9C450 / 0x9DCF0 / 0x9C100 | Other fire writers | `0x9C450` rejects tile flags `0x1A7001B1` | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x99420 | Fire effect update | Damage via `0x9E030` -> `0x192400`; building fire keeps phase | confirmed static | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x9E030 | Fire tile unit damage | Passes fire owner and reduced-damage flag | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0x9C6B0 | Search fire mask/counter | Tests `state==2 && type==9` | confirmed bytes | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x9F650 | Mark tile fire records | `+0xC8=0x10002`, `+0x28=0` (called by `0xC3C80`) | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0xDCE60 | Fire hesitation | Unit `+0x406`; hesitate once, no flee | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x7D280 | Pitch descriptor update | Ignition phase 20 updates, next phase 200 | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0xB8F40 | `fireman_find_nearest_burning_building(GameBuildingManager*, int unitId) -> int buildingId` | Callers in `0x159C30` at `0x159DF8`, `0x159F4C`, `0x15A08B`; no reservation | confirmed identity, probable semantics | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md) |
| 0x159C30 | Fireman (`CHIMP_TYPE_FIREMAN`) update | States 1/2 search, 3 walk/validate, 4 extinguish; compound follow-up | confirmed static | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md) |
| 0x79C0 | Manhattan distance helper | Unit tile `+0xC0/+0xC2` to building `+0x28/+0x2A` | probable | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md) |
| 0xC3C80 | Extinguish building | Fire counter -> 0, cooldown `0x320` = 2000 | probable | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md) |
| 0xC4D30 | Ignite building | Sets counter 1 if eligible; `0xC1F60` rejects building type 0x45 (deployed tower) | probable | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md), [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| 0xC60F0 | Building update | Increments fire counters, own terminal paths | probable | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md) |
| 0xD17D0 | Natural plague creation | Only caller `0xD1600`; 6..10 clouds on one tile, then `0x2960(0xBD)`, `0xF4D40` | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x504F0 | `c_game_ai_create_flag_disease` | Reads lord flag type, spawns disease projectile via generic spawn | confirmed identity, probable semantics | [function-claims.jsonl](../function-claims.jsonl) |
| 0x9A080 | `c_game_projectile_disease_update` | Age `+0xD0`, lifetime compare 800 at `0x9A16C` (block `0x9A164`/`0x9A176`), fade | confirmed identity, probable semantics | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md), [function-claims.jsonl](../function-claims.jsonl) |
| 0x9F700 | `c_game_projectile_disease_find_nearest_for_healer` | Apothecary search, Manhattan distance from healer building, maximum 30 at `0x9F86B` | confirmed identity, probable semantics | [function-claims.jsonl](../function-claims.jsonl) |
| 0xA0470 | Cloud treatment | Phase 1016 for state-2 clouds, phase <=1000, distance < 7 | confirmed static | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| 0x14F3C0 | Apothecary healer update | Calls `0xA0470`; no direct SFX wrapper calls | probable | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md), [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md) |
| 0xA0540 | Cloud death path | Sets state 3 | probable | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| 0x6B910 | Surface height with roofs | Used by disease neighbour gates; not a unit standing plane | confirmed static | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| 0x6AE20 | Ring tile helper | With pairs `0x2DB750` (Chebyshev 2) | probable | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| 0x198C40 | Tower/keep occupancy | Uses list `0x4C559B0` / unit `+0x8FA` | candidate | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| 0x184FD0 | `c_game_unit_update_standing_height` | Writes base `+0xB6`, signed correction `+0xB8`, aux word, transition; selected-unit addition; not a getter | confirmed | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md), [function-claims.jsonl](../function-claims.jsonl) |
| 0xC07C0 | `c_game_building_get_type_height_offset` | Read-only type switch (jump tables `0xC0830`/`0xC0858`), clobbers RAX/RCX/RDX/flags | confirmed | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md), [function-claims.jsonl](../function-claims.jsonl) |
| 0x19B260 | Movement/base normalization, speed sync | Reads tribe fast flag `+0x56C` | probable | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md), [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| 0x151090 | `c_game_unit_update_monk` (type 37, dispatch entry 37) | Fighting Monk and Temple Guard skins; walk/run branch at `0x151436` | confirmed identity, probable semantics | [function-claims.jsonl](../function-claims.jsonl) |
| 0x181340 | Bedouin healer candidate list builder | Per player; skips siege types | probable | [BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md) |
| 0x174100 | Bedouin healer updater | State `+0x2BC`, target `+0x450`; heals 10 HP per pulse | probable | [BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md) |
| 0x18DE10 | Bedouin healer target selector | Range/height/LOS; far target -> `0x196280`, state `0x65` | probable | [BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md) |
| 0x197A10 | Turn toward target | | candidate | [BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md) |
| 0xA06F0 | LOS wrapper (156 bytes) | Forward then reverse `0x9E350`, arg8 = 0; 16 callers | confirmed disassembly | [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md) |
| 0x9E350 | LOS core (2681 bytes) | Writes only `[ctx+0xC]`; returns 0, steps (<=1000) or 100000 | probable | [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md) |
| 0x6B990 | Obstacle height (290 bytes) | Terrain + building contribution; mode 1 exempts Hunter's hut | confirmed disassembly | [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md) |
| 0x18AF50 / 0x12FC70 | Hunter target search / HunterUpdate | LOS call `0x18B0A2`, visible if result 1..0x1B0 | probable | [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md) |
| 0x18E9A0 | Ranged attack order | LOS call `0x18ED6A`, visible if result > 0 | probable | [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md) |
| 0x13F540 | European archer update | Brazier predicate `0xA0310`; launch types 0x18/1 or ignited 0x5C/0x5B | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x143400 | Spearman handler | Ladder push voice at `0x143B16` via `0x18CD10` | confirmed static | [UNIT_VOICES.md](../UNIT_VOICES.md) |
| 0x16F590 | Arabian swordsman (type 75) handler | Idle sword-draw SFX at `0x1700CD` | confirmed static, in-game | [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md) |
| 0x3BC0 | Troop voice dispatcher | 27 columns, 42 situations; 18 direct call sites | probable | [UNIT_VOICES.md](../UNIT_VOICES.md) |
| 0x3770 / 0x38A0 / 0x39A0 / 0x3B20 | Engineer/siege voices / healer-sapper-demolisher + advisor (indices 65..76) / recruitment confirmations / civilian comments | | probable | [UNIT_VOICES.md](../UNIT_VOICES.md) |
| 0xD5B40 / 0xD5DE0 / 0xD6030 / 0xD6290 / 0xD6830 | Recruitment success paths calling `0x39A0` | Error paths emit advisor/Genie speech | probable | [UNIT_VOICES.md](../UNIT_VOICES.md) |
| 0x97C70 -> 0x97DA0 | Civilian comment driver | Speaks only on changed selection (`0x97E09`/`0x97E11`) | confirmed static | [UNIT_VOICES.md](../UNIT_VOICES.md) |
| 0xEDF0 | Sound manager speech request (confirmed symbol) | Filename writer; rejects busy channel 3 / mode 4 | confirmed | [UNIT_VOICES.md](../UNIT_VOICES.md) |
| 0xEF20 / 0xF070 / 0xF0F0 | Other speech filename writers | Include unrelated message paths | probable | [UNIT_VOICES.md](../UNIT_VOICES.md) |
| 0x1A4270 | Speech record writer | Record 18, channel, volume 100 | probable | [UNIT_VOICES.md](../UNIT_VOICES.md) |
| 0x2960 / 0x2990 / 0x2940 | SFX wrappers (40 bytes; spatial flag 0 / 1; explicit volume 100) | `0x2960`/`0x2990` -> `0x1A41E0`; `0x2940` (22 bytes) -> `0x1A4190` | confirmed static | [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md) |
| 0x1A41E0 / 0x1A4190 | Spatial / explicit SFX emitters | Record kind 16, 21 shorts | confirmed static | [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md) |
| 0x81870 | `DLL_GameAction` export | Command 1068 case `0x8257E` -> `0x105510` (call `0x825A5`) | confirmed | [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| 0x105510 | Extreme power selection | Cost `(id+1)*636` check, jump table VA `0x180105658`; pending power write `0x1055C7`, targeting mode 5 `0x1055CF` | confirmed | [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| 0xCD630 | Extreme power effect dispatcher | Args EDX player, R8D power, R9D tile; only caller `0x1ADFE`; jump table VA `0x1800CD830`; mana deduction `0xCD814` | confirmed | [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| 0x1264D0 | Group unit spawn | Mode 1, tile, height, player, type, count; call `0xCD6CF` | confirmed | [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| 0xE1E70 | Area heal | Radius 6, amount 8000 | confirmed | [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| 0xDD6C0 | Area volley damage (2498 bytes) | Damage, flood-fill radius, projectile kind | confirmed | [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| 0x7530 | Simulation RNG advance | Advances `0x856A6D0` sequence | confirmed | [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md), [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| 0x8CAF2 (site) | Map-point target completion | Reads pending power, writes chore from `0x8CE0C` | confirmed | [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| 0x86270 | `DLL_PreInitMap_Multiplayer` | Mode VA `0x188574B90` = 99 offline / 1 real MP (`0x86352`, `0x8636E`, `0x86377`) | confirmed export | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0x81050 -> 0x26370 | `DLL_ApplyMultiplayerSetupData` | Stores Enemy HP enum, Extreme Troops bounds | confirmed export | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md), [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x94350 | Skirmish map initializer | Enemy HP table writer only in mode 99/0 (`0x95449..0x95450`); spectator reference AI `0x94646..0x94675`; re-applies unit bound | confirmed static | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0x100530 | Mission initialization | Per-owner HP from difficulty/team | probable | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0xB47E0 | Building spawn | Default HP `0x2E7510+type*4` -> `0x168/0x16A` (`0xB4C89..0xB4C9B`), no Enemy HP | confirmed disassembly | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0xCA110 / 0xCA5F0 | Building HP normalizers | Building-type table only | probable | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0xD5030 | Building repair | Multiplies current HP by wood/stone costs | probable | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0x15710 / 0x158B0 | Chore 56 / 57 record transfers | Copy 0x490 unit / 0x32C building record without new-object init | confirmed static | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0x187B30 | Unit rebuild after transfer | No new type-initializer call | probable | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| 0x86120 / 0x8AE80 / 0x87D10 | Free Build preinit / bound upgrade / reset | Free Build Extreme flag `0x87EE2E0` | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x52B0 / 0x87D0 / 0x15C60 | Save writer / reader / state transfer of bounds | Reader defaults 3000; legacy branch 10000/6000 | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x84280 -> 0x20420 | Reverse setup export | Extreme flag = unit bound > 3000 | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x22020 | Native checksum | Includes both bounds | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0xCBD90 | Player unit quotas | `(bound-100)/slots-40` | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0xABB90 | Outpost troop group handler (types 2/106/107) | Extreme timing branch `0xAC4F4`, `0xACD91`, `0xACDC3` | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| 0x853F0 | `DLL_LoadSaveGame` | Passes descriptor table `0x336B20` to `0x87D0` | confirmed | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |

## Data and fields

Offsets are given as native anchor offset (manager + id*stride) and, where known, Extender view offset.

| Record/manager | Offset | Meaning | Type/sign/unit | Confidence | Evidence |
|---|---|---|---|---|---|
| Unit manager `0x67E8400` | stride 0x490 | One-based units; view = anchor+0x65C; save section 3015 = id*0x490+native-0x658 | | confirmed | [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md), [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | native +0x660 / view +0x4 | Written by monk movement branch | | probable | [function-claims.jsonl](../function-claims.jsonl) |
| Unit | +0x68C / +0x30 | Local selection mark | WORD | confirmed | [KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md) |
| Unit | +0x6A8 / +0x4C | Animation-advance flag | | confirmed | [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md) |
| Unit | +0x6E4 / +0x88 | AliveState: 1 NeedsInit, 2 alive, 3 delete, 4 transform | | confirmed | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| Unit | +0x6E6 / +0x8A | Unit type | word | confirmed | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| Unit | +0x6EC / +0x90 | Unnamed working word; both the invalid-component rescue and group admission require != 1 (same field, two documents) | | candidate | [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md), [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Unit | +0x6EE / +0x92 | `r_ControllableForPlayerId` (owner) | uint16 | confirmed (width corrected 2026-11-15) | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md) |
| Unit | +0x6F4 / +0x98 | Creation tick (original creation, kept through conversion) | int | confirmed | [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md) |
| Unit | +0x70A / +0xAE | Synchronized selection | | confirmed | [KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md) |
| Unit | +0x70E/+0x710 / +0xB2/+0xB4 | World position X/Y (`r_CurrentWorldPositionX`) | signed short | confirmed | [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md) |
| Unit | +0x712 / +0xB6 | Base standing elevation (`r_HeightElevation`) | signed short | confirmed | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| Unit | +0x714 / +0xB8 | Signed height correction (SE `N0000006A`) | signed short | confirmed | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md), [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md) |
| Unit | +0x71C/+0x71E / +0xC0/+0xC2 | Current tile X/Y (also SFX coordinates) | short | confirmed | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md), [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md) |
| Unit | view +0xC8 | Stored route start (= current tile) | | confirmed | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Unit | +0x72C / +0xD0 | Compact current standing tile | | confirmed | [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md) |
| Unit | view +0xDC | Next tile (builder start while moving) | | confirmed | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Unit | view +0xE8/+0xEA | Move target | | confirmed | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Unit | view +0xEC, +0x342, +0x3A4 | Cleared by group move | | confirmed static | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Unit | view +0xF6 | Route cursor (counts step toward next tile) | | confirmed | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Unit | view +0x78 | Destination marker counter (0x10) | | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Unit | +0x8F8 / +0x29C | Exclusion word (healer list requires 0; group admission) | | probable | [BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md) |
| Unit | view +0x29A, +0x40F (byte), +0x428 | Group admission requires zero | | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Unit | Spearman24 AI1 / dispatcher321D70 ->143400 | Pure stationary animation144B6E..144C57, then908=0/AI0 ->181890 positional recovery. Command6/7 can complete it early; no attack/goods/job-target consumption in this branch. Does not describe other typesAI1 or guarantee no pending context. | | confirmed static + machine switch | [TUR](../TRAPPED_UNIT_RESCUE_AUDIT.md#27112026--spearmanai1-stationary-animation-vanilla-facts) |
| Unit | +0x8FC / +0x2A0 | `r_SelectionAllowed`: initialized0; Spearman143438/14344D writes1 before every AI switch; Archer13F540 initializes a zero value from the tick word. Not a zero-valued portal/idle gate;19BA40 and its relocation closure leave it unchanged. Workers/civilians/firemen use0. | | confirmed | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md), [TUR](../TRAPPED_UNIT_RESCUE_AUDIT.md#27112026--military-selection-word-is-not-an-idle-movement-exclusion-vanilla-facts) |
| Unit | +0x8FA (native) | Link for list `0x4C559B0` (tower/keep occupancy) | | candidate | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| Unit | +0x908 / +0x2AC | Work/animation timer | | confirmed | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | +0x90C / +0x2B0 | Direction | | probable | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | +0x914, +0x99E, +0xA64 | Read by monk update (view +0x2B8, +0x342, +0x408) | | probable | [function-claims.jsonl](../function-claims.jsonl) |
| Unit | +0x916 / +0x2BA | Main-state DWORD write (monk, engineer case 101) | | probable | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | +0x918 / +0x2BC | `r_AIState` / packed main state (`0x6D`=109 transition, 101 walk, 105 parking, 122 exit, 207, `0x0005006D`/`0x0007006D`) | dword | confirmed | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | +0x922 / +0x2C6 | Requested (pending) type | word | confirmed | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| Unit | +0x924 / +0x2C8 | Pending main state | | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | view +0x2D2/+0x2D4/+0x2D6/+0x2D8/+0x2DA/+0x2E0 | Tribe leader / tribe / ordinal / slot X/Y / tribe global ID | | confirmed static | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Unit | +0x974 / +0x318 | Engineer: bound-without-refresh timeout; siege device: first crew global ID | signed dword | confirmed | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | +0x986 / +0x32A | Visual/fade word (0x0100 recruit, 0xFE20 release) | | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | +0x990 / +0x334 and +0x9C0 / +0x364 | Employer (workers) / linked unit-building ID and global (siege tower 58 <-> building 69) | | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md), [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| Unit | +0x992/+0x994 / +0x336/+0x338 | Home X/Y (Lord home from Keep; worker home) | | confirmed | [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md) |
| Unit | +0x9B0 / +0x354 | Engineer mounting word | | probable | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | +0x9BA / +0x35E | Catapult/trebuchet stone ammo word (`r_StoneAmmoLeft`/`r_StoneAmmoStacks` bytes) | signed 16-bit to consumers | confirmed | [SIEGE_AMMUNITION_SIGNEDNESS.md](../SIEGE_AMMUNITION_SIGNEDNESS.md) |
| Unit | +0x9CA / +0x36E | Recruitment/work marker | | confirmed static | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | +0x9EE / +0x392, +0x9F0 / +0x394 | Employee slot / employer building | | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| Unit | +0x9F6/+0x9F8 / +0x39A/+0x39C | Target building/device ID and global ID (fireman target; engineer device) | | confirmed | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md), [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | view +0x3B0 | Siege-type admission value (39/77 need 2, 58/59 need 4, 60 needs 1) | | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Unit | view +0x406 | Fire hesitation flag | | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Unit | +0xA48 / +0x3EC | MarkedForDeletion (dismissal) | | probable | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |
| Unit | view +0x450, +0x458 | Healer target one-based ID; exclusion byte | | probable | [BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md) |
| Unit | view +0x634 | Extreme-power group marker (state 2) | | confirmed | [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| Unit path buffer | 1000 bytes per unit | Packed route; cleared by `0x7490` before builder | | confirmed | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Tribe | stride 0x688; save section 2016 = id*0x688+native-0x28 (view origin +0x2A) | `+0x56C` fast flag; group count `+0x5C` | | confirmed | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md), [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Building manager `0x64CCBB0` | stride 0x32C; view = anchor+0x5C | Type `+0x12E`; position `+0x14A/+0x14C`; elevated base `+0x148` (view `+0xEC`); HP `+0x168/+0x16A` (view `+0x10C` signed int16 current / `+0x10E` uint16 max); compound key `+0x304` (view `+0x2A8`); `r_OnFireTicks` `+0x31A` (view `+0x2BE`); cooldown `0x320` | | confirmed | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md), [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md), [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| Building | view +0x28/+0x2A, +0x110/+0x114, +0x1FA/+0x204, +0x332 | Begin tile; siege tower unit link; employee ID/global arrays; hiring priority | | confirmed static | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md), [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md), [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| Effect manager `0x2F7A680` | stride 0xE8 | `+0x14` frame, `+0x1A` sprite file, `+0x24/+0x26` interval/timer, `+0x28` age/phase, `+0x32` next on tile, `+0x36` reclaim byte, `+0x38` prev, `+0x3C` state, `+0x3E` type, `+0x44` global ID, `+0x4C/+0x4E/+0x50` fine X/Y/height, `+0x60` tile ID, `+0x80` stop word, `+0x8C` trajectory, `+0xAA` damage step counter, `+0xB2` steps left, `+0xBA` afterimage kind, `+0xC0` bit 7 variant, `+0xC8/+0xCA` fire timer/strength, `+0xD0` disease age (signed short) | | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md), [FIRE_EFFECTS.md](../FIRE_EFFECTS.md), [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| Effect tile heads | `0x4CF23F0[tileId]` | Short record index per tile | | confirmed static | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Tile manager `0x405EDB0` | +0x898400 (=`0x48F71B0`), +0xA6F260, +0xB0BCA0, +0xD7E5A0 (=`0x4DDD350`) | Flags uint; tile grid value; building id short; terrain height byte | | confirmed | [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md), [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md) |
| Globals | `0x402FF2C` | Row lookup (stride 12, SE `GetTileId`) | | confirmed | [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md) |
| Globals | `0x50EC690` | uint16 component grid | | confirmed | [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md) |
| Globals | `0x3A11EA4` | Tile validity byte grid (800 wide) | | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Globals | `0x51890D0` | Direction bytes (movement mask) | | probable | [FIRE_EFFECTS.md](../FIRE_EFFECTS.md) |
| Globals | `0x3665F58` | Monotonic map tick (Int32; SE `GetElapsedMapTicks`; save section 1046) | | confirmed | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| Globals | `0x3665F84` | Shared effect creation serial | | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| Globals | `0x3A0FA28` | Sweep cursor-reset counter (calendar-cycling, not a clock) | | confirmed runtime | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| Globals | `0x3668E34` / `0x3668E38` | Unit / effect exclusive bounds (3000/3000 or 10000/6000) | signed dword | confirmed | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| Globals | `0x3668E30` | Legacy-format flag | | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| Globals | `0x3669048` | Native trail type (signed) | | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| Globals | `0x87EE2E0` | Free Build Extreme Troops flag | dword | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| Globals | `0x37EF950` / `0x37EF954` | Per-player unit quotas | | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md) |
| Player resources | stride 0x583C | `+0x137DD0` counter incremented in `0xCDB20` (see contradictions), healer counter `0x379E6D0+owner*0x583C`, mana `+0x39D4` (`0x379E7A4`), population `+0x2180`, migration `+0x60/+0x6C/+0x70`, housing `+0x74`, idle peasants `+0x8C`, KeepID `+0x94`, DoorID `+0x1D4`, `+0x2210/+0x2244` gates | | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md), [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md), [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| Globals | `0x379AE54`, `0x379AE48`, type table `0x3223D0` | Per-owner unit counters | | confirmed static | [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md) |
| Globals | `0x379F66C`, `0x3945748`, `0x3971668` | Bedouin healer candidate count (stride 0x583C) / ID lists (short[10000], stride 20000) / global ID lists (int[10000], stride 40000) | | probable (mod contract `ExtraFeatures/src/HealerNativeContract.cs`) | [BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md) |
| Globals | `0x8574C44` | Lineup (zero = human slot) | | confirmed | [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md) |
| Globals | VA `0x188574B90` (RVA `0x8574B90`) | Native mode: 99 offline skirmish, 1 real MP, 0 | | confirmed | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| Globals | VA `0x1887EE324` (manager+0x27A004) | Stored global Enemy HP option | | confirmed static | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| Globals | VA `0x183666058 + player*4` | Effective per-owner HP modifier (1 normal, 3 = 150%) | | confirmed static | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md) |
| Globals | `0x2E7510`, `0x2E8C60`, `0x2E68D0` | Building default HP / building sight height / building class table | | confirmed static | [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md), [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md) |
| Globals | `0x32BC40` | Disease immunity by unit type | | confirmed static | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| Globals | `0x2DB040`, `0x2DB520/0x2DB524`, `0x2DAF70`, `0x2DB750` | Afterimage kind; subtile offsets; per-type phase period; ring pairs | | confirmed static | [EXTREME_TROOPS.md](../EXTREME_TROOPS.md), [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| Globals | `0x856A6D0` / `0x856A6D2` | Simulation RNG state / word | | confirmed | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| Globals | `0x321CB0` (entry 75 at `0x321F08`), `0x9302C4` | Unit handler table; current one-based unit ID | | confirmed | [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md) |
| Globals | `0x330580`, `0x3305E0`, `0x330640`, `0x3306D0` | Arabian swordsman idle animation tables (by id&3) | | confirmed static | [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md) |
| Sound | `0xA98820`, output buffer manager+`0x10C9E8C`, SoundManager filename `+0x41A6` (VA `0x1888EB476`), `+0x28`, `+0x41A4/5` | SFX record manager; speech filename (128 bytes) | | confirmed static | [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md), [UNIT_VOICES.md](../UNIT_VOICES.md) |
| Voice tables | `0x2AD298`, `0x2AD290`, `0x2AD370`, `0x2B0EB0`; switches VA `0x183665F28`, `0x183665F0C` | File index, variant counts, per situation/column counters, column-10 alternative | | confirmed static | [UNIT_VOICES.md](../UNIT_VOICES.md) |
| Globals | `0x67E839C`, `0x2D9E58` | Selected civilian / previous selected civilian | | confirmed static | [UNIT_VOICES.md](../UNIT_VOICES.md) |
| Extreme Powers | `0x366A0C4`, `0x60AD574` | UI selected power / pending power for target completion | | confirmed | [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md) |
| Save loader | table `0x336B20` (24-byte descriptors) | Descriptor: target pointer +0, load size +12, UInt16 section ID +18. Section 3015 units `0x337030` -> VA `0x1867E8A58` (11,680,000 bytes); 1016 `0x337090` and 2016 tribes `0x3370A8` -> VA `0x187CC6748` (2,508,000 / 7,524,000); 2022 `0x337108` -> VA `0x18379ADD0` (203,292); 1046 tick `0x337210` -> VA `0x183665F58` (4) | | confirmed | [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md) |

## Behavior

### Unit life cycle and identity

- Allocation leaves life state 1; the dispatcher's later pass sets 2. Life 3 is consumed via `0x1869C0` before the type handler; removing a selected unit does not select its replacement. [confirmed, static] ([KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md))
- Slot reuse is real: never identify a unit by slot alone across saves or time; use slot + GlobalId (+ owner). Unchanged slot/GlobalId/type does not prove no reinitialization happened (`0x195D10`, `0x19C760` reinitialize in place). [confirmed, static + save evidence] ([ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md), [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md))
- Removal (`0x1869C0`) does no cargo refund, worker respawn, synchronous population decrement or immediate hiring; population is recomputed by `0xCDB20`. [confirmed, static] ([RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md))
- Building maintenance `0xC60F0` validates employee ID/global pairs through `0xC98A0` before the building type handler; `0xC9550` rejects life 3 already before the unit record is cleared. This is not immediate cleanup of every auxiliary job/animal reservation. [confirmed, static 2026-11-27] ([RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md#additional-civilian-employment-and-completed-target-fields-27-november-2026-vanilla-facts))
- Chore 56/57 multiplayer repair transfers copy whole unit/building records without running new-object initialization. [confirmed, static] ([ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md))

### Recruitment, employment and migration

- Recruitment reuses an existing type-1 peasant slot: requested type `+0x922`, state `0x6D`; after 33 updates of the type-1 handler life becomes 4, `0x195D10` restores life 2 and initializes the requested type in the same slot. The slot stays type 1 for the rest of that update pass. [confirmed, static + runtime trace 2026-09-17] ([RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md))
- Worker employment is recorded (`0xB62D0`) while the unit is still a peasant; employment does not prove the transformation finished. `0xC9550` accepts transient life 4. [confirmed, static 2026-11-27] ([RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md))
- Ordinary Peasant employment also supplies Priest33 (building36/37/38), Healer34 (building23), Innkeeper36 (building22) and Fireman53 (Waterpot70); Monk37 and BedouinHealer79 are separate types. Ox9 is allocated by the tether, not hired from a Peasant. Completed transformations retain `+0x922`; zero is not a general worker invariant. [confirmed, static + installed SE enum readback 2026-11-27] ([RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md#additional-civilian-employment-and-completed-target-fields-27-november-2026-vanilla-facts))
- Ordinary hiring CEF00/B6890/C5990/B6340/190B30 uses the same playerIDs1..8 for human and AI owners. Keep+door, vacancies, ready existing peasants, live owner-matching workplace and reachable PCL are still required; prefab owner2 alone proves none of these. Generic employee ID/global cleanup differs from per-pass Mine/Miller heartbeat IDs and reserved Cow timeouts. [confirmed, static2026-11-27] ([RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md#auxiliary-worker-bindings-and-ordinary-ai-hiring-27-november-2026-vanilla-facts))
- Deleting an employee can expose a vacancy but never creates the replacement; hiring needs eligibility, a reachable PCL route, a free peasant and a later hiring tick. [confirmed, static] ([RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md))
- Migration (`0xD2C40`): progress from popularity, arrival at progress >= 4000 when Keep and door exist, population < housing and idle peasants < 24 (or native option), spawns type 1 at the KeepDoor and resets progress to 2000; allocation can still fail. Low progress has an emigration branch. KeepDoor-to-campfire movement is not closed. [confirmed, static] ([RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md))
- Recruitment voice confirmations come from success paths and can be throttled. [probable, static] ([UNIT_VOICES.md](../UNIT_VOICES.md))

### Selection

- Local mark (`+0x30`) and synchronized selection (`+0xAE`) are different fields; `DLL_TroopSelectionChanged` only works on already marked units. The output list only includes life-2 units passing exclusion, view-owner and mark checks. [confirmed, static] ([KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md))
- `EngineInterface.run` holds `threadLock`, calls `EditorDirector.preDLLCallActions` -> `TroopSelection`, then `DLL_RunTick`; native input `0x8C5F0` runs before the game tick; the SE time site is `0xD1D4F` (at most once per run). The time path also runs from init `0x71670` and editor resize `DLL_SetMapEditorParam` `0x87460` without the monitor. [confirmed, static] ([KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md))
- Double-click expands to all visible units of the type (same identity, < 500 ms, mouseState 1); Stop subcommand 0x1F does not write the mark. [confirmed, static] ([KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md))

### Group movement (as used by fire/plague handling)

- `0x196280` alone never sets the walking state; a standing soldier moved only by it glides. `0x11B520` writes AI 101 and related fields per admitted member before routing; after failure in the exit case the state becomes 122. [confirmed, static] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md))
- Admission: life 2, `+0x29C`, `+0x29A`, `+0x40F`, `+0x428` zero, AI != 207, `+0x90` != 1, type not 40/41/61; types 39/77 need `+0x3B0`==2, 58/59 need 4, 60 needs 1. Non-admitted members keep their old order. Return 1 even with no admitted member; 0 for invalid targets, flags & 0x30, PCL <= 0, failed region checks. [confirmed, static] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md))
- Player move: `0x8C5F0` -> chore 17 -> `0x196100` -> `0x11B520(tribe,x,y,0,1,1)`; AI and patrol pass (0,0,0). `newOrder` writes `r_PatrolPoint1/2`, never `r_ObjectiveTargetTileX/Y`. Single-member tribe slot = commanded tile (`0xDA590`, `0xE1D30`). [confirmed, static] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md))
- Selection (opcode 16) moves units into a new tribe; AI tribes from `0x118660`; the AI never orders a tribe it did not create. [probable, static] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md))
- Route storage: decode a moving unit's buffer from next tile `+0xDC` over steps cursor..length-1 (about 98% end at the target). Target equal to start stops the unit (state 0, zero cursor/length). A failed builder can leave zeroed/partial bytes; failure is not a full stop (corrected 2026-11-15 in MOAT_TRAVERSAL_ALL_UNITS.md). [confirmed, static + runtime] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md))

### Lord home relocation

- Lord state 1 routes home with `0x196280`; return 0 directly relocates the Lord (`0x15ABD1` -> `0x19BA40`) with no human/AI check and no timer. Route failure can come from coordinates, surface, portal admission or route construction. [confirmed, static] ([LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md))
- Human Lords idle in state 13 (handler returns for human slots); the trigger is e.g. losing an outside building-attack target (state `0x6B` -> 0 -> 1). User-confirmed video trigger 2026-10-28. [confirmed static, user-confirmed] ([LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md))
- Invalid-standing-component rescue at `0x18300F` (return `0x183014`): one of 16 ID buckets per pass, compact tile component 0 and not IsMoat `0x40000000` (test `0x182EF5`), age (tick - creation) >= 1000, `+0x6EC` != 1, state != `0x6E`, building gates (live, class `0x2E68D0` zero, not drawbridge 0x31). Lord relocates home with state 1. Other types use other recovery paths. Not observed in natural gameplay. [confirmed, static] ([LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md))
- Component-zero producers: seed mask `0x4A5014B1` (vegetation exception `0x107160`) in rebuild `0xE49D0`; tree placement `0x6E2C0`, orchard `0x1071A0` (type 0xF); edge updater `0xD86F0`; natural growth `0x108320`/`0x107A80` unproven under units. Moat completion `0x639C0` sets IsMoat and clears PlannedMoat `0x4000`. [confirmed, static] ([LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md))

### HP, Enemy HP and arithmetic bounds

- The global Enemy HP option is applied only by `0x94350` in mode 99 or 0; real multiplayer (mode 1) never applies it. Equal team to the reference player keeps normal HP; spectator selects the first occupied AI slot as reference. [confirmed, static] ([ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md))
- Enum 3 = signed 6/4 (150%) with truncation on current and max HP for every unit type (civilians, siege units included); buildings are never scaled. AI Lord profile HP percentage applies later in mode 1 and 99. [confirmed, static] ([ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md))
- Safe bounds: unit percent uses signed Int32 HP*100 (max 21,474,836); building HP <= 32,767 and Int32.MaxValue / repair cost; siege tower unit max * building max <= Int32.MaxValue. [confirmed, arithmetic] ([ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md))
- Save-load normalizers keep wounded records' max HP. [confirmed, static] ([ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md))

### Siege engines and engineers

- Crew capacity: catapult 0x27 2, trebuchet 0x28 3, mangonel 0x29 2, siege tower 0x3A 4, ram 0x3B 4, portable shield 0x3C 1, ballista 0x3D 2, Arab ballista 0x4D 2. [confirmed, static] ([SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md))
- Crew timeout: engineer in main 5 increments `+0x974` each pass; `>120` releases (state `0x6D`, visual `0xFE20`, then main 0) and the counter survives. Tent full-crew writes main 5 without resetting it, so a stale counter can release a new crew immediately while the device keeps its crew entries. [confirmed static, runtime log 2026-09-29] ([SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md))
- Automatic crew search (state 6) differs per handler; catapult and Arab ballista AI branches accept any role-22 engineer, including ones already operating (main 5 / `0x0005006D`), so two devices can share the same crew ID/global pairs (reproduced 2026-11-13). Main 7 / `0x0007006D` are tent-assigned; main 8 is only approaching. Search spans in [the table](../SIEGE_ENGINEER_CREW_TIMEOUT.md#automatic-device-crew-selection-vanilla-facts): catapult `0x15258A`/next `0x152681`, trebuchet `0x153AFA`/`0x153BD6`, mangonel `0x154DBC`/`0x154E94`, tower `0x15F278`/`0x15F34A`, ram `0x16082A`/`0x160907`, shield `0x161549`/`0x16161B`, ballista `0x16221C`/`0x1622F5`, Arab ballista `0x17224A`/`0x172345`. [confirmed, static + live] ([SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md))
- Partial-crew tent completion is deliberate: catapult builder can create an empty device and release arrived workers with pending command 16; it writes device ID `+0x9F6` but not global `+0x9F8`. Idle unclaimed engineers can be picked up instead of later arrivals. [confirmed, static + live] ([SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md))
- AI harassment reserves hold recruits before conversion; order 15 accepts states 101 and 105 (guard excludes 9..15) but not `+0x9CA` state; engineers parked in 105 stay there indefinitely (no normalization to 0..2). Role-22 count includes stranded engineers, stalling the quota at 20. [confirmed, static + isolated execution] ([SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md))
- Death: ordinary siege death marks matching crew engineers for deletion; fatal fire turns the device into a burning man (life 4, pending type 2), skipping crew handling; dismount (action 0x11) clears crew slots; tower deployment may release engineers; deployed tower building 0x45 cannot be ignited. [confirmed, static] ([SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md))
- Stone ammo is compared signed (`TEST AX,AX; JLE`): valid finite supply 1..32767; refill targets 20 signed. 32760+20 becomes negative; saturating at 65535 does not help. Non-positive branches include separate AI handling. [confirmed, disassembly] ([SIEGE_AMMUNITION_SIGNEDNESS.md](../SIEGE_AMMUNITION_SIGNEDNESS.md))

### Fire

- Fire is effect type 9; a second fire on a burning tile renews timers and raises strength `+0xCA`; strength > 2 on suitable terrain adds a type `0x1A` record. Spawn tiles with flags `0x10300131` get no fire. [probable, static] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md))
- Effect states: 1 new, 2 active (sweep moves 1 -> 2), 3..8 end. [probable, static] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md))
- Fire damages only units on the burning tile (`0x99420` -> `0x9E030` -> `0x192400`); tiles with flags `0xBA7001B1` take none; units only hesitate once, no fleeing. [probable, static] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md))
- Isolated single fire (strength 3) lives 878 updates with `+0xC0` bit 7 set, 1088 with it clear (~22 / ~27 game seconds at 40 updates/s); reignition and building fires extend this; no general maximum. [confirmed, isolated native execution] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md))
- Pitch OnFire is only ignition (left the state within 1.5 s; fire records burned ~13 s); route direction bytes do not avoid burning pitch. [confirmed, runtime] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md))
- Firemen (wells and water pots): selector scans buildings ascending, same diplomacy group, nonzero fire ticks, Manhattan <= 999, reachable PCL, strictly closest wins (ties keep earlier ID); no reservation. One throw extinguishes completely (counter 0, cooldown 2000) and every live part with the same nonzero compound key. A fireman stopped in state 3 extinguishes from afar. Other paths can also end fires. [probable, disassembly] ([FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md))
- Fire arrows: ignited launch types become archer arrow type 1 with sprite file `0x91` (`GM_ANIM_MISSILE_FIRE`) and afterimage kind 1, emitting type-5 smoke (file 125 `GM_BODY_STEAM`) in both troop modes. [confirmed, static + user captures 2026-11-22] ([EXTREME_TROOPS.md](../EXTREME_TROOPS.md))

### Disease (plague clouds)

- Natural creation `0xD17D0`: up to 20 random tiles at -15..+14 around the building, first valid tile without flags `0x4A5014B1`, 6..10 clouds on that tile at the tile corner; also catapult-impact creation; AI lord flag disease `0x504F0`. SE `CreateProjectile(...Disease...)` matches the constructor arguments but skips RNG, `0x2960`, `0xF4D40`. [probable, static] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md))
- Life cycle: phase every 4 ticks (0..7 intro, 8..15 loop); at age = lifetime 800 phase -> 1016, 1017..1023 fade (~29..32 ticks), 1024 -> state 3. Signed 16-bit age: lifetimes > 32767 fade at once; safe maximum 32764. [confirmed, static] ([PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md#cloud-life-cycle-movement-and-damage-cadence-audit-2026-11-02-same-hash))
- Treatment needs distance < 7 and phase <= 1000; since age is below lifetime the cloud often resets to phase 8, so treatment repeats. Apothecary search radius is Manhattan 30 (`0x9F700`). [confirmed, static] ([PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md))
- Damage every 31 ticks (`+0xAA` > 30), regardless of phase (fading clouds still hurt); center tile without height gate, 8 neighbours and 16-ring tiles with `|H(center)-H(tile)| < 120` (H = `0x6B910`); no bounds checks. [confirmed, static] ([PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md))
- `0x192750` Disease branch rejects dead units, surface mask `0x10000100` (wall/elevated) and buildings 40..47/74..78; no unit height comparison; immunity table includes lord and healer; damage 400/200/150 by radius, reduced 80/60/40% by owner healer counter. Roof rendering does not imply damage. [confirmed, static + native fixtures] ([PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md))
- Movement: per 31-tick event a step counter; new count `(RNG hi) & 0x3F`, direction `(RNG>>4) & 7` (0 N .. 7 NW); clouds cross walls, buildings, water; only invalid mask or tile flags 0x30 end them. [confirmed, static] ([PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md))

### Standing height

- `0x184FD0` computes base + signed correction: stairs +12, building-backed walls +40, IsBuilding +2, elevated building base + `0xC07C0`; raw tile height is not unit height (tunnel/siege/transition branches). [confirmed, static + 115546 isolated replays] ([PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md))

### Line of sight

- Wrapper tries forward then reverse. Core: tile = coord/8 without bounds checks; start +10 on flag `0x10000000`; initial unchecked steps from ctx value (0/100/0x1C); obstacle when line height < `0x6B990` height; result 0 blocked, else steps (max 1000) or 100000 for identical tiles. [probable, disassembly + fixture] ([LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md))
- Obstacle height: flag bit 28 -> `0xC07C0`; flag bit 10 building types 7..78 via byte table `0x6BAC4`/case table `0x6BAB4`: case 0 Hunter's hut type 7 (mode 1 none), case 1 types 40-44 type offset, case 2 types 45/46/61/74-78 offset +10, case 3 table `0x2E8C60`; non-building tiles with value > 3999 in `+0xA6F260` add 50/70 via `0x3357610`. Arrows (mode 0) collide with the Hunter's hut. [confirmed, disassembly] ([LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md))
- Hunter viewer height raw +30, target +26, visible for result 1..0x1B0; ranged order viewer +30 (45/60 for types 74/83), target +26, visible for result > 0. [probable, static] ([LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md))

### Healers

- Bedouin healer candidates (`0x181340`): IDs 1..`r_NextUnitId`-1, life 2, `+0x29C`==0, same alliance, `+0x2A0`!=0, owner gating; siege types 39/40/41/58/59/60/61/77 skipped. Selector checks injury, range (threshold from 0x20 world units), height and LOS; heal pulse +10 HP clamped; retargets at full HP. [probable, static] ([BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md))

### Monk movement

- Monk type 37 handler covers Fighting Monk and Temple Guard skins and has a separate walk/run branch. [probable, data-flow] ([function-claims.jsonl](../function-claims.jsonl))

### Unit audio

- Voice file = `0x2AD298[((situation+variant)*42)+column]`; situation 42 maps to 21; situation 33 (ladder push) only from the spearman handler with 4 s throttle, viewing player only; pikeman ladder lines and `knight_atks`/`sword_atks` are never requested; 29 native files lack managed entries; column 10 alternative table depends on two unknown globals. [probable, static] ([UNIT_VOICES.md](../UNIT_VOICES.md))
- Civilian comments use the real worker type and owner; Lord/Lady (55/56) excluded; combat grunts are SFX; Monk voice bank by religion. Unchanged selection never speaks. [confirmed, static] ([UNIT_VOICES.md](../UNIT_VOICES.md))
- Speech transport: one shared 128-byte filename; all record-18 speech entries in a PlayState use the last filename (coalescing). Managed `SFXManager.playSpeech` drops paths not in `speechFolders` silently; `MyAudioManager.PlaySpeech` loads asynchronously; clips unload on replace/finish/stop/StopAllGameSounds. [confirmed, static] ([UNIT_VOICES.md](../UNIT_VOICES.md))
- SFX record: kind 16, 21 signed shorts (42 bytes), sound [3], x/y [1]/[2], volume [11], pan [12], flag [13]; spatial emitter writes volume/pan -1. Managed case 16 uses explicit volume/pan only when both >= 0, else tile/viewport spatial attenuation. Six `MemBuffer` short[14,700,000] buffers; `Director.Update` consumes up to three per frame. `SFXManager.playSound(int, float, float, bool unstoppable)`. [confirmed, static] ([SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md))
- Arabian swordsman idle draw: state 1, sound 180 `FX_SHEATH_OUT` when animation advances, id&3 > 2, timer 11 and RNG; 179 `FX_SHEATH_IN` branch at `0x17012F` reachability unproven. Other handler sounds 164/163, 65, 145 at `0x16FB19`, `0x16FEA1`, `0x16FEE0`. [confirmed, static + in-game 2026-10-12] ([SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md))
- 91 direct SFX wrapper call sites across 28 soldier/support handlers are catalogued in [SOLDIER_AUDIO_SOURCES.json](../SOLDIER_AUDIO_SOURCES.json); the healer handler has none. [confirmed inventory, semantics inferred] ([SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md))

### Projectiles, effects pool and Extreme Troops

- Extreme Troops (setup field `+0x48`, nonzero) selects bounds 10000/6000; saved with the game, part of the checksum; quotas `(bound-100)/slots-40` (trail branch can double the second quota if bound != 10000). [confirmed, static] ([EXTREME_TROOPS.md](../EXTREME_TROOPS.md))
- At the larger bound, kind-3 afterimages and non-kind-1 type-5 effects are not allocated; simulation of the shot itself is unchanged. Kind 3 on arrows/bolts/rocks etc. (launch types 01,02,03,04,07,08,14,17,18,19,21,22,24,25 hex). Type-6 copies: 100% / 68.75% / 34.375% opacity, removed after age 3. Steam HD/HD Extreme have no such early return (bound 3000/6000, 4 alpha steps). [confirmed, static + runtime] ([EXTREME_TROOPS.md](../EXTREME_TROOPS.md))
- Pool pressure: ordinary slots 25..bound-1, slots 1..24 reserved (economy indicators 0x28/0x29). Arrows, bolts, rocks, disease clouds retry from 25 and can reclaim records of types 0x09, 0x15, 0x1B, 0x1E, 0x1F (not type 6); new ground fire 0x09 and ballista bolt 0x14 return 0 when exhausted. [confirmed, static] ([EXTREME_TROOPS.md](../EXTREME_TROOPS.md#effect-allocation-under-pressure-2026-11-23-vanilla-facts))
- Extreme outposts: group-wait floor 100 vs 600, group acceleration +100 instead of +33 (individual +4 unchanged), predicate also true for trail type 0. [confirmed, static] ([EXTREME_TROOPS.md](../EXTREME_TROOPS.md))
- Managed render: `GameMap.processTestMap` -> `GameMap.addUpdateFly`; render ID slot+10000 is a shadow; sort order `-20000 + row*49 + 15`. [confirmed, static + runtime] ([EXTREME_TROOPS.md](../EXTREME_TROOPS.md))

### Extreme Powers

- Command 1068 power IDs 0..7 (arrow volley, heal, spearmen, engineers, macemen, gold, knights, rock volley), unlocked/cost `636*(id+1)` mana, mana max 7000, regen +1 when tick%3==2 (Extreme Troops skips the modulo gate). [confirmed, static] ([EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md), [EXTREME_TROOPS.md](../EXTREME_TROOPS.md))
- Effects: volley damage 6000 radius 6 arrows / 18000 radius 9 rocks; heal 8000 radius 6; spawns type 24 x20, 30 x14, 26 x20, 28 x10 via `0x1264D0`; gold 1000 + (signed RNG mod 1500). Mana is deducted even if spawning created no units. Completion sounds 0xCF heal, 0x104 spawns, 0x105 rock volley, only for the local player. No unit-target pick entry identified. [confirmed, static] ([EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md))

### Instruction-level anchors (for re-verification and hook placement)

- Siege ammo: catapult `0x153111` load, `0x15311A` TEST, `0x15311D` JLE -> `0x15312D`, `0x15311F` DEC, `0x153122` store; trebuchet `0x1541CE/0x1541D7/0x1541DA` (JLE -> `0x154293`), DEC `0x1541E0`, store `0x1541E9`; `0x154375/0x15437E/0x154381` (JLE -> `0x15439E`), DEC `0x154383`, store `0x154386`; refill `0xD5649` (MOV EBX,20), `0xD5651` (MOVSX), `0xD5654` (SUB). Raw words 32768..65535 are negative; 32760+20 = 32780 = -32756 signed. [confirmed, disassembly] ([SIEGE_AMMUNITION_SIGNEDNESS.md](../SIEGE_AMMUNITION_SIGNEDNESS.md))
- Crew search CMP/JNE span ends: catapult `0x152599`, trebuchet `0x153B09`, mangonel `0x154DCC`, tower `0x15F287`, ram `0x160839`, shield `0x161558`, ballista `0x16222C`, Arab ballista `0x172259`; anchors R11-0x8F8 (tower R11-0x6E6, mangonel/ballista R8+R9). No interior entries among 4478 functions. Engineer parking writes 105 at `0x14B0C7`, `0x14BFC7`, `0x14C391`; reached-parking path `0x14B4E4` writes direction 4 at `0x14B4FE`. [confirmed, static] ([SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md))
- Lord rescue checks in `0x182B00`: `0x182EE6`, `0x182EF5`, `0x182F01`, `0x182FAB..0x182FB8`; lineup RVA `0x8574C44` identified by LEA at `0x28A7D`. Sources also cite `0x107390` (tree helper). [confirmed, disassembly] ([LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md))
- Selection output checks `0x19DEE0`, `0x19DEEA`, `0x19DEEF`, `0x19DEFE`; writes `0x19DF07`, `0x19DF14`, `0x19DF31`. Double-click branch `0x8F01B`, `0x8F02F`, `0x8F043`, `0x8F04A`, `0x8F05A`, `0x8F062`, `0x8F07C`, `0x8F084`. [confirmed, disassembly] ([KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md))
- Effect allocator: forward scan `0x9B366..0x9B39E`; launch selector `0x9BA24` / destinations `0x9BA1C`; reclaim branch `0x9B403`, eligibility `0x9B420..0x9B44F`, return-zero `0x9B45F`, removal and allocation `0x9B480..0x9B498`; fire-arrow remap `0x9B3D4`, `0x9B821`, kind-1 override `0x9B9D6`; type-5/6 bound reads `0x9BE8F`/`0x9BFCF`; flight update after continuation at `0x9FEF0`. [confirmed, disassembly] ([EXTREME_TROOPS.md](../EXTREME_TROOPS.md))
- Direct code references to unit bound `0x3668E34`: `0x593B, 0x8D6E, 0x9140, 0x955A, 0x15D7D, 0x15FC2, 0x20873, 0x2257B, 0x26B7E, 0x26B94, 0x87D3F, 0x8B0FF, 0x948A9, 0x96D91, 0x96E09, 0xAC4F4, 0xACD91, 0xCBD9C, 0xCBDA8, 0xCBDF3, 0xCBE37, 0xCBE4C, 0xCBE6A, 0xCDD97, 0x17FEF8, 0x1A0FB4`. To effect bound `0x3668E38`: `0x594F, 0x8D78 (64-bit default store), 0x9151, 0x9564, 0x15D97, 0x15FF2, 0x2256F, 0x26B88, 0x26B9E, 0x87D56, 0x8B109, 0x96D9B, 0x96DFF, 0x9B36A, 0x9B40C, 0x9BE8F, 0x9BFCF`. Extreme counter gate `0xCDD97..0xCDDC5`. Not a proof that indirect consumers are absent. [confirmed, disassembly] ([EXTREME_TROOPS.md](../EXTREME_TROOPS.md))
- Disease neighbour gates `0x9CB6F..0x9CB89` and `0x9CC3D..0x9CC57`; `0x192750` interior site `0x192809`. [confirmed, disassembly] ([PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md))
- LOS ctx stores `0x9E4D3`, `0x9E531`, `0x9E684`, `0x9E6D2`, `0x9E93A`, `0x9E9BB`, `0x9EC17`, `0x9EC9C`; Hunter target `+0x1A` at `0x18B052`, viewer `+0x1E` at `0x18B070`, visibility test `0x18B0A7`; ranged order viewer store `0x18E9DE`, target `0x18ED3C`, test `0x18ED71`. Obstacle-height case targets `0x6BA3D` (case 0), `0x6BA30` (case 1), `0x6BA22` (case 2), `0x6BA41` (case 3); no branch into `[0x6BA0C, 0x6BA1D)`. [confirmed, disassembly] ([LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md))
- Building spawn HP chain `0xB4C89`, `0xB4C92`, `0xB4C9B`. Fireman detour boundary `0xB8F4A` (end of two 5-byte entry instructions). Arabian state-1 target `0x16FFE9`. [confirmed, disassembly] ([ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md), [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md), [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md))
- Other helpers: `0x199CD0` structure-exit/state handling (not a buffer clear); `0x118E00` group-move related (audited with `0x11B520`); `0x4660` empty, called every civilian-comment run; `0x18B880`, `0x18CE60` idle-reason/Hunter query helpers emit no speech. [confirmed, static] ([FIRE_EFFECTS.md](../FIRE_EFFECTS.md), [UNIT_VOICES.md](../UNIT_VOICES.md))
- Managed spectator startup removes the human member before map load (`FRONT_Multiplayer.cs:5575`, `10045..10049`); real MP starts at `Platform_Multiplayer.cs:3923`, offline skirmish at line 9963. [confirmed, managed static] ([ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md))

### Steam HD and HD Extreme (32-bit, image base 0x400000)

- `Stronghold Crusader.exe` (bound 3000, type-6 call `0x408D0A` -> `0x4083A0`) and `Stronghold_Crusader_Extreme.exe` (bound 6000, `0x408D1A` -> `0x4083B0`), both file version 1,1,0,1. Afterimage table `0x5B6760 + 4*(type-10)` via initializer `0x4011D0`; template interval 3 and terminal age 4 at `0x4013CA..0x4013DC`; age handler `0x401D10` writes transparency 0/8/16/24; blend at `0x4EBB21`. Main allocator `0x404AF0`, cursor reset to 25 at `0x4087F6`, type-6 saturated return at `0x4084A7`, launch jump table `0x405048/0x405050`, reclamation dispatch `0x4050B0/0x4050B8` (types 0x09, 0x15, 0x1B, 0x1E, 0x1F). No DE-style wide-pool early return; HD Extreme still allocates trails in the shared pool. [confirmed, disassembly] ([EXTREME_TROOPS.md](../EXTREME_TROOPS.md))

## Cross-topic links

- Route builder, portals, movement masks, moat traversal: see the pathing topic (`../MOAT_TRAVERSAL_ALL_UNITS.md`, [UNIT_LOAD_PLACEMENT_CONTRACTS.md](../UNIT_LOAD_PLACEMENT_CONTRACTS.md) for quotas and allocation failure).
- Outposts timing: [OUTPOSTS.md](../OUTPOSTS.md). Map tick vs periodic clock: [PERIODIC_GOLD.md](../PERIODIC_GOLD.md), [GATEHOUSE_QUERY_PASS.md](../GATEHOUSE_QUERY_PASS.md).
- Hunter prey use of LOS: [HUNTER_PREY_RULES.md](../HUNTER_PREY_RULES.md). Unity asset pipeline for trails: [UNITY_ASSET_PIPELINE.md](../UNITY_ASSET_PIPELINE.md).
- AI woodcutter orchard components: [AI_WOODCUTTER_BUILD.md](../AI_WOODCUTTER_BUILD.md). Chore transport: [CHORE_SYSTEM.md](../CHORE_SYSTEM.md).

## Open questions and contradictions

- Counter at player `+0x137DD0` in `0xCDB20` (EXTREME_TROOPS: meaning unconfirmed) is almost certainly the Extreme Powers mana regeneration block identified in EXTREME_POWERS_FINDINGS (same block `0xCDD87..0xCDDCB`, limit 7000, tick%3==2). The two documents use different base expressions (`+0x137DD0` vs local `+0x3950` = resources `+0x39D4`); treat as probable until reconciled.
- FIRE_EFFECTS lists the effect manager as slots 1..5999 (static loop ranges); the effective bound is 3000 in Standard (EXTREME_TROOPS). Both are true for their scope.
- Disease search radius 30 (`0x9F700`, apothecary search) and treatment distance < 7 (`0xA0470`) are different gates, not a contradiction.
- Open: civilian exclusion in `0x181340`; flag meanings in LOS (`0x400300`, `0x400200`, bit 7, bit 28); two core-to-helper LOS calls whose `r8d` comes from `eax`; unit names for LOS viewer offsets 45/60 (types 74/83); reachability of the sword-sheathe sound 179; `ladder_atkw` reachability; column-10 voice switch globals; meaning of `0x3BC0` shared banks; volley projectile count/spread; unit-target pick entry for Extreme Powers; invalid-component Lord rescue never observed in gameplay.

## Touched by our mods

- BugfixesAndQoL: Lord return teleport guard at `0x19BA40`, nearest fireman reservation at `0xB8F40`, siege crew fixes, siege ammo restock, projectile trail replacement at `0x9BFC0`, recruit rally tracking, healer attack command. See [../mods/units.md#bugfixesandqol](../mods/units.md#bugfixesandqol).
- ExtraFeatures: plague lifetime/apothecary range/flag disease, monk always run, Bedouin healer targets at `0x181340`, knight replacement selection, shared enemy HP. See [../mods/units.md#extrafeatures](../mods/units.md#extrafeatures).
- APIShared: plague standing-height hooks, unit audio and voices, sight-blocking buildings. See [../mods/units.md#apishared](../mods/units.md#apishared).
- Pathfinding (former MoatMove): fire and plague avoidance. See [../mods/units.md#pathfinding-former-moatmove](../mods/units.md#pathfinding-former-moatmove).
- ExtremePowers API and testmods. See [../mods/units.md#extremepowers](../mods/units.md#extremepowers) and [../mods/units.md#testmods](../mods/units.md#testmods).

## Sources

- [BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md): Bedouin healer candidate lists, selector, heal pulse; RedBird entry contract.
- [ENEMY_HP_MULTIPLIER.md](../ENEMY_HP_MULTIPLIER.md): Enemy HP mode gate, team reference, HP initializers, building HP, arithmetic bounds, transfers; shared-HP mod note.
- [EXTREME_TROOPS.md](../EXTREME_TROOPS.md): unit/effect bounds, afterimage trails, effect allocation pressure, HD comparison; trail renderer mod results.
- [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md): fireman selector and state flow, extinguish, compound fires; reservation and detour contracts.
- [FIRE_EFFECTS.md](../FIRE_EFFECTS.md): fire effect records, pitch ignition, route storage, group move admission, fire lifetime, natural plague creation; MoatMove fire avoidance.
- [KNIGHT_SELECTION_TRANSFERS.md](../KNIGHT_SELECTION_TRANSFERS.md): unit creation/removal, selection fields and phases (mod-scoped document with Vanilla facts).
- [LINE_OF_SIGHT.md](../LINE_OF_SIGHT.md): LOS wrapper, core, obstacle heights, readers.
- [LORD_HOME_TELEPORT.md](../LORD_HOME_TELEPORT.md): Lord home relocation, invalid-component rescue, standing-tile causes; BugfixesAndQoL guard.
- [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md): plague damage path, height data, cloud life cycle; APIShared/ExtraFeatures/MoatMove integration.
- [RECRUIT_TRANSFORMATION.md](../RECRUIT_TRANSFORMATION.md): recruitment, transformation, employment, removal, migration; rally tracking contract.
- [SIEGE_AMMUNITION_SIGNEDNESS.md](../SIEGE_AMMUNITION_SIGNEDNESS.md): signed stone ammunition consumers and refill.
- [SIEGE_ENGINEER_CREW_TIMEOUT.md](../SIEGE_ENGINEER_CREW_TIMEOUT.md): crew timeout, fatal fire, harassment reserves, save sections, parking states, crew selection, live observations.
- [SITUATIONAL_UNIT_AUDIO.md](../SITUATIONAL_UNIT_AUDIO.md): SFX record path, Arabian idle sword draw; APIShared audio integration history (mostly mod-only after the first sections).
- [UNIT_VOICES.md](../UNIT_VOICES.md): voice dispatchers, speech filename transport, managed playback, civilian comments; APIShared Unit Voices.
- [function-claims.jsonl](../function-claims.jsonl): claims for `0x504F0`, `0x9A080`, `0x9F700`, `0x151090`, `0xB8F40`, `0xC07C0`, `0x184FD0`.
- [SOLDIER_AUDIO_SOURCES.json](../SOLDIER_AUDIO_SOURCES.json): 91 soldier SFX call sites.
- [EXTREME_POWERS_FINDINGS](../../../../../../Findings/EXTREME_POWERS_FINDINGS.md): secondary source outside the baseline (German), Extreme Powers native functions and values.

## Migration increment and cumulative progress

- At popularity10000, the canonical Skirmish table0x311E50 index20 is signed Int32 value40 (RVA0x311EA0, bytes28 00 00 00); existing positive-growth option gates may double the effective increment. Cumulative player+6C and effective signed+70 must be read separately; the ordinary4000 arrival threshold and eligibility gates remain decisive. [confirmed, static + canonical PE readback 2026-11-27] ([RECRUIT_TRANSFORMATION](../RECRUIT_TRANSFORMATION.md#migration-table-and-public-resource-aliases-27-november-2026-vanilla-facts))
- Migration uses the resource record with cumulative DWORD+6C and signed increment+70; D2C40 starts from player-manager+13446C, with resource-record stride583C. A table value or fixed short wait alone does not prove that a natural Peasant already exists. The external reader mapping is documented separately in the linked audit. [confirmed, native static] ([RECRUIT_TRANSFORMATION](../RECRUIT_TRANSFORMATION.md#migration-table-and-public-resource-aliases-27-november-2026-vanilla-facts))


## Free Peasant movement

- The type1 handler12A8F0 routes free Peasants through AI0/CE400 to24 actual Campfire places, AI7 follows that destination and AI8 normally animates at arrival. A place inside a Campfire building is not thereby ordinary building-free terrain. AI8 can later return to AI0 under its specific animation/Campfire/PCL conditions. [confirmed static,2026-11-27] ([audit](../RECRUIT_TRANSFORMATION.md#free-peasant-campfire-movement-and-genuine-accepted-steps-27-november-2026-vanilla-facts))
- Type AI runs before generic1855A0 in182B00. Successful196280 route publication and flags2 are distinct from a genuinely accepted Next step, which1855A0 publishes later. The audited successful free-Peasant request does not choose a new AI; the failed wall-current path can instead reach AI110. Do not infer an accepted walk or a free recruitable lifetime from a requested target alone. [confirmed static,2026-11-27] ([audit](../RECRUIT_TRANSFORMATION.md#free-peasant-campfire-movement-and-genuine-accepted-steps-27-november-2026-vanilla-facts))
