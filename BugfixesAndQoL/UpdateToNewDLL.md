# Native update contract: Bugfixes and QoL damaged health bars

## AI keep-range probe allocation (2026-10-05)

The 18:51 startup failed before the actual EEF90 hook: the copied-entry probe
used heap storage, and RedBird could not reserve a 64-KiB executable slab within
its target-relative allocation window around that copy. This is not evidence
of exhaustion near the real native target or of a general RAM shortage.
The probe now uses the installed public `NativeMemoryManager.AllocateStub(target,
capacity)` and `WriteStub` APIs, with the complete scan window initialized.
Storage belongs to RedBird's process-lifetime slab allocator and must not be
passed to FreeHGlobal/VirtualFree. Only the private nonexecuted probe detour is
disposed; the published game hook remains permanent. Probe and game hook share
an explicitly Indirect-only NativeX64 backend. Existing full-function, 10-byte
displacement, pointer-slot and continuation checks still apply before/after enable.
On updates recheck the public allocation/write signatures, slab ownership and
near-range behavior against the installed RedBird implementation, not just its
assembly version. Tests cover allocation failure, initialized scan padding,
private rollback and actual trampoline execution. A corrected in-game startup
and Baibars 400x400 acceptance are still pending.

Reference: `CrusaderDE.dll` SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. This health-bar feature is bound to the hash because it reads fixed native unit/building layouts and depends on render control flow. An unknown DLL must fail closed before any hook is installed.

| Feature target | Reference RVA / image-base offset | Signature or derivation | Validation and failure |
| --- | --- | --- | --- |
| Building render helper, first path | Hook `0x489C4`, 17-byte span, return `0x489D5`, health path `0x489F5` | `83 3D 0D D5 61 03 10 75 08 3B 3D C1 F9 79 06 74 20`; gameplay mode 16, then selected building ID | Exact snapshot and live bytes, full decoded instruction lengths and branch targets, RedBird displacement, generated stub and committed hook; failure rolls back candidate |
| Building render helper, second path | Hook `0x4F9B4`, 17-byte span, return `0x4F9C5`, health path `0x4F9E4` | `83 3D 1D 65 61 03 10 75 08 3B 35 D1 89 79 06 74 1F`; same gates with another building loop | Same fail-closed checks |
| Unit render packet helper | Hook `0x1A1945`, 24-byte span, return `0x1A195D`, bar calculation continuation `0x1A1971`, alternate branch `0x1A1B96` | `66 39 B4 2F 8C 8A 7E 06 75 22 40 38 B4 2F B4 8E 7E 06 0F 84 39 02 00 00`; hover/selection gate before health-bar sprite calculation | Same fail-closed checks |
| Building HP | Current `0x64CCD18`, max `0x64CCD1A` plus renderer's register pair (`rbx+r13` or `rdi+rdx`) | Native building manager, stride `0x32C`, `GameBuilding` fields `r_CurrentHealth` at `0x10C` (signed 16) and `r_MaxHealth` at `0x10E` (unsigned 16); Game-IDs are 1-based | Only `0 < current < max` bypasses selection. No layout fallback for unknown hashes. |
| Unit HP | Current `0x67E8E20`, max `0x67E8E24` plus `rdi+rbp` | Native unit manager, stride `0x490`, `GameUnit` fields `r_CurrentHealth` at `0x3C4` and `r_MaxHealth` at `0x3C8` (both unsigned 32); renderer already holds the unit slot offset | Only `0 < current < max` bypasses selection. No layout fallback for unknown hashes. |
| Cached unit bar sprite | `0x67E8A90` plus `rdi+rbp` | `GameUnit.r_HealthBarBlocks` at `0x34`; damage/heal paths update it from health percentage. The unit renderer writes it into the type-51 supplemental packet; managed `GameMap.processTestMap` reads its `hpsFrame` word at byte offset `0x14`. | Existing Vanilla calculation remains in charge; this feature does not write this field. |
| Unit type exclusion | `0x67E8AE6` plus `rdi+rbp` | `GameUnit.r_UnitChimp` at `0x8A`; the installed `eChimps` enum assigns crow/seagull `48/49`, ghost `54`, chicken/mother/child `62..64`, and burning animals `68/69`. | These eight cosmetic types never bypass the selection gate. The mod also requires cached health-bar blocks below ten, so a visually full unit bar cannot be made persistent. |

The native hash and three PE-section instruction signatures are checked before installation. The live bytes must still be identical. The building stubs execute Vanilla's gameplay-mode comparison and branch before the added HP gate. At the first map tick the runtime checks the installed Script Extender structure sizes, field offsets and first array-element addresses against the image-base formulas, then logs plausible Unit/Building HP samples. Alt+H stays disabled if this check fails. A prior hook overlapping one of these sites is a conflict: a pattern match elsewhere would not make the overlapping control flow safe, so no fallback hook is installed. On a future native build, re-audit both renderer functions, every listed field and register formula, visibility gates, incoming edges, displaced instructions, RedBird backend and all continuation targets before updating the reference hash. The feature logs the error with a timestamp and leaves Vanilla active on any mismatch.

This feature has no pattern fallback because the native unit/building layouts, renderer register formulas and control-flow continuations are bound to the audited hash. An unknown or overlapping native build disables this feature alone; other Bugfixes and QoL features continue.

## Optional poleturner/tanner workshop idle delay

Reference native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
`EnableWorkshopIdleDelayFix` is a shared host setting, default/reset false. It changes only the idle-animation exit after the native positive-fear helper has returned zero. The native material selection, accessibility, routing, consumption and output routines remain authoritative.

| Native reference | Signature / derivation and contract |
| --- | --- |
| Poleturner update `0x13AAD0`; inline `0x13AC85`, continuation `0x13AC96`, exit `0x13BDAF` | `85 FF 0F 84 22 11 00 00 42 0F B7 84 23 D8 09 00 00`: test edi; je exit; movzx eax,[rbx+r12+9D8]. Full displacement 17 bytes. |
| Tanner update `0x13E0B0`; inline `0x13E39F`, continuation `0x13E3B0`, exit `0x13E6C2` | `85 ED 0F 84 1B 03 00 00 42 0F B7 84 23 D8 09 00 00`: test ebp; je exit; same movzx. Full displacement 17 bytes. |
| Pause entry `0x191070`, pause execution `0x185430`, animation helper `0x19B9D0` | Active entertainment can reuse AI state 1. The helper is called before both inline sites; work mode at manager-relative unit offset `0x956` equal to 1 must veto the bypass even when the helper returned zero. |
| Stockpile query `0xB9280`, resource-to-building lookup `0xBFD30`, table `0x2E76D0` | For `eGoods.STORED_WOOD_PLANKS`: reverse IDs from allocated-1 through 1; alive IsAlive, type STRUCT_GOODS_YARD, same signed-short owner, signed dword wood >= 1. No reachability check in this query. |
| Cow query `0x18B470`, distance helper `0x79C0`, scratch result `0x34A9F50` | Ascending IDs 1 through allocated-1; alive IsAlive, low short at `0x8F8` zero, same signed-short owner, CHIMP_TYPE_COW, AI state zero. Distance uses the scratch result at +12: max(abs(dx),abs(dy)) < 10000. The stub only checks existence and never calls/writes this scratch result. |
| Unit manager `0x67E8400` | Native next-ID bound at +0; stride 0x490; record origin manager+0x65C+gameId*stride. r12 is the manager, rbx is gameId*stride at both sites. Capacity 10000 including reserved slot zero. |
| Building manager `0x64CCBB0` | Native next-ID bound at +0x50; stride 0x32C; record origin manager+0x5C+gameId*stride. Capacity 4000 including reserved slot zero. |

