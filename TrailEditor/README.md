# TrailEditor

Offline editor and lossless unpacker/repacker for Stronghold Crusader Definitive Edition `.trail` files.

## Development build

The source tree requires the .NET 10 SDK and source checkouts of these additional projects:

- `MapParser`, containing `MapParser.Core\MapParser.Core.csproj`;
- `shcde-script-extender`, containing the `SHCDESE.AIVDecoder` and `SHCDESE.AICDecoder` projects.

By default, `MapParser`, `shcde-script-extender`, and `TrailEditor` are expected to be sibling directories below one common dependency root. No user-profile, drive-letter, Steam, or game-installation path is required.

The locations can be changed without editing a project file. Set `TrailEditorDependencyRoot` to a directory containing both dependency repositories, or set the MapParser project and the two decoder source roots individually:

    set "TrailEditorDependencyRoot=X:\path\to\dependency-root"
    set "TrailEditorMapParserProject=X:\other\MapParser.Core.csproj"
    set "TrailEditorAivDecoderSourceRoot=X:\other\SHCDESE.AIVDecoder"
    set "TrailEditorAicDecoderSourceRoot=X:\other\SHCDESE.AICDecoder"

TrailEditor links only the required platform-neutral AIC/AIV codec source files. It deliberately does not reference the upstream decoder executable projects, because executable project references can introduce conflicting runtime identifiers during cross-platform publishing. The upstream decoder programs themselves are not modified.

Run `build.bat` after configuring the paths. It checks for `dotnet`, builds the solution, runs the tests using the repository-local Vanilla and custom-lord trail fixtures, and leaves the development CLI at `TrailEditor.Cli\bin\Release\net10.0\TrailEditor.exe`. Missing SDKs, source projects, or test data cause an explicit build error.

## Portable Windows-x64 release

Run `publish-win-x64.bat` to build, test, and publish a self-contained single-file release under `dist\win-x64`. This directory is deliberately separate from the development output and is the directory intended for distribution.

The generated package includes `TrailEditor.exe`, the two end-user BAT files, a release README, and the `sources`, `unpacked`, and `repacked` directories. Recipients do not need .NET, the SDK, the dependency source trees, or a particular installation path. The end-user BAT files never attempt to compile source code; if the executable, inputs, or paths are missing, they display a specific error and remain open.

## Self-contained Linux-x64 build

Linux users must build the program from the complete source tree. The build machine needs Git, the .NET 10 SDK, and internet access for the initial NuGet restore. The finished self-contained executable does not require an installed .NET runtime.

Clone this repository and the Script Extender dependency into the expected sibling layout:

    git clone https://github.com/Serpens66/Stronghold-Crusader-DE-Mods.git
    cd Stronghold-Crusader-DE-Mods
    git clone https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender.git shcde-script-extender
    git -C shcde-script-extender checkout 368124119be230306f3f2593efa2a270b0e3dfb1

`MapParser` is already included in the main repository. Build the solution and run all tests from the repository root:

    dotnet build TrailEditor/TrailEditor.sln -c Release
    dotnet run --project TrailEditor/TrailEditor.Tests/TrailEditor.Tests.csproj -c Release --no-build

Publish the self-contained 64-bit Linux executable as a single file:

    dotnet publish TrailEditor/TrailEditor.Cli/TrailEditor.Cli.csproj -c Release -r linux-x64 --self-contained true -o TrailEditor/dist/linux-x64 -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
    mkdir -p TrailEditor/dist/linux-x64/sources TrailEditor/dist/linux-x64/unpacked TrailEditor/dist/linux-x64/repacked

The executable is `TrailEditor/dist/linux-x64/TrailEditor`. Use the CLI directly because the Windows BAT files do not run on Linux:

    cd TrailEditor/dist/linux-x64
    ./TrailEditor export-all sources unpacked
    ./TrailEditor build-all unpacked repacked

