# AIV lobby selection audit

Installed `CrusaderDE.dll` SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Image base: `0x180000000`; VA is base plus RVA. The entry and data path below
was reviewed against the current semantic decompiler export.

| Native role | RVA / VA | Confidence |
| --- | --- | --- |
| Skirmish player setup and AIV selector source | `0x94350` / `0x180094350` | High for branching; medium for managed lobby mapping |
| Candidate import and mapper write | `0x55320` / `0x180055320` | High for call order; medium for all field semantics |
| Fixed candidate fit | `0x54DE0` / `0x180054DE0` | High |
| Auto candidate and rotation choice | `0x54F60` / `0x180054F60` | High for control flow and thresholds |
| Mapper rotation | `0x56670` / `0x180056670` | High |
| Projected raster scan | `0x57080` / `0x180057080` | High for scan and score flow |
| Tile placement rules | `0x7B060` / `0x18007B060` | Medium for mapper-specific effects |
| Prepare chosen castle | `0x53D00` / `0x180053D00` | High for call order; medium for effects |
| Execute completed castle | `0x55F50` / `0x180055F50` | High for frame sequence |
| Execute one build step | `0x51790` / `0x180051790` | Medium for all constructors |

For this hash, the projected raster validator `0x57080` calls tile rules
`0x7B060` with player ID 0 and mode 0. Its general height limit is 200;
it does not apply the physical moat-construction threshold of 12. The
later `0x51790` mapper-106 path calls moat constructor `0x59730`
(`VA 0x180059730`), whose height branch requires a tile height below 13
for Vanilla construction. ExtraFeatures gates a bypass of that later branch
for AI players when its audited hook is installed and logically enabled;
it does not alter the candidate fit call. Confidence: high for the checked
call/branch paths and the observed Crater Lake trace; remaining constructor
conditions are not modeled by this statement.

The later mapper-105 drawbridge route in `0x51790` calls building
constructor `0x6D580` and drawbridge creator `0x739C0`. The audited
height-failure writer at RVA `0x7870B` follows a mapper-105 comparison
and a maximum-building-height comparison against 12. ExtraFeatures
suppresses this failure for allowed AI players only when its complete
hook transaction is active. Candidate fit still does not apply this
later height gate. Confidence: high for the guarded native branch and
hook contract; medium for a complete offline build outcome.

On 2026-09-23, the Crater Lake `nizar6.aivjson` prebuild trace on this
native hash recorded mapper 106 in frames 15, 65, and 66. Of 1,025
listed moat positions, 1,022 height-layer changes were observed, all
`130 -> 122`; the ExtraFeatures AI hook was active. The trace has complete
frame/provenance metadata and zero capture errors. The paired no-prebuild
start retained complete native fit scores for Nizar and Wolf. These
observations establish the fit/build distinction for this case, not exact
future construction for every projected moat cell.
The same Nizar capture recorded mapper 105 at frame 28: one prepared
drawbridge position, 25 new building-ID cells, and 15 height changes
`130 -> 122`. This is direct layer evidence for this build, not a
guarantee that every footprint cell receives the same height write.
The later complete 2026-09-24 Crater Lake replay joined tile IDs from the
offline projection and native build trace for Nizar `Default 6`, orientation
270 degrees. Mapper 106 projected 1,025 core-footprint tiles; 1,022 unique
tiles received a height write, all within that projection. Mapper 105
projected 25 footprint tiles; all 25 received a building-ID write, with
no observed write outside the footprint. The active ExtraFeatures AI hook
was recorded. Confidence is high for this exact captured path, not for
other AIVs or disabled-hook construction.

`0x94350` scans player IDs in order, resolves a per-player selector, starts
native AIV state, and routes negative selectors to `0x54F60`. Selector `-1`
passes an enabled alternative-rotation flag; `-2` and lower pass disabled.
The function receives an `AivSystem*`, zero-based village slot, and one-byte
flag. Its candidate count comes from the imported specification. The initial
orientation is stored in the village slot and rotated by `0x56670`; subsequent
orientations advance by two in the native eight-step direction field, wrapping
at eight. The caller later reads the chosen variant index, orientation and
placement state. Complete results prepare the layout and, with completed
enemy castles enabled, execute build steps into the mutable map before the
next player. The offline AIV plan alone cannot reconstruct that later state.

The 2026-09-23 follow-up checked the no-prebuild dependency at this same hash.
`0x53D00` imports the chosen AIV, records prepared frames and owner masks, and
derives the start Keep coordinate from the imported Keep marker. The following
`0x94350` start constructor receives the player ID and that coordinate; the
candidate's subsequent frames execute only through the conditional `0x55F50`
path. The fixed-fit chain `0x54DE0` -> `0x57080` -> `0x7B060` reads the live
tile layers and building records, not the prepared AIV frames or owner masks.
For valid AIVs anchored to the same Keep, distinct candidate choices with the
same selected rotation therefore have the same proven start input to the next
fit when prebuild is off. Confidence is high for this direct call/read chain;
constructor side effects outside the audited start footprint remain outside
the offline prediction. With prebuild on, candidate identity determines later
frame writes and cannot be discarded.

`0x54F60` starts at RNG modulo candidate count and increments before the first
visit. It imports and checks every candidate at the initial rotation. The
first full score `999999` returns immediately with state 2. Partial scores
track the first visited fit above 95%, the best sequential score, and the best
percentage. All ties use strict `>` and retain the earlier visited candidate.
If the flag permits, it checks three more rotations in rotation-major order.
Any full fit returns immediately. A partial alternative is considered only if
there was no positive initial score. With no positive initial score it accepts
an alternative at 86% or higher; otherwise no candidate is selected. With a
positive initial score it selects the first above 95%, else the highest
sequential score when greater than 29, else the highest percentage when at
least 91%, else the highest sequential score. Partial selection has state 1.

The installed Script Extender exposes imported AIV and live build-state views,
but no offline query against an unloaded lobby map. `AivSystem` must remain
pointer-based. The local Fixes mod changes some AIV capacities and KEEP3
handling, so it remains part of runtime compatibility review. The owner-aware
reconstruction of tightly neighboring starts is a model assumption supported
by the archived owner-marked wall discrepancy, not yet by a current-hash
runtime capture. The rechecked archive matches 4/145 cases after the owner
fix; unresolved dense-map states are blocked from later-AI lobby predictions
after any earlier start rebuild if the map has cross-owner start-wall adjacency. Serialized walls with
owner zero still require the previous
adjacency-based removal rule; the archived first-player full-fit case would
otherwise become a false partial fit. A current-hash Oracle trace should
resolve this uncertainty. See `Helpers/MapParser/Docs/AIV_PLACEMENT_RULES.md` for the
existing fit and corpus details.

## Completed-castle execution on this hash

At `0x94350` (`VA 0x180094350`), an accepted AI placement reaches
`0x53D00`, then the coupled start-building constructor. Before the loop
advances to the next player ID, it can set the prebuilt-player bit and call
`0x55F50(AivSystem*, playerId, 100)`. The observed native condition is
`(G_1887EE2F8 && (G_1887EE2F4 || (G_188574B90 != 99 && G_1887EE2F0))) ||
specialStartMode || forcedFixedCandidate`. The special-mode flag comes from
the start-path local mode 4 branch; the fixed-candidate flag is set for a
selector above 999. Exact managed provenance of the three globals remains
open, so `advopt_pre_build` alone must not be claimed to cover those modes.

