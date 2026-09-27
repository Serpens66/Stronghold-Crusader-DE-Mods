# Vanilla health-bar rendering (FBCB9319)

This static audit applies to `CrusaderDE.dll` SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Runtime behavior of the proposed mod is not yet tested.

The map render loop at RVA `0x86680` calls the visible-map renderer at `0x41D60`. That path reaches the building render helpers at `0x488B0` and `0x4C1D0` and the unit packet helper at `0x1A13C0`. The helpers work on Vanilla-visible objects. They emit health-bar data in native map records; `GameMap.processTestMap` consumes those records. Ctrl+H changes health-bar drawing order, independently of the selection and health gates below.

| Render path | Selected-object gate and whole-instruction span | Health calculation path | HP storage |
| --- | --- | --- | --- |
| Building helper `0x488B0` | `0x489C4..0x489D5` (17 bytes) | `0x489F5` | Building stride `0x32C`, current signed word at image base `+0x64CCD18+id*0x32C`, max unsigned word at `+0x64CCD1A+id*0x32C` |
| Building helper `0x4C1D0` | `0x4F9B4..0x4F9C5` (17 bytes) | `0x4F9E4` | Same building manager storage |
| Unit packet helper `0x1A13C0` | `0x1A1945..0x1A195D` (24 bytes) | `0x1A1971` | Unit stride `0x490`, current dword at image base `+0x67E8E20+index`, max dword at `+0x67E8E24+index` |

Each building gate first tests gameplay mode `0x10`, then selected building ID. The normal continuation also handles Vanilla hover behavior. The unit gate compares selection or hover fields, then branches to additional native suppression tests at `0x1A1971` before it calculates a bar sprite from the cached health percentage at image base `+0x67E8A90+index`. That percentage is refreshed by the native damage and healing paths from current and maximum HP. The unit supplemental record has type 51; its word at byte offset `0x14` is consumed as `hpsFrame` by `GameMap.processTestMap`. The unit HP fields correspond to unsigned `GameUnit` offsets `0x3C4` and `0x3C8`; building HP corresponds to `GameBuilding` offsets `0x10C` and `0x10E`. The two building render loops use different base/index registers, so their effective addresses must be checked separately.

The audited hook spans have no incoming control-flow edge into an instruction interior in the current Ghidra reference export. The installed RedBird `X64InlineHook` treats `HookSize` as a minimum of 14 bytes and rounds to complete instructions. Candidate hooks must validate the actual displaced lengths of 17, 17 and 24 bytes, preserve Vanilla branch targets and verify the generated stubs before publication. The planned active path bypasses only the selection gate when `0 < current HP < max HP`; the disabled path replays all displaced instructions. No in-game result is claimed here.
