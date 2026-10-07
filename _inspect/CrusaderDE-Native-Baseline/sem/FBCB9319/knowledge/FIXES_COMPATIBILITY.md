# Fixes 1.25.1 compatibility

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

## Native siege/search contracts

- 0x2B080 processes up to eight configured machines in one call; machine IDs 39/40/58/59/60/77 map to crew 2/3/4/4/1/2 and mappers 190/191/192/193/194/358 through tables 2C80B0/2C80C8/2C8098. It detaches the total required crew from its siege-engineer tribe first, then calls 0x2C1B0 per machine.
- 0x2C1B0 scans unit game IDs 1..<native total>, requiring alive 2, native ushort owner==player, unit type 30, low word at GameUnit+29C==0, role+426==10, tribe+2D4==0 and AIstate+2BC!=8. GameUnit record size 490; the manager sentinel precedes the API's 0-based span.
- 0x1134D0 takes the chosen tribe leader coordinates into 0xEA760 at maximum depth 100, then 200 if zero. It passes filter 60 and tribe owner. Final checks use center minus 1 in each coordinate, the active-coordinate grid and origin logic mask 4A5014B1 before 0x6D580; later 0x11E960 links the created tent.
- 0xEA760 uses FIFO/visited traversal, fixed direction masks and packed per-row neighbor deltas. The Y table at 2D2E54 uses eight-byte stride (EA9A3/EAAA0), giving [-1,-1,0,1,1,1,0,-1]. Traversal starts at depth 1 and tests sites only after current depth>4. The native filter reads a signed Int16 player tile metric, selected via 2EA70DC+player*177BC, at 5759230+selector*320800*2; this is not PathConnectionGrid/PCL. Eight footprint neighbors exclude the center; reject logic mask 4A7014B1 unless the tree helper allows its bit 1000, reject bit 4, structures and units, and require height range < 12. Fixes adds its callback at that footprint check. After acceptance the traversal enqueues unvisited neighbors. No RNG occurs in this path.
- 0x107160 reads the tile organism index, then signed native vegetation state at manager+id*9C+6A. It allows state > 4 except 15. Record header 1A and GameVegetation.AliveState offset 50 explain that address.

Function names/role associations remain candidate-level where the semantic database has not confirmed symbols. A search model alone does not prove accessibility, successful mounting or causal attribution; selected-leader and relevant-state equivalence must be established before drawing those conclusions.

## Evidence limits

Static source behavior and natural-gameplay consequences are separate claims. The siege finding above has no demonstrated gameplay failure; the reviewed PCL value flow has not been validated in a gameplay test of Fixes 1.25.1. Do not create additional testmods, gameplay evidence or author reports without a new user request.
