# PlayerControlRelayTest native contract

Audit observation: 2026-11-04. This document covers the first experimental backend:
Move and Stop for complete, unchanged existing tribes. It does not authorize
arbitrary unit subsets, tribe allocation, selection changes, native Chore producers
or new native hooks.

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
native world and the actual tribe ID/global-ID/owner tuple.

Immediately before a terminal call, compare the complete native membership with
the captured selected unit set. Each unit needs its one-based ID, global identity,
current tribe ID and matching `r_ControllableForPlayerId`; use public
`APIShared.UnitAccess.IsReallyAlive` for the established AliveState/death-marker
predicate. Reject changed membership, dead or recycled units, mixed tribes and
proper subsets. Never widen a requested subset silently to its whole tribe.
The leader must be an actual live member of that exact tribe. Counts and IDs must
be bounded against the actual native tables before dereferencing them.

Admission does not invent a new unit-type movement permission rule. The group
dispatcher retains Vanilla's per-member type, deployment, life and state filters.
Consequently an accepted complete-group request need not move every member.
An AI may issue a later order to its own group; this backend does not pause the AI
or modify its role references. No additional AI-role exclusion is established by
this audit. Consumer authorization and scenario scope decide which owners may be
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
state changes or partial per-member work. Never retry it, replay a consumed input
edge or issue direct per-unit movement as a fallback. A true return establishes
the API result, not arrival at the target or successful movement of every member.

The normal Vanilla Chore-17 route is different at its entry:
`0x10AE0 -> 0x196100 -> 0x11B520`. Wrapper `0x196100` first writes the tribe flag at
raw manager `tribe*0x688+0x56C` from the signed move flag. The public group API
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

## Arbitrary-subset gate

The public `Create(owner,false)` resolves to `0x119C00` in this image. It selects
an inactive tribe by its state at raw manager `slot*0x688+0x40`, clears its whole
record and assigns a global identity. It does not test the local pending-selection
reservation at `+0x2A`. Native selection reservation `0x11D1B0` does test that field
and partitions MP slots by player. The reservation is local bookkeeping and must
not be treated as common simulation state. Therefore `Create` is not yet a proven
deterministic, reservation-safe allocator for this experiment.

`AssignUnit` resolves to `0x11D370`: it inserts into the new bitmap/count and
overwrites the unit's tribe identity. It does not remove the old bitmap entry.
Public `UnassignUnit` and the remaining leader/rest-group behavior require their
own closed contract before enabling any subset backend. The present feature
never calls Create/Assign/Unassign, never submits synthetic Select/Deselect
Chores and never calls `SetSelectedChimps` to borrow the owner's selection.

## Modal proxy input boundary

The testmod uses a Noesis modal proxy surface and permanent managed hooks on the
public instance method `FatControler.overNoesisGUI()` and the public
`FatControler.NoesisHasKeyboard` getter. Both wrappers call their original exactly
once, preserving existing bookkeeping, then report GUI ownership while the proxy
is open or its closing physical input is still held. These are logical flags;
panel toggles do not install, remove or modify executable hook code. The testmod
does not hook managed `EngineInterface.TroopSelection` or native `DLL_TroopSelection`.

Managed baseline `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`:
`EditorDirector.Update` copies the query result into `overNoesisUI`. New left-down
troop gesture starts and right-down/right-up producers require `!overNoesisUI`.
`preDLLCallActions` suppresses ordinary world hover/placement when that flag is
true and still calls the existing managed `TroopSelection` path normally. The
Script Extender's managed selection detour and APIShared's selection observer
therefore retain their owners and call chains.

The GUI query alone does not cancel an input gesture already staged before opening.
`Update` explicitly continues an existing `CurrentAction == 8` troop drag despite
the GUI flag; its release publishes action 9 and mouse state 3.
`preDLLCallActions` has an explicit action-9 exception. The runtime therefore admits
opening only with no held mouse buttons, `MainControls.CurrentAction == 0`,
`peekleftMouseStateForEngine() == 0` and no active/established TroopSelector drag.
It does not clear the owner's existing selection to establish that transition.

`KeyManager.Update` reads `NoesisHasKeyboard` before creating its normal key-state
edges: GUI keyboard ownership prevents a held physical key from becoming a fresh
Vanilla key-down. Closing retains both query guards until the main thread observes
`Input.anyKey == false`; a held Escape/hotkey cannot immediately leak a new native
key-down into the closed proxy's following frame. This covers Vanilla's ordinary
keyboard channel, not arbitrary direct commands from third-party mods. Synthetic
selection producers also remain a separate class: no claim is made to intercept
every other mod's public calls. Real game acceptance includes opening/closing
around held buttons, staged input, native selection drags and keyboard shortcuts.

## Update and acceptance checks

On a native/Extender update, recheck the public method contracts, the complete
Move/Stop branches and dependent member/path semantics, installed enum values,
unit/tribe layouts and existing hook/event ownership. Retain independent source
and installed-assembly identities. A changed native hash disables this backend
until those checks succeed; there is no guessed RVA or pattern fallback.

Offline behavior tests should cover exact-group admission, global-ID recycling,
membership/owner/death changes, proper subsets, invalid target coordinates,
duplicate requests and single terminal invocation on false/throwing results.
These tests model terminal boundaries; they do not execute the game's simulation.

Game acceptance must compare standalone dependencies with the normal mod set,
then real matching-build host/client peers: own and allied full groups, owner
selection changing while a request is pending, overlapping Move/Stop, AI issuing
a later order, stationary/special members, unreachable targets, Save/Load and
world replacement. Record identical accepted request/tribe/unit identities and
execution ticks on both peers, unchanged nonmembers and the next native checksum.
Proper-subset attempts must reject with an explicit reason and zero native work.

Evidence is in the current Native Baseline's `PLAYER_CONTROL_CONTEXT.md`,
`MOVE_COMMAND_RELEASE.md`, `MANUAL_GROUP_ADMISSION.md`,
`MANUAL_GROUP_REGION_RECOVERY.md`, `UNIT_COMMAND_PROBE_SCRATCH.md`,
`FORMATION_MOVEMENT_PERMISSION.md`, and the complete current-image exports for
the RVAs above. Later corrections in the admission dossier supersede historical
field-name/offset interpretations in older dossiers; do not copy a historical
terminal field offset into runtime code.
