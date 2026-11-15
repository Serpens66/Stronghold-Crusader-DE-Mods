# Keep direction versus sprite variation [Scope: Vanilla + mod integration]

## Confirmed storage contract

Reaudited on 2026-11-15 against the installed canonical `CrusaderDE.dll` and
matching semantic function records, SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The two fields below are different storage and must not be treated as aliases.

| Meaning for root Keeps | GameBuilding offset | Public Extender field | Constructor value |
| --- | ---: | --- | --- |
| Actual Keep direction | `+0x92`, UInt16 | `N00012042_2` | Effective seventh argument of `0x74DA0` |
| Sprite variation | `+0x102`, UInt16 | `r_SpriteVariationIndex` | Automatic-sprite sentinel `15` |

Direction uses `0=South`, `2=East`, `4=North`, `6=West`; raw `15` takes the
default South geometry branch. Normalize `15` to `0` for cardinal calculations,
but do not infer direction from a sprite value of `15`. Camera/view rotation is
a third, independent input; see [the flag audit](KEEP_FLAG_ROTATION.md).
This semantic contract is scoped to the five root Keep types, not every building.

## Constructor, allocator and independent reader

All addresses below are RVAs in the pinned image; confidence is confirmed-static.

- Human start `0x94350` forwards the Keep argument through BuildStructure
  `0x6D580` to the common Keep constructor `0x74DA0` for Keep types 1 through 5.
  A constructor detour such as Fixes can replace the argument; the resulting
  record is the authority for the actual stored direction.
- At `0x74E2B`, the constructor supplies `15` as allocator argument 9; the call
  to `0xB47E0` is at `0x74E92`. Allocator `0xB4AB5` writes the variation using
  `mov word ptr [r8+r14+0x15e],dx`. Keep types do not take the allocator branches
  that reinterpret this argument for other building families.
- At `0x74EC5`, the constructor resolves building manager `0x64CCBB0`.
  `0x74ECC` loads its effective seventh argument into `r14d`; `0x74ED4` writes
  `r14w` to manager plus building-ID stride plus `0xEE`. This happens after root
  allocation and before player Keep registration through `0xD0630`. The Keep's
  child records receive the same direction separately.
- Independent consumer `0xD0630`, instruction `0xD069D`, reads that same
  manager-relative `+0xEE` field to calculate orientation-dependent Keep access
  coordinates. Its default branch covers `15` as South; `2/4/6` select the other
  cardinal branches. This read confirms the meaning independently of the writer.

The manager's record prefix is `0x5C`; a building record has stride `0x32C` and
native IDs are one-based, with slot zero reserved. For the same native ID:

- Direction global `0x64CCC9E` minus manager `0x64CCBB0` is `0xEE`;
  subtract record prefix `0x5C` to obtain **record `+0x92`**.
- Allocator variation displacement `0x15E` minus prefix `0x5C` is
  **record `+0x102`**.

These calculations also agree with the public, packed Extender `GameBuilding`
declarations. A field name or a successful managed build alone is not the native
proof. Recheck both writer and independent reader, record origin, width, stride,
sentinel and exact installed interop layout on an update; never transfer these
offsets to an unknown image merely because a nearby function pattern matches.

## Consumer correction and regression history [mod integration]

The stored-direction paragraph added to `KEEP_FLAG_ROTATION.md` and
`CastlePlanner/UpdateToNewDLL.md` on 2026-10-27 incorrectly identified the
direction field as `r_SpriteVariationIndex`. The live documents are corrected;
historical Git objects and analysis exports remain unchanged.

CastlePlanner source commit `2cd3fddf9ed0c6a16e8f36e0a19f3e008c7b1b55`
(2026-10-27 00:48:36 CET) introduced both the wrong-field reader and the
Keep(Post) direction check before AIV execution. A real East Keep therefore has
direction `2` and sprite `15`, while the faulty reader reports South `0` and
aborts the prepared East castle. The same reader also supplies inherited
preview labels and the player Blueprint direction fallback; correcting only
the abort check would leave those consumers wrong.

The first subsequent tracked DLL containing the failure-message literal is
commit `fc3076f05b520ab2038f4392c4d69feeaff565e7` (2026-10-28 23:55:26 CET),
whose source and package metadata identify CastlePlanner `0.8.47`. The DLL in
the source-introduction commit and its parent lacks that literal. This was
verified read-only from Git blob bytes; it dates the recorded binary, not the
first Steam publication or a user's installation. Earlier working rotations
are consistent with the pre-check implementation, which did not abort here.

Consumers must keep owner, root-Keep type, identity and life-state validation,
then read direction independently of sprite variation. Unsupported direction
values remain errors; removing the check or forcing a layout to South conceals
the defect. A general editor Keep scan must not be replaced by a mandatory
player-resource Keep-ID lookup without verifying the editor replacement path.
BugfixesAndQoL's Keep-flag late scan has the same field distinction to observe.

The exported native-field catalog is a pinned generated snapshot and has no
Keep-direction entry in the audited snapshot. It is not amended as an ad hoc
second runtime catalog here; this document and the CURRENT index are the
discoverable native evidence until a coordinated catalog regeneration.

Runtime confirmation on 2026-11-15: the real East-facing root Keep retained
direction `2` at `+0x92` and sprite variation `15` at `+0x102`, both with and
without Fixes. The corrected CastlePlanner reader agreed with the native field;
the selected castle spawned. Nine successful local game runs covered all four
rotations, Keep-only rotation, inherited East, no Fixes, save/reload and an East
Keep in the editor. Save/reload retained the castle and recovered a 90-degree
Blueprint. Compact run records and original BepInEx evidence are in the local
workspace `.work/a/castle-direction/001/result.json`; this is dated integration
evidence, not an expanded contract for other building families or unknown DLLs.
