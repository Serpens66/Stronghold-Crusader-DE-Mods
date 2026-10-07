# Building repair capability: native update checklist

## 2026-10-07: UnitAccess.IsReallyAlive

New public reference/pointer helper mirrors Vanilla's combat predicate: `AliveState.IsAlive` and zero low 16-bit death marker in `GameUnit.N0000019A`. Installed SE field offsets are `88` and `29C`, record size `490`; no health/animation test, native hook or new Assembly-CSharp member access is introduced by this helper. On native/interop updates re-audit complete death, corpse and removal flow plus melee/ranged targeting, then validate installed public field types/offsets and the enum value before native consumers use it. Source/contract tests are in `_inspect/UnitIdAccess/Tests`; real installed-layout/backend tests and the sole capture hook owner are in `../Testmods/GatehouseLivingCaptureTest`. Full audit: `../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md`. Native reference hash remains `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

## 2026-10-06: direct manual-gate close delay

Same native reference SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. APIShared's automation transaction additionally owns `D5835/15` (ends `D5844`) and `B79E2/18` (ends `B79F4`). Exact byte patterns are in `GatehouseAutomationNativeState.HookBytes`; live checks and executable-section unique-pattern fallback follow the existing hash-bound policy. New command parent `D5810`, length 96, hash `93BFC31E3B3FF25DBECFCF9624EE26A1AAA1A1A169ADDAD0ED1233201B811504`, is validated in full. Timer parent `B73D0` remains covered by the timing capability's function hash. No new public API or member access into Assembly-CSharp is introduced.

At D5835, Vanilla has validated Global-ID and written the requested gate command. RCX is native manager `64CCBB0`, R10 is one-based building ID times `32C`, EDX is that ID, R8D the command. Only a manual gate's close (10) replaces the zero write at manager+stride+`31C` with the configured delay from the existing atomic timing-snapshot pointer. Opening and inactive/automatic paths retain all displaced instructions, including the branch to `D584D`. Human/AI choice reproduces B7AB2..B7AED: mode DWORD `8574B90` nonzero, owner SHORT manager+stride+`132`, owner-indexed DWORD tables `8574BCC` equal -1 and `8574C44` nonzero, then mode !=99 or DWORD `3669040` zero. These fixed data offsets derive from the complete hash-validated native predicate; no independent layout fallback is supported. The leaf caller has a different stack alignment from B73D0, so keep RedBird's dynamic alignment, original RAX, volatile registers and XMM state; restore the displaced comparison flags before D5844.

At B79E2, AX already contains Vanilla's decremented timer. Manual gates store AX then continue at `B7A05`, skipping only the empty-enemy-list early reset at `B79F4`; automatic/inactive gates run the original block and can reset early. Do not hook B79F4 across B79FF: the native zero-timer branch enters B79FF. Both new spans are whole instructions without interior incoming branches. Existing query cadence, candidate selection, distance/reachability and coupling remain native. Direct closing restarts the timer; enemies renew the same timer; reopening changes nothing. The timer is stored in the native building record, so no new serialization or per-bridge timer is needed.

On updates recheck full command validation, all role-predicate branches and empty-list shortcut. Six optional hooks publish as one permanent transaction; activation only changes the logical resolver. Productive generators and the full direct-command/automatic-section/coupling sequence are tested in `_inspect/APISharedTests/GateBridgeDelayTests.cs`, including empty lists, enemy renewal, expiry, repeated close, opening, native-record restore, invalid Global-ID and deactivation. Actual save/load and multiplayer acceptance remain gameplay checks.

## 2026-10-05: independent gate/drawbridge automation

Owner: optional `IGatehouseAutomationCapability` on the existing gatehouse timing capability. Reference native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Full feature audit: `../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATE_BRIDGE_AUTOMATION.md`.

