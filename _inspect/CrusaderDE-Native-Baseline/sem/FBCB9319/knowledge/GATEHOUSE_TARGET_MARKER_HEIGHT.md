# Movement target marker height audit

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Managed Assembly-CSharp SHA-256: BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.
Evidence: current semantic SQLite/export pseudocode, installed native machine code,
and hash-matching managed decompilation. Static contracts below are confirmed by
data flow; the proposed visual correction has not yet received game acceptance.

## Producers and terminal rendering

DLL_RunTick (0x86680) and selected-unit overlay 0x1222A0 lead to the visible tile
renderer 0x41D60. Unit IDs are one-based, stride 0x490. Target X/Y at manager-relative
+0x934/+0x936 are packed unchanged through the native row table. 0x417A0 stores
per-tile linked overlay records (stride 0x1C; indices 1..249), suppressing duplicate
file/image entries. 0x41D10 resets lists at the end of the pass.

Green move markers use native file 107, sprite 82..89, vertical offset 6, horizontal offset 12 and
flags low bits 2. The builder 0x1A13C0 mode 8 produces sprite-buffer records.
GameMap buffer cases 8/9 pass image numbers unchanged (82..89) and negate the
native vertical offset into addUpdatePixie's tile_y. That public method positions
both new and cached sprites using tile_y / 32. Increasing tile_y therefore raises
the sprite, without changing tile coordinates, identity, sorting or commands.
The separate Noesis formation-role overlay is not this sprite path. Animation/fade
operations outside the two explicitly paired image/offset families below are not broadened by this fix.

### Short click animation (additional audit and user game observation, 2026-10-10)

The user confirmed that the corrected persistent markers are now placed correctly,
but the brief markers immediately after clicking still have the original offset.
They are a second simultaneous output from the same selected-unit publisher.

| Family | File | Raw images | Horizontal offset | Vertical base offset |
|---|---:|---:|---:|---:|
| Persistent target | 107 | 82..89 | 12 | 6 |
| Short click animation | 107 | 90..105 | 10 | 6 |

Command handler 0x11B520 initializes the short countdown at global 0x67E8AD4
plus unit stride 0x490 to 16 (writes at 0x11C02E/0x11C0EC/0x11C108).
Unit tick 0x182B00 decrements its low short when nonzero: manager-relative
int index 0x1B5 is byte offset 0x6D4, corresponding to that same field.
For countdown 16..1, publisher 0x1222A0 uses image 106-countdown, offset 10,
vertical base 6 and flags 2. Machine code 0x1224AB..0x1224EA confirms the image
subtraction and horizontal argument 10 at the 0x417A0 call.
Transparency is zero for countdown >=8 and (8-countdown)*4 thereafter, packed
into the record flags' upper 16 bits. The persistent target is also emitted
while this countdown is active, with its own transparency of countdown*2.

Both records pass unchanged through 0x417A0 to the same normal mode-8 overlay
branch in 0x41D60, including its detailed/flattened and wall/elevated height
conditions. 0x1A13C0 writes image/index 4, horizontal/index 8, vertical/index 9,
transparency/index 11 and independent layerDelay/index 12. GameMap negates
index 9 into tile_y. Neither phase changes the +20 gatehouse discrepancy.

The consumer accepts only the paired ranges above, in normal Pixie mode;
image 90 with offset 12 and image 89 with offset 10 remain excluded. The same
gate type, flags, rendering, bounds, ID and activation guards apply to both.
One applied-height log per family proves both callbacks separately without
per-frame logging. The extension still requires visual game acceptance.

### Correction of the original managed parameter audit (2026-10-10)

The former claims "managed images 83..90" and "layer 12" were incorrect. The
installed initial prefix therefore filtered out intended markers. Cache calculation
increments param7 before calling 0x19C8D0 (0x1A25FD..0x1A2631); the mode-8 output
reloads and stores the original param7. EngineInterface/Director forward that same
short array to GameMap.processTestMap, without incrementing the output image.

| 21-short output index | Native input | Managed meaning | Output store RVA |
|---:|---|---|---|
| 4 | param7, raw image | image, 82..89 | 0x1A26C7 |
| 8 | param11, horizontal offset | tile_x, 12 | 0x1A272B |
| 9 | param12, vertical offset | negated into tile_y | 0x1A2742 |
| 12 | param14, independent sorting delay | _layerDelay, variable | 0x1A2774 |

