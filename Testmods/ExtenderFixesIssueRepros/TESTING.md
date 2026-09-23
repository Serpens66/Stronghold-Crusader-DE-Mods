# Script Extender and Fixes runtime reproduction

This diagnostic mod contains three independent probes. It does not change selection, AIV data, AI flags, executable code, or hooks. It reads Fixes' private state to observe the code that the author shipped. Its only save-file change is a separate small record of AIV checksums under `serpens66-aiv-buffer-repro-v1`.

Use Script Extender 2.9.0 and Fixes 1.19.0. The mod requires Fixes to load first. Start a new game session after installing the mod. Search `BepInEx/LogOutput.log` for `[SE-SELECT]`, `[FIXES-AIV]`, and `[FIXES-FLAGS]`. Preserve the complete log and game save used for each reproduction.

## Script Extender selection: automatic on game ticks

Install the probe on both multiplayer clients. Give each player different selected units while the game runs. The probe logs a new snapshot whenever an observed selection count or local selection changes. Each log records UTC time, game tick, local player ID, local output-buffer capacity, and the count and returned IDs for each valid player ID. A nonlocal player's nonzero count combined with IDs from this client's local selection, different from that player's own client snapshot, reproduces the mixed-source error. A zero remote count or matching selections is inconclusive. The probe deliberately does not invoke invalid IDs because the API does not bounds-check them and a live call could read outside the nine-element count array. The tick event does not fire while paused.

## Fixes AIV save/load: automatic

Use a savegame with at least one active AI village and let the AI create ordered-tile data. Save the game. The probe independently stores each active village buffer's SHA-256 in its own save record and compares Fixes' actual archive entry with the active buffer and reserved slot zero. `REPRODUCED: Fixes serialized slot zero instead of the active slot` directly demonstrates the save-side error. Load that exact save. The probe compares automatically at its own load callback and on the first game tick after loading. A load-time `MISMATCH` needs the archive result and callback timing checked before it is attributed to Fixes; the AI can legitimately change the buffer after loading. A `MATCH` or no distinction from slot zero does not reproduce the issue. Keep the log lines from both save and load; if the Fixes archive entry is not available at the probe save callback, this part of the test is inconclusive.

## Fixes AI overrides: automatic at map load

Without restarting the game, load map A with a custom lord whose `override/fixes/preferences.json` enables at least one of the four reported options. Then load map B with a different custom lord without a Fixes preferences file in the same player slot. The probe logs the current lord name, whether Fixes has preferences for it, and all four native flags after each map load. `REPRODUCED: enabled flag without current preferences` on map B demonstrates retained state. If map B has no active village in that player slot, or the flag was never enabled on map A, the test is inconclusive. Compare with a fresh process started directly into map B if an actual AI behavior difference needs confirmation.

The observations run automatically; setting up different multiplayer selections, saving/loading an AI game, and switching maps still requires gameplay. These tests can produce decisive log evidence only after those scenarios run. Building and installing this mod alone does not verify the author reports.
