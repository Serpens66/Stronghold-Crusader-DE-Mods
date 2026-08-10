# Updating Extra Features for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24651686`
- DLL size: `3450880` bytes
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

The audited hash uses locally validated direct RVAs. On another hash, only the
signature- and semantics-validated features use bounded unique scans. Knight
dismount and quarry relocation remain inactive because their raw layouts are
not proven by the function signatures alone.

## Native address map

| Source pattern | Reference RVA | Unknown-hash behavior / use |
| --- | ---: | --- |
| `SleepStateComparisonPattern` | `0xC7DCB` | scan; context hook |
| `SleepStateSynchronizationFunctionPattern` | `0xC7D50` | scan; delegate |
| `EmergencyDemolitionComparisonPattern` | `0x2F454` | scan; context hook |
| `BuildingDeletePattern` | `0xC4290` | scan; detour |
| `MarketValidatorPattern` | `0xD7080` | scan; detour |
| `MarketPacketTailPattern` | `0xD7324` | scan; packet globals/sender |
| `MarketStorageCallPattern` | `0xD7119` | scan; storage delegate |
| `AutoMarketSellStatisticPattern` | `0xD0484` | scan; statistic table |
| `LifetimePattern` | `0x9A164` | scan; lifetime immediate at `+9` |
| `BuildingDistanceComparisonPattern` | `0x9F86B` | scan; distance context hook |
| worker-table byte pattern | `0x2E4E58` | full-image data scan; table begins `0x2E4DE0` |
| `ReleaseStableHorsePattern` | `0xC4110` | fixed unit/stable layout |
| `SetupBuildingEntrancesOffsetPattern` | `0xC0270` | fixed manager/candidate layout |

The named source constants and `WorkerTablePattern` contain the complete
authoritative patterns. RIP-relative targets are additionally checked against
the loaded image and their surrounding native contract.

## Required update audit

1. Require one match for every relevant entry and verify hook boundaries,
   delegates, register/stack assumptions and RIP-relative target bounds.
2. Verify the complete Vanilla worker table and its table-start calculation.
3. Revalidate market packet globals, sender/storage calls and statistic table.
4. Revalidate plague lifetime value `800` and Vanilla distance comparison `30`.
5. Before enabling fixed-layout features, revalidate knight stable fields
   `GameUnit +0x3D2/+0x3DC`, quarry manager fields `+0x31B7D0/+0x31B7D4`,
   helper ABI and all candidate semantics.
6. Test every setting enabled/disabled, restore paths, map reloads, market
   packets, church workers, plague behavior, AI protection, knights and quarry.
7. Update every RVA and only then approve the shared hash.

## Audit for Steam build 24651686

Every code signature has exactly one executable match; the worker-table pattern
and table start remain `0x2E4E58`/`0x2E4DE0`. The market call chain, statistic
target, plague constants (`800`, `30`) and all hook boundaries remain
semantically unchanged. The Vanilla stable release still uses building stride
`0x32C`, stable stride `0x196` and the knight backlinks `+0x3D2/+0x3DC`.
`setupBuildingEntrancesOffset` still writes the candidate pair at manager
`+0x31B7D0/+0x31B7D4` with the same ABI and rotation cases. Functional setting,
reload and multiplayer tests remain post-build game smoke tests.
