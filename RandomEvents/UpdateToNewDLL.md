# Updating Random Events for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

The audited hash uses direct RVAs after local byte/semantic validation. Changed
DLLs use unique executable-section signatures and the signpost lookup may use
its additional structural fallback. A failed component remains inactive without
disabling unrelated events.

## Native address map

| Source pattern / target | Reference RVA | Use |
| --- | ---: | --- |
| `LookupPattern` | `0xCB7B0` | signpost lookup and manager-field derivation |
| `PenaltyWritePattern` | `0x104C1A` | bandit player stride/state offset |
| `HasBuildingPattern` | `0xB8D00` | building prerequisite delegate |
| `WheatPattern` | `0xC30E0` | wheat handler |
| `HopsPattern` | `0xC2DE0` | hops handler |
| `ApplePattern` | `0xC2BE0` | apple handler |
| `MadCowUnitPattern` | `0x194BA0` | mad-cow unit handler |
| `MadCowBuildingPattern` | `0xC6040` | mad-cow building handler |
| `GranaryTheftPattern` | `0xC5F20` | theft handler |
| `PresentationCallsitePattern` | `0xF9AD4` | derives manager `0x1B61EE0` and handler `0x103110` |
| `WildlifeHandlerPattern` | `0x11E0B0` | common wildlife delegate |
| `WildlifeBranchPattern` | `0x11E598` | lion/rabbit branch validation |
| `RabbitPredicatePattern` | `0x117700` | count/limit state |
| `RabbitSpawnerPattern` | `0x123A20` | rabbit spawn function |
| `RabbitTileMaskPattern` | `0x123AE6` | rejected tile mask |
| `RabbitWrapperPattern` | `0x1048CA` | event wrapper/timer |
| `RabbitSourceWritePattern` | `0x123B33` | source X/Y offsets |
| `LionCasePattern` | `0x11E1C1` | lion case/tile mask |
| `LionActivationPattern` | `0x104BC4` | tribe stride/activation offset |
| `LionActionPointWrapperPattern` | `0x104BA6` | action-point wrapper |
| `ActionPointHandlerPattern` | `0xF4CF0` | action-point handler |

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
