# Troop Movement Fix 3

TroopMovementFix3 is a deliberately small movement fix.

## Behavior

- Ordinary movement commands remain Vanilla.
- Improved Spearmen are limited to the slowest normal maximum speed of a mixed
  `DefaultInSync` group. Their cadence and walk/run animation are matched to the
  group so their late Vanilla running bonus cannot make them overtake slower
  members.
- `Ctrl+move` sets Vanilla's existing `freeUnitSpeeds` tribe field. Vanilla
  then calculates every unit's own maximum speed, cadence, terrain effects, and
  animation.
- `Shift+Ctrl+move` retains Vanilla waypoint handling.
- Alt has no mod-specific behavior.

Selection and tribe rebuilding are intentionally not observed or corrected.
Vanilla may therefore change an already moving group's speed after selection,
deselection, bottom-bar filtering, or a tribe rebuild. This limitation is
accepted by design.

## Scope

The mod has no settings, UI, localization, selection hooks, tribe-rebuild
hooks, movement snapshots, or per-frame scans. It only reacts to real movement,
target/attack/stop, unit-delete, and map-unload events.

Operational diagnostics use Info level and include millisecond timestamps.

## Compatibility

- Requires the SHCDE Script Extender (`000shcdese`).
- Incompatible at runtime with `TroopMovementFix_Serp` and
  `TroopMovementFix2_Serp` because their native movement hooks overlap.
