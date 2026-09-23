# Fixes 1.19.0: a custom lord's AI overrides can carry over to the next map

## Problem

Fixes stores several per-player AI settings in native arrays that live for the whole game process. After a map loads, `FixesMapEvents.OnMapPostLoad` updates an array entry only when the current lord has a matching `preferences.json`. If the next map puts a different lord without such preferences in the same player slot, the previous lord's entry remains active.

The affected settings are wheat-sale category, minimum gold for harassment siege engines, stone-to-oxen ratio, and maximum oxen. The latter three also have value arrays whose old values remain with the enabled flags.

## Evidence

- `src/shcde-fixes/Detours/AIDetours.cs` allocates the four flag arrays once in the detour constructor. Its native hooks consult those arrays during AI decisions.
- `src/shcde-fixes/Events/FixesMapEvents.cs`, lines 18–64, runs after each map load. It writes an entry only after `CustomLordPreferences.TryGetValue(lordName, out preferences)` succeeds. There is no clearing step for players whose new lord has no entry.
- `src/shcde-fixes/Events/FixesAIEvents.cs` adds a preferences entry only when that custom lord's asset provider has `override/fixes/preferences.json`. Thus a missing entry is an expected case.
- No other source path resets these arrays between maps. They belong to the process-lived `NativeStateBlock`, outside the native map state that is reinitialized.
- Runtime test with Fixes 1.19.0: in one game process, map 1 used `testlord_serp_fixesprobe` for AI player 2. Its preferences enabled all four options. Fixes logged applying all four, and the diagnostic mod read all four native flags as `true` after load. Map 2 then used `testlord_serp` for the same player. This lord has no Fixes preferences. Immediately after map 2 loaded, all four flags were still `true`. The diagnostic log recorded `mapSequence=1` and `mapSequence=2`, both for player 2, with the previous and current lord names. The three numeric value arrays were not read in this test; their persistence follows from the same source path.

## How to reproduce

Without restarting the game, load map A with a custom lord whose `Override/Fixes/preferences.json` enables one of these options in an AI player slot, for example player 2. Then load map B with a different lord without a Fixes preferences file in the same slot. Inspect the flag for that slot after each map load. It remains enabled on map B, even though map B's lord has no corresponding preference. In our test, all four flags persisted.

## Suggested fix

At every map transition, clear each per-player flag and restore default values before applying the current map's preferences. Then a player without a matching entry uses the normal behavior.

The four retained flags were reproduced across two maps in one process. The persistence of the numeric value arrays is supported by source review but was not separately measured at runtime.