Four permanent X64InlineHook spans: `B7A4E/17` (manual cooldown), `B7BF4/15` (reopen decision), `C53D5/18` (linked gate recipient), `C54A0/16` (linked bridge recipient). Exact complete instruction signatures are `GatehouseAutomationNativeState.HookBytes`; reference bytes are checked against the load-time image and live executable memory. On a live mismatch, search for a unique complete signature in the validated executable section. Fixed continuations and structure offsets remain hash-bound: a relocated signature alone cannot authorize them. Failures log an Error and leave only the optional automation policy unavailable; timing, distance and unrelated capabilities remain usable. No new hook may compete with Fixes at `B7C39`; the existing APIShared decision generator routes manual gates before that target.

Native coupling target `C5300`, length 519, SHA-256 `71652656B8970E86BBBAE5E3EC95C422489F9B5DE4B020ACEC4C01BA044612A1`, entry signature `48 89 6C 24 08 48 89 74 24 10 48 89 7C 24 18`. Signature fallback is constrained to the same executable section and audited RVA. Automatic continuations are each hook start plus its exact span; manual continuations are `B7A71`, `B7CB4`, `C54E8`, `C54D8`. They derive from complete parent-function branches; `B73D0` is already validated by the timing resolver. Reopen timer address is module + `64CCECC` + buildingId * `32C`, signed 16-bit. Manager base `64CCBB0`, one-based IDs and the GameBuilding header difference must be re-audited with any native update. There is no independent semantic fallback for this layout.

Recheck the entire update/command/coupling/animation chain and incoming edges, not just signatures. Preserve RAX, flags, volatile GPRs and XMM0..5 around the predicate; test the result after RedBird's flag-changing wrapper. Verify actual backend DisplacedByteCount before publication. Hooks/delegates stay rooted permanently; null registration and mod activation only change logical policy. The first actual callback logs `GATE_AUTOMATION_POST_STARTUP`. Offline productive-generator execution and decoding live in `_inspect/APISharedTests/GateBridgeAutomationTests.cs`; gameplay and real host/client acceptance remain separate requirements. Existing API interface, packet/save IDs and mod versions remain unchanged.

Current verified `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. The capability resolves the HUD repair helper by fixed RVA `0xD51A0` only on this hash; there is no pattern fallback. Its entry bytes are checked before a delegate is created. Manager-relative wood and stone output offsets are `0x31B824` and `0x31B828`.

On a new native build, audit the full repair path before changing the hash: cost calculation (`0xB6F70`), action and proximity (`0xD4EE0` and `0xD51A0`), chore execution (`0xD5030`), resource checks and subtraction, building ID/global ID checks, and the `EngineInterface.CopyPlayStateStruct` call under `threadLock`. Confirm the Script Extender's `OnBuildingRepair` detour still surrounds the chore execution with Pre and Post. Re-evaluate the HUD field offsets from the manager base and validate them against a selected building's HP and plausible cost values. Update RVA, bytes, offsets, managed signatures and tests together; otherwise leave the capability unavailable.

Audit details for the current build are in `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/BUILDING_REPAIR.md`.

The installed, unpublicized `Assembly-CSharp.dll` has SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`. New direct managed accesses were checked against it: public `EngineInterface.CopyPlayStateStruct(PlayStateReturnData,int[])` and public `PlayState` fields (`app_mode`, `app_sub_mode`, `in_structure`, `in_structure_type`, `game_type`, `resources`, `keep_storage`, `repair_wood_needed`, `repair_stone_needed`, `building_hps_for_repair`, `building_maxhps_for_repair`, `can_do_repairs`); public `FatControler.NoesisGUIUpdateChecksInGame()`; public `HUD_Buildings.RefButtonRepair`; public `MainViewModel.viewModelLoaded`, `Instance`, `HUDmain`, `HUDBuildingPanel`, `getSmallGoodsIcon(int)`; public `GameData.Instance` and `lastGameState`; public `CrusaderDE.Translate.Instance` and `GameTexts`. The testmod's other directly used HUD references and `GetBuildingShowRepair(int,int)` are also public in that assembly.

## 2026-10-02: optional Assassin search observer

IEnemyGateAssassinObserver is an optional read-only capability on the existing gate provider. It supplies synchronous begin/edge/building/end observations, no hooks, native access, polling, background work or registrations of its own. Existing IEnemyGatePathPolicy members and semantics remain unchanged. Bugfixes performs diagnostic work only when a provider has a published mask and implements this capability. APIShared baseline/preset/consumer tests passed; the registered test provider emits through its existing deferred aggregate. Version unchanged.
# Passive gate route snapshots (2026-10-02)

