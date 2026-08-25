# AI tower rebuilding and tower-ruin cleanup

## Scope and reference build

This document records the native analysis and runtime evidence used by
`FixAITowerRepair` and the related AI-defense options in ExtraFeatures.

- Canonical DLL: currently installed `CrusaderDE.dll`
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Analysis date: 2026-08-25
- All RVAs below apply to that exact DLL unless a signature is explicitly named.

## Relevant native paths

The observed behavior is split across several independent native systems:

- `0x7EB00` handles building damage. Its tower-destruction cases create the five
  tower-ruin types and can route a ruin through `0x7F6FA` to the deletion routine
  after further damage.
- Tower ruins use structure types 79 and 86-89. The building-update dispatch
  table at `0x2DEAE0` sends them to the empty updater at `0xACE90`. They therefore
  have no autonomous per-ruin lifetime timer.
- `0x51790` processes finished-castle AIV frames. This is the rebuild path
  observed when a match starts with the Vanilla "finished castles" option.
- `0x52270` is an alternate AIV processing branch reached in other AIV states.
- Both paths use the placement/obstruction helper at `0x5CD90`.
- `0x7B060` is the placement validator. A ruin can cause its caller-visible
  result 2 before `0x5CD90` later decides whether that blocker may be cleared.

The placement helper is not a tower-ruin-only function. It validates a planned
AI building footprint, examines buildings and other map occupants on its tiles,
selects one of two obstruction-cleanup policies and invokes Vanilla's own
deletion and tile-cleanup functions when the selected policy permits that.

## The distance calculation

For ordinary mappers, `0x5CD90` checks whether the player has a stored keep and
compares the target coordinates with the stored keep coordinates through
`0x79C0`. That helper returns:

`abs(targetX - keepX) + abs(targetY - keepY)`

The broad path is selected when this Manhattan distance is at most 20. It is
therefore a diamond-shaped area, not a square Chebyshev radius and not a
Euclidean circle. If no stored keep is available or the result is greater than
20, the narrow path is selected.

Four mappers always select the broad path regardless of keep distance:

- Hovel (mapper 54)
- Bedouin stockade (mapper 79)
- Wooden barracks (mapper 86)
- Stone barracks (mapper 87)

These exceptions and the varied blocker masks show that this distinction is a
general AI-placement clearance policy, not a special tower-ruin timer.

The exception list is not stored in an extensible data table. The current build
implements it as a hard-coded comparison chain at `0x5CEAB-0x5CEBD`: starting
from mapper 54, successive subtractions test 54, 79, 86 and 87. A match joins
the broad initialization at `0x5CF15`; `0x5CF23` sets the pass count to two and
`0x5CF2B` sets the broad-policy flag. Consequently, additional placing-building
mappers can technically be admitted, but doing so requires a native code hook
or patch at this policy-selection site. There is no spare list entry that can
be filled through data alone.

"Always broad" is not the same as guaranteed placement or literal permission
to erase every possible object. Any mapper value can be routed into this policy,
but the initial terrain/placement checks can still return a hard failure, the
protected structure mask still applies, and variants 12 or greater do not run
the shifted second pass.

This must be distinguished from extending the narrow blocker-type mask. Adding
a mapper to the hard-coded exception list gives that planned building the whole
broad policy, including its much larger deletion set and possible shifted
second pass. Extending the narrow mask admits only selected blocker structure
types while retaining the single original footprint pass. For targeted repairs,
the latter or an exact runtime classifier exception is substantially narrower.

## Narrow path outside the keep boundary

The narrow path normally scans one generated footprint. Its type mask
`0x3C0100038` permits native cleanup only for structure types:

- 3: woodcutter's hut
- 4: ox tether
- 5: iron mine
- 20: quarry
- 30-33: wheat, hops, apple and dairy farms

Every type above 33 is rejected before cleanup. Towers, gates, walls and all
five tower-ruin types are consequently preserved. This is why an ordinary
tower ruin outside Manhattan distance 20 can permanently block Vanilla's
matching tower rebuild.

