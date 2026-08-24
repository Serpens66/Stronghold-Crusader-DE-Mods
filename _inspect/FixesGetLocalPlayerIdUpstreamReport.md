# Avoid resolving the local player ID while loading `PlaceGoodsyard`

## Problem

`LobbySettingsViewModel.PlaceGoodsyard` calls `GameNetworkAPI.GetLocalPlayerId()` in both its getter and setter. Lobby mod settings access this property during early startup, before a local lobby player exists. This produces the recurring `GetLocalPlayerId = -1` warning.

The current `Math.Max(1, GameNetworkAPI.GetLocalPlayerId())` fallback also treats slot 1 as the local player. That can write the unresolved local preference into the wrong companion-array slot when the player later resolves to another ID.

## Suggested fix

Keep the local `PlaceGoodsyard` preference independently from `PlaceGoodsyardData` and do not query the local player ID from early property access. After the local player ID has been safely resolved, copy the preference into the matching companion-array slot and publish it through the normal per-player settings path. Never use slot 1 as a fallback for an unresolved player.

This preserves the synchronized per-player data while avoiding both the false startup warning and an incorrect provisional slot assignment.
