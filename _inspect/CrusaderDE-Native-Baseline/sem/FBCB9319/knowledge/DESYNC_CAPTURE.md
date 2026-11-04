# Desync capture and native unit wire-record prefix [Scope: Vanilla + mod integration]

Observed 2026-10-30. Native SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. These are static machine facts, not a real host/client acceptance result.

## Native records and checksum inputs

`0x15710` (Chore 56 handler) transfers `0x490` bytes from RVA `0x67E8A58 + unitId*0x490`, followed by 1000 path bytes from `0x7338278 + unitId*1000`. Its unpack path reads the unit ID before changing the local preserved word and before copying the record. The preserved word is at wire offset `0x34`; it is restored after copying the path.

The GameUnit view used by the Script Extender starts four bytes after that wire-record pointer. The manager has `EmptyUnitFillValue` at `+0x658`, and its typed `GameUnitArray` at `+0x65C`; the native transfer base is the former. Slot zero is a sentinel. This distinction matters when interpreting raw captures, even though the stride is the same `0x490`.

Independent instruction anchors establish the actual native inputs. `LEA` at `0x220C2` resolves the first hashed interval to `0x67E8F74`. `LEA` at `0x2209A` resolves the temporarily masked word pair to `0x67E9304`. The record transfer `LEA` at `0x157AE` resolves to `0x67E8A58`. Relative to wire record 1, these are respectively `0x8C` and `0x41C`; relative to the typed GameUnit they are `0x88` and `0x418`.

The unit checksum scans IDs 1..9999, hashes `0x404` bytes from wire `+0x8C` with eight bytes at wire `+0x41C` zeroed, hashes the independent 1000-byte path, and adds uint32 fields at wire `+0x3C`, `+0x40`, `+0x44`. The helper `0x78E0` sign-extends input bytes while assembling words, rotates the accumulated sum left by one between complete words, and uses uint32 wraparound. Native mask writes are restored before the builder returns.

Consequently GameUnit's `r_GlobalId` at typed `+0x94` is wire `+0x98`, and `r_UnitSelected` at typed `+0xAE` is wire `+0xB2`. The older Surrender audit describes typed offsets; those must not be applied directly to a Chore-56 wire record. Its historical traces are not rewritten.

## Logging and dispatch boundaries

The timeout branch in `0x1BCF0` calls `0x1A4A50` with the string `SyncEvent - Forced run`. In this native image, `0x1A4A50` contains `C2 00 00` (`RET 0`) and produces no output. Presence of the string/call does not establish a log file. The older Chore dossier's statement that this path logs must be qualified accordingly for this image.

The table dispatches in pending execution, local pack, local immediate execution, remote measure and remote immediate execution share the no-explicit-argument handler ABI. The local immediate dispatch has an incoming branch to `0x23B98`; a 14-byte patch starting at `0x23B94` would span that target. The predecessor block `0x23B85..0x23B98` ends exactly at that boundary.

Rechecked during the 2026-10-30 review: Chore 56 at `0x15710` changes the manager phase from 1 to 0 in its pack branch before calling the field helper `0x1F5F0`; its fifth helper argument remains 0 for packing and 1 for unpacking. The manager phase observed inside a helper call is therefore not sufficient to classify the originating dispatch. The regular scheduler `0xD1AD0` calls the checksum builder with argument 0, while Chore 54 at `0x15170` calls it with argument 1. Both calls can refer to the same simulation tick; their diagnostic checkpoints must retain their different execution contexts.

## DesyncTest evidence [mod integration]

`Testmods/DesyncTest/UpdateToNewDLL.md` records all ten observed spans, their ABI, the hash binding and the intentionally unavailable unknown-image data fallback. `tools/Audit-Native.py` reads the installed PE and the hash-selected raw xref export, validates instruction boundaries/incoming edges and independently decodes the three layout anchors above. The semantic query entry remains the source for function/control-flow inspection.

Production inline generators for the six dispatch/timeout blocks were executed in Windows x64 fixtures; external callees/data were isolated at their explicit boundaries. The native position-independent hash helper was also executed against random signed-byte inputs and compared with the production managed implementation. These checks do not prove Unity startup, live locking, co-installed hook behavior or multiplayer correctness; those remain runtime acceptance work. Initial evidence is retained under `.work/a/desync/001`, with further build-specific evidence locations in the package's build proof.
