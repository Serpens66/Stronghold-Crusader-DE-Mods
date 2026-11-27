# Economy, workers and production [Scope: Vanilla]

Native hash FBCB9319 (full hash in ../../../../CURRENT.json). Summary state 2026-11-27. Check the installed DLL hash per CURRENT.md before using any address. Evidence links point to the audit documents; they stay authoritative.

Abbreviations in evidence links: TRAP = [TRAPPED_UNIT_RESCUE_AUDIT.md](../TRAPPED_UNIT_RESCUE_AUDIT.md) (assigned to topic pathing; its worker/production `[Vanilla facts]` sections are summarized here), WPB = [WORKER_PRODUCTION_BONUS.md](../WORKER_PRODUCTION_BONUS.md), HUNT = [HUNTER_PREY_RULES.md](../HUNTER_PREY_RULES.md). Offsets are manager-relative native offsets unless marked "public" (Script Extender typed view). Unit records: `0x67E8400 + id*0x490`, public view `+0x65C`. Building records: `0x64CCBB0 + id*0x32C`, public view `+0x5C`. Vegetation records: `manager + index*0x9C`, SE view `+0x18`. Player resource records: image `+0x379ADD0 + playerId*0x583C` (= SE `playerManager+0x1343FC+(playerId-1)*0x583C`, manager `0x366C210`). All game IDs are one-based.

## Overview

The economy runs inside the simulation tick `DLL_RunTick 0x86680 -> 0xCDE60 -> 0xD1AD0` (resource scheduler, 200-step cycle) plus the per-building dispatcher `0xC60F0` and the unit pass `0x182B00`, which dispatches worker handlers through table `0x321CB0`. Each worker role has its own state machine (AI state at unit `+0x918`, public `+0x2BC`); goods move into and out of physical storage records through the single primitive `0xB6100` (building-local add/remove with capacity), and player-level add/remove `0xB58B0`/`0xB7D80`. Output quantity is computed once per batch by the yield helper `0x18D940` from fear-level productivity and the native mode `0x8574B90`. Gold has a direct signed balance plus a pending incoming table delivered gradually by building updates. Market prices are a packed per-good table read by two 31-byte helpers. Popularity is recomputed per player in `0xCB090`. Trees and wild animals are separate pools (vegetation manager, herd/tribe records). Most behaviors below were confirmed statically; runtime confirmations are marked.

## Key functions

| RVA | Name/role | Notes | Confidence | Evidence |
|---|---|---|---|---|
| 0x86680 | `DLL_RunTick` | clears gift-message index, runs tick, exports via `0x19D960` | confirmed, disassembly | [ALLY](../ALLY_GOODS_TRANSFERS.md) |
| 0xCDE60 | game update | increments map tick `0x3665F58` (advancing branch only) then calls `0xD1AD0`; later `0xC60F0`, AIV `0x57330`, unit passes `0x182B00`/`0x187B30` | confirmed | [GOLD](../PERIODIC_GOLD.md), HUNT |
| 0xD1AD0 | resource/time scheduler | calls `0xBCEB0` recount, `0xCD850` productivity, `0xCB090` popularity, chicken `0xD29E0` at step `0xB4`, `0xCEF00`; SE context callback at `0xD1D4F` | confirmed | [POPP](../POPULARITY_PARAMETERS.md), HUNT |
| 0xBCEB0 | good/bad building recount | computes fear level per player | confirmed | [POPP](../POPULARITY_PARAMETERS.md) |
| 0xCD850 | productivity | `100-10*clamp(level,-5,5)` percent | confirmed | [POPP](../POPULARITY_PARAMETERS.md), WPB |
| 0xCB090 | popularity updater | components, plague modifier, clamp 0..10000 | confirmed | [POPP](../POPULARITY_PARAMETERS.md), [PLAGUE](../PLAGUE_POPULARITY.md) |
| 0xCCD20 | church/cathedral recount | called by `0xCB090` | confirmed | [POPP](../POPULARITY_PARAMETERS.md) |
| 0xCEA90 | ale coverage | clamps 0..100 | confirmed | [POPP](../POPULARITY_PARAMETERS.md) |
| 0x19D960 | play-state report exporter | recomputes popularity components with hardcoded constants; workshop product cache; status texts | confirmed | [POPP](../POPULARITY_PARAMETERS.md) |
| 0x180560 | fear damage factor | reads the same fear level; `((attr+20)*damage*5)/100` | confirmed | HUNT, TRAP |
| 0x18D940 | worker yield `(unit, base, bonusFlag)` | 17 handlers, 19 calls; +50 % when mode `0x8574B90 != 0` and flag | confirmed, disassembly | WPB, HUNT |
| 0xB6100 | building-local goods add/remove | `(mgr, bldId, bldGlobal, good, delta, cap, flag)`; returns 1 only on accepted positive capacity | confirmed | WPB |
| 0xB58B0 | player add to storage | rechecks capacity; dispatches `0xB5E90/0xC6EA0` (armoury), `0xB6000/0xBD470` (food), `0xB6100` (stacks) | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md) |
| 0xB7D80 | player remove from storage | reaches `0xC1CE0` total rebuild | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md) |
| 0xC1CE0 | rebuild stored totals | from living storage, preserves gold, excludes raw logs | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md), WPB |
| 0xC5AD0 | storage capacity query `(bldMgr, player, good)` | -1 none, 0 full, virtual class 0x28 returns 1,000,000 | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md) |
| 0xBFD30 | good-to-storage-type table lookup | table `0x2E76D0` | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md), [IDLE](../WORKSHOP_IDLE_DELAY.md) |
| 0xC6FC0 / 0xC6EA0 | armoury shared free space / acceptance | 50 places shared by all 25 goods columns | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md), TRAP |
| 0xBD470 | granary food acceptance | capacity 250 | confirmed | HUNT |
| 0xBD490 | stockpile column acceptance `(good, cap)` | rejects any other nonzero column 1..24 | confirmed | WPB |
| 0xBF9F0 | store search | partial store before empty, native index order (not nearest) | confirmed | WPB |
| 0xB9280 | goods-yard search with stock | reverse IDs, alive, same owner, stock >= 1, no reachability | confirmed | [IDLE](../WORKSHOP_IDLE_DELAY.md) |
| 0xB9AA0 | armoury search | | confirmed | HUNT, WPB |
| 0xC90E0 / 0xC9080 | storage access validation / body contact `<3` on both axes | | confirmed | WPB, TRAP |
| 0x188340 | arrival predicate | path flags 0 and portal 0; no distance check | confirmed | TRAP |
| 0x196280 | unit route builder/publication | see pathing topic | confirmed | TRAP |
| 0x196F80 / 0x196FD0 | hide+unlink worker / unhide+relink | | confirmed | TRAP, WPB |
| 0x19B930 | workplace access refresh (no route) | | confirmed | WPB |
| 0x193A70 | close work interval `(unit,1)` | rolling average | confirmed | [PAUSE](../WORKER_PAUSES.md) |
| 0x191050 / 0x191070 | rest gate wrapper / rest gate `(unit,state)` | enters AI `0x79` | confirmed | [PAUSE](../WORKER_PAUSES.md) |
| 0x185430 | rest executor (state `0x79`) | calls `0xBF0A0` | confirmed | [PAUSE](../WORKER_PAUSES.md) |
| 0xBF0A0 | positive-fear visit target selector | single caller `0x185430` | confirmed | [PAUSE](../WORKER_PAUSES.md) |
| 0x19B9D0 | animation-return helper | can re-enter `0x79` | confirmed | [PAUSE](../WORKER_PAUSES.md), [IDLE](../WORKSHOP_IDLE_DELAY.md) |
| 0xB6240 | building work reset `(bld, mode)` | mode 2 clears progress/phase, sets cycle mode 2; mode 3 selects work mode | confirmed | HUNT, WPB |
| 0xCA940 | registers employment demand | not a production credit | confirmed | WPB |
| 0xB6340 | employment (Mine special branch) | converts eligible Peasants to Miner1/Miner2 | confirmed | TRAP |
| 0xC60F0 | building dispatcher | production cadence; dispatches removal `0xB8310` | confirmed | HUNT |
| 0xCA110 | building normalization (`999` from Chore 55) | rewrites interval from type default | confirmed | HUNT |
| 0x22020 | checksum builder | buildings `0xD0..0x32B`, units incl. production words | confirmed | HUNT, WPB |
| 0xCEB10 / 0xCEB90 | market buy / sell price `(mgr, player, good, amount)` | `(base/5)*amount`, signed, unchecked | confirmed | [MARKET](../MARKET_PRICE_QUERIES.md) |
| 0xCEB00 / 0xCEBB0 | report-side price helpers | used by HUD preview at `0x19EC2E`/`0x19ECB0` | confirmed, disassembly | [MARKET](../MARKET_PRICE_QUERIES.md) |
| 0x29650 / 0x29700 | buy / sell execution | | confirmed | [MARKET](../MARKET_PRICE_QUERIES.md), [SESS](../EXTRAFEATURES_SESSION_CALLBACKS.md) |
| 0x3F270 | AI trade controller | alternates buy `0x3EC90` / sell `0x3EE10`; caller `0x2AE40` | confirmed | [WHEAT](../FIXES_WHEAT_STARTUP.md) |
| 0x3EC90 / 0x3EE10 | AI planned-goods affordability (buy) / AI seller loop | | confirmed | [MARKET](../MARKET_PRICE_QUERIES.md), [WHEAT](../FIXES_WHEAT_STARTUP.md) |
| 0xA4FE0 / 0xA50E0 / 0xA5200 | incoming-gold consumers (building updates) | table refs `0x2DEC20/28/30` | confirmed, disassembly | [GOLD](../PERIODIC_GOLD.md) |
| 0x81870 | `DLL_GameAction` | cases `0x427` gift, `0x428` request, `0x3EB` sleep, 1023 next weapon | confirmed export | [ALLY](../ALLY_GOODS_TRANSFERS.md) |
| 0x1A6F0 | Chore 113 handler | mode 1 -> `0xD78C0`, mode 2 -> `0xD7AD0` | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md) |
| 0xD7AD0 | ally gift `(recipient, good, amount, sender)` | 585 bytes, sole code caller `0x1A907` | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md) |
| 0xD78C0 | ally goods request execution | | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md) |
| 0x30290 | donor availability check (request) | | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md) |
| 0x1C8D0 | chat/notification ring writer | 10 entries of five Int16 | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md), [KILL](../KILL_REWARD_PERCENT.md) |
| 0x15A950 | unit state machine (Lord death reward block `0x15C48D`) | | confirmed (name candidate) | [LOOT](../DEFEAT_LOOT.md) |
| 0x107E00 | woodcutter tree selector | stage < 4, nearest Chebyshev | confirmed | [WTM](../WOODCUTTER_TREE_MATURITY.md), TRAP |
| 0x1086A0 / 0x1086D0 | standing-tree check (serial+felled) / same-global resource check | | confirmed | [WTM](../WOODCUTTER_TREE_MATURITY.md), TRAP |
| 0x107C90 | tree damage | | confirmed | [TREE](../TREE_RESPAWN.md) |
| 0x108040 | apple-tree selector | nearest alive type 15 stage 3, distance < 30 | confirmed | TRAP |
| 0x108800 / 0x108320 | vegetation updater / growth | bucket of 64 when `tick%8==4` | confirmed | [BLIGHT](../TREE_BLIGHT_DECAY.md) |
| 0x107390 | vegetation allocator/constructor | writes stage and initial progress, calls type handler | confirmed | [TREE](../TREE_RESPAWN.md), TRAP |
| 0x107970 -> 0x71D60 | vegetation deletion | clears occupancy, pathing, zeroes slot | confirmed | [TREE](../TREE_RESPAWN.md) |
| 0x107A00 | Tree Blight event | from `0x1045F0` case 143 and `0xF82B0` case 15 | confirmed | [BLIGHT](../TREE_BLIGHT_DECAY.md) |
| 0x12FC70 | Hunter update | | confirmed | HUNT |
| 0x18AF50 | hunter prey query | six callers | confirmed | HUNT |
| 0x18B640 | corpse search | | confirmed | HUNT |
| 0x18B470 | cow query (Tanner) | | confirmed | HUNT, [IDLE](../WORKSHOP_IDLE_DELAY.md) |
| 0xAE570 | hunter-hut update | meat +1 per phase, cap 6 | confirmed | HUNT |
| 0x117520 | herd dispatcher | | confirmed | HUNT |
| 0x11E150 | herd spawn `(mgr, art, x, y, h)` | | confirmed | HUNT |
| 0xD5870 / 0xD66B0 / 0xC52B0 | Chore 33 next-weapon handler / producer / bulk by type | | confirmed | HUNT, [WWF](../WEAPON_WORKSHOP_FIELDS.md) |
| 0xC7D50 | building sleep synchronizer | resets on disagreement with per-type flag | confirmed identity, probable semantics | function-claims.jsonl, [BUILDING_SLEEP_SELECTION.md](../BUILDING_SLEEP_SELECTION.md) |
| 0x52B0 / 0x87D0 | save writer / raw section reader | | confirmed | WPB, [MARKET](../MARKET_PRICE_QUERIES.md) |

