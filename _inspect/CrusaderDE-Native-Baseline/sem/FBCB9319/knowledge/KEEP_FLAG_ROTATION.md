# Keep Main-Flag Rotation Audit

## Provenance

- Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Primary keep-start function: `FUN_180094350`, VA `0x180094350`, RVA `0x94350`
- Projectile allocator reached by the keep path: `FUN_18009B2B0`, RVA `0x9B2B0`
- Runtime confirmation: Script Extender `2.8.0`, commit `5b4d48e732e9b6e2e93c135f0b28ce5b9d8bcd33`

## Native coordinate contract

The main Keep `Flag3` is spawned with identical source and target position and elevation. Its tile corner remains the north-east tile of the unrotated Keep footprint, but its micro-coordinate depends on the global map/view rotation in `DAT_1860ad40c`:

| View value | Micro X | Micro Y |
| --- | ---: | ---: |
| `0` or fallback | 7 | 0 |
| `2` | 7 | 7 |
| `4` | 0 | 7 |
| `6` | 0 | 0 |

The Keep orientation delivered to `BuildStructure(Pre)` is a separate value with the cardinal contract `0=South`, `2=East`, `4=North`, `6=West`; sentinel `15` is the default South orientation.

To preserve the flag's position relative to the Keep entrance for every view rotation, rotate the complete observed Vanilla micro-position around the square footprint. For local coordinates and `last = scale * 8 - 1`:

- `0`: `(x, y)`
- `2`: `(y, last - x)`
- `4`: `(last - x, last - y)`
- `6`: `(last - y, x)`

Accept only the four native micro-coordinate pairs on the north-east footprint tile and require stationary source/target equality. Any other Flag3 position remains fail-closed.

## Runtime observations

- `Crater Lake.map` produced micro `(7,0)` and was corrected and verified for all eight players.
- `The Ford Across the River.map` produced micro `(0,7)`, matching the native view-value-4 branch.
- A CastlePlanner restart with human orientation `2` and AI orientation `6` produced micro `(7,0)`; both corrected projectiles were found by their direct projectile array IDs and verified after initialization.

Projectile IDs are direct indices into `GameProjectileManagerAPI.GetProjectilesAsSpan()` with slot `0` reserved. `SourceWorldTileX/Y` and `TargetWorldTileX/Y` use micro-coordinates; `CurrentTileX/Y` use ordinary tile coordinates.
