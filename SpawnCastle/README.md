# SpawnCastle

`SpawnCastle` adds three operating modes for a selected `.aivjson` castle:

- `Disabled` leaves the game unchanged.
- `Blueprint` displays a local, non-simulating construction guide.
- `Spawn` builds the castle on a newly started singleplayer map through the
  game's native AIV castle-building pipeline.

The dropdown scans:

- `BepInEx/plugins/SpawnCastle_Serp/AIV`
- the game's `CustomLords` and `ExtendedLords` directories
- the official Castle & CPU Lord Editor's `StreamingAssets/Villages` directory

Mode, selection, and the optional Blueprint key are stored locally in
`BepInEx/config/SpawnCastle_Serp.cfg`. They are never synchronized to other
players.

## Visual Blueprint mode

Blueprint mode parses the selected AIVJSON without invoking native build,
placement, tile, or network actions. It anchors the layout to the existing local
human Keep using the fixed human-Keep orientation. The overlay consists of:

- translucent isometric footprint markers rendered by mod-owned sprites above
  the ground;
- distinct colors for buildings, walls, crenellations, stairs, traps, moats, and
  pitch ditches; and
- a manually selected clean building image from Vanilla's
  `StreamingAssets/Help/Images` for supported normal-view structures, with the
  normal non-highlighted `UI-MasterAtlas` build-menu image as fallback; and
- compact build-menu images for flattened view and for individual wall, moat,
  pitch-ditch, trap, and other placements without an approved Help image.

Help images are decoded once from the installed game, cleaned without modifying
the original files, alpha-trimmed on the CPU, and cached as inexpensive
`FullRect` sprites. The renderer never requests Unity's synchronous `Tight`
mesh generation. Missing or unreadable images safely fall back to the matching
build-menu resource.

The three Church mappers follow Vanilla's lord skin condition. Lord types 1, 2,
6, and 7 use the Mosque menu variants; the large normal-view building uses
`ST100_Mosque.png`. Other lord types use the Church menu variants and
`ST36_Church.png`.

The Keep frame is used only as the anchor and is not drawn. AIV `miscItems` are
ignored. Unknown mapper values remain visible as magenta one-tile markers.

The overlay starts hidden on every map. Use the `Blueprint: off/on` button in the
upper-left MainHUD or assign any single Unity key, mouse button, or controller
button in Mod Settings. The overlay rebuilds after map rotation or flattened
landscape changes and is cleared on map unload. In Vanilla's flattened-landscape
view, all Blueprint images use the former compact, footprint-constrained icon
size so the overview remains readable. Returning to the normal view restores
regular buildings to their natural world scale. When leaving the flattened view,
the complete Blueprint is temporarily hidden while Vanilla restores terrain
heights and Tilemap transforms. It is rendered again only after the normal-height
projection has settled, so no stale intermediate overlay is displayed.

Because Blueprint mode does not change simulation state, it works on new maps,
loaded savegames, and multiplayer. Every multiplayer client independently selects
and displays its own local file.

## Blueprint hotkey implementation

SHCDE destroys BepInEx's early plugin GameObject after the Chainloader finishes.
Consequently, `BaseUnityPlugin.Update`, component-bound coroutines, and cleanup
from its early `OnDestroy` are unsuitable for input functionality that must
remain active for the complete process lifetime.

Noesis owns the input focus while Mod Settings is open, so
`UnityEngine.Input.GetKeyDown` alone does not reliably receive assignment input.
The settings XAML therefore captures keyboard and mouse input directly through
`PreviewKeyDown`, `KeyDown`, and `PreviewMouseDown` event triggers. Their
`InvokeCommandAction` instances use `PassEventArgsToCommand="True"`. The commands
translate Noesis keys and mouse buttons into individual Unity `KeyCode` values
and mark the triggering event as handled.

