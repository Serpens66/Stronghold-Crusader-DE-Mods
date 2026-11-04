# Native targets of PitchStairsTest (variant B)

Reference DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Purpose, log markers, test procedure and dated runs: [docs/PITCHSTAIRS.md](docs/PITCHSTAIRS.md). Vanilla audit: [PITCH_STAIRS_HEIGHT.md](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/PITCH_STAIRS_HEIGHT.md).

Scope: native corrections only; NetworkMode 1; versions and minimums per [info.json](info.json).
Installation success requires matching native contracts. No normal multiplayer desync has been demonstrated.

Owning feature: variant B of the pitch/stairs fix. Pitch keeps the ground height in HeightGrid.
The audited pitch creation, overbuild, load, render and placement compensation paths are adjusted.
General fire spread and pitch-chain comparisons preserve Vanilla's virtual height, including
the terminal restoration window; ordinary damage and disease use actual ground height.

## Contract and failure behavior

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
- **Activation:** the native enabled word becomes 1 only after both validations and the early Extender manager crosscheck. Installation failure leaves it 0, so Vanilla runs. After successful activation it stays 1 for the process, including diagnostic/status publication failures.
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

## Sites

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

## Fire spread: why it is adjusted, and the limits of the adjustment

The user decided to keep Vanilla's fire behavior. Vanilla initially stores pitch 4 lower and its spread code reads that raw height. Both stubs therefore subtract 4 for non-restored PitchTrap tiles; the terminal restore window keeps Vanilla's restored height. Overbuild clears the flag: AIV at `0x6CCE3`, manual at `0x6F6B1`.

**Do not test MiscDisplay `0x2000` here.** Measured and audited on 02.11.2026 ([docs/PITCHSTAIRS.md](docs/PITCHSTAIRS.md#dated-evidence-02112026-eleven-site-builds), baseline section "MiscDisplay 0x2000 on pitch"):

- On pitch, `0x2000` means "visible to the local player". The simulation tile pass `0x65830` clears it at `0x682D9` and sets it again at `0x683D8` for burning pitch and for pitch of the local player's team.
- The bit therefore differs per client. Vanilla uses it only to draw visible pitch as a ditch: the renderer at `0x43347` and the cap at `0x68EB2` skip the +4 compensation for it.
- Excluding it would leave all own, allied and burning pitch unadjusted, and in multiplayer it would make fire spread depend on the local player.

The terminal restore window is tracked by a native byte map. A restored pitch source or neighbor
keeps its raw height; only a non-restored pitch field uses max(0,h-4). The pitch-to-pitch comparison
also saturates both local operands, so low defaults do not change its ignition decision.

## Terminal markers and the three additional sites

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

## Not adjusted (accepted or open)

- **Unit stand height and speed** (`0x19B260`, `0x184FD0`, `0x182B00`): the artificial height step disappears. Accepted by the user; still needs checking in game.
- **Fire effect height** (`0x7D44B`, `0x9C430` uses the raw height): needs a visual check.
- **Ranged targeting, projectile hits and disease:** intentionally use actual B ground height. Private native fixtures compare B pitch with ordinary ground at projectile hit-band, ranged correction +/-70 and strict disease height 120 boundaries. Ranged sight/target predicates and presentation sinks are isolated; live LOS, APIShared disease-rule interaction and gameplay remain acceptance checks.
- **Visible pitch is drawn at ground level** instead of as a ditch 4 lower; hidden enemy pitch looks as in Vanilla. This matches unit and fire-effect heights, which also use the raw height. The user checked this in game on 02.11.2026 and accepted it.
- **Editor:** loading a map into the editor goes through `DLL_LoadSaveGame` (`bLoadingEditorMap=1`) and therefore reaches `0x804C0`; this was observed on 02.11.2026. Only a new editor map (`0x85E90` → `0x5BC70`/`0x87D10`) bypasses it, and such a map has no pitch yet. No managed editor normalization is needed. Other paths outside `0x71670` are still reported by the DEVDIAG scan `PITCH_STAIRS_SCAN`.
- **Stairs already raised by +4 in old saves** carry no pitch flag any more and are not repaired.

## Re-audit on a new DLL

- Repeat the full `.text` height-writer and compensation scans of the baseline (sections "Complete pitch-drop writer and compensation inventory", "Pointer-walking grid loops" and "Further height consumers and the pitch-bit scan"): all −4 stores, +4 restores, `PitchTrap && !0x2000` compensations, `0x804C0`-like loops and functions that access Height and test bit 0x8.
- Then re-check every site's control flow, register and flag liveness, incoming edges, the 0x6B910 callers and foreign hooks: Extender detours, BFQoL/ExtraFeatures/APIShared sites and the Fixes clone.
- Then rerun `PitchStairsTest.StubTests.exe`.

## Backend block guard

The generator compares the instructions RedBird displaces with the audited decode of each site: Code, length, displacement, immediate and branch target. A mismatch throws during `Commit` and rolls the transaction back. Both are decodes at the real site address; a backend that decoded elsewhere would therefore fail closed.
