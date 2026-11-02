# Plague cloud height and damage eligibility [Scope: Vanilla + mod integration]

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Feature audit 2026-10-14; semantic baseline selected by CURRENT.json, crosschecked
against installed-image disassembly. Static native contracts below are confirmed;
the 20-unit testmod tolerance has no gameplay/multiplayer acceptance yet.

## Damage path

Natural D17D0 and catapult-impact 9C730 creation flow through 9B2B0. 9F960 moves
Disease and resets source elevation at 9FDB8..9FDD8 to raw tile height+20. Its
registration 9C730 visits center, eight neighbors and sixteen outer-ring tiles.
Neighbor gates 9CB6F..9CB89 and 9CC3D..9CC57 compare surface heights via 6B910
with strict absolute difference <120, followed by projectile AliveState==2.
This comparison is not the unit's actual standing height.

192750 takes manager, one-based unit ID, sentinel-based projectile slot and radius
classification 0/1/2. Its Disease branch rejects the dead marker, surface mask
10000100 (wall/elevated), and building types 40..47/74..78. It does not compare
the projectile elevation against unit base+height addition. Type immunity table
32BC40, damage 400/200/150, player resistance, HP/death and bar updates follow.
Disease skips the ordinary projectile-height check later in the same function.
The only static caller is registration 9C730; Extender wraps 192750 with its
existing projectile-damage event. The candidate linked list 4C559B0 / unit +8FA
also supplies tower/keep occupancy at 198C40: roof units need no global scan.

## Height data

Normal standing-unit calculation 184FD0 uses native base +712 plus signed +714
(GameUnit +B6/+B8, manager header 65C and stride 490). It prioritizes stairs +12,
building-backed walls +40, IsBuilding +2, then Elevated building base+C07C0.
Movement/base normalization 19B260 and unit-specific tunnel/siege/transition
branches mean raw tile height is not the actual target unit height.

C07C0 reads building type at manager+id*32C+12E, returns height by switch, and
clobbers only RAX/RCX/RDX/flags. All return branches and C0830/C0858 jump tables
were checked in installed code. It has no SIMD or mutable-memory effects.
Elevated building base is manager+id*32C+148 (GameBuilding +EC, header 5C).
6B910 adds roof height to its tile/default grid; it differs from normal-unit
wall handling and is insufficient as a universal standing-plane query.

Renderer 41D60 uses Disease material 134's surface rendering branch, independent
of the damage eligibility filter. Visual appearance on roofs does not imply
native damage eligibility there. Treatment A0470 sets fade 1016; 9A080 advances
animation/lifetime and terminal state; A0540/9F2D0 retain death/removal paths.

## Testmod evidence and limits [mod integration]

Testmods/PlagueHeightTest uses four permanent X64InlineHooks at 9FDB8 (32 bytes),
192809 (26), 9CB6F (26), 9CC3D (26). Full containing functions and hash-matching
SQLite Xrefs show no external entries into these interiors. Production generator
fixtures execute heights, both directions of the inclusive +/-20 boundary,
all native roof height switch entries, disabled originals, alive/invalid paths,
and register/stack preservation. Installed RedBird and Iced are checked by hash.

LoadContext.Memory is explicitly a snapshot; live competing patches and committed
jump/pointer forms must be read from actual module memory. Existing Extender
192750 entry ownership is preserved. Current ExtraFeatures and BugfixesAndQoL
plague features do not own these four sites. Canonical Fixes HEAD f91968217e5ece7901df45a7d90e01fb48be2b96 has no matching plague/target hook source.
This is static compatibility evidence, not combined-installation game acceptance.

Current implementation/publication contracts and pending game cases:
APIShared/UpdateToNewDLL.md, section Plague standing heights. Raw baseline exports remain unchanged.

Review fixtures additionally execute the complete installed 192750 routine plus
its jump tables. Ground hits match Vanilla's whole unit record across radius
0/1/2, resistance 0/1/2/3 and lethal/nonlethal health. The thirteen building
exclusions are removed only when enabled; height rejection, wall damage, source
self/dead/type immunity and unrelated-projectile early exclusion remain tested.
These are isolated native execution results, not a game or multiplayer run.

## Integration observation, 2026-10-23 [mod integration]

