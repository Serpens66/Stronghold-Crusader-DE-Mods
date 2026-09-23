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