## Broad path inside the keep boundary

The broad path enables up to two footprint passes. On its second pass,
`0x5CD90` changes the placement variant and shifts both coordinates:

- variants below 11 become `variant + 4`, with X and Y shifted by -2;
- variant 11 becomes 13, with X and Y shifted by -1;
- variants 12 or greater do not execute the second scan.

For occupied building tiles, types outside 40-73 go directly to Vanilla
cleanup. This includes tower types and tower-ruin types 79 and 86-89. Within
40-73, mask `0x380008007` protects only:

- keep types 40-42;
- campground type 55;
- keep-door types 71-73.

Most other blockers, including gates, generic towers, defensive and siege
objects, can be cleared. During the additional second pass, a blocker whose
owner has the same team number as the placing player is preserved. That
same-team check is not applied to the primary footprint pass.

The current-build instructions make the scope of that protection explicit. At
`0x5CFDB`, only pass index 1 enters the owner/team block. It reads the blocker
owner from `GameBuilding + 0x132`, compares the placing player's and owner's
entries in Vanilla's team array at `0x5D000-0x5D010`, and skips the blocker when
they match. Pass index 0 jumps directly to the structure-type classifier at
`0x5D016`. Enemy-owned blockers are not protected on either pass, and allied or
same-owner blockers are protected only on pass 1.

The earlier placement-validator call does not supply another ownership guard.
`0x5CD90` calls `0x7B060` at `0x5CE77` for every tile in the original footprint
and aborts only when the result is 1. When the structure grid contains a building
ID, the validator returns 2 at `0x7B1BB` without reading that building's owner.
That result deliberately lets `0x5CD90` continue into its own cleanup policy.

Therefore two nearby AIs can delete one another's unprotected buildings when a
broad-policy primary footprint overlaps them. If both AIVs continue retrying
their missing entries, the native logic permits a rebuild/delete cycle. Whether
it becomes a visible loop depends on the two AIV layouts and retry states, but
there is no general Vanilla safeguard against it. Only the protected structure
types and, on the shifted second pass alone, equal team numbers prevent cleanup.

A second audit of both AIV processors explains why proximity alone does not
normally produce an obvious cycle. `0x51790` and `0x52270` revisit a state-3 AIV
entry by reading the building ID at that entry's planned anchor and comparing
only the live structure type with the structure type expected for its mapper.
They do not compare the live building owner. A foreign replacement of the same
structure type therefore satisfies the old owner's AIV entry and suppresses its
rebuild, even though the replacement belongs to another player.

When the anchor is empty or holds a different structure type, the entry can
return to the placement path after its normal resource, availability and delay
checks. The cleanup return value is not used as a placement veto; the relevant
callers proceed to their building-creation path after `0x5CD90`. Conversely,
the cleanup functions at `0xC43A0` and `0xCFE90` delete/deregister the building
and update owner building lists and type-specific counters, but do not disable
the deleted owner's AIV plan entry. There is consequently no hidden general
foreign-owner guard, but there are several strong practical preconditions for
an alternating cycle:

- the two planned footprints must actually overlap; close keeps are insufficient;
- both deleted buildings must correspond to AIV entries that remain eligible for
  missing-building retries;
- the replacement must leave the other entry's anchor empty or occupied by a
  different structure type; equal structure types terminate the retry;
- each reciprocal placement must select the broad policy and pass all other
  placement, resource, availability and delay checks;
- neither blocker may be one of the broad mask's protected structure types, and
  the existing pass-1 same-team protection must not apply.

Thus Vanilla permits the cycle in a sufficiently adversarial pair of AIV
layouts, but it should not be expected merely because two AIs start very close
together. A test that only observes nearby castles without proving these exact
anchor, type, policy and AIV-state conditions does not exercise the cycle case.

