# Intermittent tail corruption in Script Extender Chore payloads

## Summary

An authenticated host-only Surrender action was queued through `ChoreNetworkTransport.SendRawBlob`. The host received the original MessagePack body, while the client received the same dynamic packet with its final field replaced by zero. The host therefore killed the Lord and the client correctly rejected the mismatched action, causing Vanilla to detect divergent simulations and start a resync.

## Evidence

- Host queue at `2026-08-26 14:33:55.678`: `playerId=1`, `operationId=1`, `lordGlobalId=4254`, `bodyHex=94010101CD109E`, `blobHex=520494010101CD109E`.
- Host execution at `2026-08-26 14:33:55.833`: decoded body `94010101CD109E`; Lord unit `37`, global ID `4254`, was killed.
- Client receipt at `2026-08-26 14:33:54.997` (client clock): decoded body `9401010100`; the same player and operation arrived with `lordGlobalId=0`, while the client's current Lord was also unit `37`, global ID `4254`.
- The client log repeatedly contains `Received SE chore with implausible length 0, discarding`, often immediately around otherwise valid Script Extender Chores.
- Earlier multiplayer runs successfully delivered the same seven-byte Surrender body shape, so the corruption is intermittent rather than a deterministic serializer mismatch.

## Relevant implementation

`BulkChoreDetours.c_game_chore_106_handler_impl` writes a four-byte inner length followed by the raw blob through two calls to `c_game_chore_pack_field_hook_impl`, then writes `totalPackedBytes` directly to `ChoreManagerVA + 0x84CD4`. The receive path mirrors those calls. The exact native packing/cursor lifetime and the direct size write should be audited for slot reuse, cursor reset, and partial or zero-filled durable payloads.

## Current-game compatibility findings

- Installed game DLL: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, size `3451392`, PE compile timestamp `2026-08-19 13:01:20`.
- Installed Script Extender: `1.42.0`; its manifest and documentation declare support for game version `2.8.0.1`, and its reference archive predates the installed DLL.
- Nevertheless, all three Chore signatures still resolve uniquely in the current DLL: queue RVA `0x23990`, receive RVA `0x23E70`, and pack-field RVA `0x1F5F0`.
- Current native queue code still reads `ChoreManager + 0x84CD4` as the payload length, uses `+0x84CD8` as the temporary payload buffer, and enforces the same `1260`-byte durable-slot ceiling. Therefore the observed corruption is not explained by a simply shifted `0x84CD4` field.
- The unsupported game version remains a compatibility risk and the Chore path should be revalidated as part of the update, but the available evidence points to an intermittent packing, slot, or payload-lifetime defect rather than a deterministic stale-offset failure.

## Requested fix

Please add raw length and payload-hash diagnostics at the pack, native receive, unpack, and dispatch boundaries, then ensure the complete `[length][blob]` region is stored atomically in the durable Chore slot. A corrupted or incomplete Chore must produce the same deterministic no-op on every peer; otherwise the sender can execute its intact local copy while remote peers reject damaged data and desynchronize.
