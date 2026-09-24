# Vanilla worker pause audit

Reference: installed `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Unit update mapping comes from Vanilla's function table and the Script Extender `UnitFunctionsVTable` layout.

The common pause gate at `0x191070` requires the worker rest flag, player work rate below 100, and a sufficiently long measured work interval. State `0x79` then searches for a valid positive-fear building and later resumes the saved state. Pauses are conditional; reaching the check does not guarantee a visit.

| Worker | Update RVA | Post-production pause route |
| --- | ---: | --- |
| Woodcutter | `0x12BC00` | Returns to the pause-checking state. |
| Fletcher | `0x12D230` | Calls `0x191070` directly before optional improved raw-material collection. |
| Hunter | `0x12FC70` | Returns to the pause-checking state. |
| Pitchman | `0x1332B0` | Returns to the pause-checking state. |
| Wheat farmer | `0x133DD0` | Usually returns to state 2; a direct field continuation can bypass this pause opportunity. |
| Hops farmer | `0x134F00` | Usually returns to state 2; a direct field continuation can bypass this pause opportunity. |
| Apple farmer | `0x135E30` | Returns to its pause-checking state. |
| Cattle farmer | `0x136D40` | Returns to state 2. |
| Miller | `0x1377C0` | Reaches state 2 only when the next wheat source is unavailable; all three mill workers use this update. |
| Baker | `0x138850` | Reaches state 2 only when the next flour source is unavailable. |
| Brewer | `0x139950` | Calls `0x191070` directly before the next ingredient choice. |
| Poleturner, blacksmith, armourer, tanner | `0x13AAD0..0x13E0B0` | Call `0x191070` directly at cycle completion. |

Quarry and similar workers whose assigned work keeps them inside their building are excluded from the correction. Wheat and hops field-continuation paths are recorded for a separate decision; this test mod changes only millers and bakers. The installed Script Extender already has pre-delivery event hooks for baker and miller goods transfers. Those events occur before Vanilla finishes the current work transition and cannot by themselves substitute for the post-delivery pause branch. The local Fixes mod's unit-update hooks target wildlife, and its pathfinding changes do not occupy either branch patched here.
