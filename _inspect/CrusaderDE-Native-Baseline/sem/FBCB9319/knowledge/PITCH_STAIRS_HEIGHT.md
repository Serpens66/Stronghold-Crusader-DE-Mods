# Pitch traps, stairs and height consistency [Scope: Vanilla + mod integration]

Observed 2026-11-01. Native SHA-256:

    FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2

Canonical source is the installed CrusaderDE.dll selected by CURRENT.json and DATABASE_INFO.json. RVAs below are bound to that identity. Generic function records remain candidate-named in the index; the additional behavioral evidence here does not silently change those records.

## Scope and evidence

The Vanilla sections record native control/data flow and isolated execution of original native bytes. Live-game counters of the PitchStairsTest mod are kept apart under "PitchStairsTest measurements [mod integration]". No multiplayer acceptance is claimed.

Reproduction artifact:

[PitchStairsAudit.py](../../../../PitchStairs/PitchStairsAudit.py)

    & 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' '_inspect/PitchStairs/PitchStairsAudit.py'

The script requires 64-bit Windows Python and pefile, verifies the native file hash, constructs a private relocated PE image and initializes synthetic tile/player/building inputs. It consumes no save file and patches neither disk DLL nor a live game. Assertions pin the observed original-code behavior of this DLL, not a presumed timeless contract or a future fix.

## Transition generation and ordinary searches are separate consumers

- 0x59380 creates pitch and stores max(0, height - 4), setting logic bit 0x8.
- 0xD86F0 generates per-tile direction bytes from terrain, heights and structure rules. Both full rebuild 0xDAA50 and local update 0xD90D0 call it.
- Its source height load is 0xD8741; the ordinary neighboring height load is 0xD8AB0.
- 0xDA590 can use direction bytes without the same additional raw-height rejection.
- 0xDAFD0 additionally reads raw source and destination heights and applies a 16-unit height comparison even for an existing direction edge. Special-structure and diagonal/moat branches have additional existing contracts.
- 0xF4930 and 0x11B520 are verified direct callers selecting these search variants according to movement state.
- 0xE1640 reconstructs paths. Existing direction edges are accepted before its mode-2 height fallback; do not infer that every source-height read there rejects the ordinary pitch/stair case.

Controlled two-cell corridor: floor (400,400), lowest stair (400,399), all other fields blocked. Direction bytes were computed with floor 8 and stair 24; they remained 0x01 and 0x10.

| Input after direction generation | DA590 forward/reverse | DAFD0 forward/reverse |
|---|---|---|
| Floor 8, stair 24, no pitch | 1 / 1 | 1 / 1 |
| Floor restored to pitch height 4, stair 24, pitch flag set, directions unchanged | 1 / 1 | 0 / 0 |

This is an original-code counterexample to the proposition that correcting direction generation alone makes all native searches consistent. It is not a claim that every unit selects DAFD0.

## AIV construction over existing pitch double-restores height

Audited chain:

    0x51790 -> 0x5CD90 -> 0x6CB20
    0x6CB20 -> 0x77CF0 -> 0xEE640 / 0xEB9A0

The examined preparation/validation path does not clear the existing pitch flag on an otherwise valid free field. The construction function first sets height from DefaultHeightGrid and adds its mapper-specific wall/stair offset. Its common pitch-removal branch then clears bit 0x8 and adds 4 to that newly formed height.

This last addition does not restore an existing depression: the field has already been reset from DefaultHeightGrid.

Controlled original-code execution of preparation and creation, default 8, mapper 185, neighboring mapper 184 at height 40 and floor at height 8:

| Previous state of the new lowest stair field | Resulting height | Flags | Floor connection both ways |
|---|---:|---|---|
| Ordinary floor | 24 | 0x8900, pitch absent | true |
| Native pitch first placed on the same field | 28 | 0x8900, pitch absent | false |

The generated 20-unit difference exists without a remaining pitch flag. A read correction conditioned only on that flag cannot recognize it. The common code also serves other freshly initialized AIV wall/stair mapper variants; the executed case is specifically the lowest stair.

0x62EB0 removes pitch records and preserves existing wall/structure height. 0x7D280 cleans records whose field no longer has bit 0x8 via that function. Do not assume this cleanup automatically repairs a previously overraised stair.

