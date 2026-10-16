# Savegame metadata and missing original maps

Confirmed 2026-10-16 against native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2 and managed SHA-256 BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.

The confirmed native DLL_LoadSaveGame entry is RVA 0x853F0. It loads the simulation state independently of the later managed restart metadata. EditorDirector.loadSaveGame clears restart state, invokes that native loader, and then reads the save header with loadRestartInfo=true.

MapFileManager.GetFileInfoFromFileName routes restart versions 50..99 to HUD_IngameMenu.RestartSkirmishMapInfo.decode. That decoder retains the AIVs, AICs, lord names, images and setup only if the original map header is available (except customTrailMission callers). The final availability block discards the entire successfully decoded object on two null-return paths. Custom trails first try their Maker map; absent trail dictionaries can additionally raise KeyNotFoundException. These are managed contracts, not native offsets.

The original map source is a byte: 0 user, 1 built-in, 2 Workshop. GetHeaderFromFileNameForSkirmishRestart searches only that source catalog by case-insensitive filename. A map copy in another catalog does not satisfy the lookup. The separate parsing-exception return must remain a rejection.

With no restart object, MainViewModel.getAIFace falls back to GameSprites[738] for custom slots; OnScreenText.getComputerName returns an empty name. The user confirmed that installing only the original Workshop map Tactical Boardgame (2 Player) repairs both displays for test_dusty in Vanilla. The save embeds both Mansur and The Leopard with intact 144x144 PNGs.

Removing only the final rejection is insufficient: encode dereferences selectedHeader three times (workshopMap, builtinMap, fileName). Correct portable metadata must retain that original identity separately, while leaving selectedHeader null. RestartSkirmishGame ultimately loads selectedMPHeader.filePath and requires the original map; its menu and action must reject missing-map restarts. FRONT_Multiplayer.doOpen clears selectedMPHeader before listing installed maps.

Testmods/SavegameMissingMapFixTest implements this as managed IL transforms with no native hook. Desktop tests execute clones of the actual game decoder/encoder, isolate catalog and Unity image creation, adapt one Unity Mono String.Split overload in a cloned setup parser, and verify byte-identical save roundtrips, disabled behavior, installed/local/Workshop/built-in/trail cases, corruption rejection and restart guarding. In-game acceptance of the new mod remains pending; the user confirmation above concerns Vanilla's original map dependency.