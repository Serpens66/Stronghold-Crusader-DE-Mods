# PlagueHeightTest native contract

Version 0.1.0; native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Audit and isolated native execution checks: 2026-10-14. Current semantic baseline
selected through CURRENT.json; local Extender commit 5908e1f12437deb7ef5f5b1dc2d64e301178fba3.
Fixes source compatibility review: local canonical HEAD f91968217e5ece7901df45a7d90e01fb48be2b96.
No gameplay or multiplayer acceptance is claimed by these static/fixture checks.

## Feature flow

Natural herd creation D17D0 and catapult impact inside 9C730 produce Disease (22)
through 9B2B0. Creation uses world coordinates (eight subtiles per tile), initializes
the E8-byte projectile record and its tile registration. 9F960 activates and moves
clouds; registration 9C730 visits Vanilla's tile-linked unit candidates on the center,
eight neighbors and the outer ring. Radius arguments 0/1/2 select Vanilla damage
400/200/150 before existing player resistance; these values are not replaced.
The unit head grid 4C559B0 and next-unit field +8FA also serve roof occupancy
queries (198C40), so no second unit scan is needed for tower/keep occupants.

192750's Disease branch first rejects dead units, then walls/elevated tiles and
building types 40..47/74..78, then retains unit-type eligibility, resistance, HP,
death state and health-bar updates. Only the surface/building rejection is replaced.
Source-unit self exclusion, other projectile types and all subsequent damage logic
remain native. Treated/fading clouds retain Vanilla scheduling; this mod adds no
damage calls and does not redefine fade damage timing. A0470, 9A080, A0540 and
9F2D0 retain treatment, lifetime, terminal state and deletion behavior.

## Hook sites and live-machine boundaries

| Owning behavior | Reference RVA/span | Continuations | Derivation |
|---|---|---|---|
| Cloud standing-plane update | 9FDB8, 32 bytes | 9FDD8 | Slot in EDX; manager RBX, module RSI; original six complete instructions |
| Disease height eligibility | 192809, 26 bytes | accepted 192861, rejected 1929D0; disabled 192823 | Unit stride RSI, manager R15, projectile stride R13; replace surface check and skip following building exclusion |
| Eight neighbors | 9CB6F, 26 bytes | accepted 9CB89, rejected 9CB9D | Preserve six arithmetic instructions and final CMP/JNE alive gate; omit old height CMP/JGE |
| Outer ring | 9CC3D, 26 bytes | accepted 9CC57, rejected 9CC6A | Same contract, projectile stride R14 instead of R13 |

Exact source bytes are maintained in PlagueHeightAssembly. All sites lie in the PE
executable section. Full containing functions, baseline cross-function Xrefs and
jump-table/data references have no entries into their interiors. tests/audit_native.py
rechecks these boundaries against the installed file and hash-matching SQLite.
No moved-build fallback is allowed: grid, structure and register contracts are
fixed-layout and cannot be established by one matching byte signature. Unknown
native hashes, backend hashes, layouts or manager addresses disable only this mod.

LoadContext.Memory is a load-time snapshot, not evidence of current patches. Before
installation all site bytes and the C07C0 helper/table are compared to actual live
process memory. After commit each real X64InlineHook must be installed with the
expected target, 32/26-byte displacement and FF25 jump, exact stub pointer and NOP
padding. Installation candidates stay logically disabled until validated/rooted.

## Heights, memory and registers

Height grid 4DDD350 is byte-valued; properties 48F71B0 use uint; building grid
4B6AA50 uses signed shorts. Tile capacity is 320800; building IDs are 1..3999,
native records have a sentinel/header and stride 32C. Building manager 64CCBB0,
type +12E and base elevation +148 correspond to public GameBuilding fields
at +D2 and +EC after its 5C-byte manager header. Unit manager is 67E8400,
stride 490; signed native base +712 and additive height +714 correspond to
GameUnit +B6/+B8 after the manager's 65C-byte header.