If the executable bit was lost while copying or extracting the file, restore it once with `chmod +x TrailEditor`. Existing outputs are not overwritten.

## Batch usage

1. Put `.trail` files anywhere below `sources`.
2. Run `unpack-all-trails.bat`.
3. Edit the generated `trail.json`, `.aivjson`, `.lordjson`, internals, images, or replace `map.map`.
4. Run `repack-all-trails.bat`.
5. Find the new files below `repacked`.

Inputs and existing outputs are never overwritten. Relative directory layouts are preserved.

## CLI

    TrailEditor inspect <file.trail>
    TrailEditor export <file.trail> [-o <bundle-directory>]
    TrailEditor build <trail.json> [-o <file.trail>]
    TrailEditor validate <file.trail|trail.json>
    TrailEditor export-all <sources-directory> <unpacked-directory>
    TrailEditor build-all <unpacked-directory> <repacked-directory>

The current writer targets restart format 60 and multiplayer setup format `-12` from SHCDE V2.8.

## `trail.json` options reference

All switches use `0` for off and `1` for on unless a different value is documented below. Unknown values should not be invented: several fields are used directly as native array indices.

### Bundle metadata

| Field | Meaning and valid values |
| --- | --- |
| `schemaVersion` | Bundle schema. Must remain `1`. |
| `originalFileName` | Name used by `build-all` for the rebuilt file. |
| `originalSha256` | SHA-256 of the exported source; informational provenance. |
| `mapFile` | Relative path to the editable embedded map, normally `map.map`. Paths outside the bundle are rejected. |
| `trail.formatVersion` | Restart format. Must remain `60`. |
| `trail.map.sourceKind` | Original map source: `0` local, `1` built-in, `2` Workshop. When building, the embedded `map.map` remains authoritative. |
| `trail.map.fileName` | Map name used by the restart data. It is synchronized with the embedded map when building. |

### Players, teams and positions

`trail.players` contains at most eight entries. Their array position is the player number minus one.

| Field | Meaning and values |
| --- | --- |
| `lordType` | `-1` local human, `-9999` empty slot, non-negative value encoded AI identity. Keep AI values synchronized with the corresponding `aiSlots` entry. Multiple `-1` entries do not create Coop players; all become the same local Steam user. |
| `team` | Team number. `0` is used for empty slots; active missions normally use `1` and above. Players with the same number are allied. |
| `colour` | Player-colour ID. `0` is used by empty slots; active colour IDs come from the game lobby. |
| `setup.keepLocationOrder` | Eight map-start locations. Each value is a zero-based player index; `-10` means unused. Example `2,3,0,1` assigns locations 1–4 to players 3, 4, 1 and 2. |

### Basic match settings

| Field | Meaning and values |
| --- | --- |
| `setup.fairness` | Gold advantage: `1` large human advantage, `2` human advantage, `3` equal, `4` CPU advantage, `5` large CPU advantage. Exact gold is listed below. |
| `setup.startingGameSpeed` | Initial game speed. The normal UI produces multiples of five from `10` through `80`; `40` is a common default. |
| `setup.startingGoodsLevel` | `1` Few, `2` Medium/normal, `3` Deathmatch, `4` hidden low-gold preset. Only `1..4` are accepted. It also selects non-gold starting resources in the native game. |
| `setup.winCondition` | Native multiplayer win-condition value. Existing custom trails use `0`; other semantics are not proven, so leave it unchanged. |
| `setup.allowAutoTrading` | Enables automatic market trading. |
| `setup.noKnockdownWalls` | Enables strong walls which normal troops cannot knock down. |
| `setup.autoSave` | Autosave interval in minutes: `0`, `5`, `10` or `20`. |
| `setup.peaceTime` | Peace time in minutes, normally `0..60`. |
| `setup.noCows` | Disables cow attacks. |
| `setup.noDogs` | Disables war dogs. |
| `setup.extremeTroops` | Enables the Crusader Extreme troop-power system. |
| `setup.extremePowers` | Enables Extreme powers. |
| `setup.extremePowersAroundLord` | Places/uses Extreme powers around the lord; meaningful only with Extreme powers enabled. |
| `setup.allowOutposts` | Allows outposts when the selected map supports them. |
| `setup.advancedOptions` | Master marker used by the multiplayer settings format. Preserve the exported value. |
| `setup.advancedSkirmishOptions` | Enables the advanced skirmish block. Set to `1` when any advanced option below should apply. |

