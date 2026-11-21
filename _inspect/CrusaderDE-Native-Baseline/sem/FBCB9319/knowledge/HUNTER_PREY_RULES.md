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
  first chicken of **that owner**. `0xD1AD0` invokes this path at step `0xB4` of its 200-step cycle, so each active player can gain one chicken every 200 simulation ticks. The flags of `cmp eax,[rdi+0x2048]` at `0xD2ABF` decide
  both branches.

## Reservation

`GameUnit +0x448` is a countdown, not an owner. A Hunter holding a target writes 2 in
states 0, 1, 9, 10 and 11. Deer and goats (`0x156130`) decrement it in every update; rabbits
(`0x157810`), camels (`0x158020`) and chickens (`0x162CB0`) do not. The query accepts only 0.
A value of 1 means one prey update has passed since the last refresh.

## Rabbits and melee

- Tribe flag `+0x58E` (absolute `0x7CC6CAE`): cleared by every order at `0x11E9CA`, set by
  Vanilla's ranged branches of orders 4 and `0x20` when the target is a rabbit, read only at `0x18F31C`.
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
stage 8, otherwise 10. Nothing in Vanilla holds a stage once chosen. The separate near refresh uses Chebyshev distance at most 20 on the world coordinates (8 world units per tile), as detailed below.

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
- Thread (direct-branch scan of `.text`, 2026-11-10): the speech chain has one reference per link, `0x1940C0` <- `0x18CE77`
  in `0x18CE60` <- `0x97E78` in `0x97DA0` <- tail jump `0x97CFC` in `0x97C70` <- `0x8692D` in `DLL_RunTick`. The speech
  query therefore runs in the same `DLL_RunTick` call as the Hunter updates, on the engine thread, never concurrently
  with them. Indirect calls through computed targets are not covered by the scan.
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

## Corpse phases and owner gates in detail [Scope: Vanilla]

Static audit of 2026-11-08 (hash `FBCB9319…31E2`); the fade immediate at `0x1584C5` re-checked by disassembly.

- Corpse phases: phase `0x75` counts the dead counter `+0x920` (native) up to the type's threshold (signed 16-bit `jle`),
  then sets state `0x6E` with counter 0. Thresholds: deer/goat 500 (`0x156C25`, imm32), lion/hyena 50 (`0x1574FC`),
  rabbit 5 (`0x157EBD`), camel 50 (`0x15848E`), crocodile 50 (`0x17F90F`), war dog 50 (`0x16794D`), all imm8 except deer.
  Chicken, cow and dog have no `0x75` phase and enter `0x6E` with their old counter (chicken +2 per update).
- Phase `0x6E`: each type has its own `mov ecx,0x20` (deer `0x156C5A`, lion `0x157535`, rabbit `0x157EDC`, camel `0x1584C5`,
  cow `0x158D45`, dog `0x159BBF`, chicken `0x163472`, war dog `0x167987`, crocodile `0x17F94C`). `cl` caps the fade byte
  `+0x986` (`cmovg`) and `cx` is the removal threshold of the counter; past it the update sets alive state 3, and the unit
  pass `0x182B00` then calls removal `0x1869C0` instead of the update. Raising the immediate therefore also stops the fade
  early (1800 leaves the corpse at fade 8 of 32). The Script Extender's `RabbitDespawnTickTime` is the immediate at `0x157EDC`.
- Arrival (`0x1306BD`, counter = 1000) starts the fade of a `0x75` corpse; a `0x6E` corpse is marked and removed one pass
  later. A Hunter in state 2 never reads the corpse again; one in state `0xB` whose corpse vanished finds the global mismatch
  at `0x1306AC` and searches anew without meat. Alive state 3 also fails the alive filters of `0x18AF50` and `0x18B640`.
