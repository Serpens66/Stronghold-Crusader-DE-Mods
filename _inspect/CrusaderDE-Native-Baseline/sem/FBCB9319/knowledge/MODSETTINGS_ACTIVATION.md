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
