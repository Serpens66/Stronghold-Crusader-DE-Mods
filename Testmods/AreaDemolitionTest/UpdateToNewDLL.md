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
| `0x9F410` | Mark static decoration on exactly one tile: `(projectileManager*, packedTile:int) -> void`; types10..15, no owner filtering | 121 |
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
User-selected conservative stale-state policy: MakeBatches checks the entire frozen
selection before partitioning; any missing/changed/foreign identity or unknown
closure returns no batches. Apply validates the complete <=32-target packet,
permissions and closures before its first mutation. Any mismatch skips the whole
packet, including otherwise valid unrelated roots. Frozen dependency associations
are not encoded separately on the wire, so partial stale-root skipping would risk
orphaning a still-live decoration. A skipped selection/packet requires a fresh
selection; receivers never independently retry targets.

Each operation repeats live root identity/owner/canonical-root, native permission
and full child/decor closure checks immediately before mutation. Previously
completed identities are tracked separately. This is required even under the
engine monitor: live SE `C4480` refund and `C4290` bulldoze hooks synchronously
raise Pre/Post events, and a subscriber may change a later plan reentrantly.
A changed remaining operation aborts the rest without rollback or further refunds;
already completed results remain Applied. Metadata is refreshed after the live
permission chain, and newly affected/unapproved children still reject execution.

Checks also bracket each separate Refund/Delete/terrain mutation helper, including
gate bridges and wall refunds. Frozen expected child identities are retained;
none is reconstructed after a refund callback. If a callback changes the target
after a successful refund, stop the optional adapter without marking a foreign
replacement or issuing another refund. Already completed effects remain Applied.
These caller-side checks cannot control a third-party subscriber mutating an
object INSIDE an existing helper's own Pre callback before that helper calls its
Original; preventing that would require changing the external hook contract.
The adapter preserves the existing live helper chain rather than installing a
competing hook or bypassing its subscribers.

## Data addresses and scoped queries

| RVA | Contract / source |
|---|---|
| `0x405EDB0` | Native tile-manager base; the helpers above take this raw base |
| `0x2F7A680` | Projectile-manager base; includes native slot zero, record stride `0xE8` |
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
queries run only after exact selected-tile overlap. PreviewSnapshot visits own
building/pitch/static-decoration records for renderer geometry without permission
queries; its incremental quote reads only the projected selected IDs and requested
ground categories. No idle polling or full-map ground scan is introduced.

The gate-child finder writes building-manager perimeter scratch at
`+0x31B7D0/+0x31B7D4`; discovery restores both values. Compound membership comes
from raw record `+0x304`, typed `N0000178A` at `+0x2A8`. The fixed array has
36 entries but is NOT universally the footprint. Query geometry uses the core
square (live size1..13) plus all36 optional points, corroborated against the live
StructureGrid. Empty hints, unrelated cells and invalid IDs are ignored. A root
with no corroborated cells is unavailable; metadata-only identity reads remain
independent of hints. This handles large gates/churches, farm outlying cells,
quarries and reserved children without a full-map scan per frame.

Stockpile constructor `0x76E80` creates four size2 GOODS_YARD records at offsets
(0,0),(3,0),(0,3),(3,3), and nine cross cells from `0x2D3060`. Cross cells have no
StructureGrid ID: tile-manager `+0x279D80` (public AlphaGfxLayer) stores their
sector ID, corroborated with GoodsyardConnection bits0x102. Probe only the bounded
8x8 neighborhood of each sector. Generic graphics-layer values are not IDs.
`0x63170/0x63080` are the corresponding removal consumers. Constructors
`0x72E10/0x73BB0/0x77400` store reserved-child parent IDs at typed `+0x192`;
canonicalization requires equal nonzero compound keys and exact ownership.
Children receive no independent refunds. QUARRYPILE canonicalizes to its same
compound QUARRY; stockpile sectors canonicalize to the first live sector.

Building targets freeze compound key in Flags and `(gridSize<<16)|reservedParentId`
in TaskIndex (parent0 for other kinds). This per-kind stamp is included in identity
and revalidation, preventing changed grouping or shape from reusing frozen approval.
PreviewSnapshot supplies own geometry without native permission scans;
BeginQuote/TryStepQuote examine only exact selected tile IDs, then permission
and indexed advisory closure. Final authority uses fresh uncached discovery.

