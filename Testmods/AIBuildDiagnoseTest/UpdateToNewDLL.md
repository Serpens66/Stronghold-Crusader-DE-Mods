# Native economy/construction/removal observation

## Reader ownership after the diagnostic split

Scheduler, route, general-site and economy hooks remain here with unchanged native
entries, ABI, displaced instructions and permanent lifetime. Scalar wood route
results are captured even without a detail observer. The optional internal
`IAiBuildDetailCapture` provider in CanarariWoodTest owns coarse-grid, nearby-path
and full route-connection reads. No provider means no special snapshot work.
See that companion's UpdateToNewDLL ledger and Verify-PassiveNative.ps1 for its
existing hash-bound field/read/search contracts. This ownership change does not
add a native address, hook, fallback or extra native call.

Reference SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Manifest: workspace `_inspect/CrusaderDE-Native-Baseline/CURRENT.json`.
Machine evidence: `_inspect/AIEconomyDiagnostic-20261016/native-entry-audit.json`.
The audit script reads the manifest-selected generation's instruction exports and checks
all exported direct incoming targets against the whole-instruction entry spans.
Native function labels without curated claims remain candidates; argument/call/value
contracts below are based on the actual instructions, not their guessed names.

## Sole diagnostic entry ownership

All entries use the installed NativeX64 backend's Indirect schema only. Extended
entry fingerprints are in `EconomyHookContract.cs`. Validate those and decode the
minimum six bytes to the listed complete instruction boundary before publication.
Validate the real installed displacement, pointer slot, entry, padding and scheme.
Tests install the actual production backend on private copies and restore only those
unpublished test candidates. Published process hooks never uninstall or reapply.

If SpectatorEditorBuildTest_Serp is installed, its existing 0xCC420 entry wins and
this diagnostic does not install that entry at all. A soft dependency fixes startup
order. Spectator 0.1.2 publishes through its own private observation seam only
after its real patch passes validation. Its wrapper reports nullable return and
whether the native original was evaluated; override returns never become native
predicate evidence. No subscriber is inert; begin/end callback exceptions are
contained independently. Readiness is rechecked at session start; older/failed
owners produce an explicit gap. All independent entries/events remain active.

| RVA | Displacement | ABI and observation |
| --- | --- | --- |
| 0x2E1E0 | 10 | int(profile root, 1-based player); actual wood eligibility result |
| 0x2DBA0 | 10 | int(profile root, player); actual farm eligibility result |
| 0x3B360 | 10 | int(profile root, player); wood retirement, refund/delete inside original |
| 0x3B270 | 10 | int(profile root, 1-based building ID); signed hut-counter retirement result |
| 0xC8F50 | 9 | void(building manager, player); accessibility sweep context; existing inner protection decision stays owned |
| 0x55E10 | 8 | void(AIV root, signed tile X, signed tile Y); affected coarse cell before/after removal retry update |
| 0xCC420 | 10 | int(player manager, mapper, player, feedback); actual resource gate, including demand writes |
| 0x3EC90 | 6 | void(profile root, player); request priority, emergency/popularity gates, affordability |
| 0x29650 | 10 | int(profile root, player, goods index, signed quantity); actual AI purchase |
| 0x29700 | 9 | void(profile root, player, goods index, signed quantity); actual AI sale; entry includes JS |
| 0x2D810 | 6 | void(profile root, player, category, byte special-case); strategic demolition/refund/delete |
| 0x40740 | 12 | void(profile root, player); AI recruitment, including failure/demand and assignment branches |
| 0xCCBB0 | 10 | void(player manager); all-player tax/donation accumulation and settlement, including zero floor |

The fallback searches executable PE sections for the unique complete entry pattern
on the same supported native hash. A moved/occupied entry is rejected: data layouts
and caller contracts have no independently proven migration fallback. Unknown hashes
disable only native decision capture; independent Extender observations remain active.
Each entry has independent availability and an original-only callback until validated.

## Closed relevant flow and evidence boundaries

Scheduler 0x539B0 calls the wood retirement/eligibility branch and the farm census/
selection branch (0x2DBA0 -> 0x2C9F0 -> 0x50D80). A rejected eligibility result is
distinct from no call. 0x50D80 and the other building callers enter 0xCC420 before
search/build; that function may modify purchase requests when resources are missing.
The observer never invokes census, resource, accessibility or search functions again.
Costs use Extender's explicit mapper-to-structure conversion, mirroring 0xC77C0,
and its active cost-table getters; observed costs alone are not an eligibility result.

