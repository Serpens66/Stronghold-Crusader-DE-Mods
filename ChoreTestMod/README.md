# ChoreTestMod

`ChoreTestMod` is a minimal, state-free reproduction for
[SHCDE Script Extender issue 137](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/137).
It exercises only the public `ChoreNetworkTransport.SendRawBlob(...)` API. It does not alter the
simulation, install native hooks, or use fixed native offsets.

## Reference environment

- Stronghold Crusader Definitive Edition `2.8.0.1`
- Script Extender `1.42.0`, commit `171d68e155a8f98c5f8c4ee154d9af154c9a2443`
- Current audited `CrusaderDE.dll` SHA-256:
  `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

## What the mod sends

The host automatically queues 127 probes after a map starts. The MessagePack packet is the same
four-field schema that originally exposed the issue:

    [protocolVersion, playerId, operationId, lordGlobalId]

The values are `[1, 1, operationId, 4254]`, with operation IDs 1 through 127. Every body is seven
bytes and every complete `[packetId][body]` blob is nine bytes. The first body is exactly:

    94 01 01 01 CD 10 9E

The explicit formatter captures the raw body presented to `MessagePackReader` before decoding it.
This makes zero-filled or otherwise changed bytes visible without relying on reserialization.

## Reproduction

1. Install the same Script Extender and `ChoreTestMod_Serp` plugin directory on both peers.
2. For the clearest result, disable unrelated mods on both peers.
3. Start a fresh multiplayer match with two human players.
4. Do not pause the game. Wait until both logs contain `RECEIVE_SUMMARY` (normally less than one
   minute at normal game speed).
5. Compare the host and client `BepInEx/LogOutput.log` files.

Useful markers:

- `SERIALIZER_SELF_TEST_PASSED` proves that operation 1 serialized to `94010101CD109E`.
- `PROBE_SEND` records the exact body and complete blob passed to `SendRawBlob`.
- `PROBE_RECEIVE` records an exact byte-for-byte match at the public receive boundary.
- `CHORE_PAYLOAD_CORRUPTION_REPRODUCED` records changed bytes, fields, duplicates, or a decode error.
- `SEND_SUMMARY` and `RECEIVE_SUMMARY` report totals and missing operation IDs.

A successful reproduction is a correct host send and self-receive paired with a client-side
`CHORE_PAYLOAD_CORRUPTION_REPRODUCED` entry for the corresponding operation ID. If no mismatch is
seen, repeat with up to three fresh matches before classifying the issue as not deterministically
reproduced in that environment.

## Confirmed and unknown causes

Script Extender 1.42.0's Chore 106 handler uses its managed `_isSending` flag instead of the native
handler phase. A remote peer therefore attempts an extra unpack in native phase 2 and emits the
separate zero-length warning tracked by issue 132.

The phase-0 receiver also trusts the embedded inner length without comparing it to the native outer
Chore length. If the outer payload is shorter, the native field copy reads into the previously
cleared remainder of the durable slot and dispatches a synthetic zero-padded body instead of
rejecting it.

These are confirmed defects. The boundary at which the observed outer payload first becomes shorter
has not yet been proven. This mod deliberately reports the public send and receive boundaries so the
Script Extender can add internal tracing between them without mixing in unrelated game logic.
