# CrusaderDE Version Comparison

## Result

- confirmed: 3791
- probable: 27
- unchanged: 1289
- changed: 2529
- removed: 658
- added: 660

`unchanged` and `changed` refer to safely matched function pairs. `removed` and `added` are deliberately not forced into a mapping.

## Confidence rules

- `confirmed`: same export name, a unique identical raw-byte hash, or a unique identical normalized-instruction hash with the same CFG.
- `probable`: mutual best match, similarity at least 0.92, at least 0.10 separation from the runner-up, plus at least two corroborators from strings, imports, CFG or at most 5 percent size difference.
- Everything else remains unmatched/candidate.

## Machine-readable files

- `version-matches.jsonl`: confirmed and probable one-to-one mappings
- `changed-functions.jsonl`: safely mapped functions whose raw hashes differ
- `removed-functions.jsonl`: historical functions without a safe mapping
- `added-functions.jsonl`: current functions without a safe mapping
