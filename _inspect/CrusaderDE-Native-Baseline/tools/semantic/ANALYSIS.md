# Offline native analysis extension

The current generation is selected by `CURRENT.json` and its database manifest.
It adds machine facts and Ghidra High P-code to the existing semantic baseline;
it does not execute the game, edit its DLL or modify the reference Ghidra project.

## Build and resume

From the workspace root, use a fresh Windows PowerShell process:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '_inspect\CrusaderDE-Native-Baseline\tools\semantic\Build-SemanticBaseline.ps1' Analysis
```

Ghidra and the external Fixes clone can require an elevated sandbox invocation.
The local user also needs access to the existing Ghidra Java/compiler cache.
The driver requires the installed Ghidra/JDK, ILSpy, .NET SDK, Python, Iced and
RedBird assemblies; it installs or updates none of them.

Each run records source and binary identities, exports with `-readOnly -noanalysis`,
checks Ghidra instruction bytes with Iced against the PE, and verifies the actual
installed RedBird selection algorithms using non-executable private fixtures.
It then validates facts and builds the new index separately. A single atomic
manifest replacement publishes the generation. The preceding database remains
available as a snapshot; no parallel legacy implementation is maintained.

An interrupted export can be resumed by repeating `Analysis`. Per-function
checkpoints are reused only when their machine profile matches. Changes to
feature selection can request a previously absent flow without redoing other
functions. An unchanged published generation is verified and reused without
rewriting it. Compiler errors cannot pass solely because Ghidra returned zero:
a matching complete-export record and full function coverage are required.

Restore an absent index from the tracked exports with the existing
`Build-SemanticBaseline.ps1 RestoreDatabase` entry point. Restoration includes
analysis tables and backend profiles and does not need the Ghidra checkpoint
directory or a running game.

## Read-only queries

```powershell
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' coverage
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' instructions 0xCEB10
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' flow 0xCEB10
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' trace 0x29650 --max-functions 128 --max-operations 10000
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' global 0x67E8400
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' feature market-price-queries
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' hookcheck 0xCEB10 --type NativeDetour --backend RedBird.Backends.NativeX64 --scheme Indirect --minimum 6 --owner hook-ai-buy-price-entry
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' hookcheck 0x13AC85 --type X64InlineHook --backend RedBird.X64 --minimum 7 --owner workshop-0x13AC85
```

Use an exact current function name or entry RVA for function queries. No fuzzy
selection of a hook destination is performed. `instructions`, `flow`, `global`
and `feature` support `--offset` and `--limit`; pages expose their total and next
offset. `feature` includes source-linked contract statements; pass
`--include-document` for the complete existing audit. Coverage distinguishes
successful flows, failures and deliberately unselected functions.

`trace` retains def-use and parameter-use edges. Cross-function argument and
return bindings require a supported callee ABI and matching recovered argument
shape. Unsupported bindings are withheld and reported. Budget frontiers and
endpoints outside the returned nodes remain visible. This does not model memory
aliasing, prove feature relevance or establish runtime reachability.

## Hookcheck contract

Hookcheck is an offline review aid, not installation approval. It reports
`statically-passed`, `conflict` or `incomplete`. It validates instruction rounding,
known incoming interior references, continuation, overlaps and conservative
register/flag liveness. NativeDetour is restricted to a function entry and an
explicit installed schema. Its self-branch widening is distinct from the
X64InlineHook minimum of 14 bytes. Unknown profiles and decoder mismatches are
rejected.

`--owner` identifies an existing span being reviewed; it exempts only that exact
span from being treated as a competing owner. Other overlaps remain conflicts.
Source-discovered hooks and event publishers are candidate evidence and require
review before concluding compatibility. Runtime hook chaining, callback ordering,
relocation, patch form, pointer slot, actual displacement and live bytes still
need the full feature/backend audit mandated by AGENTS.md. Context wrappers have
no automatic flag/SIMD preservation guarantee and are not admitted as a separate
verified hook profile by this extension.

## Evidence and limits

The feature catalog preserves existing documents and claims, without promoting
their confidence. Document coordinates may denote data, offsets, historical
addresses or function boundaries. Scope starts with exact entry candidates and
their immediate callers, then follows resolved callees to a fixed point. The
remaining upstream caller frontier is explicit. Existing declarations and new
machine checks are separate: `contractChecks` surfaces mismatches rather than
silently rewriting old evidence.

Instruction access facts come from Iced; function membership, references and
control-flow blocks come from Ghidra. Family-level liveness intentionally
overapproximates partial/conditional writes and retains unknown call/return ABI
markers. High P-code is decompiler reconstruction. Recovered switches retain
destinations without inventing case-label mappings. None of these layers is a
runtime observation or a complete Vanilla feature audit.

The manifest contains export hashes, profiles and generation identity. The
SQLite database and checkpoint work directory are reproducible local artifacts.
JSONL shards, feature/source inventories and installed-backend evidence remain
available for restoration and review. Generated text uses CRLF.

## Validation and repository size

Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File _inspect/CrusaderDE-Native-Baseline/tools/semantic/Test-NativeAnalysis.ps1 -Restore` from the workspace root. This checks existing regressions, controlled machine cases, actual read-only PowerShell query paths and a separate full database reconstruction.

