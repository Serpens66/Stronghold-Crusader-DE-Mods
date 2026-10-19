# Script Extender 2.14.3 interop additions

Recorded on 2026-10-19 (Europe/Berlin). This is an additive source-knowledge supplement to the existing baseline, not a regenerated baseline or an installed-binary/runtime acceptance record.

## Provenance and evidence boundary

- Reviewed upstream release: `v2.14.3`, commit `4014ca67c96974a5e6d697665baf5351cbfebde6`, Git tree `372b43a9e9d614075c3b827d3df2a2346ec356fa`.
- Comparison source / existing generated baseline provenance: `v2.14.2`, commit `10628df85cd432184742a50a1dee3969a66cf89e`, tree `a612b537010c88e2837bb8895c9657e730365423`.
- Canonical installed `CrusaderDE.dll` SHA-256, rechecked on the recording date: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; matches `CURRENT.json` and the existing semantic identity.
- Installed `SHCDESE.dll` at observation time: file version `2.14.2.0`, SHA-256 `577F9AAB5DE504BE0BFA2193C7DEB734B26076ECBBAF0A408FC9ABC4CC5E7175`. The 2.14.3 source contracts below are not claimed to exist in this installed assembly.
- Evidence: the complete `v2.14.2..v2.14.3` diff, specifically `src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs`, `GameUnit.cs` and `GameTribe.cs`; corresponding player-resource header rename in `ReverseEngineering/structs/Custom.h` and `PlayerResources.h`.

Field names, meanings, types and offsets below are upstream source findings. Offsets are relative to the respective SE interop struct, not automatically to a native manager record. No new independent native data-flow audit, 2.14.3 assembly reflection, or runtime validation was performed. In particular, an upstream semantic name is not proof of its full Vanilla behavior.

## Renamed fields

| Struct | Previous field | 2.14.3 field | Type (unchanged) | Struct-relative offset |
|---|---|---|---|---|
| `GamePlayerResources` | `N00004115` | `r_AILastStandPhase` | `UInt32` | `0x38F0` |
| `GameUnit` | `N000001A6` | `r_CanAutoEngageInMelee` | `UInt16` | `0x02CC` |
| `GameUnit` | `N000001C2` | `r_AttackingUnitGlobalId` | `UInt32` | `0x033C` |

The upstream comment describes `r_AILastStandPhase` as inactive at 0, entering at 8 and falling toward 1 while the threat persists. This is an upstream interpretation, not a newly confirmed native state-machine contract. Both new `GameUnit` names carry `[LuaExposed]`; `r_AILastStandPhase` does not gain that attribute in this release. The player-resource rename is also present in the two reverse-engineering headers mentioned above.

## GameTribe ranged-attacker fields

All ten named fields remain `[LuaExposed]`, change from `UInt16` to `UInt32`, and start two bytes earlier. Each new four-byte field occupies the former two-byte `unk` field plus the following two-byte named field; the source layout change does not enlarge this region or shift the following fields.

| Field | 2.14.2 offset (`UInt16`) | 2.14.3 offset (`UInt32`) | Removed `UInt16` field (old offset) |
|---|---|---|---|
| `r_LastRangedAttackerGlobalId10` | `0x062A` | `0x0628` | `unk10` (`0x0628`) |
| `r_LastRangedAttackerGlobalId1` | `0x062E` | `0x062C` | `unk1` (`0x062C`) |
| `r_LastRangedAttackerGlobalId2` | `0x0632` | `0x0630` | `unk2` (`0x0630`) |
| `r_LastRangedAttackerGlobalId3` | `0x0636` | `0x0634` | `unk3` (`0x0634`) |
| `r_LastRangedAttackerGlobalId4` | `0x063A` | `0x0638` | `unk4` (`0x0638`) |
| `r_LastRangedAttackerGlobalId5` | `0x063E` | `0x063C` | `unk5` (`0x063C`) |
| `r_LastRangedAttackerGlobalId6` | `0x0642` | `0x0640` | `unk6` (`0x0640`) |
| `r_LastRangedAttackerGlobalId7` | `0x0646` | `0x0644` | `unk7` (`0x0644`) |
| `r_LastRangedAttackerGlobalId8` | `0x064A` | `0x0648` | `unk8` (`0x0648`) |
| `r_LastRangedAttackerGlobalId9` | `0x064E` | `0x064C` | `unk9` (`0x064C`) |

The declared `GameTribe` layout remains sequential, pack 1, size 1672 (`0x688`). The following `N00000586` field remains at `0x0650`. These are source-layout observations; the meaning and runtime ordering of the ten attacker slots were not independently re-audited.

## Relationship to the generated baseline

`IDENTITY.json`, `SEMANTIC_INFO.md`, `DATABASE_INFO.json`, the SQLite index, generated source/header knowledge, Ghidra projects and exports retain their previous generation provenance. Their absence of the new names must not be interpreted as evidence against this later source supplement. Use this note alongside those records; it does not assert that the generated database or Ghidra types have been updated to 2.14.3.

No mod source, version, dependency minimum, build output or installed file was changed. No extender/mod build, installation, game start, database rebuild or Ghidra export was performed for this supplement.
