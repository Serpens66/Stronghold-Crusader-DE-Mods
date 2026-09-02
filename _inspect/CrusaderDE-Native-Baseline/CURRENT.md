# Current CrusaderDE Native Baseline

Current canonical DLL SHA-256:

`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

Start with the [scan report](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/SCAN_INFO.md).

Primary reusable artifacts:

- [Rizin 0.8.1 `aaa` project](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/rizin/CrusaderDE-rizin-0.8.1-aaa.rzdb)
- [Ghidra 12.1.3 project](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/ghidra/CrusaderDE-Ghidra-12.1.3.gpr)
- [Searchable decompiler output](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/decompiled-functions.c)
- [Ghidra functions](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/functions.jsonl)
- [Ghidra references](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/xrefs.jsonl)
- [Ghidra strings](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/strings.jsonl)
- [Raw Rizin exports](./FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/exports/)

Semantic reverse-engineering baseline:

- [Semantic overview and validation](./sem/FBCB9319/SEMANTIC_INFO.md)
- [Semantic Ghidra project](./sem/FBCB9319/ghidra/CrusaderDE-Semantic.gpr)
- [SQLite database manifest](./sem/FBCB9319/DATABASE_INFO.json); the 147 MB SQLite/FTS5 file is reproducible, retained locally and intentionally excluded from Git
- [Semantic decompiler export](./sem/FBCB9319/exports/semantic-decompiled-functions.c)
- [Managed-to-native records](./sem/FBCB9319/managed/BC8B6A39/managed-native-links.jsonl)
- [XAML index](./sem/FBCB9319/resources/xaml-index.jsonl)
- [Historical version report](./diff/17F8DD4A-FBCB9319/VERSION_DIFF.md)
- [Read-only query entry point](./tools/semantic/query.ps1)
- [Curated function claims](./sem/FBCB9319/knowledge/function-claims.jsonl), [machine hook spans](./sem/FBCB9319/knowledge/hook-spans.jsonl) and [API contracts](./sem/FBCB9319/knowledge/api-contracts.jsonl)

Typical query from the workspace root:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' stats
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' claim fn-ai-get-buy-price
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' hook 0x151436
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' gaps

If the local database is absent after a fresh clone, rebuild it from the tracked exports without changing the reference manifest:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\Build-SemanticBaseline.ps1' RestoreDatabase

For future chats, link this file and require the DLL hash to be checked before using any address or conclusion from the baseline.
