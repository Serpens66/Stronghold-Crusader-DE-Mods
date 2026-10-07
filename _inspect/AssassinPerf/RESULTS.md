# Assassin production-path measurements, 2026-10-08

## Scope and reproducibility

The test harness compiles the actual production search, binary heap, request index,
route cache, suffix field, exact-route staging, public API boundary, synchronous
handoff and F4930 capture/publication methods with Roslyn. It substitutes native
memory, game managers, the native builder and passive diagnostics. Original method
bodies are preserved under `before/*.cs.txt`; the optimized bodies come from the
current sources. Identical counters at heap-operation and pop sites observe work
without changing search decisions. The separate old 900-unit simplified model is
only an algorithm comparison and is not the basis of these performance results.

Commands, from the workspace root:

```
dotnet run --project BugfixesAndQoL/tests/AssassinPathfinding.Tests -- .
dotnet run --project BugfixesAndQoL/tests/AssassinPathfinding.Tests -- . --performance
dotnet run --project BugfixesAndQoL/tests/AssassinPathfinding.Tests -- . --performance-spread
```

The final runs use two samples for each before/after pair and reverse their order
in the second sample. Other test/build processes started by this chat had finished
before measurement. Earlier exploratory CSVs are retained for provenance but are
not used below. `production-before-after-final.csv` and `production-spread-final.csv`
contain raw timings, per-request time, expanded nodes, heap operations, complete unit
scans, managed allocation bytes, GC collections by generation, cache hits, exact
publications, native-placeholder timing/call counts and the published route checksum.

The fixture allocates the real 800x800 coordinate scratch arrays and 320,800-element
tile layers. Its traversable topology is a 64x48 subset: open ground, a wall strip
with an ordinary gate-like entrance and physical climb edges, a long detour, or an
unreachable target across a full barrier. There are 1,000/5,000/10,000 live unit
records. Compact groups repeat 64 starts; distributed groups use up to 1,380 starts;
different-target commands cycle 500 targets. Independent recalculations have no
command scope. Field queries have no single-unit publication frame. Warm runs first
issue up to 512 requests in the same scope; independent recalculations retain no
cache between requests, so warm there means warmed execution only.

All requests execute serially, matching the native manager contract. Measurement
includes fixture request setup and checking the published directions/endpoints;
initial runtime arrays, unit records and synthetic native memory are allocated before
the timer/allocation sample. Native allocations are not managed allocation counts.
Offline execution uses .NET 10, not the game's Mono runtime. These are reproducible
fixture measurements of production methods, not measured total game costs or a
guarantee for maps with 320,800 searchable tiles.

SDK: 10.0.303, runtime: .NET 10.0.11, Windows x64, 32 logical processors exposed.
Both revisions are compiled with Release optimization in the fixture. Production
installation uses the existing drivers/configurations unchanged. `summary.csv`
contains the arithmetic mean of the two samples for each condition (also the median
for two values); the raw samples remain available to show timing variation.

## Measured results

Cold-cache wall/entrance topology; total milliseconds for the specified number of
serial requests, including the harness work described above:

| Requests | Independent before | Independent after | Distributed group before | Distributed group after |
| --- | ---: | ---: | ---: | ---: |
| 1,000 | 43.85 | 30.41 | 18.73 | 13.55 |
| 5,000 | 384.38 | 137.94 | 80.10 | 68.30 |
| 10,000 | 1,174.09 | 275.52 | 155.40 | 128.35 |

The independent 10,000-request run eliminates 10,000 complete scans of 10,000 unit
records. Its optimized run expands the same 3,627,828 nodes and has the same heap
work and packed directions. Compact 10,000-request cold groups take 27.71 -> 23.37 ms,
with 9,936 cache hits; their warm counterparts take 20.11 -> 16.94 ms. Distributed
cold groups have only 448 hits at 10,000 requests and expand 1,146,658 nodes. Thus the
compact-group result must not be used as a claim about 10,000 distinct new routes.

