# Read-only inspection of a live SHCDE process

This workflow is intended for diagnostic chats that must correlate live game data with the canonical native baseline without changing the running process.

## Safety boundary

- Open the game with `OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION)` only.
- Read with `ReadProcessMemory`; never request write/operation access, inject code, allocate remote memory, suspend threads, or install hooks for diagnosis.
- Close every process handle in `finally`.
- Keep reads bounded. Validate pointers, counts, record sizes, address ranges, and multiplication overflow before reading a block.

## Reproducible setup

1. Record the PID and process start time.
2. Locate `CrusaderDE.dll` in the target process and record its ASLR-adjusted module base.
3. Hash the installed DLL and compare it with `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` before applying any RVA or structure conclusion.
4. Record the baseline hash, every RVA and offset, the ID basis, record size, and read bounds in the diagnostic output.
5. Convert an RVA to a live address only as `moduleBase + rva`; never reuse an address from a previous process.

## SHCDE-specific notes

- Unit and building game IDs are 1-based, while native arrays and managed spans are 0-based. For the unit array, use `arrayBase + (unitId - 1) * 0x490` only after validating the ID.
- Prefer `StructureGrid` and current manager records to identify live objects. Broad native-grid scans can include stale or unrelated entries.
- Composite building-animation IDs use `buildingId + subPart * 4000`; compare the base object with `compositeId % 4000` when correlating parts.
- The transient render-buffer manager is at RVA `0xA98820`: buffer pointer at `+0`, record count at `+8`, and records are 42 bytes (21 signed shorts). A zero count is normal between render phases; poll briefly, deduplicate samples, and do not treat a transient zero as failure.
- Use distinctive PowerShell helper names. Names are case-insensitive, and `ri`/`RI` collides with the built-in `Remove-Item` alias.
- If the WindowsApps `pwsh.exe` launcher fails with access denied, repeat the exact safe command once through the prescribed elevated sandbox path instead of changing quoting or adding shell layers.

## Evidence practice

- Never infer a field's meaning from one live value. Correlate at least its native writer, downstream native reader, and managed consumer.
- Compare multiple objects or terrain heights and capture both stable manager state and transient render records.
- Separate observations from interpretation. Preserve raw values and show the equation that connects them to the visible result.
- Treat unexpected pointers, counts, hashes, IDs, or structure sizes fail-closed. Re-audit the baseline instead of guessing.
