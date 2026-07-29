# Active AIV Detector

Temporary diagnostic BepInEx mod for validating the native active-AIV selection
before moving the capability into the Script Extender.

The mod does not alter AIV selection or castle construction. It captures each
AI slot's lord and AIV source list immediately before the skirmish starts, hooks
the final native layout preparation, keeps only the last finalized candidate
per player, and reports the joined result at Info level after `OnStartMap(Post)`
confirms that the complete native map-start routine has returned. The player
slot must also be an active AI at that point.

## Test

1. Close the game and run `build.bat`.
2. Start a skirmish containing one or more AI players.
3. Search `BepInEx/LogOutput.log` for `Active AIV confirmed`.
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
- `placementState=1`: best available partial fit;
- `placementState=2`: complete fit.

This prototype intentionally does not hook `DLL_ImportAIV`, because the Script
Extender already detours that export. The game's `StartSkirmishGame` code imports
custom AIVs in the exact order of the captured list, so the native candidate ID
can be mapped without adding a second detour to the same export.
