# Vanilla Peace-Time Gameplay

## Provenance

- Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Native image base: `0x180000000`
- Current simulation tick: RVA `0x3665F58`
- Active flag: RVA `0x38722DC`
- End tick: RVA `0x38722E0`
- Start tick: RVA `0x38722E8`
- Multiplayer mode: RVA `0x8574B90` (`0` singleplayer, `1` custom multiplayer, `99` skirmish/co-op preparation)
- Static evidence: Ghidra functions, decompiler, Xrefs, raw bytes, and Iced.Intel decoding from the current baseline.

## State flow

Initializer `0xCA900` converts the configured minute value into Vanilla's timer state and active flag. It stores the current simulation tick as the start and originally computes the end as `minutes * StartingGameSpeed * 60 + start`: the seven-byte instruction at `0xCA904` is `imul edx,[0x87ECA00]`, where `0x87ECA00` is the Chore `StartingGameSpeed` field, and the following instruction at `0xCA91E` multiplies by 60. With Vanilla's multiplayer default of 40 this yields `0x960` ticks per minute, while a singleplayer start speed of 300 yields 18,000 ticks per minute. The scenario reconstruction at `0x100530` uses the fixed `0x960` conversion for its separate persisted-time path. Save/load at `0x15C60` and `0x100530`, the checksum paths, and the AI military scheduler at `0x2AE40` are not restricted to multiplayer mode.

BugfixesAndQoL deliberately replaces only `0xCA904â€“0xCA90A` with `imul edx,edx,40` plus four one-byte NOPs. Vanilla's following multiplication by 60, timer state, display, expiry, gameplay consumers, event, and save/load remain authoritative. Thus every newly initialized Peace Time minute has a fixed distance of 2,400 simulation ticks regardless of the starting gamespeed; the actual simulation throughput still determines its real-time duration. The initializer has no direct branch or call target entering the replacement interior `0xCA905â€“0xCA90A`.

Display and expiry are implemented entirely by `0xCA870`: it publishes `OST_PEACETIMER` from the remaining and total simulation ticks, clears the active flag once the current tick exceeds the end tick, removes the OST, and emits Vanilla event/sound `0x123`. The function itself has no mode check, but its sole regular code caller is gated in the main simulation update `0xCDE60`: `je` at `0xCE304` skips the call at `0xCE309` only when multiplayer mode is `0`. Modes `1` and `99` already fall through. Extending Peace Time to ordinary singleplayer therefore requires neutralizing this two-byte caller gate; reimplementing the timer would diverge from Vanilla.

The state addresses above are additionally tied to the exact RIP-relative reads in `0xCA870` at `0xCA87D` (start), `0xCA889` (end), and `0xCA89C` (current tick). The mode read at `0xCE2FD` resolves to `0x8574B90`. Temporary hash-gated runtime diagnostics used these references to confirm identical 2,400-tick distances in modes 99 and 1; the finalized implementation removes that permanent tick subscription and retains this evidence only in the baseline.

The alternative update at `0xD1E50` is reached only for the special mode/submode/enable-state combination checked at `0xCE2DA-0xCE2F3`. It coordinates Freebuild/scenario timers and is not a substitute for `0xCA870` in ordinary singleplayer custom games.

The active flag has exactly these 36 audited instruction references:

`15D63, 15FD4, 22561, 2AEA4, 2AF1C, 88234, 8A14A, 8D368, 8D7C6, 8E04C, 8E0E0, 8E601, 8EA43, 8EC88, ABC9D, C70BC, CA874, CA8CD, CA917, CA92C, CDD6F, D1E95, D1F42, D2935, 10073D, 100820, 105534, 119110, 124B98, 1256A7, 1259D7, 156E8D, 17F3CE, 1868A0, 186964, 186991`.

Fourteen are mode-independent state, scheduler, timer-function, checksum, or persistence paths. This classification describes the flag references themselves; the upstream caller gate for `0xCA870` must be audited separately:

