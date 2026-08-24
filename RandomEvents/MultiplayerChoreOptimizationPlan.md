# RandomEvents multiplayer Chore optimization

Status: implemented; automated verification complete, real multiplayer acceptance pending
Implemented: 2026-08-21

## Result

- Replaced the 22-field union packet with separately registered initialization, batch, and signpost Chore packets.
- Incremented the RandomEvents protocol to version 2 and retained direct fail-closed `ChoreNetworkTransport.SendRawBlob(...)` delivery.
- Removed lobby configuration from Chore payloads. Initialization now carries a 32-byte digest of the canonical synchronized settings.
- Added `None`, `SharedDense`, `IndividualSparse`, and `IndividualDense` cooldown encodings. Individual initialization chooses the smaller serialized candidate.
- Cached one immutable initialization body and operation ID per map. Retries use 5, 10, 20, and then 30-second intervals without clearing accepted ACKs.
- Made repeated initialization idempotent. Identical duplicates resend the ACK without replaying state; conflicting duplicates disable the mod for the map.
- Changed ACK state digests from hexadecimal strings to 32-byte values.
- Preserved prepared save-game batches outside the base handshake and restored them exactly once after all participants acknowledge initialization.
- Replaced `RandomEventsSaveStateV2` and `serp-randomevents-state-v2` with a dynamic-only `RandomEventsSaveState` registered as `serp-randomevents-state`.
- Old RandomEvents save payloads are intentionally not migrated. Current synchronized lobby settings are authoritative for new-schema saves.
- Version 1.0.17 executes every simulation mutation on every peer and removes the observed event-start desyncs.
- Version 1.0.18 target-filters the native presentation and minimap action-point queues. Each peer still simulates every batch action, while only the affected local player sees its notification and location marker. The target-based filter applies equally to `SharedEvents` and `IndividualRolls`.
- Debug logs record filter installation and the exact number of suppressed presentation and action-point calls for every foreign-target action.
- Version 1.0.19 isolates signpost-based Vanilla events to exactly one temporary native source: the registered signpost nearest to the affected player's keep. Equal-distance candidates use the building ID as a deterministic tie-breaker. This removes Vanilla's opportunity to select different signposts locally while executing the same synchronized archer action.
- Archer diagnostics snapshot the unit array immediately around `GameAction` and record every new unit's array ID, global ID, owner, type, alive state, spawn tile, and target tile. This distinguishes any remaining native placement divergence from RandomEvents action/state divergence.
- Multiplayer logs from version 1.0.19 proved that nearest-signpost selection was identical and correct on both peers, but disabling every native attack scenario point made both players' archer groups use the same Vanilla fallback tile. Native analysis for version 1.0.25 showed that case `148` instead reads a 32-bit X/Y source record at `SignpostSlots + 0x40` with stride `0x10`. The event scope now exposes the selected signpost as slot zero, injects its tile into that actual source record, verifies both writes, and restores every value after `GameAction`.
- Archers, bandits, and lions now share deterministic nearest-signpost selection: distance to the keep, living-Lord fallback, and building ID as the equal-distance tie-breaker. Bandits and lions keep their established synchronized spawn paths and log both selected and actual spawn tiles.

## Verification

- `_inspect/RandomEventsProtocolTests` covers configuration digests, cooldown round trips and invalid encodings, packet round trips, stable retry bytes, Chore size limits, the new configuration-free save schema, and local presentation targeting with nested-scope restoration.
- Runtime serializer self-tests record initialization, batch, signpost, and ACK sizes and SHA-256 hashes at startup.
- Repository checks include HostClientPresetTests, CRLF/literal-escape auditing, `git diff --check`, and the normal `RandomEvents/build.bat` build/install path.

## Required real multiplayer acceptance

1. Fresh `SharedEvents` map.
2. Fresh `IndividualRolls` map.
3. Host settings selected before a fresh client joins.
4. Save/load with nonzero shared and individual cooldowns.
5. Load an old save and confirm RandomEvents starts fresh.
6. Fresh and loaded signpost-requiring maps.
7. Force a missing first ACK or duplicate initialization.
8. Trigger events for both humans in `SharedEvents`; each player must see exactly one notification and only their own minimap action point.
9. Trigger different events at different due months in `IndividualRolls`; only each event's target may see its notification and minimap action point.
10. Trigger `Archers` for both humans in `SharedEvents` and for separate targets in `IndividualRolls`. Host and client must report the same isolated signpost and injected source coordinates for each action; newly created units must spawn at or immediately beside that target's selected signpost, the two target players must use different sources when different signposts are nearest, and no resync may occur.
11. Trigger `Bandits` and `Lions` for both humans and compare target player, anchor, selected signpost, distance, actual spawn tile, result, and Chore operation ID on host and client.

Success requires normally one initialization operation ID, byte-identical retries, matching configuration/state digests, all living human players acknowledged, no RandomEvents decode failures, and exactly one execution of each batch per peer. The known Script Extender length-zero warning is benign only when the corresponding valid Chore subsequently decodes and executes exactly once.

If compact initialization is still corrupted, preserve host/client logs and prepare an English Markdown report for the Script Extender maintainer. Do not modify the canonical local Script Extender fork.
