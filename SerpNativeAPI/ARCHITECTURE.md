# SerpNativeAPI V1 architecture

SerpNativeAPI complements the SHCDE Script Extender with catalogued, typed native capabilities. Consumers cannot request arbitrary addresses, scans, writes, or detours.

Initialization occurs once from `CrusaderLibrary.LibraryLoaded`. The installed DLL hash and in-memory PE image are validated before each built-in target is independently resolved. Unknown hashes never use signature scans. Resolved addresses, detours, trampolines, and delegates remain rooted for the process lifetime; BepInEx `OnDisable` and `OnDestroy` are intentionally not cleanup points.

Exclusive patch intervals use fail-closed ownership. Hook targets use one shared broker and one real detour. Before callbacks run in ordinal owner-GUID order, cannot replace Vanilla, and cannot alter its return value. Gatehouse timing is a four-value transaction with expected-state checks, rollback, protection restoration, cache flushing, and post-write verification.

V1 is intended for the workspace's own mods. Public contracts are typed, but ABI stability for third-party consumers is not promised before version 1.0.
