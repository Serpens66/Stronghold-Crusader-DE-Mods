# Player defeat observation audit [Scope: Vanilla + mod integration]

Audited native build: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

Reproduce the primary evidence packages from the workspace root:

```powershell
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function 0xCDE60
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function 0x1D160
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function 0x57330
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' callers 0x1D160
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' callees 0xCDE60
```

`FUN_1800cde60` (RVA `0xCDE60`, structurally verified) is the simulation-loop path that calls
`FUN_18001d160` (RVA `0x1D160`) before downstream player/lord processing in
`FUN_180057330` (RVA `0x57330`). `FUN_18001d160` is the normal runtime writer of the
player `r_WinLossState` and checks lord validity periodically; save loading is a separate writer
and therefore must be treated as baseline state rather than a new transition.

The relevant `GamePlayerResources` fields are relative offsets `r_LordUnitId = 0x21F8`,
`r_IsPaused = 0x2210`, and `r_WinLossState = 0x2244`. Player and unit game IDs are one-based.
The installed Script Extender exposes these records and unit identity/alive fields directly.

APIShared consequently observes all eight player records from `GameTimeManagerAPI.OnTick`
only after the first subscriber registers. The first tick after that registration is
baseline-only, including registration during an existing mission; earlier deaths/losses
are not replayed. Without subscribers, the callback performs no state/resource/unit reads.
This consumer-demand contract was approved and covered by service regression tests on
2026-10-11; the native simulation and ID contracts below remain unchanged.
The hook is pre-tick, so mutations made by the current native simulation step become visible on
the next tick. A living lord identity arms the early edge; disappearance, invalidation, or death
then emits once unless another living identity has already replaced it. Independently, entry into
`WinLossState.Loss` emits the official defeat edge, including lordless scenarios. The first
observation of every session is baseline-only, which prevents loaded saves from replaying events.

No additional native detour is installed: melee/projectile hooks omit other causes, unit deletion
is late, the game-over presentation callback is local and late, and the AI-state hook is not an
elimination contract. Tick observation covers Vanilla `KillUnit`, fire, scripts, and direct state
changes without adding another version-sensitive hook surface.

## Natural Lord elimination and building admission (2026-11-21) [Vanilla facts]

Reference canonical native SHA-256 reverified:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The ordinary Lord state machine `0x15A950` separates its one-shot death/reward
phase from terminal state `0x6E`. In that terminal state the counter must reach
33; the same synchronous invocation then marks the Lord for removal and calls
`0xCD190(owner)`. That helper invokes `0xC3F10(buildingManager, owner)` before
returning. `0xC3F10` visits every nonempty building owned by the player and calls
`0xC4290` on it, including keep and granary. `0xC4290` immediately writes life
state 3 (MarkedForDeletion), even though normal `0xC60F0 -> 0xB8310` slot cleanup
happens later. This is not a staggered sequence of future building-removal ticks.
Current-PE instruction decoding independently confirms call `0xCD1AD -> 0xC3F10`
and traversal call `0xC3F71 -> 0xC4290`.

`0xCDE60` reaches the public pre-tick boundary inside `0xD1AD0` before building,
player-result, AI and unit passes. No subsequent simulation pre-tick can intervene
between the Lord's terminal state transition and these synchronous building marks.
`0x1D160` periodically validates Lord ID/global identity; it does not equate the
start of the death animation with an invalidated identity. `0x57330` separately
checks the player's paused/result fields before ordinary AI scheduling. Normal
elimination does not need to clear the AI lineup for already marked keep/granary
records to fail a strict live-building admission check.

Scope: confirmed-static ordinary native Lord elimination, not a new in-game test.
A script/mod writing WinLoss.Loss alone without the native cleanup is a distinct
case and is not established by this path. Do not infer that raw IsAIPlayer is a
general alive-player predicate from the building-cleanup consequence above.
