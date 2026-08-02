# AIVPlacement offline projection core

`AIVPlacement.Core` projects a parsed Stronghold Crusader DE AIV castle onto
absolute map coordinates. It preserves AIV build order, pause metadata, all four
rotations, core footprints, path cells, and known associated blocked areas. It
also defines stable placement reason codes and immutable raw-tile evidence for
the later rule evaluator.

The library targets `netstandard2.0`, has no package dependencies, and references
only `AIVParser.Core` and `MapParser.Core`. It does not yet decide whether a
projected tile is buildable. Coordinates outside the map remain in the result so
the later placement-rule stage can explain them instead of silently clipping them.

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
The tests cover all rotations, asymmetric footprints, map-edge projection, walls,
gates, drawbridges, stairs, overlap traceability, pauses, empty/anchor-only entries,
and associated blocked areas.
