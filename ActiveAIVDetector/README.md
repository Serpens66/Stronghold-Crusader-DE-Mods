# Active AIV Detector

Temporary diagnostic BepInEx mod for validating the native active-AIV selection
before moving the capability into the Script Extender.

The native oracle is enabled only for the currently audited `CrusaderDE.dll`
SHA-256 `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`.
Functions and globals are also resolved by unique signatures, but the captured
AIV structures contain fixed field offsets which signatures cannot validate on
their own. An unknown DLL therefore leaves the detector inactive and logs an
error.

The mod does not alter AIV selection or castle construction. It captures each
AI slot's lord and AIV source list immediately before the skirmish starts, hooks
the final native layout preparation, keeps only the last finalized candidate
per player, and reports the joined result at Info level after `OnStartMap(Post)`
confirms that the complete native map-start routine has returned. The player
slot must also be an active AI at that point.

The passive placement oracle additionally observes Vanilla's own candidate and
rotation tests. It never invokes a placement function itself and returns every
native result unchanged. Each `AIV placement oracle selection` line is followed
by one `AIV placement oracle attempt` line per tested candidate/rotation with the
raw fit score, fit percentage, evaluated and blocked cell counts, map identity,
map SHA-256, origin, and Keep reference. The map hash is calculated once per
map load so every row in one selection refers to the same file identity.

Multiple entries in the lobby are not a random-choice pool. The managed
`StartSkirmishGame` path imports every selected custom AIV in its captured list
order. The native placement scan then evaluates candidate/rotation fits and emits
one finalized best-fit candidate ID (`placementState=1` partial or `2` complete).
Offline consumers must consequently evaluate the full ordered list instead of
preselecting one entry randomly.

## Test

1. Close the game and run `build.bat`.
2. Start a skirmish containing one or more AI players.
3. Search `BepInEx/LogOutput.log` for `Active AIV confirmed`.
   Search for `AIV placement oracle` to inspect all native fit attempts.
4. Test these useful cases:
   - historical AIV set or a custom list containing one AIV;
   - a custom list containing several AIVs;
   - normal built-in and community sets;
   - map restart or loading a new map.

Expected values include:

- `lord` and `baseLordEnum`: the logical AI lord;
- `runtimeLordEnum`: the engine slot, which can be `SK_X1` through `SK_X8`
  while a custom lord configuration is active;
- `lordJson`: the selected custom configuration or the concrete bundled
  Vanilla `.lordjson` corresponding to the embedded default;
- `lordJsonSha256`: SHA-256 of that file when it is locally available;
- `lordConfigEffectiveSource`: whether the game read a custom file or used its
  embedded AIC. For Vanilla AICs the bundled file is only reported as a
  verified equivalent export after its manifest hash matches the installed
  `CrusaderDE.dll`;
- `aivMode`: default, community, historical, or custom;
- `candidateId`: selected candidate inside the imported list;
- `aivName` and `aivJson`: selected custom file/path or the concrete bundled
  file under `VanillaAIV` corresponding to embedded game data;
- `aivJsonSha256`: SHA-256 of that AIVJSON when it is locally available;
- `effectiveSource`: whether the data came from an actual custom file, embedded
  data, or a Script Extender asset override. A bundled Vanilla AIVJSON is the
  official editor equivalent and is not the file read by the game;
- `orientation`: `0`, `2`, `4`, or `6`;
- the selected orientation is shared by the AIV and the real rebuilt
  Keep/start complex, including the stockpile and other coupled start
  buildings; it is not an independent AIV-only rotation;
- `placementState=1`: best available partial fit;
- `placementState=2`: complete fit.

Oracle result fields use these native definitions:

- `rawFitScore=999999`: every scored AIV cell passed the native validator;
- positive lower score: partial fit, with the value tied to the earliest blocked
  build entry;
- non-positive score: rejected candidate/rotation;
- `fitPercent=(evaluatedCells-blockedCells)*100/evaluatedCells`;
- mapper value `1` is copied into the temporary occupancy grid but is not counted
  as an evaluated building cell.

This prototype intentionally does not hook `DLL_ImportAIV`, because the Script
Extender already detours that export. The game's `StartSkirmishGame` code imports
custom AIVs in the exact order of the captured list, so the native candidate ID
can be mapped without adding a second detour to the same export.

## Opt-in native traces

The cell trace can copy the native mapper, score, and result grids immediately
after filtered `EvaluateCandidateFit` calls. During only each active fit window
it also records the generic validator's tile ID, mapper, arguments, unchanged
return value, and the live native tile inputs used by that call.
These include terrain flags, heights, occupancy IDs, owner, organism class,
and game mode. Every trampoline is called exactly once; the trace does not
invoke or alter any placement function.

The trace is disabled by default. Enable `[Oracle cell trace] Enabled = true` in
`BepInEx/config/ActiveAIVDetector_Serp.cfg`. Matching cells are written as a
CRLF TSV file under
`BepInEx/plugins/ActiveAIVDetector_Serp/CellTraces/`; the normal log receives
timestamped capture and output summaries only.

For the current Chat 10 Thasos PreBuild run, `build.bat /trace` installs the
explicitly enabled profile from the historically named
`Diagnostics/Chat10-Bow-Ridge-Trace.cfg`. Version 0.9.3 disables the completed
cell-trace profile there and enables `[Oracle prebuild trace]` for player 2 and
one capture. This diagnostic hooks `ExecuteBuildStep` only while explicitly
enabled, records every frame synchronously around its single trampoline call,
and writes the unchanged return value plus all real `BuildingId` additions,
removals, and replacements under `PrebuildTraces/`. The placement-state pointer
must match the pointer observed during the filtered native validator calls.
Frames for mappers 52, 89, and 105 are marked and summarized explicitly. A
normal `build.bat` does not install or enable the profile. Its historical
filename remains unchanged because existing build automation refers to it.
