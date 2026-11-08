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
latched run; Vanilla uses them for types 12, 13 and 14. The building CRC in `0x22020` includes both words.
The temporary zero at `0x22201` addresses `RSI+0x2EC`, with RSI already at public record `+0x10`. New records are cleared in `0xB47E0`. The Extender names the same fields
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

## Corpses [Scope: Vanilla]

- `0x18B640` is the corpse search. Filter: `+0x6E4` == 2, `+0x8F8` != 0, owner `+0x6EE` == 0, type `0x2C` (deer) or `0x56`
  (goat), reservation `+0xAA4` == 0. It keeps the nearest over Chebyshev 20, then over 5, with a sight check below 54, no
  upper limit. It does not reserve. Only state 10 calls it; an idle Hunter never looks for corpses.
- Handoff at `0x130548`: target fields, state `0xB`, `0x196280`. State `0xB` (`0x1305E0`) writes reservation 2 on every
  update while the global ID still fits, sets the corpse's `+0x920` to 1000 on arrival and starts state 2.
- `+0xA0C` on the Hunter is 0 for deer, 1 for anything else; it picks the animation in state 2.
- Deer corpse (`0x156130`, jump table `0x156CB0`/`0x156CE0`): it counts `+0x2C4` up to 500 (`0x156C25`), then sets state `0x6E`
  and restarts the count. There it fades (`+0x32A` up to 0x20) and is deleted when the count passes 32 (`0x156C53`). The Hunter's
  1000 on arrival (`0x1306BD`) therefore means: fade on the next update. The reservation counts down in every state
  (`0x156203`) and only states 0, 1, 9, 10 and `0xB` renew it, so a corpse being collected shows reservation 0 for about 30 of its
  last 33 ticks. Vanilla's own `0x18B640` does not check for this either.

## Hunter speed stage [Scope: Vanilla]

State 1 (`0x12FF38`) decides the stage (`+0x348`, read at manager `+0x9A4`) anew on every update from the Manhattan
distance EDI to the target: over 40 stage 1, over 36 stage 2, over 34 stage 3, over 32 stage 4, over 30 stage 6, over 28
stage 8, otherwise 10. Nothing in Vanilla holds a stage once chosen. Chebyshev 20 or less on the tile fields triggers the new search.

## Herd flight and respawn [Scope: Vanilla]

- `0x124E80` (deer) and `0x125290` (goats): `EDI = cell +0x28 + cell +0x48` for the sector of the herd's leader (`+0x5A`), grid
  `0x3395E10`, 80 by 80 sectors of 10 by 10 tiles, stride `0xA0`. Not zero and `+0x5F4` == 0 means flight (`0x127E60`,
  `0x11DFE0`). `+0x48` counts non-animals of any player and deer and goats of a fleeing herd, `+0x28` lions, hyenas and crocodiles (`0x126810`, one sector column at a time,
  called from `0xD1AD0`), `+0x54` rabbits, `+0x24` camels; rabbits and camels scare nobody. A fleeing herd counts itself too. The grid knows
  neither sight nor connection, so a unit behind a wall in the sector scares a herd.
- `0x123670`, from `0xCDE60`, only in mode 0 and below 6 animals: `0x11E150(0x7CC6720, art, x, y, height)`. `0xDC` makes a deer
  herd of 9 to 12, `0xE2` a goat herd of 4 to 5. Tile mask `0x4A5014B1` refuses a spot. Points `0x37EDBC0`, `0x39C94B0`.
  Those tables only hold the first herd of each kind (`0x1171D0`, `0x117240`), not every placed herd.
- Herd record (`0x7CC6720`, stride `0x688`): `+0x2C` owner, `+0x34` serial from `0x3665F84` (`0x119C00`), `+0x40` state (2 in use, 3 empty, 0 free), `+0x42` kind
  (`0xD` deer, `0xE` lion, `0xF` rabbit, `0x10` camel, `0x28` goat, `0x29` hyena, `0x2A` crocodile; `0x117520` picks the update by it), `+0x5A` leader, `+0x5C` count,
  `+0x60` member bits by unit ID, `+0x5B4` up to 10 route waypoints (x, y words), `+0x5DC` route index, `+0x5DE` route count, `+0x5F4` flight. A map holds its herds in these records, so they
  are all there at its first tick. Corpses stay members until removed.