## Preview performance and authority boundary

Performance follow-up observation2026-11-11; same canonical native hash. Preview
now creates one copied PreviewSnapshot shared by screen projection and the quote:
metadata/compound membership is indexed once, optional geometry computed once,
and the6000-slot decoration array scanned once per snapshot. Selected building
hits deduplicate by native slot before canonicalization and by canonical identity
before native eligibility. Stockpile sectors/reserved children therefore do not
repeat the same expensive79B90/EE640 query. Closure membership walks only indexed
compound members, not the entire building span for every root/member. Logical
counts read frozen metadata and do not query native state or hold EngineGate.

Snapshot preparation, bounded ground discovery and root validation are separate
resumable jobs. Native chunks use nonblocking Monitor.TryEnter and release the
monitor before returning to Unity; no job retains native pointers or a monitor
across frames. Busy engine state defers work; it is not an empty selection.
Projection and all native preparation phases share one absolute2ms frame deadline.
Root validation is governed by that deadline rather than a64-root/frame throttle;
the independent32-target network packet limit is unchanged. A single native
eligibility call cannot be interrupted: the time budget bounds starting another
unit of work, not the duration of one indivisible native call.
Each distinct root enters fresh native eligibility when its quote step executes.
An in-flight advisory quote retains completed step results while it resumes; no
eligibility result is reused by a later quote or mutation. The native unit-scan
policy, SE/Fixes hooks and final authenticated execution remain unchanged.

Approved advisory roots are published incrementally and clipped to the latest
rectangle, including their dependent targets. Only a completed fresh final quote
can obtain a renderer receipt and reach submission. Projection jobs retain frozen
screen axes/zoom; a changed map generation or rotation cancels unfinished work.
Decoration discovery in live closures is lazy after checking an affected tile's
native decoration bit. A live index is never carried across refund/delete callbacks.

Protocol4 appends the independent Walls group without changing earlier group IDs.
Wall-kind targets with IsStairs remain Fortifications; other wall-kind targets use
Walls. Height/material identity, ownership and refund contracts are unchanged.
The source-visible status box and count/type text were removed entirely.

Authorized singleplayer wolf1 acceptance completed2026-11-12. Walls run010
(.work/t/area-wolf1/010/walls/cd09d5449e11/LogOutput.log) applied all2,077 received
wall targets in65 batches with zero selected walls remaining. Combined run014
(.work/t/area-wolf1/014/combined/f4d6dadef87b/LogOutput.log) applied all1,853 received
targets in58 batches (1,245 walls,192 building objects,143 pitch,272 dug moats and
one decoration); no selected walls or alive received building objects remained.
Both reconciled native expected eligibility, red/final/received targets and actual
ApplyResults. They used1920x1080, applied zoom0.175 and an explicitly clipped,
sampled unobstructed viewport fallback; offscreen castle parts are outside this
acceptance. Synthetic instance-scoped input exercised production Runtime without
global mouse/keyboard control. The temporary diagnostic driver was removed after
these runs. This is current-mod-combination SP evidence (including SE2.14.3,
Fixes1.29.1, APIShared0.48.0 and CastlePlanner0.8.51), not an isolated Vanilla/FPS
baseline. The full loaded-mod list, timings and observation limits are in TESTPLAN.
Actual editor, multiplayer and remaining manual HUD/device cases remain deferred.

Snapshots are advisory copies, not mutation authorization. Every included cached
identity is checked against live metadata when quoted. A newly linked child or
decoration absent from an older snapshot can conservatively invalidate final
MakeBatches/Apply; it is never added silently to frozen approval. Final mutation
still uses fully fresh native closure discovery, complete packet preflight and
all per-helper reentrancy guards. Actor/session/spec scheduling belongs to Runtime.

The selector has Stockpiles at bit9 (existing groups retain their numeric values).
GOODS_YARD maps only to that group. The geometry candidate mask explicitly includes
bit9; Shift logical-type matching continues independently of the menu filter.

