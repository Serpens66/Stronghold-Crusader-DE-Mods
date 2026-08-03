# Native AIV placement contract and passive Oracle

## Scope and binary identity

This document covers the native AIV candidate selection used by Stronghold
Crusader Definitive Edition before an AI castle is prepared. The analysis is
specific to this binary:

- file: `CrusaderDE.dll`
- SHA-256: `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`
- image base: `0x180000000`
- analyzed game version: `2.7.0.1`

Addresses below are recorded as RVAs so that ASLR does not affect the
documentation. The analysis used `.native-analysis/Run-Rizin-With-Ghidra.cmd`.

## Native function chain

| RVA | Descriptive name | Observed contract |
| ---: | --- | --- |
| `0x54050` | `TestSpecificCandidate` | Tests one candidate at the orientation already stored in the AIV spec and returns a percentage or signed `-2`. |
| `0x541D0` | `SelectBestFit` | Tests imported candidates, optionally repeats the tests for three further rotations, and writes the selected candidate, orientation, and placement state. |
| `0x54590` | `LoadCandidate` | Expands one imported AIV into temporary 100x100 mapper and build-order score grids. |
| `0x558E0` | `ApplyRotation` | Rotates those temporary grids to orientation `0`, `2`, `4`, or `6`. |
| `0x562F0` | `EvaluateCandidateFit` | Evaluates every relevant temporary AIV cell against the live native map state. |
| `0x7A2D0` | shared building placement validator | Returns `0` for accepted, `1` for blocked, and `2` for an occupied building condition in the observed AIV call mode. |

The AIV spec has stride `0x6D98`. Fields relevant to the Oracle are:

| Offset | Meaning |
| ---: | --- |
| `0x04` | one-based player ID |
| `0x0C` | current/final orientation |
| `0x10` | final candidate ID |
| `0x14` | final placement state |
| `0x28`, `0x2C` | absolute origin X/Y |
| `0x30`, `0x34` | Keep/reference X/Y |

## Rotationsursprung

Der gezielte Chat-10-Nachtest von `ApplyRotation` bei RVA `0x558E0` belegt,
dass Vanilla die Mapper- und Score-Grids für Orientierung `2`, `4` und `6`
rotiert, den in der AIV-Spezifikation gespeicherten absoluten Ursprung aber
nicht anpasst. Für die getesteten AIVs liegt der Roh-Keep bei `(row 56,
column 43)`; die Oracle-Zeilen zeigen entsprechend für jede Orientierung:

    originX = keepReferenceX - 43
    originY = keepReferenceY - 43

Die Weltprojektion eines bereits rotierten Gridpunkts lautet damit:

    worldX = originX + rotatedColumn
    worldY = originY + 99 - rotatedRow

Der native Fit rotiert folglich das vollständige 100×100-Grid um den festen
Orientierung-0-Ursprung. Er rotiert nicht relativ um den AIV-Keep-Marker. Die
randnahe `unittest`-Matrix bestätigt diese Formel für alle vier Orientierungen
mit 58 von 58 exakten Status-, Score- und Zellzählervergleichen.

## Meaning of the fit result

`EvaluateCandidateFit` iterates the expanded 100x100 mapper grid. Empty cells
are skipped. Mapper value `1` is copied into the temporary occupancy grid but is
not passed to the building placement validator and does not contribute to the
fit counters. Other positive values are tested as their mapper type. Negative
mapper values are tested as mapper type `86`.

For each evaluated cell, the candidate loader has stored `buildEntryIndex + 1`
in a parallel score grid. A native validator return other than zero marks that
cell as blocked. The evaluator returns:

- `999999` when no evaluated cell was blocked;
- otherwise, the smallest score-grid value among blocked cells minus one;
- therefore a non-negative partial score identifies the build prefix before the
  earliest blocked AIV entry, rather than merely counting final placed tiles.

The evaluator also writes two counters in the AIV state:

- offset `0x5B4F8`: evaluated cells;
- offset `0x5B4FC`: blocked cells.

The percentage used by both selection functions is integer division:

    (evaluatedCells - blockedCells) * 100 / evaluatedCells

When the evaluated-cell count is zero, the native selection code substitutes
`100`. This does not make the candidate complete: only raw score `999999` maps
to placement state `2`.

## `TestSpecificCandidate` contract

Inputs are `(aivState, specIndex, candidateId)`. The function first validates
the candidate ID against the consecutive imported candidate count for the
spec's player. It loads the candidate, applies the orientation stored at spec
offset `0x0C`, and calls `EvaluateCandidateFit`.

| Condition | Return | Spec result |
| --- | ---: | --- |
| raw score `999999` | `100` | candidate ID written; `placementState=2` |
| raw score greater than zero | calculated percentage, or `100` for a zero evaluated-cell count | candidate ID written; `placementState=1` |
| candidate absent or raw score non-positive | signed `-2` (`0xFFFFFFFE`) | no accepted placement |

Consequently, return value `100` alone does not prove a complete fit. The caller
must also inspect `placementState` or the raw Oracle score.

The only native caller is at RVA `0x94E00`. A negative per-player preferred-AIV
value takes the `SelectBestFit` branch. A non-negative value takes the
`TestSpecificCandidate` branch; values of at least `100` encode the orientation
in the hundreds and the candidate ID in the remainder. The managed regular
skirmish path writes `-1 - rotation`, so a normal skirmish is expected to use
`SelectBestFit`. Concrete preferred candidates occur in scripted trail/co-op
setup data, which explains why the passive hook saw no direct call in the
controlled regular-skirmish run.

## `SelectBestFit` contract

