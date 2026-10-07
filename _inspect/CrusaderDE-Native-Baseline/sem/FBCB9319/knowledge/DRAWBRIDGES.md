# Drawbridges: curated native contracts

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Date: 2026-09-17. Static contracts were checked against the canonical DLL; the resulting ExtraFeatures behavior was also verified in game. Editor placement and editor lifecycle are outside this document.

Detailed implementation history, failed approaches and live-process methodology: [Findings/Drawbridges.md](../../../../../Findings/Drawbridges.md).

## Height topology

- `DefaultHeightGrid` is image-relative at RVA `0x4E2B870` and manager-relative at `tileManager + 0xDCCAC0`. The mutable `HeightGrid` is manager-relative at `tileManager + 0xD7E5A0`.
- Vanilla moat depth is eight raw height values. Elevated moat and drawbridge tiles therefore use `max(defaultHeight - 8, 0)`. Drawbridges at default height `<=12` retain Vanilla height zero.
- Completed drawbridge creation uses the 17-byte block at RVA `0x73B35`; lowering uses the 15-byte block at RVA `0x64546`. Vanilla state update, image-base restoration, graphic refresh and pathfinding refresh remain authoritative.
- Removal and cancellation paths restore the tile's own `DefaultHeightGrid`; no building-wide constant replaces tile-local terrain restoration.

## Common building-height reference

- Building records use manager RVA `0x64CCBB0`, stride `0x32C`, and height field `+0x148`.
- The building creator at RVA `0x6D580` calculates the footprint midpoint from minimum and maximum height, forwards it through the drawbridge creator at RVA `0x739C0`, and the allocator at RVA `0xB47E0` writes it at RVA `0xB49DC`.
- This building height is the common rigid reference for all visual drawbridge parts and units. Tile-local `HeightGrid` remains the terrain/topology value and must not be substituted as the rigid visual reference on sloped footprints.

## Rendering paths

- Three distinct Vanilla paths contribute drawbridge graphics and all are required:
  - special renderer entry RVA `0x45820`, 19-byte hook span;
  - animated arguments RVA `0x43BA2`, 17-byte hook span;
  - static arguments RVA `0x44EDD`, 16-byte hook span.
- The special renderer receives the Building-ID in `EDX`; immediately after its prolog Vanilla sign-extends `EDX` and multiplies it by record stride `0x32C`. Its two direct calls are at RVAs `0x44E3C` and `0x44EC3`.
- For an elevated building height `H>12`, the special and animated paths apply the relative shift `-(H-8)`. The static path uses `shadow-H`. For `H<=12` or an inactive feature, the displaced Vanilla instructions execute unchanged.
- The apparently doubled chains observed during development were differently positioned Vanilla subparts, not duplicate custom render objects. No custom queue entries, sorting or replacement renderer are needed.

## Unit height

- The common unit-height writer starts at RVA `0x184FD0`; the drawbridge block at RVA `0x18511C` has a 19-byte hook span and continues at `0x18512F`.
- Vanilla stores current elevation at unit-slot anchor `+0x712` and vertical correction at `+0x714`. For elevated drawbridges the correction is `buildingHeight - currentElevation`; the resulting surface is therefore the same building height used by all rigid drawbridge parts. Low drawbridges retain Vanilla `8-currentElevation`.
- Type-2 sprite producers at RVAs `0x43EF3` and `0x44346` consume this correction. Type-52 interpolation forwards the already computed height at RVA `0x1A22C4`; separate movement, interpolation or sorting hooks are not required.

## Hook backend contract

- Installed RedBird.X64 is `1.1.0`; installed Iced is `1.21.0`.
- RedBird's requested hook size is a minimum. The verified displaced byte counts for the six drawbridge hooks are `17`, `15`, `19`, `17`, `16`, and `19` bytes in the order creation, lowering, special renderer, animated renderer, static renderer, and unit correction.
- Productive generators are assembled and fully decoded in the internal test project. Hook publication is atomic and fail-closed; missing, mutated or ambiguous native signatures prevent the complete Elevated-Moat transaction from being published.

