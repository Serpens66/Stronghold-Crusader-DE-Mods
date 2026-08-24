# Updating MPTest for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24816905`
- DLL size: `3451392` bytes
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

The managed MPTest UI and diagnostics remain available on another DLL. The
native Chore probe is separately fail-closed and writes a timestamped Error if
its fixed ChoreManager layout is unaudited or any target resolution fails.

## What must be checked after an update

1. Find and re-document `QueueLocalChore`, `CopyChoreField`, the handler table,
   and the original opcode-111 handler. Do not copy RVAs from an older build.
2. Verify the current function entries `0x23990` and `0x1F5F0`. Queue uses the
   stable interior `.text` signature at `0x239AE` and subtracts `0x1E`; Copy
   uses `CopyChoreFieldPattern` at its entry. Both require one unique match when
   local RVA validation fails.
3. Obtain the handler table from Script Extender
   `GameStateChoreHandlersVA` (baseline RVA `0x2C7A30`), bounds-check it, then
   read opcode 111's handler from entry `0x2C7DA8`. The baseline handler RVA is
   `0xFC30`; it is derived from the table rather than accessed by a fixed RVA
   and must match the harmless `C2 00 00` body.
4. Revalidate the ChoreManager address supplied by the Script Extender and its
   fields `+0x84CC8`, `+0x84CCC`, `+0x84CD4`, and pending slots `+0xB0BF8` with
   slot size `0x500`.
5. Confirm opcode 111 is still unused, its table entry still targets the
   harmless original handler, and the table page protection can be restored.
6. Repeat singleplayer plus host/client scheduling, command-ID barrier, payload,
   resynchronization, and handler-restoration tests.
7. Only after all fixed layout checks update the shared current hash.

The runtime additionally validates bytes and pointers before modifying the
handler table; failure leaves the table untouched.

## Audit for Steam build 24651686

`QueueLocalChore`, `CopyChoreField`, the handler table and the harmless opcode
111 handler remain at `0x23990`, `0x1F5F0`, `0x2C6A30` and `0xFC30`. Their
expected bytes still match, opcode 111 still points to the `C2 00 00` handler,
and the Script Extender resolved ChoreManager at the same manager contract.
The fixed fields and host/client scheduling require the usual live MP smoke
test before using this diagnostic mod.

## Audit for Steam build 24816905

`QueueLocalChore`, `CopyChoreField` and the harmless opcode-111 handler remain at
`0x23990`, `0x1F5F0` and `0xFC30`, with unchanged semantics. The handler table
moved by `0x1000` to `0x2C7A30`; its opcode-111 entry at `0x2C7DA8` still points
to the `C2 00 00` handler. The latest Script Extender log independently resolves
the same new table address. The first live run exposed that a prior startup hook
can replace the Queue prologue; resolution therefore now uses its stable body
signature and derives the entry. The table and opcode handler are likewise
obtained from the Script Extender/table contents instead of fixed accesses.
Failure disables only ChoreProbe; singleplayer and host/client scheduling remain
live diagnostic tests.
