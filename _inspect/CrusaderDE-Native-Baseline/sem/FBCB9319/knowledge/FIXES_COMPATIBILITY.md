# Fixes 1.25.1 compatibility [Scope: External component]

For the installed 1.26.4.0 wheat-sale crash, see the separate
[wheat startup audit](FIXES_WHEAT_STARTUP.md). Its confirmed wrong stack slot and
startup-only correction do not change the historical provenance below.

Source review date: 2026-10-07. Canonical external clone: D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Fremde Mods\shcde-fixes-main. Clean v1.25.1 source, commit 226e2e6960ad6f5992d4f5410d701161bb1cdc77, tree f65a826f16b8cc67d0c7a8acc80f41fd06796546. This identifies the reviewed source, not the installed Fixes assembly. Native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2 matches the installed DLL and CURRENT.json. Native conclusions below are scoped to that hash.

## Preference integration contracts

- CustomMinPopularityRequiredForBadThings requires CustomBuildAttemptsRequiredForBadThings. FixesMapEvents activates the override only when the attempt count is supplied; a minimum alone does not activate it. Our generated settings must supply both when requesting a popularity limit; attempts 100 retains Vanilla frequency. Internal popularity is scaled by 100: 5000 means displayed 50 and 9000 means displayed 90. This dependency is explicitly documented in the current Fixes README.
- Generated purchase preferences must contain complete threshold/amount pairs: BuyWoodAt/BuyWoodAmount, BuyIronAt/BuyIronAmount and BuyAleAt/BuyAleAmount. The README explicitly requires the wood pair. FixesMapEvents reads each nullable amount's Value when its threshold is supplied. With the corresponding arrays enabled, a missing amount can throw after partial application and skip later player settings. The R3 subscriber contains the error; this does not establish a game crash or complete map-load failure. Disabled arrays can skip the RHS through null-conditional assignment.
- Preserve independently supplied foreign values. Do not silently complete pairs, clamp representable modder values or infer an author bug solely from incomplete input or unusual limits.
- Native Bad Thing evaluation at 0x41280 is reached through the AIV-building path 0x51790/0x52270. It gates bad mappers 176/177/301..311 on popularity 5000 and attempt count 100. These describe Vanilla defaults, not additional limits to impose on custom preferences.

Source: src/shcde-fixes/Events/FixesMapEvents.cs, src/shcde-fixes/Config/CustomLordPreferencesEntry.cs and the current README's custom resource purchase and Good/Bad Thing sections.

## Current compatibility in our mods

- ExtendedData preference conversion reflects all public readable/writable properties. Missing properties keep constructor/property initializers, recursively including initialized nested objects. Unknown properties, unsupported types and lossy conversions fail with a diagnostic. Supplied values are compared structurally, with exact decimal-text normalization for JSON numbers; representable modder values are not clamped. Capture retains a complete round-trip check. Original save/snapshot and package checksums are verified before new canonical payloads are published.
- Snapshot protocol 3 and Trail requirements 2 carry nullable LordType. Custom type is -1; Extended types are configuration indexes. Missing identities are resolved only through a unique configuration name/checksum match. Runtime ambiguity fails closed instead of guessing from an alias.
- Fixes storage adapter validates reflection contracts: public Plugin.CustomLordPreferences dictionary keyed by case-insensitive Custom name; private static Preferences.ExtendedLords dictionary keyed by configuration LordType+1. Original objects and absence are restored to the correct store. Map _slots[9] are observed but never written; their precedence remains authoritative. Verification describes lord defaults separately from map override presence.
- Gatehouse snapshot verification keeps the pristine full handler hash and full human-block bytes. Live checks and instruction invariants only own B7C32..B7C38, before Fixes Farmer hook B7C39. The permanent decision hook and its complete displacement validation remain intact.
- AIAttackTest has a Fixes soft dependency and independent recruit/lord hook transactions. An enabled or unknown Fixes defensive-recruitment owner skips only the recruit capability. Unpublished failures roll back only their candidate; published hooks are never undone, and settings use data flags. Ordinary AIC tests remain independent.

## Siege direction guard

With EnableAIDistancedSiegeTents, the extra 5x5 structure-gap check runs before the directional restriction. The target guard returns 1 when IsPlayerIdValid(targetId) is true. For an invalid target, TryGetPlayerResourcesById returns false and also leads to return 1; a same-player target returns 1 directly. The directional block therefore remains unreachable.

Our siege integration may rely on the additional gap check, but must not rely on the directional block to prevent forward placement or guarantee engineer access. A resulting engineer blockade or pathfinding failure has not been demonstrated.

