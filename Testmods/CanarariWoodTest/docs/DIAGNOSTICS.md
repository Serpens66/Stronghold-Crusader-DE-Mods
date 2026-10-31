# Disposable AI wood and farm experiments

## Passive detail observation

Installing this companion activates passive wood/farm diagnostics, independently of
all experiment switches and the active-probe Vanilla candidate-gate check. It observes
arbitrary supported new/save sessions and AI players, including the early loading
boundaries; editor and replay sessions retain their existing exclusions.

`CanariPassiveDiagnostics` owns orchard/parcel/vegetation histories, coarse-grid
comparisons, candidate scans, read-only search replays and alternatives, detailed
route classification and correlations with wall snapshots from the general observer.
`CanariNativeCapture` owns the expensive native readers. The general hook owner
requests these snapshots only while this companion is registered, synchronously at
the existing capture point. This companion still installs no native hooks.

Passive and active runtime callbacks have independent error handling. Both share
the companion's bounded 56 MiB session log budget, separate from the general mod's
56 MiB construction/8 MiB economy allocation. `AI_BUILD_SPECIAL_SUMMARY`,
`AI_BUILD_SPECIAL_BUDGET` and `AI_BUILD_SPECIAL_OVERFLOW` identify summaries
and accounting. Logs use this companion's own BepInEx source.

All settings below still default to false and control **active experiments only**.
Passive observation does not enable any of them. Build the general assembly first;
`GeneralDiagnosticDir` can select its output directory for a direct MSBuild build.
`BugfixesDir` follows the general project's installed-package lookup. Both drivers
accept `/noinstall`; companion tests live in `tests/Companion.Tests`.

CanarariWoodTest requires AIBuildDiagnoseTest, APIShared, Script Extender and
BugfixesAndQoL. Install both diagnostic mods for these experiments. The companion
installs no native hooks; it uses the general mod's private integration bridge.
Its own BepInEx configuration is CanarariWoodTest_Serp.cfg. All settings default
to false; settings from AIBuildDiagnoseTest_Serp.cfg are not migrated or activated.

| Setting | Scope and effect |
| --- | --- |
| PlacementProbe.Enabled | One placement at player 6, (345,485), on test_canari_nowoodcutters_probe.sav after the expected failed wood attempt. |
| NearbyWoodTest.Enabled | On that named copy only: calibrate against unmodified Vanilla, predict exclusions, revalidate live data, temporarily change candidates for at most twelve searches and restore every touched byte. |
| FarmContractProbe.Enabled | On byte-verified rat_farm_site_contract_probe.sav, player 2: create four farms and perform controlled wood-site tests. |
| CanariFarmSwap.Enabled | On byte-verified test_canari_farm_swap_cattle.sav, test_canari_farm_swap_wheat.sav or test_canari_farm_swap_hops.sav: replace the orchard at (344,480), await cleanup/spawn/path generation and hold player 6 wood calls during preparation. |
| FixesFarmFilterOverride.Enabled | Explicit startup-only, process-wide suppression of the external Fixes farmland filter for Vanilla comparisons. Restart the game when changing this setting. The Fixes configuration file is preserved and its in-memory setting restored after native load. |

Never save experiment results over the original saves. Use the exact disposable
copies and retain their recorded hashes. Wrong names, hashes or calibration data
do not arm their corresponding experiment. Placement/Nearby retain their existing
name/calibration guards; they do not gain a fictitious source-hash guarantee.

With suppression disabled, the companion does not change Fixes settings. Active
experiments require the validated native Vanilla candidate gate; if Fixes is
already absent/disabled that comparison may be available without suppression.
Otherwise the experiments stay disabled and explain why in the log. Enabling
suppression affects the whole game process, including ordinary sessions, until
restart; it is never enabled automatically with another experiment.

Build AIBuildDiagnoseTest first, then this companion, with the same APIShared
selection. Native version guards, Vanilla placement arguments, timeout behavior,
copy-only gates and finally-based data restoration remain part of the tests.
