# Possible-start comparison, 24 September 2026

The installed Native DLL SHA-256 is
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The two current-session corpora use Crater Lake map SHA-256
`C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`
and Craggy Cliffs map SHA-256
`C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.
Input maps and AIVs are hash-checked against the immutable `Observed`
archive. The older Thasos corpora have their own historical Native hash.

| Corpus | Attempts | Exact | Not evaluable | Mismatch |
| --- | ---: | ---: | ---: | ---: |
| Current Crater Lake | 54 | 36 | 18 | 0 |
| Current Craggy Cliffs | 102 | 18 | 84 | 0 |
| Full-grid Craggy probe | 16 | 1 | 15 | 0 |
| Pivot-13 Crater regression | 20 | 11 | 9 | 0 |
| Prior Craggy | 57 | 17 | 40 | 0 |
| Prior Crater full | 89 | 43 | 46 | 0 |
| Prior Crater selected | 72 | 44 | 28 | 0 |
| Six-match Craggy | 28 | 8 | 20 | 0 |
| Six-match Crater | 41 | 13 | 28 | 0 |
| Historical Thasos paired | 48 | 8 | 40 | 0 |
| Historical Thasos session | 24 | 4 | 20 | 0 |
| **Total report entries** | **582** | **234** | **348** | **0** |

All reports completed without processing errors. Several corpora repeat
earlier native attempts, so 582 is not a unique-case count. The comparison
program uses each recorded actual selection. The CastlePlanner scenario-set
tests separately verify counterfactual candidates, rotations, Keep markers,
constructor failure, cache separation and state-dependent gray results.
The runtime still needs an in-game UI and trace check before the new
counterfactual release can be called validated.

The first collision guard compared raw tile owner bytes and incorrectly
marked own serialized starts on Crater Lake because their grid owner is
zero. Matching building record IDs to the selected start transform
restored all previously exact cases. The guard still rejects a foreign
record that can trigger whole-record cleanup near a rebuilt start.
