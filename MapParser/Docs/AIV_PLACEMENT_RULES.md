# Native AIV placement rule inventory

This document fixes the evidence boundary for Chat 7 of
`AIV_PLACEMENT_ROADMAP.md`. It inventories the rejection families used by the
native AIV fit path and assigns stable offline reason codes. It does not yet
port the rules; that belongs to Chat 8.

## Binary and entry points

- file: `x86_64/CrusaderDE.dll`
- SHA-256: `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`
- preferred image base: `0x180000000`
- `EvaluateCandidateFit`: RVA `0x562F0`
- shared building placement validator: RVA `0x7A2D0`, size `0x4B6`

The AIV evaluator calls the validator at RVA `0x56489` as:

    validator(tileManager, tileId, 0, mapperValue, 0)

The third argument is explicitly zero and the fifth, call-mode argument is zero.
The native function returns `0` for accepted, `1` for a rejected rule and `2`
when the direct building-occupancy grid is nonzero. The caller treats both
nonzero values as a blocked AIV cell.

Before that call, `EvaluateCandidateFit` rejects a projected coordinate when
either axis lies outside unsigned `0..799` or when the native 800x800 validity
mask byte is zero. These are separate checks: the rectangular native domain is
larger than the playable diamond.

## Stable reason codes

`AivPlacementIssueKind` is a flags enum so an offline audit can retain every
applicable explanation even though the native validator stops at its first
rejecting branch.

| Reason code | Meaning and current evidence |
| --- | --- |
| `OutsideMap` | X or Y is outside the native `0..799` coordinate domain. Directly proven in `EvaluateCandidateFit`. No Tile-ID or tile evidence exists. |
| `InvalidMapTile` | The coordinate is in the rectangular domain but absent from the native validity mask/playable diamond. Directly proven before Tile-ID translation. |
| `HeightMismatch` | The live Height byte is outside validator limits prepared for the current mapper. Direct reads and comparisons are proven; the mapper profile that supplies those limits is not yet ported. |
| `TerrainBlocked` | One of the proven Logic-bit/mask branches rejects the mapper. Exact masks are listed below. This code must not turn an unknown bit into a guessed terrain name. |
| `OrganismOccupied` | The Organism grid references an organism whose native class is rejected for this mapper. The grid access and mapper-dependent switch are proven. Individual organism class names are not yet fully mapped. |
| `BuildingOccupied` | The Building/Structure grid is nonzero. The validator immediately returns native result `2`. |
| `EntityOccupied` | The Entity/TileUnitId grid leads to a live entity record rejected by the mapper-specific entity checks. Entity-record fields and exceptions still require rule-level porting. |
| `OwnerConflict` | WallOwner and native owner/player state disagree with the ownership exception required by the mapper. The owner byte is masked with `0x07` and converted from zero-based to one-based in the proven branch. |
| `InternalOverlap` | Two projected AIV elements claim the same tile. This is an offline sequential-castle reason, not a direct map-layer read by the native single-cell validator. The issue records the second element index. |
| `BuildingRuleFailed` | A mapper-/building-specific native prerequisite fails, including the special cases for walls, gates, drawbridges, stairs, paths, resources and existing entity/building records. Use only when the branch semantics is known more specifically than its raw bit pattern. |
| `UnresolvedNativeRule` | Native control flow proves that a branch can affect acceptance, but its semantic field/bit or mapper exception has not been identified sufficiently for an offline decision. This must yield `NotEvaluable` later, never a permissive pass or a guessed rejection. |

Every `AivPlacementIssue` records the projected element index, build index,
mapper value, core/associated tile kind, absolute coordinate, optional Tile-ID,
optional conflicting element index and an immutable copy of all eight snapshot
raw values. Retaining all raw values is intentional: even fields unused by the
current native path make later Oracle discrepancies reproducible.

## Map-layer inventory

The native offsets below are relative to the TileManager pointer. Their names
and element widths agree with the canonical Script Extender
`GameTileManagerView`.

| Offline field | Logical Section | Native offset | Native use in AIV validator | Reason families |
| --- | ---: | ---: | --- | --- |
| `TerrainFlags` | 1003 Logic | `0x898400`, `int32` | Multiple exact bit/mask branches | `TerrainBlocked`, `BuildingRuleFailed`, unresolved cases |
| `SecondaryLogic` | 1037 Logic2 | `0x9D2500`, `byte` | No direct read in RVA `0x7A2D0` for this AIV call | None currently; retained as evidence |
| `Height` | 1005 Height | `0xD7E5A0`, `byte` | Compared with validator state prepared for the mapper | `HeightMismatch` |
| `DefaultHeight` | 1045 DefaultHeight | `0xDCCAC0`, `byte` | No direct read in RVA `0x7A2D0` for this AIV call | None currently; retained as evidence |
| `OrganismId` | 1004 Organism | `0xA6F260`, `uint16` | Resolves an organism record and class switch | `OrganismOccupied`, `BuildingRuleFailed`, unresolved cases |
| `BuildingId` | 1012 Building | `0xB0BCA0`, `uint16` | Nonzero returns native result `2` immediately | `BuildingOccupied` |
| `EntityId` | 1026 Entity | `0xBF6C00`, `uint16` | Walks live entity records and applies owner/type exceptions | `EntityOccupied`, `BuildingRuleFailed`, unresolved cases |
| `OwnerId` | 1043 WallOwner | `0xE1AFE0`, `byte` | Low three bits participate in ownership checks | `OwnerConflict`, `BuildingRuleFailed` |

