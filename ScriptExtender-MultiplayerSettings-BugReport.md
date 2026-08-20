# Three independent multiplayer transport defects affect synchronized mod settings

Host-controlled mod settings are not transported reliably through all multiplayer phases. The source and a real two-player Steam test identify three independent defects:

1. A client joining an existing lobby does not receive the host's complete settings snapshot.
2. Live lobby broadcasts use a non-reliable Steam send flag and can lose larger settings packets.
3. After map start, the receive hook discards the transport sender, so host-only settings updates cannot authenticate the host.

For simulation-affecting settings, any of these defects can leave host and client with different runtime state and eventually cause a resync.

## Environment and test setup

- Stronghold Crusader Definitive Edition with BepInEx 5.4.23.2
- SHCDE Script Extender source revision `171d68e155a8f98c5f8c4ee154d9af154c9a2443`
- Two real Steam participants: one host and one remote client
- Host log: `LogOutput.log`; client log: `LogOutput_C3.log`
- Relevant appended runs begin at host line 14,077 and client line 13,821
- 13 registered lobby-mod-settings instances
- An instrumented temporary workaround was used to expose and validate each missing transport path

Before the client joined, the host changed multiple synchronized host settings, including Unit Limits, Unit Costs, Building Costs, Building Limits, and the selected Custom Coop Trail package. The selected trail package contained five missions. This is important because the test does not rely on a small change made after both players were already present.

Disk logging was set to `LogLevels = All`. The relevant runs contain 2,087 host and 1,973 client Debug lines. Script Extender calls through `LogHelper.Debug()` are nevertheless absent because that method has `[Conditional("DEBUG")]`; those call sites are removed from the tested release build. The client-side application evidence below therefore uses the mods' own Debug callbacks, which remain compiled and record the resulting runtime values.

The two computers' clocks differ by approximately three seconds. Cross-machine events are correlated by sequence and values rather than by comparing their absolute timestamps.

## Reproduction

1. Register multiplayer mod settings through `GameXAMLManagerAPI`.
2. Host a multiplayer lobby.
3. Change several host-controlled settings before the client joins. Include a serialized settings payload around 1 KiB or larger.
4. Let a remote client join and inspect whether it receives the complete current host state.
5. Change a host-controlled settings object repeatedly while both participants are in the lobby.
6. Start the match to exercise the in-game receive hook with a real packet from the host. Sending a genuine host-only settings update at this point requires test instrumentation because the settings UI is no longer available after map start.

## Runtime evidence

### Join snapshot

Before the workaround installed its detour, the host confirmed that the extender had not installed its declared join hook (host line 14,946). When the remote client joined, the forwarded callback reached the extender's existing full-sync method (host lines 16,078–16,080):

```text
[MP-SYNC-EVIDENCE BASELINE] extenderJoinDetourInstalled=False
[GameNetworkAPI] [HandleSendCustomInfoToMember] member=[<client>]
[GameXAMLManagerAPI] [SyncSettingsToNewPlayer] Syncing all mod settings to [<client>]
[MP-SYNC-EVIDENCE JOIN] forwarded member=[<client>], steamId=<client Steam ID>, registeredSettings=13
```

Immediately afterward, the client applied the host state that had been configured before joining. The following shortened excerpt combines client lines 15,329–15,374:

```text
[Debug :Building Costs] Applied building cost materials: 3
[Debug :Building Limit] Active building limit: MAPPER_WOODSMAN = 5
[Debug :Building Limit] Active building limit: MAPPER_HUNTER = 1
[Debug :Building Limit] Active building limit: MAPPER_OXENBASE = 2
[Info  :Custom Custom Trail] Found Coop Trail package [testcooptrail] with 5 mission(s).
[Info  :Custom Custom Trail] Replaced Trail1/01 from [01.coopmission.json].
...
[Info  :Custom Custom Trail] Replaced Trail1/05 from [05.coopmission.json].
[Debug :Unit Limit] Active unit limit: CHIMP_TYPE_ARCHER = 5
[Debug :Unit Limit] Active unit limit: CHIMP_TYPE_SPEARMAN = 3
[Debug :Unit Limit] Active unit limit: CHIMP_TYPE_ARAB_SLAVE = 10
[Debug :Unit Costs] Applied human extra unit cost rows: 8
```

Every quoted value matches the host's final pre-join state (host lines 15,947–16,076). This is direct client-side application evidence for the repaired join-snapshot path.

### Reliable lobby send