`0x55F50` (`VA 0x180055F50`) computes the inclusive last frame as
`percentage * preparedLastFrame / 100` and calls `0x51790` for zero-based
frames `0..last` with `restrictedMode=0, freeOrForced=1`. `0x51790`
(`VA 0x180051790`) branches by mapper/status and can call the footprint and
cleanup helper `0x5CD90` (`VA 0x18005CD90`) before the constructor path
`0x6D580` (`VA 0x18006D580`). The checked caller does not use the helper's
return as a universal build gate. The helper may remove live structure
records after a `0x7B060` check using the real player ID and mode 1.
`0x6D580` changes several live tile layers and routes to type-specific
constructors. The mapper-99 branch of `0x51790` calls it for positions and
then returns zero, so the step return value alone cannot certify unchanged
map state. Confidence is high for these current-hash call/branch contracts,
medium for complete mapper-specific effects. The old `17F8DD4A...` Thasos
frame captures remain evidence of plan/live divergence, not proof that every
constructor is unchanged on this hash. See
`CastlePlanner/AIVPlacement_SOFORTSPAWN_FORSCHUNGSSTAND.md` for the current
feature status and test boundary.

The diagnostic observer now records sparse before/after differences for the
eight audited per-tile validator input layers and selected building-record
fields per `0x51790` frame, for multiple players and map loads in one process.
It also records raw start-option globals at selector entry when the addresses
fall in the loaded image. A 2026-09-23 Crater Lake capture on this native hash
recorded seven AI selections and 16 fit attempts with `advopt_pre_build=0`
(map SHA-256 `C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`).
Twelve attempts matched the offline status, raw score, fit percentage and cell
counts exactly. Four attempts for one Plague Doctor AIV were not compared:
`miscItems.number` exceeds the offline parser's supported `0..9` range.
Further inspection of `0x55320` (`VA 0x180055320`) shows that the native
importer clears a 320-int misc-position buffer and writes each entry at
`itemType * 10 + number`, without checking `number` separately. For a valid
type with an in-range flattened index, any `number>=10` aliases a later
ten-position group. Values 10 and 11 occur in the captured Plague Doctor AIV.
The Script Extender's AIV decoder preserves `number` as a signed
16-bit value. The audited fixed-fit raster scan `0x57080` reads the imported
building and step grids, not this misc-position buffer. Confidence is high
for these current-hash import and fit contracts; later misc-position consumers
and completed-castle effects are outside this fit conclusion. The offline
parser retains a warning for `number>9` while accepting every nonnegative
16-bit number whose flattened index is inside the 320-int buffer. This rule
does not limit the number of selected AIVJSON files. The rerun matched all 16
Crater Lake fit attempts exactly, including four Plague Doctor rotations, for
status, raw score, percentage and cell counts. This is observed equivalence
for the capture, not proof for later misc consumers.
Three cell-trace self-checks disagreed with native blocked-cell counters even
though the comparable offline scores matched. Confidence is high only for
these observed fits; this capture does not establish constructor equivalence,
managed option provenance, or a later-player prebuild simulation. Later
prebuilt players remain fail-closed in the lobby.

## 2026-09-23 Crater Lake prebuild observation

The installed native DLL still hashes to
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
On map SHA-256 `C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`,
seven player build sequences through `0x55F50` / `0x51790` were captured with
complete, provenance-linked frame snapshots and no pointer or capture errors.
The first sequence changed 4,230 recorded tile-layer values and 203 building
records. In an immediately following run on the same map and AI lineup with
prebuild disabled, 41 common `(player, candidate, orientation)` native fit
attempts matched the prebuild run in status, raw score, percentage, evaluated
cells and blocked cells. For all 41 captured fit attempts of later players in
the prebuild run, the validator's read tile IDs did not intersect tile IDs
changed by prior captured build sequences. Confidence is high for this observed
route and measured read/write sets; it does not bound writes of unselected
random variants or all mapper-specific constructors. The lobby's fail-closed
handling of later prebuilt players remains justified until a conservative
all-variant effect bound or an exact sequential simulator is established.
The 2026-09-23 follow-up re-read the current-hash entry/consumer chain before
considering a spatial independence rule. `0x57080` (`VA 0x180057080`) scans the
projected 100x100 candidate grid, resolves each used map coordinate, prepares
a one-cell footprint, and calls `0x7B060` (`VA 0x18007B060`) with player ID 0
and mode 0. In this call mode the validator reads the current tile's logic,
height, building ID and relevant owner/organism values; the entity-owner walk
is skipped. Confidence is high for the direct fit reads. The writer chain from
`0x51790` (`VA 0x180051790`) can call `0x5CD90` (`VA 0x18005CD90`) to clear
existing structure records and `0x6D580` (`VA 0x18006D580`) to dispatch to
multiple type-specific constructors. Those constructors, footprint helpers and
cleanup consequences have not yet been bounded for every imported mapper and
candidate. Local Fixes can relocate the ordered AIV tile-ID buffer, which must
be included in a production bound. Therefore a zero intersection between
sampled frame-write tiles and sampled fit-call tiles is an observation, not a
proof that every possible lobby candidate is unaffected.

The 2026-09-23 variant-coverage follow-up at the same installed hash narrowed
the first Crater Lake player's unresolved selection to candidate 4
(`nizar5.aivjson`, captured) or candidate 5 (`nizar6.aivjson`, not yet
captured), both at orientation 6. The imported files have different frame
counts (259 versus 291) and distinct occupied plan offsets, so the captured
candidate 4 build cannot stand in for candidate 5. This is a file-level
observation, not a native write bound. In the direct constructor chain,
`0x6D580` (`VA 0x18006D580`) calls `0x77E60` (`VA 0x180077E60`), checks its
result, then performs per-footprint cleanup and type-specific construction.
The current Ghidra export reports `Flow exceeded maximum allowable
instructions` for `0x77E60`; its references include 16 footprint-helper
calls to `0x69850` and 16 tile-validator calls to `0x7B060`. Confidence is
high for this direct control flow and the uncovered variant, but medium for
complete spatial side effects. A fixed AIV-raster padding cannot yet be
treated as a proven all-constructor write bound. A targeted build capture of
candidate 5 can close the observed branch gap; a general lobby guarantee
still requires the native effect bound or equivalent exact state modeling.

## 2026-09-23 17:58 Crater Lake control capture

