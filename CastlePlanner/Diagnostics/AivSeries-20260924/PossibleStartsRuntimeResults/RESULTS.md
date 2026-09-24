# Possible-start runtime series, 24 September 2026

## Provenance

- Native `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Captured original bytes in `session.log.original.bin` SHA-256: `2C9396F6FE25F2A359251F7561F621B22311400D98D7D55991BD02398D26B373`. The imported corpora bind to this hash.
- CRLF-normalized readable `session.log` SHA-256: `765D068FFE134C73D1D4B69F4C6C61B378382E3DB5E451AFE9639E5AC6BEAB99`.
- Crater Lake SHA-256: `C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`.
- Craggy Cliffs SHA-256: `C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.
- `Inputs` contains hash-checked copies of both maps and every AIV used by the imported cases. The imported corpora point only to these workspace copies.
- `SHA256SUMS.txt` covers the archived log, config, inputs, traces, corpora, reports and this summary.

## Results

| Map load | Run | Completed castles | Native fits | Exact | Not evaluable | Mismatch |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| 001 | CL-Early-off | off | 17 | 17 | 0 | 0 |
| 002 | CL-Early-on | on | 17 | 8 | 9 | 0 |
| 003 | CL-Middle-off | off | 10 | 10 | 0 | 0 |
| 004 | CC-Early-off | off | 11 | 7 | 4 | 0 |
| 005 | CC-Middle-off | off | 11 | 7 | 4 | 0 |
| 006 | CC-Middle-on | on | 21 | 1 | 20 | 0 |
| **001–006** | **planned** | | **87** | **50** | **37** | **0** |
| 007–008 | CC-Middle-on repeats | on | 42 | 2 | 40 | 0 |
| **all** | | | **129** | **52** | **77** | **0** |

All six preset launches and completions were logged. The two later map loads did not advance the already completed series and are kept as repeats. There were 64 complete Keep-start traces, 28 complete completed-castle traces, 129 cell traces and no snapshot error. The 256 files in `CellTraces` comprise 129 candidate cell traces and 127 live-building grids; the detector omits a live-building-grid file when its captured occupied-cell list is empty.

In the paired Crater Lake runs all 17 common Native fits match with completed castles off and on. In the paired Craggy Cliffs runs only five of 11 common fits match; six differ. For Emir `Default 7` at 0 degrees, blocked cells rise from zero to 252. Using the map's fixed diamond tile indexing, all 252 changed result-grid cells fall inside tile layers actually written by the earlier captured completed-castle frames. The recorded execution proves an effect on that match, not the effects of every possible earlier choice.

The recorded `CC-Early-off` start path changed 552 distinct fit-layer tile IDs across the three AI starts before Emir. Emir `Default 8` at 0 degrees read none of those IDs. Its possible unselected earlier starts and the connected-record deletion path remain unbounded, so this observation does not justify removing a gray result.

The 32 cell-trace warnings followed a diagnostic counting error: the raster scan can reject a cell before calling the tile validator. In all 129 traces, `nativeBlockedCells = validatorBlockedCells + evaluatedCells - validatorCalls`. The detector now checks that identity and records the pre-validator count. No Native score mismatch was inferred from these warnings.

Confidence is high for captured scores, counts, hashes and actual read/write intersections. Confidence remains insufficient for all counterfactual constructor outcomes or the complete sequential completed-castle effects. The production boundary remains `NotEvaluable` for these cases.
