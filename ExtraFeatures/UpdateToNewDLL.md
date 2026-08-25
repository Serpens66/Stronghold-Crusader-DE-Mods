# Updating Extra Features for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24816905`
- DLL size: `3451392` bytes
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

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
| `InaccessibleBuildingComparisonPattern` | `0x3B2FF` | executable-section unique scan; inaccessible-building context hook |
| `ExecuteBuildStepPattern` | `0x51790` | audited-hash-only defense rebuild detour |
| `PlacementPattern` | `0x5CD90` | audited-hash-only paired AIV placement detour |
| AI buy-price helper (`49 63 C0 8B 8C C1 B8 17 18 00 B8 67 66 66 66 F7 E9 D1 FA 8B C2 C1 E8 1F 03 C2 41 0F AF C1 C3`) | `0xCEB10` | executable-section unique scan; managed function detour |
| AI sell-price helper (`49 63 C0 8B 8C C1 BC 17 18 00 B8 67 66 66 66 F7 E9 D1 FA 8B C2 C1 E8 1F 03 C2 41 0F AF C1 C3`) | `0xCEB90` | executable-section unique scan; managed function detour |
| `AiFlagRoutinePattern` | `0x504F0` | scan; detour captures exact AI flag projectile provenance |
| `LifetimePattern` | `0x9A164` | scan; lifetime immediate at `+9`, conditional comparison hook at `+18` (`0x9A176`) |
| `BuildingDistanceComparisonPattern` | `0x9F86B` | scan; distance context hook |
| `MovementDecisionPattern` (Monk handler) | `0x151436` | executable-section unique scan; 20-byte inline decision hook |
| worker-table byte pattern | `0x2E5E58` | full-image data scan; table begins `0x2E5DE0` |
| `SetupBuildingEntrancesOffsetPattern` | `0xC0270` | fixed manager/candidate layout |
| `DistanceBlockPattern` | `0xB7BBB` | executable-section unique scan; derives the three related gatehouse immediates below |
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
3. Revalidate plague lifetime value `800`, the comparison hook's `R8+RBX`
   projectile pointer, and Vanilla distance comparison `30`. Confirm that the
   AI flag routine still calls the generic projectile spawn synchronously and
   does not call `c_game_disease_create_one_herd`.
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

The AI flag routine at RVA `0x504F0` reads the selected lord's `flag_type` and
calls the generic projectile spawn at RVA `0x9B2B0` directly. The separate
plague-herd routine at RVA `0xD17D0` calls the same generic spawn but is not in
the flag path. At the Disease lifetime comparison at RVA `0x9A176`, `R8+RBX`
is the current `GameProjectile`; the conditional hook therefore substitutes
Vanilla lifetime `800` only for an identity captured during the synchronous AI
flag call.

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

## Audit for Steam build 24816905

Every mod-owned signature matches the new DLL. Most code RVAs are unchanged.
The priest worker table moved to `0x2E5E58`. The Monk decision and handler moved
by `0x50` to `0x151436` and `0x151090`; the two validated walking branch targets
now resolve to `0x151463`. The gatehouse delay code remains at `0xB7C32`, while
its relocated data operand changed by `0x1000`; only that displacement is now
wildcarded and the four Vanilla distance/delay values are still checked.

The latest old-build log independently demonstrated these two failures and the
worker-table fallback, while plague, AI defense and market signatures installed
successfully. Fixed quarry and inaccessible-building layouts retain their prior
field semantics and are enabled again only by the newly audited shared hash.
Live Monk, gatehouse and quarry tests remain post-build smoke tests.

## AI defense live audit for Steam build 24816905

The 2026-08-24 finished-castle trace disproved `0x52270` as the ongoing path
for that game mode: the temporary hook received zero calls while later tower
placement and rebuilds were observed. The dispatcher at `0x539B0` calls
`0x52270` only when the current AIV entry field at `+0x14` is zero. Otherwise
it iterates frames through `ExecuteBuildStep` at `0x51790`. Production code
therefore hooks only `0x51790` and its synchronous placement helper at
`0x5CD90`; the obsolete `0x52270` observer has been removed.

The live trace also established that a permanently obstructed tower target is
not discarded. It was retried 49 times with a median interval of 2690 ticks
(67.25 game seconds), an average of 2651.3 ticks, and no building spawn. In the
same run, a tower ruin spawned at tick 75120 and Vanilla reached its next tower
placement at tick 77622, 2502 ticks later. The ruin fix marked the obstruction,
Vanilla revalidated the footprint, and the replacement tower spawned in that
same tick. `OnBuildingDelete` emitted no corresponding combat-destruction
events, so it must not be used as the rebuild clock.