Warm wall/entrance allocation examples for 10,000 requests: independent queries
66,880,000 -> 9,120,000 bytes, compact groups 8,400,000 -> 4,640,000 bytes, field-only
queries 18,480,000 -> 13,520,000 bytes. Allocation does not improve in every workload:
different-goal groups allocate 8,028,512 -> 9,329,120 bytes, and failed single-unit
requests 2,880,000 -> 3,040,000 bytes. This is the bounded extra profile, cached-route
binding and final-publication measurement bookkeeping. The dominant repeated full
unit index and warm route/encoding/validator allocations are removed. Raw CSVs retain
GC0/GC1/GC2 for each sample; occasional collections are included, not concealed by
forcing GC outside selected samples.

For 10,000 independent requests, open terrain is 1,448.88 -> 533.44 ms, long detours
2,224.08 -> 1,243.77 ms and unreachable targets 2,275.87 -> 1,314.54 ms (warm execution,
no persistent independent cache). Unreachable requests expand 15,360,000 nodes and
perform 30,720,000 heap operations in this subset. Even after optimization, such a
burst exceeds an ordinary game-frame budget. No routing approximation or limit was
added to mask it. The fixture's native placeholder takes roughly 0.16-0.20 ms for
10,000 calls; that number is solely call/measurement overhead, not Vanilla cost.

Both final matrices passed: 240 before/after sample pairs, with identical node/heap
counts and published route checksums. Future comparisons should run the same commands
without concurrent builds/tests, and retain the native/game-runtime limitations.

The final Git review additionally widens command-level accumulated node/heap statistics
to 64 bits. Per-search counters and routing decisions remain unchanged. The CSV's
independent fixture work counters already use 64 bits; its measured totals fit 32 bits.
An actual command-statistics regression checks two Int32.MaxValue additions without
wraparound. This diagnostic-width correction followed the timing run and does not
change its workload or route evidence.

## Correctness and remaining load

The actual A* agrees with Dijkstra on 320 requests across all four topologies,
including shared suffix fields, climbing enabled/disabled, validated cache hits and
the 64-route capacity limit. Specific productive tests also verify a cheap ordinary
entrance, climbing faster than a long detour, disabled climbing, direct unit speed,
full control-player validation, owner binding, stale identity, no unit scan on direct
resolution, no allocation on warmed unqualified staging and cached encoding reuse.
The actual F4930 wrapper is tested with the productive movement-start selector:
moving units bind their next tile; speed/control-word changes, ID reuse, changed
buffer/policy and native exceptions preserve the original result. Existing nested
handoff and defensive publication contracts remain in force.

Every measured before/after pair must have identical expanded-node counts, heap
operations and published-direction checksum. No heuristic approximation, additional
search limit, cross-command route cache, native hook or parallel native access was
introduced. The unchanged D9C40 native builder is called once per request. Its CSV
timing is a placeholder's call overhead; F4930 reconstruction is also simulated.
Real Vanilla builder/reconstruction costs need the game's existing command timings
and a controlled game run; they cannot be inferred from this offline timing.

Thousands of moving units do not imply thousands of simultaneous search requests.
Warm exact-route cache hits have bounded route-validation/publication work. Thousands
of independent full searches still scale with expanded nodes, and unreachable goals
are deliberately not cached across commands. Much larger disconnected map regions
can make that workload substantially more expensive. Routing quality and budgets
remain unchanged; any approximation or quality reduction requires a new decision
from the user.

## Validation artifacts

- `assassin-regressions.log`: source-linked rules, actual methods and Dijkstra checks.
  15,978 assertions pass, including direct comparisons with Git f57dfdb02 (runtime
  contracts) and 8fe105a11 (the prior F4930 wrapper). The A* body differs only in
  binding the suffix field once; cost, fallback, edge/cached-route checks, heap,
  reconstruction-field publication and original wrapper/exception behavior are
  compared as normalized C# syntax. `final-git-review.diff` retains the full review.
