# Move-command release contract

## Scope and provenance

This contract describes how one managed mouse release becomes one native troop
command. Addresses are RVAs for native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The managed side was checked against the canonical publicized
`Assembly-CSharp` baseline and Script Extender 2.8.0.

## Managed input handoff

`EditorDirector.getMouseStateForEngine` is the single consumer of the managed
release state used by `EditorDirector.preDLLCallActions`:

- The left-command scheme reports release as `leftMouseStateForEngine == 3`.
- The right-command scheme reports release as `rightUpForEngine == true`.
- Reading clears `rightUpForEngine`. Left state 3 normally advances to 0.
- If `upPending` is set, the read clears `upPending` and publishes left state 3
  for the following call instead. `stateRead` is part of this state machine.
- `clearMouseStateForEngine()` sets left state to 0, `stateRead` to true, and
  `upPending` to false; right-up must be cleared separately.

Temporarily hiding a release across `EngineInterface.run` therefore requires a
snapshot and exact restoration of left state, right-up, `stateRead`, and
`upPending`. Once a replacement command has been accepted, restoring any of
those release states can enqueue a second command and is invalid.

## Native flow

`DLL_TroopSelection` at RVA `0x879A0` stores `mouseState`, `rightDown`, and
`rightUp` in the native input globals. `DLL_RunTick` at RVA `0x86680` passes
them to RVA `0x8B7E0` and the command-decision path at RVA `0x8C5F0`, then
clears all three native globals before returning.

RVA `0x8C5F0` uses left state 3 or right-up according to the selected control
scheme. Its ground-move branch stages the selected group and target through
RVA `0x195E30`. The synchronized move then follows Chore 17 at RVA `0x10AE0`,
move staging at `0x196100`, group movement at `0x11B520`, a formation selector
or common-group path, and terminal unit movement at `0x196280`.

The release is an edge-triggered transaction. A mod that sends an equivalent
authoritative move before the original run must still call the original run
once with neutral release state, and must not restore that consumed edge.

## Preview target eligibility

The held-button preview precedes the terminal command decision, so it must not
infer a ground Move from the mouse button alone. `DLL_RunTick` supplies the
current map coordinates and object-depth inputs to RVA `0x8B7E0`; the cursor
target resolver at RVA `0x79B90` rejects out-of-map and unavailable movement
targets and publishes building, unit, wall, and ground identities. The command
dispatcher at RVA `0x8C5F0` handles object attacks and interactions before its
remaining ground branch reaches RVA `0x195E30`.

A managed preview may therefore start only from a coherent cursor snapshot for
the captured native tile with no hovered or tile-resident unit, building, or
wall, nonzero movement-target availability, and a nonzero path component. The
immediate managed `grabTroopsOnScreen` result is part of the same object-target
decision and must also be empty. If the managed tile, native cursor tile, or
native grids disagree, the classification is ambiguous and must fail closed to
Vanilla without consuming the release. While the cursor later supplies drag
direction, only the fixed command tile is revalidated; the live hover identity
must not be mistaken for a replacement command target.

### Native troop-command mode

Object-free ground is not sufficient to identify a movement command. The
managed `Troops_AttackHere` action (`1012`) enters `DLL_GameAction` at RVA
`0x81870`, whose case calls RVA `0x90510` with value `5`. For an ordinary
eligible troop selection, RVA `0x90510` writes `5` to the 32-bit current and
saved troop-command modes at RVAs `0x67E8410` and `0x67E8414`. Special
selections can instead enter modes `0x14` or `0x16`.

The dispatcher at RVA `0x8C5F0` treats current mode `1` as the ordinary
movement/object-command path and mode `5` as Attack Here. The mode-5 ground
branch may call move stager `0x195E30` as part of preparing the special order
before issuing the actual attack-tile command, so observing that stager is not
proof of a Move command. Patrol/attack-move variations remain in mode `1` and
carry their semantics through the separate move parameters.

A held-ground preview must therefore additionally require current native mode
`1` at gesture start, while held, and immediately before release handoff. Any
other or unreadable value fails closed to Vanilla without consuming the release
or publishing a move context. `MainControls.CurrentAction` is a distinct
managed placement/editor state and is not a substitute for this native mode.

## Managed event ordering

Script Extender 2.8.0 raises `OnKeyDown`, `OnKey`, and `OnKeyUp` from the
separate `KeyManager.keys` state machine after the original managed
`KeyManager.Update` has run. Those events and the `EditorDirector` release
state consumed by `preDLLCallActions` are therefore observations of different
state machines; their relative delivery order must not be assumed.

The native-facing `EditorDirector` release is authoritative. A drag controller
may use `OnKeyUp` to capture its final main-thread cursor state and remove its
preview immediately, but it must also accept the native release when no
`OnKeyUp` was observed and use the last held main-thread snapshot. Requiring
both signals can indefinitely defer the native release, leave preview markers
active, and misinterpret later mouse presses as controls for the stale drag.
After an observed input release, a new mouse-down must first discard any stale
unclaimed drag before it may start or modify another gesture.

An input event rejected in a Script Extender 2.8.0 Pre handler is not merely
hidden from the current subscriber: the extender clears the corresponding
`KeyManager.keys` entry. Code must therefore never reject an auxiliary mouse
down and then wait for its later held or release event. In particular, doing
so for the opposite primary button can suppress Vanilla deselection and leave
a permanent capture state. Auxiliary primary and middle clicks must remain
with Vanilla unless their complete down/held/up lifecycle is handled without
depending on the cleared `KeyManager` state.

