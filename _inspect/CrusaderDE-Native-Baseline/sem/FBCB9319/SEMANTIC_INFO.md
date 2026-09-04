# CrusaderDE Semantic Native Baseline

## Identity and scope

- Created: 2026-09-01, Europe/Berlin
- Current native DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Current `Assembly-CSharp.dll` SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`
- Historical native DLL SHA-256: `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`
- Script Extender commit: `171d68e155a8f98c5f8c4ee154d9af154c9a2443`
- PE image base for both native DLLs: `0x180000000`
- PDB GUID and missing Jenkins PDB path are documented in the parent `SCAN_INFO.md`.

This directory is derived from, but does not modify, the raw Ghidra/Rizin baseline. It contains no copied game DLL. Native addresses are valid only for the binary hash stored beside each record. Runtime scans, debugger attaches and game hooks were not used.

## Toolchain

- Ghidra 12.1.3 with Eclipse Temurin JDK 21.0.12.1+1
- ILSpy command-line decompiler from the installed workspace toolchain
- .NET 10.0.303 SDK
- `System.Reflection.Metadata` for direct IL and metadata analysis
- Roslyn compiler APIs from the .NET SDK for Script Extender source analysis
- AssetStudio CLI 2.4.1 for .NET 10
- External copy of `SHCDESE.Dat2XAML`; restore, intermediates and build outputs are all outside `shcde-script-extender`
- Portable Python with SQLite 3.45.3 and FTS5

Verified AssetStudio archive:

- File: `.tools/downloads/AssetStudio-net10.0-win-v2.4.1.zip`
- SHA-256: `761AF9C36AF718023CBFB1F5842A8E293F0E3C02DE15803FFD19FE98AEC2BDCE`

## Semantic Ghidra project

`ghidra/CrusaderDE-Semantic.gpr` and its matching `.rep` directory are a copy of the raw current Ghidra baseline. Only this copy was enriched.

- 9 unique direct-function AOBs and 3 confirmed curated claims received names and provenance comments.
- All 77 `CrusaderDE` exports received managed P/Invoke prototypes.
- 119 Script Extender header types were imported into the project archive.
- Ghidra exposes 259 data types after enrichment, including built-in/demangled types.
- 4,478 non-external functions were exported and decompiled again.
- Decompilation completed for 4,475 functions and failed for the same 3 explicitly recorded functions.
- 82,610 referenced global/data symbols and 142 RTTI/vtable-related symbols were exported.

Naming rules:

- `confirmed`: exact PE export or unique Script Extender direct-function AOB.
- `probable`: reserved for a well-supported, non-unique semantic inference and visibly prefixed `prob_` if applied.
- `candidate`: discovery or topic evidence that is retained in exports/database but does not rename Ghidra symbols.

Curated semantic claims now separate function identity, semantic name and ABI confidence from version-match confidence. Only a curated semantic name whose own confidence is `confirmed` is eligible for a Ghidra rename. Probable and candidate claims remain queryable without changing symbols. Topic classifications never rename functions.

## Curated semantic knowledge

The tracked `knowledge` directory is the human-audited layer between raw/semantic Ghidra exports and SQLite. Its JSONL records are hash-bound, retain independent evidence chains and counter-evidence, and are validated before Ghidra or index generation.

- Function claims: 26
- Evidence records: 55
- Fully specified hook spans: 3
- Active API boundary contracts: 0
- Confirmed curated labels: 3

The function claims cover AI sleep synchronization, emergency and targeted demolition, AIV build/placement, AI market prices, plague creation/update/healer selection, Monk movement, quarry placement candidates and gatehouse automation. The market buy/sell helpers and targeted AI hovel deletion currently meet the stricter `confirmed` semantic threshold; the remaining reconstructed names stay `probable`.

The additional Chore layer is self-contained under `knowledge`: 43 opcode records, 8 packet/scheduling/safety contracts, 2 normalized runtime observations and 6 hashed evidence records. Its compact peer traces retain 77 non-redundant original log lines plus full-source hashes and line numbers; repeated equivalent Opcode-111 probe lines are intentionally omitted. Current-build semantics are promoted only where the `FBCB...1E2` handler, producer, payload or version evidence supports them.

The previously observed `GatehouseQueryEventArgs.UnitId` index mismatch is deliberately not published as an active API contract because the Script Extender author has confirmed that it will be corrected upstream. Any temporary mod compatibility handling must therefore be reassessed against the installed Script Extender version rather than treated as a lasting baseline contract.

## Script Extender knowledge

The Roslyn extractor scanned 460 source/header files and records the Git commit, relative path, source line and SHA-256 source-file hash with each derived fact.

- AOB definitions: 332
- Delegate signatures: 135
- Struct/enum declarations: 81
- Structured type fields/properties: 9,389
- Structured VTable members: 345

AOB results for each native DLL:

- 330 patterns produced exactly one match.
- 2 patterns produced two matches each; their four match records remain non-unique and were not applied.
- No pattern produced zero matches.
- 9 patterns were statically recognized as direct native functions.
- The remaining 321 patterns retain `unknown` resolution kind and were not automatically named or typed.

The conservative resolution-kind result is intentional: an exact byte match is not by itself evidence that the address is a function entry, global address, indirect target or VTable.

## Managed-to-native map

The current `Assembly-CSharp.dll` was freshly decompiled under `managed/BC8B...FD789/decompiled`. The metadata analyzer reads IL directly rather than parsing decompiler text.

- Managed methods: 8,138
- Managed call instructions: 72,559
- P/Invokes across all modules: 84
- `CrusaderDE` P/Invokes: 77
- Resolved one-to-one against native exports: 77 of 77
- Native PE exports: 78, consisting of those 77 names plus the separate `entry` export
- Direct and transitive managed-to-native callchain records: 56,123
- Maximum retained call distance: 16 managed edges

Tokens, IL offsets, opcodes and complete call paths are retained in JSONL and SQLite. Exact names/tokens form confirmed links; fuzzy text similarity never does.

## Vanilla XAML resources

AssetStudio initially showed thread-safety corruption when assets were processed in parallel. The final baseline therefore re-extracts every known resource by exact escaped name into a fresh staging directory, one process at a time.

- Raw MonoBehaviour resources checked: 114
- Resources containing XAML: 105
- Non-XAML MonoBehaviours: 9
- XML-valid XAML files: 105 of 105
- Binding records: 7,860
- Exact managed-name links: 7 unique plus 4 ambiguous
- Unresolved binding records: 7,849

The external Dat2XAML copy has two documented container fixes: it stops before appended binary dependency tables and removes the isolated malformed prefix in `OST_Pings`. The Script Extender source remains untouched. Unresolved bindings remain explicit because most binding paths name properties or data-context members, not methods.

## Search database

`CrusaderDE-semantic.sqlite` is a read-only-oriented SQLite/FTS5 index. It is a reproducible local artifact and is intentionally excluded from Git together with its SQLite sidecars. `DATABASE_INFO.json` retains its reference SHA-256, size, SQLite/schema version, identities, logical counts and the hashes of every required tracked input. Its principal counts are:

| Table/domain | Records |
|---|---:|
| Native functions, current plus historical | 8,954 |
| Curated function claims / evidence | 26 / 55 |
| Validated hook spans / active API contracts | 3 / 0 |
| Chore opcodes / contracts / observations / evidence | 43 / 8 / 2 / 6 |
| Function-to-global/data references, current plus historical | 79,264 |
| Native call edges | 23,692 |
| Raw current Xrefs | 236,382 |
| Defined strings | 4,248 |
| Referenced globals/data symbols | 82,610 |
| Managed methods | 8,138 |
| Managed-to-native callchains | 56,123 |
| AOB match records | 668 |
| Script Extender types / fields / VTable members | 81 / 9,389 / 345 |
| XAML resources | 105 |
| Version matches | 3,818 |

Primary artifact integrity:

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| Local `CrusaderDE-semantic.sqlite` (reference recorded in `DATABASE_INFO.json`) | 154,857,472 | `21384620E1F9A084A3177BF308FD98EB08308942C17E8891B7224045864724B3` |
| `exports/semantic-decompiled-functions.c` | 10,856,822 | `012C77F892EB927144AC857EE1C2AF690D61E371066BAF307EBAB3108F92B291` |
| Current `semantic-functions.jsonl` | 3,574,616 | `58418AE6217520197158E41BC46E37ED16CAD230639393C684D099380696B447` |
| Historical `semantic-functions.jsonl` | 3,553,531 | `9541067177CDAD5EAC47572CA32B23774526A156961F42FFBDC05908BD3355D7` |

The semantic Ghidra project contains 10 files totaling 101,811,258 bytes. The historical Ghidra project contains 10 files totaling 97,354,813 bytes. Their internal project databases are validated by fresh read-only opens rather than treated as single-file archives.

Deterministic subsystem classifications currently cover:

- AI: 65 functions
- Buildings: 57
- Map logic: 139
- Network: 73
- Sound: 88
- Trade: 59
- UI: 55
- Units: 65

Evidence is stored with every classification. CamelCase is split deterministically before whole-term matching; no fuzzy classifier is used.

## Query interface

Run these commands from the workspace root:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' stats
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' search operator
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function DLL_PreInitMap_Editor
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' callers DLL_PreInitMap_Editor
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' callees 0x85E90
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' managed PreInitMap
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' claim fn-ai-get-buy-price
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' hook 0x151436
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' contract UnitId
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' gaps
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore list
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore opcode 120
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore function 0x23990
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore contract chore-sync-barrier
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore evidence runtime-peer-b
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore gaps
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' diff 17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2

After a fresh clone, recreate a missing local database from the tracked exports. This validates all input hashes and logical database contents without rewriting the reference manifest:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\Build-SemanticBaseline.ps1' RestoreDatabase

`function` returns exported symbol confidence and all matching curated claim dimensions, evidence, ABI, fields, hook spans, pseudocode, strings, data/global references, callers, callees, managed callers, referenced types and version match. `gaps` reports functions whose version identity is stronger than their semantic name.

## Historical comparison

The historical DLL was independently imported with the same stable Ghidra default analyzers and is explicitly stored under the two-hash comparison directory.

- Current functions: 4,478
- Historical functions: 4,476
- Confirmed matches: 3,791
- Probable matches: 27
- Changed matched functions: 2,529
- Removed/unmatched historical functions: 658
- Added/unmatched current functions: 660

The generated `semantic-reference-diff.jsonl` records 2,476 safely matched functions whose global/data references or referenced strings differ between the two versions.

Confirmed matches require the same export name, a unique raw-byte hash, or a unique normalized-instruction hash with the same CFG. Probable matches must be mutual best matches with score at least 0.92, at least 0.10 separation from the runner-up and at least two corroborators from strings, imports, CFG or a maximum 5 percent size difference. All other pairs remain unmatched/candidate.

See the machine-readable records and `VERSION_DIFF.md` under `../../diff/17F8DD4A-FBCB9319/`.

## Reproduction and validation

The reusable pipeline is `tools/semantic/Build-SemanticBaseline.ps1`. Derived directories use collision-checked eight-character hash keys (`sem/FBCB9319`, `managed/BC8B6A39` and `diff/17F8DD4A-FBCB9319`) to keep Windows and GitHub paths short. `IDENTITY.json` stores the complete hashes; validation fails if a short key belongs to another full hash or if a generated file path exceeds 240 characters. The stages are `Knowledge`, `Curated`, `Resources`, `GhidraExports`, `Index`, `RestoreDatabase`, `Validate` and `All`. `Curated` checks claim thresholds, source hashes, function fingerprints, hook bytes/instruction coverage and machine contracts, then produces the confirmed-only label input and validation reports. A canonical `Index` run atomically builds schema-v2 SQLite and refreshes `DATABASE_INFO.json` plus the root `CURRENT.json`; `RestoreDatabase` only consumes and validates the tracked manifest/exports and curated inputs. Source-analysis stages check all three binary hashes and the Script Extender commit. `Validate` refreshes its reports and performs fail-closed checks:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\Build-SemanticBaseline.ps1' Validate

Fresh read-only Ghidra validation results:

- Current semantic project: 4,577 total functions, 4,248 strings, 236,382 Xrefs.
- Historical project: 4,575 total functions, 4,235 strings, 236,245 Xrefs.
- Both use image base `0x180000000` and reopened successfully without analysis or save.

The final fail-closed validation confirms:

- all three binary hashes;
- raw baseline unchanged against its before-snapshot;
- Script Extender byte-identical against its before-snapshot;
- 77 of 77 `CrusaderDE` P/Invokes resolved;
- all 105 XAML files parse as XML;
- 3,818 version matches are one-to-one and satisfy their confidence thresholds;
- all 14 curated claims, 31 evidence records and three machine hookspan contracts are hash-consistent;
- caller ABI observations are mutually compatible and confirmed names alone enter the Ghidra label stream;
- JSON/JSONL parsing, VA/RVA relations and PE image ranges;
- SQLite `integrity_check`, foreign-key check and FTS5 search.
- CRLF audit of 397 text files with zero naked-LF files.
- Baseline-wide path audit with a 240-character fail-closed limit: zero violations and a maximum absolute path length of 233 characters in this workspace (reduced from 308 before path compaction).

The machine-readable result is `validation/validation-report.json`. Individual Ghidra logs and process timings remain under `logs/` and the comparison `logs/` directory.

The text audit found 13,837 literal backslash-r/backslash-n escape sequences in 17 files. They are intentional: 13,768 encode preserved Roslyn source declarations/contexts inside JSON strings, while the remaining 69 occur in raw extracted strings, documentation and newline-handling literals in the reusable C#, Java and Python tools. They are not file line endings. Details are retained in `validation/text-audit.json`.

## Known limits

- No matching native PDB or MAP file is available.
- Only nine AOBs were safe to treat automatically as direct function entries; all other resolution modes remain explicit.
- Ghidra and the Script Extender type declarations contain useful evidence but do not prove every runtime layout. Only imported header layouts and explicitly extracted offsets/slots should be treated as structure evidence.
- Managed transitive callchains show reachability, not that every path executes in a particular game mode.
- XAML bindings are data-context-sensitive; unresolved and ambiguous records are retained rather than guessed.
- No runtime behavior, timing, heap state or dynamic dispatch target was observed because runtime scans were excluded.
- Curated runtime evidence refers to focused, separately documented ExtraFeatures audits; the baseline pipeline itself still performs no live game hooks.
