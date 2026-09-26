# Fixes 1.19.1: multiplayer unit-type removal uses the wrong player's selection

In `src/shcde-fixes/Detours/UnitDetours.cs:385-409`, the hook creates a tribe for the command's `playerId` but reads `CurrentSelectedTribeId` from each peer's **local** selection. It assigns units from that tribe without checking their owner. This path is present in the installed Fixes 1.19.1.0 DLL.

**Observed:** On 26 September 2026 at 23:34:05, the host selected player 1's tribe 487 and assigned player 1's spearmen 46-48 to new tribe 6 (owner 1). For the same action, the client selected player 2's tribe 486 and assigned player 2's spearmen 49-51 to tribe 6. A passive Script Extender observer recorded `unitOwner=2 targetOwner=1 ownerMismatch=True unitTribeNow=6` for each unit and confirmed the state on the next tick. Host log lines 52302-52309; client lines 13525-13535.

**Reproduce:** Both players select their own troops; player 1 removes a unit type through the troop bar. In a comparison run with `AssignNewTribeOnUnitTypeTrim` disabled on both computers, the players repeated the action and observed no desync; the logs show no further wrong-owner assignment after startup (host `Log_148.log`, client `Log_016.log`). The option state and click come from the players' test notes, not the logs.

The wrong-owner assignment is proven. The observer records no call stack; other gameplay mods were active, so these logs do not prove that Fixes alone caused the displayed desync. The hook should derive units from the command's player and preserve one call to the original function when no suitable source tribe exists.
