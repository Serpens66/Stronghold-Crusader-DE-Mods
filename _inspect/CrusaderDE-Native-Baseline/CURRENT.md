# Current CrusaderDE Native Baseline

- [CastlePlanner visual observation](./sem/FBCB9319/knowledge/CASTLE_VISUAL_OBSERVATION.md): construction and native/managed graphics boundaries, read-only diagnostic snapshots and pending Wolf-6 reproduction evidence.

- [Foreign troop HUD skins](./sem/FBCB9319/knowledge/FOREIGN_TROOP_SKINS.md): current unit material, Monk/Temple Guard render flow, colored portrait variants and isolated view regression coverage; in-game visual acceptance remains separate.

- [Weighted Assassin search cost](./sem/FBCB9319/knowledge/ASSASSIN_WEIGHTED_SEARCH_COST.md): D9C40 depth limit 800 and climb-fallback rules, the post-load AI target burst with whole-component searches toward unreachable targets, and the exact backward unreachability proof; runtime evidence on the 8-lord stress save.

- [Gatehouse query pass](./sem/FBCB9319/knowledge/GATEHOUSE_QUERY_PASS.md): DLL_RunTick → game tick → building dispatcher → gatehouse handler chain, one query pass per gate and engine run without native writes between queries, conditional Extender tick; confirmed by disassembly.

- [Engine presentation bridge](./sem/FBCB9319/knowledge/ENGINE_PRESENTATION_BRIDGE.md): Merit snapshot ownership, achievement/save ordering, managed run lock scopes and direction-filter register/flag liveness; fixture evidence, in-game acceptance pending.

- [Move-stager data anchors](./sem/FBCB9319/knowledge/MOVE_STAGER_DATA_ANCHORS.md): code anchors with second evidence for Chore staging globals, tribe/unit/path/sound/dialogue managers, strides and speech call targets; confirmed-static, derivation blocked until the shared command runtime gets an update fallback.

- [AI negative sell thresholds](./sem/FBCB9319/knowledge/AI_SELL_THRESHOLDS.md): phantom-credit cause, all eight categories, native decision/transaction guards and original-function execution evidence; in-game acceptance pending.

- [Coop Trail Customize presentation](./sem/FBCB9319/knowledge/COOP_TRAIL_CUSTOMIZE_PRESENTATION.md): managed team-shield/radar refresh, setup metadata and input-callback suppression; static/member checks and pending visual acceptance.

- [Assassin reconstruction repair](./sem/FBCB9319/knowledge/ASSASSIN_RECONSTRUCTION_REPAIR.md): E1640 mode-3 one-way connection failure, per-unit full-map repair cost and the APIShared 0xE19C0 correction; confirmed by runtime replay and executed fixture.

- [Pause and zoom automation boundaries](./sem/FBCB9319/knowledge/AUTOMATION_PAUSE_ZOOM.md): native versus managed pause, multiplayer transport asymmetry, public camera methods and static validation limits.

- [Script Extender 2.14.3 interop additions](./sem/FBCB9319/knowledge/SE_2_14_3_INTEROP_ADDITIONS.md): additive upstream field names, types and offsets; generated baseline remains on its recorded 2.14.2 provenance.

- [Minimap last-frame HD speech wait](./sem/FBCB9319/knowledge/NOTIFICATION_LAST_FRAME.md): native presentation ordering, archived HD last-frame/20-second bound, managed completion safeguards and isolated regression limits.

- [Foreign troop HUD player colors](./sem/FBCB9319/knowledge/FOREIGN_TROOP_PLAYER_COLORS.md): current owner mapping, loaded-player remapping, material tint versus UI portrait order, and pending in-game acceptance.

- [Script Extender 2.14.2 source compatibility review](../../Shared/Tools/ScriptExtenderUpdate/2.14.1-2.14.2.review.md): music-only managed change; source baseline refresh, installed extender remains 2.14.1; runtime acceptance pending.

- [Fixes wheat-sale startup correction](./sem/FBCB9319/knowledge/FIXES_WHEAT_STARTUP.md): confirmed wrong player stack slot, second-start crash evidence, synchronous startup/engine-lock limits and pending runtime acceptance.

- [AI economy observation boundaries](./sem/FBCB9319/knowledge/AI_ECONOMY_OBSERVATION.md): tax settlement ABI, Pre-only tax notifications, separate AI trade execution and accessibility-group/removal evidence; static audit, runtime acceptance pending.

