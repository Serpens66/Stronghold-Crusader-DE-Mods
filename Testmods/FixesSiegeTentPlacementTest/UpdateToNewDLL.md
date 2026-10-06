# Native update contract

Reference SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The observer has no hooks and never calls native routines directly. Session/tick/building events and public SHCDESE interop views are used. Game IDs are1-based; spans are0-based. Layout and public-member checks run before build/registration. Unknown native hashes fail closed through DebugLogHelper; no stale-address fallback.

Siege geometry export alone reads hash-bound data: active-coordinate table RVA3A11EA4 (800*800bytes), player metric selector RVA2EA70DC+playerId*177BC (Int32; guard0..8), selected metric layer RVA5759230+selector*320800*2 (Int16). Addresses derive from full EA760/1134D0 dataflow; module .data ranges and matching native hash are mandatory. These are diagnostics, not function/patch targets. There is no semantic pattern fallback for these data layouts: on update disable only geometry export until a new full feature audit confirms the table writers/readers and interop manager bases.

Neighbor tables come through GetPackedNeighborTileDeltas; row/column tables through the actual API pointers. Tree predicate comes from public vegetation-manager pointer, organism grid and validated header1A/record9C/state50, corresponding to helper107160. No synthetic AivSystem value is ever constructed or copied. Offline constants use native .rdata RVAs2D2E54 (eight Int32 Y deltas at an eight-byte stride; verified indexed loads at EA9A3/EAAA0) and312620 (eight byte masks), verified against the reference PE before every offline build. Full producer/search audit and uncertainty are in baseline knowledge/FIXES_COMPATIBILITY.md.

Native callbacks/layout IDs, feature event reachability and observed model placements must be revalidated after native, Fixes, SHCDESE or RedBird changes. Do not convert a changed hash into a passing test. Core observation remains read-only; unsupported capture/result conditions produce INCONCLUSIVE.
