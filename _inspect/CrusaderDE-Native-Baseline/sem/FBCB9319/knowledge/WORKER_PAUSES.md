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
| Wheat farmer | `0x133DD0` | State 2 is the normal return; an immediate field-continuation route bypasses this opportunity. |
| Hops farmer | `0x134F00` | State 2 is the normal return; an immediate field-continuation route bypasses this opportunity. |
| Apple farmer | `0x135E30` | Returns to its pause-checking state. |
| Cattle farmer | `0x136D40` | Returns to state 2. |
| Miller (all three mill workers) | `0x1377C0` | State 2 and work rollup occur only on the branch where the next wheat source is unavailable; with wheat, the unit routes directly to state 3. |
| Baker | `0x138850` | Work rollup occurs after bread delivery, but with flour available the unit routes directly to state 7 instead of its state-2 pause gate. |
| Brewer | `0x139950` | Direct `0x191070(unit,4)` after rollup, before the next ingredient choice. |
| Poleturner, blacksmith, armourer, tanner | `0x13AAD0`, `0x13BDD0`, `0x13CF30`, `0x13E0B0` | Direct `0x191070(unit,4)` at cycle completion. |

Quarry workers and other jobs carried out without leaving their work building are excluded from the baker/miller parity fix. The wheat and hops continuation branches are separate observations; they have not been changed or proven to have the same gameplay impact.

## Audited baker and miller branches

- Miller branch RVA `0x1383F3`: `74 72` jumps to `0x138467` only on missing next wheat. At `0x138499` the fallback closes the work interval, then state 2 is written at `0x1384A5`. Replacing only the conditional jump with `EB 72` selects this existing fallback after every completed flour delivery.
- Baker branch RVA `0x139596`: `0F 84 B5 00 00 00` jumps to `0x139651` only on missing next flour. The interval was closed at `0x13955D`; the fallback routes to state 2. Replacing the conditional jump with `E9 B6 00 00 00 90` selects the same existing fallback after every completed bread delivery.

These are hash-bound observations. New DLLs require re-auditing the caller and callee chains, target addresses, bytes and downstream states before reuse.