The similarly named top-level fields `trail.extremeTroops`, `trail.extremePowers`, `trail.extremePowersAroundLord` and `trail.allowOutposts` are fields of `RestartSkirmishMapInfo`. They are preserved independently from the copies inside `setup`; for predictable results keep both copies equal.

### Starting-gold table

The game contains a fourth starting-goods level which is not offered by the normal skirmish UI. Set `setup.startingGoodsLevel` in `trail.json` to `4` to use it. The human/CPU gold values then depend on `setup.fairness`:

| Goods level | Fairness 1 | Fairness 2 | Fairness 3 | Fairness 4 | Fairness 5 |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 8000/2000 | 4000/2000 | 2000/2000 | 2000/4000 | 2000/8000 |
| 2 | 8000/2000 | 4000/2000 | 2000/2000 | 2000/4000 | 2000/8000 |
| 3 | 40000/3000 | 20000/7000 | 10000/10000 | 7000/20000 | 3000/40000 |
| 4 (hidden) | 4000/500 | 2000/500 | 500/500 | 500/2000 | 500/4000 |

Every cell is `human/CPU` gold. `trail.customisedExtremeTrail: true` multiplies both values by three (is ignored by the game, so does nothing!). `setup.noGold: 1` forces the human value to zero while the CPU continues to use its table value.

For an even match with 500 gold per side, use `startingGoodsLevel: 4` and `fairness: 3`. This is still a preset table entry; the `.trail` restart format has no independent arbitrary-gold field per player.

### Advanced gameplay options

| Field | Effect and values |
| --- | --- |
| `preBuild` | Enables prebuilt AI castles. |
| `improvedArabSwordsmen` | Enables the improved Arabian swordsmen rule. |
| `improvedLaddermen` | Enables the improved laddermen rule. |
| `improvedSpearmen` | Enables the improved spearmen rule. |
| `rebalancedHorseArchers` | Enables rebalanced horse archers. |
| `improvedFletchers` | Enables improved fletchers. |
| `uncappedPeasants` | Removes the normal peasant cap rule. |
| `fasterPeasants` | Enables faster peasants. |
| `enemyHitPoints` | Enemy HP setting `0..3`; the UI cycles through four levels. |
| `globalImprovedSieging` | Enables improved sieging. |
| `healers` | Enables healers. |
| `eunuchs` | Enables eunuchs. |
| `noGold` | `1` starts the human with zero gold; see the gold table above. |
| `globalImprovedSieging2` | Second V2.8 improved-sieging flag. Keep it equal to `globalImprovedSieging` unless testing a specific engine behavior. |

### Availability arrays

| Field | Structure |
| --- | --- |
| `buildingsAvailable` | Exactly 13 entries; `0` disables and `1` enables the corresponding multiplayer building group. Indices `0`, `1`, `2` are barracks, mercenary post and Bedouin stockade. The remaining indices follow the game's advanced-settings UI order. |
| `goodsAvailable` | Exactly 25 entries in native goods-enum order; `0` disables trading/availability and `1` enables it. Index `15` is gold in the native goods list, but starting gold is controlled separately. |
| `troopsAvailable` | Exactly 32 entries in the game's multiplayer troop order; `0` disables and `1` enables recruitment. |
| `preferredAivs` | Exactly eight entries, one per player slot. `-1` means no explicit preference. In normal custom-trail startup the game can replace these values from the exported `aiSlots[].rotation`. |

Do not reorder these arrays. Their indices are the serialization contract, not arbitrary list positions.

