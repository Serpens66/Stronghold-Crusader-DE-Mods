# Fire on the ground: effect records, pitch ignition and unit route storage [Scope: Vanilla + mod integration]

Native DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (installed DLL, matches `CURRENT.json`). RVAs are for this binary only. Sources: semantic-baseline pseudocode (2026-10-29), machine bytes of `0xA2E60` and `0xA02D0` read from the installed DLL, and runtime counters of the MoatMove fire runs `pf-fire-2` and `pf-fire-3` on `test_stresstest4.sav` (2026-10-29). Function names are candidate-level.

## Effect manager and fire records

- The effect manager is a static array at `0x2F7A680` of `0xE8`-byte records, slots 1..5999. `0xA2D80` relinks slots `0x19..5999`; `0xA2E60` purges slots 1..`0x18` whose state is 2.
- Record fields: `+0x3C` state (`0xA2D80` links only records with a nonzero state; `0xA2E60` unlinks state 2), `+0x3E` effect type, `+0x60` native tile id (int), `+0x32` next record on the same tile (`0xA02D0`, `0xA0280`), `+0x38` previous record (`0xA2D80`).
- Per-tile list heads: `0x4CF23F0[tileId]` (short record index), behind the records.
- Type 9 is fire. Writers: `0x9C120` (Extender `c_game_spawn_fire_internal`, callers `0x99420`, `0x9C100`), `0x9C450` and `0x9DCF0`, all through the generic effect constructor `0x9B2B0` with type 9. A fire on a tile that already burns resets its timers (`+0x28`, `+0xC8`) and raises its strength `+0xCA` instead of adding a record (`0xA02D0` finds the existing one). With strength above 2 and suitable terrain a type `0x1A` record is added as well.
- Tiles with flag bits `0x10300131` (spawn) or `0x1A7001B1` (`0x9C450`) get no fire; tiles of buildings with certain types (`0x2D`, `0x2E`, `0x4A`..`0x4E` in `0x9C120`) are skipped, other buildings are ignited through `0xC4D30`.
- `0x9F650` (called by `0xC3C80`) marks the fire records of a tile (`+0xC8 = 0x10002`, `+0x28 = 0`).
- State values (audit 2026-11-01, pseudocode of `0x9F960` and machine bytes of `0x9C6B0` and `0xA2E60`): 1 = new, 2 = active; `0x9F960` moves a record from 1 to 2 on its first update, states 3..8 end it. `0x9C6B0` (search fire mask and counter) tests `state == 2 && type == 9` only. `0xA2E60` purges the active records of slots 1..`0x18`.
- Fire damage hits only units on the burning tile itself (`0x99420 -> 0x9E030 -> 0x192400`); tiles with flag bits `0xBA7001B1` take none. There is no flee behaviour: `0xDCE60` (unit `+0x406`) only makes a unit hesitate once before stepping onto fire.

## Pitch ignition

- Setting a pitch descriptor to `PitchState.OnFire` (Extender `GamePitchManagerAPI`) only ignites it: in `pf-fire-2` all 731 OnFire pitches had left that state within 1.5 s, while fire records of type 9 burned along the ditch for about 13 s, with up to 65 burning tiles at a time (`pf-fire-3`).
- The direction bytes of `0x51890D0` do not change for burning pitch tiles (731 tiles sampled 3 s after ignition, `pf-fire-1`): Vanilla's route searches do not avoid burning pitch through the movement mask.

## Unit route storage while moving

- `0x196280` stores the unit's current tile (`+0xC0`) as the route start (`+0xC8`), although a moving unit's builder starts at its next tile (`+0xDC`). The stored start is therefore no reference for decoding while a step is in progress.
- The cursor (`+0xF6`) already counts the step towards the next tile. Decoding the packed buffer from the next tile over steps `cursor..length-1` ended at the move target `+0xE8/+0xEA` in about 98 % of about 100,000 checks (`pf-fire-3`); the rest is consistent with region intermediates.
- `0x196280` with a target equal to the movement start writes state 0 and a zero cursor and length (the unit stops); its `+0xE8/+0xEA` target is written before that branch.
- `0x196280` returns 1 when it published a route and 0 on failure. The failure path (`0x199CD0`) zeroes the route buffer but keeps state, cursor and length, so a caller must stop the unit itself afterwards.
- `GameUnit +0x2A0` (Extender `r_SelectionAllowed`) is set by every military update routine and 0 for workers, civilians and firemen. A fireman stopped in state 3 extinguishes from afar (`0x159C30 -> 0xC3C80`, see `FIREMAN_TARGETING.md`), and workers in about 80 job states treat a forced route end as reaching their job target (`0x188340`).

## Mod integration [mod integration]

MoatMove `FireRouteRules` (option `AvoidFire`) reads the type-9 records every four ticks, without writing native memory, and publishes their tiles as APIShared route tile rules with a replan policy. `RouteReplanWatch` re-plans moving soldiers (`+0x2A0` set, no siege equipment) through `0x196280` to their unchanged target and aborts the move when no fire-free route exists within the wait; `FireEscape` moves soldiers off burning tiles and back once the fire is out; since 2026-11-02 escape, return and resume are Vanilla group commands (`0x11B520`, the escaping soldier in a tribe of its own) so the walking state is set. Anchors and fallbacks: `Testmods/MoatMove/UpdateToNewDLL.md`, section "Fire effects".

