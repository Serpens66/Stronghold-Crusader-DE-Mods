# SpawnCastle

`SpawnCastle` adds a Script Extender Mod Settings dropdown for selecting an
`.aivjson` castle blueprint. On a newly started singleplayer map, the blueprint is
spawned for the local human player through the game's native AIV castle-building
pipeline.

The dropdown scans:

- `BepInEx/plugins/SpawnCastle_Serp/AIV`
- the game's `CustomLords` and `ExtendedLords` directories
- the official Castle & CPU Lord Editor's `StreamingAssets/Villages` directory

Choose `disabled` to leave new maps unchanged. Loading a savegame never spawns a
second copy. The host selection uses the Script Extender's normal mod-settings
storage and is restored before the settings UI is bound:

`BepInEx/plugins/SpawnCastle_Serp/LobbyModSettings/SpawnCastle_Serp.msgpack`

If the selected file no longer exists, the setting safely falls back to `disabled`.

## Native castle spawning

The mod converts the selected AIVJSON into Vanilla's native `short[]` AIV format
and imports it into candidate slot zero of the local player during
`OnStartMap(Pre)`. This point is after Vanilla's `InitAIVLoading()` reset and
normal AI imports, but before the native start consumes the candidate table.
When Vanilla is about to build the local human Keep,
`OnBuildStructure(Pre)` invokes the placement part of the same private native
pipeline used by the skirmish setting **Completed enemy castles**:

1. allocate an AIV specification;
2. anchor it at the intercepted Keep coordinates;
3. let Vanilla test the candidate and all rotations;
4. prepare the complete layout;
5. register the specification as the player's active AIV; and
6. let the original Keep call continue with the prepared native coordinates
   and orientation.

During `OnBuildStructure(Post)` for that same Keep, the mod marks the player in
the prebuilt-castle bit field and executes the prepared AIV to 100 percent.
Both preparation and execution therefore happen while Vanilla's native
skirmish-start function is still running. Running the fit before the Keep
occupies its footprint prevents the human Keep from rejecting its own AIV;
executing before the outer start returns lets Vanilla's remaining startup
steps finalize building tiles and visuals.

This lets Vanilla handle regular buildings, walls, crenellations, gates, stairs,
traps, moats, pitch ditches, and other mapper-specific AIV entries. The previous
manual `CreatePrefab`/wall/tile implementation has been removed and is not used as
a fallback. If native import, placement, or execution fails, the failure is logged
and the mod does not attempt another spawn method.

The failure log also reads Vanilla's native candidate pointer table directly.
For a placement result of `-2`, it therefore distinguishes a missing candidate
from a candidate that is still present but rejected by the map-fit test.

The implementation is version-gated to the analyzed `CrusaderDE.dll`:

- product version `2.7.0.1`
- SHA-256
  `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`

Every private function and native global is resolved with an AOB signature and
must match exactly once. A hash or signature mismatch disables initialization
instead of calling unknown native code.

SpawnCastle permits local singleplayer skirmishes and blocks real multiplayer
sessions because invoking the pipeline on only one client would desynchronize the
native game state. It does not use `GameNetworkAPI.IsNetworkedEnvironment()` as
the deciding signal: Vanilla also creates a local `gameMembers` list for regular
singleplayer skirmishes. Instead, the guard combines `Director` state with lobby
member classification, real Steam-backed game members, and `GameData`'s skirmish
type. `Director.SkirmishModeGame` is logged but is not required during the early
native callback because Vanilla sets it later in the managed loading sequence.
Loading a savegame is also excluded to prevent duplicate castles.

The development `build.bat` overlays deployed files instead of deleting the
installed plugin directory. This preserves the runtime-created
`LobbyModSettings/SpawnCastle_Serp.msgpack` file across rebuilds.

Technical reverse-engineering and implementation details are documented in
[`AISpawnCastle.md`](AISpawnCastle.md).
