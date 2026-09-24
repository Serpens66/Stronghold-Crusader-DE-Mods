# Worker Break Parity Test: native update contract

The owner feature is the post-delivery transition in the miller and baker unit updates. The reference is the installed `CrusaderDE.dll` with SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. The mod does not activate on another hash.

| Worker | Owner update RVA | Branch RVA | Original | Replacement | Existing Vanilla target |
| --- | ---: | ---: | --- | --- | ---: |
| Miller | `0x1377C0` | `0x1383F3` | `74 72` | `EB 72` | `0x138467` |
| Baker | `0x138850` | `0x139596` | `0F 84 B5 00 00 00` | `E9 B6 00 00 00 90` | `0x139651` |

Each branch follows the completed output delivery. The original conditional branch selects Vanilla's fallback when no next wheat or flour source is found. The replacement selects that same fallback after every completed delivery. The miller fallback finalizes the work interval at `0x138499`; the baker has finalized it at `0x13955D`. Both fallbacks lead to state 2, where Vanilla's `0x191050` / `0x191070` pause gate runs after its normal delay. State `0x79` visits a positive fear building through `0x185430` / `0xBF0A0` and restores the saved worker state afterward.

At the matching hash, validate the bytes at the reference RVA first. If they differ, search the executable `.text` range `0x1000..0x20A1FF` for each unique anchored signature (`85 C0 74 72 8B 15` for the miller; `85 C0 0F 84 B5 00 00 00 48 69 FD 2C 03 00 00` for the baker), derive the branch at offset 2 and require the same owner function and reference RVA. Missing, ambiguous, or altered bytes disable the whole test feature. The RedBird patch transaction commits both replacements together. No published patch is removed during map or Unity component cleanup.

For a new DLL: audit the post-delivery branches, work rollup, state-2 return, pause gate, and fear-building visit anew. Recalculate branch targets and bytes, retest the unique signatures, update the reference hash and supported game version, and check the Script Extender and Fixes mod for overlapping patches.
