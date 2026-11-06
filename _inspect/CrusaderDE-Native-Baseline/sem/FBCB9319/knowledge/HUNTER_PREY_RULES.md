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
`0x18B470` (same owner, state 0). Leather armour delivery in `0x13E0B0` hardcodes good
`0x17` and armouries. Workshop mode field: `0x2EC` next and `0x2EA` latched; setter chain
ends in `0xD5870`, where a player-wide request (`> 32000`) falls back to Fletchers for good
12. Static only, not implemented.

## ImprovedHunters integration [mod integration]

`HunterNativeStubs.cs` holds machine-code gates without managed transitions:
`OwnedChickenHuntPatch` for the three barriers, `PreyReservationHeartbeatPatch` for the
countdown at `0x157828`, `0x15803A`, `0x162CC8` (the Fixes mod owns the update entries),
`RabbitMeleeOrderPatch` at `0x186952`, `0x11EE64`, `0x18C731`. Executed against the audited
bytes by `_inspect/ImprovedHuntersChickenTests/NativeStubExecutionTests.cs`.

All time-dependent decisions use APIShared's native map tick (`HunterSimulationTime`, 40 ticks per
second at the default speed). The scan runs only in `SimulationTickEvents` on fixed tick boundaries.
Mission start precedes multiplayer simulation (`MISSION_LIFECYCLE.md`), so the feature flags and cleared
caches are in place before the first tick on every peer. Mod state is not transferred by a native resync.

## Evidence and limits [mod integration]

Real single-player skirmish, Script Extender, APIShared, BugfixesAndQoL and Fixes installed,
2026-11-06 (`Testmods/UiAcceptance/scenarios/SP-hunters*.json`):

- Hunter targeted its own chicken, killed it and delivered the configured 37 meat;
  it never targeted a foreign chicken. Chickens stayed owned and walked over 38 tiles; the
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

- With the map-tick time base (2026-11-06, single player only): the scenarios `SP-hunters`,
  `SP-hunters-deer` and `SP-hunters-chase` passed and `SP-hunters-followup` passed after removing
  its deer part and its known default-stance chase step. Hunting works during peace time.

Not observed: real multiplayer, a native resync with hunters active, a truly non-lethal arrow on deer, the absence of the warning
sound (executed in the fixture only), statistics after own-chicken kills.
