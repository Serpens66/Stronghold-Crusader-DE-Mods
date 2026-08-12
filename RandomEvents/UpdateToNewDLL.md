# Updating Random Events for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24651686`
- DLL size: `3450880` bytes
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

The audited hash uses direct RVAs after local byte/semantic validation. Changed
DLLs use unique executable-section signatures and the signpost lookup may use
its additional structural fallback. A failed component remains inactive without
disabling unrelated events.

## Native address map

| Source pattern / target | Reference RVA | Use |
| --- | ---: | --- |
| `LookupPattern` | `0xCB800` | signpost lookup and manager-field derivation |
| `PenaltyWritePattern` | `0x104C6A` | bandit player stride/state offset |
| `HasBuildingPattern` | `0xB8D50` | building prerequisite delegate |
| `WheatPattern` | `0xC3130` | wheat handler |
| `HopsPattern` | `0xC2E30` | hops handler |
| `ApplePattern` | `0xC2C30` | apple handler |
| `MadCowUnitPattern` | `0x194BF0` | mad-cow unit handler |
| `MadCowBuildingPattern` | `0xC6090` | mad-cow building handler |
| `GranaryTheftPattern` | `0xC5F70` | theft handler |
| `PresentationCallsitePattern` | `0xF9B24` | derives manager `0x1B61EE0` and handler `0x103160` |
| `WildlifeHandlerPattern` | `0x11E100` | common wildlife delegate |
| `WildlifeBranchPattern` | `0x11E5E8` | lion/rabbit branch validation |
| `RabbitPredicatePattern` | `0x117750` | count/limit state |
| `RabbitSpawnerPattern` | `0x123A70` | rabbit spawn function |
| `RabbitTileMaskPattern` | `0x123B36` | rejected tile mask |
| `RabbitWrapperPattern` | `0x10491A` | event wrapper/timer |
| `RabbitSourceWritePattern` | `0x123B83` | source X/Y offsets |
| `LionCasePattern` | `0x11E211` | lion case/tile mask |
| `LionActivationPattern` | `0x104C14` | tribe stride/activation offset |
| `LionActionPointWrapperPattern` | `0x104BF6` | action-point wrapper |
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
   signposts, timeline events, map reloads and multiplayer restrictions.
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
tests.