The native validator also reads TileManager placement-state fields and live
entity/organism records that are not serialized as these eight map layers.
Consequently a raw snapshot is necessary but not by itself sufficient for full
parity. Chat 8 must introduce explicit mapper profiles or return
`UnresolvedNativeRule`; it must not synthesize absent native state silently.

## Proven Logic tests, names and unknowns

The following constants are literal operands in RVA `0x7A2D0`. A branch ending
at RVA `0x7A3A8` returns blocked (`1`); the accepted path reaches RVA `0x7A77F`
and returns zero. Names in the third column are used only where the Script
Extender flag enum has an existing reverse-engineered name. They do not by
themselves prove the entire compound rule.

| RVA | Test | Known flag names / evidence status |
| ---: | --- | --- |
| `0x7A329` | `flags & 0x00000100` | `IsWall`; owner handling follows before general checks. |
| `0x7A413` | `flags & 0x00000008` | `PitchTrap`; mapper `99` has an explicit exception. |
| `0x7A5D3` | `flags & 0x00000001` | `Sea`; mappers `195..198` have an explicit exception. |
| `0x7A5E9` | `flags & 0x00000030` | `RealityEdge | MapBorder`; rejected. |
| `0x7A5F3` | bit `20` / `0x00100000` | `River`; mappers `195..198` have an explicit exception. |
| `0x7A698` | `flags & 0x10000400` | `IsBuilding | IsElevated`; rejected after owner special handling. |
| `0x7A6A4` | `flags & 0x00003000` | `IsTree | TreeProximity`; enters organism-record classification when an organism ID exists. |
| `0x7A712` | `flags & 0x0F000000` | farm-related high bits in the current enum; acceptance also depends on call mode. Compound semantics remain unresolved. |
| `0x7A723` | bit `21` / `0x00200000` | `Ford`; rejected. |
| `0x7A731` | `flags & 0x20000000` | `IsSwamp`; acceptance depends on TileManager state at `+0x204E740`. |
| `0x7A737` | `flags & 0x40000000` | `IsMoat`; acceptance depends on TileManager state at `+0x204E74C`. |
| `0x7A73C` | `flags & 0x00000180 == 0x80` | `ImpassableEdge` without `IsWall`; acceptance depends on mapper/placement state. |

Additional exact operands occur in mapper and entity/organism switches:

- mapper IDs `70..73` select four special placement-state values;
- mapper IDs `100..147` use special wall-owner handling;
- mapper IDs `195..198` are exceptions for Sea and River;
- mapper ID `99` is an exception for PitchTrap;
- entity record type `0x37`, owner fields, linked entity IDs and several record
  thresholds affect occupancy acceptance;
- organism-record classes `5..19` contain accepted switch cases while the
  default path consults additional owner/player state.

The numerical control flow is proven, but the complete gameplay meaning of
these special cases is not. Until Chat 8 establishes a mapper profile and the
necessary record semantics, such branches map to `UnresolvedNativeRule` rather
than speculative names.

## Rule boundaries for Chat 8

Chat 8 can implement these groups independently and keep each rule small:

1. Geometry rule: `OutsideMap` and `InvalidMapTile` using
   `MapTileGeometry.TryGetTileId`.
2. Direct Building rule: nonzero `BuildingId` gives `BuildingOccupied`.
3. Height rule: only after the mapper's native height limits are represented.
4. Ownership rule: low Owner bits plus the proven mapper exceptions.
5. Entity rule: only after required entity-record semantics are available
   offline; otherwise `UnresolvedNativeRule`.
6. Organism rule: only for proven organism classes and mapper exceptions;
   otherwise `UnresolvedNativeRule`.
7. Logic-mask rules: one named rule per proven mask/exception, never one large
   Boolean expression.
8. Sequential rule: detect `InternalOverlap` in AIV build order and retain both
   source element indices.
9. Mapper-specific rules: walls, gates, drawbridges, stairs, paths and resource
   buildings remain separate profiles.

`Logic2` and `DefaultHeight` must not be used merely because the snapshot
contains them. Their role in this exact AIV validator path is currently not
proven. Conversely, missing live record data must not be ignored merely because
the corresponding Tile-ID is present in the map file.