- Owner gates (all 14-byte spans, two instructions, no inbound branch): query `[0x18AFD8,0x18AFE6)` (`cmp word[rbx-0x20A],0;
  jne 0x18B0DD`, free: rax, r8-r11, rdi), attack `[0x18EBDA,0x18EBE8)` (team-table compare at `0x37EDF3C`, `je 0x18ECA7`; free:
  rax, r11), arrow `[0x192AA7,0x192AB5)` (team compare plus `movzx r11d,[rsp+0x78]`; the `jne` at `0x192AB5` reads ZF). The
  query checks the owner word (`+0x6EE`, public `+0x92`); attack and arrow compare teams.

## Ownership since ImprovedHunters 1.2 [mod integration]

From 2026-11-08 (working tree, not yet released): APIShared (`APIShared.Wildlife`) owns the owner gates at `0x18AFD8`,
`0x18EBDA`, `0x192AA7`, the reservation release of registered types (managed tick sweep, no update-entry stubs), the
query-entry record `0x18AF50`, the corpse-arrival record and corpse removal `0x1306AE`, and the corrected Hunter ids of the
Script Extender's query and meat-pickup events. ImprovedHunters registers rabbit, camel and chicken there, plus its target
policy and meat handler; its own `OwnedChickenHuntPatch`, `PreyReservationHeartbeatPatch` and issue-123 capture are gone. The
"ImprovedHunters integration" section above describes the state before this change.

## Herd kinds, spawn arts and route readers [Scope: Vanilla]

Static audit of 2026-11-08 (hash FBCB9319). The herd dispatcher `0x117520` selects the update by the record kind (`+0x42`) through
two jump tables: deer `0xD` `0x124E80`, lion `0xE` `0x1259D0`, rabbit `0xF` `0x125D00`, camel `0x10` `0x124890`, goat `0x28`
`0x125290`, hyena `0x29` `0x1256A0`, crocodile `0x2A` `0x124B90`; kinds `0x2`-`0xC` and `0x13`-`0x27` are troop groups (`0x10AA20`).
The Extender's `GameTribe` starts `0x2A` after a raw record: owner `+0x2C`, global id `+0x34` (the "serial"), state `+0x40`, leader
`+0x5A`, count `+0x5C`, member bits `+0x60`, waypoints `+0x5B4`, route index `+0x5DC`, route count `+0x5DE` are its named fields.

- Spawn `0x11E150(manager, art, x, y, height)` arts: deer `0xDC` (9-12 animals), lion `0xDD` (3-4), rabbit `0xDE` (14-21),
  camel `0xDF` (2-5), goat `0xE2` (4-5), hyena `0xE3` (6-9), crocodile `0xE5` (1). `0xE0`, `0xE1`, `0xE4` call other functions
  and create no herd. Callers: `0x123670` (deer, goats), `0x1238C0`, `0x1239C0` (camels), `0x123AC0`, `0x123C20` (camels), and
  map loading paths `0x13CA0`, `0xF82B0`, `0x1045F0`.
- `0x1261F0` runs every tick right before the dispatcher (`0xCE26D`) over every record in use and removes members whose alive state
  is not 1, 2 or 4, or that are dead (`+0x8F8`) or in another tribe (`+0x930`), through `0x123EA0`.
- Route builder `0x127E60` is called by deer, goats, lions, hyenas, rabbits and camels (rabbits also use `0x129010`; crocodiles
  build no route). It zeroes index and count (`0x1283D6`) before its only early exit (`0x128931`) and appends at most 10 waypoints.
  Readers right after it either test `count > 0` or walk to `route[index]` without it (`0x1249F2` camel, `0x124FBE` deer,
  `0x1253CE` goat, `0x125829` hyena, `0x125B6D` lion).
- Only the deer and goat updates read the sector threat counters (`0x124F20`, `0x125330`, byte-identical functions but for the type
  constants); rabbits set their flight word elsewhere.

## Animal hostility and target filters [Scope: Vanilla]

Static audit of 2026-11-08 (hash FBCB9319), types by the Extender's `eChimps`. Three independent filters decide whether a unit
attacks an animal by itself:

