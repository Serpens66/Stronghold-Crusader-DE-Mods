# Updating Extra Features for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24651686`
- DLL size: `3450880` bytes
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

The audited hash uses locally validated direct RVAs. On another hash, only the
signature- and semantics-validated features use bounded unique scans. Quarry
relocation remains inactive because its fixed native layout is not proven by
the function signature alone. Knight mount/dismount uses only public Script
Extender fields and the bidirectional stable-link API, so it has no private RVA.

## Native address map

| Source pattern | Reference RVA | Unknown-hash behavior / use |
| --- | ---: | --- |
| `SleepStateComparisonPattern` | `0xC7DCB` | scan; context hook |
| `SleepStateSynchronizationFunctionPattern` | `0xC7D50` | scan; delegate |
| `EmergencyDemolitionComparisonPattern` | `0x2F454` | scan; context hook |
| `AIHovelDemolitionFunctionPattern` | `0x3B1D0` | scan; detour at the AI decision point |
| `MarketValidatorPattern` | `0xD7080` | scan; detour |
| `MarketPacketTailPattern` | `0xD7324` | scan; packet globals/sender |
| `MarketStorageCallPattern` | `0xD7119` | scan; storage delegate |
| `AutoMarketSellStatisticPattern` | `0xD0484` | scan; statistic table |
| `LifetimePattern` | `0x9A164` | scan; lifetime immediate at `+9` |
| `BuildingDistanceComparisonPattern` | `0x9F86B` | scan; distance context hook |
| worker-table byte pattern | `0x2E4E58` | full-image data scan; table begins `0x2E4DE0` |
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
5. Revalidate that the AI hovel-demolition function still selects structure
   type `1`, applies the demolition refund, and is called only by the AI update.
6. Before enabling quarry relocation, revalidate the quarry manager fields
   `+0x31B7D0/+0x31B7D4`, helper ABI and all candidate semantics.
7. Test every setting enabled/disabled, restore paths, map reloads, market
   packets, church workers, plague behavior, AI protection, knights and quarry.
8. Update every RVA and only then approve the shared hash.

## Audit for Steam build 24651686

Every code signature has exactly one executable match; the worker-table pattern
and table start remain `0x2E4E58`/`0x2E4DE0`. The market call chain, statistic
target, plague constants (`800`, `30`) and all hook boundaries remain
semantically unchanged. Vanilla stable release was also verified to decrement
`r_TotalHorses`, clear the horse slot and then recount `r_UsedHorses`; the mod
reproduces that state transition through public Script Extender access and has
no stable-release RVA or pattern to update. `setupBuildingEntrancesOffset`
still writes the candidate pair at manager
`+0x31B7D0/+0x31B7D4` with the same ABI and rotation cases. Functional setting,
reload and multiplayer tests remain post-build game smoke tests.

The hovel protection now detours the AI-only function at `0x3B1D0`. Its single
direct caller is the AI update at call-site RVA `0x53C33`; the routine requests
`STRUCT_HOVEL` (`1`) before refunding and deleting the selected building. The
owner-wide game cleanup path remains separate: `0xCD190` calls `0xC3F10`, which
reaches `0xC4290`; none of these cleanup stages is intercepted.

Fast Recruit Rally Movement obtains its speed/cadence hooks from
BugfixesAndQoL. For the audited DLL, the speed reset occurs at BugfixesAndQoL
RVA `0x19B4B6`, after Vanilla's base/group calculation and before its late
terrain/status modifiers. The reflection callback contract uses `GameUnit*`
as `IntPtr`; both mods must be rebuilt and installed together when this
contract changes. Explicit `UnitMoveHere` orders and movement restarted after
the rally flag with a different target now terminate per-unit rally tracking;
same-target path restarts remain valid rally movement.
