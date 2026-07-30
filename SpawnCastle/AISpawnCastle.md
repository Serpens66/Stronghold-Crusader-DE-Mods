# Native AI castle spawning

This document records the current reverse-engineering results for the skirmish
option **Completed enemy castles** (`Settings_PreBuild`). It describes how the
game selects, places, and fully constructs an AI castle from AIV data and which
parts of that mechanism may be reusable by `SpawnCastle`.

`SpawnCastle` version `0.2.0` now implements this reuse directly. The former
managed `CreatePrefab`/wall/tile spawner has been removed; there is deliberately
no fallback path.

Since version `0.2.1`, multiplayer detection does not count alive non-AI player
resource slots and does not rely on
`GameNetworkAPI.IsNetworkedEnvironment()`. Both are false-positive sources in a
local skirmish: unused player resources can appear alive, and Vanilla creates a
local `Platform_Multiplayer.gameMembers` list even without network peers.
SpawnCastle instead combines `Director.MultiplayerGame` with lobby members not
marked as `SkirmishMember` and Steam-backed non-AI game members. The `Director`
value alone is not sufficient at `OnStartMap(Post)`, because the online frontend
can set it only after the synchronous native start call returns.

The same timing applies to `Director.SkirmishModeGame`: a local skirmish can
still report `false` at this callback. Version `0.2.2` therefore confirms a
singleplayer skirmish from an active lobby containing only `SkirmishMember`
entries, at least one `SkirmishHumanMember`, no Steam-backed human game member,
and a nonnegative `GameData.SkirmishGameType`.

The analysis applies to the following native library:

- File: `x86_64/CrusaderDE.dll`
- Product version: `2.7.0.1`
- SHA-256:
  `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`
- Preferred image base: `0x180000000`

All native addresses below are RVAs unless explicitly written as full virtual
addresses. They must be resolved relative to the loaded `CrusaderDE.dll` base.
They are not stable API addresses and must not be used without version checks or
AOB signatures.

## Summary

The lobby option does not spawn buildings through managed Unity code or through
`GameBuildingManagerAPI.CreatePrefab(...)`. Managed code only:

1. stores the `advopt_pre_build` setting;
2. converts AIVJSON into the native `short[]` AIV representation;
3. imports the AIV candidates for each AI player;
4. registers the AI player; and
5. passes the complete setup structure into `CrusaderDE.dll`.

During `DLL_StartMultiplayerGame(false)`, the native engine selects an AIV
candidate, tests its rotations and map fit, prepares all AIV build entries, and
then executes the normal AI castle construction routine at 100 percent. The
same low-level construction code is used for normal buildings, walls, moats,
pitch ditches, and other special AIV entries.

Simply setting `advopt_pre_build = 1` cannot construct a castle for the human
player. The native map-start loop only performs AIV selection and construction
for players registered as skirmish AI.

## Managed lobby setting

The managed setup type is `EngineInterface.MultiplayerSetupData`. Its relevant
field is:

    public int advopt_pre_build;

The lobby click handler is in
`.inspect/CrusaderDE/FRONT_Multiplayer.cs`, in the
`Settings_PreBuild` case around line 6110. It toggles
`MPsetupData.advopt_pre_build` for a skirmish game and
`MPTEMPsetupData.advopt_pre_build` while editing multiplayer settings.

The complete setup is serialized by `MultiplayerSetupData.ToString()`. In the
current format, `advopt_pre_build` follows `advanced_skirmish_options`.

### Native transfer structure

`EngineInterface.MultiplayerSetupTransferData` consists of sequential 32-bit
integer fields. The relevant beginning of the layout is:

| Offset | Field |
| ---: | --- |
| `0x00` | `fairness` |
| `0x04` | `starting_gamespeed` |
| `0x08` | `starting_goods_level` |
| `0x0C` | `win_condition` |
| `0x10` | `allow_autotrading` |
| `0x14` | `no_knockdown_walls` |
| `0x18` | `autosave` |
| `0x1C` | `peacetime` |
| `0x20` | `no_cows` |
| `0x24` | `no_dogs` |
| `0x28`–`0x44` | eight `start_keep_location_order` values |
| `0x48` | `extreme_troops` |
| `0x4C` | `extreme_powers` |
| `0x50` | `extreme_powers_around_lord` |
| `0x54` | `allow_outposts` |
| `0x58` | `advanced_options` |
| `0x5C` | `advanced_skirmish_options` |
| `0x60` | `advopt_pre_build` |

Managed `EngineInterface.setMultiplayerStartingData(...)` serializes this
structure and passes it to:

    DLL_ApplyMultiplayerSetupData(byte* data)