The installed DLL and map hashes remained `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
and `C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`.
The native chain under comparison remains the audited `0x94350` player start,
`0x54F60` candidate selector, `0x53D00` preparation, `0x55F50` / `0x51790`
prebuild, and `0x57080` / `0x7B060` fit read. Seven AI sequences were captured
with `advopt_pre_build=1`, all provenance- and frame-complete with zero pointer
or capture errors. The following `advopt_pre_build=0` start used the same
positions and lords, with random AIV selection. The enabled run provided 47
fit attempts; the disabled run 42. Among 39 common player/AIV-hash/rotation
attempts, native status, raw score, fit percentage, evaluated cells, and
blocked cells agreed exactly. In the enabled run, all 46 later-player cell
traces had zero intersection between their observed validator-read tile IDs
and the prior captured build-frame writes in the eight validator-input tile
layers. The build order was Nox, Marshal, Jewel, Abbot, Wolf, Nizar, Nomad;
the chosen Nizar variant was built-in `Default 5`, not `Default 6`.

Confidence is high for these observed traces and the zero intersections.
This does not close the constructor-side-effect or unchosen-variant branches:
building records are only partially snapshotted, and the capture cannot
establish an all-variant write bound. No general later-player lobby fit with
completed castles is released from this observation. The comparison reads
player order and file hashes from trace metadata instead of assuming a
fixed player ID or AIV filename.
The two-start log was imported as 89 Oracle cases and compared with the
current offline implementation: 43 exact (all 42 prebuild-off attempts and
the first prebuild-on attempt), 46 intentionally not evaluable (later
prebuild-on attempts), zero mismatches, and zero comparison errors. This
confirms current fail-closed behavior for the observed capture; it does not
validate later-player simulation.

## 2026-09-23 lobby preset activation boundary

This finding concerns the managed frontend before the native start chain; the
installed native hash is still `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
In the 23:21 game session, the test plugin's persistent render callback ran
and observed a managed `MainViewModel.FRONTMultiplayer` with a non-null lobby,
`skirmishGame=True`, `Show_MPGameCreation=True`, and `panelActive=True`.
There was no preset `Prepare` result or exception. The installed APIShared
`LobbyPreparationOverride.Tick` checked `view == null` before calling
`Begin`. `FRONT_Multiplayer` derives from Noesis `BaseComponent`, whose
installed `operator ==` treats a non-null wrapper with zero `swigCPtr.Handle`
as equal to null. The installed Noesis assembly SHA-256 is
`98476D3CA84AE0F2DCFBADDCC64B01A1F65474BD44402673FD6856D1B5347648`.
Confidence is high for the managed branch and equality contract. The initial
deduction that this lobby had a zero native handle was disproved by the next
session: at 23:31 the test plugin logged `noesisViewNull=False` alongside
`skirmish=True`, `Show_MPGameCreation=True`, and `panelActive=True`, yet still
logged no `Prepare` outcome. The cause is therefore unresolved within the
managed preparation bridge. Its registered owner, callback, current-lobby,
prepared, active and apply-attempted state must be observed before and after
`Tick`; the test plugin now records these once per lobby-ready transition.
The offline AIV fit and native selection contracts are unaffected.

The installed Script Extender 2.9.0 detours
`FRONT_Multiplayer.SkirmishAIAddClick` and forwards ordinary lord choices
through Vanilla. Vanilla's managed `SkirmishAIAddClick` calls
`Platform_Multiplayer.AddSkirmishPlayerLocal`, refreshes player-ID mappings,
and initializes the corresponding `AIVs[playerId - 1]`. At match start,
`StartSkirmishGame` serializes the lobby members, Keep order and AIV choices;
the native `0x94350` chain consumes those start inputs later. Confidence is
high for this current managed and extender source path.

## 2026-09-23 runtime visibility of lobby members

The installed game's real `Assembly-CSharp.dll`, rather than the publicized
compile-time copy, declares `FRONT_Multiplayer.PlayerCap`, `MPsetupData`,
`selectedMPHeader`, and `RefFileLists` private. It also declares
`UpdateHostInfo`, `UpdateRadarShieldPositions`, `updateSteamIDMappings`, and
`ReSortTeamInfo` private. `SkirmishAIAddClick`, `currentLobby`, `AIVs`,
`panelActive`, and `trailMakerMode` are public. Confidence is high from
decompilation of the installed managed assembly. The 23:37 runtime trace
confirms the boundary: `LobbyPreparationOverride.Begin` marked preparation
attempted, but the test callback threw `System.FieldAccessException` for
`FRONT_Multiplayer.PlayerCap` before its first log statement. The exception
was written to Unity `Player.log`, not BepInEx `LogOutput.log`. A mod compiled
against `Assembly-CSharp-publicized.dll` cannot directly access these private
members at runtime; any required access needs an explicitly audited runtime
mechanism or a public Vanilla route.

## 2026-09-24 ten-run Crater Lake / Craggy Cliffs observation

The installed DLL still hashes to
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The audited path is `0x94350` (`VA 0x180094350`) for ordered player
initialization, `0x54F60` / `0x54DE0` for selection, `0x57080` -> `0x7B060`
for fit reads, and `0x53D00` -> `0x6D580` -> optional `0x55F50` / `0x51790`
for prepared start and completed-castle writes. At `0x94350`, the accepted
candidate's Keep reference is passed to `0x6D580` with structure type `0x3d`
before the optional `0x55F50` loop. `0x6D580` delegates its initial
placement to `0x77E60`. The decompiler export for `0x77E60` is incomplete;
its placement, failure, and footprint effects remain a native audit gap.
Confidence: high for this direct call order, medium for the full start
constructor effects.