Optional IEnemyGateRoutePolicyProvider and IEnemyGateRoutePolicySnapshot extend the registered gate provider without hooks, polling or game access in APIShared. Snapshot object identity is immutable and player-specific; IsCurrent checks publication lifetime. Assassin observation also counts rejected search candidates separately from published-route violations. No native contract or background callback added.

## 2026-10-03: independent bridge diagnosis and gatehouse boundary

All bridge edge masks (including the old center seam) have been removed from EnemyGatePathfindingTest. Gate identity/axis linkage is retained. EnemyBridgePathTest 0.1.0 is read-only and independently registered through APIShared; mainmod-owned hooks emit existing results only when an observer is registered. No new native hooks or active bridge policy. The 22:46 experimental mask and its 5,712 NoRoute result are historical, not the current gate policy. Native/SE identities remain confirmed. Pure-gate game acceptance remains pending; see Testmods/EnemyGatePathfindingTest/ACCEPTANCE.md and Testmods/EnemyBridgePathTest/HANDOFF.md. Work commands use existing nested MoveHere and synchronous before/after fields; no task-index or return value is interpreted as proof of work execution.

## 2026-10-07: shared Assassin path hooks

APIShared.AssassinPathAPI is the sole owner of builder D9C40 and endpoint guards
D9F0C, D9F1C, E19D8 and E19F9. BugfixesAndQoL registers its existing weighted
builder and calls the shared original exactly once; its reconstruction wrapper
publishes logical state only. The testmod registers the optional gate rule.
Reference SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Complete audit: _inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ASSASSIN_DIRECT_GATEHOUSE_CLIMB.md.

NativeX64 Indirect-only builder span is 10 bytes; actual scheme, FF25 entry,
pointer slot and trampoline displacement are verified before publication.
X64InlineHook spans are 16/15/18/23 bytes. Patterns and full incoming-edge checks
are in AssassinPathNativeDefinition; unique executable signature fallback cannot
authorize relocated continuation/layout contracts. Unknown hashes fail closed.
The installed RedBird 1.5 implementation was checked and exercised directly.
Pure assembler guards save flags/RAX/R10/R11, replay remaining wall/surface tests
and accept nonzero IDs only for live gate roof endpoints (types 45/46, IsWall).
Weighted reconstruction preserves the prior optional broad guard relaxation.

Hash-bound grids: building ID WORD at 4B6AA50, tile flags DWORD at 48F71B0,
320800 tiles. One-based building IDs 1..3999 use stride 32C, alive WORD base
64CCCDC and type WORD base 64CCCDE. These bases include the native sentinel.
Re-audit layout, roof placement/rebuild, cursor, AI gate action, reconstruction,
physical climb and neighboring-wall contracts on every native update.
SE's 196870 detour and mainmod selection adapters retain their owners. Fixes
1.25.1 gate filtering EAD8C/departure EACC3 has no overlap; preserve its targeting.
Hooks remain rooted until process exit; activation writes aligned policy data
only. No executable repatching or published teardown is permitted.

Source-linked production tests: _inspect/AssassinGateClimb/tests. Runtime and
installed-member preflight: _inspect/AssassinGateClimb/verify.ps1. Real in-game
human/AI acceptance is pending; see Testmods/AssassinGatehouseClimbTest/ACCEPTANCE.md.

## Exact weighted single-unit route publication (2026-10-07)

Native reference hash: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
E1640 reconstructs from step distances, with a mutable best distance inside its direction loop.
For target-first nodes T(1,1,d4),R(2,1,d3),P(2,2,d2),S(1,2,d1), it can accept R at direction 2,
then S at direction 4 against the already reduced distance 3. This replaces the cheap
S->P->R->T entrance route with a costly S->T climb. Weighted parents and tick costs
are not represented by the published unit-step distances. This is a confirmed static
counterexample, not yet causal proof of the user's particular gameplay run.

