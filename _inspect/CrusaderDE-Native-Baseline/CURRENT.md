# Current CrusaderDE Native Baseline

Current canonical DLL SHA-256:

`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

For future chats, link this file and require the DLL hash to be checked before using any address or conclusion from the baseline.

## Vanilla versus mod knowledge

- The raw baseline, the semantic exports, the Ghidra/Rizin projects, the managed and XAML exports and the version diff below describe only the Vanilla game files.
- Knowledge documents under `sem/FBCB9319/knowledge` are grouped below by scope. The machine-readable assignment is [document-scopes.json](./sem/FBCB9319/knowledge/document-scopes.json).
- Each document title ends with `[Scope: ...]`. In mixed documents, headings ending with `[mod integration]` describe our mods; in mod and external documents, headings ending with `[Vanilla facts]` hold reusable Vanilla facts. Documents whose exact bytes are pinned by validators stay unmarked; they are listed under `markers.untagged` in `document-scopes.json`.
- For Vanilla questions use the Vanilla sections first. Mod, testmod and external-component documents are not Vanilla evidence by themselves.

## Vanilla raw and semantic artifacts (Vanilla only)

Start with the [scan report](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/SCAN_INFO.md).

Primary reusable artifacts:

- [Rizin 0.8.1 `aaa` project](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/rizin/CrusaderDE-rizin-0.8.1-aaa.rzdb)
- [Ghidra 12.1.3 project](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/ghidra/CrusaderDE-Ghidra-12.1.3.gpr)
- [Searchable decompiler output](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/decompiled-functions.c)
- [Ghidra functions](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/functions.jsonl)
- [Ghidra references](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/xrefs.jsonl)
- [Ghidra strings](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/strings.jsonl)
- [Raw Rizin exports](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/)

Semantic reverse-engineering baseline:

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

## Knowledge tooling

- [NativeKnowledge in Ghidra](./tools/semantic/NATIVE_KNOWLEDGE.md): all 88 APIShared catalog fields and four double-reviewed function records, including newly named building-height-offset and unit-standing-height helpers; confidence, provenance, historical views and reader documentation remain visible; raw baseline unchanged.

## Vanilla behavior audits

Only Vanilla behavior; Script Extender names appear only as vocabulary.

- [AI resource reserve audit](./sem/FBCB9319/knowledge/AI_RESOURCE_RESERVE.md)
- [Bedouin healer target-selection audit](./sem/FBCB9319/knowledge/BEDOUIN_HEALER_TARGETS.md): candidate-list exclusions, selection, movement and healing flow.
- [Cursor dispatcher: verified boundary and embedded tables](./sem/FBCB9319/knowledge/UNIT_COMMAND_CURSOR_BOUNDARY.md)
- [Gatehouse query pass](./sem/FBCB9319/knowledge/GATEHOUSE_QUERY_PASS.md): DLL_RunTick → game tick → building dispatcher → gatehouse handler chain, one query pass per gate and engine run without native writes between queries, conditional Extender tick; confirmed by disassembly.
- [Health-bar rendering audit](./sem/FBCB9319/knowledge/HEALTH_BAR_RENDERING.md): visible-map render paths, selection gates, HP storage and candidate hook spans.
- [Keep and campground ground: verified records](./sem/FBCB9319/knowledge/KEEP_CAMPGROUND_GROUND.md)
- [Keep main-flag rotation audit](./sem/FBCB9319/knowledge/KEEP_FLAG_ROTATION.md): view-dependent Vanilla micro-anchors, Keep-relative rotation, and projectile coordinate contracts.
- [Minimap (radar) pass 0xF5A40 (FBCB9319)](./sem/FBCB9319/knowledge/RADAR_PASS_F5A40.md)
- [Surrender selection and multiplayer checksum audit](./sem/FBCB9319/knowledge/SURRENDER_SELECTION.md): selected Lord field, checksum buckets, deselection and trace evidence.

## Vanilla audits with mod integration

Vanilla audits that also contain mod-specific parts; see the `[mod integration]` headings and `document-scopes.json`.

- [Lord home relocation](./sem/FBCB9319/knowledge/LORD_HOME_TELEPORT.md): failed return paths, building-attack target-loss trigger and deliberately preserved invalid-component rescue, now integrated into BugfixesAndQoL with a default-enabled host option; human return-path suppression confirmed by four real blocked calls in singleplayer, other gameplay/MP cases pending.
- [AI economy observation boundaries](./sem/FBCB9319/knowledge/AI_ECONOMY_OBSERVATION.md): tax settlement ABI, Pre-only tax notifications, separate AI trade execution and accessibility-group/removal evidence; static audit, runtime acceptance pending.
- [AI negative sell thresholds](./sem/FBCB9319/knowledge/AI_SELL_THRESHOLDS.md): phantom-credit cause, all eight categories, native decision/transaction guards and original-function execution evidence; in-game acceptance pending.
- [AI woodcutter construction gate](./sem/FBCB9319/knowledge/AI_WOODCUTTER_BUILD.md)
- [AIV defensive build range](./sem/FBCB9319/knowledge/AIV_BUILD_RANGE.md): map-size limits, Keep reference, full-footprint range, Baibars reproduction, ExtraFeatures API ownership, integrated BugfixesAndQoL AI-only EEF90 detour, target-relative probe allocation, and type-zero AIV cell-clear import semantics.
- [AIV lobby selection audit](./sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md): candidate order, rotation branches, thresholds, and completed-castle state boundary.
- [Assassin direct gatehouse climb: native audit](./sem/FBCB9319/knowledge/ASSASSIN_DIRECT_GATEHOUSE_CLIMB.md)
- [Assassin reconstruction repair](./sem/FBCB9319/knowledge/ASSASSIN_RECONSTRUCTION_REPAIR.md): E1640 mode-3 one-way connection failure, per-unit full-map repair cost and the APIShared 0xE19C0 correction; confirmed by runtime replay and executed fixture.
- [Enemy-unit proximity during building actions](./sem/FBCB9319/knowledge/ENEMY_BUILD_PROXIMITY.md): separate civilian/military radii, unchanged building/wall restrictions, native predicate and migration contracts.
- [Building repair contract (CrusaderDE.dll FBCB9319…F2831E2)](./sem/FBCB9319/knowledge/BUILDING_REPAIR.md)
- [Building sleep and local selection audit](./sem/FBCB9319/knowledge/BUILDING_SLEEP_SELECTION.md): Chore 43's building-type payload, local selection use and runtime-confirmed resource divergence.
- [Civilian worker pause audit](./sem/FBCB9319/knowledge/WORKER_PAUSES.md): shared fear-building visit logic, worker comparisons, and the baker/miller post-delivery branches.
- [Coop Trail Customize presentation](./sem/FBCB9319/knowledge/COOP_TRAIL_CUSTOMIZE_PRESENTATION.md): managed team-shield/radar refresh, setup metadata and input-callback suppression; static/member checks and pending visual acceptance.
- [Crenelated wall construction](./sem/FBCB9319/knowledge/CRENELATED_WALLS.md): complete human/AI placement and redraw flow, sticky high pieces, read-only native availability, ownership decoding and corrected current Chore-25 field contract.
- [Drawbridge AI decision audit](./sem/FBCB9319/knowledge/DRAWBRIDGE_AI_DECISIONS.md): player siege planning, candidate/task consumption, physical transitions and passive focused capture.
- [Drawbridge native contracts](./sem/FBCB9319/knowledge/DRAWBRIDGES.md): tile topology, shared building height, three rendering paths, unit height and verified RedBird spans.
- [Drawbridge stored-route contracts](./sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md): packed Unit path origin/cursor, existing movement publisher and passive route evidence; full installed-hash evidence under EnemyGateBuildingContextAudit/bridge-stored-path-evidence.txt.
- [Dry mixed-group admission before order mutation](./sem/FBCB9319/knowledge/MANUAL_GROUP_ADMISSION.md)
- [Dry mixed-group movement recovery](./sem/FBCB9319/knowledge/MANUAL_GROUP_REGION_RECOVERY.md)
- [Editor map lifecycle audit](./sem/FBCB9319/knowledge/EDITOR_MAP_LIFECYCLE.md): native creation/loading paths, managed completion, and event boundaries.
- [Editor terrain region publication](./sem/FBCB9319/knowledge/EDITOR_REGION_REFRESH.md): missing terrain invalidation, shared water/moat path, other editor tools and locked pre-cursor correction; native fixture evidence and GUI acceptance limits.
- [Enemy gate building-query player contract](./sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md): selected opponent versus leader control, zero query role, one-based leader indexing, and unresolved runtime attribution.
- [Engine presentation bridge](./sem/FBCB9319/knowledge/ENGINE_PRESENTATION_BRIDGE.md): Merit snapshot ownership, achievement/save ordering, managed run lock scopes and direction-filter register/flag liveness; fixture evidence, in-game acceptance pending.
- [Farm construction preview](./sem/FBCB9319/knowledge/FARM_BUILD_PREVIEW.md)
- [Fireman targeting and extinguishing audit](./sem/FBCB9319/knowledge/FIREMAN_TARGETING.md): target selection, states, compound fires, one-throw extinguishing and verified detour entry.
- [Foreign troop HUD player colors](./sem/FBCB9319/knowledge/FOREIGN_TROOP_PLAYER_COLORS.md): current owner mapping, loaded-player remapping, material tint versus UI portrait order, and pending in-game acceptance.
- [Foreign troop HUD skins](./sem/FBCB9319/knowledge/FOREIGN_TROOP_SKINS.md): current unit material, Monk/Temple Guard render flow, colored portrait variants and isolated view regression coverage; in-game visual acceptance remains separate.
- [Formation fallback and native movement permission](./sem/FBCB9319/knowledge/FORMATION_MOVEMENT_PERMISSION.md)
- [Selected movement marker records](./sem/FBCB9319/knowledge/LINEUP_MARKERS.md): native sprite/category order, bounded marker identities, frame reset and isolated LineUp recovery.
- [Gatehouse capture and true unit life](./sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md): complete capture/death/removal flow, low-word death marker, combat predicate and permanent capture-filter boundary.
- [Gatehouse damage and drawbridge destruction](./sem/FBCB9319/knowledge/DRAWBRIDGE_DESTRUCTION.md): native bridge damage immunity, premature direct-hit removal, lethal/demolition paths and pending standalone fix acceptance.
- [Gatehouse policy: native baseline and evidence limits (2026-10-13)](./sem/FBCB9319/knowledge/ENEMY_GATE_VANILLA_EVIDENCE.md)
- [Gatehouse target marker height](./sem/FBCB9319/knowledge/GATEHOUSE_TARGET_MARKER_HEIGHT.md): complete marker render path, gate wall-height discrepancy, tower/keep counterchecks and managed consumer correction.
- [HUD presentation scheduling audit](./sem/FBCB9319/knowledge/HUD_PRESENTATION.md): Vanilla writers, render-state flow, visibility and active-consumer boundaries.
- [Incoming gold and periodic amounts](./sem/FBCB9319/knowledge/PERIODIC_GOLD.md): signed positive-only delivery, pending versus direct debit, overflow limits and static evidence.
- [Independent manual gatehouse and drawbridge control](./sem/FBCB9319/knowledge/GATE_BRIDGE_AUTOMATION.md)
- [Lobby chat layout](./sem/FBCB9319/knowledge/LOBBY_CHAT_LAYOUT.md): shared Coop/Skirmish managed renderer, fixed-width name and forced line break, local compact presentation and foreach/finally insertion boundary; native visual acceptance pending.
- [Lobby save loading and Skirmish load-button visibility](./sem/FBCB9319/knowledge/LOBBY_SAVE_LOADING.md): solo-host eligibility, absent-human removal, persistent frontend path and consumer acceptance limits.
- [Long simulation ticks from AI group movement (FBCB9319)](./sem/FBCB9319/knowledge/SIM_TICK_SPIKE_PATHING.md)
- [Lord defeat loot audit](./sem/FBCB9319/knowledge/DEFEAT_LOOT.md): winner attribution, direct gold and incoming-goods awards, and the native skip branch.
- [Lord defeat percentages and message capacity](./sem/FBCB9319/knowledge/KILL_REWARD_PERCENT.md): exact goods/cleanup fields, signed-short gold messages, bounded transfer and existing-span integration.
- [Lord AIC slots and optional media lookup](./sem/FBCB9319/knowledge/LORD_MEDIA_LOOKUP.md): Vanilla AIC-slot remapping, normal media fallback and independently reachable SE registration failures.
- [Minimap last-frame HD speech wait](./sem/FBCB9319/knowledge/NOTIFICATION_LAST_FRAME.md): native presentation ordering, archived HD last-frame/20-second bound, managed completion safeguards and isolated regression limits.
- [Moat terrain restoration](./sem/FBCB9319/knowledge/MOAT_TERRAIN_RESTORATION.md): confirmed soil-zero writer, direct placement ordering, bridge conversion/removal, explicit replacement boundaries and private native fixture evidence; in-game acceptance pending.
- [Modsettings activation: peace time and No Dogs](./sem/FBCB9319/knowledge/MODSETTINGS_ACTIVATION.md)
- [Move-command release contract](./sem/FBCB9319/knowledge/MOVE_COMMAND_RELEASE.md): managed release state machine, native troop-input consumption and the single-command handoff rule.
- [Move-stager data anchors](./sem/FBCB9319/knowledge/MOVE_STAGER_DATA_ANCHORS.md): code anchors with second evidence for Chore staging globals, tribe/unit/path/sound/dialogue managers, strides and speech call targets; confirmed-static, derivation blocked until the shared command runtime gets an update fallback.
- [Native market price query contract](./sem/FBCB9319/knowledge/MARKET_PRICE_QUERIES.md)
- [Offline AI test tools](./sem/FBCB9319/knowledge/SPECTATOR_TEST_TOOLS.md): full ally-order request path, exact Extender acceptance-event parameters, scoped forced acceptance, delivery reserves and private native-call monitor; static evidence with in-game acceptance still outstanding.
- [Outpost static audit](./sem/FBCB9319/knowledge/OUTPOSTS.md): profiles, spawn/guard/group flows, selection, rally scope and documented runtime gaps.
- [Packaged Coop maps and Ready admission](./sem/FBCB9319/knowledge/COOP_PACKAGE_MAP_READINESS.md): direct package headers versus Vanilla multiplayer catalogs, cached verification, silent Ready rejection and lobby system-message rendering; confirmed-static managed path, two-peer acceptance pending.
- [Pause and zoom automation boundaries](./sem/FBCB9319/knowledge/AUTOMATION_PAUSE_ZOOM.md): native versus managed pause, multiplayer transport asymmetry, public camera methods and static validation limits.
- [Plague cloud height and damage](./sem/FBCB9319/knowledge/PLAGUE_HEIGHT.md): explicit wall/roof immunity, standing-height contracts and standalone PlagueHeightTest native fixture evidence.
- [Plague popularity: scheduling and identity contract](./sem/FBCB9319/knowledge/PLAGUE_POPULARITY.md)
- [Player defeat observation audit](./sem/FBCB9319/knowledge/PLAYER_DEFEAT.md)
- [Poleturner and tanner idle-delay audit](./sem/FBCB9319/knowledge/WORKSHOP_IDLE_DELAY.md): material predicates, fear breaks and permanent inline-hook boundaries.
- [Popularity contribution parameters](./sem/FBCB9319/knowledge/POPULARITY_PARAMETERS.md)
- [Prebuilt AI workshop production audit](./sem/FBCB9319/knowledge/PREBUILT_WORKSHOP_BOTH.md): Both AIC values, native initialization, balancing, and the pending game-start timing check.
- [Raid target and attack access audit](./sem/FBCB9319/knowledge/RAID_RETARGET.md): target priorities, granary eligibility, command dispatch, attack-tile checks and the savegame observation.
- [Rally-point terrain slowdown audit (2026-10-08)](./sem/FBCB9319/knowledge/RALLY_TERRAIN_SLOWDOWN.md)
- [Read-only manual unit path probes](./sem/FBCB9319/knowledge/UNIT_COMMAND_PROBE_SCRATCH.md)
- [Recruit transformation and rally tracking](./sem/FBCB9319/knowledge/RECRUIT_TRANSFORMATION.md): same-slot recruitment, transient AliveState 4, finalization and consumer lifetime contract.
- [Remapped-player Lord spawn audit](./sem/FBCB9319/knowledge/LORD_SLOT_REMAP.md): map-slot reassignment, stale Lord identity fields and the Vanilla spawn suppression path.
- [Savegame metadata without the original map](./sem/FBCB9319/knowledge/SAVEGAME_MISSING_MAP_METADATA.md): confirmed Vanilla map-availability rejection, embedded Lord presentation, safe serialization boundary and desktop-tested managed testmod.
- [Savegame modsettings load boundaries](./sem/FBCB9319/knowledge/SAVEGAME_MODSETTINGS_LOAD.md): multiplayer map-load routing, installed archive-context mismatch, Solo/Coop boundaries and readiness validation limits.
- [Situational unit audio](./sem/FBCB9319/knowledge/SITUATIONAL_UNIT_AUDIO.md): complete Arabian idle sword-draw production, buffer transport, per-play volume and permanent hook contracts.
- [Spectator building and wall placement](./sem/FBCB9319/knowledge/SPECTATOR_BUILD_PLACEMENT.md)
- [Spectator report path](./sem/FBCB9319/knowledge/SPECTATOR_REPORTS.md): selected view index, native report buffer, managed Army gate, and Food action controls.
- [Tannery animation transparency audit](./sem/FBCB9319/knowledge/TANNER_ALPHA.md): native building updater, alpha source, renderer, and simulation tick boundary.
- [Trail import list and local deletion](./sem/FBCB9319/knowledge/TRAIL_IMPORT_DELETION.md)
- [Trail Maker custom AIC export and confirmation overlay](./sem/FBCB9319/knowledge/TRAIL_CUSTOM_AIC_EXPORT.md)
- [Tree Blight and woodland decay](./sem/FBCB9319/knowledge/TREE_BLIGHT_DECAY.md): common Vanilla/RandomEvents path, native mode distinction, cumulative thresholds, blocked decay, permanent interception and pending game acceptance.
- [Unified mission lifecycle audit](./sem/FBCB9319/knowledge/MISSION_LIFECYCLE.md): gameplay, save, editor and multiplayer operation boundaries and central mode permissions.
- [Vanilla peace-time gameplay audit](./sem/FBCB9319/knowledge/VANILLA_PEACE_TIME.md): all 36 active-flag references, mode-gated gameplay predicates, and the starting-troop dispatcher entry contract.
- [Weighted Assassin search cost](./sem/FBCB9319/knowledge/ASSASSIN_WEIGHTED_SEARCH_COST.md): D9C40 depth limit 800 and climb-fallback rules, the post-load AI target burst with whole-component searches toward unreachable targets, and the exact backward unreachability proof; runtime evidence on the 8-lord stress save.

## Mod and testmod work (not Vanilla)

Design, diagnosis, acceptance and migration records of our mods and testmods. Use only their `[Vanilla facts]` sections as Vanilla knowledge.

- [Assassin attack control test](./sem/FBCB9319/knowledge/ASSASSIN_ATTACK_CONTROL_TEST.md)
- [Assassin gate-policy diagnosis](./sem/FBCB9319/knowledge/ENEMY_GATE_ASSASSINS.md): separate native/weighted/cached requests, exact directed-edge attribution and explicitly unmeasured native cache/fallback hits.
- [CastlePlanner visual observation](./sem/FBCB9319/knowledge/CASTLE_VISUAL_OBSERVATION.md): construction and native/managed graphics boundaries, read-only diagnostic snapshots and pending Wolf-6 reproduction evidence.
- [Enemy bridge general diagnosis evidence](./sem/FBCB9319/knowledge/ENEMY_BRIDGE_GENERAL_DIAGNOSIS.md)
- [Enemy gate capture adapter contract](./sem/FBCB9319/knowledge/ENEMY_GATE_CAPTURE_ADAPTERS.md)
- [Enemy gate policy: lateral drawbridge access (2026-10-02)](./sem/FBCB9319/knowledge/ENEMY_GATE_DRAWBRIDGE_POLICY.md)
- [ExtraFeatures session callbacks](./sem/FBCB9319/knowledge/EXTRAFEATURES_SESSION_CALLBACKS.md): Lord creation/HP normalization, persisted actual bases and market helper callers.
- [Gate routing migration, 2026-10-13](./sem/FBCB9319/knowledge/GATE_ROUTING_MIGRATION.md)
- [Opt-in pathfinding table replacements](./sem/FBCB9319/knowledge/PATHFINDING_TABLE_REPLACEMENTS.md): native 90-slot rows, installed SE 89-stride counterexample, additive APIShared replacement and runtime acceptance limits.
- [Own command / military frame observations](./sem/FBCB9319/knowledge/ENEMY_BRIDGE_PAIR_INPUTS.md)
- [Own military entry capture contract](./sem/FBCB9319/knowledge/ENEMY_BRIDGE_ENTRY_CAPTURE.md)
- [Paired planning and command: effective counts and normal flood input](./sem/FBCB9319/knowledge/ENEMY_BRIDGE_COUNT_INPUTS.md)
- [Temporary Raid / Assassin gate acceptance, 2026-10-07](./sem/FBCB9319/knowledge/TEMP_GATE_ROUTE_ACCEPTANCE.md)

## External components (Script Extender, Fixes, dependencies)

Third-party components; not Vanilla evidence by themselves.

- [Fixes 1.25.1 compatibility](./sem/FBCB9319/knowledge/FIXES_COMPATIBILITY.md)
- [Fixes wheat-sale startup correction](./sem/FBCB9319/knowledge/FIXES_WHEAT_STARTUP.md): legacy player stack slot and Fixes 1.29.1 R9/remainder/category defects, bounded in-place startup repair, native fixtures and separate gameplay acceptance limits.
- [Script Extender 2.10.1 unit and projectile hook audit](./sem/FBCB9319/knowledge/SE210_RANGE_HOOKS.md)
- [Script Extender 2.11.0 speech-path hook audit](./sem/FBCB9319/knowledge/SE_2_11_SPEECH_PATH.md): module-bound signature, RedBird displacement, continuation, and the earlier path-length defect.
- [Script Extender 2.14.1 engage-range audit](./sem/FBCB9319/knowledge/SE_2_14_1_ENGAGE_RANGE.md): full native feature path, all 35 actual RedBird spans, generated-code checks, consumer compatibility and acceptance limits.
- [Script Extender 2.14.2 source compatibility review](../../Shared/Tools/ScriptExtenderUpdate/2.14.1-2.14.2.review.md): music-only managed change; source baseline refresh, installed extender remains 2.14.1; runtime acceptance pending.
- [Script Extender 2.14.3 interop additions](./sem/FBCB9319/knowledge/SE_2_14_3_INTEROP_ADDITIONS.md): additive upstream field names, types and offsets; since 2026-10-27 the generated baseline itself is on 2.14.3.
- [UU-ImGUI shutdown audit](./sem/FBCB9319/knowledge/UU_IMGUI_SHUTDOWN.md): dependency context teardown race, normal exit path, binary identities, controlled tests, exact Unity Mono backend checks and four successful game shutdown paths.

## Queries

Typical query from the workspace root:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' stats
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' claim fn-ai-get-buy-price
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' hook 0x151436
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' gaps
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore opcode 120
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore gaps

If the local database is absent after a fresh clone, rebuild it from the tracked exports without changing the reference manifest:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\Build-SemanticBaseline.ps1' RestoreDatabase

## Offline machine and data-flow analysis

The Analysis stage adds hash-bound instruction facts, conservative liveness, selected High P-code, feature dossiers and installed-backend hook checks. See [operation and limitations](./tools/semantic/ANALYSIS.md). Queries: `instructions`, `flow`, `trace`, `global`, `feature`, `hookcheck`, `coverage`. The current database manifest selects the published schema-6 analysis generation; failed flows and unresolved audit boundaries remain explicit.

Published coverage (generation `g72CA4AA9715C`, 2026-10-27, Script Extender 2.14.3 knowledge): 4,478 native functions, 436,211 independently verified instructions, 2,752 complete selected High P-code flows and 99 existing audit dossiers. Three decompiler failures remain explicit; 1,723 functions have no selected deep flow. [Acceptance record](./sem/FBCB9319/analysis/g72CA4AA9715C/acceptance.json) confirms fresh Windows PowerShell queries, unchanged reference projects and exact schema-6 analysis reconstruction.

Additional offline queries: `slice`, `memory`, `switch`, `summary`, `type` and `changes`. They expose bounded value slices, 79,169 reconstructed memory accesses, 932 recovered layouts and 3,818 historical function comparisons. Reconstruction and audited contracts remain separate; partial flows, aliasing and decompiler diagnostics are explicit. See [usage and limits](./tools/semantic/ANALYSIS.md).

Mod sources are mutable integration snapshots. Later edits do not invalidate Vanilla machine facts or block feature and summary queries. Hook checks retain machine evidence but report incomplete current ownership when the source inventory has changed. A newly installed Script Extender assembly is reported the same way and needs `Build-SemanticBaseline.ps1 UpdateForScriptExtender`, not a new analysis generation. Concurrent baseline knowledge is preserved by publication guards.