## Manual construction has a related but distinct writer error

Audited chain:

    0x7A3B0 -> 0x5C4E0 / 0x6A4C0
    0x6F230 -> target-height assignment -> shared pitch-removal branch

For stairs, 0x6F230 computes a positive increment which already places the field at the progressing attachment-wall height minus 16. The shared pitch-removal code then adds 4 again.

Controlled complete manual construction with attachment wall height 98:

| Previous state | Generated five heights | First/second stair transition both ways |
|---|---|---|
| No pitch under new stairs | 82,66,50,34,18 | true |
| Pitch only under first new stair field | 86,66,50,34,18 | false |

Validation accepted both inputs. The first transition changes from 16 to 20.

This does not justify removing the +4 branch for every operation in 0x6F230. Other wall operations add offsets to an existing depressed field and can still need the restoration. Distinguish the stair branch whose target height has already replaced that contribution.

## Independent building-access height checks

0xC90E0 checks a cached entrance and falls back through 0xBACC0 to 0xBB3D0. The latter examines perimeter candidates with raw heights, type offsets and connection rules.

Controlled boundary model: a synthetic building footprint/stored height is 24; lower entrances are on default 8, with height 8 or pitch height 4. Keep reference and entrances explicitly share PCL 1.

| Lower entrance | C90E0 | BB3D0 |
|---|---:|---:|
| No pitch, raw height 8 | 1 | 1 |
| Pitch, raw height 4 | 0 | 0 |

This proves an independent raw-height rejection with no PCL separation in the model. It is not yet a demonstration that a regular game-generated building naturally acquires precisely this boundary configuration, nor the cause assignment for a particular historical save.

## Limits

- These observations expose separate producer and consumer inconsistencies; they do not certify a complete correction.
- Damaged stair fields have already lost their pitch flag. Their provenance cannot safely be inferred from height alone.
- The two supplied castle blueprints, test_wolf_pitchstairs.aivjson and Castillo de Burgalimar Pitch Test.aivjson, contained no earlier pitch placement exactly on a later stair field in the examined frame order. The overwrite bug is additional to their original neighboring-pitch investigation.
- No universal change to rendering, fire simulation, default terrain levels or all height readers follows from these tests.

## Complete pitch-drop writer and compensation inventory

Observed 2026-11-01 for the same native hash by a full linear `.text` scan (Capstone; 462 HeightGrid accesses in about 212 pdata ranges, 136 direct HeightGrid writes) plus targeted disassembly. Static evidence.

Writers of the pitch drop and its reversal:

- 0x594CB in 0x59380 is the only `max(0, h - 4)` store of pitch placement. 0x59380 has the direct callers 0x6D580 (build dispatcher) and 0xC9A30.
- 0x804C0 (only caller 0x7D527 in 0x7D510, which runs inside 0x71670) visits every packed tile on start and load. For a PitchTrap tile with Default >= 4 it stores `Default - 4`, independent of the previous height, wall or structure state; Default 0..3 stays untouched. The same loop also clears misc-display bit 0x20 under structure conditions. Its return value is unused by 0x7D510.
- 0x71670 is reached from DLL_LoadSaveGame 0x853F0 and from 0x94350/0x100530 (DLL_LoadMapToPlay 0x84AC0). The call tree of 0x804C0 has only the roots 0x853F0, 0x84AC0 and the candidate-named 0x1002A0, 0x87C40 and 0x1054E0. DLL_PreInitMap_Editor 0x85E90 calls 0x5BC70 and 0x87D10 instead and does not reach 0x804C0.
- Loading a map into the editor calls DLL_LoadSaveGame with `bLoadingEditorMap=1` (Extender log, 02.11.2026) and therefore reaches 0x71670 -> 0x7D510 -> 0x804C0; the live counter is under "PitchStairsTest measurements". Only a newly created editor map (0x85E90) bypasses it; such a map contains no pitch yet.
- +4 reversals exist only at 0x6CCED (0x6CB20) and 0x6F6B6 (0x6F230), as documented above.
- 0x62EB0 (pitch removal) clears bit 0x8 and restores DefaultHeight when neither IsWall nor a structure is present.
- 0x7D4A0 (end of the pitch burn phase in the 0x7D280 chain) restores DefaultHeight without clearing bit 0x8 at that point.

