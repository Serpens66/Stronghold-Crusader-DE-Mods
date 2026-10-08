# Script Extender 2.14.1 engage-range audit

Review date: 2026-10-08. Previous baseline commit: 5049b5d051d0face848cc2eb098db1fedf5451a7 (2.14.0). Reviewed target: 5908e1f12437deb7ef5f5b1dc2d64e301178fba3, tree c238441ea12db35ee36c5c5668d3c8fce0897295 (v2.14.1). Installed/local build/package SHCDESE.dll: SHA-256 E5D78FDF2EA9701336C398F7D29410F206D141FC01AA8099F30C824E6AB8A0B3, assembly 2.14.1.0. Native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2; managed Assembly-CSharp SHA-256 BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789. These are provenance-bound facts, not permanent version expectations.

The complete release diff changes only BulkUnitDetours.cs and CHANGELOG.md. No public API or interop layout changes in this release. Unit-vtable role names are candidate associations; native bytes, offsets, decoded branch targets and memory relationships below were verified. Evidence is retained in ../../../../ScriptExtender2141Audit (native-audit.json, native-sites.json, emitter-provenance.json, installed-layout.json, stubs.json, stub-verification.json and their reproduction scripts).

## Complete feature path and identity contract

Dispatcher 0x182B00 selects unit updates through vtable 0x321CB0. The active unit context is a 1-based game ID; iteration begins at 1 and advances by record stride 0x490 relative to the manager sentinel. Installed GameUnitManager.GameUnitArray starts at +0x65C (sentinel origin); API views account for that sentinel, yielding 0-based first-real-unit spans. Do not pass a span index as CurrentContextUnitId or add/subtract one inside this generator. Installed public fields: GameUnit.r_AliveState +0x88 AliveState, r_UnitChimp +0x8A eChimps, r_ControllableForPlayerId +0x92 UInt16, r_GlobalId +0x94 UInt32, r_IsKilledByProjectile +0x29C UInt16, r_WorldDistanceToNearestEnemy +0x2A2 UInt16. Reflection verification is retained in installed-layout.json.

Distance producer 0x18C040 handles wild/owner-zero selection with Chebyshev distance, initializes the distance to 10000 and validates global identity. Producer 0x18C160 traverses enemy candidates, calculates the integer approximation max + (((min * 2) / 5) * min) / max, clamps to 32000 and performs attack/path side effects. Refresh 0x19A1F0 is also included in the producer audit. The native WORD manager+0x8FE is GameUnit+0x2A2 after the sentinel/stride mapping. Native comparisons use signed 16-bit arithmetic despite the public UInt16 storage view. Keep arithmetic signedness and native identity separate from managed field naming.

Downstream 0x18E9A0 retains native identity, visibility, unit type, attack and height checks, followed by native unit-state transitions. Changing this range comparison does not replace target validity, attack-state or retreat/path decisions. The override is global per unit type; no local selected-unit or player scope is implied.

## Coverage and actual displacement

The installed RedBird 1.5 X64InlineHook backend rounds Math.Max(minHookSize, 14) to complete instructions. The 35 actual displacement lengths are 15..20 bytes. Source-selected update functions were decoded over their complete reachable boundaries with the same non-call control-flow traversal used by the backend, and matched the semantic database extents. Forward branch destinations are included; call targets are not mistaken for part of the function. Function-boundary limits in 2.14.1 are MaxReadLength 65536 and MaxInstructions 16384. All native signature matches and displacements were enumerated, including source-selected functions with zero matching comparisons.

| Candidate update role | Function RVA | Range comparisons |
| --- | --- | --- |
| ArcherUpdate | 0x13F540 | 5 |
| ArabBallistaUpdate | 0x171C50 | 2 |
| ArabBowUpdate | 0x167D40 | 5 |
| BallistaUpdate | 0x161CB0 | 1 |
| TrebuchetUpdate | 0x1535F0 | 0 |
| CatapultUpdate | 0x1520D0 | 0 |
| MangonelUpdate | 0x1547F0 | 0 |
| ArabSlingerUpdate | 0x16B220 | 4 |
| BedouinHeavyCamelUpdate | 0x17A590 | 2 |
| BedouinSkirmisherUpdate | 0x178350 | 6 |
| XbowmanUpdate | 0x141DA0 | 3 |
| ArabGrenadierUpdate | 0x170570 | 4 |
| ArabHorsemanUpdate | 0x16E440 | 3 |

