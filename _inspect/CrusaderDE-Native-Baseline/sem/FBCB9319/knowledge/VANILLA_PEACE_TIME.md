# Vanilla Peace-Time Gameplay

## Provenance

- Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Native image base: `0x180000000`
- Active flag: RVA `0x38722DC`
- Multiplayer mode: RVA `0x8574B90` (`0` singleplayer, `1` custom multiplayer, `99` skirmish/co-op preparation)
- Static evidence: Ghidra functions, decompiler, Xrefs, raw bytes, and Iced.Intel decoding from the current baseline.

## State flow

Initializer `0xCA900` converts the configured minute value into Vanilla's timer state and active flag. It stores the current simulation tick as the start and computes the end as `minutes * 60 * 40 + start`; the scenario reconstruction at `0x100530` independently confirms the `0x960` ticks-per-minute constant. Save/load at `0x15C60` and `0x100530`, the checksum paths, and the AI military scheduler at `0x2AE40` are not restricted to multiplayer mode.

Display and expiry are implemented entirely by `0xCA870`: it publishes `OST_PEACETIMER` from the remaining and total simulation ticks, clears the active flag once the current tick exceeds the end tick, removes the OST, and emits Vanilla event/sound `0x123`. The function itself has no mode check, but its sole regular code caller is gated in the main simulation update `0xCDE60`: `je` at `0xCE304` skips the call at `0xCE309` only when multiplayer mode is `0`. Modes `1` and `99` already fall through. Extending Peace Time to ordinary singleplayer therefore requires neutralizing this two-byte caller gate; reimplementing the timer would diverge from Vanilla.

The alternative update at `0xD1E50` is reached only for the special mode/submode/enable-state combination checked at `0xCE2DA-0xCE2F3`. It coordinates Freebuild/scenario timers and is not a substitute for `0xCA870` in ordinary singleplayer custom games.

The active flag has exactly these 36 audited instruction references:

`15D63, 15FD4, 22561, 2AEA4, 2AF1C, 88234, 8A14A, 8D368, 8D7C6, 8E04C, 8E0E0, 8E601, 8EA43, 8EC88, ABC9D, C70BC, CA874, CA8CD, CA917, CA92C, CDD6F, D1E95, D1F42, D2935, 10073D, 100820, 105534, 119110, 124B98, 1256A7, 1259D7, 156E8D, 17F3CE, 1868A0, 186964, 186991`.

Fourteen are mode-independent state, scheduler, timer-function, checksum, or persistence paths. This classification describes the flag references themselves; the upstream caller gate for `0xCA870` must be audited separately:

`15D63, 15FD4, 22561, 2AEA4, 2AF1C, CA874, CA8CD, CA917, CA92C, D1E95, D1F42, D2935, 10073D, 100820`.

The other 22 are gameplay consumers in the audited predicate/dispatcher functions. Their mode-0/mode-99 bypasses are located in functions `0x89F40`, `0x8C5F0`, `0xABB90`, `0xC70B0`, `0xCDB20`, `0x105510`, `0x124B90`, `0x1256A0`, `0x1259D0`, `0x156DC0`, `0x17F2F0`, and `0x1867A0`, plus the starting-troop dispatcher `0x119050`.

## Mode-gate patch contract

The 28 single-instruction spans are:

- NOP mode bypasses: `8A148`, `8D366`, `8D7C4`, `8E05E`, `8E0DE`, `8E5FF`, `8EA59`, `8EC9E`, `C70BA`, `186976`, `18697A`, `1869A3`.
- Replace conditional selections with their peace-aware move: `8E062`, `8EA5D`, `8ECA2`.
- Redirect mode 0/99 directly to the Vanilla flag check, skipping the multiplayer-network flag: `ABC88 -> ABC9D`, `ABC8E -> ABC9D`, `105520 -> 105534`, `105525 -> 105534`.
- Preserve Vanilla's existing taken exit as unconditional while the preceding active-flag branch retains authority: `CDD85 -> CDDCC`, `124BB4 -> 124E56`, `1256C3 -> 1259A7`, `1259F3 -> 125CEB`, `156EAB -> 156F4A`, `17F3E4 -> 17F40D`, `1868B6 -> 18688E`, `1869A7 -> 18688E`.
- NOP the display/expiry caller gate `CE304` (`74 08`) so mode 0 reaches Vanilla's existing call at `CE309 -> CA870`. Modes 1 and 99 already use that fallthrough and remain unchanged.

These edits change only the audited mode exceptions. Mode 1 reaches the same Vanilla flag decisions and terminal states as before.

## Starting troops

Dispatcher `0x119050` has length 2459 bytes. In multiplayer mode 1, its Vanilla branch at reference `0x119110` exits while the active flag is set before advancing the start-troop counters. Modes 0 and 99 bypass that branch.

The dispatcher is invoked at `0xCE2C3`, before the `0xCA870` call at `0xCE309`. Consequently, it remains blocked on the exact expiry tick and resumes through its original mode-specific path on the following processed simulation tick. The entry span `0x119050–0x119060` is six complete instructions with lengths `2,1,1,2,4,6` and bytes:

`40 53 56 57 41 54 48 83 EC 68 8B 05 C8 CE 54 03`

RedBird 1.3.2 displaces exactly 16 bytes for this requested minimum. Ghidra's direct Xrefs have no target inside the open interval `0x119051–0x11905F`; Iced decoding of the entire dispatcher confirms no internal direct branch/call enters it. A native entry guard may therefore read the active flag and return before the prologue when set; otherwise it must replay all six instructions and resume at `0x119060`. Its temporary RAX/flags changes are safe: RAX is volatile and the replayed final `mov eax,[rip+...]` restores the original value before the following Vanilla comparisons.

## Confidence and remaining runtime validation

Static control/data-flow confidence is high for the current hash. Runtime validation must still cover countdown expiry, damage/targeting, AI military resumption, and start-troop cadence in modes 0, 1, and 99. No conclusion here applies to another native hash without a fresh audit.