In the analyzed DLL, the export is located at RVA `0x802C0`. It copies the
32-bit value at structure offset `0x60` into the native global at virtual
address `0x1887EB2E8`.

The confirmed native references to this global are:

- `0x180093966`: copies the setting into map-start working data;
- `0x180094080`: writes the setting;
- `0x180095206`: tests it in the AIV castle initialization loop.

The comparison at `0x180095206` is the relevant PreBuild decision. A nonzero
value reaches the call at `0x180095255`, which invokes the full AIV execution
routine with a percentage of 100.

## AIVJSON conversion and native import

The native engine does not parse `.aivjson` directly. Managed code converts the
JSON document into a compact `short[]`.

The original implementation is:

    JsonUtility.FromJson<AIVLoader.SaveData>(json).GetRawData()

It is used in `.inspect/CustomisationFileManager.cs` around line 651.
`AIVLoader.SaveData.GetRawData()` is in `.inspect/AIVLoader.cs` near the start
of the file.

The raw sequence has this layout:

1. `pauseDelayAmount`;
2. number of pause indices;
3. pause indices;
4. number of build frames;
5. encoded build frames;
6. number of miscellaneous entries;
7. triples of miscellaneous item type, position, and number.

A build frame containing exactly one position is encoded as:

    itemType, position

A build frame containing multiple positions is encoded as:

    -itemType, positionCount, position1, position2, ...

For miscellaneous types greater than 9000, `GetRawData()` subtracts 9000 before
writing the native value.

The current `SpawnCastle` code should not return to `JsonUtility`, because it
has already produced empty frame lists in this plugin environment. If the
native mechanism is adopted, the existing dependency-free reader should emit
the same `short[]` format directly.

### `DLL_ImportAIV`

Managed import eventually calls:

    DLL_ImportAIV(
        int playerSlot,
        int candidateId,
        short* data,
        int shortCount,
        int custom);

The export is at RVA `0x83AC0`.

Despite the managed parameter name `AILord`, the first argument is the
zero-based player slot. Vanilla calls it as:

    EngineInterface.ImportAIV(playerId - 1, candidateId, data, custom);

`AIVLoader.UploadDefaultAIV(...)` normally uploads eight candidates per AI
player. Custom lobby selections may upload one or more custom candidates.

The native import accepts player slots `0` through `7`. It allocates and copies
the raw AIV data, then stores the pointer in an eight-by-1000 candidate pointer
table. Passing an invalid player slot is used by `InitAIVLoading()` to free and
clear all previously imported candidates.

For the supported DLL, the candidate-count helper at RVA `0x52F30` scans this
pointer table from candidate zero until the first null pointer. Each player
slot occupies `1000 * sizeof(void*)`, or 8000 bytes in the 64-bit process. The
Script Extender exposes the runtime table address as
`GameGlobalsManager.Instance.AIVDataTableVA`.

Consequently, a human player's slot can technically hold imported AIV bytes.
The missing part is not import support; it is the native AIV initialization
branch, which Vanilla does not run for a human player.

## Managed skirmish start order

`FRONT_Multiplayer.StartSkirmishGame(...)`, around line 10491 of the decompiled
file, performs the following relevant operations:

1. sets the preferred AIV/rotation values;
2. calls `EngineInterface.initMultiplayerGame(skirmishGame: true, ...)`;
3. calls `EngineInterface.setMultiplayerStartingData(MPsetupData)`;
4. calls `EngineInterface.InitAIVLoading()`;
5. iterates the lobby members;
6. registers a human with `RegisterMPPlayer(...)`;
7. registers an AI with `RegisterSkirmishUser(...)`;
8. imports the selected or default AIV candidates for each AI;
9. loads the multiplayer map;
10. calls `EngineInterface.StartMultiplayerGame(fromSave: false)`;
11. finishes managed post-loading and starts the simulation thread.

Any additional AIV import for `SpawnCastle` must occur after
`InitAIVLoading()`. An import made earlier would be cleared.

## Native AI-only branch

During native map start, the engine loops through player IDs 1 through 8. The
important part begins near virtual address `0x180094D35`.

Native player registration data selects one of two paths:

- a normal multiplayer/human path; or
- a skirmish-AI path with AIV candidate selection.

Only the skirmish-AI path reaches:

- AIV spec allocation;
- candidate and rotation evaluation;
- final layout preparation; and
- the PreBuild call.

This is why changing only `advopt_pre_build` cannot make Vanilla build the
human player's selected AIV. Registering the human as a skirmish AI to enter
this branch would alter player control and other player state and is not a
safe workaround.

## Native AIV state