- `native-assassin-tests.log`: 1,396 checks on the installed RedBird NativeX64 backend.
- `gate-testmod-tests.log`: 9,900 policy assertions, including both actual capturer adapters.
- `moat-compatibility-tests.log`: 1,477 actual manual-path/mixed-group assertions and
  35 actual NativeX64 detours; hook ownership and EAX/ZF preservation pass.
- `api-contract-tests-elevated.log`: complete APIShared contract tests pass. The first
  sandbox run could not create its nested temporary preset directory; the same test
  passed outside the sandbox. Its project reference also rebuilt the local APIShared
  package; installation is performed through the prescribed drivers.
- `shared-final-preflight.log`: current source closures, JSON/lifecycle, permanent
  hooks, XAML, CRLF and installed public-member validation. The unchanged HEAD
  `MissionLifecycleCapability.gameLocalPlayerID` access is explicitly classified by
  the existing checker; all new accesses were checked against installed metadata.
- Runtime/Fixes/gate preflight logs are retained alongside these files.

Native SHA-256 is the current baseline FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Script Extender and canonical Fixes source tags remain v2.13.1 and v1.25.1.
README files, versions, Script Extender sources and other chat changes are preserved.
No new gameplay acceptance is claimed.

## Default-off performance diagnostics (user follow-up)

`AssassinPathfindingRuntime.PerformanceDiagnosticsEnabled` is a separate compile-time
switch, default `false`. Set it to `true` and rebuild only for targeted debugging.
The detailed per-request switch remains separate. Enabling performance diagnostics
now logs each command that actually invokes the builder, rather than only slow ones.

With performance diagnostics disabled, clock reads, statistic recorder calls and
heap-operation increments are removed from the active methods by C# compilation.
The command retains its functional caches; its detail list is not allocated.
Single-unit publication uses the original staging overload without a measurement
callback. APIShared skips its initial clock read when no completion callback exists;
the null-conditional callback also skips its argument clock read. Error messages and
all identity, capacity, transition, policy and cache validations remain active.
The expanded-node counter is retained because it enforces the search budget.
Ground/climb route counts are retained because cached-route validation uses them.
Explicitly registered testmod observers remain available independently of this
production performance switch, preserving current testmods and acceptance probes.

The updated source-linked suite passes 16,229 assertions, including both switch
states against Dijkstra and prior Git contracts. Compiled IL checks verify no clock,
statistics-recorder or diagnostic staging helper calls in the disabled active path.
1,000/5,000/10,000 warm gate requests were also compared in group, independent and
field modes, twice with reversed order. Route checksums remain identical. At 10,000
cached group requests, disabling diagnostics reduced allocation from 4,640,000 to
3,600,000 bytes (104 bytes per exact publication), mean time 17.602 to 15.290 ms.
Other timings fluctuate, including tiered JIT/GC effects, and are not a universal
speedup guarantee. See `diagnostics-switch-performance.csv` for all samples.
Diagnostic cache-hit counters are deliberately zero in the disabled runs; that does
not mean functional route caches are disabled. Earlier broad matrix results above
were recorded before this follow-up with always-active performance counters.
These are offline .NET timings, not total game/native/Mono costs.

The runtime preflight, installed-member, permanent-hook, XAML and CRLF checks were
rerun before rebuilding. README and versions remain unchanged.
Both prescribed elevated build.bat drivers completed successfully and installed
the default-off diagnostics build. Package and installed DLL SHA-256 match:
APIShared 7D1AE6B6A6B8D81169D6A5B3E9893EBF31DB4EC0D02017230DFC210E4F087D02
BugfixesAndQoL 85CA728982379F266F4B7FD07599D8D0318FA264592D39B4B12402FC8F5134CC
Build logs: APIShared-diagnostics-build.log and BugfixesAndQoL-diagnostics-build.log.
The Bugfixes driver reports 0 errors; additional CS0162 warnings reflect deliberately
compiled-out diagnostic branches. Existing SDK/type warnings remain unchanged.