- [Offline AI test tools](./sem/FBCB9319/knowledge/SPECTATOR_TEST_TOOLS.md): full ally-order request path, exact Extender acceptance-event parameters, scoped forced acceptance, delivery reserves and private native-call monitor; static evidence with in-game acceptance still outstanding.

- [Crenelated wall construction](./sem/FBCB9319/knowledge/CRENELATED_WALLS.md): complete human/AI placement and redraw flow, sticky high pieces, read-only native availability, ownership decoding and corrected current Chore-25 field contract.

- [Savegame modsettings load boundaries](./sem/FBCB9319/knowledge/SAVEGAME_MODSETTINGS_LOAD.md): multiplayer map-load routing, installed archive-context mismatch, Solo/Coop boundaries and readiness validation limits.

- [Opt-in pathfinding table replacements](./sem/FBCB9319/knowledge/PATHFINDING_TABLE_REPLACEMENTS.md): native 90-slot rows, installed SE 89-stride counterexample, additive APIShared replacement and runtime acceptance limits.

- [Gatehouse damage and drawbridge destruction](./sem/FBCB9319/knowledge/DRAWBRIDGE_DESTRUCTION.md): native bridge damage immunity, premature direct-hit removal, lethal/demolition paths and pending standalone fix acceptance.

Current canonical DLL SHA-256:

`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

Start with the [scan report](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/SCAN_INFO.md).

Primary reusable artifacts:

- [Lobby save loading and Skirmish load-button visibility](./sem/FBCB9319/knowledge/LOBBY_SAVE_LOADING.md): solo-host eligibility, absent-human removal, persistent frontend path and consumer acceptance limits.

- [Rizin 0.8.1 `aaa` project](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/rizin/CrusaderDE-rizin-0.8.1-aaa.rzdb)
- [Ghidra 12.1.3 project](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/ghidra/CrusaderDE-Ghidra-12.1.3.gpr)
- [Searchable decompiler output](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/decompiled-functions.c)
- [Ghidra functions](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/functions.jsonl)
- [Ghidra references](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/xrefs.jsonl)
- [Ghidra strings](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/strings.jsonl)
- [Raw Rizin exports](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/)

Semantic reverse-engineering baseline:

- [Gatehouse target marker height](./sem/FBCB9319/knowledge/GATEHOUSE_TARGET_MARKER_HEIGHT.md): complete marker render path, gate wall-height discrepancy, tower/keep counterchecks and managed consumer correction.

- [UU-ImGUI shutdown audit](./sem/FBCB9319/knowledge/UU_IMGUI_SHUTDOWN.md): dependency context teardown race, normal exit path, binary identities, controlled tests, exact Unity Mono backend checks and four successful game shutdown paths.

- [Script Extender 2.14.1 engage-range audit](./sem/FBCB9319/knowledge/SE_2_14_1_ENGAGE_RANGE.md): full native feature path, all 35 actual RedBird spans, generated-code checks, consumer compatibility and acceptance limits.

- [Gatehouse capture and true unit life](./sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md): complete capture/death/removal flow, low-word death marker, combat predicate and permanent capture-filter boundary.

- [AIV defensive build range](./sem/FBCB9319/knowledge/AIV_BUILD_RANGE.md): map-size limits, Keep reference, full-footprint range, Baibars reproduction, ExtraFeatures API ownership, integrated BugfixesAndQoL AI-only EEF90 detour, target-relative probe allocation, and type-zero AIV cell-clear import semantics.

- [Poleturner and tanner idle-delay audit](./sem/FBCB9319/knowledge/WORKSHOP_IDLE_DELAY.md): material predicates, fear breaks and permanent inline-hook boundaries.

- [Spectator report path](./sem/FBCB9319/knowledge/SPECTATOR_REPORTS.md): selected view index, native report buffer, managed Army gate, and Food action controls.
- [Script Extender 2.11.0 speech-path hook audit](./sem/FBCB9319/knowledge/SE_2_11_SPEECH_PATH.md): module-bound signature, RedBird displacement, continuation, and the earlier path-length defect.
- [Fireman targeting and extinguishing audit](./sem/FBCB9319/knowledge/FIREMAN_TARGETING.md): target selection, states, compound fires, one-throw extinguishing and verified detour entry.
- [Bedouin healer target-selection audit](./sem/FBCB9319/knowledge/BEDOUIN_HEALER_TARGETS.md): candidate-list exclusions, selection, movement and healing flow.
- [HUD presentation scheduling audit](./sem/FBCB9319/knowledge/HUD_PRESENTATION.md): Vanilla writers, render-state flow, visibility and active-consumer boundaries.
- [Health-bar rendering audit](./sem/FBCB9319/knowledge/HEALTH_BAR_RENDERING.md): visible-map render paths, selection gates, HP storage and candidate hook spans.

- [Drawbridge AI decision audit](./sem/FBCB9319/knowledge/DRAWBRIDGE_AI_DECISIONS.md): player siege planning, candidate/task consumption, physical transitions and passive focused capture.
- [Drawbridge native contracts](./sem/FBCB9319/knowledge/DRAWBRIDGES.md): tile topology, shared building height, three rendering paths, unit height and verified RedBird spans.
- [Outpost static audit](./sem/FBCB9319/knowledge/OUTPOSTS.md): profiles, spawn/guard/group flows, selection, rally scope and documented runtime gaps.
- [Recruit transformation and rally tracking](./sem/FBCB9319/knowledge/RECRUIT_TRANSFORMATION.md): same-slot recruitment, transient AliveState 4, finalization and consumer lifetime contract.

- [Editor map lifecycle audit](./sem/FBCB9319/knowledge/EDITOR_MAP_LIFECYCLE.md): native creation/loading paths, managed completion, and event boundaries.
- [Unified mission lifecycle audit](./sem/FBCB9319/knowledge/MISSION_LIFECYCLE.md): gameplay, save, editor and multiplayer operation boundaries and central mode permissions.
- [Vanilla peace-time gameplay audit](./sem/FBCB9319/knowledge/VANILLA_PEACE_TIME.md): all 36 active-flag references, mode-gated gameplay predicates, and the starting-troop dispatcher entry contract.
- [ExtraFeatures session callbacks](./sem/FBCB9319/knowledge/EXTRAFEATURES_SESSION_CALLBACKS.md): Lord creation/HP normalization, persisted actual bases and market helper callers.
- [Remapped-player Lord spawn audit](./sem/FBCB9319/knowledge/LORD_SLOT_REMAP.md): map-slot reassignment, stale Lord identity fields and the Vanilla spawn suppression path.
- [Move-command release contract](./sem/FBCB9319/knowledge/MOVE_COMMAND_RELEASE.md): managed release state machine, native troop-input consumption and the single-command handoff rule.
- [Keep main-flag rotation audit](./sem/FBCB9319/knowledge/KEEP_FLAG_ROTATION.md): view-dependent Vanilla micro-anchors, Keep-relative rotation, and projectile coordinate contracts.
- [AIV lobby selection audit](./sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md): candidate order, rotation branches, thresholds, and completed-castle state boundary.
- [Prebuilt AI workshop production audit](./sem/FBCB9319/knowledge/PREBUILT_WORKSHOP_BOTH.md): Both AIC values, native initialization, balancing, and the pending game-start timing check.
- [Raid target and attack access audit](./sem/FBCB9319/knowledge/RAID_RETARGET.md): target priorities, granary eligibility, command dispatch, attack-tile checks and the savegame observation.

- [Semantic overview and validation](./sem/FBCB9319/SEMANTIC_INFO.md)
- [Semantic Ghidra project](./sem/FBCB9319/ghidra/CrusaderDE-Semantic.gpr)
- [SQLite database manifest](./sem/FBCB9319/DATABASE_INFO.json); the 148 MB SQLite/FTS5 file is reproducible, retained locally and intentionally excluded from Git
- [Semantic decompiler export](./sem/FBCB9319/exports/semantic-decompiled-functions.c)
- [Managed-to-native records](./sem/FBCB9319/managed/BC8B6A39/managed-native-links.jsonl)
- [XAML index](./sem/FBCB9319/resources/xaml-index.jsonl)
- [Historical version report](./diff/17F8DD4A-FBCB9319/VERSION_DIFF.md)
- [Read-only query entry point](./tools/semantic/query.ps1)
- [Curated function claims](./sem/FBCB9319/knowledge/function-claims.jsonl), [machine hook spans](./sem/FBCB9319/knowledge/hook-spans.jsonl) and [API contracts](./sem/FBCB9319/knowledge/api-contracts.jsonl)
- [Standalone Chore reconstruction](./sem/FBCB9319/knowledge/CHORE_SYSTEM.md) with structured [opcodes](./sem/FBCB9319/knowledge/chore-opcodes.jsonl), [contracts](./sem/FBCB9319/knowledge/chore-contracts.jsonl), [observations](./sem/FBCB9319/knowledge/chore-observations.jsonl) and [evidence](./sem/FBCB9319/knowledge/chore-evidence.jsonl)
- [Surrender selection and multiplayer checksum audit](./sem/FBCB9319/knowledge/SURRENDER_SELECTION.md): selected Lord field, checksum buckets, deselection and trace evidence.
- [Building sleep and local selection audit](./sem/FBCB9319/knowledge/BUILDING_SLEEP_SELECTION.md): Chore 43's building-type payload, local selection use and runtime-confirmed resource divergence.
- [Lord defeat loot audit](./sem/FBCB9319/knowledge/DEFEAT_LOOT.md): winner attribution, direct gold and incoming-goods awards, and the native skip branch.
- [Civilian worker pause audit](./sem/FBCB9319/knowledge/WORKER_PAUSES.md): shared fear-building visit logic, worker comparisons, and the baker/miller post-delivery branches.
- [Tannery animation transparency audit](./sem/FBCB9319/knowledge/TANNER_ALPHA.md): native building updater, alpha source, renderer, and simulation tick boundary.

Typical query from the workspace root:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' stats
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' claim fn-ai-get-buy-price
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' hook 0x151436
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' gaps
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore opcode 120
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore gaps

If the local database is absent after a fresh clone, rebuild it from the tracked exports without changing the reference manifest:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\Build-SemanticBaseline.ps1' RestoreDatabase

For future chats, link this file and require the DLL hash to be checked before using any address or conclusion from the baseline.

- [Enemy gate building-query player contract](./sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md): selected opponent versus leader control, zero query role, one-based leader indexing, and unresolved runtime attribution.

- [Assassin gate-policy diagnosis](./sem/FBCB9319/knowledge/ENEMY_GATE_ASSASSINS.md): separate native/weighted/cached requests, exact directed-edge attribution and explicitly unmeasured native cache/fallback hits.

- [Drawbridge stored-route contracts](./sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md): packed Unit path origin/cursor, existing movement publisher and passive route evidence; full installed-hash evidence under EnemyGateBuildingContextAudit/bridge-stored-path-evidence.txt.

- [Situational unit audio](./sem/FBCB9319/knowledge/SITUATIONAL_UNIT_AUDIO.md): complete Arabian idle sword-draw production, buffer transport, per-play volume and permanent hook contracts.

- [Plague cloud height and damage](./sem/FBCB9319/knowledge/PLAGUE_HEIGHT.md): explicit wall/roof immunity, standing-height contracts and standalone PlagueHeightTest native fixture evidence.

## Offline machine and data-flow analysis

The Analysis stage adds hash-bound instruction facts, conservative liveness, selected High P-code, feature dossiers and installed-backend hook checks. See [operation and limitations](./tools/semantic/ANALYSIS.md). Queries: `instructions`, `flow`, `trace`, `global`, `feature`, `hookcheck`, `coverage`. The current database manifest selects the published schema-6 analysis generation; failed flows and unresolved audit boundaries remain explicit.

Published coverage: 4,478 native functions, 436,211 independently verified instructions, 2,601 complete selected High P-code flows and 69 existing audit dossiers. Three decompiler failures remain explicit; 1,874 functions have no selected deep flow. [Acceptance record](./sem/FBCB9319/analysis/g1E2FBFF53E33/acceptance.json) confirms fresh Windows PowerShell queries, unchanged reference projects and exact schema-6 analysis reconstruction.

Additional offline queries: `slice`, `memory`, `switch`, `summary`, `type` and `changes`. They expose bounded value slices, 76,794 reconstructed memory accesses, 788 recovered layouts and 3,818 historical function comparisons. Reconstruction and audited contracts remain separate; partial flows, aliasing and decompiler diagnostics are explicit. See [usage and limits](./tools/semantic/ANALYSIS.md).

Mod sources are mutable integration snapshots. Later edits do not invalidate Vanilla machine facts or block feature and summary queries. Hook checks retain machine evidence but report incomplete current ownership when the source inventory has changed. Concurrent baseline knowledge is preserved by publication guards.

- [Savegame metadata without the original map](./sem/FBCB9319/knowledge/SAVEGAME_MISSING_MAP_METADATA.md): confirmed Vanilla map-availability rejection, embedded Lord presentation, safe serialization boundary and desktop-tested managed testmod.