A live test on 2026-08-25 did exercise at least one reciprocal conflict: an AI
barracks was placed, an enemy tower replaced it, and the barracks was then
rebuilt over the tower. This rules out a blanket ownership guard and also rules
out a one-step rule that permanently protects the first replacement. The
sequence stopped visibly after the barracks replacement, but that is not
evidence of a dedicated cycle breaker. The cleanup helper and deletion routines
retain no previous-blocker identity, conflict counter or alternating-owner
state, and a previous trace retried one permanently obstructed tower 49 times
with a median interval of 2690 ticks (67.25 internal game seconds).

The corresponding logged map was active only from approximately 22:34:52 to
22:36:26 wall-clock time, and ordinary overbuild events were not logged. A
further tower attempt could therefore have been pending behind the normal AIV
scheduler, resources, availability, enemy proximity or ExtraFeatures' optional
rebuild delay. In particular, after a damage-free cleanup deletion the first
missing-target attempt may start a positive rebuild delay; a later scheduled
attempt is then required after that delay expires. The observed three-step
sequence proves reciprocal permission, but neither proves an infinite loop nor
proves Vanilla cycle prevention.

A native exception can be added safely at the occupied-building decision: read
the already available placing player and blocker owner before either broad pass
reaches the type filter, and preserve a blocker when it belongs to a different
real player. A policy such as `owner in 1..8 && owner != placingPlayer` protects
both allied and enemy castles while still allowing an AI to replace its own
eligible buildings and, if desired, owner-0 neutral map objects. A same-team-only
extension to pass 0 would close the allied gap but would not prevent two enemy
AIs from overwriting one another. Unknown or invalid owner values should be
preserved fail-closed rather than treated as neutral.

Protecting every blocker that could itself select the broad policy is therefore
a conservative approximation, not the minimal anti-cycle rule. A precise rule
would additionally establish that the blocker belongs to a retryable AIV entry
whose planned anchor/footprint conflicts with the current entry and whose
expected structure type differs. That preserves intentional one-way overbuilding
in cases where the deleted building cannot reciprocally replace the new one.

For the requested policy that intentionally retains one-way enemy overbuilding,
a practical conservative guard is: preserve a foreign blocker only when its own
placement would itself receive broad cleanup permission. That is true when the
blocker's planned anchor is within Manhattan distance 20 of its owner's keep, or
when its placing mapper is one of the four unconditional exceptions. Merely
testing the live structure type is not always equivalent to testing the mapper;
the robust implementation should resolve the blocker to its owner's AIV entry
and mapper. This guard prevents the demonstrated tower/barracks conflict because
both sides are broad-capable, while still allowing broad placement to erase a
foreign blocker that can only use the narrow policy. It remains conservative:
a broad-capable blocker whose AIV entry is no longer retryable would be protected
even though it could not actually continue the cycle.

If no building ID occupies a footprint tile, both policies can additionally
inspect tile flags and remove certain other registered map occupants through
`0xB8ED0` or `0xB8E00`. The broad policy may reach more of these occupants due
to its additional shifted pass.

## Why the 20-step distinction must not be removed globally

The native helper has a player, mapper and target position, but no validated
active-AIV boundary. Manhattan distance 20 is apparently Vanilla's inexpensive
proxy for the central castle area. The broad policy is intentionally much more
destructive than the narrow policy.

Forcing the broad policy everywhere would not merely allow tower ruins to be
replaced. It could let distant AI placement attempts clear many economic,
defensive, preplaced, allied or hostile blockers and could run an extra shifted
cleanup pass. The safe repair is therefore to admit only the exact matching
runtime-created AI tower ruin in whichever native branch is already active.

## Current `FixAITowerRepair` behavior

The fix preserves the broad/narrow selection and all unrelated Vanilla rules.

1. AI-owned tower ruins created after map start are recorded from building
   spawn events. Preplaced decoration ruins are not admitted.
2. At both native classifier sites (`0x5D025` broad and `0x5D055` narrow), the
   callback validates building ID, global ID, AI owner, ruin type, anchor and
   the matching live tower mapper.