- Routes (`0x127E60`, `0x129010` for rabbits only): a flood over the sectors from the leader, up to 5 sectors for deer (50 tiles) and 3 for goats, then waypoints outward in a direction by chance.
  Every route starts where the herd is, so a herd can wander over its whole connected land. Flight takes the last waypoint at once.
- Growth (`0x118130`, arguments confirmed at `0x124F96`, `0x1253A6`): each herd update counts up and every 100 updates rolls 28 in 128 for one more animal, whatever the herd's size, also for a
  single animal, not in flight, not in the editor. `0x11B460` removes all above 30. Measured on `testanimals.map` (2026-11-07, `.work/a/hunters/079`, run `rsp`, flight between herds removed by the mod): a herd of 2 reached 30 in at most 12700 ticks, 453 per animal against 457 by the rule, 11 s at 40 ticks per second. In Vanilla's chain flight (97 % of all samples in flight on this map, 2 % with the mod's rule) growth nearly stands still.
- `0x3665F28 == 1` is the editor: herd updates return before any route or growth, confirmed in the editor (2026-11-07, no route and no new animal over 1800 ticks).
- Camels found new herds of their own (`0x124890`, `0x123C20`, `0x11E150`), seen in the same run as a new record with its own serial.
- `0xFFC70` case `0x4E`: outside mode 0 the Hunter's hut button is off when `0x37EF97C` is 0 (deer and goats at map start,
  `0x94350`, `0x1860E0`). Confirmed in skirmish: with the setting off the button is disabled on a map without them.

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

- Hunter's hut, range, closer prey (2026-11-06, `SP-hunters-hut`, `-hut-off`, `-range`, `-switch`): on a skirmish map
  without deer and goats the build button is disabled with the setting off and works with it on, also by click. A deer out
  of range stays unhunted until the range is raised. One change of target from a far to a clearly closer deer, none back.
- Herd flight (`SP-hunters-flee`, `-flee-off`): a soldier in a closed wall ring in the leader's sector left the herd calm for
  16 s while `cell +0x48` was set, with the setting on; beside the herd in the open it fled 94 ms later. With the setting off the
  ringed soldier sets it off, as in Vanilla. Walls: ground may refuse them, and a line stops when the stone is gone.
- Speed stage (`SP-hunters-speed`): the same live deer, stage 1 to 2 at Manhattan 37 to 40, then back to 1 within one update
  when it was moved to 67 to 70. In this test the deer jumps, because a deer ignores `MoveToTile` often enough to make it walking
  unusable. The sight change and the detour of the plan's check are covered by the matrix below, not with stage logging.
- Free corpses (`SP-hunters-corpse`, `-corpse-off`, `-corpse-hit`): an idle Hunter took a corpse he had not shot, deer 9 meat and rabbit
  3, each the base amount plus Vanilla's 50 percent. A corpse with a renewed foreign reservation stayed untouched for 15 s and was
  taken after it ended. Two Hunters, one corpse: one went. Setting off: left alone. A Hunter on his way switched to the corpse of a
  hit in 78 ms (hit injected) and delivered it. With a real arrow whose aim was cleared in flight, the event and the shooter read
  from the arrow led to the same handoff. A natural hit on an unaimed animal was not seen in 150 s on eight deer on one tile.
- Matrix (`SP-hunters-matrix-*`): deer, goat, rabbit, camel: free, sight blocked by a wall, sight blocked by a building, walled in and
  ignored for 15 s then opened and hunted, cut off while approached and dropped. Each hunt delivered (deer and goat 9, rabbit 3, camel
  12). Deer also with two Hunters, 1147 frames both hunting, never one animal. Chicken: the free case only, because at a chicken
  target of 0 the granary rule above kills the fixture and at any other it spawns nearer chickens; delivered 1.
- Cost, DevDiag build, tick time on the simulation thread (`SP-hunters-perf*`). `test_stresstest5.sav`, 800 by 800, about 10,000 units:
  the whole scan 4.5 ms per second of play, 1.5 ms at most per call; herd rule 1.1 ms, corpse rule 0.6 ms per second. 300
  fixture animals and 6 huts on a 400 map at speed 80: 8 to 9 ms per second, at most 2 ms. On maps of size 200 and 160 it ran without
  error (15 to 17 and 9 ms per second, at most 4 ms). The size comes from `GetCurrentMapSize`; a large map file need not be a large map.

