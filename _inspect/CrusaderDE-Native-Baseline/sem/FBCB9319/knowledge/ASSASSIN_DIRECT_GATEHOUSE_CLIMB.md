# Assassin direct gatehouse climb: native audit

Audit date: 2026-10-07. Installed CrusaderDE.dll SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
CURRENT.json and DATABASE_INFO.json identify this same binary. Complete function
pseudocode, caller/callee lists and disassembly from the installed binary are
preserved in `_inspect/AssassinGateClimb/0x<RVA>.txt`.

## Confirmed static result

Vanilla's physical Assassin climb implementation supports gatehouse roof tiles.
Its special path search and path reconstruction nevertheless reject a climb
edge when either endpoint has a nonzero building ID. Gatehouse roofs have both
the wall flag and a building ID. Thus a freestanding gatehouse has no direct
Assassin climb edge in the vanilla special path graph. An ordinary movement
connection through an adjoining wall can bypass this restriction. This explains
why wall-connected access can work without proving any particular rare observed
climb was direct. No controlled game run has yet established that observation.

Function names classified as candidate in the semantic database remain candidate
names. The conditions below are confirmed static instructions for this hash;
runtime behavior of the proposed change remains untested.

## Gatehouse tile construction and topology

- Building dispatcher 0x6D580 selects 0x74240 for type 45 and 0x74080 for type 46.
  Both constructors enumerate the footprint through 0x69850, raise the tile
  height by 90, set tile property IsWall (0x100), assign the owner, and write the
  allocated 1-based building ID into the building grid. They initialize building
  path topology through 0xD8510 and update tiles through 0x725E0.
- Grid building IDs reside at RVA 0x4B6AA50, wall/property grid at 0x48F71B0,
  height grid at 0x4DDD350 and movement direction grid at 0x51890D0. These are
  separate grids. Removing an IsBuilding flag does not remove a building ID.
- Load/topology flow 0x71670 -> 0xC8380 -> 0xB9510 preserves IsWall on the complete
  type 45/46 footprint and clears IsBuilding (0x400). It does not clear roof
  building IDs. Type 47 follows a separate high-wall branch and is outside scope.
- 0xC5040 and 0x725E0 call 0xD8CE0 to update gate passage direction bits according
  to gate orientation/state, adjacent surface legality and height difference.
  These ordinary movement connections must remain authoritative.

## Human target recognition and synchronized movement

- Input flow DLL_TroopSelection / DLL_RunTick -> 0x8C5F0, with target resolution
  0x79B90 -> 0x90830, distinguishes buildings from ground and walls.
- In 0x8C5F0, selected-Assassin predicate 0x196870 and the building-type switch
  already permit gate types 45/46 to enter the movement branch when the
  Assassin reachability query 0xE2CA0 succeeds. An unsuccessful query leaves
  building attack handling available. This is a reachability restriction, not
  absence of all gatehouse command handling.
- 0xB70C0 probes perimeter attack reachability; 0xB72C0 is a separate wall attack
  probe. They must not be replaced with unconditional climb acceptance.
- Ground move staging 0x195E30 emits Chore 17. Its consumer 0x10AE0 -> 0x196100
  -> 0x11B520 issues group movement. 0x199C30 handles separate command/sound
  bookkeeping and is not the movement consumer. Input release must not be
  replaced by an unsynchronized second command.
- 0xE2CA0 may return immediately for a shared path component or consult its pair
  cache; its forced query path uses 0xD9C40. Cursor and group queries therefore
  need the same climb-edge eligibility as actual route creation.

## AI and route generation

- AI tribe processing 0x117520 -> 0x10AA20, case 0x41A, already calls 0x1140C0.
  This gate-target action queries 0xEAB80 at increasing search budgets, resets
  tribe members through command 3 in 0x11E960 and moves the tribe to gatehouse
  coordinates through 0x11B520. Target search already recognizes live, nonburning
  enemy gatehouses of types 45/46; it is not proof of a realizable climb route.
- 0x117820 identifies an Assassin-only tribe. 0x11B520 then builds a reverse
  Assassin distance field through 0xD9C40 before distributing per-unit movement
  through 0x196280. Mixed groups use ordinary path constraints.
