# Tree Blight and woodland decay

Audit date: 2026-10-25. Canonical CrusaderDE.dll SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
All addresses below are RVAs for this image; this is a hash-bound semantic contract.
Sources: the canonical DLL, the baseline's full decompilation and references,
Script Extender vegetation/event/enum code, the local Fixes clone, APIShared's
mode/lifecycle and native-site contracts, and Shared's corresponding adapters.

## Complete native path

The direct FreeBuild_Event dispatcher at `0x1045F0`, case 143, calls `0x107A00`
from `0x1049B7`. Timeline processing `0xF82B0`, case 15, calls the same function
from `0xF9F1F`. RandomEvents uses the original FreeBuild_Event API. Correcting the
common function therefore covers Vanilla and RandomEvents without a dependency.
The event traverses the existing vegetation pool, not all map tiles. It selects
woodland types 1..4 at stages 2..4 using Vanilla's existing random condition and
writes stage 5 without initializing its cumulative progress. It also retains
health, maximum health, felled state, resources and existing worker reservations.

The existing updater `0x108800` selects one of 64 growth buckets on ticks with
`tick % 8 == 4`. Growth `0x108320` first checks scenario/pause evidence and the
record's bucket. Its woodland path rejects felled records (`+0x9A != 0`) and
signed health below maximum (`+0x96 < +0x98`) before advancing progress. An old
blighted tree can consequently decay extremely late or never decay. Natural stage
changes call `0x1095D0`, updating health/resource fields from the stage tables;
Tree Blight does not call this function.

Cumulative thresholds at `0x31E220`, seven Int32s per vegetation type:

| Type | 0 | 1 | 2 | 3 | 4 | 5 (dead start) | 6 (exit) |
|---|---:|---:|---:|---:|---:|---:|---:|
| Chestnut, Oak | 0 | 24 | 48 | 72 | 96 | 120 | 130 |
| Pine | 0 | 20 | 40 | 60 | 80 | 100 | 110 |
| Birch | 0 | 16 | 32 | 48 | 64 | 80 | 90 |

Ten further selected growth calls remain from the canonical dead-phase start.
Assuming uniformly selected buckets, the expectation is `10 * 8 * 64 = 5120`
simulation ticks, not a deterministic removal deadline. Stage 6 sets alive=3;
the existing updater subsequently deletes the record through `0x107970` and
`0x71D60`, which clear its vegetation occupancy and pathing/build obstruction.
Existing seed, density and new-growth branches remain in the original function.
An already advanced or overflowing old counter is not reset by the fix.

The remaining growth gates are manager `+4 != 0`, frontend state at `0x3665ED8`
not 12, and native game type at `0x3665F28` neither 1 nor 6. Native type 1 is the
map editor, type 6 the siege editor. Played Freebuild is normally type 2 (map),
skirmish type 3, campaign type 0. APIShared GameModeKind and MissionMapType.FreeBuild
must not be confused with these raw native enum values. Freebuild already ages
woodland. No additional Freebuild activation is required or implemented.
Scenario/pause state `0x3665F90` and the random-bucket test precede the hook.

Woodcutter target selection and AI wood-density/economy weighting require earlier
stages (stage < 4). Stage-5 woodland is not a new harvest target. An existing target
can survive the event, explaining residual damage/felled state; clearing worker
fields or replenishing health/resources would change unrelated behavior. Building
placement still uses occupancy until Vanilla deletion; no overbuilding is enabled.

## Permanent interception contract

Native record base is `manager + nativeID * 0x9C`. Fields are alive UInt16 `+0x68`,
type UInt16 `+0x6A`, health/max UInt16 `+0x96/+0x98`, felled UInt16 `+0x9A`,
resources/current-max `+0x9C/+0x9E`, stage Int32 `+0xA4`, cumulative progress Int32
`+0xA8`, bucket `+0xAC`. The SE struct begins 0x18 bytes later than that base;
its named field offsets must not be used as manager-relative record offsets.
Both stubs use the native registers, avoiding the SE ID-view discrepancy.

