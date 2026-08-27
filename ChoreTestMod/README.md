# ChoreTestMod

Minimal, state-free reproduction for
[SHCDE Script Extender issue 137](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/137).
The mod uses only `ChoreNetworkTransport.SendRawBlob(...)`. It has no button, native hook, fixed
offset, or simulation mutation.

## Reference environment

- Stronghold Crusader Definitive Edition `2.8.0.1`
- Script Extender `1.42.0`, commit `171d68e155a8f98c5f8c4ee154d9af154c9a2443`
- `CrusaderDE.dll` SHA-256:
  `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

## Cause

Vanilla calls a Chore handler in separate size, pack, and unpack phases. During the receive-size
phase, a handler must publish its payload size in a shared ChoreManager field. Script Extender
1.42.0 distinguishes only `_isSending` from receiving. It therefore tries to unpack Chore 106 from
the newly zeroed destination slot during the size phase, reads an inner length of zero, returns,
and leaves the previous Chore's payload size in the shared field.

Vanilla then copies the incoming Chore 106 using that stale size. A preceding size of 10 or 11
copies too little of the 13-byte historical Surrender payload. The zeroed slot remainder changes
the final MessagePack value from Lord ID `4254` to `0`. This is timing-dependent because every
intervening Chore can replace the shared size. Vanilla Chores 12, 21, 28, and 68 use size 10;
another local Script Extender send can leave size 11.

## Reproduction test

The first attempt starts at tick 100. Every peer sends one harmless precondition packet whose last
small integer identifies the attempt:

    Values:             [1, 1, 0, attempt]
    First body:         94 01 01 00 01
    Blob bytes:         7
    Native packed size: 11

One tick later, only the peer that was host at map start sends the exact historical target:

    Values:             [1, 1, 1, 4254]
    MessagePack body:   94 01 01 01 CD 10 9E
    Blob bytes:         9
    Native packed size: 13

The local precondition attempts to leave size 11 on each peer. Other Vanilla Chores can replace the
shared size before the target is received, so the pair runs 12 times at five-second intervals. This
samples different immediately preceding Chore states without changing the historical target body.
A host migration cannot create an extra target send.

## Reproduction

1. Install identical Script Extender and `ChoreTestMod_Serp` builds on both peers.
2. Start a two-player multiplayer map and let it run for about one minute.
3. Compare both `BepInEx/LogOutput.log` files after `SUMMARY` appears.

Relevant markers:

- `SERIALIZER_SELF_TEST_PASSED`: both exact bodies were produced locally.
- `PRECONDITION_SEND`: the peer queued the seven-byte blob that sets native size 11.
- `TARGET_SEND`: the host queued the historical nine-byte blob.
- `TARGET_RECEIVE_MATCH`: the target arrived unchanged.
- `CHORE_PAYLOAD_CORRUPTION_REPRODUCED`: target bytes, fields, or decoding changed.
- `SUMMARY`: reports all 12 target receives, corruptions, and missing targets.

The expected reproduction is `TARGET_RECEIVE_MATCH` on the host and
`CHORE_PAYLOAD_CORRUPTION_REPRODUCED` on the client. The formatter records the raw MessagePack body
before decoding, so `94 01 01 01 CD 00 00` remains visible instead of being normalized to
`94 01 01 01 00` by reserialization.