Offline tests exercise production PreviewIndex and CollectRegisteredTiles on
120/600/1200compound-heavy records, compare canonical roots/compound closure
with a complete reference scan, and compare duplicate geometry passes with one shared snapshot.
A recorded1200-record five-pass fixture performed7200000legacy membership reads
versus20000indexed member visits (~63.2ms versus2.7ms in that CLI observation).
Geometry tile probes halve21600->10800 (~0.23ms->0.13ms in its private grid fixture).
These are algorithm fixtures on .NET, not measurements of Mono/native query time,
projection, real game FPS or multiplayer acceptance. Timings are printed per run;
result-equivalence and work-count guarantees are the assertions.

Native eligibility scratch uses stackalloc for exactly0x80cursor bytes,
32neighbor bytes and QueryOffsets.Length(8) DWORDs instead of three heap arrays
per query. The audited finally restore set/order is unchanged; the existing
success/reject/exception private-memory suite still verifies every saved byte.

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

## Static decorations and indirect cleanup

`0x90CD0` maps flag mappers120..123 to ProjectileType10..13, Heads mapper129
to15 and Brazier mapper148 to14, queued through Chore69 (`0x172B0`). These six
are projectile records, not the similarly named eStruct menu values. Do not
include other projectiles. `0x90830` calls `0x7A340 ->0xDF9D0` before building
permission; native Chore97 (`0x19630 ->0x628C0 ->0xDF9D0`) passes maximum depth4,
starting at depth1: at most three cardinal tile steps, not a four-step radius.
The area adapter uses only its terminal exact-tile helper `0x9F410`, avoiding
unselected neighboring targets and shared BFS scratch.

`0x9F410` checks MiscDisplay bit0x1000, marks state3 for every alive2 type10..15
on the tile, and clears that bit. It does not filter owner. All same-tile effects
therefore form an atomic own closure; any foreign/unidentifiable/new record
rejects the owning operation. Native owner is LOW16 of projectile `+0x40`.
Identity is exact native slot, generation at `+0x44`, type at `+0x3E`, owner and
current tile at `+0x60` (coordinate cross-check `+0x58/+0x5A`). Unlike building
spans, projectile spans include slot0; do not add1 to their indexes.

The generation field is independently verified in canonical machine code:
`0x9B6EF` loads the shared generation counter, `0x9B6F5` stores it at record+44,
`0x9B6F8` increments it for slots>=25. Reserved economy slots1..24 (spawn types
40..47) write zero; ordinary static records use slots>=25. Generation zero itself
is accepted: nonzero is not a proven counter invariant. Exact generation equality
protects slot reuse. Semantic metadata for spawn `0x9B2B0` has no pseudocode;
these writer claims were checked by decoding the canonical PE instructions.

Delayed cleanup remains native: `0x9F960` sees state3, calls live `0x9F2D0`,
which unlinks through `0x9F490` and zeros the record. The adapter never directly
clears a projectile record or edits its linked list. A decoration is Applied
when marked3; disappearance follows the regular engine tick.

Building terminal `0x61FC0 ->0x61EF0` also calls9F410 on its core square;
wall `0x63700` calls it on its one tile. Discover these indirect six-type effects
before authorizing the parent, including compound/gate child core squares.
Root filters decide selection; every implicit own effect is previewed/frozen.
Explicit decoration operations in the same approved packet mark them before
the later building cleanup sees the cleared bit. Purely optional farm hints
are not decoration-removal tiles. No refunds apply to decorations.

Projection uses the complete signed-short row5/32 domain [-1024,1023.96875]
for its conservative broad phase, then actual managed height for exact tile quads.
The terrain conversion table's smaller numeric domain is deliberately unused.
`0x1A2D90` emits ground record kind0 row5 as signed-short height minus effect;
GameMap.processTestMap divides it by32 into the managed tile height.

Renderer correspondence (separate audit): `0x41D60` passes the native projectile
slot to `0x1A13C0`; sourceMap row10 keeps it unchanged, row20 is its type. This
allows scoped managed Fly tint lookup with slot/type/tile correlation.

## Bulldozer request-cell coverage and Shift lookup

