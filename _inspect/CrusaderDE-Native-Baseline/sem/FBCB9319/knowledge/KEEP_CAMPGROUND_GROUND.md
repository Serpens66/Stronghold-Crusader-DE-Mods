# Keep and campground ground: verified records

## Provenance

- Installed `CrusaderDE.dll` SHA-256 on 2026-09-26: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Installed `Assembly-CSharp.dll` SHA-256 on 2026-09-26: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
- The runtime observations below come from the BepInEx `LogOutput.log` entries dated 2026-09-26. Counts describe those recorded runs, not every map or terrain type.

## Native and Extender paths

- The installed Script Extender's `c_game_building_spawn` detour raises `OnBuildingSpawn` before and after its original native call. The higher-level `OnBuildStructure` event precedes building spawn in the Extender source.
- In the installed native build, RVA `0x6D580` calls RVA `0x76E80` in its type-`10` switch case. RVA `0x74DA0` calls the building allocator at RVA `0xB47E0` with structure type `0x37` and also calls `0x76E80`.
- RVA `0x76E80` creates four building records through `0xB47E0`. Its first loop writes structure occupancy at tile-manager offset `0xB0BCA0` and sets logic bits `0x502`. Its second loop visits nine offsets from `DAT_1802D3060`, sets logic bits `0x102`, writes the last created record ID to AlphaGFX at offset `0x279D80`, updates tile fields, and marks those cells changed.
- The visual function at RVA `0x6E620` writes packed graphic IDs to GFX at offset `0x140900`; selected branches also write AlphaGFX at offset `0x279D80`. Its generic-building GFX store starts at RVA `0x6F0A0`; the next record read starts at `0x6F0A8`, the corresponding AlphaGFX store is at `0x6F0D0`, and the shared loop tail begins at `0x6F0D8`. The record type word at image-relative offset `0x64CCCDE` distinguishes campground type `0x37` in this branch.
- RVA `0x5BC70` initializes 320,800 GFX cells at offset `0x140900` and then calls terrain functions `0x65830` and `0x650C0` in that order. The simulation tick at RVA `0xCDE60` also calls `0x65830` before `0x650C0`. RVA `0x65830` contains GFX-zeroing stores at `0x68640`, `0x68FBA`, and `0x6901F`. RVA `0x650C0` tests for a zero GFX cell and eligible logic before filling it.
- A branch in `0x65830` calls `0x6BDD0`, writes a related value at tile-manager offset `0x625B00`, and writes GFX values in the `0x000C0060..0x000C006F` range according to tile fields. The native branch at `0x69011..0x6902C` tests logic bit 15, writes zero to GFX at `0x6901F`, and continues at its loop tail.
- The deletion path `0xB8310 -> 0x61FC0 -> 0x628F0` clears structure occupancy in the latter function.

## Recorded graphics observations

- Archived Vanilla probe logs (`Log_140.log` and `Log_141.log`, 2026-09-26) record campground cells changing from file-`0x37` ground graphics to file-`0x06` graphics, including image IDs `0x18` and `0x45`. A user-provided Vanilla screenshot before peasant arrival shows the stone fireplace without cauldron or flames.
- The 2026-09-26 21:45:30 test log records nine captured foreground image IDs in this order: `0x3C,0x37,0x31,0x36,0x30,0x2A,0x2F,0x29,0x24`. The installed images contain stone-ring and dirt pixels together. The same test log records all 49 campground structure cells occupied while the ground-preservation hook is active.
- In a later 21x21 Keep sample, all 339 free cells whose GFX became zero during `0x65830` were hit at the instrumented `0x6901D` site. The instrumented `0x68638` and `0x68FB6` sites had no hit in that sample.
- In the run logged at 2026-09-26 22:38, the first Keep sample suppressed 348 original-ground writes at `0x6901D`; a second suppressed 347. Each run recorded eight structure-free cells whose AlphaGFX changed from zero before the first recalc and whose GFX became `0x000C006x` during recalc.
- In the run logged at 2026-09-26 23:14:13, eight structure-free cells changed AlphaGFX from zero to `0x26` while their GFX stayed unchanged before recalc. Those eight cells changed from file-`0x37` ground graphics to `0x000C006x` during recalc. The log recorded 348 suppressed original-ground writes at `0x6901D` and no suppressed writes for those eight cells. The campground summary reported `valid=49`, `occupied=49`, and `originalGraphicIntact=49` with the test hook active.
