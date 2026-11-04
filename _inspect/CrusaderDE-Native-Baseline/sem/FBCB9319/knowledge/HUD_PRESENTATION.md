# HUD presentation scheduling audit [Scope: Vanilla + mod integration]

## Provenance

- Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (installed DLL verified).
- Managed SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789` (installed assembly verified).
- Script Extender: installed assembly 2.6.0.0; local tag v2.6.0, commit `2cee24e33b5a5d81d1c275efabc714ac59917b7b`, tree `9cfb59b7b531553b23709b90b3cc3f10b0615cc1`, matching baseline provenance.
- Scope: scheduling, allocation and visibility of APIShared unit presentation and Lord action controls. No replacement of native selection, recruitment, counting or command semantics.

## Native-to-managed data flow

`DLL_RunTick` RVA `0x86680` / VA `0x180086680` is a confirmed export. It accepts the paused flag and caller-owned output buffers, performs the relevant input/render preparation outside the unpaused simulation branch, and calls RVA `0x19D960` with the render manager, PlayState output buffer and separate selected-unit buffer. The latter function retains its baseline candidate name `FUN_18019d960`; the following roles are inferred from its writes and matching managed field offsets, not promoted to a globally confirmed function name.

- Output byte offset 100: signed 32-bit selection count. The separate buffer contains interleaved signed 32-bit game ID/type pairs. Its native loop begins at ID 1. `CopyPlayStateStruct` copies these to separate `selectedChimps` / `selectedChimpTypes` arrays without changing the IDs.
- Output byte offsets 812..879: 34 signed 16-bit troop counts. `CopyPlayStateStruct` retains that width and signedness; `UpdateSHTroopsData` maps slot `i - 1` to `AllTroops[i]`, for `i = 1..34`.
- The relevant count cache is rebuilt through RVA `0x19CDE0` / VA `0x18019CDE0` (baseline candidate name). The observed caller increments a counter and refreshes when it exceeds 40. The helper clears its per-type counter storage and traverses unit records with native state, owner and exclusion checks. This is not identical to APIShared's live-category scan and must not be replaced by an assumed per-tick category count.
- In-building unit-detail mode writes `in_chimp` at byte offset 956 and its displayed type at 960; mode-dependent branches can disguise the displayed type. APIShared's existing native identity validation remains separate and unchanged.

Managed path: `EngineInterface.run` -> `DLL_RunTick` -> `CopyPlayStateStruct` -> `MemoryBuffers` -> `Director.Update` -> `GameMap.processTestMap` -> `GameData.setGameState` -> `FatControler.NoesisGUIUpdateChecksInGame`. Director also performs GUI checks after an elapsed-time interval without a newly rendered simulation buffer. Script Extender `OnTick` explicitly does not fire while paused.

## Presentation writers, readers and lifetime

- `HUD_Troops.SelectedTroops` runs both selection setup and action setup. `SetuptroopActionsUI`, `SelectedEngiBuild` and `ShownAttackHereOrder` write the Lord-relevant button visibility. Comparing only the mod's last desired visibility would miss subsequent Vanilla writes; compare actual control values.
- Vanilla selection type counting excludes Lord type 55. APIShared keeps its existing selection identity check and managed setup hook; this audit does not justify replacing them with one input event.
- `MainViewModel.setUpInbuilding` hides the building subpanels and selects the requested panel. Army modes 76/68/67/66 use four separate panels, all consuming `AllTroops`. APIShared's custom category host is attached to the first page, but corrections to base-type counts also affect the other pages.
- `FatControler.NoesisGUIUpdateChecksInGame` calls `UpdateSHTroopsData`, which only writes changed raw counts. Category deactivation therefore needs explicit restoration from the latest raw counts, even if those raw counts did not change.
- In detail mode 70 the GUI check rewrites `ChimpTypeText`. Category text needs repeated correction while active, and conditional restoration of the observed underlying value when disabled.
- `UpdateUITroopSprites` writes the image slots owned by registered image overrides. With valid context, deactivate by applying the original sprite update followed by remaining active overrides. After map unload, without a usable player context, conditionally restore captured originals only where the exact mod image is still present.
- `HUD_Troops` and `HUD_Buildings` initialize named controls in their constructors and publish themselves to MainViewModel. Cache controls by panel identity and only publish complete successful resolutions. `viewModelLoaded` precedes `HUDmain` readiness.

## Limits and safe optimization boundary [mod integration]

No complete change event for arbitrary mod-provided category matchers or image/text resolvers was established. Visible active categories must continue to be evaluated at their existing render cadence. Explicit owner/registration activation is a separate managed contract, not inferred from whether a matcher currently returns false. Recruitment ticket expiry is maintenance work and must continue while the panel is hidden or its owner is inactive. Rendering, native IDs, public immutable snapshots and command handling remain independent.

The allocation comparison in `_inspect/Verify-HudResources.ps1` uses the previous/current production tint methods with managed Noesis stand-ins; it does not measure Unity frame time. Actual Noesis presentation across pause, panel recreation and map transitions still requires an in-game verification.

## Shared managed publishers audited 2026-10-10 [mod integration]

Real Assembly-CSharp hash BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789:
APIShared 0.6 owns GUI checks, HUD_Main.UpdateRollover and MainViewModel recruitment/
troop-panel hover and recharge interception. All eight managed signatures (including
EngineInterface.GameAction(GameActionCommand,int,int,int)) are checked mechanically
by APIShared/tools/Validation/Verify-ManagedInterceptionContracts.ps1. Four
MainViewModel targets are private and used only through validated detour trampolines.
Shared Post follows the existing original chain; it is a managed observation and
cannot acknowledge native Chore execution. Startup plugin cleanup does not remove
these statically rooted hooks. GUI Post includes the extender's original-chain UI
augmentation. Deferred replacement UI actions execute only after all Pre vetoes.

## Patrol active-button tooltip audited 2026-10-27 [Vanilla facts]

- Confidence: confirmed static managed/XAML control and data flow; in-game reproduction was reported by the user, but the corrected UI has not been exercised in-game by this audit.
- Installed Native SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` and Managed SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789` were rechecked on the audit date and match CURRENT.json / DATABASE_INFO.json. This defect is in managed Vanilla UI code; no native RVA or memory-layout assumption is needed for the correction.
- [HUD_Troops.xaml](../resources/xaml/Assets/GUI/XAMLResources/HUD_Troops.xaml) lines 391-446 defines two overlapping buttons, `UnitPatrol` and `UnitPatrolActive`. Both invoke `ButtonUnitPatrolCommand`; their MouseEnter actions pass their own button Name to `ButtonTroopPanelMouseEnterCommand`.
- [MainViewModel.ButtonUnitPatrol](../managed/BC8B6A39/decompiled/CrusaderDE/MainViewModel.cs) (token `0x0600152E`, lines 36952-36963) calls `EngineInterface.GameAction(Troops_Patrol, 0, 0)` and toggles the managed byte `lastGameState.troops_patrol_mode` between zero and one. [FatControler.NoesisGUIUpdateChecksInGame](../managed/BC8B6A39/decompiled/FatControler.cs) lines 1249-1260 shows the active button for a nonzero mode and the ordinary button for zero when `PatrolShouldBeVisible` is true in the troop-control app modes. HUD setup initially shows the ordinary button; siege-build selection hides both.
- `MainViewModel.ButtonTroopPanelMouseEnter` (token `0x06001541`, lines 37099-37390) handles only `UnitPatrol` (lines 37179-37183), assigning the localized title, troop-help description and the two-line layout. There is no case for `UnitPatrolActive`. An active-button hover therefore clears the auxiliary amounts/image, leaves `TroopsPanelRollover` unchanged and shows the single-line layout because its local layout flag remains false. The displayed title is the last title stored by the troop-panel rollover handler, including any intervening button hover.
- `ButtonTroopPanelMouseLeave` (lines 37392-37396) hides both rollover text blocks without clearing their bound text. Both XAML layouts bind their main Run to the same `TroopsPanelRollover` property (lines 77-133). Returning to the ordinary Patrol button restores the existing valid handler path; the defect follows the active control rather than permanently corrupting the tooltip after one click.

## Patrol tooltip correction [mod integration]

BugfixesAndQoL's existing `Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml` sets only the active button's MouseEnter `InvokeCommandAction.CommandParameter` to the literal `UnitPatrol`, using the Script Extender's `SetAttribute` operation. This reuses Vanilla translation, description and layout handling while retaining the button Name, click command, MouseLeave and visibility/state logic. The asset patch has no additional setting or runtime callback. Validation applies the operation in memory to both baseline Vanilla XAML and the local Extender override, requiring exactly one match and equality with the original document after restoring the original parameter. No build or installation is part of this change; actual Noesis/game acceptance remains pending.
