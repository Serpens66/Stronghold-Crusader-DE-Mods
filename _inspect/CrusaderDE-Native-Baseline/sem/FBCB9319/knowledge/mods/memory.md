# Manager globals, record layouts, field catalog and Script Extender interop [Scope: Mods]

Native hash FBCB9319. Summary state 2026-11-27. Vanilla facts are in [vanilla/memory.md](../vanilla/memory.md); this file covers our tools, mods and testmods and external components. Paths are workspace-relative; code locations are a working-tree snapshot of 2026-11-27.

## Read-only state access tools

### `Shared/Tools/GameMemReader` (net10.0 console, read-only)
- Opens the game with `PROCESS_VM_READ` only; needs no mod. Resolves the manager globals with the Script Extender's own AOB patterns and caches them per native hash and Extender source ([globals table](../vanilla/memory.md#consolidated-manager-and-record-table)).
- Field names and offsets come from the installed `SHCDESE.dll` by reflection; every struct size is checked against the runtime size (`layout <Type>` prints the table).
- Commands: `status`, `units`/`unit <id>`, `buildings`/`building <id>`, `players [id]` (with `--fields a,b|*`, `--all-fields`, `--where field=value` incl. enum names), `struct <Type> <addr>`, `read <type> <addr>`, `raw <addr> <len>`, `grid <GridName> x y [w h]`, `globals`, `types`, `layout`; common `--repeat`, `--interval`, `--compact`.
- Full unit-array read (11.7 MB) about 2.4 ms. Outside reads are not tick-consistent (see [read consistency](../vanilla/memory.md#read-consistency-during-the-tick)).
- Also the client of GameStateProbe: `eval`, `ptypes`, `members`, `probe '<json>'`, `--samples n --every k`, `--source probe --context tick`.
- Status: working with Script Extender 2.14.4.0 on 2026-11-14 ([RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md#tools-and-usage-mod-integration)).

### `Testmods/GameStateProbe` (BepInEx, `NetworkMode=0`, development only)
- Line-JSON server on `127.0.0.1:47811`. Context `tick` (inside `OnTick`, consistent between ticks), `main` (Unity main thread, works while paused) or `auto` (tick while simulation runs).
- Commands: `mem`/`memv` (in-process copy, each page checked with `VirtualQuery`; invalid addresses rejected cleanly), `eval "Type::Member..."` (managed reads; only Get/Is/Has/Can/Count/Find/Contains/ToString; `SetCurrentYear` rejected; `MainViewModel.Instance` refused while `viewModelLoaded` is false), `types`, `members`, `watch`, `quit`.
- Cost: one queue check per tick when idle; tick-consistent copy of the full unit array via probe about 300 ms incl. base64. Use `--fields` or `struct` for large measurements.
- Confirmed query starting points (2026-11-14, SE 2.14.4.0): `GameUnitManagerAPI::Instance.GetAllAliveUnits()`, `GameUnitManagerAPI::Instance.GetUnitManager().*`, `GameTimeManagerAPI::Instance.GetCurrentYear()/GetCurrentDay()`, `GameData::Instance`, `MainViewModel::viewModelLoaded`, `ptypes SHCDESE.API.Game`, `members GameTimeManagerAPI --instance`, `players 1 --source probe --fields r_CurrentPopularity,r_TotalPeasants,r_ExistingPeasants`.
- Unattended run recipe: install `GameStateProbe_Serp` + `AutoLoadSave_Serp`, set `RandomEvents_Serp` aside, start via `steam://rungameid/3024040`, poll `GameMemReader status` every 5 s until `unitsTotal > 0` and the tick advances (save loaded in < 5 min), run queries, end with `probe '{"cmd":"quit"}' --context main` (game exits by itself), save `LogOutput.log`, restore plugins.
- Limits: neither tool writes game state (`quit` is the only state-changing command); no 10,000-unit load test yet.

### Live inspection workflow
- Read-only rules (handles, bounds, hash check against CURRENT.json, `moduleBase + rva` only) are in [LiveProcessReadOnlyInspection.md](../../../../../../Findings/LiveProcessReadOnlyInspection.md). PowerShell note there: avoid helper names like `ri` (collides with `Remove-Item`).

## APIShared NativeKnowledge
- Location `APIShared/src/NativeKnowledge` (namespace `APIShared.NativeKnowledge`; System/System.Core/System.Memory only). Files: `NativeFieldCatalog.cs`/`.Data.cs`, `NativeFieldIds.cs`, `NativeFieldReader.cs`, `NativeKnowledgeContracts.cs`, `functions.json`, `FELDKATALOG.md`, `FUNKTIONEN.md`, `INTEGRATION.md`. The tracked export is [native-field-catalog.json](../native-field-catalog.json).
- Use permanent `NativeFieldIds.Fxxxxxx`; `NativeFieldReader.TryGetCurrent(out reader)` is published in APIShared's `LibraryLoaded` sequence right after the native hash step and survives startup cleanup; unknown hashes return `UnsupportedImage`. `NativeRecordView` takes a caller-owned span plus `NativeRecordTypes` family; caller must supply a valid pointer on the owning game thread and not keep views across callbacks or map transitions. Failure returns default(T) plus a reason.
- Contract examples: `F000054` death marker (not proof of projectile death; does not replace `APIShared.UnitAccess.IsReallyAlive`); `F000055` capture eligibility needs `NativeFieldReadPolicy.AllowUncertainMeaning`.
- Tests: `NativeKnowledgeTests` in the game-independent Core.Tests; authoring ledger `catalog-identities.json` (append-only) under `tests/NativeKnowledge`.
- Status: source integration 2026-10-22 without build/installation/version change by request; build, test and gameplay checks were due at the first authorized build (not re-verified here).
- Ghidra import: `Build-SemanticBaseline.ps1 NativeKnowledge` (optionally `-NativeKnowledgeSourceDirectory APIShared/src/NativeKnowledge`); details in [NATIVE_KNOWLEDGE.md](../../../../tools/semantic/NATIVE_KNOWLEDGE.md). Acceptance 2026-10-25: an index-driver mistake replaced an ignored SQLite cache inside a published generation; it was reconstructed (original physical hash not recovered); the driver now writes `CrusaderDE-knowledge.sqlite` beside the semantic artifacts.

## Move-stager anchors in the command runtime
- The cursor move stager `0x195E30` is detoured by our command runtime `UnitCommandPathRuntime` (audit 2026-10-22 named BugfixesAndQoL; the runtime now lives in `Pathfinding/src/UnitCommands`, contract in `Pathfinding/src/UnitCommands/Native/NativeDetourContracts.cs`). Derivations of anchors must therefore read the unmodified load image (`CrusaderLibraryLoadContext.Memory`), not live code.
- ExtendedShiftCommandQueue and Formation (incl. FormationSpeech), now under `Pathfinding/src`, depend on this runtime, which is bound to the reference hash (about 40 fixed detour prefixes, about 100 native constants). Until it gets the anchor-based update fallback, a game update disables these features (fail closed). Formation-core constants `TribeUnitCountOffset 0x5C` and `MovementTargetAvailability 0x3A11EA4` have no anchor yet ([MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md#limits)).
- The anchor set is a second evidence for the pattern-resolved FormationSpeech and gate-routing entries. No runtime acceptance of a derivation exists.

## Hook spans registry
[hook-spans.jsonl](../hook-spans.jsonl) (4 entries, all hash FBCB9319):

| Span | RVA range | Kind | Owner | Notes |
|---|---|---|---|---|
| `hook-ai-buy-price-entry` | 0xCEB10..0xCEB1A | detour entry, 10 bytes | ExtraFeatures `AIMarketVanillaPriceHook.cs` | clobbers RAX/ECX; trampoline replays two instructions |
| `hook-ai-sell-price-entry` | 0xCEB90..0xCEB9A | detour entry | ExtraFeatures `AIMarketVanillaPriceHook.cs` | paired spans do not overlap |
| `hook-monk-movement-decision` | 0x151436..0x15144A | inline decision | ExtraFeatures `MonkAlwaysRunPatch.cs` | PUSH/POP preserves CMP flags; explicit branch to running `0x15144C` or walking `0x151463` |
| `hook-fireman-target-search-entry` | 0xB8F40..0xB8F4A | NativeDetour, RedBird 1.5.0 Indirect | BugfixesAndQoL `WaterboyNativeDefinition.cs` | 10 displaced bytes confirmed by 2026-09-24 runtime log |

`api-contracts.jsonl` is empty (no API contracts recorded).

## External components

### Script Extender versions (snapshot)
- Installed 2.14.4.0 on 2026-11-14 ([RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md)); local clone `shcde-script-extender` at tag v2.14.6 on 2026-11-27. Generated baseline moved to v2.14.3 on 2026-10-27 (generation `g72CA4AA9715C`; only Ghidra input change was the `r_AILastStandPhase` header rename).
- Release-hook inventories: `Shared/Tools/ScriptExtenderUpdate/<from>-<to>.release-hooks.json` (the audits cite the older path `Shared/ScriptExtenderUpdate`).

### 2.10.1 range and projectile hooks ([SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md))
- Pair: tag v2.10.1, commit `a8ba202f…`. `GamePlayerManagerAPI.GetSelectedChimps()` became parameterless (local player); the count API keeps its player argument. Our command/editor callers reject an active player different from the local player; `SelectedUnitInfo` IDs are game IDs.
- Hooks: per-type range loader at `0x18EB20` (only when its flag is active; no Serps mod enables it); 2.10.0 hook at `0x19B786` replaced the range-table load; 2.10.1 hotfix captures the sign-extended attacker type at `0x19B64E` into the third shadow-space slot (unique AOB match); projectile inline hook at `0xA1E0E` (declared min size `0x17`, RedBird rounds to whole instructions) computes extended-range launch speed; thirteen ranged update hooks on compares of unit `+0x8FE`; interaction-range loader near `0x8D769`.
- Neighbours: ImprovedHunters manual-attack sites `0x18EB36`, `0x18ED96`; BugfixesAndQoL attack-pair gate `0x8D72B` and peace-time site `0x8D7C4` (owner as of the 2.10.1 audit); Fixes optionally hooks seven wildlife update entries and patches unit `+0x916` (selection speed).
- Known: `GetProjectilesAsSpan()` header-inclusive slot-view bug remains, local adapter stays; early crash-handler false alarm has no fix evidence; 365 patterns found; no in-game run of the range paths claimed.

### 2.11.0 speech-path hook ([SE_2_11_SPEECH_PATH.md](../SE_2_11_SPEECH_PATH.md))
- `BulkAIDetours.cs:92` module-bound `AddInline` at `0xFEF21` (15 displaced bytes, continuation `0xFEF30`), commit `48fd239d…`; RedBird.X64 1.5.0 probe confirms relocation and a 256-byte allocation in RCX/RDX. Pattern `resolutionKind=unknown`, so no Ghidra label. No local release hook or Fixes patch overlaps.
- The hook does not fix the Vanilla long-path overflow (see [message path](../vanilla/memory.md#message-and-speech-path)); 2026-09-27 log showed a 134-byte `NotSendingGoods` path (user reported playback OK). Upstream length review of `0xEF20` and the 256-byte buffer remains open. Probe: `Shared/Tools/ScriptExtenderUpdate/2.10.4-2.11.0.HookProbe`.

### 2.14.1 engage-range hooks ([SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md))
- Release diff only touches `BulkUnitDetours.cs` and CHANGELOG; installed SHCDESE.dll 2.14.1.0 SHA-256 `E5D78FDF…`. Evidence retained in `_inspect/ScriptExtender2141Audit`.
- 35 inline hooks (15..20 displaced bytes; RedBird 1.5 rounds `max(minHookSize,14)` to whole instructions) on compares of native `+0x8FE`: Archer `0x13FAA1`, `0x13FAD1`, `0x13FBCF`, `0x14135C`, `0x141527`; ArabBallista `0x171DBD`, `0x171EDD`; ArabBow `0x167F93`, `0x167FC3`, `0x1680D4`, `0x1692A7`, `0x1693D6`; Ballista `0x161E3D`; ArabSlinger `0x16B3DE`, `0x16B4DF`, `0x16C51B`, `0x16C69C`; BedouinHeavyCamel `0x17A6F3`, `0x17A801`; BedouinSkirmisher `0x178856`, `0x178886`, `0x1789B9`, `0x178C0E`, `0x179EA2`, `0x179F50`; Xbowman `0x141F47`, `0x14203F`, `0x142E5A`; ArabGrenadier `0x170733`, `0x170834`, `0x171679`, `0x171714`; ArabHorseman `0x16E62C`, `0x16E73F`, `0x16E97A`. No external entry lands inside an overwritten span.
- Emitter contract: original compare first; spare GPR pushed; `CurrentContextUnitId` (1-based game ID, never a span index) via r13d; vanilla branch restores flags and registers; override branch sign-extends the Int16 threshold and restores r13-r15 before the replacement compare so the following conditional branch sees the intended flags; balanced stack. Offline interpreter: 35,280 cases over all sites (not gameplay).
- Consumers: no release mod uses engage-range overrides or owns these spans; ImprovedHunters partial AOBs near two heavy-camel sites resolve inside hunter `0x12FC70..0x131422` (no overlap). Fixes v1.26.3 ladder hooks `0x111C00`/`0x6A5D0` do not overlap; `EnemyBridgePathTest` competing `0x111C00` owner stays excluded. AffectedMods/BuildMods empty.
- Acceptance: gameplay check (ranged search, attack, retreat after startup cleanup) remains with the user; crash-handler false-alarm note still open.

### 2.14.3 interop additions ([SE_2_14_3_INTEROP_ADDITIONS.md](../SE_2_14_3_INTEROP_ADDITIONS.md))
- Source-only renames (`r_AILastStandPhase`, `r_CanAutoEngageInMelee`, `r_AttackingUnitGlobalId`) and widening of ten `GameTribe` ranged-attacker fields to UInt32 two bytes earlier; `GameTribe` stays 1672 bytes. No mod source, version, dependency or installed file changed (2026-10-19). Catalog keeps old halfwords as alternatives.

## Sources
- [RUNTIME_STATE_ACCESS.md](../RUNTIME_STATE_ACCESS.md), [MOVE_STAGER_DATA_ANCHORS.md](../MOVE_STAGER_DATA_ANCHORS.md), [SE210_RANGE_HOOKS.md](../SE210_RANGE_HOOKS.md), [SE_2_11_SPEECH_PATH.md](../SE_2_11_SPEECH_PATH.md), [SE_2_14_1_ENGAGE_RANGE.md](../SE_2_14_1_ENGAGE_RANGE.md), [SE_2_14_3_INTEROP_ADDITIONS.md](../SE_2_14_3_INTEROP_ADDITIONS.md), [hook-spans.jsonl](../hook-spans.jsonl), [NATIVE_KNOWLEDGE.md](../../../../tools/semantic/NATIVE_KNOWLEDGE.md).
