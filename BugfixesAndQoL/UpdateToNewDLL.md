# Native update contract: Bugfixes and QoL damaged health bars

Native contracts of commands, routes, formations, the Shift queue, troop movement, gate routing and Assassin
routes moved with their code to `Pathfinding/UpdateToNewDLL.md` (2026-11-07).

## Engine presentation bridge (2026-10-23)

The private VanillaPerformance bridge uses managed `EngineInterface.run` and
`SaveSaveGame` IL, the existing `threadLock`, and public managed/PInvoke methods.
No new native hook, address, structure layout or APIShared signature. Validate
both wrapper signatures, unique Monitor/finally scopes, gate field identity,
buffer/native/publication order and the save call before activation. Unknown
anchors retain the incoming IL and disable this optimization. Other transpilers,
including PerfProbe, retain labels and exception blocks. Recheck these contracts
and the production Harmony fixtures after a game update.

The underlying GetMeritData/SetAchValues/save behavior was audited against
native hash `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
see [ENGINE_PRESENTATION_BRIDGE.md](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENGINE_PRESENTATION_BRIDGE.md).

## Minimap refresh throttle V13 (2026-10-23)

- Owner: `src/VanillaPerformance/MinimapRefreshThrottle.cs` (host setting `EnableMinimapRefreshThrottle`).
- Reference: hash `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, RVA `0xF5A40` (minimap pass), prologue `48 89 5C 24 10 55`; NativeX64 `Indirect` detour with exactly 6 displaced bytes (pointer-slot patch validated after commit).
- ABI: single direct caller `0x86B9B` in `DLL_RunTick`; `rcx`, `rdx`, `r8` (`radarMap`), `r9d` and seven stack dwords `[rsp+0x20..0x50]`; return value unused. No internal branch targets `0xF5A41..0xF5A45`.
- Managed contract: `GameMap.processTestMap(short[], int, EngineInterface.PlayState, byte[])` stores the private `lastRadarMap`; `GameMap.ApplyRadarMap` uploads it once per frame; `MemoryBuffers` keeps a ring of 6 buffers.
- Fallback: none (hash-bound). Another hash, prologue or member layout leaves Vanilla unchanged and logs `VANILLA_PERF_MINIMAP_THROTTLE_DISABLED`.
- Update checks: find the pass again (only writer of `radarMap` and `*(radar+0x70)`, no simulation writes), re-check the call ABI, the displaced span and the managed buffer ring; audit: [RADAR_PASS_F5A40.md](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/RADAR_PASS_F5A40.md).

## Tribe member lookup V14 (2026-11-02)

