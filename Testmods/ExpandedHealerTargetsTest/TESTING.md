# Expanded Healer Targets Test: game checks

The testmod is installed in `BepInEx/plugins/ExpandedHealerTargetsTest_Serp`. Its two BepInEx options, `HealSiegeEngines` and `HealHumanCivilians`, default to `true`. Change them in `BepInEx/config/ExpandedHealerTargetsTest_Serp.cfg` and restart the game; live config changes intentionally take effect on the next launch.

1. Start a game or an editor map with one Bedouin healer and an owned, damaged, fully built catapult immediately beside it. Wait for automatic target search, movement, heal animation and repeated HP increases. Record all `HEALER_TARGETS_CATAPULT` lines, including `reason`, both native selector flags, healer state and target.
2. Repeat with an owned, damaged priest. Verify automatic movement, animation and HP increases. A human worker such as a woodcutter is a useful control.
3. Keep a healthy target, a dead target, an enemy target, a neutral target and an animal nearby. None should gain HP. A blocked sight line or unreachable target should follow Vanilla's normal search or movement outcome.
4. Restart with each option disabled in turn and then both disabled. The disabled category must stay excluded; normal soldier healing must still work.
5. For multiplayer, use the same two option values on every participant and confirm a real host/client match before drawing conclusions about synchronization.

After startup, first confirm that `HEALER_TARGETS_DIAGNOSTIC` reports added civilians and `callbackFailures=0`. A temporary `HEALER_TARGETS_LIST_SKIPPED` during map setup may occur; a later valid list must still add targets. Any `HEALER_TARGETS_CALLBACK_FAILED` reports the exact exception and context. `HEALER_TARGETS_CATAPULT_DIAGNOSTIC_FAILED` must not stop civilian additions.

`BepInEx/LogOutput.log` should contain `HEALER_TARGETS_HOOK_READY` at load and `HEALER_TARGETS_POST_STARTUP` after the startup cleanup and first simulation tick. Up to five `HEALER_TARGETS_DIAGNOSTIC` lines report totals. Up to 80 change-driven `HEALER_TARGETS_CATAPULT` lines report the list decision for catapults within 80 world units of a living same-player Bedouin healer. They do not change Vanilla's target search or healing. If the hook fails its native or RedBird validation, it stays uninstalled and the log explains why.