Readers that compensate the drop, all with the predicate `PitchTrap && !(MiscDisplay & 0x2000)`. MiscDisplay is the word grid at tile manager +0xF05F40 (RVA 0x4F64CF0). Bit 0x2000 and the alternative 0x4000 are written by 0x628F0/0x635D0 from the footprint-edge test 0x6C510.

- Renderer 0x41D60, block 0x43347..0x43368: reads the screen-height table at tile manager +0x7090 (RVA 0x4065E40) instead of +0x7080 (RVA 0x4065E30), which is four int32 indices higher.
- Placement 0x676F0, block 0x68E99..0x68EC0: `esi = h + 4` before `cmp esi, 0x88`.

0xF5677, 0xF61E7 and 0xF67FF test the same bits only to choose other constants and use no height.

Raw-height consumers relevant to fire:

- 0x99420 (general fire spread): burning-tile height at 0x997B4; four neighbor heights through 0x6B910 at 0x997E1, 0x9982A, 0x99886 and 0x998DB; each with `|delta| < 25`.
- 0x6B910 returns DefaultHeight plus a building correction for gatehouse types 0x2D/0x2E, raw height plus a building correction for other structures, and raw height otherwise. It has more than 40 callers.
- 0x7D3B1/0x7D411 compare two pitch tiles with the same `< 25` bound (pitch chain).

Limits: the inventory is a pattern scan plus manual review. A compensation written in another arithmetic form could still exist. Unit stand height (0x19B260, 0x184FD0, 0x182B00) and fire-effect height (0x7D44B -> 0x9C430) read raw HeightGrid and have no compensation.

### Pointer-walking grid loops (same date and hash)

The displacement scan above misses loops that walk a grid through a register (such as `mov [r9], al` in 0x804C0). All `lea` instructions that load a Height, Default, Logic or MiscDisplay base, RIP-relative or relative to the tile manager, were therefore reviewed separately:

- 0x80320: for tiles with logic bit 0x800 and height <= Default, sets Height = Default and clears related bits.
- 0x80430: moat tiles get height 0.
- 0x71390 (called from 0x71670, 0x87460, 0x87D10): for logic bits 0x30, sets Height = Default and clears the bits.
- 0x7FBE0: Default = Height minus wall offsets 0x62/0x5A, with gatehouse averaging. It runs only from the version-gated migrations in 0x7D510 for files below version 0x78.
- 0x58FC0 stores grid pointers into the tile manager: Logic +0x204E830, Height +0x204E840, MiscDisplay +0x204E880. No native code reads them.

None of these loops lowers or compensates pitch. Managed `GameMap` takes tile heights from the native renderer's output buffer (slot 5 of 21 per tile), not directly from HeightGrid.

### Pitch fire path (02.11.2026, same hash, static disassembly)

- Pitch placement 0x59380 sets logic bit 0x8 at 0x594E7 and clears MiscDisplay bits 0x4000 (0x594F2) and 0x2000 (0x59502) on the placed tile.
- 0x7D280 walks the pitch records (stride 0x14, state word read at `[rbx-2]`): 0 → 1; 2 (burning) counts to 0x14, then becomes 3 and ignites up to eight neighbor pitch records in state 1 whose raw heights differ by less than 25. Each ignition calls 0x9C430 with the neighbor's raw height and strength 3 and sets the neighbor to 2. State 3 counts to 200, restores DefaultHeight (0x7D4A0) and becomes -1. A record whose tile has lost bit 0x8 is removed through 0x62EB0.
- 0x9C430 → 0x9C450: the position is the pixel coordinate plus a jitter from the table at 0x2DC890 (`rand & 0x3F`), so the fire can land on a neighbor tile. An existing fire on the tile is restarted (+0xC8 = 0, +0x28 = 0, strength raised to the new value). Otherwise nothing is spawned when the tile's logic has any bit of 0x1A7001B1. Otherwise 0x9B2B0 creates effect type 9 with +0xCA = strength, plus a type 0x1A effect for strength ≥ 3.
- Effect handlers sit in the qword table at RVA 0x2DA9C0, indexed by effect field +0x3E. 0x2DA9C0[9] = 0x99420. 0x9F960 dispatches it through 0x9BE80 (from 0x9FEAE) and 0x9BFC0 (from 0x9FED0). 0x99420 has no direct callers.
- In 0x99420 (effect array RVA 0x2F7A680, stride 0xE8), the spread block 0x99786.. runs only for phase +0xC8 == 0 and frame +0x28 in 1..6. The burning-tile height is loaded at 0x997B4 before `cmp ax,4`, where ax is the direction index +0xCC. Spread then needs direction < 4 and strength +0xCA > 1. Each attempt increments +0xCC at 0x9994A.

