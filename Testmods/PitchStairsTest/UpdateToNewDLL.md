# Native targets of PitchStairsTest (variant B)

Reference DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Background and audit: `PitchStairs.md` sections 17-20 in the workspace root.

Owning feature: variant B of the pitch/stairs fix. Pitch keeps the ground height in HeightGrid.
Every Vanilla code path that writes, restores or compensates the pitch drop of 4 is adjusted.
The general fire spread is the exception: it keeps Vanilla's lowered height on purpose.

## Contract and failure behavior

- **Atomic set:** all eleven sites are one feature unit. One `HookTransaction` (`RollbackAndThrow`, `OwnsHooks`) and one exclusive `NativeSiteRegistry` claim (`PitchStairsVariantB`) cover them. A partial installation never becomes active.
- **Validated before commit:**
  - installed hash equals the reference hash;
  - `NativePatternResolver` finds each site at its reference RVA;
  - image and live bytes match the site bytes;
  - the following bytes are unchanged;
  - instruction lengths match;
  - the `X64InlineHook` probe reports `DisplacedByteCount` equal to the span;
  - every stub assembles and decodes.
- **Validated after commit:** each site holds `FF 25 00000000 <stub>` followed by NOP padding.
- **Activation:** the native enabled word becomes 1 only after both validations. On any failure it stays 0, so Vanilla runs.
- **No fallback for another DLL:** there is deliberately no pattern fallback. The tile-manager offsets, the table offset and the stub spans are proven only for this hash. Another hash leaves the mod inactive and logs `PITCH_STAIRS_UNAVAILABLE`.
- **Lifetime:** hooks are never restored after publication. The plugin component is destroyed by the startup cleanup; the runtime stays rooted through static fields.
- **Second evidence for the data layout**, decoded at install time:
  - tile manager `0x405EDB0` from the `lea rcx` at `0x71956` in `0x71670`;
  - Structure `+0xB0BCA0` from `0x62F04`;
  - DefaultHeight `+0xDCCAC0` from `0x62F0F`;
  - MiscDisplay-to-Structure delta `-0x3FA2A0` from `0x804FA`;
  - Height `+0xD7E5A0`, Logic `+0x898400`, MiscDisplay `0x4F64CF0` (image RVA), the table offsets `0x7080`/`0x7090` and the call target `0x6B910` from the displaced blocks themselves.
- **Live cross-check:** at the first mission start, the Extender's `GameGlobalsManager.GameTileManagerVA` must equal image + `0x405EDB0`. A mismatch sets the enabled word to 0 and logs `PITCH_STAIRS_DISABLED`.
- **No instruction inside any span is a branch target** (full `.text` scan). Boundary targets are allowed: `0x6CCFC`, `0x80541` and `0x68EC0` are continuations.

## Sites

| Site | RVA / span / continuation | Original block | Enabled behavior | Flags after the skipped instruction |
|---|---|---|---|---|
| pitch-lowering (in `0x59380`) | `0x594C1` / 18 / `0x594D3` | `lea eax,[rdx-4]`; `movzx ecx,al`; `cmovbe ecx,r9d`; `mov [r8+rsi+Height],cl` | Store skipped; height stays as it was. The first three instructions run before the guard, so `cmovbe` consumes Vanilla's `cmp dl,4`. | `mov` sets no flags |
| aiv-pitch-restore (in `0x6CB20`) | `0x6CCED` / 15 / `0x6CCFC` | `add byte [rdx+rdi+Height],4`; `mov eax,[rdi+rdx*4+Logic]` | `add` skipped. The height was rebuilt just before from DefaultHeight plus the mapper offset. | next consumer is `bt eax,0xC` |
| manual-pitch-restore (in `0x6F230`) | `0x6F6AD` / 18 / `0x6F6BF` | `test al,8`; `je 0x6F6C1`; `and eax,~8`; `mov [rbx],eax`; `add byte [r12+r13+Height],4` | `add` skipped for stairs and walls; the ground under pitch is never lowered. `je` is re-encoded with its absolute target. | next consumer is `bt eax,0xC` |
| load-pitch-normalization (in `0x804C0`, called by `0x7D510` in `0x71670`) | `0x80530` / 17 / `0x80541` | `movzx eax,[r9+Default-Height]`; `cmp al,4`; `jb 0x80541`; `sub al,4`; `mov [r9],al` | Default ≥ 4: Height = Default (Vanilla: Default−4). Default 1..3: a pitch tile at height 0 without wall or structure is raised to Default; otherwise unchanged. The loop return value (eax) is unused by `0x7D510`. | next consumer is `add rdx,2` |
| render-pitch-height (in renderer `0x41D60`) | `0x43357` / 17 / `0x43368` | `movzx eax,[rbx+r9+Height]`; `mov eax,[r9+rax*4+0x7090]` | Reads the ordinary table entry `+0x7080` instead of the +4 compensation `+0x7090`. | identical (`mov`) |
| placement-pitch-height (in `0x676F0`) | `0x68EAD` / 19 / `0x68EC0` | `mov eax,0x2000`; `test word [rbp+r13*2+0x4F64CF0],ax`; `jne 0x68EC0`; `add esi,4` | `add esi,4` (pitch compensation before `cmp esi,0x88`) skipped. | next consumer is `shr edi,3` |
| fire-source-height (in `0x99420`) | `0x997B4` / 19 / `0x997C7` | `movzx edi,[rcx+r15+Height]`; `cmp ax,4`; `jge 0x99952` | For pitch without footprint-edge bit `0x2000`, edi = max(0, edi−4), i.e. Vanilla's lowered height for the spread comparison (\|Δh\| < 25). | entry flags dead; `cmp ax,4` recomputes them |
| fire-neighbor-west / -east / -row-a / -row-b (in `0x99420`) | `0x997DB`, `0x99824` / 15 / +15 | `lea edx,[rcx∓1]`; `mov rcx,r15`; `call 0x6B910`; `sub edi,eax`; `mov eax,edi` | The 0x6B910 result for a pitch neighbor without bit `0x2000` and without structure (its raw-height path) gets max(0, h−4). | `sub edi,eax` recomputes them |
| (same, row variants) | `0x9987C`, `0x998D1` / 15 / +15 | `add edx,[rbx+rcx*4+0x402FF38/20]`; `mov rcx,r15`; `call 0x6B910` | as above | same |