JSONL exports are split into shards below 32 MiB, including chunked records for large functions. SQLite indexes, per-function checkpoints, superseded unpublished candidates and compiler caches are ignored by Git. Existing files of 50 MiB and above are explicitly excluded; their local copies are retained. Git ignore rules cannot enforce a size threshold on arbitrary new filenames, so check new artifacts before staging them. Offline compiler output lives in `.tools/native-analysis` to keep Windows paths short.

## Focused analysis and schema 6

The expanded Analysis stage derives context-sensitive slices, memory-address
observations, reusable function summaries and historical structural comparisons.
It exports both native versions with read-only Ghidra sessions and checks each
against its own PE bytes with the installed Iced assembly. No game process runs.
The existing commands and schema-5 exports remain readable; schema-6 generations
add memory, layout, contract and change tables reconstructed from tracked inputs.

Examples from the workspace root:

```powershell
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' slice 0xCEB10 --return --direction backward
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' slice 0xCEB10 --parameter 2 --direction forward
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' memory all --model unit-manager-records --field ai-state --access write --limit 20
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' switch 0xD7AD0
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' summary 0xCEB10
& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' changes all --feature workshop-idle-delay --changed-only --limit 10
```

`slice` requires one seed: `--parameter`, exact `--operation` ID, `--instruction`
RVA, or `--return`. Choose `--direction backward` for origins or `forward` for
possible consequences. Each call site has its own context; argument observations
are separate from dependence of the return value. An operation seed on a CALL
selects its result, while an instruction seed includes the invocation observation.
Caller origins require `--caller` and an exact `--call-site` into the seed function.
Use `--data-only` to exclude control dependence. Function, node, edge and slice
budgets expose frontiers and omissions. Control dependence uses the recovered
High P-code CFG; nonterminating or unresolved regions remain open.

`memory` accepts an exact function or `all`, plus optional model, field and access
filters. It reconstructs affine addresses with explicit integer widths, retained
casts, opaque unsupported operations and bounded SSA alternatives. It joins only
explicit pointer types or separately audited root/stride/offset models. A field
with an unverified extent keeps that status. Pointer loads are not silently
converted into aliases. Same-field accesses can affect different records;
indexed accesses do not establish valid game IDs, bounds or execution.

`type` requires an exact Ghidra database type path. It exposes fields, child type
paths, arrays, typedefs, enums, sizes and alignment with database authority.
Packing/layout recovery is not a proof of original source or installed interop.
`switch` separates typed numeric cases, observed raw 32-bit label pairs, defaults,
unknown labels and backing load-table observations. Unequal raw arrays are never
zipped. A null/unknown case is not a default. Shared bodies retain each case.

`summary` separates decoded writes and decompiler effects from existing audited
contracts. The two price-helper contracts require exact complete function bytes,
current audit evidence, complete flow and independent decoder verification. No
other observation is automatically promoted. `changes` compares recovered
conditions, memory expressions, arguments, returns, prototypes and machine/CFG
observations. Existing match confidence is retained. Absolute-address changes,
CFG numbering and decompiler reconstruction can produce differences; no result
proves semantic equivalence or supplies an automatic hook/field migration.

## Parallel changes

Source/document capture hashes the exact bytes that were parsed. Newly added
source files and Knowledge documents count as changes, not only edits to known
files. All queries retain hash-bound observations while reporting stale source
snapshots. Features and summaries describe captured evidence; changed mod sources
do not disable them. Hook checks retain machine results and recorded conflicts,
but report incomplete current ownership when the source inventory has changed.
A changed installed Script Extender assembly (`SHCDESE.dll`) is likewise a
snapshot change: no machine fact, backend contract or hook span is derived from
it, and `UpdateForScriptExtender` refreshes the Extender knowledge. Changes to the
native DLL, Iced or the RedBird backends still withhold all analysis results.
Mods need not be final or frozen. Publication records source drift as snapshot
metadata rather than rejecting valid Vanilla analyses. Document inventories and
reference exports are still checked to preserve concurrent baseline knowledge.
Cooperating manifest writers use one baseline publication lock and compare the
captured manifest and CURRENT identities before replacement. A conflicting writer
leaves the analysis candidate intact; repeat Analysis to capture the new state
and reuse compatible current/historical checkpoints. Knowledge files are never
rewritten by the analysis publisher. This guard does not make arbitrary external
file edits transactional; freshness checks continue to expose later changes.

A source-owned PE fixture covers signed negative cases, shared case bodies,
defaults, unequal raw arrays and backing table bytes. `Test-SwitchExport.ps1`
only edits its own hash-bound fixture project under `_inspect/NativeAnalysisFixtures`.
It never applies fixture prototypes to either game reference project and never
executes the generated PE. Full acceptance also compares all schema-6 rows with
a separate reconstruction and verifies the reference-project hashes.
