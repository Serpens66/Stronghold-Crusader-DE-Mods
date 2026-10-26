# Move-stager data anchors [Scope: Vanilla + mod integration]

## Scope and provenance

This note records code anchors from which native data addresses and layout values can be derived without a fixed RVA. Addresses are RVAs for native SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

Evidence:
- complete Iced disassembly of the mapped installed DLL (2026-10-22, `Shared/Tests/Native.Tests/DisassemblyProbe.cs`);
- the semantic baseline function export of `0x195E30`;
- a whole-image count of `lea rcx,[rip+X]` arguments before direct calls (`call_args` scan).

Confidence is confirmed-static. Runtime acceptance of a derivation does not exist yet.

Purpose: the update fallback in [AGENTS/native-analysis.md](../../../../../AGENTS/native-analysis.md) (section „Prüfkosten“) needs a validated anchor plus a second independent evidence per value. The table lists both for every value.

## Cursor move stager `0x195E30`

The function also appears in [MOVE_COMMAND_RELEASE.md](MOVE_COMMAND_RELEASE.md). APIShared validates its entry as „cursor move-stager entry“, and BugfixesAndQoL owns a detour at it. Derivations must therefore read the unmodified load image (`CrusaderLibraryLoadContext.Memory`), not the live code.

Arguments: `rcx` = unit manager context, `edx` = tribe ID, `r8d` = tile X / command, `r9d` = tile Y, stack `[rsp+80h]` (arg 5) = patrol mode, `[rsp+88h]` (arg 6) = move type.

| Value | Anchor in 0x195E30 (offset, instruction) | Second evidence |
|---|---|---|
| Chore tribe ID `0x86C132C` | +0x44 `mov [rip],r14d` (r14 = movsxd edx) | `0x10AE0`+0x2C `lea rdx,[rip]` (first packed field) |
| Chore tile X / command `0x86C1330` | +0x52 `mov [rip],r8d` | `0x10AE0`+0x4B `lea rdx,[rip]` (second) |
| Chore tile Y `0x86C1334` | +0x4B `mov [rip],r15d` (r15 = movsxd r9d) | `0x10AE0`+0x6A `lea rdx,[rip]` (third) |
| Chore patrol mode `0x86C1338` | +0x59 `mov [rip],eax` (eax = arg 5) | `0x10AE0`+0x8A `lea rdx,[rip]` (fourth) |
| Chore move type `0x86C133C` | +0x5F `mov [rip],ebp` (ebp = arg 6) | `0x10AE0`+0xAA `lea rdx,[rip]` (fifth) |
| Tribe manager `0x7CC6720` | +0x6D `lea rcx,[rip]` before `call 0x11A1B0` | +0x1C5 and +0x236, same `lea` |
| Tribe record stride `0x688` | +0x1CC `imul rbp,r14,688h` | followed by `cmp [rcx+rbp+2Ch],eax` with rcx = tribe manager |
| Tribe player-ID offset `0x2C` | +0x1D3 `cmp [rcx+rbp+2Ch],eax`, eax = active player | the stride and tribe manager of the same compare |
| Unit manager `0x67E8400` | +0xC1 `lea rdx,[rip]` | +0x11D, +0x15D, +0x1A1, same `lea` |
| Unit record stride `0x490` | +0xBA `imul rsi,rcx,490h` | `0x18D460`+0xA7 and +0xBA `imul …,490h` |
| Path manager `0x60AD660` | +0x102 `lea rcx,[rip]` before `call 0xE2610` | +0x173 `lea rcx,[rip]` before `call 0xDB650` |
| Active player ID `0x88E3D70` | +0x109 `mov edx,[rip]` | +0x1BF `mov eax,[rip]`; also `0x18D460`+0x6C, `0xE0E6` |
| Sound manager `0x88E72D0` | +0x202 `lea rcx,[rip]` before `call 0xE0E0` (speech busy) | whole image: 10 of 11 direct calls of `0xE0E0` pass this `lea rcx` argument |
| Dialogue manager `0x64C4490` | +0x2A5 `lea rcx,[rip]` before `call 0x3BC0` (speech) | whole image: all 13 direct calls of `0x3BC0` pass this argument |

Direct call targets inside the stager:
- `0x11A1B0` selected voice, `0x18D460` representative, `0xE0E0` speech busy and `0x3BC0` speech;
- also `0xE2610` PCL reachability and `0xDB650` direct tile search.

These are a second evidence for the separately pattern-resolved FormationSpeech and gate-routing entries.

## Other anchors

- **Chore mode `0x85F8FEC`:** read by `mov eax,[rip]` at the start of all three Chore handlers: `0x10AE0`+0x06, `0x12BF0`+0x06 and `0x176C0`+0x04.
- **Current-unit scratch `0x9302C4`:** written by `mov [rip],edx` at `0x18D460`+0x75 and +0xFE.
- **Chore handler table `0x2C7A30`** (populated at runtime): entries 17, 36 and 71 hold the move, target-order and waypoint-append handlers. Three entries matching three pattern-resolved handlers would locate the table without its RVA.

## Without an anchor yet

- **`TribeUnitCountOffset 0x5C`** (Formation core): no anchor was confirmed. The stager reads `[tribe+0x5A]`, a different field.
- **`MovementTargetAvailability 0x3A11EA4`** (Formation core): no anchor was confirmed.

## Limits

- **Dependent runtime:** ExtendedShiftCommandQueue and Formation (including FormationSpeech) depend on BugfixesAndQoL's shared command runtime `UnitCommandPathRuntime`. That runtime requires the reference hash (about 40 fixed detour prefixes and about 100 native constants). Until it gets the same fallback, deriving these values cannot keep the features working after a game update.
- **Fixed instruction offsets:** these anchors locate values at fixed offsets inside the validated function. An update that changes the function body fails closed instead of guessing.
