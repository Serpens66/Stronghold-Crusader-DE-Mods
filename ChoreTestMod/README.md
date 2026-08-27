# ChoreTestMod

Minimal multiplayer reproduction for
[SHCDE Script Extender issue 137](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/137).

The mod sends the exact MessagePack packet that exposed remote payload truncation in ExtraFeatures.
Each Vanilla repair compares two trigger positions: Script Extender `OnBuildingRepair(Post)` and a
managed postfix after `MainViewModel.ButtonRepairFunction` has returned.

## Reference environment

- Stronghold Crusader Definitive Edition `2.8.0.1`
- Script Extender `1.42.0`, commit `171d68e155a8f98c5f8c4ee154d9af154c9a2443`
- `CrusaderDE.dll` SHA-256:
  `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- ChoreTestMod `1.0.0`

## Reproduction

1. Install identical Script Extender and `ChoreTestMod_Serp` builds on two peers.
2. Disable unrelated gameplay mods and start a fresh two-player multiplayer map.
3. Wait for `CONTROL_COMPLETE`. One unprimed control packet is sent.
4. The host places any tower. At `AliveState.IsAlive`, the mod reduces its health by five percent
   on both peers and logs `TOWER_READY`. Every later host tower is also damaged exactly once.
5. The host selects that tower and clicks the normal Vanilla repair button.
6. Wait for `CYCLE_SUMMARY` on both peers.

Every later valid repair click starts another numbered test cycle. Every host tower is prepared
automatically once so its repair button is available.

## One repair cycle

Each click sends six identical probes in this order:

1. `POSTFIX_SEND 1/3` through `3/3` after the repair-button call has queued Vanilla repair.
2. `EVENT_SEND 1/3` through `3/3` from the later `OnBuildingRepair(Post)` callback.

The receiver assigns the reliable Chore stream to `POSTFIX` and `EVENT` in the same order. A
separate `CYCLE_SUMMARY` compares both methods for every repair click.

## Exact packet

    Values:             [1, 1, 1, 4254]
    MessagePack body:   94 01 01 01 CD 10 9E
    SE blob bytes:      [dynamic packet ID, 2 bytes] + body = 9 bytes
    Native Chore bytes: [Chore 106, 4 bytes] + blob = 13 bytes

An explicit four-field formatter copies the raw MessagePack sequence before decoding. The sender
verifies the exact body before every `ChoreNetworkTransport.SendRawBlob(...)` call.

## Confirmed observations

Separate validation runs established the difference between the trigger positions:

- Unprimed controls remained `94010101CD109E`, Lord ID `4254`.
- Five probes sent from the repair-button postfix arrived remotely as `94010101000000`, Lord ID `0`.
- Three probes sent from `OnBuildingRepair(Post)` remained correct.
- Sender-side self-receives remained correct in both runs.
- No packets were missing and no decoding failed.

`OnBuildingRepair(Post)` does not reproduce the defect. It runs only when the queued Vanilla repair
is processed, which is too late to place the SE Chore directly behind RepairBuilding Chore 68. It is
kept solely as a negative control using the same packet and repair click.

The `MainViewModel.ButtonRepairFunction` postfix is required for reproduction. It sends after the
Vanilla repair has been queued and therefore preserves the faulty `RepairBuilding -> SE Chore`
ordering. The combined build compares both trigger positions in one repair cycle.

## Cause

Vanilla calls a Chore handler in separate size, pack, and unpack phases. During the receive-size
phase, the handler must publish its own payload size in a shared ChoreManager field.

Script Extender 1.42.0 Chore 106 instead reads the newly cleared destination slot, obtains an inner
length of zero, returns, and leaves the preceding Chore's size in the shared field. The receive
scheduler then copies Chore 106 using that stale size.

RepairBuilding Chore 68 leaves size `10`; the historical Script Extender packet needs size `13`.
Only 10 bytes are copied when Chore 68 is the immediate predecessor. The missing MessagePack bytes
remain zero, changing Lord ID `4254` to `0`. A preceding size of at least 13 masks the defect.

`Received SE chore with implausible length 0` alone is not the reproduction result. The proof is a
correct sender body paired with changed raw bytes on the remote peer.

## Scope

- Public `OnBuildingSpawn(Post)` and `GameBuildingManagerAPI.SetCurrentHealth(...)` for setup
- Public `OnBuildingRepair(Post)` as comparison trigger
- Managed Harmony postfix on Vanilla `MainViewModel.ButtonRepairFunction`
- Public `ChoreNetworkTransport.SendRawBlob(...)` for every probe
- No custom UI, native hook, fixed native offset, Surrender, resync, or test-packet state mutation