`15D63, 15FD4, 22561, 2AEA4, 2AF1C, CA874, CA8CD, CA917, CA92C, D1E95, D1F42, D2935, 10073D, 100820`.

The other 22 are gameplay consumers in the audited predicate/dispatcher functions. Their mode-0/mode-99 bypasses are located in functions `0x89F40`, `0x8C5F0`, `0xABB90`, `0xC70B0`, `0xCDB20`, `0x105510`, `0x124B90`, `0x1256A0`, `0x1259D0`, `0x156DC0`, `0x17F2F0`, and `0x1867A0`, plus the starting-troop dispatcher `0x119050`.

## Mode-gate patch contract

The corrected 28 single-instruction spans are:

- Replace the Vanilla `StartingGameSpeed` factor at `CA904` (`0F AF 15 F5 20 72 08`) with the fixed canonical factor 40 while preserving the seven-byte span.
- NOP mode bypasses: `8A148`, `8D366`, `8D7C4`, `8E05E`, `8E0DE`, `8E5FF`, `8EA59`, `8EC9E`, `C70BA`, `186976`, `18697A`.
- Replace conditional selections with their peace-aware move: `8E062`, `8EA5D`, `8ECA2`.
- Redirect mode 0/99 directly to the Vanilla flag check, skipping the multiplayer-network flag: `ABC88 -> ABC9D`, `ABC8E -> ABC9D`, `105520 -> 105534`, `105525 -> 105534`.
- Preserve Vanilla's existing taken exit as unconditional while the preceding active-flag branch retains authority: `CDD85 -> CDDCC`, `124BB4 -> 124E56`, `1256C3 -> 1259A7`, `1259F3 -> 125CEB`, `156EAB -> 156F4A`, `17F3E4 -> 17F40D`, `1868B6 -> 18688E`.
- Replace the peace-only mode read at `18699A` (`8B 05 F0 E1 3E 08`) with `mov eax,1` plus NOP (`B8 01 00 00 00 90`). Preserve `1869A3` and the shared conditional `1869A7` unchanged.
- NOP the display/expiry caller gate `CE304` (`74 08`) so mode 0 reaches Vanilla's existing call at `CE309 -> CA870`. Modes 1 and 99 already use that fallthrough and remain unchanged.

These instruction edits change the audited mode exceptions. The additional wildlife entry guard below deliberately blocks player-unit targets during active Peace Time in every mode; outside Peace Time the complete exclusion predicate matches Vanilla.

## Starting troops

Dispatcher `0x119050` has length 2459 bytes. In multiplayer mode 1, its Vanilla branch at reference `0x119110` exits while the active flag is set before advancing the start-troop counters. Modes 0 and 99 bypass that branch.

The dispatcher is invoked at `0xCE2C3`, before the `0xCA870` call at `0xCE309`. Consequently, it remains blocked on the exact expiry tick and resumes through its original mode-specific path on the following processed simulation tick. The entry span `0x119050â€“0x119060` is six complete instructions with lengths `2,1,1,2,4,6` and bytes:

`40 53 56 57 41 54 48 83 EC 68 8B 05 C8 CE 54 03`

RedBird 1.3.2 displaces exactly 16 bytes for this requested minimum. Ghidra's direct Xrefs have no target inside the open interval `0x119051â€“0x11905F`; Iced decoding of the entire dispatcher confirms no internal direct branch/call enters it. A native entry guard may therefore read the active flag and return before the prologue when set; otherwise it must replay all six instructions and resume at `0x119060`. Its temporary RAX/flags changes are safe: RAX is volatile and the replayed final `mov eax,[rip+...]` restores the original value before the following Vanilla comparisons.

## Confidence and remaining runtime validation

Static control/data-flow confidence is high for the current hash. Runtime validation must still cover countdown expiry, damage/targeting, AI military resumption, and start-troop cadence in modes 0, 1, and 99. No conclusion here applies to another native hash without a fresh audit.