## Group move state and membership (audit 2026-11-02, same hash)

Sources: decompile and disassembly of the installed DLL (`0x11B520`, `0x196100`, `0x118E00`, `0x11D370`, `0x123EA0`, `0x119C00`, `0x118660`, `0x8C5F0`), Extender detour patterns resolved by a section-limited scan (one hit each). Static only; no runtime test.

- `0x196280` never sets the military walking state. `0x11B520` writes, per admitted member and before `0x196280`, `r_AIState` (+0x2BC) = 101, +0x342 = 0, +0x3A4 = 0, +0xEC = 0, the slot to +0x2D8/+0x2DA, and with a nonzero seventh argument +0x78 = 0x10 (destination marker counter); after a failed `0x196280` in the exit case the state becomes 122 with an exit route. A standing soldier moved by `0x196280` alone keeps its idle state and glides.
- Admission (all three member loops): AliveState 2, +0x29C, +0x29A, byte +0x40F and +0x428 zero, `r_AIState` != 207, +0x90 != 1, type not 40/41/61; types 39/77 need +0x3B0 == 2, 58/59 need 4, 60 needs 1. A member that is not admitted gets no write and keeps its old order. `0x11B520` returns 1 also without any admitted member or after a failed `0x196280`; it returns 0 for invalid or unavailable targets, target flags `& 0x30`, target PCL <= 0 and failed region checks.
- An ordinary player move is `0x8C5F0 -> chore 17 -> 0x196100 -> 0x11B520(tribe, x, y, patrol 0, newOrder 1, flags 1)`; `0x196100` additionally sets the tribe's +0x56C "fast" flag from input bit 7 (read by `0x19B260` speed sync). AI and patrol legs pass `(0, 0, 0)`.
- With `newOrder != 0` `0x11B520` writes the two-point order to the tribe (Extender `r_PatrolPoint1` = leader tile, `r_PatrolPoint2` = commanded tile, index 1, count 2, patrol mode); it never writes Extender `r_ObjectiveTargetTileX/Y`. In a single-member tribe the slot is exactly the commanded tile (`0xDA590` seeds it, `0xE1D30` accepts it).
- `0x11D370` (assign) and `0x123EA0` (remove) write membership fields only (unit +0x2D2 leader, +0x2D4 tribe, +0x2D6 ordinal, +0x2E0 tribe global id; tribe count, bitset, leader). Removing the last member sets the tribe's AliveState to 3 and `0x1261F0` deletes it in the same tick. `Create` (`0x119C00`) scans slots 1..4499. Player selection (opcode 16) moves the selected units into a new tribe; control groups are stored per unit (HD evidence). Units of an AI player without a valid tribe get a typed AI tribe from `0x118660` (call-site coverage probable); the AI never orders a tribe it did not create.

## Isolated fire lifetime (reported 2026-11-02; preserved 2026-11-13) [Vanilla facts]

Historical experiment evidence: root-repository `PathfindingFinalReport.md`, section 8.4,
Git blob `fba3f58d2a043ef418a72103da59ee4bcca4158f`. This section preserves that
reported result; it is not a new execution or a complete game acceptance run. The
original experiment harness and raw execution trace were not additionally located
during this documentation move. The installed native SHA-256 was rechecked on
2026-11-13 and matches the hash at the top of this document.

The experiment created one type-9 fire of strength 3 with original `0x9C450`,
executed original updates `0x9F960 -> 0x99420` and actual removal through `0x9F2D0`
in a private image of that DLL. There were no buildings, units or reignition.
To isolate the single tile, the spread callee `0x9C120` returned immediately in
the private image, and the separate accompanying smoke was removed. The installed
DLL and saves were unchanged.

| Effect variant, bit 7 of record `+0xC0` | Marked for deletion after updates | Actually removed after updates | At 40 simulation updates per game second |
| --- | ---: | ---: | ---: |
| Set | 877 | 878 | 21.95 game seconds |
| Clear | 1087 | 1088 | 27.2 game seconds |

These counts include the removal update and apply only to that isolated initial
state. The seconds are arithmetic conversions of simulation updates, not measured
wall time; speed and pauses change the elapsed real time.

The pitch descriptor in `0x7D280` advances after 20 updates of its ignition phase
and another 200 updates of the next phase. Those 220 updates do not end the
separate fire effect. `0xCDE60` calls the pitch and effect updates once each in
the same regular simulation block.

Reignition through `0x9C450` or `0x9C120` resets an existing fire's phase/frame.
While an associated building keeps burning, `0x99420` can retain the main phase
and reset its cycle counter. Consequently, 1088 updates are not a general maximum
for a connected pitch area, spreading/reignited fire or building fire. The earlier
approximately 13-second area observation in `pf-fire-3` is not such a maximum either.

The phase transitions, building extension, reignition reset and shared update block
were also checked against the current semantic queries during this move. Relevant
raw body hashes: `0x99420` =
`95DBB916A57D1F2755C14493B56A003F7AD047FAB544C01EB652B865A49AC6DD`;
`0x7D280` = `DCFB48D611AD0D90D46C4D162BF95B7892FDDB6574A64AC79F86884759FBB855`.
