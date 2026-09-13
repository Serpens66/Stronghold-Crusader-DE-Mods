# Pre-Winter Native Analysis

- Source DLL: `D:/CDesktopLink/Portable/SteamDepotDownloader/depots/3024041/24816905/Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll`
- SHA-256: `2A7BD065A00F7A14408C1586BD6F499536CF1AD97E67319EEE28E3881C870C35`
- Compared DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Ghidra: 4,473 functions, 236,000 references, 4,065 strings
- Decompilation: 4,470 completed, 3 failed; no failed function belongs to the audited Lord-attack chain
- Read-only reopen validation: successful (`VALIDATION_OK`; 4,607 temporarily reanalyzed functions, 4,065 strings, 236,006 references; changes discarded)
- Semantic comparison: 3,728 confirmed matches, 27 probable matches, 1,243 unchanged, 2,512 changed, 718 removed, 723 added

This directory is dedicated to the pre-Winter DLL. It does not alter or reuse the current hash's raw or semantic baseline. The source DLL itself was not copied into the workspace.
