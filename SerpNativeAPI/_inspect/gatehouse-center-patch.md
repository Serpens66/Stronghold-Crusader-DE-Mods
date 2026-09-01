# Gatehouse center-distance patch evidence

## Provenance

- Canonical `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Gatehouse handler: RVA `0xB73D0`, exclusive end `0xB7CE5`
- Handler SHA-256: `F73E9FF6F69D9EC1ECD59D528BC6D4861739F54E0A9C59C6E6BAD91369FA57C8`
- Replaced distance block: `[0xB7B70, 0xB7BBB)`, exactly 75 bytes
- Script Extender `OnGatehouseQuery` hook: starts before the replacement and returns at `0xB7B70`; the ranges are adjacent and do not overlap.

The original 75 bytes were read again from the canonical installed DLL and match the compiled catalog exactly. The replacement is also exactly 75 bytes, contains no branch, and falls through to the unchanged `test sil, sil` at `0xB7BBB`.

## Semantics

Vanilla reads `r_TilePositionXBegin` and `r_TilePositionYBegin` and computes a Chebyshev distance from that corner. The replacement additionally reads `r_TilePositionXEnd` and `r_TilePositionYEnd` and computes:

    centerXNative = (beginX + endX) * 4
    centerYNative = (beginY + endY) * 4
    distance = max(abs(centerXNative - unitX), abs(centerYNative - unitY))

This preserves half-tile centers exactly. Reversed Begin/End bounds produce the same center. The resulting distance remains in the same native unit scale used by the unchanged Human and AI comparisons.

## Crash finding and corrected register contract

The first implementation loaded `unitX`, used `cdq` for `abs(dx)`, and only then loaded `unitY` through `[rdx + moduleBase + ...]`. `cdq` writes `EDX` and therefore destroys the still-live Unit-record offset in `RDX`; a negative X delta turns it into `0x00000000FFFFFFFF`, causing the following Y read to access an invalid address. The observed editor crash occurred exactly when a placed unit first entered this gatehouse query path, with no managed exception in the BepInEx log.

The corrected sequence loads both `unitX` and `unitY` before its first `cdq`. Only after both reads does it reuse `EDX` as the sign mask. `R8D` temporarily holds `unitX`, then the absolute X distance, and finally the maximum distance expected by the unchanged decision block.

## Bytes

Vanilla:

    0F BF 8C 2A 0E 8B 7E 06 42 8D 2C FD 00 00 00 00
    8B C1 44 8B C5 2B C5 44 2B C1 3B E9 42 8D 2C E5
    00 00 00 00 44 0F 4E C0 48 8D 05 61 84 F4 FF 0F
    BF 8C 02 10 8B 7E 06 8B D5 2B D1 8B C1 2B C5 3B
    E9 0F 4E D0 44 3B C2 44 0F 4C C2

Centered replacement:

    44 0F BF 84 2A 0E 8B 7E 06 0F BF 8C 2A 10 8B 7E
    06 0F BF 84 2B 0A CD 4C 06 44 01 F8 C1 E0 02 44 29
    C0 99 31 D0 29 D0 41 89 C0 0F BF 84 2B 0C CD 4C 06
    44 01 E0 C1 E0 02 29 C8 99 31 D0 29 D0 41 39 C0 44
    0F 4C C0 90 90 90 90 90

The five trailing NOPs pad the replacement to the exact original boundary. `R8D` contains the final distance at fall-through, matching the original contract. Registers used after either threshold path are reinitialized by Vanilla before their next use.

## Safety and validation

- The resolver validates full DLL hash, full handler hash, executable section, function bounds, original distance bytes, decision bytes, delay bytes, and all four Vanilla immediates before exposing the capability.
- Live memory must contain the expected complete Vanilla or centered block before every operation.
- The distance block and four immediates share one exclusive ownership transaction. Any write or verification failure rolls the complete block and all values back to their preceding expected state.
- `Enabled=false` restores Vanilla timing values but intentionally retains the capability-wide midpoint correction.
- Unknown hashes or any byte mismatch fail closed without mutation.
- The regression test pins all 75 replacement bytes and verifies that the complete `unitY` load precedes the first `cdq` write to `RDX`.

Automated coverage is in `_inspect/SerpNativeAPITests`. In-game acceptance for small and large gatehouses in every orientation remains required before release/versioning.
