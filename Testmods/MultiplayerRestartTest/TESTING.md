# Multiplayer Restart Test 0.1.4

Experimental standalone testmod. Main mods and Script Extender remain separate. APIShared 0.64.0 provides the general original-button override API; it also retains the earlier lobby-agreement correction.

## Install and exercise

1. Build with `build.bat /nopause`. This builds the working testmod against installed dependencies, runs the offline checks and creates `.local/MultiplayerRestartTest.zip`. It does not install anything.
2. Extract the same ZIP into the game directory on **every participating computer**. It contains only `BepInEx/plugins/MultiplayerRestartTest_Serp`, including its `Locales` directory. APIShared 0.64.0 or newer and Script Extender 2.14.0 or newer must already be installed; use identical compatible dependencies on all peers.
3. Fully exit and restart the game on every computer. The worker checkpoint must be installed before Director starts its simulation thread.
4. Start a **new real multiplayer game**. Loaded saves and missing initial launch data are intentionally unsupported. Local singleplayer is not a multiplayer test.
5. On the host, open ESC and select the original **Restart Mission** button (its Vanilla label follows the game language). The same button remains disabled on clients. Confirm the short **Restart?** question once on the host. Singleplayer keeps Vanilla's original command. No second restart entry is registered, including on the additional-actions page.
6. Expect a brief automatic private lobby transition and then an automatic normal game start. The testmod waits for all original humans, exact slots/teams/colours/Lords, AI data, original map bytes, Vanilla setup and host Modsettings. It never silently substitutes settings or starts with a missing peer.

Version 0.1.4 uses protocol v3; older testmod packages cannot participate. Existing Steam peer sessions stay open during the handoff. Every participant must first consume every old peer's reliable stop marker before the host creates the replacement lobby. After loading, all peers confirm `TargetLoaded` before the host sends Vanilla's one-shot seed. These receipts are sent from before-render after the enclosing frontend update and its queue reset have returned. They do not require the seed-dependent managed client start or first PlayState. The next restart is enabled on the host only after all peers confirm their target simulation started and acknowledge completion. Logical map filename casing may differ between computers; map contents, CRC and local source validation remain strict.

The prototype verifies current synchronized Host properties, not each computer's personal preset. Different personal preferences therefore do not block a restart when the actual Host values match. Dynamic settings/application backends are blocked until their active-state contract is supported. It does not write mission overrides into personal presets. A context that returns different host values in the replacement lobby is blocked rather than silently changed. Original random choices are not promised to replay byte-for-byte.

## Acceptance cases

- Ordinary multiplayer with at least two humans and AI; all original keeps, teams, colours, Lord/AIV/AIC selections and settings match after restart.
- Built-in Coop repeats the same mission, not the following mission; separately exercise custom Coop.
- Restart twice in succession and wait for actual simulation progress on both computers each time.
- Use different game languages on host and client. Labels and recovery prompts must follow each game's language; the single-line confirmation title must fit. Leave the production agreement pending briefly: the testmod must wait without repeatedly clicking Ready or flooding chat.
- With other ESC actions installed, inspect both the main menu and additional-actions page. Exactly one original Restart Mission button must exist; singleplayer, editor and other menu commands retain Vanilla behavior.
- Verify the client cannot initiate a restart and that a missing/incompatible testmod prevents the host action.
- Changed roster, save/resync, mission end, changed source file and competing transitions block or abort the operation.
- Disconnect before commit must leave the old game untouched by the restart. Failure after commitment cannot roll back the old simulation; the failure dialog offers controlled return to the frontend. Do not try to continue a partially stopped match.
- Delay or disconnect a participant during target loading and during completion. The remaining participants must not declare success early. Any already started target is parked again before the failure dialog or cleanup. A legitimate target defeat retains normal Vanilla presentation and cancels the pending restart.
- Check normal singleplayer and ordinary postgame lobby return with the testmod dormant.

Keep **both** BepInEx logs. Relevant markers contain `MP_RESTART_TEST`: `INITIALIZED`, `CAPTURE`, `PREPARED`, `STOPPED`, `LOBBY_CREATED`, `LOBBY_RESTORED`, `LOBBY_VERIFIED`, `AUTOSTART`, `TARGET_SEED_HELD`, `TARGET_LOADED`, `TARGET_SEED_SENT`, `MANAGED_START_COMPLETE`, `FIRST_PLAYSTATE`, `SIMULATION_READY`, `RESTART_COMPLETE`, `FAILED` and `CANCELLED`. Managed completion alone does not prove the multiplayer simulation started.

## Evidence and limits

Offline fixtures execute the production protocol, actual MessagePack formatter, actual installed Director IL and real private-member metadata. The actual installed SE lobby receiver reproduces consumed-and-discarded Vanilla seed packets; the real Vanilla seed/ack/start path is executed with a parked worker. Narrow desktop-only adapters replace unavailable Mono BCL calls (String.Split, ConcurrentQueue.Clear and ReadOnlySpan BitConverter overloads) and external frontend/transport/UI boundaries; runtime seed routing and park-before-recovery paths execute production bodies. Capture/restore and live Steam transport still need multiplayer acceptance.

The opt-in `startup-smoke` scenario uses the public APIShared automation service. It consumes only its own valid `BepInEx/config/SerpAutomation/MultiplayerRestartTest_Serp.run` request and checks startup initialization plus an actual worker-checkpoint callback before quitting. It sends no OS mouse or keyboard input and does not claim multiplayer acceptance.

Uninstall by removing only `BepInEx/plugins/MultiplayerRestartTest_Serp` while the game is closed. This testmod is not part of the main modpack.
