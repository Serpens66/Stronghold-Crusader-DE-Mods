# Script Extender and Fixes runtime reproduction

This diagnostic mod contains three independent probes. It does not change selection, AIV data, AI flags, executable code, or hooks. It reads Fixes' private state to observe the code that the author shipped. Its only save-file change is a separate small record of AIV checksums under `serpens66-aiv-buffer-repro-v1`.

Use Script Extender 2.9.0 and Fixes 1.19.0. The mod requires Fixes to load first. Start a new game session after installing the mod. Search `BepInEx/LogOutput.log` for `[SE-SELECT]`, `[FIXES-AIV]`, and `[FIXES-FLAGS]`. Preserve the complete log and game save used for each reproduction.

## Script Extender selection: automatic on game ticks

Install the probe on both multiplayer clients. Give each player different selected units while the game runs. The probe logs a new snapshot whenever an observed selection count or local selection changes. Each log records UTC time, game tick, local player ID, local output-buffer capacity, and the count and returned IDs for each valid player ID. A nonlocal player's nonzero count combined with IDs from this client's local selection, different from that player's own client snapshot, reproduces the mixed-source error. A zero remote count or matching selections is inconclusive. The probe deliberately does not invoke invalid IDs because the API does not bounds-check them and a live call could read outside the nine-element count array. The tick event does not fire while paused.

## Fixes AIV save/load: automatic

Use a savegame with at least one active AI village and let the AI create ordered-tile data. Save the game. The probe independently stores each active village buffer's SHA-256 in its own save record. Close the game completely, restart it, and load that exact save. During load, the probe compares Fixes' actual archive entry with the saved active hashes and reserved slot zero, then compares live memory at load and on the first game tick. `REPRODUCED: Fixes serialized slot zero instead of the active slot` directly demonstrates the save-side error. A load-time `MISMATCH` needs the archive result checked before attribution; the AI can legitimately change the buffer after loading. A same-process `MATCH` is inconclusive because memory can survive a reload. Keep the log lines from both save and cold load.

## Fixes AI overrides: automatic at map load

Without restarting the game, load map A with `testlord_serp_fixesprobe` in an AI player slot. Its `Override/Fixes/preferences.json` enables all four reported options. Then load map B with `testlord_serp`, which has no Fixes preferences file, in the same player slot. The probe logs the map sequence, current and previous lord, preference presence, and all four native flags after each map load. `REPRODUCED: prior lord's enabled flag survived a different lord without preferences` on map B demonstrates retained state. If map B has no active village in that player slot, or the first map was not observed, the test is inconclusive.

The observations run automatically; setting up different multiplayer selections, saving/loading an AI game, and switching maps still requires gameplay. These tests can produce decisive log evidence only after those scenarios run. Building and installing this mod alone does not verify the author reports.