Worker handlers (table `0x321CB0`; building handlers table `0x2DEAE0`): Woodcutter `0x12BC00`, Fletcher `0x12D230`, Hunter `0x12FC70`, Quarry grunt `0x1319A0`, Ox `0x132910`, Mason `0x131430`, Pitchman `0x1332B0`, Wheat farmer `0x133DD0`, Hops farmer `0x134F00`, Apple farmer `0x135E30`, Cattle farmer `0x136D40`, Miller `0x1377C0`, Baker `0x138850`, Brewer `0x139950`, Poleturner `0x13AAD0`, Blacksmith `0x13BDD0`, Armourer `0x13CF30`, Tanner `0x13E0B0`, Miner1 `0x14D6F0`, Miner2 (iron carrier) `0x14DAC0`. Buildings: quarry tether `0xAD180`, Mine `0xAD540`, pitch rig `0xADF70`, hunter hut `0xAE570`, wheat farm `0xA36D0`, hops farm `0xA3810`, cattle farm `0xA3A70` ([PAUSE](../WORKER_PAUSES.md), WPB, TRAP).

## Data and fields

| Record/manager | Offset | Meaning | Type/sign/unit | Confidence | Evidence |
|---|---|---|---|---|---|
| player resources | +0x50C | current gold (`r_TotalGoodsGold`) | Int32 signed | confirmed | [LOOT](../DEFEAT_LOOT.md), [GOLD](../PERIODIC_GOLD.md) |
| player resources | +0x4D0 | total goods table | Int32 | confirmed | [KILL](../KILL_REWARD_PERCENT.md) |
| player resources | +0x46C..+0x4CC | incoming goods table (25 entries incl. gold); gold at +0x4A8 | Int32 signed (SE says UInt32) | confirmed | [KILL](../KILL_REWARD_PERCENT.md), [GOLD](../PERIODIC_GOLD.md) |
| player resources | +0x3C | goods-notification flag (set 2) | Int32 | confirmed | [KILL](../KILL_REWARD_PERCENT.md) |
| player resources | +0x2134 / +0x212C / +0x2130 / +0x2180 | signed fear level / bad count / good count / population | Int32 | confirmed | [POPP](../POPULARITY_PARAMETERS.md) |
| player resources | +0x94 / +0x1D4 | `r_KeepId` / `r_KeepDoorId` (eligibility > 0 for popularity) | Int32 | confirmed | [POPP](../POPULARITY_PARAMETERS.md) |
| player resources | +0x2194 + type | per-type building sleep flag (91 bytes) | byte | confirmed | [BUILDING_PAUSE_UI.md](../BUILDING_PAUSE_UI.md) |
| image | 0x379CF04 / 0x379CEEC (+player*0x583C) | fear level cell / cached productivity percent | Int32 | confirmed | WPB |
| image | 0x379B278 / 0x379B2DC (+player*0x583C) | incoming gold / current gold | Int32 | confirmed | [GOLD](../PERIODIC_GOLD.md) |
| manager `0x366C210` | +0x12EC20 (+player slot) | popularity accumulator | Int32 | confirmed | [PLAGUE](../PLAGUE_POPULARITY.md) |
| player manager | +0x1817B8 / +0x1817BC | buy / sell base price per good, stride 8 (VA `0x1837ED9C8`, RVA `0x37ED9C8`) | Int32 | confirmed | [MARKET](../MARKET_PRICE_QUERIES.md) |
| image | 0x3327B0 | 20 good IDs used by Lord-defeat loot | table | confirmed | [KILL](../KILL_REWARD_PERCENT.md) |
| image | 0x2CEC90 | 20-entry goods/category table of AI seller (raw pitch 7 before wheat 9) | table | confirmed | [WHEAT](../FIXES_WHEAT_STARTUP.md) |
| image | 0x2E5FA0 | per-good stockpile capacity (good1 logs = 48) | table | confirmed | WPB |
| image | 0x2E76D0 | good to storage building type (Flour 16 -> goods yard 10, Bread 10 -> granary 19) | table | confirmed | TRAP |
| image | 0x87EED98 (+winner*4) | Lord-kill award statistic | Int32 | confirmed (corrected 2026-10-27) | [KILL](../KILL_REWARD_PERCENT.md) |
| image | 0x8574B90 (manager `0x8574320+0x870`) | native mode: 0 single player/campaign, 99 local skirmish, 1 network MP | Int32 | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md), HUNT |
| image | 0x8574320 + 0x275A00 / +0x275A08 | message ring index / five-short records | Int16 | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md) |
| image | 0x905B90 (VA `0x180905B90`) | per-sender gift-receipt timer, stride 4 | UInt32 ms | confirmed | [ALLY](../ALLY_GOODS_TRANSFERS.md) |
| unit | +0x918 (public +0x2BC) | AI state; DWORD form `saved<<16 | state` e.g. `0x5006D` | Int16 pair | confirmed | HUNT, WPB |
| unit | +0x95C / +0x95E (public +0x300/+0x302) | yield remainder / extra output | Int16 signed (SE UInt16) | confirmed | WPB |
| unit | +0x9E0 (public +0x384) | carried goods (cargo) | Int16 | confirmed | WPB |
| unit | +0x990 / +0x992 / +0x994 / +0x9C0 | workplace ID / home / home / workplace global | | confirmed | TRAP, [IDLE](../WORKSHOP_IDLE_DELAY.md) |
| unit | +0x98E / +0x6F8 (public +0x9C) | selected store ID / store global lifetime | | confirmed | TRAP |
| unit | +0x700 (public +0xA4) | role context: Woodcutter vegetation serial, Hunter target context | | confirmed | TRAP |
| unit | +0x9A0 | selected vegetation native index (0 = none) | | confirmed | TRAP |
| unit | +0x9EC (low word) / +0x9EE | apple picking counter / slot index (Miller registration, cow animal slot) | | confirmed | TRAP, HUNT |
| unit | +0x956 | work mode (1 = entertainment) | | confirmed | [IDLE](../WORKSHOP_IDLE_DELAY.md) |
| unit | +0x9D8 | animation variant (SE `r_TicksSinceRestPulse`) | | confirmed | [IDLE](../WORKSHOP_IDLE_DELAY.md) |
| unit | +0x306 / +0x308 / +0x30A / +0x370 (public) | rest attempt counter / carry ticks / signed duration / visit mode | Int16 | confirmed | [PAUSE](../WORKER_PAUSES.md) |
| unit | +0xA47 | Fletcher material need (2 bows, 3 crossbows) | byte | confirmed | WPB |
| unit | +0xAA4 (public +0x448) | prey reservation countdown (also war-dog index, Engineer flag) | Int16 signed | confirmed | HUNT |
| unit | +0x920 | dead/timer counter | Int16 | confirmed | HUNT |
| unit | +0x986 | fade byte | byte | confirmed | HUNT |
| unit | +0x8F8 | death marker (SE `r_IsKilledByProjectile`, public +0x29C; also melee) | UInt16 | confirmed | [IDLE](../WORKSHOP_IDLE_DELAY.md) |
| unit | +0xA0C (Hunter) | 0 deer, 1 other prey (animation) | | confirmed | HUNT |
| unit | +0x58E (tribe, abs `0x7CC6CAE`) | rabbit-attack permission | byte | confirmed | HUNT |
| unit (cow) | +0x990 / +0x9C0 / +0x9EE / +0x9F0 | farm ID / farm global / animal slot / farm ID | | confirmed | HUNT |
| building | +0x10 / +0x14 / +0x18 (public) | production interval / last-step counter / current tick counter | | confirmed | HUNT |
| building | +0x20 / +0x2E / +0x2C / +0x26 / +0x118 (public) | production frame / increment / completion / cycle mode / phase word | | confirmed | HUNT |
| building | +0x84 / +0x88 (native) | pitch rig completed-animation DWORD / completion | | confirmed | WPB |
| building | +0x17C + 4*good | local goods (wood +0x184, hides +0x190, iron +0x194) | Int32 | confirmed | [IDLE](../WORKSHOP_IDLE_DELAY.md), [WWF](../WEAPON_WORKSHOP_FIELDS.md) |
| building | +0x1AC / +0x150 public | hunter-hut meat (not the second worker global) | Int32 | confirmed | HUNT |
| building | +0x1FA + 2*slot / +0x204 + 4*slot (public +0x19E/+0x1A8) | employee IDs / globals (slot 0 regular worker) | | confirmed | HUNT, TRAP |
| building | +0x214 + 2*slot (public +0x1B8) | active-worker WORD array (Miller, Mine) | | confirmed | TRAP |
| building | +0x198 | `r_TotalWorkersRequired` | Int32 signed | confirmed | [PAUSE](../WORKER_PAUSES.md) |
| building | +0x1A8 native / +0x14C public | cattle-farm readiness (SE `r_CheeseAmount`) | UInt32 | confirmed | TRAP |
| building | +0x224 native / +0x1C8 public | stored farm plot list | packed IDs | confirmed | TRAP |
| building | +0x2EA / +0x2EC (public +0x28E/+0x290) | latched current product / next product (SE names reversed) | Int16 | confirmed | [WWF](../WEAPON_WORKSHOP_FIELDS.md) |
| building | +0x2F2 / +0x2F3 | farm pause byte / animal counter | byte | confirmed | HUNT |
| building | +0x2C8 / +0x2C4 (public +0x26C/+0x268) | quarry tether Ox ID / global | | confirmed | WPB |
| building | +0x304 (typed +0x2A8) | demolition group key | | confirmed | HUNT, TRAP |
| vegetation | +0x68 / +0x6A / +0x70 | alive / type / serial | UInt16 | confirmed | [WTM](../WOODCUTTER_TREE_MATURITY.md) |
| vegetation | +0x86 / +0x88 | X / Y (SE +0x6E/+0x70) | | confirmed | TRAP |
| vegetation | +0x96 / +0x98 / +0x9A | health / max / felled | Int16 signed | confirmed | [BLIGHT](../TREE_BLIGHT_DECAY.md) |
| vegetation | +0x9C / +0x9E / +0xA4 / +0xA8 / +0xAC | resources / max / stage / progress / bucket | stage, progress Int32 (SE UInt32 public +0x8C/+0x90) | confirmed | [BLIGHT](../TREE_BLIGHT_DECAY.md), TRAP |
| herd record `0x7CC6720` | stride 0x688: +0x2C owner, +0x34 global, +0x40 state, +0x42 kind, +0x5A leader, +0x5C count, +0x60 member bits, +0x5B4 waypoints, +0x5DC/+0x5DE route index/count, +0x5F4 flight | | | confirmed | HUNT |
| sector grid `0x3395E10` | 80x80 sectors of 10x10 tiles, stride 0xA0: +0x24 camels, +0x28 predators, +0x48 non-animals and fleeing herds, +0x54 rabbits | counters | | confirmed | HUNT |

