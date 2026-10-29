# Vanilla data contract

Reference native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

No custom native addresses or hooks. `EngineInterface.GetMeritData()` owns the engine lock and calls Vanilla `DLL_GetMeritData` (reference RVA 0x84140). It copies 45 Int32 values into a 9 by 5 array. Player IDs are 1 through 8; column 0 is occupied player ID, column 1 is merit/alive sentinel, columns 2 and 3 are the exact gold and army values displayed by `HUD_MeritPanel.populateList`. Column 4 is team. Vanilla clamps negative gold to zero; defeated Lords eventually have merit -1 and gold/army zero. No team-dependent concealment branch exists here.

The testmod reads the same snapshot without mutating Vanilla's ranking array. All reads are marshalled to the main thread, once when opening and subsequently once per 40 simulation ticks while visible. Header clicks only sort cached rows. Paused simulation does not schedule periodic samples.

On a game/Extender update verify the real managed wrapper signature and the merit columns against `HUD_MeritPanel`; inspect the current native export if semantics change. No independent RVA fallback is required because Vanilla owns the import. Side-HUD button registration, missions and resource styling are existing APIShared contracts. The parent of the provided side-HUD host is a Grid in IngameUIScreens; a changed structure disables presentation with bounded diagnostics.

## Overview presentation

The packaged crown PNGs derive from the confirmed `ButtonMerit` resource `UI-Buttons A056`, 60 by 70 pixels. The crown-only crop is (12,19,36,20), with a silhouette excluding the old wooden shield. `UI-Buildings A001` and `A002` (68 by 68) supply the unchanged normal/hover frame pixels. The parchment center is recovered from the shared backgrounds of Vanilla building-button pairs A001/2, A003/4, A005/6, A007/8, A009/10, A011/12, A013/14, A017/18 and A019/20. Three-sprite RGB clusters tolerate atlas compression differences (14 per channel); uncovered pixels inherit nearest verified parchment grain. At least 2000 of 2704 inner pixels must be recovered. This replaces the former enlarged bright edge sample; no manual background tint is applied. Only the crown receives an outline/shadow. The pressed PNG moves the crown by one source pixel. `tools/Build-CrownAssets.py` reproduces these assets from the reference atlas; it is developer-only and is never run in the game or required for ordinary builds.

The runtime loads the 68 by 68 images through the installed Noesis/Extender texture provider using unique `Assets/GUI/Sprites/LordOverviewTest_Crown*.png` paths. Vanilla's `BTN_Image` style displays all three states through PropEx.Sprite1/2/3/4. Unlike BTN_Building, BTN_Image uses Sprite3 for the pressed state. No custom Tag binding, template callback or runtime mask remains. The loader checks actual pixel dimensions and reports asset failures through the existing bounded runtime error path.

Table background is neutral RGB(96,96,96) with brush opacity 0.5; child opacity is unchanged. Player colors follow Spectator's material-color resolution (`remapColours[player]`, `spriteLoader.GetGMMaterial(GM_BODY_ARCHER,...)`), with no second mapping or lightening. RGB changes participate in render invalidation even when values stay unchanged; unknown colors temporarily use white and are retried with the next ordinary refresh. Portrait clipping, contrast outlines, row-only fading and the 40-tick sampling path remain unchanged. Identity width is 46; metric minima are 56, popularity 40, fear 32. Longer numbers expand their column.

## Configurable statistics and information rights

Native audit on 2026-10-29 against the hash above: `DLL_GetMPScoreData` (reference RVA 0x83CB0) copies the running statistics arrays without setting a result or opening any UI. `EngineInterface.GetMPScoreData` allocates a separate managed snapshot. Numeric fields match `HUD_MissionOver` rows; kills sum matrix row `player*9+other` for victims 1..8 excluding self; losses sum column `other*9+player` for killers 0..8 excluding self. Use Int64 for those sums. Neither troop kill points nor produced troops substitute current army counts.

The native report builder at 0x19D960 reads the selected player's current popularity at resource offset 0x60 and divides by 100 using integer division. Its signed fear level at report slot 0x30 is resource `r_GoodBadThingBoost` (offset 0x2134), without a sign inversion; it is not the popularity contribution. The mod reads these fields through `GamePlayerManagerAPI.TryGetPlayerResourcesById` and the installed SE's typed resource view for explicit IDs 1..8, never by changing the viewed/controlled player. Invalid/unavailable values remain unknown. The report's historical max/min fear arrays are not used for the current level. Vanilla `HUD_MissionOver.GetPlayerFear(0)` uses `GameSprites[115]`; only its neutral icon is reused as a header symbol, not as a data source.

