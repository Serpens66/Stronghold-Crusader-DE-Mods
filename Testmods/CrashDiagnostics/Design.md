# CrashDiagnostics developer contract

This is a temporary diagnostic testmod, not a gameplay fix or a normal release.
The managed runtime is compiled only by `build.bat /devdiag /nopause /noinstall`.
An ordinary build has no active native capture, subscriptions or collector launch.
The driver defaults to no installation and references the verified published
APIShared 0.53.0 SDK selected from main. It does not rebuild that SDK or the modpack.

## Lifetime and ownership

The static managed runtime owns public APIShared observers, the LibraryLoaded
subscriber and static render/quitting callbacks. Startup destruction of the plugin
component is not shutdown. Published native handlers, callbacks and executable DLL
storage live until process exit. No game instruction is patched, no gameplay state
is written and no network or save payload is registered. Engine observers record
fixed numeric markers; formatting, hashing and file work are outside that path.

The native library starts a hidden collector and creates a file-backed shared
mapping. The collector opens the game by PID and verifies its creation FILETIME
against the mapping; PID reuse never authorizes attaching to a different process.
The initial native handshake is bounded. Later exception observations do not wait
for the collector, perform filesystem I/O, call managed/Mono code or use DbgHelp.

## ABI and on-disk format

`native/Protocol.h` is the authoritative little-endian Windows x64 ABI v1.
The standalone production testhost's `--layout` emits the compiled sizes/offsets.
Changes to these layouts require a format version change and parser updates.

- `CD_GetAbiVersion`: must equal the managed consumer's supported version.
- `CD_Initialize`: session directory, collector executable, BepInEx log,
  Unity Player log and existing extender crash directory (UTF-16).
- `CD_RegisterImage`: image base, image size, UTF-8 name and disk SHA-256.
- `CD_PublishContext`: bounded UTF-8 mission context JSON.
- `CD_RecordEvent`: kind plus three Int64 values; no per-call string formatting.
- `CD_MarkShutdown`: genuine managed shutdown observation only.
- `CD_GetStatus`: active VEH, live ready collector, Mono profiler and initial-scan
  completion flags. Raw capture can remain available after collector failure.
- `CD_Bootstrap`: bounded initial Mono-code lookup from the normal Unity callback;
  never from an exception handler and never forces compilation or method execution.

The shared file contains a fixed header, a 4096-entry trail ring, an 8192-entry
code-map ring and sixteen 1 MiB exception slots. Records publish a matching
sequence/commit pair after copying. Readers check the pair before and after reading;
partial records and overwritten generations are not interpreted as complete data.
Code-map and trail journals preserve additional observed history within their limits.
New code-map journals use `CDMAPS2`/version2/width0 followed by a 64-byte
numeric/length header and exact UTF-8 name/image/hash bytes per record; native
shared-memory ABI1 and fixed-width trail journals are unchanged. The analyzer also
reads historical fixed-width MAPS1 captures. Malformed optional journals are marked
unavailable without discarding independently valid emergency captures.
The code-map journal reserves 32 MiB for method names/images/lifetime invalidations
and 8 MiB for generic buffers/chunk creation (40 MiB total); the trail journal is
limited to 8 MiB. Saturating the generic bucket does not stop method-name capture.
Policy omissions and actual producer/ring gaps are reported separately.

An exception slot contains the original EXCEPTION_RECORD and CONTEXT, a copied
mission-context snapshot, typed memory-range descriptors and raw copied bytes.
Only `payloadBytes`, committed range counts and validated offsets are meaningful.
Stack-code candidates are pointer heuristics, never a proven stack unwind.

Address resolution must consider capture time, map publication time, sequence,
method-free and code-chunk lifetime records. A code owner is not a cause. Disk hashes
and earlier/live section hashes are distinct evidence. Never apply current native
baseline RVAs to a foreign DLL hash; unknown identities retain raw addresses.

## Diagnostic limits

Native events are first-chance observations. Subsequent activity and process exit
are separate evidence. Missing clean shutdown alone cannot prove a fatal exception.
The context is copied during the handler and is not an atomic whole-engine snapshot.
Bootstrap scans have incomplete coverage (including tiny old functions); unknown
methods remain unknown. JIT callbacks use an independent modern profiler handle,
not legacy/global replacement, method instrumentation or sampling.
Profiling is restricted to the actually tested Unity Mono binary SHA-256
`6ef9f938fe54c1d4ed6958f892596a39721b488d4ebed1a54f1644ecb230216f`.
Unknown runtime hashes keep raw capture but skip optional Mono inspection. Updating
this gate requires actual runtime/ABI tests; changing a hash to pass a check is not
validation. Chunk-destruction records carry independently tracked known spans;
unknown spans conservatively invalidate older generated-code names in the analyzer.

The session is limited to 128 MiB, with root admission reserving that maximum per
session against 512 MiB. No automatic deletion of old failure evidence. Every ring,
list, file, payload and archive limit has an explicit incomplete/capped diagnostic.
Collector log capture keeps recent bounded segments. Complete heaps and saves are
not collected. OS power loss, fail-fast, exhausted stacks and collector failure may
prevent complete evidence; generic native capture does not promise those cases.

## Validation and packaging

`tests/Run-Tests.ps1` exercises the actual DLL/collector in separate processes:
handled/unhandled AV, inaccessible pages, competing VEH, concurrency, ring wrap,
collector loss and an actual Unity Mono method. No fault is injected into the game.
The developer analyzer and adversarial format tests live under `tools/`.
Normal/diagnostic assemblies are separately inspected using Mono.Cecil.

In-game `startup`/`smoke`/`load` scenarios require an explicit run request consumed
through the public APIShared automation API. No request means no automation.
Use the existing transactional InGameTest runner; restore the original installation
and settings afterward. Real Proton and asymmetric host/client acceptance must be
recorded separately; Windows success is not a Proton or multiplayer result.

The distributed ZIP contains only the testmod and its own native components and
instructions. It contains no replacement main mods, APIShared or Script Extender.
Build provenance and compact validation results are in `obj/build.json` and
`obj/tests.json`; an unsuccessful build is not current success proof.