The function starts at the orientation already stored in the spec. Candidate
iteration begins at a deterministic pseudo-random offset and wraps around the
candidate list. A complete raw score immediately writes candidate/orientation,
sets `placementState=2`, and returns.

Without a complete result, the function tracks both:

- the highest percentage, derived from evaluated and blocked cells; and
- the highest positive sequential raw score, derived from the earliest blocked
  build entry.

If rotation search is enabled, orientations advance by `2` with wraparound
through `0`, `2`, `4`, and `6`. A complete result in any rotation is accepted
immediately. Partial selection contains native thresholds (`85`, `90`, `95`,
and a raw-score boundary at `30`) and is not equivalent to simply taking the
highest percentage. If no candidate passes the relevant partial thresholds,
the allocation-time value `placementState=0` remains unchanged. Any accepted
partial candidate sets `placementState=1`.

The targeted Chat-9 audit of RVA `0x541D0` fixes the exact branch order:

1. The initial orientation tracks the highest percentage, the highest positive
   sequential score, and the first candidate whose percentage is greater than
   `95`.
2. A complete score still returns immediately in every tested orientation.
3. If the initial orientation produced any positive sequential score, rotated
   partials do not replace it. The initial orientation chooses the first
   greater-than-`95` candidate when present; otherwise a sequential score of at
   least `30` wins, else a percentage greater than `90` wins, with the best
   sequential candidate as the remaining fallback.
4. Only when the initial orientation produced no positive sequential score can
   a rotated partial be selected. It must have a percentage greater than `85`;
   strict greater-than comparison retains the first orientation on a tie.

`AivPlacementEvaluator.EvaluateAllRotations` ports the one-AIV subset of this
contract. The multi-AIV candidate-order branches remain documented here for a
future caller that owns more than one blueprint.

## Map state consulted by the fit

The evaluator first checks its translated coordinates against the native
800x800 coordinate domain and a native validity mask. It then translates the
coordinate through the native row-offset table and invokes the shared building
placement validator once per evaluated AIV cell.

The shared validator directly reads native tile flags, height/logic bytes,
building occupancy and building records, ownership/player state, and organism
or entity occupancy. It contains mapper-specific exceptions and can mutate
building state in non-Oracle call modes. The AIV evaluator invokes it with the
non-mutating mode argument `0`. Exact flag and mapper-specific meanings belong
to Chat 7 and are intentionally not assigned speculative names here.

## Passive runtime Oracle

`ActiveAIVDetector` version `0.7.0` detours the five AIV functions above and
calls each original trampoline exactly once. It never initiates a candidate
test and returns every native result unchanged. The existing process-lifetime
runtime owns the detours, so destruction of the temporary BepInEx component
during startup does not remove them.

After `OnStartMap(Post)`, the mod emits:

- one `AIV placement oracle selection` record with map path, map SHA-256, player slot,
  native selection method, final candidate, final rotation, state, and direct
  return value where applicable;
- one `AIV placement oracle attempt` record for every candidate/rotation test,
  including AIV path/hash when resolvable, raw score, percentage, counters,
  absolute origin, and Keep reference.

Every record receives a local timestamp with millisecond precision through
`Shared.DebugLogHelper`.

Chat 10's targeted audit of `LoadCandidate` at RVA `0x54590` additionally
proved the native associated areas used by the temporary 100x100 grid. Keep
mappers `60..64` add a 5x5 area, a 7x7 area, and three connector cells. Mappers
`79`, `86`, and `87` add three footprint-sized yard areas; mappers `88` and
`89` add one. These cells are evaluated even though they do not represent a
separate core building. The offline catalog now models all of them explicitly.

The structured offline comparison, measured sample, and remaining coverage gap
are documented in `AIV_PLACEMENT_ORACLE_COMPARISON.md`.

## Controlled runtime matrix

One regular-skirmish start on 2026-08-02 provided a deliberately broad batch:
`Marshy Mayhem`, seven AI slots, five Extended Wolf `wolf+` selections, one
`testlord_serpcastle1` selection, and one default Rat selection. Vanilla's
instant-complete-castles option was enabled. Candidate selection finished by
`22:49:18.524`; all seven final records were confirmed by `22:49:18.531`.
Subsequent map-start/castle handling began at `22:49:18.546`, so prebuilding did
not contaminate the fit inputs.

| Case | Observation | Evidence status |
| --- | --- | --- |
| completely free placement | Rat `Standard 2`, orientation `2`: raw `999999`, 612 evaluated, 0 blocked, 100%, state `2` | runtime confirmed |
| small collision set | Rat `Standard 2`, orientation `0`: raw `14`, 612 evaluated, 6 blocked, 99%, still partial | runtime confirmed; an exactly one-cell terrain reason is intentionally deferred to Chat 7 |
| map-edge rejection | coordinate-domain and validity-mask branches reject out-of-range cells before the shared validator | native control flow confirmed; per-cell reason attribution belongs to Chat 7 |
| several blocked elements | Wolf attempts ranged up to 2,435 blocked cells; raw score and percentage varied independently | runtime confirmed |
| four rotations | Wolf and custom-AIV selections emitted separate attempts for `0`, `2`, `4`, and `6` | runtime confirmed |

The run captured 35 attempts: 34 partial attempts and one complete attempt. Six
final selections had state `1`; the Rat selection had state `2`. State `0` did
not occur in this map, but its meaning is fixed by both selection functions:
the spec is initialized to zero and remains zero when no candidate obtains an
accepted positive result. `TestSpecificCandidate` likewise returns signed `-2`
without accepting a placement for an absent candidate or a non-positive raw
score. No extra game start is required merely to repeat these already explicit
branches.

Runtime observations must come from an unmodified Vanilla selection path. The
Oracle must not be used to force a result or to prepare/execute a castle.
