# Plague cloud height and damage eligibility

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

## Testmod evidence and limits

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

Detailed implementation/rollback/memory contracts and pending game cases:
Testmods/PlagueHeightTest/UpdateToNewDLL.md. Raw baseline exports remain unchanged.

Review fixtures additionally execute the complete installed 192750 routine plus
its jump tables. Ground hits match Vanilla's whole unit record across radius
0/1/2, resistance 0/1/2/3 and lethal/nonlethal health. The thirteen building
exclusions are removed only when enabled; height rejection, wall damage, source
self/dead/type immunity and unrelated-projectile early exclusion remain tested.
These are isolated native execution results, not a game or multiplayer run.