One main-thread capture holds the same engine lock across merit, selected statistics and resource reads. In the real unmodified Assembly-CSharp this is a private static `EngineInterface.threadLock` of type Object: resolve and validate that exact field once through reflection, never compile against a publicized version. Revalidate on managed updates. No direct native address or overlapping hook is introduced.

Information rights use the controlled player and merit teams (unassigned team zero does not imply alliance). A confirmed spectator requires the game spectator flag and spectator identity; editor and an eliminated local player also qualify. Elimination accepts Vanilla's official resource loss state, the local merit defeat sentinel, or a positively identified dead local Lord (type/owner/global identity checked). An absent/unresolved resource or an arbitrary dead viewed opponent does not qualify. This preserves statistics access before/without automatic spectator promotion and after loading an eliminated save. No Spectator or BFQ assembly dependency is required.

Host permission and local column preference use the existing shared preset integration. A client's own saved host preference is insufficient: its setter records an accepted `IncomingNetwork` update under SE's public origin contract for the observed lobby. An equal-value update also confirms; a different/absent lobby invalidates confirmation, while an explicit map-transition preservation retains it. The normal shared host-only setter guard still rejects unauthorized writes. On SE updates verify `GameXAMLManagerAPI.CurrentLobbyModSettingsChangeOrigin` and shared lobby transition semantics. This is observation of the existing transport, not another synchronization protocol. A locked mission source retains an in-game local display choice separately until normal local preset persistence is editable again; no host/Trail snapshot is edited to store it.

Metric identifiers, selection order and sorting are independent of visual positions; only selected permitted values enter published rows. Maximum five visible metrics does not limit the available catalog. Refresh remains on open and every forty visible simulation ticks; column changes/sorting only render the current cache (newly selected values become available at the next scheduled sample). Popup outside clicks use the view's preview event without marking it handled or capturing the mouse. All popup/root handlers and UI references are session/HUD-owned and released on detach.

## Selection popup and header gestures (2026-10-29)

The three themes are Economy, Military and Population/mood; they include both current and cumulative values. Stable metric IDs and saved order are unchanged. The popup, pages, checkboxes and ScrollViewer are created once per HUD. Changes only update check states, availability and the count. A 22-unit content gutter reserves room for the Vanilla scroll skin. The header/footer never scroll. Per-category offsets and the selected category are retained for the session and restored after layout through the guarded Unity queue. The menu uses its own fixed-width anchor sibling, positioned once per opening; changing the table width does not reposition the popup. No transparent click catcher is created.

Only value headers support pointer gestures. A six-logical-unit movement threshold separates sorting from drag; a drop commits once, by metric ID, and preserves the selected sort ID. Invalid targets, session/HUD-generation changes, Escape, focus/capture loss and hiding cancel the gesture. Actual mouse capture and focus-change events are used, without a frame poll or new native hook. The data cadence still runs every forty visible ticks; presentation is deferred from pointer-down until release/cancel so it cannot replace a captured header. Ordinary value updates also retain header controls while columns, widths and sort state stay unchanged. APIShared and information rights are untouched.

## Extensible columns and source-relative popups (2026-10-29)

APIShared 0.19.0 owns the process-lived provider catalog and exclusive tooltip scaler, not this HUD.
Builtin persisted IDs remain unchanged. External keys are escaped owner/local-ID pairs; absent
providers retain removable selections. External providers receive only rows with ExtendedAllowed,
after the existing controlled-player/spectator/elimination policy. Native builtin snapshots finish
under the cached engine lock before external readers run; revalidate session afterwards.

The table uses one Auto Grid for header and cells. No maximum count or character-width estimate;
gear and identity precede metrics. Intrinsic rendered text (including custom decimal units) and
symbols determine width. Header icons have balanced arrow space. Mouse-down captures actual
arranged column geometry; data still samples every 40 ticks while presentation waits until release.
No horizontal scrollbar, truncation or rescaling is introduced. Overwide content extends right.

All owned statistics popups opt into SharedToolTipSizing with reference element and logical font 16.
Templates bind every sized property. Settings popups use the same public scaler's preset mode.
No new native contract, hook, perspective write, serializer or game data source is introduced.
Actual Noesis rendering and viewport-scale acceptance remain separate from CLI validation.
