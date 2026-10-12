# RandomEvents

- Adds freely configurable positive and negative random events from singleplayer missions to skirmishes
- Customize the event interval, cooldown, individual probability and minimum/maximum strength of each event type
- A host-controlled **Also affect AI players** option can include living AI opponents in all events; it is disabled by default
- Shared events use the same roll and strength for every eligible human and AI player, while individual events roll separately for each player
- Events that affect only AI players run silently without notifications, videos, or minimap markers for human players

## Compatibility and diagnostics

This mod requires the Script Extender and **APIShared 0.7.0 or newer**. Subscribe to APIShared separately when using the Steam modpack. Mod settings indicate when gameplay changes are inactive for a direct mission or Trail launch. Use **Customize** where available to start with those changes; edits made while inactive are saved for later games.

Random-event batches and signpost initialization use APIShared synchronized multiplayer transport.

Routine activity is logged at Debug level. Repeated diagnostics are limited to reduce log spam; warnings and errors remain available. No gameplay setting is needed for this change.
