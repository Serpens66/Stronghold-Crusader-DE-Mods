# Fixes 1.19.0: a custom lord's AI overrides can carry over to the next map

## Problem

Fixes stores several per-player AI settings in native arrays that live for the whole game process. After a map loads, `FixesMapEvents.OnMapPostLoad` updates an array entry only when the current lord has a matching `preferences.json`. If the next map puts a different lord without such preferences in the same player slot, the previous lord's entry remains active.

The affected settings are wheat-sale category, minimum gold for harassment siege engines, stone-to-oxen ratio, and maximum oxen. The latter three also have value arrays whose old values remain with the enabled flags.

## Evidence

- `src/shcde-fixes/Detours/AIDetours.cs` allocates the four flag arrays once in the detour constructor. Its native hooks consult those arrays during AI decisions.
- `src/shcde-fixes/Events/FixesMapEvents.cs`, lines 18–64, runs after each map load. It writes an entry only after `CustomLordPreferences.TryGetValue(lordName, out preferences)` succeeds. There is no clearing step for players whose new lord has no entry.
- `src/shcde-fixes/Events/FixesAIEvents.cs` adds a preferences entry only when that custom lord's asset provider has `override/fixes/preferences.json`. Thus a missing entry is an expected case.
- No other source path resets these arrays between maps. They belong to the process-lived `NativeStateBlock`, outside the native map state that is reinitialized.

## How to reproduce

Without restarting the game, load map A with a custom lord that has a distinctive override in player slot 1. Then load map B with a different lord lacking Fixes preferences in slot 1. Compare that lord's AI behavior or the relevant native flag/value entries with a fresh game start directly into map B. Map B can retain map A's override.

## Suggested fix

At every map transition, clear each per-player flag and restore default values before applying the current map's preferences. Then a player without a matching entry uses the normal behavior.

The missing reset path and persistent arrays are confirmed by source review. A two-map runtime test has not yet been run.
