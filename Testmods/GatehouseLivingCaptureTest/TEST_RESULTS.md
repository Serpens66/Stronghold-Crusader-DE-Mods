# Gatehouse living capture: verification results

Date: 2026-10-07 19:27:50.852

- PASS: production UnitAccess life predicates and installed public GameUnit types/offsets; low-word-only death marker, all AliveStates, null, and health neutrality.
- PASS: exact native DLL/full gatehouse handler hashes, complete handler decode/incoming boundaries, tile-chain backedge and unique executable-section signature.
- PASS: 648 machine cases using the installed X64InlineHook backend. Actual 18-byte patch/stub/continuation, both JE outcomes, entry/backedge, GPR/XMM/stack, inactive/null/error fail-open; 243 deliberately caught synthetic errors.
- PASS: workspace UnitAccess/permanent-hook regression, explicit JSON/lifecycle/MonoBehaviour scheduling/teardown/CRLF/XAML checks and APIShared prebuild preflights.
- PASS: APIShared baseline suite, native gate/bridge generators/coupling/cooldown scenarios, lobby-settings presets and preset-consumer compilation.
- PASS: APIShared and GatehouseLivingCaptureTest built/installed via elevated direct build.bat calls; installed DLL hashes match local package outputs.
- PASS: public API test now allows additional exported types/overloads. Required existing contracts and native-interface safety remain checked. Updated test executable passes against the compiled APIShared; no additional runtime rebuild is needed for this test-only change.
- Initial APIShared build was stopped by outdated exported-type/pointer contract expectations; these were updated before successful installation.
- The test-only rerun in the restricted sandbox failed while writing a long redirected temporary preset path; rerunning the same compiled suite outside sandbox passed.
- Gameplay, save/load, real host/client and installed-Fixes acceptance remain unperformed. Offline/build evidence does not prove gameplay.

APIShared DLL SHA-256: 89054361D21E7E5B4B2EA018D7FACE1A2F51B0AAAB5F362DB3A7B609B19593CC
GatehouseLivingCaptureTest DLL SHA-256: 58D8CBC9A140F08659C74098FBAE0A2E5679C1FB136393C50B951CB7B7D0FDA4

Versions unchanged: APIShared 0.4.10; new standalone testmod 0.1.0; NetworkMode 1.
Build logs: api-shared-build.log, build.log. Updated API contract suite: api-contract-tests.log.
Reproduce source/backend tests: powershell.exe -NoProfile -ExecutionPolicy Bypass -File Testmods\GatehouseLivingCaptureTest\verify.ps1 -RunTests
## Gameplay log evidence, 2026-10-07

The user reports successful in-game capture testing. The relevant BepInEx session ran approximately 22:28-22:33 with Fixes 1.25.1.0 loaded, in MapEditor. A later 22:34-22:36 session contains no GatehouseLivingCaptureTest markers and is not evidence for this hook.

- 22:28:57.704: READY confirms native hash, expected interop offsets and exactly 18 displaced bytes with RedBird 1.5.0.0.
- 22:30:27.274: CONFIRMED proves actual hook execution after startup cleanup; unitId=3/globalId=15 has IsAlive and deathLowWord=0.
- 22:30:48.415: FIRST_EXCLUSION records the same unitId/globalId with IsAlive and deathLowWord=1, driving the original JE to skip it.
- No Error/Fatal log entries or gatehouse callback-error markers in that session; application exit is logged normally at 22:33:31.145.
- The early 22:28:49 native crash-handler dump is a roughly 47 MB null-read report with Mono frames, matching the documented startup false alarm. It precedes hook installation; the session continues through gameplay and normal exit.
- Separate warnings concern map archives, Lua unload state and ImGui exports. The log provides no link between those warnings and the gatehouse filter.

This establishes execution and exclusion of a death-marked IsAlive unit alongside Fixes, together with the user's positive visual test. It does not independently establish every gatehouse variant, save/load or real multiplayer acceptance. Previous statements that gameplay/Fixes acceptance remained wholly unperformed are superseded by this limited evidence.