Installed native/backend hashes and the full original four spans were revalidated.
APIShared now owns these sites; ExtraFeatures is an owner-bound policy consumer.
Tests/audits formerly under Testmods/PlagueHeightTest moved to
APIShared/tests/PlagueHeight.NativeTests and workspace _inspect/PlagueHeightAudit.py.
Full native damage fixtures now also cover all 16 category combinations and
building-before-wall classification. Existing historical findings above are retained.
User reports successful in-game cloud-edge movement, wall demolition and tower
collapse on the prior testmod. This does not establish multiplayer acceptance of
the new integration or the separate category controls.

## Cloud life cycle, movement and damage cadence (audit 2026-11-02, same hash)

Static audit for MoatMove's plague avoidance (decompile and capstone disassembly of the installed DLL; no runtime test).

- Type-22 records: state `+0x3C` 1 (new, set by `0x9B2B0`; `0x9F960` moves it to 2 on its next pass), 2 (active), 3 (end: `0x9A080` at phase 1024, `0x9C730` for an invalid position, `0xA0540`); `0x9F960` frees a state-3 record via `0x9F2D0`. Disease records use slots from 0x19 upwards; Global-ID `+0x44`.
- Phase `+0x28` rises every 4 ticks (`+0x24`/`+0x26`): 0..7 intro, 8..15 loop. When age `+0xD0` (signed short, +1 per handler call) reaches the lifetime (`0x9A16C`, 800; ExtraFeatures hooks the compare), the phase becomes 1016; 1017..1023 fade (about 29..32 ticks), 1024 sets state 3. The signed 16-bit compare makes lifetimes above 32767 fade at once (only AX counts). While phase is 1016 the loop branch resets it to 8 and re-checks the age on each call until the phase step (type-22 period 4, table `0x2DAF70`, applied in `0x9F960` before the handler) moves it to 1017, so the age reaches lifetime + 3 while still compared; a wrap past 32767 there resets the cloud to its loop phase. Safe maximum lifetime: 32764.
- Treatment (`0xA0470`, healer update `0x14F3C0`) sets phase 1016 only for state-2 clouds with phase <= 1000 within distance < 7; as age is still below the lifetime, `0x9A080` resets the phase to 8 unless the 4-tick increment lands first, so treatment often repeats.
- Damage: `0x9C730` runs at creation (state 1, so no damage) and then from `0x9F960` whenever `+0xAA` exceeds 30, i.e. every 31 ticks after a step of 1/8 tile; it never checks the phase, so fading clouds still damage. Center `+0x60` without height gate; 8 neighbours via `int[0x405EDB0 + (y*8+d)*4]`; 16-tile ring via `0x6AE20` and the pairs at `0x2DB750` (all Chebyshev distance 2); no bounds checks on neighbours and ring. Vanilla gate for both: `|H(center) - H(tile)| < 120`, H = `0x6B910`.
- Movement: on each 31-tick event `+0xB2` steps left decrease; at <= 0 a new count `(RNG hi byte) & 0x3F` and direction `(RNG >> 4) & 7` come from the simulation RNG (0x856A6D0). Directions 0 N, 1 NE, 2 E, 3 SE, 4 S, 5 SW, 6 W, 7 NW. Clouds drift over walls, buildings, towers and water; only an invalid mask or tile flags 0x30 end them.
- `0x192750` for Disease always returns 0; immune unit types (table `0x32BC40`) include lord and healer among soldiers; the healer counter `int[0x379E6D0 + owner*0x583C]` only scales the damage (80/60/40 %).

### MoatMove plague avoidance [mod integration]

`Testmods/MoatMove/src/PlagueRouteRules.cs` and `PlagueDamageModel.cs` mark tiles a cloud would damage (Vanilla rules, or APIShared's policy once readable) as route tile rules and let soldiers leave them; see `Testmods/MoatMove/UpdateToNewDLL.md`, "Plague clouds".

### ExtraFeatures plague duration limit [mod integration]

`ExtraFeatures/src/PlagueDurationPatch.cs` clamps the substituted lifetime to 32764 ticks (multiplier at most 40x); see `ExtraFeatures/UpdateToNewDLL.md`, "Plague lifetime limit".