The shared helper follows normal standing-unit branch precedence from 184FD0:
stairs raw+12; building-backed wall raw+40; bare wall raw; IsBuilding raw+2;
Elevated building base+C07C0; other tiles raw. Unit-specific tunneling, siege-engine
and movement offsets are not copied to clouds; the actual target unit's stored
height remains authoritative. Out-of-range tiles/buildings fail closed. Invalid
cloud planes reject damage and keep Vanilla's visual update, with one error marker.

C07C0 is audited through its return branches and native jump tables C0830/C0858.
It only changes RAX/RCX/RDX/flags and uses no SIMD or mutable memory. The generated
helper saves RBX, reserves native shadow space and uses valid Windows x64 alignment.
Hook wrappers save every volatile GPR and flags before this helper; all nonvolatile
registers and SIMD are preserved. Update restores original RAX/RCX outputs; its
continuation overwrites flags before any use. Damage continuations independently
recompute needed conditions. Neighbor wrappers preserve arithmetic outputs and
Vanilla's alive comparison. No managed Context wrapper/flag assumption is used.

Cloud visual elevation becomes standing plane+20. Eligibility compares standing
planes, inclusive difference -20..20, not the visual plume center. The conservative
initial tolerance follows the existing 20-unit plume offset and is below the
40-unit building-wall additive height; gameplay boundary cases still need testing.

Executable helper memory is prepared once before installation; publication prevents
freeing or modifying it. Hooks, runtime, settings and publishers are statically
rooted. Only unpublished candidates can roll back; settings write an aligned
atomic data flag. No teardown hook, coroutine or plugin Update path exists.

## Validation and game acceptance

build.bat /verifyonly /nopause invokes fresh Windows PowerShell with the same
working directory/environment as installation. It performs JSON/lifecycle/CRLF,
workspace hook regression, native control-flow/backend hashes, full runtime semantic
compilation against the real Assembly-CSharp and .NET Framework 4.8.1, and native
fixture execution of production generators. It never builds/installs the mod.
The normal build.bat alone compiles and installs the plugin, then verifies hashes.
APIShared is referenced from its canonical verified workspace consumer package,
as with existing mod drivers. The driver installs only PlagueHeightTest; APIShared
must be active in the game for this hard-dependent plugin to load.

Fixtures execute all four stubs, disabled/enabled branches, inclusive +/-19/20/21,
stairs, bare/building walls, native tower/keep height table, changed/destructed
surface, invalid contexts, alive rejection, Windows stack and volatile/flag
preservation. Reachable generated instructions and indirect pointer slots are
decoded with installed Iced; decode-only displacement probes use installed
X64InlineHook on private buffers. No test installs hooks in a game process.

The additional review fixture executes the complete installed 192750 routine and
its building-exclusion tables on isolated records. Across all three radius values,
four resistance levels and lethal/nonlethal hits, enabled ground damage matches
Vanilla's complete 490-byte unit record exactly. All thirteen excluded building
types become damageable at matching height; actual wall damage, lower-level
rejection, source-self/dead/type immunity and an unrelated projectile's early
exclusion are checked through the full native function. Runtime backend hashes
now identify the actually loaded X64/Core/Abstractions/Iced assemblies, rather
than assuming adjacent files represent the assemblies loaded by Mono.

Remaining game checks: map editor and gameplay, naturally spawned and catapult
clouds, all actual roofs/walls/stairs, destruction, save/load, treatment/fade,
overlapping clouds, disabled Vanilla parity, and real host/client simulation.
Install alongside ExtraFeatures/BugfixesAndQoL/Fixes; no competing entry detour is
installed on 192750. The testmod is NetworkMode=1. BepInEx General/Enabled defaults
true and must match on every multiplayer peer; it is not automatically synchronized
by lobby modsettings. Changes during a mission are deferred until mission end.
