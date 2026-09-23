# Native AIV placement rule inventory

## Current native baseline: lobby fit, Steam build 24816905

The installed `CrusaderDE.dll` has SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The preferred image base is `0x180000000`. The function identities below were
matched to the earlier build by the current semantic baseline's unique
normalized hash and control-flow comparison, then checked against its
decompiled caller/callee chain. Their RVA, VA and role are:

| Role | RVA | VA | Confidence |
| --- | ---: | ---: | --- |
| Skirmish start and per-player loop | `0x94350` | `0x180094350` | confirmed caller chain; probable semantics |
| Import one encoded candidate into the 100×100 grids | `0x55320` | `0x180055320` | confirmed identity; probable field semantics |
| Test a specific candidate | `0x54DE0` | `0x180054DE0` | confirmed identity and call chain |
| Rotate the prepared candidate grids | `0x56670` | `0x180056670` | confirmed identity and call chain |
| Scan the projected candidate against map tiles | `0x57080` | `0x180057080` | confirmed identity; probable parameter roles |
| Shared tile placement validator | `0x7B060` | `0x18007B060` | confirmed identity; probable rule semantics |
| Select a rotation for one candidate | `0x54F60` | `0x180054F60` | confirmed identity; probable selection semantics |
| Prepare the selected live layout | `0x53D00` | `0x180053D00` | confirmed identity; probable side effects |
| Execute prepared frames up to a percentage | `0x55F50` | `0x180055F50` | confirmed identity and call chain |
| Execute one prepared build step | `0x51790` | `0x180051790` | confirmed identity; probable side effects |

`0x94350` scans AI players in ID order and passes the native AIV-system pointer,
a zero-based spec index and a zero-based variant index to `0x54DE0`. The latter
loads the variant with `0x55320`, applies the spec's initial rotation with
`0x56670`, then runs `0x57080`. An absent or rejected variant returns `-2`;
a positive prefix score yields placement state `1`, while `999999` yields state
`2`. Its return value is an integer percentage. The selection path at
`0x54F60` also considers alternatives in a specific order and applies
percentage thresholds; the offline lobby aggregation must retain candidate
order and both native score dimensions.

The `0x94350` caller passes its per-player negative AIV selector to `0x54F60`:
`-1` enables alternative rotations, while `-2` and lower do not. The lobby
does not expose the native RNG state, so the offline selector currently
enumerates every possible variant start index and both rotation-mode values.
Confidence is high for this control flow and medium for its lobby setting
mapping. At `0x54F60`, the first visited full fit (`999999`) returns at once.
The initial rotation visits candidates cyclically, beginning *after* the random
index. Strict `>` comparisons preserve visit-order ties. A partial result first
selects a visited candidate above 95% fit; otherwise it selects the largest
sequential score if that score exceeds 29, or the highest percentage if it is
at least 91%. Without any positive initial score, the selector tries the next
three rotations in native order if enabled, accepts a full fit at once, and
otherwise accepts the highest percentage only from 86% upward. A partial
alternative rotation cannot displace a positive initial score. The lobby
publishes an auto candidate and rotation only when all enumerated outcomes
agree; otherwise it reports the auto outcome as undetermined and retains the
four separate rotation results. This is an offline prediction, not a change
to Vanilla's choice.

`0x57080` scans the final 100×100 mapper and frame-score grids row by row.
The later imported frame wins when two frames claim one cell. For each
nonempty cell, the spec's signed X/Y origins plus the grid column/row are
checked as unsigned coordinates in `0..799`, then against the native validity
mask and row lookup. The validator receives `(tileManager, tileId, 0,
mapperValue, 0)`: player ID zero and call mode zero. Nonzero validator results
count as blocked cells; the minimum blocked frame ordinal determines the
sequential score. The current `0x7B060` still reads the documented height,
BuildingId, Logic, owner, organism and entity layers. In this call mode,
the entity walk is skipped for player zero; the Skirmish mode and player zero
also preserve the documented organism bypass. Mapper profile lookup and
single-cell height preparation moved to `0xC77C0` and `0x696D0`.

After selection, `0x53D00` prepares native construction state. With completed
enemy castles enabled, `0x55F50` calls `0x51790` in frame order. Its
mapper-specific constructors can change subsequent map tiles differently
from the fit grid, so a preceding AIV plan cannot stand in for the resulting
live state. The lobby evaluator therefore remains `NotEvaluable` for later AI
players in that mode until a sequential model has independent Oracle evidence.

The Script Extender 2.8.0 AIV API exposes imported-variant pointers, live
village slots, build-step spans and layout grids. These are useful for
diagnostics after native import, but provide no fit query against an unloaded
`.map` in the lobby. The offline parser and snapshot remain the lobby input.
`AivSystem` is 1,930,456 bytes and must be accessed only through pointers or
the Extender's bounded views, never copied as a CLR value.

