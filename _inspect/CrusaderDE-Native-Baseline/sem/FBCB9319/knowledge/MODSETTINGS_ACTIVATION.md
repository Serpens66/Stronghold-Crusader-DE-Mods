# Modsettings activation: peace time and No Dogs [Scope: Vanilla + mod integration]

Observation: 2026-10-22. Native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Read together with [VANILLA_PEACE_TIME](VANILLA_PEACE_TIME.md).
This records verified native behavior and isolated feasibility checks, not an installed fix.

## No Dogs: complete relevant state and decision chain

The confirmed export `DLL_IsMapperAvailable` at RVA `84A20` passes the mapper ID
from ECX to EDX, sets RCX to the mapper manager at image RVA `32C7730`, and
tail-jumps to `FFC70`. The latter has a fully decoded 889-byte body; its generic
`FUN_1800ffc70` name is still a candidate semantic label, not a public API identity.
It is a leaf function with a manager pointer and unsigned mapper ID. It also
implements scenario/content restrictions, signed short availability entries at
manager + `22C` + mapper * 2, special mapper entries and table branches.
The installed Extender enum confirms `MAPPER_DOG_CAGE = 0x138`.

For that mapper only, the relevant path is:

| RVA | Original action |
| --- | --- |
| `FFD87` | Compare mapper with `0x138`; other mappers branch to their own restrictions |
| `FFD8F` / `FFD92` | Test game mode in R10D; mode 0 bypasses the NoDogs restriction |
| `FFD94` | Compare R10D with 99 |
| `FFD98` | `74 31`: mode 99 bypasses to `FFDCB` |
| `FFD9A` | Compare active NoDogs int at image RVA `37EF964` with zero |
| `FFDA1` | Zero continues ordinary availability |
| `FFDA3` | Nonzero returns zero (unavailable) |

The current BugfixesAndQoL replacement is exactly two NOPs at `FFD98`, so it
removes only the mode-99 bypass. It does not remove the earlier mode-0 bypass,
change the setting, spawn/remove dogs, or change other mapper paths.

Managed setup provenance, checked against the hash-matched managed baseline:
`FRONT_Multiplayer` toggles `MultiplayerSetupData.no_dogs`; `setMPSetup` copies it
to the transfer struct. `setMultiplayerStartingData` serializes the struct and
calls confirmed export `DLL_ApplyMultiplayerSetupData` (`81050`). It tail-jumps to
`26370`, whose writes at `263D4/263D7` copy int at transfer offset `24` to
manager + `279FB8`. The manager is image RVA `8574320`, making the setup global
`87EE2D8`. The reverse export `DLL_GetMultiplayerSetupData` (`84280`) reaches
`20420`, which reads that field and writes transfer offset `24`.
This agrees with Extender `ChoreManagerOptionsInternal.NoDogs` offset 2596792.

Preinitialization (`DLL_PreInitMap_Multiplayer`, `86270`) sets native mode 99 for
the skirmish boolean and mode 1 otherwise. Mode 99 also occurs in local Coop
starts using that managed initialization path; it is not a cosmetic UI flag.
Initialization routine `94350` copies setup `87EE2D8` to active `37EF964` at
`96D79/96D7F`. Save/setup serialization at `14C00` transfers the four-byte setup
field in both directions. State production in `19D960` reads the active flag and
sets bit 8 in its option byte at offset `7DF`. No proposed activation gate should
erase either representation. The indexed reference in `D6B40` is a table access;
its symbolic decompiler alias must not be mistaken for an unconditional NoDogs read.

All native callers of `FFC70` were inspected: the export, `C2030` (mapper IDs
`60`/`61`, so it does not take the dog branch), and state producer `19D960`.
Preserve native restrictions and state production outside the narrow extension.

## Permanent logical dispatch feasibility [mod integration]

