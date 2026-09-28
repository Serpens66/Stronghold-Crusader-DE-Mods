# Spectator report path

Native baseline: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Managed `Assembly-CSharp.dll` inspected with SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.

## Native data and mode flow

- `DLL_SetEditorPlayer` at RVA `0x872E0` changes the local native view index `DAT_1888e3d70` independently of spectator status.
- `DLL_RunTick` at RVA `0x86680` temporarily changes that index to zero for spectator input processing, restores it, and then calls `FUN_18019d960` at RVA `0x19D960` to fill the managed `PlayState` buffer. Thus the returned report fields follow the selected view index while the spectator input path remains separate.
- `DLL_SetAppMode` at RVA `0x87220` accepts report mode `16` and its report submodes, including menu `71`, Popularity `72`, Fear Factor `73`, Population `74`, Food `75`, Army `76`, Stores `77`, Weapons `78`, and Religion `79`. Its visible branch does not reject spectator mode.
- `FUN_18019d960` copies resources, popularity components, population, troop counts, religion values, and other report fields using the selected view index. Their managed consumers are in `FatControler`.

## Managed entry gate

- `MainViewModel.ButtonReports(object)` reads `FreezeMainControls` once. When true, it forces every request to Army (`num = 9`) and hides the Army back buttons. Its normal branch maps the book and eight menu buttons to report submodes. This is the spectator menu restriction; it is separate from native report-data selection.
- `FatControler` freezes build controls when `EditorDirector.ActivePlayerID <= 0`. That freeze must remain intact for spectators.
- `FatControler` sets the report menu's `PlayerNameText` from `ConfigSettings.Settings_UserName`, so a changed spectator view needs its own displayed name.
- `MainViewModel.PlayerNameText` only raises `NotifyPropertyChanged` when its incoming string differs from the stored value. Writing a selected lord name once per render fights the Vanilla writer and repeatedly triggers this notification; substituting a cached selected name at the setter keeps the normal equality guard effective.
- The Food report XAML includes four buttons bound to `ButtonSetEdibleCommand`. `MainViewModel.ButtonChangeEdibleState` sends `SetFoodEaten`, so the report page is not entirely read-only. The Food back button returns to report menu when `WasInGranary` is false; `ButtonReports` sets it false.
- Those four buttons use `BTN_Building`. Its `IsEnabled=false` trigger switches to `SpriteLocked`, while its style-level enabled trigger starts a fade-out storyboard; disabling them can hide the food sprites entirely. `Noesis.UIElement.IsHitTestVisible=false` blocks mouse hits without activating either disabled-state visual, while the managed command hook remains the action guard.

These contracts were checked statically against the installed files. Behavior of the added testmod's report navigation and data freshness still requires an in-game test after installation.

## Ally panel and spectator commands

The installed native hash remains `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; the installed managed Assembly-CSharp hash is `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.

- `HUD_Main.BuildScreenAllies` opens the same `HUD_AlliesPanel` in Skirmish and Multiplayer. Its ally and enemy lists and current order display use managed `GameData.playerID`, which `SetEditorPlayer` does not update. The panel's `GetAllyList`, `GetEnemyList`, and `UpdateAllies` contain respectively three, two, and two calls to that getter in the installed Assembly.
- `HUD_AlliesPanel.ButtonClicked` sends `Ally_Orders` through `Ally_CancelOrders` (`1061..1066`) to `EngineInterface.GameAction`. `DLL_GameAction` at RVA `0x81870` (confirmed export) uses the current native view index `DAT_1888e3d70` as the ally-command sender. Order/cancel/confirm branches enqueue opcode `113`; send/request-goods branches check or use sender resources before enqueuing. The producer for orders at RVA `0xD7520` is candidate by generic function name but its argument flow and caller are statically traced.
- The opcode `113` handler at RVA `0x1A6F0` (candidate function label, confirmed static producer/handler association) decodes a 16-bit operation and four 32-bit values. Its branches at RVAs `0xD7580`, `0xD78C0`, `0xD7AD0`, `0xD7450`, `0xD7360`, and `0xD77D0` update ally orders, resources, requests and notifications. `DLL_RunTick` at RVA `0x86680` (confirmed) restores the chosen index after suppressing spectator map input, so the ally UI's direct `GameAction` path can still enqueue these commands.
- The managed ally menu has no multiplayer-specific command implementation. The native Chore scheduler at RVA `0x23990` takes different local/network timing branches, but the same ally payload handler is used. Static analysis does not establish whether a spectator-origin packet is accepted and remains synchronized on every peer; this requires a host/client runtime test.
