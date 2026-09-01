# SerpNativeAPI V1 architecture

SerpNativeAPI complements the SHCDE Script Extender with catalogued, typed capabilities. Consumers cannot request arbitrary addresses, scans, writes, or detours.

Initialization occurs once from `CrusaderLibrary.LibraryLoaded`. Capabilities have independent error boundaries: the hash-bound Gatehouse capability can be unavailable while the Script Extender-backed selected-unit event remains available. `Unavailable` is reserved for a failure of global API publication.

## Gatehouse timing

The supported DLL SHA-256 is `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. The catalog also validates the complete native gatehouse handler interval `[0xB73D0, 0xB7CE5)` against SHA-256 `F73E9FF6F69D9EC1ECD59D528BC6D4861739F54E0A9C59C6E6BAD91369FA57C8`, its executable PE section, exact instruction blocks, immediate boundaries, and Vanilla values. There is intentionally no AOB fallback.

The four settings are the AI and human enemy-proximity closing distances plus their gate reopening delays. Distances use eight native units per tile; delays use forty ticks per second. Writes are an exclusive four-value transaction with expected-state and opcode checks, rollback, per-page protection restoration, instruction-cache flushing, and post-write verification. The current four RVAs share one 4 KiB page; the protection lease nevertheless preserves every individual page protection if a future catalog spans multiple pages.

## Selected-unit commands

The selected-unit capability installs no native hook. One process-lifetime subscription brokers the Script Extender's `TribeR3EventHooks.OnTribeIssueOrderWithTarget` Pre events. Consumers receive immutable snapshots with a typed `TribeAICommand`; they never receive the mutable Script Extender EventArgs.

Callbacks run in ordinal owner-GUID order. One callback's exception does not stop the remaining API callbacks. SerpNativeAPI never sets `SkipOriginalFunction`, changes arguments, or changes return values. Direct third-party subscribers to the underlying Script Extender event remain outside that guarantee.

V1 is intended for the workspace's own mods. Public contracts are typed, but third-party ABI stability is not promised before version 1.0.