A six-byte indirect jump at `FFD94` can replace exactly the complete CMP/JE pair.
Dormant execution replays both instructions, preserving the branch to `FFDCB`;
active execution replays CMP plus the current NOP replacement and resumes at
`FFD9A`. A flag-neutral byte dispatch can preserve incoming flags, RCX, stack
and SIMD state. The complete original mapper function was executed in a private
mapped image for 322,560 inputs in four variants: canonical original, production
patch, dormant prototype, active prototype. Dormant matched original and active
matched production for every input. The 40 changed cases were exclusively dog-cage,
mode-99, nonzero-NoDogs states with otherwise nonzero availability.

For peace time and No Dogs together, the 31 existing intervention sites admit
25 nonoverlapping grouped dispatch candidates. Complete function ranges and all
known code/table incoming targets were checked; function size is the sum of its
possibly discontiguous ranges and must not be treated as a contiguous extent.
The installed `X64AssemblyPatch` backend prepared all 25 six-byte bodies with
exact complete-instruction spans of six through nine bytes. For the two existing
entry hooks, retain the independently audited 16-/18-byte production spans.
The other 23 dormant/active copied blocks relocated 104 instructions successfully,
preserving operands, flags, RIP-relative data targets and internal/external branch
destinations. Existing peace tests passed 499,401 assertions, including 1,520,832
inactive and 1,520,832 active wildlife cases.

This does not establish an already integrated installer or game/MP acceptance.
Keep hooks permanent, prepare near pointer slots and all dependent resources,
validate the expanded physical spans against live bytes, and fail closed on
ownership/byte mismatch. Do not use a minimum-14-byte hook on a two-byte branch
or restore published native bytes when a setting changes.

Reproducible scripts, decoded functions, span candidates, source identities and
test logs: [modsettings audit](../../../../../.work/a/modsettings/001/AuditReport.md).
The local audit artifact is intentionally retained; native raw data and SQLite
were opened read-only and remain unchanged.


## AutoTrade setup, native execution and save routes (2026-11-27)

The installed native SHA-256 matched the manifest and instruction/export records
listed above. The installed managed SHA-256 was
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
This section reports static and disassembly evidence.

### Setup and execution

The managed `Settings_Autotrading` toggle writes 0 or 1 to
`MPTEMPsetupData.allow_autotrading` (`FRONT_Multiplayer.cs:4683–4700`).
`EngineInterface.setMPSetup` copies the value into the fifth Int32 of the
transfer struct. `setMultiplayerStartingData` serializes it and calls
`DLL_ApplyMultiplayerSetupData` under the engine monitor
(`EngineInterface.cs:170–178,4180–4189,6388–6397`).
The export at `81050` loads the network-state instance at image RVA
`8574320` into RCX and tail-jumps to `26370`. Instruction `263A0`
(`8B4210`) reads the fifth Int32 at `[RDX+0x10]`. Instruction `263A3`
(`8981A0872700`) writes it to `[RCX+0x2787A0]`, image RVA `87ECAC0`.
The converse helper `20420` reads the same slot to transfer field 4.
The current local Script Extender view names the offset
`ChoreManagerOptionsInternal.AllowAutoTrading` (decimal 2590624).

At scheduled phases 21–28, the simulation calls `D0380` for player IDs 1–8
through `CDE60` → `D1AD0`. In that routine a paused player or one without
the market-availability prerequisite exits before the rule check. `D03BD`
compares native mode at `8574B90` to zero, and `D03C4` jumps directly into
the trade loop in mode 0. Otherwise `D03C6` compares the active AutoTrade
Int32 at `87ECAC0` to zero. `D03CD` exits on zero. Only admitted players
reach the per-good threshold, quantity, price and transfer work.
`19D960` publishes the active rule as bit 2 in its output byte at offset
`0x7DF`. Managed `PlayState.MP_AllowAutoTrading` reads that bit via
`(gotSignpost & 2) > 0` (`EngineInterface.cs:3210`).

[Chore 109](CHORE_SYSTEM.md) uses a separate producer/receiver path.
The serializer and receiver `1A240` transfer seven bytes and update paused
state or the selected good's AutoTrade settings. This handler does not test
the mode or active allow flag. Receiving or changing AutoTrade settings does
not establish that the periodic transaction routine will execute them.

