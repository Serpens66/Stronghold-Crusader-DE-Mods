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
same global ID has an `AI tower ruin routed to Vanilla cleanup` entry.

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
