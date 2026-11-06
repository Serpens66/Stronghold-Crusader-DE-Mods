# Hunter prey ownership, reservation and shot states [Scope: Vanilla + mod integration]

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Audit dated 2026-11-06. Disassembly plus decompile, confirmed in a real skirmish where marked.

## Hunter states

`HunterUpdate` is `0x12FC70`, AI state at `GameUnit +0x2BC`.

- State 1 approaches. It writes `+0x398 = 4` (explicit attack on its target) and calls `0x18E9A0`.
- State 9 only turns towards the target (`0x197A10`), then writes state 10.
- **State 10 is the shot.** Animation frame `0x16` fires the arrow through `0x1883B0`
  with type 1 after a visibility check. When the animation ends: a corpse target goes
  to state 11, otherwise `0x18B640` looks for another corpse, otherwise the Hunter query
  decides between state 0 and state 6.
- Skipping state 10 therefore fires no arrow at all. Earlier notes called it a "sit"
  state and treated seven "non-lethal shots" as misses; no arrow had been fired.

## Three owner barriers

A Hunter's own animal is blocked at exactly three points, in this order:

| RVA | Function | Rule |
|---|---|---|
| `0x18AFD8` | query `0x18AF50` | candidate owner must be 0, checked before the Extender's type hook at `0x18AFE6` |
| `0x18EBDA` | attack `0x18E9A0`, explicit branch | teams of target and attacker must differ; Hunters bypass `0x1867A0` |
| `0x192AA7` | arrow hit `0x192750`, only caller `0x9C730` | arrows skip units of the shooter's team |

Registers at `0x192AA7`: `r15` manager, `rsi` victim offset (`victim * 0x490`), `r13`
projectile offset, `r12d` victim type, `cx` projectile type, `r8`/`r10` projectile and
victim owner. A crash in a real run established `r15`/`rsi`; the two are not interchangeable
for any read that does not use the victim offset.

After the hit nothing depends on the victim's owner except the local warning at
`0x192B78`. Damage uses the attacker's fear factor (`0x180560`).

## Chicken behaviour and population

- Update `0x162CB0`, unit-function table index 62. It increments `r_GranaryChickens` of
  its owner and wanders between that owner's bakery, mill and granary (`0xBF300`, types
  `0x11`, `0x22`, `0x13`). With owner 0 no building is found, so a neutral chicken loses this.
- `0xD29E0`: target 0 without food, then by food up to 10. Below target it spawns at a
  random bakery, mill or granary (`0xBF510`). Above target it sets state `0x6E` on the
  first chicken of **that owner**. The flags of `cmp eax,[rdi+0x2048]` at `0xD2ABF` decide
  both branches.

## Reservation

`GameUnit +0x448` is a countdown, not an owner. A Hunter holding a target writes 2 in
states 0, 1, 9, 10 and 11. Deer and goats (`0x156130`) decrement it in every update; rabbits
(`0x157810`), camels (`0x158020`) and chickens (`0x162CB0`) do not. The query accepts only 0.
A value of 1 means one prey update has passed since the last refresh.

## Rabbits and melee

- Tribe flag `+0x58E` (absolute `0x7CC6CAE`): cleared by every order at `0x11E9CA`, set by
  Vanilla only in the ranged branches of orders 4 and `0x20/0x21`, read only at `0x18F31C`.
- Melee contact: `0x182B00` -> `0x195170` -> `0x188A20` -> `0x1867A0` with `r9b == 0`; a
  rabbit target returns "attackable" at `0x186952`. The stance scan `0x18C160` rejects
  rabbits at `0x18C731`.

## Cows

Dairy cows are units (type 51) owned by the player; the Tanner finds them through
`0x18B470` (same owner, state 0). A cow only exists as part of its farm: update `0x158780` marks it
for deletion in its first update unless `+0x334` and `+0x364` name a live farm that lists it, so a cow
spawned alone disappears at once.

Tanner `0x13E0B0`, AI state at `+0x2BC`: state 9 kills the cow and adds exactly 3 hides (good 5);
state 6 takes one hide and state 5 runs production; at its end `0x18D940(unit,1,1)` sets the carried
amount `+0x384`. State 7 searches an armoury (`0xB9AA0` good `0x17`), state 8 delivers one item per 5
updates and counts armour. The Hunter's food path is `0xBF9F0` (search),
`0xBD470` (acceptance, capacity `0xFA`), `0xB6100`, the statistic at `0x87EEF64` and `0x19D030` category 2.
That statistic counts all food deliveries, including cheese, not only meat.

