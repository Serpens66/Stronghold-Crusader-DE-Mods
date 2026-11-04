# CastlePlanner
- Spawn a selected aivjson castle in a new game for human players at game start
- Let the host set the real-time castle selection limit from 60 to 600 seconds (default: 120 seconds)
- Load custom aivjson castles as visible blueprints ingame, so you can copy build your favorite AI castle
- Select your castle and set blueprint icon size and opacity with the **Blueprints** button in the bottom-left HUD extras area
- Restarting to spawn a completed castle preserves your active mission settings and the Trail author's restrictions
- Spawned castles include the AIV's additional stockpile, granary and armoury sections where the normal placement rules allow them; blocked locations stay empty

## AI castle placement checks

In the lobby, CastlePlanner shows whether the selected AI castles fit on the chosen map, with details for their possible rotations. Hover over a result for warnings about overlaps, elevated moats or drawbridges, and uncertain placement. Results that cannot be evaluated stay marked as unknown; they are not a guarantee that a castle will build successfully. The host can disable these checks in the mod settings.

## AI castle plan for spectators

Multiplayer spectators can view the accepted AI castle plan of the player they are watching as a read-only blueprint, including the source AIV file name when the session provides it. Eliminated players get the same view; they cannot choose, rotate or confirm a castle, including from a panel opened before elimination.

## Compatibility

This mod requires the Script Extender and **APIShared 0.26.0 or newer**. Subscribe to APIShared separately when using the Steam modpack. Mod settings indicate when gameplay changes are inactive for a direct mission or Trail launch. Use **Customize** where available to start with those changes; edits made while inactive are saved for later games. Blueprints remain available during those direct launches.

All multiplayer participants need matching modpack and APIShared versions. Castle spawning applies to new games; loading a saved game does not spawn the chosen castle again.