### Archived Oracle cross-checks

The archived `Log_009.log` (SHA-256
`DDB5D7E89CDDE3638852D72A724DA273746A6E849E4429DAA78E06D90024A654`)
contains 212 imported attempts on `Target Zone.map` (map SHA-256
`AA7E4C0538448C25C9514B4A2FB2C490DF8B56D483921680C13BDADD1EFD85`),
all with `advopt_pre_build=0`. With all 19 AIV sources verified against their
logged SHA-256 values, the offline comparison is exact in all 212 cases,
including native raw score, fit percentage, and evaluated/blocked cell counts.

The archived `Log_019.log` (SHA-256
`01489B611C8AAB4CBD965F45E77A9D2E9AD65196B99D782627B62641D64F258E`)
contains 145 attempts on the custom `test AI overbuild eachother.map` (map
SHA-256 `D63CD2FF3AEABA80BC3BC173BB615207666F1EAF759ECAC573FAD0DA61979DF3`),
also with `advopt_pre_build=0`. With all 27 AIV sources hash-verified, 7 cases
match exactly and 138 differ in status, sequential score, blocked-cell count,
or fit percentage. The
first discrepancy occurs for player 3 after player 2's selection; the offline
evidence includes an owner-marked wall tile. The source reconstruction had
assigned player 3's wall at `(287,401)` to adjacent player 2's rebuilt start,
moving it to `(284,405)` and creating an artificial owner conflict. Rebuilt
walls are now associated only with a neighboring start of the same owner;
multiple distinct matching transforms fail closed. Removal of a pending
start's wall likewise checks owner identity against retained neighbors when
the serialized wall records a nonzero owner. Owner-zero walls retain the
legacy adjacency rule because the archived first-player Oracle requires their
removal and the map provides no owner discriminator for those cells.
Confidence is high for this local false-positive diagnosis; the remaining
corpus-wide effect was checked again: 4/145 cases are exact, and the remaining
141 still differ in the offline model. The lobby therefore marks a later AI
`NotEvaluable` after a start rebuild if the map contains an owner-marked wall
adjacent to another player's start. The first AI remains evaluable. These
archived logs do not record the native DLL
hash, so neither result alone validates the currently installed binary.

The archived completed-castle corpora were rechecked with hash-verified local
AIV files: `Log_075` has 4/4 exact cases across `testanimals.map` and
`Crater Lake.map`; `Log_069` has 4 exact and 6 `NotEvaluable` cases on
`Crater Lake.map`, with no mismatches. These results support evaluating the
first AI before a completed-castle prebuild, while later build state remains
unmodelled. They also lack current-DLL hash provenance.

All older RVAs, field claims and trace references in the sections below belong
to the historical `17F8DD4A…` DLL. They are retained as provenance and must
not be used as current addresses without the current-hash mapping above.

This document fixes the evidence boundary established in Chat 7 of
`AIV_PLACEMENT_ROADMAP.md` and records the offline implementation completed in
Chat 8. It inventories the rejection families used by the native AIV fit path,
assigns stable reason codes and records the Skirmish-specific organism bypass.

## Historical binary and entry points

- file: `x86_64/CrusaderDE.dll`
- SHA-256: `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`
- preferred image base: `0x180000000`
- `EvaluateCandidateFit`: RVA `0x562F0`
- footprint-height state preparation: RVA `0x68940`
- native footprint-offset lookup: RVA `0x68AC0`
- mapper-to-profile lookup: RVA `0xC69D0`
- shared building placement validator: RVA `0x7A2D0`, size `0x4B6`
- Skirmish initialization: RVA `0x854E0`; game-mode store at RVA `0x855E7`

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
| `HeightMismatch` | The Height byte exceeds the native mapper limit. The AIV call prepares a one-cell footprint; all AIVParser mapper profiles resolve to a maximum of `200`. |
| `TerrainBlocked` | One of the proven Logic-bit/mask branches rejects the mapper. Exact masks are listed below. This code must not turn an unknown bit into a guessed terrain name. |
| `OrganismOccupied` | Reserved for validator modes outside the Skirmish AIV path. Skirmish mode and player ID `0` bypass organism-class rejection, so this reason is not emitted here. |
| `BuildingOccupied` | The Building/Structure grid is nonzero. The validator immediately returns native result `2`. |
| `PriorAivPrebuiltOccupied` | A prior player's Sofortspawn occupancy was observed in live runtime evidence. Planned AIV elements never emit this reason. |
| `EntityOccupied` | Reserved for other validator modes. `EvaluateCandidateFit` passes player ID `0`, so the entity-record loop is not entered in this AIV path. |
| `OwnerConflict` | Existing `IsWall` terrain is rejected in the AIV path. The stored owner becomes `1..8`, which can never equal the passed player ID `0`. |
| `InternalOverlap` | Two projected AIV elements claim the same tile. This remains trace evidence, but native candidate fit flattens the AIV first and lets the later loaded frame overwrite the earlier cell; it is not a live-map blocker by itself. |
| `BuildingRuleFailed` | Reserved for a future prerequisite that cannot be expressed by a more specific reason. The currently ported mapper exceptions are reported as terrain, height or owner reasons. |
| `UnresolvedNativeRule` | Native control flow proves that a branch can affect acceptance, but required live record data is absent from the snapshot. This yields `NotEvaluable` when no deterministic rejection also applies, never a permissive pass or a guessed rejection. |

