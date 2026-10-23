# CastlePlanner
- Spawn a selected aivjson castle in a new game for human players at game start
- Let the host set the real-time castle selection limit from 60 to 600 seconds (default: 120 seconds)
- Load custom aivjson castles as visible blueprints ingame, so you can copy build your favorite AI castle
- Select your castle and set blueprint icon size and opacity with the **Blueprints** button in the bottom-left HUD extras area; APIShared coordinates its placement with buttons from other mods
- Restarting to spawn a completed castle preserves your active mission settings and the Trail author's restrictions

## AI castle placement checks

In the lobby, CastlePlanner shows whether the selected AI castles fit on the chosen map, with details for their possible rotations. Hover over a result for warnings about overlaps, elevated moats or drawbridges, and uncertain placement. Results that cannot be evaluated stay marked as unknown; they are not a guarantee that a castle will build successfully. The host can disable these checks in the mod settings.

## Drawbridge blueprint display

Drawbridge images now mirror consistently in composite images, depth fragments, and fallback images. Their direction follows the camera-rotated map grid independently of terrain height and flattened view.

## Compatibility and diagnostics

This mod requires the Script Extender and **APIShared 0.11.1 or newer**. Subscribe to APIShared separately when using the Steam modpack. Mod settings indicate when gameplay changes are inactive for a direct mission or Trail launch. Use **Customize** where available to start with those changes; edits made while inactive are saved for later games. Blueprints remain available during those direct launches.

Shared HUD services place the Blueprints button alongside buttons from other mods and coordinate GUI updates and preview pause commands.

Routine activity is logged at Debug level. Repeated diagnostics are limited to reduce log spam; warnings and errors remain available. No gameplay setting is needed for this change.
