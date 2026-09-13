# FastNative implementation contract

Version remains 0.1.1; target SE 2.6.0. This is a comparison backend, not a claim of improved game performance.

## Native provenance and boundary

Canonical CrusaderDE SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Baseline entry DA590 (candidate name, directly reviewed body) spans DA590..DAA4C exclusive. The full feature audit remains under `_inspect/MoatMove/NativeAudit`; it covers command/region gates, search, formation, attacks/work, publication, consumption and terrain writers. No global native patch is required by this backend.

The private copy preserves DA590's FIFO first-visit expansion, queue continuation and eight neighbor branches. It uses rectangular private node IDs, not packed native tile IDs. The managed mask producer performs that conversion using the existing traversal predicate. All distances are widened to 32 bits; stamps remain 16 bits with private generation 1 and touched-cell reset. Diagonal distance increments become one. This preserves long ground-reachability proofs independently of the 2,000-direction output limit.

## Replacement machine contract (before executable allocation)

- Windows x64 signature: context RCX, root X EDX, root Y R8D, target X R9D, target Y/absolute expansion limit/continuation in stack arguments 5..7. Caller always supplies continuation. Initialization and multiple roots belong to the private field wrapper.
- Preserve original prologue, stack frame and epilogues. RBX, RSI, RDI, RBP, R12..R15 retain their original save/restore contracts; no new scratch register is introduced. Root coordinates are validated before entering the copy.
- DA64A redirects to DA6AF. DA652..DA6AF becomes unreachable NOPs, removing the global generation reset and native memset call. No external call is retained.
- Every image-base LEA in reachable code becomes MOV of the same destination register from RBX, the private slab base. MOV preserves flags. Every global grid/table displacement is mapped to an explicit private slab range. Queue and state displacements relative to RBX are also mapped. Reject unknown global, RIP-relative, or external branch references.
- Distance loads/stores use 32-bit operands and scale 4 instead of 2, including the +/- one-neighbor displacement. DA778 increments R9D instead of R9W. DA87A/DA8C3/DA959 copy R9D without the extra diagonal increment; DA9A1 becomes NOP. These flags have no live consumer: neighbor branches test stamps/mask bits explicitly. Queue Y and stamp writes remain 16-bit.
- DA710 sets the field-only target sentinel to -1 instead of zero. Calls use target X=-1 so tile zero remains searchable. The wrapper determines Found from private distances after each bounded slice.
- Native stamp reads precede direction tests. Distance/stamp arrays therefore include a width+1 guard on both sides. Invalid outer directions are cleared during mask preparation; no invalid neighbor can be enqueued. Every legal node is enqueued once, so queue capacity equals node count.
- All internal branch destinations retain original instruction identities during Iced block relocation. Decode and validate the final code before executable allocation; reject residual external data/control references. Published executable code is rooted for process lifetime.

## Shared behavior

FastNative selects Fast behavior with a distinct field factory. Scheduling, exact attack/work targets, terminal fill handling, patrol/queue, identity checks, rollback, save data and final live-edge validation remain shared. Mask preparation is deterministic and separately counted; it consumes preparation work allowance before native expansion. No managed callback occurs in native expansion. UI fields remain separate from simulation fields.

The simulation pool accounts for actual private slab sizes, reserves its candidate field, and limits concurrent queued searches so ground/friendly leases cannot occupy every slot needed by synchronous publication. No own weighted or managed large-search fallback is allowed in FastNative.

The implemented 800x800 private slab occupies 9,004,880 bytes. Six simulation pool entries plus one candidate field total 63,034,160 bytes, below 64 MiB. Two pending groups can own four entries, leaving two for synchronous publication. Cursor fields and shared preparation masks are separate and reported separately. Each preparation cell consumes one deterministic work unit; therefore the existing 8,192 limit also bounds mask preparation, while native expansions never exceed that limit. Changed player transitions conservatively reset all of that player's native fields; commands retain their leases. This avoids using a stale completed mask in a previously unexplored portion of a retained field.

## Executed comparison, 2026-09-13

The actual relocated function passed directed reference, multiple-root, guard-memory, interleaving, budget and reconstruction fixtures. A directed snake retained a ground distance of 639,999 and independently reported TooLong for the packed route. Existing runtime fixtures were also executed with the native backend: 3,769 native calls, 6,400,000 mask preparation cells (approximately 604 ms aggregate in that fixture run). Installed-assembly source compilation, original Fast/Precise fixtures and native hook contracts passed before installation.

An open 160x160 synthetic field with shared starts measured approximately 1.1 ms for managed Fast versus 7.2 ms for FastNative for 680 routes in one sample. Timings include mask preparation and packed reconstruction, but exclude backend construction and allocation. They are not game benchmarks. The native expansion alone is inexpensive; mask preparation and the eight-neighbor downhill reconstruction can dominate. FastNative is consequently an experimental comparison option, not a recommended performance replacement yet. No default or existing config is switched to it.

Reproduction: `dotnet run --project Testmods/MoatMove/tests/MoatMove.Tests.csproj -- . --native-only` exercises the emitted function; `--runtime-native` runs existing runtime fixtures with an explicit native-call assertion. The complete emitted code is retained as `_inspect/MoatMove/fast-native-kernel.bin`; the native hook inventory and runtime results are retained beside it. Game acceptance remains outstanding, including FastNative with APIShared.

## Validation and acceptance

Required: execute the generated native function against an independent directed BFS reference, compare sliced/full runs, multiple roots, long paths, masks and unchanged input/native state; run existing Fast/Precise and command fixtures; compile against installed assemblies and check original hooks. Report preparation work/time separately from expansions and route reconstruction. In-game acceptance includes FastNative alone and with APIShared, especially attacks, work, patrol, queue, cancellation and save/map transitions.
