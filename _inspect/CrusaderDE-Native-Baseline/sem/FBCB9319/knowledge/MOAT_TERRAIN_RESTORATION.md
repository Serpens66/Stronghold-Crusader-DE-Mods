# Moat terrain restoration [Scope: Vanilla + mod integration]

Observation: 2026-10-25. Canonical native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The function names in the database remain candidate symbols; the following particular
machine writes, branches and register contracts are confirmed statically against the
installed image. This is not in-game or multiplayer acceptance.

## Soil and terminal states

The native tile manager is at module `+0x405EDB0`. Its `Logic2Grid` is a byte per
packed tile at manager `+0x9D2500`, separate from logic flags `+0x898400`, height
`+0xD7E5A0`, default height `+0xDCCAC0` and structure IDs `+0xB0BCA0`. The physical
capacity is 320800. `TileType.OasisGrass` is bit `0x10`; the complete original byte
must be retained, rather than inferring grass from neighboring cells or recording
only that bit. Native readers also distinguish foliage, sand, height-edge and stone bits.

`0x639C0(manager, taskIndex)` increments the byte progress of a 16-byte moat record.
Only its completed branch (progress >=4) reaches `0x63A4E`: task state becomes 0x0204,
height becomes zero, planned/pitch flags are cleared, completed moat is set, and
`0x63A9F` explicitly writes soil zero. Task-index callers from all ten audited unit
handlers share this branch. `0x69470(manager, taskIndex)` decrements progress; positive
progress returns without removal. At <=0 it restores height 8, clears completed moat,
invalidates/rebuilds path data and removes the task through `0x61E70`. It never restores
the original soil. A resource-visual update can follow if a building occupies the tile.

`0x6FE90` iterates accepted brush cells. Its moat operation numbers are 0 plan,
1 cancel plan, 2 immediate completed placement, 3 direct removal. Modes 2/3 pass the
soil-zero writer `0x705BF`; mode 2 allocates through `0x59210` afterwards, so a hook
only on task creation would already observe the erased byte. `0x70940` is reached
after the accepted mutation and before the unchanged 3x3 update call. It is also used
by non-moat terrain operations; these represent explicit replacement, not restoration.
Some texture-only paints skip that update block, and require the accepted texture
commit at `0x7082E`. The editor's `0x7AE50` clear operation deliberately replaces soil
with zero/height-edge bits and must invalidate history, including when invoked as
preparation for another paint by `0x6FE90`.

## Bridges and replacement

`0x6D580` validates a building request before iterating its footprint. At `0x6D8DF`,
`RBX=tileManager`, `ESI=packed tile`, `R12D=native building struct type` (drawbridge 0x31).
It deletes prior moat tasks and clears the moat/planned bits before dispatching to
drawbridge creation `0x739C0`. That function writes the bridge occupancy and recreates
tasks with `0x59210`. Therefore capture/preservation has to precede the cleanup, and
the later allocation must not replace the retained origin with the current dirt.
The native bridge mapper selects its underlying moat cells; other footprint cells
also need origins because bridge removal ORs soil bit 2 on non-moat footprint cells.

`0x622D0` may remove a bridge while keeping a completed moat underneath. Its later
soil OR-2 path follows the existing height-restoration block. `0x6240E` is after the
last soil write but before structure occupancy is cleared. Remaining completed moat
retains its origin. `0x6CA30` purges the bridge footprint and its `0x6CADD` block is
after task/flag cleanup. `0xED2E0` area removal reaches `0xEDA8C` after the moat flag
and height writers and before later region/3x3 publication.

Filling a moat under a surviving bridge can finish before the bridge disappears.
Restoring soil at filling completion must retain the bridge's origin until a later
bridge demolition, otherwise the OR-2 writer can again change the restored soil.
An old untracked moat or lowered bridge is not a source of historical information.

Ordinary building preparation invalidates history. `0x5D740` is another accepted
replacement path, and vegetation creation/update `0x108320/0x109930` can clear moat
state after allocating or updating vegetation. Their committed replacement branches
invalidate history rather than repainting the new object.

## Ownership, persistence and evidence [mod integration]

BugfixesAndQoL's private native stubs own only the disjoint soil/capture spans listed
in its `MoatTerrain/NativeDefinition.cs`. ExtraFeatures retains its height hooks;
command-runtime task entry detours remain authoritative. The native-site registry
records the new exclusive spans. No new APIShared interception API is introduced.

The map-scoped ushort buffer is mod memory: planned, dug, bridge and transient direct
placement states are distinct. It never occupies spare Vanilla record fields. Only
persistent, still-relevant entries reach schema-1 `BugfixesAndQoL.MoatTerrain` via
APIShared VersionedSaveData, with map/save scope and simulation-relevant comparison.
Disabled loading discards known history. Unknown future records retain APIShared's
opaque preservation rules and cannot activate this mission's restoration.

The real `EngineInterface.SaveSaveGame` holds its private static `threadLock` around
`DLL_SaveSaveGame` (also for map saves). The mod resolves this existing object once by
validated reflection for lifecycle/save coordination; it adds no lock in native tile
paths. Missions arm after managed initialization and retire/reset through APIShared.

Retained evidence: `.work/a/moat-terrain/001` (18 complete instruction streams,
canonical byte comparisons and feature bodies); compiled-site audit and executable
production tests in `BugfixesAndQoL/tests/MoatTerrain.Tests`. The audit resolves
CURRENT.json and the current database manifest, checks whole function bytes,
instruction-aligned spans, incoming interior xrefs and own overlap. Fixtures execute
the production operations/emitter and use the installed RedBird backend on private
memory; external refresh/call destinations are explicit fixture boundaries.
Game visuals, actual farm placement and real multiplayer remain untested.
