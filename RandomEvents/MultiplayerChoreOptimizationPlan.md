# RandomEvents multiplayer Chore optimization plan

Status: handoff plan, not implemented  
Created: 2026-08-21  
Scope: reduce RandomEvents multiplayer Chore payload size and retry frequency without weakening deterministic or fail-closed simulation behavior

## Objective

RandomEvents already uses the correct two networking layers, but its initialization Chore currently duplicates lobby configuration and always carries the complete runtime state. The implementation should:

1. keep host-owned configuration on the existing shared lobby-modsettings path;
2. use Script Extender Chores only for tick-relevant runtime state and simulation actions;
3. make initialization retries idempotent and reuse one operation ID and one serialized snapshot;
4. reduce the initialization payload, especially the fixed 135-entry individual-cooldown array;
5. retain fail-closed validation, deterministic state digests, direct `ChoreNetworkTransport.SendRawBlob(...)`, and synchronized execution on every peer;
6. preserve the existing save format unless a change is demonstrably required.

This plan does **not** authorize changes to the canonical local Script Extender fork. Per [`../AGENTS.md`](../AGENTS.md), upstream Script Extender changes must be reported to its author instead.

## Required reading and relevant code

- [`src/RandomEventsPlugin.cs`](src/RandomEventsPlugin.cs)
  - `OnCrusaderLibraryLoaded(...)` registers all network packet types before lobby settings.
  - `LobbyModSettingsPresetRegistration.Register(...)` is already the canonical settings path.
- [`src/RandomEventsSettingsViewModel.cs`](src/RandomEventsSettingsViewModel.cs)
  - all gameplay configuration is already `[SyncHostOnly]`;
  - relevant values are `EnableMod`, `IntervalMonths`, `CooldownMonths`, `MultiplayerEventModeIndex`, 15 chances, and six strength ranges.
- [`../Shared/PresetLobbyModSettingsViewModel.cs`](../Shared/PresetLobbyModSettingsViewModel.cs)
  - contains the shared preset system and temporary Script Extender multiplayer-settings workaround;
  - do not add a RandomEvents-specific settings transport, roster poller, join snapshot, or resync packet.
- [`src/RandomEventsRuntime.cs`](src/RandomEventsRuntime.cs)
  - constants and packet registration: `ChoreProtocolVersion`, `InitializeCommand`, `ExecuteBatchCommand`, `InitializeSignpostsCommand`, `InitializeNetwork()`;
  - map state creation: `InitializeMultiplayerMap(...)`, `CreateFreshState()`;
  - retry logic: `ProcessInitializationHandshake(...)`, `TryQueueInitializationChore()`;
  - Chore transport: `TrySendChore(...)`;
  - ACK handling: `OnInitializationAckPacketReceived(...)`, `TryCompleteInitializationHandshake()`;
  - receive/apply paths: `ApplyInitializationChore(...)`, `ApplyBatchChore(...)`, `ApplySignpostInitializationChore(...)`;
  - state validation and save restoration: `ValidateInitializationPacket(...)`, `ValidateLoadedState(...)`.
- [`src/RandomEventsChorePacket.cs`](src/RandomEventsChorePacket.cs)
  - current 22-field union packet and explicit formatter;
  - the observed corruption occurs while reading current field 16, `BatchPrepared`.
- [`src/RandomEventsInitializationAckPacket.cs`](src/RandomEventsInitializationAckPacket.cs)
  - ordinary network control-plane ACK; keep an explicit formatter and stable numeric keys.
- [`src/RandomEventsDiagnostics.cs`](src/RandomEventsDiagnostics.cs)
  - serializer round-trip checks, body hashes, action digests, and full save-state digest.
- [`src/RandomEventsSaveStateV2.cs`](src/RandomEventsSaveStateV2.cs)
  - canonical persisted full-size state; currently `CurrentVersion = 4`;
  - full cooldown arrays remain useful for simple runtime indexing and save persistence even if the network representation becomes compact.
- [`src/SignpostPlacementService.cs`](src/SignpostPlacementService.cs)
  - signpost initialization is already a separate synchronized Chore and derives deterministic placement from state.
- Working ExtraFeatures Chore examples:
  - [`../ExtraFeatures/src/SingleBuildingPauseHook.cs`](../ExtraFeatures/src/SingleBuildingPauseHook.cs)
  - [`../ExtraFeatures/src/KnightDismountRuntime.cs`](../ExtraFeatures/src/KnightDismountRuntime.cs)
  - [`../ExtraFeatures/src/QuarryPileRelocationRuntime.cs`](../ExtraFeatures/src/QuarryPileRelocationRuntime.cs)

