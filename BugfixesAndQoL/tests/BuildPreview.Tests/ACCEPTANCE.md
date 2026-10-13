# Improved building preview acceptance

The user confirmed red dirt/sand markings, the expanded farm/resource counters and the common tooltip appearance in-game on 2026-10-13. BugfixesAndQoL 1.0.189 adopts that implementation; the integrated local checkbox still requires a visible game test.

- Test wheat/hops coverage 80/81/82 and green 49/50/51; apple/cattle coverage 99/100/101 and green 49/50/51. Check each failed requirement, their union, combined flags and River.
- Test quarry stone 7/8/9, iron 3/4/5 and pitch 0/1/2. Preserve Vanilla red trees, rocks and Killingpit, and its intentionally visible building sprites.
- In both tooltip variants ground counts sit immediately above unchanged costs. Symbol hover shows minima. Check with and without BuildingCosts, BuildingLimit and Fixes, including other providers and inactive contributions.
- Move rapidly, rotate, cancel, pause, switch buildings and maps, and approach map edges. Counts must match the current mission/position. Bodenanforderungen beim Bauen anzeigen and the client master switch must disable both our contribution and terrain augmentation without restart; re-enabling must not revive old counts.
- Check local persistence, preset selection and independent host/client choices; the setting must never enter a host snapshot or multiplayer packet.
- Verify wrapping/scaling and RTL in all relevant fonts. All 21 locales have matching labels and setting help.
- Normal logs contain no waiting/install/first-mark spam. The one native post-startup confirmation is Debug with milliseconds. Genuine errors remain bounded. No teardown or second preview hook exists.

Automated native, policy, state-generation, allocation, managed contract, locale/XAML composition and preset/routing checks are part of the QoL build driver. Isolated allocation checks exercise production snapshot publication after warm-up, not Noesis rendering, the whole game frame or FPS.

## Migration verification, 2026-10-13

APIShared build.bat /nopause completed at 21:10:27 and BugfixesAndQoL build.bat /nopause at 21:13:50, both exit 0. APIShared's 297 tests and the 1,614 migrated preview assertions passed, along with the full QoL driver regressions and real preset/routing checks. Both drivers ran elevated through direct PowerShell invocation.

The installed package matched all 49 local package files using the shared .NET SHA-256 helper. Compiled checks confirmed version 1.0.189.0, APIShared 0.9.0.0, permanent static roots, one Vanilla call, local-only PresetLocal metadata and no hoisted allocation before the native activation/original path. All active version declarations agree; 1.0.188 remains only as historical changelog metadata.

The retired test source, installed plugin package and its exact configuration were removed with the game closed after successful package verification. Native/presentation/managed/source checks passed again after removal; no active references to the retired mod remain. Analysis artifacts outside its source folder were retained. The integrated checkbox game test above remains pending.
