# Fireman Targeting and Extinguishing Audit

## Provenance

- Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Script Extender commit: `5b4d48e732e9b6e2e93c135f0b28ce5b9d8bcd33`
- Building stride: `0x32C`; unit stride: `0x490`

## Target selection

`FUN_1800B8F40` (`RVA 0xB8F40`) receives the building manager in `RCX` and a 1-based fireman unit ID in `EDX`. It returns a 1-based building ID or zero. Its only three direct callers are in `FUN_180159C30` at RVAs `0x159DF8`, `0x159F4C`, and `0x15A08B`.

The selector scans active buildings in ascending ID order. A candidate must be active, belong to the same diplomacy group as the fireman, have nonzero `r_OnFireTicks`, be within Manhattan distance 999, and have a reachable destination PCL. The native selector addresses the counter as `manager + buildingId * 0x32C + 0x31A`. The Script Extender's zero-based `GameBuilding` view starts `0x5C` later and therefore exposes the same field at struct offset `0x2BE`. The strictly closest accepted candidate wins; equal-distance candidates preserve the earlier building ID. Vanilla has no reservation or worker-ownership check.

## Fireman state flow

`FUN_180159C30` (`RVA 0x159C30`) is the `CHIMP_TYPE_FIREMAN` update used by workers from both wells and water pots. States 1 and 2 search for a fire. State 3 validates and walks toward the target, reselecting when its slot/global identity or fire state becomes invalid. State 4 performs the extinguishing animation. The target building ID and global ID are stored at unit offsets `0x39A` and `0x39C`.

## Extinguishing and linked buildings

`FUN_1800C3C80` (`RVA 0xC3C80`) changes a nonzero fire counter directly to zero and sets the building cooldown at `0x320` to 2000. One completed water throw therefore extinguishes an ordinary target completely.

After the direct target is extinguished, `FUN_180159C30` reads the 32-bit building compound key through the manager-indexed offset `0x304`, corresponding to `GameBuilding` offset `0x2A8` after the same `0x5C` base translation. For a nonzero key it scans every live building and invokes the same extinguish routine on every part with the identical key. Different burning parts of one compound are therefore one logical fire target.

`FUN_1800C4D30` (`RVA 0xC4D30`) starts an eligible fire by setting the counter to one. The building update `FUN_1800C60F0` (`RVA 0xC60F0`) increments active fire counters and contains independent terminal paths, so reservations must revalidate the target rather than assume only a fireman can end a fire.

## Detour contract

The target-selector entry starts with five complete instructions totaling 15 bytes before RVA `0xB8F4F`. The installed RedBird backend requires at least 14 displaced bytes and therefore displaces this 15-byte range. No direct branch enters the open interval. A detour must retain the original ABI `(GameBuildingManager*, int unitId) -> int buildingId` and call the trampoline for Vanilla distance, diplomacy, and reachability behavior.
