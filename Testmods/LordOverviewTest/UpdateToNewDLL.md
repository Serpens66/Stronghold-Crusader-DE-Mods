# Vanilla data contract

Reference native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

No custom native addresses or hooks. `EngineInterface.GetMeritData()` owns the engine lock and calls Vanilla `DLL_GetMeritData` (reference RVA 0x84140). It copies 45 Int32 values into a 9 by 5 array. Player IDs are 1 through 8; column 0 is occupied player ID, column 1 is merit/alive sentinel, columns 2 and 3 are the exact gold and army values displayed by `HUD_MeritPanel.populateList`. Column 4 is team. Vanilla clamps negative gold to zero; defeated Lords eventually have merit -1 and gold/army zero. No team-dependent concealment branch exists here.

The testmod reads the same snapshot without mutating Vanilla's ranking array. All reads are marshalled to the main thread, once when opening and subsequently once per 40 simulation ticks while visible. Header clicks only sort cached rows. Paused simulation does not schedule periodic samples.

On a game/Extender update verify the real managed wrapper signature and the merit columns against `HUD_MeritPanel`; inspect the current native export if semantics change. No independent RVA fallback is required because Vanilla owns the import. Side-HUD button registration, missions and resource styling are existing APIShared contracts. The parent of the provided side-HUD host is a Grid in IngameUIScreens; a changed structure disables presentation with bounded diagnostics.

## Overview presentation

The packaged crown PNGs derive from the confirmed `ButtonMerit` resource `UI-Buttons A056`, 60 by 70 pixels. The crown-only crop is (12,19,36,20), with a silhouette excluding the old wooden shield. `UI-Buildings A001` and `A002` (68 by 68) supply the unchanged normal/hover frame pixels and a clean parchment center sampled at (8,9,8,8). Only the crown receives an outline/shadow. The pressed PNG moves the crown by one source pixel. `tools/Build-CrownAssets.py` reproduces these assets from the reference atlas; it is developer-only and is never run in the game or required for ordinary builds.

The runtime loads the 68 by 68 images through the installed Noesis/Extender texture provider using unique `Assets/GUI/Sprites/LordOverviewTest_Crown*.png` paths. Vanilla's `BTN_Image` style displays all three states through PropEx.Sprite1/2/3/4. Unlike BTN_Building, BTN_Image uses Sprite3 for the pressed state. No custom Tag binding, template callback or runtime mask remains. The loader checks actual pixel dimensions and reports asset failures through the existing bounded runtime error path.

Table background is neutral RGB(96,96,96), alpha 77/255 (approximately 30%). Player colors follow Spectator's material-color resolution (`remapColours[player]`, `spriteLoader.GetGMMaterial(GM_BODY_ARCHER,...)`), with no second mapping or lightening. RGB changes participate in render invalidation even when values stay unchanged; unknown colors temporarily use white and are retried with the next ordinary refresh. Portrait clipping, contrast outlines, row-only fading and the 40-tick sampling path remain unchanged. Identity width is 46; metric width is at least 56 and expands for longer values.
