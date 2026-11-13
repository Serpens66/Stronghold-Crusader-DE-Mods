# Assassin Keep test native read contract

This explicitly requested diagnostic test reads no fixed native RVA and installs
no native hook. The installed native SHA-256 must match
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
before the fixture starts. Unknown binaries fail closed before any raw field read.

The explicit editor scenario uses the genuine public `loadMapIntoEditor` callback
path and a local adapter around the existing private static `object`
`EngineInterface.threadLock`. The adapter validates reflection metadata, Unity
thread, current local editor mission, session and actual monitor reference before
and inside the bounded lock. It does not bypass APIShared's documented exclusion
of editor from `AutomationRun.InSimulation`: gameplay retains that helper and the
editor branch uses its own checked short monitor scope. No native addresses or
hooks are added. On managed updates recheck the monitor declaration and the full
editor load path before running this diagnostic.

The ordinary public Script Extender `UnitAccess` pointer is the GameUnit start,
not the unit-manager base. Read-only fields relative to that pointer:

| Offset | Read | Evidence |
| --- | --- | --- |
| `0xB8` | signed Int16 render/standing-height adjustment | Vanilla `0x184FD0`, caller `0x180A80` |
| `0x410` | signed low Int16 climb height | `0xDCE60`, especially `0xDCFCD`; `0x16CD70` states126–129 |
| `0x414` | signed low Int16 climb progress | `0x16CD70` states126–129 |

The complete native feature chain and original instruction evidence is selected
through `_inspect/CrusaderDE-Native-Baseline/CURRENT.json`, particularly
`knowledge/ASSASSIN_DIRECT_GATEHOUSE_CLIMB.md` and the preserved hash-bound
`_inspect/AssassinGateClimb/0xDCE60.txt`, `0x16CD70.txt`, `0x180A80.txt`.

On update, re-audit initializers, signedness, low-WORD consumers, both ascent and
descent terminal paths and the standing-height writer before updating the hash.
There is no semantic layout fallback: this is a disposable, hash-bound diagnostic
fixture. Tile flags, height, edge masks, StructureWas and packed directions use
public Extender readers and remain observational. Unit ID/global ID/type/control
player are revalidated before each sample. No raw field is written.
