# NativeKnowledge in Ghidra

The current semantic project contains a metadata-only copy of APIShared's native
field catalog and separately reviewed function-name metadata. The raw baseline and previously published analysis generations stay
immutable. No function signature, memory data definition or pointer was retyped.

## Finding the information

Open the semantic project linked by `CURRENT.md`, then open `CrusaderDE.dll`.
In the CodeBrowser, enable **Window → Data Type Manager** and expand the program's
`NativeKnowledge/<native-short-hash>` category. Open `START_HERE` first.

- `GameUnit_KnowledgeView`, `GameBuilding_KnowledgeView`,
  `GamePlayerResources_KnowledgeView` and `GameTribe_KnowledgeView` show the named
  fields at public-view-relative byte offsets. Packing is disabled so offsets are exact.
- `Fields` contains one type per permanent `nkf-…` identity. The component comment
  and type description contain the complete catalog entry: meanings, confidence,
  aliases, relationships, hash bindings, layout, read permission and evidence.
- `Alternatives` contains unions and their padded views for overlapping identities.
  These retain old halfwords beside newer DWORD interpretations and the historical
  death DWORD beside the current death WORD. They are alternatives, not conversions.
- `Guides` contains documentation carriers with the complete captured integration
  guide, German field guide and source XML API contracts in their type descriptions.
  Documentation carriers must never be applied to game memory.

Reviewed function names are also applied to the actual functions, so they appear
in **Symbol Tree → Functions**, navigation and the Decompiler. The function's
comment contains its complete `functions.json` record. The source reference is
APIShared's `src/NativeKnowledge/FUNKTIONEN.md`; this authoring data is not a runtime
invocation API. Exact source-level ABI and reconstructed role confidence remain
separate; prototypes are not changed by this import.

`prob_`, `cand_`, `historical_` and `rejected_` make uncertainty/history visible in
names. A confirmed meaning does not imply a verified storage contract. Unverified
storage is rendered using undefined bytes; its captured scalar interpretation is
retained in comments. The manager's `UnknownExtent_KnowledgeView` covers only its
described prefix and does not establish the full manager size. Arrays retain their
exact documented element counts and strides; entity IDs are not array indexes.

These views do not automatically change the Decompiler. That requires a separately
audited pointer/type assignment. Existing native header types remain independent
because their origins can differ from the public interop views.

The captured guides include dated integration observations. Those historical
statements are preserved as evidence, not current build or gameplay acceptance.

## Reproducing or refreshing

From the workspace root, use the existing pipeline:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File _inspect/CrusaderDE-Native-Baseline/tools/semantic/Build-SemanticBaseline.ps1 NativeKnowledge

Without a source argument, this uses the tracked `knowledge/native-field-catalog.json`
and rebuilds the Ghidra views without an APIShared checkout or .NET compilation.
To capture the actual current C# catalog and source documentation again, append:

    -NativeKnowledgeSourceDirectory APIShared/src/NativeKnowledge

The offline exporter compiles the actual catalog sources without loading the game
or APIShared runtime. `native-field-ghidra.json` is its deterministic Ghidra view
plan, not an independently audited native layout. Both files are manifest inputs.
Evidence validation retains original hashes, resolves changed sources against Git
history and discloses captured line ranges beyond a file's end. It never replaces
an old evidence hash simply to make validation pass.

The importer checks the program hash before mutation, updates only its owned type
inventory and refuses collisions or changed/missing owned annotations. It stores
fingerprints of its annotations in program options. Updates run in a transaction;
failures roll back. Removed or renamed owned types require an explicit migration;
they are never automatically deleted because manually applied types can also be
referenced directly by the listing or functions.
Repeated imports are checked for zero changes and a fresh read-only open verifies
all fields, comments, offsets, primitive types and array counts. Additional checks
exercise wrong-image rejection and simulated manual changes without saving them.

The stage refreshes type/function metadata, affected decompiled functions and their
direct callers, SQLite and the manifest while retaining the recorded Extender
identity and machine-analysis generations. Existing analysis packets retain their
captured names; current function names resolve to those same RVA-bound facts.
`query.ps1 search DeathMarker` also finds the exported type documentation.

Logs, timestamps and raw/analysis before-and-after snapshots are retained in the
allocated `.work/a/native-knowledge` run directory. A normal current-project Ghidra
regeneration reapplies the saved view plan; it does not require APIShared sources.

## Acceptance observation, 25 October 2026

The initial refresh exposed an index-driver mistake: the previously selected
ignored SQLite cache was inside the published analysis directory and was replaced.
The driver now writes `CrusaderDE-knowledge.sqlite` beside the maintained semantic
artifacts. The old cache was reconstructed from the byte-identical retained base
and unchanged machine-analysis inputs. Its original physical SHA-256 was **not**
recovered; this exception must not be reported as byte-identical preservation.
All 34 raw-baseline files and the other 231 historical analysis files are unchanged.
The [acceptance record](../../sem/FBCB9319/validation/native-knowledge-acceptance.json)
retains the original and reconstructed hashes, checks and limitations. This dated
repair observation does not permit future writes inside historical generations.
