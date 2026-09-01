# SerpNativeAPI V1 architecture

SerpNativeAPI complements the SHCDE Script Extender with catalogued, typed capabilities. Consumers cannot request arbitrary addresses, scans, writes, or detours.

Initialization occurs once from `CrusaderLibrary.LibraryLoaded`. Capabilities have independent error boundaries: either hash-bound Gatehouse capability can be unavailable while the other Gatehouse capability or the Script Extender-backed selected-unit event remains available. `Unavailable` is reserved for a failure of global API publication.

## Gatehouse distance origin and timing

The supported DLL SHA-256 is `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. The catalog also validates the complete native gatehouse handler interval `[0xB73D0, 0xB7CE5)` against SHA-256 `F73E9FF6F69D9EC1ECD59D528BC6D4861739F54E0A9C59C6E6BAD91369FA57C8`, its executable PE section, exact instruction blocks, immediate boundaries, and Vanilla values. There is intentionally no AOB fallback.

`gatehouse-distance-origin` exclusively owns `[0xB7B70, 0xB7BBB)` and switches between Vanilla's begin-coordinate origin and the exact center of the complete gatehouse bounds while preserving the original Chebyshev metric. The 75-byte block ends exactly where the unchanged Human/AI decision begins and does not overlap the Script Extender's preceding `OnGatehouseQuery` hook. Its intended future consumer is BugfixesAndQoL.

`gatehouse-timing` exclusively owns only the four AI/Human enemy-proximity closing-distance and reopening-delay immediates. Distances use eight native units per tile; delays use forty ticks per second. `Enabled=false` restores only those four Vanilla values and never changes the separately owned distance origin. Its intended future consumer is ExtraFeatures.

Each capability has its own validation result, ownership intervals, expected state, transaction, rollback, cache flush, and post-write verification. Because both sets of intervals currently share one 4 KiB page, their mutations use one shared process lock so page-protection leases cannot race. SerpNativeAPI applies neither gameplay change until a consumer explicitly requests it.

## Selected-unit commands

The selected-unit capability installs no native hook. One process-lifetime subscription brokers the Script Extender's `TribeR3EventHooks.OnTribeIssueOrderWithTarget` Pre events. Consumers receive immutable snapshots with a typed `TribeAICommand`; they never receive the mutable Script Extender EventArgs.

Callbacks run in ordinal owner-GUID order. One callback's exception does not stop the remaining API callbacks. SerpNativeAPI never sets `SkipOriginalFunction`, changes arguments, or changes return values. Direct third-party subscribers to the underlying Script Extender event remain outside that guarantee.

V1 is intended for the workspace's own mods. Public contracts are typed, but third-party ABI stability is not promised before version 1.0.

Capability-specific public contracts, target resolution, adapters, and implementation live together in the corresponding capability source file. Shared lifecycle, diagnostics, native infrastructure, and publication remain separate. The exported consumer surface is guarded by an allowlist audit and XML-documentation warnings. Consumer guidance is in `README.md`; gatehouse-center status is recorded in `TODOGatehouse.md`, and byte/disassembly evidence is kept under `_inspect`.
