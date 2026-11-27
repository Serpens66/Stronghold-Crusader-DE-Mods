# Manager globals, record layouts, field catalog and Script Extender interop [Scope: Vanilla]

Native hash FBCB9319 (full hash in ../../../../CURRENT.json). Summary state 2026-11-27. Check the installed DLL hash per CURRENT.md before using any address. Evidence links point to the audit documents; they stay authoritative.

Binding rules for RVAs, offsets, signatures and hash binding are in [AGENTS/native-analysis.md](../../../../../../AGENTS/native-analysis.md); they are not repeated here.

## Overview

- The native simulation keeps its state in a small set of statically allocated manager globals inside `CrusaderDE.dll` (unit, building, player, tribe, tile, path, sound, dialogue, message managers). Live address = module base + RVA; never reuse an address across processes ([LiveProcessReadOnlyInspection.md](../../../../../../Findings/LiveProcessReadOnlyInspection.md)).
- Entity managers hold fixed-capacity arrays of fixed-stride records behind a small manager header. Unit, building and tribe game IDs are 1-based; slot 0 is a reserved sentinel. Public interop spans (Script Extender / APIShared) start at the first real entity and are 0-based.
- Native code addresses an entity field as `manager + id * stride + (arrayOrigin + viewOffset)`; for units `arrayOrigin = 0x65C`, so native displacement = public `GameUnit` view offset + 0x65C (e.g. native `+0x8FE` = `GameUnit+0x2A2`).
- Aggregate manager counters are rebuilt during the simulation tick; only per-record scans or in-tick reads are consistent.
- Field knowledge is curated in a hash-bound field catalog ([native-field-catalog.json](../native-field-catalog.json), 88 field identities on 5 record families) and mirrored as Ghidra data types (`NativeKnowledge/FBCB9319`, see [NATIVE_KNOWLEDGE.md](../../../../tools/semantic/NATIVE_KNOWLEDGE.md)).
- Main code entry points in this topic: the cursor move stager `0x195E30` (best set of data anchors), the unit update dispatcher `0x182B00` with vtable `0x321CB0`, the target/range chain `0x18C040`/`0x18C160` → `0x18E9A0` → `0x19B630`, the projectile initializer `0xA1DB0`, and the message/speech path `0x103290` → `0xFEE50`.

## Consolidated manager and record table

Offsets in the "view" column are relative to the public interop record (Script Extender / catalog view), not to the manager. "Native disp." is the displacement native code uses together with `id * stride`.

