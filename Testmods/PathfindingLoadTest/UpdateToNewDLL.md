# Native provenance: PathfindingLoadTest

The fixture introduces no hooks, patches, trampoline, raw native state write or copied
pathfinding implementation. Standard unit, wall and selection calls use the public
Script Extender; lifecycle, readiness, monitor and runner use APIShared automation.

The single direct command delegate reuses UiAcceptance.ProbeSteps' established
ordinary player-move contract: native SHA256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`,
entry RVA `0x196100`, unit-manager base RVA `0x67E8400`, six Win64 arguments
`(manager, tribe, targetX, targetY, patrol=0, flags=0)`.
The full on-disk hash is checked before world preparation. Unknown hash fails the
test; there is deliberately no guessed signature fallback. The live entry can be
Pathfinding's detour and is invoked through normally, never byte-compared/replaced.

Full current-hash feature audits: baseline `CURRENT.json` chooses the dataset;
`KNIGHT_SELECTION_TRANSFERS.md` closes creation/init and three-phase selection;
`FORMATION_MOVEMENT_PERMISSION.md` and `MOVE_COMMAND_RELEASE.md` close ordinary
command/native permissions; `CRENELATED_WALLS.md` closes standard wall admission,
cost/quota, ownership, dirty path state and downstream consumers.

Additional closed analysis for this fixture is retained in
`.work/a/pf-opt/001/audit.md` with canonical pseudocode and assembly artifacts.
Allocator `0x17FEF0` uses mutable exclusive bound at `0x3668E34`:
default3000 or normal Extreme Troops10000. Public output `chimps_limit` is bound-1.
Lobby setup scalar18 maps to the normal writer `0x26370` through public
`setMultiplayerStartingData`/`DLL_ApplyMultiplayerSetupData`; no limit write by us.

Selection chain: managed wrapper -> `0x879A0` -> `0x8AC80` input staging ->
`0x8C5F0`/`0x190010` local marks -> `0x191130`/`0x123D60`1250-byte bitmap ->
native Chore16 (`0x10960`) -> `0x191250` owner/life/bitmap recheck ->
`0x119CE0` reserved tribe initialization -> `0x11D510`/`0x11D370` insertion.
Reader `0x119F90` traverses the same625-word membership bitmap.5000 is within the
10000-bit storage and signed16-bit member count. No250-/500-member clamp exists
in this relevant flow. Actual eligibility/admission is separately observed.
The semantic decompiler omits own-selection switch cases in `0x190010`; its
canonical code through `0x190494` and jump table were checked explicitly.

For an update, rehash the canonical DLL, re-audit the complete call/state paths,
compare actual installed Extender/Fixes consumers and public game-member visibility,
and rerun offline/full consumer checks before enabling game tests. Never change the
expected hash solely to make the fixture run. Multiplayer/editor are outside scope.

Read-only wall status after the ordinary call uses tile-manager-relative offsets
`0x204E6E4` (invalid), `0x204E6FC` (query blocked), and `0x204E704` (possibly stale reason).
Their full current-hash gate, write, resource and caller proofs are retained in
`.work/a/pf-opt/001/wall-placement-audit.md` and `resources-audit.md`; the additional
`native-dissolve-audit.md` and `troop-cap-audit.md` distinguish terminal deletion
from unproven population-limit explanations. The fixture reads these fields only.
Creation/selection/deletion observations preserve actual IDs and full health/life
snapshots; a healthy native dissolve is not attributed to Pathfinding without its entry path.

Route shape read (Fixture.Shape.cs, shape-* scenarios): after the hash-gated command the
fixture reads each member's published direction buffer read-only at the same unit-manager
base (RVA 0x67E8400) + 0xB4FE78 + one-based unitId * 1000 bytes, two 4-bit directions per
byte (low nibble first), r_PathPlanLength directions. This is the existing hash-bound
publication contract documented in Pathfinding/UpdateToNewDLL.md (unit manager buffer,
capacity 1000 bytes / 2000 directions). Identity, Global-ID and length are checked first;
directions above 7 count as invalid. No write, call or hook; unknown hash fails before.

## wall-5000-gate: portal table read (Fixture.Gate.cs)

Read-only, hash-bound to the same SHA256 as the move delegate (BindMove runs first; no
fallback). Path manager / portal table base RVA `0x60AD660`: int `[0]` is the highest
used id + 1 (written only by `0xF4540` each tick, plus `0x71895` at load); entry `id`
(1..199) at int index `id*0x81`, state `+0x809` (0 = free), building `+0x80C`. Entries are
created by `0xD8510` (gatehouses via placement `0x6D580`, ladders via `0x1950B0`).
Evidence: `portal-table-audit.md` of the 16.11.2026 resweep analysis (F4540 pseudocode
checked against the semantic baseline). Used only for log output, never for PASS/FAIL.
Update: re-derive the base from `0xF4540`/`0xE2610` RIP operands and the field indices
from `0xD8510`.