## Confidence

- Managed state transitions: confirmed-static.
- `DLL_TroopSelection` and `DLL_RunTick` parameter/global flow:
  confirmed-static.
- `0x8C5F0` release branches and ground-move staging: confirmed-static.
- `0x79B90` cursor target rejection and unit/building/wall/ground publication:
  confirmed-static.
- Object-command dispatch preceding the `0x195E30` ground branch at `0x8C5F0`:
  confirmed-static.
- `Troops_AttackHere` mode setup at `0x90510`, 32-bit command-mode global RVA
  `0x67E8410`, and mode-1/mode-5 dispatch split at `0x8C5F0`:
  confirmed-static.
- Duplicate-order consequence from restoring the release: confirmed-runtime
  by a formation command completing all terminal assignments before a later
  Vanilla order replaced its targets.
- Independent Script Extender input-event and `EditorDirector` release state
  machines: confirmed-static against Script Extender 2.8.0 and the publicized
  managed game assembly.
- Missing `OnKeyUp` leaving a FormationTest drag active while the native
  release was repeatedly deferred: confirmed-runtime in the 2026-09-19 log.
- Rejected Pre events clearing the corresponding Script Extender 2.8.0
  `KeyManager.keys` entry: confirmed-static and confirmed-runtime by the lost
  auxiliary release and subsequent blocked Vanilla deselection.


## 2026-10-02: final native ground feedback and formation preview

Revalidated installed native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2
and real managed SHA-256 BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.
Feature audit covers RunTick 86680 → 8B7E0 → 8C5F0; representative selection
18D460 / route mode 18DC40; E7C40, E2610, DB650 and the E2CA0/E9D90/E9FF0
fallbacks; terminal cursor dispatch; renderer 41D60/436DE/41D10; command staging
195E30 → chore17/10AE0 → 196100 → 11B520 → 196280/F4930.

Ground reject 8F3DA writes detail -10 at 60AD560, image 41 at 60AD548,
file AC at 60AD54C and command 11 at 60AD55C. Final cursor dispatch table
90088 entry 3 goes to 90028: 9002D/90033 publishes kind 3 at 34A9E4C.
Approved ordinary ground starts file 6B/image 0, command 1/detail 0; applicable
special ground output uses file 6B/image 20, command 9/detail 0. Type-specific
positive fallback results remain authoritative. Object/wall contexts are excluded.
Terminal kind 3, mode 1 and coherent player/group/count/anchor are required;
unknown or rejected output never authorizes extra green formation markers.

Read-only RVAs: player 88E3D70, active tribe 7CC6720, selection count 67E8420,
mode 67E8410, cursor X/Y 3A11E2C/3A11E30, hovered unit 3A11DF0,
hovered building 3A11DE4, wall 3A11E34, cursor kind 34A9E4C,
file/image/command/detail 60AD54C/548/55C/560. Cursor offsets corroborated by
installed-layout GameCursorManager (unit +30, building +24, wall +74).
The new reader validates unchanged rejection, terminal-dispatch and mode bytes
before use; no executable memory is changed.

IMPORTANT: 41D60 calls 41D10 at the END, after visible tiles and cursor drawing.
It clears 34A9E4C immediately before that reset. Therefore authorization runs in
the first existing 436DE callback of each render pass, not in the reset callback;
reset only invalidates per-pass memoization. A confirmed gesture keeps its fixed
anchor when hover moves during spacing adjustment. EngineInterface.run and
existing input/map paths invalidate changed selection/gesture context. No native
search is added to marker rendering. 195E30 queues before checking feedback;
no additional command veto or artificial replacement order is introduced.

Cursor adapter 8F1BF uses immediate reference-first/filtered-last DB650 on demand.
DB650 scratch mutation is confined to this already audited non-search cursor
callsite; active direct/tactical scopes and reentry defer fail-open. Both scopes
restore via finally. Snapshot identity, epoch and generation guard memoization.
No new hook spans/owners, SE/Fixes conflicts or public APIShared interfaces.


Buildnachweis 02.10.2026: Beide betroffenen build.bat-Treiber abgeschlossen,
DLLs lokal/installiert SHA-256-identisch. Testmod: 1.699 Assertions einschließlich
beider installierten RedBird-Maschinentests, 0 Warnungen/Fehler. Hauptmod:
9.228 Formations-/Queue-Checks und vollständige Treiber-Regressionen bestanden;
0 Fehler, 1 MSB3277-Warnung für MonoMod.Utils-Referenzversionen. Der alte
Quelltexttest im Hauptmod-Nativeharness wurde auf die atomare Veröffentlichung
von Markerkacheln und Autorisierungsdelegate aktualisiert; der erneute komplette
Treiber bestand. JSON-/Lifecycle-/Hookmutations-/XAML-/CRLF-Vorprüfungen bestanden.
Testmod DLL: 19C3DAB31C0B5CEE3054E771CBEA98BB5A20F11FA00DA354EEA97322AC0056B6.
BugfixesAndQoL DLL: 4CD2567C4565FAA0E916561B40092A78A593DD897EDBC4117E6397BF00A36036.
APIShared unverändert; keine Versionserhöhung. Spielabnahme weiterhin offen.