Ten verified test-series starts (seven AIs each) used Crater Lake map SHA-256
`C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`
and Craggy Cliffs map SHA-256
`C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.
One intervening Crater Lake start had the wrong completed-castle option and
was excluded. The confirmed series contains 119 native fit attempts: 57
offline exact matches, 59 deliberately `NotEvaluable` after prior completed
castle construction, three mismatches, and zero comparison errors. Crater
Lake contributes 34 exact and 28 `NotEvaluable` with no mismatch. The three
mismatches are on Craggy Cliffs **without** completed castles: Emir
`Default 1` at 0 and 180 degrees has two extra offline blocked cells in
each case; Jewel `Default 1` at 270 degrees has native 95% / 106 blocked
cells versus offline 94% / 117. These are not evidence of a different
`0x7B060` rule by themselves: native live-building snapshots disagree with
the offline rebuilt-start state at the candidate cells. For Emir at 0 degrees,
the offline reconstruction inserts building ID 32 at `(383,530)` and
`(384,530)`, but the native building grid has no building there. At 180
degrees the analogous over-inserted cells are `(389,519)` and `(389,521)`.
Jewel's candidate has 20 cells with different modeled/native building IDs.
Confidence is high for the observed snapshots and comparison, medium for
attributing every mismatched blocked cell to the start constructor. The
existing affine 13x13 start rebuild is therefore not yet an exact native
contract for nearby candidate footprints.

All five completed-castle runs yielded complete, provenance-linked frame
captures with zero pointer/capture errors: seven sequences each on three
Crater Lake runs, six on Craggy `CC-A-on` (Emir's AIV was natively rejected
in every rotation, so it did not build), and seven on `CC-B-on`. In the
three Crater Lake off/on pairs, 31 common native attempts were unchanged,
and none of 28 later-player fit traces intersected earlier recorded
validator-layer writes. On Craggy Cliffs, 5/13 and 6/11 common native
attempts changed between off/on; 8/15 and 12/16 later fit traces touched
earlier captured tile writes. This directly disproves a general spatial
independence inference from Crater Lake. Confidence is high for the observed
pairwise scores and read/write sets; unchosen variants and all constructor
side effects remain unbounded. The product's later-player completed-castle
`NotEvaluable` boundary remains necessary. See
`CastlePlanner/Diagnostics/AivSeries-20260924/RESULTS.md` and
`CastlePlanner/AIVPlacement_SOFORTSPAWN_FORSCHUNGSSTAND.md` for files and next
checks.

## 2026-09-24 follow-up: AI start constructor and live-grid boundary

Installed `CrusaderDE.dll` SHA-256 remains
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
At `0x94350`, `0xC43A0` removes the serialized start Keep and linked records;
`0xC3FA0` removes this player's serialized goods yards. Candidate selection
(`0x54F60`/`0x54DE0`) follows. `0x53D00` imports the chosen AIV and obtains
the first mapper `0x3d` marker from the rotated 100x100 grid. The resulting
coordinate is passed to `0x6D580` with mapper `0x3d` and scale seven before
the optional `0x55F50`/`0x51790` completed-castle loop. `0x6D580` calls
`0x77E60` first and returns without construction when its failure flag is
set. On success, `0x69850` iterates the structure footprint, clears affected
tile fields and prior objects, and the constructor path writes building,
owner and terrain occupancy and updates adjacent path cells. The exported
decompilation of `0x77E60` exceeds its instruction-flow limit; the Rizin
analysis exposes multiple type-dependent validation branches. No general
success/failure contract for arbitrary nearby starts follows from this
audit. Confidence: high for call order, branch/side-effect boundary and
observed grids; limited for the complete validator conditions.

The archived Craggy Cliffs map has source Keep ID 10 at `(330..336,333..339)`
and campground ID 14 at `(330..336,341..347)`. A selected 270-degree start
appears in the native pre-fit grid at `(337..343,333..339)` and
`(329..335,333..339)`, respectively. The 0-degree start at slot 3 retains
both source bounding boxes without the previous offline `(+1,+1)` offset.
A 90-degree start at slot 6 agrees with the previous `(y+1,12-x)` relative
mapping. Thus the previously used 0- and 270-degree affine offsets are
disproved; the corrected observed mappings are identity at 0 degrees and
`(13-y,x)` at 270 degrees. No equally direct seven-by-seven 180-degree
constructor observation is in this corpus. The map's object records have
type 41 for these Keeps and type 55 for the seven-by-seven campground; the
separate type-10 goods-yard records must not be conflated with it. Confidence:
high for these map and live-grid cells, limited for generalizing to all
maps and constructor outcomes.

The shared offline core now refuses a fit that reads cells in or near an
earlier rebuilt AI start. The guard includes a 24-tile Keep neighborhood
for the canonical AIV marker and source/modeled target cells with a four-tile
margin for local constructor/path updates. These are conservative product
boundaries, not proof that every native side effect ends at those margins.
A candidate outside those areas still relies on the established offline map
normalization and fit rules. A selected AIV with a shifted start marker makes
later AI fits `NotEvaluable` until its resulting native start is proven.
All failed-construction branches remain research gaps. Fixes'
local `ModularGoodsyardPlacement` hook is in the native Keep-spawn tail;
CastlePlanner's separate player rotation compatibility changes only human
selection. Neither path establishes a safe AI-start cell reconstruction.

After the guarded correction, the ten selected archive sessions have 38
exact comparisons, 81 deliberate `NotEvaluable`, no mismatches and no
errors (119 attempts). Including the excluded extra Crater Lake session,
the full imports have 47/82/0/0 across 129 attempts. This validates the
current fail-closed boundary against those captures; it does not prove
arbitrary constructor outcomes. Reports: `.inspect/oracle-crater-safe.json`
and `.inspect/oracle-craggy-safe.json`.

## 2026-09-24 correction: compound AI Keep start

Installed native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The mapper `0x3d` passed by `0x94350` is **not** a generic small
building. `0xC77C0` maps it to structure type `0x29` (41). After
`0x77E60` accepts the placement, `0x6D580` dispatches type 41 to the
compound Keep constructor `0x74DA0`. This corrects the earlier generic
constructor interpretation; the call order and fail-closed product boundary
above remain valid. Confidence: high (native mapper table and dispatch).

For type 41, `0x74DA0` creates a 7x7 Keep, three one-tile linked structures
(types `0x47`, `0x49`, `0x48`), a separate 7x7 campground (type `0x37`),
and calls `0x76E80` for the goods yard. The table at VA `0x1802D3230`
places the campground relative to the Keep anchor at `(0,+8)`, `(+8,0)`,
`(0,-8)`, `(-8,0)` for native rotations 0, 2, 4, 6. The three linked
cells, from VA `0x1802D3150`, are respectively `(2,7)/(3,7)/(4,7)`,
`(7,4)/(7,3)/(7,2)`, `(4,-1)/(3,-1)/(2,-1)`, and
`(-1,2)/(-1,3)/(-1,4)`. The goods-yard anchor table at VA
`0x1802D3290` gives `(+7,+2)`, `(+2,-5)`, `(-5,0)`, `(0,+7)`.
`0x76E80` creates four 2x2 building records and writes nine further
one-tile goods-yard cells. The constructor clears the campground's 7x7
area and the yard's 5x5 area before writing them. Confidence: high for
the static successful-construction path and offsets; no claim that every
placement succeeds or that serialized start records rotate as one rigid
group.

`0x77E60` receives mapper `0x3d` and follows its default validation
branch. The observed checks include `0xEE640` over live unit records,
`0xEE840` over opposing player distances, `0xEC130` path traversal,
`0xEB9A0`, and tile checks through `0x7B060`. A failed flag at
`placementState+0x204E6FC` makes `0x6D580` return before the Keep
constructor. These dependencies can vary with the live map-start state.
The complete success and abort contract, especially for all possible
nearby units and connected paths, is not established by the archived
traces. Confidence: high for these dependencies and the abort boundary;
limited for an exact offline validator.

The local Fixes mod may replace the tail call to `0x76E80` when
`ModularGoodsyardPlacement` is enabled. Its per-player
`PlaceGoodsyardData` defaults to true but is mutable. This option affects
the start construction, not the earlier AIV candidate score. An offline
start-state implementation must account for its effective per-player
value; the current near-start `NotEvaluable` boundary also covers an
unknown value. Confidence: high from the local Fixes source and native
constructor call site, limited for any runtime setting not captured in
the archive.

The installed Script Extender's `BuildingR3EventHooks.OnBuildStructure`
raises Pre immediately before its `0x6D580` original call and Post
immediately afterward. The Post `BuildStructureEventArgs.ReturnValue` is
currently always zero because that event type does not receive the native
return value. A passive observer can instead read the native failure flag
and reason at tile-manager offsets `0x204E6FC` and `0x204E704` at Post,
then compare nearby tile layers. Its Post event carries the original
argument values, not necessarily values changed by another Pre subscriber;
Pre/Post argument mismatches must invalidate a trace. Confidence: high
from the installed-version source contract and native layout; runtime
capture was validated in the four-match regression below.

## 2026-09-24 four-match Keep-start regression

The four verified Craggy Cliffs starts (`CC-A-off/on`, `CC-B-off/on`)
used the installed DLL hash above and map SHA-256
`C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.
All 32 mapper-`0x3d` Pre/Post pairs produced complete 41-by-41 regional
snapshots of the eight fit-relevant layers; every post failure flag was zero.
The native fit scores, percentages and blocked-cell totals in all 57
candidate attempts match the earlier archived Craggy corpus exactly.
The raw failure-reason field was nonzero after 24 successful starts, so it
is stale diagnostic state unless the failure flag is set. No constructor
abort branch was observed. Confidence: high for this capture and the
failure-flag interpretation; none for arbitrary abort conditions.