- Owner: `src/VanillaPerformance/TribeMemberLookupCode.cs` (machine code) and `TribeMemberLookupOptimization.cs` (installation), enabled with `VanillaPerformanceOptimizationsActive`.
- Reference: hash `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, RVA `0x119F90` ("unit id of the i-th tribe member"), body `0x8C` bytes with SHA-256 `CD5DAE0B455BA6CBFC20D0193849591A8250C710C8B77BBDCFFF6908FBD1544F` (shared constant with `TribeFloodMembership`). `X64InlineHook` at the entry with exactly 18 displaced bytes: `mov [rsp+8],rbx; movsxd rax,edx; xor r9d,r9d; imul rdx,rax,0x688` (no RIP-relative operand, no branch of the body into `0x119F91..0x119FA1`); absolute-indirect jump and padding validated after commit; exclusive `NativeSiteRegistry` span.
- Contract: `rcx` tribe manager, `edx` tribe id, `r8d` ordinal; 0 unless `(short)tribe+0x40 == 2` and `0 <= i < (short)tribe+0x5C`; otherwise `word*16+bit` of the i-th set bit of the 625 words at `+0x60`, else 0. The stub writes only `rax, rdx, r9, r10, r11` (a subset of the original's) and keeps `r8` as the original does. Disabled: the displaced instructions and the original body run.
- Fallback: none (hash- and body-bound). A different hash, body or an occupied entry leaves Vanilla unchanged and logs `VANILLA_PERF_TRIBE_MEMBER_LOOKUP_UNAVAILABLE`.
- Tests: `tests/TribeMemberLookup.Tests` copies the installed body into executable memory (it is position independent) and compares Vanilla's copy with a copy hooked through the real `X64InlineHook` backend, flag on and off, on random and edge-case tribe records; registers `r8`/`rbx` are checked.
- Update checks: re-audit `0x119F90` and its callers (stride `0x688`, kind `+0x40`, count `+0x5C`, words `+0x60`, 625 words), the displaced span and incoming branches; re-run the machine test against the new DLL.

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

Resolution requires the audited file hash, the full 40-byte prologue at the
reference RVA and the complete function body. A changed hash or entry stops
validation before any pattern search. Caller and incoming-edge evidence applies
to the audited binary and entry. Error-level timestamped
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
## AI sell threshold safety (1.0.197)

BFQoL owns no executable bytes for this fix. Its default-enabled, synchronized Host
option `EnableAiSellThresholdFix` requests both safety flags from APIShared
`AiMarketSellPolicies`, registration `BugfixesAndQoL_Serp/ai-sell-threshold-safety`.
Effective activation is EnableMod && EnableAiSellThresholdFix on every peer.
The static registration survives startup cleanup and card/save changes; only its
logical activation changes. No Dispose/unpatch path and no per-sale managed callback.

APIShared 0.12.6 or newer is required. The SDK owns hash/backend/layout validation,
both 15-byte spans (0x3F1F3 and 0x29723), final stock recheck and cancellation via
0x2978B. Its UpdateToNewDLL.md section "AI market sell policies" is authoritative.
Reference native SHA-256 remains
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
No overlap with BFQoL's stone-reserve site 0x3F156..0x3F16A, Fixes' wheat-category
site or native price hooks. Disable the standalone AISellThresholdFixTest plugin
before the next game start; retain sources/evidence rather than deleting it.

## Update fallback, startup checks and remaining hash bindings (2026-10-22)

Procedure: [AGENTS/native-analysis.md](../AGENTS/native-analysis.md), section â€žPrÃ¼fkostenâ€œ.
- **Known native hash:** reference bytes only.
- **Unknown hash:** one section-limited pattern fallback whose result is cached in `BepInEx/cache/BugfixesAndQoL_Serp.native-cache.tsv`. The cache key is the native SHA-256 plus the mod version.
- The shared cache is `src/Core/Native/BfqolNativeCache.cs` over `Shared/Runtime/Native/NativeResolutionCache.cs`, saved once at the end of LibraryLoaded.
- The CrusaderDE.dll hash itself is cached across starts by path, size and write time (`Serp.file-hashes.tsv`).

**Notification queue (`src/Notifications/NotificationQueueNativeContract.cs`)**

The eight patterns previously had no reference RVA and were scanned on every start. The references below come from the installed FBCB9319 DLL; each pattern matched exactly once, and the fixed distances in the contract hold:

| Pattern | Reference RVA |
|---|---|
| DLL_RunTick entry | `0x86680` |
| DLL_RunTick notification call | `0x86A91` (= entry + 0x411) |
| Queue update entry | `0xFE570` |
| Completion tail | `0xFE5C9` (= update + 0x59) |
| Queue finalizer | `0x102A70` |
| Queue promotion | `0x102AB5` |
| Presentation entry | `0xFEE50` |
| Presentation pending-flag set | `0xFEEB1` (= entry + 0x61) |

Another build scans the unique patterns once and caches them. All call-target and distance checks of the contract still run.

**Reference-first uniqueness checks**

Formation selectors (`ValidateUniqueEntry`), AI defense patrol, AI resource-shortage sleep, AIV defender position, AI wall targeting, control-group disband, healer attack, Lord control group and the connectivity observer used to rescan the executable sections to prove uniqueness, although their result must equal the reference RVA.

They now confirm the reference bytes when the hash matches and scan only for another build, so the behavior on an unknown build is unchanged. The second uniqueness scan of the workshop idle delay was removed; that feature is gated by the reference hash.

**SavegameMissingMap (managed)**

The SHA-256 gate of `Assembly-CSharp.dll` was removed. The fix is gated by the IL contract of the loaded methods:
- member checks;
- the `RunHookPlan` preflight of every transform;
- a JIT-compiled copy of each transformed method.

An update that keeps these methods keeps the fix; any IL change fails closed with `FIX_DISABLED`.

**Peace time / native switches (`src/Core/Native/PermanentNativeSwitch.cs`)**

Instruction boundaries are proven on the unmodified load image. Live, a foreign hook may already start at the boundary after a span; for example, the Script Extender's wall-targeting context hook starts at `0x8EA60` right after the peace-time span `0x8EA59..0x8EA5F`. Only `jmp rel32` or `jmp [rip+disp32]` is accepted there; any other live change fails closed.

**Still hash-bound: ExtendedShiftCommandQueue, Formation and FormationSpeech**

These features depend on the shared command runtime `UnitCommandPathRuntime`. It requires the reference hash and binds about 40 fixed detour prefixes (`src/UnitCommands/Native/NativeDetourContracts.cs`) and about 100 native constants. Deriving their own data addresses cannot keep them working after an update until that runtime gets the same fallback.

The audited anchors, with a second evidence for the Chore staging globals, managers, strides and speech call targets, are recorded in [MOVE_STAGER_DATA_ANCHORS.md](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MOVE_STAGER_DATA_ANCHORS.md). Formation's `TribeUnitCountOffset 0x5C` and `MovementTargetAvailability 0x3A11EA4` have no confirmed anchor yet.

## Moat terrain restoration (2026-10-25)

Owner: BugfixesAndQoL `MoatTerrain`, host `EnableMoatTerrainRestoration` (default true).
Reference SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The feature is layout-bound. No unknown DLL may use these field/table offsets solely
because a code pattern matches. Unknown hash/layout or any failed span disables only
this fix, retaining Vanilla. There is no compatibility fallback for the grid layout;
it has not been independently derived for an unknown binary.

| Stage | Function RVA | Hook `[start,end)` | Length |
|---|---|---|---|
| TaskCreated | `0x59210` | `0x59359..0x5936C` | 19 |
| Excavated | `0x639C0` | `0x63A4E..0x63A5E` | 16 |
| Filled | `0x69470` | `0x694D3..0x694E2` | 15 |
| DirectPrepare | `0x6FE90` | `0x705BF..0x705CD` | 14 |
| BrushFinished | `0x6FE90` | `0x70940..0x70952` | 18 |
| BridgeRemoved | `0x622D0` | `0x6240E..0x6241F` | 17 |
| BridgePurged | `0x6CA30` | `0x6CADD..0x6CAED` | 16 |
| AreaRemoved | `0xED2E0` | `0xEDA8C..0xEDA9B` | 15 |
| Paint | `0x6FE90` | `0x7082E..0x7083F` | 17 |
| BuildingPrepare | `0x6D580` | `0x6D8DF..0x6D8F3` | 20 |
| Replaced | `0x5D740` | `0x5D76F..0x5D783` | 20 |
| Erased | `0x7AE50` | `0x7AFAF..0x7AFC0` | 17 |
| Vegetation | `0x108320` | `0x1085D0..0x1085E2` | 18 |
| VegetationUpdate | `0x109930` | `0x109A05..0x109A16` | 17 |

Every site has exact reference bytes and a section-bounded unique-byte pattern
resolver before installation. A moved pattern result is refused under the current
hash-bound semantic contract. Whole immutable function-body hashes, complete
instruction decoding, canonical DLL SHA-256 and the retained incoming-reference
audit qualify these spans; all live spans must still match before publication.
The installed RedBird X64 inline backend must displace exactly the recorded length.
All original instructions execute once at the original RSP, including external
calls and conditional branches. Full GPRs used by our operations and original flags
are saved/restored; our native operations use no XMM instructions or managed calls.

Data anchors: module+`0x405EDB0` tile manager; manager+`0x898400` uint logic flags,
+`0x9D2500` byte soil, +`0xB0BCA0` ushort building IDs; physical capacity 320800.
The task allocator's frame argument at original RSP+`0x50` is the native creation
mode (zero planned, nonzero completed/filling record). Task and vegetation pointer
operands remain the original validated native records; tile indices are zero-based
packed indices, not one-based unit/building game IDs. Bridge identity uses the
Extender `eStructs.STRUCT_DRAWBRIDGE`; flags use `TilePropertyFlag`.

ExtraFeatures height spans end adjacent to, or outside, these sites, including
`[0x694BD,0x694D3)` and `[0xEDA77,0xEDA8C)`. Their hooks and the command runtime's
entry detours are not relocated or replaced. The immutable load snapshot is used
for semantic validation; only our actual live spans are required to be pristine.
NativeSiteRegistry claims the complete group before the hook transaction.
Published hooks, memory and registrations remain rooted until process exit;
settings/load/end only change data and the native activation gate.

Release review 2026-10-27: prepare with `InstallOnCommit=false`, validate all prepared
handles, retain resources as soon as the save registration succeeds, then enable
the hooks and validate every installed span before publishing readiness. A throwing
first/partial Enable or post-write verification failure never reaches rollback or
buffer release. Failed publication remains logically inactive across settings and
mission changes. Only a candidate with neither registrations nor attempted patch
activation can dispose its transaction and free its buffer. The existing production
publication fixture covers these failure boundaries separately from native-byte tests.

Native-only per-tile work uses a fixed 641664-byte state allocation (64-byte header
and 320800 ushorts), bounded tile access and no searches, allocations, logging or
managed callbacks. The buffer is not a Vanilla field. The versioned binary record
is a 16-byte header plus 641600 payload bytes, below the 1 MiB public entry limit.
Schema/identifier and known entry `terrain` are persistence contracts. Save scope is
`SaveGamesAndMaps`, and `SimulationRelevant=true` uses APIShared's existing peer
agreement. Known empty/disabled state writes an explicit empty record; future opaque
records retain APIShared preservation semantics and cannot enable restoration.

The reflected `EngineInterface.threadLock` must remain a non-null private static
object and the normal save/map-save wrapper must retain that gate around native
saving. Recheck both the real managed field and locking IL on an update. No direct
private-member access, own transport or per-tile locking is introduced.

Update checks: re-audit the full capture, completion, bridge conversion, all terminal
removal and explicit replacement branches; verify each input register/stack source,
field layout, branch ingress, continuation, shared neighbor and live span. Run
`Test-MoatTerrain.ps1 -RunTests`, production Modsettings tests and relevant preset
checks. Fixtures substitute external native refresh/call boundaries and do not prove
actual game visuals, farm eligibility or real host/client acceptance. See
`_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MOAT_TERRAIN_RESTORATION.md`.


## Tree-Blight decay (EnableTreeBlightDecayFix)

Hash-bound to FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Common event function 0x107A00 and growth 0x108320; exact half-open inline spans
[0x107A6C,0x107A7A) and [0x10838E,0x10839C), both 14 bytes. Reference patterns:
`41 FF C1 49 81 C0 9C 00 00 00 45 3B 4A 10` and
`66 83 BB 9A 00 00 00 00 0F 85 A9 02 00 00`. Following instructions are
`7C 9A` (jl 0x107A16) and `0F B7 83 98 00 00 00` (movzx eax,[rbx+0x98]).
NativePatternResolver validates the original load snapshot; moved matches are
refused until the complete semantics are audited. Live claimed spans must remain
original before installation. RedBird must displace exactly 14 bytes (3/7/4 and
8/6); there are no interior branch/jump-table/external entries. Re-audit these
properties on updates; a matching short pattern alone is insufficient.

Table RVA 0x31E220 is cumulative Int32 thresholds with seven entries per type.
Native woodland IDs 1..4 correspond to SE Chestnut/Oak/Pine/Birch and stage5 is 5.
Native record stride 0x9C, alive/type at +0x68/+0x6A, health/max +0x96/+0x98,
felled +0x9A, stage/progress +0xA4/+0xA8. SE struct offsets have a 0x18 header
translation. Event R8 points to type; R9D index and R10 manager feed the unchanged
loop tail. Growth RBX is record, RDI manager. Conditional bypass targets 0x1083B0;
original rejection is 0x108645. Global raw mode 0x3665F28, frontend 0x3665ED8,
scenario state 0x3665F90 and bucket selection must retain their audited semantics.
Mode 1/6 are editors, not played Freebuild. Preserve all original growth gates.

Validate both sites together, registry group TreeBlightDecay (exclusive), then
commit one owned permanent transaction from LibraryLoaded. Keep published hooks
and the 32-byte native state rooted for process lifetime. Changes only write the
aligned enabled flag; verification failure after publication cannot reactivate it.
No load scan, extra RNG, managed per-tree callback, save/network extension or
health/resource/worker reset. Preserve Extender entry 0x108320 and observer
0x108518, and recheck Fixes/APIShared spans on update.

Run tests/TreeBlightDecay.Tests (the production emitter, x64 native fixtures),
production Test-Modsettings.ps1 and relevant HostClientPresetTests. Keep in-game
Vanilla/RandomEvents, Freebuild/skirmish/mission, Save/Load, real host/client,
occupancy cleanup and paired 800x800 load/event/throughput acceptance separate from
private-memory fixture results. Full audit and ABI details:
`_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/TREE_BLIGHT_DECAY.md`.

## 2026-10-26: editor terrain region publication

The editor-only correction consumes SE Post events for type painting/clearing, all
height tools, deletion and vegetation, armed only after APIShared editor completion.
It records pending changes, deferring both invalidation and forced rebuilding until
the primary mouse button is released. Work runs at the permanent managed preparation
postfix inside the real EngineInterface.threadLock, before DLL_RunTick/cursor work.
No map-start flood, per-tile managed hook or competing native detour is installed.

Reference native SHA256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Call-only target: region rebuild RVA 0xE49D0, Win64 int(context*, int force). The
production semantic signature checks force -> dirty+6C, countdown gating, generation
+74 and dirty clearing. Known hash uses reference bytes; failed reference validation
still reaches executable-section unique-pattern fallback. Unknown images use the
existing native-hash/mod-version resolution cache, confirming the signature again.
Context and generation/dirty access use the public installed SE view, whose two
field offsets are independently fixed by those decoded native instructions.
No stock component-buffer pointer/capacity is cached; foreign relocated buffers and
the existing command-runtime E49D0 entry detour remain authoritative.

The pre-cursor boundary validates actual public managed signatures, unique
preparation/native calls and engine Monitor/finally order; it also requires current
ownership of that lock before native work. Missing/ambiguous patterns, unavailable
APIs, unknown managed boundary or unsuccessful native publication disable only this
feature with a millisecond-timestamped error. Pending edits are consumed before the
call so nested changes survive. A generation changed AFTER the latest completed
edit AND clean dirty flag avoids a redundant rebuild; a mid-stroke rebuild cannot
cover subsequent unmarked edits. All patches/subscriptions
remain rooted until process exit; the master switch changes logical activation.

Release batching (2026-10-26): a permanent prefix on the real private instance
EditorDirector.Update() samples physical Mouse0 only for a pending batch. The IL
contract requires primary-button down/held/up ownership. SE KeyManager events are
not authoritative here: their independent state machine is UI-filtered and can
arrive before or after the editor's native-facing release. Unity input is never
queried on the engine thread. A batch-tagged atomic snapshot starts unknown, so
stale samples from a retired batch/session cannot release a later batch. Focus
loss publishes release; focus gain requires a fresh sample. While held/unknown,
the engine callback returns before reading path context, generation or dirty.
Only a released pending batch can request invalidation, once immediately before
its forced live rebuild. Actual delayed edits remain pending for another pass;
there is no timer or suppression of independent Vanilla rebuilds.

Audit: current baseline knowledge/EDITOR_REGION_REFRESH.md. Tests: production
RefreshState, installed member/IL preflight, and unchanged canonical flood/query
execution in private PE memory. Fixture memset and input graph are explicit
boundaries; real GUI and continuous-drawing latency need game acceptance.


## Endansicht nach der Zuschauer-Auslagerung (2026-10-27)

Die verwalteten Einstiegspunkte `HUD_MissionOver.ButtonClicked(string)`,
`GameData.Scenarios.setGameOverState(int,int,int)` und
`OnScreenText.addOSTEntry(eOnScreenText,int,int,int,int,int)` besitzen genau einen
APIShared-Hook je Stelle. Keine direkten Hooks der beiden Verbraucher mehr.
Bei einem Spielupdate Signaturen und Reihenfolge erneut prüfen: Spielende-OST
bereitet den Lobbyersatz vor dem Abbau der Teilnehmer vor; positive Endzustände
verwerfen eine Statistikvorschau vor dem Vanilla-Aufbau. Vorschau-Exit unterdrückt
Vanilla und Lobby-Deferral unabhängig von der Registrierungsreihenfolge.
Verzögerte Originalfortsetzungen sind an Aufrufthread, Sitzung und Ansicht gebunden.

Spectator besitzt Perspektive, Berichte/Verbündete, Save-Herkunft, Statistikvorschau
und den synchronisierten Zuschauerwechsel nach Ausscheiden. BugfixesAndQoL behält
Aufgeben, Lobby-Rückkehr und allgemeine Statistikabzeichen. Die native Niederlage
bleibt beim vorhandenen PlayerDefeat-Dienst; keine neue native Eingriffsstelle.

## Shared presentation ownership, 2026-10-27

APIShared 0.18.0 now owns the `0x41D60` visible-map entry. This mod registers its
render observer and toggles demand; it no longer installs a competing frame hook.
The original parameters and execution remain unchanged. Native contract and ABI
coverage are maintained in APIShared/UpdateToNewDLL.md and VisibleMapRenderTests.
Pause-menu extras use IngameMenuActions; Vanilla Quit keeps its original command.
Spectator statistics use the existing menu Close path (pause restoration), refresh
inside MissionOver, and the MissionOverEvents preview-exit contract. The Spectator
placement freeze exemption is scoped to active placement; report access independently
requires its enabled spectator perspective. BFQ retains no spectator report hook.

## Human Lord return teleport guard (2026-10-28)

Reference SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

### Ownership and activation

This feature owns only the permanent inline entry `[0x19BA40,0x19BA50)`.
APIShared.NativeSites group `Lord.HomeTeleportGuard` claims it exclusively before installation.
The unit-manager and lineup anchors and the standing-rescue CALL are registered as Observe. No APIShared/SE/Fixes source is changed.
Installed RedBird.X64 1.5.0 X64InlineHook decodes at least 14 bytes, emits the supplied generator,
then appends `FF 25 00000000` and the eight-byte continuation address. Here its actual displacement
is exactly 16 bytes. The prepared backend stub and installed patch are compared byte-for-byte.

Original instructions and lengths: `mov [rsp+18h],rbp` (5), `push rsi` (1), `push rdi` (1),
`push r14` (2), `sub rsp,20h` (4), `movsxd rsi,r8d` (3). Resume at `0x19BA50` is `mov r14,rcx`.
Direct-flow scan of all installed PE `.pdata` functions and existing semantic Xrefs found no target
inside the span; raw absolute-pointer scan found no pointer to an interior byte.
No neighboring foreign hook is bypassed: the normal branch resumes at the live continuation.

The hook is installed in LibraryLoaded even with the option disabled. Its logical flag is published as
installed && EnableMod && EnableLordReturnTeleportFix and is refreshed through ApplySettings.
Its runtime, hook and activation flag are process-rooted. There is no OnDestroy/OnDisable,
map-change disposal, live unpatch or delayed installation. Settings change only the atomic data flag. Only unpublished failure candidates
can roll back. A failed rollback retains storage and ownership with flag zero.

### Function and state flow

`0x19BA40(manager, oneBasedUnitId, signedTileX, signedTileY, height)` is a void relocation helper
(Windows x64 ABI: RCX/EDX/R8D/R9D, fifth argument on caller stack). It removes old occupancy through
`0x186AD0`, resets movement fields, writes next/current tile coordinates and fine coordinates `tile*8+4`,
height and compact tile ID, then adds occupancy through `0x180230` and refreshes view offsets `0x18BE50`.

Lord update `0x15A950`, CALL `0x15ABD1`, invokes it after `0x196280` returns zero in state 1 with
the new-route flag set. Return address `0x15ABD6`; Vanilla then clears the route flag and normally enters
state `0x6D`, resume state 2. Unit dispatcher `0x182B00`, CALL `0x18300F`, is the second rescue:
the current compact tile has component zero, lacks tile bit `0x40000000`, passes its building/special-unit
gates, is not state `0x6E`, and the unit age is at least 1000 ticks. It checks one of sixteen unit-ID buckets
per pass. It relocates Lords home and then sets state 1/new-route flag zero. Return address `0x183014`.

Important trigger correction: closing a gate and waiting is insufficient. In native nonzero game mode,
a human Lord finishing an ordinary move goes to state 13 and its normal idle handler does not automatically
leave that state. The building-attack state `0x6B` has target-loss/completion branches returning to state 0
(or state 1 for native zero mode), which reaches the return-path attempt. The user confirmed the video
shows a teleport after destruction of a building. Full user gameplay reproduction remains separate.

The standing-tile rescue is deliberately preserved for all players. The native stub compares the original
return address with image+0x183014 before inspecting unit data and runs Vanilla for this caller. Four
saved stack slots (flags/RAX/R10/R11) put that address at [rsp+32]. The five-byte CALL at 0x18300F is
validated against the captured image and registered as Observe. No caller or terrain code is patched.

For all other callers the patch suppresses only a live Lord with controlling player 1..8, lineup entry zero (SK_NULL), and both
requested coordinates equal to its stored home coordinates. It does not use local selection, local-player
identity, a managed roster, AI portrait or wall/tile searches. It changes no caller state or route result.
The blocked exit restores scratch registers and incoming flags before RET; the pass-through exit restores
them before replaying the original prologue and its normal flag effects. No XMM instruction or managed call
exists in the production predicate. Completed trace buffers, counters and per-teleport diagnostics have been removed.

### Address and layout map

| Contract | Reference RVA / offset | Evidence and update policy |
|---|---|---|
| Relocation entry | `0x19BA40`, resume `0x19BA50` | 31-byte exact semantic entry signature; executable PE sections only. Known hash checks reference bytes first, then unique signature fallback. A moved entry is refused because the rest of this prototype is hash-bound. |
| Standing rescue exemption | CALL `0x18300F`, return `0x183014` | Decode the five-byte direct CALL in captured memory, require relocation entry as target and the exact continuation; derive native comparison from validated image base. A changed contract refuses installation. |
| Unit manager | `0x67E8400` | RIP-relative LEA RBP at `0x15AB83`; independently CDE60 caller and native relocation member accesses. Runtime decodes the seven-byte anchor. |
| AI lineup | `0x8574C44` | RIP-relative LEA RAX at `0x28A7D`; same table as SE GamePlayerManagerAPI.GetAILord/IsAIPlayer. Int32 slots 0..8; zero is human/non-AI. Runtime decodes the anchor. |
| Unit addressing | `manager + id*0x490 + 0x65C` | Reserved slot zero, live IDs 1..9999; native loop bounds and installed GameUnitManager constants/layout. No `id-1` for this manager-relative formula. |
| Life/type/control/global | GameUnit `+0x88/+0x8A/+0x92/+0x94` | Manager working offsets `0x6E4/0x6E6/0x6EE/0x6F0`; layout checked against installed SHCDESE.dll. |
| Home X/Y | GameUnit `+0x336/+0x338` | Working offsets `0x992/0x994`; periodically written by `0xC23C0` from Keep position (Y-1 except Keep type `0x28`). |
| Current X/Y/height | GameUnit `+0xC0/+0xC2/+0xB6` | Working offsets `0x71C/0x71E/0x712`; actual native relocation writer and installed layout. |
| AI state | GameUnit `+0x2BC` | Working offset `0x918`; layout validation and test fixture only; caller writes checked by executed fixture. |

All current direct/derived RVAs used by runtime code are listed above. The standing-rescue CALL/return
is a validated gameplay exemption; other CALL/return addresses describe audited call flow and are not hook targets. Test-only native fixture references are the documented callees and caller blocks,
map validity `0x3A11EA4`, row-offset table `0x402FF2C`, tile height `0x4DDD350`, current-unit context `0x9302C4`,
route builder `0x196280`, completion query `0x188340`, Lord exit `0x15D3C5` and dispatcher continuation `0x183041`.
These fixture addresses never address the game process and are bound to the full reference file hash.

No data/layout fallback is claimed on a new hash: interop compatibility does not prove native storage or
player-table semantics. Unsupported hash, anchor, bytes, span or ownership disables only this feature with
LORD_TELEPORT_GUARD_UNAVAILABLE; no partial patch is published. Re-audit all writers/readers and both callers
before changing the hash, including zero/nonzero mode and human idle behavior. Do not merely update a pin.


Migrated production sources: src/LordTeleportGuard; native execution suite: tests/LordTeleportGuard.Tests.
NativeSiteRegistry owner is BugfixesAndQoL_Serp, registration Lord.HomeTeleportGuard.
The host-only EnableLordReturnTeleportFix defaults and resets to true; the client UI switch is unrelated.
No managed call or setting lookup is introduced in the native stub. The static feature root retains
its published hook independently of Unity cleanup; no tick observer is required. BepInIncompatibility
rejects LordTeleportGuardTest_Serp. Runtime logging keeps the shared once-only native address resolution
and the single LORD_TELEPORT_GUARD_UNAVAILABLE initialization failure; there is no success dump,
heartbeat, teleport trace, trace allocation or counter, including in /devdiag builds.
The user confirmed the integrated fix works on 2026-10-28. This is user-reported gameplay acceptance;
no additional editor, save/load or real host/client acceptance is inferred. Historical records remain unchanged.

## Peace-time civilian building proximity (2026-11-02)

This consumer adds no native site. It registers a civilian-only Limit of3 through
APIShared.Buildings.EnemyProximityPolicies and reads actual active peace through
APIShared.Simulation.PeaceTimeState. APIShared/UpdateToNewDLL.md owns those native
contracts. Existing VanillaPeaceTimeNativeContract patches remain independent.
Recheck standalone operation, legacy-ExtraFeatures refusal, native expiry/save
state, soldier/building exclusions and priority of ExtraFeatures rebuild vetoes
when either SDK or native library changes.

## Delayed prebuilt AI storage sections (2026-11-04)

Reference native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
`PrebuiltAiStorageFixRuntime` adds no native hook. Its prebuilt-player bitfield
reference is the `MOV ECX,[RIP+disp32]` at RVA `0x95FF8`, validated against the
same instruction pattern as the existing prebuilt-workshop correction. The
target is derived from displacement bytes `+2..+5` and instruction end `+6`;
the following player-bit setting and `0x55F50(...,100)` call establish the role.
Only the audited native hash is admitted. There is no unknown-build layout
fallback: a matching code reference cannot establish the fixed building and
AIV structure contracts. Resolution/layout failure leaves this feature inactive.

The storage adjacency branch at `0x7897F` (stores), `0x789FF` (granary), and
`0x78A7F` (armoury) calls `0xC72C0`, which requires an initialized same-owner,
same-type neighbor. `0xB47E0` creates `NeedsInit` records and immediately registers
the first yard through `0xD0630`; only the later `0xC60F0` dispatcher changes
state 1 to 2. The shared startup queue waits for that regular initialization.
Creation uses the Extender's `CreatePrefab` with active Vanilla placement rules.

The success bookkeeping mirrors `0x51790`: RVA `0x5214D` writes ushort 1 at
building-manager + id*`0x32C` + `0x324` (GameBuilding + `0x2C8`, named
`p_UnknownRelevant17`); `0x52174` writes step state 3 and `0x5219F..0x521B1`
writes rebuild delay 10 on success. The manager prefix is `0x5C`. A yard consists
of four records; mark the final record captured from that synchronous constructor,
not an assumed adjacent ID. `0x31000` only adjusts workshop goods and has no
storage effect. Existing confirmed sections retain their building flags while
matching prepared AIV retry frames become Built/10.

Recheck public SE AivVillageState stride `0x6D98`, MaximumBuildStep +`0x20`,
BuildStep stride `0x0C` with state/delay/type/payload at `0/1/2/8`, the real
selected-village mapping, all four yard records, first-yard Fixes policy, and
new-game/save/replay/multiplayer behavior when updating Native or Extender.
Source and offline tests do not establish actual game or peer acceptance.

### Pitch/stairs variant B integration

Reference DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Purpose, log markers, test procedure and dated runs: [docs/PITCHSTAIRS.md](docs/PITCHSTAIRS.md). Vanilla audit: [PITCH_STAIRS_HEIGHT.md](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/PITCH_STAIRS_HEIGHT.md).

Owner: `src/PitchStairs/NativeCode.cs`, `PitchStairsRuntime.cs` and `PitchStairsActivation.cs`. Scope: native corrections only; NetworkMode 1; versions and minimums per [info.json](info.json). Host option `EnablePitchStairsFix` is `[SyncHostOnly]`, default/reset true, under HostSiege / Fixes.
Installation success requires matching native contracts. No normal multiplayer desync has been demonstrated.

Owning feature: variant B of the pitch/stairs fix. Pitch keeps the ground height in HeightGrid.
The audited pitch creation, overbuild, load, render and placement compensation paths are adjusted.
General fire spread and pitch-chain comparisons preserve Vanilla's virtual height, including
the terminal restoration window; ordinary damage and disease use actual ground height.

### Contract and failure behavior

- **Atomic set:** all fourteen sites are one feature unit. One `HookTransaction` (`RollbackAndThrow`, `OwnsHooks`) and one exclusive `NativeSiteRegistry` claim (`PitchStairsVariantB`) cover them. A partial installation never becomes active.
- **Validated before commit:**
  - installed hash equals the reference hash;
  - `NativePatternResolver` finds each site at its reference RVA;
  - image and live bytes match the site bytes;
  - the following bytes are unchanged;
  - instruction lengths match;
  - the `X64InlineHook` probe reports `DisplacedByteCount` equal to the span;
  - every stub assembles and decodes.
- **Validated after commit:** each site holds `FF 25 00000000 <stub>` followed by NOP padding.
- **Activation:** installation validates the complete set and early Extender manager crosscheck but leaves the native enabled word zero. The first `BeforeLoad` snapshots `ready && EnableMod && EnablePitchStairsFix` after save/MP settings restoration. Four permanent managed loader wrappers (`loadMap`, `LoadSaveFile`, `LoadMapFile`, `newMapEditor`) publish it inside one continuous validated `EngineInterface.threadLock` scope through the original load. This closes the queued-old-tick gap and supports either APIShared/BFQoL hook order. Duplicate/nested notifications cannot resnapshot. End discards an unpublished snapshot; a published rule stays unchanged after mission end, failed/replaced loads, settings callbacks or diagnostics. Only another session's actual load can apply its new snapshot. Installation failure leaves Vanilla selected. No mid-world disable or executable repatching is permitted.
- **No fallback for another DLL:** there is deliberately no pattern fallback. The tile-manager offsets, the table offset and the stub spans are proven only for this hash. Another hash leaves the mod inactive and logs `PITCH_STAIRS_UNAVAILABLE`.
- **Lifetime:** hooks are never restored after publication. The plugin component is destroyed by the startup cleanup; the runtime stays rooted through static fields.
- **Second evidence for the data layout**, decoded at install time:
  - tile manager `0x405EDB0` from the `lea rcx` at `0x71956` in `0x71670`;
  - Structure `+0xB0BCA0` from `0x62F04`;
  - DefaultHeight `+0xDCCAC0` from `0x62F0F`;
  - MiscDisplay-to-Structure delta `-0x3FA2A0` from `0x804FA`;
  - Height `+0xD7E5A0`, Logic `+0x898400`, MiscDisplay `0x4F64CF0` (image RVA), the table offsets `0x7080`/`0x7090` and the call target `0x6B910` from the displaced blocks themselves.
- **Early cross-check:** before claims, allocation or hook publication, the Extender's `GameGlobalsManager.GameTileManagerVA` must equal image + `0x405EDB0`. A mismatch refuses initialization. Mission start never changes an already published map's height rule.
- **No instruction inside any span is a branch target** (full `.text` scan). Boundary targets are allowed: `0x6CCFC`, `0x80541` and `0x68EC0` are continuations.

### Sites

| Site | RVA / span / continuation | Original block | Enabled behavior | Flags after the skipped instruction |
|---|---|---|---|---|
| pitch-lowering (in `0x59380`) | `0x594C1` / 18 / `0x594D3` | `lea eax,[rdx-4]`; `movzx ecx,al`; `cmovbe ecx,r9d`; `mov [r8+rsi+Height],cl` | Store skipped; height stays as it was. The first three instructions run before the guard, so `cmovbe` consumes Vanilla's `cmp dl,4`. | `mov` sets no flags |
| aiv-pitch-restore (in `0x6CB20`) | `0x6CCED` / 15 / `0x6CCFC` | `add byte [rdx+rdi+Height],4`; `mov eax,[rdi+rdx*4+Logic]` | `add` skipped. The height was rebuilt just before from DefaultHeight plus the mapper offset. | next consumer is `bt eax,0xC` |
| manual-pitch-restore (in `0x6F230`) | `0x6F6AD` / 18 / `0x6F6BF` | `test al,8`; `je 0x6F6C1`; `and eax,~8`; `mov [rbx],eax`; `add byte [r12+r13+Height],4` | `add` skipped for stairs and walls; the ground under pitch is never lowered. `je` is re-encoded with its absolute target. | next consumer is `bt eax,0xC` |
| load-pitch-normalization (in `0x804C0`, called by `0x7D510` in `0x71670`) | `0x80530` / 17 / `0x80541` | `movzx eax,[r9+Default-Height]`; `cmp al,4`; `jb 0x80541`; `sub al,4`; `mov [r9],al` | Default ≥ 4: Height = Default (Vanilla: Default−4). Default 1..3: a pitch tile at height 0 without wall or structure is raised to Default; otherwise unchanged. The loop return value (eax) is unused by `0x7D510`. | next consumer is `add rdx,2` |
| render-pitch-height (in renderer `0x41D60`) | `0x43357` / 17 / `0x43368` | `movzx eax,[rbx+r9+Height]`; `mov eax,[r9+rax*4+0x7090]` | Reads the ordinary table entry `+0x7080` instead of the +4 compensation `+0x7090`. | identical (`mov`) |
| placement-pitch-height (in the simulation tile pass `0x65830`, block from `0x676F0`) | `0x68EAD` / 19 / `0x68EC0` | `mov eax,0x2000`; `test word [rbp+r13*2+0x4F64CF0],ax`; `jne 0x68EC0`; `add esi,4` | `add esi,4` (pitch compensation before `cmp esi,0x88`) skipped. | next consumer is `shr edi,3` |
| fire-source-height (in `0x99420`) | `0x997B4` / 19 / `0x997C7` | `movzx edi,[rcx+r15+Height]`; `cmp ax,4`; `jge 0x99952` | A valid, non-restored PitchTrap source uses max(0,h−4); a terminal restored source keeps h. | entry flags dead; `cmp ax,4` recomputes them |
| fire-neighbor-west / -east / -row-a / -row-b (in `0x99420`) | `0x997DB`, `0x99824` / 15 / +15 | `lea edx,[rcx∓1]`; `mov rcx,r15`; `call 0x6B910`; `sub edi,eax`; `mov eax,edi` | A valid, non-restored PitchTrap neighbor without structure uses max(0,h−4). A restored neighbor keeps the original query result. | `sub edi,eax` recomputes them |
| (same, row variants) | `0x9987C`, `0x998D1` / 15 / +15 | `add edx,[rbx+rcx*4+0x402FF38/20]`; `mov rcx,r15`; `call 0x6B910` | as above | same |

Fire-neighbor stub contract:

- The adjust decision runs before the call; the stub then takes one of two call paths. Neither touches the stack, so the callee runs with Vanilla's `rsp` and home area.
- The plain path (disabled, out of bounds, restored marker, no pitch flag, or a structure on the neighbor) is Vanilla's block, including the flags the callee leaves behind. The row variants end with the call itself.
- `rax`, `rcx` and the entry flags are dead at these calls (not arguments; clobbered by the callee).
- `0x6B910` itself is not patched: it has more than 40 callers. The current BFQoL source contains a contract entry but no installed detour; the original call target is preserved.

DEVDIAG builds only: both fire stubs also count their executions (`fireSourceCalls`, `fireNeighborCalls`), and the source stub counts pitch tiles and those with the visibility bit `0x2000` (`fireSourcePitch`, `fireSourcePitchVisible`). The bit is only observed and never changes the decision. The increments sit where the flags are dead. Release stubs contain none of them; StubTests checks both variants.

### Fire spread: why it is adjusted, and the limits of the adjustment

The user decided to keep Vanilla's fire behavior. Vanilla initially stores pitch 4 lower and its spread code reads that raw height. Both stubs therefore subtract 4 for non-restored PitchTrap tiles; the terminal restore window keeps Vanilla's restored height. Overbuild clears the flag: AIV at `0x6CCE3`, manual at `0x6F6B1`.

**Do not test MiscDisplay `0x2000` here.** Measured and audited on 02.11.2026 ([docs/PITCHSTAIRS.md](docs/PITCHSTAIRS.md#dated-evidence-02112026-eleven-site-builds), baseline section "MiscDisplay 0x2000 on pitch"):

- On pitch, `0x2000` means "visible to the local player". The simulation tile pass `0x65830` clears it at `0x682D9` and sets it again at `0x683D8` for burning pitch and for pitch of the local player's team.
- The bit therefore differs per client. Vanilla uses it only to draw visible pitch as a ditch: the renderer at `0x43347` and the cap at `0x68EB2` skip the +4 compensation for it.
- Excluding it would leave all own, allied and burning pitch unadjusted, and in multiplayer it would make fire spread depend on the local player.

The terminal restore window is tracked by a native byte map. A restored pitch source or neighbor
keeps its raw height; only a non-restored pitch field uses max(0,h-4). The pitch-to-pitch comparison
also saturates both local operands, so low defaults do not change its ignition decision.

### Terminal markers and the three additional sites

State bytes 0..95 keep their existing meanings; byte[320800] starts at +96, total 320896 bytes.
Marker 1 means the exact native burn-out writer restored the field. Each new pitch creation clears
its marker. Removal needs no hook because a marker without PitchTrap has no effect.

| Site | RVA / span / continuation | Original block and register contract | Enabled behavior |
|---|---|---|---|
| load-marker-bootstrap | `0x804C0` / 18 / `0x804D2` | `mov [rsp+8],rbx`; `lea r9,HeightGrid`; `mov r11d,320800`, lengths 5/7/6. Store at original RSP; emit the decoded LEA target as MOV R9,absolute. Save scratch GPRs and flags. | Clear 40100 qwords; scan occupied native records once to reconstruct low terminal markers. No managed call, SIMD or direction-flag change. |
| burn-out-height-restore | `0x7D497` / 17 / `0x7D4A8` | Load DefaultHeight into EAX and store AL in HeightGrid, lengths 9/8. The following state=-1 write remains Vanilla. | Replay originals; mark the valid ECX tile as restored, preserving registers/flags. |
| pitch-chain-height | `0x7D411` / 14 / `0x7D41F` | `movzx ecx,Height[neighbor]`; `mov eax,r12d`; `sub eax,ecx`, lengths 9/3/2. Continuation is CDQ. | Saturate EAX and ECX by subtracting 4, then original SUB. R12 stays unchanged; ECX is dead after SUB and SUB supplies the comparison flags. |

Bootstrap records: `manager +0x2038E44 + id*20`; PackedTileId int32 +0, occupied OwnerId
int16 +8, signed State +0x10. Iterate IDs 1..<min(signed PitchIdLimit,4000), with no scan for
limits <=1. PitchIdLimit is manager +0x204C6C4. Mark only state -1, valid tile and Default<4.
Default>=4 is relowered by Vanilla on every load, including state -1, so its marker remains zero.
Default<4 terminal height is retained by Vanilla, hence its marker must be reconstructed.
The ordinary tile normalization does not overwrite these markers. PackedTileId precedes the
Extender descriptor's GlobalId; its Reserved/IsAlive members are not used as native record contracts.

Every new map access uses an unsigned index bound. Out-of-grid neighbors take the untouched
native query path: native manager padding does not imply padding in the new marker allocation.
Known raw record layout remains hash-bound. Independent decoded anchors in 0x695C0/0x65830
validate the limit, record base, occupancy and terminal-state displacements before installation.

All three spans have no direct interior branch/call targets in the installed .text scan or current
baseline xrefs. At update time repeat that scan, foreign-owner checks and actual RedBird span probes.
The current source tests additionally execute complete original native load, pitch chain, terminal
restore, cleanup and replacement paths with the production generators in a private PE image.

DEVDIAG expectation: terminal pitch remains included in fireSourcePitch, but does not increment
fireSourceAdjusted. Equality of those counters is therefore not an invariant after this correction.

### Not adjusted (accepted or open)

- **Unit stand height and speed** (`0x19B260`, `0x184FD0`, `0x182B00`): the artificial height step disappears. Accepted by the user; still needs checking in game.
- **Fire effect height** (`0x7D44B`, `0x9C430` uses the raw height): needs a visual check.
- **Ranged targeting, projectile hits and disease:** intentionally use actual B ground height. Private native fixtures compare B pitch with ordinary ground at projectile hit-band, ranged correction +/-70 and strict disease height 120 boundaries. Ranged sight/target predicates and presentation sinks are isolated; live LOS, APIShared disease-rule interaction and gameplay remain acceptance checks.
- **Visible pitch is drawn at ground level** instead of as a ditch 4 lower; hidden enemy pitch looks as in Vanilla. This matches unit and fire-effect heights, which also use the raw height. The user checked this in game on 02.11.2026 and accepted it.
- **Editor:** loading a map into the editor goes through `DLL_LoadSaveGame` (`bLoadingEditorMap=1`) and therefore reaches `0x804C0`; this was observed on 02.11.2026. Only a new editor map (`0x85E90` → `0x5BC70`/`0x87D10`) bypasses it, and such a map has no pitch yet. No managed editor normalization is needed. Other paths outside `0x71670` are still reported by the DEVDIAG scan `PITCH_STAIRS_SCAN`.
- **Stairs already raised by +4 in old saves** carry no pitch flag any more and are not repaired.

### Re-audit on a new DLL

- Repeat the full `.text` height-writer and compensation scans of the baseline (sections "Complete pitch-drop writer and compensation inventory", "Pointer-walking grid loops" and "Further height consumers and the pitch-bit scan"): all −4 stores, +4 restores, `PitchTrap && !0x2000` compensations, `0x804C0`-like loops and functions that access Height and test bit 0x8.
- Then re-check every site's control flow, register and flag liveness, incoming edges, the 0x6B910 callers and foreign hooks: Extender detours, BFQoL/ExtraFeatures/APIShared sites and the Fixes clone.
- Then rerun the migrated `tests/PitchStairs.Tests` suite (`BugfixesAndQoL.PitchStairs.Tests.exe`), including the activation/lifecycle cases. Historical testmod executable identity: `PitchStairsTest.StubTests.exe`.

### Backend block guard

The generator compares the instructions RedBird displaces with the audited decode of each site: Code, length, displacement, immediate and branch target. A mismatch throws during `Commit` and rolls the transaction back. Both are decodes at the real site address; a backend that decoded elsewhere would therefore fail closed.


The complete original testmod documentation was migrated to [docs/PITCHSTAIRS.md](docs/PITCHSTAIRS.md); its dated eleven-site runs and fourteen-site offline result retain their original build/log identities. They do not certify the integrated fourteen-site implementation. There is no new MP start broker: that separately deferred work is preserved under `.work/a/pitch-fix/001/deferred-mp`.

## AI Outpost retarget after defeat

Owner: `AiOutpostRetargetFix`. `EnableAiOutpostRetargetFix` is a default-on synchronized host setting and is combined with the mod master switch. Native installation is gated by the actual shared DLL hash check against `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. See [the Vanilla audit](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/OUTPOST_ATTACK_RETARGET.md).