- `0x1867A0(manager, attacker, target, r9b)` returns 0 for "may attack". Callers: `0x188A20` (melee contact, r9b 0, from `0x194550`,
  `0x195170`), `0x187230` (r9b 0, from `0x174F90`), `0x18E9A0` (r9b 1, the ranged target search, 17 callers). Traders, trader horses
  and ghosts never; a camel never attacks. A non-animal attacker (`+0x984` 0) may attack lions, hyenas, crocodiles and rabbits
  always, deer and goats only with r9b set, war dogs of another team, ox, cow and dog depending on a mode flag (`0x186964`); never
  camels or chickens. Lions, hyenas and crocodiles attack deer, rabbits, cows, dogs, goats and non-animals.
- Melee target search `0x18C160` (through `0x19A1F0`, 16 callers) takes of the animals only lions, war dogs and hyenas (`0x18C731`,
  mask `0x40000400001` from type `0x2D`).
- Ranged target search inside `0x18E9A0`, type switch at `0x18F2B2` (types `0x2C`-`0x57`): deer and goats only for Hunters, rabbits
  only with the attacker's tribe flag `+0x58E` and never for Hunters, lions and hyenas only when their herd attacks (`+0x606`,
  `+0x608`, or AI state `0xCF` on a non-animal), war dogs of another team. Crocodiles pass this switch and are ordinary
  hostile candidates. The separate explicit-order branch calls `0x1867A0` for non-Hunters before preparing the target.

## Hunter meat inventory, worker identity and retirement boundary (2026-11-15) [Scope: Vanilla]

Confirmed static extension, installed native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, independently
rehash-checked on 2026-11-15 against CURRENT.json. Same-hash function pseudocode was
checked against direct installed PE instruction decoding. These are native facts;
they do not establish in-game acceptance of a new retirement policy.

Public building records start at `0x64CCBB0 + buildingId*0x32C + 0x5C`, public unit
records at `0x67E8400 + unitId*0x490 + 0x65C`. Game IDs are one-based. In hunter-hut
update `0xAE570`, the first worker ID at public building `+0x19E` is validated
against the unit global ID and the stored worker global at building `+0x1A8`
(Extender name `N0000176A`). The second pair `+0x1A0/+0x1AC` is the hound; its
respawn path creates type `0x34`. The Hunter's linked-building global at public
unit `+0x364` is written from the hut global in that spawn/assignment path.

The inventories are hut meat at public building `+0x150` (manager-relative field
`+0x1AC`) and carried goods at public unit `+0x384`. They must not be confused with
the second worker's global ID at public building `+0x1AC`. Hunter update `0x12FC70`:

- States 1/9/10/11 concern hunting and corpse approach; state 2 returns with a
  carcass; states 3/109 run hut processing. None proves an empty production chain.
- State 0, on finding nonzero hut meat, first enters state 4 and removes six meat
  through `0xB6100(manager, hutId, hutGlobal, 0xC, -6, 0, 1)`. State 4 owns a deferred
  yield even if hut meat and unit carry are both zero.
- A successful state-4 granary route changes to state 5, then calls
  `0x18D940(hunterId, 6, 1)` at `0x1309FE` and stores its return at unit `+0x384`.
  Granary lookup, path failure, acceptance and deposit remain within states 4/5;
  an empty carry check alone cannot certify completion. Return state 6 is separate.
- Idle states 0/7/8 have no deferred delivery of this kind. They still require
  separate inventory and hut-production checks before a consumer concludes drain.
- `0xB6240(...,2)` clears public building progress `+0x2C` and animation stage
  `+0x118`, but sets cycle mode `+0x26` to 2. Hut update `0xAE570` later recomputes
  cycle mode from its worker-interaction data. The fractional productivity rest at
  public unit `+0x300` is not a carried-meat field.

The existing AI retirement branch in `0x3B270`, after its failure-counter/control
guards, executes these three calls in order at `0x3B31E`, `0x3B331`, `0x3B33B`:

1. `0x55E10(0x34A9F70, originTileX, originTileY)` records coarse-cell avoidance.
2. `0xC4480(0x64CCBB0, buildingId, ownerPlayerId, 50)` applies ordinary native refunds.
3. `0xC4290(0x64CCBB0, buildingId)` marks removal, including propagation to buildings
   with the same nonzero public `+0x2A8` demolition group.

