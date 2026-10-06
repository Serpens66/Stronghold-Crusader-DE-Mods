# Fixes bad-thing popularity evidence

Status: code behavior reproduced; natural gameplay effect and control still need verification.

## Enable only the selected observer

The observer is disabled by default. With the game closed create/edit `BepInEx/config/FixesBadThingPopularityTest_Serp.cfg`:

```ini
[Diagnostic]
Enabled = true
```

Enable only this observer, disable the other observer and all extra gameplay mods, then restart. Recording only activates in an isolated Fixes1.24 session. Keep `Enabled = false` for ordinary play. This avoids a separate preparation game just to create the config file.

## Setup

Use unchanged Fixes 1.24.0, SHCDESE 2.13.0, APIShared >=0.4.2 and only this observer. Disable all other gameplay mods and the siege observer. Build/install only through `build.bat /nopause`, with the game closed.

The two fixtures have byte-identical AIC and AIV. The AIV contains keep, stockpile, market, granary, housing and a gallows. Import A into the official Castle & CPU Lord Editor as an **Extended Wolf** configuration named `Fixes Popularity`. Import its AIV and save. For B use the same configuration and AIV, with only the preference file changed. Do not change the map, AI economy or lord behavior between A and B.

For Extended Lords Fixes 1.24 reads `<SourceDirectory>/Override/fixes/preferences.json`. Place the chosen fixture's preference file in that path of the editor-saved lord package. Check the Fixes load log names that file and the observer's effective preferences match. Do not rely on putting preference files beside a Custom Lord without a registered asset provider.

A preferences: `CustomMinPopularityRequiredForBadThings=9000`; leave required attempts null/absent.
B preferences: the same limit plus `CustomBuildAttemptsRequiredForBadThings=100`.

Create one flat two-player map in the game editor, human slot 1 and AI slot 2, widely separated. Supply enough AI gold, wood, stone and food; do not pre-place Bad Things. Keep the human inactive and prevent combat. The normal AI economy must reach a stable popularity in 5000..8999. If it never does, diagnose the actual reason; do not clamp popularity with another mod. Save the map, chosen lord/AIV and exact checksums.

## A/B execution

1. A: start naturally with A preferences, record the effective preferences, bad-thing flag/minimum/required-attempt state, current popularity, counters, AIV variant/unlocked step and raw Bad Thing step states. Confirm the gallows step is unlocked and buildable and that its spawn belongs to the AI's AIV, not a player/script or a pre-existing building. Keep the save immediately before the relevant build if possible.
2. B: restart from the same preparation conditions with B preferences applied before map initialization. Loading an A save does not itself prove B's native arrays were reinitialized. Recheck every effective field and AIV demand, resources, terrain and popularity. Observe at least the A build interval, preferably twice as long. A static attempt counter under B is expected below the minimum and cannot prove that AI has actually requested the blocked step.
3. If necessary, one controlled continuation after naturally reaching popularity >=9000 can demonstrate that B's pending AIV step was buildable. Otherwise mark an absent B spawn as inconclusive, not proof.

The observer is passive and uses native Building events, session events and per-tick state. It tracks 1-based game IDs plus global identities, including slot reuse. Evidence is under `BepInEx/FixesEvidence/FixesBadThingPopularityTest_Serp/<run>/events.jsonl`.

## Evaluation

Run `Review-Evidence.ps1 -RunDirectory '<A-run>' -ControlDirectory '<B-run>'`. It checks markers, versions, isolation and callback errors and lists relevant spawns. Complete the returned checklist with fixture/save evidence. CONFIRMED needs a natural Bad Thing spawn in A at popularity 5000..8999, the effective A limit 9000 and the matching B control with actually pending/buildable AIV demand. NOT_REPRODUCED needs both valid conditions and sufficient observation. Otherwise the outcome is INCONCLUSIVE.

Keep both preference files, identical AIC/AIV hashes, map/save, complete run logs and reproduction steps. Only an in-game-confirmed effect gets an English Markdown report for Rawra. No report is sent automatically; out-of-range values or incomplete preference pairs are baseline notes.

## Record an independently checked outcome

Copy REVIEW.template.json into the evidence folder and complete it only after reviewing the actual gameplay and control. Bind both events.jsonl files and each supporting map/save/video/notes file with SHA-256, identify the reviewer and explain causality or normal completion. Run Review-Evidence.ps1 with -ReviewFile and both run directories. It accepts CONFIRMED or NOT_REPRODUCED only with all required conditions and matching evidence hashes; incomplete or changed evidence remains INCONCLUSIVE. This records a human review, not an automatic determination of gameplay causality.
