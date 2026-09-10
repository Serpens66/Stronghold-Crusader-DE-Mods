# `PathConnectionGrid` exposes twice Vanilla's audited element count

## Environment

- Stronghold Crusader Definitive Edition native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- SHCDE Script Extender: 2.3.0, semantic baseline commit `a0cd52993b44a6909d4f7f6a92f82fa5888a8e63`

## Finding

`GameTileManager.PathConnectionGrid` starts at the correct native address. The public span is created with `MAX_WIDTH * MAX_HEIGHT` (`800 * 800 = 640,000`) `ushort` elements, however Vanilla's PCL selection routine at RVA `0x572B0` scans only:

- start RVA: `0x50EC690`
- exclusive end RVA: `0x51890D0`
- byte length: `641,600`
- `ushort` element count: `320,800`
- highest valid index for this native array: `320,799`

The remaining 319,200 exposed elements are adjacent native memory, not part of the PCL array processed by this Vanilla routine. Iterating the complete public span therefore produces impossible distributions and values unrelated to tile PCLs.

## Suggested change

Expose `PathConnectionGrid` with the audited native element count of 320,800 for this binary, or introduce a separately named view whose length contract matches the native diamond-tile array. `IsValidTileId` should not by itself be used to validate an index into this particular view unless both contracts are intentionally identical and tested.

The start offset does not need correction. Consumers should still fail closed on an unsupported native hash.