## Behavior

### Storage primitive and goods accounting
- `0xB6100` checks target global ID and local+amount against the passed capacity as a whole, updates local and player totals, visuals and total storage, and returns 1 only on the accepted positive-capacity branch; capacity 0 clears the local field and returns 0; add 0 is a dry run. [confirmed, static] (WPB)
- Worker callers decrement cargo and update statistics after the final `0xB6100` call without testing its result; carry decrease, statistics or an SE drop-off event never prove accepted quantity. [confirmed, static] (WPB)
- Final production `0xB6100` call sites: Wood `0x12CA0C`, Hunter `0x130AFF`, Pitch `0x1338E0`, Hops `0x1359A3`, Apple `0x1368F5`, Cattle `0x1373A7`, Iron `0x14E01C`, Wheat `0x134A40`, Miller `0x13827D`, Baker `0x1393C7`, Ox `0x132E4B`, Quarry grunt `0x132502`, Brewer `0x13A51F`, Fletcher `0x12DD21`, Poleturner `0x13B70A`, Blacksmith `0x13C898`, Armourer `0x13DA12`, Tanner `0x13EF43`. Grunt `0x131F0D`, Miller `0x137D86` and Baker `0x138E58` negative calls are pickups/ingredient withdrawals. [confirmed, static] (WPB)
- Raw logs (good 1): Woodcutter stores them in its hut (cap 3) and converts to planks; `0xBFD30` still maps good 1 to stockpile type 10 with capacity 48 while `0xC1CE0` excludes it from usable totals, so adding logs through `0xB58B0` can fill an unusable tile. [confirmed, static] (WPB)
- Capacity: armouries share 50 places across all 25 goods; granaries 250 food; goods-yard stacks use per-good table; nonexistent storage -1, full 0. Refined pitch maps to raw pitch for checks. [confirmed, static] ([ALLY](../ALLY_GOODS_TRANSFERS.md))
- Native stockpile constructor `0x76E80` allocates four 2x2 records via `0xB47E0` at origins `0x2D2FB8` (0,0),(3,0),(0,3),(3,3) with flags OR `0x502`, plus a nine-cell central cross from `0x2D3060` with flags OR `0x102` and BuildingID 0; cells get effective elevation +10; logical surface is offsets 0..4. A BuildingID 0 cell may still be stockpile surface. [confirmed, disassembly; runtime-corroborated runs 140/142] (TRAP)
- Farm-plot cleanup `0xB8ED0` scans all 36 stored plot entries under flag `0x04000000`; `0xB8E00` finds preparation areas under flag 4; demolition `0xC43A0 -> 0xB8310 -> 0x61FC0` can remove distant buildings sharing the group key; apple removal uses preparation size 11 vs creator size 10. [confirmed, static] (TRAP)

### Gold and incoming goods
- Pending incoming gold is delivered by building updates `0xA4FE0`, `0xA50E0`, `0xA5200`: at `0xA506B`/`0xA5183`/`0xA52A3` zero/negative pending is skipped; positive moves 100 if >1000, 10 if >100, else 1, from pending to current gold, then display words and dirty flag. Skipped in native modes 1 and 6 and under global update guards. Gradual, not an immediate or fixed-delay credit. [confirmed, disassembly] ([GOLD](../PERIODIC_GOLD.md))
- The map tick `0x3665F58` (Int32) is incremented by `0xCDE60` only in its advancing branch (`mpFrameSkip == 0`), right before `0xD1AD0`; managed `Director.getSimTickCount()` is a different local counter reset in `startSimThread`. [confirmed, static] ([GOLD](../PERIODIC_GOLD.md))

### Market
- `0xCEB10`/`0xCEB90` (31 bytes each, single block, no internal branch): sign-extended good indexes the packed pair at manager `+0x1817B8`/`+0x1817BC`; result `(basePrice/5)*amount`, truncating division then unchecked Int32 multiply; playerId unused; no side effects. [confirmed, disassembly] ([MARKET](../MARKET_PRICE_QUERIES.md), function-claims.jsonl)
- Callers: buy execution `0x29650` (goods addition succeeds before gold/stat deduction), AI affordability `0x3EC90`, sale `0x29700`, ally request `0xD78C0`, ally gift `0xD7AD0` (valuation only). Replacing a price with 0 changes cost, it does not cancel the trade. [confirmed, static] ([MARKET](../MARKET_PRICE_QUERIES.md), [SESS](../EXTRAFEATURES_SESSION_CALLBACKS.md))
- HUD report preview: `0x19EC2E` stores AX buy lot price, `0x19EC82..0x19EC92` multiplies by 5 in 16 bits (25-good lot), so buy base must be <= 6553; sell path `0x19ECB0/0x19ECB5` allows base <= 6554 (6555 gives 32775). [confirmed, disassembly] ([MARKET](../MARKET_PRICE_QUERIES.md))
- Price table persistence: it lies in save section `0x3FF` (table entry `0x336B20`, pointer `0x37ED180`, length `0x82474`), so .sav and editor .map store it. Reader `0x87D0` callers: `0x85496` (`DLL_LoadSaveGame` `0x853F0`), `0x94B15` (skirmish start `0x94350`), `0x1006D7` (other map starts `0x100530`). Both map-to-play starts then copy default prices (`0x100530` via `0x102FC0` at `0x1023C3`; `0x94350` loop `0x967C0..0x9696E`), so map-stored prices never reach a played mission; saves keep them. Default prices are separate from this mutable table. [confirmed, static] ([MARKET](../MARKET_PRICE_QUERIES.md))
- AI seller `0x3EE10` (called from controller `0x3F270`, scheduler `0x2AE40 <- 0x57330 <- 0xCDE60`): saves EDX player to its home slot; at `0x3EE27` R9 = player*0x583C but each goods iteration overwrites R9D (`0x3F0E3`, `0x3F1FA`) and the back edge `0x3F215` returns to `0x3F0C3` without recomputing it; a successful sale leaves through `0x29700`. [confirmed, disassembly] ([WHEAT](../FIXES_WHEAT_STARTUP.md))
- `0xCDE60` is also reached from load helper `0x71670` and editor export `DLL_SetMapEditorParam` `0x87460` (outside `EngineInterface.threadLock`). [confirmed, static] ([WHEAT](../FIXES_WHEAT_STARTUP.md), [TREE](../TREE_RESPAWN.md))
- AI market lifecycle and sell thresholds: see [AI_MARKET_LIFECYCLE.md](../AI_MARKET_LIFECYCLE.md), [AI_SELL_THRESHOLDS.md](../AI_SELL_THRESHOLDS.md) (topic ai).