- Exclusive inline site `0x30675..0x3068B` follows the busy check and precedes the signed target load. Validate bytes, 8+5+9 instruction span and continuation. The only incoming branch to the block is `0x3065A` to its start. The reference resolver can scan for the exact block if the reference bytes differ, but publication still requires the audited RVA and all the hash-bound data and entry contracts. A hook match alone cannot establish the AI-manager and field layout on another hash.
- Win64 selector `0x2A720` takes AI manager `0x404C950`, owner and tribe ID, and writes the target in place. Lord validity helper `0x187E60` takes unit manager `0x67E8400` and player ID. Validate their live prologues and register them as observed spans to reject overlapping writers. Register the complete inline block before patching. A failed initialization leaves this feature inactive.
- Tribe manager `0x7CC6720 + 0x2A + tribeId*0x688` yields the Extender record. Target player is signed Int16 at `+0x620`, cached building is Int16 at `+0x622`, and cached global ID is UInt32 at `+0x626`. WinLoss is a UInt32 enum at `0x379D014+playerId*0x583C`. Native Lord lookup tables are `0x3A0F9F0+playerId*2` and `0x3A0FA04+playerId*4`. Unit slot checks use `0x67E8400+unitId*0x490` with alive word `+0x6E4`, identity dword `+0x6F0` and death word `+0x8F8`. Reconfirm all native anchors and the typed Extender offsets on updates.
- The stub checks demand, slot bounds and the current target directly in native code. Busy, disabled and living-target cases preserve Vanilla. A live Lord in NeedsInit is given the original update while it initializes. For an invalidated target the callback rechecks native Lord validity unless loss is official, then runs the original selector and validates its result before clearing the cached building pair. The displaced instructions read the resulting target once and resume Vanilla. RSP is aligned at the site. The stub restores the original flags, volatile GPRs and XMM0–5 around the slow callback, with the Win64 shadow space reserved.
- The transaction, delegate and demand byte are rooted by the process-level BugfixesAndQoL runtime. Settings updates change the demand byte only. The same native update runs on each peer, and no independent tick publisher, queue or save data is required. `AiOutpostRetarget.Tests` executes the production emitter with the installed RedBird backend and a native callback stand-in. Gameplay and two-peer acceptance separately require observing the order Vanilla issues after the target changes.

