# Civilian worker breaks after production

Reference native library SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Evidence is the installed `CrusaderDE.dll`, the semantic decompiler export, and the unit-update function table at image file offset `0x3202B0` (pointer index equals the Script Extender `UnitFunctionsVTable` field index). These are native unit-slot indices, not 1-based game unit IDs.

## Common pause path

- `0x193A70(unit,1)` closes a measured work interval, computes the recent average in the unit record, advances the rolling slot, and clears the next slot.
- `0x191050` wraps `0x191070(unit,currentState)`. The latter requires rest flag 1, owner work rate below 100, and average interval above 100; when due, it stores the return state and changes the worker to state `0x79`.
- `0x185430` processes state `0x79`, asks `0xBF0A0` for an eligible positive-fear building and uses the usual unit route builder `0x196280`; it eventually restores the saved worker state. Failure to find a reachable destination returns to work.

## Unit-update comparison

| Unit type | Update RVA | Break opportunity at completed output |
| --- | ---: | --- |
| Woodcutter | `0x12BC00` | Returns to the pause-checking worker state. |
| Fletcher | `0x12D230` | Direct `0x191070(unit,3)` after rollup, before optional improved wood collection. |
| Hunter | `0x12FC70` | Returns to its pause-checking state. |
| Pitchman | `0x1332B0` | Returns to its pause-checking state. |
| Wheat farmer | `0x133DD0` | Calls the pause gate in state 2; a valid farm and successful route after stockpile delivery can instead return directly to field work. |
| Hops farmer | `0x134F00` | Calls the pause gate in state 2; a valid farm and successful route after stockpile delivery can instead return directly to field work. |
| Apple farmer | `0x135E30` | After stockpile delivery, closes the work interval and enters state 5 with pause-check flag 1; state 5 calls `0x191050`. |
| Cattle farmer | `0x136D40` | After stockpile delivery, closes the work interval and enters state 2 with pause-check flag 1; state 2 calls `0x191050`. |
| Miller (all three mill workers) | `0x1377C0` | State 2 and work rollup occur only on the branch where the next wheat source is unavailable; with wheat, the unit routes directly to state 3. |
| Baker | `0x138850` | Work rollup occurs after bread delivery, but with flour available the unit routes directly to state 7 instead of its state-2 pause gate. |
| Brewer | `0x139950` | Direct `0x191070(unit,4)` after rollup, before the next ingredient choice. |
| Poleturner, blacksmith, armourer, tanner | `0x13AAD0`, `0x13BDD0`, `0x13CF30`, `0x13E0B0` | Direct `0x191070(unit,4)` at cycle completion. |

Quarry workers and other jobs carried out without leaving their work building are excluded from the baker/miller parity fix. The wheat and hops continuation branches remain Vanilla behavior. Crop aging gives a plausible reason for prioritizing the return to the farm, but does not establish developer intent.

## Wheat and hops farmer continuation details

