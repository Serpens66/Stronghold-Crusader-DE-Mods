# Repeated `GameNetworkAPI.GetLocalPlayerId` warnings during local skirmish

In a local skirmish, `GamePlayerManagerAPI.GetSelectedChimps()` reaches `GameNetworkAPI.GetLocalPlayerId()`. The latter treats the session as networked because game members exist, but the local Steam ID has no assigned multiplayer slot. It returns `-1` and logs `Local SteamID [...] has no assigned slot yet` on every call. UI paths call the selection reader repeatedly, so one underlying slot mismatch becomes a warning flood.

The local player ID from `GamePlayerManagerAPI.GetLocalPlayerId()` remains valid. Vanilla publishes the selected unit count, IDs, and types together in `GameData.lastGameState` after `EngineInterface.CopyPlayStateStruct`. Mods can consume that completed state without asking the network slot resolver. We have switched our consumers to a shared, demand-driven snapshot of those fields.

Suggested Extender fix: in `GetSelectedChimps()`, avoid resolving the local multiplayer slot when the selection can be read from the completed local play state. If the network resolver is still needed elsewhere, distinguish a local skirmish with no assigned Steam slot from a pending multiplayer slot and suppress repeated warnings for that expected state. Preserve explicit diagnostics for an actual multiplayer slot failure.