The 2026-11-11 selection follow-up uses the installed managed assembly SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
`MainControls.computeMouseMapPosition` uses `GameMap.CalcMapTileFromMousePos`
with both height options enabled for bulldozer action6. That resolver tests
screen Y offsets -800..0 in steps2. At each step it computes ScreenToWorldPoint
and WorldToCell, then accepts the first cell for which offset/zoom plus
`testHeight*64 + buildingHeight*64` is nonnegative. If buildingHeight is zero,
the additional height is instead mirrorTileImage.rect.height-32 when positive;
constructionOrigImage overrides that sprite. There is no alpha test or direct
Fly hit test. Higher offsets take precedence; conservative hulls intentionally
do not reproduce occlusion or the two-pixel quantization exactly.

`PreviewHitCandidate` keeps a logical canonical root attached to its possible
requested cells. Building cells are actual registered core/hint/stockpile-alpha
cells from every canonical compound member. A one-neighbour broad phase covers
all view directions, but ordinary building request cells must then resolve through
requested StructureGrid to that exact canonical root. `0x90830` refreshes this
selector before `0x79B90`; plain grass beside a house cannot target the house.
Only structure-free GoodsyardRelated cells retain the audited stockpile-neighbour
fallback through `0x64F80`. Reserved children and stockpile sectors still normalize
to their canonical root. No native permission query is made for each hull cell.
Pitch and bounded ground wall/moat candidates use the same inverse. Ground
discovery inspects only the screen broad-phase request cells and their neighbours,
never a full-map wall/moat scan. Pure managed projection may select a root from
these cells, but quoting never reinterprets a request cell as a different object.

For six-type decorations, `0xDF9D0` expands only directions0,2,4,6 from the native
packed-neighbour table. Admission checks border flags RealityEdge|MapBorder
(0x30); other terrain and buildings do not block this search. Reverse coverage
has at most25 cells and checks the forward edge's destination border bit, so a
border seed is not assumed to have symmetric reachability. Native FlyGrid is a
signed slot head; record+32 is the signed next link. At most nine records are
examined, owner is low16 of+40, and the first matching type10..15 wins the query.
The query itself does not test AliveState. An unidentifiable/dead winning static
record therefore blocks Shift lookup rather than falling through to a building.
Normal snapshot coverage requires a qualifying own chain member on the target
tile; its full own same-tile closure is still checked before preview approval.
No pathfinder generation, BFS scratch, linked list or Chore is mutated.

`AtTile` follows decoration BFS order, then prepares only the eligibility inputs
from `0x459D0`. `0x90830` refreshes the building selector from the requested
StructureGrid cell; `0x79B90` retains stockpile neighbour normalization. The
intermediate `0x8AAF0` dispatcher resets cursor+68 to the requested tile; the
direct point-query contract reproduces that reset too, while preserving the
moat/pitch selector calculation and effective coordinates from `0x459D0`. The
permission output is dispatched in native90830 priority: moat, wall, signed
building/pitch slot. Canonical parents and exact-own complete closures remain
required, even when vanilla would also remove foreign same-tile decoration.

Additional read contract: native view direction at RVA `0x60AD41C`, anchored by
the complete immutable cursor-builder body at `0x459D0`, length812, SHA-256
`F46306230DDC09A978046DE8FF6778470F4241EF63AD51CA257C9D7E1514A879`.
The body reads that global for (view+4)&7, PCL at tileManager+108D8E0, native
neighbour deltas at tileManager+0, StructureGrid and the existing flat flag.
No cursor-builder delegate is created or invoked. The data offsets and complete
selector semantics remain native-hash bound; there is no cross-version pattern
fallback that would infer them from a single instruction. A mismatching body
fails startup closed. `QueryRequested` uses the same stack-allocated complete
cursor/neighbour/query/actor save and finally restore as canonical Allows.

Each budgeted quote now receives explicit frozen roots and records approved
`PreviewRootResult { Root, Closure }` entries. Live rectangle shrink can discard
an entire root and all of its indirect children without accidentally retaining
an orphan. Completed flat results are the union of these approved closures.
Native permission calls remain budgeted and authoritative MakeBatches/Apply
identity, owner, permission and reentrant pre-mutation checks remain fresh.