Source: src/shcde-fixes/Detours/AIDetours.cs, c_game_ai_find_valid_siege_tent_site_hook. API contract: shcde-script-extender/src/SHCDESE.BepInEx/API/GamePlayerManagerAPI.cs, IsPlayerIdValid and TryGetPlayerResourcesById (valid player IDs 1..8).

## PCL component-count integration

In src/shcde-fixes/Detours/PathingDetours.cs, sub_18010F150_hook replaces the load at 0x10F1B3 inside native function 0x10F150 when EnableExtendedPCLArray is enabled. Its generator loads the replacement table address into RAX, reads the component count into EAX using RCX as the component index, and replays the remaining original instructions. The Vanilla store at 0x10F1BA writes that loaded count to the global at 0x2E9CA14. The following instructions replace RAX with the current player context before continuing.

This describes the reviewed source/data flow, not a gameplay validation of the installed Fixes assembly.

A live entry to the separate neighbor-search routines 0x10DCB0 and 0x114760 is not established. The known chain to 0x10DCB0 is 0x40180 -> 0x114680 -> 0x10DCB0; references to 0x114760 in .rdata are unwind metadata. Do not infer dormant status for 0x10F150 from those separate routines: its recorded callers are 0x10C1C0,0x10D9F0 and 0x1102D0. Any new adapter to a native routine needs a complete active caller-chain audit.

## Native siege/search contracts [Vanilla facts]

- 0x2B080 processes up to eight configured machines in one call; machine IDs 39/40/58/59/60/77 map to crew 2/3/4/4/1/2 and mappers 190/191/192/193/194/358 through tables 2C80B0/2C80C8/2C8098. It detaches the total required crew from its siege-engineer tribe first, then calls 0x2C1B0 per machine.
- 0x2C1B0 scans unit game IDs 1..<native total>, requiring alive 2, native ushort owner==player, unit type 30, low word at GameUnit+29C==0, role+426==10, tribe+2D4==0 and AIstate+2BC!=8. GameUnit record size 490; the manager sentinel precedes the API's 0-based span.
- 0x1134D0 takes the chosen tribe leader coordinates into 0xEA760 at maximum depth 100, then 200 if zero. It passes filter 60 and tribe owner. Final checks use center minus 1 in each coordinate, the active-coordinate grid and origin logic mask 4A5014B1 before 0x6D580; later 0x11E960 links the created tent.
- 0xEA760 uses FIFO/visited traversal, fixed direction masks and packed per-row neighbor deltas. The Y table at 2D2E54 uses eight-byte stride (EA9A3/EAAA0), giving [-1,-1,0,1,1,1,0,-1]. Traversal starts at depth 1 and tests sites only after current depth>4. The native filter reads a signed Int16 player tile metric, selected via 2EA70DC+player*177BC, at 5759230+selector*320800*2; this is not PathConnectionGrid/PCL. Eight footprint neighbors exclude the center; reject logic mask 4A7014B1 unless the tree helper allows its bit 1000, reject bit 4, structures and units, and require height range < 12. Fixes adds its callback at that footprint check. After acceptance the traversal enqueues unvisited neighbors. No RNG occurs in this path.
- 0x107160 reads the tile organism index, then signed native vegetation state at manager+id*9C+6A. It allows state > 4 except 15. Record header 1A and GameVegetation.AliveState offset 50 explain that address.

Function names/role associations remain candidate-level where the semantic database has not confirmed symbols. A search model alone does not prove accessibility, successful mounting or causal attribution; selected-leader and relevant-state equivalence must be established before drawing those conclusions.

## Evidence limits

Static source behavior and natural-gameplay consequences are separate claims. The siege finding above has no demonstrated gameplay failure; the reviewed PCL value flow has not been validated in a gameplay test of Fixes 1.25.1. Do not create additional testmods, gameplay evidence or author reports without a new user request.

## Fixes 1.26.0 source compatibility review, 2026-10-08

Canonical clean clone HEAD 64c0637; diff against v1.25.1 adds SmarterSiegeLaddermen,
two inline hooks, distance-map logic and a cache reset. The earlier preference,
distanced-siege-tent, capture and PCL implementations are unchanged by this diff;
their documented constraints above still apply. This is source compatibility evidence,
not an installed-Fixes gameplay test.

New hook signatures uniquely match executable-section RVAs 0x111C00 (objective
selector) and 0x6A5D0 (attachment ranking) in the unchanged native hash. Released
workspace mods do not own these entries. EnemyBridgePathTest does instrument
0x111C00; the user explicitly excluded its source changes and installation from
this update. Do not interpret its existing package as compatible with Fixes 1.26.0.
The additive Extender BFS delegate and provisional objective storage are not adopted
by our update; the physical storage limit is not a confirmed semantic table capacity.