### AI, AIV, AIC and images

`trail.aiSlots` also contains exactly eight positional entries.

| Field | Meaning |
| --- | --- |
| `lordType` | Zero-based base lord type used by the AI slot. |
| `builtIn`, `community`, `historical` | Catalogue-origin flags used by the game's lord/AIV selection. |
| `rotation` | AIV rotation: `0`, `1`, `2`, `3` correspond to 0°, 90°, 180°, 270°. |
| `aivs` | References to exported `.aivjson` assets. Empty means the built-in selection is used. |
| `builtInLord` | `true` uses the built-in AIC; `false` requires the referenced exported custom lord data. |
| `lordConfig` | Custom `.lordjson` plus an internals JSON preserving `opponent_type_for_speech`, which public Lord JSON deliberately omits. Trail lord-config version 1 uses 1108 bytes; the current version 2 uses 1120 bytes. |
| `lordName` | Custom lord identity/name used by the game. |
| `imageFile` | Relative path to the optional 144x144 lord image. |
| `originalImageSha256` | Provenance hash of the exported image. |

### Trail bookkeeping flags

| Field | Meaning |
| --- | --- |
| `customisedExtremeTrail` | Applies the Extreme-trail x3 starting-gold multiplier. This is independent from enabling Extreme troops/powers. |
| `customTestMission` | Internal Trail Maker test/restart mode. It is not a Coop flag. Normally leave `false` in a published mission. |
| `customTrail` | Runtime bookkeeping flag. The game sets it when loading a mission from a custom trail. |
| `customTrailLevel` | Runtime mission index used for restart/progression; `-1` in a freshly produced mission. |
| `customTrailName` | Runtime trail identity used for restart/progression; normally empty in a freshly produced mission. |
| `customTrailDifficulty` | Runtime difficulty remembered for restart. The menu normally supplies this when launching the trail. |

## Coop trails

Restart format 60 has no `isCoop` or equivalent field. Vanilla Coop trails use a separate `CoopMissionSetupData` structure containing map name, keep order, teams, fairness, starting level, AI lords and AIV choices. These mission tables are constructed in `FRONT_Multiplayer.InitCoopMissions`; the game then creates a Steam multiplayer lobby and passes separate Coop trail and mission IDs into the native map initializer.

Adding a second negative `lordType` to `players` does not create a remote player: the `.trail` lobby constructor maps every negative entry to the same local Steam ID. A genuine custom Coop trail therefore needs a game mod which adds a new Coop catalogue/lobby loading path. It cannot be produced by changing only the `.trail` bytes.

### Simpler mod approach: replace an existing Vanilla Coop mission

This is feasible and substantially simpler than adding a fifth Coop menu. SHCDE V2.8 keeps four private static `CoopTrail1` through `CoopTrail4` arrays with ten `CoopMissionSetupData` entries each. A BepInEx/Harmony mod can run after the private `InitCoopMissions` method and replace one existing entry.

The replacement entry can directly set:

- multiplayer map name and resolved `FileHeader`;
- eight keep positions and teams;
- fairness and starting-goods level, including hidden level `4`;
- AI lord IDs and preferred AIV IDs.

The existing Vanilla Coop UI, invitation flow, Steam lobby, synchronization and mission-start path then remain in use. The trade-offs are:

- one Vanilla/DLC Coop mission slot is replaced and retains its original localized title, icon and progress slot unless those UI values are patched too;
- both players need the mod and identical map/assets;
- the map must be installed or registered as a multiplayer `.map`, because `CoopMissionSetupData` references a map name rather than embedding a `.trail` container;
- `CoopMissionSetupData` does not contain every field from `trail.json`. Full building/goods/troop restrictions, custom AIC/AIV payloads and images require an additional postfix on `CoopMissionChanged` plus synchronized asset loading.

For a first version limited to map, positions, teams, built-in lords/AIVs, fairness and start level, replacing an existing slot is the most practical route.