Unit manager-relative fields read: alive 0x6E4; type 0x6E6; signed-short owner 0x6EE (native combines the Extender's owner byte and next byte); signed-short tile X/Y 0x71C/0x71E; low-short cow exclusion 0x8F8; AI state 0x918; work mode 0x956; workshop ID 0x990; workshop global ID 0x9C0; original animation variant 0x9D8 (the interop name r_TicksSinceRestPulse is reused by this native worker path).
Building fields: alive 0x12C, type 0x12E, owner 0x132, global ID 0x134; resource dword at 0x17C+4*eGoods (wood 0x184, hides 0x190). Hides are compared as signed >0, matching the tanner update.

Resolution first validates the reference RVA and then tries a unique executable-section signature if it differs. A relocated signature is rejected because register formulas, exits and fixed data roots have no independently proven migration contract. An overlapping hook therefore cannot be bypassed by hooking a lookalike. All these fixed-layout accesses remain hash-bound; unknown hashes disable only this feature. Successful resolutions are timestamped Info messages; failures are timestamped Error messages.

Both complete native update functions and semantic xrefs contain no incoming edges into the 17-byte interiors. Originals and continuations are checked against the installed PE. The installed X64InlineHook backend is probed without Generate/Enable before installation; its minimum 14-byte decode rounds to 17. Generated stubs are assembled and decoded before commit; callbacks validate the displaced instructions and branch destinations. After commit the actual handles, targets and displacements are checked before publication.

The stub saves RFLAGS, RAX, RCX, RDX, R8-R11; RBX/R12 and the completion register EDI/EBP stay untouched. No managed calls, SIMD instructions or Context wrapper occur. Both paths restore the stack/registers/flags before replaying Vanilla. Ready replays TEST and MOVZX and omits only JE; disabled/not-ready replays all three. The next CMP overwrites TEST flags as in Vanilla. No executable bytes are changed after publication: one aligned Volatile.Write changes the data gate. Static runtime roots retain the transaction and its allocation for process lifetime. Only unpublished, disabled initialization candidates may roll back.

Runtime validates installed interop sizes and field offsets and logs manager allocation samples at the first game tick. Existing Extender worker events occur after material selection or on state writes and cannot correct this idle exit. Local Fixes has no hook at these sites; the existing building-level tanner fade fix owns a separate function at 0xB10D0.

Automated tests execute the emitted stub in private buffers, check both paths, both resource sources, signed bounds, pauses, invalid/recycled workshops, registers, TEST flags and read-only game memory. They also decode the same installed X64InlineHook backend and compare unique signatures with the canonical DLL. In-game first arrival, return from a fear-factor break and host/client synchronization remain manual acceptance tests.

## AI melee raid retarget fix (1.0.174 integration)

Owner: `AiRaidRetargetFixRuntime`, default-on synchronized host setting `EnableAiRaidRetargetFix`. The main switch AND this setting control every simulation participant, never only the host. Ordinary `[SyncHostOnly]` registration, reflection-based host presets and the existing settings-change callback are used. Reset restores true. UI resides in the existing host AI section; German/English strings and the established English fallback in all other locales are included.

The complete Vanilla feature audit, backend inspection and acceptance logs are retained in `../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/RAID_RETARGET.md`, `../.inspect/RaidRetargetEvaluation/NativeSearchAudit.txt`, `CommandDisassembly.txt`, `InstalledX64InlineHook.txt` and the retained testmod `UpdateToNewDLL.md`. Reference SHA-256 is `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Unknown native hashes fail closed for this feature alone. Minimum Extender remains 2.10.4: the used public role, path-context and attack APIs exist at that tag; installed 2.12.0 signatures and interop visibility are checked by `Test-AiRaidApiContracts.ps1`. No new private Assembly-CSharp access is introduced.

### Native address and layout inventory

| Reference RVA / offset | Meaning and derivation | Validation / fallback |
| --- | --- | --- |
| `0x29190`, `0x2C620`, `0x3E200` | Candidate collection, type-priority selection, six raid roles | Full retained Vanilla audit; hash-bound semantics; no distance preference introduced |
| `0x11E960` | Native building-command entry for commands 9/36/38 | Extender owns entry/events; no competing detour |
| `[0x11FFA7,0x11FFB8)` | CALL `0x123090`, CMP first building field, JE `0x121B20` | Exact 17 bytes, full function direct edges and retained Xrefs, installed backend displacement and both continuations; no unsafe signature fallback |
| `0xDA020`, `0x123090` | Builder and candidate filter | Integrated FriendlyMoatMovement owns its existing function detours; observer calls live filter exactly once |
| `0x60AD660`, `+0x1B344`, `0x60C89A8` | Public path context, 500 12-byte records, first building field | Context identity matches native callback and command frames. Standfield zero alone terminates; stale tail ignored |
| `0x379E38C`, `0x379E454`, `0x583C` | Candidate list, count, player resource stride | Matches public resource pointer plus `0x35BC`/`0x3684`; Extender pointer bias is -`0x5C`; count 1..100 and address equality required |
| `0x379D9A4` | Shared target-player scratch value | Must match captured enemy; mismatch aborts without clearing stored order |
| `0x856A6D2` | Priority-table selector | Hash-bound validated fixed layout; three values only |
| `0x2C7F80`, `0x2C7EC0`, `0x2C7E00` | Three Vanilla priority tables (47 entries) | Read-only, original rank/candidate ordering; no priority or proximity changes |
| Tribe-relative `0x622` / `0x626` | Stored building ID / Global-ID | Write only after finished Vanilla call, matched group and target identities, and proven negative results; never clear on uncertain candidates |
| `GamePlayerResources +0x2B78/+0x390C` | Retarget / remaining counters | Existing audit/layout validation only; never modified |

Fixed fields/tables cannot be migrated using a code signature alone. Reference hash and explicit interop sizes/offsets remain mandatory; after a native update repeat the complete selector/order/filter/writer/backend audit, not only a byte-pattern match. Live and snapshot bytes at the observer reject overlapping hooks. The installed RedBird implementation decodes a minimum 14 bytes into exactly 17 here; post-commit handle, address and displacement are checked before publication. EDI/R14D carry 1-based tribe/building IDs. The custom emitter preserves volatile GPRs, XMM0..5, flags and native stack alignment; original CMP recomputes the JE condition. No ContextHook flag wrapper is used. Canonical local Fixes and Script Extender have no conflicting hook at this span.

### Runtime and safe correction contract

The native hook only observes. One common evaluator uses its final live search result to control the existing bounded correction. Exactly one associated completion must match session/epoch, command frame/sequence, thread, context, player/role, Tribe-ID/Global-ID and Building-ID/Global-ID; decision/Post snapshots must agree. Identical scratch content is allowed only with that proof. Fresh empty lists or a first building field zero reject the target. The first usable cardinal stand/target-building pair accepts it; invalid, missing or contradictory evidence preserves Vanilla and stops further replacement commands.

All eight players and six currently proven raid roles are identified through role storage, not fixed Tribe IDs. Replaced roles discard rejection/retry state. Suitable melee plus ignored archers are eligible; building-capable siege and unknown compositions stay under Vanilla control. Commands 36/38 and roles' additional movement branches receive no new correction. Building identities always include Global-ID. Rejections expire after 300 ticks; ordered replacement candidates are bounded by 100 and four actual commands per group per tick. Missing candidates, unexpected target-player scratch data or zero count after a live selected target preserve the order. Reachable granaries remain valid.

`processAiRaidRetargetFixRuntime`, subscriptions, observer transaction/handle/delegate/receiver and logger stay rooted for process lifetime. The durable Extender `GameTimeManagerAPI.OnTick` publisher survives startup cleanup. `Shared.GameplaySessionLifecycle` delegates mode/start/end detection to APIShared; only successful gameplay sessions are active, editors excluded. `GameplaySessionStartedContext.IsReplay` means cached notification delivery, not a gameplay replay, and must not block this persistent feature. No custom mode polling or host-only simulation execution is added. Disable/map end invalidates epoch and clears pending frames/retries/rejections, without removing any published hook. Reenable waits for a fresh Vanilla attack. Initialization failure disables only this feature; only unpublished subscription/hook candidates may roll back.

Logging is limited to actual hook/readiness, one durable tick/search confirmation, the first successful replacement per session, once-per-reason validation warnings and one session-end counter summary. Repeated warnings are counted, normal attacks produce no lines. No damage/deletion/movement/manual-control subscriber remains.

### Proof and remaining acceptance

Standalone 0.1.1 acceptance on 2026-10-01 includes the granary -> freshly empty armoury -> valid church -> damage/deletion chain and later 12/12 successful retries with 68 matched original searches (56 positive, 12 negative). Final logs cover role 0 for all eight players, not other roles or special compositions. These prove the transferred algorithm, not the new mainmod integration or multiplayer.

Canonical regression sources now reside in `tests/AiRaidRetarget.Tests` and compile the actual mainmod evaluator, observer, evidence and runtime. Coverage includes sentinel/rest values, first-field gate, 500 limit, identical fresh lists, absent marker/hook, contradictory snapshots, identities, nested commands, installed assembler/backend and both gate paths; activation/editor/cached notification/disable/reenable/map end are exercised on the actual runtime with an isolated lifecycle publisher stub. Build runs these and all prior mainmod build gates. Host setting classification/default/reset/preset routing and APIShared synchronization remain under the existing shared host-setting contract. Real host/client acceptance is still required.

After successful mainmod installation, `Archive-RaidTestmod.ps1` verifies installed DLL/version and moves the exact former installed `RaidRetargetDiagnostic_Serp` folder to the retained workspace archive, so no second hook instance can load. Its source build driver is retired; source/audits/logs remain available. Verify in game the mainmod readiness and an autonomous replacement chain, disabled option, and a real host/client game before claiming integration runtime/MP coverage.

### Completed integration checks (2026-10-01)

The own build.bat completed successfully for 1.0.174 after fixing Windows PowerShell-specific verification-script loading/hash compatibility; runtime correction code did not change during those fixes. Full prior mainmod build regressions, full shared host/client preset/routing/mode tests and the new raid tests passed. New raid coverage: 1,945 evaluator assertions, 89 evidence/native-emitter/backend assertions and 13 actual runtime activation/lifecycle/log-throttling assertions. A synthetic 34,001-operation logging stream produced 440 bytes (not a real gameplay-duration measurement). A method-body comparison confirms all 14 transferred native-access/eligibility/identity/replacement methods unchanged; evaluator/evidence differ only in namespace and observer validation/emission are identical. Canonical Fixes source commit checked: a47b2a4c897a85dd4b8b57d2653291def44f0d78.

Post-install verification: all 48 local packaged files match installed files by SHA-256, installed assembly is 1.0.174.0, both manifests are 1.0.174, and the compiled setting has SyncHostOnly. Source/project/plugin/assembly versions agree; 1.0.173 remains only in historical changelog entries. There was already no active standalone RaidRetargetDiagnostic DLL in the installed plugin tree, so the archive step correctly performed no move. Reference sources/local test package/logs remain retained and the old installation driver is retired. Build evidence: .inspect/RaidRetargetEvaluation/MainmodBuild-1.0.174-attempt3.log and .release-output/local/BugfixesAndQoL/latest.provenance.json. The known dependency-unification build warning remains; there were no compiler errors.

Actual mainmod startup, autonomous raid replacement, disabled option and a real host/client game remain pending user gameplay acceptance. Existing standalone game logs are not presented as evidence for these integration tests.

### Managed raid hardening (2026-10-02, version unchanged)

The installed Native SHA-256, complete raid/order/filter audit and observer emission/span remain unchanged. Public EventHookBase.SkipOriginalFunction was checked in the installed Extender and in the existing minimum tag 2.10.4. Its command wrapper emits no Post if final Pre.SkipOriginalFunction is true. Frames now retain the live Pre event; the native search/Post consumers discard skipped top frames before association. New Pre never prunes a temporarily skipped outer publisher frame. Invalid Tribe IDs still produce a neutral barrier with no candidate/context read, so their Post cannot consume an outer command. Unproved/mismatching completion remains unknown and cannot trigger a correction. Original filter/search/register contracts and replacement priorities are untouched.

APIShared cached start delivery may happen during registration. OnSessionStarted now requires initialized AND the activation gates. CompleteInitialization roots subscriptions and publishes initialized/active together under the command lock, applying the already delivered session and the latest requested setting. Initialization failure clears logical state under that lock; the published native observer is never disposed. Native event callbacks recheck active after acquiring the lock, and callback error/frame/warning state is serialized there too.

Expired building rejections are purged at intervals of 200 simulation ticks, with an interval check that also handles skipped tick values. Empty rejection group maps are removed. The exact rejection duration remains 300 ticks and lookup checks expiry independently of cleanup. Map change/disable/backward tick resets the schedule and cached identities. This is managed bookkeeping; it does not change native commands or fields.

HardeningTests compiles the actual mainmod source: 35 assertions cover cached start before publication, failed registration, disabled-at-commit, canceled nested calls including identical parameters, an outer Pre later restored by another subscriber, invalid-ID neutral barriers, missing Post across ticks, independent sequences, exact expiry/recycled identities/player-role identity, an eight-player/six-role 20,000-tick event stream, backward ticks and genuinely blocked callback threads released after logical disable. These supplement the 2,047 existing raid assertions; they do not establish new gameplay/MP coverage. Version 1.0.174 and README are unchanged.
### Hardening verification completed (2026-10-02)

All 2,082 raid assertions (2,047 existing plus 35 managed hardening checks) and the complete mainmod build regression suite passed. Runtime/API/JSON/lifecycle/permanent-hook/Fixes/MoatMove/XAML/version/diff/CRLF gates passed before the own build.bat was run once with the game stopped. No native-access, eligibility, identity or replacement algorithm was changed: comparison of all 14 relevant method bodies against the pre-hardening source is identical. Native observer/backend hashes and the validated 17-byte span remain unchanged.

Build and installation succeeded with the known dependency-unification warning and no compiler errors. All 48 package files match the installation by SHA-256; installed assembly is 1.0.174.0 and the compiled EnableAiRaidRetargetFix property retains SyncHostOnly. Installed DLL SHA-256: 0CCF91D597E287CA5512FD7D7EAE59A123113ABAEA920182AC2DE65D6FB6377A. No active standalone RaidRetargetDiagnostic DLL is installed. Active version remains 1.0.174; README is unchanged. Evidence: .inspect/RaidRetargetEvaluation/MainmodHardeningBuild-2026-10-02.log and .release-output/local/BugfixesAndQoL/latest.provenance.json. Actual integrated startup/autonomous raid and real host/client gameplay acceptance remain pending; synthetic tests and standalone testmod logs do not establish that runtime evidence.

## 2026-10-02: read-only Assassin gate diagnosis

See the hash-bound baseline `knowledge/ENEMY_GATE_ASSASSINS.md`. Four pre-change runs had 403/402/207/624 NoRoute results; the first three observed open/open, open/closed and closed/closed configurations. Automatic closure in the open runs led to retargeting; no persistent stuck-unit conclusion follows from the counters.

An optional passive APIShared observer measures the existing D9C40 detour, weighted/cache-route edges and native cache contents; it never changes search results. Existing deferred aggregates retain exact counts and numeric sums with no event cap. Gate edge IDs come from mask construction, overlapping identities remain ambiguous. New Unit fields were checked against installed public SE members, and the complete control WORD is reported. Native singleton reads use +0x84/+0x88/+0x90 and ten positive/negative PCL pairs at +0x416D8C/+0x416DDC with stride 8. No new hooks or executable mutations; original route/search/cache algorithms remain unchanged. Native-only flood fields and cache-hit/fallback measurement gaps are explicitly labeled. Versions and README are unchanged.

Abnahme remains open until paired Assassin runs locate the first different reachability result. No additional Assassin movement correction is included in this diagnostic build.
# Optional Assassin gate route policy (2026-10-02)

The existing D9C40 owner also filters its own weighted routes when an APIShared gate snapshot provider is active. No new native RVA, hook span or query. A* admission, route-cache validation and pre-publication validation share one immutable player snapshot; route and suffix cache keys separate its identity. Control uses verified public GameUnit low/high fields at 0x92/0x93 for gate contexts. Negative-destination/continuation calls and inactive-policy operation retain existing paths; freshness/identity failures retain the one native result before writing a replacement field. Preserve legitimate unmasked wall-climb transitions. Test mainmods without Testmod on updates.

## 2026-10-03: independent bridge diagnosis and gatehouse boundary

All bridge edge masks (including the old center seam) have been removed from EnemyGatePathfindingTest. Gate identity/axis linkage is retained. EnemyBridgePathTest 0.1.0 is read-only and independently registered through APIShared; mainmod-owned hooks emit existing results only when an observer is registered. No new native hooks or active bridge policy. The 22:46 experimental mask and its 5,712 NoRoute result are historical, not the current gate policy. Native/SE identities remain confirmed. Pure-gate game acceptance remains pending; see Testmods/EnemyGatePathfindingTest/ACCEPTANCE.md and Testmods/EnemyBridgePathTest/HANDOFF.md. Work commands use existing nested MoveHere and synchronous before/after fields; no task-index or return value is interpreted as proof of work execution.

# AI-only Keep build range (integrated from AIKeepRangeLimitTest)

Integrated feature; BugfixesAndQoL version remains unchanged during integration tests. Host-controlled `EnableMod` and
`RemoveAIKeepRangeLimit` default to true. Both use `[SyncHostOnly]`, APIShared's
`PresetLobbyModSettingsViewModel` and `LobbyModSettingsPresetRegistration`.
No separate transport, session settings copy, engine lock or polling exists.
All peers need the matching BugfixesAndQoL NetworkMode=1 mod. README files are intentionally unchanged.

## Native identity and resolution

Owner: removal of the own/allied Keep distance limit for **all AI callers**.
Canonical CrusaderDE.dll SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Entry RVA `0xEEF90`, length 317 bytes, body SHA-256
`83D062DDDBAFEC9EB33F704FA914609B6761E16DAE351A64F7491319984DF12E`.
Windows x64 ABI: `int64 (void* manager, int32 playerId, uint32 x, uint32 y, int32 range)`.
Zero accepts; one rejects with reason 18. Own Keep uses inclusive max-axis
distance <= range; allied Keeps use integer(range/2). This helper performs no
terrain, occupation, enemy-distance or resource validation. Its coordinate
guard returns zero for out-of-map cells; the caller owns other validity checks.

Resolution validates the reference entry against the full 40-byte prologue
pattern in `AIKeepRangeNativeContract.Pattern`. If that fails, the shared resolver searches
executable PE sections for a unique match, including when the file hash matches.
Then require the audited file hash, original RVA and complete function body.
A new binary remains disabled even with a matching prologue: incoming-edge and
caller semantics have not been proved for that binary. Error-level timestamped
diagnostics identify validation failures; no global override or alternate hook
is used. Successful resolution reports its method and actual RVA at Info level.

## Detour and ABI contract

Installed RedBird.NativeX64 1.5.0.0 is tested with its actual NativeDetour backend,
not an inline/context hook. Accept only `Indirect`: six patch bytes, rounded to
two five-byte instructions, `[0xEEF90,0xEEF9A)`. These instructions save RBX to
`[rsp+8]` and RBP to `[rsp+16]`; they do not clobber arguments, flags or registers.
The first undisplaced instruction at `0xEEF9A` saves RSI. The trampoline must
contain both unchanged instructions and jump to target+10. No custom register or
flags wrapper is required for this full-function ABI detour.

Validate Scheme, DisplacedByteCount, target, chain depth, trampoline and original
entry before installation; afterwards verify them again plus FF25 entry jump,
pointer slot, hook entry and four NOP padding bytes. Probe the copied entry first.
The full predicate is decoded with Iced and checked for interior branch targets.
The semantic database has calls at `0x7A73A`, `0xEE611` and a function reference
at `0x88F5680`, all to the entry, none to its interior. Placement's `0x783EA` call
is confirmed from actual bytes despite the baseline callgraph coverage gap.
The executable-section raw rel32 candidate scan also rejects any potential
interior edge; it is conservative and does not claim every byte is an instruction.
There is no jump table or indirect entry in this predicate. Preserve this full
audit when updating rather than relying on a prologue-only match.

The original delegate is assigned before enabling the unpublished candidate.
Only an installation failure can dispose that candidate. A published hook, its
delegate, runtime, logger and subscriptions remain rooted until process exit.
Activation changes only the logical integer flag. No runtime Dispose method or
Unity teardown path exists. Central initialization refreshes before prebuilt
castles and after loading; mission end clears the logical flag. Activation follows BugfixesAndQoL EnableMod and RemoveAIKeepRangeLimit; no extra game-mode restriction is introduced.

## Player identity, compatibility and side effects

Use the installed public `GamePlayerManagerAPI.MAX_PLAYERS` (8) and
`IsAIPlayer(int) -> bool`. The API bounds player IDs and tests GetAILord against
SK_NULL. No new Assembly-CSharp member access or publicized reference is used.
Invalid IDs, humans, disabled settings and failed AI classification run the
original exactly once with unchanged arguments. Classification errors turn off
the logical exception and are logged once. Logging cannot suppress or repeat
Vanilla. Successful AI decisions return zero without the distance helper's
rejection/scratch writes; callers' subsequent checks still run.

Caller audit includes ordinary footprint validation `0x77E60`, `0x7A3B0` and
`0xEE320`, AIV construction/prebuild and procedural economy construction
`0x52270 -> 0x6D580 -> 0x77E60`. This deliberately covers more than AIV frames.
Script Extender owns global range provider `0x6AF00`; this mod never changes
`KeepProximityOverride`. ExtraFeatures' global slider continues to affect humans;
AI ignores the resulting distance threshold while this test is enabled.
The inspected Extender and canonical Fixes sources have no competing EEF90 hook.
Soft dependencies order this mod after Fixes and ExtraFeatures when present;
neither is required. A foreign native patch fails full-body validation.

## Validation and pending gameplay acceptance

`Test-AIKeepRangePreflight.ps1` checks runtime JSON/lifecycle/polling, candidate-only mutations,
workspace permanent hooks, host metadata, XAML, all locale keys and CRLF. It runs
the hash/xref audit and tests compiled against the installed RedBird binaries.
The tests execute an actual detour and original trampoline over the audited
prologue with a synthetic continuation, preserving the fifth stack argument.
They cover AI, human, invalid and disabled branches and exact original call counts.
This proves the tested ABI and decision policy, not live native game behavior.
Existing `_inspect/HostClientPresetTests` exercises shared host/preset/trail rules.

Runtime evidence from the former testmod, 2026-10-05: installation at 14:09:28,
AI player 2 bypass at 14:11:28 with native range 70, human player 1 forwarded
at 14:12:35 with range 70, across two singleplayer sessions. No Error/Fatal or
classification failures in that start section. The user confirmed successful
castle construction. This is prior behavior evidence, not an integration test.

Integrated runtime logging: one Info installation line (resolution method, RVA,
Indirect/10), one first-callback Debug marker AI_KEEP_RANGE_CALLBACK, and a single
classification Error that disables the exception until process restart. No
per-cell/player success counters or recurring messages remain. Lifecycle and
settings changes cannot clear a classification fault. Central mission events
still apply before prebuild, after save initialization and at mission end.

RemoveAIKeepRangeLimit defaults/resets to true and uses the existing host-setting
system under Fixes?. It is independent of EnableClientFeatures. Settings changes
refresh this feature once; EnableMod follows the normal ApplySettings path.
The runtime, callback and hook are statically rooted and excluded from the parent
runtime's generic Dispose. No new public API or private Assembly-CSharp access.
The existing Extender/APIShared minimum versions remain unchanged: all required
native/public API dependencies were already referenced by BugfixesAndQoL.

A soft dependency orders initialization after any residual AIKeepRangeLimitTest_Serp.
A loaded-testmod guard rejects only this new feature before native resolution,
leaving unrelated BugfixesAndQoL features active. After verified installation,
remove the old testmod project and its installed plugin folder while the game is
stopped. No old test settings are imported; defaults apply. Analysis artifacts
in _inspect/AIKeepRangeLimit remain. Backend/decision tests now live in
BugfixesAndQoL/tests/AIKeepRange.Tests and are invoked by the main build driver.
They also compile the production runtime with test settings/publishers to check
prebuild/save initialization, mission change, host disable and permanent fault
handling. Existing shared host/client/preset/trail tests remain authoritative
for transport, roles and persistence.

Pending integration gameplay acceptance: Baibars 400x400 at global -1 with the
new checkbox on/off; human fortifications; prebuilt castles, rebuilding,
additional AI construction callers, saves and genuine host/client multiplayer.
Other ExtraFeatures slider values remain untested in game.
Integration validation: the AI-specific preflight and 105 backend/decision
checks passed, plus the production-runtime lifecycle/fault harness. The existing
build driver completed its host/client/preset/trail and other regression suites,
built and installed version 1.0.174 with no errors. Dependency assembly-version
warnings remain in the existing build graph; this feature adds no references.
Installed DLL SHA-256:
DCDD0D1D8EF43453591AF4F0E2AB17204D306BF91EA27315BADA54A5BC89A042.
All 24 DLL/metadata/XAML/locale files matched the local package. With the game
stopped and both absolute paths verified, the former testmod project and installed
AIKeepRangeLimitTest_Serp plugin folder were removed. Analysis artifacts remain.
No integrated in-game tests were performed during this implementation.

## Wild-animal targeting and actual Peace Time (1.0.175)

Reference SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Owner: VanillaPeaceTimeGameplayPatch. SHCDESE2.13.0/RedBird1.5.0 were verified against installed assemblies and canonical source. APIShared is unchanged. Source review found no overlapping Extender/Fixes hook for the new exclusion entry; the existing fixes soft dependency loads this mod afterwards.

| Target | Reference RVA / signature or derivation | Search/fallback and failure contract |
| --- | --- | --- |
| Wildlife exclusion entry |0x1867A0..0x1867B2; `48 89 5C 24 08 48 63 C2 48 8B D9 4C 69 D8 90 04 00 00`; four instructions5/3/3/7; continuation `4C 03 D9` | Exact reference-hash and live-byte validation; full533-byte function branch audit, no interior Xrefs. Hash-bound because fixed unit layout and species/owner data flow cannot be proved by entry signature alone. No pattern fallback on another hash; roll back complete unpublished peace transaction and log existing feature-failure marker. |
| Peace-only mode read |0x18699A..0x1869A0; `8B 05 F0 E1 3E 08` -> `B8 01 00 00 00 90` | Same hash-bound feature; no interior direct branch; backend BodyByteCount/OverwrittenByteCount6. |
| Shared result, preserved |0x1869A5..0x1869AD; `85 C0 0F 85 E1 FE FF FF`; conditional target0x18688E | Validate before installation and after commit.0x1868DE also enters the shared JNE with classification flags; never replace with unconditional JMP. |
| Actual peace flag |0x38722DC, from RIP-relative CMP at0x186991 and the existing audited36 references | Read-only; this flag alone controls suppression and automatic resumption. |
| Native unit inputs |manager + gameId*0x490, sentinel +0x65C; type +0x6E6, owner byte +0x6EE | Validate against real GameUnit size and named offsets; IDs remain1-based. Symbolic lion/hyena/crocodile eChimps; target owner1..8. No guessed semantics for raw classification byte +0x984. |

The native prefix uses only scratch RAX/flags before the prologue. Original MOVSXD/IMUL and continuation ADD restore Vanilla's data/flag flow. No arguments, nonvolatile registers, stack, SIMD or callback flags are changed. All28 instruction patches and both inline hooks share one transaction; validate actual committed target/spans before publishing the static owner. Published hooks have no teardown. Failure in an unpublished candidate alone may dispose the transaction.

The previous unconditional0x1869A7 patch explains lion/hyena target rejection and lost contact damage outside Peace Time. Target selection0x188A20/0x18E9A0 and contact melee0x187230 share exclusion0x1867A0; retained contact0x195170 rechecks through0x188A20 before damage0x199110. Baseline details and confidence are in current knowledge/VANILLA_PEACE_TIME.md.

Regression tests execute the whole native predicate, historical faulty byte fixture and production guard via the installed RedBird prepared stub:1,520,832 inactive plus1,520,832 active cases, all unit pairs/classes/owners/team/filter combinations in modes0/1/99, owner boundaries and active-to-expired transition. Prepared copied-memory candidates are never enabled; disposal only rolls back unpublished test storage. Mutation tests reject modified entry/shared bytes and incoming branches. Existing generated-patch/timer/start-troop checks remain. In-game acceptance with/without Fixes is still pending; versions were raised at the user's explicit request, not as a claim of completed gameplay validation.

Build/installation verification (2026-10-07): the prescribed elevated build.bat /nopause completed successfully, including all preflight/regression checks and the native execution tests above. Installed AssemblyVersion is 1.0.175.0 and info.json Version is 1.0.175; all 48 package DLL/metadata/resource files match their installed SHA-256. Build log: _inspect/WildlifePeaceTime-1.0.175-build.log. MSB3277 dependency-version warnings remain (Mono.Cecil in the runtime/test references; additional dependency warnings in the existing FriendlyMoatMovement test project); there were no build errors. No in-game acceptance was performed.

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

## Gatehouse living capture integration

Host setting EnableGatehouseLivingCaptureFix (default true), gated by EnableMod. Permanent X64InlineHook at B7540..B7552; native/layout/byte/backend contracts and tests in tests/GatehouseLivingCapture.Tests/UpdateToNewDLL.md. Test-GatehouseLivingCapturePreflight.ps1 -RunTests validates installed public APIs and runs the actual-backend suite. Setting changes are atomic data writes; no published teardown. Keep Fixes soft dependency and test gatehouse capture alongside existing gate controls.

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
## 2026-10-08: living-unit action guards

Native SHA-256 remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. The complete death/combat/removal chain is documented in the current baseline knowledge/GATEHOUSE_LIVING_CAPTURE.md; transient recruitment states and persistent slot identity in RECRUIT_TRANSFORMATION.md. Managed action guards use APIShared.UnitAccess.IsReallyAlive; snapshot property names and identity checks are preserved.

Existing synchronized movement generators and the poleturner/tanner idle-delay generator additionally test the low WORD at manager-relative unit offset 0x8F8 (record origin 0x65C, GameUnit+0x29C). This follows the existing IsAlive check at manager+0x6E4. No full-DWORD marker test, health substitute or new hook is introduced. Death-marked units follow the existing restore-and-replay path without changing tracking tables, speed or animation. The native upper marker word is deliberately ignored. Existing hook RVAs, patterns, pristine replay instructions, displacement checks, ownership and native-hash fail-closed policy are unchanged; no relocation fallback for this fixed layout is added. Revalidate the origin formula, installed GameUnit.N0000019A layout and full death/combat flow on native/interop updates. Workshop tests execute the actual emitted stubs with dying-worker and upper-word cases; movement parity tests cover exclusion without erasing recruitment tracking.

## Integrated formation command contract (2026-10-08)

FormationTest is absorbed into BugfixesAndQoL; checkbox/presets/host synchronization keep
their existing key. FormationRuntime in APIShared uses the existing E1D30/E0970/118E00
dispatchers rather than installing a second native detour. Native 196280 remains owned
by the Script Extender event. Formation targets run before UnitMovementContext Pre;
completion runs after its Post. The accepted protocol-5 command carries kind, density,
direction, width, role placement, unit count and deterministic identity/target hash.
Consumed left/right release state is never restored. Shift queue transport retains only
its own marker; no old density encoding or fallback remains.

FBCB9319 native closure: 879A0/86680, 8B7E0/8C5F0, 195E30, chore 10AE0,
196100/11B520, E1D30/E0970/118E00/119F90/196280, and cursor/overlay
8F3DA..90088, 1222A0/417A0/41D10/41D60/436DE/1A13C0. Preserve authoritative
object, Assassin, terrain, region and owner checks. NativeDetour Indirect contracts are
validated by NativeDetourContracts; marker context displaces 17 bytes to 436EF,
AfterCallback, with All GPRs and replayed TEST/JE. Installed RedBird is exercised by
the copied-buffer regression; no published hook is torn down.

The merged marker renderer is process-owned by UnitCommandPathAPI.MoveMarkers.
Drag preview authorization is memoized per native pass; role dots use the same
authorization. Overflow and queue rendering share the original overlay/draw callbacks.
Installed interop is checked by _inspect/FormationIntegration/Verify-Interop.ps1:
GameUnit 0x490, GameTribe 0x688, cursor view 0x480, including all consumed field
widths/offsets. UnitAccess.IsReallyAlive uses UInt16 r_IsKilledByProjectile at 0x29C.
Game APIs are checked without emission against the true Assembly-CSharp.dll.
Fixes 1.26.0 additions (111C00 and 6A5D0) do not overlap this closure.
Formation config migration reads FormationTest_Serp.cfg without modifying it; existing
main keys win, invalid/missing legacy values use Block/2/Off/true.
Re-audit the complete closure, editor release fields, tile grids and both installed
hook backends after native/Extender updates. Runtime versions remain unchanged during
testing. Multiplayer/gameplay acceptance still requires an actual game session.
# Rally-point terrain slowdown (1.0.180)

The independent default-enabled `EnableRallyTerrainSlowdownFix` captures
effective movement delay before the type handler and restores it at the
common cadence hook. Full audit and hook contracts:
`../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/RALLY_TERRAIN_SLOWDOWN.md`.

For a native update, re-audit the entire recruitment/rally/type-handler/
dispatcher/movement/arrival/terrain chain for all 26 supported types.
Validate hash-bound unit layout, dispatch pattern at RVA 0x18410C,
its 8-byte indirect call plus 6-byte current-ID load, continuation
0x18411A, incoming edges, and actual installed RedBird inline displacement
(14 bytes). Also recheck cadence 0x184203 (23 displaced bytes), terrain
hook 0x19B506, and preservation of elevation and all later terrain/state
calculations. Never infer a new fixed layout from a signature match.

The capture transaction is optional: validation failure disables only
the new terrain correction. Settings publish atomic data flags with an
epoch; published hooks and native tables persist until process exit.
Keep the terrain setting separate from recruit running, including host
sync, presets, reset and search. Run the actual production generators in
`RallyTerrainGeneratorTests` and repeat in-game swamp, ford, uphill,
downhill and level routes with both running settings and loaded saves.

Formation rows follow-up (2026-10-08): internal protocol 6 adds ushort Rows. Wheel adjusts gesture-local rows; drag only sets the eight-sector facing. Width is validated against maximum rank width. Integer local coordinates precede rotation; exact reachable slots are reserved before obstacle fallback. Existing native selectors, permanent input/renderer hooks and installed SHCDESE 2.14.0 contracts are unchanged. No new public API or game-assembly access.

## Command engine ownership (2026-10-09)

The coherent command engine is owned by BugfixesAndQoL/src/UnitCommands;
formations remain BugfixesAndQoL/src/Formations. MoatMove provides its optional
search/traversal implementation through the internal BugfixesAndQoL assembly
contract. APIShared only owns the public managed route-search preference events.
This ownership move does not alter native addresses, algorithms, delegates,
registration order, settings, formats or process lifetime.

Reference native SHA256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
NativeDetourContracts.cs is the exact128-entry function-prefix catalog; feature
native-contract files in Runtime/Native/Cursor/Movement/Moat/Attack/Formation
retain their reference RVAs, patterns, derivations and ABI checks. Initialization
validates the reference bytes against the Extender-owned scan image and the
existing bounded resolver. Fixed record/scratch/path layouts remain hash-bound:
a code signature does not prove a new native layout. Existing feature groups
fail closed independently; published hooks stay rooted and installed. No new
fallback is introduced by the move. Reaudit all affected flows, displaced spans,
incoming edges, actual NativeX64 scheme and layouts on a native/backend update.

Native movement wrapper18E1E0 has four parameters;196280 is its distinct core.
Unit records are0x490 bytes; movement preview snapshots/restores the record and
must retain the distinction between preview and actual world notifications.
The cursor dispatcher8C5F0 has PE unwind range[8C5F0,90500) including19 embedded
switch tables; the old candidate database end90001 is incomplete. Do not use
linear decoding of that entire range as proof of executable instructions.

Detailed local review/evidence: _inspect/APISharedOwnership/COMMAND_OWNERSHIP_AUDIT.md,
cursor-dispatch-targets.log, fixes-dynamic-command-spans.json and preserved
before-command-ownership sources. They are workspace audit artifacts, not public
APIShared dependencies. Source equivalence across70 moved files is checked modulo
namespace/friend ownership and two imports. Game acceptance remains outstanding.

## Formation movement permission correction (2026-10-11)

Owning feature: Formations. Reference native SHA256:
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The existing 195E30/196100/11B520/118E00/196280 chain and 18E1E0 path probe
retain their existing signatures, search scopes, resolution and fail-closed rules.
No new RVA, hook or layout dependency is introduced. Group-member permission is
owned by Vanilla, not inferred from a positive path query. 11B520 excludes stationary
Ballista (3D), Trebuchet (28), Mangonel (29) and restricted states; 118E00 has its
own member predicates. 196280 does not repeat those checks.

The formation post-order retry now requires a matched, unvetoed terminal Post
with observed return zero. Missing calls, abandoned/vetoed frames and positive
returns never receive direct MoveToTile retries. Recheck current global identity,
life and tribe and retain the original extra parameter. Reaudit these event and
permission boundaries after Native/Extender updates; do not substitute a type list.
Evidence and pending gameplay acceptance:
_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FORMATION_MOVEMENT_PERMISSION.md.

## 2026-10-11: preserve native ground cursor permission

Native SHA-256 remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The complete cursor CFG [8C5F0,90500), including its embedded dispatch tables,
shows a separate permission channel from route reachability. At 8F10F R15D
starts at one. Target veto 198C40 at 8F1E5 and selection predicate 1811A0 at
8F1F8 can clear both EBX (reachability) and R15D (permission). 1811A0 scans
selected living, non-killed units controlled by the active native player and
requires a type other than 28/29/3D. These are the native stationary engines;
no mod-maintained type list is introduced. 198C40 retains its target/structure
restrictions. From 8F206 to the call at 8F325 no instruction writes R15.
Function names remain candidate; these register/data-flow findings are static.

The 196870 call at 8F325 is the native Assassin/special-selection fallback,
not a fresh movement permission check. Promoting its zero result from a
positive 18E1E0 path witness previously re-enabled EBX via E2CA0 at 8F350,
bypassing the prior permission veto. This produced false accepted cursor
feedback; 195E30 stages Chore17 before its own feedback/type checks. The later
formation fallback fix protected execution but did not protect this UI path.

The existing six-site adapter now passes its RVA and, only at 8F325, R15D.
The callback clears pending cursor state and retains a zero result when that
native ground permission is zero. Nonzero original/Extender results remain
full-width and authoritative. The other five sites are attack queries and
never consume their unrelated R15. Existing reachable-member and moat route
corrections remain after the permission gate. No marker suppression, extra
order, second 196870 detour, permission probe, or public interface is added.

Producer bytes and the existing complete displaced spans are checked before
publication. Production emitter tests execute all six Win64 callback paths
with permission zero/one and zero/nonzero/full-width results. The complete
production cursor fixture exercises pending-state clearing, provider settings,
route repair and attack isolation. Rendering still reads final native feedback;
formations and Shift commands continue through the original group/member
checks. Visible Vanilla comparison and multiplayer acceptance remain pending.

### TEMP_GATE_ROUTE_ACCEPTANCE: output-mode evidence (2026-10-11)

Owner: existing UnitCommandPathRuntime F4930 detour; no added hook. Reference DLL SHA256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2, RVA 0xF4930. At the existing entry/exit, read already audited manager modes +0x84/+0x88/+0x94 and output length +0x155F68; capture original arguments 2/3 as raw values. F4930 at 0xF4B9C tests +0x94 and argument 3 zero before DB650/DA590 and E1640 mode-1 reconstruction. Entry eligibility is not runtime proof that this branch executed. +0x94 remains excluded from exact staged Assassin publication. Use the existing hash/signature and displaced-span validation of the functional owner; no separate executable patch or address fallback. Unknown/unowned context is not read; diagnosis is inactive without an observer and cannot authorize publication. On future native updates re-audit the whole F4930 search/reconstruction flow rather than removing this guard.

### Drawbridge nonlethal gatehouse damage (2026-10-13)

Owner: src/DrawbridgeDestructionFix/FixRuntime.cs, permanent X64InlineHook.
Reference native SHA256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Audit the complete direct damage function 0x7EB00..0x7F88A, all incoming
edges, bridge coupling/removal, area damage, demolition and deferred removal
before adapting this feature. Hook 0x7EE6B displaces exactly 15 bytes (4+6+5),
continues at 0x7EE7A, and skips early bridge removal at 0x7EF87 only when
argument 8 is 1, type is 45/46 and the newly stored signed Int16 HP is positive.
Native manager slot stride 0x32C, type +0x12E, health +0x168; public view +0x5C.
Original displaced instructions must execute fully when disabled. Preserve
RAX, flags and all other live state. The installed RedBird backend must again
assemble/decode the production emitter and confirm its 15-byte displacement.

Test-DrawbridgeDestructionFix.ps1 and tests/DrawbridgeDestructionFix.Tests
verify hash, original bytes, layout, full-function control flow and 150 native
execution cases. Live interior bytes must match the reference function;
Extender entry instrumentation is allowed only in the first audited 64 bytes.
Unknown hash, conflicting bytes or an unexpected span abort this feature only.
Installation candidates roll back before publication; published hooks and
subscriptions remain statically rooted and activation changes only data.

EnableDrawbridgeDestructionFix is SyncHostOnly, defaults true, resets true and
participates in the existing preset/host/client/trail rules. Effective state
is EnableMod && EnableDrawbridgeDestructionFix. Existing dependency minima
and main version remain unchanged during main-mod acceptance. APIShared is
unchanged: existing public damage/coupling/lifecycle services suffice and no
second user justifies a new public native service. Diagnose installation and
state transitions, one identity-checked protected hit, and at most one observer
error; the observer exits immediately after confirmation. Do not restore test
snapshots, map summaries, geometry caches or per-hit logging.

Independent testmod revision 2 editor acceptance: 2026-10-13 16:57..16:59,
668 paired calls, 665 protected native decisions, 64 detailed surviving-hit
bridge captures and 3 lethal removals (both stone gate sizes), no errors or
pending follow-ups. This confirms the testmod, not main-mod or multiplayer
acceptance. Main-mod testing must include two bridges and bridge states,
option/mod disabled, demolition, projectile/area damage and multiplayer.

## Improved building preview (local QoL)

#### Native contract

Feature: local presentation only. Reference native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Script Extender source commit 5908e1f12437deb7ef5f5b1dc2d64e301178fba3, tree c238441ea12db35ee36c5c5668d3c8fce0897295; installed enums and farm scales checked independently.

### Audited data flow

Input 0x90CD0 recentres the cursor by scale/2, passes the native manager, player, top-left XY, signed short mapper, scale and preview-mode byte to 0x77E60. Game and delayed construction 0x6D580 perform the same validator before any creation. This mod changes neither path.
0x619C0 generates square XY offsets 0..scale-1; 0x69850 reads them, changes footprint count and rotation-dependent sprite indices. Wheat/hops use 9x9; apple/cattle use 10x10. The validator reads logic at manager+0x898400 and terrain at +0x9D2500; River excludes a tile from coverage, otherwise OasisGrass, ThickFoliage and Foliage each add one separately. Coverage must reach scale squared. The independent green counter counts OasisGrass and ThickFoliage once each, never Foliage, and requires >=50; River excludes both counters (0x787C7..0x78878, final comparisons at 0x79898). Combined flags may compensate a zero-contribution tile; do not assume a simple percentage.
Native validity uses image+0x3A11EA4[y*800+x]; packed indices use image+0x402FF2C[y*3]+x. Coordinates are not packed indices. Layer capacity is 320800, including packed index zero. Manager image RVA 0x405EDB0.

0x5DD70 builds preview graphics. Input ABI void(manager pointer, int x, int y, signed short mapper, int scale). Capture manager+0x204E6FC before Vanilla (0 valid, 1 blocked, 2 out-of-map); +0x204E7EC is the one-shot alternate preview override, cleared by Vanilla. Alternate/queued preview calls from 0x63FB0 must not acquire markings. In the blocked path 0x7B060 tests collision, height, ownership and object blockers but not farm ground coverage. For farm types, it writes palette token 55 (red) or 62 (dark) to manager+0x3B3200. Valid placement instead builds the farm sprite/plot preview. Only replace existing 62 after Original, and only if the corresponding full-footprint requirement is deficient. Failed green coverage marks zero-green contributors; union the failed farm requirements.
Renderer 0x41D60 reads image+0x4411FB0 (manager+0x3B3200); 0x1A2D90 transports the colour at short record index 7 of 21. Director.Update consumes the exact frame buffer; GameMap.processTestMap copies floatColour and gameTile applies TilemapManager.tileColRed for token 55. Renderer clears consumed construction entries. Terrain, geometry, creation, costs and chores remain Vanilla.
These contracts are confirmed statically against the installed native image, disassembly and Managed/Extender sources. The user confirmed the first-stage red dirt/sand markings in-game. The user subsequently confirmed expanded counters and the shared tooltip layout in-game. The integrated local setting still needs a game test.

### Permanent detour

Entry RVA 0x5DD70; function range [0x5DD70,0x60CAC). Entry bytes: 48 89 5C 24 20 / 55; continuation RVA 0x5DD76 (push rsi).
Installed RedBird NativeX64 1.5.0.0: Indirect needs six bytes, displaced instructions are exactly the five-byte rbx shadow-space store and one-byte rbp push. They have no branches or RIP-relative operands; the trampoline must execute both and jump to entry+6. ABI-preserved registers/flags and stack follow Vanilla; no context callback, SIMD clobber, custom assembly or Rflags interpretation is used.
Allow only Indirect, refuse chaining, follow neither jumps nor calls. Confirm Scheme, DisplacedByteCount, target, copied trampoline bytes, continuation, FF25 patch, signed RIP pointer-slot displacement and pointed-to HookEntryPoint both before and after installation. Full body/direct-branch decoding plus baseline xrefs checks prohibit outside entries into (0x5DD70,0x5DD76).
Resolver first checks reference RVA/signature, then an executable-section unique pattern fallback. The fallback does not authorize changed manager/field layouts: this entire feature remains hash-bound and the resolved function must still match the audited RVA. Unknown hash, backend or conflicting hook disables only the feature with Error-level diagnostics; initialization candidates may roll back before publication.
Published hooks never disable, dispose or repatch. Logical active state is atomic; an inactive wrapper calls Original exactly once. Runtime, logger, callback and hook are static and outlive startup component destruction. A single Debug callback marker with milliseconds proves execution after startup cleanup; install/wait/first-mark Info reports were removed.

### Updating

Repeat the complete input/validator/footprint/preview/render/reset/construction audit; verify changed source and installed Extender/Fixes ownership; regenerate all references together. Native marker numbers are palette protocol tokens rather than terrain enums. Runtime uses named Extender domain enums. No semantic fallback exists for fixed native data offsets.

### Resource and tooltip extension, 2026-10-13

0x77E60 counts HasStone (bit 17), HasIron (bit 19), IsPitch (bit 31) before River exclusion. Quarry mapper 56 uses 6x6 and >=8; iron mapper 90 uses 4x4 and >=4; pitch mapper 91 uses 4x4 and >=1. No percentage or texture inference. The existing preview hook also captures suitable placements, without invoking another validator. Trees, rocks and Killingpit already receive Vanilla red; clearly visible buildings keep their deliberately omitted preview tokens. No additional missing object class was demonstrated.

Immutable snapshots carry mission SessionId, mapper and native top-left XY. Native workers never touch Noesis. APIShared BuildingRollover/GuiChecks Post observers are process-lived, use the Awake-confirmed Unity thread, and render only matching snapshots. MainControls map coordinates are converted through GameMapTile.gameMapX/Y and scale/2, matching 0x90CD0. Cancel, mission end/initialization and invalid/changed selections discard old snapshots. Pending camera rotation and GUI hover use minimum values. No time-based expiry, so pause remains supported.

The real Assembly-CSharp hash BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789 is checked before builds. Private HUD_Main HoverStruct/SelectedStruct/SelectedString/OtherVisible/OtherHighestPri are read only through validated reflection; HoverString is public. Public MainViewModel.Instance/HUDmain/RolloverBuilding_TooltipVis, MainControls.instance/CurrentAction/CurrentSubAction/overGUI/isOffWorld/getMouseMapTilePosition, GameMap.instance/pendingRotationtriggered/getMapTile and GameMapTile.gameMapX/gameMapY are compiled against the real assembly. Actual public signatures are additionally asserted by managed-contract checks.

Vanilla FatControler.setupText exposes 21 locale identifiers, matching BuildingCosts/BuildingLimit's 21 files; the plan's count of 22 was inaccurate. All 21 receive five labels. Arabic numeric fragments use Unicode LTR isolates inside the inherited RTL flow; both shared controls wrap within 440px. APIShared inserts its shared hosts immediately before the original cost rows in both variants, preserving BuildingCosts replacements and BuildingLimit contributions. Each patch Content has one root. Shared publisher usage and optional dependency ordering avoid competing HUD hooks; Fixes does not hook this preview entry.

### Shared tooltip layout, 2026-10-13

The user confirmed the expanded counters and markings work in-game, but rejected the prior split/moved tooltip layout. APIShared 0.9.0 now owns plain-text contribution hosts before both cost rows. Farm contributes Ground with maximum sort order, so ordinary contributions (including BuildingLimit) sit above it. Private HUD selection reflection moved to APIShared; the farm's native snapshot/cursor/session rules and permanent hook remain unchanged. BuildingCosts keeps cost/icon rendering but preserves the original left anchor. The old farm XAML patch and view model were replaced; installation removes that exact stale patch from local/installed packages.

### Local QoL migration, 2026-10-13

BugfixesAndQoL owns the preview detour and the Ground contribution. [PresetLocal] EnableImprovedBuildPreview defaults true, persists through the existing preset service and is gated by EnableClientFeatures. APIShared minimum is 0.9.0. Shared tooltip/public contracts are unchanged; optional consumers need no GUID-specific integration.

BuildPreviewStateStore atomically publishes immutable activation, generation, session and snapshot together. Activation/session changes increment the generation and clear actual counts. A captured producer can only compare/exchange against the same generation/session, so a late callback cannot republish into a newer activation. Unchanged snapshot values reuse the state and snapshot; soil and temporary preview buffers are still checked on every eligible native callback. The diagnostic eligible-tile traversal has been removed.

Native regression and full-image incoming-edge checks: tests/BuildPreview.Tests. Real Assembly-CSharp signatures, hash, installed NativeX64 Indirect scheme, six bytes, trampoline, pointer-slot and HookEntryPoint remain covered by Test-BuildPreviewPreflight.ps1 and the machine tests. Acceptance evidence and pending integrated-switch checks live in tests/BuildPreview.Tests/ACCEPTANCE.md. No allocation/FPS claim is made beyond the isolated production state publication path.

## Infinite patrol isolation from the Shift queue (2026-10-17)

Reference SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The existing hash-bound Chore-17 hook also reads the signed patrol-mode staging
field at RVA `0x86C1338` (between Y `0x86C1334` and move type `0x86C133C`).
Producer `0x195E30` stores argument 5 there; Chore-17 handler `0x10AE0` packs
one byte and sign-extends it on unpack before `0x196100` / `0x11B520`.
The field is read only during outgoing Chore packing. No additional hook or
executable-memory mutation is introduced. No semantic relocation fallback is
available for these staging globals; the existing fixed-layout hash gate remains
mandatory. On a native update recheck the complete pack/unpack flow and field
width/order together with the existing queue marker contract.

`TribePatrolMode.PatrolInfinite` bypasses queue marking and capture. Chore 71
(`0x176C0` -> `0x11C3A0`) retains its original one-based tribe ID, coordinates,
waypoint index and signed mode. Only the affected tribe's old queue and dedup
state are discarded. `PatrolOnce` and ordinary Shift Moves retain queue handling.
Production-entrypoint regressions: `tests/Issue6.Tests`; actual in-game looping
and real host/client acceptance are separate from those boundary-double tests.


## Formation command speech (2026-10-17, Issue 6)

Reference native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
FormationFeature retains the canonical native hash gate. FormationSpeechNative is a
mod-internal callable adapter, not a hook or a replacement movement command.

- 0x11A1B0: selected dominant voice type. ABI: int(nativeTribeManager, int tribe,
  int* optionalCount). RCX/RDX are unused in this binary; R8 is the optional output.
  Despite the decompiler's void declaration, EAX returns the chosen type (verified
  complete disassembly and 0x195EA7 call). It counts selected, alive, local units of
  types below 90; ties prefer the lower type. No parallel voice table is maintained.
- 0x18D460: native selected representative. ABI: int(nativeUnitManager, int firstId).
  Use manager RVA 0x67E8400 and first Game-ID 1. This scans by native type priority
  and uses current-unit scratch global 0x9302C4; capture saves/restores that Int32
  in finally. The representative's current LogicGrid tile must not have bit
  0x40000000. Engineer (Extender enum) with GameUnit+0x388 != 0 suppresses speech;
  this field is manager+0x658+gameId*0x490+0x388, matching global 0x67E8DE0.
- 0xE0E0: int(nativeSoundManager), manager RVA 0x88E72D0. Preserves native
  busy-channel policy, including its per-player exception. This is checked inside
  the native frame, after DLL_RunTick has copied current Unity channel states.
- 0x3BC0: void(dialogueManager, int voiceType, int response, int targetTile).
  Dialogue manager RVA 0x64C4490. Response 6 is used when both leader/anchor axis
  differences are <=72; response 10 otherwise. Response 6 receives GetTileId(anchor),
  response 10 receives zero. Capture leader position before dispatch; never derive
  the voice from a delayed multiplayer receiver. DefaultInSync excludes Archer,
  Crossbowman and Catapult; Trebuchet, Mangonel and Ballista are always excluded.
  Archer_debug remains allowed. Native 0x3BC0 owns selection, duplicate suppression
  and special-type branches; 0x3770/0x38A0 and 0xEDF0 remain untouched.

Each new code target uses its complete-instruction signature, scanned only in PE
executable sections; duplicate or missing matches fail closed. An invalid reference
RVA still tries that executable pattern fallback. Data layouts have no semantic
fallback and remain hash-bound. Resolution logs retain method and resolved RVA.
Only speech is disabled on resolution/capture/playback failure; formation and
marker availability are not changed. No hook is added to any of these targets.

Local release capture -> accepted dispatch -> consumed input -> pending speech.
The existing 0x41D10 draw-list reset detour invokes the internal feedback callback
after its original. Full flow: DLL_RunTick 0x86680 updates channels, render 0x41D60
ends at 0x41D10, then dialogue 0x41C0 and sound/export paths run. 0xEDF0 writes
channel flags/path and emits a sound-volume record; the managed EngineInterface
reads speechFileName from returned play-state data. Callback exceptions cannot
repeat the original reset or disable markers. Only a matching main-thread
EngineInterface.run can consume pending feedback. No-buffer runs retain it; stale
map/player/tribe-global identities and feature disable discard it. Consume before
checking busy channels or emitting, so suppression/errors never replay a response.
Packet protocol and synchronized simulation execution remain unchanged.

Offline verification: tests/FormationSpeech.Tests executes production engine and
speech methods with boundary doubles, and checks real installed enum/layout,
canonical binary signatures, complete instruction spans and ambiguous/fallback
resolution. Audible output, Mono execution and real Host/Client acceptance still
require an in-game test; no installation is performed by the Issue 6 build.

Capture acquires the real private static object EngineInterface.threadLock by
validated reflection and nonblocking Monitor.TryEnter. A busy engine omits only
that optional response. Both the temporary 18D460 scratch write and its restore
are inside this lock. The render-end emitter also requires Monitor.IsEntered
on the same object. EngineInterface.run holds it around preDLLCallActions,
DLL_RunTick and play-state copying; no new native call races the simulation.
The offline test checks this field against the real Assembly-CSharp metadata,
not a publicized assembly, and exercises busy/unprotected/protected cases.

## Modsettings activation dispatches (1.0.196)

The current implementation replaces the former unconditional No Dogs / peace
patch installation with `PermanentNativeSwitch` and `NativeSwitchCatalog`.
Reference SHA-256 remains
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The complete feature audit is in baseline knowledge/MODSETTINGS_ACTIVATION.md;
its full-function incoming-edge, switch-table, data-flow and Fixes checks apply.
This paragraph supersedes the earlier description of the installation mechanism;
the original active No Dogs and peace semantics remain the reference.

No Dogs demand is `EnableMod && ShowMpAdvancedSettingsInSingleplayer`;
peace demand is `EnableMod && EnablePeaceTimeGameplayFixes`. Client is independent.
Each logical feature roots all prepared hooks before publication with demand=0.
All groups and following instructions are byte-validated before the first Enable.
A partial publication failure retains every rooted hook with demand=0; original
paths remain valid. Published code, pointer slots and pages survive process exit;
settings changes write only the one-byte native demand, never executable bytes.
There is no native/managed hot-path transition or setting-driven teardown.

The actual RedBird X64AssemblyPatch backend writes a six-byte RIP-indirect jump,
rounded to complete instructions; the existing two entry spans remain 16/18 bytes.
Near allocation and signed RIP reach, OverwrittenByteCount, target address,
complete original/copy bytes and the first following instruction are checked.
Info initialization logs contain start/end RVA, instruction lengths and following
bytes. Reaudit all spans and incoming destinations on a DLL/backend update;
a changed hash is rejected without a pattern fallback.

| Group RVA | Physical bytes | Copied active bytes | First following instruction bytes |
| --- | ---: | ---: | --- |
| `0x105520` | 7 | 7 | `833D46A46E0300` |
| `0x119050` | 16 | 16 | `33DB` |
| `0x124BB4` | 6 | 6 | `48895C2460` |
| `0x1256C3` | 6 | 6 | `48895C2460` |
| `0x1259F3` | 6 | 6 | `8B0529055403` |
| `0x156EAB` | 6 | 6 | `664239BC2B2C0A0000` |
| `0x17F3E0` | 6 | 6 | `460FBF8C291E070000` |
| `0x1867A0` | 18 | 18 | `4C03D9` |
| `0x1868B2` | 6 | 6 | `6683E92D` |
| `0x186976` | 6 | 6 | `B801000000` |
| `0x18699A` | 6 | 6 | `83F863` |
| `0x8A141` | 7 | 9 | `744E` |
| `0x8D360` | 6 | 8 | `740F` |
| `0x8D7BD` | 7 | 9 | `7409` |
| `0x8E05E` | 7 | 7 | `448B44244C` |
| `0x8E0D7` | 7 | 9 | `7409` |
| `0x8E5F8` | 7 | 9 | `7409` |
| `0x8EA59` | 7 | 7 | `4963C1` |
| `0x8EC9E` | 7 | 7 | `4963C1` |
| `0xABC88` | 6 | 8 | `741A` |
| `0xC70BA` | 9 | 9 | `0F858A010000` |
| `0xCA904` | 7 | 7 | `8B0D47B65903` |
| `0xCDD81` | 6 | 6 | `458B8850390000` |
| `0xCE2FD` | 7 | 9 | `7408` |
| `0xFFD94` | 6 | 6 | `833DC3FB6E0300` |

The dormant side relocates exactly the physical original instructions and jumps
to the first following original instruction. The active side relocates the full
audited group, applies the original 28 peace edits / No Dogs edit, and returns
after that group; starting-troop and wildlife prefixes retain their old guards.
The flag-neutral dispatcher saves RCX, reads demand with MOV/MOVZX/JRCXZ, restores
RCX and tail-jumps. It preserves incoming flags, stack, other GPRs and XMM state.

`Test-Modsettings.ps1 -RunNative` exercises the real installer in private executable
PE copies. It compares the full No Dogs function for 322,560 inputs across original,
old active, production off/on/off, validates every grouped/following byte, and
checks register/flag preservation, branch/RIP relocation and exact continuations.
Executable target bytes remain identical across settings changes. The existing
VanillaPeaceTimeNativeTests retains active start-troop/wildlife/peace coverage.
These offline tests do not replace live Mono, game UI or two-player acceptance.
