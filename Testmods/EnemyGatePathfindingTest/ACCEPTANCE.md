# Gatehouse acceptance after bridge split

EnemyGatePathfindingTest 0.1.5 now limits its edge masks to gatehouse entry/exit boundaries. Linked bridges still establish identity/axis and appear in gate state diagnostics. All independent bridge edge masks, including the former center seam, are removed. EnemyBridgePathTest is independently registered read-only diagnosis. Versions and README remain unchanged; no gate behavior is integrated into BugfixesAndQoL yet.

## Evidence matrix

| Case | Existing evidence | Required before integration |
|---|---|---|
| Open/closed enemy gate, moat alternative | User observed moat filling in both; 8,888 / 8,612 AI searches, zero NoRoute | Repeat known gate layout after removing bridge masks |
| Genuine access beside gate | User observed correct use; 9,554 searches, zero NoRoute | Repeat with pure gate mask |
| Building movement player vs native search player | Three resolver runs: 11,100 / 14,151 / 8,888 searches; zero NoRoute, 13 / 14 / 30 verified role differences; integrity passed | Normal building attack and Raid attack |
| Weighted Assassin compatibility | User confirmed improvement after policy integration; weighted route/caches statically checked | Improvement enabled on pure-gate layout |
| Own keep/gate and cursor/formation | User confirmed corrected cursor and preview | Blocked enemy cursor target then own keep/gate |
| Captured gate | User observed use; older 16,835-search run had four constant early NoRoute | Capture and recapture on final scope |
| Capturer's ally | Observed in existing capture path | No dedicated new map needed |
| Original owner's ally | Statically checked, not observed in game | No user test required, limitation retained |
| Map replacement | Two epochs in one process without integrity errors | Include reload after longer test |
| Multiple attack waves | Not yet final acceptance | No growing failed movement series |
| Mainmods without testmods | Static/regression tests; short game comparison awaiting user confirmation | Normal short run without both testmods |
| Independent lateral bridge crossing | Separate experimental failure: 5,712 NoRoute | Out of gatehouse acceptance; bridge investigation |

Historical scope conflicts are not retroactively cleared. Prior working gate runs used the broader bridge mask; removal therefore requires the specific regression above. Successful technical tests alone do not settle the game behavior.

## Integration decision

Gate integration can be prepared once pure-gate regressions confirm the known alternatives, real access, building/Raid and Assassin paths, capture/cursor behavior, longer run and mainmod comparison. It is not yet released or marked finally accepted. Vanilla continues to choose alternatives and orders.
