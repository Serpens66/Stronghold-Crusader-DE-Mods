# Cursor dispatcher: verified boundary and embedded tables

Native SHA256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

The candidate database function at RVA8C5F0 ends at90001, inside the JMP
instruction beginning8FFFE. It omits switch tails and is not a complete boundary.
Installed PE exception directory identifies [8C5F0,90500), unwind RVA22D8E4.
The unwind range includes embedded switch tables, so it must not be decoded as
one uninterrupted instruction stream.

A fail-closed CFG decode from the entry, expanding all19 installed jump tables,
contains3040 instructions and14973 executable bytes, with no unresolved indirect
jumps or external direct branches. This establishes instruction coverage, not
callee semantics or gameplay acceptance. The cursor's final switch at8CF13 uses
six32-bit RVA entries at90088:8FFF7,90003,9001C,90028,90045,90054. They update
cursor mode and branch back; they are not function calls.

Evidence/tool: workspace _inspect/APISharedOwnership/Inspect-CursorDispatch.ps1,
cursor-dispatch-targets.log and native-commands/0x8C5F0.cfg-disassembly.txt.
Original database candidate and provenance are retained. Revalidate PE boundaries,
tables and instruction coverage when the native binary changes.