### MiscDisplay 0x2000 on pitch = visible to the local player (02.11.2026, same hash)

This corrects the footprint-edge reading above for pitch tiles. The 0x2000 writes of 0x628F0/0x6C510 apply only to building removal.

- The tile pass 0x65830 is called once per simulation block from 0xCDE60 (0xCE0E1) and from the editor path 0x5BC70 (0x5BDC4).
  - Per tile it loads `r10d = 0xFFFFDFFF` (0x67630).
  - For PitchTrap tiles it clears 0x2000 (0x682D9) and looks up the pitch record (0x695C0).
- It then sets MiscDisplay 0x800 (0x683A1) and 0x2000 (0x683D8), plus a pitch sprite id in the grid at tile manager +0x140900, when any of these holds:
  - a global flag (0x682FE);
  - record state ≥ 2, i.e. burning or burnt (0x6830B);
  - the record owner is on the local player's team (0x68316..0x68334, table 0x37EDF3C indexed by a global player index);
  - another global flag (0x68336).
- Otherwise the bit stays clear: enemy pitch that is not burning is hidden.
- Consequence: the renderer and the 0x68EAD cap draw visible pitch at its lowered stored height, a visible ditch. Hidden pitch gets the +4 compensation and looks like flat ground. Stored height is lowered for every pitch tile regardless of the bit. Because the bit depends on the local player, Vanilla can use it only for presentation; gameplay code must not branch on it.
- A full `.text` scan of `or`/`mov` word writes to +0xF05F40 / RVA 0x4F64CF0, following immediate values loaded shortly before, found 0x683D8 as the only write of 0x2000. Building removal sets it through a combined register value at 0x629EF; review found that one by hand. Register values with no nearby immediate were not resolved.
- AIV overbuild (0x6CCE3) and manual overbuild (0x6F6B1) clear PitchTrap when they set IsWall, so wall tiles never carry the pitch flag.

## Burn-out, low-height reloads and generic consumers (2026-11-02)

Same canonical native hash as above. The following facts were checked against
installed instructions and isolated executions of the complete original native
functions. Generic database function names remain candidate names; the machine
blocks and state flow described here are independently verified.

- `0x7D280` restores DefaultHeight at `0x7D497..0x7D4A8` when state 3 reaches
  timer 200, then writes signed state -1 at `0x7D4A8`. PitchTrap remains set
  until the next record cleanup through `0x62EB0`. `0xCDE60` calls `0x7D280`
  before the projectile/effect update `0x9F960` in the same simulation tick.
  Consequently the general fire comparison can observe restored height while
  the pitch flag is still set. Default 8 restores height 8, not 4; a fire source
  at 32 then passes the strict height-difference bound 25 (24, not 28).
- `0x804C0` re-lowers PitchTrap tiles only when DefaultHeight is at least 4.
  It preserves a terminal low tile's restored height: Default 3/state -1 stays
  at height 3 after loading, while Default 4/state -1 becomes height 0.
  Treating all pitch-flagged load states as lowered would conflate these cases.
- The pitch chain loads source height at `0x7D3B1` and neighboring height at
  `0x7D411`, followed by the signed difference/absolute-value comparison. Its
  source has just changed from state 2 to 3; eligible neighbors are state 1.
  The clamp used at pitch placement is nonlinear below height 4: original
  placement on floors 3/28 produces 0/24 and the chain ignites the neighbor.
  Comparing unreduced floors 3/28 instead would fail at difference 25.