- 0x196280 -> 0xF4930 creates/reconstructs a per-unit route. 0xD9C40 callers also
  include 0xE2CA0 and 0x123090. Negative target coordinates request a flood field;
  the flood's zero return does not necessarily mean no useful field was built.
- 0xD9C40 accepts an ordinary direction-bit edge first. Its extra climb branch
  requires a cardinal direction, permitted target surface (mask 0x4A5014B1 or
  the vanilla vegetation exception 0x107160), zero source AND target building
  IDs, and IsWall at one or both endpoints. This is the direct gatehouse blocker.
- 0xE1640 reconstruction mode 3 repeats cardinal/zero-ID/wall constraints when
  ordinary direction bits do not connect the pair. Relaxing the search alone
  cannot produce a consistent usable path. Other reconstruction modes, ladder
  logic, search limits, stamp ownership and packed path semantics remain intact.

## Physical step, animation and completion

- Simulation 0x182B00 -> 0x1855A0 consumes low-first packed direction nibbles.
  Existing Assassin states 126..129 suspend ordinary movement stepping.
- For a new step, 0x1855A0 calls 0xDCE60 then 0xDCD60. The Assassin branch of
  0xDCE60 accepts ordinary direction connections first, otherwise validates the
  target surface and IsWall. Unlike the path search, it has no zero-building-ID
  requirement. Unit type 73 enters ascending state 126 or descending state 128;
  height difference, facing and animation/progress fields are initialized there.
  Other movement profiles and ladder branch are separate and outside scope.
- Assassin state dispatcher 0x16CD70 progresses 126 -> 127 and 128 -> 129,
  updates climb animation/progress, then resumes ordinary state 101.
  0x180A80 finalizes next-tile coordinates and actual height with ordinary
  occupancy remove/add helpers. 0x188340 is the normal movement-arrival test.
- Ordinary gate interaction/capture and combat remain vanilla. Merely setting
  movement or attack context is not evidence that states 126/127 occurred.
  A runtime test must observe command, route, physical climb state and arrival.

## Workspace and Fixes compatibility

- BugfixesAndQoL AssassinPathfindingRuntime owns the 0xD9C40 function replacement.
  AssassinPathReconstructionPatch owns the 0xE19D8 / 0xE19F9 reconstruction guards.
  Its current reservation policy permits nonzero IDs only when the endpoint's
  ordinary movement mask is nonzero. It is not a gatehouse-specific contract.
- AssassinSelectionAdapters own selection call adapters at 0x8D724, 0x8E2B8,
  0x8E550, 0x8F325, 0xB7161 and 0xB7321. The Script Extender owns predicate
  0x196870. Positive original results remain authoritative; competing hooks at
  any of these owned addresses are unacceptable.
- AssassinClimbRuntime and AssassinClimbCancellationRuntime govern existing
  player climb settings and stop/cancel semantics. A gatehouse experiment must
  respect those settings and cannot reset active climb states independently.
- Canonical external Fixes source was checked first. SmarterSiegeAssassins hooks
  gate target filtering at 0xEAD8C and departure at 0xEACC3; the optional non-siege
  target filter and capture handler affect later AI targeting. Fixes preserves
  live/type/fire eligibility and does not remove the D9C40 zero-ID climb rule.
  A testmod must preserve Fixes' captured-gate/ally decisions, not replace its
  complete gate selection action.

## Shared implementation decision (2026-10-07)

The user authorized central ownership in APIShared. AssassinPathAPI owns the
D9C40 detour and all four building-ID guard adapters. BugfixesAndQoL registers
its weighted builder and consumes the same gate endpoint rule. The experiment
does not replace human command dispatch or Fixes AI target selection. Physical
climbing remains Vanilla. Runtime gameplay acceptance remains pending.

Audited spans: D9C40 NativeX64 Indirect-only / 10 bytes; X64InlineHook D9F0C /16,
D9F1C /15, E19D8 /18, E19F9 /23. Complete containing functions and cross-function
direct edges were checked for interior incoming branches. Source-linked native
tests execute the productive assembler, decode it completely, validate actual
installed RedBird displacement and detour pointer-slot contracts, and exercise
active/inactive gate eligibility. See `_inspect/AssassinGateClimb`.