Each displaced instruction and direct jump destination is shown below; continuation is the first instruction after the overwritten span. Branch operands are original native virtual addresses (image base 0x180000000). No external direct entry or semantic xref lands inside an overwritten span. Original branch relocation is retained by Iced; the final indirect jump targets the exact copied-buffer continuation during offline testing.

| Hook RVA | Bytes | Displaced instructions / branch targets | Continuation |
| --- | --- | --- | --- |
| 0x13FAA1 | 17 | `cmp word ptr [r9 + rdi + 0x8fe], r8w; jg 0x18013fab4; cmp ecx, 0x186a0` | 0x13FAB2: `jne 0x18013fac7` |
| 0x13FAD1 | 16 | `cmp word ptr [rcx + rdi + 0x8fe], r8w; jle 0x18013faee; mov eax, 0xb` | 0x13FAE1: `mov word ptr [rcx + rdi + 0x918], ax` |
| 0x13FBCF | 16 | `cmp word ptr [rdi + r15 + 0x8fe], ax; jg 0x18013fc18; mov edx, ebx; mov rcx, r15` | 0x13FBDF: `call 0x18018e9a0` |
| 0x14135C | 19 | `cmp word ptr [rdi + r10 + 0x8fe], r8w; jl 0x1801413a0; cmp byte ptr [rdi + r10 + 0xa50], sil` | 0x14136F: `jne 0x180141391` |
| 0x141527 | 15 | `cmp word ptr [rdi + r12 + 0x8fe], r8w; jle 0x180140837` | 0x141536: `movsx rax, word ptr [r13]` |
| 0x171DBD | 16 | `cmp word ptr [rbx + r14 + 0x8fe], ax; jg 0x180171e0d; mov edx, edi; mov rcx, r14` | 0x171DCD: `call 0x18018e9a0` |
| 0x171EDD | 18 | `cmp word ptr [rbx + rsi + 0x8fe], ax; jle 0x180171f0c; movzx eax, word ptr [rbx + rsi + 0x9f4]` | 0x171EEF: `mov edx, 0xfffb` |
| 0x167F93 | 17 | `cmp word ptr [r8 + r11 + 0x8fe], r9w; jg 0x180167fa6; cmp ecx, 0x186a0` | 0x167FA4: `jne 0x180167fb9` |
| 0x167FC3 | 16 | `cmp word ptr [rcx + r11 + 0x8fe], r9w; jle 0x180167fe1; mov eax, 0xb` | 0x167FD3: `mov word ptr [rcx + r11 + 0x918], ax` |
| 0x1680D4 | 16 | `cmp word ptr [rdi + r12 + 0x8fe], ax; jg 0x18016811d; mov edx, ebx; mov rcx, r12` | 0x1680E4: `call 0x18018e9a0` |
| 0x1692A7 | 19 | `cmp word ptr [rdi + r11 + 0x8fe], r9w; jl 0x1801692e7; cmp byte ptr [rdi + r11 + 0xa50], sil` | 0x1692BA: `jne 0x1801692de` |
| 0x1693D6 | 15 | `cmp word ptr [rdi + r11 + 0x8fe], r9w; jle 0x180169662` | 0x1693E5: `movsx rax, word ptr [r12]` |
| 0x161E3D | 18 | `cmp word ptr [rsi + rbx + 0x8fe], ax; jle 0x180161e75; movzx eax, word ptr [rsi + rbx + 0x9f4]` | 0x161E4F: `mov edx, 0xfffb` |
| 0x16B3DE | 16 | `cmp word ptr [r12 + rcx + 0x8fe], r11w; jle 0x18016b3fc; mov eax, 0xb` | 0x16B3EE: `mov word ptr [rcx + r12 + 0x918], ax` |
| 0x16B4DF | 16 | `cmp word ptr [r12 + rdi + 0x8fe], ax; jg 0x18016b522; mov edx, ebx; mov rcx, r12` | 0x16B4EF: `call 0x18018e9a0` |
| 0x16C51B | 19 | `cmp word ptr [r12 + rdi + 0x8fe], r11w; jl 0x18016c55d; cmp byte ptr [r12 + rdi + 0xa50], sil` | 0x16C52E: `jne 0x18016c554` |
| 0x16C69C | 15 | `cmp word ptr [r12 + rdi + 0x8fe], r11w; jle 0x18016c85a` | 0x16C6AB: `movsx rax, word ptr [r12 + rdi + 0x9f4]` |
| 0x17A6F3 | 16 | `cmp word ptr [rcx + r13 + 0x8fe], ax; jge 0x18017a754; mov edx, edi; mov rcx, r13` | 0x17A703: `call 0x18018e9a0` |
| 0x17A801 | 16 | `cmp word ptr [rbx + r13 + 0x8fe], ax; jg 0x18017a854; mov edx, edi; mov rcx, r13` | 0x17A811: `call 0x18018e9a0` |
| 0x178856 | 17 | `cmp word ptr [r8 + r11 + 0x8fe], r9w; jg 0x180178869; cmp ecx, 0x186a0` | 0x178867: `jne 0x18017887c` |
| 0x178886 | 16 | `cmp word ptr [rcx + r11 + 0x8fe], r9w; jle 0x1801788a4; mov eax, 0xb` | 0x178896: `mov word ptr [rcx + r11 + 0x918], ax` |
| 0x1789B9 | 15 | `cmp word ptr [rdi + rbp + 0x8fe], ax; jg 0x180178a01; mov edx, ebx; mov rcx, rbp` | 0x1789C8: `call 0x18018e9a0` |
| 0x178C0E | 15 | `cmp word ptr [rdi + rbp + 0x8fe], ax; jge 0x180178c5c; mov edx, ebx; mov rcx, rbp` | 0x178C1D: `call 0x18018e9a0` |
| 0x179EA2 | 19 | `cmp word ptr [rdi + r11 + 0x8fe], r9w; jl 0x180179ed0; cmp byte ptr [rdi + r11 + 0xa50], sil` | 0x179EB5: `jne 0x180179ec7` |
| 0x179F50 | 15 | `cmp word ptr [rdi + r14 + 0x8fe], r9w; jle 0x180179688` | 0x179F5F: `movsx rax, word ptr [rdx]` |
| 0x141F47 | 16 | `cmp word ptr [rcx + r13 + 0x8fe], ax; jle 0x180141f65; mov edx, 1` | 0x141F57: `mov word ptr [rcx + r13 + 0x918], dx` |
| 0x14203F | 16 | `cmp word ptr [rdi + r13 + 0x8fe], ax; jg 0x180142089; mov edx, ebx; mov rcx, r13` | 0x14204F: `call 0x18018e9a0` |
| 0x142E5A | 18 | `cmp word ptr [rdi + r13 + 0x8fe], ax; jge 0x180142e76; lea rax, [rip + 0x35293a4]` | 0x142E6C: `cmp dword ptr [r12 + rax + 0x130d08], esi` |
| 0x170733 | 16 | `cmp word ptr [rcx + r12 + 0x8fe], dx; jle 0x180170751; mov eax, 0xb` | 0x170743: `mov word ptr [rcx + r12 + 0x918], ax` |
| 0x170834 | 16 | `cmp word ptr [rdi + r12 + 0x8fe], ax; jg 0x180170877; mov edx, ebx; mov rcx, r12` | 0x170844: `call 0x18018e9a0` |
| 0x171679 | 19 | `cmp word ptr [rdi + r12 + 0x8fe], dx; jl 0x1801716ac; cmp byte ptr [rdi + r12 + 0xa50], sil` | 0x17168C: `jne 0x1801716a3` |
| 0x171714 | 20 | `cmp word ptr [rdi + r12 + 0x8fe], dx; jle 0x180171734; movsx rax, word ptr [rdi + r12 + 0x9f4]` | 0x171728: `cmp ax, 0x22` |
| 0x16E62C | 16 | `cmp word ptr [rcx + r10 + 0x8fe], ax; jge 0x18016e698; mov edx, edi; mov rcx, r10` | 0x16E63C: `call 0x18018e9a0` |
| 0x16E73F | 15 | `cmp word ptr [rbx + rbp + 0x8fe], ax; jg 0x18016e78f; mov edx, edi; mov rcx, rbp` | 0x16E74E: `call 0x18018e9a0` |
| 0x16E97A | 15 | `cmp word ptr [rbx + rbp + 0x8fe], ax; jge 0x18016e9bf; mov edx, edi; mov rcx, rbp` | 0x16E989: `call 0x18018e9a0` |

