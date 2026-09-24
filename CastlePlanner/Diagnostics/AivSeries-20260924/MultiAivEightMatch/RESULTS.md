# AIV eight-match series, 24 September 2026

## Provenance

- Installed `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Captured BepInEx section: `Observed/eight-match-session.log`, SHA-256 `BA7B5F1B56E37B84882908D4737F270D918083CF46E29B9A0C12275B5BC837A0`. The session reports Script Extender 2.9.0.
- Crater Lake map SHA-256: `C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`.
- Craggy Cliffs map SHA-256: `C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.
- `Observed/Inputs` contains the exact two maps and 18 distinct AIV files used by these captures, plus hash-checked inputs restored for older corpora. The two JSON corpora in `Observed/Corpus` refer to these copies and include each AIV SHA-256. `Observed/SHA256SUMS.txt` covers the raw logs, traces, inputs, corpora, and comparison reports.

## Planned series versus repeats

| Map load | Planned run | Completed castles | Native fits | Exact | Not evaluable |
| --- | --- | ---: | ---: | ---: | ---: |
| 001 | CL-Early-off | off | 17 | 17 | 0 |
| 002 | CL-Early-on | on | 17 | 8 | 9 |
| 003 | CL-Middle-off | off | 10 | 10 | 0 |
| 004 | CL-Middle-on | on | 10 | 1 | 9 |
| 005 | CC-Early-off | off | 11 | 7 | 4 |
| 006 | CC-Early-on | on | 17 | 1 | 16 |
| 007 | CC-Middle-off | off | 11 | 7 | 4 |
| 008 | CC-Middle-on | on | 21 | 1 | 20 |
| **001–008** | **planned total** | | **114** | **52** | **62** |
| 009 | CC-Middle-on repeat | on | 21 | 1 | 20 |
| 010 | CC-Middle-on repeat | on | 21 | 1 | 20 |

No Oracle mismatch or processing error occurred. The planned eight runs produced 64 complete Keep-start captures and 28 complete completed-castle captures. The two repeats add 16 and 14 respectively. Across all ten map loads, all 80 Keep-start captures and all 42 completed-castle captures report no snapshot or pointer error; 310 cell traces are archived. All recorded start constructors wrote 117 newly occupied building cells and reported no failure flag. A nonzero reason without a failure flag is not a constructor abort.

The repeats were not silently folded into the planned evidence: they contribute two exact and 40 unevaluable fit attempts. In map loads 008 and 009 the randomized candidate order for Emir's `Default 2` and `Default 7` changed, while the candidate-specific native scores and final selected AIV/rotation remained the same. This confirms the need to enumerate random start points, but it does not make every possible outcome equal.

## Native interpretation and production boundary

The current native chain is `0x94350 -> 0x54F60/0x53D00 -> 0x6D580 -> 0x77E60/0x74DA0`, with raster fit through `0x57080 -> 0x7B060`. Completed castles run later through `0x55F50 -> 0x51790`. The constructor's collision path can mark an existing building at `0x5D3A0` and remove connected records through `0xC4290 -> 0xB8310`; the completed-castle path can clear records through `0x5CD90 -> 0xC43A0/0x61FC0`. Thus the observed 117 cells are an observed result, not a universal change bound. The available `0x77E60` export still does not prove all abort branches.

Confidence is high for capture completeness and for the 52 exact planned Oracle comparisons, medium for the native semantic reconstruction of connected-record side effects, and insufficient for releasing a general later-player state set or completed-castle simulation. Later fits remain `NotEvaluable` whenever a previous possible start or completed-castle effect cannot be reconstructed for every possible outcome. The existing state and cache identity already include the selected start rotation and AIV Keep marker; an ambiguous marker or rotation is not collapsed to the single observed random outcome. No map, player ID, or variant is special-cased in the production logic.

The self-contained comparison reports are `Observed/crater-lake-C5D9906A-comparison.json` (36 exact, 18 unevaluable) and `Observed/craggy-cliffs-C46B71C9-comparison.json` (18 exact, 84 unevaluable). They include the two repeats and can be rerun without the current plugin installation.

The older corpora were also copied to `Observed/LegacyCorpus` with every
referenced map and AIV hash checked. The first recheck exposed a Dog Cage
footprint regression: the current parser marked the mapper as a trap and
therefore projected one cell instead of the native 3x3 area. This was
not present in the older saved report. The parser keeps its trap
classification for spawn filtering and uses the separately verified
three-cell building scale for AIV fit.
The Thasos captures are historical evidence for Native SHA-256
`17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`,
as documented in `Helpers/MapParser/Docs/AIV_PLACEMENT_ORACLE_COMPARISON.md`.
They are kept separate from the current `FBCB9319...` series. The
current-DLL import chain and installed Extender independently support
the 3x3 mapper scale.

| Rehydrated archive | Attempts | Exact | Not evaluable | Mismatch |
| --- | ---: | ---: | ---: | ---: |
| Prior Crater Lake full | 89 | 43 | 46 | 0 |
| Prior Crater Lake selected series | 72 | 44 | 28 | 0 |
| Prior Craggy Cliffs | 57 | 17 | 40 | 0 |
| Full-grid Craggy probe | 16 | 1 | 15 | 0 |
| Six-match Crater Lake | 41 | 13 | 28 | 0 |
| Six-match Craggy Cliffs | 28 | 8 | 20 | 0 |
| Pivot-13 regression | 20 | 11 | 9 | 0 |
| Thasos paired | 48 | 8 | 40 | 0 |
| Thasos session | 24 | 4 | 20 | 0 |

The marker validation archive adds 31 exact cases and zero mismatches.
Across these reports and the 156 current-session attempts, 582 recorded
attempts were rechecked, with 234 exact, 348 deliberately unevaluable,
and zero mismatches or errors. Several archives reuse the same native
attempts; 582 is a report-entry count, not a count of independent cases.

## Remaining evidence needed

Static work must close the `0x77E60` failure states and the connected-building write closure before a new state-set release is safe. A targeted runtime experiment is useful only for a concrete branch left open by that audit. The current series does not justify another general eight-match run or a claim that completed-castle construction is modeled.