Refund 50 is the call argument. `0xC4480` gives no refund in native mode 1, overrides
the percentage to 100 in mode 6, and otherwise uses integer construction-cost
percentages. Marked state 3 is a request: `0xC60F0` later dispatches `0xB8310`, which
clears the record and releases worker links. Existing detailed cell/retry behavior
is documented in [AI woodcutter retirement](AI_WOODCUTTER_RETIREMENT.md).

Directly verified full original function lengths/hashes:

| RVA | Length | SHA-256 |
|---|---:|---|
| `0x3B270` | 229 | `EFF1F3C1FB0BB922746F17F266D62EAA252E6FF7C64610EF50856EA2833AAD9D` |
| `0x55E10` | 109 | `84F5ED0A91B52B6FD62CB4B56F35A5EB320BAB9B4A1F18E9603B89C2208DB828` |
| `0xC4480` | 1320 | `C1E4DC936740B9D35117AC1539BC0A07D48EF212F61CE57778DC2A82AA8445D7` |
| `0xC4290` | 257 | `713BF8FC70A1320F9EA7BB8B1CEBC82F0E52211C78A41D1DA3286EA5E24B0E56` |

No PE base-relocation entry lies within those ranges. RIP-relative manager anchors
at `0x3B27F` and `0x3B30E` resolve to `0x64CCBB0` and `0x34A9F70`, both in readable/
writable `.data`; the following direct CALLs resolve to the three callees above.

Native scheduling evidence: `0xD1D4F` is the counter-increment/XOR boundary inside
`0xD1AD0`, whose only caller is `0xCDE60`. The caller increments the map tick before
it, then later performs ordinary building update `0xC60F0`, AIV update `0x57330`
and unit passes `0x182B00`/`0x187B30`. Earlier helpers called by `0xD1AD0` return
before this boundary; no HunterUpdate is suspended at it. This native ordering
does not by itself prove where a future external tick publisher will execute.

## Startup archers and rabbit permission (2026-11-19) [Scope: Vanilla]

The installed DLL and semantic manifest matched SHA-256 `FBCB9319…31E2`. The map's
starting-troop dispatcher `0x119050` can call `0x117D20`, which places each group's
members at a common position and assigns them to the tribe. Archer update `0x13F540`
can fill the aim field while in formation-move state 101. This state can propagate
automatic targeting through group order `0x21`; the ordinary shooting states
4/5/6 are distinct. The formation movement ends through `0x188340`, which requires
path flags zero and either a zero selected connection record or resolved destination
coordinates equal to the current tile. Archer state 6 releases the arrow at frame 6
after rechecking line of sight, target global ID, life, corpse state, applicable type
and range. A failed release re-runs target selection and can re-enter state 5 on a
valid alternative. State 5 can also abandon a target killed during state 4.

The ordinary ranged query's rabbit branch at `0x18F31C` uses the same tribe flag for
human and AI owners. Order `0x20` and automatic group order `0x21` enter the same
dispatcher block at `0x11F156`. The ranged branch at `0x11F230` sends `0x21` past
the explicit target writes and the rabbit flag store at `0x11F2AF`; automatic
propagation cannot grant permission there. The other writer at `0x11EC89` is the
ranged branch of order 4. All orders first clear the flag at `0x11E9CA`.

## Startup archer and rabbit observations [mod integration]

On `testanimals.map` in skirmish with ImprovedHunters 1.2.3 and the installed
Script Extender and Fixes (2026-11-19, runs
`.work/a/hunter-start-sight/006` and `007`):

- All five human starting archers aimed at crocodile 268/global 358 while following
  formation routes of 3–5 tiles at tick 204. The target stood on land, about 50 tiles
  away, with equal effective and native ArcherArrow ranges of 54 and positive sight.
  Without a player command, all five finished forming and fired at crocodiles in
  ticks 355–376. The first intended-target damage was logged at tick 455.
