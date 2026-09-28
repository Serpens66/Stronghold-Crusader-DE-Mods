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

These contracts were checked statically against the installed files. Behavior of the added testmod's report navigation and data freshness still requires an in-game test after installation.