### Ally gifts and requests
- UI: `HUD_AlliesPanel` offers 21 goods (indices 2,3,4,6,7,9..24; no logs, hides, refined pitch), integer amounts incl. gold, six ally slots. [confirmed, static] ([ALLY](../ALLY_GOODS_TRANSFERS.md))
- Gift: `DLL_GameAction` case `0x427` checks sender balance and recipient capacity `0xC5AD0` (caps the amount, can be 0), gold uses signed balance only; stages Chore 113 mode 2 (Int16 mode + four Int32 = 18 bytes, payload `(recipient, good, amount, sender)`); return 0 = local queue submission. [confirmed, static] ([ALLY](../ALLY_GOODS_TRANSFERS.md))
- `0xD7AD0` performs no alliance, defeat, positive-amount or overflow validation. Gold: capped by sender funds, direct add/sub, no overflow check, no incoming credit, bypasses SE add/sub resource events. Goods: refined pitch -> raw pitch, `0xB58B0` add (result ignored) then `0xB7D80` subtract of the original amount; valued twice via `0xCEB90` (statistics, not two gifts). [confirmed, static] ([ALLY](../ALLY_GOODS_TRANSFERS.md))
- Enqueue-time checks reserve nothing: if production fills recipient space before execution, the add fails but the sender is still debited (gift and request alike). Statically reachable, not reproduced live. [confirmed, static] ([ALLY](../ALLY_GOODS_TRANSFERS.md))
- Request: case `0x428` checks requester capacity and donor via `0x30290`; failures return 1; success queues Chore 113 mode 1 `(requestedPlayer, good-1, amount, requester)` via `0x23990`; ready scan `0x21EE0`. `0xD78C0` human branch stores Int16 good/amount and local feedback; AI non-gold branch credits via `0xB58B0` then debits donor via `0xB7D80` regardless. [confirmed, static] ([ALLY](../ALLY_GOODS_TRANSFERS.md))
- Follow-through: clears a matching request, AI response `0x3F870` (validates Lord via `0x187E60`, speech `0x103AA0 -> 0x103290`, 20 s global throttle, 10-entry queue). [confirmed, static] ([ALLY](../ALLY_GOODS_TRANSFERS.md))
- Messages: both gift-text branches require mode `0x8574B90` not 0 and not 99 (`0xD7C49`). Recipient branch `0xD7C58..0xD7C8E` needs local recipient and per-sender timer (`0xD7BD2..0xD7C44`, 30 s wall clock, shared by all goods), writes category 10 via `0x1C8D0` at `0xD7C7B` and speech `ally_request09.wav` (`0xEDF0`); sender branch `0xD7C93..0xD7CB2` writes category 15 without timer. Presentation event16 via `0x2940 -> 0x1A4190` (subtype 36 iron/gold, 39 weapons, else 35). Timer init `0xD4DF0` (from reset `0x87D10`) sets current time, no 30 s suppression. [confirmed, disassembly] ([ALLY](../ALLY_GOODS_TRANSFERS.md))
- Gate writes: `DLL_PreInitMap_Multiplayer` `0x86270` sets 99 for skirmishGame (local SP skirmish) and 1 for network (`0x86352..0x86377`); resets `0x87D10`, `0x8ADC0`, `0x211A0` write 0 (`0x211A0` also sets GameType `0x3665F28`=3); map load `0x94350` restores the old value; `DLL_StartMultiplayerGame` `0x87880`/`0x878B0` and `DLL_SetEditorPlayer` `0x872E0` (viewpoint write `0x872FC`) do not change it. So fresh local skirmish (GameType 3, gate 99) produces no gift chat rows. Gate is distinct from GameType `0x3665F28`, skirmish origin `0x3669040`, skirmish mode `0x88E6A04` (SE anchors `0x4078C`, `0x28C5A`, `0xD14E0`). [confirmed, disassembly] ([ALLY](../ALLY_GOODS_TRANSFERS.md))
- `0x1C8D0` writes `(category, self, other, amount, good)` as Int16 (amount truncated) into a 10-entry ring, no dedup; `DLL_RunTick` clears the index, exporter copies count to output `+899` and arrays to `+89C/8B0/8C4/8D8/8EC`; 11 writes per tick leave only the overwritten first record. Managed `FatControler.GUICheck` consumes only in MP/skirmish; HUD keeps 5 rows, 20 s expiry. [confirmed, static] ([ALLY](../ALLY_GOODS_TRANSFERS.md))

### Lord defeat loot
- In `0x15A950`, one-shot death branch (excluding marker `+0xA69 == 99`): loser ID loaded at `0x15C44F`, credited winner at `0x15C458`; `0x15C477` skips to `0x15CDC3` without winner, `0x15C47D/0x15C487` skip for the special flag. Reward block `0x15C48D`: gold = truncated 25 % of loser gold clamped 500..100000, credited directly; 25 % of 20 goods totals (IDs from `0x3327B0`, excluding gold, logs, hides, flour) added to winner incoming table; victory counter `INC [R10+RBP+0x27AD01]` at `0x15C4DB`. [confirmed, disassembly] ([LOOT](../DEFEAT_LOOT.md), [KILL](../KILL_REWARD_PERCENT.md))
- At `0x15C4EA`: RBP winner, RBX/R14 loser, R11 = loser*0x160F, R12D clamped gold; first gold write `0x15C4FC`. Cleanup `0x15C980` (needs RCX = image+`0x366C210`) zeroes the loser's incoming table `0x46C..0x4CC` (not totals) at `0x15C98F..0x15C9FD`, adds R12D to statistic `0x87EED98`+winner*4 at `0x15C997`, mode guards `0x15C99F..0x15CA25`, type-8 victory message at `0x15CA3B`, type-1 gold message `0x15CA40..0x15CA6F`, follow-up `0x3F640` call at `0x15CA9B`. [confirmed, disassembly] ([KILL](../KILL_REWARD_PERCENT.md))
- Gold message splits amount base 30000 (remainder, quotient) into Int16 fields; managed reconstructs `[n,3]+[n,4]*30000`. [confirmed, static] ([KILL](../KILL_REWARD_PERCENT.md))

### Popularity and fear factor
- `0xCB090` runs for players 1..8 when `+0x12EC54`/`+0x12ED94` (public `r_KeepId`/`r_KeepDoorId`) > 0; special app modes assign fixed values and small-population protection assigns 7500, bypassing components. [confirmed, static] ([POPP](../POPULARITY_PARAMETERS.md))
- Components (report points; internal = points*25; total 0..10000 internal = 100 per displayed point): rations -8/-4/0/+4/+8; no food -8, no population +8 (report shows 0: known Vanilla discrepancy); variety 2/3/4 types +1/+2/+3; crowding >100/120/140/160/180 % -2..-10; taxes +7..-24 (donation needs positive gold, unknown mode -24); fair +16; blessing >24/49/74/94 % +2..+8; church +1, cathedral +2; ale >=25/50/75/100 % +2..+8; fear level *1. [confirmed, static] ([POPP](../POPULARITY_PARAMETERS.md))
- Religion also adds `(uint)religion/25` to an auxiliary counter (meaning unknown). Order: components, worst-source selection, then timed events (plague etc.), final clamp. Report `0x19D960` recomputes components from hardcoded constants (church_adjustment 25/50/75 is also a label identity). [confirmed, static] ([POPP](../POPULARITY_PARAMETERS.md))
- Fear level: `0xBCEB0` clears counts, excludes building status 0/3, bad types 62..64 and 91..98, good types 65, 66, 100, 101, 103; denominator `trunc(pop/16)+1`; level = clamp(trunc((good-bad)/denominator), -5, 5). Population 100 needs 7 net buildings per level. [confirmed, disassembly] ([POPP](../POPULARITY_PARAMETERS.md))
- Productivity `0xCD850` reads `0x379CF04` and writes `0x379CEEC`: 150 % at level -5 ... 50 % at +5. [confirmed, static] (WPB)
- Plague: disease scheduling `0xD1600` (from `0xF82B0`, `0x1045F0`) -> `0xD17D0` creates a herd near a building; projectile slot `0x9B2B0` (type 22, update `0x9A080` via table `0x2DA9C0`); `0x9F960` advances, `0x9F2D0` deletes (unlink `0x9F490`); `0x9C730`/`0xA0540`/`0x9A080` can write state 3 without Delete. In `0xCB090` the timed plague modifier is written before the common report write at `0xCB57C` (R14 player, AX plague contribution, EDX accumulated, accumulator R12+RBP+`0x12EC20`); the SE calculation span `0xCB16E..0xCB4B5` converges there. [confirmed, static] ([PLAGUE](../PLAGUE_POPULARITY.md))

### Yield helper and productivity bonus
- `0x18D940..0x18D9E1(unit, base, flag)`: percent = max(100, productivity); +50 if mode `0x8574B90 != 0` and flag; `sum = (percent-100)*base + remainder`, extra = sum/100 (signed truncation), remainder kept in `+0x95C`, extra in `+0x95E` (`0x18D9C6`); returns base+extra. Conditional block `0x18D976..0x18D987` (17 bytes). Owner read `0x18D954`, cache cell `0x18D95D`/`0x18D964`. [confirmed, disassembly] (WPB, HUNT)
- Consequence: only negative fear raises output (to 150 %); skirmish (99) and MP (1) give +50 % on flagged roles without fear; single player none. A cow's 3 hides give armour 1,2,1 then 2,1,2 (avg 4.5 per 3); Hunter base 6 for every prey gives 9 meat. [confirmed, static; runtime-corroborated 2026-11-06] (HUNT)
- Flag 1 callers: wood `0x12BC00`, fletcher, hunter, stone driver `0x1319A0` (passes the Ox ID), pitch, hops, apple (two), cattle, brewer, poleturner, blacksmith, armourer, tanner, iron miner `0x14DAC0`. Flag 0: wheat `0x133DD0` (base 2), miller `0x1377C0`, baker `0x138850` (two; one clears R8D explicitly). [confirmed, disassembly] (WPB)
- Natively extra is never negative (productivity floored at 100). Signed fields: raw 65526 means -10. [confirmed, static] (WPB)
- Unit render helper `0x18CE60` (from `0x97DA0`) reads extra at `0x18D2D6`/`0x18D300` for the positive yield symbol when productivity >= 101 and a transport state accepted by `0x193F80`. [confirmed, static] (WPB)

