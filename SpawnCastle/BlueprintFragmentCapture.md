# Blueprint depth-atlas capture

This workflow is development-only. Normal players should leave
`CaptureBlueprintFragments = false` in
`BepInEx/config/SpawnCastle_Serp.cfg`.

## Smoke capture

1. Set `[Development] CaptureBlueprintFragments = true` and start the game.
2. On a suitable map, hold a multi-row 4x4 building and a large building as
   valid Vanilla construction previews. Stairs use their placed Tilemap visuals;
   build them in both ascending directions and keep each target still until the
   log reports both successful reconstruction validation and a saved depth
   capture. Finished Wall, Woodwall, Crenal, and Crenal2 icons remain protected
   screenshot-derived PNGs; the importer turns them into regular one-fragment
   depth-atlas captures instead of accepting automatic wall fragments.
3. Inspect
   `BepInEx/plugins/SpawnCastle_Serp/BlueprintImages/_Captured`. The detailed
   development manifests, individual source fragments, and the generated
   `BlueprintDepthAtlases.tsv` plus `DepthAtlases` directory must exist. Text
   manifests must use CRLF.
4. Check `BepInEx/LogOutput.log`. A capture is accepted only after its source
   fragments were loaded again, recomposed, and compared with the immediate
   Vanilla reference within the built-in pixel and alpha tolerance. Atlas
   packing then copies those exact pixels into a deterministic, padded page.

## Complete capture set

Continue selecting the requested construction previews until
`_Captured/MissingBlueprintCaptures.txt` reports no missing variants. The list
comes from the existing `GetRequiredRequests` catalog; it intentionally contains
only the known mapper, skin, front/back, and normalized-flip variants, not every
map rotation. Include the Church and Mosque skins, gates, drawbridges and both
stair directions shown by that catalog.

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

The importer validates the detailed capture source before changing the shipped
library. It rejects unsafe paths, schema errors, duplicate or discontinuous
indices, incomplete variants, invalid image dimensions, wrong hashes, invalid
row offsets, atlas rectangle overlaps, and pixel differences between fragments
and atlases.

The production library contains only `BlueprintDepthAtlases.tsv` and the PNGs
below `DepthAtlases` for normal-view depth rendering. The compact CRLF manifest
uses versioned capture (`C`), page (`P`), and fragment (`F`) rows. Detailed tile
capture data and individual fragment PNGs stay under `_Captured` and are not
loaded by normal players. Composite PNGs remain available for flat view. The
four protected Wall/Crenal composites additionally serve as the deterministic
sources for their generated single-fragment normal-view atlas pages.

The standalone migration command is:

    powershell -ExecutionPolicy Bypass -File SpawnCastle/tools/Convert-BlueprintFragmentsToAtlases.ps1 -SourceDirectory <captured BlueprintImages path> -TargetDirectory <production BlueprintImages path> -RemoveLegacy

`-RemoveLegacy` removes the old production `Fragments` directory and the three
legacy fragment manifests after the new atlas set has passed its validation.
Legacy version-1 composite manifests can be migrated once with:

    powershell -ExecutionPolicy Bypass -File SpawnCastle/tools/Add-BlueprintCompositeAlphaBounds.ps1 -BlueprintImagesDirectory <BlueprintImages path>

Finally set `CaptureBlueprintFragments = false`, rebuild with
`SpawnCastle/build.bat`, and test `baldwin6.aivjson` at scale 1 and alpha 0.55.
Check walls both behind and in front of large buildings, all four map rotations,
flat view, reserved yards, gates, drawbridges, stairs, crenellations, churches,
large towers, and slightly uneven ground. On a cold normal-view load, colored
ground meshes should appear first and the HUD should report
`Blueprint: loading x/y` while atlas meshes are added. Hiding and showing the
same valid overlay must reuse it. Alpha and scale changes must update the
existing renderers without rebuilding the complete layout.

The log records manifest loading, background file reads, PNG decoding, mesh
construction, first visible output, and completion with millisecond timestamps.
Changing AIV, map, keep position, camera projection, or layout while loading must
not append graphics from an older layout revision.