Fire-neighbor stub stack contract:

- `sub rsp,0x30` keeps the 16-byte alignment at the call.
- `[rsp..+0x20)` is the callee's home area.
- `[rsp+0x28]` holds the stub's adjust flag.
- `rax` and `rcx` are dead at these calls (not arguments; clobbered by the callee).
- `0x6B910` itself is not patched: it has more than 40 callers, and BFQoL installs an entry detour on it, which the stub's absolute call still reaches.

## Fire spread: why it is adjusted, and the limits of the adjustment

The user decided to keep Vanilla's fire behavior. Vanilla treats pitch as lowered exactly when the PitchTrap flag is set and the misc-display bit `0x2000` (footprint edge, see `0x6C510`) is clear. The renderer (`0x43347`) and the placement cap (`0x68EB2`) use the same predicate, and the fire stubs reuse it.

Known limit: after a burn-out, `0x7D4A0` restores DefaultHeight while the flag can still be set until the record is cleaned up. During that short window the stub assumes a lowered pitch tile; Vanilla does not.

The pitch-to-pitch chain in `0x7D280` (`0x7D3B1`/`0x7D411`) compares two pitch tiles, both raised by 4, so it is not adjusted. Only low default heights under 4 differ there.

## Not adjusted (accepted or open)

- **Unit stand height and speed** (`0x19B260`, `0x184FD0`, `0x182B00`): the artificial height step disappears. Accepted by the user; still needs checking in game.
- **Fire effect height** (`0x7D44B`, `0x9C430` uses the raw height): needs a visual check.
- **Editor:** the editor load path (`0x85E90` → `0x5BC70`/`0x87D10`) does not reach `0x804C0`. The mission-start callback for `EditorCreated`/`EditorLoaded` therefore applies the same rule once, through `NativeCode.NormalizedPitchHeight` over the Extender grid spans (log field `editorRaisedTiles`; replays excluded). It does not rebuild pathing transitions there; every playable start recomputes them in `0x71670`. Other paths outside `0x71670` are still reported by the DEVDIAG scan `PITCH_STAIRS_SCAN`.
- **Stairs already raised by +4 in old saves** carry no pitch flag any more and are not repaired.

## Re-audit on a new DLL

- Repeat the full `.text` height-writer and compensation scans from PitchStairs.md section 20: all −4 stores, +4 restores, `PitchTrap && !0x2000` compensations and `0x804C0`-like loops.
- Then re-check every site's control flow, register and flag liveness, incoming edges, the 0x6B910 callers and foreign hooks: Extender detours, BFQoL/ExtraFeatures/APIShared sites and the Fixes clone.
- Then rerun `PitchStairsTest.StubTests.exe`.

## Backend block guard

The generator compares the instructions RedBird displaces with the audited decode of each site: Code, length, displacement, immediate and branch target. A mismatch throws during `Commit` and rolls the transaction back. Both are decodes at the real site address; a backend that decoded elsewhere would therefore fail closed.