- Native pitch record address is `tileManager + 0x2038E44 + id*20`, with
  one-based IDs. PackedTileId is int32 at +0, GlobalId uint32 at +4, occupied
  OwnerId int16 at +8, state int16 at +0x10 and timer int16 at +0x12.
  `0x695C0` and `0x7D280` use OwnerId != 0, not GlobalId, as occupancy.
  The signed exclusive PitchIdLimit is at manager +0x204C6C4; physical slot
  capacity is 4000 including slot zero. PackedTileId lies four bytes before
  the Extender descriptor's GlobalId. Its trailing Reserved field must not
  be mistaken for that same record's packed index.
- The ordinary editor number brush at `0x70DF0` clamps terrain height to
  8..156. Heights 0..3 therefore require separate evidence for custom/imported
  maps or Extender editing; the native byte layout itself supports them.

Generic height consumers also matter even when a particular branch never checks
PitchTrap. `0x18E9A0` uses unit base plus height addition for its ranged
height-correction marker (+0x9E2, thresholds +/-70), not a universal target
permission gate, and writes raw ground height into the look-at height for coordinate
targets. Ordinary projectile damage in `0x192750` checks projectile elevation
against actual unit height and the unit-type hit interval. The shared effect
routine `0x9C730` contains both fire/pitch branches and the separate Disease
(type 0x16) neighbor/ring surface test through `0x6B910`, strictly |delta| <120.
These are real dependencies of HeightGrid, not evidence that all readers should
be compensated or that a changed surface admission necessarily causes damage.

## Construction and pathing-data update chain (2026-11-01) [Vanilla facts]

Same hash. Static control/data flow; generic function names remain candidates.

- AIV execution 0x51790 hands the pitch group (AIV mapper 99) tile by tile to the build dispatcher 0x6D580, whose internal case 0x44 calls 0x59380. Besides the height drop and bit 0x8, 0x59380 creates the pitch record (owner, position, state), updates bookkeeping and further tile bits and marks the tile changed. It does not recompute direction bytes.
- The common completion of 0x6D580 calls 0xE3B90 (marks a neighbourhood as changed by geometric search; change-marker grid RVA 0x4A7FAF0, pitch writes 2), sets the pathfinding manager's dirty word (+0x6C) and calls 0xD95A0, which only shortens the PCL countdown (RVA 0x37ED4CC). None of them recomputes direction bytes.
- AIV walls/stairs 0x6CB20 set height = DefaultHeight + mapper offset: mappers 181/182/183/184/185 add 80/64/48/32/16 (88/72/56/40/24 at Default 8). No such height branch was found for mapper 186 in this path. 0x6CB20 updates pathing locally through 0xE0850 (`c_game_update_pathfinding_pot`).
- Manual stairs: 0x6A4C0 finds the maximum height of a matching adjacent wall; 0x6F230 places the first step 16 below it and lowers each further step by 16.
- The +4 pitch restores are `add byte [..+Height],4` at 0x6CCED (8 bytes) and 0x6F6B6 (9 bytes). Both overbuild branches first test bit 0x8 of the previous logic word (`test al,8`). At 0x6F6B6 the mapper is in SI; the stair case is `MAPPER_STAIR`.

Two pathing data layers:

- Direction bytes (8 bits per tile, RVA 0x51890D0) come from 0xD86F0 (ordinary branch: height window +/-16). PCL regions (16 bits per tile, RVA 0x50EC690) are flooded over the existing direction bytes; connection records sit at pathfinding manager 0x60AD660 +0x2024, stride 0x204, and also cover gate relations.
- 0xDAA50 is the full pass over all packed tiles, followed by diagonal post-processing 0xE04B0. 0xE49D0 rebuilds PCL from the existing direction bytes and does not call 0xDAA50; inside it, 0xC5040 updates buildings and 0xC4BF0 toggles gates temporarily. 0xF4540 updates connection records. In the examined chain 0xE2610 answers PCL-to-PCL reachability for a player.
- Start/load normalization 0x71670 runs 0xDAA50, then 0xE49D0 with force, then building and connection updates.
- PCL numbers are reassigned by rebuilds and are no stable identities. Different PCL numbers alone do not prove unreachability; connection records and their player/gate conditions belong to the check.
- The ordinary route paths 0xF4930, 0xF3060, 0xF32B0, 0xDAAC0 and 0xDA590 consume existing direction bytes. 0xDAFD0 adds its own raw-height check (see above).

