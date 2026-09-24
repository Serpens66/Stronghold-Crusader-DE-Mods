# Cleanup-link probe: Craggy Cliffs, 2026-09-24

Native DLL SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Map SHA-256:
`C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.
Installed Script Extender 2.9.0 SHA-256:
`710B4DA701D08250C4760181B4B5C0702A333AEAE8EBA33C311B30530FE9A697`.
The hash-checked map and 14 AIV inputs are in `Inputs`; per-case AIV hashes
are in `CurrentCorpus/craggy-cliffs-C46B71C9.json`.
The byte-exact current process log section is `session-current.original.bin`,
SHA-256 `57E60F9711F90C87C3001931921719A4D130A3C00173CE546347B86879459157`.
`SHA256SUMS.txt` lists and verifies all 445 archived data and report files.

## Series and capture completeness

The testmod verified all four configured runs and advanced to
`nextIndex: 4`, `lastCompletedRunId: CC-B-on`. After series completion,
the user started four more maps without preset override. They reproduced
the `CC-B-on` setup; each has the same 17-case native fit signature.
They are counted separately as repetitions, not as additional prescribed
setups.

| Map load | Setup | Completed Castles | Native fits | Exact | NotEvaluable | Prebuild traces |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 001 | CC-A-off | 0 | 13 | 8 | 5 | 0 |
| 002 | CC-A-on | 1 | 16 | 1 | 15 | 6 |
| 003 | CC-B-off | 0 | 11 | 7 | 4 | 0 |
| 004 | CC-B-on | 1 | 17 | 1 | 16 | 7 |
| 005-008 | CC-B-on repeats | 1 | 68 | 4 | 64 | 28 |
| **Total** | | | **125** | **21** | **104** | **41** |

The 64 Keep-start traces (eight per load) are complete. Each of their
64 linked `.records.tsv` files has the declared number of before/after
rows and the new `nativeCleanupLinkId` column. All 64 post-call native
failure flags are zero. The 41 prebuild traces report no pointer or
capture errors. The 125 native fit attempts have 125 cell traces and
125 paired live-building grids. The offline comparison reports zero
score, percentage or blocked-cell mismatches and zero processing errors.
The release tests pass: 35/35 shared-core tests and 90/90 CastlePlanner
lobby tests (the latter must run from the CastlePlanner directory).

`CC-A-on` has six rather than seven prebuild traces because player 5
had no accepted AIV selection. Its compound Keep still executed with
native orientation 15 and post-call failure flag zero; that is not a
failed constructor. The six other AI prebuild captures are complete.

## Native cleanup link values

The installed Script Extender field
`GameBuilding.r_UsedInSiegeAttemptId` is the 32-bit value at struct
offset `0x2A8` used by native `0xC4290`/`0xC43A0` to propagate a
building-record deletion. It differs from `r_GlobalId` at `0xD8`.

- In `CC-A-off` and `CC-B-off`, no existing alive record slot was
  overwritten during any of the eight Keep starts in either load.
- In `CC-A-on`, player 5's Keep at `(418,519)` cleared 60 old
  building-ID cells and replaced eight. Three old player-4 record
  slots were reused. Old slot 575 was type 46, global ID 35681 and
  cleanup-link value 35681; old slots 579 and 596 had link value zero.
  None of the other changed slots was an existing alive record. The
  trace does not show an additional companion deleted solely because
  it shared link value 35681.
- In `CC-B-on`, player 7's Keep at `(506,357)` cleared all 16 cells
  of old player-6 slot 590. Its cleanup-link value was zero. The four
  repeats reproduced the same deletion and identical native fit
  results. The earlier Native audit explains this off-footprint
  deletion through the fixed-orientation camp cleanup scan.

Across all eight map loads, only eight previously alive record slots
were overwritten at Keep starts: three in `CC-A-on` and one in each
of `CC-B-on` plus its four repeats. Only one of these eight had a
nonzero cleanup-link value. Every executed new type-41 compound Keep
created nine record changes; the new Keep and its linked structures
carry shared cleanup-link values, but this probe did not delete such
a multi-record group later in the same match.

## Production decision and next boundary

The existing broad `StartOverlapUnproven` guard covers the observed
native cleanup triggers. Restricting it to the final rotated Keep
footprint would be wrong: the earlier static audit and the 16-cell
trace show deletion outside that footprint. The new traces do not
prove a complete bound for connected groups on other maps, the
constructor failure branches, or sequential completed-castle effects
of unselected AIVs. Therefore no additional later-player fit is
released on this evidence. Observed exact results remain available;
the other cases retain their specific `NotEvaluable` reasons.

The next useful work is static: determine whether the serialized map
record link fields and all type-specific `0xB8310`/`0x61FC0` effects
can bound every possible earlier start. A new game run is only needed
for a concrete connected-group or constructor-failure branch that
cannot be decided from native code and the archived traces.

The map's section 4013 was also decoded offline into
`Inputs/building-records-section.bin` (4,000 records of 812 bytes).
All 72 alive serialized records have nonzero `0x2A8` link values.
For each of eight players, five Keep/camp/linked records share one
value and four goods-yard records share another. This confirms that
the map file can supply initial group membership for these start
structures. It does not supply the later dynamic records produced by
the chosen AIV's completed-castle construction. This result narrows
the static investigation but does not change the production guard.

## Static follow-up on the installed DLL

The installed DLL hash was checked again after the capture. Native
`0xC43A0` saves the removed record's nonzero value at record offset
`+0x304`, removes that record through `0xB8310`, then scans other live
records for the same value and removes each match. Each removal reaches
`0x61FC0`, which dispatches on building type. Its ordinary branch
clears the recorded footprint through `0x628F0`; types 10, 30-33,
49, 69, and 80-84 take additional branches before the shared tile,
path, and visual updates. These branches still require a bounded
fit-layer side-effect audit before the global overlap guard can be
narrowed. The 72 serialized records give the initial link membership,
but not the records created by earlier completed-castle construction.
Confidence: high for the dispatcher and link comparison, incomplete
for the transitive fit-layer write bound.
