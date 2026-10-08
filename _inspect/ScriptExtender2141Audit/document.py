"""Commit-bound audit documentation; preserve all earlier evidence."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).resolve().parent
KNOW=ROOT/'_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge'
def write(path,text):
    expected=text.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8')
    path.write_bytes(expected)
    assert path.read_bytes()==expected
sites=json.loads((OUT/'native-sites.json').read_text())
audit=json.loads((OUT/'native-audit.json').read_text())
text='''# Script Extender 2.14.1 engage-range audit

Review date: 2026-10-08. Previous baseline commit: 5049b5d051d0face848cc2eb098db1fedf5451a7 (2.14.0). Reviewed target: 5908e1f12437deb7ef5f5b1dc2d64e301178fba3, tree c238441ea12db35ee36c5c5668d3c8fce0897295 (v2.14.1). Installed/local build/package SHCDESE.dll: SHA-256 E5D78FDF2EA9701336C398F7D29410F206D141FC01AA8099F30C824E6AB8A0B3, assembly 2.14.1.0. Native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2; managed Assembly-CSharp SHA-256 BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789. These are provenance-bound facts, not permanent version expectations.

The complete release diff changes only BulkUnitDetours.cs and CHANGELOG.md. No public API or interop layout changes in this release. Unit-vtable role names are candidate associations; native bytes, offsets, decoded branch targets and memory relationships below were verified. Evidence is retained in ../../../../ScriptExtender2141Audit (native-audit.json, native-sites.json, emitter-provenance.json, installed-layout.json, stubs.json, stub-verification.json and their reproduction scripts).

## Complete feature path and identity contract

Dispatcher 0x182B00 selects unit updates through vtable 0x321CB0. The active unit context is a 1-based game ID; iteration begins at 1 and advances by record stride 0x490 relative to the manager sentinel. Installed GameUnitManager.GameUnitArray starts at +0x65C (sentinel origin); API views account for that sentinel, yielding 0-based first-real-unit spans. Do not pass a span index as CurrentContextUnitId or add/subtract one inside this generator. Installed public fields: GameUnit.r_AliveState +0x88 AliveState, r_UnitChimp +0x8A eChimps, r_ControllableForPlayerId +0x92 UInt16, r_GlobalId +0x94 UInt32, r_IsKilledByProjectile +0x29C UInt16, r_WorldDistanceToNearestEnemy +0x2A2 UInt16. Reflection verification is retained in installed-layout.json.

Distance producer 0x18C040 handles wild/owner-zero selection with Chebyshev distance, initializes the distance to 10000 and validates global identity. Producer 0x18C160 traverses enemy candidates, calculates the integer approximation max + (((min * 2) / 5) * min) / max, clamps to 32000 and performs attack/path side effects. Refresh 0x19A1F0 is also included in the producer audit. The native WORD manager+0x8FE is GameUnit+0x2A2 after the sentinel/stride mapping. Native comparisons use signed 16-bit arithmetic despite the public UInt16 storage view. Keep arithmetic signedness and native identity separate from managed field naming.

Downstream 0x18E9A0 retains native identity, visibility, unit type, attack and height checks, followed by native unit-state transitions. Changing this range comparison does not replace target validity, attack-state or retreat/path decisions. The override is global per unit type; no local selected-unit or player scope is implied.

## Coverage and actual displacement

The installed RedBird 1.5 X64InlineHook backend rounds Math.Max(minHookSize, 14) to complete instructions. The 35 actual displacement lengths are 15..20 bytes. Source-selected update functions were decoded over their complete reachable boundaries with the same non-call control-flow traversal used by the backend, and matched the semantic database extents. Forward branch destinations are included; call targets are not mistaken for part of the function. Function-boundary limits in 2.14.1 are MaxReadLength 65536 and MaxInstructions 16384. All native signature matches and displacements were enumerated, including source-selected functions with zero matching comparisons.

| Candidate update role | Function RVA | Range comparisons |
| --- | --- | --- |
'''
for f in audit['functions']:
    text+=f"| {f['role']} | 0x{f['rva']:X} | {f['matchCount']} |\n"
text+='''
Each displaced instruction and direct jump destination is shown below; continuation is the first instruction after the overwritten span. Branch operands are original native virtual addresses (image base 0x180000000). No external direct entry or semantic xref lands inside an overwritten span. Original branch relocation is retained by Iced; the final indirect jump targets the exact copied-buffer continuation during offline testing.

| Hook RVA | Bytes | Displaced instructions / branch targets | Continuation |
| --- | --- | --- | --- |
'''
for s in sites:
    ins='; '.join(i['text'] for i in s['instructions'])
    text+=f"| 0x{s['rva']:X} | {s['length']} | `{ins}` | 0x{s['continuationRva']:X}: `{s['continuationInstruction']}` |\n"
text+='''
## Generator, registers, flags and publication

The audit extracts the exact upstream emitter lambda, records its SHA-256 and substitutes only two global-address constants for isolated scratch storage. It uses installed Iced and RedBird 1.5 to generate an unpublished X64InlineHook on a copied native byte buffer. Hook Enable is never called. DisplacedByteCount and ReturnAddress must match the native audit, reassembly must match the actual allocated stub, every executable byte must decode, and the trailing indirect-jump pointer must equal target + actual displacement. The last eight pointer bytes are data, not executable instructions. Native full-span bytes are preserved in native-sites.json.

The original compare runs first. A spare GPR excludes the compare operand, memory base and index. Its old value is pushed; the original operand is captured before r13/r14/r15 are repurposed. The scratch registers and flags are saved. CurrentContextUnitId is loaded through r13d before computing the unit record/type; native flag lookup preserves its own temporary. Default-cache initialization records the original operand, including when it occupied r13w/r14w/r15w. Cached defaults are not overwritten.

Vanilla branch restores saved flags and every scratch register. Override branch sign-extends the chosen Int16 threshold into the spare register, drops the saved flag slot with LEA, restores r15/r14/r13 before the replacement compare, compares against the spare WORD, then pops the spare register without changing flags. Thus the following original conditional branch consumes flags from the intended comparison. Stack restoration is identical on both paths. No ContextAssemblyGenerator callback wrapper is used here; its SUB/ADD flag caveat remains applicable to other context hooks. This emitter contains no SIMD operations or managed callback; GPR checks do not assert unrelated wrapper SIMD behavior.

The bounded actual-byte interpreter exercises 35,280 cases: all 35 sites, signed distance/override boundaries, three original thresholds, active/inactive overrides, cold/warm default caches and two incoming flag states. It verifies all sixteen GPRs, balanced stack, six arithmetic compare flags, original-value caching and allowed memory writes at the first restored native conditional branch. It fails closed on unsupported instructions. Full stub decode and continuation-pointer checks supplement this bounded prefix execution. This is an offline machine-contract test, not gameplay execution. Hooks are process-lifetime owned by the Extender; no release mod changes or new hook publication occur in this update.

## Consumers and compatibility

Release native ownership is recorded in Shared/ScriptExtenderUpdate/2.14.0-2.14.1.release-hooks.json (dynamically fingerprinted inventory). No release mod uses engage-range overrides or owns these overwritten spans. Across release sources, relevant test sources and Fixes, partial AOB fragments near two heavy-camel sites belong to ImprovedHunters queries; their complete concatenated signatures resolve inside hunter 0x12FC70..0x131422 and establish no overlap.

Clean canonical Fixes v1.26.3 commit cbabdecac15a6e81cb45404f1a3501a4856dae39, tree 185f84b1fa6fc2faad793f2980a18d38599290e0 is reviewed separately in FIXES_COMPATIBILITY.md. Its ladder hooks 0x111C00 and 0x6A5D0 do not overlap engage sites. EnemyBridgePathTest has an existing competing 0x111C00 owner and remains excluded; this update makes no compatibility claim for that test.

Existing capture-event and PCL route integrations are retained. The additive 2.13/2.14 APIs provide no demonstrated benefit requiring a migration here. AffectedMods and BuildMods are empty; all mod versions, minimum versions, metadata and changelogs remain unchanged.

## Acceptance limits

Gameplay acceptance remains with the user: start beyond startup cleanup, ranged target search, attack and retreat. Review only the new BepInEx start section and newly timestamped crash artifacts afterward. The known early crash-handler null-read/minidump false-alarm note remains: no source change in this release establishes its correction, and no controlled startup evidence has yet been supplied. Offline checks do not substitute for that acceptance.
'''
write(KNOW/'SE_2_14_1_ENGAGE_RANGE.md',text)
capture=KNOW/'GATEHOUSE_LIVING_CAPTURE.md'
old=capture.read_text()
needle='Installed SE exposes public `r_AliveState : AliveState` (signed Int16) and `N0000019A : UInt32`. Only the low 16 bits of the latter are tested natively; do not reject an unrelated nonzero upper word.'
replacement='Current installed SE 2.14.1 exposes public `r_AliveState : AliveState` (signed Int16, +0x88) and `r_IsKilledByProjectile : UInt16` (+0x29C), verified against assembly SHA-256 E5D78FDF2EA9701336C398F7D29410F206D141FC01AA8099F30C824E6AB8A0B3. Historical `N0000019A : UInt32` combined this WORD and the adjacent unrelated WORD; only the low 16 bits were tested natively. Current access uses the dedicated UInt16 field. The appended 2.14.0 migration audit remains historical provenance.'
assert needle in old or replacement in old
write(capture,old.replace(needle,replacement))
fixes=KNOW/'FIXES_COMPATIBILITY.md'
section='''
## Current Fixes 1.26.3 source review, 2026-10-08

Canonical clean clone v1.26.3: commit cbabdecac15a6e81cb45404f1a3501a4856dae39, tree 185f84b1fa6fc2faad793f2980a18d38599290e0. This supersedes the current source identity; the 1.25.1 and 1.26.0 sections above retain their historical provenance. No installed-Fixes assembly identity or gameplay result is inferred from the clone.

The full v1.25.1..v1.26.3 source diff preserves the documented popularity dependency, purchase pairs, distanced-siege-tent direction behavior, capture hook and PCL flow. SmarterSiegeLaddermen adds two inline hooks, distance-map/reset logic and a 16 MiB native state block. Their signatures uniquely resolve to 0x111C00 and 0x6A5D0 in the unchanged canonical native DLL. Release mods own neither entry; none of the 35 changed Extender engage-range spans overlaps them. EnemyBridgePathTest remains excluded due to its existing 0x111C00 ownership; its unchanged package is not validated with current Fixes.

The ladder search invokes BulkPathingDetours.c_game_pathsearch_bfs_ignoring_dynamic_occupancy and consumes shared WalkGeneration, WalkGrid, CertainPathGrid and AIPlayerObjectiveStorage scratch state. This is process-global mutable pathfinder state, not a pure read-only route query suitable for arbitrary reentrant consumer use. Our mods do not adopt this BFS path. These are source/data-flow findings, not demonstrated gamebreaking failures. No additional testmod or author report is introduced.
'''
if '## Current Fixes 1.26.3 source review, 2026-10-08' not in fixes.read_text():
    write(fixes,fixes.read_text()+section)
current=ROOT/'_inspect/CrusaderDE-Native-Baseline/CURRENT.md'
old=current.read_text()
needle='Semantic reverse-engineering baseline:\n'
assert needle in old
if '[Script Extender 2.14.1 engage-range audit]' not in old:
    write(current,old.replace(needle,needle+'\n- [Script Extender 2.14.1 engage-range audit](./sem/FBCB9319/knowledge/SE_2_14_1_ENGAGE_RANGE.md): full native feature path, all 35 actual RedBird spans, generated-code checks, consumer compatibility and acceptance limits.\n',1))
print('Wrote audit, current index and scoped current-contract corrections; historical evidence retained.')
