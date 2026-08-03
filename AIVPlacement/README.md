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

`AivPlacementEvaluator.Evaluate(...)` aggregates those element results into
`Complete`, `Partial`, `Impossible` or `NotEvaluable`. Its score retains both
native dimensions: the build prefix before the first blocked step and the
integer tile-fit percentage. Counts, the first blocking build step and every
issue remain available in the immutable result.

`EvaluateAllRotations(...)` tests the initial rotation followed by the other
three native rotations. It selects the first complete fit, preserves a positive
partial from the initial rotation, and only accepts an alternative partial above
Vanilla's 85-percent boundary. Complete and partial variants are also exposed as
separate deterministic sorted lists. An unresolved earlier variant yields
`NotEvaluable` instead of being treated as a failed fit.

The per-rotation statuses are defined as follows:

- `Complete`: at least one tile was evaluated and no tile is blocked or
  unresolved; the sequential score is the native sentinel `999999`.
- `Partial`: the first proven block occurs after build step zero, so a positive
  build prefix remains placeable.
- `Impossible`: the first proven block is build step zero. A multi-rotation
  selection also uses this status when no alternative passes the native partial
  threshold.
- `NotEvaluable`: a required native rule input or footprint is unknown, or the
  blueprint contains no evaluable tiles. This state always remains distinct
  from a proven rejection.

Implemented rules cover the native coordinate domain and validity diamond,
Height limit, Building occupancy, the proven Logic masks and mapper-specific
edge/swamp/moat profiles, existing-wall ownership, and internal overlap. Trees
and other serialized organisms are accepted because Skirmish initializes the
native mode to `1` or `99` and the AIV caller passes player ID zero; together
these values bypass organism-class rejection. Entity IDs likewise are not
treated as occupancy because player ID zero bypasses the entity-record loop.

The native-rule inventory and the evidence boundary for those reason codes are
documented in `../MapParser/Docs/AIV_PLACEMENT_RULES.md`.

## Coordinate contract

- The map keep anchor is the exact Section-1013 keep coordinate supplied by
  `MapParser.Core`, or its extended Section-4013 equivalent, not a U4 radar
  coordinate.
- The AIV keep anchor is the single keep placement parsed by `AIVParser.Core`.
- AIV columns follow map X; AIV rows run opposite to map Y.
- Vanilla rotates the complete 100x100 fit grids while retaining the world
  origin established for orientation zero. Rotated fits therefore do not rotate
  around the AIV keep marker.
- That fixed fit-grid origin is only a coordinate rule. The selected rotation is
  shared by the AIV and the real Keep/start complex, including the stockpile and
  other coupled start buildings; they must never be rotated independently.
- The stored AIV building point and its square footprint semantics come from
  `AIVParser.Core`.
- Core and associated blocked tiles remain separately identified.
- Tiles are not deduplicated across elements, preserving their source element even
  when two AIV elements overlap.
- A frame without positions remains an empty build step. A mapper without a known
  footprint remains an `AnchorOnly` element and occupies no inferred tiles.
- An anchor-only element is `NotEvaluable` during rule evaluation because its
  unknown footprint cannot safely contribute to a complete result.

## Build and test

Run `build.bat`. It builds the Release solution and executes the synthetic tests.
The tests cover projection plus positive and negative rule cases, multi-tile
evidence, mapper exceptions, the Skirmish organism bypass, deterministic overlap
ordering, and associated blocked areas.

`AIVPlacement.OracleComparison` remains a separate diagnostic executable. It
checks map/AIV SHA-256 values, resolves the Section-1013/4013 Keep anchor by its exact
coordinate without assuming a lobby player-to-slot mapping, evaluates
captured native cases and writes status, both score dimensions, the first issue
and all available raw tile evidence. For example:

    dotnet run --project AIVPlacement.OracleComparison -c Release -- \
      OracleCorpus/Captured-2026-08-03-SessionAware/v-thasos.json --limit 1

Use `--case <id>` for one reproducible case and `--output <report.json>` for a
machine-readable report. Corpus runs log progress, elapsed time and ETA with
millisecond timestamps.

Current ActiveAIVDetector 0.9.1 Oracle logs can be imported without copying
proprietary map or AIV files:

    dotnet run --project AIVPlacement.OracleComparison -c Release -- \
      import-log "<BepInEx LogOutput.log>" OracleCorpus/Captured-YYYY-MM-DD

The importer takes a shared-read snapshot because BepInEx can keep the log
open, rejects a snapshot that changes during reading, and binds every generated
corpus to the source-log SHA-256. Canonical multi-player corpora require an
explicit map-load session ID and one unambiguous `advopt_pre_build` value per
session as well as the native selection row. Session-less legacy logs, unknown
PreBuild values and manually supplied mode overrides are rejected instead of
being interpreted heuristically. A comparison returns a nonzero exit code for
errors or mismatches. If no exact building-object Keep exists, the native case is
retained as `NotEvaluable` with the concrete anchor failure instead of being
reported as an error or evaluated from the runtime-only Keep coordinate.
