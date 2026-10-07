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
