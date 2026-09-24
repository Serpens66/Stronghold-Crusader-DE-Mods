# AIVPlacement diagnostic archive

The former `CastlePlanner/Diagnostics` directory was moved here on 2026-09-25.
All 3,686 files were retained. The raw logs, maps, AIV files, traces and ZIP
archives were moved without changing their bytes.

Sixteen Oracle corpus JSON files contained absolute paths into the old
directory. Those paths now point here; their SHA256SUMS entries were updated.
The `Find-NaturalLinkPairs.ps1` package path and its SHA256SUMS entry were also
updated. The archive's SHA256SUMS files verify 2,985 entries: 2,953 ordinary
files and 32 start traces stored inside `StartRebuildRegression/start-traces.zip`.

CastlePlanner tests and the research documents now refer to this directory.