### Single-click cursor freshness

Managed pointer recomputation alone is insufficient when an owned visual overlay
prevented Vanilla's previous `DLL_RunTick` cursor refresh. Public DeleteBuilding
(`0x85BD0 ->0x8AAF0 ->0x90830`) refreshes coordinates, +68 and StructureGrid+24,
but leaves wall+5C, pitch+4C, moat+44 and unit-hover+30 otherwise stale.
`VanillaSingle` therefore temporarily stages fresh audited point selectors under
EngineInterface.threadLock around exactly one call to the existing public
DeleteBuilding wrapper. It restores all128 cursor bytes in finally. It does not
restore real queued commands, legitimate query outputs, unit orders or gameplay
effects. No native cursor-builder, RunTick or TroopSelection is invoked.

The fresh unit comes from public `GameMap.grabTroopsOnScreen` in its zero/zero
point branch at the frozen release position. That path tests body boxes, sorts
descending baseSortLayer and returns firstID/depth; its only stored diagnostic
change is debugGrabbedChimps. Native `0x879A0 ->0x8AC80` sets cursor+30 to the
first under-cursor ID unless the building is in front. The exact gate is:
`buildingDepth < -32766 || selectionOn || buildingDepth <= troopDepth`.
RunTick `0x86680` receives the managed CalcMapTileFromMousePos clickDepth minus49
as buildingDepth, and grabTroopsOnScreen/baseSortLayer unchanged as troopDepth.
selectionOn is the fresh `TroopSelector.selection_on OR
EditorDirector.troopSelectionBoxOn`; the private box flag requires a validated
managed access in the input owner. Established-selection flags only affect
selected-list truncation, not this hover assignment. The adapter does not alter
selection lists or flags; native90830 retains all of its existing owner/type/MP
conditions and unit-special/building/decor branches.

Reference audit bodies: `0x879A0`,377 bytes,
`73993440F8A139674E6B4654B7331DFAB34DC740CAAC734B8630F88232407715`;
`0x8AC80`,313 bytes,
`C31AF075825391F3308503FC314419EA104F0D2BD9C151A197B86CE7E787F70A`.
They are semantic evidence only; the adapter adds no delegate or fixed-address
read for them. Supported native-image hashing still gates the reproduced contract.
CLI fixtures exercise gate boundaries/ties/selection bypass and cursor restoration
after callback success/failure, while preserving the callback's legitimate output.

## Fixes low-wall refund

The immutable handler at `0x13750` contains `7F 11` at `0x13880`, jumping over the
halving block when current height minus default height is greater than 60. Normal
refund is 50%, halved to 25% for differences <=60. Fixes' documented low-wall fix
writes `EB 11`, bypassing halving for all heights. The adapter recognizes exactly
these two live states and includes the selected policy in its capability
fingerprint. Any other live state disables wall targets without disabling unrelated
building/pitch/moat functions. A later policy change also rejects wall execution.
No external setting is changed and no live code is written.

## Deferred crenellated-wall appearance and frozen identity

Follow-up audit and reproduction: 2026-11-12, canonical native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The canonical file, CURRENT.json and selected semantic dataset identity matched.
The index labels below remain `candidate`; the relevant field writes, predicates
and call order were inspected directly, including canonical machine code for the
appearance-bit write. These RVAs are evidence, not new callable bindings.

Vanilla data/control chain:

- `0x63700(tileManager*, actor:int, packedTileId:int)` clears only the target's
  wall/stair/crenellation flags with `0xFFBEF4FF`, resets its HeightGrid byte
  (`+0xD7E5A0`) to DefaultHeightGrid (`+0xDCCAC0`), removes same-tile decoration,
  updates pathfinding, and calls `0xE3B90(pathManager*,9,x,y)`.
- `0xE3B90` traverses the bounded eight-neighbour region and marks ChangedGrid
  (`tileManager+0xA20D40`) plus the relevant ten-by-ten dirty blocks. It does not
  change neighbouring wall material/height itself.
