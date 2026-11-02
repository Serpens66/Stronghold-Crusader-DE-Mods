# Pitch traps, stairs and height consistency [Scope: Vanilla]

Observed 2026-11-01. Native SHA-256:

    FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2

Canonical source is the installed CrusaderDE.dll selected by CURRENT.json and DATABASE_INFO.json. RVAs below are bound to that identity. Generic function records remain candidate-named in the index; the additional behavioral evidence here does not silently change those records.

## Scope and evidence

This note records native control/data flow and isolated execution of original native bytes. No production patch, live-game run or multiplayer acceptance is claimed.

Reproduction artifact:

[PitchStairsAudit.py](<D:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/_inspect/PitchStairsAudit.py>)

    & 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' '_inspect/PitchStairsAudit.py'

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

Observed 2026-11-01 for the same native hash by a full linear `.text` scan (Capstone; 462 HeightGrid accesses in about 212 pdata ranges, 136 direct HeightGrid writes) plus targeted disassembly. Static evidence; the variant-B test mod of the workspace consumes it, and its live acceptance is still open.

Writers of the pitch drop and its reversal:

- 0x594CB in 0x59380 is the only `max(0, h - 4)` store of pitch placement. 0x59380 has the direct callers 0x6D580 (build dispatcher) and 0xC9A30.
- 0x804C0 (only caller 0x7D527 in 0x7D510, which runs inside 0x71670) visits every packed tile on start and load. For a PitchTrap tile with Default >= 4 it stores `Default - 4`, independent of the previous height, wall or structure state; Default 0..3 stays untouched. The same loop also clears misc-display bit 0x20 under structure conditions. Its return value is unused by 0x7D510.
- 0x71670 is reached from DLL_LoadSaveGame 0x853F0 and from 0x94350/0x100530 (DLL_LoadMapToPlay 0x84AC0). DLL_PreInitMap_Editor 0x85E90 calls 0x5BC70 and 0x87D10 instead and does not reach 0x804C0.
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
