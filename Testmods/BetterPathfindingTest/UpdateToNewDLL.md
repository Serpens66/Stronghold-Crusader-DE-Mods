# Native read-view and private backend contracts

This mod installs no native game hooks and writes no game memory. The existing
capture adapter remains a read-only, hash-bound diagnostic contract.

## Game capture

Reference DLL: the canonical installed `CrusaderDE.dll`, SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Resolve the current baseline through `../../_inspect/CrusaderDE-Native-Baseline/CURRENT.json`
before auditing an update. The completed managed-kernel feature audit is documented
in `../../_inspect/MoatMove/NativeAudit/PROGRESS.md`; its scope does not authorize
replacing native movement or keeping unobserved live state across changes.

| Feature | Reference address / derivation | Layout and checks | Fallback / failure |
|---|---|---|---|
| Coordinate availability | Module handle supplied by the installed Extender, RVA `0x3A11EA4` | Exactly 640,000 bytes, index `y*800+x`; module handle non-null; copied under validated engine monitor | No semantic pattern fallback is claimed for this data layout. Unknown DLL hash stops observation as `UNSUPPORTED`. |
| Direction-bit order | Same validated module, RVA `0x312620` | Eight bytes must equal `01 02 04 08 10 20 40 80` | Hash and byte checks are mandatory; mismatch stops observation. |
| Packed coordinate mapping | Public Extender `MapRowLookupTable` | Non-null tile-manager/lookup pointers; copy 2,400 `int` values; validate every available coordinate against copied packed storage | No private pointer-layout fallback. Mismatched capacities/layouts remain unsupported. |
| Tile/unit/structure layers | Existing public Extender read APIs | Validate span capacities before copying and all own arrays before interpreting; no worker retains native pointers | Transient map/capture failures end the affected session; permanent capacity contracts stop observation. |
| Engine synchronization | Loaded `EngineInterface.run` IL and reflected private monitor | Validate `Monitor.Enter`/native tick/finally-exit contract; use nonblocking `TryEnter`; no file `Assembly.Location` assumption | Unknown IL/monitor contract stops observation; contention retries up to the capture deadline. |

For a game/SDK update, re-audit availability producers, row mapping, direction order,
packed layer sizes, actual SDK unit layouts, and the complete edge/owner/height/
structure predicate. A matching address or compilation alone proves none of those.
The current signed-field/enum checks are exercised against the installed managed
assembly in both file and byte-load test processes. Existing gate policies remain
immutable per-player snapshots and must pass identity/currentness checks.

## Private navigation backend

`BetterPathfindingNative.dll` contains only owned-data algorithms. It imports no
game methods, uses no game RVAs and never receives a game pointer. ABI 3 is identified
by `bpf_contract() == 0x03003828`; request/result/statistics sizes are 56/40/128 bytes
on x64. Original request identities survive internal cohort scheduling. The native
compiler proof records source hash, output hash, compiler, Windows SDK and flags.

An ABI mismatch or missing backend is explicitly unsupported. C++ exceptions do not
cross the ABI. Memory/slice exhaustion never becomes unreachable; incomplete fields
are discarded. Normal packages exclude the native diagnostic DLL. Native context
destruction releases only privately owned buffers; the module stays process-loaded.

New integration hooks, external function calls or writable native views require a
separate feature audit and an update to this contract before implementation.