## Wildlife regression and Peace-Time guard (2026-10-07)

The original audit incorrectly treated `1869A7` as exclusively peace-owned. The full 533-byte predicate `1867A0..1869B5` also reaches that shared JNE from `1868DE`, with flags produced by the classification-byte comparison at `1868D6`. Turning it into JMP returns exclusion=1 for valid lion/hyena versus troop contacts even when the active flag is zero. Original exclusion=0 admits the target. The erroneous assumption in the earlier mode-gate contract is superseded by the corrected contract above.

The relevant raw unit inputs are RCX=unit-manager base, EDX=1-based attacker ID, R8D=1-based target ID, R9B=filter flag. Native record addresses use `manager + gameId*0x490`, relative to LastOrderedUnit at +0x65C. Type reads at +0x6E6 match `GameUnit.r_UnitChimp` (+0x8A); owner reads at +0x6EE match the low-byte public `r_ControllableForPlayerId` (+0x92), followed by its native adjacent byte. The classification byte at +0x984 is the low byte of the presently unnamed GameUnit field +0x328; no guessed military/animal name is assigned to it. Species symbols were checked against installed SHCDESE 2.13.0: lion45, hyena87, crocodile88.

The lion and hyena dispatcher entries at table RVA0x321CB0 both resolve to0x156DC0. Target admission0x188A20, retained-target evaluation0x18E9A0 and contact melee0x187230 consume the exclusion predicate. The additional contact path0x195170 revalidates its retained target through0x188A20 before damage0x199110. Damage0x199110 subtracts HP, clamps at zero and publishes hit/death state; its other callers0x198ED0 (self/environmental damage) and0x1ABF0 (lord resolution) are outside wildlife contact selection. Baseline function-name confidence remains candidate; inspected instructions, parameter flow and dispatcher values provide the feature-specific evidence.

The additional permanent X64InlineHook starts at0x1867A0 and displaces18 bytes: `48 89 5C 24 08 / 48 63 C2 / 48 8B D9 / 4C 69 D8 90 04 00 00` (lengths5,3,3,7); continuation0x1867B2 is `4C 03 D9` (add r11,rcx). Full-function direct branch decoding and baseline Xrefs show no entry into0x1867A1..0x1867B1. The six-byte mode-read replacement has no interior target either. Installed RedBird1.5.0 decodes at least max(requested,14) bytes, yielding exactly18. X64AssemblyPatch instead rounds its six-byte body to exactly the original six-byte MOV; the inline minimum must not be applied to it.

Before the original prologue, the generated guard returns exclusion=1 only when the actual active flag is nonzero, attacker type is lion/hyena/crocodile and the target's public owner byte is1..8. All other paths replay the complete prologue and continue. Only RAX/flags are scratch: the replay recomputes RAX and flags before any Vanilla consumer; RCX/EDX/R8D/R9B, RBX, stack and SIMD are untouched by the prefix. No ContextHook/managed callback is involved. The transaction is rooted by the static plugin patch field; normal lifecycle/settings/map transitions never undo it. Actual native flag expiry changes behavior immediately, without executable-memory mutation.

Offline execution of complete copied native predicates and the real prepared RedBird stub covers1,520,832 inactive and1,520,832 active cases over every unit-type pair, both raw classification bytes, both owner0/1 values, equal/unequal teams, both filter flags and modes0/1/99. Inactive results equal pristine Vanilla; active wildlife/player results exclude, and all other guard results equal the corrected body. Mode1 unrelated active results also equal Vanilla. Legacy lion/hyena failures are reproduced, all player IDs1..8 plus invalid/non-player values are checked, and expiry uses the same stub without reinstallation. These are offline native execution results, not an in-game or multiplayer acceptance claim. Real targeting, animation/contact damage and expiry with/without Fixes remain to be observed.