Workshop product: `GameAction(SetNextWeaponMade = 1023, structure, good)` -> `0xD66B0` (only goods
`0x11..0x16` need a research check; a structure other than the opened one is ignored) -> chore 33 ->
`0xD5870`, which writes `+0x2EC` without a type check after comparing the global ID. A structure above
32000 (Vanilla's Shift sends -1) instead selects a building type through `0xC52B0`, falling back to
Fletchers for every good outside `0x13..0x16`. Readers use `+0x2EC` for the next run and `+0x2EA` as the
latched run; both exist for type 12, 13 and 14 only in Vanilla. Vanilla's checksum `0x22073` skips
`+0x2EC` but includes `+0x2EA`. New records are cleared in `0xB47E0`. The Extender names the same fields
`r_ProducedGoodId` (`+0x2EC`) and `r_NextProducedGoodId` (`+0x2EA`), the reverse of what they do.

Worker yield `0x18D940(unit, base, bonusFlag)` (Extender: `b50PercentBonus`), 17 callers:
`percent = max(100, productivity)`, `+50` if the mode at `0x8574B90` is not 0 and the flag is set;
`sum = (percent-100)*base + rest`, `bonus = sum/100`, rest stays per unit in `+0x95C`, bonus also in `+0x95E`
(no reader); returns `base + bonus`. Productivity `0x379CEEC + owner*0x583C` is `100-10*level`
(`POPULARITY_PARAMETERS.md`), so only a negative fear level raises it, up to 150. The mode is 0 in single
player, 99 in skirmish and 1 in multiplayer (`VANILLA_PEACE_TIME.md`). So skirmish and multiplayer give +50%
without any fear factor: a cow's 3 hides give 1,2,1 armours, then 2,1,2, on average 4.5. The Tanner and
Hunter pass flag 1; `0x133DD0` (2,0), `0x1377C0` and `0x138850` (1,0) pass 0. The Hunter passes base 6 for
every prey (`0x12FC70`), goats included: 9 meat in skirmish.

Chore 33 (`0x128F0`) carries the structure as 2 bytes, the good as **1 byte**, the global ID as 4 bytes.
`0xD66B0` also writes the local player's `0x379ADE0`; opening a Fletcher, Poleturner or Blacksmith sets that
from `+0x2EC` again (`0x89F40`, `0x97380`) and only those panels read it (`0x19D960`). Native readers of
`+0x2EC`/`+0x2EA` are the workers of those three types, `0x189220` and `0x19D960`; none is for the Tanner.

## ImprovedHunters integration [mod integration]

`HunterNativeStubs.cs` holds machine-code gates without managed transitions:
`OwnedChickenHuntPatch` for the three barriers, `PreyReservationHeartbeatPatch` for the
countdown at `0x157828`, `0x15803A`, `0x162CC8` (the Fixes mod owns the update entries),
`RabbitMeleeOrderPatch` at `0x186952`, `0x11EE64`, `0x18C731`; `TannerMeatPatch` at `0x13EBF7`,
`0x13ED8B`, `0x13EEFB`, `0x13EF38`, `0x13F009`. Executed against the audited
bytes by `_inspect/ImprovedHuntersChickenTests/NativeStubExecutionTests.cs`.

The Hunter's kill does not set his yield directly: he puts the corpse's meat into the hut (`+0x1AC`), and
when idle with meat there he takes it (state 4, Extender's pickup event at `0x12FD7C`, `0xB6100` good
`0xC`, -6) and only then, from state 4 to 5, calls `0x18D940` at `0x1309FE`. Before that he searches
no new target, so his last target is the prey of this yield; the corpse may be gone by then.

All time-dependent decisions use APIShared's native map tick (`HunterSimulationTime`, 40 ticks per
second at the default speed). The scan runs only in `SimulationTickEvents` on fixed tick boundaries.
Mission start precedes multiplayer simulation (`MISSION_LIFECYCLE.md`), so the feature flags and cleared
caches are in place before the first tick on every peer. Mod state is not transferred by a native resync.

## Evidence and limits [mod integration]

Real single-player skirmish, Script Extender, APIShared, BugfixesAndQoL and Fixes installed,
2026-11-06 (`Testmods/UiAcceptance/scenarios/SP-hunters*.json`):

- Hunter targeted its own chicken, killed it and delivered meat; it never targeted a foreign chicken. Chickens stayed owned and walked over 38 tiles; the
  count held at 4 and refilled after the kill.
- Three soldiers beside three rabbits killed none in 12 s; with the rule off they did at
  once; after an attack order all died within 70 ms.
- A rabbit 7 tiles away stayed alive for 8 s beside aggressive soldiers and was chased after
  an order on another rabbit. In the default stance it was not chased within 60 s: the melee
  scan `0x18C160` follows the tribe stance (`+0x634`): hold none, defensive `0x28`, aggressive `200`.
  After a move order a rabbit on the next tile stayed alive for 8 s.
- A Hunter killed three deer with one arrow each and delivered 17 meat without leaving a
  living deer. The rabbit and camel update entries carry the Fixes mod's wildlife hook, which
  disabled the first placement of the countdown through the live-byte overlap check.

- After an arrow left a chicken alive, the post-shot handoff returned the same identity
  through Vanilla's State-0 path and a further arrow followed (two consecutive cases, up to
  three arrows on one chicken).

- Tannery slider (2026-11-06, `SP-hunters-tanner*.json`, 5 meat per trip, mode 99 and productivity 100 read
  from memory, armour and meat stock measured with meat eating off, amounts in the order of the runs):
  no meat 4 armour; two meat runs 3 armour and 30 meat for two cows; after loading the save 2 and 15;
  three meat runs 0 and 22; one meat run 3 and 8; no meat 4 and 0. Each amount was predicted from the
  yield formula, whose fractions carry over on the unit. A value set through Vanilla's action
  (`+0x2EC` = 80) stayed through all of this and through the save, and did not change the runs. The
  position survived saving and loading; a new mission started without meat. Real mouse input: 25 drags
  in a row and track clicks.

- Hunter yield (same day): `ChickenMeat` 37 gave a carried amount of 55, later 56; a deer with the base
  amount 6 gave 9. Before the fix the carried amount stayed 9 for every prey, because the prey type was read
  when the hut's meat was taken, after the corpse was gone. So earlier configured meat values had no effect
  whenever the corpse was gone; the earlier check only tested `>= 37`, which several kills also reach.

- With the map-tick time base (2026-11-06, single player only): the scenarios `SP-hunters`,
  `SP-hunters-deer` and `SP-hunters-chase` passed and `SP-hunters-followup` passed after removing
  its deer part and its known default-stance chase step. Hunting works during peace time.

Not observed: real multiplayer (also not for the Tannery), a native resync with hunters active, an AI Tannery with the mod, a truly non-lethal arrow on deer, the absence of the warning
sound (executed in the fixture only), statistics after own-chicken kills.
