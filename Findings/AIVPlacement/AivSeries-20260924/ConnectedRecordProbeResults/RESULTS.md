# Connected-record probe: Craggy Cliffs, 2026-09-24

Native DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Map SHA-256: `C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.
The AIV hashes for every fit are recorded in `CurrentCorpus/craggy-cliffs-C46B71C9.json`.
`session-current.original.bin` is the byte-exact current BepInEx process section
(SHA-256 `7FD0E32983CC888CF1A40760B3421557192EB4672F1AD1E5EDD0E9922A4CB9C6`);
`session.log.original.bin` preserves the whole append log.

## Completion and offline comparison

The test series advanced through `CC-A-on` and `CC-B-on` to `nextIndex: 2`.
A third map load repeated the `CC-B-on` setup after the series had finished;
it is a repeat, not a third planned run. All three loads produced eight complete
Keep-start traces each, 20 complete prebuild traces in total, and no snapshot,
pointer or Keep-start failure flag. The current process contains 50 native fit
attempts: three exact offline matches, 47 deliberately `NotEvaluable`, zero
mismatches and zero comparison errors. By load the exact/unevaluable split is
`1/15`, `1/16`, `1/16`. This is evidence for the observed paths only.

Each of the 24 record snapshots contains exactly the row count named in its
header. The selected fields of nine building-record slots changed at each
Keep start. Previously occupied slots can be deleted and immediately reused
by the new compound Keep, so a final `AliveState` alone cannot identify the
deletion. The tile and record traces must be read together.

## Observed deletion and its native cause

- `CC-A-on`, player 5, Keep `(418,519)`, native orientation `15`: the start
  cleared 60 earlier building-ID cells and replaced eight. Slots 575, 579
  and 596 belonged to player 4 before the call and were reused for player 5
  structures afterward. Their old tile extents were `(416..420,517..521)`,
  `(413..418,529..534)` and `(425..428,525..528)` respectively.
- `CC-B-on`, player 7, Keep `(506,357)`, orientation `2`: the start cleared
  16 cells of old slot 590, a player-6 4x4 building at
  `(504..507,371..374)`, without replacement cells. Slot 590 became the
  player-7 Keep. The extra repeat reproduced this deletion exactly.

For type 41, native `0x74DA0` calls `0x5D3A0` when tile-manager
`+0x204E7FC` requests collision cleanup. `0x5D3A0` indexes its linked,
camp and yard cleanup offsets with `type - 0x28` and does **not** receive the
selected orientation. Its type-41 camp scan starts at Keep offset `(0,+8)`
and covers 7x7 cells; its yard scan starts at `(+7,+2)` and covers 5x5.
In contrast, the subsequent construction in `0x74DA0` uses
`orientation / 2 + 4 * (type - 0x28)` for those offsets. The cleanup camp
rectangle for the recorded Keep therefore includes `(506..512,365..371)`
and intersects two cells of old slot 590 in row 371, even though the
rotated compound construction is elsewhere. `0xC4290` marks the entire
existing record, and `0xB8310` removes all its occupied cells. This
explains the 16-cell off-footprint deletion. Confidence: high from the
installed DLL's control/data flow and two matching full-grid traces.

`0xC4290` also marks records with the same nonzero native field at record
offset `+0x304` (equivalent to `GameBuilding` offset `0x2A8`). In the
**installed** Script Extender 2.9.0 DLL, reflection/`Marshal.OffsetOf`
identifies this as `r_UsedInSiegeAttemptId` (680 decimal), matching the
local C# interop source at commit `70a4483`. The separate, older
`ReverseEngineering/structs/GameBuildingManager.h` calls `0x2A8`
`N0000178A`; this header does not describe the current compiled field
name. The native cleanup field is
**not** `r_GlobalId` (offset `0xD8`). The current detector
captured `r_GlobalId`, not `N0000178A`; therefore this probe does not
establish which other buildings might share the deletion group on arbitrary
maps. The prior interpretation of `r_GlobalId` as this cleanup link is
incorrect. Confidence: high for the field identity, limited for possible
linked-record effects in other starts.

## Production decision

The current AIVPlacement `StartOverlapUnproven` guard checks a broad area
around every possible earlier start. It includes the observed trigger and
correctly leaves the affected later predictions gray. Narrowing it to the
final rotated construction footprint would introduce a false safe result.
These traces contain no failed Keep start and do not bound linked records
that have no cells in the cleanup scan. No new later-player fit is released
on this evidence. The next useful diagnostic should capture the actual
`0x2A8` link field and deliberately exercise a linked-record or constructor
failure branch before replacing this conservative guard.