## Current Fixes 1.26.3 source review, 2026-10-08

Canonical clean clone v1.26.3: commit cbabdecac15a6e81cb45404f1a3501a4856dae39, tree 185f84b1fa6fc2faad793f2980a18d38599290e0. This supersedes the current source identity; the 1.25.1 and 1.26.0 sections above retain their historical provenance. No installed-Fixes assembly identity or gameplay result is inferred from the clone.

The full v1.25.1..v1.26.3 source diff preserves the documented popularity dependency, purchase pairs, distanced-siege-tent direction behavior, capture hook and PCL flow. SmarterSiegeLaddermen adds two inline hooks, distance-map/reset logic and a 16 MiB native state block. Their signatures uniquely resolve to 0x111C00 and 0x6A5D0 in the unchanged canonical native DLL. Release mods own neither entry; none of the 35 changed Extender engage-range spans overlaps them. EnemyBridgePathTest remains excluded due to its existing 0x111C00 ownership; its unchanged package is not validated with current Fixes.

The ladder search invokes BulkPathingDetours.c_game_pathsearch_bfs_ignoring_dynamic_occupancy and consumes shared WalkGeneration, WalkGrid, CertainPathGrid and AIPlayerObjectiveStorage scratch state. This is process-global mutable pathfinder state, not a pure read-only route query suitable for arbitrary reentrant consumer use. Our mods do not adopt this BFS path. These are source/data-flow findings, not demonstrated gamebreaking failures. No additional testmod or author report is introduced.

## Fixes 1.29.0 compatibility implementation, 2026-10-23

Source observation: the canonical clean clone and upstream main/v1.29.0 identify
commit `29c9a5dd6b05b4bf62e24916c20e9bfc49ac5c6e`, tree
`539287f9790310a215afde0035af61d8fc76c55b`. Installed canonical CrusaderDE.dll
still has SHA256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The external source trees were read only. Source compatibility, offline tests and
successful compilation do not establish in-game or real multiplayer behavior.

### Preference inheritance and native identity

Fixes `Config/Preferences.cs` resolves map `_slots` first, then a selected Custom
Lord's own named entry, then the AIC base's named entry, the base Extended entry,
and finally the selected Extended entry. Map loading also merges its overrides
over these defaults. The base comes from the selected AIC's `opponent_type`;
the displayed Lord name or the current lobby slot cannot substitute for it.
The native feature audit traced `DLL_RegisterSkirmishUser` at RVA `0x86550` and
`DLL_SetExtendedLordConfig` at `0x87350`, including the AIC copy through `0x7140`
and `0x1A8EE0`: assignment/speech fields change, while the configuration's initial
`opponent_type` remains available to Fixes' base lookup. The selected managed
configuration is checksum bound. Its `lordData` and `opponent_type` members were
also verified public in the real installed Assembly-CSharp.dll; ExtendedData's
managed preflight now checks both contracts.