A prior AIV plan is never converted into blocking occupancy. The native
`ExecuteBuildStep` result depends on mapper-specific creation paths and may
differ from the preceding fit result. Without observed post-build tile state,
later players in a Sofortspawn session are therefore `NotEvaluable`.

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
| `TerrainFlags` | 1003 Logic | `0x898400`, `int32` | Multiple exact bit/mask branches | `TerrainBlocked`, `OwnerConflict` |
| `SecondaryLogic` | 1037 Logic2 | `0x9D2500`, `byte` | No direct read in RVA `0x7A2D0` for this AIV call | None currently; retained as evidence |
| `Height` | 1005 Height | `0xD7E5A0`, `byte` | Compared with the mapper limit and the one-cell footprint minimum | `HeightMismatch` |
| `DefaultHeight` | 1045 DefaultHeight | `0xDCCAC0`, `byte` | Only substituted for a wall owned by the passed player; impossible with AIV player ID `0` | None in this AIV path; retained as evidence |
| `OrganismId` | 1004 Organism | `0xA6F260`, `uint16` | Enters a class switch, but Skirmish mode plus AIV player ID `0` accepts its default path | None in this AIV path; retained as evidence |
| `BuildingId` | 1012 Building | `0xB0BCA0`, `uint16` | Nonzero returns native result `2` immediately | `BuildingOccupied` |
| `EntityId` | 1026 Entity | `0xBF6C00`, `uint16` | Read, but the record walk requires a nonzero player ID and is skipped here | None in this AIV path; retained as evidence |
| `OwnerId` | 1043 WallOwner | `0xE1AFE0`, `byte` | Low three bits become owner `1..8`; no value can match AIV player ID `0` | `OwnerConflict` |

The native mapper profiles are now ported for every AIVParser mapper used by the
offline evaluator. Entity and organism records are not missing input for this
specific AIV path because its fixed player ID and Skirmish mode bypass their
rejecting subpaths.

This organism branch is not a blanket occupancy rejection. Section 1014 is
4000 records of `0x9C` bytes, and the validator reads its class field at record
offset `0x46`; classes `5..14` and `16..19` are accepted directly. The global
mode value at VA `0x188571B80` controls the remaining classes.

`DLL_PreInitMap_Multiplayer` sets that value to `1` or `99` at RVA `0x855E7`.
Both are nonzero. In the default class path, a nonzero mode proceeds to player
handling; the AIV call's player ID is `0`, so it reaches acceptance without a
record-dependent rejection. Section 1038 is a separate 4000-by-`0x20` rock
array and is not read by this branch. Quarry resource stones likewise do not
create an organism rejection. The offline Skirmish evaluator therefore accepts
trees, tree proximity and these rock records without decoding either object
section.

## Proven Logic tests, names and unknowns

The following constants are literal operands in RVA `0x7A2D0`. A branch ending
at RVA `0x7A3A8` returns blocked (`1`); the accepted path reaches RVA `0x7A77F`
and returns zero. Names in the third column are used only where the Script
Extender flag enum has an existing reverse-engineered name. They do not by
themselves prove the entire compound rule.

| RVA | Test | Known flag names / evidence status |
| ---: | --- | --- |
| `0x7A329` | `flags & 0x00000100` | `IsWall`; owner handling follows before general checks. |
| `0x7A413` | `flags & 0x00000008` | `PitchTrap`; only mapper `99` is rejected by this bit in call mode `0`. |
| `0x7A5D3` | `flags & 0x00000001` | `Sea`; mappers `195..198` have an explicit exception. |
| `0x7A5E9` | `flags & 0x00000030` | `RealityEdge | MapBorder`; rejected. |
| `0x7A5F3` | bit `20` / `0x00100000` | `River`; mappers `195..198` have an explicit exception. |
| `0x7A698` | `flags & 0x10000400` | `IsBuilding | IsElevated`; rejected after owner special handling. |
| `0x7A6A4` | `flags & 0x00003000` | `IsTree | TreeProximity`; enters organism-record classification when an organism ID exists. |
| `0x7A712` | `flags & 0x0F000000` | farm-related high bits; call mode `0` rejects them. |
| `0x7A723` | bit `21` / `0x00200000` | `Ford`; rejected. |
| `0x7A731` | `flags & 0x20000000` | `IsSwamp`; mapper profile permits it only for mapper `91`. |
| `0x7A737` | `flags & 0x40000000` | `IsMoat`; mapper profile permits it only for mapper `105`. |
| `0x7A73C` | `flags & 0x00000180 == 0x80` | `ImpassableEdge` without `IsWall`; the exact allowed mapper set is ported below. |

