# Updating Improved Hunters for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24651686`
- DLL size: `3450880` bytes
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

On the audited hash, the mod uses each reference RVA directly and validates its
local semantic pattern without scanning the DLL. On a changed hash, each native
feature performs an executable-section-limited unique pattern search and then
validates its derived branches, tables and operands. An absent, ambiguous or
semantically inconsistent match disables only the affected feature. Raw unit
fields still require an in-game smoke test after a game update.

## Native address map

| Source pattern | Reference RVA | Use / offset |
| --- | ---: | --- |
| `CamelDespawnTickTimePattern` | `0x158468` | signed immediate at `+13` |
| `ChickenDespawnTickTimePattern` | `0x163415` | signed immediate at `+13` |
| `TargetSelectionTypeDispatchPattern` | `0x18F262` | automatic target type dispatch |
| `ManualAttackCommandPattern` | `0x18EAE6` | explicit `AttackUnit` command path |
| `ManualAttackTargetAssignmentPattern` | `0x18ED46` | explicit target assignment before automatic dispatch |
| `ComparisonSequencePattern` | `0xD2AB4` | granary chicken target comparison hook at `+11` (`0xD2ABF`) |
| `HunterQueryCandidateLoopPattern` | `0x18AF70` | temporary Script Extender issue-123 actor capture |

The source constants contain the complete wildcard patterns.

The automatic target selector starts at RVA `0x18E950`. A valid explicit
`TribeAICommand.AttackUnit` is checked at RVA `0x18EAE6` and assigns its target
at RVA `0x18ED46`, before the automatic candidate type dispatch.

The dispatch target table is at RVA `0x18F9C4`, and the type-to-dispatch-index
table is at RVA `0x18F9E0`. Chicken type `62` has table index `62 - 44 = 18`,
so its byte is at RVA `0x18F9F2`. Vanilla value `6` routes to the general
acceptance case at RVA `0x18F3A6`. Improved Hunters temporarily writes value
`0`, which routes to RVA `0x18F28E`; that case accepts only attacker type `6`
(hunter) and otherwise rejects the candidate at RVA `0x18F754`.

Vanilla's per-player granary chicken update starts at RVA `0xD29E0`. Its
food/population-derived target is in `eax` when RVA `0xD2ABF` compares it with
the current native player chicken count at `[rdi+0x2048]`. The following signed
`jle` skips spawning at RVA `0xD2BA7`; otherwise Vanilla reaches the existing
granary spawn event at RVA `0xD2B4C` and the unit creation call at RVA `0xD2B5C`.
Improved Hunters hooks only the comparison and normalizes `eax` to `INT_MAX`
below the configured tracked limit or to `0` at/above it. Thus exactly the next
Vanilla spawn is permitted or denied while Vanilla retains its keep, granary,
mode, position and timing checks.

The comparison callback identifies the source player through `rbx` (validated
range `1..8`). Runtime tracking stores source player, one-based unit slot and
`GameUnit.r_GlobalId`; both `AliveState.NeedsInit` and live, healthy
`AliveState.IsAlive` chickens count. Slot/global mismatches, dead/corpse units
and deletion states are removed. Loaded neutral chickens are assigned to the
nearest active granary using Chebyshev distance, then building ID and player ID
as deterministic tie-breakers. Relevant fields are unit type `+0x8A`, owner
`+0x92`, global ID `+0x94`, tile `+0xC0/+0xC2`, corpse flag `+0x29C`, and
building alive/type/owner/global/tile fields in the publicized `GameBuilding`
layout.

The Script Extender versions through the locally inspected 1.41.0 can report
the caller's saved `RBX` value instead of the native Hunter ID in
`OnUnitHunterQueryTarget`; see upstream work item 123. Improved Hunters installs
a temporary read-only context hook at the candidate-loop anchor `0x18AF70`.
At this point `R13 = UnitManager + hunterId * 0x490`, `R14 = UnitManager` and
`ESI` is the one-based candidate ID. The mod reconstructs and validates the
Hunter ID from the exact divisible delta and consumes it only for the matching
public query candidate on the same thread. If capture and the reported ID are
both invalid, the callback leaves Vanilla's target decision unchanged. Remove
this workaround once the minimum supported Script Extender fixes work item 123.

## Required update audit

1. Require one semantic match for both despawn patterns and verify that operand 1 at
   pattern offset `13` remains the signed 16-bit despawn duration.
2. Revalidate both automatic target dispatch tables, the chicken entry, the
   hunter-only acceptance/rejection branches and that explicit `AttackUnit`
   target assignment still occurs before automatic candidate dispatch.
3. Revalidate the Script Extender unit array and raw fields `+0x88`, `+0x92`,
   `+0x94`, `+0xC0`, `+0xC2`, `+0x29C`, `+0x2BC`, `+0x2C4`, `+0x370`,
   `+0x39A`, `+0x39C` and `+0x448`.
4. Confirm hunter/prey states, corpse flag, death timer, reservations, target
   IDs, coordinates, camel health and visual health refresh behavior.
5. Test automatic ranged rejection, explicit ranged `AttackUnit`, Hunter
   retargeting, stalled/blocked projectile compensation, resulting `0x6E` corpse
   pickup, line-of-sight recovery movement, corpse cleanup, camel health and
   chicken neutralization on fresh and loaded maps.
