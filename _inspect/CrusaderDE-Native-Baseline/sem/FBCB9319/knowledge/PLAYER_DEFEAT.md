# Player defeat observation audit

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

APIShared consequently observes all eight player records from `GameTimeManagerAPI.OnTick`.
The hook is pre-tick, so mutations made by the current native simulation step become visible on
the next tick. A living lord identity arms the early edge; disappearance, invalidation, or death
then emits once unless another living identity has already replaced it. Independently, entry into
`WinLossState.Loss` emits the official defeat edge, including lordless scenarios. The first
observation of every session is baseline-only, which prevents loaded saves from replaying events.

No additional native detour is installed: melee/projectile hooks omit other causes, unit deletion
is late, the game-over presentation callback is local and late, and the AI-state hook is not an
elimination contract. Tick observation covers Vanilla `KillUnit`, fire, scripts, and direct state
changes without adding another version-sensitive hook surface.