The native AIV manager used during map start was observed at virtual address
`0x1834A7F00` in this DLL build. Runtime code must capture or resolve it rather
than hard-code this address.

An AIV spec has a stride of `0x6D98`. The following fields have been confirmed:

| AIV spec offset | Meaning |
| ---: | --- |
| `0x04` | one-based player ID |
| `0x08` | copied per-player AIV value |
| `0x0C` | orientation |
| `0x10` | selected candidate ID |
| `0x14` | placement state |
| `0x20` | value captured while allocating the spec |
| `0x24` | highest prepared build-frame index |
| `0x28` | placed AIV origin X |
| `0x2C` | placed AIV origin Y |
| `0x30` | keep/reference X passed to placement |
| `0x34` | keep/reference Y passed to placement |

Confirmed orientation values are:

| Value | Rotation |
| ---: | ---: |
| `0` | 0 degrees |
| `2` | 90 degrees |
| `4` | 180 degrees |
| `6` | 270 degrees |

Confirmed placement states are:

| Value | Meaning |
| ---: | --- |
| `0` | no accepted placement |
| `1` | best partial fit |
| `2` | complete fit |

`ActiveAIVDetector` hooks the final layout preparation routine and reads these
fields. A runtime test confirmed a selected Rat AIV with candidate ID 3,
orientation 2, and placement state 2 before `OnStartMap(Post)`.

## Native function chain

The following names are descriptive names assigned during analysis. They are
not symbols exported by the game.

### RVA `0x4F8E0`: allocate AIV spec

Proposed name:

    c_game_aiv_allocate_spec_for_player

Observed signature:

    int AllocateAivSpec(AivState* state, int playerId);

The function searches spec indices 1 through 8 for an unused entry. It records
the one-based player ID, initializes the placement state to zero, and returns
the allocated spec index. It returns zero when no free entry exists.

### RVA `0x54130`: set placement anchor and orientation

Proposed name:

    c_game_aiv_set_placement

Observed signature:

    void SetPlacement(
        AivState* state,
        int specIndex,
        int keepX,
        int keepY,
        int orientation);

It stores:

    originX = keepX - 43
    originY = keepY - 43
    referenceX = keepX
    referenceY = keepY

When the supplied orientation is negative, the function chooses one of the
four supported rotations from the native deterministic random state.
Otherwise it stores the requested orientation directly.

### RVA `0x541D0`: select the best candidate and rotation

Proposed name:

    c_game_aiv_select_best_fit

Observed signature:

    void SelectBestFit(AivState* state, int specIndex, bool tryOtherRotations);

The function:

1. obtains the imported candidate count for the spec's player slot;
2. loads each candidate into temporary AIV grids;
3. checks how much of the layout can be placed at the current origin;
4. immediately accepts a perfect result;
5. otherwise retains the best sufficiently complete partial result;
6. optionally repeats the tests for the other three rotations;
7. writes the candidate ID to offset `0x10`;
8. writes the final orientation to offset `0x0C`;
9. writes placement state 1 or 2 to offset `0x14`.

A perfect fit is represented internally by the special score `999999` and
becomes placement state 2.

### RVA `0x54050`: test a specific candidate

This alternative selection function tests one explicitly requested candidate
at the already selected orientation. It returns:

- `100` for a complete fit;
- a fit percentage for a partial fit; or
- `0xFFFFFFFE` when the candidate cannot be accepted.

Vanilla uses this path when lobby data requests a concrete preferred AIV.

The function returns `0xFFFFFFFE` in two distinct cases:

1. the requested candidate ID is outside the number of consecutive non-null
   candidate pointers for that player slot; or
2. the candidate was loaded, but the native map-fit function returned a
   non-positive score.

Since version `0.2.4`, `SpawnCastle` reads the real candidate table directly
after the Pre import and again before Post placement. The failure log labels
these cases as `candidate-missing-from-native-table` or
`candidate-present-but-map-fit-rejected`; it also logs candidate zero's native
pointer and the per-player value copied into AIV spec offset `0x08`.

The version `0.2.4` test comparison isolated the initial stockpile from this
failure:

- with **Place Initial Stockpile** enabled, player 1 owned nine building
  records before the Post fit test;
- with it disabled, player 1 owned five building records;
- both tests retained candidate zero at the same non-null native pointer; and
- both explicit fit tests returned signed `-2`.

The stockpile therefore accounts for four records in this startup state but is
not the placement blocker. The five records left without it belong to the
already-created Vanilla Keep complex. More importantly, native control flow
shows that AI placement selection and `PrepareAIV` run before the AI Keep is
built, whereas the original SpawnCastle prototype ran them from
`OnStartMap(Post)`, after the human Keep existed.

