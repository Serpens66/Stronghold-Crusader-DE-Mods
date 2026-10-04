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

## 2026-10-04: gate-only game regression after bridge split

LogOutput.log contains one process start and one test_gates.sav map epoch, 16:12:12.928–16:13:53.811. Loaded APIShared 0.4.7, BugfixesAndQoL 1.0.174 and EnemyGatePathfindingTest 0.1.5; no EnemyBridgePathTest loading/runtime lines. User reports that everything appeared to work in game; specific scenarios were not individually identified, so this does not certify every outstanding acceptance case.

Final counters: 21,204 AI builder searches, zero AI NoRoute; 23,953 total queries, 866 rejected edges, 673 AI vanilla detours and zero policy NoRoute. Building approach: 35 calls and 35 validated argument-role differences, zero building-context failures. Scope mismatches, exceptions, thread-slot conflicts, snapshot-pool exhaustion and snapshot errors all zero. Hook execution/runtime integrity/thread-slot/lifecycle verdicts PASS. Final command events are paired: target 263/263, tribe movement 1,010/1,010, unit movement 50,930/50,930. Sum of every decision aggregate count is exactly 978,565, matching the final observation counter.

Gate 819 retains Global-ID 2423576 during repeated capturer changes: one initial capture and sixteen subsequent capture transitions. All 17 callback CaptureMismatch observations (14 graph, 3 builder) were confirmed recovered by later matching publication, none unresolved at map end. Owner and capture relations remain separate: own + captured-by-other is recorded; capturer/self and capturer/ally are recorded as permitted. These observations do not by themselves prove recapture by the original owner.

Cursor: 781 requests, 172 refreshes, 609 exact cache hits, no validation deferrals or exceptions; average 0.205 ms, maximum 3.177 ms. No proven cursor-policy block was exercised in this run. Seven Assassin observations were native flood-field queries without a materialized route; this is not a new weighted Assassin route/cache acceptance test. Raid-specific retargeting and a genuine alternative access are not separately proven by the supplied observation. Duration about 101 seconds does not replace a multi-wave endurance test.

Known path-table coverage warning remains 89/90 profiles and 534/540 permissions; all compared values match, no mutation evidence. No Error/Fatal log lines. The legacy hookOwnerConflict=True label denotes shared mainmod ownership, not a failing integrity verdict. No runtime code or installed DLL was changed for this analysis. This run supports the gate-only regression and independence from the bridge observer; remaining targeted acceptance cases retain their documented limits.
Source log SHA-256: 53FCE6F47F5086030DCB0334CAA84393A421933DB4950A526A1C984BBAFA893C.