## Building reachability and demolition marking (2026-11-01) [Vanilla facts]

- 0xC90E0 returns 1 (valid reachable entrance), 2 (geometrically suitable entrance, but no matching connection to the keep region in that search branch) or 0 (no entrance). It checks the stored entrance first (height, terrain, keep PCL, PCL connection, type exceptions) and falls back to 0xBACC0 (preparation) and 0xBB3D0 (perimeter search with several branches and type-dependent differences).
- Its terrain check uses mask 0x50501481 with special cases for some vegetation and building types; PCL flooding uses its own mask 0x4A5014B1. The masks are not interchangeable walkability rules.
- 0xC8F50 checks the buildings of one player, skips types through the table at RVA 0x2E4FE0, sets state 3 on result 0 or 2, and with a non-zero group (record +0x2A8) can propagate state 3 to further active buildings of that group. In the examined flow 0x55E10 then temporarily avoids the coarse AI build area.
- State 3 was measured as the demolition mark in isolated execution; the terminal demolition was not simulated there.
- The keep reference for the PCL check is the packed tile at player record +0xA0 (player base RVA 0x379AF10, stride 0x583C).

Raw tile-height reads of the pathing and access consumers (machine scan of this hash):

| Function | Height-read RVAs |
|---|---|
| 0xD86F0 | 0xD8741 (source), 0xD8AB0 (neighbour) |
| 0xDAFD0 | 0xDB1FE, 0xDB2FE, 0xDB391, 0xDB424 (source, destination, both corner tiles) |
| 0xC90E0 | 0xC9171, 0xC91BD (building ground, stored entrance) |
| 0xBB3D0 | 0xBB545, 0xBB74C, 0xBB942 (candidates of its search passes) |
| 0xE1640 | 0xE1874, 0xE1880, 0xE18F7, 0xE1970 (mode-2 fallback) |

0xE1874 and 0xE1880 are too close for two separate inline hooks. Stored building heights and type offsets are separate building scalars, not tile reads.

Memory layout used by the isolated experiments (bound to this hash):

- Tile manager RVA 0x405EDB0 (it also starts with row-dependent neighbour deltas). Logic +0x898400 (RVA 0x48F71B0, uint32), Height +0xD7E5A0 (RVA 0x4DDD350, byte), DefaultHeight +0xDCCAC0 (RVA 0x4E2B870, byte); building occupancy RVA 0x4B6AA50 (16 bits per tile).
- Building manager RVA 0x64CCBB0: +0x50 building search limit; record = +0x5C + id*0x32C (slot 0 exists); record +0xD0 state, +0xD2 native type, +0xEE/+0xF0 anchor X/Y, +0xFE/+0x100 stored entrance X/Y, +0x2A8 group (all 16 bits).
- Save sections with the same records: 3003 logic (320800 uint32), 3005 height, 3045 DefaultHeight, 3012 building occupancy, 3021 PCL, 4013 4000 building records of 0x32C, 2022 nine player records of 0x583C, 1107 nine native AIV slots of 0x6D98 (prepared steps and tile buffers), 1031 200 connection records of 0x204. Save-record offsets are not manager or Extender-structure offsets without a separate check.
- AIV offsets are no packed tile indices. For one unrotated slot (origin 349/448) the observed mapping was X = 349 + n mod 100, Y = 547 - floor(n/100). Native AIV preparation partly reorders the tiles within a group; native step 0 is a bookkeeping entry and the keep (step 1) is generated separately.

## Stale direction bytes after pitch placement (2026-11-01) [Vanilla facts]

Isolated execution of original functions on reconstructed data of `test2_pitchstrais.sav` (SHA-256 38c6ac5e6b1e16d1d6fc8e5c8df85f304d17ab08924fe750b85f10eff78ca92f; the file now at that path has other content). The AIV stair foot (428,478) has height 24, Default 8; the pitch strip at Y 479 lies in front of it and was built after the stair (AIV frame 203 of 205).

