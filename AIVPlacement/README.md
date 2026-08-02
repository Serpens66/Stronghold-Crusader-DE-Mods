# AIVPlacement offline projection core

`AIVPlacement.Core` projects a parsed Stronghold Crusader DE AIV castle onto
absolute map coordinates and evaluates the native AIV fit rules that can be
reproduced from a `MapPlacementSnapshot`. It preserves AIV build order, pause
metadata, all four rotations, core footprints, path cells, and known associated
blocked areas.

The library targets `netstandard2.0`, has no package dependencies, and references
only `AIVParser.Core` and `MapParser.Core`. Coordinates outside the map remain in
the result and are reported instead of silently clipped.

`AivPlacementRuleEvaluator.EvaluateElement(...)` returns `Placeable`, `Blocked`
or `NotEvaluable` plus immutable issues with the exact element, tile, Tile-ID and
all eight raw layer values. `EvaluateElements(...)` additionally detects claims
of the same coordinate by later AIV elements in original build order.

Implemented rules cover the native coordinate domain and validity diamond,
Height limit, Building occupancy, the proven Logic masks and mapper-specific
edge/swamp/moat profiles, existing-wall ownership, and internal overlap. The one
remaining non-evaluable branch is a tree/organism reference whose live organism
class is not serialized in the map snapshot. Entity IDs are intentionally not
treated as occupancy in this AIV path because the native caller passes player ID
zero and bypasses the entity-record loop.

The native-rule inventory and the evidence boundary for those reason codes are
documented in `../MapParser/Docs/AIV_PLACEMENT_RULES.md`.

## Coordinate contract

- The map keep anchor is the exact Section-1013 keep coordinate supplied by
  `MapParser.Core`, not a U4 radar coordinate.
- The AIV keep anchor is the single keep placement parsed by `AIVParser.Core`.
- AIV columns follow map X; AIV rows run opposite to map Y.
- The stored AIV building point and its square footprint semantics come from
  `AIVParser.Core`.
- Core and associated blocked tiles remain separately identified.
- Tiles are not deduplicated across elements, preserving their source element even
  when two AIV elements overlap.
- A frame without positions remains an empty build step. A mapper without a known
  footprint remains an `AnchorOnly` element and occupies no inferred tiles.

## Build and test

Run `build.bat`. It builds the Release solution and executes the synthetic tests.
The tests cover projection plus positive and negative rule cases, multi-tile
evidence, mapper exceptions, unresolved organism records, deterministic overlap
ordering, and associated blocked areas.