The production state stores only whether each `(playerId, frameIndex)` has
successfully produced a defense. This deliberately avoids building-ID lifetime
and prepared-frame-status interpretation while still separating an AIV part
that never fit from a genuine post-success retry. Short-lived damage-event
identity is used only to anchor a later rebuild delay to the last confirmed hit.

Both production detours use the same PolyHook2.NET managed-function hook type
already live tested by ActiveAIVDetector. Its six-byte minimum covers complete
prologue instructions: `0x51790..0x51797` is `2+1+1+1+2=7` bytes and
`0x5CD90..0x5CD9A` is `5+5=10` bytes. The following instructions begin exactly
at `0x51797` and `0x5CD9A`; neither span splits an instruction.
Recheck direct incoming targets and any new detour overlap before accepting a
future DLL.

Target-coordinate resolution reads the audited process-state origin fields
at placement-state offsets `0x204E760` and `0x204E764`. Those fixed offsets are
not proven by the function signatures. Consequently both native rebuild hooks
are disabled together on an unknown DLL hash, while the independent
managed repair-radius behavior remains available. For a new DLL, revalidate
the `ExecuteBuildStep` ABI `(aivState, playerId, frameIndex, restrictedMode,
freeOrForced)`, the frame bound `0x922`, both origin fields, and the placement
helper ABI before updating the baseline hash.

### Follow-up live result and minimal rebuild gate

The 2026-08-24 `1.0.47` follow-up ran for about 61,500 simulation ticks with
both native hooks installed and no callback failure. A player-4 tower at
`(437,118)`, frame 107, produced ruins at ticks 43702, 47134 and 53484. Its
successful Vanilla rebuilds occurred synchronously inside the same
`ExecuteBuildStep` frame at ticks 44906 and 49556. Later calls at ticks 55256,
57806 and 60356 reached the same placement helper but did not spawn; the repair
proximity query at the same target was blocked by nearby enemies. This confirms
that the existing proximity function is also the correct per-target gate for
this rebuild path. It also confirms that returning its normal blocked result
does not consume or globally stop the AIV frame: Vanilla revisits the target.

The implemented delay therefore keeps no building IDs, destruction events or
AIV status interpretation. During `OnStartMap(Pre)..Post` it records only that
an AI tower/gate target position has existed. A later placement in the same
`(playerId, frameIndex)` is a rebuild; its first Vanilla placement detection
stores one immutable tick. Until the configured duration has elapsed, the
synchronous proximity question returns Vanilla's blocked value. Rejected calls
never rewrite that tick. Once elapsed, subsequent calls remain released even
when the configured enemy radius independently blocks them. A successful
synchronous spawn clears only that frame's missing-period timer. This makes the
delay per target, keeps multi-part gate calls in one atomic frame state, and
does not prevent Vanilla from advancing through other frames.

Initial frame placements remain Vanilla: neither the delay nor the shared
radius is applied until the target was observed as a prior tower/gate spawn.
`-1` bypasses the respective rule, and `0` creates no delay state. Live setting
changes continue to compare against the original first-detection tick.

The historical diagnostic log contained 130 summarized proximity windows,
including 32 with a native blocked result, but no `OnBuildingRepair` event.
That instrumentation served its purpose and has been removed from production;
the finding remains relevant because it prevented treating the building-repair
Chore as the AI tower/gate repair path.

## Release-quarantine rollback

The temporary `1.0.49` DLL-update release quarantine has been removed. The AI
defense settings are visible again and default/reset to radius `30` and delay
`60`. Both values at `-1` form a true Vanilla mode: the runtime installs no new
event subscriptions or native detours when starting in that configuration. If
hooks were installed before a live switch to Vanilla, every callback directly
passes through without tracking, mutation or feature diagnostics, and retained
timer state is discarded. A later lobby-side activation retries both
managed and native initialization against the retained canonical DLL mapping.

## 2026-08-25 finished-castle anchor correction

The first post-release live trace exposed a concrete identity mismatch for a
finished-castle tower. Its ruin spawned at `(427,119)` on tick `5436`, while the
first later placement call supplied raw coordinates `(427,119)` but an
origin-adjusted proximity target `(428,120)`. Because prior spawn identity had
been compared only with the adjusted target, frame 17 was misclassified as an
initial placement and rebuilt at tick `6164` with `delay=vanilla`, only 728
ticks (18.2 internal seconds) after the ruin despite a 60-second setting.

Rebuild identity now uses the raw placement coordinates that exactly match the
building-spawn anchor. The validated origin-adjusted position remains separate
and is still used by the native proximity and placement checks. The old
adjusted identity remains a compatibility fallback for mapper and
multi-part gate variants not represented in this trace, so the correction does
not discard previously valid matches. The first detected missing-period tick
is still immutable and rejected attempts still cannot restart the delay.
