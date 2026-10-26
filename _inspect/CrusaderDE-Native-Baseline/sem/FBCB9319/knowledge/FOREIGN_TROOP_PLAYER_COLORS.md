# Foreign troop HUD player colors [Scope: Vanilla + mod integration]

## Provenance and audited flow

- Installed native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Installed managed SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
- `DLL_GetColourMapping`, RVA `0x83440`, is a confirmed export with managed signature `void(int*, int)`. It normalizes invalid native color slots, fills unassigned slots, copies nine signed Int32 values (including neutral slot zero) to the caller's buffer, and stores the requested player remap. It is not a side-effect-free query for a HUD refresh.
- Managed new-map, editor and successful save-load wrappers call that export and pass its output through `GameMap.setColourMapping` to `SpriteMapping.SetRemapColours`. Failed save loads do not publish a successful mapping.
- `EditorDirector.postLoading` then applies player selection via `BuildPlayerColourMapping`, or saved radar colors via `BuildSkirmishLoadColourMapping`. Multiplayer roots perform additional mapping after `postLoading`; delayed clients complete this setup in the incoming-message path. The mission lifecycle's outer completion boundary is required, not an early native load callback.
- `BuildMultiPlayerColourMapping` has ordinary lobby and loaded-player variants. `RemapMPLoadedColour` returns its input if no loaded mapping exists or the input is outside that mapping; otherwise it returns the mapped player slot.

## Distinct color domains

Vanilla player labels (`HUD_MPChatMessages`, allies, on-screen text) resolve an owner with:

    SpriteMapping.remapColours[SpriteMapping.RemapMPLoadedColour(owner)]

The resulting material palette order is red, blue, orange, yellow, purple, black/gray, light blue, green. `SpriteMapping.SetBodySprite` applies `remapColours` before `spriteLoader.GetGMMaterial`; the latter reads the selected GM's color array. Some GM files have special palettes, so the canonical player tint is read using `Enums.GM.GM_BODY_ARCHER`, which uses `defaultColours`, with zero transparency.

Portrait resources have a different order: red, orange, yellow, blue, black/gray, purple, light blue, green. Vanilla converts material palette indices using `Platform_Multiplayer.Instance.UIColourRemap` (`0,1,4,2,3,6,5,7,8`) before `UpdateUITroopSprites`. Do not apply this conversion to material tints.

`SpriteMapping.remapColours`, `RemapMPLoadedColour`, `Platform_Multiplayer.Instance`, `UIColourRemap`, `spriteLoader.instance` and `GetGMMaterial` were checked as public against the installed real managed assembly using ILSpy. No publicized-member access or new native hook is needed.

## BugfixesAndQoL consumer [mod integration]

Previously each owner/type group copied the first unit's `r_SpritePlayerColorId` directly into both the label palette and portrait lookup. Ownership and a sprite's color field are distinct; grouping by owner does not ensure equal sprite fields. The mod's UI-order label palette also did not implement the current Vanilla player mapping. This permits inconsistent P2 labels; the screenshot alone does not establish individual units' actual stored values.

`ForeignTroopPlayerColors` resolves each owner once within the existing main-thread render refresh. A reset before every capture pass and on the existing session/HUD reset path prevents mapstart colors from becoming permanent. Entries retain the resolved portrait ID and ARGB tint separately; view copies and draw comparisons include both, so unchanged selections update when the player mapping changes. Neutral/invalid owners or temporarily missing mapping dependencies use a neutral label and base artwork, retried on the next refresh. The shared Lord portrait helper keeps its existing UI-order contract.

There is no new simulation write, JSON dependency, native call or publisher. Static mission observers and `Application.onBeforeRender` remain the long-lived roots, including during pause. The foreign HUD's existing readiness and mode gates remain authoritative for editor/spectator presentation. The local Fixes source search found no competing ownership of these mapping functions.

## Validation limits

`BugfixesAndQoL/tests/ForeignTroopHud/Test-PlayerColors.ps1` exercises the production resolver using isolated managed stand-ins, and checks runtime/view source integration. It covers all eight palette conversions, per-owner memoization, refresh/reset changes, loaded-player remapping, neutral owners and readiness retries. It does not validate actual Noesis controls, sprites, host/client timing or visual accuracy inside the game.

No mod build, installation or in-game test was performed for this change, per user request. Runtime acceptance remains pending: screenshot selection, all eight colors, unchanged selection after a color change, new map/save, MP host/client remapping, editor/spectator, pause, pagination and HUD recreation.
