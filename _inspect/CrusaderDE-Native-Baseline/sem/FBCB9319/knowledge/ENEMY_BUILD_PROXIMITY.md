# Enemy-unit proximity during building actions [Scope: Vanilla + mod integration]

Static audit: 2026-10-29. Canonical installed native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
RVA coordinates below apply only to this binary (image base 0x180000000).
Evidence: current semantic function exports, direct Capstone disassembly of the
installed DLL and executed original-machine comparison fixtures. Function-role
conclusions here are confirmed-static; this is not in-game acceptance.

## Candidate production and predicate

`0x181340` (called by `0xD1AD0`) rebuilds a player's hostile-unit list. It checks
live state, teams and exclusions, but includes civilian workers. Count is at
RVA `0x379CE58 + player*0x583C`; word game IDs at `0x38C19E8 + player*20000`,
paired global IDs at `0x38ED908 + player*40000`. Initializers clear these arrays;
no mod should remove workers from the shared list because other systems use it.

`0xEE640..0xEE835` is a read-only, 502-byte predicate. Complete body SHA-256:
`1D2DDB19C37B3EC505D426272421E57FE4336CAA2BF9FA80BE6F1143282C565D`.
Inputs: unused first pointer, one-based player, X/Y integer tile coordinates,
signed integer radius, and one byte controlling an existing exclusion. Coordinates
are checked unsigned against 800 and the full-width valid-map raster. Radius is
squared in Int32; each signed Int16 unit tile coordinate contributes to signed
Int32 `dx*dx + dy*dy`. Only a strict `< radius*radius` match can block.

Unit address is `module + 0x67E8A5C + gameId*0x490`; this is the sentinel-relative
native base, not a zero-based public Span. Reads: type +0x8A, global ID +0x94,
current tile +0xC0/+0xC2, death marker +0x29C. The predicate additionally requires
word +0x354 zero, word +0x90 not one, and owner/control word +0x92 nonzero.
Byte +0x328 must be zero unless argument six is nonzero. A stale global ID is
ignored. No universal extra AliveState check is present in this reader.

Types `CHIMP_TYPE_ARAB_ASSASIN` (0x49) and `CHIMP_TYPE_BEDOUIN_AMBUSHER` (0x51)
have further state/counter exclusions: +0x2BC states 0x6A/0x6B, dword +0x434,
signed word +0x440, and native context global 0x8574B90. The exact comparisons
are preserved by replaying all original instructions outside the distance CMP.
First eligible nearby candidate returns one; exhaustion or invalid map coordinates
returns zero. It writes no simulation state.

## Callers and independent restrictions

All direct callers were inspected: 0x5CA40, 0x60CB0, 0x6C540, 0x6FE90,
0x77CF0, 0x77E60, 0x79B90, 0x7A3B0, 0x90830, 0xD4EE0 and 0xD51A0.
The main 0x77E60 decompiler export has a missing fragment; direct disassembly
covers 0x780D0..0x78585, including calls 0x78118/0x783A8/0x78568/0x785E8.
The ordinary branches iterate every footprint cell; special branches use radius
three. Other callers have radius five or brush-size adjustments. Ordinary
constants are 30/15 depending on native context; this global is also involved in
Skirmish rules and is not a proof of real multiplayer. A mod must use its central
real-multiplayer snapshot to choose its own SP/MP configuration, while `-1` uses
the actual incoming radius rather than inferring a default from network status.

For ordinary builds, 0x6D580 invokes 0x77E60 before constructing. Failed unit
queries publish rejection (reason 0x11) and block placement. Success proceeds to
other validation. The AIV 0x51790 scheduler retries failures; procedural 0x52270
also reaches construction validation. Preview and execution both use these
questions; changing only a cursor color would not make the build succeed.

Enemy buildings and walls are checked separately by 0xEE320 and 0xEB9A0; the
latter is a bounded tile traversal using building/structure ownership, not the
worker loop. 0xEE840 checks hostile Keep proximity. Own/allied Keep range uses
0xEEF90. They can independently reject after the unit predicate allows a build.
The repair HUD 0xD51A0 and repair action 0xD4EE0 also query unit proximity;
0xD5030 executes the queued repair. Demolition uses 0x79B90/0x90830.

## ExtraFeatures integration [mod integration]

The existing SE event `OnBuildingAllowRepairInProximity` owns the live entry
hook at 0xEE640. Its Pre event can skip the original and supply a result; Post
is raised only when the original executes. ExtraFeatures uses this documented
replacement path and respects an existing skip/veto. No additional live hook,
shared candidate-list mutation or immediate write is introduced.

The private predicate is generated once in LibraryLoaded from the complete
validated pristine body. Four RIP-relative anchors become absolute data loads;
all internal branch IP identities are retained for Iced BlockEncoder relocation.
Only CMP at 0xEE748 is replaced: original R8D squared distance is compared with
EDI (original squared military radius) or a seventh Win64 argument containing
the civilian squared radius, chosen via an enum-based category table. RCX/RDX
are dead at this point; RAX and all loop state survive. No new stack frame,
per-unit managed callback, data write, or original flags dependency is added.
The original following JGE and all later filters remain. Original stack+0x58
is argument seven after four pushes; it is only read.

NativeMemoryManager.AllocateStub(int)/WriteStub from the installed RedBird Core
prepare one unpublished 4096-byte allocation; WriteStub performs cache flushing.
It is never rewritten, finalized or freed after publication. Unsupported native
hash/body leaves the category feature Vanilla; fixed list/record offsets have
no speculative pattern fallback. Existing AI rebuild-delay logic is independent.

Only ordinary human radius-15/30 calls and previously classified AI repairs or
rebuilds use category settings. AI initial placement, special human radii and
independent building/wall rules retain their original paths. The full civilian
list is the reviewed switch in EnemyProximityQuery; economic oxen/horses and
both mine workers are included. Military engineers, tunnelers, monks and slaves
remain military. Other threat types use the military radius.

Existing settings migrate via detached snapshot normalization. A read-only
pre-registration adapter captures the original serialized presence before the
preset owner materializes defaults. The owner alone writes migrated values.
Personal path candidates and schema-1/2/3 envelope keys reflect the inspected
APIShared implementation, not an unversioned external storage API; review this
adapter when that implementation changes. New explicit `-1` wins over aliases.

Evidence: `.work/a/proximity/001/`; maintained native fixture under
`_inspect/ExtraFeaturesNativeTests/EnemyProximityNativeTests.cs` executes the
original predicate and production variant on sparse private state, with Win64
GPR and XMM6/XMM15 sentinels. Actual startup allocation/delegate is exercised.
Preset fixtures cover old files, authoritative nested snapshots, named JSON
profiles, explicit Vanilla, restart, Trail restoration and client persistence.
No new game installation or actual gameplay/host-client acceptance is claimed.