### Worker breaks (rest) and idle sequences
- `0x191070` requires rest flag 1, owner work rate < 100 and average interval > 100; stores return state and enters `0x79`; duration at `+0x30A` is signed (values > `0x7FFF` break semantics); attempt counter starts at carry+1. `0x185430` asks `0xBF0A0`; failure restores state and sets visit mode 2. [confirmed, static] ([PAUSE](../WORKER_PAUSES.md))
- `0xBF0A0(bldMgr, owner, unitId)`: up to 3 random priority candidates of types well 27, maypole 65, garden 66, statue 100, shrine 101, bee hive 102, dancing bear 103 (not pond 104), then 3 staffed-workplace fallbacks (`r_TotalWorkersRequired` > 0), then the player's stored fallback. Entry bytes `48 89 5C 24 08 48 89 6C 24 10`. [confirmed, disassembly] ([PAUSE](../WORKER_PAUSES.md))
- Per role break opportunity: direct `0x191070(unit,3)` in Fletcher; `(unit,4)` in Brewer, Poleturner, Blacksmith, Armourer, Tanner; Woodcutter, Hunter, Pitchman return to pause-checking state; Apple (state 5) and Cattle (state 2) after delivery; Wheat/Hops skip rollup and break when the farm is valid and the return route succeeds (`0x134B80` je `0x134C53`, rollup `0x134C5E`; hops `0x135AE2` je `0x135B7F`, rollup `0x135B8B`); Miller `0x1383F3` (`74 72` -> `0x138467`, fallback rollup `0x138499`, state 2 at `0x1384A5`) and Baker `0x139596` (-> `0x139651`; interval closed at `0x13955D`) break only when the next input is missing. [confirmed, disassembly] ([PAUSE](../WORKER_PAUSES.md))
- Poleturner and Tanner route 0 -> 4 -> 1; state 1 runs an idle sequence and `0x19B9D0` before material selection (sequence lengths Poleturner 54/26/100/156 at `0x332D30/0x332CF8/0x332D80/0x332E00`, Tanner 16/32/22/32/48 at `0x332788/0x3328C8/0x332AD8/0x332B08/0x332B40`). Inline exits: Poleturner `0x13AC85` (cont. `0x13AC96`, exit `0x13BDAF`), Tanner `0x13E39F` (cont. `0x13E3B0`, exit `0x13E6C2`). Status `0x189220` shows "Waiting for Wood/Cow" (159/161) without checking stock. Fletcher selects material in state 0, blacksmith in state 1, armourer has a fixed 20-update setup delay. [confirmed, disassembly] ([IDLE](../WORKSHOP_IDLE_DELAY.md))

### Common worker cycle contracts
- Storage lifetime: the 18 roles with storage phases bind store ID `+0x98E` and global `+0x6F8` (selectors return ID; caller copies building global `+0x134`); Mason `0x131430` and Miner1 `0x14D6F0` have none; `+0x700` is role context (never store). Inactive phases may hold stale `+0x6F8`. [confirmed, static] (TRAP)
- `0x188340` is not a geometric arrival test; relocation followed by stale job execution can advance work without arrival. Worker hiding (`0x196F80`) unlinks occupancy without killing the worker. [confirmed, static] (TRAP)
- Arrival-before-yield roles (wood, hunter, pitch, hops, apples, cattle, iron) choose storage and route before computing yield; successful arrival yield calls `0x12C965`, `0x130A59`, `0x133840`, `0x135901`, `0x136854`, `0x137302`, `0x14DF7B`. Fletcher and blacksmith search storage after production; brewer, poleturner, armourer and tanner finish state 109 first. [confirmed, static] (WPB)
- Removal `0x1869C0 -> 0x186C20/0x19A5D0/0x123EA0` (and `0x7490` clearing the whole record, provenance cleared via `0x1A8D20`) refunds no consumed input (Hunter meat -6, Tanner hide -1) and does not credit carried output. [confirmed, static] (HUNT, WPB)
- Twenty handlers distinguish travel, linked-actor travel, contact/production and retry/terminal states; Tanner/cow and Hunter/prey carry extra identity state. [confirmed, static] (TRAP)

### Woodcutter and trees
- Woodland types 1..4 (chestnut, oak, pine, birch): wood 1,2,4,9,9,0,0 and health 2,3,4,5,5,0,0 by stage 0..6; stage 3 first full yield. Slots 1..3999, slot 0 reserved. [confirmed, static] ([WTM](../WOODCUTTER_TREE_MATURITY.md))
- Selector `0x107E00` (state 0 and HUD state-10 query) scans ascending indices: alive 2, stage < 4, `0x1086D0` positive resource, reachable neighbour, height compatible; minimizes Chebyshev distance from the worker's current position (`0x79C0` scratch `+0x0C`, not its Manhattan return); ties keep the earlier index; no species priority. Woodcutter stores the index in `+0x9A0` and the record global in `+0x700`. [confirmed, disassembly] (TRAP, [WTM](../WOODCUTTER_TREE_MATURITY.md))
- `0x1086A0` validates serial and felled; a false result can still reach the resource-only check, so a worker can collect a felled tree; damage `0x107C90` after these checks. State 1 checks tree distance < 3 and returns to state 0 when too far. [confirmed, static] ([WTM](../WOODCUTTER_TREE_MATURITY.md), TRAP)
- Growth `0x108320` rejects felled (`+0x9A != 0`) and damaged (`+0x96 < +0x98`) records, so a damaged tree never grows; stage changes call `0x1095D0` (health/resources from stage tables). Gates: manager `+4 != 0`, frontend `0x3665ED8 != 12`, GameType `0x3665F28` not 1 (map editor) or 6 (siege editor), scenario/pause `0x3665F90`. Freebuild (type 2), skirmish (3), campaign (0) all age woodland. [confirmed, static] ([BLIGHT](../TREE_BLIGHT_DECAY.md))
- Cumulative thresholds `0x31E220` (7 Int32 per type): chestnut/oak 0,24,48,72,96,120,130; pine 0,20,40,60,80,100,110; birch 0,16,32,48,64,80,90; apple row 0,500,300,200,1000,40,40. Expected dead-phase duration ~10 selected growth calls = about 5120 ticks (random). Stage 6 sets alive 3, then `0x107970` deletes. [confirmed, static] ([BLIGHT](../TREE_BLIGHT_DECAY.md), TRAP)
- Tree Blight `0x107A00` walks the vegetation pool, picks woodland stages 2..4 by Vanilla RNG and writes stage 5 without initializing progress (keeps health, felled, resources, reservations), so old blighted damaged trees may never decay. [confirmed, static] ([BLIGHT](../TREE_BLIGHT_DECAY.md))
- Seed branch: `0x107A80` picks a neighbour; allocator `0x107390` writes slot, coordinates, serial, health, resources, stage, initial progress (can return 0 at capacity), then tree flag, proximity `0x59520`, pathing, component rebuild. Editor placement `0x6E2C0` uses stage 3. Map init `0x109660 -> 0x109B60` can remove invalid woodland. [confirmed, static] ([TREE](../TREE_RESPAWN.md))
- Constructor: Oak (type 2, handler `0x105A10`) at stage 3 returns stage 3/progress 72, health 5 (`0x31DFF0`), resource 9 (`0x31DDC0`); stage 4 returns 4/96 and is not selectable. [confirmed, disassembly] (TRAP)
- Woodcutter targets and AI wood-density weighting need stage < 4, so blighted stage-5 trees are no new harvest target, but an existing target can survive the event (residual damage/felled state). Woodcutter R1 arrival checks distance and `0x1086A0`/`0x1086D0`, not the growth < 4 test. Building placement keeps occupancy until deletion. The organism grid stores the one-based native slot; its final value distinguishes completed deletion from a fall, damage or mark. [confirmed, static] ([BLIGHT](../TREE_BLIGHT_DECAY.md), [TREE](../TREE_RESPAWN.md), TRAP)
- A freed vegetation slot can be reallocated to a new lifetime within tens of ticks; slot number alone is not lifetime identity. [runtime-corroborated, run 146] (TRAP)

### Apple farm
- `0x108040` selects the nearest alive type-15 stage-3 tree with distance < 30, any owner/farm, approaches at tree X/Y +/-2 on one axis; returns positive index or 0. [confirmed, static] (TRAP)
- `0x135E30`: R2/R8 arrival via `0x188340`; only stage 3 enters picking AI3, otherwise AI5/resume 2 with 20-tick timer and later reselection. AI3 increments the picking counter and after the fourth stage searches storage, routes and assigns yield (boundary `0x13640E`); it may select the next tree in the same call. Delivery boundary `0x136901` after `0xB6100`. Apple has no selected-tree lifetime stamp. [confirmed, static; runtime-corroborated run 134] (TRAP)
- Apple handler `0x106A70` advances one stage when progress >= `0x31E3C8`[stage] (500,300,200,1000,40,40); constructor input 0/1/2/3 returns stage 0/2/3/3 with progress 1/0/0/201. Proximity: `0x108280` level 2 for stage >= 2; `0x59520` ORs TreeProximity `0x2000` over count table `0x2D1C38` (0,1,5,13). Orchard constructor `0x72A50` calls `0x72490` size 10; own trees via `0x1071A0` at stage 0. `0x108320` excludes type 15 from spreading. [confirmed, disassembly] (TRAP)
- Vegetation type handlers are dispatched through table `0x31DC70[type]`; entry 15 (apple) is `0x106A70` (body ends `0x106D41`). Selected-tree indexing: `0x1080EE` multiplies the native index by stride `0x9C`, `0x1080F5`/`0x108103` read manager-relative X/Y at `+0x86/+0x88`; the typed array starts at manager `+0x18` with record X/Y at `+0x6E/+0x70`, so native index and public one-based SDK ID differ. Miller wheat debit: `0x137D77` passes store `+0x6F8` to `0xB6100` (call `0x137D86`). [confirmed, disassembly] ([TRAPPED_UNIT_RESCUE_AUDIT.md](../TRAPPED_UNIT_RESCUE_AUDIT.md))

### Wheat, hops and cattle farms
- Wheat farm `0xA36D0` advances when its counter exceeds 150 via `0xC8840`; tiles up to stage `0x67`; late stages drop out of the productive count (yield lost, tile stays). Hops `0xA3810` advances after 400 via `0xC8110`; productive `0x0E..0x1B`, `0x1F -> 0x20 -> 0` clears. Workers replant in state 4 (wheat stage 1, hops stage 2). [confirmed, static] ([PAUSE](../WORKER_PAUSES.md))
- Plot layouts: wheat `0x776F0` stores 36 plot IDs from `0x2D37C0` (stride `0x120`, two variants, relative 0..8, IsWheat `0x01000000`); hops `0x74410` 24 entries from `0x2D3C40` (stride `0xC0`, IsHops `0x02000000`); cattle `0x73450` 27 triples (x,y,initialDamage) from `0x2D3E00`, records 4..26 get FarmFence `0x08000000`. [confirmed, static] (TRAP)
- Hops `0x134F00` with `0xBBB70` (24 entries, PCL match, height diff < 16; plant damage 0/1, harvest 14..27) routes to a cardinal neighbour and stores the crop in `+0x730` and `+0xA00`; no `0x198620` call. Wheat calls `0x198620(unit,1)` (shortens endpoint) then restores crop context. Output states keep the store route endpoint. [confirmed, static] (TRAP)
- Cattle: `0xA3A70` spawns cow (type 51) via `0x17FEF0` into animal slots 1..3 (slot 0 farmer), stamps farm ID/global/slot; phase 6 -> 0 writes readiness DWORD `+0x1A8` = 1, consumed by farmer AI1. Cow update `0x158780` needs farm lifetime, unpaused `+0x2F2`, nonzero farmer slot 0; a cow without farm binding is deleted. [confirmed, static] (HUNT, TRAP)