The successful starts show a 7x7 Keep, three linked cells, a 7x7 camp and
four 2x2 yard pieces in the native building layer. Normal starts changed
117 building-ID cells. During completed-castle starts, player 5 in `CC-A-on`
also cleared 60 existing building cells and replaced eight, while player 7
in `CC-B-on` cleared 16. These are real before/after effects, not 117
independent empty-to-occupied writes. The observed changed cells lie at
most 22 tiles (Chebyshev distance) from their map Keep anchor; this is an
observation, not a global write bound. The raw traces, hashes and selected
oracle log are archived in the CastlePlanner diagnostic folder
`AivSeries-20260924/StartRebuildRegression`.

The full potential write set remains unproven. `0x6D580` performs a
footprint clear before `0x74DA0`; the Keep constructor can enter
`0x5D3A0`, which marks preexisting building records through `0xC4290`,
and later calls `0x5D740`, `0x6FE90`, pathfinding updates and the optional
`0x76E80` yard constructor. Record removal and the nested tile/path helpers
cannot be bounded from these four successful map starts. In particular,
no 180-degree start or failed validator outcome was captured. The offline
near-start guard and shifted-marker `NotEvaluable` boundary must not be
narrowed based on the observed 22-tile maximum. Confidence: high for the
identified native calls and captured write cells; limited for a universal
tile-mutation envelope.

## 2026-09-24 coverage correction and full-grid capture preparation

The 32 archived Keep-start traces sampled only `x/y = anchor - 16 ..
anchor + 24`, not the full 320,800-tile grid. Their `sampledRegionComplete`
header meant that this **region** was fully read; it cannot exclude writes
outside it. Therefore the reported 22-tile maximum is a maximum **within the
sampled region** and must not be used as a native write bound. The detector
now snapshots all eight fit-relevant tile layers across all 320,800 tile IDs
immediately before and after the published `OnBuildStructure` call, using
the Script Extender's native row/column lookups for changed-cell coordinates.
It records scan times and a `fullMapTileLayersComplete` marker. No result
from the new capture exists yet, and even a complete-grid observation on
one map does not prove all constructor or validator branches. Confidence:
high for the original capture extent and the audited event/lookup contract;
unverified at runtime for the new full-grid instrumentation.

The overlap cleanup switch at tile-manager `+0x204E7FC` gates
`0x74DA0 -> 0x5D3A0`. The latter visits Keep, linked-cell, camp and yard
footprints for existing building IDs, calls `0xC4290` to mark those records,
and then scans all 3,999 ordinary building records with `0xB8310` for
state 3. `0x5D3A0` sets tile-manager `+0x204E778` when it encounters an
occupied start cell. The detector now records both raw fields before/after
each type-41 call, so an overlap-cleanup run can be distinguished from a
plain start. A full-grid diff is still needed to observe actual fit-layer
effects of that whole-record pass. Confidence: high for the native branch,
record loop and offsets; not yet measured for the new trace.

At the type-41 call sites in `0x74DA0`, `0x6FE90` receives `param_6=0`;
its `0x6A620` path then calls `0xE3360` with that per-footprint index, while
direct writes target the returned tile. The constructor also invokes
`0xE3B90`, `0x725E0`, `0x6CDD0`, `0x76E80` and possible existing-building
removal through `0x5D3A0`. Their complete fit-layer effects and all
abort/overlap branches still require a bounded call-chain proof before the
offline uncertainty region can be reduced. Confidence: high for call sites
and arguments; limited for their aggregate spatial side effects.

## 2026-09-24 full-grid Craggy result

