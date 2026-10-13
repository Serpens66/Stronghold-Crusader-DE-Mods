from pathlib import Path
text='''
## 2026-10-13: gate damage and retained bridge-loss evidence

Complete hash-bound decision audit122 plus63 full damage/deletion/attack bodies,
including installed instruction bodies, are retained in
_inspect/EnemyGateBuildingContextAudit/bridge-loss-full-evidence.txt and
bridge-loss-contracts.json. Native SHA256 remains
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Fixes canonical local source is v1.26.4; existing owners remain unchanged.

7EB00 can mark up to two coupled class49 bridges via B9330/C4290 when its
parameter8 is1, before checking whether a class45/46 gatehouse has died. C4290
marks alive state3; C60F0 later consumes state3 through B8310. B8310 calls61FC0,
which dispatches class49 to622D0. Native class49 removal-table value at2E7190
is1. The resulting keep-moat word120 is1:622D0 keeps the moat flag instead of
calling61E70 to clear the moat record. This is a damage/deletion distinction,
not evidence that a separate bridge attack was ordered. Damage SDK arguments
do not expose every native branch flag or the actual return value.

The full log snapshot contains a later separate short process as well. The
explicitly selected historical process (7312 paired commands,359190 completed
native calls) is frozen under historical-20261013-152715. Original full-file
SHA25647F1F1CF57FBA9F72C5AB2A5A0FC325AD9AA7BAC8E865E2790FC876A1ED7D46C;
selected UTF8 process-section SHA256
7FE42449AF3164AA0FE835BB9CDB96C92FBCECB8454BCAA98BF5FC79CDF0C463.
Gate163 lost6HP; bridge494 left the active index; the next completed rebuild
followed. Old logs did not retain the removed footprint, so persistent moat
state is native/user evidence rather than a complete old-log field proof.

Runtime now allows relevant own military entry phases4/5, still two attempts.
Existing unit-command events can copy own unit identity and context target tile;
existing movement Post can supply an explicitly ongoing-state snapshot. These
are not selector results, fresh decisions, or proof of melee execution. Missing
native context targets can use only a remaining, identity-matched deck path as
a geometrical coupling candidate. Consumed path sections never qualify.
Unit/tribe GlobalIds are distinct. Native copied slot window has header65C:
unit Global offset65C+94=6F0; context building tile65C+3A4=A00.
Own unit event inputs, Unknown stack parameter and suppression are preserved.

BridgeLossHistory holds at most128 historical bridge/gate/footprint identities
per session, separate from live topology and permissions. Reuse/coupling changes
do not overwrite old evidence. Fields and state are recorded at relevant index,
damage and completed-rebuild observations. Unchanged field definitions are
referenced; generation/dirty changes alone do not generate duplicate state.
PCL50EC690, flags48F71B0, directions51890D0 and moat-task layer5F011A0 are read
within320800 entries. Moat-task5F011A0 derives from tile root405EDB0+1EA23F0;
it is not the organism layer485A770 used by69560. Historic evidence is never
used to grant or deny live reachability. Scalar snapshots are non-atomic and
cannot authorize negative decisions. Capacity overflow explicitly loses
coverage. Later gate damage remains relevant after its last bridge disappears.

Importer retains legacy artifacts and validates optional unitIdentity4/own
observation origin. Zero roots never constitute an exact causal link. Weak
ongoing-path snapshots are deferred, with one bounded final fallback; stronger
own input can consume the remaining attempt. Two delivered artifact limits,
64 records/64KiB blocks and the durable session-delivered marker remain.

Behavior fix remains disabled. Copied DA020/EDF30 and conservative melee-access
alternatives remain offline-only; full real own unit-command/formations and
effective permission integration still require actual input replay. The resource
errors have no proven Bridge caller and are not suppressed or replaced.

Next targeted capture: same gate-only-access layout; permanently lowered bridge,
closed gatehouse, Gate/Fixes/manual feature active, no own moat work. Observe a
fresh military decision and allow gate hit/bridge loss. Require planning artifact,
own unit-command or explicitly ongoing-state follow input, bridge-loss field/state
records, later gate damage if it occurs, completed topology and both artifact/
session delivery markers. It is a capture run, not behavior-fix acceptance.
'''
paths=[Path('Testmods/EnemyBridgePathTest')/p for p in ['HANDOFF.md','ACCEPTANCE.md','DECISION_FIX_DESIGN.md','UpdateToNewDLL.md']]
paths += [Path('_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENEMY_BRIDGE_GENERAL_DIAGNOSIS.md'),Path('_inspect/BridgePlanningTests/STRUCTURE_ACCESS_20261013.md')]
for path in paths:
 old=path.read_text(encoding='utf-8-sig');assert '## 2026-10-13: gate damage and retained bridge-loss evidence' not in old
 expected=(old.rstrip()+'\n'+text).replace('\r\n','\n').replace('\n','\r\n').encode('utf-8');path.write_bytes(expected);assert path.read_bytes()==expected
