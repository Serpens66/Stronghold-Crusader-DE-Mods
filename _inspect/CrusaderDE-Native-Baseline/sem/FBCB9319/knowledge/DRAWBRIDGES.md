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
