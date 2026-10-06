# Fixes siege tent placement evidence

Status: gameplay unconfirmed. Offline output is a scenario candidate, never a bug report.

## Enable only the selected observer

The observer is disabled by default. With the game closed create/edit `BepInEx/config/FixesSiegeTentPlacementTest_Serp.cfg`:

```ini
[Diagnostic]
Enabled = true
```

Enable only this observer, disable the other observer and all extra gameplay mods, then restart. Recording only activates in an isolated Fixes1.24 session. Keep `Enabled = false` for ordinary play. This avoids a separate preparation game just to create the config file.

## Installed combination and isolation

Use unchanged Fixes 1.24.0, SHCDESE 2.13.0, APIShared >=0.4.2 and only this observer. Disable other gameplay mods and the popularity observer for this run. Do not use AIAttackTest, CheatMod or scripted unit creation. This observer changes no simulation decisions and creates no native hooks.

Run `build.bat /nopause` with the game closed. It verifies runtime sources, native/managed contracts, fixture metadata and offline callback provenance, runs the offline self-test, then builds and installs through the project driver. Existing mod versions are unchanged during testing.

## Prepare the lord and one flat editor map

1. In the official Castle & CPU Lord Editor import `Fixtures/SiegeThreeCatapults.lordjson` and its `.aivjson` as an **Extended Wolf** test configuration named `Fixes Siege Three Catapults`. Use the editor's import and save functions, then inspect the resulting exported AIC/AIV. Select this specific configuration when starting the game. The templates use the editor's existing AIV JSON format; their successful economic construction still requires a preparation run.
2. Machines must be `[39,39,39,0,0,0,0,0]`, engineer target 12, attack variance 0, joint-attack chance 0 and harassment machines 0. A small ordinary escort remains so a natural attack can start. Do not interpret engineer target 12 as evidence that six suitable engineers exist.
3. In the game map editor create one small flat skirmish map. Put human slot 1 and AI slot 2 on opposite halves, at least 100 tiles apart, with broad unobstructed ground between them. Give the AI enough starting gold, wood, stone and food (e.g. 5000 gold, 500 wood, 500 stone, 500 food). Leave the human keep undefended and inactive; do not kill or move the observed engineers. The AI AIV needs its keep, stockpile, market, granary, housing and engineers' guild to finish. Verify the editor did not rotate or omit required placements.
4. Keep `EnableAIDistancedSiegeTents` enabled in Fixes. Save the map and retain its original file and checksum. Start with the chosen testlord; do not repeatedly tune a narrow choke point by trial and error.

## Preparation, offline validation and candidate selection

The observer writes `BepInEx/FixesEvidence/FixesSiegeTentPlacementTest_Serp/<run>/events.jsonl` and at most four `geometry-N.json` snapshots at `BuildStructure.Pre`. They contain actual native packed row/column and neighbor tables, active coordinates, edge masks, height, logic, vegetation predicate, unit/structure grids and the selected **player tile metric** layer. This search filter is not ordinary PCL.

The producer unassigns its siege-engineer tribe and then selects crew inside one native call. Tick samples may miss that intermediate state. Inspect `eligibleNow`, all engineer identities and each native selection conjunct, the previous tick and the spawn-time sample. A stored crew counter cannot substitute for this evidence. A zero intermediate count is inconclusive; do not claim that six engineer records have been proven eligible during an unobserved interval.

A geometry capture occurs after search. Identify the actual first selected tribe leader and its starting coordinates from the recorded crew/tribe state, and independently check that the sampled geometry still equals the pre-search state. If that cannot be established, model validation remains inconclusive.

Use the already built offline executable (or `build.bat /nopause /offline` to build/test only the offline project):

```powershell
& '.\Offline\bin\Release\net10.0\Offline.exe' '<geometry.json>' validate <leaderX> <leaderY> 2
& '.\Offline\bin\Release\net10.0\Offline.exe' '<geometry.json>' enumerate <leaderX> <leaderY> 2
```

`validate` checks the observed first-tent center and the producer's final origin check. A mismatch vetoes any safe-reproduction claim. Repeat validation against a later simple placement before relying on a candidate. `enumerate` varies natural first-tent starting positions and subsequent crew origins, adds only a hypothetical first tent offline and compares unchanged Fixes with the two-guard counterfactual. It keeps Vanilla checks, the working 5x5 check, the 100/200-depth search and the native player metric filter. Hypothetical occupancy is explicitly not a game-state mutation or evidence of an engineer fault.

If no candidate remains, stop map experiments. If candidates remain, prefer a cluster with several plausible crew starts. Use its logged first/next tent coordinates to position the AI assembly area in the editor; retain the broad flat control area. Do not manufacture tents through a mod or bypass natural AI placement. Save the selected map as the evidence fixture, with exact coordinates and a file hash in the run notes.

## At most three targeted runs before reassessment

1. Preparation: verify natural siege, machine list, eligible engineer records, target and broad terrain; save a useful state manually in the game UI. Obtain the simple placements used for model validation.
2. Evidence: reload that state; recheck lord, preferences, crew/global IDs, target, metric and map hash. Observe natural tentative placement, subsequent engineer movement, successful mounting or failure, losses and later machine transformation. Retain the save, map, logs, geometry snapshots and a short video showing the relevant path.
3. Control: reload the same state first, then use the documented broad staging control if necessary. Recheck conditions rather than assuming bit-identical replay. Compare engineer completion and distances in the same tick interval.

Missing conditions require a written diagnosis before another run. Do not append blind runs.

## Outcome and author evidence

Run `Review-Evidence.ps1 -RunDirectory '<run>'`. The automatic result remains INCONCLUSIVE until independent gameplay evidence is present. NOT_REPRODUCED requires the intended situation, suitable surviving crew and sufficient observation, with normal completion. CONFIRMED requires a concrete failure caused by the disputed placement plus a matched control; missing crew, combat losses, pre-existing inaccessible paths and unknown leader/state invalidate attribution. Different offline decisions or skipped direction guards alone never confirm the game bug.

Only then prepare an English report containing exact versions/hashes, settings, fixture, save, sequence and logs. Send nothing automatically. Do not modify Fixes for the evidence run.

## Record an independently checked outcome

Copy REVIEW.template.json into the evidence folder and complete it only after reviewing the actual gameplay and control. Bind both events.jsonl files and each supporting map/save/video/notes file with SHA-256, identify the reviewer and explain causality or normal completion. Run Review-Evidence.ps1 with -ReviewFile and both run directories. It accepts CONFIRMED or NOT_REPRODUCED only with all required conditions and matching evidence hashes; incomplete or changed evidence remains INCONCLUSIVE. This records a human review, not an automatic determination of gameplay causality.
