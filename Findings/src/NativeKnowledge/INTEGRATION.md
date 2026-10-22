# Native field knowledge — staged implementation

This directory contains production C# sources intended for `APIShared/src/NativeKnowledge`.
It is staged under Findings at the maintainer's request. No APIShared source, public API
baseline, build output, installed package or plugin initialization is changed here.
Copy the five `.cs` files together; they use `APIShared.NativeKnowledge` and only
System/System.Core/System.Memory already available to the runtime project. They do not
reference Unity, BepInEx, SHCDESE, a publicized assembly, workspace Shared sources, or a JSON parser.

## Identity, semantics and safety

Use permanent `NativeFieldIds.Fxxxxxx` identifiers for compiled integrations. The
IDs identify raw storage; current names, proposed meanings and source names are discovery
metadata. `FindByName` is exact, case-insensitive and returns every match. In particular,
`CurrentHealthRaw` is ambiguous across structures. Do not select the first result blindly.

`Meanings[0]` is the preferred interpretation. Other meanings can record competing
hypotheses or rejected history. `Confirmed` applies to the stated scope, not to every
use of the field. Unit state fields are reused by type-specific state machines.
Source names and Ghidra reconstructions are not independent native semantic confirmation.

Default reads require both a confirmed preferred interpretation and an audited storage
binding for the host-verified image. `AllowUncertainMeaning` admits Candidate/Probable
interpretations **only** when the separate storage contract is verified. It never enables
source-only bindings, unknown images, wrong record/type, short records or array spill.
Rejected preferred meanings cannot be read with either policy. Failure returns default(T)
and a specific reason; default(T) alone is not evidence of a successful read.

Exact-storage aliases survive renaming. Different-width historical declarations are
separate related entries, not implicit conversions. Examples include the former combined
`N0000019A` DWORD and the ten old Tribe attacker halfword pairs. `RelatedFieldIds` means
overlapping known storage, not semantic equivalence. New evidence must not silently redirect
an existing ID or a previously published semantic helper to different bits or behavior.

## Host integration and caller-owned views

Construct one `NativeFieldReader` using the **actual** native SHA-256 already computed by
the host's normal native initialization. Do not pass `CapturedNativeHash` merely because it
is available: it describes the evidence, not the running process. Unknown well-formed
hashes construct a reader, but reads return `UnsupportedImage`. Malformed hashes throw.
Before native readiness, expose no reader. The later APIShared integration must publish
the reader through its existing process-owned initialization; no additional hook or event
subscription is needed. Pure catalog queries are available without a game process.

`NativeRecordView` accepts a complete caller-owned byte span and an exact record-family
ID from `NativeRecordTypes`. It does not acquire native pointers or validate the caller's
label. For live game data the caller must acquire a valid immediate pointer through the
appropriate audited API, use the owning game thread, and retain validity until the read
finishes. A native-backed Span can still fault if the caller supplies stale/invalid memory.
The reader is not a memory-safety boundary for arbitrary pointers. Offline snapshots are
also supported. Do not retain native views across callbacks or map transitions.

All current record origins are the beginning of the named public interop view, **not**
the manager base or the first manager-relative displacement. `GameUnitManager` metadata
uses its own manager origin; its full extent is unverified and no reads are granted.
IDs/slot lookups are outside this component. Unit/building/tribe game IDs are one-based;
public spans use their documented zero-based indexes. No ID arithmetic is guessed here.
Never create/copy/box an `AivSystem` value to obtain bytes: use an audited pointer/span/ref
view if a future catalog entry covers that family.

Example for an already acquired valid Unit byte view (same code works with an owned snapshot):

