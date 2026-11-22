# Wall deposit restoration integration

Move `src/DepositRestoration.cs` and `src/DepositHooks.cs` to the
receiving mod. Retain a process-owned instance of
`DepositRestorationRuntime`, constructed on Unity's startup thread
with the receiving mod's GUID and logger. Call `Install(context)`
from the receiving mod's native library callback. The standalone
`Plugin.cs` is the existing integration example.

Keep the archive identifier and schema in the runtime. They read
editor maps and saves written by version 0.1.0. Ship a single owner
of the native spans and archive identifier. Remove the standalone
package when moving the feature into the receiving mod.

The runtime uses Script Extender 2.14.4 and APIShared 0.63.0 or newer.
It registers an atomic simulation record and a required multiplayer
capability. All peers use the same feature registration and compare
the matching saved metadata before loading. The fix has no
player-local setting, timer or network command.

The common native repair lifecycle suspends capture and restoration,
then clears private history on every peer before resuming. This can
lose the origin of a wall that was standing before a resync. The
original terrain is still available for deposits exposed before
the repair and for walls built afterwards.

## Acceptance

Use a disposable map with stone, iron and ordinary dirt. The
timestamped mission message confirms activation for the current mode.

| Case | Expected result with the testmod |
| --- | --- |
| Build a high wall, a low wall, a crenel, and a connecting staircase with fresh tiles crossing each deposit | The wall occupies the tiles as usual. After removal, the original ore type returns per tile. |
| Include ordinary dirt in every test line and repeat the build and demolition | Only the original resource cells restore. Repeat construction and demolition gives the same result. |
| Cover multiple adjacent stone and iron tiles, then demolish a subset | Only removed sections reveal their respective deposits. |
| Undo a new wall and undo a repair of an existing wall | The first action reveals the original resource. The second preserves history until final wall removal. |
| Attack a covered wall until it collapses, or remove a covered AI wall after defeating its owner | The uncovered mineral reappears after the wall is gone. Partial damage leaves the wall and its history intact. |
| Save a covered deposit before demolition, then load and demolish | The same terrain restores after reload. Repeat with an editor map save. |
| Place and remove a tower, gatehouse, or legal resource building on test terrain | Compare the exposed terrain to the original and to Vanilla. Verify quarry and mine placement on the recovered deposits. |
| Load an older Vanilla save whose wall already erased a deposit | No resource is inferred under that wall. New placements on intact deposits still track normally. |
| Host and client each build and demolish wall cells on both deposits | Both see the same tile flags after the orders and after later checksum checkpoints. |
| Load a shared multiplayer save with covered cells, then demolish | Both peers restore the same original deposits. A mismatching mod record must prevent the shared load. |
| Exercise an actual multiplayer repair and then place and remove new walls | The peers retire the old history and resume recording on the same repair boundary. |
