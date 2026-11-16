# Multiplayer Restart Test 0.1.1

Experimental standalone testmod. No main-mod, APIShared or Script Extender sources are modified.

## Install and exercise

1. Build with `build.bat /nopause`. This builds the working testmod against installed dependencies, runs the offline checks and creates `.local/MultiplayerRestartTest.zip`. It does not install anything.
2. Extract the same ZIP into the game directory on **every participating computer**. It contains only `BepInEx/plugins/MultiplayerRestartTest_Serp`. APIShared 0.50.0 or newer and Script Extender 2.14.0 or newer must already be installed; use identical compatible dependencies on all peers.
3. Fully exit and restart the game on every computer. The worker checkpoint must be installed before Director starts its simulation thread.
4. Start a **new real multiplayer game**. Loaded saves and missing initial launch data are intentionally unsupported. Local singleplayer is not a multiplayer test.
5. On the host, open ESC and select **Mission neustarten (Test)**. The tooltip explains a blocked action. Clients have no restart action. Confirm once on the host.
6. Expect a brief automatic private lobby transition and then an automatic normal game start. The testmod waits for all original humans, exact slots/teams/colours/Lords, AI data, original map bytes, Vanilla setup and host Modsettings. It never silently substitutes settings or starts with a missing peer.

Version 0.1.1 uses protocol v2; older testmod packages cannot participate. Existing Steam peer sessions stay open during the handoff. Every participant must first consume every old peer's reliable stop marker before the host creates the replacement lobby. The next restart is enabled on the host only after all peers confirm their target simulation started and acknowledge completion. Logical map filename casing may differ between computers; map contents, CRC and local source validation remain strict.

The first prototype verifies existing Modsettings through their public snapshot contracts. It does not write mission overrides into personal presets. A context that returns different host values in the replacement lobby is blocked rather than silently changed. Original random choices are not promised to replay byte-for-byte.

## Acceptance cases

- Ordinary multiplayer with at least two humans and AI; all original keeps, teams, colours, Lord/AIV/AIC selections and settings match after restart.
- Built-in Coop repeats the same mission, not the following mission; separately exercise custom Coop.
- Restart twice in succession and wait for actual simulation progress on both computers each time.
- Verify the client cannot initiate a restart and that a missing/incompatible testmod prevents the host action.
- Changed roster, save/resync, mission end, changed source file and competing transitions block or abort the operation.
- Disconnect before commit must leave the old game untouched by the restart. Failure after commitment cannot roll back the old simulation; the failure dialog offers controlled return to the frontend. Do not try to continue a partially stopped match.
- Delay or disconnect a participant during target loading and during completion. The remaining participants must not declare success early. Any already started target is parked again before the failure dialog or cleanup. A legitimate target defeat retains normal Vanilla presentation and cancels the pending restart.
- Check normal singleplayer and ordinary postgame lobby return with the testmod dormant.

Keep **both** BepInEx logs. Relevant markers contain `MP_RESTART_TEST`: `INITIALIZED`, `CAPTURE`, `PREPARED`, `STOPPED`, `LOBBY_CREATED`, `LOBBY_RESTORED`, `LOBBY_VERIFIED`, `AUTOSTART`, `MANAGED_START_COMPLETE`, `FIRST_PLAYSTATE`, `SIMULATION_READY`, `RESTART_COMPLETE`, `FAILED` and `CANCELLED`. Managed completion alone does not prove the multiplayer simulation started.

## Evidence and limits

Offline fixtures execute the production protocol, actual MessagePack formatter, actual installed Director IL and real private-member metadata. Narrow desktop-only adapters replace unavailable Mono BCL calls (String.Split and ConcurrentQueue.Clear) and external frontend/transport/UI boundaries; runtime message routing and park-before-recovery paths execute production bodies. Capture/restore and live Steam transport still need multiplayer acceptance.

The opt-in `startup-smoke` scenario uses the public APIShared automation service. It consumes only its own valid `BepInEx/config/SerpAutomation/MultiplayerRestartTest_Serp.run` request and checks startup initialization plus an actual worker-checkpoint callback before quitting. It sends no OS mouse or keyboard input and does not claim multiplayer acceptance.

Uninstall by removing only `BepInEx/plugins/MultiplayerRestartTest_Serp` while the game is closed. This testmod is not part of the main modpack.
