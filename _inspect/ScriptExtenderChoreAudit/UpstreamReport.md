# Script Extender chore 106 phase and length-validation defects

## Scope and reference build

- Script Extender commit: `171d68e155a8f98c5f8c4ee154d9af154c9a2443`
- Relevant implementation: `src/SHCDESE.BepInEx/Detours/BulkChoreDetours.cs`
- The chore implementation was introduced by commit `bf45ef9`; the later relevant commit `5e8eb47` only changed log levels.
- Canonical `CrusaderDE.dll` SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`
- PE image base: `0x180000000`

No Script Extender or mod source was changed during this audit.

## Confirmed defect 1: chore 106 ignores the native handler phase

`c_game_chore_106_handler_impl()` selects pack versus unpack solely from the managed `_isSending` flag. `_isSending` is only true while the local queue detour is running. It is therefore not a valid phase discriminator on a receiving peer.

Vanilla handlers instead read the native phase at `ChoreManager + 0x84CCC`:

- phase `1`: pack fields;
- phase `0`: unpack fields and execute the action;
- any other phase: return without touching the payload.

This behavior was verified in several unrelated native handlers, including chore 31 at RVA `0x127A0`, chore 34 at RVA `0x12A20`, and chore 43 at RVA `0x13C10`.

The native remote-chore routine at RVA `0x23F10` proves the extra invocation sequence:

1. zero all 1260 bytes of the durable payload slot;
2. set `ChoreManager + 0x84CCC` to phase `2`;
3. invoke the chore handler;
4. copy the received payload into the durable slot;
5. set the phase to `0` and reset the field cursor;
6. invoke the handler again for unpack/execute.

Because the managed handler treats every `_isSending == false` invocation as unpack, it attempts to read the deliberately empty phase-2 slot and logs `Received SE chore with implausible length 0, discarding`. This is a real Script Extender defect and directly explains the warning documented in work item 132.

## Important negative finding: the phase defect does not create a partially copied payload

The same native remote-chore routine disproves the hypothesis that the phase-2 mistake itself exposes a payload while it is being copied. The destination slot is first cleared, the phase-2 handler returns, and the entire outer payload is then copied with one native `memcpy` before phase 0 is set and the handler runs again.

Consequently:

- the phase defect explains the length-zero warning with certainty;
- it does not by itself explain a later phase-0 payload whose prefix is valid but whose tail is zero-filled;
- payload size reduction can reduce exposure to a separate transport/length problem, but it is not a fix for the phase defect.

## Confirmed defect 2: the receiver trusts the inner length without checking the native outer length

During phase 0, `BulkChoreDetours` reads the first four payload bytes as `len`, checks only `1 <= len <= 1200`, allocates that many bytes, and asks `c_game_chore_pack_field` to copy all of them.

It does not compare `4 + len` against the native outer chore length stored at `ChoreManager + 0x84CD4`.

The native field-copy function at RVA `0x1F5F0` also does not enforce the current chore's outer length. It copies the requested field size first and only compares the aggregate cursor against the much larger temporary-buffer ceiling `0x2BF20`. Therefore, if an outer chore contains a valid four-byte inner length but fewer than `4 + len` actual bytes, the managed receiver reads beyond the actual payload into the remainder of the durable slot. The remote routine cleared that remainder to zero immediately beforehand. The resulting synthetic, zero-padded buffer is then dispatched to MessagePack instead of being rejected as truncated.

This is independently a real validation defect even if the reason for an outer-length mismatch has not yet been identified. It also matches the observed failure shape: a valid beginning followed by zero where a later MessagePack Boolean was expected.

## Size limits and what is not yet proven

The native durable slot accepts up to `0x4EC` (1260) payload bytes. The Script Extender caps the complete `[packetId][body]` blob at 1200 bytes and adds its own four-byte length prefix, so a maximum accepted send occupies 1204 native payload bytes. That is within the native ceiling.

The failing RandomEvents bodies were approximately 228/229 bytes (plus the two-byte packet ID and four-byte Script Extender prefix), far below both limits. There is no size threshold near 228 bytes in the inspected Script Extender pack/unpack code.

The source audit therefore does not prove that “large payloads” alone are truncated, fragmented, or corrupted. It proves that:

1. the receiver runs in an invalid native phase;
2. the receiver cannot distinguish a complete outer payload from one that is shorter than its embedded length;
3. a shortened outer payload is deterministically zero-padded and dispatched as if complete.

Identifying where the outer length first becomes shorter requires runtime instrumentation or a controlled multiplayer payload sweep. Static analysis alone cannot distinguish native network construction, native receive parsing, or another transport boundary once the sender's durable slot has been packed correctly.

## Recommended upstream fix

1. Resolve and expose the native chore phase for the supported DLL. For this build it is `ChoreManager + 0x84CCC`; validate the offset semantically from the queue/remote-processing code rather than treating it as universally stable.
2. In handler 106:
   - phase `1`: pack only when `_isSending` is true and a pending payload exists;
   - phase `0`: unpack and dispatch;
   - all other phases, including phase `2`: return without reading or logging a malformed payload.
3. In phase 0, read the native outer length at `ChoreManager + 0x84CD4` before copying the body. Require at minimum `outerLength >= 4` and `innerLength <= outerLength - 4`; requiring exact equality is preferable for this dedicated schema.
4. Reject a mismatch before allocating or calling the native field-copy function. Log phase, outer length, inner length, and cursor for diagnosis.
5. Put `_isSending` and `_pendingSendPayload` cleanup in `finally` blocks so an exception cannot poison later chore handling.
6. Add real multiplayer tests for payload sizes 18, 219, 220, 228, 229, 512, 1199, and 1200 bytes, plus injected outer/inner length mismatches and a value above 1200.

## Relevant native RVAs for the reference DLL

| Function/data | RVA or manager offset | Finding |
| --- | ---: | --- |
| `c_game_chore_pack_field` | `0x1F5F0` | Copies the requested field without checking the current outer chore length |
| `c_game_queue_chore` | `0x23990` | Creates a 1260-byte durable payload slot and starts phase 1 |
| `c_game_receive_chore_internal` | `0x23E70` | Atomically appends each received outer block to the native receive buffer |
| remote chore materialization/execution | `0x23F10` | Phase 2 on a zeroed slot, one full payload copy, then phase 0 |
| chore handler table | `0x2C6A30` | Script Extender replaces entry 106 |
| native phase | `ChoreManager + 0x84CCC` | Values 2, 1, and 0 control native handler behavior |
| native outer packed length | `ChoreManager + 0x84CD4` | Must bound the Script Extender's embedded length |
| native field cursor | `ChoreManager + 0x370BF8` | Reset before native handler phases |