## Generator, registers, flags and publication

The audit extracts the exact upstream emitter lambda, records its SHA-256 and substitutes only two global-address constants for isolated scratch storage. It uses installed Iced and RedBird 1.5 to generate an unpublished X64InlineHook on a copied native byte buffer. Hook Enable is never called. DisplacedByteCount and ReturnAddress must match the native audit, reassembly must match the actual allocated stub, every executable byte must decode, and the trailing indirect-jump pointer must equal target + actual displacement. The last eight pointer bytes are data, not executable instructions. Native full-span bytes are preserved in native-sites.json.

The original compare runs first. A spare GPR excludes the compare operand, memory base and index. Its old value is pushed; the original operand is captured before r13/r14/r15 are repurposed. The scratch registers and flags are saved. CurrentContextUnitId is loaded through r13d before computing the unit record/type; native flag lookup preserves its own temporary. Default-cache initialization records the original operand, including when it occupied r13w/r14w/r15w. Cached defaults are not overwritten.

Vanilla branch restores saved flags and every scratch register. Override branch sign-extends the chosen Int16 threshold into the spare register, drops the saved flag slot with LEA, restores r15/r14/r13 before the replacement compare, compares against the spare WORD, then pops the spare register without changing flags. Thus the following original conditional branch consumes flags from the intended comparison. Stack restoration is identical on both paths. No ContextAssemblyGenerator callback wrapper is used here; its SUB/ADD flag caveat remains applicable to other context hooks. This emitter contains no SIMD operations or managed callback; GPR checks do not assert unrelated wrapper SIMD behavior.