The one-match `CC-A-on` probe used the same native SHA-256 and Craggy map
SHA-256 `C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.
The test series advanced to complete; eight type-41 starts had full
320,800-tile snapshots before/after the call and no incomplete captures.
All eight had `preNativeStartCleanupFlag=1`, `postNativeStartCleanupFlag=0`,
`postNativeDestroyedRecordMarker=1`, and `postNativeFailureFlag=0`.
No changed fit-layer cell was outside the old 41-by-41 sampling window.
Within that window, all eight new change-row sets match the archived
`CC-A-on` start traces exactly (zero differing rows). Full-grid scanning
cost 18-22 ms per start in this run. Confidence: high for this map,
selected AIVs and measured successful branches; not a universal bound.

Six provenance-complete completed-castle traces were captured with zero
pointer or frame errors. Their building and fit-layer change rows match
the original ten-match series' same `CC-A-on` run (`session009`) exactly
for each of players 2, 3, 4, 6, 7 and 8. Among 16 native candidate-fit
attempts, eight validator read sets intersected prior captured prebuild
write sets; only one also intersected a prior Keep-start write set.
Eight attempts had no observed prior-write intersection. These are
actual-path comparisons, not a bound on different selectable AIVs or
failed constructors. The offline comparison of the imported 16 attempts
remains one exact, 15 `NotEvaluable`, zero mismatches/errors.

`0x51790` calls `0x5CD90` before `0x6D580` for relevant mapper/status
branches. `0x5CD90` can clear an existing building via `0xC43A0`, which
may invoke `0xB8310` on other records sharing the nonzero native cleanup
link field at record offset `+0x304` (`GameBuilding` offset `0x2A8`);
this field is not `r_GlobalId` at `GameBuilding` offset `0xD8`. `0xB8310`
enters type-specific tile cleanup through `0x61FC0`. Thus the proposed
simple union of planned AIV footprints alone is not a proven superset of
all fit-layer writes. The later-player prebuild `NotEvaluable` boundary
remains. Confidence: high for this call chain and the observed traces;
limited for its full-map effects on arbitrary maps and imported mappers.

Raw full-grid, prebuild, cell and log archives with hashes reside in
`CastlePlanner/Diagnostics/AivSeries-20260924/FullGridProbeResults`.

## 2026-09-24 six-match full-grid start and 180-degree audit

Installed Native SHA-256 remains
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Six verified starts (`CL-A-off`, `CL-A-on`, `CL-B-on`, `CL-Reverse-on`,
`CC-B-off`, `CC-B-on`) yielded 48 complete full-map type-41 start
diffs and 28 complete completed-castle sequences. Each start wrote within
the previously sampled 41x41 neighborhood, with no reported constructor
failure flag. This is an observation of these inputs, not a general write
bound or proof of the `0x77E60` abort conditions.

Eight 180-degree starts whose AIV first mapper was the canonical grid
marker `(row 56, column 43)` were joined by map source building record and
tile coordinate to their native post-start building cells. For every start,
all 117 serialized compound Keep cells match the transform
`(keepX + 13 - dx, keepY + 13 - dy)`; the former `+12` transform misses
31 of the 117 native cells each time. The observed native Keep minimum is
`map Keep + (7,7)`, while the campground is eight cells north of that
native Keep. This agrees with the `0x53D00 -> 0x6D580 -> 0x74DA0` rotated
marker and type-41 offset tables. Noncanonical markers `(55,44)` and
`(56,45)` shift the native 180-degree Keep minimum to `+(6,6)` and
`+(5,7)` in the observed Crater Lake starts; the product still treats
subsequent fits after such markers as `NotEvaluable`.

The 69 new native fit attempts compare as 19 exact and 50 deliberately
`NotEvaluable`, with no mismatch or processing error after the pivot
correction. Confidence is high for the eight observed canonical 180-degree
compound footprints and Native offsets; the complete constructor effects,
failure paths, other AIV markers, maps and earlier completed-castle state
remain unproven. Raw traces and reports are archived at
`CastlePlanner/Diagnostics/AivSeries-20260924/SixMatchResults`.

The installed pivot-13 implementation was checked with two further
confirmed Crater Lake starts using the same fixed seven-AI setup, first
without and then with completed castles. Native SHA-256 remained
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
map SHA-256 remained `C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`.
All 16 full-map start captures and seven completed-castle build captures
were complete. The 20 native fit attempts yielded ten exact offline
comparisons and ten conservative `NotEvaluable` classifications, with
zero mismatches or processing errors. This is a runtime regression of
the observed branches; it does not close the shifted-marker, constructor
failure, or sequential prebuild contracts. Evidence is archived under
`CastlePlanner/Diagnostics/AivSeries-20260924/Pivot13RuntimeRegression/Observed`.

## 2026-09-24 selected AIV marker propagation

Native SHA-256 remains
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The feature chain `0x94350 -> 0x54EC0/0x54DE0 -> 0x53D00 ->
0x6D580 -> 0x77E60/0x74DA0` was followed from selection through the
successful compound-start writes. `0x53D00` imports the selected AIV,
then uses its first rotated `0x3D` Keep marker for the type-41 start
anchor. The relevant later fit input is thus the selected start marker
and rotation, rather than rotation alone. For a marker `(row, col)`, the
offline displacement relative to canonical `(56,43)` is the rotated
column difference in map X and the negative rotated row difference in
map Y. Source and displaced target cells remain guarded where the
constructor's complete side effects are not established.

The observed shifted-marker cases in the Crater Lake and Craggy Cliffs
archives, including Jewel `(55,44)` at 180° and Nomad `(55,48)` at 0°,
now agree with the native downstream fits. Five stored corpora total
218 native fit attempts: 93 exact, 125 deliberately unevaluable, zero
mismatches or processing errors. Reports are `marker-report.json` in
`CastlePlanner/Diagnostics/AivSeries-20260924/SixMatchResults` and
`Pivot13RuntimeRegression/Observed`. Confidence is high for this
observed selection-to-anchor data flow and the compared scores; the
unobserved `0x77E60` abort branches and completed-castle sequential
writes remain outside the proven model. This supersedes the earlier
product boundary that rejected every noncanonical selected marker;
it does not assert a general constructor-write bound.

## 2026-09-24 marker-runtime validation and write-bound limit

Installed Native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Crater Lake SHA-256:
`C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`.
The verified `CL-B-off` and `CL-Reverse-off` starts plus one repeat of
the latter produced 24 complete full-grid compound-start traces.
Each had failure flag zero and 117 newly occupied building cells.
The maximum observed Chebyshev distance of a changed cell from its
Keep was 20. The 31 captured native fits all match the current offline
score, percentage and blocked-cell count; 21 are from distinct setups
and ten repeat a setup. Evidence with source hashes is in
`CastlePlanner/Diagnostics/AivSeries-20260924/MarkerRuntimeValidation/Observed`.

`CL-B-off` has an ambiguous prior Wolf selection with possible 0- and
90-degree start rotations; `CL-Reverse-off` permits definite starts
for all seven AI players. One observed random choice is not proof of
the other possible start state.

For the feature chain `0x94350 -> 0x53D00 -> 0x6D580 -> 0x77E60 ->
0x74DA0`, compound construction can call `0x5D3A0`: its footprint
collision pass marks building records and its later record scan can
dispatch `0xB8310` for whole linked buildings, including cells outside
the immediate footprint. Optional completed-castle construction through
`0x55F50 -> 0x51790 -> 0x5CD90` can also reach `0xC43A0` and
`0x61FC0` on collided building records. These control and data-flow
paths prevent treating the observed distance 20 or the 41x41 capture
window as a universal write bound. The available `0x77E60` export
does not yet establish every constructor-abort branch. Confidence is
high for the observed successful starts and exact fits, limited for
all-map write bounds, constructor failures and later-player prebuild.
Uncertain previous states must remain unevaluable unless all fit
inputs are independently proven invariant.

## 2026-09-24 eight-match multi-AIV validation

Installed Native SHA-256 remains
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
the captured process reported Script Extender 2.9.0. Eight planned seven-AI
loads on Crater Lake (`C5D9906A...`) and Craggy Cliffs (`C46B71C9...`)
plus two separately counted Craggy Cliffs repeats produced 80 complete
Keep-start captures, 42 complete completed-castle captures, and 310 cell
traces without snapshot or pointer errors. The planned 114 native fit
attempts compared as 52 exact, 62 deliberately `NotEvaluable`, and zero
mismatches or processing errors. The two repeats compared as two exact and
40 deliberately `NotEvaluable`. The observed randomized order of Emir's
two Default AIVs changed between Craggy loads 008 and 009 without changing
their individual native scores or final chosen AIV/rotation. This is an
observed branch, not proof that all RNG starts agree.

The feature flow rechecked for this release decision is selection
`0x94350 -> 0x54F60/0x53D00`, candidate fit `0x57080 -> 0x7B060`,
compound start `0x6D580 -> 0x77E60/0x74DA0`, and completed-castle
construction `0x55F50 -> 0x51790`. `0x74DA0` can call the collision pass
`0x5D3A0`; it can mark an existing record through `0xC4290` and later
delete connected records through `0xB8310`. The completed-castle cleanup
at `0x5CD90` can reach `0xC43A0` and `0x61FC0`. Therefore the observed
117 newly occupied start cells and local cell differences are not a general
write bound. The `0x77E60` export still does not close every constructor
abort path. Confidence: high for trace completeness and exact compared
fits, medium for identified connected-record side effects, insufficient
for a general state-set or completed-castle release. The affected later
fits remain fail-closed. Full provenance and per-load results:
`CastlePlanner/Diagnostics/AivSeries-20260924/MultiAivEightMatch/RESULTS.md`.

The archival Thasos Oracle exposed a separate parser regression. At
`0x54DE0` (VA `0x180054DE0`) the candidate path calls raster import
`0x55320` (VA `0x180055320`), which obtains mapper scale from `0x6A190`
(VA `0x18006A190`) and stamps the resulting square before the
`0x57080` scan. The installed Script Extender 2.9.0
`BuildingScales.GetScale(eMappers.MAPPER_DOG_CAGE)` gives three. An
August category change retained Dog Cage as a trap but accidentally
made its offline raster a single cell. The same archived AIV at the
same map hash had eight fewer offline evaluated cells in every
rotation, exactly the difference between 3x3 and 1x1. This is a
parser-footprint error, not a changed native fit rule. Confidence high
for the native import path, Extender scale and four-rotation Oracle
comparison; the general start-constructor uncertainty above remains.
The cited Thasos capture used historical Native SHA-256
`17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`;
it is not current-DLL runtime evidence. Current-DLL import control flow
and installed Extender 2.9.0 scale data independently support the
footprint correction.
After the footprint correction, 582 entries across all recoverable
archived and new reports compare as 234 exact and 348 deliberately
unevaluable, with no mismatch or processing error. Some archives repeat
earlier native attempts; these totals are report entries, not unique
game states. The post-fix reports and their hash-verified inputs are
under `CastlePlanner/Diagnostics/AivSeries-20260924/MultiAivEightMatch/Observed`.

## 2026-09-24 possible-start audit and conservative state set

The installed Native SHA-256 was rechecked as
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The selection chain `0x94350 -> 0x54F60/0x53D00`, tile fit
`0x57080 -> 0x7B060`, compound start `0x6D580 -> 0x77E60 -> 0x74DA0`
and optional prebuild `0x55F50 -> 0x51790` were reviewed together. At
`0x77E90` the validator resets the failure flag; numerous branches set
`tileManager+0x204E6FC` to one. `0x6D608` checks that flag immediately
after `0x77E60` and jumps past the compound constructor on failure.
Failure-reason `+0x204E704` alone remains nonauthoritative. The validator
uses live unit, enemy-distance, path and tile queries, so a successful
AIV raster fit does not prove its start will be built. Confidence is high
for the failure-flag control flow, limited for deciding every failure
condition offline. The no-start possibility is therefore retained.

The successful type-41 path can collide with existing building records;
`0x5D3A0 -> 0xC4290 -> 0xB8310` can then remove connected tiles beyond
the immediate footprint. The static Keep, camp and yard offsets fit within
24 tiles of the selected Keep marker, but that radius bounds only which
existing records can trigger this cleanup, not the reach of a connected
record once triggered. Offline evaluation rejects any state where a
different building record can meet this wider footprint. Own serialized
start records are identified by their mapped record IDs and selected
start transform, not by the tile-grid owner byte: the Crater Lake archive
contains own start cells with `owner=0`. Other owners' rebuilt records
remain collision candidates. Confidence is high for the collision entry
and the archived owner-zero observation, limited for complete downstream
record-deletion effects; no deletion simulation is released.

Without completed castles, the offline lobby service now enumerates every
possible selected candidate, rotation and Keep marker, plus a failed-start
outcome. It evaluates later candidates under each retained scenario and
publishes an individual fit only when status, all four scores, blocked
cells and build-warning exposure agree. Scenario identity includes absent
starts, retained-slot mask, markers and rotations; bounded state/work
limits fail closed. This is a conservative comparison of the existing
offline model, not a proof that all constructor-side tile effects are
reconstructed. The near-start and possible connected-record guards still
apply. Completed-castle construction remains unmodeled for later AIs:
`0x51790 -> 0x5CD90/0x6D580` branches by mapper and can clear connected
records through `0xC43A0/0x61FC0`. The installed Fixes mod can skip the
type-41 goods-yard tail through its per-player setting; no unsupported
assumption about that setting is added to the fit.

The 11 hash-checked archived corpora were rerun after this change: 582
report entries, 234 exact, 348 deliberately unevaluable, zero mismatch
or processing error. The comparison uses the recorded actual start for
each native attempt; it does not by itself validate every counterfactual
scenario. Results and manifest provenance are under
`CastlePlanner/Diagnostics/AivSeries-20260924/MultiAivEightMatch/`.

## 2026-09-24 possible-start runtime series and raster counter contract

The installed DLL still hashes to
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Six verified seven-AI starts and two additional Craggy-Cliffs repeats
produced 129 native fit attempts, 64 complete Keep-start traces and 28
complete prebuild traces with no snapshot error. The hash-checked offline
comparison yields 52 exact fits, 77 deliberately unevaluable fits and no
mismatch. Planned runs alone contribute 50 exact and 37 unevaluable fits;
the two repeats contribute two exact and 40 unevaluable fits. This is high
confidence for the observed executions, not for unselected random outcomes.

The `0x57080` raster loop increments its evaluated-cell count before
checking the world coordinate and native valid-tile byte. An invalid
coordinate or tile skips `0x7B060` and increments the blocked-cell count
directly. Consequently `nativeBlocked = validatorBlocked +
(evaluatedCells - validatorCalls)`. This identity holds for all 129 new
cell traces, including the 32 previously warned about. Their warning was
an incorrect detector self-check, not evidence of a native-score mismatch.
Confidence: high from the decompiled branch and complete trace accounting.

For the paired Crater-Lake starts, all 17 common native fit results agree
with prebuild off and on. For the paired Craggy-Cliffs starts, only five of
11 common fits agree; six change. Emir's `Default 7` at 0 degrees changes
from zero to 252 blocked cells. All 252 differing result-grid tile IDs lie
in layers changed by earlier captured prebuild frames when mapped with the
native 320,800-tile diamond geometry. This is direct evidence that
completed-castle effects matter on that map. The recorded actual earlier
starts do not bound unselected candidate, rotation or constructor-failure
outcomes. Later-player prebuild fits remain fail-closed.

On the recorded Craggy-Cliffs off-path, Emir's `Default 8` at 0 degrees
reads none of the 552 distinct tiles changed by the three earlier
Keep-start traces in that match. Its lobby result can still be gray because
counterfactual earlier start states intersect the guarded area. The
`0x6D580 -> 0x77E60 -> 0x5D3A0 -> 0xC4290 -> 0xB8310` collision path can
clear connected records beyond its immediate footprint; the observed
zero-intersection is therefore not a general safe-release criterion.

For the next targeted connected-record probe, the passive Keep-start observer
also snapshots the selected fields of every native building record before
and after each published type-41 `OnBuildStructure` event. A separate linked
trace records one-based building ID, alive state, type, owner, global ID,
occupied tile-grid origin and size, and tile coordinates. This uses the
Script Extender's existing `GetBuildingsAsSpan` view and the already rooted
event subscription; it does not alter Vanilla construction. `CC-A-on` and
`CC-B-on` are selected because earlier full-grid traces on this hash showed
60 and 16 cleared building-ID cells, respectively. Confidence is high for
the reviewed event and record-read contract; runtime completeness of the
new record traces remains unverified until these two starts are captured.

## 2026-09-24 connected-record probe result and cleanup orientation

The planned `CC-A-on` and `CC-B-on` loads completed, followed by one
unplanned `CC-B-on` repeat. The 24 Keep-start record/tile traces and 20
prebuild traces are complete, with zero capture or Keep-start failure flags.
All 50 native fit attempts compare as three exact, 47 conservatively
unevaluable and zero mismatches/errors. Raw hashes and per-record evidence
are archived in `CastlePlanner/Diagnostics/AivSeries-20260924/ConnectedRecordProbeResults/RESULTS.md`.
Confidence: high for these three observed loads, not for unobserved starts.

For type 41, the conditional cleanup call `0x74DA0 -> 0x5D3A0` scans the
Keep, linked cells, 7x7 camp and 5x5 yard before the rotated compound
construction. Its offsets use only `type - 0x28`. In particular, the
type-41 camp starts at `(0,+8)` and the yard at `(+7,+2)` relative to
the Keep; the selected orientation is not passed to this cleanup function.
The construction later uses `orientation / 2 + 4 * (type - 0x28)` for
its offsets. With a Keep at `(506,357)` and orientation 2, the cleanup
camp intersects old record 590 in row 371. The pass deletes all 16 tiles
of that 4x4 building at `(504..507,371..374)`, including tiles outside
the eventual rotated compound footprint. An extra run reproduced this
effect. Confidence: high from the installed DLL and two full-grid traces.

`0xC4290` and `0xC43A0` propagate deletion by the nonzero field at native
record offset `+0x304`, which is
`GameBuilding.r_UsedInSiegeAttemptId` at offset `0x2A8` in the
installed Script Extender 2.9.0 DLL (checked with `Marshal.OffsetOf`).
The local C# interop source at `70a4483` agrees. The separate, older
`ReverseEngineering/structs/GameBuildingManager.h` still calls offset
`0x2A8` `N0000178A` and must not override the compiled contract.
The field is distinct from
`r_GlobalId` at `0xD8`. The current record trace did not capture
the `0x2A8` value, so it cannot establish the membership or spatial reach of
such a linked deletion group. The existing broad `StartOverlapUnproven`
guard includes the observed trigger. A guard limited to the final rotated
build footprint would be unsound. No release of later-player fits follows
from these observations. Confidence: high for field identity and branch;
limited for unobserved linked-record groups or abort outcomes.

## 2026-09-24 follow-up: captured cleanup-link values

The installed 2.9.0 `GameBuilding.r_UsedInSiegeAttemptId` field at
offset `0x2A8` was added to the passive building-record snapshots.
Four planned Craggy-Cliffs loads (`CC-A-off/on`, `CC-B-off/on`) and four
separate repeats of `CC-B-on` produced 64 complete type-41 start
captures, 41 complete prebuild traces and 125 native fit attempts.
The hash-checked offline comparison is 21 exact, 104 deliberately
unevaluable, zero mismatches/errors. Raw hashes and detailed counts are
in `CastlePlanner/Diagnostics/AivSeries-20260924/CleanupLinkProbeResults/RESULTS.md`.

When completed castles were off, neither planned setup overwrote a
previously alive building slot during a Keep start. With completed
castles on, `CC-A-on` player 5 reused three alive slots and cleared
60 old building-ID cells. One old type-46 slot had nonzero cleanup-link
value 35681; the other two had zero. `CC-B-on` player 7 reused one
old slot with cleanup-link value zero and cleared its 16 cells. The
four repetitions reproduced this 16-cell deletion. No observed start
deleted an additional companion record solely by a shared nonzero
link value. All 64 post-call constructor failure flags were zero;
one unaccepted AIV selection still executed a successful compound
Keep. Confidence: high for observed records and native fit comparisons,
insufficient to narrow the global connected-record/abort safety guard.

Offline decoding of Craggy Cliffs map section 4013 on the same hash
shows 72 alive serialized building records: nine start records per
player. The five Keep/camp/linked records share one nonzero `0x2A8`
cleanup-link value per owner; the four goods-yard records share a
second value. This agrees with `0xC43A0` deleting an entire linked
start group before a player's native AIV selection. It establishes
initial serialized grouping on this map, not the later groups built
by an unselected AIV or a general write bound. Confidence: high for
the decoded map bytes and native link comparison; limited for
other maps and sequential dynamic building effects.

Static follow-up against the installed DLL with the same hash: `0xC43A0`
stores the old record's value at native offset `+0x304` before invoking
`0xB8310`. It then scans live records for that same nonzero value and
invokes `0xB8310` on every match. `0xB8310` reaches `0x61FC0`, whose
building-type dispatch includes special branches for types 10, 30-33,
49, 69, and 80-84 plus common tile/path/visual updates. Thus a
serialized group ID alone is not a complete bound on fit-layer writes.
The transitive writes of these branches and any dynamically created
records remain unaudited for the proposed narrower offline guard.
Confidence: high for the direct control flow and offsets; incomplete
for its full spatial side-effect bound.

## 2026-09-24 dense-start probe on the same installed DLL

The four verified `test AI overbuild eachother.map` setups and two
separate repeats produced 30 complete compound-start traces, eight
complete prebuild traces, and 74 native fit attempts. Six fits compare
exactly with the offline evaluator; 68 are deliberately unevaluable,
with zero mismatches/errors. Raw inputs, byte-exact process log,
hashes, and record rows are in
`CastlePlanner/Diagnostics/AivSeries-20260924/DenseStartLinkResults/RESULTS.md`.
The map hash is
`D63CD2FF3AEABA80BC3BC173BB615207666F1EAF759ECAC573FAD0DA61979DF3`.

Section 4013 contains 45 initially live records, all with a nonzero
native cleanup-link value at `GameBuilding+0x2A8`: five Keep/camp
records and four yard records per player, under two distinct values.
The complete live-building grid at the first AI's fit contains only
the human compound's nine record IDs, so the serialized AI groups
have already been removed before that fit. Of 279 changed record
slots during the 30 observed Keep calls, 65 were live beforehand;
none of those 65 carried a nonzero cleanup link. Nine were fully
deleted: three type-67, zero-link records during the fourth setup
and the same three in each repeat. All 30 post-call constructor
failure flags were zero. This confirms actual close-start cleanup
without observing a linked-group deletion or an abort. Confidence:
high for these captures, limited for the unobserved `0xC43A0`
multi-record branch and earlier map-start normalization.

The `0x94350` decompile separates a first loop over the configured
players, which calls `0xC43A0` on their serialized start records,
from the later loop that selects AIVs and calls `0x53D00/0x6D580`.
The dense-map first-AI live grid verifies that only the human
compound remains when native AIV fit begins. Eleven of the 68
conservatively gray cases are currently stopped by a source-tile
record from a serialized AI group; for example map record 10 is
type 41, owner 2, cleanup link 11, yet absent from that live grid.
Nine other gray cases identify a rebuilt earlier start and 48 follow
a completed-castle prebuild. Ignoring an already-removed serialized
source record in the offline collision trigger is supported by this
startup order, but the candidate-local uncertain-tile test and every
counterfactual earlier start still have to pass before any result is
released. No new fit is certified solely by this observation.
Confidence: high for the two-loop order and recorded first-AI grid;
not a complete counterfactual-start proof.

## 2026-09-24 offline guard refinement with Script Extender 2.10.1

The installed native hash remains `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
the installed Script Extender 2.10.1 hashes to
`85591256082C6F2329EDC0BFB0C1C163D9CF1BF2991DC8B11F6790907B60F2F2`.
The offline collision guard now ignores a serialized source building ID
only when it is already normalized away **and** belongs to the exact
serialized Keep record or its nonzero native `+0x304` cleanup-link group.
The pre-existing own-transform exception remains intact. It still
checks retained source buildings, rebuilt earlier starts, and candidate
reads in the uncertain constructor area. This follows the two-loop
`0x94350` order above; it does not assert a complete `0xC43A0/0x61FC0`
write bound. Eleven dense-map gray cases changed to a more accurate gray
reason, without releasing a fit. Across 960 archived comparison runs,
316 were exact and 644 remained unevaluable, with no mismatch/error;
some corpora overlap. Confidence: high for the startup order and observed
regression results, limited for unobserved connected cleanup or aborts.

The same-hash `0xC43A0` link-group path invokes `0xB8310` for each
matching record. Direct callee review shows that `0xB8310` first calls
`0xB8460`, `0x1977A0`, and `0xB5C40`, then `0x61FC0`, followed by
`0xCFE90`, before clearing the native record. Thus an exact fit-layer
write bound must audit these calls as well as the type dispatch inside
`0x61FC0`; bounding `0x61FC0` alone would be incomplete. `0x61FC0`
itself may return early after `0x79AB0` reports a failure. Confidence:
high for the direct call/branch order in the installed DLL, incomplete
for transitive tile and record writes.
