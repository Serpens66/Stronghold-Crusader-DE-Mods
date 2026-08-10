# Updating Extra Features for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

The audited hash uses locally validated direct RVAs. On another hash, only the
signature- and semantics-validated features use bounded unique scans. Knight
dismount and quarry relocation remain inactive because their raw layouts are
not proven by the function signatures alone.

## Native address map

| Source pattern | Reference RVA | Unknown-hash behavior / use |
| --- | ---: | --- |
| `SleepStateComparisonPattern` | `0xC7D7B` | scan; context hook |
| `SleepStateSynchronizationFunctionPattern` | `0xC7D00` | scan; delegate |
| `EmergencyDemolitionComparisonPattern` | `0x2F454` | scan; context hook |
| `BuildingDeletePattern` | `0xC4240` | scan; detour |
| `MarketValidatorPattern` | `0xD7030` | scan; detour |
| `MarketPacketTailPattern` | `0xD72D4` | scan; packet globals/sender |
| `MarketStorageCallPattern` | `0xD70C9` | scan; storage delegate |
| `AutoMarketSellStatisticPattern` | `0xD0434` | scan; statistic table |
| `LifetimePattern` | `0x9A114` | scan; lifetime immediate at `+9` |
| `BuildingDistanceComparisonPattern` | `0x9F81B` | scan; distance context hook |
| worker-table byte pattern | `0x2E4E58` | full-image data scan; table begins `0x2E4DE0` |
| `ReleaseStableHorsePattern` | `0xC40C0` | disabled; fixed unit/stable layout |
| `SetupBuildingEntrancesOffsetPattern` | `0xC0220` | disabled; fixed manager/candidate layout |

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