```csharp
var catalog = NativeFieldCatalog.Default;
var reader = new NativeFieldReader(catalog, actualVerifiedNativeHash);
if (!catalog.TryGetKey<ushort>(NativeFieldIds.F000054, out var deathMarker))
    throw new InvalidOperationException("Required field identity unavailable.");
var view = new NativeRecordView(NativeRecordTypes.GameUnit, unitRecordBytes);
if (reader.TryRead(view, deathMarker, out ushort raw, out var failure))
{
    // raw is the low death word, not proof of projectile causation.
    // This does not replace APIShared.UnitAccess.IsReallyAlive or identity validation.
}
```

For uncertain capture eligibility use `F000055` and explicitly pass
`NativeFieldReadPolicy.AllowUncertainMeaning` on that read. Native array element indexes
are zero-based **within that field**, not entity game IDs. `RaidCandidateBuildingIds`
returns raw entries from a bounded 100-element allocation; the separate live count
still determines which entries are meaningful. No count/value is silently clamped.

## Evidence and update boundaries

Capture on 2026-10-22: native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
published analysis generation `sem/FBCB9319/analysis/g1E2FBFF53E33`.
The generated baseline records Extender 2.14.2 commit
`10628df85cd432184742a50a1dee3969a66cf89e`; later source/installed Extender 2.14.3
was inspected separately. These are capture observations, not a minimum-version policy.

The offline authoring inventory examines all 84 current Knowledge Markdown documents.
Exact qualified references to source fields, explicit reviewed field maps, outpost/worker
offset notes and the later 2.14.3 supplement yield the initial catalog. This is a curated
field inventory, **not** a claim that every unstructured address or every Extender field
has been exhaustively identified. Unstructured prose and generated layout observations
remain discovery sources for future additions. No automatic import grants read permission.

Each entry retains evidence kind, portable source identifier, exact source SHA-256 and
line range. `KnowledgeReference` is only a mention; `ExtenderSource` is a declaration;
neither is promoted to a native audit. Captured source-file changes do not invalidate
unchanged native machine facts automatically. Recheck the affected integration assumption
instead of requiring a frozen workspace. Live game acceptance remains independent.

Two audited raw Tribe slices deliberately differ from source field boundaries:
target building ID at public view `+0x622` and target global ID at `+0x626` derive from
manager stores `+0x64C/+0x650` minus `0x2A`. Original instructions at `0x11FC2C`
and `0x11FC38` establish WORD/DWORD writes. The installed-layout test separately checks
these bytes and full public-record coverage, rather than treating neighboring names as aliases.

Existing native audits cover read-enabled bindings. Unknown width/signedness/extent,
partial state-machine knowledge and later upstream names remain explicitly withheld.
Do not infer a missing writer from selected-flow coverage or assume recovered aliases,
historical similarity or a matching source name establish a new binding.

## Verification and later adoption

The separate `Findings/tests/NativeKnowledge` directory contains production-source CLI
tests, a .NET Framework 4.8.1 compilation fixture, installed-interop verification and
offline authoring evidence. Run its `verify.ps1` in fresh Windows PowerShell. It writes
only isolated test artifacts; it does not build, install or start APIShared/the game.
Synthetic byte buffers verify reader behavior, not native pointer lifetime or gameplay.

When adopting this area in APIShared, add the tests to its own test infrastructure,
document discovery in API_CATALOG/THIRD_PARTY_GUIDE/ARCHITECTURE, register the new public
surface in its compatibility baseline, document hash-bound layouts in UpdateToNewDLL,
and connect the reader to the existing verified initialization. Run relevant APIShared
build/integration/game checks then. No README edit or version change is included here.

The authoring `catalog-identities.json` is an append-only **capture ledger for this native
image**, not a runtime lookup by offset and not an automatic migration strategy. Preserve
it when adding fields. After publication, moved storage must be explicitly mapped to its
existing identity in a separately audited hash binding; never regenerate identity from
the new address/name. `capture_catalog.py` refuses a new native hash until its reviewed
contracts are explicitly re-audited. Treat the generator and `catalog-evidence.json` as
authoring/test artifacts, never compile or load them in a runtime mod.