A pre-join Building Costs update exercised the replacement broadcast path with a 2,363-byte packet (host line 15,939). It correctly had zero eligible recipients because the client had not joined yet:

```text
[MP-SYNC-EVIDENCE RELIABLE-SEND] packetType=1100, payloadBytes=2363, eligibleRecipients=0, successfulRecipients=0
```

This evidence marker is intentionally emitted only once per process, so it does not repeat for later broadcasts. After the client joined, the host then changed the synchronized `UnitLimits` object 82 times between 17:48:07.217 and 17:48:17.218. The client recorded exactly 82 corresponding application events between 17:48:04.347 and 17:48:14.341. The final state also matches on both machines; shortened excerpts from host lines 17,397–17,416 and client lines 16,686–16,705 show:

```text
HOST:   CHIMP_TYPE_ARCHER = 5
CLIENT: CHIMP_TYPE_ARCHER = 5
HOST:   CHIMP_TYPE_ARAB_SWORDSMAN = 37
CLIENT: CHIMP_TYPE_ARAB_SWORDSMAN = 37
HOST:   CHIMP_TYPE_BEDOUIN_CAMEL_LANCER = 26
CLIENT: CHIMP_TYPE_BEDOUIN_CAMEL_LANCER = 26
HOST:   CHIMP_TYPE_BEDOUIN_SKIRMISHER = 9
CLIENT: CHIMP_TYPE_BEDOUIN_SKIRMISHER = 9
```

The one-to-one event count and identical final values provide client-side application evidence for live lobby updates through the replacement reliable path. No settings-transport deserialization, registration, origin-verification, or packet-rejection error occurs in the relevant client run.

### In-game sender propagation

The client log confirms that the test reached a real multiplayer match with two human participants and that the client was not the host (client lines 16,947–16,948):

```text
realMultiplayer=True, networkActivePlayers=2, nativeLocalPlayerId=2, platformIsHost=False, lobbyMembers=2, gameMembers=2
```

The replacement receive path then handled a real remote in-game packet with a non-null sender (client line 17,723):

```text
[MP-SYNC-EVIDENCE INGAME-SENDER] packetType=1003, payloadBytes=25, senderSteamId=<host Steam ID>, handled=True
```

This packet was a seed packet rather than a host-only mod-settings change. It validates transport-sender propagation in the affected in-game receive path; the host-only rejection mechanism described below follows directly from the current source.

## Defect 1: The join-snapshot detour is declared but never installed

`Platform_Multiplayer_Hooks` declares `platform_Multiplayer_SendCustomInfoToMember_hook`. Its callback reaches `HandleSendCustomInfoToMember()` and then `SyncSettingsToNewPlayer()`. However, `ManagedHookManager.Apply()` never creates this detour.

Consequently, the existing code intended to send all current settings to a newly joined member is unreachable in an unmodified process. The runtime baseline `extenderJoinDetourInstalled=False` confirms this in the tested build.

**Required fix:** Instantiate the existing `SendCustomInfoToMember` detour in `ManagedHookManager.Apply()`.

## Defect 2: Direct lobby broadcasts use a non-reliable send flag

The direct Steam path in `GameNetworkAPI.SendPacketToAllLobby()` passes `64` to `SteamNetworkingMessages.SendMessageToUser()`. This value does not include `k_nSteamNetworkingSend_Reliable` (`8`). The targeted-send paths use `40`, equivalent to `Reliable | AutoRestartBrokenSession`.

Small updates may appear to work, while larger serialized settings packets can be dropped. A successful API call for a small packet therefore does not demonstrate reliable synchronization.

**Required fix:** Use `40`, or the equivalent named reliable flags, in the direct lobby broadcast path.

## Defect 3: The in-game receive hook discards the transport sender

`Platform_Multiplayer_ProcessMessage_ILHook` currently calls:

```text
HandleRawPacket(data.packetType, data.data)
```

It does not pass `fromMember.steamID` as the third argument. As a result, `ReceiveCustomPacketEventArgs.SenderSteamId` is null. `ApplyHostOnlyUpdate()` cannot verify that the packet came from the lobby host and rejects the host-only update as unauthenticated.

**Required fix:** Pass `fromMember.steamID` to the third `HandleRawPacket()` parameter.

## Reference workaround

The instrumented temporary workaround used to validate all three transport paths is available [here](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/710183322146f51b02893160ef204af7fa7022c7/Shared/PresetLobbyModSettingsViewModel.cs#L955-L1280).
