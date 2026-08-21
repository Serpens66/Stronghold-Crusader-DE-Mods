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
| AI buy-price helper (`49 63 C0 8B 8C C1 B8 17 18 00 B8 67 66 66 66 F7 E9 D1 FA 8B C2 C1 E8 1F 03 C2 41 0F AF C1 C3`) | `0xCEB10` | executable-section unique scan; managed function detour |
| AI sell-price helper (`49 63 C0 8B 8C C1 BC 17 18 00 B8 67 66 66 66 F7 E9 D1 FA 8B C2 C1 E8 1F 03 C2 41 0F AF C1 C3`) | `0xCEB90` | executable-section unique scan; managed function detour |
| `LifetimePattern` | `0x9A164` | scan; lifetime immediate at `+9` |
| `BuildingDistanceComparisonPattern` | `0x9F86B` | scan; distance context hook |
| `MovementDecisionPattern` (Monk handler) | `0x1513E6` | executable-section unique scan; 20-byte inline decision hook |
| worker-table byte pattern | `0x2E4E58` | full-image data scan; table begins `0x2E4DE0` |
| `SetupBuildingEntrancesOffsetPattern` | `0xC0270` | fixed manager/candidate layout |
| Gatehouse AI closing-distance immediate | `0xB7BC3` | `DistanceBlockPattern`; Vanilla `200` native units = `25` fields |
| Gatehouse AI reopening-delay immediate | `0xB7BCA` | `DistanceBlockPattern`; Vanilla `1200` ticks = `30` seconds at gamespeed 40 |
| Gatehouse human closing-distance immediate | `0xB7BD3` | `DistanceBlockPattern`; Vanilla `140` native units = `17.5` fields |
| Gatehouse human reopening-delay immediate | `0xB7C35` | `HumanDelayPattern`; Vanilla `100` ticks = `2.5` seconds at gamespeed 40 |

The named source constants and `WorkerTablePattern` contain the complete
authoritative patterns. RIP-relative targets are additionally checked against
the loaded image and their surrounding native contract.

Gatehouse timing and distance values are four related `int32` immediates in the
same audited decision region. On the reference hash their RVAs and Vanilla
values are validated directly. On another hash both bounded semantic patterns
must resolve uniquely, remain within `0x100` bytes, and all four Vanilla values
must match before any write occurs. The four values are written as one guarded
transaction and restored together when the feature is disabled. Revalidate the
fixed 50-tick gatehouse scan cadence separately; the mod deliberately leaves it
unchanged. The reachability filter also remains reference-hash-only because it
depends on the audited gatehouse query context and native gate entry layout.

## Required update audit

1. Require one match for every relevant entry and verify hook boundaries,
   delegates, register/stack assumptions and RIP-relative target bounds.
2. Verify the complete Vanilla worker table and its table-start calculation.
3. Revalidate plague lifetime value `800` and Vanilla distance comparison `30`.
4. Revalidate that the AI hovel-demolition function still selects structure
   type `1`, applies the demolition refund, and is called only by the AI update.
5. Before enabling quarry relocation, revalidate the quarry manager fields
   `+0x31B7D0/+0x31B7D4`, helper ABI and all candidate semantics.
6. Test every setting enabled/disabled, restore paths, map reloads, church
   workers, plague behavior, AI protection, knights and quarry.
7. Revalidate both AI market-price helpers as one atomic hook set:
   - ABI `int (playerManager, playerId, good, amount)` in `RCX/EDX/R8D/R9D`;
   - signed `unchecked((basePrice / 5) * amount)` arithmetic;
   - the first two instructions remain 3+7 bytes, cover PolyHook2.NET 1.1.3's
     six-byte minimum, contain no RIP-relative operand, and are followed by
     `mov eax, 66666667h`;
   - no direct call or branch targets the interior of either 10-byte span and
     the two spans do not overlap;
   - buy callers still include the AI gold decision and the actual purchase,
     while the sell helper still supplies the transaction proceeds.
9. Update every RVA and only then approve the shared hash.
10. Revalidate the Monk handler at `0x151040` and its movement decision:
    - the hook at `0x1513E6` remains three complete instructions of `9+2+9`
      bytes and the following `je` remains at `0x1513FA`;
    - no direct control transfer enters the open interval
      `0x1513E6..0x1513FA`;
    - walking remains `state=1, speedBonus=0` at `0x151413`, while running
      remains `state=0x81, speedBonus=1` at `0x1513FC`;
    - the enabled branch still matches the official Improved-Spearmen
      translation of the ordinary Archer decision (`+0x914`, tribe `+0x582`,
      and `+0xA64`), while the disabled branch reproduces Monk Vanilla
      (`+0x914` and `+0x99E`);
    - Monk type 37 still dispatches to this handler and both
      `GM_BODY_FIGHTING_MONK` and `GM_BODY_TEMPLE_GUARD` remain material/skin
      selections inside that same handler.

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