- Twelve actual arrows targeted crocodiles before the group's first move at tick 728,
  which left rabbit permission at zero. A genuine aborted release was traced on
  archer 2262/global 2744: wind-up at tick 439, target crocodile 189/global 249
  killed by another archer at 464, wind-up on crocodile 269/global 360 at 487 and
  an actual arrow on it at 531. The same pattern occurred when the second crocodile
  was killed. A persistent no-release loop was not reproduced.
- Six AI starting archers never aimed at a rabbit in either run. One selected a
  crocodile at tick 911 with rabbit permission zero, and the test ended while it
  had reached the release state. This observation concerns the tested start rather
  than every AI command path.
- In the separate control, neither of two nearby rabbits was aimed at or shot before
  the explicit order. The group then fired on the ordered rabbit at tick 373 and on
  the second rabbit at 486. Both targets died from actual arrows. A move at tick 848
  cleared the group's permission from 1 to 0.

## Final movement step and Hunter near refresh (2026-11-19) [Scope: Vanilla]

On the same hash, movement at `0x1855A0` resets the substep (public unit `+0xF4`)
to zero while queuing the next path step and increments the path index (`+0xF6`).
The index can then equal the path length (`+0xF8`) while path state `+0xF2` is
still 2 and the unit is finishing its step. The ordinary completion check takes
place when the movement substep reaches 8. Treating index equality alone as
arrival drops the first update of the final step, including its substep-zero
opening for the attack gate in Hunter state 1.

Hunter state 1 renews the target's reservation before its near-world-distance
branch at `0x130069`. The distance helper `0x79C0` returns Manhattan distance for the tile-based speed and attack checks and writes the maximum axis difference at scratch `+0xC`. The near branch compares that scratch after calling the helper with world X/Y. At Chebyshev distance at most 20 world units it calls the ordinary
query. That query rejects reserved units and checks minimum tile distances of
20 and 5, so it can return zero while the current prey is live and visible.
The return leads to state 6. The alternate branch at distance greater than 20
can call the ordinary attack routine through its existing locomotion gate.

## Corridor reproduction [mod integration]

Real skirmish, two complete 12-tile wall lines separated by one walkable tile,
`.work/a/hunter-start-sight/013`. At tick 1864 the live prey was visible in both
directions at the last tile before its position. The positive snapshot was
accepted at path state/substep/index/length `2/7/17/18`, while Vanilla waited
for the locomotion gate. At `2/0/18/18` the old mod discarded its continuation
with `path-unavailable-or-complete-2-18-18`. The native near query ran and the
Hunter entered state 6 at tick 1874 with sight 6 in each direction. No arrow
was created on that approach. A separately staged firing tile was used on the
next approach. This failure is independent of the bidirectional sight rule.

The first comparison that kept the final step in the continuation (`014`)
accepted Vanilla's attack at path `2/0/18/18`. The Hunter still finished that
step during state 9 and reached the prey's exact world position. The state-10
sight check returned its identical-position sentinel, and Vanilla did create
an arrow. The arrow was deleted with the prey still at 2500 HP. The ordinary
post-shot continuation then sent the Hunter back toward the prey and repeated
the approach. The final change stages an existing bounded firing-position
search when the prey survives and is at most one Manhattan tile away, before
the same-target State-0 MoveHere. A Hunter starting that retry on the prey's
tile continues moving even when the sight routine reports the co-located
points as visible.

The release configuration in run `020` entered state 9 at tick 1874 in the
same corridor geometry. Its first arrow at 2022 missed from the prey's tile.
At 2099 the same prey received a new four-step approach, and the Hunter left
the prey's tile. The next arrow at 2337 reduced the deer's HP to zero by tick
2341. The Hunter carried nine meat and the delivery event fired. No return to
the hut interrupted the two attempts.

Run `020` also confirmed initially blocked sight at a crosswall and an
offset corner. In both cases the Hunter changed position before firing,
killed the deer and delivered meat. The existing separate building-obstacle
scenario reached the meat carrying state. The combined run passed all 127
steps. The open control (`017`) logged a real arrow, meat delivery and
31 successful steps; the outer runner reported a transient log-sharing error
after exit, with a verified restore. The disabled-pathfinding control
(`018`) still failed to obtain meat in the one-tile corridor.