0x3F270 requires the player's market and alternates purchase processing with sales.
It is deliberately NOT instrumented: the installed Release Fixes 1.26.4 wheat-sale
hook at 0x3F0C9 reads an incorrect caller stack slot, exposed by the managed
controller detour. The controller detour and its exclusive entry contract are removed
for the next process start; no live hook is undone, register repaired or replacement
instrumentation installed. This configuration does not depend on any APIShared fix.
The trade-controller coverage notice marks the market gate, alternation and aborted
sale decisions unknown; controller call counts are not emitted as false negative evidence.
0x3EC90 reads 25 request slots, applies its emergency/popularity/request gates, asks
0xCEB10 for the actual final price, then calls either the ally-request path 0x300F0
or purchase 0x29650. Purchase calls stock-add 0xB58B0 and only then deducts gold and
updates trade state 0xCB050. Sale 0x29700 credits gold, removes stock through 0xB7D80
and updates trade state. Price helpers 0xCEB10/0xCEB90 belong exclusively to existing
APIShared MarketPriceEvents; queries/ally valuations never imply trades.
0x40740's verified argument pair encloses only recruitment decisions, the actual
0x1819D0/0x190CA0 recruitment calls and subsequent assignment/demand handling. Existing
Fixes/Bugfixes inline hooks deeper in that function remain untouched.

Tax settlement 0xCCBB0 has one manager argument, not one player argument. It iterates
players 1..8, accumulates income/expenses through 0xCBCD0 and 0xCB9A0,
normalizes by ten on settlement, changes gold and floors at zero. A before/after
operation for each observed AI measures the actual net change. Existing Extender tax
notifications are Pre-only and stay separate from confirmed settlement accounting.

0x2F080's emergency gate already belongs to BugfixesAndQoL at 0x2F454. The user
requires an unchanged installed build (minimum 1.0.192); it has no per-decision publisher.
Do not patch that owner or infer protected outcomes from settings/absent calls.
Actual strategic deletion enters 0x2D810 with the player. Hovel deletion 0x3B1D0
also remains exclusively owned by that build; its decisions/context are unavailable.
Current settings and published-hook status are read through the verified managed
field/property contract only, with missing members reported as unavailable.
Native 0xC8F50 calls
0xC90E0, sets state 3 for accessibility returns 0/2, propagates by group and updates
the heatmap. Existing decision hook 0xC8FD7 is untouched; original/effective results
and classification are unavailable with the retained owner. Marking is not terminal
deletion. Group is the actual dword at
GameBuilding+0x2A8 (`N0000178A`), derived from state address +0x1D8 in the group loop.
Delete/bulldoze/damage/refund/pause/spawn use existing Extender events exclusively.
Delayed deletion watches and full inventory comparisons distinguish a surviving
marked slot, zeroed slot, reused global ID and unavailable lookup.

## Data roots and update checks

### Worker causes: immutable copies, two permanent inline observation sites

Same supported Native hash. Existing Extender kill events remain the sole owners of
their native sites: projectile 0x193363 in 0x192750, melee 0x199444 in 0x199110.
Fatal branches set worker states 111..116. The woodcutter handler 0x12BC00 sends
completed death states to 110, just as a failed return helper 0x19B930 can do.
State 110 eventually marks the unit for deletion and adds 10 to the hut counter.
No additional death, movement, accessibility or search function is called.

WorkerCauseNativeHooks owns only:

| RVA | Displacement | Callback/continuation contract |
| --- | --- | --- |
| 0x12CD07 | 18 | before both stores; AX==110 filtered natively; unit ID = RCX/0x490; then state store and timer reset, jump to epilogue; flags dead |
| 0x12D1A2 | 21 | after the +10 at 0x12D199, before CMP/JNE/MOVSX; unit ID = RBX/0x490, native building ID = RSI; relocated CMP regenerates live branch flags |

Both require R12 == module+0x67E8400. The increment site also requires RBP ==
module+0x64CCBB0. R14 is reused for vegetation and is NOT a universal unit ID.
GameUnit starts at unit-manager+id*0x490+0x65C. Verified structure-relative fields:
alive +0x88, owner +0x92, global ID +0x94, AI state +0x2BC, linked building +0x334,
linked building global ID +0x364, health +0x3C4. UnitAccess resolves checked 1-based
unit IDs; corpse/marked records are permitted read-only views, not living-unit claims.
Tile and movement-target coordinates use installed public Interop fields. Projectile
lookup uses its manager's own index convention, with no unit-style +/-1 conversion.

The actual increment destination is building-manager+nativeSlot*0x32C+0x338
(GameBuilding+0x2DC). Read its signed short only inside the validated module bounds;
an unresolved/stale building link is not silently reinterpreted as another identity.
CounterBeforeDerived = signed-short(after-10) is explicitly derived, not sampled.

Verify-RemovalNative.py validates both fingerprints and incoming xrefs against the
CURRENT-selected database. Existing Extender pickup/drop sites are 0x12C8D6 and
0x12CA3B and do not overlap. Every callback precedes its displaced instructions;
the counter JNE's external target 0x12D1CD must remain intact. The handler has no
SIMD operands and no volatile XMM state live across either callback site. Guards
preserve flags/RCX/stack on the dormant path; the return filter restores its flags
before displacement. Context-stub flags are dead at the return site, regenerated
by the real CMP at the increment site. Production machine tests execute both
counter branch outcomes, native demand gating, return-value filtering and sentinel
register/flags/XMM behavior on private buffers. CLR tests replace only the generic
context callback body with a native stand-in, not the production guard/displacement.

