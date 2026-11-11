# Siege engineer crew timeout [Scope: Vanilla]

Native image SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. RVAs below were verified against the executable sections of that image.

The record anchor is the unit manager plus the one-based unit ID multiplied by the native stride 0x490. For an engineer, the signed dword at anchor +0x974 (GameUnit +0x318) counts time spent bound without a crew refresh. A siege engine uses the same storage in its own record for the first crew global ID. The work/animation counter at anchor +0x908 is separate.

- The unit update at 0x182B00 increments the timeout at 0x1832C7 for engineer type 0x1E in main state 5. The siege-unit pass at 0x1839C0 clears it when the engine participates in the pass, the crew slot's global ID matches and the engineer is in state 5.
- The engineer handler at 0x14AED0 compares the timeout to 120 at 0x14C3E0. The greater-than path clears work, sets packed state 0x0000006D and visual word 0xFE20, then makes the engineer visible again. The transition runs to main state 0. The expired timeout survives this release.
- Full-crew completion in the building handlers for Arab ballista (0xA65A0), catapult (0xA8E40) and trebuchet (0xA93C0) writes main state 5 to each engineer at 0xA68EF, 0xA916A and 0xA97F6 respectively. These loops clear the low visual byte but omit timeout initialization. The completed engine takes the same one-based crew IDs and global IDs from the tent. Failed spawn and incomplete crew take separate paths.
- A stale timeout above 120 can therefore release the engineer again before a matching crew refresh, leaving the engine's crew entry intact. Explicit dismount in action 0x11 of 0x11E960 clears the device's crew slots and uses the referenced identities, which explains the visible unit count after dismount.

Observed on 2026-09-29 in BepInEx/logs/Log_185.log. Device 254/3075 left siege scope at tick 2687, and engineers 1/2927, 170/2945 and 173/2948 went from bound with counter 120 at tick 2807 to release with counter 121 at tick 2808. Their free records still held 121. Device 305/4575 received all three matching crew pairs at tick 3390 and all three engineers were in 0x0000006D while the device retained them at tick 3391. The test observer's detailed engineer transition budget had ended before that second completion, so its logs do not contain a fresh counter sample from tick 3391. See the semantic decompiler export for the timeout increment, reset, threshold and full-crew writes.


## Fatal fire damage [Vanilla facts]

A fire tile calls `0x9E030` for the units linked to that tile. Its caller passes the fire owner and reduced-damage flag to the per-unit routine at `0x192400`. Nonlethal damage returns before conversion. For the eight siege unit types the fatal branch at `0x1925DB` sets pending state 0 and life state 4. The switch at `0x192612` chooses pending type 2 for each. The unit pass at `0x182B00` consumes life state 4 via `0x195D10` and then dispatches to the burning-man handler at `0x12B980`. It no longer executes the old siege handler or refreshes the original crew. The captured release at ticks 2808 and 2841 is the existing engineers' timeout and exit transition.

Ordinary siege death uses the unit handlers listed below. Each checks the crew global IDs against the recorded unit slots and marks the matching engineers for deletion in its normal death branch. The dismissal path sets the marker at record anchor `+0xA48` via `0x186D10` and has separate crew handling. The unit pass consumes `MarkedForDeletion` via `0x1869C0`, which clears the unit and associated path state. Manual dismount uses action `0x11` in `0x11E960` and clears the device's crew entries.

| Device | Type | Handler RVA | Crew capacity |
| --- | --- | --- | --- |
| Catapult | 0x27 | 0x1520D0 | 2 |
| Trebuchet | 0x28 | 0x1535F0 | 3 |
| Mangonel | 0x29 | 0x1547F0 | 2 |
| Siege tower | 0x3A | 0x15ECE0 | 4 |
| Battering ram | 0x3B | 0x160420 | 4 |
| Portable shield | 0x3C | 0x161220 | 1 |
| Ballista | 0x3D | 0x161CB0 | 2 |
| Arab ballista | 0x4D | 0x171C50 | 2 |

The tower's native deployment has an alternate branch that releases its engineers and clears the crew count. The deployed building proxy type 0x45 is rejected by the Vanilla building ignition check at `0xC1F60` called from `0xC4D30`. The unit and building are distinct records.