The output stride is 42 bytes (21 shorts). 0x417A0 stores the horizontal offset
from its fourth parameter; 0x41D60 passes it as builder param11. Its separate
sorting delay is builder param14. It must not participate in marker recognition.
The first corrected consumer used file 107, raw image 82..89, tile_x == 12 and !hiMode;
the subsequent click-animation extension adds the separate pairing documented above.
Source-linked production-prefix tests exercise these records, both gate types,
variable delays, invalid lookups, render flags, settings, other structures and
unknown builds. Once-only installation/observed/applied logs distinguish a
published prefix from a correction actually reached in the running renderer.
Static verification does not establish visual acceptance; another game test is
required after installing the corrected build.

## Structure-height branches

0x41D60 uses terrain height (global RVA 0x42D8D8) and adjusts structure height only
when detailed rendering (int RVA 0x60AD43C) is nonzero and not both rendering
flags 0x60AD444 and 0x60AD44C nonzero. Elevated flag 0x10000000 takes precedence:
it uses 0xC07C0. Otherwise IsWall 0x100 adds 4 without a building, 6 for type 47,
or 20 for other building-backed walls (add esi,0x14 at 0x43779). Marker input is
6 - terrainHeight - structureHeight. No target-coordinate decrement is present.

Terrain lookup initialization at 0x63C40..0x63CC3 (not indexed as a function in the
semantic database) writes the identity table 0..255 for ordinary rendering; its
alternate mode writes a constant. The 20/40 difference is not a lookup scale.

Gate constructors 0x74240/0x74080, dispatched by 0x6D580, set roof tile raw height
to terrain+90, IsWall and building IDs for types 45/46. Footprints use 0x69850;
0xD8510 updates topology and 0x6CDD0/0x725E0 update visual anchors/resources.
0x6E620 handles camera-dependent graphics, without compensating marker offsets.
Load normalization 0xB9510 retains IsWall for types 45/46 and clears IsBuilding.
Unit height calculation 0x184FD0 adds 40 on ordinary building-backed IsWall tiles;
tunnel and other property branches are distinct. Unit base height is tracked
through 0x19B260. Thus these gate markers are 20 height units too low relative to
the unit standing plane, retaining the intentional six-unit marker offset.

## Towers and all five keep variants

Constructor dispatch 0x6D580 sends types 40..44 to keep construction 0x74DA0 and
types 74..78 to tower construction 0x77230. Their roof tiles use Elevated rather
than IsWall. Tower perimeter exceptions use ordinary IsBuilding instead of roof
flags. Keep type 40 uses IsBuilding, while types 41..44 use Elevated.

Both the marker elevated branch and ordinary unit elevated branch call 0xC07C0:

| Native type | Structure | Shared height |
|---|---|---:|
| 41 | Keep 2 | 92 |
| 42 | Keep 3 | 190 |
| 43/44 | Keeps 4/5 | 0 (default helper result) |
| 74 | Tower 1 | 296 |
| 75 | Tower 2 | 148 |
| 76 | Tower 3 | 180 |
| 77/78 | Towers 4/5 | 192 |

Keep 1 (type 40) has no marker structure-height adjustment; normal unit IsBuilding
adjustment is 2. This is not the gate's 20-unit discrepancy. No equivalent static
20-unit error is shown for intact towers or keeps; other sprite/pivot defects are
not excluded. Destroyed tower types and visual changes require separate in-game
counterchecks; this audit does not claim full destroyed-building visual parity.

## Consumer implementation and compatibility

BugfixesAndQoL owns a permanent Harmony prefix on public GameMap.addUpdatePixie.
It applies tile_y += 20 only to the documented green marker family on types 45/46,
IsWall without Elevated, and the same native structure-rendering condition.
Managed GameMap.getMapTile(x,y) supplies public gameMapX/gameMapY, so camera
rotation is not manually inverted. Script Extender GetTileId uses native row
lookup; GetTileBuildingId returns a one-based ID for TryGetBuildingById.
Null tiles, invalid coordinates/IDs, unknown native hashes and disabled settings
preserve Vanilla. Native addresses are read-only. Existing custom preview/overflow
formulas remain unchanged so Vanilla and custom markers share one end correction.

No corresponding Script Extender event or APIShared broker exists. APIShared
architecture keeps marker rendering in consumers; no public service is introduced.
Canonical Fixes GameMapManagedDetours targets private getPivotOffset for Arab
ballistas, not addUpdatePixie. The marker's file 107 does not use that troop pivot.
No new native hook or competing native reservation is introduced.

Acceptance still required: both gates/all camera rotations/zoom/flattened view,
Vanilla and custom preview/overflow/cached updates, local activation toggles,
map reload, intact and damaged/destroyed towers, all five keeps, SE plus Fixes.
