# Assassin direct gatehouse climbing experiment

Installing this testmod enables APIShared's direct gatehouse endpoint rule.
BugfixesAndQoL uses the shared hooks and the same endpoint exception. Test with
and without its improved Assassin pathfinding setting. Existing player climb
settings still apply. Install the matching APIShared and BugfixesAndQoL builds.

The static runtime is rooted by LibraryLoaded, the shared mission publisher and
GameTimeManagerAPI.OnTick. The plugin component needs only Awake. Observe READY,
MAP START and RUNTIME TICK markers in BepInEx/LogOutput.log.

Gameplay acceptance (not yet performed):

1. Place enemy small and large gatehouses without adjoining walls. From each
   usable side, order only Assassins onto the roof. Confirm movement/climb cursor,
   physical states 126..129 and arrival; no gatehouse attack.
2. Repeat with adjoining walls, occupied roofs and ordinary building targets.
   Confirm existing wall behavior and ordinary attacks remain valid.
3. Let AI siege Assassins approach freestanding gates; confirm their existing
   gatehouse movement action reaches a physical climb rather than attacking.
4. Repeat with Fixes SmarterSiegeAssassins on/off. Allied/captured gates must
   retain Fixes target selection. Repeat with improved pathfinding on/off and
   player climb settings on/off, then save/load and map change.
5. Multiplayer requires identical active testmods on all peers (NetworkMode 1).

OBS logs are bounded to 150 per map. They report native command, unit ID, player,
state, current/next tiles, gate endpoint presence and a physical climb episode.
A route result alone does not prove gameplay acceptance. Remove the testmod to
disable the experiment on the next process start; APIShared ownership remains.

No README or existing mod version was changed during testing.
