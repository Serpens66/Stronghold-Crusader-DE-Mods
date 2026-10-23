# AI negative sell thresholds and phantom gold

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Static audit and private native execution dated 2026-10-22. In-game acceptance is pending.

Vanilla `.lordjson` fields sell_food_at and trade_wood_at/trade_stone_at/
trade_resources_at/trade_flour_at/trade_weapons_at/trade_ale_at/trade_pitch_at
are signed Int32. Managed CreateAILordConfigData, serialization and native
DLL_SetExtendedLordConfig (RVA 0x87350) preserve negative values.

Scheduler 0x2AE40 reaches controller 0x3F270, which requires a market, toggles
buy/sell phase and branches into purchase 0x3EC90 or sale 0x3EE10. The latter
iterates 20 goods/category pairs at 0x2CEC90, applies minimum quantity, popularity,
emergency/gold, purchase demand, sell_all and cooldown branches. At 0x3F202 it
compares signed stock against the effective reserve; negative reserve permits
zero stock and may select an earlier empty good instead of a later stocked one.
The requested quantity is stock minus reserve plus the branch's retained minimum.
The apparently duplicated controller pseudocode is a tail-shared-body artifact;
the actual controller ends at 0x3F2BA and jumps to the separate handlers.

Sale executor 0x29700 rejects negative quantities only. Price helper 0xCEB90
computes `(price / 5) * requestedQuantity`. Before subtracting goods, 0x2974D,
0x29758 and 0x2975F credit player sale statistics, gold and income respectively.
0xB7D80 removes only existing eligible physical storage goods (or uses its separate
native-mode-6/direct-account branch), using storage mapping 0xBFD30 and total recount
0xC1CE0. Exhausted/no storage does not undo gold. 0xCB050's presentation branch
does not repair accounting. The defect affects all eight sell-threshold categories.

Private execution reproduces the bug using original copied Vanilla decision,
price, sale, stock removal and recount functions. Rendering endpoints alone are
stubbed. With empty ale and -999 threshold, gold increases without goods; with
stock 3 and requested quantity 99, credit covers 99 while only 3 are removed.

Standalone AISellThresholdFixTest applies two permanent pure-native inline stubs:

- RVA 0x3F1F3..0x3F202: floor final EDX reserve at zero before the original signed
  comparison. Empty goods fail eligibility and the normal search proceeds.
- RVA 0x29723..0x29732: bound EBX and R9D together by signed nonnegative total
  stock before pricing. Nonpositive quantities bypass all effects through the
  existing fully matched epilogue at 0x2978B.

Both spans are 15 complete bytes without known interior references. Installed
RedBird.X64 uses minimum 14 bytes rounded to an instruction boundary; actual
displacements are checked. APIShared sell-price entry, Fixes wheat-category site
and AIBuildDiagnoseTest's sale entry/fingerprint are separate. Runtime checks live
bytes and the native/backend/layout identities; unknown/occupied targets fail closed.
There is no file/AIC data rewriting, managed per-sale callback or extra polling fix.

The actual production generators and installed backend execute in private memory;
tests cover every table good, all categories, threshold/minimum extremes, alternative
branches, empty/partial stock, first-empty priority starvation, regular-sale parity
and native stack/register/flags/SIMD preservation. Names without curated symbols
remain candidate labels; the machine and accounting evidence is distinct.

Detailed update ledger: Testmods/AISellThresholdFixTest/UpdateToNewDLL.md.
Reproducer/harness: Testmods/AISellThresholdFixTest/tests/Program.cs.
Read-only control-flow/xref audit: tests/audit_native.py, resolving CURRENT's
database manifest rather than an old guessed database path. Execution/build records
are under .work/a/aisell/001. No game process or multiplayer result is claimed by
the private fixture; save loading, map replacement, editor and real peers still
require game acceptance.

Review follow-up, 2026-10-22: the original standalone constructor's post-commit
rollback was unsafe if executable writes had already happened. The corrected
owner prepares with InstallOnCommit=false, roots the full candidate before the
first activation attempt and never rolls back/frees state after that boundary,
including partial activation or post-write validation failure. Production-boundary
fixtures cover pre-write cleanup, throwing and partial backends, post-write
validation, no retry, GC retention and executable private native code after failure.
Both test variants now pass 12,746 checks. Review/build evidence:
.work/a/aisell-review/001. This is a failure-path lifetime correction; the two native
reserve/quantity generators are unchanged. A raw mode value 6 alone was not an
audited basis for labeling that alternate removal path as the general editor path.
