# Native contracts of the disposable experiments

Reference DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
These are existing contracts transferred from AIBuildDiagnoseTest, not new native
findings. Canonical evidence: the matching `AI_WOODCUTTER_BUILD.md` in the native
baseline selected through `CURRENT.json`.

| Owner / use | Reference address or layout | Validation and update behavior |
| --- | --- | --- |
| Fixes comparison | Candidate branch RVA `0x58B12`, 24 Vanilla bytes | Matching native hash and full byte sequence required. Read only. No pattern fallback: the bytes and downstream coarse-field semantics form one version-bound experiment. A mismatch disables active probes and logs the reason. |
| Farm-contract route probe | Function RVA `0xC3BF0`, manager RVA `0x64CCBB0` | Existing audited `(manager, player, route mode, x, y)` call, including mode 3 for woodsmen. Enabled only with the matching native hash. No cross-version fallback for the manager/layout. The general mod owns the observation detour at the same function; this companion only calls the validated service. |
| Nearby copy overlay | AIV state `+0x5B834 + (coarseX*160+coarseY)*0x30` | The existing native caller supplies state. Require player/attempt identity, named copy, exact normal calibration and copied 16-byte coarse-cell agreement. Only foreign count 0 to 1 is changed, then every touched byte is restored after the single original call. All subsequent evidence and footprint checks remain authoritative. |
| Path-generation wait | Pathing root RVA `0x60AD660`, generation offset `+0x74` | Hash-bound read only; unavailable reads return unknown and log failure. Farm replacement waits for a later generation instead of assuming one tick is sufficient. |
| Placement and demolition | Script Extender public CreatePrefab/DeleteBuildingSafe wrappers | No companion-owned RVA calls or hooks. Preserve existing exact placement parameters and observe real cleanup/spawn/tile evidence before continuing. |

On game updates, verify the installed DLL identity, original caller/control flow,
coarse layout and signedness, pathing root, route ABI, placement parameters and
demolition terminal states. Update the baseline and source guards together before
enabling experiments. A function-entry pattern alone is insufficient to validate
these data layouts. Never patch or remove a published hook to run a comparison.
