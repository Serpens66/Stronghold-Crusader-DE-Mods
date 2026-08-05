# Updating Active AIV Detector for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

The detector writes a timestamped Error and installs no native hooks for every
other DLL.

## What must be checked after an update

1. Confirm every AOB in `ActiveAIVDetectionRuntime` and `AivPlacementOracle`
   has exactly one match and still represents the same function and ABI.
2. Revalidate the RIP-relative organism-record and active-layout-index
   references and their resolved image bounds.
3. Revalidate the AIV spec stride `0x6D98` and fields `+0x04`, `+0x0C`, `+0x10`,
   `+0x14`, `+0x28`, `+0x2C`, `+0x30`, and `+0x34`.
4. Revalidate placement-state grids and counters at `+0x3DA6C`, `+0x4288C`,
   `+0x5B4F8`, `+0x5B4FC`, and `+0x1B9844`.
5. Revalidate map grids at `+0x898400`, `+0xA6F260`, `+0xB0BCA0`, `+0xBF6C00`,
   `+0xD7E5A0`, `+0xDCCAC0`, and `+0xE1AFE0`.
6. Revalidate organism stride `0x9C`/class `+0x46`, player-state stride
   `0x583C`, and prepared-entry layout `+0x38` with entry size `0x0C`.
7. Run the cell and prebuild traces on known maps and compare their results with
   Vanilla before accepting the new hash in the shared version check.

Matching function signatures alone are not enough to approve changed structure
offsets.
