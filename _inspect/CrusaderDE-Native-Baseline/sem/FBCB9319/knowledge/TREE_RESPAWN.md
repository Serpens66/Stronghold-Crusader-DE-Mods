# Original woodland sites and seedling placement [Scope: Vanilla + mod integration]

Checked on 2026-11-07 against the canonical native image with SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
RVAs below are evidence from that image. Generic function names in the export
remain candidate names. The field, branch and caller relationships below follow
from the decompilation, installed Extender IL and the local Extender source.

## Woodland records and deletion

- Native woodland species are chestnut, oak, pine and birch. Orchard trees,
  shrubs and minerals use separate types. The tile organism grid carries the
  one-based native vegetation slot. At manager + slot × 156 + 24 the record
  matches the Extender's `GameVegetation` layout. The Extender span starts
  at slot zero. Slot zero is reserved and the native allocation range ends
  before 4,000. The Extender's separate `TryGetVegetationById` method indexes
  the span at ID minus one, so consumers of the native deletion event and grid
  use the span index directly and cross-check coordinates and occupancy.
- The woodcutter path calls damage at `0x107C90`. A felled tree can remain
  in the organism grid while its wood is harvested. The vegetation update
  at `0x108800` handles records marked for deletion. `0x107970` calls
  `0x71D60` to clear the organism grid and tree/proximity state, updates
  pathing and zeroes the slot. Terrain and editor paths also call the deletion
  function. The final tile grid value distinguishes a completed deletion
  from the earlier fall, a damage event, or a marked record.

## Growth and placement

- `0x108320` advances woodland growth in the tick updater. Its seed branch
  uses `0x107A80` to select a neighbour, with terrain, organism, structure,
  unit, height, species and surrounding-space checks. Allocation at
  `0x107390` receives species and stage 0, writes the slot, coordinates,
  serial, health, resources, stage and initial growth progress. It can
  return zero when capacity is unavailable. The branch publishes the slot
  and tree flag, calls the proximity update at `0x59520` and the local
  pathing update, and sets the component rebuild flag.
- The editor placement at `0x6E2C0` uses the same allocator with stage 3.
  Vanilla growth has editor gates. The placement call itself accepts stage 0
  in the editor and has a separate editor capacity guard.
- Native map initialization can normalize loaded vegetation before it is
  available to gameplay. `0x109660` invokes `0x109B60`, which can remove
  invalid woodland. A capture from the completed native load therefore
  describes the accepted map vegetation.
- The simulation time contract follows [Periodic Gold](PERIODIC_GOLD.md).
  The advancing path from `DLL_RunTick` increments the map tick and
  reaches the Extender's time callback before the vegetation update.
  The editor's positive-size branch in `DLL_SetMapEditorParam` at
  `0x87460` also calls the game update at `0xCDE60`. The managed
  resize path calls that export after `SafeToSave`, outside
  `EngineInterface.run` and its monitor.

## ExtraFeatures integration [mod integration]

The feature snapshots accepted woodland positions on the central
`NativeLoaded` checkpoint and activates on managed mission start. It
combines the Extender's existing deletion notifications with a bounded
map-tick reconciliation of those positions. Planting uses the public
Extender `CreateEx` with stage 0 and the public component rebuild request.
The tile checks exclude existing terrain, buildings, units and vegetation
that would make planting unsafe. Vanilla's natural growth branch retains
its own density, species and neighbour selection.

APIShared supplies mission ownership, the synchronized map tick, and the
versioned, simulation-relevant save entry. The normal mod and Fixes hooks
retain their existing sites. Runtime acceptance requires observing live
load, deletion, placement, save/reload, editor, and matching peers.

On 2026-11-07 an isolated .NET 10 fixture linked the production scheduler
with 3,999 sites. The mean was 0.0102 ms per occupied-site sweep and
0.0350 ms for a sweep and all 250 bounded batches when all sites were due.
Both warmed loops reported zero thread-local allocated bytes. The
fixture used stub vegetation reads and writes. Native placement,
pathfinding and in-game tick costs still require measurement.