- One corpse, several Hunters (2026-11-07, `SP-hunters-corpse-once`, three Hunters, the idle ones forced to search right when the first
  arrives): before `IsFadingCorpse` a second Hunter went for the same corpse, deer and goat, in the one run made. After it only one
  did, deer and goat, also in one run. Corpses of goat, camel and chicken are taken and bring 9, 12 and 1 (`SP-hunters-corpse-kinds`).

Not observed: real multiplayer (also not for the Tannery), free corpses and the respawn in multiplayer, a native resync with hunters active, an AI Tannery with the mod, a truly non-lethal arrow on deer, the absence of the warning
sound (executed in the fixture only), statistics after own-chicken kills.

## Reservation word: complete access list [Scope: Vanilla]

Static audit of 2026-11-08, hash `FBCB9319…31E2`, nothing executed in the game. Public `GameUnit +0x448` is manager form
`+0xAA4` (manager `0x67E8400`, slot `0x490`, public pointer = manager + id*0x490 + 0x65C); unit loops read it as `[rbx+0x1AC]`
from `mgr+0xD88`. Found by a .text displacement scan for `0xAA4`/`0x448`, a scan of all stride-`0x490` loops and wider stores,
and the Ghidra memory database (same set). Struct copies, memcpy sizes, player (`0x583C`), AIC (`0x5E4`) and building records
with the same displacement were checked and excluded.

- Writers of lease 2 (register r14 = 2): Hunter `0x12FC70` at `0x12FE63` (state 0 after a successful query), `0x130044` (state 1),
  `0x130259` (state 9), `0x130338` (state 10), `0x13064F` (state `0xB`, corpse). Each renewal first compares the target's global
  ID (`+0x6F0`) with the Hunter's `+0x9F8` (public `+0x39C`). States 2, carrying, delivering and 6 do not renew.
