# Side HUD visibility [Scope: Vanilla]

Verified on 2026-11-08 against the installed `Assembly-CSharp.dll` with SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789` and the
managed and XAML exports of the current `FBCB9319` native baseline.

- [KeyManager](../managed/BC8B6A39/decompiled/KeyManager.cs#L1302) and the [leftward right-button gesture](../managed/BC8B6A39/decompiled/CrusaderDE/HUD_RightClick.cs#L159) call `MainControls.toggleUIVisibility`. The mode is set before calling `MainViewModel.SetVisibleState`, so `MainControls.IsUIVisible` already reflects the new mode when the property notifications run. The [force and conditional restore paths](../managed/BC8B6A39/decompiled/MainControls.cs#L332) converge on the same toggle.
- [`Show_InGameUI`](../managed/BC8B6A39/decompiled/CrusaderDE/MainViewModel.cs#L20300) notifies its own name and `Show_HUD_Extras`. The [extras getter and setter](../managed/BC8B6A39/decompiled/CrusaderDE/MainViewModel.cs#L21041) expose `_show_HUD_Extras && _show_InGameUI` and notify independently when the raw extras flag changes. Scene initialization clears the raw flag and objectives/goods initialization enables it. A false public getter during Hide HUD therefore says nothing about the independent flag.
- The [side HUD grid](../resources/xaml/Assets/GUI/XAML/IngameUIScreens.xaml#L105) binds its own visibility to `Show_HUD_Extras`. The [in-game root](../resources/xaml/Assets/GUI/XAML/MasterController.xaml#L61) separately binds to `Show_InGame`. The audited visibility chain is managed code and XAML.

## Calendar date

- [`OnScreenText.updateOST`](../managed/BC8B6A39/decompiled/OnScreenText.cs#L338) copies the month and year from the current `PlayState`, translates the month with `TEXT_MONTHS` and publishes the formatted string through `OST_Date_Text`. The regular `GameData.setGameState` path and the `Director` UI refresh with no new tick both reach this update. The date producer does not depend on the HUD display mode.
- `initOST` clears `OST_Date_Vis` at scene or map initialization. The active-date entry is prepared for game views outside the map editor. The [Vanilla date label](../resources/xaml/Assets/GUI/XAMLResources/HUD_Main.xaml#L5157) binds both properties inside `MainHUD`, whose parent collapses with `Show_InGameUI`. `ShowGameTime` belongs to the separate elapsed-time display.
- The in-game menu closes or hides itself when opening the embedded Help or Workshop uploader, and the Freebuild Events key opens its own menu while Hide HUD is active. These panels have independent visibility properties and do not restore the HUD mode on opening. [In-game menu](../managed/BC8B6A39/decompiled/CrusaderDE/HUD_IngameMenu.cs#L1053) · [Freebuild Events key](../managed/BC8B6A39/decompiled/KeyManager.cs#L1347)