- On a later `0xCDE60` update, the ordered render-maintenance calls include
  `0x7C0A0`, `0x7D170`, `0x65830`, `0x650C0`, then `0xE5E60` dirty clearing.
  `0x7C0A0` updates visual layers and the unrelated `0x8000` logic bit.
- `0x65830` processes dirty tiles. For a wall with no registered structure and
  no active damage byte, the CrenelatedLow branch chooses a neighbour-dependent
  image. `0x5C2D0` tests opposing cardinal CrenelatedLow neighbours and absence of
  the other pair (directions0/2 cover both axes); `0x5C1C0` distinguishes corner
  patterns. If neither straight-pair branch applies, the image path additionally
  sets CrenelatedHigh on that existing wall. At **RVA `0x65EF0`**, the instruction
  is `or dword ptr [rbx+rax*4+0x898400],0x400000`; `rbx` is TileManager and `rax`
  is its current packed tile index from `+0x204E814`. Removing a neighbour can
  therefore change High from zero to one without replacing the wall. This path
  does not clear High or recalculate CrenelatedLow, IsLowWall or native height.
- These material/height fields have actual construction semantics in `0x6F230`;
  they are retained in the frozen identity. Eligibility `0x79B90` still checks
  live IsWall, structure/Goodsyard status, ownership and native proximity rules.
  It does not distinguish walls by High/Low crenellation flags. Refund `0x13750`
  still reads actual Height minus DefaultHeight, with its existing Fixes policy.

High is not globally cosmetic: existing baseline `knowledge/CRENELATED_WALLS.md`
records height/path consumers including `0x6B530`, `0x9C730`, `0xD86F0` and
`0xDC900`. The adapter neither changes nor masks the live native flag. Its
normalization applies exclusively to frozen target comparison; live permission,
rendering, pathfinding and deletion continue reading the current native state.

The full observed run is preserved at
`.work/t/area-wolf1/009/walls/e23b931662da/LogOutput.log`. It selected/sent1,975
wall targets and left672 after conservative packet rejection. Before packet5,
tile233160 changed flags `0x300 ->0x400300`, while owner1, Height106,
DefaultHeight8, unit0 and structure0 stayed unchanged; packet7 showed the same
High-only change at tile234924 with Height76/DefaultHeight8. This reproduces
Vanilla's deferred writer and explains rejection of the affected frozen targets;
it does not imply every remaining wall independently changed.

`CreateWallIdentity` is the single production builder used by WallAt. Its mask
omits **only CrenelatedHigh**; IsWall, CrenelatedLow, IsLowWall, IsStairs,
height-difference signature, owner and tile coordinates remain frozen. SameState,
fresh native permission, dependent closure checks, reentrant pre-mutation checks
and conservative packet handling are unchanged. Protocol4's layout remains
unchanged; the capability fingerprint adds `wall-stable-identity-v1`, preventing
old/new peers from accepting mismatched identity semantics.

Native CLI regression fixtures pass production-built identities through the real
SameState comparator: High-only normalization is equal, while crenellation,
low-wall/stair/material, height, owner and tile changes are rejected. This is not
a guarantee against an indistinguishable same-owner/same-material replacement;
tile-based walls expose no independent generation identity in this contract.

## Verification on updates

1. Verify canonical file hash, immutable function bodies, all caller/callee and
   record-prefix contracts, and installed Extender field/enum layouts.
2. Recheck the complete `0x79B90` write set and all callees; new scratch or gameplay
   writes must be incorporated before allowing preview queries.
3. Compare parser-free recipes with native handlers, including gate children,
   compound records, parent refund low-word exclusion and both live wall policies.
4. Run `tests/Native` for private-memory query restoration (success, rejection and
   injected exceptions), terrain progress ordering, resolver section boundaries,
   live fear families, refund boundaries, empty/large/reserved/farm/stockpile
   registered geometry, six-type ownership, zero-generation identity and closure
   conservative selection/packet preflight, missing-parent/surviving-decoration
   and new-unapproved-dependent rejection. These tests call no game engine.
5. Build the integrated mod and perform the separately authorized SP/editor/MP
   acceptance matrix. CLI fixtures do not establish rendering or actual two-peer
   agreement. No installation or game launch is part of the native fixture suite.
