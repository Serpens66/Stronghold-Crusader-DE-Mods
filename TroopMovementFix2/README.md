# Troop Movement Fix 2

TroopMovementFix2 is a separate, minimal successor to TroopMovementFix. The old
project remains unchanged, but both plugins must not be enabled at the same time.
The original TroopMovementFix already produces the intended in-game movement.
Fix2 exists only to reach the same result with fewer interventions and with as
much of the original Vanilla movement pipeline as possible.

## Behavior

- A normal move command keeps the incoming Vanilla movement mode and is not
  rewritten by the mod.
- Selection and Vanilla's tribe rebuilding remain unchanged. Immediately before
  rebuilding, Fix2 captures the current Vanilla movement-continuation state. If
  a rebuilt tribe contains only units with the same compatible state, the new
  tribe inherits that state during Vanilla's final selection-only template-copy
  routine, before any unit-type handler can observe it. The inherited state
  includes the native leader speed seeds, transition seed, free-speed flag,
  synchronized group-speed fields, patrol mode, and adjacent movement-
  continuation fields. Mixed or unknown states remain completely Vanilla.
- While selection rebuilding temporarily leaves an already moving unit in tribe
  zero, its unchanged Vanilla unit-type handler sees the unit's last valid
  movement tribe for that handler call only. Fix2 restores tribe zero immediately
  afterwards. This prevents Vanilla from mistaking the temporary selection state
  for free maximum-speed movement without skipping the type handler, changing
  selection membership, or special-casing unit types.
- The same tribe-level movement state is preserved at the selection boundary for an existing
  tribe which Vanilla continues to use without issuing assignment events. This
  happens, for example, when selected Archers stay in their current tribe while
  Vanilla changes that tribe from synchronized movement to running. Every living
  tribe member must have the same known movement state or Fix2 rejects the
  restore.
- After a completed real `DefaultInSync` order, Fix2 prepares the synchronized
  group speed and matching walk/run cadence as a dormant per-unit fallback.
  The fallback is exposed only while Vanilla temporarily leaves that concrete
  unit at tribe zero during selection rebuilding. As soon as the unit has any
  valid tribe ID, Fix2 returns no fallback and Vanilla remains authoritative.
  In particular, Vanilla's legitimate slowdown after deselection is not undone.
  `Fast`/Ctrl units never receive this fallback.
- Improved Spearmen no longer outrun slower members of a mixed synchronized
  group.
- Ctrl+move enables the same `freeUnitSpeeds` tribe state used by Vanilla's
  no-matched-speed wrapper. The ordinary movement value is left unchanged, and
  Vanilla itself calculates every selected unit's own maximum speed, cadence,
  terrain effects, and animation.
- Shift+Ctrl+move keeps Vanilla waypoint handling.
- Alt has no mod-specific behavior.

Per-unit movement snapshots are captured only after real orders and consulted
when Vanilla demonstrably recalculates those units after selection. Fix2 never
copies pre-selection speed bonuses, transition timers, or animations back into
units. The unit transition timer is logged only for diagnostics. The narrow
tribe-zero fallback uses the synchronized speed and cadence derived from the
completed real Vanilla order and becomes inactive immediately when Vanilla has
assigned a valid tribe. The compatible group-level Vanilla movement state is
captured at the selection boundary and inherited by the selection-rebuilt tribe
during its synchronous native initialization. Detailed diagnostics are capped
per selection.
Improved Spearmen additionally receive the same native speed/cadence correction
when a mixed synchronized order is first issued, because their late Vanilla run
bonus otherwise bypasses group synchronization.

Operational diagnostics are written at Info level with millisecond timestamps.

## Compatibility

- Requires the SHCDE Script Extender (`000shcdese`).
- Incompatible with the original `TroopMovementFix_Serp` plugin at runtime.
- No settings or localization files are required.

The proven native speed/cadence hook is compiled into this assembly from the
unchanged TroopMovementFix source tree. There is no runtime dependency on the
old plugin DLL.
