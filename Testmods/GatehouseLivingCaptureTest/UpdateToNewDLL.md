# Gatehouse living capture: native contract

Reference DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Feature audit: `../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md`.

## Targets and resolution

- Owner: this standalone testmod. Common gatehouse handler `B73D0..B7CE5`, pristine full hash `F73E9FF6F69D9EC1ECD59D528BC6D4861739F54E0A9C59C6E6BAD91369FA57C8`.
- Hook `B7540..B7552`, 18 bytes: `CDQE` (2), `IMUL RDI,RAX,490h` (7), `CMP word [RDI+R13+67E8CFCh],R14W` (9). Exact bytes and signature in `NativeDefinition.cs`.
- Continuation `B7552`: original `74 63` / `JE B75B7`. `B75B7` reads the next tile-chain unit ID; `B75C2` loops to `B7540`. The full-function decode and semantic xrefs have no incoming target inside the displaced block. No displaced RIP-relative operands, calls or branches.
- Resolution first checks reference RVA bytes, then a unique signature in executable PE sections, including after a reference-byte mismatch. Resolved RVA must still be `B7540`: fixed continuation and structure contracts have no independent update fallback. Unknown file hash, nonunique signature, changed layout, live bytes or backend span fail closed before publication, logging a timestamped Error and preserving Vanilla.
- Native eligibility displacement `67E8CFC` addresses an unnamed 16-bit unit field, not ownership. The helper independently reads `GameUnit.r_AliveState` at `88` and the low word of `N0000019A` at `29C`. Record size `490`, owner at `92`, health at `3C4`. IDs from EAX/RAX are signed, one-based game IDs; RDI must equal ID times `490`. Resolve the immediate unit view using APIShared.UnitAccess, never cache a slot pointer.

## Backend and state

Installed RedBird X64InlineHook decodes at least `max(requested,14)` bytes to complete instructions. Requested 18 must yield exactly 18. Its absolute-indirect patch uses `FF25 00000000`, an eight-byte destination, and four NOPs. Validate real hook target/span/patch before publishing; rollback only an unpublished failed candidate.

Installed X64SmartCPUContext is 136 bytes (17 eight-byte fields), not 144. Its storage is padded to 144 before the XMM spill area. Validate both the actual structure size and every field offset. The transaction owns its hooks so Dispose can fully roll back an unpublished failure; it remains permanently rooted and undisposed after publication.

Replay all originals once. Preserve every GPR, the incoming stack, and volatile XMM0..5; nonvolatile XMM6..15 use the Win64 ABI contract. Save original inequality in temporary R11b while keeping real R11 on the stack. The callback can clear eligibility only. After restoring the wrapper stack/registers, `TEST R11b,R11b; POP R11` reconstructs the ZF consumed by the original JE. Both outgoing paths overwrite other flags before using them. Do not use the installed ContextAssemblyGenerator flags snapshot to steer this branch.

Hook/delegate/runtime/logger remain process-rooted. Active state is data only; no repatching, plugin updates, coroutines, timer, map scans or normal teardown. Callback/lookup errors preserve original eligibility. Logging errors cannot change capture behavior. Tests compile the productive emitter, callback/runtime and UnitAccess against installed dependencies and execute the actual RedBird hook and both JE branches on copied bytes.

## Compatibility and update checklist

Script Extender capture-score and capture-event hooks start at `B757A` and `B790B`; Fixes' farmer hook starts at `B7C39`. APIShared timing/distance/automation hooks occupy separate later blocks. The new span ends before all of them. Keep a Fixes soft dependency and check live bytes before installation. Neither external project is modified.

On a native/SE/RedBird update repeat the complete capture/death/removal audit, ID and member-layout validation, all incoming-edge checks and actual-backend machine tests. Recheck helper semantics in ranged/melee targeting, not health alone. Run static JSON/lifecycle/permanent-hook/CRLF/XAML checks before build. Build/install using build.bat; the APIShared build must precede this mod. Versions remain in testing.

Gameplay acceptance (not proved by offline tests): large/small stone and wooden gates, last enemy dying with corpse still present, another living defender blocking, friendly corpse not capturing, team/tie rules, linked drawbridge behavior, save/load and matching real host/client installations with Fixes. Inspect `GATEHOUSE_LIVING_CAPTURE_READY`, `...CONFIRMED`, `...FIRST_EXCLUSION` and absence of `...CALLBACK_ERROR` in the latest launch section. READY alone is not a gameplay result.