The Monk update handler starts at RVA `0x151040`; unit-dispatch entry 37 points
to it. Its audited movement block chooses walking at RVA `0x151413`
(`state=1`, speed bonus `0`) or running at RVA `0x1513FC` (`state=0x81`, speed
bonus `1`). The new inline hook starts at RVA `0x1513E6`, replaces exactly
`9+2+9=20` bytes, and ends before the original conditional jump at
`0x1513FA`. Direct branches across all executable sections were checked and no
external branch targets the interior of this span; the existing predecessor
targets the hook start, which remains a basic-block boundary. The concrete
`X64InlineHook` writes a 14-byte absolute indirect jump, so the validated
20-byte span fully contains its detour without splitting an instruction.

When the setting is disabled, the generated branch reproduces the two original
Monk comparisons at unit fields `+0x914` and `+0x99E`. When enabled, it uses the
ordinary Archer walk/run decision already translated for the official Improved
Spearmen option: unit `+0x914`, tribe state `+0x582`, and unit `+0xA64`. This is
the necessary distinction from Fast Recruit Rally Movement: forcing Monk's
maximum Vanilla cadence values alone still selects the slow walking state and
animation. The hook's unmanaged enable flag is initialized to zero, so merely
loading the mod does not alter Monk behavior.

Both Monk appearances are covered without a separate animation patch. The
same type-37 handler selects either `GM_BODY_FIGHTING_MONK` or
`GM_BODY_TEMPLE_GUARD` as its material before reaching this shared movement
state machine; both skins therefore consume the same running state `0x81`.

The hovel protection now detours the AI-only function at `0x3B1D0`. Its single
direct caller is the AI update at call-site RVA `0x53C33`; the routine requests
`STRUCT_HOVEL` (`1`) before refunding and deleting the selected building. The
owner-wide game cleanup path remains separate: `0xCD190` calls `0xC3F10`, which
reaches `0xC4290`; none of these cleanup stages is intercepted.

The AI market-price override detours two complete native helpers. The buy
helper at `0xCEB10` is called at `0x3ED72` for the AI gold/price decision and at
`0x29684` by the purchase transaction; the latter is invoked by the AI at
`0x3ED9E`. The sell helper at `0xCEB90` is called at `0x29732` by the sale
transaction that the AI invokes at `0x3F22F`. The audited AI sale decision
selects excess goods from stock/minimum thresholds and has no separate price
comparison. Correct sale proceeds therefore influence later decisions through
the resulting gold balance.

Both helper delegates return `int` and receive `playerManager`, `playerId`,
`good`, and `amount` through the Windows x64 `RCX`, `EDX`, `R8D`, and `R9D`
argument registers. Their Vanilla total is signed
`unchecked((basePrice / 5) * amount)`, including division toward zero before
the multiplication. The buy and sell base-price fields are read at manager
displacements `0x1817B8` and `0x1817BC` respectively.

On the audited hash, the implementation validates the full function bytes at
the two reference RVAs and does not scan. On any other hash it searches only
executable PE sections and requires exactly one full-signature match for each
helper. Both addresses, their 10-byte spans, following instructions, direct
incoming branch targets, and non-overlap are validated before the first hook is
added. The two detours then commit through one rollback-on-failure transaction.
If either resolution or safety check fails, neither detour remains installed:
only AI-specific Vanilla market prices are disabled for that process, while
the global/per-good market factors and every other Extra Features feature stay
active. There is deliberately no fallback that temporarily changes global
prices or infers trades from resource/gold differences.

Fast Recruit Rally Movement obtains its speed/cadence hooks from
BugfixesAndQoL. For the audited DLL, the speed reset occurs at BugfixesAndQoL
RVA `0x19B4B6`, after Vanilla's base/group calculation and before its late
terrain/status modifiers. The reflection callback contract uses `GameUnit*`
as `IntPtr`; both mods must be rebuilt and installed together when this
contract changes. Do not use `UnitMoveHere` as a cancellation signal: the game
emits that event for the automatic barracks/outpost rally route as well, so it
would disable running cadence before it is applied. Explicit tribe orders still
cancel tracking. Movement restarted after the rally flag is rejected when its
target differs from the captured rally target; same-target restarts remain
valid rally movement.
