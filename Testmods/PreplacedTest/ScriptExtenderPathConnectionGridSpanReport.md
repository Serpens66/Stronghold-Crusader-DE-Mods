# `PathConnectionGrid` exposes more elements than Vanilla's native PCL array

## Environment

- Stronghold Crusader Definitive Edition native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- SHCDE Script Extender: 2.3.0, semantic baseline commit `a0cd52993b44a6909d4f7f6a92f82fa5888a8e63`

## Finding

`GameTileManager.PathConnectionGrid` starts at the correct native address, but its public span uses `MAX_WIDTH * MAX_HEIGHT` (`800 * 800 = 640,000`) `ushort` elements. Multiple independent Vanilla paths use only 320,800 entries:

| Evidence | RVA | FileOffset | Native behavior |
| --- | ---: | ---: | --- |
| Economy-grid update | `0x50720` | `0x4FB20` | Processes the PCL grid with the native diamond-map capacity. |
| Dominant-PCL selection | `0x572B0` | `0x566B0` | Scans exactly `0x4E520`, or 320,800, `ushort` entries. |
| PCL-array start | `0x50EC690` | N/A | Runtime address in the zero-filled virtual part of `.data`; it has no file-backed offset. |
| PCL-array exclusive end | `0x51890D0` | N/A | Exactly 641,600 bytes after the start; a separate byte grid begins here. |

The valid native range is therefore:

- byte length: 641,600;
- `ushort` element count: 320,800;
- valid indices: `0..320799`;
- excess elements in the current public span: 319,200.

The excess span does not address additional PCL entries. It reinterprets adjacent native grids and later memory as `ushort` PCL values. This can silently produce plausible-looking but invalid labels and distributions rather than an immediate access violation.

## Practical impact

Mods and analysis tools commonly use the PCL grid to count regions, classify map components, compare connectivity, or decide whether a destination is reachable. Iterating the current public span can feed unrelated native data into those calculations, leading to false topology or reachability conclusions. This use case does not depend on any particular consumer implementation.

## Suggested change

Introduce one audited native diamond-tile capacity of 320,800 entries for this binary and use it for `PathConnectionGrid`. Audit `IsValidTileId` and the other flattened tile-grid views against the same native layout instead of assuming that every native grid contains `800 * 800` entries.

If changing the existing property's length is considered too disruptive, add a correctly bounded named view and deprecate the unsafe contract. The start offset does not need correction. Consumers and the Extender should continue to fail closed on unsupported native hashes.