| Manager / record | Global RVA | Array origin, stride, capacity | ID basis | Most important fields (view offset → meaning, type) | Confidence | Evidence |
|---|---|---|---|---|---|---|
| Unit manager `GameUnitManager` / `GameUnit` | `0x67E8400` (rip) | origin `+0x65C`, stride `0x490` (1168), capacity 10,000 slots (IDs 1..9999) | 1-based game ID; slot 0 reserved; ID *n* at `+0x65C + n*0x490`; span index = ID-1 | header: `+0x0` `r_NextUnitId`, `+0x4` `r_TotalUnits`, `+0x24` `r_SelectedChimpsCount[9]` (UInt32 per player). Record: `+0x88` alive/life state Int16 (2 = alive); `+0x8A` unit type `r_UnitChimp` (eChimps); `+0x92` controlling player UInt16; `+0x94` global ID UInt32; `+0xB2/+0xB4` world X/Y; `+0xB6/+0xB8` standing height base/correction Int16; `+0xC0/+0xC2` current tile X/Y; `+0x29C` death marker UInt16; `+0x2A0` capture eligibility; `+0x2A2` distance to nearest enemy (signed in native compares); `+0x2BC` AI state Int16; `+0x2D0` current health %; `+0x2D4` tribe ID; `+0x3C4/+0x3C8` current/max health UInt32 | confirmed (runtime + static) for global, origin, stride, header counters, `+0x88/+0x8A/+0x94/+0xC0/+0xC2/+0x2D0/+0x2D4`; other fields per catalog row | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md), [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md), [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md), [native-field-catalog.json](../native-field-catalog.json) |
| Building manager / `GameBuilding` | `0x64CCBB0` (rip) | origin `+0x5C`, stride `0x32C` (812), capacity 4,000 | 1-based; ID *n* at `+0x5C + n*0x32C`; player 1 keep + doors were IDs 1-3 | `+0xD6` owner `r_PlayerIdOwner`; `r_BuildingType` (offset not recorded in the audit); `+0xEE/+0xF0` origin tile X/Y; `+0xEC` rigid building height Int16; `+0x10C/+0x10E` current/max health; `+0x2A2` gate state (candidate); `+0x2A8` shared link word; `+0x2AC` used-in-siege-attempt ID; `+0x2BE` on-fire counter; `+0x300..+0x318` outpost block | confirmed (runtime) for global, origin, stride, `+0xD6/+0xEE/+0xF0` | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md), [native-field-catalog.json](../native-field-catalog.json) |
| Player manager / `GamePlayerResources` | `0x366C210` (rip) | resource records at `+0x1343FC`, stride `0x583C` (22,588) | 1-based player IDs 1-8 | `+0x46C` pending incoming goods table; `+0x50C` gold Int32; `+0x21F8` lord unit game ID; `+0x2210` player pause word; `+0x2244` win/loss state; `+0x35BC` raid candidate building IDs (100 x UInt16) with count at `+0x3684`; `+0x38F0` `r_AILastStandPhase` (candidate). Manager-relative (not record-relative): AI buy price table `+0x1817B8 + good*8`, sell `+0x1817BC + good*8` | confirmed (runtime) for global, base, stride; price tables confirmed static | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md), [native-field-catalog.json](../native-field-catalog.json) |
| Tribe manager / `GameTribe` | `0x7CC6720` (rip) | stride `0x688` (1672); Extender source puts `GameTribeArray` at manager `+0x2A`, capacity 4,500 | 1-based tribe ID | native disp. = view offset + 0x2A. Native `+0x2C` = player ID of the tribe (compared with active player); native `+0x5A` read by the stager (= view `+0x30` `LeaderUnitGameId`, UInt16, confirmed in catalog); view `+0x622` stored target building ID (native `+0x64C`), `+0x626` its global ID DWORD (native `+0x650`); `+0x628..+0x64F` ten `r_LastRangedAttackerGlobalId*` UInt32 (upstream 2.14.3 names, candidate) | confirmed static (stride, player-ID offset, global); not read at runtime | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md), [SE_2_14_3_INTEROP_ADDITIONS.md](../SE_2_14_3_INTEROP_ADDITIONS.md), [native-field-catalog.json](../native-field-catalog.json) |
| Tile manager (`GameTileManagerView`) | `0x405EDB0` (rip) | packed map: axis capacity 800, 320,800 packed tiles; per-grid offsets inside the manager (Extender source, e.g. StructureGrid `+0xB0BCA0` Int16, TileUnitIdGrid `+0xBF6C00` Int16, HeightGrid `+0xD7E5A0` byte) | packed tile index | grids decoded at the Extender's offsets at runtime | confirmed (runtime) for global and grid offsets used | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md) |
| Map row lookup table | `0x402FF2C` (module-relative disp) | 3 entries per row (formula `x + table[3*y]` unverified) | tile row y | row starts y=400/401/402 = 160400/161199/161996 | confirmed read; formula open | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md) |
| Path manager | `0x60AD660` | layout not audited here | - | passed as `rcx` to `0xE2610` (PCL reachability) and `0xDB650` (direct tile search) | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| Game tick | `0x3665F58` | scalar | - | monotonically increasing native tick (~215,000 in a long session) | confirmed runtime | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md) |
| Active player ID | `0x88E3D70` | scalar | 1-based | read by stager `+0x109`, `+0x1BF`; also `0x18D460`+0x6C and `0xE0E6` | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| Sound manager | `0x88E72D0` | - | - | `rcx` of speech-busy `0xE0E0` (10 of 11 direct calls) | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| Dialogue manager | `0x64C4490` | - | - | `rcx` of speech `0x3BC0` (all 13 direct calls) | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| Message manager | not anchored here | immediate record at `+0x70`, `ImmediatePlayerId` `+0xD4`, `QueueAudioPaths[slot][100]` | - | 100-byte audio path interval | header source + static | [SE_2_11_SPEECH_PATH.md](../SE_2_11_SPEECH_PATH.md) |
| Render-buffer manager (transient) | `0xA98820` | buffer pointer `+0`, record count `+8`; records 42 bytes (21 Int16) | - | count 0 between render phases is normal | runtime (workflow note) | [LiveProcessReadOnlyInspection.md](../../../../../../Findings/LiveProcessReadOnlyInspection.md) |
| Chore scratch globals | tribe ID `0x86C132C`, tile X/command `0x86C1330`, tile Y `0x86C1334`, patrol mode `0x86C1338`, move type `0x86C133C`; chore mode `0x85F8FEC` | scalars written by the stager, packed by `0x10AE0` | - | see [Cursor move stager](#cursor-move-stager-and-chore-scratch) | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| Current-unit scratch | `0x9302C4` | scalar | game ID | written by `0x18D460` at +0x75 and +0xFE | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| Chore handler table | `0x2C7A30` (filled at runtime) | function pointer table | chore opcode | entries 17 move, 36 target order, 71 waypoint append | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| Unit update vtable `UnitFunctionsVTable` | `0x321CB0` | per unit type | unit type | selected by dispatcher `0x182B00` | confirmed static | [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| Projectile range-class table | `0x2DA8F0` | indexed by range class | - | read by `0x18E9A0` and `0x19B630` | candidate | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |

## Key functions

| RVA | Name/role | Notes | Confidence | Evidence |
|---|---|---|---|---|
| 0x195E30 | cursor move stager | `rcx` unit manager, `edx` tribe ID, `r8d` tile X/command, `r9d` tile Y, arg5 `[rsp+80h]` patrol mode, arg6 `[rsp+88h]` move type; writes the five chore scratch globals; anchors unit/tribe/path/sound/dialogue managers and active player | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| 0x10AE0 | chore move handler / packer | reads chore mode at +0x06; `lea rdx` of the five scratch globals at +0x2C/+0x4B/+0x6A/+0x8A/+0xAA | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| 0x12BF0 | chore handler | reads chore mode at +0x06 | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| 0x176C0 | chore handler | reads chore mode at +0x04 | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| 0x11A1B0 | selected voice | called from stager with tribe manager | role per audit | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| 0x18D460 | representative unit of a group | `imul ...,490h` at +0xA7/+0xBA; writes current-unit scratch `0x9302C4`; reads active player at +0x6C | role per audit | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| 0xE0E0 | speech busy query | `rcx` = sound manager | role per audit | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| 0x3BC0 | play speech | `rcx` = dialogue manager | role per audit | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| 0xE2610 | PCL reachability | `rcx` = path manager | role per audit | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| 0xDB650 | direct tile search | `rcx` = path manager | role per audit | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| 0x86680 | `DLL_RunTick` | tick path | export, confirmed | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| 0x879A0 | `DLL_TroopSelection` | local selection, consumed before commands are sent | export, confirmed | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| 0x8C5F0 | troop command branch | inside the tick path | per MOVE_COMMAND_RELEASE audit | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| 0x182B00 | unit update dispatcher | iterates units from ID 1 with stride `0x490` from the sentinel; selects update via vtable `0x321CB0`; active unit context is a 1-based game ID | confirmed static (role names candidate) | [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| 0x13F540 / 0x171C50 / 0x167D40 / 0x161CB0 / 0x1535F0 / 0x1520D0 / 0x1547F0 / 0x16B220 / 0x17A590 / 0x178350 / 0x141DA0 / 0x170570 / 0x16E440 | ranged unit updates: Archer / ArabBallista / ArabBow / Ballista / Trebuchet / Catapult / Mangonel / ArabSlinger / BedouinHeavyCamel / BedouinSkirmisher / Xbowman / ArabGrenadier / ArabHorseman | 5/2/5/1/0/0/0/4/2/6/3/4/3 compares of native `+0x8FE` (nearest-enemy distance) against engage range, 35 total | role candidate; compares confirmed | [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| 0x18C040 | nearest-distance producer (wild / owner 0) | Chebyshev distance, initial 10000, validates global identity | confirmed static | [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| 0x18C160 | nearest-enemy distance producer | `max + (((min*2)/5)*min)/max`, clamp 32000; attack/path side effects | confirmed static | [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| 0x19A1F0 | distance refresh | part of producer audit | confirmed static | [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| 0x18E9A0 | target selection | args unit manager, unit game ID; maps unit type to projectile range class (`0x2DA8F0`); squared tile distance (world >> 3); publishes target/order fields or returns none; fallback resets native `+0x9F4` and may hand movement to `0x196280` | candidate | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md), [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| 0x196280 | movement hand-back target of `0x18E9A0` fallback | - | candidate | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| 0x19B630 | attack distance predicate | args manager, attacker ID, target ID; type `+0x6E6`, positions `+0x70E/+0x710` (native disp.); two special ranged types may set `+0x9F4 = 3` and return false without prerequisite `+0x9BA`; else squared tile deltas vs squared type range | candidate | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| 0xA1DB0 | projectile initializer | trajectory mode; default speed/angle tables; writes launch speed `+0xAA` or angle `+0xB0` of the projectile | candidate | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| 0x8D769 | interaction-range load (near) | Extender adds a loader here; distinct from `0x8D72B`, `0x8D7C4` | candidate | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| 0x12FC70..0x131422 | hunter update function extent | contains ImprovedHunters query sites | confirmed extent | [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| 0x103290 | message enqueue (immediate or queued) | writes `ImmediatePlayerId` (+0xD4) then copies the audio path; busy branch copies into `QueueAudioPaths[slot][100]` without bound | confirmed static | [SE_2_11_SPEECH_PATH.md](../SE_2_11_SPEECH_PATH.md) |
| 0xFEE50 | message function | copies at `0xFEEA0` into immediate record `manager+0x70`; reads `ImmediatePlayerId` after the copy; contains `sprintf` call block at `0xFEF21` | confirmed static | [SE_2_11_SPEECH_PATH.md](../SE_2_11_SPEECH_PATH.md) |
| 0xEF20 | sound-manager path copy | needs length review | open | [SE_2_11_SPEECH_PATH.md](../SE_2_11_SPEECH_PATH.md) |
| 0x11FC2C / 0x11FC38 | tribe stored-target writes | WORD write to native `+0x64C`, DWORD write to `+0x650` | confirmed static | [native-field-catalog.json](../native-field-catalog.json) (INTEGRATION guide), [RAID_RETARGET.md](../RAID_RETARGET.md) |
| 0xCEB10 | `c_game_ai_get_buy_price` | `int (playerManager, playerId, good, amount)`; reads `[rcx+good*8+0x1817B8]`; Int32 unchecked overflow; callers `0x3E000`, `0x29000` | confirmed (identity, semantics, ABI) | [native-field-catalog.json](../native-field-catalog.json), [MARKET_PRICE_QUERIES.md](../MARKET_PRICE_QUERIES.md) |
| 0xCEB90 | `c_game_ai_get_sell_price` | same ABI; `+0x1817BC`; caller `0x29000` | confirmed | [native-field-catalog.json](../native-field-catalog.json) |
| 0xC07C0 | `c_game_building_get_type_height_offset` | read-only type-dependent height offset, not absolute height | confirmed semantics, ABI candidate | [native-field-catalog.json](../native-field-catalog.json), [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| 0x184FD0 | `c_game_unit_update_standing_height` | read-write `GameUnit+0xB6`, writes `+0xB8`; not a getter | confirmed semantics, ABI candidate | [native-field-catalog.json](../native-field-catalog.json), [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |

## Data and fields

Unit fields: view offset first, native displacement (+0x65C) in brackets where native code was quoted. Catalog `rv` = the catalog grants a verified read binding.

| Record/manager | Offset | Meaning | Type/sign/unit | Confidence | Evidence |
|---|---|---|---|---|---|
| GameUnitManager | +0x0 / +0x4 | `r_NextUnitId` / `r_TotalUnits` | UInt32; total rebuilt mid-tick | confirmed runtime | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md) |
| GameUnitManager | +0x24 | `r_SelectedChimpsCount[9]` | UInt32 x9 | candidate, no read | [native-field-catalog.json](../native-field-catalog.json) |
| GameUnitManager | +0x65C | unit array origin (sentinel slot 0) | stride 0x490 | confirmed runtime | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md) |
| GameUnit | +0x34 | cached health-bar blocks | UInt32, rv | confirmed | [HEALTH_BAR_RENDERING.md](../HEALTH_BAR_RENDERING.md) |
| GameUnit | +0x88 | alive/life state `r_AliveState` | Int16, 2 = alive, rv | confirmed runtime | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md) |
| GameUnit | +0x8A [0x6E6] | unit type `r_UnitChimp` | UInt16 eChimps | confirmed runtime decode; catalog keeps candidate | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md), [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| GameUnit | +0x92 | controlling player | UInt16, rv | confirmed | [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| GameUnit | +0x94 | global ID `r_GlobalId` | UInt32 | confirmed runtime | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md) |
| GameUnit | +0xAC | recruitment rally field | UInt16 | probable | [OUTPOSTS.md](../OUTPOSTS.md) |
| GameUnit | +0xB2/+0xB4 [0x70E/0x710] | world position X/Y (used by distance predicate; catalog: healer world position) | UInt16 | probable (catalog), used in candidate predicate | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md), [BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md) |
| GameUnit | +0xB6/+0xB8 | standing base height / height correction | Int16 | confirmed | [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md) |
| GameUnit | +0xC0/+0xC2 | current tile X/Y | Int16 tile | confirmed runtime | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md) |
| GameUnit | +0x29C | death marker (Extender name `r_IsKilledByProjectile`) | UInt16, rv; not proof of projectile death | confirmed; "projectile death only" rejected | [GATEHOUSE_LIVING_CAPTURE.md](../GATEHOUSE_LIVING_CAPTURE.md), [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| GameUnit | +0x2A0 | capture eligibility word | UInt16, rv only with uncertain-meaning policy | probable | [GATEHOUSE_LIVING_CAPTURE.md](../GATEHOUSE_LIVING_CAPTURE.md) |
| GameUnit | +0x2A2 [0x8FE] | world distance to nearest enemy | stored UInt16, compared signed Int16; 10000 initial, clamp 32000 | probable (catalog), compares confirmed | [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| GameUnit | +0x2BA [0x916] | addressed by Fixes unit-selection speed patches | unknown | static | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| GameUnit | +0x2BC [0x918] | AI state; ranged updates store 0xB (or 1 for Xbowman) after a range compare | Int16 | confirmed (catalog) | [OUTPOSTS.md](../OUTPOSTS.md), [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| GameUnit | +0x2CC | `r_CanAutoEngageInMelee` (upstream 2.14.3 name, was `N000001A6`) | UInt16 | candidate | [SE_2_14_3_INTEROP_ADDITIONS.md](../SE_2_14_3_INTEROP_ADDITIONS.md) |
| GameUnit | +0x2D0 | current health percentage | - | confirmed runtime | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md) |
| GameUnit | +0x2D4 | tribe ID | - | confirmed runtime | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md) |
| GameUnit | +0x2FA / +0x334 / +0x364 / +0x37C | worker work mode / workshop ID / workshop global ID / animation variant | UInt16/UInt16/UInt32/UInt16 | probable | [WORKSHOP_IDLE_DELAY.md](../WORKSHOP_IDLE_DELAY.md) |
| GameUnit | +0x33C | `r_AttackingUnitGlobalId` (was `N000001C2`) | UInt32 | candidate | [SE_2_14_3_INTEROP_ADDITIONS.md](../SE_2_14_3_INTEROP_ADDITIONS.md) |
| GameUnit | +0x35E [0x9BA] | prerequisite state for the two special ranged types in `0x19B630` | unknown | candidate | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| GameUnit | +0x398 [0x9F4] | attack state (set to 3 by `0x19B630`, reset by `0x18E9A0` fallback) | Int16 | candidate | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| GameUnit | +0x39A / +0x39C | fireman target building ID / global ID | UInt16 / UInt32 | probable | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md) |
| GameUnit | +0x3C4 / +0x3C8 | current / max health raw | UInt32, rv | confirmed | [HEALTH_BAR_RENDERING.md](../HEALTH_BAR_RENDERING.md) |
| GameUnit | +0x3F4 [0xA50] | byte compared in ranged updates after the range compare | byte | static, meaning open | [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md) |
| GameUnit | +0x426 / +0x42C / +0x450 | outpost AI tribe role / `N000001FE` / healer target unit ID | UInt16 | probable / candidate / probable | [OUTPOSTS.md](../OUTPOSTS.md), [ENEMY_GATE_ASSASSINS.md](../ENEMY_GATE_ASSASSINS.md), [BEDOUIN_HEALER_TARGETS.md](../BEDOUIN_HEALER_TARGETS.md) |
| GameBuilding | +0xD6 / +0xEE / +0xF0 | owner / origin tile X / Y | - | confirmed runtime | [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md) |
| GameBuilding | +0xEC | rigid building height (not mutable tile height) | Int16, signedness withheld | confirmed | [DRAWBRIDGES.md](../DRAWBRIDGES.md) |
| GameBuilding | +0x10C / +0x10E | current / max health raw | Int16 / UInt16, rv | confirmed | [HEALTH_BAR_RENDERING.md](../HEALTH_BAR_RENDERING.md) |
| GameBuilding | +0x2A2 | gate state | UInt8 | candidate | [ENEMY_GATE_ASSASSINS.md](../ENEMY_GATE_ASSASSINS.md) |
| GameBuilding | +0x2A8 / +0x2AC | shared building link word / used-in-siege-attempt ID | UInt32 | probable rv / candidate | [AI_ECONOMY_OBSERVATION.md](../AI_ECONOMY_OBSERVATION.md), [AIV_LOBBY_SELECTION.md](../AIV_LOBBY_SELECTION.md) |
| GameBuilding | +0x2BE | on-fire counter | UInt16 | probable | [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md) |
| GameBuilding | +0x300..+0x318 | outpost block: profile mask, active tribe ID / global ID, production counter, target group size, active profile, size setting, production delay, catapult flag, vicinity initialized (+0x314 byte), group / spawn acceleration | UInt16 mostly | probable | [OUTPOSTS.md](../OUTPOSTS.md) |
| GamePlayerResources | +0x46C / +0x50C | pending incoming goods table / gold | UInt32 / Int32 rv | probable / confirmed | [DEFEAT_LOOT.md](../DEFEAT_LOOT.md) |
| GamePlayerResources | +0x21F8 / +0x2210 / +0x2244 | lord unit game ID / pause word / win-loss state | UInt32 | confirmed / probable / confirmed | [PLAYER_DEFEAT.md](../PLAYER_DEFEAT.md) |
| GamePlayerResources | +0x35BC / +0x3684 | raid candidate building IDs (100 x UInt16, 0-based within field) / live count | rv; count decides meaningful entries | confirmed | [RAID_RETARGET.md](../RAID_RETARGET.md) |
| GamePlayerResources | +0x38F0 | `r_AILastStandPhase` (was `N00004115`): upstream says 0 inactive, 8 on entry, falls toward 1 | UInt32 | candidate (upstream only) | [SE_2_14_3_INTEROP_ADDITIONS.md](../SE_2_14_3_INTEROP_ADDITIONS.md) |
| GameTribe | +0x30 [0x5A] | leader unit game ID | UInt16, rv | confirmed | [ENEMY_GATE_BUILDING_CONTEXT.md](../ENEMY_GATE_BUILDING_CONTEXT.md) |
| GameTribe | [0x2C] | player ID of the tribe | compared with active player | confirmed static | [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md) |
| GameTribe | +0x622 / +0x626 [0x64C/0x650] | stored target building ID / global ID | UInt16 / UInt32, rv; slices deliberately differ from source field boundaries | confirmed | [RAID_RETARGET.md](../RAID_RETARGET.md) |
| GameTribe | +0x628..+0x64C | ten `r_LastRangedAttackerGlobalId1..10` (slot 10 at +0x628, 1 at +0x62C, ... 9 at +0x64C); `N00000586` stays at +0x650 | UInt32 since 2.14.3 (was UInt16 at +2 with `unk` halfwords) | candidate (upstream source only) | [SE_2_14_3_INTEROP_ADDITIONS.md](../SE_2_14_3_INTEROP_ADDITIONS.md) |
| Projectile | +0xAA / +0xB0 | launch speed / launch angle (written by `0xA1DB0`) | - | candidate | [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md) |
| Player manager | +0x1817B8 / +0x1817BC (+ good*8) | AI buy / sell price per good | Int32 | confirmed | [native-field-catalog.json](../native-field-catalog.json) |
| Message manager | +0x70 / +0xD4 | immediate message record (100-byte audio path) / `ImmediatePlayerId` | - | header + static | [SE_2_11_SPEECH_PATH.md](../SE_2_11_SPEECH_PATH.md) |

## Behavior

### Cursor move stager and chore scratch
- Stager offsets: `+0x44` writes tribe ID (`r14` = movsxd edx), `+0x4B` tile Y (`r15` = movsxd r9d), `+0x52` tile X/command, `+0x59` patrol mode, `+0x5F` move type. `[confirmed, disassembly]` ([MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md))
- Tribe manager `lea` at +0x6D/+0x1C5/+0x236 before `call 0x11A1B0`; tribe stride `imul rbp,r14,688h` at +0x1CC, player compare `cmp [rcx+rbp+2Ch],eax` at +0x1D3. `[confirmed, disassembly]` ([MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md))
- Unit manager `lea` at +0xC1/+0x11D/+0x15D/+0x1A1; unit stride `imul rsi,rcx,490h` at +0xBA; path manager before `0xE2610` (+0x102) and `0xDB650` (+0x173); sound manager at +0x202; dialogue manager at +0x2A5. `[confirmed, disassembly]` ([MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md))
- The stager reads native `[tribe+0x5A]`, which with the +0x2A tribe origin is the catalog's leader unit ID (view +0x30); this equation is derived here, not stated by the audit. `TribeUnitCountOffset 0x5C` (view +0x32) and `MovementTargetAvailability 0x3A11EA4` have no confirmed anchor. `[candidate, static]` ([MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md))
- Every value in the anchor table has a second independent evidence (call-argument scan or a second instruction). Instruction offsets are fixed inside the validated function; a changed body must fail closed. `[confirmed, static]` ([MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md))

### Script Extender global resolution
- All 103 Extender AOB patterns (`GameGlobalsManager.cs`) resolve on this DLL: 66 RIP-relative, 37 `[reg+disp]`. Runtime module base + RVA equals the Extender's logged VAs for unit, building, player managers, tick, local player and tile manager. `[confirmed, runtime]` ([RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md))
- Pattern artifacts (not Vanilla facts): the patterns for `GameTileManagerVA` and `GameGatehouseManagerVA` are identical apart from a stray escaped quote, so both resolve to `0x405EDB0`; `GlobalImprovedSiegingBehaviourVA` matches twice and the first match is used. `[confirmed, static]` ([RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md))

### ID bases and indexing
- Unit, building and tribe game IDs are 1-based with a reserved slot 0; record address = origin + ID x stride. Public spans start at ID 1 (0-based index = ID-1): `spanBase + (unitId-1)*0x490` is the same address as `mgr+0x65C+unitId*0x490`. Never pass a span index where a game ID is expected. `[confirmed, runtime+static]` ([RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md), [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md), [LiveProcessReadOnlyInspection.md](../../../../../../Findings/LiveProcessReadOnlyInspection.md))
- Array element indexes inside a field (e.g. raid candidate list) are 0-based within that field and are not entity IDs. `[confirmed, catalog contract]` ([native-field-catalog.json](../native-field-catalog.json))
- Composite building-animation IDs are `buildingId + subPart * 4000`; correlate with `compositeId % 4000`. `[runtime, workflow note]` ([LiveProcessReadOnlyInspection.md](../../../../../../Findings/LiveProcessReadOnlyInspection.md))
- Prefer `StructureGrid` and current manager records to identify live objects; broad grid scans can contain stale entries. `[runtime, workflow note]` ([LiveProcessReadOnlyInspection.md](../../../../../../Findings/LiveProcessReadOnlyInspection.md))

### Read consistency during the tick
- `r_TotalUnits` read from outside mid-tick was 521 while an in-tick read in the same second gave 1971 (`r_NextUnitId` 1982); a full `r_AliveState != 0` scan found 1972 from both sides. Vanilla rebuilds the counter during the tick. `[probable, runtime, single observation]` ([RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md))
- Per-record fields read from outside can mix tick phases (an animation frame jumped back and forth over 20 reads 50 ms apart). `[probable, runtime]` ([RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md))
- The simulation tick (Extender `OnTick`) runs on a different managed thread than the Unity main thread (16 vs 1 in the observation). The Extender tick counter starts at 1 after loading and is unrelated to the native game tick. `[confirmed, runtime]` ([RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md))
- `r_AliveState` was 2 for every non-empty slot in a ~1970-unit save. `[confirmed, runtime]` ([RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md))

### Engage range and target search
- Native compares of the nearest-enemy distance are signed 16-bit although the public storage is UInt16; keep signedness separate from managed naming. `[confirmed, disassembly]` ([SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md))
- The 13 ranged update functions contain 35 compares of native `+0x8FE`; Trebuchet, Catapult and Mangonel updates contain none. Several compares are followed by `call 0x18E9A0` or by a store of 0xB into native `+0x918`. `[confirmed, disassembly]` ([SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md))
- `0x18E9A0` keeps identity, visibility, unit-type, attack and height checks after the range compare; changing the range does not replace target validity, attack-state or retreat/path decisions. `[confirmed, disassembly]` ([SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md))
- `0x19B630` compares squared tile deltas (sum) against a squared type-specific range read from the range table. `[candidate, disassembly]` ([SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md))
- Several ranged updates follow the range compare with `cmp ecx,0x186A0` (100000; meaning not audited). `[static, meaning open]` ([SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md))
- Instruction sites inside these functions that external code patches (listed with owners in [External components](../mods/memory.md#external-components)): `0x18EB20`, `0x18EB36`, `0x18ED96` (after the `0x18E9A0` entry, described with that function), `0x19B64E`, `0x19B786` (in `0x19B630`), `0xA1E0E` (in `0xA1DB0`), plus 35 compare sites in the 13 ranged updates.

### Message and speech path
- `0x103290` writes `ImmediatePlayerId` (manager `+0xD4`) and then copies the audio path into the 100-byte interval at `manager+0x70`; `0xFEE50` reads the player ID after the copy. A path longer than 99 bytes overwrites the player ID (a 134-byte path reached `manager+0xF6`, bytes 100-103 `NG_G` = `0x475F474E`). The busy branch copies into `QueueAudioPaths[slot][100]` without a bound and crosses into the next slot. Preexisting Vanilla defect; sound can still play while adjacent state is changed. `[confirmed, static + runtime log]` ([SE_2_11_SPEECH_PATH.md](../SE_2_11_SPEECH_PATH.md))
- The block at `0xFEF21` (inside `0xFEE50`) is `lea rcx,[rsp+..]; call c_game_cruntime_sprintf; lea rdx,[rsp+..]` (15 bytes, continuation `0xFEF30`); no direct incoming edge targets it. Historical DLL equivalent `0xFE041` (historical evidence only). `[confirmed, static]` ([SE_2_11_SPEECH_PATH.md](../SE_2_11_SPEECH_PATH.md))

### Field catalog (`native-field-catalog.json`)
- Content: 88 field identities (`nkf-0000NN`) on 5 record families: GameTribe 33 (incl. 20 historical halfword alternatives), GameUnit 27, GameBuilding 19, GamePlayerResources 8, GameUnitManager 1; plus 4 reviewed functions (`0xCEB10`, `0xCEB90`, `0xC07C0`, `0x184FD0`), 4 captured documents (FELDKATALOG, FUNKTIONEN, INTEGRATION, API contracts) and 105 evidence-validation entries (102 current, 3 historical bytes). Record sizes: unit 1168, building 812, player resources 22588, tribe 1672. Captured 2026-10-22 against analysis `g1E2FBFF53E33`, Extender baseline 2.14.2, source 2.14.3. `[confirmed, file]` ([native-field-catalog.json](../native-field-catalog.json))
- Each field has meanings (preferred first; competing or rejected history kept), hash-bound bindings (record size, offset, scalar type, element count/stride, `readVerified`) and evidence with source SHA-256 and line range. Offsets are relative to the public view origin, not the manager. `[confirmed, file]` ([native-field-catalog.json](../native-field-catalog.json))
- Read policy: default reads need a confirmed preferred meaning and a verified storage binding; uncertain-meaning policy only relaxes the meaning, never the storage contract; rejected meanings are never readable. `CurrentHealthRaw` exists on both unit and building; `FindByName` returns all matches. `[confirmed, catalog contract]` ([native-field-catalog.json](../native-field-catalog.json))
- Overlapping identities are alternatives, not conversions: former combined DWORD `N0000019A` at unit `+0x29C` beside the current death WORD; old tribe attacker halfword pairs beside the 2.14.3 DWORDs. `[confirmed, catalog contract]` ([native-field-catalog.json](../native-field-catalog.json))
- Query: `python -I -c` over `fields[]` filtered by `recordTypeId` (`nkr-000001` building, `-02` player resources, `-03` tribe, `-04` unit, `-05` unit manager) and `bindings[].offset`; or `tools/semantic/query.ps1 search <Name>`; or in Ghidra the `NativeKnowledge/FBCB9319` category. `[confirmed, file]` ([NATIVE_KNOWLEDGE.md](../../../../tools/semantic/NATIVE_KNOWLEDGE.md))
- `native-field-ghidra.json` is the deterministic Ghidra view plan generated from the catalog (not an independent audit): 4 functions, 88 field ids, 142 types = 88 `Fields` structures, 33 `Alternatives` structures + 11 unions, 4 `Guides` carriers, and 6 root types (`START_HERE`, `GameUnit/GameBuilding/GamePlayerResources/GameTribe_KnowledgeView`, `GameUnitManager_UnknownExtent_KnowledgeView`). Views have packing disabled; unverified storage is rendered as undefined bytes; guide carriers must never be applied to memory; the views do not retype the decompiler. `[confirmed, file]` ([NATIVE_KNOWLEDGE.md](../../../../tools/semantic/NATIVE_KNOWLEDGE.md))
- `hook-spans.jsonl` holds 4 audited hook spans (market buy/sell price entries `0xCEB10..0xCEB1A` and `0xCEB90..0xCEB9A`, monk movement decision `0x151436..0x15144A`, fireman target search entry `0xB8F40..0xB8F4A`) with original bytes, live-in/out registers and continuations. `api-contracts.jsonl` is currently empty. Details: [Hook spans](../mods/memory.md#hook-spans-registry). `[confirmed, file]`
- Vanilla facts of those spans: `0xCEB10` starts `movsxd rax,r8d; mov ecx,[rcx+rax*8+0x1817B8]`; monk update compares `[unit+0x914]` (view +0x2B8) and `[unit+0x99E]` (view +0x342) with r14w, then branches to running `0x15144C` or walking `0x151463`; `0xB8F40` (fireman nearest burning building) starts with two stack saves. `[confirmed, disassembly]` ([hook-spans.jsonl](../hook-spans.jsonl), [FIREMAN_TARGETING.md](../FIREMAN_TARGETING.md))

## Cross-topic links
- Move commands and the tick path: [MOVE_COMMAND_RELEASE.md](../MOVE_COMMAND_RELEASE.md).
- Field meanings in their feature context: [HEALTH_BAR_RENDERING.md](../HEALTH_BAR_RENDERING.md), [GATEHOUSE_LIVING_CAPTURE.md](../GATEHOUSE_LIVING_CAPTURE.md), [RAID_RETARGET.md](../RAID_RETARGET.md), [OUTPOSTS.md](../OUTPOSTS.md), [PLAYER_DEFEAT.md](../PLAYER_DEFEAT.md), [PLAGUE_HEIGHT.md](../PLAGUE_HEIGHT.md), [WORKSHOP_IDLE_DELAY.md](../WORKSHOP_IDLE_DELAY.md), [SURRENDER_SELECTION.md](../SURRENDER_SELECTION.md).
- Fixes ladder hooks at `0x111C00` and `0x6A5D0` and Fixes field `+0x916` patches: [FIXES_COMPATIBILITY.md](../FIXES_COMPATIBILITY.md).

## Open questions and contradictions
- Whether `tile index = x + MapRowLookupTable[3*y]` holds for every row (not checked against `r_CurrentPositionTileId`). No 10,000-unit read test. ([RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md))
- Tribe overlap: the audited stored-target global ID DWORD at view `+0x626..+0x629` overlaps the upstream 2.14.3 `r_LastRangedAttackerGlobalId10` at `+0x628`. The catalog keeps both as related identities; meaning and slot ordering of the ten attacker slots were not re-audited. ([SE_2_14_3_INTEROP_ADDITIONS.md](../SE_2_14_3_INTEROP_ADDITIONS.md), [native-field-catalog.json](../native-field-catalog.json))
- Unit `+0x29C`: the Extender name `r_IsKilledByProjectile` suggests projectile-only death; the catalog rejects that meaning and treats it as a generic death marker. ([SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md), [native-field-catalog.json](../native-field-catalog.json))
- `r_UnitChimp` is runtime-decoded as unit type, yet the catalog keeps its meaning at candidate (no read grant). ([RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md))
- Unit-update role names for the 13 vtable functions and the names of `0x18E9A0`, `0x19B630`, `0xA1DB0` remain candidates. `r_AILastStandPhase` semantics are upstream interpretation only.
- `0xEF20` sound-manager copy and Extender 256-byte allocation need a length review. ([SE_2_11_SPEECH_PATH.md](../SE_2_11_SPEECH_PATH.md))
- Exact player-record address formula from `+0x1343FC` (whether player 1 is at offset 0 or one stride in) is not stated by the audit. Building `r_BuildingType` offset not recorded there.
- The full extent of `GameUnitManager` is unverified; the catalog grants no manager reads.

## Touched by our mods
- GameMemReader (tool) and GameStateProbe (testmod) read these globals and records read-only: [mods/memory.md](../mods/memory.md#read-only-state-access-tools).
- APIShared NativeKnowledge publishes the field catalog and reader: [mods/memory.md](../mods/memory.md#apishared-nativeknowledge).
- Pathfinding (command runtime) detours the stager `0x195E30` and depends on these anchors: [mods/memory.md](../mods/memory.md#move-stager-anchors-in-the-command-runtime).
- ExtraFeatures and BugfixesAndQoL own the catalogued hook spans: [mods/memory.md](../mods/memory.md#hook-spans-registry).
- Script Extender range, projectile and speech hooks: [mods/memory.md](../mods/memory.md#external-components).

## Sources
- [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md): static code anchors in `0x195E30` and chore handlers for globals, strides and scratch values, each with second evidence; dependency note for the command runtime.
- [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md): runtime-confirmed manager globals, record layouts, tick consistency; GameMemReader / GameStateProbe usage (mod part).
- [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md): Extender 2.10.1 hook audit (external); Vanilla facts on selection, target selection, distance predicate, projectile initializer.
- [SE_2_11_SPEECH_PATH.md](../SE_2_11_SPEECH_PATH.md): Extender 2.11.0 speech hook (external); Vanilla message-manager overflow defect.
- [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md): Extender 2.14.1 engage-range hooks (external); dispatcher, distance producers, unit sentinel/stride mapping, 13 update functions.
- [SE_2_14_3_INTEROP_ADDITIONS.md](../SE_2_14_3_INTEROP_ADDITIONS.md): upstream 2.14.3 interop renames and tribe attacker field widening (source findings only).
- [native-field-catalog.json](../native-field-catalog.json), [native-field-ghidra.json](../native-field-ghidra.json), [hook-spans.jsonl](../hook-spans.jsonl), [api-contracts.jsonl](../api-contracts.jsonl): machine-readable field, view, hook-span and (empty) API-contract inventories.
- [NATIVE_KNOWLEDGE.md](../../../../tools/semantic/NATIVE_KNOWLEDGE.md): how the catalog is shown and refreshed in Ghidra.
- [LiveProcessReadOnlyInspection.md](../../../../../../Findings/LiveProcessReadOnlyInspection.md): secondary source, read-only live inspection workflow and ID notes.
