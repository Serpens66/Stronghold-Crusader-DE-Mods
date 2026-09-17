# HUD presentation scheduling audit

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

## Limits and safe optimization boundary

No complete change event for arbitrary mod-provided category matchers or image/text resolvers was established. Visible active categories must continue to be evaluated at their existing render cadence. Explicit owner/registration activation is a separate managed contract, not inferred from whether a matcher currently returns false. Recruitment ticket expiry is maintenance work and must continue while the panel is hidden or its owner is inactive. Rendering, native IDs, public immutable snapshots and command handling remain independent.

The allocation comparison in `_inspect/Verify-HudResources.ps1` uses the previous/current production tint methods with managed Noesis stand-ins; it does not measure Unity frame time. Actual Noesis presentation across pause, panel recreation and map transitions still requires an in-game verification.
