# Multiplayer mod settings are not synchronized reliably

> **Validation status:** Draft. The source-level defects are confirmed, but this report must not be submitted until a new real host/client multiplayer test correlates the `[MP-SYNC-EVIDENCE ...]` workaround markers with successful client-side setting application and the absence of resyncs.

## Impact

- Clients that join after the host changed mod settings can keep their local defaults, causing divergent simulation state and resyncs.
- Larger host setting updates sent in the lobby can be dropped completely, while small updates may appear to work.
- Host-only setting packets received after the map starts are rejected because the sender is reported as unknown.

## Faults

1. `Platform_Multiplayer_Hooks` declares `platform_Multiplayer_SendCustomInfoToMember_hook`, and its callback calls `GameNetworkAPI.HandleSendCustomInfoToMember`, but `ManagedHookManager.Apply()` never installs that detour. Therefore `GameXAMLManagerAPI.SyncSettingsToNewPlayer()` is not reached on join.
2. `GameNetworkAPI.SendPacketToAllLobby()` calls `SteamNetworkingMessages.SendMessageToUser(..., 64, 2)` and labels `64` as reliable. The reliable flag combination used elsewhere in the same class is `40`; `64` permits unreliable fragmented messages to be discarded.
3. The `Platform_Multiplayer.processMessage` IL hook calls `HandleRawPacket(packetType, data)` without loading the `MPGameMember fromMember` argument. Consequently `ReceiveCustomPacketEventArgs.SenderSteamId` is null and `GameXAMLManagerAPI.ApplyHostOnlyUpdate()` rejects the packet.

## Expected fix

- Install the existing `SendCustomInfoToMember` detour.
- Use reliable send flags (`40`) for direct lobby broadcasts.
- Pass `fromMember.steamID` to `HandleRawPacket` from the in-game receive hook.