- Release: only deer/goat `0x156130` (`0x156203`/`0x156213`, minus 1 while the signed word is above 0, every state).
- Readers that require 0: query `0x18AF50` at `0x18AFFD` (after the Extender's type hook at `0x18AFE6`), corpse search
  `0x18B640` at `0x18B6ED`, and through the query the idle-reason helper `0x1940C0` (0x34, caller `0x18CE60`, probable).
- Other meanings of the same word: the kennel `0xB47E0` writes the war-dog slot index 0..3 (`0xB51E7`, `0xB528C`, `0xB5335`,
  `0xB53E1`), read by the war dog `0x166FE0` at `0x1671BB` (delay index*8+0x20); the Engineer `0x14AED0` keeps a flag there
  (`0x14B58D`/`0x14B59B`, `0x14CD7C`, `0x14CF08`). Neither type may be a Hunter target.
- Never touched by rabbit `0x157810`, camel `0x158020`, chicken `0x162CB0`, lion/hyena `0x156DC0`, crocodile `0x17F2F0`,
  cow `0x158780`, dog `0x158EF0`, crow `0x158670`, seagull `0x158680`, Tanner `0x13E0B0`, direct attack `0x18E9A0`, spawn
  `0x17FEF0`, type init `0x19A240`. Removal `0x1869C0` clears the whole slot; spawn takes only alive state 0 slots.
- Consequence: a huntable type without a release is selected once and then never again until its slot is removed. Units update
  once per pass of `0x182B00` in ascending ID order (once per tick from `0xCDE60`: probable), so a held lease ends each tick at
  1 or 2 and a released one reaches 0 within two prey updates.
- Corpse dead-time thresholds before state `0x6E` (`+0x920`): deer/goat 500, lion/hyena/camel/crocodile/war dog 50, rabbit 5,
  cow 1000 in state 5, chicken enters `0x6E` directly (+2 per update); fading then removes the unit after 32 (`+0x986` to 0x20).
  Deer and chicken confirmed, the others probable.
- Not traced: the clear at mission start, save/load of the word, what `0x18CE60` does with the idle reason.

## Hunter query callers and meat path [Scope: Vanilla]

Static audit of 2026-11-08 (hash `FBCB9319…31E2`), core sites re-checked by disassembly; nothing executed in the game.

- Query `0x18AF50(rcx = unit manager, edx = querying unit id)` has exactly six callers: Hunter states 0 (`0x12FDBC`),
  1 (`0x12FF7E`), 10 (`0x130521`, `0x1305C7`), `0xB` (`0x1306DD`), and `0x1941B4` in `0x1940C0`, which computes the idle
  reason (0x34) of the selected unit for its speech (`0x18CE60` <- `0x97DA0`). Only state 0 uses the id; the others use
  the result as yes/no. The entry span `[0x18AF50,0x18AF5E)` has no inbound branch and holds the hunter id in `edx`.
- Candidate loop: `r14` = manager, `r13` = manager + hunter id * 0x490, `esi` = candidate id from 1, `rbx` = candidate
  public pointer + `0x29C`, bound `esi < [r14]`. Filters in order: alive state 2 (`0x18AFC0`), no corpse (`0x18AFCE`),
  owner 0 (`0x18AFD8`), type deer/goat (`0x18AFE6`, replaced by the Script Extender's hook), lease 0 (`0x18AFFD`),
  Chebyshev distance above the pass threshold (20, then a second pass with 5; `0x18B02A`), sight 1..0x1B0 below
  distance 54 (`0x18B0A2`), nearest wins, ties to the lowest id. No upper range, no lease write.
- Meat: the hut makes it, not the Hunter. While he is inside (state 3) the hut update `0xAE570` adds one meat per
  phase through `0xB6100(hut,+1,max 6)` at `0xAE928`; state 0 checks hut `+0x1AC` before any search (`0x12FD72`),
  takes it (state 4, slot set to 0 at `0x12FDAD`) and from state 4 calls the only Hunter yield `0x18D940(hunter,6,1)`
  at `0x1309FE`. A new hunt before the pickup is impossible.
- Every butchered animal passes the arrival `0x1306AE` exactly once (Vanilla kill, other killer, `0x18B640`, a mod's
  free-corpse choice): `rbx` = Hunter id, `rdi` = Hunter id * 0x490, `r14` = manager, `r13`/`rcx` = corpse id and
  id * 0x490, `eax` = corpse global (= Hunter `+0x9F8`), type at `[rcx+r14+0x6E6]`. `[0x1306AE,0x1306BD)` is 15 bytes,
  no inbound branch, no RIP-relative operand, flags dead at entry. Then corpse `+0x920` = 1000 (`0x1306BD`) and
  Hunter `+0xA0C` = (type != deer) (`0x1306CA`).
- The save stores the unit records (manager + 0x658, 10000 * 0x490) and building records (4000 * 0x32C) raw
  (section table entries `0x337030`, `0x336FB8`), so Hunter target fields and hut meat survive a load.

## Script Extender hunter events [Scope: External component]

Installed Extender 2.14.3 (product commit 4628c0e), RedBird 1.5.0, static check of 2026-11-08.

- `OnUnitHunterQueryTarget` (`0x18AFE6`): the reported hunter id is read from `[rsp+0xF0]`, assuming a 0x60 shift;
  RedBird 1.5.0's `X64FastcallSafe(preserveRAX:false)` pushes 0x30, so the slot is the native `[rsp+0xC0]`, which for
  Hunter updates holds HunterUpdate's saved R15 = the constant 2 set by the dispatcher at `0x183C35`. The event
  therefore reports Hunter 2 (issue 123; matches the logged "reported=2, reconstructed=1"). The event also fires for
  the local speech path `0x1941B4`.
- `OnHunterPickUpMeat` (`0x12FD7C`, `mov r8d,[r14+rdi+0x134]`) reads the unit id from R9, which HunterUpdate sets only
  later (`0x12FDA1`); the value is a leftover of the dispatcher. The Hunter id is in RBX. Logged ids 0, 1887428016,
  -1224745552 match.
- `OnHunterDropOffMeat` (`0x130B54`, RBX) is correct and fires once per delivered meat. `OnCalculateBonusYield` is a
  full detour of `0x18D940`; a Pre change of the base reaches Vanilla's bonus calculation.
