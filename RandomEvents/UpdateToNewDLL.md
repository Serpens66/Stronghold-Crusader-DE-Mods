# Updating Random Events for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24816905`
- DLL size: `3451392` bytes
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

The audited hash uses direct RVAs after local byte/semantic validation. Changed
DLLs use unique executable-section signatures and the signpost lookup may use
its additional structural fallback. A failed component remains inactive without
disabling unrelated events.

## Native address map

| Source pattern / target | Reference RVA | Use |
| --- | ---: | --- |
| `LookupPattern` | `0xCB800` | signpost lookup and manager-field derivation |
| `PenaltyWritePattern` | `0x104CBA` | bandit player stride/state offset |
| `HasBuildingPattern` | `0xB8D50` | building prerequisite delegate |
| `WheatPattern` | `0xC3130` | wheat handler |
| `HopsPattern` | `0xC2E30` | hops handler |
| `ApplePattern` | `0xC2C30` | apple handler |
| `MadCowUnitPattern` | `0x194C40` | mad-cow unit handler |
| `MadCowBuildingPattern` | `0xC6090` | mad-cow building handler |
| `GranaryTheftPattern` | `0xC5F70` | theft handler |
| `PresentationCallsitePattern` | `0xF9B74` | derives manager `0x1B62EE0` and handler `0x1031B0` |
| `WildlifeHandlerPattern` | `0x11E150` | common wildlife delegate |
| `WildlifeBranchPattern` | `0x11E638` | lion/rabbit branch validation |
| `RabbitPredicatePattern` | `0x1177A0` | count/limit state |
| `RabbitSpawnerPattern` | `0x123AC0` | rabbit spawn function |
| `RabbitTileMaskPattern` | `0x123B86` | rejected tile mask |
| `RabbitWrapperPattern` | `0x10496A` | event wrapper/timer |
| `RabbitSourceWritePattern` | `0x123BD3` | source X/Y offsets |
| `LionCasePattern` | `0x11E261` | lion case/tile mask |
| `LionActivationPattern` | `0x104C64` | tribe stride/activation offset |
| `LionActionPointWrapperPattern` | `0x104C46` | action-point wrapper |
| `ActionPointHandlerPattern` | `0xF4D40` | action-point handler |

The named source constants contain the complete byte patterns.

## Required update audit

1. Require one match for each entry and revalidate every delegate ABI,
   relative-call chain, RIP-relative target and image bound.
2. Revalidate signpost lookup semantics: eight slots, building stride `0x32C`,
   type `52`, manager offset and attack-point delta.
3. Revalidate bandit resource stride/state offset, event prerequisites,
   presentation targets, wildlife masks, rabbit limit and lion tribe fields.
4. Test each event independently, failed-component isolation, scenario
   signposts, timeline events, map reloads and both multiplayer distribution modes.
5. Update the RVA table. The signature fallbacks can continue on an unknown
   hash, but update the shared hash only after every fixed semantic check passes.

## Audit for Steam build 24651686

All 21 table signatures match exactly once. The presentation manager target
remains `0x1B61EE0`; its handler moved to `0x103160`. Signpost slots/stride/type,
bandit player state, all event prerequisites and wildlife masks/limits retain
their previous semantics. The unit-spawn function is now `0x17FEA0`. Script
Extender 1.41 names its first two managed arguments `playerOwnerId` and
`playerColorId`; the native function writes them to `GameUnit +0x92/+0x0C`.
Independent event, reload and multiplayer tests remain post-build game smoke
tests. Multiplayer tests must verify matching initialization/batch Chore operation
IDs, payload sizes below 1200 bytes and identical action order on host and client.

## Audit for Steam build 24816905

All 21 signature sites resolve successfully in the latest game log and in the
independent scanner. Unchanged handlers include signposts, prerequisites,
granary theft and action points. Code after the inserted block moved by `0x50`:
bandit penalty `0x104CBA`, mad-cow unit `0x194C40`, presentation call/handler
`0xF9B74`/`0x1031B0`, and all wildlife sites now recorded in source from
`0x10496A` through `0x123BD3`. The presentation manager moved by `0x1000` to
`0x1B62EE0`; unit spawn is `0x17FEF0`. Event field semantics and masks are
unchanged. Independent event, reload and multiplayer tests remain smoke tests.