### Hunter, prey and wildlife
- Hunter `0x12FC70`: state 1 approach (writes `+0x398 = 4`, calls `0x18E9A0`; speed stage `+0x348` from Manhattan distance >40:1, >36:2, >34:3, >32:4, >30:6, >28:8, else 10; near branch `0x130069` at Chebyshev <= 20 world units re-queries and may go to state 6); state 9 turns (`0x197A10`); state 10 shoots at animation frame `0x16` via `0x1883B0` (skipping it fires no arrow); state 11 corpse approach; state 2 returns; state 3/109 hut processing; state 4 takes 6 meat (`0xB6100(...,0xC,-6,0,1)`) and yields `0x18D940(hunter,6,1)` at `0x1309FE`; delivery to granary via `0xBF9F0`/`0xBD470`, statistic `0x87EEF64` and `0x19D030` category 2 (counts all food). [confirmed, disassembly] (HUNT)
- Meat is made by the hut `0xAE570` (+1 per middle phase at `0xAE928`, cap 6) while the Hunter is inside; phase 0 16 frame advances, 1..6 32 each, 7 16 = 224 advances, nominally 896 ticks at interval 4 (type-7 default `0xB4B8B`, table `0x2E8AA0`); completion `0xAEA8C`. Hut cadence: `0xC60F0` increments `+0x18` at `0xC633C`, compares with interval at `0xC6355/0xC6359`. [confirmed, disassembly] (HUNT)
- Prey query `0x18AF50` filters: alive 2 (`0x18AFC0`), no corpse (`0x18AFCE`), owner 0 (`0x18AFD8`), deer/goat (`0x18AFE6`, SE hook site), lease 0 (`0x18AFFD`), Chebyshev pass 20 then 5 (`0x18B02A`), sight below 54 (`0x18B0A2`); nearest, ties lowest ID; callers `0x12FDBC`, `0x12FF7E`, `0x130521`, `0x1305C7`, `0x1306DD`, `0x1941B4` (idle-reason speech `0x1940C0 <- 0x18CE60 <- 0x97DA0 <- 0x97C70 <- 0x8692D`, same tick thread). [confirmed, disassembly] (HUNT)
- Owner barriers (14-byte spans): query owner word `0x18AFD8..0x18AFE6`; attack team compare `0x18EBDA..0x18EBE8` (table `0x37EDF3C`, `je 0x18ECA7`); arrow hit `0x192AA7..0x192AB5` in `0x192750` (only caller `0x9C730`). After a hit only the warning `0x192B78` depends on owner. [confirmed, disassembly] (HUNT)
- Reservation `+0xAA4`: Hunter writes 2 in states 0/1/9/10/0xB (`0x12FE63`, `0x130044`, `0x130259`, `0x130338`, `0x13064F`); only deer/goat `0x156130` decrement (`0x156203`/`0x156213`); rabbits `0x157810`, camels `0x158020`, chickens `0x162CB0` never release, so they are selectable once until removed. Also used as war-dog slot (`0xB47E0`, read `0x1671BB` in `0x166FE0`) and Engineer flag (`0x14AED0`). [confirmed, static] (HUNT)
- Corpses: search `0x18B640` (deer/goat, owner 0, lease 0, no reservation write; only state 10 calls it); handoff `0x130548` -> state 0xB `0x1305E0`; arrival `0x1306AE..0x1306BD` (15 bytes, every butchered animal once) sets corpse `+0x920` = 1000 and `+0xA0C`. Dead thresholds: deer/goat 500 (`0x156C25`), lion/hyena 50 (`0x1574FC`), rabbit 5 (`0x157EBD`), camel 50 (`0x15848E`), crocodile 50 (`0x17F90F`), war dog 50 (`0x16794D`); fade removal at 32 via per-type `mov ecx,0x20` (deer `0x156C5A`, lion `0x157535`, rabbit `0x157EDC`, camel `0x1584C5`, cow `0x158D45`, dog `0x159BBF`, chicken `0x163472`, war dog `0x167987`, crocodile `0x17F94C`). [confirmed, disassembly] (HUNT)
- Chickens `0x162CB0` (table index 62) count per owner and wander between bakery/mill/granary (`0xBF300`); `0xD29E0` targets 0..10 by food, spawns at `0xBF510`, kills the owner's first chicken above target (`0xD2ABF`), one per player per 200 ticks. [confirmed, static] (HUNT)
- Herds: dispatcher `0x117520` (deer `0x124E80`, lion `0x1259D0`, rabbit `0x125D00`, camel `0x124890`, goat `0x125290`, hyena `0x1256A0`, crocodile `0x124B90`; other kinds troop groups `0x10AA20`); spawn arts deer `0xDC` 9-12, lion `0xDD`, rabbit `0xDE`, camel `0xDF`, goat `0xE2` 4-5, hyena `0xE3`, crocodile `0xE5`; respawn `0x123670` (from `0xCDE60`, mode 0, < 6 animals; first-herd tables `0x1171D0`/`0x117240`); growth `0x118130` every 100 updates 28/128 chance, cap 30 (`0x11B460`), not in flight/editor; flight from sector threat (`0x124F20`/`0x125330`, predators counted by `0x126810`); routes `0x127E60`/`0x129010`; member cleanup `0x1261F0` each tick (`0xCE26D`). Hunter's-hut button needs deer/goats at map start (`0x37EF97C`, `0xFFC70` case `0x4E`) outside mode 0. [confirmed, static; parts in-game 2026-11-07] (HUNT)
- Hostility: `0x1867A0` (callers `0x188A20`, `0x187230`, `0x18E9A0`); melee scan `0x18C160` takes only lions, war dogs, hyenas (`0x18C731`); ranged switch `0x18F2B2`: deer/goats only for Hunters, rabbits only with tribe flag `+0x58E` (set by order 4 `0x11EC89` and `0x20` `0x11F2AF`, cleared `0x11E9CA`, read `0x18F31C`), crocodiles ordinary targets. Melee contact `0x182B00 -> 0x195170 -> 0x188A20 -> 0x1867A0` returns attackable for rabbits at `0x186952`. [confirmed, static] (HUNT)
- Removal of the Hunter or Tanner after their input debit refunds nothing. [confirmed, static] (HUNT)
- After the shot animation (state 10): a corpse target goes to state 11, otherwise `0x18B640` looks for another corpse, otherwise the query decides state 0 or 6; an idle Hunter never searches corpses. Corpse search keeps the nearest over Chebyshev 20, then 5, sight check below 54, no upper limit. A corpse being collected shows reservation 0 for about 30 of its last 33 ticks (only states 0/1/9/10/0xB renew). Units update once per `0x182B00` pass in ascending ID, so a held lease ends each tick at 1 or 2. [confirmed, static] (HUNT)
- Hunter inventory and identity: hut worker ID at public building `+0x19E` validated against stored global `+0x1A8`; second pair `+0x1A0/+0x1AC` is the hound (respawn type `0x34`); Hunter's linked-building global at public unit `+0x364`. State 4 owns a deferred yield even with zero meat/cargo; states 0/7/8 have no deferred delivery; `0xB6240(...,2)` clears public `+0x2C`/`+0x118` and sets cycle mode 2. Hunter R2 arrival writes `0x0003006D` (109/saved 3), fade `0x0200`, hides after 17 updates; outgoing AI3 uses 109/saved 0 and unhides at once. [confirmed, static] (HUNT, TRAP)
- Final movement step: `0x1855A0` resets substep (public `+0xF4`) and increments the path index `+0xF6` when queuing a step, so index can equal length `+0xF8` while path state `+0xF2` is still 2; completion is at substep 8. Hunter state 1 renews the reservation before the near branch; `0x79C0` returns Manhattan distance and writes the Chebyshev maximum to scratch `+0xC`. [confirmed, static] (HUNT)
- Herds: the sector grid knows neither sight nor connection (a unit behind a wall in the sector scares a herd); a fleeing herd counts itself; routes flood up to 5 sectors (deer) or 3 (goats) then random-direction waypoints, flight takes the last waypoint at once; corpses stay members until removed; camels found new herds (`0x124890`, `0x123C20`); in the editor (`0x3665F28 == 1`) herd updates return before route/growth (in-game confirmed 2026-11-07). A neutral chicken (owner 0) finds no building to wander to. [confirmed, static/in-game] (HUNT)
- AI hunter-hut retirement branch in `0x3B270` (manager anchors `0x3B27F` -> `0x64CCBB0`, `0x3B30E` -> `0x34A9F70`): `0x55E10` cell avoidance at `0x3B31E`, `0xC4480(...,50)` refund at `0x3B331` (no refund in mode 1, 100 % in mode 6), `0xC4290` removal mark at `0x3B33B` (propagates to the demolition group; `0xC60F0 -> 0xB8310` clears later). `0xD1D4F` is a boundary inside `0xD1AD0`; no HunterUpdate is suspended there. Details: [AI_WOODCUTTER_RETIREMENT.md](../AI_WOODCUTTER_RETIREMENT.md). [confirmed, disassembly] (HUNT)

### Tanner and cows
- Cow query `0x18B470`: ascending IDs, alive, death marker 0, same owner, type cow, AI 0, distance < 10000. Tanner 2/3 uses selected cow `+0x9F6`/global `+0x9F8`; cow 4 checks the bound Tanner and its `+0x8F8` marker, enters 5, after 1001 updates 6; Tanner 9 frame 8 writes cow 6 without HP damage; cow enters 110 then Life 3. [confirmed, static] (HUNT)
- Tanner AI9 adds hides +3 cap 3 (`0x13EA38..0x13EA4C`), publishes `0x0006006D`; AI6 debits one hide (`0x13ECC0`, cap 48) after its preparation interval, enters 5; completed AI5 resets `0xB6240`, unhides, calls yield (`0x13EBF7`), output fixed leather 23; one cow gives three charges. AI7 armoury route, AI8 delivers via `0xB6100(23,+1,50)` before cargo decrement. Building fade fix site `0xB10D0` belongs to the Tanner building. [confirmed, static] ([WWF](../WEAPON_WORKSHOP_FIELDS.md))

