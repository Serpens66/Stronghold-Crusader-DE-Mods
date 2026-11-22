# Stone and iron under walls [Scope: Vanilla + mod integration]

Static audit of the installed CrusaderDE.dll, SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Addresses are RVAs. Verify again against the installed DLL on update.

## Data and placement

Stone and iron are logic-grid flags `0x20000` and `0x80000` at
TileManager `+0x898400`. The editor's stone and iron paint branches in
`0x6FE90` set those bits and separately reset the soil byte at
`+0x9D2500`. The soil byte handled by the moat restoration therefore
does not encode these deposits. The placement validator has direct
resource predicates in addition to the rendered terrain, so the
logic flags are also relevant to later quarry/mine placement.

* The human/editor wall command reaches Chore 25 (`0x11B50`), then
  `0x6F230` and its validation gate `0x7A3B0`. Its per-tile commit
  at `0x6F55D` applies `AND EAX,FFF0FFFFh`, `BTS EAX,8`, then
  writes the resulting logic flags. Both deposits are lost before any
  removal. This includes high walls (mapper 25), low walls (mapper 46,
  named `MAPPER_WOODWALL` by the Extender), crenels (26), and stairs
  (27). The special conversion of an existing crenel to high wall skips
  this mask and has no second deposit to erase.
* The AIV executor `0x51790` reaches `0x6CB20`. Accepted cells
  converge at `0x6CCC8` and use the same mask at `0x6CCCF`.
  This includes the alternate low crenel mapper 35 and AIV stair steps
  181 through 185. Vanilla lacks a dedicated height branch for step
  186, which still reaches the common writer if accepted. A repaired
  tile may already lack both deposit flags.
* The ordinary building constructor's preparation mask at `0x6D711`
  is `FFBEF4F7h`, which retains both resources. Normal footprint
  writers use occupancy ORs. The specialized tower, gate, drawbridge,
  farm and storage paths also have footprint/terrain helper calls.
  For farm/gate ground brushes that run after footprint publication,
  the footprint tile is blocked by the structure layer. The generic
  soil-brush operation 2 additionally rejects ore tiles before
  writing. Ordinary terminal building footprint cleanup `0x628F0`
  uses `EFBFF0FFh`, retaining both resource bits. These paths do
  not reproduce the wall-mask loss, even though placement eligibility
  and temporary appearance vary by building. For a structure erected
  over an existing wall, a resource already erased by the wall remains
  absent in Vanilla.
  The build branches inspected include quarry `0x76780`, ordinary
  structure/iron mine `0x75780`, gatehouses `0x74240/0x74080`,
  drawbridge `0x739C0` and tower variants `0x77230`. Ground
  presentation in `0x65830` branches on the stone and iron bits
  during the dirty-tile update.
* Explicit painting, rock/cliff placement and erase tools have their
  own replacement masks, for example `0x6D130`, `0x6FE90`, and
  `0x7AE50`. They intentionally change the terrain. The moat brush
  rejects stone and iron before the soil reset.

## Wall removal and terrain publication

* Bulldozer Chore 41 reaches `0x63700`, whose successful branch resets
  height and clears the wall bits at `0x63775` with `FFBEF4FFh`.
  It leaves any resource bits that remain and publishes the changed tile
  through native path/dirty updates. A refused demolition never reaches
  this write. The map cleanup/brush callers use the same helper.
* The same wall-removal mask is used by player defeat (`0x71F0D`),
  direct clear (`0x71FF3`), area damage (`0x7E15A`), focused damage
  (`0x7ED20`) and area clear (`0xED990`). The terminal branches run
  before the normal path and dirty work. Building conversion/preparation
  can remove wall state at `0x6D711`, `0x5D76F` or `0x638D3` and
  these masks also retain both resources.
* Undo snapshots at `0x596B0` omit the original deposit flags
  (mask `00470B00h`). `0x63E90` merges the saved wall state
  into the live tile after a mask that also clears stone. It can restore
  a pre-existing wall when undoing a repair. `0x80320` separately
  normalizes zero-height stairs during map initialization.

## Runtime integration [mod integration]

The test plugin records only the two lost resource bits in an independent
packed-tile buffer. The installed hooks publish the bits immediately before
Vanilla's ordinary refresh. They leave the Vanilla covered tile state intact,
including the absence of resource bits while a wall stands. The AI capture
point at `0x6CB85` follows validation and precedes the separate Fixes hook
at `0x6CCC8`. Footprint replacement at `0x5D78A` follows the existing
moat hook span and the wall clear, and precedes final footprint publication.
Undo reads the saved wall flag before consuming history. The explicit rock
writer discards history, including the editor branch that retains the wall bit.

The native hash, original spans, packed bounds and published jump encodings
are checked independently. The standalone package's 0.1.0 local behavior
was accepted in game by the user. The 0.2.0 runtime removes the mode filter
and uses the same history for editor, campaign, trail and skirmish modes.
True multiplayer arms only after APIShared admits the capability for all
peers. The preserved schema is an atomic, simulation-relevant record. The
native restore callback restores the record before the first simulation fold.
Existing walls without a record have no recoverable origin.

Chore 65's native writer at `0x16C90` transfers the vanilla logic block;
the native checksum builder at `0x22020` reads the same grid. Neither
includes the private deposit buffer. The common executed Chore 54/67
boundaries suspend and retire private history on every peer so repair
cannot resume with contradictory wall origins. Older covered deposits
can therefore be lost on repair. The synchronized wall Undo consumer
at `0x17CC0` reads the complete native undo packet before invoking
`0x63E90`, including the wall flags used to decide whether to restore
or retain an origin.

The private fixture checked native bookkeeping and the installed RedBird
backend on the 13 original spans. Genuine Host/Client gameplay, recovery
after native repair, and the new early restore path await live acceptance.
