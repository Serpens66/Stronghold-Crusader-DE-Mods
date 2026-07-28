# Troop Movement Fix 2

TroopMovementFix2 is a separate, minimal successor to TroopMovementFix. The old
project remains unchanged, but both plugins must not be enabled at the same time.
The original TroopMovementFix already produces the intended in-game movement.
Fix2 exists only to reach the same result with fewer interventions and with as
much of the original Vanilla movement pipeline as possible.

## Behavior

- A normal move command keeps the incoming Vanilla movement mode and is not
  rewritten by the mod.
- Selection is currently left completely Vanilla. Version 1.0.3 observes both
  world/mouse selection and bottom-bar/UI selection paths so the later Vanilla
  speed recalculation can be identified without rewriting movement state.
- Improved Spearmen no longer outrun slower members of a mixed synchronized
  group.
- Ctrl+move enables the same `freeUnitSpeeds` tribe state used by Vanilla's
  no-matched-speed wrapper. The ordinary movement value is left unchanged, and
  Vanilla itself calculates every selected unit's own maximum speed, cadence,
  terrain effects, and animation.
- Shift+Ctrl+move keeps Vanilla waypoint handling.
- Alt has no mod-specific behavior.

Movement snapshots captured for selection diagnostics are read-only and are
never written back. Detailed diagnostics are capped per selection. A native
speed/cadence directive is used exclusively for Improved Spearmen in mixed
synchronized groups, because their late Vanilla run bonus is the behavior being
fixed.

Operational diagnostics are written at Info level with millisecond timestamps.

## Compatibility

- Requires the SHCDE Script Extender (`000shcdese`).
- Incompatible with the original `TroopMovementFix_Serp` plugin at runtime.
- No settings or localization files are required.

The proven native speed/cadence hook is compiled into this assembly from the
unchanged TroopMovementFix source tree. There is no runtime dependency on the
old plugin DLL.
