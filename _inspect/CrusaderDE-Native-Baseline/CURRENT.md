# Current CrusaderDE Native Baseline

Current canonical DLL SHA-256:

`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

For future chats, link this file and require the DLL hash to be checked before using any address or conclusion from the baseline. [CURRENT.json](./CURRENT.json) is the machine-readable identity.

## Start here

1. **Vanilla knowledge:** [Vanilla index](./sem/FBCB9319/knowledge/vanilla/INDEX.md). Nine current-state topic summaries (pathfinding, fortifications, units, economy, AI, session, presentation, network, memory layouts), each linking its evidence.
2. **Our mods:** [Mod index](./sem/FBCB9319/knowledge/mods/INDEX.md). What our mods, testmods and external components change per topic, with acceptance status.
3. **Details and proofs:** the audit documents in [knowledge/](./sem/FBCB9319/knowledge/). They are dated, append-only evidence records referenced by line from the field catalog and the analysis dossiers. Every document belongs to one topic ([topics.json](./sem/FBCB9319/knowledge/topics.json)) and one scope ([document-scopes.json](./sem/FBCB9319/knowledge/document-scopes.json)).
4. **Anything else:** the query tool below.

## Vanilla versus mod knowledge

- The raw baseline, the semantic exports, the Ghidra/Rizin projects, the managed and XAML exports and the version diff describe only the Vanilla game files.
- `knowledge/vanilla/` contains only Vanilla facts. `knowledge/mods/` contains only our mods and external components.
- Audit documents: each title ends with `[Scope: ...]`. In mixed documents, headings ending with `[mod integration]` describe our mods; in mod and external documents, headings ending with `[Vanilla facts]` hold reusable Vanilla facts. Documents whose exact bytes are pinned by validators stay unmarked; they are listed under `markers.untagged` in `document-scopes.json`.
- Mod, testmod and external-component documents are not Vanilla evidence by themselves.

## Vanilla raw and semantic artifacts (Vanilla only)

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
- [SQLite database manifest](./sem/FBCB9319/DATABASE_INFO.json); the 148 MB SQLite/FTS5 file is reproducible, retained locally and intentionally excluded from Git
- [Semantic decompiler export](./sem/FBCB9319/exports/semantic-decompiled-functions.c)
- [Managed-to-native records](./sem/FBCB9319/managed/BC8B6A39/managed-native-links.jsonl)
- [XAML index](./sem/FBCB9319/resources/xaml-index.jsonl)
- [Historical version report](./diff/17F8DD4A-FBCB9319/VERSION_DIFF.md)
- [Read-only query entry point](./tools/semantic/query.ps1)
- [Curated function claims](./sem/FBCB9319/knowledge/function-claims.jsonl), [machine hook spans](./sem/FBCB9319/knowledge/hook-spans.jsonl) and [API contracts](./sem/FBCB9319/knowledge/api-contracts.jsonl)
- [Standalone Chore reconstruction](./sem/FBCB9319/knowledge/CHORE_SYSTEM.md) with structured [opcodes](./sem/FBCB9319/knowledge/chore-opcodes.jsonl), [contracts](./sem/FBCB9319/knowledge/chore-contracts.jsonl), [observations](./sem/FBCB9319/knowledge/chore-observations.jsonl) and [evidence](./sem/FBCB9319/knowledge/chore-evidence.jsonl)
- [Native field catalog](./sem/FBCB9319/knowledge/native-field-catalog.json), summarized in [memory layouts](./sem/FBCB9319/knowledge/vanilla/memory.md)

## Knowledge tooling

- [NativeKnowledge in Ghidra](./tools/semantic/NATIVE_KNOWLEDGE.md): 88 fields and four reviewed function records in the published catalog snapshot, including the building-height-offset and unit-standing-height helpers.
- `tools/semantic/knowledge_lint.py`: checks topic assignment, links, anchors, mojibake and CRLF of the curated layer; `--coverage` lists coordinates of a topic's audit documents that its summary does not mention.
- `tools/semantic/repair_mojibake.py check|repair <paths>`: finds and reverses repeated UTF-8/cp1252 mis-encoding.

## Queries

Typical query from the workspace root:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' stats
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function <Name|VA|RVA>
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' search <Text>
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' claim fn-ai-get-buy-price
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' hook 0x151436
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' gaps
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore opcode 120
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore gaps

If the local database is absent after a fresh clone, rebuild it from the tracked exports without changing the reference manifest:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\Build-SemanticBaseline.ps1' RestoreDatabase

## Offline machine and data-flow analysis

The Analysis stage adds hash-bound instruction facts, conservative liveness, selected High P-code, feature dossiers and installed-backend hook checks. See [operation and limitations](./tools/semantic/ANALYSIS.md). Queries: `instructions`, `flow`, `trace`, `global`, `feature`, `hookcheck`, `coverage`. The current database manifest selects the published schema-6 analysis generation; failed flows and unresolved audit boundaries remain explicit.

Published coverage (generation `g72CA4AA9715C`, 2026-10-27, Script Extender 2.14.3 knowledge): 4,478 native functions, 436,211 independently verified instructions, 2,752 complete selected High P-code flows and 99 existing audit dossiers. Three decompiler failures remain explicit; 1,723 functions have no selected deep flow. [Acceptance record](./sem/FBCB9319/analysis/g72CA4AA9715C/acceptance.json) confirms fresh Windows PowerShell queries, unchanged reference projects and exact schema-6 analysis reconstruction.

Additional offline queries: `slice`, `memory`, `switch`, `summary`, `type` and `changes`. They expose bounded value slices, 79,169 reconstructed memory accesses, 932 recovered layouts and 3,818 historical function comparisons. Reconstruction and audited contracts remain separate; partial flows, aliasing and decompiler diagnostics are explicit. See [usage and limits](./tools/semantic/ANALYSIS.md).

Mod sources are mutable integration snapshots. Later edits do not invalidate Vanilla machine facts or block feature and summary queries. Hook checks retain machine evidence but report incomplete current ownership when the source inventory has changed. A newly installed Script Extender assembly is reported the same way and needs `Build-SemanticBaseline.ps1 UpdateForScriptExtender`, not a new analysis generation. Concurrent baseline knowledge is preserved by publication guards.

Known deviation (2026-11-27): five audit documents were repaired from repeated UTF-8/cp1252 mis-encoding (`DRAWBRIDGE_AI_DECISIONS.md` line 41 shrank from 14 million characters back to its original 556; line counts unchanged). The published generation still holds their old hashes and reports them as "audit changed" until the next generation. Older analysis generations and large raw captures were removed from the working tree the same day; tracked parts remain in Git history (last complete in commit `67f4de243`).