The mouse click that activates **Assign key** is not assigned accidentally:
Noesis delivers the button's mouse event before its command enables capture.
`Application.onBeforeRender`, Vanilla's `KeyManager`, and held-state polling
provide process-persistent fallbacks, especially for controller buttons. These
callbacks and Noesis event registrations must not be removed from the early
`BaseUnityPlugin.OnDestroy()`.

The mouse-preview measurement code is retained but disabled by the central
`BlueprintBuildingSizeCalibration.EnablePreviewMeasurement` switch. Setting it
to `true` allows building-icon sizes to be calibrated directly from Vanilla's
transparent construction preview in normal landscape view. Selecting a supported
building in the build menu and holding it over the map is sufficient; no
placement is required. SpawnCastle then uses the proven wide Tilemap scan and
ignores unchanged sprites and ordinary 64x32 placement-ground sprites.
Buildings with additional Vanilla placement reservations are excluded from
mouse-preview measurement.
Their visible widths are fixed from the Help-image-to-reservation ratios instead:
all three Barracks 5/10, Engineers and Tunnelers Guild 5/7.5, and Oil Smelter
253/384. After three stable samples a
measurement for any other supported building is stored in
`BepInEx/config/SpawnCastle_Serp.BlueprintBuildingSizes.tsv`. A visible
Blueprint is refreshed immediately. Every rendered building should have either
a usable measurement or a fixed reserved-area width. If neither exists but a
valid icon and footprint are known, SpawnCastle reports an error and estimates
the normal-view scale by fitting the complete icon width to that footprint. Only
when even this estimate is not meaningful is that building icon skipped; the
Blueprint continues rendering without throwing. The preferred uniform
normal-view scale covers both the measured width and height. Clean Help images
use the measurement without a generic correction; build-menu image fallbacks
retain their small visual correction.

The Crusader, Arabian, and Bedouin Outposts are known footprint-5 calibration
candidates even though they are not AIV Blueprint icon entries. Their map-editor
build-menu images are `UI-Buildings N073`, `N071`, and `N075`, respectively.
Holding each Outpost's Vanilla construction preview over the map therefore adds
its measured size to the same TSV file. Unlike Barracks, they have no additional
reserved placement area; the measured width of each Outpost is exactly 5 world
units.

Saved measurements carry an algorithm revision. Only revision 4 and newer are
used; older measurements came from superseded filters. The three Barracks,
both guilds, and the Oil Smelter cannot be overwritten by old TSV rows or by
their much larger reservation-yard previews.

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

Hovels require one additional native-compatibility adjustment. The AIV
executor passes visual-style argument `15` because Vanilla's AI-only Hovel
placement path ignores it and instead cycles through styles `0..6`. A human
player takes Vanilla's human Hovel path, which consumes the argument directly;
style `15` consequently selects invalid Hovel graphics. While the prepared AIV
is executing, SpawnCastle rewrites only Hovel `OnBuildStructure(Pre)` arguments
to the same deterministic `0..6` cycle. It does not mark the human player as AI
and does not alter non-Hovel buildings.

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

Native Spawn mode permits local singleplayer skirmishes and blocks real
multiplayer sessions because invoking the pipeline on only one client would
desynchronize the native game state. It does not use
`GameNetworkAPI.IsNetworkedEnvironment()` as
the deciding signal: Vanilla also creates a local `gameMembers` list for regular
singleplayer skirmishes. Instead, the guard combines `Director` state with lobby
member classification, real Steam-backed game members, and `GameData`'s skirmish
type. `Director.SkirmishModeGame` is logged but is not required during the early
native callback because Vanilla sets it later in the managed loading sequence.
Loading a savegame is also excluded to prevent duplicate castles.

The development `build.bat` overlays deployed files instead of deleting the
installed plugin directory. This preserves the local BepInEx configuration
across rebuilds.

Technical reverse-engineering and implementation details are documented in
[`AISpawnCastle.md`](AISpawnCastle.md).
