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