## Scope boundary

These contracts establish runtime drawbridge height, rendering, unit placement, refresh and restoration behavior for the stated binary hash. They do not establish editor-mode placement permissions or editor lifecycle behavior.

## Blueprint view contract (2026-10-07)

Installed native and managed hashes were rechecked: native `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, managed `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`. The following is a static rendering audit and asset inspection. On 2026-10-07 the user confirmed that the installed correction works in game; this confirmation does not establish which camera/terrain combinations were individually exercised.

- Creation `0x6D580` dispatches drawbridges to `0x739C0`; allocator `0xB47E0` stores tile X/Y at building-relative `+0x14A/+0x14C`, footprint size at `+0x154`, and the orientation argument at `+0x15E`. Native records are sentinel-relative, one-based Building-IDs, stride `0x32C`. Invalid map coordinates and exhausted allocation return zero. The creation path writes the ordered footprint occupancy and refreshes graphics/pathfinding through `0x6CDD0/0x725E0`.
- `0x6CDD0` selects drawbridge render-anchor cells for all four camera quarters. `0xA5E20` clears the previous subpart frames and calculates `(buildingOrientation - cameraRotation + 8) % 8` for negative differences; valid cardinal views are `0/2/4/6`. Open, raising, raised and lowering states use that same view index; state transitions do not derive orientation from height. Gate-adjacency lookup `0xB9330` chooses alternate static supports in two views and returns zero when no eligible live gate is found.
- Tile renderer `0x41D60` routes the drawbridge to the special/animated path `0x45820` and static path `0x4C1D0`, with flattened-view and tile-render flags controlling visibility. `0x19D110` publishes the selected frames, offsets and transform fields into the sprite queue. Height shifts affect placement, not the view choice. Function names in the semantic index remain candidate names; these specific branches and field flows were inspected directly in the current export.
- Managed `GameMap.mapGameTileToTilemapCoord` applies the current North/East/South/West camera transform before isometric cell projection. Blueprint direction must use these planar cell centers; adding terrain or flattened-view heights before comparing bridge and gate can reverse the vertical sign on slopes. The visual ground-height path remains separate.
- CastlePlanner's bundled `DrawbridgeFront` PNG is canonical BottomRight; `DrawbridgeRear` is canonical TopLeft. Mirror the front for BottomLeft and the rear for TopRight. Composite and depth captures resolve through `BlueprintBuildingCaptureCatalog`; the fallback catalog shares the same mirror mapping. Unknown adjacency retains the existing unresolved-image behavior.
- Retained regression coverage: `Helpers/AIVParser/AIVParser.Tests/Program.cs` tests both gate sizes, A/B axes, both bridge sides and all 16 castle/camera combinations per fixture against frozen expected screen directions and the audited managed projection. `_inspect/Test-CastlePlannerDrawbridgePreflight.ps1` checks that runtime direction cannot consume terrain/flattened height, and that the two library readers share the capture request. No native hook, executable patch, Script Extender source change or new game API is needed.

Validation on 2026-10-07: all 39 AIVParser test groups passed, including 128 gate/castle/camera fixtures; all 98 CastlePlanner test groups passed. JSON/lifecycle, persistent publishers, XAML, enum source/assembly parity, planar-direction and workspace executable-mutation checks passed. The runtime build reported zero warnings/errors. Installation used CastlePlanner/build.bat; local and installed CastlePlanner.dll match SHA-256 BF8BCA1EEEA3000F844F3837F24785E0985C9A19B588C028BF698E80276EDA6C. This initial validation build used version 0.8.36; automated fixture success does not claim a live four-view visual check.

Release acceptance on 2026-10-07: the user confirmed the fix works and requested the version increase to 0.8.37. Plugin and package versions are advanced together; the historical 0.8.36 changelog remains intact.
