# Disposable AI wood and farm experiments

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
