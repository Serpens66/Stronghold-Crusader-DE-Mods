# DLL_GameAction classes for a foreign-view observer [Scope: Vanilla + mod integration]

Native baseline: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Audit date: 2026-11-03. Static evidence from the semantic pseudocode and managed callers; no
runtime or multiplayer test. Evidence confidence per row: confirmed unless marked probable.

## Question [Vanilla facts]

Which `Enums.GameActionCommand` values act as the player in Vanilla's native viewpoint
(`0x88E3D70`, manager `+0x36FA50`) when the local spectator flag (`0x3666080`) is set and the
viewpoint shows another player than the local participant? The local command queue `0x23990`
takes its network source from that viewpoint (see `PLAYER_CONTROL_CONTEXT.md`).

## Dispatch facts [Vanilla facts]

- `DLL_GameAction` (RVA `0x81870`) switches over 1001..1073 (cases `0x3E9..0x431`); 2000..2026 go
  through the default to `0x80770`. `0x7DE` is a case there without an enum value.
- Only `Undo` (`0x40C`) checks the spectator flag itself (early return).
- Every enqueue goes through `0x23990`; no branch takes its source from elsewhere.
- Per-player record: `0x379ADxx + view*0x583C`; selected building `DAT_1867e8394`.
- Values below 1001 belong to the separate managed overload `GameAction(KeyFunctions)` and are
  outside this table (e.g. MPPing `0xCA` enqueues op `0x66` as the view; GroupTroops changes
  control groups via `0xD0300`/`0xD0F80`).

## Classes [Vanilla facts]

**Acts as the viewed player (P):**

| Kind | Commands (value, evidence) |
|---|---|
| Enqueue as view | GateHouseState 0x3E9 (`0xD6620` op 0x2D), RepairBuilding 0x3EA (`0xD4EE0` op 0x44), ToggleSleep 0x3EB (`0xD6F00` op 0x2B), SetTaxRate 0x3EC (writes +ADE4; enqueue via `0xD6DE0` op 0x22), SetRationing 0x3ED (`0xD6790` op 0x23), SetFoodEaten 0x3EE (`0xD6590`), MakeTroop 0x3EF (op 0x1F), DrawbridgeState 0x3F0 (`0xD6520` op 0x25), Troops_Stop 0x3F2 / Troops_Disband 0x3F3 (`0x90510` → `0x199B50` op 0x24), Troops_ChangeStance 0x3FC (`0x11E860` op 0x46), Troops_DeSelectType 0x3FD / Troops_DeSelectExceptType 0x3FE (op 0x0F), SetNextWeaponMade 0x3FF (`0xD66B0` op 0x21), BuyGoods 0x400 / SellGoods 0x401 (`0xD7080` op 0x26), AmmoRecharge 0x411 (`0x191DC0` op 0x56), Autotrade_Pause 0x41A / Autotrade_Apply 0x41E (op 0x6D), Ally_* 0x425..0x42A (`0xD7520` op 0x71), ExtremePower 0x42C (`0x105510` op 0x77) |
| Arms cursor/placement as view (probable) | Troops_AttackHere 0x3F4, Troops_Cow 0x3F5, EngBuild_* 0x3F6..0x3FA, Troops_Patrol 0x3FB, EngBuild_ArabBallista 0x424 |
| Direct write to view/building state | SetCurrentTradedGood 0x403 (+ADF8), FreeBuild_Event 0x407 (`0x1045F0`, probable), SH1Cheats 0x414 (SP modes 0/99 only), SelectBuildingType 0x417 (opens the view's building panel via `0x89F40`, writes mirrors), SetOutpostState/Size/Delay 0x41F..0x421 (no queue) |

**Does not act as the viewed player:**

- Local presentation/camera/options: OpenDogCage 0x3F1 (no-op), CycleBookmarks 0x402, RadarClicked 0x404,
  AlignSprites 0x405, ResizeRadarMapBuffer 0x406 (automatic), ActionPointClicked 0x40B, CentreMarker 0x413,
  HideObjectiveProgress 0x415 (automatic), SetRadarMapRotationType 0x416, Game_Paused 0x418, RotateBuilding 0x419,
  GenieSpeech 0x422, LordType 0x423, ShowPlannedMoat 0x42B, SkirmishInsult 0x42D, ResetTime 0x42F, SpectatorMode 0x431.
- Autotrade staging globals until Apply: Autotrade_OnOff/SetBuy/SetSell 0x41B..0x41D (partly called
  directly by `EngineInterface.SetAutoTrade`, bypassing the managed broker).
- Player-neutral or editor globals: FreeBuild_Invasion* 0x408..0x40A, SetStartingTeam 0x40D,
  Set_AI_Patrolling 0x40E, Fix_Tribes 0x40F, AI_Target_Special 0x412, MPConfig 0x42E, SandsCheatAllowed 0x430,
  Scenario_Set_* and SetMarkerState (2000..2025); EditorWipeGoods, RespawnLord, WipeAnimals, HoldTime,
  FastGoodsFeedin, Balanced run only with editor flag `0x3665F28 == 1`.
- Self-guarded: Undo 0x40C.
- CloseTroopsPanel 0x410 (`0x897F0`): sent by ESC (`EditorDirector.EscapeCloseUIPanel`) and the Reports
  button (`MainViewModel.ButtonReports`); enqueues a deselect (op 0x0F, source = view) only while the local
  selection count `0x67E8420` is above zero, then clears local marking.

## Open uncertainties [Vanilla facts]

1. `DLL_RunTick` (`0x86680`) calls `0x97C70` after restoring the view; in app mode `0x10` sub-mode 2 it
   reaches `0x982A0` → `0xD6DE0`, which enqueues op 0x22 as the view whenever view `+ADE4 != +ADF0`. An open
   tax panel under a foreign view could therefore send a tax change; whether the fields can differ is not
   verified. Sub-modes 4, 0x23, 0x2C, 0x3F and 0x46 of `0x97C70` are not audited.
2. `setUpInbuilding` and `FatControler.NoesisGUIUpdateChecksInGame` send Autotrade_Apply/Pause automatically
   while the auto-trade panel is visible.
3. Mirror writes (+ADF8, +ADE0, +ADE8, +AE04) of 0x403/0x417/`0x89F40`: checksum/save relevance not verified.
4. Troops_AttackHere's `0x8BD60(0x42)` path is only partly traced (its 0x123 branch enqueues op 0x56).
5. The real dog-cage action uses another export (0x3F1 is a native no-op); not audited.

## Spectator observer command policy [mod integration]

`Spectator/src/SpectatorObserverActionPolicy.cs` blocks the P class for a confirmed eliminated
observer, only for local input on Unity's main thread; synchronized simulation/Chore calls are never
blocked. Every other value of this table is allowed, including CloseTroopsPanel: Spectator clears the
participant's selection once (CloseTroopsPanel while the native view is still its own) before confirming
the observer role, and Vanilla's spectator flag suppresses new map selection. Values outside this audited
enum are blocked and logged. Other mods can allow single commands through APIShared
`ObserverCommandExceptions`. The KeyFunctions overload (MPPing, GroupTroops) is not covered by the managed
GameAction broker.