### Weapon workshops
- Fields `+0x2EA` latched current, `+0x2EC` next/desired (public `+0x28E`/`+0x290`, SE names swapped); initialized in `0xB47E0`; Fletcher, Poleturner, Blacksmith copy next to current at latch points; `0x19D960` publishes current low byte and the selected cache `0x379ADE0`; opening the panel (`0x89F40`, `0x97380`) refreshes it. Armourer and Tanner use fixed goods. Checksum `0x22020` includes both (temporary zero at `0x22201`). [confirmed, static] ([WWF](../WEAPON_WORKSHOP_FIELDS.md), HUNT)
- `SetNextWeaponMade` (1023) -> `0xD66B0` (research check for goods `0x11..0x16`; writes `0x379ADE0`) -> Chore 33 (`0x128F0`: structure 2 bytes, good 1 byte, global 4 bytes) -> `0xD5870` writes `+0x2EC` without type check after global compare; structure > 32000 (Shift sends -1) applies `0xC52B0` by type, Fletchers for goods outside `0x13..0x16`. [confirmed, static] (HUNT)
- Inputs: Fletcher 2 wood bows / 3 crossbows (threshold `0x12D755`, clear `0x12D9E7`); Poleturner 1 spear / 2 pike (`0x13B10F`, `0x13B3A7`); Blacksmith 1 iron (local cap 48, state 6 at `0x13C4E2`); Armourer 1 iron (`0x13D6AC`); Tanner 1 hide (state 1 `0x13E408`). `0xB6100` rejects over-capacity as a whole, so 3 hides plus any retained hide exceed the cap. [confirmed, static] ([WWF](../WEAPON_WORKSHOP_FIELDS.md))
- Fletcher `0x12D230`: R1 consumes 1 wood externally (cargo 0 -> 1, AI2), R2 credits workshop; AI5 clears the 2/3-wood buffer directly (no debit callback); completed AI4 unhides, AI6, yield `(unit,1,1)`, assign cargo; AI6 armoury route; R7 delivers product +1 cap 50 per call; R8 is waiting/failure. [confirmed, static] (WPB)
- Blacksmith `0x13BDD0` and Armourer `0x13CF30`: pick up iron 6 (`-1`, cap 48) after committing AI3/cargo 1; Smith credits home at AI6, Armourer at 109/saved 6; both consume local iron at AI6, work AI5. Smith latches `+0x2EC` into `+0x2EA` (maces 21 / swords 22), goes AI7 directly and yields; Armourer constant metal armour 24 with local product +1/-1 and 109/saved 7. R8 delivers via shared 50-slot armoury and may reselect. [confirmed, static] (TRAP)

### Pitch, quarry, mine, miller, baker
- Pitch: struct 6 dispatches to `0xADF70` (building `+0x84` completed-animation DWORD, completion `+0x88`); R4 home 109/saved 1, hides after 17 updates; completed work cargo 1, AI2 selects `0xBF9F0(Pitch 7)`, routes, AI3 and yield; R3 needs contact and `0xBD490(7,16)`. Pitch workings need real IsPitch in a 4x4 area (IsSwamp insufficient). [confirmed, static] (WPB)
- Quarry: tether `0xAD180` creates the Ox (type 9) at `+0x2C8`; its invalid-quarry demolition branch is AI-only. Grunt `0x1319A0` picks a pile via `0xBBA40` (Chebyshev, later index wins ties), withdraws 1 stone (cap 48), adds 1 to the tether buffer directly on arrival; at buffer >= 8 it routes, applies stair postprocessor `0x198B60`, clears the buffer and assigns `0x18D940(Ox,8,1)` to the Ox (stored at `0x131C9B`). Ox `0x132910` departs only with cargo >= 8 (`0x132A09`, `0x132C17`); fallback `0x1320B0` checks < 0. Grunt path profile 1 (`0xF3060`, no wall/stair filter) makes pre-stair reissues reachable. Ox failed storage recovery can clear cargo; a reduced Ox batch 1..7 would never depart. [confirmed, static] [confirmed, static] (WPB, TRAP)
- Mine: `0xB6340` turns eligible Peasants into Miner1 (type 31, slot 0) and Miner2 (type 32, slot 1). `0xAD540` needs active slot 0 and credits local iron +1 cap 8, waits at >= 7. Miner2 `0x14DAC0` (workplace adjacency `0x193F10`) state 3 selects a store, on successful route decrements Mine iron directly (no `0xB6100`) and yields; state 4 delivers `0xB6100(Iron,+1,48)`; failed replanning delay -15. [confirmed, static] (WPB, TRAP)
- Miller `0x1377C0` registers at building `+0x214`; input R3 commits AI4 then wheat -1; yield at pickup `0x137CF6` (withdrawal `0x137D86` uses the pre-call count); flour R8 deposits one flour per call with AI8 and cargo still positive. Store global `+0x6F8` writers `0x137AC4`, `0x137DFA`, `0x13814C`, `0x138352`, `0x138443`. [confirmed, static] (TRAP, WPB)
- Baker `0x138850`: R7 flour pickup commits AI8 then `0xB6100(Flour,-1,32)` (yield at `0x138DD1`, withdrawal `0x138E58`); production commits `0x5006D` then yield base 8 at `0x1390F8`; R5/R6 granary route and `0xBD470(Bread,250)`; delivery one bread per call. [confirmed, static] (TRAP, WPB)

### Production state in saves and repair
- Units are saved raw (entry 54 at `0x337030`, base `0x67E8A58`, length `0xB23900`); buildings raw (`0x336FB8`); writer `0x52B0` (from `0x86F80`, `0x13030`; CRT write `0x1DDB94`, close `0x1DBB20`), loader `0x853F0 -> 0x87D0 -> 0xCF20`. Hunter target fields, hut meat and yield words survive load. `0x853F0` first calls `0x87D10` (scene `0x3665ED8` = 40) so the first `0x71670 -> 0xCDE60` fold dispatches no workers (`0x7E50` accepts scenes 12/14/16). Post-load normalizers `0x186C50`/`0x19A500` do not touch carry. [confirmed, static] (WPB, HUNT)
- Chore 56 `0x15710` transfers the full unit record (remainder/extra at wire 304/306); Chore 57 `0x158B0` the full building record; host repair loop `0x251F0`; completion Chore 55 `0x15200` calls `0xCA110(999)` at `0x15459`, which rewrites the production interval from the type default (`0xCA371`/`0xCA379`). Building checksum includes interval and frame (`0x2222D`, `0x22213`) but not the two cadence counters. [confirmed, disassembly] (HUNT, WPB)

### Building sleep
- Per-type sleep flags; `GameAction ToggleSleep` -> Chore 43 carries the building type; `0xD5980` toggles and calls `0xC7E90`, which clears local goods and worker references of matching alive buildings. `0xC7D50` periodically resyncs each building's sleep state against the flag and performs the destructive reset on disagreement (comparison at `0xC7DCB`). Details in fortifications/network topics. [confirmed identity, probable semantics] (function-claims.jsonl, [BUILDING_SLEEP_SELECTION.md](../BUILDING_SLEEP_SELECTION.md))

### Lord creation timing and HP basis
- Lord spawn `0x17FEF0` (final callees `0x180230`/`0x1802A0`, `0x18BE50`, no HP rewrite) returns before caller `0xC23C0` publishes `Player.LordUnitId` and applies AI profile health; `0xCEF00 -> 0xC23C0` is also reached from `0xD1AD0`. Spawn starts in NeedsInit 1 with HP from `0x19A240` (per-owner modifiers VA `0x183666058`, distinct from global Advanced EnemyHPS VA `0x1887EE324`). `0xC23C0` profile scaling requires mode `0x8574B90 != 0` (99 included), player slot -1 and nonzero lineup. New-game normalizer `0x197EF0` and load normalizer `0x19C300` (unconditional in `0x19A8C0`) reset healthy units' HP from the default table; older-format migration `0x19BF70` is not universal. `0xC23C0` also creates Jesters (word `0x37EDF82`). [confirmed, static] ([SESS](../EXTRAFEATURES_SESSION_CALLBACKS.md), TRAP)

### Instruction sites with known register contracts
Vanilla instruction boundaries that mods hook or that carry separate meaning (all inside functions listed above; exact bytes in the evidence):
- Blight event loop tail `[0x107A6C,0x107A7A)` (R8 type field, R9D slot, R10 manager; `jl 0x107A16`); callers `0x1049B7` (`0x1045F0`), `0xF9F1F` (`0xF82B0`). Growth felled test `[0x10838E,0x10839C)` (RBX record, RDI manager; reject `0x108645`, pass target `0x1083B0`); SE growth observer site `0x108518`. ([BLIGHT](../TREE_BLIGHT_DECAY.md))
- Woodcutter: candidate filter `0x107EC5` (RSI alive word, continuation `0x107ED9`, skip `0x107FEA`); arrival standing check `0x12BFCF` (cont. `0x12BFE1`, resource-only fallback `0x12BFFA`, reset `0x12C03D`); strike `0x12C1D8` (cont. `0x12C1E9`, fallback `0x12C291`, reset `0x12C2B3`); resets load RAX from signed Int32 `0x9302C4`; pickup/drop-off `0x12C8D6`/`0x12CA3B`; wood production `0x12C984`. ([WTM](../WOODCUTTER_TREE_MATURITY.md))
- Lord reward: winner test `0x15C475`; displaced IMULs end `0x15C4F8`; `IMUL ECX` at `0x15C512` overwrites RDX; `LEA R13` at `0x15C988`; gold message call span `0x15CA65..0x15CA74` (`0x15CA6A` prepares it); R12 overwritten at `0x15CA7B`/`0x15CAB1`. ([LOOT](../DEFEAT_LOOT.md), [KILL](../KILL_REWARD_PERCENT.md))
- Miller/Baker break branches end at `0x138404`/`0x1395A7` (next instructions `0x138405`/`0x1395A8`). Output boundaries: Wheat `0x1349B7`, Miller prepare `0x1380A1`, pickup `0x138162`, deposit `0x138290`, finish `0x1383AF`, Baker `0x1391F5`/`0x139337`; unit checksum injection point `0x22149`. Tanner state 7/8 sites `0x13ED8B`, `0x13EEFB`, `0x13EF38`, `0x13F009`; Tanner stores `AIState/SavedState = 0x7006D` after yield. (WPB, [PAUSE](../WORKER_PAUSES.md))
- Hunter: hut-meat check `0x12FD72`, pickup `0x12FD7C` (`mov r8d,[r14+rdi+0x134]`; R9 only set at `0x12FDA1`), hut slot cleared `0x12FDAD`, state-1 stage decision `0x12FF38`, corpse global mismatch `0x1306AC`, `+0xA0C` write `0x1306CA`, drop-off `0x130B54` (RBX Hunter); query entry span `[0x18AF50,0x18AF5E)` (EDX Hunter), query reject `0x18B0DD`, corpse lease read `0x18B6ED`; dispatcher sets R15 = 2 at `0x183C35`; rabbit/camel/chicken update-entry sites `0x157828`, `0x15803A`, `0x162CC8`; rabbit order site `0x11EE64`. (HUNT)
- AI seller: Fixes stub site `0x3F0C9` (19 displaced bytes, return `0x3F0DC`); separate reserve site `0x3F156`. ([WHEAT](../FIXES_WHEAT_STARTUP.md))
- Gift branch internals: R13 is the chat manager (LEA `0xD7B1A` gold path, `0xD7BA9` goods path, both `0x8574320`); writer returns `0xD7C80`/`0xD7CB7`; PreInit gate instructions `0x862CE`, `0x8635F`, `0x8636E`. ([ALLY](../ALLY_GOODS_TRANSFERS.md))

