# Deposit Restoration Test

The game package contains the plugin DLL, its optional PDB and `info.json`.
It uses Script Extender 2.14.4 and APIShared 0.62.0 or newer. Start a new
local map or use the map editor. The log marker
`DEPOSIT_RESTORATION_SESSION ... active=True` confirms the map has armed
the hooks. Use a disposable map and save to compare the following cases
with the same build of Vanilla.

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

The private fixture compiles the production emitter and runs it against private
memory for both resource flags, repeated placement, undo, disabled execution,
and the legal packed-index boundary. Build `tests/DepositHooks.Tests.csproj`
with MSBuild and pass the installed `CrusaderDE.dll` path to the resulting EXE.
The fixture checks that DLL's hash and the installed RedBird backend's
displacement and jump shape. It does not run a game session.
