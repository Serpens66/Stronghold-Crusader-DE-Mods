# Native update contract: EnemyGatePathfindingTest

## Capturer comparison adapters

Reference DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

The PCL graph adapter owns RVA `0xE2705..0xE2718` inside `0xE2610`. The builder-precheck adapter owns RVA `0xE3024..0xE3037` inside `0xE2F60`. Both spans are exactly 20 bytes: `MOVSXD RCX,[R9-0xC]`, `IMUL RDX,RCX,0x32C`, and the capture-word `CMP`. The first comparison uses the module base in RAX and compares with zero; the second uses R13 and compares with AX. Query player is R14D / EBP respectively. No incoming branch enters either span's interior. Original continuations are `JE 0xE272C` at `0xE2719` and `JE 0xE3047` at `0xE3038`; owner/alliance acceptance bypasses the hooks through `0xE271B` / `0xE303A`.

Resolution validates the reference bytes and then the module-bounded unique patterns declared in `EnemyGatePathfindingNativeDefinition`. Both resolved sites must still match their audited RVAs because the layout is hash-bound. Failed resolution, displacement or assembly validation aborts the unpublished hook transaction. Published adapters remain installed until process exit; map changes publish only empty/active policy snapshots. BugfixesAndQoL owns the `0xE2610` function detour; these interior sites do not overlap its prolog. Installed Script Extender and local Fixes must be checked for collisions on every update.

The capture table is module RVA `0x64CCED2`, indexed by one-based building ID times `0x32C`, and read as `ushort`. R9 addresses the current connection record's enabled field at structure offset `0x18`. Record stride is `0x204`; building ID / subject global ID / owner are R9-relative `-0xC` / `-0x4` / `+0x1CC`. Component A/B/C are `+0x1C` / `+0x20` / `+0x1D0` (structure offsets `0x34` / `0x38` / `0x1E8`). The prior negative PCL offsets read the predecessor and must not be reused. Field offsets have no cross-build semantic fallback; unknown hashes disable this native feature.

RedBird.X64 `1.5.0.0` and installed Script Extender `2.12.0.0` were checked on 2026-10-01. RedBird's stock ContextAssemblyGenerator ends with `ADD RSP,144` after POPFQ; writing Rflags therefore cannot drive a subsequent JE. Our inline emitter performs the original comparison once, saves real R11, seeds R11b with SETNE, preserves all GPRs and volatile XMM0..5 across the IntPtr callback, then performs TEST R11b after callback cleanup followed by flag-neutral POP R11. Only ZF is live at the native successor; subsequent INC/ADD replace other arithmetic flags. No original SIMD value is used across these native blocks; SIMD preservation additionally protects the managed callback boundary.

The custom context uses X64SmartCPUContext field offsets `0..128` in eight-byte increments and a 144-byte context area. Machine tests verify installed metadata, Win64 shadow space/alignment, actual RedBird displacement, both real branch outcomes, GPR/XMM preservation and stack balance. Re-run these tests on every RedBird update. Runtime callbacks and their function pointers are rooted by the static plugin runtime; no new Assembly-CSharp member access or APIShared interface is introduced.

Other native targets and existing policies remain declared in `EnemyGatePathfindingNativeDefinition` and documented in `PROJECT_FINDINGS.md`. This change introduces no additional native target.


## Building query-player contract audit (2026-10-02)

No new runtime address or hook is introduced. The complete static contract is
recorded in the semantic baseline knowledge/ENEMY_GATE_BUILDING_CONTEXT.md.
For the reference hash above, DA020's sixth argument is the selected opponent
in 30620 (call 30A33), but the signed leader control WORD in 11E960 commands
9/38 (call 11FF9A), or zero for command 36. The planner field is tribe-relative
0x620, not the named AttackTargetOwnerPlayerId field at 0x61C. The native
control read at unit-relative 0x92 spans two installed Interop BYTE fields.
A future implementation must preserve this width and distinguish Vanilla's
query player from the moving actor. Same-PCL and E2CA0 alternative/cache paths
must be audited too. Reference-specific machine checks and installed-layout
checks are retained in _inspect/EnemyGateBuildingContextAudit. Unknown builds
must not reuse these field/call-site claims as runtime adapters.