### Temporary Center of Power trace [DEVDIAG]

The observation span `0x30651..0x3065F` follows the call at `0x3064C` to the native busy check `0x2DC30`. On the audited Outpost DLL hash it contains RIP-relative LEA (7), TEST EAX (2), JE (2), and XOR R15D (3). Register R13 carries the AI manager, ESI the one-based tribe ID, and EAX the unmodified busy result. The relocated TEST determines the original branch to `0x30675` when ready. The busy path resumes at `0x3065F`. No native branch or xref enters the span interior. The diagnostic owns only this span. The production retarget hook remains at its independently validated site.

The trace opens on Center of Power and remembers the observed native wave members by slot and global ID. A Lord kill event or native Lord loss opens a 1,600-map-tick observation window for the wave. Successful retarget also starts the window when the loss marker was unavailable. The Extender command event is recorded as a request, since intervening subscribers can change its arguments. The per-member snapshots record actual command, state, path fields and position, and distinguish detached, dead, reused and unreadable members. The native exclusion word at GameUnit `+0x428` is sampled as raw state. `0x11DD10` writes it and `0x11B520` checks it. A snapshot of the word alone cannot prove which branch set it.

Look for `OUTPOST_TRACE_READY`, `START`, `WAVE`, `LORD_KILL` or `LOSS`, `GATE`, `RETARGET`, `ORDER_REQUEST`, `UNIT`, `SAMPLE`, and `DONE`. `ERROR`, `LIMIT` or `TICK_FALLBACK` qualify the evidence. Match the last Lord and melee attacker identities to the same wave, then compare member tiles and path state across samples. A second loss after the first window can still log the target change but does not reopen that wave's member trace. A prepared build or successful hook install is separate from gameplay evidence.
