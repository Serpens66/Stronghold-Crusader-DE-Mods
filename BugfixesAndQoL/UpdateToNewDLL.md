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