- Both farmers call the common pause gate from state 2: wheat (decompiler lines 178007-178018), hops (lines 178546-178557). Each call is conditional on a local delay/flag; entering the state alone does not guarantee a check or a visit. The hops function ends at decompiler line 178927; the state-5 pause call at lines 179099-179110 belongs to the apple farmer.
- Wheat: at the end of the field phase (state 7, lines 178188-178226), a valid assigned farm of the expected building types sends the worker directly into the next field/return phase; otherwise it sends the worker to state 2. More importantly, after depositing wheat at the stockpile (state 10, lines 178352-178433), a valid assigned farm and successful route send the worker directly back to farm state 6/8, bypassing both work-interval rollup and state 2. The alternative calls `0x193A70(unit,1)` and enters state 2.
- Hops: after the field phase (state 6, lines 178646-178699), a valid assigned farm sends the worker directly to state 5; otherwise it enters state 2. After depositing hops at the stockpile (state 8, lines 178821-178905), a valid assigned farm and successful route send the worker directly to farm state 5, bypassing the work-interval rollup and state 2; the alternative rolls up and enters state 2. There is no separate pause call in hops state 5.
- Therefore continued field work by itself is not an exact no-pause condition. The relevant branch tests the assigned farm's validity/type and, on the post-stockpile route, path creation. This is a static control-flow finding; the frequency of actual breaks in uninterrupted gameplay has not been measured.
- The wheat farmer's completed stockpile delivery reaches the farm validity test at RVA `0x134B80` (`0F 84 CD 00 00 00`, `je 0x134C53`); the fallback begins at `0x134C53`, rolls up the work interval at `0x134C5E`, then enters state 2 with pause-check flag 1. The hops farmer has the analogous branch at RVA `0x135AE2` (`0F 84 97 00 00 00`, `je 0x135B7F`); its fallback rolls up at `0x135B8B` and enters state 2 with pause-check flag 1. The test mod no longer replaces either farmer branch.
- The apple farmer's stockpile handoff is in state 7 (decompiler lines 179175-179247): after the last carried good, it calls `0x193A70(unit,1)`, enters state 5 and sets the check flag to 1; state 5 calls `0x191050` when that flag is set (lines 179099-179110). The cattle farmer's corresponding handoff (lines 179645-179670) calls `0x193A70(unit,1)`, enters state 2 and sets the check flag to 1; state 2 invokes `0x191050` (lines 179506-179513). Neither needs this parity patch.

## Crop aging and harvest pressure

- The farm building update table at RVA `0x2DEAE0` maps wheat to `0xA36D0` and hops to `0xA3810`. Wheat advances after its update counter exceeds 150, via `0xC8840` (decompiler lines 94775-94813 and 115331-115535). Wheat tile stages advance up to `0x67`; the building's productive-stage count no longer includes late unharvested stages. The worker's state 4 replants tiles at stage 1 (lines 178073-178110), while state 7 harvests productive tiles (lines 178188-178226). Thus leaving wheat unharvested can lose its current yield even though the tile need not vanish.
- Hops advances after its update counter exceeds 400, via `0xC8110` (lines 94820-94858 and 114901-115075). Unharvested stages progress through `0x1F`, then `0x20`, then clear to 0 on a later update. Productive stages `0x0E..0x1B` contribute to the building count; worker state 4 replants at stage 2 and state 6 harvests (lines 178601-178699). Thus unharvested hops can disappear and lose yield.
- These timing and state transitions support a gameplay rationale for promptly returning to the farm after delivery. They do not prove that the developers consciously designed the pause bypass for this reason. The farmer branch condition itself checks farm validity and routing, not an explicit spoilage timer or a direct "still harvestable" condition.

## Audited baker and miller branches

- Miller branch RVA `0x1383F3`: `74 72` jumps to `0x138467` only on missing next wheat. At `0x138499` the fallback closes the work interval, then state 2 is written at `0x1384A5`. Replacing only the conditional jump with `EB 72` selects this existing fallback after every completed flour delivery.
- Baker branch RVA `0x139596`: `0F 84 B5 00 00 00` jumps to `0x139651` only on missing next flour. The interval was closed at `0x13955D`; the fallback routes to state 2. Replacing the conditional jump with `E9 B6 00 00 00 90` selects the same existing fallback after every completed bread delivery.

The optional `BugfixesAndQoL` host setting now uses permanent, conditional RedBird inline hooks at these branches. The installed backend displaces 18 complete bytes at each site: miller `0x1383F3..0x138404` (return `0x138405`) and baker `0x139596..0x1395A7` (return `0x1395A8`). The disabled gateway replays the original conditional branch and following instructions; the enabled gateway jumps to the existing Vanilla targets above. The installed DLL's xref export has no external incoming references into either displaced span. Each hook is hash- and byte-bound; the synchronized host setting changes an atomically written data flag, not executable bytes.

These are hash-bound observations. New DLLs require re-auditing the caller and callee chains, target addresses, bytes and downstream states before reuse.