### RVA `0x52F70`: prepare final AIV layout

Known name used by `ActiveAIVDetector`:

    c_game_aiv_prepare_layout

Observed signature:

    void PrepareLayout(AivState* state, int specIndex, int playerId);

The current AOB signature for the function start is:

    44 89 44 24 18 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 68

This function runs after candidate and orientation selection. Among other
work, it:

- loads the selected candidate;
- applies the selected rotation;
- translates AIV coordinates into world tiles;
- builds per-frame placement entries;
- marks occupied/blocked tiles for the player;
- prepares multi-tile entries;
- copies miscellaneous defensive positions into per-player data;
- records the highest usable build-frame index.

The AIV grid is 100 by 100. Build entries are stored in a per-spec array and
are later consumed by the same function used during normal gradual AI castle
construction.

### RVA `0x551C0`: execute AIV to a percentage

Proposed name:

    c_game_aiv_execute_to_percentage

Observed signature:

    void ExecuteToPercentage(AivState* state, int playerId, int percentage);

The function obtains the active AIV spec for the player, reads its highest
build-frame index, calculates the requested final frame, and calls the
single-step routine for every frame from zero through that index.

The PreBuild branch passes `percentage = 100`, so every prepared frame is
processed immediately.

### RVA `0x509F0`: execute one AIV build frame

Proposed name:

    c_game_aiv_execute_build_step

Approximate signature:

    bool ExecuteBuildStep(
        AivState* state,
        int playerId,
        int frameIndex,
        int restrictedMode,
        bool freeOrForced);

The 100-percent PreBuild wrapper calls it with:

    restrictedMode = 0
    freeOrForced = true

The fifth argument bypasses checks used by normal gradual AI construction,
including resource availability. The routine then uses native building and
tile placement functions. It has distinct branches for different mapper
families instead of treating every entry as a prefab.

Observed special handling includes:

- regular and multi-tile buildings;
- stone/wood wall-like tile entries;
- pitch ditches (`itemType 99`);
- moats (`itemType 106`);
- gates and rotation-dependent multi-part structures;
- stairs, traps, and other mapper-specific entries;
- retry/delay states used by gradual AI construction.

This is the main advantage over `SpawnCastle`'s former manual
`CreatePrefab(...)` loop: Vanilla already knows which AIV entries represent
buildings, paths, tile overlays, or multi-part structures and invokes the
appropriate low-level game operation.

## PreBuild call

After a spec has an accepted placement, Vanilla calls
`c_game_aiv_prepare_layout(...)`. It then performs additional lord/keep setup
and reaches the PreBuild test at `0x180095206`.

When the complete-castle branch is selected, it:

1. sets the corresponding player bit in the native prebuilt-castle bit field;
2. passes `100` as the desired completion percentage;
3. calls `c_game_aiv_execute_to_percentage(...)`.

The relevant call site is:

    0x180095238  read/update prebuilt player bit field
    0x180095244  mov r8d, 100
    0x180095250  mov edx, playerId
    0x180095252  mov rcx, aivState
    0x180095255  call 0x1800551C0

The castle is therefore created before the managed game-start sequence calls
`EditorDirector.postLoading(...)` and before the simulation thread starts.

## Relationship to `ActiveAIVDetector`

`ActiveAIVDetector` already provides a validated hook for
`c_game_aiv_prepare_layout`. Its callback receives:

    AivState* state
    int specIndex
    int playerId

It captures the final candidate, orientation, and placement state, then waits
until `OnStartMap(Post)` before reporting the result. This establishes two
useful facts:

1. the AIV state pointer can be captured without hard-coding its global
   address; and
2. `OnStartMap(Post)` occurs after Vanilla's complete native AIV selection and
   PreBuild sequence.

The detector deliberately does not hook `DLL_ImportAIV`, because lobby
metadata preserves the imported candidate order and therefore maps the native
candidate ID back to the selected AIV source.

## Reuse options for `SpawnCastle`

### Option 1: only enable `advopt_pre_build`

This is insufficient.

It would construct completed castles for active enemy AI players, but the
human player never enters the native AIV initialization branch. It may also
change a lobby setting the user did not intend to apply to the opponents.

### Option 2: temporarily register the human as AI

This is not recommended.

Although it would enter the correct native branch, it would also change player
registration, control, lord state, and potentially network ownership. Restoring
all affected state afterward is not understood and would be fragile.

### Option 3: manually invoke the native AIV pipeline for the human

This is technically feasible and is the implementation used since
`SpawnCastle` version `0.2.0`.

The corrected sequence used since version `0.2.5` is:

