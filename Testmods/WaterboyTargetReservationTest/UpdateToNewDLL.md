# Native update contract

Reference DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

The water-carrier target selector is `RVA 0xB8F40`. It is resolved by the unique executable-section pattern in `WaterboyNativeDefinition.FindNearestBurningBuildingPattern`; the reference RVA is accepted only after the local bytes match and the complete test suite confirms all three callers at `0x159DF8`, `0x159F4C`, and `0x15A08B`.

The installed RedBird 1.5.0 `NativeDetour<T>` must select `Indirect`, displace exactly the two complete instructions in `0xB8F40..0xB8F4A`, and install `FF 25 rel32` plus four NOP bytes. The pointer slot, hook entry, original entry, trampoline and chain depth are validated before the runtime is published. Any mismatch rolls back only the unpublished initialization candidate and leaves Vanilla active.

The implementation is intentionally hash-bound because it also depends on the audited native `GameBuilding` stride `0x32C`, manager-indexed fire offset `0x31A`, manager-indexed compound offset `0x304`, and `GameUnit` target identity offsets `0x39A/0x39C`. A new DLL requires a complete fireman-selector/state/extinguish audit and refreshed semantic baseline before these values may be changed.
