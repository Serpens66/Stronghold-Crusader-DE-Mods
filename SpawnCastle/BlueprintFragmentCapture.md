# Blueprint depth-fragment capture

This workflow is development-only. Normal players should leave
`CaptureBlueprintFragments = false` in
`BepInEx/config/SpawnCastle_Serp.cfg`.

## Smoke capture

1. Set `[Development] CaptureBlueprintFragments = true` and start the game.
2. On a suitable map, hold a multi-row 4x4 building and a large building as
   valid Vanilla construction previews. Stairs use their placed Tilemap visuals;
   build them in both ascending directions and keep each target still until the
   log reports both successful reconstruction validation and a saved depth
   capture. Finished Wall, Woodwall, Crenal, and Crenal2 icons are maintained as
   complete screenshot-derived PNGs and deliberately have no fragment capture.
3. Inspect
   `BepInEx/plugins/SpawnCastle_Serp/BlueprintImages/_Captured`.
   `BlueprintFragmentCaptures.tsv`, `BlueprintCaptureTiles.tsv`, and
   `BlueprintFragments.tsv` must exist, use CRLF, and reference individual PNGs
   below `Fragments/<mapper_skin_view>/`.
4. Check `BepInEx/LogOutput.log`. A capture is accepted only after its saved
   fragment PNGs were loaded again, recomposed, and compared with the immediate
   Vanilla reference within the built-in pixel and alpha tolerance.

## Complete capture set

Continue selecting the requested construction previews until
`_Captured/MissingBlueprintCaptures.txt` reports no missing fragment variants.
The list comes from the existing `GetRequiredRequests` catalog; it intentionally
contains only the known mapper, skin, front/back, and normalized-flip variants,
not every map rotation. Include the Church and Mosque skins, gates,
drawbridges and both stair directions shown by that catalog.

Front/rear keys are routed automatically. Engineers/Tunnelers Guild and Oil
Smelter still need to be shown once from each half-turn of the map; adjacent
quarter-turns are normalized by mirroring. For Drawbridges, hold the preview at
a front and a rear side of a built gatehouse. The actual side selects the key;
rotating the map is optional and does not select a Drawbridge variant by itself.
Stairs must be built in both ascending directions. All numbered stair mapper
cells share `MAPPER_STAIR_Generic_StairNorth.png` or
`MAPPER_STAIR_Generic_StairSouth.png`; their count is not limited by the image
library. The operator never selects a front/rear key manually.

Normal preview recaptures retain an existing composite PNG byte-for-byte.
Placed stairs deliberately overwrite the shared directional stair composite.
Do not replace the four screenshot-derived Wall/Crenal PNGs with held-preview
captures.

## Validate and import

Close the game, then run from the repository root:

    powershell -ExecutionPolicy Bypass -File SpawnCastle/tools/Validate-And-Import-BlueprintImages.ps1

The importer rejects unsafe paths, schema errors, duplicate or discontinuous
indices, incomplete variants, invalid PNG dimensions, wrong hashes, and invalid
row offsets before copying anything. It imports the three manifests and their
fragment directories together while retaining the existing composite assets.

Finally set `CaptureBlueprintFragments = false`, rebuild with
`SpawnCastle/build.bat`, and test `baldwin6.aivjson` at scale 1 and alpha 0.55.
Check walls both behind and in front of large buildings, all four map rotations,
flat view, reserved yards, gates, drawbridges, stairs, crenellations, churches,
large towers, and slightly uneven ground. Building the Blueprint should allocate
the fragment renderers once; normal frames must not create additional fragments.