### Load and restart

- **New local skirmish and restart from its setup.**
  `FRONT_Multiplayer.StartSkirmishGame` runs MP preinit with the skirmish
  byte true, applies `MPsetupData`, then loads the map
  (`FRONT_Multiplayer.cs:9963,9983–9985,10049`).
  `RestartSkirmishGame` supplies setup from its restart object
  (`FRONT_Multiplayer.cs:9828–9841`). The restart metadata encodes
  `MPsetupData.ToString()` (`HUD_IngameMenu.cs:281–310`), and the setup
  text includes `allow_autotrading` on write and read
  (`EngineInterface.cs:502,674`). Local Coop and custom Trail start through
  the corresponding skirmish preinit branches. `86270` sets active mode 99
  with the skirmish byte true, and 1 otherwise. In the `94350` initializer,
  `9447D` captures `87ECAC0` before the archive read and `95548` restores
  it. It also saves mode and restores it at `94CEE`.
- **New network multiplayer.** The shared `Platform_Multiplayer.StartGame`
  method uses preinit with the skirmish byte false, then applies the supplied
  `MPSetupData` before loading (`Platform_Multiplayer.cs:3923–3925`).
  The preinit/setup pair is outside host-only branching. On the normal MP
  path, the same native mode/flag preservation gives mode 1 with the applied
  rule. This proves the route for host and client and does not establish
  equality of their setup inputs during a real launch.
- **Network multiplayer save.** The shared `Platform_Multiplayer.StartSave`
  method uses `initMultiplayerGame()` with the default skirmish byte false,
  then loads with `multiplayerSave: true`
  (`Platform_Multiplayer.cs:4083–4127`). Host and client register/remap
  players differently, but both enter those same setup boundaries. The
  preinit helper `24220` sets `[manager+0x2787A0]` to 1. This route does
  not reapply `MPSetupData` after preinit. Its `GameAction(MPConfig)`
  uses speed via `20AB0`, not AutoTrade permission. A nonhost lobby update
  can apply the advertised setup before entering `StartSave`
  (`FRONT_Multiplayer.cs:7197–7199`), but the later preinit resets it.
  The save branch of `94350` restores the value captured before its
  archive read, so the initialization path carries the native default 1
  rather than reconstituting the original AutoTrade rule from the save.
  The save branch calls the common finalization helper `71670` after
  restoring mode. That helper keeps mode 1 for the ordinary network save
  family, sets 99 for a saved skirmish marker, and can normalize to 0 for
  other loaded families. Do not infer a nonzero mode from an arbitrary file
  offered to the loader.
- **Ordinary local save load.** `EditorDirector.loadSaveGame` calls
  `LoadSaveFile` and stores available restart metadata for later use
  (`EditorDirector.cs:764–791`). It does not reapply its saved setup.
  `DLL_LoadSaveGame` at `853F0` calls the archive reader `87D0`, which
  clears the active rule at `89DC`. The option block `14C00` transfers
  the adjacent Int32 at `87ECABC` in both directions and does not transfer
  `87ECAC0`. The saved mode marker is a distinct Int32 at `8574B94`.
  Routine `1CD20`, called at `86A23` from `DLL_RunTick`, copies active
  mode to this marker. The save writer `52B0` stores it at `58D7` and
  the archive reader reads it at `90B9` when the field is present.
  `71670` and `853F0` restore active mode 99 for a marker of 99.
  Consequently an ordinary load of a skirmish save with that marker carries
  mode 99 and the reader's zeroed AutoTrade rule into play, even if a later
  restart from the same save's setup restores a different rule.

Evidence sources are the managed baseline, the Iced-verified instruction
records and the native semantic and raw reference exports for the stated
hashes. The paths above were not exercised in a running game. The first
published PlayState and later ticks on two peers are still needed to confirm
the effective flags, archive provenance and trade behavior in each scenario.
No conclusion is implied for externally altered saves or later foreign writes.