### Further wildlife and dispatch details
- Herd-related: map-loading spawn paths `0x13CA0`, `0xF82B0`, `0x1045F0`; spawn callers `0x1238C0`, `0x1239C0` (camels), `0x123AC0`, `0x123C20` (camels); growth argument sites `0x124F96`/`0x1253A6`; flight `0x11DFE0`; route builder zeroes index/count at `0x1283D6` before its only early exit `0x128931`; route readers without count check: camel `0x1249F2`, deer `0x124FBE`, goat `0x1253CE`, hyena `0x125829`, lion `0x125B6D`; herd serial from `0x3665F84` via `0x119C00` (also the tribe-ID allocator, 1..4499). Deer corpse jump tables `0x156CB0`/`0x156CE0`, deletion at count > 32 `0x156C53`. Other animal updates: lion/hyena `0x156DC0`, crocodile `0x17F2F0`, dog `0x158EF0`, crow `0x158670`, seagull `0x158680`; cow `0x158780`, Tanner cow helper `0x18B540`. (HUNT)
- Hut button availability: deer/goat count at map start computed in `0x94350` and `0x1860E0`. Animal hostility mode flag at `0x186964`; melee callers `0x194550`, `0x195170`; `0x19A1F0` wraps the melee scan; damage `0x199110` (from melee `0x195170`) writes the death marker also for melee. Engineer reservation-word flag writes `0x14B58D`/`0x14B59B`, `0x14CD7C`, `0x14CF08`; war-dog slot writes `0xB51E7`, `0xB528C`, `0xB5335`, `0xB53E1`. (HUNT, [IDLE](../WORKSHOP_IDLE_DELAY.md))
- Start troops: dispatcher `0x119050 -> 0x117D20` places groups; Archer `0x13F540` can aim in formation state 101; order dispatch `0x11F156` (orders `0x20`/`0x21`), ranged branch `0x11F230` skips the rabbit flag store for automatic `0x21`. Combat details: [units](units.md). (HUNT)
- Speech chain links: `0x18CE77` (in `0x18CE60`), `0x97E78` (in `0x97DA0`), tail jump `0x97CFC` (in `0x97C70`). Engineer handler `0x174F90` also calls `0x1867A0`-guarded attack paths. (HUNT)
- Plague projectile helpers: type helper `0xA1DB0`, movement init `0xA13E0`. Report builder price preview ends at `0x19ECD7`; buy lot store `0x19EC33`. Presentation sound prep `0xEDF0 -> 0x1A4270`. Unit-update function table file offset `0x3202B0` (index = SE `UnitFunctionsVTable` field). Building production handler table `0x2DEAE0`; incoming-gold dispatch entries `0x2DEC20`/`0x2DEC28`/`0x2DEC30`; hunter-hut phase animation data `0x2DF558`/`0x2DF570`/`0x2DF598`. Pitch chain helper `0x76060`; minimap builder `0xF4DF0` receives no save path; restart blob at `0x8EC020` copied from its source. Chicken target compare field `[rdi+0x2048]`. ([PAUSE](../WORKER_PAUSES.md), WPB, [PLAGUE](../PLAGUE_POPULARITY.md), HUNT)
- Resource-record anchors: native slot prefix `0x12EBC0` (manager-relative), signed fear boost at native slot `+0x130CF4`; `.data` begins at RVA `0x240000`. ([POPP](../POPULARITY_PARAMETERS.md), [KILL](../KILL_REWARD_PERCENT.md))

## Cross-topic links
- Route publication, arrival, occupancy, relocation `0x19BA40`, `0x187110`: [pathing](pathing.md).
- Chore transport, checksums, building sleep Chore 43: [network](network.md); building pause UI and construction costs: [fortifications](fortifications.md).
- AI market, sell thresholds, woodcutter build/retirement (`0x3B270`, `0x55E10`, `0xC4480` refund, `0xC4290` removal mark), economy observation: [ai](ai.md).
- Disease projectiles, recruits/Peasant employment, unit damage: [units](units.md); peace time and mode values: [session](session.md); record layouts: [memory](memory.md).

## Open questions and contradictions
- Ally transfer capacity mismatch (sender debited after failed add) is static only; no live reproduction. ([ALLY](../ALLY_GOODS_TRANSFERS.md))
- Economic meaning of the AI response value in `0x3F870` and of the religion auxiliary counter is not established. ([ALLY](../ALLY_GOODS_TRANSFERS.md), [POPP](../POPULARITY_PARAMETERS.md))
- function-claims.jsonl lists buy-price caller functions `0x3E000`/`0x29000` (call sites `0x3ED72`/`0x29684`, sell `0x29732`), while MARKET_PRICE_QUERIES and EXTRAFEATURES_SESSION_CALLBACKS give precise caller starts `0x3EC90`/`0x29650`/`0x29700`; the call sites lie inside the latter, so the claims file uses coarser function bounds.
- Reservation word: clear at mission start, save/load of the word and `0x18CE60` idle-reason use are not traced; corpse thresholds for lion/hyena/camel/crocodile/war dog are probable. (HUNT)
- `0x3668E38` secondary troop cap (3000/6000) semantics unknown. (TRAP)
- Gift statistics and speech can occur although resource add/sub did not balance (unchecked Int32). ([ALLY](../ALLY_GOODS_TRANSFERS.md))
- Earlier claims corrected: the Lord-kill statistic address was mistyped as `0x887EED98` (corrected 2026-10-27); report fields do not follow SE popularity values (corrected 2026-10-17); Hunter state 10 is the shot, not a "sit" state; the building-sleep Chore 43 payload is a building type, not a building ID; cattle readiness is `+0x1A8`, not `+0x158` (2026-11-27); Apple constructor input 1 returns stage 2 (2026-11-26).

## Touched by our mods
- APIShared: market price broker, ally transfer service, worker yield/receipt/production state hooks, wildlife owner gates, map-tick events: [mods](../mods/economy.md#apishared).
- ExtraFeatures: Lord loot/kill reward, periodic gold, woodcutter maturity, tree respawn, production percent, popularity targeting, AI market prices: [mods](../mods/economy.md#extrafeatures).
- BugfixesAndQoL: Tree Blight decay, miller/baker pauses, workshop idle delay, plague popularity: [mods](../mods/economy.md#bugfixesandqol).
- ImprovedHunters: prey rules, corpses, herds, Tannery meat: [mods](../mods/economy.md#improvedhunters).
- Testmods (PopularityTest, TrappedUnitRescueTest): [mods](../mods/economy.md#testmods).
- External (Script Extender, Fixes, Tweaker): [mods](../mods/economy.md#external-components).

## Sources
- [ALLY_GOODS_TRANSFERS.md](../ALLY_GOODS_TRANSFERS.md): gift/request execution, capacity, gift messages and chat gate; APIShared adapter.
- [DEFEAT_LOOT.md](../DEFEAT_LOOT.md): Lord-death reward block and ExtraFeatures suppression hooks.
- [EXTRAFEATURES_SESSION_CALLBACKS.md](../EXTRAFEATURES_SESSION_CALLBACKS.md): mostly mod; Lord HP timing and buy/sell caller starts.
- [FIXES_WHEAT_STARTUP.md](../FIXES_WHEAT_STARTUP.md): external Fixes wheat hook defect; AI seller loop facts.
- [HUNTER_PREY_RULES.md](../HUNTER_PREY_RULES.md): Hunter, prey, corpses, herds, chickens, cows, Tanner, yield helper; ImprovedHunters.
- [KILL_REWARD_PERCENT.md](../KILL_REWARD_PERCENT.md): reward registers, cleanup, statistic correction, message capacity.
- [MARKET_PRICE_QUERIES.md](../MARKET_PRICE_QUERIES.md): price helpers, callers, HUD limits, persistence.
- [PERIODIC_GOLD.md](../PERIODIC_GOLD.md): incoming gold delivery and map tick; ExtraFeatures periodic gold.
- [PLAGUE_POPULARITY.md](../PLAGUE_POPULARITY.md): plague clouds and popularity correction point.
- [POPULARITY_PARAMETERS.md](../POPULARITY_PARAMETERS.md): components, fear level, productivity, report path; APIShared adapters.
- [TREE_BLIGHT_DECAY.md](../TREE_BLIGHT_DECAY.md): growth, thresholds, blight; BugfixesAndQoL fix.
- [TREE_RESPAWN.md](../TREE_RESPAWN.md): vegetation records, deletion, seeding; ExtraFeatures respawn.
- [WEAPON_WORKSHOP_FIELDS.md](../WEAPON_WORKSHOP_FIELDS.md): product words, input thresholds, Tanner chain.
- [WOODCUTTER_TREE_MATURITY.md](../WOODCUTTER_TREE_MATURITY.md): wood tables, selector; ExtraFeatures spans.
- [WORKER_PAUSES.md](../WORKER_PAUSES.md): rest gate, per-role breaks, crops, visit targets.
- [WORKER_PRODUCTION_BONUS.md](../WORKER_PRODUCTION_BONUS.md): yield, receipts, saves, per-role chains (Fletcher, Pitch, Quarry, Miner2).
- [WORKSHOP_IDLE_DELAY.md](../WORKSHOP_IDLE_DELAY.md): Poleturner/Tanner idle sequences; BugfixesAndQoL fix.
- [TRAPPED_UNIT_RESCUE_AUDIT.md](../TRAPPED_UNIT_RESCUE_AUDIT.md) (topic pathing): worker/production Vanilla facts (Apple, Miller, Baker, iron workshops, Mine, Hunter hide, wood selector, farms, stockpile constructor).
- function-claims.jsonl (`0xC7D50`, `0xCEB10`, `0xCEB90`) and [BUILDING_SLEEP_SELECTION.md](../BUILDING_SLEEP_SELECTION.md).
