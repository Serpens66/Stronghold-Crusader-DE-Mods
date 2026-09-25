# Fixes Tribe Probe

This test mod records tribe creation and unit assignment through Script Extender events. It does not issue game commands or change event arguments. Its log marker is `FIXES_TRIBE_PROBE` in `BepInEx/LogOutput.log`.

## Preparation

1. Install the same `FixesTribeProbe_Serp` package on host and client. Keep Script Extender and Fixes 1.19.1 installed on both machines.
2. For an isolated run, temporarily disable all other gameplay mods on both machines. Record the active plugin list from each BepInEx startup log.
3. Set `[AssignNewTribeOnUnitTypeTrim] Enabled = true` in `fixes.cfg` on both machines before starting the game.
4. Confirm that each log contains `FIXES_TRIBE_PROBE READY` and, after loading a map, `FIXES_TRIBE_PROBE POST_STARTUP_TICK`.

## Multiplayer action

1. Start a two-player game with a few archers for each player.
2. Each player selects only their own archers. Keep both selections active.
3. One player right-clicks the archer icon in the selected-troops panel once to deselect archers.
4. Let the game advance at least 200 ticks, then retain both complete logs and record whether a game desync or resync appeared.
5. Repeat from a fresh game with the Fixes option disabled on both machines. Restore the original `fixes.cfg` values after testing.

Match `CREATE_POST`, `ASSIGN_PRE`, `CROSS_OWNER_ASSIGN` or `ASSIGN_POST`, and `SNAPSHOT_NEXT_TICK` lines by tick, unit ID and group ID across host and client. `previousMember=true` in a live previous group, with `unitTribeNow` naming another group, is a group membership inconsistency. The marker alone does not establish that Fixes caused a desync: compare the two logs and the option-disabled control run.

For a final two-mod reproduction, repeat the user action without this observer installed and retain the BepInEx logs. The observer's run supplies the state explanation; the run without it establishes whether the visible problem occurs with only Script Extender and Fixes.