The bounded actual-byte interpreter exercises 35,280 cases: all 35 sites, signed distance/override boundaries, three original thresholds, active/inactive overrides, cold/warm default caches and two incoming flag states. It verifies all sixteen GPRs, balanced stack, six arithmetic compare flags, original-value caching and allowed memory writes at the first restored native conditional branch. It fails closed on unsupported instructions. Full stub decode and continuation-pointer checks supplement this bounded prefix execution. This is an offline machine-contract test, not gameplay execution. Hooks are process-lifetime owned by the Extender; no release mod changes or new hook publication occur in this update.

## Consumers and compatibility

Release native ownership is recorded in Shared/ScriptExtenderUpdate/2.14.0-2.14.1.release-hooks.json (dynamically fingerprinted inventory). No release mod uses engage-range overrides or owns these overwritten spans. Across release sources, relevant test sources and Fixes, partial AOB fragments near two heavy-camel sites belong to ImprovedHunters queries; their complete concatenated signatures resolve inside hunter 0x12FC70..0x131422 and establish no overlap.

Clean canonical Fixes v1.26.3 commit cbabdecac15a6e81cb45404f1a3501a4856dae39, tree 185f84b1fa6fc2faad793f2980a18d38599290e0 is reviewed separately in FIXES_COMPATIBILITY.md. Its ladder hooks 0x111C00 and 0x6A5D0 do not overlap engage sites. EnemyBridgePathTest has an existing competing 0x111C00 owner and remains excluded; this update makes no compatibility claim for that test.

Existing capture-event and PCL route integrations are retained. The additive 2.13/2.14 APIs provide no demonstrated benefit requiring a migration here. AffectedMods and BuildMods are empty; all mod versions, minimum versions, metadata and changelogs remain unchanged.

## Acceptance limits

Gameplay acceptance remains with the user: start beyond startup cleanup, ranged target search, attack and retreat. Review only the new BepInEx start section and newly timestamped crash artifacts afterward. The known early crash-handler null-read/minidump false-alarm note remains: no source change in this release establishes its correction, and no controlled startup evidence has yet been supplied. Offline checks do not substitute for that acceptance.
