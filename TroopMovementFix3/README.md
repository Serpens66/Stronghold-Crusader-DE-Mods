# Troop Movement Fix 3

TroopMovementFix3 is a deliberately small fix for synchronized movement of
mixed unit groups.

## Behavior

- A genuine new `DefaultInSync` order scans the living tribe members once.
- Homogeneous groups remain completely Vanilla.
- Mixed groups use the slowest member's individual Vanilla maximum speed. In
  the game's delay-based speed representation, this is the largest
  `r_CurrentSpeed` value in the group.
- The result is written once to Vanilla's tribe `MovementSpeed` field.
  Vanilla still applies terrain and state penalties, so an affected unit can
  become slower when the game requires it.
- The move type remains unchanged and no per-unit speed directive is created.
- If every member type has a detected native running animation, the group uses
  a common running cadence. If one member cannot run, the complete group uses
  a common walking cadence.
- With the official Improved-Spearman option disabled, Spearmen count as
  walking-only. With it enabled, they use the ordinary Archer-equivalent
  Vanilla walk/run decision.
- The Improved-Spearman option's separate Vanilla combat effect is not
  modified.
- `Ctrl+move` removes an existing synchronization and sets Vanilla's
  `freeUnitSpeeds` tribe field. Every unit then uses its own maximum speed.
- `Shift+Ctrl+move` retains Vanilla waypoint handling.
- Target, attack, stop, later selection-driven tribe assignment, and a new
  movement order invalidate the remembered synchronization.

Selection-driven tribe rebuilding intentionally returns control to Vanilla.
The mod does not attempt to preserve an earlier group synchronization across
later selection or filtering.

## Scope

The full group scan runs only once for a genuine new mixed
`DefaultInSync` order. There is no `Update()`, timer, coroutine, periodic group
scan, movement-speed detour, or recurring write to `r_CurrentSpeed` or
`r_CurrentSpeed2`.

Vanilla's unit-type handlers overwrite cadence and walk/run animation shortly
before the common movement cadence calculation. One small native callback at
that common point therefore corrects only registered tribes:

- synchronized running uses one shared `r_SpeedBonus`, selected from the
  slowest unit type's statically detected native running cadence, plus each
  type's detected matching running animation;
- synchronized walking uses `r_SpeedBonus = 0` and the detected matching
  walking animation;
- unregistered tribes return after one dictionary lookup without a unit scan.

Native type bonuses are resolved only during the one-time movement-order scan.
If multiple limiting types have the same maximum-speed delay, the smallest of
their native bonuses becomes the shared value. This keeps faster types such as
Archers on the Assassin cadence instead of either accelerating the Assassin or
pulling ahead of it.

The existing Spearman fix remains a transactionally installed native inline
stub. Runtime subscriptions and native hooks are held for the complete process
lifetime because the BepInEx manager object is destroyed shortly after startup
in this game.

Operational diagnostics use Debug level and include millisecond timestamps.
Warnings and errors remain visible at their corresponding levels.

## Compatibility

- Requires the SHCDE Script Extender (`000shcdese`).
- Incompatible at runtime with `TroopMovementFix_Serp` and
  `TroopMovementFix2_Serp` because their native movement hooks overlap.
