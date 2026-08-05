# Updating MPTest for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

MPTest writes a timestamped Error and returns before registering its UI or
native probe for every other DLL.

## What must be checked after an update

1. Find and re-document `QueueLocalChore`, `CopyChoreField`, the handler table,
   and the original opcode-111 handler. Do not copy RVAs from an older build.
2. Update and verify the current RVAs (`0x23990`, `0x1F5F0`, `0x2C6A30`, and
   `0xFC30` for this baseline), file size, SHA-256, and expected prologue bytes.
3. Revalidate the ChoreManager address supplied by the Script Extender and its
   fields `+0x84CC8`, `+0x84CCC`, `+0x84CD4`, and pending slots `+0xB0BF8` with
   slot size `0x500`.
4. Confirm opcode 111 is still unused, its table entry still targets the
   harmless original handler, and the table page protection can be restored.
5. Repeat singleplayer plus host/client scheduling, command-ID barrier, payload,
   resynchronization, and handler-restoration tests.
6. Only after all checks update both `ExpectedCrusaderSha256` in
   `NativeChoreProbe` and the shared current hash.

The runtime additionally validates bytes and pointers before modifying the
handler table; failure leaves the table untouched.
