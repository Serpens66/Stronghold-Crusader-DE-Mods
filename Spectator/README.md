# Spectator

- Switch the displayed player, their reports and the ally view through the portrait bar as a spectator or as an eliminated player with spectator views
- Optional: as a spectator from the start of a singleplayer skirmish, control the player selected in the portrait bar with normal Vanilla input (off by default)
- Let eliminated human players keep watching the match with spectator views (host option); their original player slot stays unchanged
- Open the detailed Vanilla statistics screen during the match
- Show the lord overview with the crown button; the host can allow extra enemy statistics for active players

## Test tools

Spectators in singleplayer and eliminated players with spectator views can use test tools for AI lords: free buildings and units, goods and gold, AI orders, powers and gates. A separate AI lord test mode offers these tools to active single-player participants. Both options are **off by default**, are disabled in real multiplayer and are not available in the map editor.

When you place a building for an AI lord with the test tools, the camera stays where you are looking. Without the test tools, eliminated players with spectator views cannot build from the build menu.

Drag the toolbar to move it or dock it beside the build menu. Switching the viewed player keeps normal camera navigation available.

### Goods and gold

Choose the recipient, good and a positive whole-number amount, then click **Credit** or **Debit**. Credit adds gold immediately; goods arrive as storage space becomes available. Debit removes the amount from stored and incoming reserves, stopping at zero. Your own resources are not spent. Amounts that would exceed the game's balance limit are rejected with an explanation.

Click directly in the amount field to edit it. **Enter** or **Esc** finishes editing and returns keyboard control to the game; it does not execute a credit or debit. An empty field becomes 0 when editing ends.

## Compatibility

This mod requires the Script Extender and **APIShared 0.27.0 or newer**. Subscribe to APIShared separately when using the Steam modpack. Local options (views, control, statistics, lord overview and test tools) can be changed by every player; eliminated-player spectator views and extra enemy statistics follow the host settings.

All multiplayer participants need matching modpack and APIShared versions. The optional singleplayer control setting requires player perspectives, applies only to spectators from the start of a skirmish and leaves the selected player's AI active.