Check pristine AND live spans before preparation; validate actual displacement,
published absolute jump, stub target and NOP padding. Each site has an independent
process-owned demand byte and readiness gate. After publication attempts retain
the rooted hook; never undo executable bytes. Only never-enabled preparation
candidates may be disposed. Unknown hashes disable these sites, not other events.

### Removal operands and small-cell reads

The additional entries above use the same permanent NativeX64 Indirect backend.
`tools/Verify-RemovalNative.py` resolves CURRENT.json's database, checks the installed
image/entry fingerprints and all recorded incoming xrefs for every economy entry.
Diagnostics.Tests also executes the real new prologues on private buffers, validates
the actual backend span and tests the production observer wrappers' failure paths.
No observer calls the game function a second time. Entry spans are disjoint from
the existing Bugfix accessibility decision at 0xC8FD7 and from each other. Keep
checking Extender, installed owners and Fixes source/metadata when these change.

Regular retirement reads the existing player roots plus wood planks 0x379B2A8 and
worst building ID 0x379D8F4, each with player*0x583C. Writer 0xACF40 counts huts
with GameBuilding+0x19C == 0 as fully staffed and copies the largest stored signed
short at +0x2D4 into the player maximum. Worker 0x12BC00 writes that short from
unit path-plan length after successful 0x196280 routing to the tree approach point.
It is neither a direct hut radius nor guaranteed to be a whole cross-component route.

Counter retirement reads signed low 16 bits at GameBuilding+0x2DC and +0x1B8
(Interop exposes the latter as UInt32; only its low word is tested by Vanilla).
Additional int32 operands: control 0x8574BCC + player*4 == -1, enabled 0x8574C44 +
player*4 != 0, game flag 0x8574B90 != 0, rotating player 0x382D080. Removal requires
counter >= 20 and guard low word == 0; otherwise the rotating-owner branch may
decrement the counter. These field meanings remain qualified, not invented APIs.

0x55E10 supplies the AIV pointer and actual tile coordinates. Require the supported
hash, root module+0x34A9F70, cell x/5,y/5 in 0..159 and an in-image read range.
Signed bytes are at root + (coarseX*160+coarseY)*0x30 + 0x5B837 (wood weight),
0x5B843 (retry delay), 0x5B847 (removal count). Capture reads only these three bytes
before/after the single original call. Same-image data layouts have no migration
fallback; an unknown hash disables only this native evidence. Layout validation
checks the installed Interop fields independently and reports its own coverage gap.

The sweep's 0x55E10 call follows its mark/group propagation, so a matching marked
identity is evidence of that path. It does not reveal the original accessibility
return before Bugfix protection. Cell-to-building lookup uses existing inventory
identities and spawn updates; missing/ambiguous identities stay unavailable.
All retained marks require slot AND global ID in the same session. Actual cleanup
is later in 0xC60F0 -> 0xB8310, not the retirement return. Rebuild correlation is
bounded spatial history and never changes or reinterprets a native decision.

Player state stride is 0x583C; native slots are 1-based. `GamePlayerResources` is
0x583C, gold field +0x50C and goods base +0x4D0. Building stride is 0x32C, alive
state +0xD0, owner +0xD6, global ID +0xD8, group +0x2A8. Managed layout is validated
before capture. Goods index `Count` and negative pseudo-goods are not stock entries.

Raw per-player RVAs: 0x379D0D0 (effective profile), 0x379D838/0x379D83C,
0x379E630, 0x379E6BC/0x379E6C0, 0x379E744, 0x379CF50,
0x379D8EC/0x379D8F0 and 0x379D840 + goods*4 (25 request slots).
0x3505470 is the AIV root 0x34A9F70 + 0x5B500. Writer 0x50720 clears it and adds
three per qualifying wood-tree tile (types 1..4, alive 2, growth below 4 and the
0x1086D0 resource/identity predicate). It is the map-wide grid wood weight, not a
local visible-tree count. Its `<1` comparison is the first wood eligibility branch.
Profile data is 0x404C950 + (profileIndex-1)*0x5E4, checked against image bounds;
no guessed maximum of 64 profiles is imposed. Runtime AIC size 0x5E4 and each
selected public field's offset are taken from installed Interop. Lord enum and
effective profile index are logged separately. Never use a Lord enum as that index.

Optional AIWoodSafetyTest uses an internal intervention bridge at the already owned
0x12D1A2 point. Before any optional correction, copy the actual native target WORD
and identity globals: unit module+0x67E8400+unitId*0x490+0x6F0; building
module+0x64CCBB0+buildingId*0x32C+0x134. These are native manager-relative offsets,
not GameUnit/GameBuilding structure-relative fields. Validate the typed unit death
marker at +0x29C as well. The sink/hooks/public APIShared/Canari contracts do not
change. Only the separate safety mod writes; general logs compare captured native
counter and globals with the independently read remaining counter. Unknown or
unacknowledged changes never become effective +10 evidence. Nested wood contexts
restore an optional intervention scope in finally, independent of scalar observers.
Review both mods' update contracts together when moving these points or layouts.

For an update re-audit complete functions/callers, all conditional/alternative exits,
incoming edges, relocated instructions and the installed backend. Refresh fingerprints,
offset contracts and machine evidence together. No stale RVA on an unknown image.