3. Only that exact ruin receives temporary classifier value 3. Both Vanilla
   masks already route type 3 to their existing cleanup block.
4. The real building type is never changed. Vanilla reloads it before calling
   its own demolition and tile-cleanup routines.
5. Human, enemy, preplaced, reused-ID, mismatched and unrelated ruins remain
   unchanged.
6. The mod does not call `DeleteBuildingSafe`, does not scan all ruins every 30
   seconds and does not create a tower directly.

The classifier hooks are active only when `EnableAiFixes` and
`FixAITowerRepair` are enabled. Disabling the feature disables both hooks and
leaves the native classifiers unchanged.

## Interaction with rebuild delay and enemy proximity

Ruin cleanup is intentionally independent of ExtraFeatures' rebuild delay and
enemy-radius setting. It happens when Vanilla next attempts the matching AIV
tower and reaches `0x5CD90`.

The tower itself remains subject to the configured rebuild rules. With a
60-second rebuild delay, the observed sequence can therefore be:

1. last confirmed damage anchors the rebuild timer;
2. a matching AIV attempt reaches the ruin and removes it through Vanilla;
3. ExtraFeatures still blocks tower creation until the damage-anchored delay
   expires;
4. a later regular AIV attempt creates the tower.

Removing the ruin does not start, reset or extend the rebuild timer. Native
placement may also require a later AIV call after cleanup even when no mod delay
is active, because cleanup and successful creation are distinct outcomes of the
placement process.

Enemy proximity is evaluated for the concrete rebuild target when tower
creation is eligible. It does not postpone clearing the ruin.

## Runtime evidence

Earlier traces proved damage-free Vanilla broad-path cleanup at ticks 6155 and
8255. A complementary trace proved that a type-89 ruin outside the boundary was
repeatedly rejected by the narrow classifier and remained in place.

The latest test session beginning at log line 48861 loaded BugfixesAndQoL
1.0.91 with both classifier hooks installed. It produced five explicit mod
routings and no ruin-hook failure:

| Player | Ruin | Anchor | Branch | Spawn tick | Routing/removal tick |
| --- | --- | --- | --- | ---: | ---: |
| 6 | Tower 4 ruin, global 7339846 | 521,290 | broad | 945 | 1525 |
| 6 | Tower 4 ruin, global 7346496 | 533,302 | narrow | 1822 | 2325 |
| 4 | Tower 4 ruin, global 7373392 | 642,435 | narrow | 7289 | 8102 |
| 3 | Tower 5 ruin, global 7395452 | 408,682 | broad | 9926 | 12323 |
| 8 | Tower 1 ruin, global 7435741 | 411,340 | narrow | 14368 | 14949 |

Each routing was immediately followed in the same simulation tick by a
`bulldoze-pre` removal with `duringDamage=False`. This proves that the deletion
was caused by the mod-assisted Vanilla placement cleanup rather than subsequent
damage.

The log also proves matching later replacements for routed ruins, including:

- player 6 at 533,302: removed at tick 2325, Tower 4 spawned at tick 5384;
- player 3 at 408,682: removed at tick 12323, Tower 5 spawned at tick 13344.

Other ruin removals in the same session occurred through additional damage or
unmodified Vanilla behavior and must not be attributed to this fix unless the
the same global ID has a debug-level `AI tower ruin routed through Vanilla ... cleanup`
entry when debug logging is enabled.

## Remaining verification

The native and runtime evidence now proves both classifier branches, selective
same-tick Vanilla cleanup and later tower replacement. Useful remaining tests
are behavioral rather than hook-discovery tests:

- a narrow-branch ruin that receives no further damage and is later rebuilt;
- a matching ruin while enemy proximity blocks tower creation, confirming that
  only creation waits;
- a preplaced or human-owned ruin on a possible footprint, confirming that it
  remains untouched;
- genuine host/client multiplayer with identical routing and rebuild ticks.