APIShared's existing F4930 owner now opens a synchronous, thread-local single-unit
handoff. Bugfixes stages a copied exact low-nibble-first forward direction sequence;
F4930 still executes once, then its checked output buffer receives the prepared bytes
before the 196280 consumer latches length/cursor/state. Successful staging retains the
original D9C40 field/result until the outer builder publishes. Failure inside a unit
frame preserves native data. Flood/continuation calls never stage; standalone queries
retain the checked stamp/distance publication path. Nested builders shadow all outer
frames, including unqualified/manual-probe frames. Delegates live only in the synchronous
frame, whose Leave restores its predecessor in finally; native hook roots remain permanent.

Existing hash-bound data contracts (no additional hook, fixed RVA, AOB or executable write):
- Path manager at module+60AD660: source int32 +8/+C, destination int32 +10/+14;
  Assassin flag int32 +88, moat flag +84, later alternate-builder flag +94.
  Exact publication requires Assassin enabled and the other two flags zero.
- F4930/E1640/E4E90/196280 prove output pointer +155F60, direction-count int32 +155F68.
- Unit manager module+67E8400: buffer +B4FE78 + one-based unitId*1000, capacity
  1000 bytes/2000 directions. Buffer arithmetic derives the ID and checks it against
  UnitAccess, type, true life, Global-ID, control player and native movement start.
- Existing grid bases: connections 51890D0, direction masks 312620, row table 402FF2C,
  heights 4DDD350, surfaces 48F71B0, building IDs 4B6AA50. Physical DCE60 accepts
  source-forward OR destination-reverse before climbing. E1640 tests its own
  target-to-predecessor connection, so reconstruction capability is checked separately.

All addresses derive from the canonical installed-hash disassembly and the existing
shared command layout validator. Fixed layouts have no independent semantic fallback;
search scope is the supported installed module only. Unknown hashes retain the existing
fail-closed feature initialization. F4930 hook resolution/backend remain unchanged;
its existing section-bounded function signature fallback and displacement checks apply.
On native updates re-audit field layout, buffer ownership, nibble reversal, movement-start
selection, alternate builders and the terminal 196280 consumer before permitting publication.
No new Assembly-CSharp access. Fixes gate targeting and Script Extender selection ownership
remain unchanged; the existing soft dependency loads the mainmod after Fixes.

Regression sources compile the productive handoff, encoder, and actual shared builder/
publication methods. Tests cover the expensive reconstruction shortcut, exact buffer bytes,
length, ID reuse, changed policy/pointer, nested frames and native exceptions. Physical
walking/climbing and open owned/captured gate roof acceptance still require an in-game run.
README files and versions remain unchanged during that acceptance.

Validation completed 2026-10-07:
- 15,890 Assassin A*/Dijkstra, physical-transition and exact-publication assertions passed.
- 1,396 source-linked Assassin checks passed on the installed NativeX64 backend.
- Actual F4930 wrapper/publication methods compiled in an isolated memory fixture:
  exact entrance bytes/count, changed identity/policy/buffer and native exceptions passed.
- Shared source/API visibility, JSON, lifecycle, permanent-hook, XAML, CRLF, UnitAccess
  and Fixes compatibility preflights passed; existing native command/moat suites passed.
- APIShared public API regression now permits only the audited context parameter of
  TryStageWeightedRoute in addition to the existing native builder bridge exceptions.
- Elevated direct build.bat drivers completed for APIShared and BugfixesAndQoL.
  The first APIShared driver stopped in its public API test before runtime build;
  the corrected test passed before the successful driver retry.
- Installed standalone APIShared SHA-256:
  A1B08496BEE16EDCACB66B657BD342168F6B74B7F0508BF7F0ABC02F964A2DB1.
- Installed standalone BugfixesAndQoL SHA-256:
  1041E2AFFD6B90C2E3A33774B02E58F9F5EE64F7F4B1162CB2AB7C0FFF1799DE.
  Both match their local package DLLs. Build logs: _inspect/AssassinGateClimb/
  exact-route-APIShared-build-retry.log and exact-route-BugfixesAndQoL-build.log.
- No in-game acceptance claimed. Test roof orders on both gate sizes, owned/captured
  open gates and different approach sides, with Improved Pathfinding on/off.
