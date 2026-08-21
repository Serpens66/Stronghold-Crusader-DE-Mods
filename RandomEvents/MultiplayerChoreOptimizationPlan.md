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

## Verification

- `_inspect/RandomEventsProtocolTests` covers configuration digests, cooldown round trips and invalid encodings, packet round trips, stable retry bytes, Chore size limits, and the new configuration-free save schema.
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

Success requires normally one initialization operation ID, byte-identical retries, matching configuration/state digests, all living human players acknowledged, no RandomEvents decode failures, and exactly one execution of each batch per peer. The known Script Extender length-zero warning is benign only when the corresponding valid Chore subsequently decodes and executes exactly once.

If compact initialization is still corrupted, preserve host/client logs and prepare an English Markdown report for the Script Extender maintainer. Do not modify the canonical local Script Extender fork.