| State | Height (428,479) | Direction bytes (428,479) / (428,478) | Reachability of 5 compared buildings | 3 ordinary routes (0xDA590) | New state-3 marks |
|---|---:|---|---|---|---:|
| Before pitch | 8 | 0x7D / 0x78 | 1 | 1/1/1 | not measured |
| After seven real 0x59380 calls, 0xE3B90, dirty word, 0xD95A0 and PCL rebuild | 4 | 0x7D / 0x78 | 1 | 1/1/1 | 0 |
| After an additional full 0xDAA50, PCL and connection update | 4 | 0x7C / 0x40 | 2 | 0/0/0 | 25 |

- The pitch placement changed 0 bytes of the whole direction array; only the full pass removed the connection.
- Counterchecks on the saved state with the chain 0xDAA50 -> 0xE49D0(force) -> 0xF4540 -> 0xC8F50: clearing only bit 0x8 at height 4 changed nothing (25 marks). Computing direction bytes with height 8 and restoring height 4 and bit 0x8 afterwards gave reachability 1, all routes 1 and 0 marks. The separation comes from the direction bytes, not from a direct building check.
- Consequence: a game in progress keeps the transition from before the pitch until a full or matching local recomputation; loading runs 0xDAA50 in 0x71670. User observation (01.11.2026): new game without demolition, saved immediately, loaded, buildings demolished. A further, unreproduced user report of demolition without loading is mechanically possible because the local update 0xD90D0 also calls 0xD86F0.
- Limits: not every game system was reconstructed; PCL numbers and the 25 building IDs belong to that reconstructed state.

## Further height consumers and the pitch-bit scan (2026-11-01..02) [Vanilla facts]

- Unit stand height: 0x19B260 uses the stored tile height to approach the unit's stand height and for a speed value; 0x184FD0 contains further stand-height logic; 0x182B00 uses both in the position update. Isolated single call, target ground height 8, base speed 15: start stand height 4 gives stand 7 and `r_CurrentSpeed2` 18; start 8 gives 8 and 15. These are single-call values, not route durations.
- General fire spread (0x99420), isolated branch test: a burning pitch tile at height 4 next to ordinary height 32 (|delta| 28) attempts no spread; at height 8 (24) it calls 0x9C120. 0x9C120 calls the RNG 0x7530 before its target checks, so an additional attempt changes the random sequence even when no new fire results.
- Direct fire damage 0x99420 -> 0x9E030 -> 0x192400 walks the unit list of the tile; no comparison between fire height and unit stand height was found there.
- A further raw-height read for fire-effect height is at 0x9C8B8.
- Renderer compensation (derived): for Default 0..3 the stored drop is clamped at 0, so the +4-index branch draws hidden pitch at index 4 rather than at Default.
- Scan (02.11.2026): all `.text` functions that access Height (+0xD7E5A0 / RVA 0x4DDD350) and test logic bit 0x8, assigned through `.pdata`: 29 functions. Most hits are `and` masks of building and terrain writers that keep bit 0x8. The others:
  - 0x639C0 (moat over pitch) clears PitchTrap and sets height 0;
  - 0x5D740 and 0x6D580 clear PitchTrap (mask ...F7) and set height = Default;
  - 0x10DDC0 and 0x17CF50 (units) only look up the pitch record at the unit tile for ignition, without a height decision;
  - 0x9C730 ignites pitch and reads the height only for the effect height;
  - 0x65830 and 0x7D280 as described above.
- Scan limit: pitch tests on logic values that were loaded into a register earlier are not caught; the known cases are 0x6CB20, 0x6F230 and 0x41D60.

## PitchStairsTest measurements [mod integration]

PitchStairsTest (`Testmods/PitchStairsTest`, variant B: pitch keeps the ground height) consumes the writer and compensation inventory above. Its contract, sites and dated runs are in [UpdateToNewDLL.md](../../../../../Testmods/PitchStairsTest/UpdateToNewDLL.md) and [PITCHSTAIRS.md](../../../../../Testmods/PitchStairsTest/docs/PITCHSTAIRS.md).

- 02.11.2026, game log: loading `test_pitch.map` into the editor (`EditorLoaded`) reported `loadPitchTiles=84` from the load stub inside 0x804C0, confirming the editor load path through DLL_LoadSaveGame.
- 02.11.2026 (PitchStairsTest DEVDIAG, log SHA-256 5D8F4AEE…FC1F): 0x997B4 ran 648 times, 305 of them on PitchTrap tiles, and all 305 had MiscDisplay 0x2000 set.