ExtendedData 1.0.17 retains the meaning of `FixesJson` (selected Lord's own entry)
and captures additional sources separately, including explicit missing entries.
`FixesSources = null` is incomplete historical information; an empty list is a
complete capture without additional dependencies. Snapshot/Trail formats write
version 4; package manifests write version 2 with an independent source digest
and an explicit embedded-Trail marker. Historical formats and verified historical
digests remain readable. Package readiness checks identities, direct and inherited
digests and actual applied values, even if a prior READY acknowledgement exists.

Source requirements are merged across selected configurations before writes.
Contradictions are rejected, and original objects and original absence are retained
for rollback, selection changes and session end. Fixes `_slots` is never written.
Host/local migration projects stored direct and inherited entries first and reads
current host originals only for missing information, including when a previous
selection is still overlaid. Clients accept the completed host snapshot rather
than deriving sources themselves. Migration logs the loss of historical inheritance
information with a timestamp and persists only on normal save/export. Trail saves
now include their applied snapshot. Multiple initialization phases reuse the already
acknowledged values. Unresolvable old Lord/AIC identities fail explicitly.

### Diagnostic hook conflicts and targeted startup gates

Fixes' expanded defensive positions entry at `0x53D00` overlaps ActiveAIVDetector's
final-selection detour. ActiveAIVDetector 0.10.3 has a Fixes soft dependency and
skips only that detour when `[AllowExpandedDefensivePositionsPatch] Enabled` is
true or cannot be verified. Oracle, validator and trace installation remains
independent; disabled final capture produces no misleading missing-selection
reports. A startup Error names `fixes.cfg`, `Enabled = false` and the required
restart. When permitted, live bytes are checked before installation.

The Fixes farmland patch spans `[0x58B12,0x58B2E)`; the optional WoodSiteGuard
occupies `[0x58B26,0x58B2E)`. AICoarsePathComponentFixTest 0.1.2 checks
`[FixPlacementSelectionNotAccountingForFarmland] Enabled` before constructing
the guard, allocating its stub or patching the site. Enabled/unknown states skip
the guard with the same actionable Error policy, while coarse observation and
the independent outcome observer continue. Its existing exact live-byte check
and permanent publication rules remain in place. Neither gate changes foreign
settings or foreign/published hooks, and this implementation adds no native hook.

The other newly reviewed Fixes sites (`0x55D43`, `0x2BC40`, `0x2FE20`, `0x3E850`,
`0x10E26E`, `0x11F0E5`, `0x11BB38`) introduce no new production-mod ownership
overlap in the reviewed workspace. AIAttackTest still independently suppresses
its recruitment capability when Fixes owns that comparison and validates live
bytes. Current EnemyBridgePathTest `OwnsEntry` excludes both `0xE49D0` and
`0x111C00`; its remaining owned entries and relevant attack-body bytes are checked
before hook preparation. This supersedes the historical 1.26.x exclusion above
for the current source, without claiming a joint gameplay test. CastlePlanner
requires no behavior change for this update. No runtime changes were made to
AIAttackTest, EnemyBridgePathTest or CastlePlanner.

### Verification scope

Evidence and build/install records are under `.work/a/fixes/001` in the local
workspace. ExtendedData behavior tests cover source priority, checksum-bound
different bases, explicit absence, shared conflicts, migration, selection restore,
one-shot write failure rollback, historical formats, separate digests and save/
Trail roundtrips. FixesCompatibility.Tests compiles the actual canonical external
preference schema and tests nullable values, absent/enabled/disabled/unknown config
states and changed live bytes. Both runtime projects compile against actual game,
Extender and validated APIShared references. Diagnostic wiring guardrails are
bounded source checks, not a native runtime proof.

The generic scoped permanent-patch scanner flags the testmod's pre-existing
`VirtualProtect` startup writer; it has no dedicated exception for this experiment.
Its own `verify.ps1` and manual call-path review check the initial publication and
unpublished-candidate rollback; the new setting gate precedes that entire path.
This is a scanner limitation, not permission to toggle published patches. No game
or multiplayer session was run as part of this implementation.

### Additional review and ExtendedData 1.0.18, 2026-10-23

The managed map-default path has an additional named lookup: Fixes
`Preferences.Load` consults `CustomLordPreferences[name]` for Extended Lords as
well as Custom Lords, whereas ordinary `TryGet` consults that selected name only
for Custom Lords. The Extender obtains this name from the selected restart/lobby
slot through `GetCustomAILordNameByPlayerId` / `GetGenericSlotLordName`. ExtendedData
now captures the Extended selection's named map-default source, including absence,
alongside its ordinary AIC-base sources. Equal selected/base names share one entry.
The selected Extended entry retains its original `FixesJson` meaning. Fixes' map
slots and merge precedence remain unchanged.

When an embedded Trail uses a different registered media provider, the captured
map-default entry follows the selected media name. The old name remains a source
only when the AIC still needs it as its base. Contradictory map/base identities
reject preparation before mutation; preparation failures remain inside the normal
launch-blocking error path. Regression cases verify named values, explicit absence,
original reference restoration, migration, media remapping and shared base names.

Local-values save validation now checks the current dictionary values in addition
to the historical source digest. Comparing a saved digest to its own saved source
list alone cannot detect changed or additionally present local defaults. The same
live-source comparison is used by ordinary applied-snapshot verification.

Fresh review evidence is under `.work/a/fixes-review/001`: 87/87 ExtendedData tests,
122 Fixes startup/schema assertions, 76 AIAttackTest assertions, scoped EnemyBridge
verification and ExtendedData source/member preflights passed. ExtendedData 1.0.18
was built against the previously validated isolated SDK and installed with all 34
package hashes checked. The other 458 previously installed ActiveAIVDetector and
BugfixesAndQoL files were unchanged. Testmods were not installed; game/MP acceptance
remains open.

The unscoped EnemyBridge/Coarse verification scripts also invoke a workspace-wide
scanner. In this review that scanner rejected concurrent, unrelated APIShared
`src/Projectiles/PlagueNativeCode.cs` executable-memory code. Scoped EnemyBridge
verification passed; Coarse's dedicated checks reached that final global scan,
but its complete unscoped script is not recorded as passing. Those external work
in progress sources and scanner expectations were not modified. Build warnings
remain the existing linked JSON type-name collisions and an unused field; no
compile errors occurred.