| Purpose | Exact overwritten half-open span | Displaced instructions | First continuation |
|---|---|---|---|
| Event | `[0x107A6C,0x107A7A)` | inc r9d (3), add r8,0x9C (7), cmp r9d,[r10+0x10] (4) | jl 0x107A16 (2) |
| Growth | `[0x10838E,0x10839C)` | cmp word [rbx+0x9A],0 (8), jne 0x108645 (6) | movzx eax,word [rbx+0x98] (7) |

Event R8 points at the current type field, R9D is the slot index, R10 is manager.
It floors stage-5 woodland progress before replaying the original loop tail.
Newly blighted Vanilla stages 2..4 have counters below the floor; old stage-5 trees
retain any further decay progress. The game stores no Tree-Blight provenance, so
repair of old stage-5 records does not claim such provenance.

Growth RBX is the record base and RDI is manager. Only alive=2, woodland stage 5
bypasses the felled/health predicates, jumping to `0x1083B0`. Its progress is floored
only while the remaining manager/frontend/mode gates allow growth. All other paths
replay the original predicates, whose reject target is `0x108645`.

Both producers save RFLAGS/RAX/RCX; no calls, SIMD, RNG or allocation occur. Replay
happens at the original RSP and exactly preserves the original loop flags/register
effects. The aligned 32-byte native state holds enabled Int32 at 0, table pointer
at 8, game-type pointer at 16, frontend pointer at 24. Thresholds are read from the
live native table, not hard-coded into executable code. No new scan, load callback,
managed per-tree callback, JSON/save format, public API or network packet is added.

Validation includes the full canonical image hash, exact site and continuation
bytes, unique patterns, SE layouts/domain values and actual RedBird 14-byte spans.
The entire surrounding functions, growth jump table and external references were
audited: no entry lies strictly inside either span. A separate linear .text branch
scan also found no direct interior target; that scan alone is not an indirect-flow
proof. Incoming event-loop skip branches target the start, which is valid.

Extender's `0x108320` entry detour and later `0x108518` growth observer are outside
these spans. The local Fixes and APIShared hooks do not overlap. NativeSiteRegistry
claims both spans exclusively before one owned transaction. Live site bytes must
still be original. Hash/layout/site/registry failure disables only this feature.
The static runtime retains hooks, transaction, native state and registration until
process exit. After commit, even verification failure only clears the enabled flag;
settings, map transitions and startup cleanup never undo published hooks.

## Consumer and verification boundary

BugfixesAndQoL's host-only EnableTreeBlightDecayFix defaults/resets true and is
Recommended under Host / Economy / Fixes. The normal host master switch and option
jointly control the native flag. Existing settings/preset/Trail/authority logic is
used. Stored false remains false; missing older values retain the default.
Initialization uses the existing long-lived LibraryLoaded path and has no early
Unity component to be destroyed by startup cleanup. No new runtime JSON dependency
or shutdown subscription is introduced.

TreeBlightDecay.Tests executes the production emitter in private executable memory
against copied native tables, with independent branch/field/ABI expectations. It
checks all woodland types, live/dead stages, damage/felled states, progress floors,
advanced progress, disabled mode, editors, frontend/manager gates, exact displaced
spans, continuation instructions, switch ingress, stack/GPR/flags/SIMD preservation.
Modsettings production-DLL tests and existing HostClientPresetTests cover metadata,
UI/search/recommendation, defaults, migration, explicit false and host/client locks.

2026-10-25 evidence is retained in `.work/a/tree-blight/001`. The native fixture's
alternating on/off timing includes delegate/harness overhead and is only a native
cost indication. It does not prove an 800x800 game-load time or simulation rate.
Actual Vanilla and RandomEvents gameplay, Freebuild/skirmish/mission, Save/Load,
occupancy after removal, and real host/client acceptance remain unperformed while
the installed diagnostic build must remain untouched. This limitation must remain
explicit in delivery; an isolated build cannot substitute for those checks.