## Verified multiplayer evidence from 2026-08-21

Host log:

`E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log`

Client log used successfully during this analysis:

`\\192.168.0.186\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log`

The configured named UNC path may depend on Windows name resolution. If it is unavailable to the tool process, use the proven IP/share path above after verifying that the client still owns that address.

Observed behavior:

- A real two-human-player match was confirmed by `GameModeSnapshot` and roster `[1,2]`.
- The RandomEvents initialization started at approximately `19:52:36` and completed at approximately `20:00:17`.
- The host generated operations 1 through 154 at roughly three-second intervals.
- Within the examined 20-minute client window, 84 initialization payloads failed MessagePack decoding.
- Failures consistently occurred at current packet field 16 (`BatchPrepared`), offset 219 or 220. A Boolean was expected, but the next byte was `0x00` and was reported as an integer.
- Initialization operation 154 eventually decoded, produced the same serialized body/hash on both peers, was acknowledged by player 2, and completed the handshake.
- Twelve subsequent RandomEvents Chores executed successfully.
- The client also logged 99 `Received SE chore with implausible length 0, discarding` warnings.
- Host and client binaries were identical:
  - `RandomEvents.dll`: `5AC83FAAEEE24F725F4486DE23E9855411E93F808D239BC5A264A235A9F12EE2`
  - `SHCDESE.dll`: `CC49562CD4416C5BD2F5BE9CE8F473818037EC279C842FC66EEFB57F9CCA29EE`
  - `CrusaderDE.dll`: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`
- Local serializer round trips succeeded before every send. This and the eventual identical successful payload rule out a simple formatter-version mismatch.

Upstream issue [shcde-script-extender work item 132](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/132) explains the length-zero warning as a false extra remote phase: a valid small Chore is later dispatched successfully. That warning alone is therefore not a RandomEvents failure. It remains plausible, but not proven, that the same missing native-phase gate exposes a partially packed larger buffer in the RandomEvents case. Payload reduction is a worthwhile mitigation and design improvement, but it must not be represented as a proven fix for the upstream phase bug.

## Architectural boundary: settings versus Chores

### Existing shared modsettings path is authoritative for configuration

Do not send these values again in the initialization Chore:

- `EffectiveEnabled` / `EnableMod`;
- `IntervalMonths`;
- `CooldownMonths`;
- `MultiplayerMode`;
- all event chances;
- all strength minimums and maximums.

They are already host-owned `[SyncHostOnly]` settings and are synchronized through [`LobbyModSettingsPresetRegistration.Register(...)`](../Shared/PresetLobbyModSettingsViewModel.cs). The common workaround covers pre-join snapshots, reliable large lobby updates, and authenticated ingame host-setting updates. RandomEvents must not create a parallel settings packet.

### Chores remain authoritative for dynamic simulation state

The following data must not be moved into lobby modsettings:

- private host PRNG state;
- absolute scheduling baseline and next due month;
- elapsed shared/individual cooldown state;
- a prepared event batch, if one exists in a loaded save;
- synchronized signpost initialization command;
- actual event execution actions.

Lobby settings are configuration, are not tick-synchronized, and intentionally stop changing during a running match. Chores are therefore still the correct system for the dynamic state above.

## Recommended target design

### 1. Create one canonical configuration snapshot and digest

Add an internal immutable representation, for example `RandomEventsConfigurationSnapshot`, constructed from `RandomEventsSettingsViewModel` after the map starts and settings synchronization has stopped.

It should contain normalized values in a stable order:

1. enabled flag;
2. interval;
3. cooldown;
4. multiplayer mode;
5. 15 chances;
6. six strength minimums;
7. six strength maximums.

Requirements:

- Clamp and validate in one place; do not independently normalize differently on host and client.
- Compute a canonical SHA-256 digest from explicit binary fields, not from reflection or contractless serialization.
- Carry the digest as a 32-byte binary value in the initialization Chore. Convert to hex only for logs.
- The client computes its own digest from the already synchronized settings and rejects initialization without ACK if it differs.
- Include both host-supplied and locally computed digest in the mismatch log.
- The final initialization state digest must cover the locally reconstructed configuration plus dynamic state.

Recommended loaded-save semantics: the current synchronized lobby settings remain authoritative whenever a saved map is started. On the host, overwrite the configuration portion of a valid loaded `RandomEventsSaveStateV2` from the canonical configuration snapshot while retaining its dynamic PRNG, timing, cooldown, pending-action, and signpost state. This makes the settings UI the single source of truth for fresh and loaded sessions. Document this semantic explicitly in code comments and tests.

If preserving the configuration captured at save time is desired instead, that is a materially different design: the saved configuration would still have to be transported to clients. Do not silently mix these two semantics. The recommended implementation is current lobby settings as source of truth.

### 2. Replace the 22-field union with command-specific packets

Use the same Chore system as ExtraFeatures, but give each command a compact packet with an explicit `IMessagePackFormatter<T>` and stable numeric keys:

#### `RandomEventsInitializationChorePacket`

Suggested fields:

1. protocol version;
2. operation ID;
3. 32-byte configuration digest;
4. PRNG state 0;
5. PRNG state 1;
6. next due absolute month;
7. start absolute month;
8. cooldown encoding;
9. cooldown data.

Do not include configuration arrays, `BatchPrepared`, prepared-action arrays, or signpost fields.

#### `RandomEventsBatchChorePacket`

Suggested fields:

1. protocol version;
2. operation ID;
3. PRNG state 0;
4. PRNG state 1;
5. due absolute month;
6. event kinds;
7. strengths;
8. target player IDs.

This is the existing event-batch information without unrelated empty union fields.

#### `RandomEventsSignpostChorePacket`

Suggested fields:

1. protocol version;
2. operation ID.

Signpost building IDs do not belong in the base initialization payload. Current code deliberately resets `SignpostsInitialized` for signpost-requiring loaded maps and runs deterministic initialization later through `InitializeSignpostsCommand`. Reconstruct initial signpost state consistently on each peer:

- no required signpost events: initialized `true`, IDs `[-1,-1,-1,-1]`;
- required signpost events: initialized `false`, IDs `[-1,-1,-1,-1]`, followed by the existing signpost Chore.

Before removing signpost IDs from initialization, verify a loaded multiplayer save containing existing registered signposts: `SignpostPlacementService.TryInitialize(...)` must rediscover and select the same existing IDs on every peer.

#### Registration rules

- Register all Chore packet types and the ACK packet unconditionally in exactly the same order on every peer.
- Keep registration before lobby-modsettings registration, as in current `RandomEventsPlugin.OnCrusaderLibraryLoaded(...)`.
- Prefix every serialized Chore body with its two-byte packet ID.
- Call `ChoreNetworkTransport.SendRawBlob(blob)` directly and fail closed.
- Never fall back to a non-tick-aligned Steam send and never apply the action locally before the Chore returns to the sender.
- Increment the RandomEvents network protocol version because backward compatibility is not required.

### 3. Use a compact, mode-aware cooldown representation

Keep `RandomEventsSaveStateV2` and the in-memory state as full arrays so existing indexing remains simple:

- shared: 15 entries;
- individual: `(MAX_PLAYERS + 1) * 15`, currently 135 entries including unused slot 0.

Only the initialization network representation should be compact.

Recommended encodings:

- `None`: all relevant entries are zero;
- `SharedDense`: exactly 15 shared cooldown values;
- `IndividualSparse`: flattened `(index, untilAbsoluteMonth)` pairs for nonzero entries;
- `IndividualDense`: slots 1 through 8 only, exactly 120 values; reconstruct slot 0 as zeros.

For individual mode, serialize both sparse and dense candidates locally and choose the smaller actual MessagePack body. Do not assume sparse is always smaller: a dense loaded save can make index/value pairs larger than the 120-value dense array.

Validation must reject:

- an encoding incompatible with the synchronized multiplayer mode;
- negative cooldown months;
- invalid flattened indices;
- slot-0 entries;
- duplicate sparse indices;
- wrong dense lengths;
- trailing or unconsumed data;
- reconstructed arrays with unexpected lengths.

After decoding, reconstruct the exact canonical full arrays before computing the state digest or enabling the mod.

### 4. Do not include a prepared batch in base initialization

Fresh multiplayer state is not prepared before the current handshake, so these arrays are normally empty. A loaded save can theoretically contain `BatchPrepared=true` with pending actions.

Handle this exceptional case without bloating every initialization packet:

1. Before creating the base initialization snapshot, clone any prepared batch into a host-only deferred holder.
2. Normalize the initialization state to `BatchPrepared=false` and empty prepared-action arrays on all peers.
3. Complete the initialization handshake over this normalized base state.
4. Immediately queue the retained prepared batch through the normal `RandomEventsBatchChorePacket` path after the handshake.
5. Ensure the host does not lose the deferred batch when it receives its own initialization Chore.
6. Ensure save-data serialization during an unfinished handshake either reinserts the deferred batch or refuses/fails closed; do not silently lose it.

The PRNG values in the saved state may already reflect preparation of that batch. Preserve them exactly; the subsequent batch packet carries the same post-preparation PRNG and recorded action order.

### 5. Cache one immutable initialization attempt

Replace the current behavior in `TryQueueInitializationChore()`, which calls `NextOperationId()` and clears ACKs on every retry.

At the beginning of one map handshake:

- allocate one operation ID;
- create one configuration snapshot and digest;
- create one normalized dynamic initialization state;
- create one initialization state digest;
- serialize and locally round-trip-verify the packet once;
- cache the packet/body/hash until handshake completion or map unload;
- clear `initializationAcknowledgedPlayerIds` only here and in `ResetMapState()`.

Every retry must resend the exact same bytes and retain already accepted ACKs. Log the attempt number, operation ID, byte count, body hash, configuration digest, state digest, expected players, and missing players.

Recommended retry schedule:

- initial attempt after the existing map-stability delay;
- retries after 5, 10, 20, then at most every 30 seconds;
- no retry after all currently participating living human players have acknowledged;
- remain fail closed indefinitely rather than starting events with missing peers.

This prevents the previous 154-operation moving target and substantially reduces traffic while still permitting recovery.

### 6. Make duplicate initialization delivery idempotent

Because retries reuse the same operation ID:

- A client receiving the same operation ID, configuration digest, state digest, and body hash again must not recreate or rewind its runtime state. It should resend the ACK.
- A host receiving its own duplicate must not clear peer ACKs or replace progressed state.
- The same operation ID with different contents is a protocol violation: log and disable RandomEvents for that map.
- An older operation ID after a newer accepted initialization is stale and must be ignored.
- ACK handling should continue using a `HashSet<int>` so duplicate ACKs are harmless.
- Keep the current distinction that ACKs are control-plane receipts and use the ordinary packet path rather than consuming another synchronized Chore in the same tick.

The current ACK carries its state digest as a hex string. For compactness, change it to a fixed 32-byte digest if practical and retain hex formatting only in logs. Preserve the explicit formatter.

### 7. Keep normal event traffic event-driven

Do not introduce periodic runtime-state replication. After initialization:

- the host prepares a batch only when `NextDueAbsoluteMonth` is reached;
- one compact batch Chore records the exact global action order and post-roll PRNG state;
- all peers execute the direct native mutations from that Chore in the same tick;
- Vanilla GameActions remain host-only where current code already prevents duplication;
- signpost initialization remains a separate minimal Chore and is not retried more frequently than necessary.

Maintain the existing one-RandomEvents-Chore-per-tick guard. If a deferred prepared batch and signpost initialization become ready together, queue them deterministically on separate ticks.

## Implementation sequence

1. Record a clean baseline:
   - current git status;
   - current RandomEvents DLL hash;
   - current serializer self-test sizes;
   - the log evidence above.
2. Add the canonical configuration snapshot/digest and tests without changing transport.
3. Add compact cooldown encode/decode helpers with exhaustive validation and round-trip tests.
4. Introduce command-specific packet classes and formatters; register them eagerly in fixed order.
5. Update `RandomEventsDiagnostics` serializer self-tests for every command, every cooldown encoding, invalid inputs, and maximum-size cases.
6. Refactor initialization to build/cache one immutable attempt and preserve ACKs across retries.
7. Add duplicate-delivery handling and the bounded retry schedule.
8. Separate a loaded prepared batch from base initialization and replay it through the normal batch Chore after ACK completion.
9. Remove duplicated configuration, prepared-batch, and signpost fields from the initialization packet.
10. Update logs so packet size, encoding, retry attempt, hashes, and ACK convergence can be compared directly across peers.
11. Complete all static checks and tests before running `RandomEvents\build.bat` exactly once.

## Required automated tests

Add focused protocol tests, preferably in a small `_inspect/RandomEventsProtocolTests` project or another repository-native test harness. At minimum cover:

### Configuration

- same settings produce the same 32-byte digest;
- changing each individual setting changes the digest;
- host/client configuration mismatch is rejected without ACK;
- loaded state is normalized to current synchronized settings according to the chosen semantics.

### Cooldowns

- all-zero SharedEvents round trip;
- nonzero SharedEvents round trip;
- all-zero IndividualRolls round trip;
- sparse individual round trip with several players and event kinds;
- dense individual round trip;
- encoder chooses the smaller actual representation;
- malformed length, negative value, slot 0, duplicate index, and invalid mode are rejected;
- reconstructed arrays are exactly 15 and 135 entries.

### Packet formatters

- explicit round trip for initialization, batch, signpost, and ACK;
- unknown trailing numeric fields can be skipped only if deliberate forward compatibility is retained;
- truncated payloads fail with field/offset/type diagnostics;
- every packet stays below the Script Extender 1200-byte limit;
- typical fresh SharedEvents and IndividualRolls initialization sizes are recorded as regression thresholds;
- retries serialize to byte-for-byte identical bodies and hashes.

### Handshake state machine

- operation ID allocated once;
- retry does not clear existing ACKs;
- duplicate initialization resends ACK without applying state twice;
- conflicting duplicate fails closed;
- player leave during handshake recalculates the expected living-human set safely;
- handshake does not complete while any expected player is missing;
- map unload clears cached packet, attempts, digests, and ACKs;
- deferred loaded batch is delivered once after handshake and is not lost.

### Existing repository checks

- Run `_inspect/HostClientPresetTests` as documented, but execute its EXE via the elevated sandbox path required by [`../AGENTS.md`](../AGENTS.md); its `File.Replace` persistence checks fail artificially inside the workspace sandbox.
- Run `_inspect/AuditModSettings.ps1` with PowerShell 7 via the elevated path if settings/ViewModel/XAML files change.
- Verify CRLF and absence of literal `\\r\\n` in every changed text file.
- Run `git diff --check`.
- Only after all checks pass, run `RandomEvents\build.bat` directly from PowerShell with `/nopause`; do not build or install by another route.

## Required real multiplayer test matrix

Use an actual host and a fresh client with deliberately different local defaults before join. For every run, verify `GameModeSnapshot` reports multiple human participants.

1. Fresh map, `SharedEvents`, zero cooldowns.
2. Fresh map, `IndividualRolls`, at least two humans.
3. Host settings selected before the client joins; confirm the shared settings join snapshot arrives before RandomEvents initialization.
4. Large host settings update before map transition; confirm configuration digests match.
5. Loaded multiplayer save with nonzero shared cooldowns.
6. Loaded multiplayer save with nonzero individual cooldowns across multiple player slots.
7. Loaded save containing a prepared batch, if a fixture can be created safely.
8. Signpost-requiring events on a fresh map.
9. Signpost-requiring events on a loaded map with existing registered signposts.
10. Artificial duplicate initialization delivery or a forced missing first ACK to verify stable retry behavior.

Success criteria in host and client logs:

- normally one initialization operation ID;
- retries, if forced, reuse the same operation ID and body hash;
- no ACK set is cleared by a retry;
- identical configuration and initialization-state digests;
- all expected human player IDs acknowledged;
- no `RandomEvents Chore decode failed` or `unsupported payload` errors;
- initialization payload is materially smaller than the previous approximately 228/229-byte body in the all-zero fresh-map case;
- no periodic state-sync traffic after handshake;
- each event batch and signpost initialization executes exactly once per peer;
- next due month, PRNG values, action digest, and post-execution state digest converge;
- the mod remains fail closed on any mismatch.

Until upstream work item 132 is fixed, a length-zero warning may still accompany an otherwise successful remote Chore. Treat it as the known upstream warning only when the corresponding valid Chore subsequently decodes and executes exactly once. A decode failure, missing execution, different body hash, or divergent state digest is not benign.

## Upstream follow-up boundary

If a compact initialization packet still arrives with zero-filled or corrupted content:

1. do not add a RandomEvents-specific non-Chore simulation fallback;
2. preserve host/client logs with packet byte count, hash, operation ID, field offset, and Script Extender hash;
3. add the evidence to [work item 132](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/132) or prepare a separate concise English Markdown report if the maintainer considers payload corruption distinct from the known false warning;
4. do not modify the canonical local `shcde-script-extender` fork.

## Expected files to change during implementation

Likely:

- `RandomEvents/src/RandomEventsRuntime.cs`
- `RandomEvents/src/RandomEventsDiagnostics.cs`
- `RandomEvents/src/RandomEventsInitializationAckPacket.cs`
- replace or substantially redesign `RandomEvents/src/RandomEventsChorePacket.cs`
- add command-specific packet/formatter and compact cooldown helper files under `RandomEvents/src/`
- `RandomEvents/RandomEvents.csproj`
- focused test project/files under `_inspect/`

Potentially, but only if required by the chosen loaded-save semantics:

- `RandomEvents/src/RandomEventsSaveStateV2.cs`

Do not change lobby-settings attributes, add a second settings sync mechanism, or edit Script Extender sources as part of this task.