Additional exact operands occur in mapper and entity/organism switches:

- mapper IDs `70..73` select four special placement-state values;
- mapper IDs `100..147` use special wall-owner handling;
- mapper IDs `195..198` are exceptions for Sea and River;
- mapper ID `99` is the special rejection case for PitchTrap in call mode `0`;
- entity record type `0x37`, owner fields, linked entity IDs and several record
  thresholds affect occupancy acceptance;
- organism-record classes `5..14` and `16..19` contain accepted switch cases,
  while the remaining paths consult additional owner/player state.

In addition, call mode `0` rejects the low `IsFarm` bit `0x00000004`. The exact
profile switches read by this validator are nonzero for:

- bare `ImpassableEdge`: mappers `51`, `55`, `56`, `77`, `86..90`, `92`, `93`,
  `110..114`, `180`, and `330`;
- `IsSwamp`: mapper `91`;
- `IsMoat`: mapper `105`.

These lists come from profile tables at RVAs `0x2E3680`, `0x2E3A00` and
`0x2E3BC0` after the mapper lookup at RVA `0xC69D0`. They are binary-bound to
the SHA-256 above.

## Height preparation in the AIV call

`EvaluateCandidateFit` writes the mapper profile values from RVAs `0x2E3140`
and `0x2E3300`, then calls RVA `0x68940` with footprint size `1`. That function
scans exactly the current AIV cell and records its minimum and maximum Height.
The validator therefore checks:

    Height <= 200
    Height <= sameCellHeight + mapperTolerance

Every mapper profile reachable from the current `AIVParser` has limit `200`
and a non-negative tolerance (`12`, `40`, or `80`), so the second check
cannot reject the one-cell AIV call. Chat 8 consequently emits
`HeightMismatch` exactly for `Height > 200`; it does not guess a slope from
neighboring tiles.

## Chat 8 offline evaluator

`AivPlacementRuleEvaluator` applies the proven rules independently per
projected tile and returns immutable `AivElementPlacementResult` values:

- `Placeable`: no proven issue;
- `Blocked`: at least one deterministic reason applies;
- `NotEvaluable`: a different required native rule or projection input is
  genuinely unavailable.

`EvaluateElements` additionally tracks tile claims in original build order and
reports `InternalOverlap` against the first earlier element for diagnostics.
The official fit result must ultimately use the flattened last-writer-wins
100×100 candidate grid described in `AIV_PREBUILD_AND_OVERLAP_ORDER.md`;
`InternalOverlap` must not be interpreted as another live building.

## Implemented and deferred boundary

Chat 8 implements geometry, direct Building occupancy, the exact one-cell
height limit, wall ownership, proven Logic masks and mapper-profile exceptions,
plus sequential AIV overlap. The rules stay separated by reason family and do
not infer unsupported meanings from raw values.

`SecondaryLogic`, `DefaultHeight`, `OrganismId` and `EntityId` are retained only
as evidence: the first is not read by this validator, the second is unreachable
for player ID `0`, the organism rejection is bypassed by Skirmish mode plus that
player ID, and the entity record loop is bypassed for the same player ID.

## Pre-placement map state used by the Oracle comparison

The serialized `.map` already contains the player start buildings in Section
1013 and their occupancy effects in the placement layers. The passive native
Oracle runs before those start buildings affect its candidate test. Evaluating
the unmodified snapshot therefore double-counts Keeps and nearby wall state.

`AivPreplacementMapState` wraps an immutable `MapPlacementSnapshot` and clears
only state attributable to living Section-1013 or Section-4013 start buildings
owned by players `1..8`:

- a nonzero Section-1012 value is the building-object record index itself;
- tiles referencing those records clear `IsBuilding`, `IsElevated`, `IsWall`,
  `BuildingId`, and `OwnerId`;
- an owner-marked wall tile with no building ID is normalized only when one of
  its eight neighbors references such a start-building record.

The narrow adjacency rule is intentional: it removes the three observed Keep
edge cells without erasing unrelated serialized walls. Synthetic tests cover
direct occupancy, adjacent edge cells, isolated walls, dead/non-player records,
and preservation of all unrelated layer values. The original snapshot remains
available unchanged for diagnostics.
