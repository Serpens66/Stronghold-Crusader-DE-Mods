# Trapped Unit Rescue Test: native contracts

Reference: canonical installed CrusaderDE.dll SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
These prototype contracts were inspected on 2026-11-15. Resolve future baselines through CURRENT.json. Unknown native images disable this test runtime; no semantic fallback is claimed for unit/grid layouts.

## Native relocation and observations

- Live callable `0x19BA40`: Winapi void(unit manager, one-based ID, uint tile X, uint tile Y, ushort ground height). Canonical entry bytes, typed layouts and complete image hash are checked. Original routine unlinks/relinks tile occupancy and normalizes current/next/fine position. It does not guarantee job continuation or normalize special AI states.
- Live callable `0x196280`: native single-unit move pipeline, including existing Script Extender/Pathfinding hooks. Result is inspected; failed experimental reissue is a failure, not a successful rescue-with-continuation.
- `0x19BA40` may already be owned by BugfixesAndQoL's `Lord.HomeTeleportGuard`. Accept only that registered, byte-validated live entry; never install a competing hook or bypass the owner's restrictions.
- Read-only image-relative grids: flags `0x48F71B0`, outgoing directions `0x51890D0`, building IDs `0x4B6AA50`, occupancy heads `0x4C559B0`, terrain heights `0x4DDD350`. Packed tile capacity 320800, map mask via the public Extender API.
- Native unit stride `0x490`, typed GameUnit bias `0x65C`; field anchors: next occupancy `0x8FA`, mounting `0x9B0`, portal `0x8EC`, transition `0x8F6`, crew IDs `0x96C`, crew globals `0x974`, crew count `0xA0C`, stored moat permission `0x9C8`. Source chains are validated before native unlink.

## Optional Fixes suppression

Installed Fixes binary reference SHA-256 `BE7D8E42AEE2C8FEF400A701472F5481A0A6F864E4DDAB54182302465DAE19C0`. Loaded/disk MVID and managed method contracts are checked. An unknown binary leaves suppression inactive.

- Managed `Fixes.Events.FixesAIEvents.OnAIBuildWall`: permanent SafeIlHook guards Pre only. Post custom-height behavior remains original. No external config edits or unsubscription.
- Own native span `[0x196342,0x196352)`, exactly 16 bytes `498BCF410FBFC489442470E8EE040000`. Validate the full enclosing native function, exact instructions, incoming direct branches, actual backend length and live bytes. Static whole-baseline xrefs contain no interior entry; no indirect control flow is accepted in the enclosing function.
- Relocated original call reaches live `0x196840`; preserve EAX. Only active mode forces ZF=1 after that call. This skips the adjacent Fixes worker-deletion body, whose original replay restores flags before Vanilla's CMOV. Inactive mode preserves original flags/registers/stack exactly.
- Adjacent Fixes span `[0x196352,0x196364)` is not modified. Re-emit and byte-compare its complete audited stub, displaced instructions and continuation before publishing our guard. Register exclusive ownership of our disjoint span and retain hook/resources until process exit.
- The existing gatehouse-worker solution is outside this prototype replacement and remains untouched.

Required update checks: full DLL/layout identity; decode caller and both branches; external and local incoming edges; Script Extender, Fixes, APIShared and Pathfinding site ownership; live adjacency and stub equality; execute tests/WorkerGuardFixture on the real backend before game tests. Native fixture success is not gameplay or multiplayer acceptance.
