# AI woodcutter construction gate

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Evidence: installed `CrusaderDE.dll` and `exports/decompiled-functions.c` for RVAs `0x51540` and `0xC3BF0`.

- `0x51540` checks the woodcutter census, calls wood search `0x58020`, then nearby-position search `0x58950`. If both retain a position, it passes the nearby coarse coordinates multiplied by five to `0xC3BF0` with mapper index 3.
- `0xC3BF0` (133 bytes; raw SHA-256 `1AC2D046356187774EE0EF0C7F782C59B3E416BAEDDBD463305304CF709EB0B0`) compares the player's keep-tile path component with the target tile's component. Its source tile index comes from `0x379AFB0 + playerId * 0x583C`, its target tile from the row lookup at `0x402FF2C`, and both components from the 320,800-entry grid at `0x50EC690`. When the route check is needed, it calls `0xE2610` with mode 0. A zero return rejects construction; a nonzero return allows the caller to proceed.
- Only on a nonzero route result does `0x51540` call building construction at `0x6D580`. The installed Script Extender already detours `0x6D580` and exposes the pre/post `OnBuildStructure` event. Its post event does not carry the original return value. The lower-level `OnBuildingSpawn` event does carry its post return value.
- The `0xC3BF0` entry begins `48 83 EC 38 49 63 C0`: two complete straight-line instructions spanning seven bytes. The installed RedBird NativeX64 indirect-detour backend must confirm this displacement before an observation hook is published.
