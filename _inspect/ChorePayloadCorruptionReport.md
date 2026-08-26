# Chore 106 corrupts small payloads on remote peers

Script Extender `1.42.0` does not always deliver the byte array passed to `ChoreNetworkTransport.SendRawBlob` unchanged to remote peers.

## Evidence

A host queued this 9-byte blob:

`52 04 94 01 01 01 CD 10 9E`

The first two bytes are the dynamic packet ID. The remaining MessagePack body represents `[1, 1, 1, 4254]`.

- Sender execution decoded `[1, 1, 1, 4254]`.
- Remote execution of the same packet ID decoded `[1, 1, 1, 0]`.
- Both peers used the same packet formatter and mod build.
- The different synchronized state caused a multiplayer resync.

The payload is far below every documented size limit. The current game DLL still uses the same Chore phases and relevant manager offsets, and native handler-table entry `106` originally points to an empty stub. This is not a payload-size-limit, serializer, Chore-ID-collision, or shifted-offset problem.

Work item [#132](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/132) already covers the separate phase-`2` zero-length warning. Its analysis correctly notes that this phase error alone does not explain a payload that is valid at the beginning and zero-filled at the end.

## Required fix

Trace the outer length and a payload hash at these Chore 106 boundaries:

1. `SendRawBlob` input;
2. sender durable slot after packing;
3. remote outer Chore block before materialization;
4. remote durable slot immediately before phase `0` dispatch.

Fix the first boundary where length or hash changes. Before dispatch, also require `outerLength == 4 + innerLength` and reject any mismatch instead of copying zero-filled bytes beyond the received payload.