In the eight-player `Distant Encounters` skirmish (`012` and `019`), the
native prebuild option was enabled and all seven AI slots were The Pig.
The test supplied only missing hut materials before the test mod's scheduled
retry. Its own placement logic produced three Hunter huts in each run.
The baseline Hunter killed a camel. The final build's Hunter killed an
own-player chicken with a real arrow and entered the meat return path.
The logs distinguish an intentional switch to nearer prey from the corridor
abort. Neither eight-player run reproduced a population-wide exclusion of all
reachable prey.

## Hunter hut work cadence and multiplayer repair (2026-11-21) [Scope: Vanilla]

Static audit of the installed DLL, SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The relevant PE instructions and table entries were checked against the semantic export.

- Building dispatcher `0xC60F0` advances the production frame before calling the
  type handler. With cycle mode nonzero, `0xC633C` increments the current tick
  counter (public `GameBuilding +0x18`). The signed 32-bit comparison at
  `0xC6355/0xC6359` tests current minus last-step counter (public `+0x14`)
  against the configured interval (public `+0x10`). At the threshold it copies
  current to last-step and adds the production increment (public `+0x2E`) to the
  frame (public `+0x20`). The type-7 constructor default is 4 ticks per step
  (`0xB4B8B`, table `0x2E8AA0 + 7*4`).
- Handler `0xAE570` derives cycle mode from the worker interaction in the
  building record. Its phase word (public `+0x118`) runs through phase 0
  (16 frame advances), phases 1–6 (32 each), and phase 7 (16).
  The animation data at `0x2DF558/0x2DF570/0x2DF598` and the endpoint branches
  confirm those boundaries. The six middle phases each call `0xB6100` at
  `0xAE928` with meat +1 and cap 6. The handler resets the frame at each phase
  transition and signals completion at `0xAEA8C` (public `+0x2C`).
  A regular uninterrupted cycle takes 224 frame advances, nominally 896
  simulation ticks at the default interval. This count covers the hut phases.
  The initial counter alignment, the worker's entry and exit, and transport
  to the granary are separate from that count.
- The host's repair loop `0x251F0` selects units and buildings by their
  per-slot checksums. Chore 56 (`0x15710`) transfers unit ID, the native unit
  record and its path. Chore 57 (`0x158B0`) transfers the building ID and all
  `0x32C` bytes starting at the public building record. The building frame,
  phase, interval and both cadence counters are therefore present when that
  building is sent.
- On the receiving peer, repair completion Chore 55 (`0x15200`) calls
  `0xCA110(999)` at `0x15459` before the final checksum. The normalization
  pass loads the type default at `0xCA371` and writes it to public building
  `+0x10` at `0xCA379`. Hunter huts take this path and return to interval 4
  even when a different interval was present in the Chore 57 payload.
- The ordinary building checksum in `0x22020` includes public `+0x10`
  (`0x2222D`) and the frame word at `+0x20` (`0x22213`), but neither
  cadence counter at `+0x14/+0x18`. The hashed byte block starts at public
  `+0xD0` and includes the phase word. A difference confined to the two
  cadence counters therefore does not select a building for native repair.

## APIShared Hunter state and repair transport (2026-11-21) [mod integration]

In APIShared source at `13f7b1c98951`, the Hunter capture at `0x1306AE`
keeps the Hunter Global ID, corpse Global ID and prey type in a private row
keyed by Hunter slot. This row is saved separately and is absent from the
native unit checksum and Chore 56 payload. The live unit's target Global ID
can validate the row after the corpse disappears, but it does not supply the
prey's type.

APIShared already owns the receive validation, checksum contribution, and
measurement, packing and unpacking of extended Chore 56 production records
(`WorkerProductionStateNativeRuntime`). Its optional
`WeaponProductionTransferNativeRuntime` also owns the Chore 57 extension
and building checksum span. These paths currently carry production and
weapon-workshop state. The Hunter record is outside those paths. The frozen
huntable-prey catalogue already participates in the production peer agreement.

The findings above are static. No modified Hunter work cadence or Hunter-specific
repair payload was exercised in a host-client game.
