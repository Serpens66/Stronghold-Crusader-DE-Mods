# Troop Movement Fix 3

TroopMovementFix3 is a deliberately small movement fix.

## Behavior

- Ordinary movement commands remain Vanilla.
- With the official Improved-Spearman option disabled, Spearmen retain their
  Vanilla walking-only behavior.
- With the option enabled, its special movement path is replaced by the same
  Vanilla walk/run decision used by Archers. Consequently, synchronized mixed
  groups make both types walk, while later Vanilla selection and tribe changes
  affect both types consistently.
- The Improved-Spearman option itself remains enabled. Its separate Vanilla
  combat effect is not modified.
- `Ctrl+move` sets Vanilla's existing `freeUnitSpeeds` tribe field. Vanilla
  then calculates every unit's own maximum speed, cadence, terrain effects, and
  animation.
- `Shift+Ctrl+move` retains Vanilla waypoint handling.
- Alt has no mod-specific behavior.

Selection and tribe rebuilding are intentionally not observed or corrected.
Vanilla may therefore change an already moving group's speed after selection,
deselection, bottom-bar filtering, or a tribe rebuild. Spearmen now follow the
same movement decision as Archers when that happens.

## Scope

The Spearman fix is one transactionally installed, purely native inline stub.
It reads Vanilla's existing Improved-Spearman option flag directly. Disabled
Spearmen jump to the original walking block; enabled Spearmen use the
Archer-equivalent decision. It has no managed callback, unit lookup, group
scan, stored directive, speed write, cadence hook, animation hook, selection
hook, tribe-rebuild hook, snapshot, or per-frame work.

The only managed runtime event handles genuine movement commands and writes
Vanilla's `freeUnitSpeeds` field when Ctrl is held. Normal movement-command data
is not changed.

Operational diagnostics use Debug level and include millisecond timestamps.
Successful movement commands do not produce log messages. Warnings and errors
remain visible at their corresponding levels.

## Compatibility

- Requires the SHCDE Script Extender (`000shcdese`).
- Incompatible at runtime with `TroopMovementFix_Serp` and
  `TroopMovementFix2_Serp` because their native movement hooks overlap.