1. parse the selected AIVJSON with the existing dependency-free reader;
2. encode the document into Vanilla's native `short[]` representation;
3. import candidate zero into `localPlayerId - 1` from
   `MapLoaderR3EventHooks.OnStartMap(Pre)`, after `InitAIVLoading()` and the
   normal lobby imports but before the native start consumes the candidate
   table;
4. subscribe to `BuildingR3EventHooks.OnBuildStructure(Pre)` and wait for the
   local player's Keep mapper;
5. at that point, Vanilla has initialized native AIV state but has not yet
   occupied the Keep footprint;
6. allocate an AIV spec for the local player;
7. use the intercepted Keep coordinates and orientation zero as the initial
   placement;
8. run the best-fit function with rotation search enabled and require placement
   state 1 or 2;
9. prepare the selected layout and write its spec index into the local player's
   active-AIV state;
10. read the prepared Keep X/Y globals and orientation from the selected spec;
11. replace the intercepted Keep call's coordinates and orientation with those
    prepared values, while retaining Vanilla's `MAPPER_KEEP2`, scale 7, and
    non-free build flags;
12. let the original native Keep call continue;
13. from the same Keep's `OnBuildStructure(Post)` event, set the local player's
    prebuilt bit and execute the prepared AIV to 100 percent, before the outer
    native skirmish-start function returns.

This matches the relevant native AI ordering at RVAs `0x950DE` through
`0x9511A`: `PrepareAIV` is called first, then the game reads the prepared global
Keep coordinates, reads the orientation from spec offset `0x0C`, and invokes
`c_game_player_build_structure` for mapper 61. The Script Extender's
`OnBuildStructure(Pre)` is a detour on that same building function (RVA
`0x6C7F0`), so it is early enough to reproduce the ordering for the human
branch without suppressing the entire Keep initialization path.

The per-player active spec field follows a stride of `0x583C`. In the analyzed
build, the field for player 1 is at virtual address `0x1837A0898`; subtracting
one stride gives the base associated with player index zero. These absolute
addresses must not be hard-coded.

This approach should create all castle elements directly for the human player,
so ownership conversion after spawning should not be necessary.

## Required implementation safeguards

A native implementation should include at least:

- SHA-256 or version verification for `CrusaderDE.dll`;
- AOB signatures for every private function being called;
- validation that every signature has exactly one match;
- validation of spec index, candidate ID, orientation, and placement state;
- map bounds and valid local-player checks;
- a strict new-game-only guard;
- no execution for savegame loads;
- timestamps with milliseconds in all diagnostic logging;
- logs before import, after placement selection, before 100-percent execution,
  and after execution;
- a safe disabled result when any native prerequisite is missing;
- retention of hook and delegate objects for the full process lifetime;
- no cleanup from `BaseUnityPlugin.OnDestroy()`, because that callback occurs
  during startup in this BepInEx environment.

There is intentionally no `CreatePrefab(...)` fallback. Runtime verification
should cover:

- stone and wood walls;
- crenellations;
- gates and drawbridges;
- all stair orientations;
- moats;
- pitch ditches;
- killing pits and other traps;
- multi-part or rotation-dependent buildings;
- partial-fit and out-of-bounds AIVs;
- save and reload;
- map restart;
- consecutive skirmish starts in the same process.

## Multiplayer considerations

Vanilla's PreBuild path executes deterministically as part of the synchronized
native game start. A mod-triggered human castle must preserve that property.

Before multiplayer support is enabled, all peers must have:

- the identical selected AIV data;
- an agreed checksum;
- the same rotation and placement result;
- the same native call order;
- the same execution point before simulation begins.

Running the native pipeline on only one client would create different building
and tile state and is expected to desynchronize the game. The first
implementation should therefore be limited to ordinary singleplayer skirmish
until synchronized execution has been tested explicitly.

## Current conclusion

The game's AI PreBuild mechanism is not a public Script Extender feature, but
the underlying native pipeline is reusable and is now used by `SpawnCastle`.
It is a better match than individually translating every AIV mapper into
`CreatePrefab`, `CreateWall`, pitch, moat, and trap API calls.

The current implementation is version-gated and contains:

- dependency-free AIVJSON-to-raw conversion;
- local-slot import from `OnStartMap(Pre)`;
- AIV fit and layout preparation from the local Keep's
  `OnBuildStructure(Pre)` event;
- 100-percent execution from the local Keep's `OnBuildStructure(Post)` event;
- AOB-resolved private native calls;
- explicit validation and logging;
- a strict SHA-256 gate for the supported native DLL;
- singleplayer and new-map-only guards; and
- no managed placement fallback.
