# Offline native planning and raised-state proof - 2026-10-07

Behavior fix remains disabled. No game process/library initialization was used.
Native SHA256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Installed SE2.13.0.0 SHA256: 7F7750481B392007CCCAD6D609554FBA138D8ED2781AAC54CCE0B235FC5F39F4.

## Audit and reference process

Renewed existing122-function planning/path audit and42-body topology audit before
implementation. `audit.py` retains27 complete installed native bodies and two
additional table-reader bodies, including raw trailing data, hashes, pseudocode
and disassembly in `native-evidence.txt`. The executable manifest contains the27
reference bodies only. Guards trap unsupported targets1CE80,61ED0,1A6A4C; they
are not permissive substitutes. These branches cover an unrelated visual class,
flame handling and failing security cookie, respectively. No fixture reaches them.

The separate x64 .NET executable copies PE sections into a private relocated image,
checks the whole DLL and every executable body hash, and never calls LoadLibrary,
DllMain or game startup. Managed fixture reads/writes check complete ranges.
Dispose frees this unpublished private image only. The native accesses follow the
fully audited bodies; this is not an emulator or an instruction-level memory tracer.
Unsupported native calls trap or fail the test process. Runtime hooks are unaffected.

## Productive copied planning core

`VirtualBridgePlanning.cs` is source-linked by the native tests and compiled into
the mod, with no live caller. Inputs clone complete arrays; missing building blocking
bytes are rejected, not replaced by defaults. State and partial results are owned
by the calculation. Budget exhaustion/missing component answers leave Complete=false.
No native queries, game members, public APIs, hooks or lifecycle callbacks are added.

D95E0 takes the TARGET player; R8 chooses2000/6000 queue cap. R9 is unused by this
installed body. Reproduced: eight-direction ordering, signed seed bytes, independent
building-block byte, class45/46 edge override, flag100 extra cost, flag40000000
alternation, gate-close/restore inputs and inactive-target preservation.
D9190 consumes the caller's seeds, uses the attacker's bank, increments/reset stamps
at32000, expands cardinal directions based on CURRENT flags100031, maintains the
ring queue and signed distances, caps candidates1000, and retries deferred coarse
cells if the first pass selected none. Its oracle uses copied/cached values only.

69 differential planning cases compare all320800 seed/weight values, queue tiles/
rows, distance-bank2, visit stamps, generation/head/tail/level, candidate count/order
and relevant batch/max-distance fields. Included:2000/6000 seed caps, candidate1000
cap, full initial queue wrap, signed seed extrema, generation reset, both deferred
outcomes, independent building-block state, limits, temporary gates in both
orientations/states0/1/2, and48 cases after actual native raising. Entire test run:
7616 assertions plus full-array comparisons. These are synthetic private fixtures,
not historical state reconstructed by invented values. Fixtures explicitly use a
rectangular401x800 packed table to exercise native lookup indirection; production
requires copied actual row/direction tables.

## Raised overlay and component semantics

144 cases execute actual645C0, native direction helpers andE49D0 on private data:
eight orientations, three moat-record patterns, three height patterns(0,16,17),
full alternative terrain and a sole crossing corridor. Compared exact changed
flag cells, entire direction grid, entire rebuilt PCL grid and directed crossings
in both directions. Normal countdown requires199 nonrebuild calls before rebuild;
Dirty alone is not a completed topology update.

A copied raised overlay sets40000000, isolates audited deck directions and accepts
independently rebuilt virtual components. It does NOT blanket-exclude deck cells
from planning: native seed fringe can still assign weights there, and D9190's
expansion mask does not include40000000. All48 raised planning comparisons match.
Special-seed107160/E49D0 tests cover kinds0,4,5,14,15,16 on an isolated field with
40001000. Allowed kinds>=5 except15 can retain a component despite raised flag.
Thus a flags-only PCL0 shortcut would be wrong; full special-field data is required
before a negative answer is authoritative. Existing conservative shadow policy
stays Unknown where that proof is incomplete.

## Permissions and effective Gate semantics

Native E2610 tests cover own, ally, enemy, own capturer and unrelated capturer,
both directions/modes0/1, class1/class3, third endpoint and equal-component shortcut.
Vanilla admits any nonzero capturer; the effective Gate policy admits only rightful
capturer or its ally. Existing Gate policy suite passes3840 assertions, including
real capturer adapters, invalid identities and fail-open cases. Shared spatial
coupling remains independent of roles; r_GatehouseId remains a connection-record ID.

Native storage is90 profiles and6x90 permission entries, stride168 hex. Installed
SE2.13 getter IL and canonical v2.13.0 source still use89. Internal dormant
NativePathfindingTableCopy copies current90/540 values using hash/range checks,
two-pass equality, exact content comparison and Unknown for nonboolean permissions.
It never inserts defaults or calls the erroneous per-class view. Slot89 is native
storage coverage, not a claim that enum value89 is a valid game unit type. The
short English report is updated; no SE-fork changes or external sending occurred.

## Historical coverage and integration boundary

Original keep670225 and group828296 artifacts remain immutable. Their productive
reachability replays must remain: no-cut Reachable; only703 NoRoute; all-hostile
NoRoute, both directions/modes. Group-mode hypotheses remain labeled hypotheses.
They do NOT contain complete early planning inputs. Consequently a historical
candidate/weight reproduction is not claimed by these synthetic native comparisons.

Precisely absent at the early decision boundary: pre-call seed bytes535EF90;
queue155F6C/28F3EC and root counters; pre-D9190 stamps52C2550, selected distance
bank5759230, candidate list405B4F0; native direction-offset table (the artifacts already contain packed rows/X/Y); per-tile
building identities/types/blocking byte(+300); target active/castle position;
attacker-selected bank2EA70DC; and coarse phase bytes35057AF (48-byte records).
Existing physical artifacts cover flags/PCL/edges, packed rows/X/Y, special kinds
and macro identities, not these
mutable planning inputs. No presumed historical values may replace them.

No additional game start is requested here. The now-proven copied output contract
prepares integration into existing D95E0/D9190 owners. Activation still requires a
coherent decision-time collector, exact effective Gate/role publication, complete
special-field/topology inputs, bounded prepared answers, and caller side-effect
preservation. A queued answer cannot rewrite an already consumed decision. CF-only
filtering, command vetoes and movement blocking remain insufficient.

Offline costs and allocation-heavy full-grid test timings are not live performance
claims. No additional live calculation/log volume is introduced by these dormant
classes. Existing64-record/2ms publisher and artifact completion contracts remain.

## Build environment boundary

The prescribed installed build aborted before package replacement because the local
game plugins folder contains only000shcdese; APIShared is absent. The verified
local APIShared package hash remainsDD27FEBD116D11E8E58CDEF1F8467927EEBD39473C64BC1DFA627805698F0CFE
and contains the public shared coupling methods. The build driver now forwards its
existing APISHARED_DIR override consistently to the compiler. Final packaging uses
that package and `/noinstall`; the removed game-mod installation is preserved.
Versions and READMEs are unchanged. This is not a runtime activation or game test.