6. Revalidate the granary chicken function at `0xD29E0`, the comparison
   sequence at `0xD2AB4`, hook instruction at `0xD2ABF`, signed `jle` target
   `0xD2BA7`, spawn event path `0xD2B4C`, `rbx` player identity and native
   count field `[rdi+0x2048]`.
7. Update all reference RVAs and the dispatch-table map before approving
   the new shared hash.
8. Revalidate the Hunter query candidate-loop signature, `R13`/`R14` slot
   formula, `ESI` candidate ID and callback ordering before the public Extender
   event. Check whether upstream work item 123 is fixed and remove the temporary
   workaround when it is no longer needed.

## Temporary Hunter visibility diagnostic

Version 1.1.26 keeps the visibility investigation in the removable
`HunterVisibilityDiagnostic.cs` file. It installs no native hook and changes no
branch result or unit state. Version 1.1.25 briefly used behavior-neutral hooks
at RVAs `0x18EE14` and `0x130171`; the first real blocked-order test ended in a
native CTD before the hook's first confirmation marker. Those hooks and their
patterns were therefore removed completely rather than retained as fallbacks.

The diagnostic now correlates the public Hunter-query event, targets observed
by the existing 100-ms native unit scan and successful projectile-spawn events.
When that safe scan observes a Hunter in AI state `6`, it resolves the current,
recently assigned or recently accepted chicken by unit slot plus global ID. The
bounded log records movement at most every two seconds while positions remain
unchanged. It includes unit tile and world positions, elevation and look-at
coordinates, Hunter path/order fields, matching-projectile age, the straight
tile line's terrain-height range, and each building ID, type, owner, footprint
and occupied-line-tile count. The existing projectile-spawn event emits the same
line context for successful attack paths, providing a behavior-neutral control.

The diagnostic reads the already documented `GameUnit` layout fields at
`+0x88`, `+0x8A`, `+0x94`, `+0xC0/+0xC2`, `+0xF2`, `+0xF4`, `+0x2BC`,
`+0x398`, `+0x39A`, `+0x39C`, `+0x3FE` and `+0x448`. Because it adds no native
address, a future DLL update requires only the normal structure-layout audit.

## Hunter blocked-shot and line-of-sight recovery

Version 1.1.27 removes the one-second `KillUnit` compensation. That API enters
the melee-death path and produced animal state `0x6F`, which the Hunter did not
subsequently collect. A pending shot now stores Hunter, prey and projectile slot
plus global IDs. While that exact `ArcherArrow` is still alive, the runtime calls
the Script Extender's `GameUnitManagerAPI.DamageUnitRanged(victim, projectile)`
only after the arrow has stopped moving for 300 ms, is within 32 world units of
the target, or reaches its public projectile-delete pre-event. This re-enters
Vanilla's ranged damage/death path and is limited to three attempts. An unresolved
intent expires after five seconds without a synthetic kill. The validation also
requires the live Hunter source, live configured prey, matching projectile source
and target, owner/color-independent prey eligibility, and projectile source-player
consistency.

Pre-shot visibility recovery is isolated in `HunterLineOfSightRecovery.cs` and
adds no native address or hook. A target abort is eligible only for a live chicken
while the Hunter is in waiting state `6`, the straight tile line intersects an
active non-Hunter-hut building, three such aborts occur within three seconds, and
no matching Hunter projectile was created in the preceding two seconds. The
recovery searches at most eight tiles around the Hunter for an unoccupied tile
within a three-to-twenty-tile shot distance. Candidate firing lines must contain
no building at all—including Hunter huts, because arrows can physically collide
with them—and at most eight candidates are checked with the public Vanilla-aware
pathfinder. Movement is issued through `GameUnitManagerAPI.MoveToTile`; it does
not teleport the Hunter or bypass collision. The regular 30-second aborted-target
cooldown is suppressed only when such a move was actually issued, allowing the
Hunter to retry after arriving.

Future Script Extender updates must revalidate the public ranged-damage,
projectile-delete, pathfinding and move-order semantics. No new direct RVA or
pattern is introduced by these two recoveries; the established exact-hash RVA
and changed-hash unique-pattern strategy for the existing native features remains
unchanged.

## Audit for Steam build 24651686

Both complete patterns match exactly once. Their signed 16-bit immediate still
starts at pattern offset `13`; the surrounding animal state remains `0x6E` and
the death-timer field remains `+0x986` in the native manager-relative form.
The automatic target pattern also matches exactly once. Its encoded table RVAs
remain `0x18F9E0` and `0x18F9C4`; chicken entry `0x18F9F2` is `6`, dispatch
entries `0` and `6` remain `0x18F28E` and `0x18F3A6`, and the hunter-only case
still rejects non-hunters at `0x18F754`. Explicit `AttackUnit` still assigns its
target before the automatic dispatch.
The Script Extender initialized the same `0x490`-byte unit records, and targeted
native accesses reconfirmed the raw field map used by the runtime. Fresh/load
map behavior remains a post-build game smoke test.

The granary update and comparison signature were also audited on this hash.
The semantic sequence at `0xD2AB4` is unique, the compare remains
`cmp eax,[rdi+0x2048]` at `0xD2ABF`, its signed `jle` still targets `0xD2BA7`,
and the existing Script Extender granary spawn hook remains on the fall-through
spawn path at `0xD2B4C`.
