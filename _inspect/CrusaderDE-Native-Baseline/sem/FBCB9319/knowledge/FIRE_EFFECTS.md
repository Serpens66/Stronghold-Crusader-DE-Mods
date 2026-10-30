# Fire on the ground: effect records, pitch ignition and unit route storage [Scope: Vanilla + mod integration]

Native DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (installed DLL, matches `CURRENT.json`). RVAs are for this binary only. Sources: semantic-baseline pseudocode (2026-10-29), machine bytes of `0xA2E60` and `0xA02D0` read from the installed DLL, and runtime counters of the MoatMove fire runs `pf-fire-2` and `pf-fire-3` on `test_stresstest4.sav` (2026-10-29). Function names are candidate-level.

## Effect manager and fire records

- The effect manager is a static array at `0x2F7A680` of `0xE8`-byte records, slots 1..5999. `0xA2D80` relinks slots `0x19..5999`; `0xA2E60` purges slots 1..`0x18` whose state is 2.
- Record fields: `+0x3C` state (`0xA2D80` links only records with a nonzero state; `0xA2E60` unlinks state 2), `+0x3E` effect type, `+0x60` native tile id (int), `+0x32` next record on the same tile (`0xA02D0`, `0xA0280`), `+0x38` previous record (`0xA2D80`).
- Per-tile list heads: `0x4CF23F0[tileId]` (short record index), behind the records.
- Type 9 is fire. Writers: `0x9C120` (Extender `c_game_spawn_fire_internal`, callers `0x99420`, `0x9C100`), `0x9C450` and `0x9DCF0`, all through the generic effect constructor `0x9B2B0` with type 9. A fire on a tile that already burns resets its timers (`+0x28`, `+0xC8`) and raises its strength `+0xCA` instead of adding a record (`0xA02D0` finds the existing one). With strength above 2 and suitable terrain a type `0x1A` record is added as well.
- Tiles with flag bits `0x10300131` (spawn) or `0x1A7001B1` (`0x9C450`) get no fire; tiles of buildings with certain types (`0x2D`, `0x2E`, `0x4A`..`0x4E` in `0x9C120`) are skipped, other buildings are ignited through `0xC4D30`.
- `0x9F650` (called by `0xC3C80`) marks the fire records of a tile (`+0xC8 = 0x10002`, `+0x28 = 0`).

## Pitch ignition

- Setting a pitch descriptor to `PitchState.OnFire` (Extender `GamePitchManagerAPI`) only ignites it: in `pf-fire-2` all 731 OnFire pitches had left that state within 1.5 s, while fire records of type 9 burned along the ditch for about 13 s, with up to 65 burning tiles at a time (`pf-fire-3`).
- The direction bytes of `0x51890D0` do not change for burning pitch tiles (731 tiles sampled 3 s after ignition, `pf-fire-1`): Vanilla's route searches do not avoid burning pitch through the movement mask.

## Unit route storage while moving

- `0x196280` stores the unit's current tile (`+0xC0`) as the route start (`+0xC8`), although a moving unit's builder starts at its next tile (`+0xDC`). The stored start is therefore no reference for decoding while a step is in progress.
- The cursor (`+0xF6`) already counts the step towards the next tile. Decoding the packed buffer from the next tile over steps `cursor..length-1` ended at the move target `+0xE8/+0xEA` in about 98 % of about 100,000 checks (`pf-fire-3`); the rest is consistent with region intermediates.
- `0x196280` with a target equal to the movement start writes state 0 and a zero cursor and length (the unit stops); its `+0xE8/+0xEA` target is written before that branch.

## Mod integration [mod integration]

MoatMove `FireRouteRules` (option `AvoidFire`) reads the type-9 records every four ticks, without writing native memory, and publishes their tiles as APIShared route tile rules with a replan policy. `RouteReplanWatch` re-plans moving units through `0x196280` to their unchanged target. Anchors and fallbacks: `Testmods/MoatMove/UpdateToNewDLL.md`, section "Fire effects".
