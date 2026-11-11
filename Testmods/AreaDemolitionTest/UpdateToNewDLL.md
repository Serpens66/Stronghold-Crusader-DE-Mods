# AreaDemolitionTest native contracts

Audited and implemented against canonical `CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Observation: 2026-11-11. Resolve the native baseline through its `CURRENT.json`;
the installed DLL was independently hashed. Internal function labels remain
candidate names; the particular instructions and argument/side-effect contracts
below were checked against the installed machine code. Game and multiplayer
acceptance are separate from these static/private-memory checks.

## Ownership and update policy

This adapter installs no native detours or code patches. Its delegates enter the
live functions, preserving Script Extender events, Fixes patches and BFQoL terrain
callbacks. The plugin/runtime owns the adapter and its delegates for the process.
No native call or address is available until the canonical file hash and the
immutable load-context function bodies have been validated.

`NativeBindings.Bind` first validates each reference entry in the immutable loaded
image. If its prologue differs, the exact audited prologue is searched only in PE
sections marked executable; zero or multiple matches fail. The complete resolved
body must still have the recorded SHA-256. Known hashes do not perform a full scan
when the reference entry is valid. No live instruction is changed.

Unknown native hashes fail closed. There is deliberately no cross-version layout
fallback: an instruction prologue cannot establish the cursor scratch layout,
building-record prefix, terrain layers, compound relations and moat-task identity
together. Update these contracts and fixtures before enabling a new image.

## Callable functions

All addresses are RVAs, using the Windows x64 ABI. Hashes/prologues are recorded
alongside the bindings in `src/NativeBindings.cs`.

| RVA | Role and native parameters | Byte length |
|---|---|---:|
| `0x79B90` | Eligibility: `(tileManager*, actor:int, x:uint, y:uint) -> void`; writes query scratch | 1414 |
| `0xC4480` | Refund: `(buildingManager*, buildingId:int, actor:int, percentage:int) -> void` | 1320 |
| `0xC4290` | Mark building and compound records: `(buildingManager*, buildingId:int) -> void` | 257 |
| `0xB9330` | Adjacent building: `(buildingManager*, unusedOwner:int, x:int, y:int, size:int, type:int, excludedId:int) -> int` | 269 |
| `0x62EB0` | Remove pitch: `(tileManager*, oneBasedPitchId:int) -> void` | 176 |
| `0x63700` | Remove wall: `(tileManager*, actor:int, packedTile:int) -> void` | 348 |
| `0x69470` | Complete fill step: `(tileManager*, taskIndex:int) -> int` | 237 |
| `0x2960` | Native sound command: `(unusedManager*, x:int, y:int, command:int) -> void` | 40 |

The original paths are `EngineInterface.DeleteBuilding` -> `DLL_MapAction`
`0x85BD0` -> `0x8AAF0` -> `0x90830`, with execute handlers `0x12400` (Chore 29)
and `0x13750` (Chore 41). Those handlers parse their own native transfer buffer;
they are **not** called from an authenticated custom Chore. The adapter implements
their parser-free execute sequence using the above live helpers. No receiver
enqueues a second native Chore and no reader buffer, slot or transfer cursor is
substituted. APIShared supplies authenticated synchronous delivery separately.

Building cleanup remains delayed: `0xC4290` marks state 3; `0xC60F0` later calls
`0xB8310`, which releases registrations/resources and zeros the terminal record.
Positive building/pitch identities are checked again immediately before mutation.
The parent refund exclusion tests the **low 16 bits** of typed building `+0x288`
(`N00001786`), not all 32 bits. The demolition flag below is set before each mark.
Gate-main/inner handling uses at most two `STRUCT_DRAWBRIDGE` searches, matching
Chore 29. All linked consequences must be owned by the actor and present in the
frozen packet. `MakeBatches` keeps each linked closure in one bounded packet;
new/unapproved children and closures larger than the packet capacity are rejected.

## Data addresses and scoped queries

| RVA | Contract / source |
|---|---|
| `0x405EDB0` | Native tile-manager base; the helpers above take this raw base |
| `0x64CCBB0` | Native building-manager base; raw slots use stride `0x32C` and a `0x5C` prefix before the typed building view |
| `0x3A11DC0` | Cursor block; first `0x80` bytes saved/restored around canonical target queries |
| `0x8DDCA0` | Eight neighboring-building scratch integers, populated by `0x64F80` and deduplicated by `0x79B90` |
| `0x88E3D70` | Native local viewpoint; temporarily scoped to the already authorized actor only for eligibility, then restored |
| `0x60AD528` | Demolition-context DWORD; set to 1 before `0xC4290`, which resets it; previous context restored on exit |
| `0x13880` | Live two-byte low-wall refund branch, described below |

The engine monitor is the actual `EngineInterface.threadLock`, resolved by
validated static-field reflection, not a new independent lock. The publicized
assembly is not used. Cursor fields relative to `0x3A11DC0`: requested x/y at
`+0x0C/+0x10`, packed tile at `+0x14`, building slot `+0x24`, moat selector `+0x44`,
pitch slot `+0x4C`, wall tile `+0x5C`, effective packed tile `+0x68`, effective x/y
`+0x6C/+0x70`. Unused selectors are zeroed. The native hover builder `0x459D0` is
not called because it can emit an unrelated presentation event.

Eligibility writes exactly these tile-manager DWORDs:
`+0x204E6FC` (blocked), `+0x204E704` (message), `+0x204E720` (signed target),
`+0x204E76C` (wall), `+0x204E770` (moat), `+0x204E774` (tile), `+0x204E7F8`
(proximity). Save/restore all seven, the eight neighbors and complete cursor
prefix, including exceptions. The adapter additionally saves/restores and zeroes
flattened targeting at `+0x204E694`. The native actor is restored before execution
so presentation keeps Vanilla's original-viewpoint behavior.

`0xEE640`, called by eligibility, reads enemy proximity state without gameplay
writes. It remains the live hooked function, preserving Extender policy. Native
queries run only for matching active-selection candidates; default building-only
capture visits the building slots and their real registered occupied cells rather
than every tile in the rectangle. Pitch scans and wall/moat area scans are gated
by requested categories. No idle polling is introduced by this adapter.

The gate-child finder writes building-manager perimeter scratch at
`+0x31B7D0/+0x31B7D4`; discovery restores both values. Compound membership comes
from raw record `+0x304`, typed `N0000178A` at `+0x2A8`. Occupied-cell arrays have
at most 36 entries (`r_OccupyTileGridSize <= 6`); this is separate from larger AIV
placement/reserved yards. Registered cells, not sprite overhang or empty bounding
box corners, establish area intersection.

## Terrain identities and terminal fill

Extender public tile/pitch views provide the hash-bound grids. The direct moat
recipe uses native task slots at tile-manager `+0x1F3EE30`, stride `0x10`, and the
misc-display ushort grid at `+0xF05F40`. The existing lookup links packed tiles to
one-based task slots. Exact task owner must match the authenticated actor; Vanilla
eligibility itself also permits allied moats, which is too broad for this feature.

At `0x1383D`, Chore 41 writes **progress byte `+0xD = 0`**, preserving work type
`+0xE`, then invokes `0x69470` once. That helper decrements to -1 and reaches its
terminal height/flag/task cleanup through `0x61E70`. Finally Chore 41 clears
misc-display pending bit `0x400`; the adapter follows the same order. It enters
the live helper and therefore also executes BFQoL's `0x694D3` soil-restoration
callback. Un-dug plans use a different operation and are intentionally excluded.

Pitch records use slot plus global identity. Wall identity retains its structural
flag mask and height variant; native terrain ownership uses the low three wall
owner bits plus one. Removal invokes the existing native helpers; it never clears
terrain by manually writing replacement grids.

## Fixes low-wall refund

The immutable handler at `0x13750` contains `7F 11` at `0x13880`, jumping over the
halving block when current height minus default height is greater than 60. Normal
refund is 50%, halved to 25% for differences <=60. Fixes' documented low-wall fix
writes `EB 11`, bypassing halving for all heights. The adapter recognizes exactly
these two live states and includes the selected policy in its capability
fingerprint. Any other live state disables wall targets without disabling unrelated
building/pitch/moat functions. A later policy change also rejects wall execution.
No external setting is changed and no live code is written.

## Verification on updates

1. Verify canonical file hash, immutable function bodies, all caller/callee and
   record-prefix contracts, and installed Extender field/enum layouts.
2. Recheck the complete `0x79B90` write set and all callees; new scratch or gameplay
   writes must be incorporated before allowing preview queries.
3. Compare parser-free recipes with native handlers, including gate children,
   compound records, parent refund low-word exclusion and both live wall policies.
4. Run `tests/Native` for private-memory query restoration (success, rejection and
   injected exceptions), terrain progress ordering, resolver section boundaries,
   live fear families and refund boundaries. These tests call no game engine.
5. Build the integrated mod and perform the separately authorized SP/editor/MP
   acceptance matrix. CLI fixtures do not establish rendering or actual two-peer
   agreement. No installation or game launch is part of the native fixture suite.
