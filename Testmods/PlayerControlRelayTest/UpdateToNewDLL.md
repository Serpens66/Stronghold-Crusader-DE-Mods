# PlayerControlRelayTest native contract

Audit observation: 2026-11-05. This document covers the experimental backend:
Move, Stop, Stance, Patrol, Disband and Attack Here for exactly the units a player selected. It
regroups them through the public tribe calls inside the Chore. It does not authorize native Chore
producers, selection changes on the owner's PC or new native hooks.

## Identity and provenance

- Canonical installed `CrusaderDE.dll` SHA-256:
  `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
  Rechecked from the installed game on the audit date against baseline `CURRENT.json`.
- Script Extender analysis source: local commit
  `4014ca67c96974a5e6d697665baf5351cbfebde6`. The installed assembly's
  `TribeMoveType` was independently decompiled: `NoChange=0`,
  `DefaultInSync=1`, `Fast=-255`.
- APIShared SDK selection is the local `develop` commit
  `cfd94c1b11348fa4c50d36f652ea1bf139742a27` observed during this audit.
  This is a dated observation, not a requirement to keep a mutable branch there.
  The package actually selected by a build and its public contracts remain decisive.
- Addresses below are RVAs for the full native hash above. Internal names in the
  semantic database remain candidate labels; the described data flows have static
  evidence. No live-game or real host/client acceptance is claimed.

The testmod uses public Script Extender methods. It must not call the internal
BulkTribeDetours wrappers, derive pointers from these RVAs, compete with their
hooks or use a second allocator. Unknown images leave the experimental command
backend unavailable until its dependent contracts have been re-audited.

Runtime initialization matches `context.ModuleHandle` against Win32
`GetModuleHandle("CrusaderDE.dll")`, obtains that loaded module's actual path with
`GetModuleFileName`, and hashes its disk image through the shared cached file-hash
helper. Availability rechecks the module handle. It does not access the Extender's
internal `LibraryModuleHandle` member. The private static object
`EngineInterface.threadLock` is resolved by validated reflection, and every native
read/call requires ownership of that same still-current monitor.

## Admission and identities

The authenticated Chore receiver is the only gameplay mutation boundary. Every
peer executes the same accepted command there, once. Submission or a Steam-packet
callback never calls the terminal APIs. Capture and execution both identify the
native world; execution resolves each requested unit again.

A command names unit slots, its owner and the highest global ID among those units at
capture. Unit global IDs come from the monotonic native counter at `0x183665F84`, shared
with tribes, so a reused slot carries a higher global ID and is skipped. The slot list is
sorted and LEB128-delta encoded with at most 1000 bytes; `RelaySlotCodec.Split` divides
larger selections into separate commands.

Execution rechecks each unit with the selection filter described under Input observation,
plus owner and global ID. A command without any remaining unit is rejected without a native call.
Units the owner has selected are ordered too: `0x1173F0`, `0x1918A0`, `0x19A5D0` and `0x19A700` read
the unit's current tribe, and `0x123EA0` checks it, so no crash path is visible after regrouping.
The owner keeps seeing such a unit as selected; the next order to the old tribe does not reach it
until the owner selects again. This is static evidence; a real match has not confirmed it.

If the remaining units are exactly all members of one tribe, that tribe receives the
order. Otherwise `TryRegroup` creates a tribe with `Create` and the majority stance
(tie rule of `0x11B150`), then moves every unit with `UnassignUnit` and `AssignUnit`.
It verifies `r_TribeId` per unit and rolls back on failure. Vanilla's opcode 16 handler
`0x10960 -> 0x191250` builds a tribe from a selection in the same spirit, but its selection
is per issuing player, so it is not reused. Exactly one native Move or Stop follows.

A false return from `0x11B520` happens before its per-member loop; only group
fields may have been written. Stop returns 1 from case `0x1F`. A thrown exception ends
the command as `Uncertain`, without a retry.

Admission does not invent a new unit-type movement permission rule. The group
dispatcher retains Vanilla's per-member type, deployment, life and state filters.
Consequently an accepted request need not move every member.
An AI may issue a later order to its own group; this backend does not pause the AI
or modify its role references. Whether AI role tables cope with regrouped units in a long
match is not audited; the acceptance scenario only covers 45 s without a fault.
No additional AI-role exclusion is established by this audit. Consumer authorization and scenario scope decide which owners may be
requested, independently of whether the group can be resolved safely.

## Move call and downstream behavior

Use the public call:

    GameTribeManagerAPI.Instance.IssueMoveHereCommand(
        tribeId, tileX, tileY, isPatrolPath: false,
        bIsNewOrder: 1, tribeMoveType: TribeMoveType.DefaultInSync);

Coordinates are tile X/Y integers, not world positions or a packed tile ID. The
native grid axes are bounded by the Extender's `MAX_WIDTH`/`MAX_HEIGHT`. Validate
the active map through the public tile API before tile lookup; its
`GetTileId(x,y)` alone does not perform bounds checks. Native target availability,
terrain and route refusal remain authoritative.

The public API checks tribe lookup and calls the existing Extender group-move
wrapper. That wrapper raises `OnTribeIssueOrderMoveHere(Pre)`, honors a veto or
modified inputs, calls the original group function, then raises Post. Existing
participants remain in this chain. The testmod does not bypass their callbacks.

Current native chain:

- `0x11B520`: owner/leader/current tribe bitmap, coordinate/target checks,
  ordinary/Assassin/moat route selection and native member permission predicates.
- `0x117BC0` / `0xE7C40`: one route selects common-group `0x118E00`; otherwise
  group-mode lookup `0x117C70`, region check `0xE2610` and structure/exit fallbacks
  `0xE9D90` / `0xE9FF0` participate.
- The group route prepares native destination maps with `0xDA590`, `0xD9C40` or
  `0xDAFD0`, checks membership through `0x124740` / `0x119F90`, and retains native
  limits and failure branches. No extra path probe is needed for admission.
- Group/common dispatch writes order and waypoint state before terminal
  `0x196280`; this normalizes its start through `0x19B1B0`, handles components and
  portals, builds/consumes native paths and can enter structure-exit state
  `0x199CD0`. Ordinary path builders/reconstruction retain their native repair
  behavior and shared scratch lifetime.

This is an execution call, not a read-only probe. A false return can follow native
group-field writes. Never retry it, replay a consumed input
edge or issue direct per-unit movement as a fallback. A true return establishes
the API result, not arrival at the target or successful movement of every member.

The normal Vanilla Chore-17 route is different at its entry:
`0x10AE0 -> 0x196100 -> 0x11B520`. Wrapper `0x196100` passes a fixed `1` as new-order
argument and the move flags without bit 7 as move type; a normal click has flags 1,
which matches this backend's arguments. It first writes the tribe flag at
raw manager `tribe*0x688+0x56C` from the signed move flag (input bit 7, toggled by a
repeated click on the same tile within 500 ms). The public group API
enters `0x11B520` directly and does not perform that write. This testmod deliberately
tests the public group API semantics; it must not promise byte-for-byte parity
with ordinary Chore-17 input or add a private field write to mimic it.

## Stop call and downstream behavior

Use public `GameTribeManagerAPI.Instance.Stop(tribeId)`. The public wrapper issues
`TribeAICommand.UnitStop` with arguments `0,0,1` through the existing target-order
Pre/original/Post chain. The native target dispatcher is `0x11E960`, case `0x1F`.

The common entry clears group target/queue metadata, and the Stop case clears the
group waypoint state. For each eligible current member it sets the native stop
order, clears target/order fields and invokes `0x197950` and `0x198ED0`.
`0x197950` clears movement-related fields only. `0x198ED0` handles an existing
special traversal state: it can normalize position through `0x19BA40`, or invoke
the ordinary native damage path `0x199110` for a nonstanding unit in blocked
terrain. Preserve this native behavior; do not replace Stop with a guessed field
reset or promise it is universally side-effect free.

The Stop case does not enter the separate disband case `0x1E`, charge/refund the
local view, deselect the owner's troops, create a tribe or rewrite membership.
It has no native-view identity write. Its public return has the same limits as
Move: no automatic retries or transactional rollback are inferred.

## Coexistence and caller context

- The authenticated native Chore runs synchronously in the simulation/engine
  context. Do not marshal the actual command to Unity or use a worker callback.
  UI and diagnostics receive immutable results separately.
- Script Extender owns `0x11B520` and `0x11E960`; the public APIs traverse those
  owners. No new native entry or instruction span is owned by this testmod.
- BugfixesAndQoL subscribes to both existing tribe-order events. Its normal Move
  admission, queue and formation callbacks plus inner path hooks remain reachable.
  Wrapper-only providers at `0x196100` are not entered by the public group API;
  their ordinary Chore-17 behavior is not a promised feature of this test backend.
- The canonical Fixes source has optional inner Move and target hooks (including
  Zerg unit destination distribution). Calling the public API leaves these hooks
  in place. Identical matching builds/settings on all peers and genuine combined
  game acceptance are still required; source inspection alone proves no FPS,
  latency or gameplay result.
- Existing local selection and `CurrentSelectedTribeId` are not commandeered.
  This says nothing about preserving the group's previous order: changing that
  order is the requested effect. Third-party native-order participants retain
  their own documented ability to alter or veto execution.

## Regrouping contract

The public `Create(owner,false)` resolves to `0x119C00` in this image. It selects
an inactive tribe by its state at raw manager `slot*0x688+0x40`, clears its whole
record and assigns a global identity. It does not test the local pending-selection
reservation at `+0x2A`. Native selection reservation `0x11D1B0` does test that field
and partitions MP slots by player. The reservation is local bookkeeping and must
not be treated as common simulation state. The allocation itself is deterministic
and used by Vanilla's multiplayer recruit Chore
(`0x127A0 -> 0xD5250 -> 0x190CA0 -> 0x117000 -> 0x119C00`), so the reservation
collision exists in Vanilla as well; it requires all lower slots to be occupied.
Why `0x1142E0` uses `0x119D60` in multiplayer instead is not established.

`AssignUnit` resolves to `0x11D370`: it inserts into the new bitmap/count and
overwrites the unit's tribe identity. It does not remove the old bitmap entry, so
`UnassignUnit` comes first. Public `UnassignUnit` resolves to `0x123EA0`; its pattern
matches this one site in this image. It elects a new leader and marks an emptied tribe
with state 3. Maintenance `0x1261F0` runs every simulation pass after unit loop
`0x182B00` and clears tribes without living members through `0x118C70`.

The production precedent for this call order, stance copy (raw `+0x634`, typed
`r_TribeStance`) and rollback is `Pathfinding/src/CommandQueue/ExtendedShiftCommandQueueRuntime.cs`.
The feature never submits synthetic Select/Deselect Chores and never calls
`SetSelectedChimps` to borrow the owner's selection.

## Input observation

The testmod has no panel, no HUD button and no modal surface. It adds two Harmony
postfixes. The public managed `EngineInterface.TroopSelection` is the method where
APIShared's marked selection and the Extender's selection override also observe input.
Its `mousePosX/Y` are screen pixels, so the postfix only stores a released gesture. The
postfix on its caller `EditorDirector.preDLLCallActions` then dispatches it with
`mouseOverX/Y`, the tile `DLL_RunTick` receives. Both only read their arguments, so
Vanilla keeps handling the local player's units. They stay installed for the process and
return without allocating unless the left button was released or the right button
changed. They run on the simulation thread inside the engine pass.

Managed baseline `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`:
`EditorDirector.preDLLCallActions` calls `TroopSelection` once per pass, with mouse
state 3 for a release, 0 while paused or over GUI, and the unit IDs of `grabTroopsOnScreen`
for all owners. `mouseOverX/Y` hold `gameMapX/Y` of the tile under the cursor, or -1 over
GUI. A finished drag is back at `CurrentAction == 0` by then; placement and other tools are not, and those gestures
are ignored. The native marking `0x1904C0` marks only units of the local view, so
foreign units cannot use Vanilla's selection or its highlight. `TryGetSelectableUnit`
applies that function's per-unit filter without its owner test. It reads
`r_SelectionAllowed`, `r_AIObjectiveState`, `r_AIState` (0x6D is the disband state
written by `0x186D10`), and the unnamed field at typed `+0xAC`. Lords pass this filter, because `0x1904C0` only keeps them out of box marking and Vanilla has own
Lord paths (`0x191C90`, `lordOnlySelected`). The input applies the box rule locally, so execution stays
peer-independent. Their offsets are part of the initialization layout check.

Gestures follow `ConfigSettings.Settings_SH1RTSControls`, which `DLL_RunTick` passes as argument 26 to
input `0x8C5F0` as `param_4`, and which the mod reads per gesture. `true`: left release orders, right press
clears (`0x1800897F0`). `false`: right release (`param_5`) orders, a left click without Shift clears.
`preDLLCallActions` zeroes only the left state while paused, so `RelayGesture` checks the pause for the right
button itself. A release BugfixesAndQoL suppresses after a placement cancel does not reach the mod either.
In both modes a left click selects the topmost selectable allied unit, a box selects all hit
allied units without Lords, and Shift adds. A click on an enemy gives no order. An armed patrol or attack
behaves like Vanilla's placement cursor: left confirms, right cancels.

A click on an own building also counts as a click without a unit and therefore moves
the selected allied units there; Vanilla's building-specific order codes (`DAT_1860ad560`) are not used.
Spectators have no participating player and no alliance; the relay stays inactive and logs that once.

## Vanilla troop HUD

Managed baseline as above. `GameData` opens the HUD at `app_mode == 14` and `app_sub_mode` 61 or 62 through
`TroopsSelectedGameAction`, and `updateDLLSelectedTroops` feeds the portraits from `numSelectedChimps`,
`selectedChimps` and `selectedChimpTypes`. KeyManager's Stop, stance and Attack Here keys and FatControler's
stance display read the same state. `RelayHud` extends only the managed result of
`EngineInterface.CopyPlayStateStruct` by a Harmony postfix. It sets sub-mode 61, the unit lists,
`troops_show_stance`, `troops_show_disband`, `troops_show_patrol`, `troops_patrol_mode`, `lordOnlySelected` and
`troops_show_attack_here_and_type` 5. Native `0x19D960` writes that last field with 1 for oil, 2 for tunnel and
3 to 5 for other fighters; the mod does not reproduce this per-type rule, so Attack Here is shown for every
allied selection. All other `troops_show_*` stay 0, which hides the special buttons. Nothing reaches the
simulation or the native selection. BugfixesAndQoL's foreign HUD leaves by its own rule
(`ForeignTroopHudRuntime`: `vanilla-troop-selection`).

All buttons end in `EngineInterface.GameAction`. APIShared's `GameActionEvents` Pre vetoes `Troops_*`, `EngBuild_*` and
`CloseTroopsPanel` for a purely allied selection, because `DLL_GameAction` would otherwise act for the view
(Stop `0x3F2`, Disband `0x3F3`, stance `0x3FC`; `0x3F4` and `0x3FB` arm a placement cursor). Next to own units
Vanilla keeps acting for those, and the mod orders the allied units as well, except for the cursor actions.
A prefix on `EditorDirector.placeBuilding` catches Vanilla's patrol key, which skips `GameAction`.

Public calls in the Chore: `SetStance`, `SetPatrolPath` with `PatrolInfinite` between the leader's tile and the
target, `IssueTargettedCommand(UnitDissolve, 0, 0, 1)` as in `Stop`, and `AttackTile`.

## Update and acceptance checks

On a native/Extender update, recheck the public method contracts, the complete
Move/Stop branches and dependent member/path semantics, installed enum values,
unit/tribe layouts and existing hook/event ownership. Retain independent source
and installed-assembly identities. A changed native hash disables this backend
until those checks succeed; there is no guessed RVA or pattern fallback.

Offline behavior tests cover the slot codec and splitting, order planning, global-ID
recycling, owner changes, whole-group detection, invalid target coordinates, duplicate
requests and the wire limit of the largest command. They model these boundaries; they do
not execute the game's simulation or the terminal calls.

`UiAcceptance` scenario `SP-relay.json` runs the real `preDLLCallActions` in a skirmish in both mouse modes and
replaces only Unity's mouse values. It checks real picking of foreign units, exact selections, their target
change, no outsider in the ordered group, the Vanilla HUD with the foreign HUD closed, Stop, stance, patrol,
disband, the Lord, regrouped AI units over about 50 s and no freeze with the Spectator placement mode.
Attack Here is checked only up to the native call. It proves no physical mouse input and no network behavior.

Game acceptance must compare standalone dependencies with the normal mod set,
then real matching-build host/client peers: own and allied units, owner
selection changing while a request is pending, overlapping Move/Stop, AI issuing
a later order, stationary/special members, losses during a pending order, selections
above the packet limit, a target without a route, Save/Load
and world replacement. Record identical accepted request/tribe identities and
execution ticks on both peers, unchanged nonmembers and the next native checksum.
All peers need the same build: the two packet types shift later extender packet IDs.

Evidence is in the current Native Baseline's `PLAYER_CONTROL_CONTEXT.md`,
`MOVE_COMMAND_RELEASE.md`, `MANUAL_GROUP_ADMISSION.md`,
`MANUAL_GROUP_REGION_RECOVERY.md`, `UNIT_COMMAND_PROBE_SCRATCH.md`,
`FORMATION_MOVEMENT_PERMISSION.md`, and the complete current-image exports for
the RVAs above. Later corrections in the admission dossier supersede historical
field-name/offset interpretations in older dossiers; do not copy a historical
terminal field offset into runtime code.
